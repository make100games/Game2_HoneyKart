using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the selected player's boost charge and the coin, bomb, and boost feedback animations.
/// </summary>
public sealed class BoostMeterUI : MonoBehaviour
{
    private const float FillAnimationDuration = 0.68f;
    private const float EventIconDuration = 0.82f;
    private const float BombShakeDuration = 0.55f;
    private const float BoostTextDuration = 1.25f;
    private const float CoinFallDistance = 12f;
    private const float ShakeDistance = 11f;
    private const float InitialFeedbackScale = 0.75f;
    private const float BoostOvershootScale = 1.2f;

    private const int BorderCornerRadius = 4;
    private const int BackgroundCornerRadius = 4;
    private const int FillCornerRadius = 4;

    private static readonly Vector2 BorderAnchoredPosition = new(-75f, -5f);
    private static readonly Vector2 BorderSize = new(306f, 44f);
    private static readonly Vector2 BackgroundAnchoredPosition = new(-79f, -9f);
    private static readonly Vector2 BackgroundSize = new(298f, 36f);
    private static readonly Vector2 FillAnchoredPosition = new(-81f, -11f);
    private static readonly Vector2 FillSize = new(294f, 32f);
    private static readonly Color BorderColor = new(1f, 1f, 1f, 0.92f);
    private static readonly Color BackgroundColor = new(0.15f, 0.17f, 0.21f, 1f);

    private BoostMeterGradientGraphic m_MeterGraphic;
    private RectTransform m_CoinIconRect;
    private CanvasGroup m_CoinIconGroup;
    private RectTransform m_BombIconRect;
    private CanvasGroup m_BombIconGroup;
    private RectTransform m_BoostTextRect;
    private CanvasGroup m_BoostTextGroup;
    private BoostMeter m_BoostMeter;
    private CoinCollector m_CoinCollector;
    private KartCombatHandler m_CombatHandler;
    private KartBoost m_KartBoost;
    private Vector2 m_RootPosition;
    private Vector2 m_CoinStartPosition;
    private float m_DisplayedCharge;
    private float m_FillStartCharge;
    private float m_FillTargetCharge;
    private float m_FillElapsed;
    private float m_EventIconElapsed = EventIconDuration;
    private float m_ShakeRemaining;
    private float m_BoostTextRemaining;
    private bool m_AnimateNextIncrease;
    private bool m_HoldFullUntilFillCompletes;
    private FeedbackType m_FeedbackType;

    private enum FeedbackType
    {
        None,
        Coin,
        Bomb
    }

