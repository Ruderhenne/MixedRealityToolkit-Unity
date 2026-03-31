using System;
using UnityEngine;
using Microsoft.MixedReality.OpenXR;  // ← Wichtig!

public class QRTracker : MonoBehaviour
{
    [Header("Zuweisen im Inspector")]
    [SerializeField] private GameObject dashboardRoot;    // Drag Dashboard-Prefab
    [SerializeField] private GameObject markerAnchor;     // Leeres GameObject als Child
    [SerializeField] private string expectedQRData = "CenterMarker-MoshadXR";
    [SerializeField] public TMPro.TextMeshPro statusText;

    private ARMarkerManager markerManager;

    void Awake()
    {
        markerManager = GetComponent<ARMarkerManager>();
        if (markerManager == null)
        {
            markerManager = FindObjectOfType<ARMarkerManager>();
        }
        if (markerManager == null)
        {
            Debug.LogError("Kein ARMarkerManager gefunden!");
            return;
        }
        markerManager.markersChanged += OnMarkersChanged;
        Debug.Log("QRTracker gestartet!");
    }

    public void StartScanning()
    {
        Debug.Log("QR-Scan gestartet! Halte QR-Code vor Kamera.");

        // VISUELLES FEEDBACK
        if (statusText != null)
            statusText.text = "🔍 Suche QR-Code...";
    }

    private void OnMarkersChanged(ARMarkersChangedEventArgs args)
    {
        // Neue Marker prüfen
        foreach (var addedMarker in args.added)
        {
            if (addedMarker.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
            {
                // QR-Code-Daten auslesen
                try
                {
                    var qrProps = markerManager.GetQRCodeProperties(addedMarker.trackableId);
                    string qrData = markerManager.GetDecodedString(addedMarker.trackableId);

                    Debug.Log($"QR-Code erkannt: '{qrData}' (Version: {qrProps.version})");

                    if (qrData == expectedQRData)
                    {
                        // Position übernehmen
                        markerAnchor.transform.position = addedMarker.transform.position;
                        markerAnchor.transform.rotation = addedMarker.transform.rotation;

                        // Dashboard zentrieren
                        if (dashboardRoot != null)
                        {
                            dashboardRoot.transform.position = markerAnchor.transform.position;
                            dashboardRoot.transform.rotation = markerAnchor.transform.rotation;
                            Debug.Log("✅ Dashboard zentriert auf CenterMarker!");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("Kein QR-Code (vielleicht anderes Marker-Typ?): " + e.Message);
                }
            }
        }
    }

    void OnDestroy()
    {
        if (markerManager != null)
        {
            markerManager.markersChanged -= OnMarkersChanged;
        }
    }
}
