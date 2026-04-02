using MixedReality.Toolkit.UX;
using Photon.Pun;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

    // Tracking, ob der Slider gerade aktiv manipuliert wird
    private bool isSliderGrabbed = false;
    private int sliderGrabUserNumber = -1;
    private float sliderValueBeforeGrab = 0f;

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "dashboard_state.json");
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
            valueSlider.lastSelectExited.AddListener(OnSliderReleased);
            valueSlider.firstSelectEntered.AddListener(OnSliderGrabbed);

            Debug.Log("NetworkDashboardManager: Registered OnMrtkSliderValueChanged + Select listeners.");
        }

        if (PhotonNetwork.IsMasterClient)
        {
            LoadDashboardState();
        }
        else
        {
            photonView.RPC("RPC_RequestState", RpcTarget.MasterClient);
        }

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

    private int GetLocalUserNumber()
    {
        return PhotonNetwork.LocalPlayer.ActorNumber;
    }

    // Slider Grab/Release Callbacks
    private void OnSliderGrabbed(SelectEnterEventArgs args)
    {
        isSliderGrabbed = true;
        sliderGrabUserNumber = GetLocalUserNumber();
        sliderValueBeforeGrab = sliderValue;
    }

    private void OnSliderReleased(SelectExitEventArgs args)
    {
        if (isSliderGrabbed)
        {
            isSliderGrabbed = false;

            int fromVal = Mathf.RoundToInt(sliderValueBeforeGrab);
            int toVal = Mathf.RoundToInt(sliderValue);

            photonView.RPC("RPC_LogStatus", RpcTarget.All,
                $"User {sliderGrabUserNumber}: Slider changed from {fromVal} to {toVal}");
        }
    }

    // MRTK Slider Event-Handler
    public void OnMrtkSliderValueChanged(SliderEventData eventData)
    {
        float value = eventData.NewValue;
        OnValueSliderChanged(value);
    }

    // COUNTER METHODEN
    public void IncrementCounter()
    {
        if (isInitializing)
        {
            Debug.Log("IncrementCounter ignored during initialization (awaiting master sync).");
            return;
        }

        counterValue++;
        UpdateCounterDisplay();

        photonView.RPC("RPC_LogStatus", RpcTarget.All,
            $"User {GetLocalUserNumber()}: Increment button pressed");

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

        photonView.RPC("RPC_LogStatus", RpcTarget.All,
            $"User {GetLocalUserNumber()}: Reset button pressed");

        photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
    }

    // SLIDER METHODE
    public void OnValueSliderChanged(float value)
    {
        if (isInitializing || suppressSliderCallback)
            return;

        if (Mathf.Approximately(value, sliderValue))
            return;

        sliderValue = value;
        sliderTimestamp = PhotonNetwork.Time;

        if (sliderText != null)
            sliderText.text = Mathf.RoundToInt(value).ToString();

        Debug.Log($"OnValueSliderChanged -> sending value={value} ts={sliderTimestamp}");

        photonView.RPC("RPC_UpdateSlider", RpcTarget.Others, sliderTimestamp, value);
    }

    // EmergencyStop
    public void OnEmergencyStopPressed()
    {
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC("RPC_LogStatus", RpcTarget.All,
                $"User {GetLocalUserNumber()}: Emergency stop pressed");
        }

        Debug.Log("Emergency stop pressed. Exiting application.");

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // RPCs
    [PunRPC]
    void RPC_RequestState()
    {
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

        isInitializing = false;

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
            sliderText.text = Mathf.RoundToInt(value).ToString();

        StartCoroutine(ResetSuppress());

        if (PhotonNetwork.IsMasterClient)
            SaveDashboardState();

        isInitializing = false;
    }

    [PunRPC]
    void RPC_LogStatus(string message)
    {
        StatusLogger.Log(message);
    }

    System.Collections.IEnumerator ResetSuppress()
    {
        yield return null;
        suppressSliderCallback = false;
    }

    [PunRPC]
    void RPC_SetDashboardTransform(float px, float py, float pz,
                                   float rx, float ry, float rz, float rw)
    {
        var pos = new Vector3(px, py, pz);
        var rot = new Quaternion(rx, ry, rz, rw);

        var transformView = GetComponent<PhotonTransformView>();
        var transformViewClassic = GetComponent<PhotonTransformViewClassic>();

        if (transformView != null)        transformView.enabled = false;
        if (transformViewClassic != null) transformViewClassic.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        Debug.Log($"[NetworkDashboardManager] RPC_SetDashboardTransform: pos={pos} rot={rot.eulerAngles}");

        if (transformView != null)        transformView.enabled = true;
        if (transformViewClassic != null) transformViewClassic.enabled = true;
    }

    void UpdateCounterDisplay() => counterText.text = counterValue.ToString();

    void SaveDashboardState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

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