    /// <summary>Creates the complete meter hierarchy under the existing top HUD reveal group.</summary>
    public static BoostMeterUI Create(RectTransform topHudGroup, Sprite coinSprite, Sprite bombSprite, Sprite boostTextSprite)
    {
        GameObject root = CreateUiObject("BoostMeterUI", topHudGroup);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.one;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = Vector2.one;
        rootRect.anchoredPosition = new Vector2(-24f, -20f);
        rootRect.sizeDelta = new Vector2(420f, 104f);

        BoostMeterUI controller = root.AddComponent<BoostMeterUI>();
        controller.m_RootPosition = rootRect.anchoredPosition;

        GameObject meterBorder = CreateUiObject("MeterBorder", rootRect);
        RectTransform meterBorderRect = meterBorder.GetComponent<RectTransform>();
        SetRect(meterBorderRect, Vector2.one, BorderAnchoredPosition, BorderSize);
        Image meterBorderImage = meterBorder.AddComponent<Image>();
        meterBorderImage.sprite = RoundedRectSpriteFactory.GetSlicedRoundedRect(BorderCornerRadius);
        meterBorderImage.type = Image.Type.Sliced;
        meterBorderImage.pixelsPerUnitMultiplier = 1f;
        meterBorderImage.color = BorderColor;
        meterBorderImage.raycastTarget = false;

        GameObject meterBackground = CreateUiObject("MeterBackground", rootRect);
        RectTransform meterBackgroundRect = meterBackground.GetComponent<RectTransform>();
        SetRect(meterBackgroundRect, Vector2.one, BackgroundAnchoredPosition, BackgroundSize);
        Image meterBackgroundImage = meterBackground.AddComponent<Image>();
        meterBackgroundImage.sprite = RoundedRectSpriteFactory.GetSlicedRoundedRect(BackgroundCornerRadius);
        meterBackgroundImage.type = Image.Type.Sliced;
        meterBackgroundImage.pixelsPerUnitMultiplier = 1f;
        meterBackgroundImage.color = BackgroundColor;
        meterBackgroundImage.raycastTarget = false;

        GameObject meter = CreateUiObject("GradientFill", rootRect);
        RectTransform meterRect = meter.GetComponent<RectTransform>();
        SetRect(meterRect, Vector2.one, FillAnchoredPosition, FillSize);
        controller.m_MeterGraphic = meter.AddComponent<BoostMeterGradientGraphic>();
        controller.m_MeterGraphic.Initialize(FillSize, FillCornerRadius);

        CreateFeedbackImage("CoinFeedback", rootRect, coinSprite, new Vector2(-8f, -3f), new Vector2(60f, 60f), out controller.m_CoinIconRect, out controller.m_CoinIconGroup);
        controller.m_CoinStartPosition = controller.m_CoinIconRect.anchoredPosition;
        CreateFeedbackImage("BombFeedback", rootRect, bombSprite, new Vector2(-8f, -3f), new Vector2(60f, 60f), out controller.m_BombIconRect, out controller.m_BombIconGroup);
        CreateFeedbackImage("BoostText", rootRect, boostTextSprite, new Vector2(-6f, -56f), new Vector2(190f, 62f), out controller.m_BoostTextRect, out controller.m_BoostTextGroup);

        controller.ResetFeedbackVisuals();
        return controller;
    }

    /// <summary>Binds the HUD to the selected player kart and refreshes its current charge.</summary>
    public void Bind(BoostMeter boostMeter, CoinCollector coinCollector, KartCombatHandler combatHandler, KartBoost kartBoost)
    {
        Unbind();
        m_BoostMeter = boostMeter;
        m_CoinCollector = coinCollector;
        m_CombatHandler = combatHandler;
        m_KartBoost = kartBoost;

        m_HoldFullUntilFillCompletes = false;
        m_AnimateNextIncrease = false;
        m_FillElapsed = FillAnimationDuration;

        if (m_BoostMeter != null)
        {
            m_DisplayedCharge = m_BoostMeter.Charge01;
            m_FillStartCharge = m_DisplayedCharge;
            m_FillTargetCharge = m_DisplayedCharge;
            m_MeterGraphic.SetFillAmount(m_DisplayedCharge);
            m_BoostMeter.ChargeChanged += HandleChargeChanged;
        }

        if (m_CoinCollector != null)
            m_CoinCollector.CoinCollected += HandleCoinCollected;

        if (m_CombatHandler != null)
            m_CombatHandler.ExplosionHit += HandleExplosionHit;
        if (m_KartBoost != null)
            m_KartBoost.BoostStarted += HandleBoostStarted;
    }

    /// <summary>Removes subscriptions from the previously selected player kart.</summary>
    public void Unbind()
    {
        if (m_BoostMeter != null)
            m_BoostMeter.ChargeChanged -= HandleChargeChanged;
        if (m_CoinCollector != null)
            m_CoinCollector.CoinCollected -= HandleCoinCollected;
        if (m_CombatHandler != null)
            m_CombatHandler.ExplosionHit -= HandleExplosionHit;
        if (m_KartBoost != null)
            m_KartBoost.BoostStarted -= HandleBoostStarted;

        m_BoostMeter = null;
        m_CoinCollector = null;
        m_CombatHandler = null;
        m_KartBoost = null;
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        SynchronizeCharge();
        UpdateFillAnimation();
        UpdateFeedbackIcon();
        UpdateBombShake();
        UpdateBoostText();
    }

