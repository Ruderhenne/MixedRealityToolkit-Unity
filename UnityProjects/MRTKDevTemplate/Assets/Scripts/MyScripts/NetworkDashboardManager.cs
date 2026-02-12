using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using MixedReality.Toolkit.UX;    
using System.IO;

// Fix: Ersetze Microsoft.MixedReality.Toolkit.UI.Slider durch UnityEngine.UI.Slider
public class NetworkDashboardManager : MonoBehaviourPun
{
    [Header("UI References - Deine Elemente")]
    [SerializeField] TMP_Text counterText;
    [SerializeField] MixedReality.Toolkit.UX.Slider valueSlider;
    [SerializeField] TMP_Text sliderText;

    [Header("Persistenz")]
    private string savePath = "";

    // Zustand
    private int counterValue = 0;
    private float sliderValue = 0f;

    void Start()
    {
        savePath = Path.Combine(Application.persistentDataPath, "dashboard_state.json");

        if (PhotonNetwork.IsMasterClient)
        {
            LoadDashboardState();
        }
    }

    // COUNTER METHODEN (IncrementButton, ResetButton)
    public void IncrementCounter()
    {
        //if (!photonView.IsMine) return;
        counterValue++;
        UpdateCounterDisplay();
        photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
        if (PhotonNetwork.IsMasterClient) SaveDashboardState();
    }

    public void ResetCounter()
    {
        //if (!photonView.IsMine) return;
        counterValue = 0;
        UpdateCounterDisplay();
        photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
        if (PhotonNetwork.IsMasterClient) SaveDashboardState();
    }

    // SLIDER METHODE (ValueSlider)
    public void OnValueSliderChanged(float value)
    {
        //if (!photonView.IsMine) return;
        sliderValue = value;
        sliderText.text = value.ToString("F1");
        photonView.RPC("RPC_UpdateSlider", RpcTarget.All, value);
        if (PhotonNetwork.IsMasterClient) SaveDashboardState();
    }

    // RPCs (Netzwerk-Sync)
    [PunRPC]
    void RPC_UpdateCounter(int value)
    {
        counterValue = value;
        UpdateCounterDisplay();
    }

    [PunRPC]
    void RPC_UpdateSlider(float value)
    {
        sliderValue = value;
        if (valueSlider != null)
        {
            valueSlider.Value = value;
        }
        if (sliderText != null)
        {
            sliderText.text = value.ToString("F1");
        }
    }

    //  UI UPDATES
    void UpdateCounterDisplay() => counterText.text = counterValue.ToString();

    // PERSISTENZ (Phase 2 - JSON)
    void SaveDashboardState()
    {
        var state = new DashboardState { counter = counterValue, slider = sliderValue };
        File.WriteAllText(savePath, JsonUtility.ToJson(state));
        Debug.Log($"Dashboard saved: Counter={counterValue}, Slider={sliderValue}");
    }

    void LoadDashboardState()
    {
        if (File.Exists(savePath))
        {
            var state = JsonUtility.FromJson<DashboardState>(File.ReadAllText(savePath));
            counterValue = state.counter;
            sliderValue = state.slider;

            photonView.RPC("RPC_UpdateCounter", RpcTarget.All, counterValue);
            photonView.RPC("RPC_UpdateSlider", RpcTarget.All, sliderValue);
            Debug.Log($"Dashboard loaded: Counter={counterValue}, Slider={sliderValue}");
        }
    }

    [System.Serializable]
    private class DashboardState
    {
        public int counter;
        public float slider;
    }
}
