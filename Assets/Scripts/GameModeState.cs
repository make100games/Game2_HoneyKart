/// <summary>
/// Static flag that tracks whether the game is currently in attract mode.
/// Set to false by AttractModeManager.StartGame() when the player begins a race.
/// </summary>
public static class GameModeState
{
    /// <summary>True on first load — attract mode. Set to false when the player starts the game.</summary>
    public static bool IsAttractMode = true;
}
