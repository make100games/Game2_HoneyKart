using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD component that reflects the player's held bomb count inside the item box.
/// The box image itself is always visible; the bomb icon and count label are only
/// shown while the player holds one or more bombs. playerLauncher is null in the
/// Editor and is assigned at runtime by RaceState.Enter().
/// </summary>
public class ItemHUD : MonoBehaviour
{
    private const string CountPrefix = "x";

    [Tooltip("Bomb icon Image shown inside the box while the player holds bombs.")]
    public Image itemIcon;

    [Tooltip("Label showing the held bomb count (e.g. 'x3'), shown near the bottom of the box.")]
    public TMP_Text countLabel;

    [Tooltip("The player kart's BombLauncher. Assigned at runtime by RaceState — null is safe until then.")]
    public BombLauncher playerLauncher;

    private int m_LastCount = -1;

    void Update()
    {
        int count = playerLauncher != null ? playerLauncher.RemainingBombs : 0;
        if (count == m_LastCount) return;

        m_LastCount = count;
        bool hasItems = count > 0;

        if (itemIcon != null)
            itemIcon.enabled = hasItems;

        if (countLabel != null)
        {
            countLabel.enabled = hasItems;
            if (hasItems)
                countLabel.text = CountPrefix + count;
        }
    }
}
