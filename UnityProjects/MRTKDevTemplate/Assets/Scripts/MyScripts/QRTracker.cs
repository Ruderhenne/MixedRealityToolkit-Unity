using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Microsoft.MixedReality.OpenXR;
using TMPro;
using Photon.Pun;

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

    [Header("Dashboard-Platzierung")]
    [Tooltip("Abstand vor dem QR-Code in Metern")]
    [SerializeField] private float dashboardDistance = 0.20f;

    [Header("Stabilisierung")]
    [Tooltip("Anzahl der Tracking-Samples bevor das Dashboard platziert wird")]
    [SerializeField] private int requiredSamples = 15;
    [Tooltip("Minimale Wartezeit in Sekunden nach dem ersten Scan")]
    [SerializeField] private float minStabilizationTime = 2.0f;

    private ARMarkerManager markerManager;
    private Camera mainCam;
    private bool isScanning;
    private bool markerManagerSubscribed;
    private bool qrFound;

    // Stabilisierungs-Daten
    private List<Vector3> positionSamples = new List<Vector3>();
    private List<Quaternion> rotationSamples = new List<Quaternion>();
    private float firstSampleTime;
    private bool isStabilizing;
    private TrackableId stabilizingMarkerId;

    private static QRTracker sceneInstance;
    private Coroutine clearTextCoroutine;

    void Awake()
    {
        mainCam = Camera.main;
        markerManager = GetComponent<ARMarkerManager>();

        if (markerManager != null)
        {
            sceneInstance = this;
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
            scanReticle = CreateDefaultReticle();

        scanReticle.SetActive(false);
    }

    IEnumerator Start()
    {
        if (markerManager != null)
        {
            markerManager.markersChanged += OnMarkersChanged;
            markerManagerSubscribed = true;

            yield return new WaitForSeconds(2f);
            var subsystem = markerManager.subsystem;
            if (subsystem != null)
                Debug.Log($"[QRTracker] Subsystem running={subsystem.running}");
            else
                Debug.LogWarning("[QRTracker] Subsystem ist NULL nach 2 Sekunden.");

            yield break;
        }

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
        isStabilizing = false;
        positionSamples.Clear();
        rotationSamples.Clear();

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
        StartCoroutine(ScanTimeout(30f));
    }

    private IEnumerator ScanTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (isScanning && !qrFound)
        {
            var subsystem = markerManager.subsystem;
            string subsystemInfo = subsystem != null ? $"running={subsystem.running}" : "NULL";
            LogStatus($"Noch kein QR-Code gefunden. Subsystem: {subsystemInfo}. " +
                      "Halte den QR-Code ruhig, ca. 50cm-1m Abstand.", true);
        }
    }

    public void StopScanning()
    {
        isScanning = false;
        isStabilizing = false;
        if (scanReticle != null) scanReticle.SetActive(false);
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
            if (marker.trackingState != TrackingState.Tracking) continue;

            try
            {
                string qrData = markerManager.GetDecodedString(marker.trackableId);
                if (qrData != null) qrData = qrData.Trim();

                if (!string.Equals(qrData, expectedQRData, StringComparison.OrdinalIgnoreCase))
                {
                    LogStatus($"QR: '{qrData}' (erwartet '{expectedQRData}').");
                    continue;
                }

                if (!isStabilizing)
                {
                    isStabilizing = true;
                    stabilizingMarkerId = marker.trackableId;
                    firstSampleTime = Time.time;
                    positionSamples.Clear();
                    rotationSamples.Clear();
                    LogStatus($"QR-Code erkannt – stabilisiere... (0/{requiredSamples})");
                }

                if (marker.trackableId != stabilizingMarkerId) continue;

                // KAMERA-RELATIV speichern: Position und Rotation des Markers
                // im lokalen Raum der Kamera. Dadurch ist das Ergebnis
                // unabhängig vom Welt-Ursprung (= Startposition der HoloLens).
                Vector3 markerPosInCamSpace = mainCam.transform.InverseTransformPoint(marker.transform.position);
                Quaternion markerRotInCamSpace = Quaternion.Inverse(mainCam.transform.rotation) * marker.transform.rotation;

                positionSamples.Add(markerPosInCamSpace);
                rotationSamples.Add(markerRotInCamSpace);

                float elapsed = Time.time - firstSampleTime;
                int count = positionSamples.Count;

                Debug.Log($"[QRTracker] Sample {count}/{requiredSamples}, elapsed={elapsed:F2}s, camSpacePos={markerPosInCamSpace}");
                LogStatus($"Stabilisiere... ({count}/{requiredSamples})");

                if (count >= requiredSamples && elapsed >= minStabilizationTime)
                {
                    qrFound = true;

                    // Kamera-relativen Durchschnitt bilden
                    Vector3 avgCamPos = Vector3.zero;
                    foreach (var p in positionSamples) avgCamPos += p;
                    avgCamPos /= count;

                    Quaternion avgCamRot = rotationSamples[0];
                    for (int i = 1; i < rotationSamples.Count; i++)
                        avgCamRot = Quaternion.Slerp(avgCamRot, rotationSamples[i], 1f / (i + 1));

                    // Zurück in Weltkoordinaten – aber jetzt ZUVERLÄSSIG,
                    // weil der Mittelwert im Kameraraum gebildet wurde.
                    Vector3 avgWorldPos = mainCam.transform.TransformPoint(avgCamPos);
                    Quaternion avgWorldRot = mainCam.transform.rotation * avgCamRot;

                    Vector3 markerForward = avgWorldRot * Vector3.forward;
                    Vector3 markerUp     = avgWorldRot * Vector3.up;

                    Debug.Log($"[QRTracker] FINAL camPos={avgCamPos} worldPos={avgWorldPos} rot={avgWorldRot.eulerAngles} samples={count}");

                    if (markerAnchor != null)
                        markerAnchor.transform.SetPositionAndRotation(avgWorldPos, avgWorldRot);

                    if (dashboardRoot != null)
                    {
                        Vector3 dashboardPos  = avgWorldPos + markerForward * dashboardDistance;
                        Quaternion dashboardRot = Quaternion.LookRotation(-markerForward, markerUp);
                        ApplyDashboardTransform(dashboardPos, dashboardRot);
                    }
                    else
                    {
                        LogStatus("Kein DashboardRoot zugewiesen.", true);
                    }

                    StopScanning();
                    return;
                }
            }
            catch (Exception e)
            {
                LogStatus("Fehler: " + e.Message, true);
            }
        }
    }

    /// <summary>
    /// Setzt Position und Rotation des Dashboards.
    /// Deaktiviert dabei vorübergehend den PhotonTransformView,
    /// damit der Netzwerk-Sync die neue Position nicht sofort überschreibt.
    /// Danach wird die neue Position per RPC an alle Clients gesendet.
    /// </summary>
    private void ApplyDashboardTransform(Vector3 pos, Quaternion rot)
    {
        // PhotonTransformView (und ggf. PhotonTransformViewClassic) kurz deaktivieren,
        // damit Photon die neue Transformation nicht sofort mit dem alten
        // Netzwerkzustand überschreibt.
        var photonView = dashboardRoot.GetComponent<PhotonView>();
        var transformView = dashboardRoot.GetComponent<PhotonTransformView>();
        var transformViewClassic = dashboardRoot.GetComponent<PhotonTransformViewClassic>();

        if (transformView != null)   transformView.enabled = false;
        if (transformViewClassic != null) transformViewClassic.enabled = false;

        // Transformation lokal setzen
        dashboardRoot.transform.SetPositionAndRotation(pos, rot);

        Debug.Log($"[QRTracker] Dashboard gesetzt: pos={pos} rot={rot.eulerAngles}");

        // Neue Position an alle Clients broadcasten (nur Master darf das)
        if (photonView != null && PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_SetDashboardTransform", RpcTarget.AllBuffered,
                pos.x, pos.y, pos.z,
                rot.x, rot.y, rot.z, rot.w);

            Debug.Log("[QRTracker] RPC_SetDashboardTransform gesendet.");
        }

        // PhotonTransformView wieder aktivieren – ab jetzt wird die neue
        // Position als Basis für den Sync verwendet.
        if (transformView != null)   transformView.enabled = true;
        if (transformViewClassic != null) transformViewClassic.enabled = true;

        LogStatus("Dashboard zentriert auf CenterMarker!");
    }

    void OnDestroy()
    {
        if (markerManager != null && markerManagerSubscribed)
        {
            markerManager.markersChanged -= OnMarkersChanged;
            markerManagerSubscribed = false;
        }

        if (sceneInstance == this)
            sceneInstance = null;
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
                if (clearTextCoroutine != null) StopCoroutine(clearTextCoroutine);
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

            if (mat != null) lr.material = mat;
        }

        return reticle;
    }
}
