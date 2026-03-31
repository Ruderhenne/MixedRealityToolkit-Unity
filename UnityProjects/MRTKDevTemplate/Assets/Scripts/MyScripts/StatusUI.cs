using TMPro;
using UnityEngine;

public class StatusUI : MonoBehaviour
{
    [SerializeField] private QRTracker qrTracker;

    private TMPro.TextMeshPro text;

    void Start()
    {
        text = GetComponent<TMPro.TextMeshPro>();
        qrTracker.statusText = text;  // Automatisch verknüpfen
        text.text = "Bereit";
    }
}
