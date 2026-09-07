using System;
using KartGame.AI;
using UnityEngine;

/// <summary>
/// Per-kart component that validates forward checkpoint traversal and counts completed laps.
/// Requires forward progress around the TrackCheckpoints ring since the last valid lap, tolerating
/// a small forward skip so a single missed trigger (e.g. a boosted kart tunneling through a thin
/// checkpoint gate in one physics step) does not permanently desync the kart. The finish-line
/// crossing is latched so it can be evaluated against whichever event — checkpoint or finish-line
/// trigger — arrives second. Large backward jumps are ignored and do not reset progress.
/// </summary>
public class LapTracker : MonoBehaviour
{
    private const float FinishSoundVolumeScale = 1.3f;

    // Guards against a single missed checkpoint trigger permanently desyncing a kart: the 25
    // checkpoint gates are thin (~1 unit) trigger volumes, and a boosted kart can tunnel through
    // one within a single physics step without ever firing OnTriggerEnter for it. Accepting a
    // small forward skip (instead of requiring an exact index match) lets the kart recover on the
    // very next checkpoint instead of getting stuck forever. Half the ring length still rejects
    // large jumps, which mainly come from driving backward across a checkpoint (its forward
    // distance wraps almost all the way around the ring) rather than a genuine missed trigger.
    private const int MaxForwardCheckpointSkip = 12;

    [Tooltip("Display name used in race results logging.")]
    public string racerName;

    [Tooltip("Layer mask for checkpoint colliders. Must include both the training checkpoint ring layer " +
             "and the legacy checkpoint layer.")]
    public LayerMask checkpointMask;

    [Tooltip("Ordered checkpoint ring used to validate traversal. Optional — resolved from RaceManager " +
             "when left unassigned.")]
    [SerializeField] private TrackCheckpoints trackCheckpoints;

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

    /// <summary>
    /// Monotonic progress metric: total checkpoints passed across the whole race, never reset per lap.
    /// Higher is further ahead. 25x finer than the old lap-and-half-lap bucketed score.
    /// </summary>
    public int ProgressScore => m_CheckpointsPassed;

    /// <summary>Index of the next checkpoint this kart must cross. Used by RaceManager for the distance tiebreak.</summary>
    public int NextCheckpointIndex => m_NextCheckpointIndex;

    /// <summary>Number of checkpoints hit since the last valid lap. Useful for wrong-way/shortcut diagnostics.</summary>
    public int CheckpointsHitThisLap => m_CheckpointsHitThisLap;

    /// <summary>Fired with the new lap count each time a lap is completed.</summary>
    public event Action<int> OnLapCompleted;

    /// <summary>Fired when all required laps are done.</summary>
    public event Action OnRaceFinished;

    private int m_LapsCompleted;
    private bool m_HasFinished;
    private int m_NextCheckpointIndex;
    private int m_CheckpointsPassed;
    private int m_CheckpointsHitThisLap;
    private bool m_FinishLinePending;
    private bool m_LoggedMissingTrackCheckpoints;

    void Awake()
    {
        // Must not read RaceManager.Instance here — the singleton may not exist yet.
        // Real initialization of m_NextCheckpointIndex happens in ResetProgress().
        m_LapsCompleted = 0;
        m_HasFinished = false;
        m_CheckpointsPassed = 0;
        m_CheckpointsHitThisLap = 0;
        m_FinishLinePending = false;
    }

    /// <summary>
    /// Resets all race progress state. Called by RaceManager.RegisterAllActiveRacers() for every
    /// registered tracker so each kart starts a race from a known checkpoint index.
    /// </summary>
    public void ResetProgress()
    {
        m_LapsCompleted = 0;
        m_HasFinished = false;
        m_CheckpointsPassed = 0;
        m_CheckpointsHitThisLap = 0;
        m_FinishLinePending = false;

        if (trackCheckpoints == null && RaceManager.Instance != null)
        {
            trackCheckpoints = RaceManager.Instance.TrackCheckpoints;
        }

        m_NextCheckpointIndex = RaceManager.Instance != null ? RaceManager.Instance.StartCheckpointIndex : 0;
    }

