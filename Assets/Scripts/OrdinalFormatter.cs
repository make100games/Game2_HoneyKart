/// <summary>
/// Shared utility for formatting 1-based race positions as ordinal strings (e.g. 1 → "1st").
/// </summary>
public static class OrdinalFormatter
{
    /// <summary>Converts a 1-based position integer to its ordinal string (e.g. 1 → "1st", 4 → "4th").</summary>
    public static string ToOrdinal(int n)
    {
        return n switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{n}th"
        };
    }
}
