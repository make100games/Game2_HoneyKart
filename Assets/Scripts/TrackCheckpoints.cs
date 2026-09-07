using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative ordered checkpoint ring used for race position tracking.
/// Independent from KartAgent.Colliders/CheckpointMask (used for ML-Agents training) but walks
/// the same 25 checkpoint colliders in the same sibling order, so both systems stay naturally in sync.
/// </summary>
public class TrackCheckpoints : MonoBehaviour
{
    [Tooltip("Optional explicit ordered checkpoint colliders. Leave empty to build the order from this " +
             "GameObject's child sibling order instead.")]
    [SerializeField] private Collider[] orderedCheckpoints;

    [Tooltip("Enables per-trigger diagnostic logging for checkpoint and finish-line crossings.")]
    [SerializeField] private bool verboseLogging;

    /// <summary>Number of checkpoints in the ordered ring. Zero if initialization failed.</summary>
    public int Count => m_Ordered?.Length ?? 0;

    /// <summary>True when per-trigger diagnostic logging is enabled.</summary>
    public bool VerboseLogging => verboseLogging;

    private Collider[] m_Ordered;
    private Dictionary<int, int> m_ColliderIdToIndex;
    private bool m_Initialized;

    void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Builds the ordered checkpoint list and the collider-instance-ID-to-index lookup.
    /// Idempotent — safe to call multiple times; only the first call does any work.
    /// </summary>
    public void EnsureInitialized()
    {
        if (m_Initialized) return;
        m_Initialized = true;

        if (orderedCheckpoints != null && orderedCheckpoints.Length > 0)
        {
            m_Ordered = orderedCheckpoints;
        }
        else
        {
            var built = new List<Collider>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                Collider collider = child.GetComponent<Collider>();
                if (collider == null)
                {
                    Debug.LogWarning($"[TrackCheckpoints] Child '{child.name}' has no Collider — skipping.", child);
                    continue;
                }

                built.Add(collider);
            }

            m_Ordered = built.ToArray();
        }

        m_ColliderIdToIndex = new Dictionary<int, int>(m_Ordered.Length);
        for (int i = 0; i < m_Ordered.Length; i++)
        {
            Collider collider = m_Ordered[i];
            if (collider == null) continue;

            int id = collider.GetInstanceID();
            if (m_ColliderIdToIndex.ContainsKey(id))
            {
                Debug.LogWarning($"[TrackCheckpoints] Duplicate collider '{collider.name}' in ordered list — keeping first index.", collider);
                continue;
            }

            m_ColliderIdToIndex[id] = i;
        }

        if (m_Ordered.Length == 0)
        {
            Debug.LogError("[TrackCheckpoints] Resolved zero checkpoints — race position ranking will fall back to lap count only.", this);
        }
        else
        {
            Debug.Log($"[TrackCheckpoints] Initialized with {m_Ordered.Length} checkpoints.", this);
        }
    }

    /// <summary>
    /// Attempts to resolve a checkpoint collider to its ordinal index in the ring.
    /// Returns false with index -1 for unknown colliders (expected on the hot path — e.g. the
    /// finish-line trigger is not part of this ring).
    /// </summary>
    public bool TryGetIndex(Collider checkpointCollider, out int index)
    {
        if (checkpointCollider != null && m_ColliderIdToIndex != null &&
            m_ColliderIdToIndex.TryGetValue(checkpointCollider.GetInstanceID(), out index))
        {
            return true;
        }

        index = -1;
        return false;
    }

    /// <summary>Returns the index following the given index, wrapping around the ring.</summary>
    public int NextIndex(int index)
    {
        if (Count == 0) return 0;
        return (index + 1) % Count;
    }

    /// <summary>Returns the Transform for the checkpoint at the given index, or null if out of range.</summary>
    public Transform GetCheckpointTransform(int index)
    {
        if (m_Ordered == null || index < 0 || index >= m_Ordered.Length) return null;
        Collider collider = m_Ordered[index];
        return collider != null ? collider.transform : null;
    }
}
