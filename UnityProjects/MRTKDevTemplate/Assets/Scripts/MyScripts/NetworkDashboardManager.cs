using System;
using System.Collections;
using System.IO;
using MixedReality.Toolkit.UX;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class NetworkDashboardManager : MonoBehaviourPun
{
    [Header("UI References")]
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private MixedReality.Toolkit.UX.Slider valueSlider;
    [SerializeField] private TMP_Text sliderText;

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

    // Persistenz
    private string savePath;

    #region Lifecycle

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "dashboard_state.json");
        isInitializing = true;
    }

    void Start()
    {
        if (valueSlider != null)
        {
            valueSlider.OnValueUpdated.AddListener((eventData) => OnMrtkSliderValueChanged(eventData));
            valueSlider.lastSelectExited.AddListener(OnSliderReleased);
            valueSlider.firstSelectEntered.AddListener(OnSliderGrabbed);
        }
        else
        {
            Debug.LogWarning("[NetworkDashboardManager] valueSlider ist nicht gesetzt (Inspector).");
        }

        if (PhotonNetwork.IsMasterClient)
        {
            LoadAndBroadcastState();
        }
        else
        {
            photonView.RPC("RPC_RequestState", RpcTarget.MasterClient);
        }

        StartCoroutine(EndInitializationTimeout());
    }

    private IEnumerator EndInitializationTimeout()
    {
        yield return new WaitForSeconds(1.0f);
        if (isInitializing)
        {
            Debug.LogWarning("[NetworkDashboardManager] Initialization timeout – enabling local interaction.");
            isInitializing = false;
        }
    }

    #endregion

    #region UI Callbacks

    private void OnSliderGrabbed(SelectEnterEventArgs args)
    {
        isSliderGrabbed = true;
        sliderGrabUserNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        sliderValueBeforeGrab = sliderValue;
    }

    private void OnSliderReleased(SelectExitEventArgs args)
    {
        if (!isSliderGrabbed) return;

        isSliderGrabbed = false;
        int fromVal = Mathf.RoundToInt(sliderValueBeforeGrab);
        int toVal = Mathf.RoundToInt(sliderValue);

        photonView.RPC("RPC_LogStatus", RpcTarget.All,
            $"User {sliderGrabUserNumber}: Slider changed from {fromVal} to {toVal}");
    }

    public void OnMrtkSliderValueChanged(SliderEventData eventData)
    {
        OnValueSliderChanged(eventData.NewValue);
    }

    public void IncrementCounter()
    {
        if (isInitializing) return;

        counterValue++;
        UpdateCounterDisplay();

        photonView.RPC("RPC_LogStatus", RpcTarget.All,
            $"User {PhotonNetwork.LocalPlayer.ActorNumber}: Increment button pressed");
        photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
    }

    public void ResetCounter()
    {
        if (isInitializing) return;

        counterValue = 0;
        UpdateCounterDisplay();

        photonView.RPC("RPC_LogStatus", RpcTarget.All,
            $"User {PhotonNetwork.LocalPlayer.ActorNumber}: Reset button pressed");
        photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
    }

    public void OnValueSliderChanged(float value)
    {
        if (isInitializing || suppressSliderCallback) return;
        if (Mathf.Approximately(value, sliderValue)) return;

        sliderValue = value;
        sliderTimestamp = PhotonNetwork.Time;
        UpdateSliderDisplay();

        photonView.RPC("RPC_UpdateSlider", RpcTarget.Others, sliderTimestamp, value);
    }

    public void OnEmergencyStopPressed()
    {
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC("RPC_LogStatus", RpcTarget.All,
                $"User {PhotonNetwork.LocalPlayer.ActorNumber}: Emergency stop pressed");
        }

        Debug.Log("[NetworkDashboardManager] Emergency stop pressed. Exiting application.");

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region RPCs

    [PunRPC]
    private void RPC_RequestState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("[NetworkDashboardManager] RPC_RequestState – broadcasting state.");
        sliderTimestamp = PhotonNetwork.Time;
        photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
        photonView.RPC("RPC_UpdateSlider", RpcTarget.All, sliderTimestamp, sliderValue);
    }

    [PunRPC]
    private void RPC_UpdateCounter(int value)
    {
        counterValue = value;
        UpdateCounterDisplay();
        isInitializing = false;

        if (PhotonNetwork.IsMasterClient)
            SaveState();
    }

    [PunRPC]
    private void RPC_UpdateSlider(double timestamp, float value)
    {
        if (timestamp < sliderTimestamp) return;

        sliderTimestamp = timestamp;

        if (Mathf.Approximately(value, sliderValue) && !isInitializing)
        {
            isInitializing = false;
            return;
        }

        sliderValue = value;

        suppressSliderCallback = true;
        if (valueSlider != null)
            valueSlider.Value = value;

        UpdateSliderDisplay();
        StartCoroutine(ResetSuppress());

        if (PhotonNetwork.IsMasterClient)
            SaveState();

        isInitializing = false;
    }

    [PunRPC]
    private void RPC_LogStatus(string message)
    {
        StatusLogger.Log(message);
    }

    [PunRPC]
    private void RPC_SetDashboardTransform(float px, float py, float pz,
                                           float rx, float ry, float rz, float rw)
    {
        var pos = new Vector3(px, py, pz);
        var rot = new Quaternion(rx, ry, rz, rw);

        var transformView = GetComponent<PhotonTransformView>();
        var transformViewClassic = GetComponent<PhotonTransformViewClassic>();

        if (transformView != null)        transformView.enabled = false;
        if (transformViewClassic != null) transformViewClassic.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        if (transformView != null)        transformView.enabled = true;
        if (transformViewClassic != null) transformViewClassic.enabled = true;
    }

    private IEnumerator ResetSuppress()
    {
        yield return null;
        suppressSliderCallback = false;
    }

    #endregion

    #region UI Helpers

    private void UpdateCounterDisplay()
    {
        if (counterText != null)
            counterText.text = counterValue.ToString();
    }

    private void UpdateSliderDisplay()
    {
        if (sliderText != null)
            sliderText.text = Mathf.RoundToInt(sliderValue).ToString();
    }

    #endregion

    #region Persistenz

    private void SaveState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        try
        {
            var state = new DashboardSaveState { counterValue = counterValue, sliderValue = sliderValue };
            File.WriteAllText(savePath, JsonUtility.ToJson(state));
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkDashboardManager] Save failed: {e.Message}");
        }
    }

    private void LoadAndBroadcastState()
    {
        if (!File.Exists(savePath)) return;

        try
        {
            string json = File.ReadAllText(savePath);
            var state = JsonUtility.FromJson<DashboardSaveState>(json);

            if (state != null)
            {
                counterValue = state.counterValue;
                sliderValue = state.sliderValue;
            }

            sliderTimestamp = PhotonNetwork.Time;
            photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
            photonView.RPC("RPC_UpdateSlider", RpcTarget.All, sliderTimestamp, sliderValue);

            Debug.Log($"[NetworkDashboardManager] Loaded: Counter={counterValue}, Slider={sliderValue}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkDashboardManager] Load failed: {e.Message}");
        }
    }

    /// <summary>
    /// Einheitliches Speicherformat.
    /// </summary>
    [Serializable]
    private class DashboardSaveState
    {
        public int counterValue;
        public float sliderValue;
    }

    #endregion
}
