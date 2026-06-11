using System.Collections;
using KartGame.KartSystems;
using TMPro;
using UnityEngine;

/// <summary>
/// Slides the player's finishing position label onto the screen from the left
/// after a configurable delay when the race ends.
/// </summary>
public class FinishPositionUI : MonoBehaviour
{
    [Tooltip("Reference to the child PositionText TextMeshProUGUI object.")]
    public TMP_Text positionLabel;

    [Tooltip("Seconds after race finish before the slide animation starts.")]
    public float slideInDelay = 1f;

    [Tooltip("Duration of the horizontal slide animation in seconds.")]
    public float slideInDuration = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("Horizontal stop point as a fraction of canvas width from the left edge.")]
    public float targetXNormalized = 0.75f;

    [Range(0f, 1f)]
    [Tooltip("Vertical position as a fraction of canvas height from the bottom (0.75 = upper third).")]
    public float targetYNormalized = 0.75f;

    [Tooltip("Easing curve applied to the horizontal lerp. Defaults to ease-out.")]
    public AnimationCurve slideInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private RectTransform m_CanvasRect;
    private bool m_Triggered;

    void Awake()
    {
        m_CanvasRect = GetComponent<RectTransform>();

        if (positionLabel != null)
            positionLabel.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        m_Triggered = false;
    }

    /// <summary>
    /// Called directly by RaceResultsState.Enter() with the finishing kart.
    /// Starts the delayed slide-in sequence for the position label.
    /// </summary>
    public void TriggerSlideIn(ArcadeKart kart)
    {
        if (m_Triggered) return;
        m_Triggered = true;

        LapTracker tracker = kart.GetComponent<LapTracker>();
        int position = RaceManager.GetFinishPosition(tracker);

        StartCoroutine(SlideInRoutine(position));
    }

    private IEnumerator SlideInRoutine(int position)
    {
        yield return new WaitForSeconds(slideInDelay);

        positionLabel.text = GetOrdinalText(position);
        positionLabel.gameObject.SetActive(true);

        float w = m_CanvasRect.rect.width;
        float h = m_CanvasRect.rect.height;

        float startX  = -w;
        float endX    = w * targetXNormalized - w * 0.5f;
        float targetY = h * targetYNormalized - h * 0.5f;

        positionLabel.rectTransform.anchoredPosition = new Vector2(startX, targetY);

        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideInDuration);
            float x = Mathf.Lerp(startX, endX, slideInCurve.Evaluate(t));
            positionLabel.rectTransform.anchoredPosition = new Vector2(x, targetY);
            yield return null;
        }

        positionLabel.rectTransform.anchoredPosition = new Vector2(endX, targetY);
    }

    /// <summary>Converts a 1-based position integer to its ordinal string (e.g. 1 → "1st").</summary>
    private static string GetOrdinalText(int n)
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
