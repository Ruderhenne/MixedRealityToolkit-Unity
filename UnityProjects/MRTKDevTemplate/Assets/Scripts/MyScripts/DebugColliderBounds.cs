using UnityEngine;

/// <summary>
/// Temporäres Debug-Script: Listet ALLE Collider in der gesamten
/// DashboardWindow-Hierarchie auf, um versteckte Treffflächen zu finden.
/// </summary>
public class DebugColliderBounds : MonoBehaviour
{
    void Start()
    {
        // Finde das oberste Parent (DashboardWindow)
        Transform root = transform;
        while (root.parent != null)
        {
            root = root.parent;
        }

        Debug.Log($"[DebugCollider] === Scanning all colliders under '{root.name}' ===");

        Collider[] allColliders = root.GetComponentsInChildren<Collider>(true);
        Debug.Log($"[DebugCollider] Found {allColliders.Length} collider(s) total.");

        foreach (var col in allColliders)
        {
            string type = col.GetType().Name;
            bool active = col.enabled && col.gameObject.activeInHierarchy;
            Vector3 worldSize = Vector3.zero;

            if (col is BoxCollider box)
            {
                worldSize = Vector3.Scale(box.size, col.transform.lossyScale);
            }
            else if (col is MeshCollider mesh)
            {
                worldSize = mesh.bounds.size;
            }

            // Prüfe ob ein Interactable auf diesem oder einem Parent-Objekt sitzt
            var interactable = col.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable>();
            string interactableName = interactable != null ? interactable.gameObject.name : "NONE";

            Debug.Log($"[DebugCollider] {(active ? "ACTIVE" : "INACTIVE")} " +
                      $"'{col.gameObject.name}' ({type}) " +
                      $"WorldSize={worldSize} " +
                      $"Interactable->'{interactableName}' " +
                      $"Path={GetPath(col.transform)}");
        }

        Debug.Log($"[DebugCollider] === Scan complete ===");
    }

    private string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(col.center, col.size);
    }
}
