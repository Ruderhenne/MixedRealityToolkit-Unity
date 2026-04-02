using MixedReality.Toolkit.UX;
using Photon.Pun;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class NetworkDashboardManager : MonoBehaviourPun
{
    [Header("UI References - Deine Elemente")]
    [SerializeField] TMP_Text counterText;
    [SerializeField] MixedReality.Toolkit.UX.Slider valueSlider;
    [SerializeField] TMP_Text sliderText;

    [Header("Persistenz")]
    private string savePath;

    // Zustand
    private int counterValue = 0;
    private float sliderValue = 0f;

    // Zeitstempel für Konfliktauflösung (PhotonNetwork.Time)
    private double sliderTimestamp = 0.0;

    // Verhindert Interaktion bis initialer Sync vom Master empfangen wurde
    private bool isInitializing = true;

    // Unterdrückt Callbacks, wenn Wert programmgesteuert gesetzt wird
    private bool suppressSliderCallback = false;

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "dashboard_state.json");
        // Standard: bis Sync vom Master ist IEnumerator/Flag aktiv
        isInitializing = true;
    }

    void Start()
    {
        if (string.IsNullOrEmpty(savePath))
            savePath = Path.Combine(Application.persistentDataPath, "dashboard_state.json");

        if (valueSlider == null)
            Debug.LogWarning("NetworkDashboardManager: valueSlider ist nicht gesetzt (Inspector).");

        if (valueSlider != null)
        {
            valueSlider.OnValueUpdated.AddListener(OnMrtkSliderValueChanged);
            Debug.Log("NetworkDashboardManager: Registered OnMrtkSliderValueChanged listener.");
        }

        // Master lädt lokale Persistenz und broadcastet; Clients fordern Zustand an
        if (PhotonNetwork.IsMasterClient)
        {
            LoadDashboardState();
        }
        else
        {
            // Fordere Zustand beim Master an; Master antwortet per RPC_UpdateCounter/Slider
            photonView.RPC("RPC_RequestState", RpcTarget.MasterClient);
        }

        // Fallback: falls Master aus irgendeinem Grund nicht antwortet, beende Initialisierung nach kurzer Zeit
        StartCoroutine(EndInitializationTimeout());
    }

    System.Collections.IEnumerator EndInitializationTimeout()
    {
        yield return new WaitForSeconds(1.0f);
        if (isInitializing)
        {
            Debug.LogWarning("NetworkDashboardManager: Initialization timeout ended without master sync; enabling local interaction.");
            isInitializing = false;
        }
    }

    // MRTK Slider Event-Handler
    public void OnMrtkSliderValueChanged(SliderEventData eventData)
    {
        float value = eventData.NewValue;
        OnValueSliderChanged(value);
    }

    // COUNTER METHODEN (IncrementButton, ResetButton)
    public void IncrementCounter()
    {
        if (isInitializing)
        {
            Debug.Log("IncrementCounter ignored during initialization (awaiting master sync).");
            return;
        }

        counterValue++;
        UpdateCounterDisplay();

        // Sende Änderung an alle (inkl. Master) — Master ist verantwortlich für Persistenz
        photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
    }

    public void ResetCounter()
    {
        if (isInitializing)
        {
            Debug.Log("ResetCounter ignored during initialization (awaiting master sync).");
            return;
        }

        counterValue = 0;
        UpdateCounterDisplay();
        photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
    }

    // SLIDER METHODE (ValueSlider) — alle Clients dürfen ändern
    public void OnValueSliderChanged(float value)
    {
        //Debug.Log($"OnValueSliderChanged called value={value} suppress={suppressSliderCallback} init={isInitializing}");

        if (isInitializing || suppressSliderCallback)
        {
            //Debug.Log($"OnValueSliderChanged ignored (init/suppress): {value}");
            return;
        }

        if (Mathf.Approximately(value, sliderValue))
        {
            //Debug.Log($"OnValueSliderChanged no-op (same value): {value}");
            return;
        }

        sliderValue = value;
        sliderTimestamp = PhotonNetwork.Time;

        if (sliderText != null)
            sliderText.text = value.ToString("F1");

        Debug.Log($"OnValueSliderChanged -> sending value={value} ts={sliderTimestamp}");

        // Sende an andere Clients; Master erhält ebenfalls und speichert
        photonView.RPC("RPC_UpdateSlider", RpcTarget.Others, sliderTimestamp, value);
    }

    // Schliessen-Knopf (EmergencyStop)
    public void OnEmergencyStopPressed()
    {
        Debug.Log("Emergency stop pressed. Exiting application.");

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // RPCs (Netzwerk-Sync)
    [PunRPC]
    void RPC_RequestState()
    {
        // Wird auf Master aufgerufen; dieser broadcastet aktuellen Zustand
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("RPC_RequestState received on Master; broadcasting state.");
        sliderTimestamp = PhotonNetwork.Time;
        photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
        photonView.RPC("RPC_UpdateSlider", RpcTarget.All, sliderTimestamp, sliderValue);
    }

    [PunRPC]
    void RPC_UpdateCounter(int value)
    {
        Debug.Log($"RPC_UpdateCounter received value={value} (was {counterValue})");
        counterValue = value;
        UpdateCounterDisplay();

        // Nach erstem Counter-RPC ist der Client initialisiert
        isInitializing = false;

        // Master speichert persistent, wenn er die Änderung empfangen hat (Master ist authoritative)
        if (PhotonNetwork.IsMasterClient)
            SaveDashboardState();
    }

    [PunRPC]
    void RPC_UpdateSlider(double timestamp, float value)
    {
        Debug.Log($"RPC_UpdateSlider received ts={timestamp} value={value} localTs={sliderTimestamp} isInit={isInitializing}");

        if (timestamp < sliderTimestamp)
        {
            Debug.Log("RPC_UpdateSlider ignored: older timestamp.");
            return;
        }

        sliderTimestamp = timestamp;

        if (Mathf.Approximately(value, sliderValue) && !isInitializing)
        {
            Debug.Log("RPC_UpdateSlider no-op (same value).");
            isInitializing = false;
            return;
        }

        sliderValue = value;

        suppressSliderCallback = true;
        if (valueSlider != null)
            valueSlider.Value = value;

        if (sliderText != null)
            sliderText.text = value.ToString("F1");

        StartCoroutine(ResetSuppress());

        // Master speichert persistent
        if (PhotonNetwork.IsMasterClient)
            SaveDashboardState();

        isInitializing = false;
    }

    System.Collections.IEnumerator ResetSuppress()
    {
        yield return null;
        suppressSliderCallback = false;
    }

    /// <summary>
    /// Wird vom Master via QRTracker gesendet, wenn das Dashboard
    /// auf den QR-Code zentriert wurde. Setzt Position und Rotation
    /// auf allen Clients direkt, ohne dass der PhotonTransformView
    /// stört.
    /// </summary>
    [PunRPC]
    void RPC_SetDashboardTransform(float px, float py, float pz,
                                   float rx, float ry, float rz, float rw)
    {
        var pos = new Vector3(px, py, pz);
        var rot = new Quaternion(rx, ry, rz, rw);

        // PhotonTransformView kurz deaktivieren
        var transformView = GetComponent<PhotonTransformView>();
        var transformViewClassic = GetComponent<PhotonTransformViewClassic>();

        if (transformView != null)        transformView.enabled = false;
        if (transformViewClassic != null) transformViewClassic.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        Debug.Log($"[NetworkDashboardManager] RPC_SetDashboardTransform: pos={pos} rot={rot.eulerAngles}");

        // Wieder aktivieren – neue Position ist jetzt die Sync-Basis
        if (transformView != null)        transformView.enabled = true;
        if (transformViewClassic != null) transformViewClassic.enabled = true;
    }

    //  UI UPDATES
    void UpdateCounterDisplay() => counterText.text = counterValue.ToString();

    // PERSISTENZ
    void SaveDashboardState()
    {
        if (!PhotonNetwork.IsMasterClient) return; // Nur Master darf persistent speichern

        var legacy = new LegacyDashboardState { counterValue = counterValue, sliderValue = sliderValue };
        File.WriteAllText(savePath, JsonUtility.ToJson(legacy));
        Debug.Log($"Dashboard saved: Counter={counterValue}, Slider={sliderValue}");
    }

    void LoadDashboardState()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);

            if (json.Contains("counterValue") || json.Contains("sliderValue"))
            {
                var legacy = JsonUtility.FromJson<LegacyDashboardState>(json);
                counterValue = legacy.counterValue;
                sliderValue = legacy.sliderValue;
            }
            else
            {
                var state = JsonUtility.FromJson<DashboardState>(json);
                counterValue = state != null ? state.counter : 0;
                sliderValue = state != null ? state.slider : 0f;
            }

            // Broadcast des geladenen Zustands (Master macht das)
            sliderTimestamp = PhotonNetwork.Time;
            photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
            photonView.RPC("RPC_UpdateSlider", RpcTarget.All, sliderTimestamp, sliderValue);
            Debug.Log($"Dashboard loaded: Counter={counterValue}, Slider={sliderValue} ts={sliderTimestamp}");
        }
    }

    [System.Serializable]
    private class DashboardState
    {
        public int counter;
        public float slider;
    }

    [System.Serializable]
    private class LegacyDashboardState
    {
        public int counterValue;
        public float sliderValue;
    }
}
