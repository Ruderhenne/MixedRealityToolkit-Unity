using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Microsoft.MixedReality.OpenXR;
using TMPro;

public class QRTracker : MonoBehaviour
{
    [Header("Zuweisen im Inspector")]
    [SerializeField] private GameObject dashboardRoot;
    [SerializeField] private GameObject markerAnchor;
    [SerializeField] private string expectedQRData = "CenterMarker-MoshadXR";

    [Header("Status Text")]
    [SerializeField] public TMP_Text statusText;
    [SerializeField] private string statusTextName = "statusText";

    [Header("Scan-Reticle (Zielhilfe)")]
    [Tooltip("Wird automatisch erzeugt, falls nicht zugewiesen.")]
    [SerializeField] private GameObject scanReticle;
    [SerializeField] private float reticleDistance = 1.0f;
    [SerializeField] private float reticleSize = 0.15f;
    [Tooltip("Material für das Reticle. Bitte im Editor zuweisen (z.B. Unlit/Color-Material).")]
    [SerializeField] private Material reticleMaterial;

    private ARMarkerManager markerManager;
    private Camera mainCam;
    private bool isScanning;
    private bool markerManagerSubscribed;
    private bool qrFound;

    private static QRTracker sceneInstance;
    private Coroutine clearTextCoroutine;

    void Awake()
    {
        mainCam = Camera.main;

        markerManager = GetComponent<ARMarkerManager>();

        if (markerManager != null)
        {
            sceneInstance = this;
            // ARMarkerManager NICHT deaktivieren – das Scene-Marker-Subsystem
            // braucht Zeit, um QR-Codes in der Umgebung zu finden.
            // Es bleibt von Anfang an aktiv.
            Debug.Log("[QRTracker] Szenen-Instanz mit ARMarkerManager registriert.");
        }

        if (statusText == null && dashboardRoot != null)
        {
            var named = dashboardRoot.transform.Find(statusTextName);
            if (named != null) statusText = named.GetComponent<TMP_Text>();
            if (statusText == null) statusText = dashboardRoot.GetComponentInChildren<TMP_Text>(true);

            if (statusText != null)
                LogStatus("StatusText automatisch mit Dashboard verbunden.");
            else
                LogStatus("StatusText nicht gefunden. Bitte im Inspector zuweisen.", true);
        }

        if (scanReticle == null)
        {
            scanReticle = CreateDefaultReticle();
        }
        scanReticle.SetActive(false);
    }

    IEnumerator Start()
    {
        if (markerManager != null)
        {
            // Szenen-Instanz: sofort subscriben und Manager aktiviert lassen
            markerManager.markersChanged += OnMarkersChanged;
            markerManagerSubscribed = true;

            //LogStatus("QRTracker bereit. Drücke 'Center' zum Scannen.");

            // Debug: Subsystem-Status nach ein paar Frames loggen
            yield return new WaitForSeconds(2f);
            var subsystem = markerManager.subsystem;
            if (subsystem != null)
                Debug.Log($"[QRTracker] Subsystem running={subsystem.running}");
            else
                Debug.LogWarning("[QRTracker] Subsystem ist NULL nach 2 Sekunden.");

            yield break;
        }

        // Prefab-Instanz: auf die Szenen-Instanz warten
        float timeout = 10f;
        float timer = 0f;
        while (sceneInstance == null && timer < timeout)
        {  
            timer += Time.deltaTime;
            yield return null;
        }

        if (sceneInstance != null)
        {
            Debug.Log("[QRTracker] Prefab-Instanz hat Szenen-QRTracker gefunden.");
        }
        else
        {
            markerManager = FindObjectOfType<ARMarkerManager>();
            if (markerManager != null)
            {
                sceneInstance = this;
                markerManager.markersChanged += OnMarkersChanged;
                markerManagerSubscribed = true;
                LogStatus("QRTracker bereit (ARMarkerManager in Szene gefunden).");
            }
            else
            {
                LogStatus("Kein ARMarkerManager in der Szene gefunden!", true);
            }
        }
    }

