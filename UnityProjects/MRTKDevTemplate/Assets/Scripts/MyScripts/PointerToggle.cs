using UnityEngine;

/// <summary>
/// Steuert, ob der lokale Spieler die Pointer anderer Spieler sieht.
/// Der eigene Pointer wird immer gesendet.
///
/// ToggleReceive() → "On Clicked ()"-Event des RayButtons binden.
/// </summary>
public class PointerToggle : MonoBehaviour
{
    private SharedPointerManager _pointerManager;
    private bool _receiveEnabled = false;

    private void Start()
    {
        _pointerManager = FindObjectOfType<SharedPointerManager>();

        if (_pointerManager == null)
            Debug.LogWarning("[PointerToggle] SharedPointerManager nicht in der Szene gefunden.");

        // Standardmäßig deaktiviert
        if (_pointerManager != null)
            _pointerManager.showRemotePointers = false;

        // Kamera-Clear sicherstellen, um Motion-Trail-Artefakte zu verhindern
        if (Camera.main != null && Camera.main.clearFlags != CameraClearFlags.SolidColor)
        {
            Camera.main.clearFlags      = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.black;
            Debug.Log("[PointerToggle] Camera.main ClearFlags auf SolidColor gesetzt.");
        }
    }

    /// <summary>
    /// Schaltet das Anzeigen der Pointer anderer Spieler ein/aus.
    /// An den "On Clicked ()"-Event des RayButtons binden.
    /// </summary>
    public void ToggleReceive()
    {
        _receiveEnabled = !_receiveEnabled;

        if (_pointerManager != null)
            _pointerManager.showRemotePointers = _receiveEnabled;

        Debug.Log($"[PointerToggle] Empfangen {(_receiveEnabled ? "aktiviert" : "deaktiviert")}.");
    }

    public bool ReceiveEnabled => _receiveEnabled;
}
