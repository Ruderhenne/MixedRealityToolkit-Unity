using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Leitet den Button-Click an den QRTracker in der Szene weiter.
/// Kann direkt am CenterButton im Prefab hängen.
/// </summary>
public class QRScanButton : MonoBehaviour
{
    public void OnCenterButtonClicked()
    {
        var tracker = FindObjectOfType<QRTracker>();
        if (tracker != null)
        {
            tracker.StartScanning();
        }
        else
        {
            Debug.LogWarning("[QRScanButton] Kein QRTracker in der Szene gefunden!");
        }
    }
}
