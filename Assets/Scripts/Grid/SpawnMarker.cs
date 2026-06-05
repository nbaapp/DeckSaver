using UnityEngine;

/// <summary>Which side a SpawnMarker designates a slot for.</summary>
public enum SpawnMarkerKind
{
    Player,
    Enemy,
}

/// <summary>
/// Placed as a child of a painted encounter map prefab. The marker's transform
/// position resolves to a grid cell at battle start; its kind + slotIndex tell
/// the spawner what to put there.
///
/// Player markers are filled in slotIndex order from the run's surviving units.
/// Enemy markers are filled in slotIndex order from EncounterDefinition.enemyRoster.
/// Markers without a matching roster entry are ignored (left empty).
/// </summary>
public class SpawnMarker : MonoBehaviour
{
    public SpawnMarkerKind kind;

    [Tooltip("Index that maps this marker to a roster entry. Lowest first.")]
    public int slotIndex;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = kind == SpawnMarkerKind.Player
            ? new Color(0.4f, 0.8f, 1f, 0.9f)
            : new Color(1f, 0.4f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, $"{kind} {slotIndex}");
    }
#endif
}
