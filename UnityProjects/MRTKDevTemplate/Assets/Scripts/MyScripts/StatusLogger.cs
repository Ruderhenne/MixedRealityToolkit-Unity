using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Central status logger for the dashboard InfoSection.
/// Messages are displayed line-by-line (newest on top) and each line
/// is removed individually after a configurable delay.
/// Limited to a maximum number of visible lines.
/// </summary>
public class StatusLogger : MonoBehaviour
{
    public static StatusLogger Instance { get; private set; }

    [SerializeField] private TMP_Text statusText;
    [SerializeField] private float clearDelay = 5f;
    [SerializeField] private int maxLines = 3;

    // Each line entry with its own removal coroutine
    private readonly List<string> lines = new List<string>();
    private readonly List<Coroutine> lineCoroutines = new List<Coroutine>();

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

    /// <summary>
    /// Adds a message as the topmost line in the InfoSection.
    /// The line is automatically removed after <see cref="clearDelay"/> seconds.
    /// If the maximum number of lines is reached, the oldest line is removed immediately.
    /// </summary>
    public static void Log(string message)
    {
        Debug.Log($"[StatusLogger] {message}");

        if (Instance != null && Instance.statusText != null)
        {
            try
            {
                // Remove oldest line if limit is reached
                while (Instance.lines.Count >= Instance.maxLines)
                {
                    int last = Instance.lines.Count - 1;
                    Instance.StopCoroutine(Instance.lineCoroutines[last]);
                    Instance.lines.RemoveAt(last);
                    Instance.lineCoroutines.RemoveAt(last);
                }

                // Insert new line at the top
                Instance.lines.Insert(0, message);

                // Start a coroutine that removes this specific line after the delay
                var co = Instance.StartCoroutine(Instance.RemoveLineAfterDelay(message));
                Instance.lineCoroutines.Insert(0, co);

                Instance.RefreshDisplay();
            }
            catch (System.Exception) { }
        }
    }

    private void RefreshDisplay()
    {
        if (statusText != null)
        {
            try
            {
                statusText.text = string.Join("\n", lines.ToArray());
            }
            catch (System.Exception) { }
        }
    }

    private IEnumerator RemoveLineAfterDelay(string message)
    {
        yield return new WaitForSeconds(clearDelay);

        int index = lines.LastIndexOf(message);
        if (index >= 0)
        {
            lines.RemoveAt(index);
            lineCoroutines.RemoveAt(index);
            RefreshDisplay();
        }
    }
}
