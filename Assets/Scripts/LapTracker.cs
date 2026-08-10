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

    [Tooltip("Layer mask for checkpoint colliders. Must include the layer the checkpoints are on.")]
    public LayerMask checkpointMask;

    [Header("Finish Sound Effect")]
    [Tooltip("Only true on player-controlled kart prefabs — prevents AI finishes from playing the human finish cue.")]
    [SerializeField] private bool playFinishSound;

    [Tooltip("Non-spatial AudioSource used for the finish cue. Assign the shared root-level 2D source.")]
    [SerializeField] private AudioSource finishSfxSource;

    [Tooltip("Sound effect played when this tracker completes its final required lap.")]
    [SerializeField] private AudioClip finishClip;

    /// <summary>Current number of completed laps.</summary>
    public int LapsCompleted => m_LapsCompleted;

    /// <summary>True once all required laps (per RaceManager.TotalLaps) are done.</summary>
    public bool HasFinished => m_HasFinished;

    /// <summary>Monotonic progress metric: laps * 2 + 1 if next checkpoint is the finish line, else 0.</summary>
    public int ProgressScore => m_LapsCompleted * 2 + (m_tagOfNextCheckpoint == Tags.CheckpointFinishLine ? 1 : 0);

    /// <summary>Tag of the next checkpoint this kart must cross. Used by RaceManager for distance-based tiebreaking.</summary>
    public string NextCheckpointTag => m_tagOfNextCheckpoint;

    /// <summary>Fired with the new lap count each time a lap is completed.</summary>
    public event Action<int> OnLapCompleted;

    /// <summary>Fired when all required laps are done.</summary>
    public event Action OnRaceFinished;

    private int m_LapsCompleted;
    private bool m_HasFinished;
    private string m_tagOfNextCheckpoint;

    void Awake()
    {
        m_tagOfNextCheckpoint = Tags.CheckpointHalfwayPoint;
        m_LapsCompleted = 0;
        m_HasFinished = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (m_HasFinished) return;
        if (RaceManager.Instance == null) return;
        if (((1 << other.gameObject.layer) & checkpointMask.value) == 0) return;

        var collidedCheckpointTag = other.gameObject.tag;
        Debug.Log("Player collided with checkpoint: " + collidedCheckpointTag);
        if(collidedCheckpointTag != m_tagOfNextCheckpoint) return;
        if(collidedCheckpointTag == Tags.CheckpointHalfwayPoint) {
            m_tagOfNextCheckpoint = Tags.CheckpointFinishLine;
        } else {
            m_tagOfNextCheckpoint = Tags.CheckpointHalfwayPoint;
            CompleteLap();
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
            PlayFinishSound();
            OnRaceFinished?.Invoke();
            RaceManager.Instance.OnRaceFinished(this);
        }
    }

    /// <summary>
    /// Hard-stops any pending/current cue on the shared non-spatial finish source, then
    /// plays the finish clip from sample zero. Only fires for player-prefab trackers.
    /// </summary>
    private void PlayFinishSound()
    {
        if (!playFinishSound)
            return;

        if (finishSfxSource == null || finishClip == null)
        {
            Debug.LogWarning("[LapTracker] finishSfxSource or finishClip is unassigned — skipping finish sound.", this);
            return;
        }

        finishSfxSource.Stop();
        finishSfxSource.clip = finishClip;
        finishSfxSource.time = 0f;
        finishSfxSource.Play();
    }
}
