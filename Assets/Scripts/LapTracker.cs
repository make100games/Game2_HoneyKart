using System;
using KartGame.AI;
using UnityEngine;

/// <summary>
/// Per-kart component that validates sequential checkpoint traversal and counts completed laps.
/// Uses FixedUpdate polling (local-space point-in-box test) instead of OnTriggerEnter so it is
/// unaffected by the Physics Layer Collision Matrix or compound-collider trigger routing.
/// Requires every checkpoint to be hit in index order (0 → 1 → … → 24 → 0). Any out-of-order
/// or skipped visit is silently ignored.
/// </summary>
public class LapTracker : MonoBehaviour
{
    [Tooltip("Display name used in race results logging.")]
    public string racerName;

    [Tooltip("Ordered checkpoint BoxColliders. Leave empty to auto-populate from KartAgent.Colliders at Awake.")]
    public Collider[] checkpoints;

    [Tooltip("Unused at runtime — kept for Inspector reference only. Detection is via FixedUpdate polling.")]
    public LayerMask checkpointMask;

    /// <summary>Current number of completed laps.</summary>
    public int LapsCompleted => m_LapsCompleted;

    /// <summary>True once all required laps (per RaceManager.TotalLaps) are done.</summary>
    public bool HasFinished => m_HasFinished;

    /// <summary>Fired with the new lap count each time a lap is completed.</summary>
    public event Action<int> OnLapCompleted;

    /// <summary>Fired when all required laps are done.</summary>
    public event Action OnRaceFinished;

    private int m_LapsCompleted;
    private bool m_HasFinished;
    private bool m_RaceStarted;
    private int m_NextExpectedCheckpointIndex;
    private bool m_InsideNextCheckpoint;

    void Awake()
    {
        Debug.LogWarning($"[LapTracker] AWAKE — '{racerName}' on '{gameObject.name}'", this);

        if (checkpoints == null || checkpoints.Length == 0)
        {
            var agent = GetComponent<KartAgent>();
            if (agent != null)
            {
                checkpoints = agent.Colliders;
            }
        }

        if (checkpoints == null || checkpoints.Length == 0)
        {
            Debug.LogWarning($"[LapTracker] '{racerName}': No checkpoints assigned and KartAgent auto-population failed.", this);
        }

        m_NextExpectedCheckpointIndex = 0;
        m_RaceStarted = false;
        m_LapsCompleted = 0;
        m_HasFinished = false;
        m_InsideNextCheckpoint = false;
    }

    void Start()
    {
        Debug.LogWarning($"[LapTracker] START — '{racerName}', checkpoints={checkpoints?.Length ?? 0}, RaceManager={(RaceManager.Instance != null ? "found" : "NULL")}", this);
        RaceManager.Register(this);
    }

    void FixedUpdate()
    {
        if (m_HasFinished) return;
        if (RaceManager.Instance == null) return;
        if (checkpoints == null || checkpoints.Length == 0) return;

        var nextCollider = checkpoints[m_NextExpectedCheckpointIndex] as BoxCollider;
        if (nextCollider == null) return;

        bool isInside = IsInsideBoxCollider(nextCollider, transform.position);

        if (isInside && !m_InsideNextCheckpoint)
        {
            m_InsideNextCheckpoint = true;
            ProcessCheckpointReached(m_NextExpectedCheckpointIndex);
        }
        else if (!isInside && m_InsideNextCheckpoint)
        {
            m_InsideNextCheckpoint = false;
        }
    }

    private void ProcessCheckpointReached(int index)
    {
        // Advance to the next expected checkpoint before any callbacks fire.
        m_NextExpectedCheckpointIndex = (index + 1) % checkpoints.Length;
        // Reset so the (now different) next checkpoint starts fresh.
        m_InsideNextCheckpoint = false;

        if (index == 0)
        {
            if (m_RaceStarted)
            {
                CompleteLap();
            }
            m_RaceStarted = true;
        }
    }

    private void CompleteLap()
    {
        m_LapsCompleted++;
        RaceManager.Instance.OnLapCompleted(this, m_LapsCompleted);
        OnLapCompleted?.Invoke(m_LapsCompleted);

        if (m_LapsCompleted >= RaceManager.Instance.TotalLaps)
        {
            m_HasFinished = true;
            Debug.Log($"[LapTracker] '{racerName}': Race finished. Completed all {RaceManager.Instance.TotalLaps} laps.");
            OnRaceFinished?.Invoke();
            RaceManager.Instance.OnRaceFinished(this);
        }
    }

    /// <summary>
    /// Returns true if worldPoint is inside box, accounting for the collider's rotation and scale.
    /// Uses InverseTransformPoint so rotated gates work correctly.
    /// </summary>
    private static bool IsInsideBoxCollider(BoxCollider box, Vector3 worldPoint)
    {
        // Transform world point into the box's local space (handles position, rotation, scale).
        Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 halfExtents = box.size * 0.5f;
        return Mathf.Abs(localPoint.x) <= halfExtents.x &&
               Mathf.Abs(localPoint.y) <= halfExtents.y &&
               Mathf.Abs(localPoint.z) <= halfExtents.z;
    }
}
