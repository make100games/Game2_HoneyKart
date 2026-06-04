using System;
using KartGame.AI;
using UnityEngine;

/// <summary>
/// Per-kart component that validates sequential checkpoint traversal and counts completed laps.
/// Requires every checkpoint to be hit in index order (0 → 1 → … → N-1 → 0). Any out-of-order
/// or skipped trigger is silently ignored.
/// </summary>
public class LapTracker : MonoBehaviour
{
    [Tooltip("Display name used in race results logging.")]
    public string racerName;

    [Tooltip("Ordered checkpoint Colliders. Leave empty to auto-populate from KartAgent.Colliders at Awake.")]
    public Collider[] checkpoints;

    [Tooltip("Layer mask for checkpoint colliders. Must include the layer the checkpoints are on.")]
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

    void Awake()
    {
        if (checkpoints == null || checkpoints.Length == 0)
        {
            var agent = GetComponent<KartAgent>();
            if (agent != null)
                checkpoints = agent.Colliders;
        }

        m_NextExpectedCheckpointIndex = 0;
        m_RaceStarted = false;
        m_LapsCompleted = 0;
        m_HasFinished = false;
    }

    void Start()
    {
        RaceManager.Register(this);

        m_NextExpectedCheckpointIndex = RaceManager.Instance != null
            ? RaceManager.Instance.StartCheckpointIndex
            : 0;
    }

    void OnTriggerEnter(Collider other)
    {
        if (m_HasFinished) return;
        if (RaceManager.Instance == null) return;
        if (checkpoints == null || checkpoints.Length == 0) return;
        if (((1 << other.gameObject.layer) & checkpointMask.value) == 0) return;

        int index = FindCheckpointIndex(other);
        if (index < 0 || index != m_NextExpectedCheckpointIndex) return;

        m_NextExpectedCheckpointIndex = (index + 1) % checkpoints.Length;

        int startIndex = RaceManager.Instance.StartCheckpointIndex;
        if (index == startIndex)
        {
            if (m_RaceStarted)
                CompleteLap();

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
            OnRaceFinished?.Invoke();
            RaceManager.Instance.OnRaceFinished(this);
        }
    }

    /// <summary>
    /// Searches the checkpoints array for the given collider by instance ID.
    /// Returns the index or -1 if not found.
    /// </summary>
    private int FindCheckpointIndex(Collider other)
    {
        int id = other.GetInstanceID();
        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] != null && checkpoints[i].GetInstanceID() == id)
                return i;
        }
        return -1;
    }
}
