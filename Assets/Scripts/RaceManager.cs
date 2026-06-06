using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level singleton that owns the configurable TotalLaps value and tracks race completion order.
/// All LapTracker instances register themselves here during Start.
/// </summary>
public class RaceManager : MonoBehaviour
{
    [Tooltip("Total number of laps required to finish the race.")]
    public int TotalLaps = 3;

    [Tooltip("Index into the checkpoints array that acts as the start/finish line. " +
             "Set this to the first checkpoint the karts cross after the starting grid. " +
             "A lap is counted each time a kart passes this checkpoint after completing a full circuit.")]
    public int StartCheckpointIndex = 0;

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

    /// <summary>Called by each LapTracker.Start() to register with the race.</summary>
    public static void Register(LapTracker tracker)
    {
        if (s_Instance == null)
        {
            Debug.LogWarning("[RaceManager] Register called but no RaceManager instance exists in the scene.");
            return;
        }

        s_Instance.RegisterInternal(tracker);
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
        return index >= 0 ? index + 1 : 1;
    }

    /// <summary>Called by LapTracker when a racer finishes the race. Records finish order and logs results.</summary>
    public void OnRaceFinished(LapTracker tracker)
    {
        if (m_FinishOrder.Contains(tracker)) return;

        m_FinishOrder.Add(tracker);
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
}
