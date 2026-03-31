using System.Collections;
using UnityEngine;

public class StatusUI : MonoBehaviour
{
    [SerializeField] private QRTracker qrTracker;
    [SerializeField] private string statusChildName = "statusText";

    private TMPro.TMP_Text text;

    IEnumerator Start()
    {
        // Sofortversuch: auf dem gleichen GameObject oder in Children (inkl. deaktivierter)
        text = GetComponent<TMPro.TMP_Text>() ?? GetComponentInChildren<TMPro.TMP_Text>(true);

        // Falls ein spezieller Child‑Name bekannt ist, kurz prüfen
        if (text == null && !string.IsNullOrEmpty(statusChildName))
        {
            var t = transform.Find(statusChildName);
            if (t != null) text = t.GetComponent<TMPro.TMP_Text>();
        }

        // Kurzes Warten (z. B. falls Prefab noch parented/aktiviert wird)
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
            Debug.LogWarning($"StatusUI: Kein TMP_Text auf oder unter '{name}' gefunden. Prüfe Hierarchie und Komponententyp (TMP vs TMPUGUI).");
            yield break;
        }

        // QRTracker zur Laufzeit finden, falls nicht per Inspector gesetzt
        if (qrTracker == null)
            qrTracker = FindObjectOfType<QRTracker>();

        if (qrTracker != null)
        {
            // Explizite Umwandlung von TMP_Text zu TextMeshPro
            if (text is TMPro.TextMeshPro tmp)
            {
                qrTracker.statusText = tmp;
            }
            else
            {
                Debug.LogWarning("StatusUI: Das gefundene TMP_Text ist kein TextMeshPro. Status-Updates werden nicht übermittelt.");
            }
        }
        else
        {
            Debug.LogWarning("StatusUI: Kein QRTracker in der Szene gefunden. Status-Updates werden nicht übermittelt.");
        }

        text.text = "Bereit";
    }
}
