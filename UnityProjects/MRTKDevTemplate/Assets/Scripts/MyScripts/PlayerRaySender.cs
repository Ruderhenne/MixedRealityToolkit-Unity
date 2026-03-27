using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;

public class PlayerRaySender : MonoBehaviourPun
{
    public Transform leftHandPalm;   // Drag LeftHand → Palm
    public Transform rightHandPalm;  // Drag RightHand → Palm

    private bool leftHandActive = false;
    private bool rightHandActive = false;

    void Update()
    {
        if (!PhotonNetwork.InRoom) return;

        // aktuelle Aktivität beider Hände prüfen
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
    private void SendHandRay(Transform handPalm, string posKey, string dirKey)
    {
        if (handPalm == null) return;

        Vector3 position = handPalm.position;
        Vector3 direction = handPalm.forward;

        var props = new Hashtable
        {
            { posKey, position },
            { dirKey, direction }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    // Entfernt die angegebenen Keys aus den CustomProperties (Setzen auf null entfernt den Key)
    private void ClearHandRay(string posKey, string dirKey)
    {
        var props = new Hashtable
        {
            { posKey, null },
            { dirKey, null }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }
}
