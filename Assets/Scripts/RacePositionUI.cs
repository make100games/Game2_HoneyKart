using TMPro;
using UnityEngine;

/// <summary>
/// HUD component that continuously displays the player's live race position in the bottom-right corner.
/// The playerTracker reference is null in the Editor and is assigned at runtime by RaceState.Enter().
/// Once the player finishes the race, updates are halted — the finish overlay takes over.
/// </summary>
public class RacePositionUI : MonoBehaviour
{
    [Tooltip("Reference to the TextMeshProUGUI label that shows the current position (e.g. '1st').")]
    public TMP_Text positionLabel;

    [Tooltip("The player kart's LapTracker. Assigned at runtime by RaceState — null is safe until then.")]
    public LapTracker playerTracker;

    [Tooltip("Seconds between position updates. Defaults to 0.1 (10 Hz) to reduce per-frame cost.")]
    public float updateInterval = 0.1f;

    private float m_TimeSinceLastUpdate;

    void Update()
    {
        if (playerTracker == null) return;
        if (playerTracker.HasFinished) return;
        if (RaceManager.Instance == null) return;

        m_TimeSinceLastUpdate += Time.deltaTime;
        if (m_TimeSinceLastUpdate < updateInterval) return;

        m_TimeSinceLastUpdate = 0f;

        int position = RaceManager.Instance.GetLivePosition(playerTracker);

        if (positionLabel != null)
            positionLabel.text = OrdinalFormatter.ToOrdinal(position);
    }
}
