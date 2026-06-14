/// <summary>
/// Static data holder for the character index chosen in CharacterSelectState.
/// Read by RaceState when entering the race to activate the correct kart.
/// </summary>
public static class PlayerCharacterSelection
{
    /// <summary>Index of the selected character kart. Defaults to 0 (Brutus).</summary>
    public static int SelectedIndex { get; set; } = 0;
}
