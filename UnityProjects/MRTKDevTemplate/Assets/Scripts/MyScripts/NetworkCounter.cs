using UnityEngine;
using Photon.Pun;
using TMPro;
using System.IO;

public class NetworkCounter : MonoBehaviourPun
{
    [SerializeField] TMP_Text counterText;
    private int counterValue = 0;
    private string savePath = "";

    void Start()
    {
        savePath = Path.Combine(Application.persistentDataPath, "counter.txt");

        // Nur der Master lädt beim Start den gespeicherten Wert
        if (PhotonNetwork.IsMasterClient)
        {
            LoadCounter();
        }
    }

    public void IncrementCounter()
    {
        // Nur der Owner des PhotonViews darf den Wert ändern
        if (!photonView.IsMine) return;

        counterValue++;
        counterText.text = counterValue.ToString();

        // Netzwerk‑Sync an alle Clients
        photonView.RPC("UpdateCounter", RpcTarget.All, counterValue);

        // Nur der Master speichert persistent
        if (PhotonNetwork.IsMasterClient)
        {
            SaveCounter();
        }
    }

    [PunRPC]
    void UpdateCounter(int newValue)
    {
        counterValue = newValue;
        counterText.text = newValue.ToString();
    }

    void SaveCounter()
    {
        try
        {
            File.WriteAllText(savePath, counterValue.ToString());
            Debug.Log($"Counter saved: {counterValue} -> {savePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
        }
    }

    void LoadCounter()
    {
        try
        {
            if (File.Exists(savePath))
            {
                counterValue = int.Parse(File.ReadAllText(savePath));
                counterText.text = counterValue.ToString();

                // Geladenen Wert sofort an alle Clients verteilen
                photonView.RPC("UpdateCounter", RpcTarget.All, counterValue);
                Debug.Log($"Counter loaded: {counterValue}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Load failed: {e.Message}");
        }
    }
}
