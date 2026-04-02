using System.Collections;
using UnityEngine;

public class StatusUI : MonoBehaviour
{
    [SerializeField] private QRTracker qrTracker;
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

        if (qrTracker == null)
            qrTracker = FindObjectOfType<QRTracker>();

        if (qrTracker != null)
        {
            if (text is TMPro.TextMeshPro tmp)
            {
                qrTracker.statusText = tmp;
            }
            else
            {
                Debug.LogWarning("StatusUI: Found TMP_Text is not TextMeshPro. Status updates will not be forwarded.");
            }
        }
        else
        {
            Debug.LogWarning("StatusUI: No QRTracker found in scene. Status updates will not be forwarded.");
        }

        if (StatusLogger.Instance != null)
        {
            StatusLogger.Instance.SetStatusText(text);
        }

        text.text = "Dashboard ready";
    }
}
