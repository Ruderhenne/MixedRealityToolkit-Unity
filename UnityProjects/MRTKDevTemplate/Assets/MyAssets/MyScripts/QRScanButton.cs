using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Leitet den Button-Click an den QRTracker in der Szene weiter.
/// Kann direkt am CenterButton im Prefab hängen.
/// </summary>
public class QRScanButton : MonoBehaviour
{
    [Tooltip("Das Dashboard-Root-Objekt, das auf den QR-Code zentriert werden soll.")]
    [SerializeField] private GameObject dashboardRoot;

    [Tooltip("StatusText für Scan-Feedback (optional).")]
    [SerializeField] private TMPro.TMP_Text statusText;

    public void OnCenterButtonClicked()
    {
        var tracker = FindObjectOfType<QRTracker>();
        if (tracker != null)
        {
            // Referenzen übergeben – genau wie es der alte Prefab-QRTracker tat
            tracker.dashboardRoot = this.dashboardRoot;
            if (this.statusText != null)
                tracker.statusText = this.statusText;

            tracker.StartScanning();
        }
        else
        {
            Debug.LogWarning("[QRScanButton] Kein QRTracker in der Szene gefunden!");
        }
    }
}
