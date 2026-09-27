/// <summary>
/// Static flag that tracks whether the game is currently in attract mode.
/// Set to false by GameStateManager.StartGame() when the player begins a race.
/// Reset back to true by GameStateManager.Start(), since this static field would
/// otherwise keep its value across the scene reload done by RestartGame().
/// </summary>
public static class GameModeState
{
    /// <summary>True on first load — attract mode. Set to false when the player starts the game.</summary>
    public static bool IsAttractMode = true;
}