    private void HandleCoinCollected()
    {
        m_AnimateNextIncrease = true;
        m_FeedbackType = FeedbackType.Coin;
        m_EventIconElapsed = 0f;
    }

    private void SynchronizeCharge()
    {
        if (m_BoostMeter == null || m_HoldFullUntilFillCompletes)
            return;

        float authoritativeCharge = m_BoostMeter.Charge01;
        if (m_FillElapsed < FillAnimationDuration)
        {
            if (authoritativeCharge > 0f)
                m_FillTargetCharge = authoritativeCharge;
            return;
        }

        if (authoritativeCharge > m_DisplayedCharge)
        {
            m_FillStartCharge = m_DisplayedCharge;
            m_FillTargetCharge = authoritativeCharge;
            m_FillElapsed = 0f;
        }
        else if (authoritativeCharge < m_DisplayedCharge)
        {
            m_DisplayedCharge = authoritativeCharge;
            m_MeterGraphic.SetFillAmount(m_DisplayedCharge);
        }
    }

    private void HandleChargeChanged(float charge)
    {
        if (charge > m_DisplayedCharge && m_AnimateNextIncrease)
        {
            m_FillStartCharge = m_DisplayedCharge;
            m_FillTargetCharge = charge;
            m_FillElapsed = 0f;
            m_AnimateNextIncrease = false;
            return;
        }

        if (charge <= 0f && m_HoldFullUntilFillCompletes)
            return;

        if (m_FillElapsed < FillAnimationDuration && charge > 0f)
        {
            m_FillTargetCharge = charge;
            return;
        }

        m_AnimateNextIncrease = false;
        m_FillElapsed = FillAnimationDuration;
        m_DisplayedCharge = charge;
        m_MeterGraphic.SetFillAmount(m_DisplayedCharge);
    }

    private void HandleExplosionHit()
    {
        m_FeedbackType = FeedbackType.Bomb;
        m_EventIconElapsed = 0f;
        m_ShakeRemaining = BombShakeDuration;
        m_HoldFullUntilFillCompletes = false;
        m_FillElapsed = FillAnimationDuration;
        m_DisplayedCharge = 0f;
        m_MeterGraphic.SetFillAmount(0f);
    }

    private void HandleBoostStarted()
    {
        m_BoostTextRemaining = BoostTextDuration;
        m_HoldFullUntilFillCompletes = true;
    }

    private void UpdateFillAnimation()
    {
        if (m_FillElapsed >= FillAnimationDuration)
        {
            if (m_HoldFullUntilFillCompletes)
                ReleaseFullChargeHold();
            return;
        }

        m_FillElapsed += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(m_FillElapsed / FillAnimationDuration);
        float easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f);
        m_DisplayedCharge = Mathf.Lerp(m_FillStartCharge, m_FillTargetCharge, easedTime);
        m_MeterGraphic.SetFillAmount(m_DisplayedCharge);

