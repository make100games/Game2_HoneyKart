using System.Collections;
using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Race state — activates the GameManager and RaceRoot hierarchy, subscribes to the race
/// finished event, and forwards completion to GameStateManager with a one-frame defer.
/// </summary>
public class RaceState : GameStateBase
{
    [Tooltip("Manages AI kart spawn positions; ResetToSpawn() is called on every race entry.")]
    [SerializeField] private OpponentSpawnManager opponentSpawnManager;

    [Tooltip("GameFlowManager component on the GameManager child — assigned playerKart before Start() fires.")]
    [SerializeField] private GameFlowManager gameFlowManager;

    [Tooltip("One player kart per character (Brutus=0, Elvis=1, HoneyBee=2, Squirrel=3). All must start inactive in the Editor.")]
    [SerializeField] private ArcadeKart[] playerKartOptions;

    /// <summary>
    /// Activates the selected player kart, assigns it to GameFlowManager before Start() fires,
    /// resets AI karts to spawn, activates the race hierarchy, and subscribes to the race finished event.
    /// </summary>
    public override void Enter()
    {
        if (gameFlowManager == null)
        {
            Debug.LogWarning("[RaceState] gameFlowManager reference is null — player kart will not be assigned.");
        }

        if (playerKartOptions == null || playerKartOptions.Length == 0)
        {
            Debug.LogWarning("[RaceState] playerKartOptions is empty — no player kart will be activated.");
        }
        else
        {
            int selectedIndex = PlayerCharacterSelection.SelectedIndex;
            for (int i = 0; i < playerKartOptions.Length; i++)
            {
                if (playerKartOptions[i] != null)
                    playerKartOptions[i].gameObject.SetActive(i == selectedIndex);
            }

            if (gameFlowManager != null && selectedIndex < playerKartOptions.Length && playerKartOptions[selectedIndex] != null)
            {
                gameFlowManager.autoFindKarts = false;
                gameFlowManager.playerKart = playerKartOptions[selectedIndex];
            }
        }

        opponentSpawnManager.ResetToSpawn();
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
