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

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Master Client creating dashboard...");

            Transform cam = Camera.main.transform;
            Vector3 pos = cam.position + cam.forward * 0.9f;    //Entfernung des Dashboards vor der Kamera
            Quaternion rot = Quaternion.LookRotation(cam.forward);

            // Wichtig: als Room-Objekt instanziieren, damit es bestehen bleibt, wenn der Master geht
            GameObject dash = PhotonNetwork.InstantiateRoomObject("DashboardPanel", pos, rot);

            Debug.Log($"Dashboard instantiated at {pos} - Children: {dash.transform.childCount}");

            // Master hat nun sein eigenes Dashboard mit seinem eigenen SharedPointerManager
            // Der SharedPointerManager ist am XR-Rig, nicht am Prefab
            Debug.Log($"Master Dashboard created successfully");
        }
        else
        {
            Debug.Log("Non-Master Client joining room...");
            // Client erhält das Dashboard vom Master
            // Der SharedPointerManager ist am XR-Rig des Clients
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player entered room: {newPlayer.ActorNumber}");
        Debug.Log($"Total players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player left room: {otherPlayer.ActorNumber}");
        Debug.Log($"Remaining players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left room");
    }
}
