using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class RoomManager : MonoBehaviourPunCallbacks
{
    void Start()
    {
        Debug.Log("RoomManager Start");
        PhotonNetwork.ConnectUsingSettings();
        DontDestroyOnLoad(gameObject);
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master");
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 4 };
        PhotonNetwork.JoinOrCreateRoom("DashboardRoom", roomOptions, null);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        Debug.Log($"Current Room Players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        Debug.Log($"Local Player Actor Number: {PhotonNetwork.LocalPlayer.ActorNumber}");

        StatusLogger.Log($"User {PhotonNetwork.LocalPlayer.ActorNumber}: Joined session");

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Master Client creating dashboard...");

            Transform cam = Camera.main.transform;
            Vector3 pos = cam.position + cam.forward * 1.1f + Vector3.down * 0.2f;
            Quaternion rot = Quaternion.LookRotation(cam.forward);

            GameObject dash = PhotonNetwork.InstantiateRoomObject("DashboardWindow", pos, rot);

            Debug.Log($"Dashboard instantiated at {pos} - Children: {dash.transform.childCount}");
            Debug.Log($"Master Dashboard created successfully");
        }
        else
        {
            Debug.Log("Non-Master Client joining room...");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player entered room: {newPlayer.ActorNumber}");
        Debug.Log($"Total players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");

        StatusLogger.Log($"User {newPlayer.ActorNumber}: Joined session");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player left room: {otherPlayer.ActorNumber}");
        Debug.Log($"Remaining players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");

        StatusLogger.Log($"User {otherPlayer.ActorNumber}: Left session");
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left room");
    }
}
