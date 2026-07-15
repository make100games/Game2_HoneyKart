using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level singleton that owns the configurable TotalLaps value and tracks race completion order.
/// Racers are registered by RaceState via RegisterAllActiveRacers() when the race hierarchy activates.
/// </summary>
public class RaceManager : MonoBehaviour
{
    [Tooltip("Total number of laps required to finish the race.")]
    public int TotalLaps = 3;

    [Tooltip("Index into the checkpoints array that acts as the start/finish line. " +
             "Set this to the first checkpoint the karts cross after the starting grid. " +
             "A lap is counted each time a kart passes this checkpoint after completing a full circuit.")]
    public int StartCheckpointIndex = 0;

    [Tooltip("Transform of the halfway checkpoint trigger. Used for live position distance tiebreaking.")]
    public Transform halfwayCheckpoint;

    [Tooltip("Transform of the finish line checkpoint trigger. Used for live position distance tiebreaking.")]
    public Transform finishLineCheckpoint;

    /// <summary>Singleton accessor set in Awake.</summary>
    public static RaceManager Instance => s_Instance;

    private static RaceManager s_Instance;

    private List<LapTracker> m_Racers;
    private List<LapTracker> m_FinishOrder;

    void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Debug.LogError("[RaceManager] Duplicate singleton detected. Destroying this instance.", this);
            Destroy(this);
            return;
        }

        s_Instance = this;
        m_Racers = new List<LapTracker>();
        m_FinishOrder = new List<LapTracker>();
    }

    void OnDestroy()
    {
        if (s_Instance == this)
        {
            s_Instance = null;
        }
    }

    /// <summary>
    /// Clears the current racer list and registers every currently active LapTracker in the scene.
    /// Called by RaceState once the race hierarchy is activated, so opponents and the runtime-selected
    /// player kart are all active (and non-selected player kart candidates are correctly excluded).
    /// </summary>
    public void RegisterAllActiveRacers()
    {
        m_Racers.Clear();

        LapTracker[] activeTrackers = FindObjectsByType<LapTracker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < activeTrackers.Length; i++)
        {
            RegisterInternal(activeTrackers[i]);
        }

        Debug.Log($"[RaceManager] Registered {m_Racers.Count} racers.");
    }

    private void RegisterInternal(LapTracker tracker)
    {
        if (m_Racers.Contains(tracker)) return;
        m_Racers.Add(tracker);
    }

    /// <summary>Called by LapTracker when a lap is completed.</summary>
    public void OnLapCompleted(LapTracker tracker, int lapCount)
    {
        Debug.Log($"[RaceManager] {tracker.racerName} completed lap {lapCount}/{TotalLaps}");
    }

    /// <summary>
    /// Returns the 1-based finishing position of the given tracker.
    /// Returns 1 as a safe fallback if the singleton is not yet available or the tracker is not in the finish order.
    /// </summary>
    public static int GetFinishPosition(LapTracker tracker)
    {
        if (s_Instance == null)
        {
            Debug.LogWarning("[RaceManager] GetFinishPosition called but no RaceManager instance exists.");
            return 1;
        }

        int index = s_Instance.m_FinishOrder.IndexOf(tracker);
        Debug.Log("Finish position (index) of " + tracker.racerName + ": " + index);
        return index >= 0 ? index + 1 : 1;
    }

    /// <summary>Called by LapTracker when a racer finishes the race. Records finish order and logs results.</summary>
    public void OnRaceFinished(LapTracker tracker)
    {
        if (m_FinishOrder.Contains(tracker)) return;

        m_FinishOrder.Add(tracker);

        for(int i = 0; i < m_FinishOrder.Count; i++) {
            var t = m_FinishOrder[i];
            Debug.Log("Racer at position " + i + ": " + t.racerName);
        }

        int position = m_FinishOrder.Count;

        if (position == 1)
        {
            Debug.Log($"[RaceManager] {tracker.racerName} wins the race!");
        }
        else
        {
            Debug.Log($"[RaceManager] {tracker.racerName} finished in position {position}");
        }
    }

    /// <summary>
    /// Returns the 1-based live race position of the given tracker among all registered racers.
    /// Uses a hybrid ranking: sort by ProgressScore, then break ties by ascending distance to
    /// the racer's next checkpoint. O(n) with no allocations.
    /// Returns 1 as a safe fallback if the singleton is unavailable or the tracker is not registered.
    /// </summary>
    public int GetLivePosition(LapTracker tracker)
    {
        if (tracker == null || !m_Racers.Contains(tracker))
        {
            Debug.LogWarning("[RaceManager] GetLivePosition called with an unregistered or null tracker.");
            return 1;
        }

        int trackerScore = tracker.ProgressScore;
        float trackerDist = DistanceToNextCheckpoint(tracker);

        int aheadCount = 0;
        for (int i = 0; i < m_Racers.Count; i++)
        {
            LapTracker r = m_Racers[i];
            if (r == tracker) continue;

            int rScore = r.ProgressScore;
            if (rScore > trackerScore)
            {
                aheadCount++;
            }
            else if (rScore == trackerScore && DistanceToNextCheckpoint(r) < trackerDist)
            {
                aheadCount++;
            }
        }

        return aheadCount + 1;
    }

    /// <summary>
    /// Returns the world-space distance from the racer to its next checkpoint.
    /// Falls back to float.MaxValue if the relevant checkpoint Transform is not assigned.
    /// </summary>
    private float DistanceToNextCheckpoint(LapTracker r)
    {
        Transform checkpoint = r.NextCheckpointTag == Tags.CheckpointFinishLine
            ? finishLineCheckpoint
            : halfwayCheckpoint;

        if (checkpoint == null)
        {
            Debug.LogWarning("[RaceManager] A checkpoint Transform reference is null — distance tiebreaking will be skipped for this racer.");
            return float.MaxValue;
        }

        return Vector3.Distance(r.transform.position, checkpoint.position);
    }
}
