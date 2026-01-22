using System;
using System.IO;
using UnityEngine;
using TMPro; // Für TextMeshPro
using UnityEngine.Events;
using MixedReality.Toolkit.UX;


#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class DashboardState
{
    public int counterValue = 0;
    public float sliderValue = 0f; // 0–100
}

public class DashboardManager : MonoBehaviour
{


    [Header("UI References - Left")]
    [SerializeField] private TextMeshPro counterText;

    [Header("UI References - Right")]
    [SerializeField] private TextMeshPro sliderText;
    //[SerializeField] private UnityEngine.UI.Slider valueSlider;

    [Header("Emergency")]
    // Kein Button-Feld mehr hier

    private DashboardState state = new DashboardState();
    private string saveFilePath;

    private void Awake()
    {
        saveFilePath = Path.Combine(Application.persistentDataPath, "dashboard_state.json");
        LoadState();
        ApplyStateToUI();
    }


    private void OnDestroy()
    {
    }

    #region Event wiring



    #endregion

    #region UI Callbacks

    public void OnIncrementButtonPressed()
    {
        state.counterValue++;
        UpdateCounterUI();
        SaveState();
    }

    public void OnResetButtonPressed()
    {
        //Debug.Log("Reset button pressed");
        state.counterValue = 0;
        UpdateCounterUI();
        SaveState();
    }

    public void OnSliderValueChanged(float value)
    {
        Debug.Log($"Slider changed: {value}");
        state.sliderValue = value;
        UpdateSliderUI();
        SaveState();
    }

    public void OnMrtkSliderValueChanged(SliderEventData eventData)
    {
        // MRTK-Slider liefert den Wert zwischen Min/Max
        float value = eventData.NewValue;
        OnSliderValueChanged(value);
    }


    public void OnEmergencyStopPressed()
    {
        Debug.Log("Emergency stop pressed. Exiting application.");

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region UI helpers

    //private void ApplyStateToUI()
    //{
    //    UpdateCounterUI();
    //    UpdateSliderUI();
    //}

    private void ApplyStateToUI()
    {
        //Debug.Log($"ApplyStateToUI, counter={state.counterValue}, slider={state.sliderValue}");

        UpdateCounterUI();
        UpdateSliderUI();

        // Versuche, einen Slider-Component im DashboardPanel zu finden
        var mrtkSlider = GetComponentInChildren<Slider>();
        if (mrtkSlider != null)
        {
            mrtkSlider.Value = state.sliderValue;
        }
        else
        {
            Debug.LogWarning("No MRTK Slider component found as child of DashboardPanel.");
        }
    }




    private void UpdateCounterUI()
    {
        if (counterText != null)
        {
            counterText.text = $"Counter: {state.counterValue}";
        }
    }

    private void UpdateSliderUI()
    {
        if (sliderText != null)
        {
            sliderText.text = $"Slider: {Mathf.RoundToInt(state.sliderValue)}";
        }
    }

    #endregion

    #region Persistence

    private void SaveState()
    {
        try
        {
            string json = JsonUtility.ToJson(state, true);
            File.WriteAllText(saveFilePath, json);
            Debug.Log($"Dashboard state saved to {saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save dashboard state: {e.Message}");
        }
    }

    private void LoadState()
    {
        try
        {
            if (File.Exists(saveFilePath))
            {
                string json = File.ReadAllText(saveFilePath);
                state = JsonUtility.FromJson<DashboardState>(json);
                if (state == null)
                {
                    state = new DashboardState();
                }
                Debug.Log($"Dashboard state loaded from {saveFilePath}");
            }
            else
            {
                state = new DashboardState();
                Debug.Log("No dashboard state file found. Using defaults.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load dashboard state: {e.Message}");
            state = new DashboardState();
        }
    }

    #endregion
}
