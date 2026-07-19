using TMPro;
using UnityEngine;

/// <summary>
/// HUD component that continuously displays the player's current lap in the top-left corner,
/// formatted as "Lap X of Y". The playerTracker reference is null in the Editor and is assigned
/// at runtime by RaceState.Enter(). The label only updates when the displayed lap number changes.
/// </summary>
public class LapCounterUI : MonoBehaviour
{
    private const string LapTextFormat = "Lap {0} of {1}";

    [Tooltip("Reference to the TextMeshProUGUI label that shows the current lap (e.g. 'Lap 1 of 3').")]
    public TMP_Text lapLabel;

    [Tooltip("The player kart's LapTracker. Assigned at runtime by RaceState — null is safe until then.")]
    public LapTracker playerTracker;

    private int m_LastDisplayedLap = -1;

    void Update()
    {
        if (lapLabel == null) return;
        if (playerTracker == null) return;
        if (RaceManager.Instance == null) return;

        int totalLaps = RaceManager.Instance.TotalLaps;
        int currentLap = Mathf.Clamp(playerTracker.LapsCompleted + 1, 1, totalLaps);

        if (currentLap == m_LastDisplayedLap) return;

        m_LastDisplayedLap = currentLap;
        lapLabel.text = string.Format(LapTextFormat, currentLap, totalLaps);
    }
}
