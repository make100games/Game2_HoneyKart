using System.Collections;
using UnityEngine;

/// <summary>
/// Coordinates the level banner and race HUD reveal animations from
/// <see cref="PreRaceCameraFlyIn"/> phase events. The level banner plays a fade in/hold/fade
/// out sequence shortly after the fly-through camera phase starts; the lap counter, item HUD,
/// and race-position HUD groups stay hidden (alpha zero, offset off-screen) until the follow
/// camera takes over, then slide/fade in together. Attach under /RaceState/RaceRoot alongside
/// the existing HUD hierarchy.
/// </summary>
public class RaceUIPolishController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Camera fly-in sequencer whose FlythroughStarted/FollowCameraActivated events drive this controller.")]
    public PreRaceCameraFlyIn preRaceCameraFlyIn;

    [Header("Level Banner")]
    [Tooltip("CanvasGroup on the full-width top level banner — initially inactive at alpha 0.")]
    public CanvasGroup levelBannerGroup;

    [Tooltip("Seconds after FlythroughStarted before the banner begins to fade in. Clamped to non-negative.")]
    public float bannerDelay = 0.5f;

    [Tooltip("Seconds for the banner to fade from alpha 0 to 1. Clamped to non-negative.")]
    public float bannerFadeInDuration = 0.5f;

    [Tooltip("Seconds the banner remains fully visible. Clamped to non-negative.")]
    public float bannerHoldDuration = 2f;

    [Tooltip("Seconds for the banner to fade from alpha 1 to 0. Clamped to non-negative.")]
    public float bannerFadeOutDuration = 0.5f;

    [Header("HUD Groups")]
    [Tooltip("RectTransform wrapping the lap counter and item HUD — animates in from above the screen.")]
    public RectTransform topHudGroup;

    [Tooltip("CanvasGroup on the top HUD wrapper.")]
    public CanvasGroup topHudCanvasGroup;

    [Tooltip("RectTransform wrapping the race-position HUD — animates in from below the screen.")]
    public RectTransform bottomHudGroup;

    [Tooltip("CanvasGroup on the bottom HUD wrapper.")]
    public CanvasGroup bottomHudCanvasGroup;

    [Header("HUD Reveal Timing")]
    [Tooltip("Seconds after FollowCameraActivated before the HUD groups begin to slide/fade in. Clamped to non-negative.")]
    public float hudRevealDelay = 0.5f;

    [Tooltip("Seconds for the simultaneous top/bottom HUD slide and fade. Clamped to non-negative.")]
    public float hudRevealDuration = 0.8f;

    [Header("HUD Hidden Offsets")]
    [Tooltip("Vertical distance (canvas units) the top HUD group starts above its authored anchored position.")]
    public float topHudHiddenOffset = TopHudHiddenOffsetDefault;

    [Tooltip("Vertical distance (canvas units) the bottom HUD group starts below its authored anchored position.")]
    public float bottomHudHiddenOffset = BottomHudHiddenOffsetDefault;

    private const float TopHudHiddenOffsetDefault = 300f;
    private const float BottomHudHiddenOffsetDefault = 300f;

    private Vector2 m_TopHudShownPosition;
    private Vector2 m_BottomHudShownPosition;
    private bool m_HudPositionsCached;

    private Coroutine m_BannerCoroutine;
    private Coroutine m_HudRevealCoroutine;

    private bool m_MissingFlyInWarned;

    void OnEnable()
    {
        CacheHudShownPositions();
        ResetBannerState();
        ResetHudState();

        if (preRaceCameraFlyIn == null)
        {
            if (!m_MissingFlyInWarned)
            {
                Debug.LogWarning("[RaceUIPolishController] preRaceCameraFlyIn is unassigned — HUD will remain hidden.", this);
                m_MissingFlyInWarned = true;
            }
            return;
        }

        preRaceCameraFlyIn.FlythroughStarted += HandleFlythroughStarted;
        preRaceCameraFlyIn.FollowCameraActivated += HandleFollowCameraActivated;
    }

    void OnDisable()
    {
        if (preRaceCameraFlyIn != null)
        {
            preRaceCameraFlyIn.FlythroughStarted -= HandleFlythroughStarted;
            preRaceCameraFlyIn.FollowCameraActivated -= HandleFollowCameraActivated;
        }

        if (m_BannerCoroutine != null)
        {
            StopCoroutine(m_BannerCoroutine);
            m_BannerCoroutine = null;
        }

        if (m_HudRevealCoroutine != null)
        {
            StopCoroutine(m_HudRevealCoroutine);
            m_HudRevealCoroutine = null;
        }

        ResetBannerState();
        ResetHudState();
    }

    /// <summary>Caches each HUD wrapper's authored anchored position once, before hidden offsets are applied.</summary>
    private void CacheHudShownPositions()
    {
        if (m_HudPositionsCached)
            return;

        if (topHudGroup != null)
            m_TopHudShownPosition = topHudGroup.anchoredPosition;
        if (bottomHudGroup != null)
            m_BottomHudShownPosition = bottomHudGroup.anchoredPosition;

        m_HudPositionsCached = true;
    }

    /// <summary>Deactivates the banner and resets its alpha to zero.</summary>
    private void ResetBannerState()
    {
        if (levelBannerGroup != null)
        {
            levelBannerGroup.gameObject.SetActive(false);
            levelBannerGroup.alpha = 0f;
        }
    }

    /// <summary>Moves both HUD groups to their hidden offset positions, zeroes alpha, and disables input.</summary>
    private void ResetHudState()
    {
        float topOffset = Mathf.Max(0f, topHudHiddenOffset);
        float bottomOffset = Mathf.Max(0f, bottomHudHiddenOffset);

        if (topHudGroup != null)
            topHudGroup.anchoredPosition = m_TopHudShownPosition + new Vector2(0f, topOffset);
        if (topHudCanvasGroup != null)
        {
            topHudCanvasGroup.alpha = 0f;
            topHudCanvasGroup.interactable = false;
            topHudCanvasGroup.blocksRaycasts = false;
        }

        if (bottomHudGroup != null)
            bottomHudGroup.anchoredPosition = m_BottomHudShownPosition - new Vector2(0f, bottomOffset);
        if (bottomHudCanvasGroup != null)
        {
            bottomHudCanvasGroup.alpha = 0f;
            bottomHudCanvasGroup.interactable = false;
            bottomHudCanvasGroup.blocksRaycasts = false;
        }
    }

    private void HandleFlythroughStarted()
    {
        if (m_BannerCoroutine != null)
            StopCoroutine(m_BannerCoroutine);
        m_BannerCoroutine = StartCoroutine(BannerSequenceCoroutine());
    }

    private void HandleFollowCameraActivated()
    {
        if (m_HudRevealCoroutine != null)
            StopCoroutine(m_HudRevealCoroutine);
        m_HudRevealCoroutine = StartCoroutine(HudRevealCoroutine());
    }

    /// <summary>Waits bannerDelay, fades the banner in, holds, then fades it out and deactivates it.</summary>
    private IEnumerator BannerSequenceCoroutine()
    {
        if (levelBannerGroup == null)
        {
            Debug.LogWarning("[RaceUIPolishController] levelBannerGroup is unassigned — skipping banner sequence.", this);
            yield break;
        }

        float delay = Mathf.Max(0f, bannerDelay);
        float fadeIn = Mathf.Max(0f, bannerFadeInDuration);
        float hold = Mathf.Max(0f, bannerHoldDuration);
        float fadeOut = Mathf.Max(0f, bannerFadeOutDuration);

        yield return new WaitForSeconds(delay);

        levelBannerGroup.gameObject.SetActive(true);
        levelBannerGroup.alpha = 0f;

        yield return FadeCanvasGroup(levelBannerGroup, 0f, 1f, fadeIn);

        yield return new WaitForSeconds(hold);

        yield return FadeCanvasGroup(levelBannerGroup, 1f, 0f, fadeOut);

        levelBannerGroup.gameObject.SetActive(false);
        m_BannerCoroutine = null;
    }

    /// <summary>Waits hudRevealDelay, then slides and fades both HUD groups in together over hudRevealDuration.</summary>
    private IEnumerator HudRevealCoroutine()
    {
        CacheHudShownPositions();

        float delay = Mathf.Max(0f, hudRevealDelay);
        float duration = Mathf.Max(0f, hudRevealDuration);

        yield return new WaitForSeconds(delay);

        Vector2 topStart = topHudGroup != null ? topHudGroup.anchoredPosition : Vector2.zero;
        Vector2 bottomStart = bottomHudGroup != null ? bottomHudGroup.anchoredPosition : Vector2.zero;
        float topAlphaStart = topHudCanvasGroup != null ? topHudCanvasGroup.alpha : 0f;
        float bottomAlphaStart = bottomHudCanvasGroup != null ? bottomHudCanvasGroup.alpha : 0f;

        if (duration <= 0f)
        {
            SnapHudToShown();
            m_HudRevealCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // cubic ease-out

            if (topHudGroup != null)
                topHudGroup.anchoredPosition = Vector2.Lerp(topStart, m_TopHudShownPosition, eased);
            if (topHudCanvasGroup != null)
                topHudCanvasGroup.alpha = Mathf.Lerp(topAlphaStart, 1f, eased);

            if (bottomHudGroup != null)
                bottomHudGroup.anchoredPosition = Vector2.Lerp(bottomStart, m_BottomHudShownPosition, eased);
            if (bottomHudCanvasGroup != null)
                bottomHudCanvasGroup.alpha = Mathf.Lerp(bottomAlphaStart, 1f, eased);

            yield return null;
        }

        SnapHudToShown();
        m_HudRevealCoroutine = null;
    }

    /// <summary>Snaps both HUD groups to their cached shown positions and full alpha, and re-enables input.</summary>
    private void SnapHudToShown()
    {
        if (topHudGroup != null)
            topHudGroup.anchoredPosition = m_TopHudShownPosition;
        if (topHudCanvasGroup != null)
        {
            topHudCanvasGroup.alpha = 1f;
            topHudCanvasGroup.interactable = true;
            topHudCanvasGroup.blocksRaycasts = true;
        }

        if (bottomHudGroup != null)
            bottomHudGroup.anchoredPosition = m_BottomHudShownPosition;
        if (bottomHudCanvasGroup != null)
        {
            bottomHudCanvasGroup.alpha = 1f;
            bottomHudCanvasGroup.interactable = true;
            bottomHudCanvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>Linearly fades a CanvasGroup's alpha from `from` to `to` over `duration` seconds using Time.deltaTime.</summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        if (duration <= 0f)
        {
            group.alpha = Mathf.Clamp01(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Clamp01(Mathf.Lerp(from, to, t));
            yield return null;
        }

        group.alpha = Mathf.Clamp01(to);
    }
}
