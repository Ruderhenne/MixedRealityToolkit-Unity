using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

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

        if (PhotonNetwork.IsMasterClient)
        {
            Transform cam = Camera.main.transform;
            Vector3 pos = cam.position + cam.forward * 0.5f;
            Quaternion rot = Quaternion.LookRotation(cam.forward);

            GameObject dash = PhotonNetwork.Instantiate("DashboardPanel", pos, rot);
            Debug.Log($"Dashboard at {pos} - Children: {dash.transform.childCount}");
        }
    }

}