    public void StartScanning()
    {
        // Prefab-Instanz → an Szenen-Instanz delegieren
        if (markerManager == null && sceneInstance != null && sceneInstance != this)
        {
            Debug.Log("[QRTracker] Prefab-Instanz delegiert StartScanning an Szenen-Instanz.");
            sceneInstance.dashboardRoot = this.dashboardRoot;
            sceneInstance.statusText = this.statusText;
            sceneInstance.StartScanning();
            return;
        }

        if (markerManager == null)
        {
            LogStatus("ARMarkerManager nicht gefunden! QR-Scan nicht möglich.", true);
            return;
        }

        isScanning = true;
        qrFound = false;

        // Sicherstellen dass Manager und Subscription aktiv sind
        if (!markerManager.enabled)
        {
            markerManager.enabled = true;
            Debug.Log("[QRTracker] ARMarkerManager reaktiviert.");
        }

        if (!markerManagerSubscribed)
        {
            markerManager.markersChanged += OnMarkersChanged;
            markerManagerSubscribed = true;
            Debug.Log("[QRTracker] Event-Subscription erneuert.");
        }

        if (scanReticle != null) scanReticle.SetActive(true);
        LogStatus("Suche QR-Code... Blicke auf den QR-Code und bewege den Kopf langsam.");

        // Timeout-Coroutine: nach 30s Feedback geben
        StartCoroutine(ScanTimeout(30f));
    }

