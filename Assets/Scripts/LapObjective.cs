using System.Collections;
using KartGame.Track;
using UnityEngine;

/// <summary>
/// Replaces ObjectiveCompleteLaps on GameManager. Subscribes to the player LapTracker.OnLapCompleted
/// event and drives the existing HUD and GameFlowManager win condition via the Objective base class lifecycle.
/// </summary>
public class LapObjective : Objective
{
    [Tooltip("Reference to the player kart's LapTracker component.")]
    public LapTracker playerTracker;

    IEnumerator Start()
    {
        if (playerTracker == null)
        {
            Debug.LogWarning("[LapObjective] playerTracker is not assigned. Objective will not function.", this);
            yield break;
        }

        if (RaceManager.Instance == null)
        {
            Debug.LogWarning("[LapObjective] RaceManager.Instance is null. Objective will not function.", this);
            yield break;
        }

        if (string.IsNullOrEmpty(title))
        {
            title = $"Complete {RaceManager.Instance.TotalLaps} laps";
        }

        //TimeDisplay.OnSetLaps(RaceManager.Instance.TotalLaps);

        // One-frame delay required before Register() so ObjectiveManager and HUD managers are ready.
        yield return new WaitForEndOfFrame();

        Register();
        playerTracker.OnLapCompleted += HandleLapCompleted;
    }

    void OnDestroy()
    {
        if (playerTracker != null)
        {
            playerTracker.OnLapCompleted -= HandleLapCompleted;
        }
    }

    private void HandleLapCompleted(int lapCount)
    {
        //TimeDisplay.OnUpdateLap();
        ReachCheckpoint(0);
    }

    protected override void ReachCheckpoint(int remaining)
    {
        if (isCompleted) return;

        int lapsRemaining = RaceManager.Instance.TotalLaps - playerTracker.LapsCompleted;

        if (lapsRemaining <= 0)
        {
            CompleteObjective(string.Empty, GetUpdatedCounterAmount(), "Objective complete: " + title);
        }
        else
        {
            string notificationText = lapsRemaining == 1 ? "One lap left" : string.Empty;
            UpdateObjective(string.Empty, GetUpdatedCounterAmount(), notificationText);
        }
    }

    /// <summary>Returns the current lap counter string for HUD display.</summary>
    public override string GetUpdatedCounterAmount()
    {
        if (playerTracker == null || RaceManager.Instance == null) return string.Empty;
        return $"{playerTracker.LapsCompleted} / {RaceManager.Instance.TotalLaps}";
    }
}
