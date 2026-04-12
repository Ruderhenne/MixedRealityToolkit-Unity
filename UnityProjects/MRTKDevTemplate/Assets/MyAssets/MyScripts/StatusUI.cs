using UnityEngine;
using System.Collections;

public class StatusUI : MonoBehaviour
{
    [SerializeField] private string statusChildName = "statusText";

    private TMPro.TMP_Text text;

    IEnumerator Start()
    {
        text = GetComponent<TMPro.TMP_Text>() ?? GetComponentInChildren<TMPro.TMP_Text>(true);

        if (text == null && !string.IsNullOrEmpty(statusChildName))
        {
            var t = transform.Find(statusChildName);
            if (t != null) text = t.GetComponent<TMPro.TMP_Text>();
        }

        float timeout = 3f;
        float timer = 0f;
        while (text == null && timer < timeout)
        {
            yield return null;
            timer += Time.deltaTime;
            text = GetComponentInChildren<TMPro.TMP_Text>(true);
        }

        if (text == null)
        {
            Debug.LogWarning($"StatusUI: No TMP_Text found on or under '{name}'. Check hierarchy and component type (TMP vs TMPUGUI).");
            yield break;
        }

        // Nur noch den zentralen StatusLogger verdrahten
        if (StatusLogger.Instance != null)
        {
            StatusLogger.Instance.SetStatusText(text);
        }

        text.text = "Dashboard ready";
    }
}
