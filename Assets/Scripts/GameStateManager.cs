using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Singleton that owns the active game state and drives all transitions.
/// All three state root GameObjects must start inactive; Start() triggers the
/// first Enter() call to kick off attract mode.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    /// <summary>Single instance — set in Awake; not DontDestroyOnLoad (single-scene game).</summary>
    public static GameStateManager Instance { get; private set; }

    [SerializeField] AttractModeState attractModeState;
    [SerializeField] CharacterSelectState characterSelectState;
    [SerializeField] RaceState raceState;
    [SerializeField] RaceResultsState raceResultsState;

    private GameStateBase currentState;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        TransitionTo(attractModeState);
    }

    /// <summary>
    /// Exits the current state (if any) and enters the next state.
    /// Transition names are logged for debugging.
    /// </summary>
    public void TransitionTo(GameStateBase nextState)
    {
        if (currentState != null)
        {
            Debug.Log($"[GameStateManager] Exiting {currentState.GetType().Name}");
            currentState.Exit();
        }

        Debug.Log($"[GameStateManager] Entering {nextState.GetType().Name}");
        currentState = nextState;
        currentState.Enter();
    }

    /// <summary>Called by AttractModeState when the player chooses to start the race. Routes to character selection first.</summary>
    public void StartGame()
    {
        GameModeState.IsAttractMode = false;
        TransitionTo(characterSelectState);
    }

    /// <summary>Called by CharacterSelectState once a character is chosen. Transitions directly to the race.</summary>
    public void EnterRace()
    {
        TransitionTo(raceState);
    }

    /// <summary>Called by RaceState when the race finishes. Prepares the results state then transitions.</summary>
    public void CompleteRace(ArcadeKart kart)
    {
        raceResultsState.PrepareEntry(kart);
        TransitionTo(raceResultsState);
    }

    /// <summary>Called by RaceResultsState when the player quits. Resets attract mode flag and returns to the title screen.</summary>
    public void RestartGame()
    {
        GameModeState.IsAttractMode = true;
        TransitionTo(attractModeState);
    }
}
