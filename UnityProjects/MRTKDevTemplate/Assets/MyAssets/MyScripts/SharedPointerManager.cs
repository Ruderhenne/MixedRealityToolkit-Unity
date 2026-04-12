using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class SharedPointerManager : MonoBehaviourPun
{
    [Header("Visual")]
    public LineRenderer pointerLinePrefab;
    [Tooltip("Fallback-Material")]
    public Material pointerMaterial;
    [Tooltip("Material für den linken Pointer")]
    public Material pointerMaterialLeft;
    [Tooltip("Material für den rechten Pointer")]
    public Material pointerMaterialRight;
    [Tooltip("Wie lange ein Pointer sichtbar bleibt, wenn Handdaten fehlen (Sekunden)")]
    public float disappearDelay = 0.25f;
    public float pointerLength = 5f;

    [Header("Behaviour")]
    [Tooltip("Soll der lokale Client seinen eigenen Pointer sehen?")]
    public bool showLocalPointer = false;
    [Tooltip("Sollen die Pointer anderer Spieler angezeigt werden?")]
    public bool showRemotePointers = false;

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public float debugLogInterval = 0.25f;

    private const int LEFT = 0;
    private const int RIGHT = 1;

    // Mapping: ActorNumber -> [leftLine, rightLine]
    private Dictionary<int, LineRenderer[]> playerPointers = new Dictionary<int, LineRenderer[]>();
    private Dictionary<int, float[]> lastSeenTime = new Dictionary<int, float[]>();
    private Dictionary<int, float> lastLogTime = new Dictionary<int, float>();

    private readonly HashSet<int> currentActors = new HashSet<int>();
    private readonly List<int> keysToRemove = new List<int>();

    void Update()
    {
        if (!PhotonNetwork.InRoom || Time.time < 2f) return;
        UpdatePointers();
    }

    void UpdatePointers()
    {
        // InRoom-Check hier entfernen – bereits in Update() geprüft

        var players = PhotonNetwork.PlayerList;

        currentActors.Clear();
        foreach (var p in players)
            currentActors.Add(p.ActorNumber);

        foreach (var p in players)
        {
            if (!playerPointers.ContainsKey(p.ActorNumber))
                CreatePointersForPlayer(p.ActorNumber);
        }

        keysToRemove.Clear();
        foreach (var kv in playerPointers)
        {
            if (!currentActors.Contains(kv.Key))
                keysToRemove.Add(kv.Key);
        }
        foreach (int actor in keysToRemove)
            DestroyPointersForPlayer(actor);

        foreach (var p in players)
        {
            if (p == PhotonNetwork.LocalPlayer)
            {
                HandleLocalPlayer(p);
                continue;
            }
            UpdateRemotePlayerPointer(p);
        }
    }

    void HandleLocalPlayer(Player player)
    {
        if (player == null || !playerPointers.ContainsKey(player.ActorNumber)) return;

        var localPair = playerPointers[player.ActorNumber];

        if (showLocalPointer)
        {
            SetPointerVisible(localPair[LEFT], true);
            SetPointerVisible(localPair[RIGHT], true);
        }
        else
        {
            SetPointerVisible(localPair[LEFT], false);
            SetPointerVisible(localPair[RIGHT], false);
        }
    }

    void UpdateRemotePlayerPointer(Player player)
    {
        // SICHERHEITSCHECKS
        if (player == null || !playerPointers.ContainsKey(player.ActorNumber)) return;
        if (player == PhotonNetwork.LocalPlayer) return;

        var pair = playerPointers[player.ActorNumber];
        var times = lastSeenTime[player.ActorNumber];

        // Remote-Pointer nicht anzeigen, wenn deaktiviert
        if (!showRemotePointers)
        {
            SetPointerVisible(pair[LEFT], false);
            SetPointerVisible(pair[RIGHT], false);
            return;
        }

        bool leftValid = false;
        bool rightValid = false;
        Vector3 leftPos = Vector3.zero, leftDir = Vector3.zero;
        Vector3 rightPos = Vector3.zero, rightDir = Vector3.zero;

        // DATEN VON REMOTE-SPIELER HOLEN
        bool hasLeftKeys = player.CustomProperties.ContainsKey("leftHandPos") && player.CustomProperties.ContainsKey("leftHandDir");
        bool hasRightKeys = player.CustomProperties.ContainsKey("rightHandPos") && player.CustomProperties.ContainsKey("rightHandDir");

        // LINKE HAND VERARBEITEN
        if (hasLeftKeys)
        {
            if (TryParseVector3(player.CustomProperties["leftHandPos"], out leftPos) &&
                TryParseVector3(player.CustomProperties["leftHandDir"], out leftDir))
            {
                Vector3 leftEnd = leftPos + leftDir.normalized * pointerLength;
                pair[LEFT].SetPosition(0, leftPos);
                pair[LEFT].SetPosition(1, leftEnd);
                times[LEFT] = Time.time;
                leftValid = true;
                SetPointerVisible(pair[LEFT], true);

                if (enableDebugLogs)
                    Debug.Log($"[SharedPointerManager] Updated LEFT pointer for REMOTE player {player.ActorNumber}: Pos={leftPos}, Dir={leftDir}");
            }
        }

        // RECHTE HAND VERARBEITEN
        if (hasRightKeys)
        {
            if (TryParseVector3(player.CustomProperties["rightHandPos"], out rightPos) &&
                TryParseVector3(player.CustomProperties["rightHandDir"], out rightDir))
            {
                Vector3 rightEnd = rightPos + rightDir.normalized * pointerLength;
                pair[RIGHT].SetPosition(0, rightPos);
                pair[RIGHT].SetPosition(1, rightEnd);
                times[RIGHT] = Time.time;
                rightValid = true;
                SetPointerVisible(pair[RIGHT], true);

                if (enableDebugLogs)
                    Debug.Log($"[SharedPointerManager] Updated RIGHT pointer for REMOTE player {player.ActorNumber}: Pos={rightPos}, Dir={rightDir}");
            }
        }

        // TIMEOUT-BASIERTE AUSBLENDUNG
        if (!leftValid && Time.time - times[LEFT] > disappearDelay)
            SetPointerVisible(pair[LEFT], false);
        if (!rightValid && Time.time - times[RIGHT] > disappearDelay)
            SetPointerVisible(pair[RIGHT], false);
    }

    void CreatePointersForPlayer(int actorNumber)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SharedPointerManager] Creating pointers for player {actorNumber} (Local: {PhotonNetwork.LocalPlayer.ActorNumber})");
        }

        // Materialauswahl
        Material baseMat = pointerLinePrefab != null ? pointerLinePrefab.sharedMaterial : null;
        Material leftMat = pointerMaterialLeft != null ? pointerMaterialLeft : (pointerMaterial != null ? pointerMaterial : baseMat);
        Material rightMat = pointerMaterialRight != null ? pointerMaterialRight : (pointerMaterial != null ? pointerMaterial : baseMat);

        // Pointer-Instanzen erstellen
        LineRenderer leftLine = Instantiate(pointerLinePrefab);
        LineRenderer rightLine = Instantiate(pointerLinePrefab);

        // Pointer in Szenen-Root platzieren (wichtig!)
        leftLine.transform.SetParent(null, true);
        rightLine.transform.SetParent(null, true);

        // Materialien zuweisen
        if (leftMat != null) leftLine.material = leftMat;
        if (rightMat != null) rightLine.material = rightMat;

        // Standardfarben falls keine Materialien gesetzt
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

        // Pointer-Einstellungen
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

        // Pointer speichern
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
        if (lastLogTime.ContainsKey(actorNumber))
            lastLogTime.Remove(actorNumber);
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
