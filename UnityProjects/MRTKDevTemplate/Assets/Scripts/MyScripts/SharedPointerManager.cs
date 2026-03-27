using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class SharedPointerManager : MonoBehaviourPun
{
    [Header("Visual")]
    public LineRenderer pointerLinePrefab;
    [Tooltip("Fallback-Material (wird genutzt, wenn keine spezifischen Materialien gesetzt sind)")]
    public Material pointerMaterial;
    [Tooltip("Material für den linken Pointer")]
    public Material pointerMaterialLeft;
    [Tooltip("Material für den rechten Pointer")]
    public Material pointerMaterialRight;
    [Tooltip("Wie lange ein Pointer noch sichtbar bleibt, wenn Handdaten kurzzeitig fehlen (Sekunden)")]
    public float disappearDelay = 0.25f;
    public float pointerLength = 5f;

    [Header("Behaviour")]
    [Tooltip("Soll der lokale Client seinen eigenen Pointer sehen? (false = nur andere Clients sichtbar)")]
    public bool showLocalPointer = false;

    private const int LEFT = 0;
    private const int RIGHT = 1;

    // Mapping: ActorNumber -> [leftLine, rightLine]
    private Dictionary<int, LineRenderer[]> playerPointers = new Dictionary<int, LineRenderer[]>();
    private Dictionary<int, float[]> lastSeenTime = new Dictionary<int, float[]>();

    void Update()
    {
        if (!PhotonNetwork.InRoom || Time.time < 2f) return;

        UpdatePointers();
    }

    void UpdatePointers()
    {
        // Erhalte aktuelle Players und ihre ActorNumbers
        var players = PhotonNetwork.PlayerList;
        var currentActors = new HashSet<int>();
        foreach (var p in players)
            currentActors.Add(p.ActorNumber);

        // Sicherstellen: Für jeden Player existieren Pointer
        foreach (var p in players)
        {
            if (!playerPointers.ContainsKey(p.ActorNumber))
                CreatePointersForPlayer(p.ActorNumber);
        }

        // Entferne Pointer für Spieler, die nicht mehr in der Lobby sind
        var keysToRemove = new List<int>();
        foreach (var kv in playerPointers)
        {
            if (!currentActors.Contains(kv.Key))
                keysToRemove.Add(kv.Key);
        }
        foreach (int actor in keysToRemove)
        {
            DestroyPointersForPlayer(actor);
        }

        // Aktualisiere Pointer pro Player
        foreach (var p in players)
        {
            // Optional: lokale Pointer unterdrücken
            if (!showLocalPointer && p == PhotonNetwork.LocalPlayer)
            {
                var localPair = playerPointers[p.ActorNumber];
                SetPointerVisible(localPair[LEFT], false);
                SetPointerVisible(localPair[RIGHT], false);
                continue;
            }

            var pair = playerPointers[p.ActorNumber];
            var times = lastSeenTime[p.ActorNumber];

            bool leftValid = false;
            bool rightValid = false;

            if (p.CustomProperties.ContainsKey("leftHandPos") &&
                p.CustomProperties.ContainsKey("leftHandDir"))
            {
                if (TryParseVector3(p.CustomProperties["leftHandPos"], out Vector3 leftPos) &&
                    TryParseVector3(p.CustomProperties["leftHandDir"], out Vector3 leftDir))
                {
                    Vector3 leftEnd = leftPos + leftDir.normalized * pointerLength;
                    pair[LEFT].SetPosition(0, leftPos);
                    pair[LEFT].SetPosition(1, leftEnd);
                    times[LEFT] = Time.time;
                    leftValid = true;
                    SetPointerVisible(pair[LEFT], true);
                }
            }

            if (p.CustomProperties.ContainsKey("rightHandPos") &&
                p.CustomProperties.ContainsKey("rightHandDir"))
            {
                if (TryParseVector3(p.CustomProperties["rightHandPos"], out Vector3 rightPos) &&
                    TryParseVector3(p.CustomProperties["rightHandDir"], out Vector3 rightDir))
                {
                    Vector3 rightEnd = rightPos + rightDir.normalized * pointerLength;
                    pair[RIGHT].SetPosition(0, rightPos);
                    pair[RIGHT].SetPosition(1, rightEnd);
                    times[RIGHT] = Time.time;
                    rightValid = true;
                    SetPointerVisible(pair[RIGHT], true);
                }
            }

            // Rückwärtskompatibilität: headPos/rayDir als primärer (right)
            if (!leftValid && !rightValid &&
                p.CustomProperties.ContainsKey("headPos") &&
                p.CustomProperties.ContainsKey("rayDir"))
            {
                if (TryParseVector3(p.CustomProperties["headPos"], out Vector3 headPos) &&
                    TryParseVector3(p.CustomProperties["rayDir"], out Vector3 headDir))
                {
                    Vector3 endPos = headPos + headDir.normalized * pointerLength;
                    pair[RIGHT].SetPosition(0, headPos);
                    pair[RIGHT].SetPosition(1, endPos);
                    times[RIGHT] = Time.time;
                    rightValid = true;
                    SetPointerVisible(pair[RIGHT], true);
                }
            }

            // Timeout-basierte Ausblendung
            if (!leftValid && Time.time - times[LEFT] > disappearDelay)
                SetPointerVisible(pair[LEFT], false);
            if (!rightValid && Time.time - times[RIGHT] > disappearDelay)
                SetPointerVisible(pair[RIGHT], false);
        }
    }

    void CreatePointersForPlayer(int actorNumber)
    {
        // Materialauswahl
        Material baseMat = pointerLinePrefab != null ? pointerLinePrefab.sharedMaterial : null;
        Material leftMat = pointerMaterialLeft != null ? pointerMaterialLeft : (pointerMaterial != null ? pointerMaterial : baseMat);
        Material rightMat = pointerMaterialRight != null ? pointerMaterialRight : (pointerMaterial != null ? pointerMaterial : baseMat);

        // WICHTIG: Pointer nicht als Kind von diesem (möglicherweise beweglichen) Objekt erstellen.
        // Stattdessen in Szenen-Root (null) instantiieren, damit sie sich nicht bewegen, wenn der Master sein Rig bewegt.
        LineRenderer leftLine = Instantiate(pointerLinePrefab);
        LineRenderer rightLine = Instantiate(pointerLinePrefab);

        // Sicherstellen: Parent der Pointer ist die Szenen-Root (keine Bewegung durch lokale Rigs).
        leftLine.transform.SetParent(null, true);
        rightLine.transform.SetParent(null, true);

        if (leftMat != null) leftLine.material = leftMat;
        if (rightMat != null) rightLine.material = rightMat;

        if (pointerMaterialLeft == null && pointerMaterial == null)
        {
            leftLine.colorGradient = new Gradient()
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(Color.cyan, 0f),
                    new GradientColorKey(Color.cyan, 1f)
                }
            };
        }

        if (pointerMaterialRight == null && pointerMaterial == null)
        {
            rightLine.colorGradient = new Gradient()
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(Color.yellow, 0f),
                    new GradientColorKey(Color.yellow, 1f)
                }
            };
        }

        leftLine.sortingLayerName = "UI";
        leftLine.sortingOrder = 1000;
        leftLine.positionCount = 2;
        leftLine.useWorldSpace = true;
        leftLine.gameObject.SetActive(false);

        rightLine.sortingLayerName = "UI";
        rightLine.sortingOrder = 1000;
        rightLine.positionCount = 2;
        rightLine.useWorldSpace = true;
        rightLine.gameObject.SetActive(false);

        playerPointers[actorNumber] = new LineRenderer[] { leftLine, rightLine };
        lastSeenTime[actorNumber] = new float[] { Mathf.NegativeInfinity, Mathf.NegativeInfinity };
    }

    void DestroyPointersForPlayer(int actorNumber)
    {
        if (playerPointers.TryGetValue(actorNumber, out var pair))
        {
            if (pair[LEFT] != null) Destroy(pair[LEFT].gameObject);
            if (pair[RIGHT] != null) Destroy(pair[RIGHT].gameObject);
            playerPointers.Remove(actorNumber);
        }
        if (lastSeenTime.ContainsKey(actorNumber))
            lastSeenTime.Remove(actorNumber);
    }

    void SetPointerVisible(LineRenderer lr, bool visible)
    {
        if (lr == null) return;
        lr.gameObject.SetActive(visible);
        lr.enabled = visible;
    }

    bool TryParseVector3(object obj, out Vector3 result)
    {
        result = Vector3.zero;

        if (obj is Vector3 v)
        {
            result = v;
            return true;
        }

        if (obj is float[] fa && fa.Length == 3)
        {
            result = new Vector3(fa[0], fa[1], fa[2]);
            return true;
        }

        if (obj is object[] oa && oa.Length == 3)
        {
            if (oa[0] is float fx && oa[1] is float fy && oa[2] is float fz)
            {
                result = new Vector3(fx, fy, fz);
                return true;
            }
            if (oa[0] is double dx && oa[1] is double dy && oa[2] is double dz)
            {
                result = new Vector3((float)dx, (float)dy, (float)dz);
                return true;
            }
        }

        return false;
    }
}
