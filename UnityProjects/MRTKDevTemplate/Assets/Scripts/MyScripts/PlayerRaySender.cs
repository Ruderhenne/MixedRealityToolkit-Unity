using System.Collections;
using Photon.Pun;
using ExitGames.Client.Photon;
using UnityEngine;

public class PlayerRaySender : MonoBehaviourPun
{
    public Transform leftHandPalm;
    public Transform rightHandPalm;

    [Tooltip("Minimale Positionsänderung in Metern, um ein Update zu senden")]
    public float positionThreshold = 0.005f;
    [Tooltip("Minimale Richtungsänderung in Grad, um ein Update zu senden")]
    public float directionThresholdDeg = 1f;

    private bool leftHandActive = false;
    private bool rightHandActive = false;

    // Letzte gesendete Werte für Dirty-Check
    private Vector3 lastLeftPos, lastLeftDir;
    private Vector3 lastRightPos, lastRightDir;
    private Vector3 lastLegacyPos, lastLegacyDir;

    private IEnumerator Start()
    {
        // Warte bis Photon den LocalPlayer und Raum initialisiert hat
        yield return new WaitUntil(() => PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null);

        int actor = PhotonNetwork.LocalPlayer.ActorNumber;
        Debug.Log($"[PlayerRaySender] START Actor {actor} - enabled:{enabled} leftAssigned:{(leftHandPalm != null)} rightAssigned:{(rightHandPalm != null)} InRoom:{PhotonNetwork.InRoom}");
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom) return;

        // Aktuelle Aktivität beider Hände prüfen
        leftHandActive = leftHandPalm && leftHandPalm.gameObject.activeInHierarchy;
        rightHandActive = rightHandPalm && rightHandPalm.gameObject.activeInHierarchy;

        // Sammle alle geänderten Properties in einem einzigen Hashtable
        var props = new ExitGames.Client.Photon.Hashtable();

        if (leftHandActive)
            TryAddHandRay(leftHandPalm, "leftHandPos", "leftHandDir", ref lastLeftPos, ref lastLeftDir, props);
        else
            AddClearKeys("leftHandPos", "leftHandDir", ref lastLeftPos, ref lastLeftDir, props);

        if (rightHandActive)
            TryAddHandRay(rightHandPalm, "rightHandPos", "rightHandDir", ref lastRightPos, ref lastRightDir, props);
        else
            AddClearKeys("rightHandPos", "rightHandDir", ref lastRightPos, ref lastRightDir, props);

        // Legacy-Keys: bevorzugt rechts, dann links
        Transform legacyHand = rightHandActive ? rightHandPalm : (leftHandActive ? leftHandPalm : null);
        if (legacyHand != null)
            TryAddHandRay(legacyHand, "headPos", "rayDir", ref lastLegacyPos, ref lastLegacyDir, props);
        else
            AddClearKeys("headPos", "rayDir", ref lastLegacyPos, ref lastLegacyDir, props);

        // Nur EIN Netzwerk-Call pro Frame, und nur wenn sich etwas geändert hat
        if (props.Count > 0)
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private void TryAddHandRay(Transform handPalm, string posKey, string dirKey,
        ref Vector3 lastPos, ref Vector3 lastDir, ExitGames.Client.Photon.Hashtable props)
    {
        Vector3 position = handPalm.position;
        Vector3 direction = handPalm.forward;

        bool posChanged = Vector3.Distance(position, lastPos) > positionThreshold;
        bool dirChanged = Vector3.Angle(direction, lastDir) > directionThresholdDeg;

        if (!posChanged && !dirChanged) return;

        lastPos = position;
        lastDir = direction;

        float[] posArr = { position.x, position.y, position.z };
        float[] dirArr = { direction.x, direction.y, direction.z };

        props[posKey] = posArr;
        props[dirKey] = dirArr;
    }

    private void AddClearKeys(string posKey, string dirKey,
        ref Vector3 lastPos, ref Vector3 lastDir, ExitGames.Client.Photon.Hashtable props)
    {
        // Nur clearen, wenn vorher tatsächlich Werte gesendet wurden
        if (lastPos == Vector3.zero && lastDir == Vector3.zero) return;

        lastPos = Vector3.zero;
        lastDir = Vector3.zero;
        props[posKey] = null;
        props[dirKey] = null;
    }
}