    private IEnumerator ScanTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (isScanning && !qrFound)
        {
            var subsystem = markerManager.subsystem;
            string subsystemInfo = subsystem != null
                ? $"running={subsystem.running}"
                : "NULL";

            LogStatus($"Noch kein QR-Code gefunden. Subsystem: {subsystemInfo}. " +
                      "Halte den QR-Code ruhig, ca. 50cm-1m Abstand.", true);
        }
    }

    public void StopScanning()
    {
        isScanning = false;
        if (scanReticle != null) scanReticle.SetActive(false);
        // ARMarkerManager NICHT deaktivieren – soll weiterlaufen
    }

    void Update()
    {
        if (isScanning && scanReticle != null && mainCam != null)
        {
            scanReticle.transform.position = mainCam.transform.position + mainCam.transform.forward * reticleDistance;
            scanReticle.transform.rotation = mainCam.transform.rotation;
        }
    }

    private void OnMarkersChanged(ARMarkersChangedEventArgs args)
    {
        int total = (args.added?.Count ?? 0) + (args.updated?.Count ?? 0) + (args.removed?.Count ?? 0);

        // Immer loggen wenn Marker erkannt werden – auch wenn nicht aktiv gescannt wird
        Debug.Log($"[QRTracker] OnMarkersChanged: +{args.added?.Count ?? 0} ~{args.updated?.Count ?? 0} -{args.removed?.Count ?? 0} (isScanning={isScanning})");

        if (!isScanning) return;

        ProcessMarkers(args.added);
        ProcessMarkers(args.updated);
    }

    private void ProcessMarkers(IReadOnlyList<ARMarker> markers)
    {
        if (markers == null) return;

        foreach (var marker in markers)
        {
            Debug.Log($"[QRTracker] Marker: id={marker.trackableId} state={marker.trackingState} type={marker.markerType}");

            if (marker.trackingState == TrackingState.Tracking)
            {
                try
                {
                    string qrData = markerManager.GetDecodedString(marker.trackableId);
                    if (qrData != null) qrData = qrData.Trim();

                    LogStatus($"QR-Code erkannt: '{qrData}'");

                    if (string.Equals(qrData, expectedQRData, StringComparison.OrdinalIgnoreCase))
                    {
                        qrFound = true;

                        Vector3 markerPos = marker.transform.position;
                        Quaternion markerRot = marker.transform.rotation;

                        // Z-Achse des Markers zeigt aus der Wand heraus (zum Betrachter)
                        Vector3 markerForward = markerRot * Vector3.forward;
                        // Y-Achse des Markers zeigt "oben" am QR-Code entlang
                        Vector3 markerUp = markerRot * Vector3.up;

                        Debug.Log($"[QRTracker] Marker Pos={markerPos} Rot={markerRot.eulerAngles} Forward={markerForward} Up={markerUp}");

                        if (markerAnchor != null)
                        {
                            markerAnchor.transform.SetPositionAndRotation(markerPos, markerRot);
                        }

                        if (dashboardRoot != null)
                        {
                            // 20cm vor dem QR-Code (entlang seiner Normalen)
                            Vector3 dashboardPos = markerPos + markerForward * 0.20f;

                            // Dashboard schaut zum Betrachter
                            Quaternion dashboardRot = Quaternion.LookRotation(-markerForward, markerUp);

                            dashboardRoot.transform.SetPositionAndRotation(dashboardPos, dashboardRot);

                            LogStatus("Dashboard zentriert auf CenterMarker!");
                        }
                        else
                        {
                            LogStatus("Kein DashboardRoot zugewiesen.", true);
                        }

                        StopScanning();
                        return;
                    }
                    else
                    {
                        LogStatus($"QR: '{qrData}' (erwartet '{expectedQRData}').");
                    }
                }
                catch (Exception e)
                {
                    LogStatus("Fehler: " + e.Message, true);
                }
            }
        }
    }

    void OnDestroy()
    {
        if (markerManager != null && markerManagerSubscribed)
        {
            markerManager.markersChanged -= OnMarkersChanged;
            markerManagerSubscribed = false;
        }

        if (sceneInstance == this)
        {
            sceneInstance = null;
        }
    }

    private void LogStatus(string message, bool isWarning = false)
    {
        if (isWarning)
            Debug.LogWarning($"[QRTracker] {message}");
        else
            Debug.Log($"[QRTracker] {message}");

        if (statusText != null)
        {
            try
            {
                statusText.text = message;

                if (clearTextCoroutine != null)
                    StopCoroutine(clearTextCoroutine);

                clearTextCoroutine = StartCoroutine(ClearStatusText(5f));
            }
            catch (Exception) { }
        }
    }

    private IEnumerator ClearStatusText(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (statusText != null)
        {
            try { statusText.text = ""; }
            catch (Exception) { }
        }

        clearTextCoroutine = null;
    }

    private GameObject CreateDefaultReticle()
    {
        var reticle = new GameObject("ScanReticle");
        reticle.transform.SetParent(transform);

        Material mat = reticleMaterial;
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                mat = new Material(shader);
                mat.color = Color.green;
            }
            else
            {
                Debug.LogWarning("[QRTracker] Kein Reticle-Material! Bitte im Inspector zuweisen.");
            }
        }

        float half = reticleSize / 2f;
        float cornerLen = reticleSize * 0.3f;

        Vector3[] corners = new Vector3[]
        {
            new Vector3(-half, -half, 0f),
            new Vector3( half, -half, 0f),
            new Vector3( half,  half, 0f),
            new Vector3(-half,  half, 0f),
        };

        Vector3[][] cornerLines = new Vector3[][]
        {
            new[] { corners[0] + Vector3.up * cornerLen, corners[0], corners[0] + Vector3.right * cornerLen },
            new[] { corners[1] + Vector3.left * cornerLen, corners[1], corners[1] + Vector3.up * cornerLen },
            new[] { corners[2] + Vector3.down * cornerLen, corners[2], corners[2] + Vector3.left * cornerLen },
            new[] { corners[3] + Vector3.right * cornerLen, corners[3], corners[3] + Vector3.down * cornerLen },
        };

        for (int i = 0; i < cornerLines.Length; i++)
        {
            var lineObj = new GameObject($"Corner_{i}");
            lineObj.transform.SetParent(reticle.transform, false);

            var lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = cornerLines[i].Length;
            lr.SetPositions(cornerLines[i]);
            lr.startWidth = 0.003f;
            lr.endWidth = 0.003f;
            lr.startColor = Color.green;
            lr.endColor = Color.green;

            if (mat != null)
            {
                lr.material = mat;
            }
        }

        return reticle;
    }
}
