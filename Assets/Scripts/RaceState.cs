using System.Collections;
using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Race state — activates the GameManager and RaceRoot hierarchy, subscribes to the race
/// finished event, and forwards completion to GameStateManager with a one-frame defer.
/// </summary>
public class RaceState : GameStateBase
{
    /// <summary>
    /// Activates the race hierarchy (triggering deferred Awake/Start on GameFlowManager, etc.)
    /// and subscribes to the race finished event.
    /// </summary>
    public override void Enter()
    {
        gameObject.SetActive(true);
        GameFlowManager.OnRaceFinished += HandleRaceFinished;
    }

    /// <summary>
    /// Unsubscribes from the race finished event and deactivates the entire race hierarchy.
    /// </summary>
    public override void Exit()
    {
        GameFlowManager.OnRaceFinished -= HandleRaceFinished;
        gameObject.SetActive(false);
    }

    private void HandleRaceFinished(ArcadeKart kart)
    {
        // Unsubscribe immediately to prevent double-fire.
        GameFlowManager.OnRaceFinished -= HandleRaceFinished;
        StartCoroutine(TransitionToResultsRoutine(kart));
    }

    /// <summary>
    /// Defers CompleteRace by one frame so the OnRaceFinished event dispatch chain on
    /// GameFlowManager finishes before its GameObject is deactivated.
    /// </summary>
    private IEnumerator TransitionToResultsRoutine(ArcadeKart kart)
    {
        yield return null;
        GameStateManager.Instance.CompleteRace(kart);
    }
}