    void OnTriggerEnter(Collider other)
    {
        if (m_HasFinished) return;
        if (RaceManager.Instance == null) return;
        if (((1 << other.gameObject.layer) & checkpointMask.value) == 0) return;

        if (other.CompareTag(Tags.CheckpointFinishLine))
        {
            m_FinishLinePending = true;
            if (trackCheckpoints != null && trackCheckpoints.VerboseLogging)
            {
                Debug.Log($"[LapTracker] {racerName} crossed the finish-line trigger (checkpoints hit this lap: {m_CheckpointsHitThisLap}).", this);
            }

            TryCompleteLap();
            return;
        }

        if (trackCheckpoints == null)
        {
            if (!m_LoggedMissingTrackCheckpoints)
            {
                Debug.LogError("[LapTracker] No TrackCheckpoints assigned or resolved — checkpoint progress is disabled; race runs on the finish-line gate alone.", this);
                m_LoggedMissingTrackCheckpoints = true;
            }

            return;
        }

        if (trackCheckpoints.Count == 0) return;

        if (trackCheckpoints.TryGetIndex(other, out int index))
        {
            HandleCheckpointCrossed(index);
        }
        // Else: unrecognized trigger (e.g. the now-decorative Checkpoint-HalfwayPoint) — ignore silently.
    }

    /// <summary>
    /// Accepts a checkpoint crossing when it represents forward progress along the ring — either
    /// the strictly expected next index, or a small forward skip that tolerates a single missed
    /// trigger (see MaxForwardCheckpointSkip). Large jumps, which are almost always a kart driving
    /// backward across a checkpoint rather than a genuine miss, are rejected. Progress is credited
    /// for every checkpoint in the skipped span, so a lap still requires the ring's full length of
    /// forward travel even when a trigger was missed.
    /// Note: KartAgent's inference-recovery teleport (LateUpdate) re-triggers the checkpoint the
    /// kart was teleported onto, which lands here as either a zero-skip re-accept or a rejected
    /// backward jump depending on how far along the kart already was — both are fine; this is
    /// intentional self-healing, not a bug to "fix".
    /// </summary>
    private void HandleCheckpointCrossed(int index)
    {
        int count = trackCheckpoints.Count;
        if (count == 0) return;

        int forwardSkip = ((index - m_NextCheckpointIndex) % count + count) % count;
        if (forwardSkip > MaxForwardCheckpointSkip) return;

        int checkpointsGained = forwardSkip + 1;
        m_CheckpointsPassed += checkpointsGained;
        m_CheckpointsHitThisLap += checkpointsGained;
        m_NextCheckpointIndex = trackCheckpoints.NextIndex(index);

        if (trackCheckpoints.VerboseLogging)
        {
            Debug.Log($"[LapTracker] {racerName} passed checkpoint {index} (total: {m_CheckpointsPassed}, skipped: {forwardSkip}).", this);
        }

        TryCompleteLap();
    }

    /// <summary>
    /// Completes the lap only once both the finish-line crossing is latched and all checkpoints
    /// in the ring have been hit since the last valid lap. Evaluated from both the finish-line
    /// and checkpoint handlers so it fires on whichever event arrives second.
    /// </summary>
    private void TryCompleteLap()
    {
        if (trackCheckpoints == null) return;
        if (!m_FinishLinePending || m_CheckpointsHitThisLap < trackCheckpoints.Count) return;

        m_FinishLinePending = false;
        m_CheckpointsHitThisLap = 0;
        CompleteLap();
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
    /// Hard-stops any pending/current cue on the shared non-spatial finish source, then plays
    /// the finish clip at a boosted volume via PlayOneShot, which is not clamped to the
    /// AudioSource's own 0-1 volume ceiling. This source is shared with UI menu cues, which
    /// are unaffected since PlayOneShot's volume scale only applies to this single call.
    /// Only fires for player-prefab trackers.
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
        finishSfxSource.PlayOneShot(finishClip, FinishSoundVolumeScale);
    }
}