        if (normalizedTime >= 1f && m_HoldFullUntilFillCompletes)
            ReleaseFullChargeHold();
    }

    private void ReleaseFullChargeHold()
    {
        m_HoldFullUntilFillCompletes = false;
        m_DisplayedCharge = m_BoostMeter != null ? m_BoostMeter.Charge01 : 0f;
        m_FillStartCharge = m_DisplayedCharge;
        m_FillTargetCharge = m_DisplayedCharge;
        m_MeterGraphic.SetFillAmount(m_DisplayedCharge);
    }

    private void UpdateFeedbackIcon()
    {
        if (m_FeedbackType == FeedbackType.None)
            return;

        m_EventIconElapsed += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(m_EventIconElapsed / EventIconDuration);
        float appearTime = Mathf.Clamp01(normalizedTime / 0.34f);
        float disappearAlpha = Mathf.Clamp01((1f - normalizedTime) / 0.28f);
        float alpha = Mathf.Min(EaseOutCubic(appearTime), disappearAlpha);
        float scale = Mathf.Lerp(InitialFeedbackScale, 1f, EaseOutBack(appearTime));

        bool showCoin = m_FeedbackType == FeedbackType.Coin;
        m_CoinIconGroup.alpha = showCoin ? alpha : 0f;
        m_BombIconGroup.alpha = showCoin ? 0f : alpha;
        m_CoinIconRect.localScale = Vector3.one * scale;
        m_BombIconRect.localScale = Vector3.one * scale;
        m_CoinIconRect.anchoredPosition = m_CoinStartPosition + Vector2.down * Mathf.Lerp(-CoinFallDistance, CoinFallDistance, EaseOutCubic(normalizedTime));

        if (normalizedTime >= 1f)
        {
            m_FeedbackType = FeedbackType.None;
            m_CoinIconGroup.alpha = 0f;
            m_BombIconGroup.alpha = 0f;
        }
    }

    private void UpdateBombShake()
    {
        RectTransform rootRect = (RectTransform)transform;
        if (m_ShakeRemaining <= 0f)
        {
            rootRect.anchoredPosition = m_RootPosition;
            return;
        }

        m_ShakeRemaining = Mathf.Max(0f, m_ShakeRemaining - Time.deltaTime);
        float strength = m_ShakeRemaining / BombShakeDuration;
        rootRect.anchoredPosition = m_RootPosition + new Vector2(
            Mathf.Sin(m_ShakeRemaining * 92f) * ShakeDistance * strength,
            Mathf.Cos(m_ShakeRemaining * 71f) * ShakeDistance * 0.3f * strength);
    }

    private void UpdateBoostText()
    {
        if (m_BoostTextRemaining <= 0f)
        {
            m_BoostTextGroup.alpha = 0f;
            return;
        }

        m_BoostTextRemaining = Mathf.Max(0f, m_BoostTextRemaining - Time.deltaTime);
        float elapsed = BoostTextDuration - m_BoostTextRemaining;
        float appearTime = Mathf.Clamp01(elapsed / 0.34f);
        float disappearAlpha = Mathf.Clamp01(m_BoostTextRemaining / 0.28f);
        m_BoostTextGroup.alpha = Mathf.Min(EaseOutCubic(appearTime), disappearAlpha);

        float scale = appearTime < 0.72f
            ? Mathf.Lerp(InitialFeedbackScale, BoostOvershootScale, EaseOutCubic(appearTime / 0.72f))
            : Mathf.Lerp(BoostOvershootScale, 1f, EaseOutCubic((appearTime - 0.72f) / 0.28f));
        m_BoostTextRect.localScale = Vector3.one * scale;
    }

    private void ResetFeedbackVisuals()
    {
        m_FillElapsed = FillAnimationDuration;
        m_CoinIconGroup.alpha = 0f;
        m_BombIconGroup.alpha = 0f;
        m_BoostTextGroup.alpha = 0f;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform));
        uiObject.layer = parent.gameObject.layer;
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void CreateFeedbackImage(string objectName, RectTransform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 size, out RectTransform imageRect, out CanvasGroup canvasGroup)
    {
        GameObject imageObject = CreateUiObject(objectName, parent);
        imageRect = imageObject.GetComponent<RectTransform>();
        SetRect(imageRect, Vector2.one, anchoredPosition, size);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
        image.raycastTarget = false;
        canvasGroup = imageObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private static void SetRect(RectTransform rectTransform, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = Vector2.one;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private static float EaseOutCubic(float normalizedTime)
    {
        return 1f - Mathf.Pow(1f - Mathf.Clamp01(normalizedTime), 3f);
    }

    private static float EaseOutBack(float normalizedTime)
    {
        float time = Mathf.Clamp01(normalizedTime) - 1f;
        const float overshoot = 1.70158f;
        return 1f + (overshoot + 1f) * time * time * time + overshoot * time * time;
    }
}
