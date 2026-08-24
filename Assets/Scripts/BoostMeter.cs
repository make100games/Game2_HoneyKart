using System;
using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Per-kart boost charge meter. Fills on coin pickup and drains passively over time, both at
/// rates scaled by live race position so last place charges fastest/drains slowest and first
/// place charges slowest/drains fastest, giving the rubber-banding the boost design calls for.
/// Fires the kart's <see cref="KartBoost"/> and empties immediately once the meter is full.
/// </summary>
public class BoostMeter : MonoBehaviour
{
    private const float DefaultChargePerCoinFirstPlace = 0.125f;
    private const float DefaultChargePerCoinLastPlace = 0.34f;
    private const float DefaultDrainPerSecondFirstPlace = 0.2f;
    private const float DefaultDrainPerSecondLastPlace = 0.05f;
    private const float DefaultPositionPollInterval = 0.1f;
    private const float MinRateEpsilon = 0.001f;

    [Header("Charge Rates (per coin)")]
    [Tooltip("Charge added per coin when this kart is in first place.")]
    [SerializeField] private float chargePerCoinFirstPlace = DefaultChargePerCoinFirstPlace;

    [Tooltip("Charge added per coin when this kart is in last place.")]
    [SerializeField] private float chargePerCoinLastPlace = DefaultChargePerCoinLastPlace;

    [Header("Drain Rates (per second)")]
    [Tooltip("Passive drain per second when this kart is in first place.")]
    [SerializeField] private float drainPerSecondFirstPlace = DefaultDrainPerSecondFirstPlace;

    [Tooltip("Passive drain per second when this kart is in last place.")]
    [SerializeField] private float drainPerSecondLastPlace = DefaultDrainPerSecondLastPlace;

    [Header("Polling")]
    [Tooltip("Seconds between live-position refreshes used to recompute fill/drain rates. Matches RacePositionUI.updateInterval.")]
    [SerializeField] private float positionPollInterval = DefaultPositionPollInterval;

    /// <summary>Normalized 0-1 boost charge.</summary>
    public float Charge01 => m_Charge;

    /// <summary>True once the meter has reached full charge.</summary>
    public bool IsFull => m_Charge >= 1f;

    /// <summary>Fired when this kart accepts a coin pickup for boost charging.</summary>
    public event Action CoinCollected;

    /// <summary>Fired only when the charge value actually changes.</summary>
    public event Action<float> ChargeChanged;

    private LapTracker m_LapTracker;
    private KartBoost m_KartBoost;
    private ArcadeKart m_ArcadeKart;

    private float m_Charge;
    private float m_TimeSincePositionPoll;
    private float m_PositionBlendT = 0.5f;

    private void Awake()
    {
        m_LapTracker = GetComponent<LapTracker>();
        m_KartBoost = GetComponent<KartBoost>();
        m_ArcadeKart = GetComponent<ArcadeKart>();

        if (m_KartBoost == null)
            Debug.LogWarning("BoostMeter: No KartBoost component found on this GameObject!", this);

        if (m_LapTracker == null)
            Debug.LogWarning("BoostMeter: No LapTracker component found on this GameObject!", this);
    }

    private void OnEnable()
    {
        // Ensures a kart re-activated by RaceState.Enter() starts a fresh race with an empty meter.
        SetCharge(0f);
        m_TimeSincePositionPoll = 0f;
        m_PositionBlendT = 0.5f;
    }

    private void FixedUpdate()
    {
        if (!GameFlowManager.IsRaceActive) return;

        m_TimeSincePositionPoll += Time.fixedDeltaTime;
        if (m_TimeSincePositionPoll >= positionPollInterval)
        {
            m_TimeSincePositionPoll = 0f;
            RefreshPositionBlend();
        }

        float drainPerSecond = Mathf.Lerp(drainPerSecondFirstPlace, drainPerSecondLastPlace, m_PositionBlendT);
        SetCharge(m_Charge - drainPerSecond * Time.fixedDeltaTime);
    }

    /// <summary>Called by CoinCollector when this kart picks up a coin. Ignored until a new boost can fire.</summary>
    public void AddChargeForCoin()
    {
        if (!GameFlowManager.IsRaceActive) return;
        if (m_KartBoost != null && !m_KartBoost.CanFire) return;

        float chargePerCoin = Mathf.Lerp(chargePerCoinFirstPlace, chargePerCoinLastPlace, m_PositionBlendT);
        SetCharge(m_Charge + chargePerCoin);

        if (m_Charge >= 1f && m_KartBoost != null && m_KartBoost.Fire())
            EmptyImmediately();
    }

    /// <summary>Empties the meter immediately. Called on bomb hit, and internally when a boost fires.</summary>
    public void EmptyImmediately()
    {
        SetCharge(0f);
    }

    private void RefreshPositionBlend()
    {
        int racerCount = RaceManager.Instance != null ? RaceManager.Instance.RacerCount : 0;

        if (RaceManager.Instance == null || racerCount <= 1)
        {
            m_PositionBlendT = 0.5f;
            return;
        }

        int position = RaceManager.Instance.GetLivePosition(m_LapTracker);
        m_PositionBlendT = Mathf.Clamp01((position - 1) / (float)(racerCount - 1));
    }

    private void SetCharge(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, m_Charge)) return;

        m_Charge = clamped;
        ChargeChanged?.Invoke(m_Charge);
    }

    private void OnValidate()
    {
        chargePerCoinFirstPlace = Mathf.Max(MinRateEpsilon, chargePerCoinFirstPlace);
        chargePerCoinLastPlace = Mathf.Max(MinRateEpsilon, chargePerCoinLastPlace);
        drainPerSecondFirstPlace = Mathf.Max(0f, drainPerSecondFirstPlace);
        drainPerSecondLastPlace = Mathf.Max(0f, drainPerSecondLastPlace);
        positionPollInterval = Mathf.Max(0.01f, positionPollInterval);
    }
}
