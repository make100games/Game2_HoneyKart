using UnityEngine.SceneManagement;

/// <summary>
/// Static state that persists across scene reloads via static fields.
/// Controls whether the game is in attract mode or play mode.
/// </summary>
public static class GameModeState
{
    /// <summary>True on first load — attract mode. Set to false when the player starts the game.</summary>
    public static bool IsAttractMode = true;

    /// <summary>Disables attract mode and reloads the active scene to begin a normal race.</summary>
    public static void StartGame()
    {
        IsAttractMode = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
