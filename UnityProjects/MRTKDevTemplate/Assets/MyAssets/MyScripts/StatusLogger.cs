using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatusLogger : MonoBehaviour
{
    public static StatusLogger Instance { get; private set; }

    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float clearDelay = 5f;
    [SerializeField] private int maxLines = 3;

    private struct LineEntry
    {
        public int id;
        public string text;
        public Coroutine coroutine;
    }

    private int nextId = 0;
    private readonly List<LineEntry> entries = new List<LineEntry>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[StatusLogger] Duplicate found – ignored.");
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetStatusText(TMP_Text text)
    {
        statusText = text;
    }

    public TMP_Text GetStatusText() => statusText;

    public static void Log(string message)
    {
        Debug.Log($"[StatusLogger] {message}");

        if (Instance != null && Instance.statusText != null)
        {
            try
            {
                Instance.AddLine(message);
            }
            catch (System.Exception) { }
        }
    }

    private void AddLine(string message)
    {
        // Älteste Zeile entfernen, wenn Limit erreicht
        while (entries.Count >= maxLines)
        {
            var oldest = entries[entries.Count - 1];
            StopCoroutine(oldest.coroutine);
            entries.RemoveAt(entries.Count - 1);
        }

        int id = nextId++;
        var co = StartCoroutine(RemoveLineAfterDelay(id));
        entries.Insert(0, new LineEntry { id = id, text = message, coroutine = co });

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (statusText == null) return;
        try
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(entries[i].text);
            }
            statusText.text = sb.ToString();
        }
        catch (System.Exception) { }
    }

    private IEnumerator RemoveLineAfterDelay(int id)
    {
        yield return new WaitForSeconds(clearDelay);

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].id == id)
            {
                entries.RemoveAt(i);
                break;
            }
        }
        RefreshDisplay();
    }
}
