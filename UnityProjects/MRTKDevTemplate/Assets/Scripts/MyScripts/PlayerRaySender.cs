using System.Collections;
using Photon.Pun;
using ExitGames.Client.Photon;
using UnityEngine;

public class PlayerRaySender : MonoBehaviourPun
{
    public Transform leftHandPalm;   // Drag LeftHand → Palm
    public Transform rightHandPalm;  // Drag RightHand → Palm

    private bool leftHandActive = false;
    private bool rightHandActive = false;

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

        // Je Hand eigene Keys senden/entfernen, damit später beide Pointer möglich sind
        if (leftHandActive)
            SendHandRay(leftHandPalm, "leftHandPos", "leftHandDir");
        else
            ClearHandRay("leftHandPos", "leftHandDir");

        if (rightHandActive)
            SendHandRay(rightHandPalm, "rightHandPos", "rightHandDir");
        else
            ClearHandRay("rightHandPos", "rightHandDir");

        // Zusätzlich: Kompatibilität mit altem Code / bestehendem Pointer-Renderer:
        // Setze immer die globalen Keys "headPos"/"rayDir" auf die bevorzugte Hand (rechts vor links).
        if (rightHandActive)
        {
            SendHandRay(rightHandPalm, "headPos", "rayDir");
        }
        else if (leftHandActive)
        {
            SendHandRay(leftHandPalm, "headPos", "rayDir");
        }
        else
        {
            ClearHandRay("headPos", "rayDir");
        }
    }

    // Sendet Position und Richtung einer Hand als Player-CustomProperties
    // Als float[] (robuster) + Debug-Log
    private void SendHandRay(Transform handPalm, string posKey, string dirKey)
    {
        if (handPalm == null) return;

        Vector3 position = handPalm.position;
        Vector3 direction = handPalm.forward;

        float[] posArr = new float[] { position.x, position.y, position.z };
        float[] dirArr = new float[] { direction.x, direction.y, direction.z };

        var props = new ExitGames.Client.Photon.Hashtable
        {
            { posKey, posArr },
            { dirKey, dirArr }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log($"[PlayerRaySender] Actor {PhotonNetwork.LocalPlayer.ActorNumber} set {posKey}={posArr[0]:F3},{posArr[1]:F3},{posArr[2]:F3} {dirKey}={dirArr[0]:F3},{dirArr[1]:F3},{dirArr[2]:F3}");
    }

    // Entfernt die angegebenen Keys aus den CustomProperties (Setzen auf null entfernt den Key)
    private void ClearHandRay(string posKey, string dirKey)
    {
        var props = new ExitGames.Client.Photon.Hashtable
        {
            { posKey, null },
            { dirKey, null }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        // Loggen, damit wir sehen, wenn ein Client die Keys entfernt (z.B. weil Hände inactive)
        if (PhotonNetwork.LocalPlayer != null)
        {
            Debug.Log($"[PlayerRaySender] Actor {PhotonNetwork.LocalPlayer.ActorNumber} cleared {posKey} & {dirKey}");
        }
    }
}
