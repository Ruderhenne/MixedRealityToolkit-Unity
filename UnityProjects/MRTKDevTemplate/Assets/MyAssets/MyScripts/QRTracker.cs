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
    [SerializeField] public GameObject dashboardRoot;
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
    [SerializeField] private int requiredSamples = 5;
    [Tooltip("Minimale Wartezeit in Sekunden nach dem ersten Scan")]
    [SerializeField] private float minStabilizationTime = 2.0f;

    private ARMarkerManager markerManager;
    private Camera mainCam;
    private bool isScanning;
    private bool markerManagerSubscribed;
    private bool qrFound;

    // Stabilization data
    private List<Vector3> positionSamples = new List<Vector3>();
    private List<Quaternion> rotationSamples = new List<Quaternion>();
    private float firstSampleTime;
    private bool isStabilizing;
    private TrackableId stabilizingMarkerId;

    private static QRTracker sceneInstance;

    void Awake()
    {
        mainCam = Camera.main;
        markerManager = GetComponent<ARMarkerManager>();

        if (markerManager != null)
        {
            sceneInstance = this;
            Debug.Log("[QRTracker] Scene instance with ARMarkerManager registered.");
        }

        if (statusText == null && dashboardRoot != null)
        {
            var named = dashboardRoot.transform.Find(statusTextName);
            if (named != null) statusText = named.GetComponent<TMP_Text>();
            if (statusText == null) statusText = dashboardRoot.GetComponentInChildren<TMP_Text>(true);

            if (statusText != null)
                LogStatus("StatusText automatically linked to dashboard.");
            else
                LogStatus("StatusText not found. Please assign in Inspector.", true);
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
                Debug.LogWarning("[QRTracker] Subsystem is NULL after 2 seconds.");

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
            Debug.Log("[QRTracker] Prefab instance found scene QRTracker.");
        }
        else
        {
            markerManager = FindObjectOfType<ARMarkerManager>();
            if (markerManager != null)
            {
                sceneInstance = this;
                markerManager.markersChanged += OnMarkersChanged;
                markerManagerSubscribed = true;
                LogStatus("QRTracker ready (ARMarkerManager found in scene).");
            }
            else
            {
                LogStatus("No ARMarkerManager found in the scene!", true);
            }
        }
    }

    public void StartScanning()
    {
        if (markerManager == null && sceneInstance != null && sceneInstance != this)
        {
            Debug.Log("[QRTracker] Prefab instance delegates StartScanning to scene instance.");
            sceneInstance.dashboardRoot = this.dashboardRoot;
            sceneInstance.statusText = this.statusText;
            sceneInstance.StartScanning();
            return;
        }

        if (markerManager == null)
        {
            LogStatus("ARMarkerManager not found! QR scan not possible.", true);
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
            Debug.Log("[QRTracker] ARMarkerManager reactivated.");
        }

        if (!markerManagerSubscribed)
        {
            markerManager.markersChanged += OnMarkersChanged;
            markerManagerSubscribed = true;
            Debug.Log("[QRTracker] Event subscription renewed.");
        }

        if (scanReticle != null) scanReticle.SetActive(true);
        LogStatus("Searching for QR code... Look at the QR code and move your head slowly.");
        StartCoroutine(ScanTimeout(30f));
    }

    private IEnumerator ScanTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (isScanning && !qrFound)
        {
            var subsystem = markerManager.subsystem;
            string subsystemInfo = subsystem != null ? $"running={subsystem.running}" : "NULL";
            LogStatus($"No QR code found yet. Subsystem: {subsystemInfo}. " +
                      "Hold the QR code steady, approx. 50cm-1m distance.", true);
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
                    LogStatus($"QR: '{qrData}' (expected '{expectedQRData}').");
                    continue;
                }

                if (!isStabilizing)
                {
                    isStabilizing = true;
                    stabilizingMarkerId = marker.trackableId;
                    firstSampleTime = Time.time;
                    positionSamples.Clear();
                    rotationSamples.Clear();
                    LogStatus($"QR code detected – stabilizing... (0/{requiredSamples})");
                }

                if (marker.trackableId != stabilizingMarkerId) continue;

                // Store CAMERA-RELATIVE: marker position and rotation in the
                // camera's local space. This makes the result independent of
                // the world origin (= HoloLens start position).
                Vector3 markerPosInCamSpace = mainCam.transform.InverseTransformPoint(marker.transform.position);
                Quaternion markerRotInCamSpace = Quaternion.Inverse(mainCam.transform.rotation) * marker.transform.rotation;

                positionSamples.Add(markerPosInCamSpace);
                rotationSamples.Add(markerRotInCamSpace);

                float elapsed = Time.time - firstSampleTime;
                int count = positionSamples.Count;

                Debug.Log($"[QRTracker] Sample {count}/{requiredSamples}, elapsed={elapsed:F2}s, camSpacePos={markerPosInCamSpace}");
                LogStatus($"Stabilizing... ({count}/{requiredSamples})");

                if (count >= requiredSamples && elapsed >= minStabilizationTime)
                {
                    qrFound = true;

                    // Compute camera-relative average
                    Vector3 avgCamPos = Vector3.zero;
                    foreach (var p in positionSamples) avgCamPos += p;
                    avgCamPos /= count;

                    Quaternion avgCamRot = rotationSamples[0];
                    for (int i = 1; i < rotationSamples.Count; i++)
                        avgCamRot = Quaternion.Slerp(avgCamRot, rotationSamples[i], 1f / (i + 1));

                    // Convert back to world coordinates – now RELIABLE,
                    // because the average was computed in camera space.
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
                        LogStatus("No DashboardRoot assigned.", true);
                    }

                    StopScanning();
                    return;
                }
            }
            catch (Exception e)
            {
                LogStatus("Error: " + e.Message, true);
            }
        }
    }

    /// <summary>
    /// Sets the position and rotation of the dashboard.
    /// Temporarily disables the PhotonTransformView so that
    /// network sync does not immediately overwrite the new position.
    /// Afterwards the new position is sent to all clients via RPC.
    /// </summary>
    private void ApplyDashboardTransform(Vector3 pos, Quaternion rot)
    {
        // Temporarily disable PhotonTransformView (and PhotonTransformViewClassic)
        // so Photon does not immediately overwrite the new transform
        // with the old network state.
        var photonView = dashboardRoot.GetComponent<PhotonView>();
        var transformView = dashboardRoot.GetComponent<PhotonTransformView>();
        var transformViewClassic = dashboardRoot.GetComponent<PhotonTransformViewClassic>();

        if (transformView != null)   transformView.enabled = false;
        if (transformViewClassic != null) transformViewClassic.enabled = false;

        // Set transform locally
        dashboardRoot.transform.SetPositionAndRotation(pos, rot);

        Debug.Log($"[QRTracker] Dashboard placed: pos={pos} rot={rot.eulerAngles}");

        // Broadcast new position to all clients (only master may do this)
        if (photonView != null && PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_SetDashboardTransform", RpcTarget.AllBuffered,
                pos.x, pos.y, pos.z,
                rot.x, rot.y, rot.z, rot.w);

            Debug.Log("[QRTracker] RPC_SetDashboardTransform sent.");
        }

        // Re-enable PhotonTransformView – from now on the new position
        // is used as the base for sync.
        if (transformView != null)   transformView.enabled = true;
        if (transformViewClassic != null) transformViewClassic.enabled = true;

        LogStatus("Dashboard centered on CenterMarker!");
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

        // Use central StatusLogger (displays message with auto-clear)
        StatusLogger.Log(message);
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
                Debug.LogWarning("[QRTracker] No reticle material! Please assign in Inspector.");
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
