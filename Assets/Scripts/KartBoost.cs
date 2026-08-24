using System;
using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Executes a single impulse-style speed boost on this kart: an instant velocity jump,
/// a brief raised-ceiling sustain (via a StatPowerup) that lets ArcadeKart's own clamp hold
/// the kart at the boosted top speed, then a forced settle back down to base top speed
/// (ArcadeKart never decays over-max-speed velocity on its own while accelerating).
/// Fired by <see cref="BoostMeter"/> when its charge reaches full; cancelled by
/// <see cref="KartCombatHandler"/> on a bomb hit.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KartBoost : MonoBehaviour
{
    private const string BoostPowerUpID = "CoinBoost";
    private const float DefaultImpulseSpeedGain = 9f;
    private const float DefaultBoostTopSpeedBonus = 9f;
    private const float DefaultBoostAccelerationBonus = 6f;
    private const float DefaultBoostDuration = 1.25f;
    private const float DefaultSettleDeceleration = 10f;
    private const float DefaultSettleTimeout = 3f;

    private enum BoostPhase
    {
        Idle,
        Sustain,
        Settle
    }

    [Header("Impulse")]
    [Tooltip("Immediate speed (m/s) added along the kart's ground-projected forward direction when the boost fires.")]
    [SerializeField] private float impulseSpeedGain = DefaultImpulseSpeedGain;

    [Header("Sustain")]
    [Tooltip("Top speed bonus applied for the sustain duration, matched to impulseSpeedGain so the kart holds its peak instead of being clamped down.")]
    [SerializeField] private float boostTopSpeedBonus = DefaultBoostTopSpeedBonus;

    [Tooltip("Acceleration bonus applied for the sustain duration, letting the kart regain the ceiling if the boost started below top speed.")]
    [SerializeField] private float boostAccelerationBonus = DefaultBoostAccelerationBonus;

    [Tooltip("How long the raised-ceiling sustain phase lasts, in seconds.")]
    [SerializeField] private float boostDuration = DefaultBoostDuration;

    [Header("Settle")]
    [Tooltip("Deceleration (m/s^2) applied to horizontal velocity during the settle phase.")]
    [SerializeField] private float settleDeceleration = DefaultSettleDeceleration;

    [Tooltip("Safety cap on the settle phase in case horizontal speed never reaches the base top speed.")]
    [SerializeField] private float settleTimeout = DefaultSettleTimeout;

    /// <summary>True while the impulse/sustain phase is active (not during settle).</summary>
    public bool IsBoosting => m_Phase == BoostPhase.Sustain;

    /// <summary>True when the kart can immediately accept and start a new boost.</summary>
    public bool CanFire => m_Phase == BoostPhase.Idle && m_Kart != null && m_Kart.CanMove && GameFlowManager.IsRaceActive;

    /// <summary>Fired the moment a boost begins (impulse applied, sustain starting).</summary>
    public event Action BoostStarted;

    /// <summary>Fired when the sustain phase ends (boost powerup revoked, settle beginning or boost cancelled).</summary>
    public event Action BoostEnded;

    private ArcadeKart m_Kart;
    private Rigidbody m_Rigidbody;
    private BoostPhase m_Phase = BoostPhase.Idle;
    private float m_PhaseTimer;
    private ArcadeKart.StatPowerup m_ActivePowerup;

    private void Awake()
    {
        m_Kart = GetComponent<ArcadeKart>();
        m_Rigidbody = GetComponent<Rigidbody>();

        if (m_Kart == null || m_Rigidbody == null)
        {
            Debug.LogWarning("KartBoost: ArcadeKart or Rigidbody not found on this GameObject — disabling.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// Attempts to begin a boost with an instant velocity impulse and raised-ceiling sustain window.
    /// </summary>
    /// <returns>True when the boost started; otherwise false.</returns>
    public bool Fire()
    {
        if (m_Phase != BoostPhase.Idle) return false;
        if (m_Kart == null || !m_Kart.CanMove) return false;
        if (!GameFlowManager.IsRaceActive) return false;

        Vector3 boostDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        m_Rigidbody.linearVelocity += boostDirection * impulseSpeedGain;

        m_ActivePowerup = new ArcadeKart.StatPowerup
        {
            PowerUpID = BoostPowerUpID,
            MaxTime = boostDuration,
            ElapsedTime = 0f,
            modifiers = new ArcadeKart.Stats
            {
                TopSpeed = boostTopSpeedBonus,
                Acceleration = boostAccelerationBonus,
                AccelerationCurve = 0f,
                Braking = 0f,
                CoastingDrag = 0f,
                AddedGravity = 0f,
                Grip = 0f,
                ReverseAcceleration = 0f,
                ReverseSpeed = 0f,
                Steer = 0f
            }
        };
        m_Kart.AddPowerup(m_ActivePowerup);

        m_Phase = BoostPhase.Sustain;
        m_PhaseTimer = 0f;
        BoostStarted?.Invoke();
        return true;
    }

    /// <summary>
    /// Aborts an in-progress boost: revokes the raised-ceiling powerup and skips straight to
    /// the settle phase. Safe to call even when not currently boosting.
    /// </summary>
    public void Cancel()
    {
        if (m_Phase == BoostPhase.Idle) return;

        bool wasSustaining = m_Phase == BoostPhase.Sustain;
        RevokeActivePowerup();

        m_Phase = BoostPhase.Settle;
        m_PhaseTimer = 0f;

        if (wasSustaining)
            BoostEnded?.Invoke();
    }

    private void FixedUpdate()
    {
        if (m_Phase == BoostPhase.Idle) return;

        m_PhaseTimer += Time.fixedDeltaTime;

        if (m_Phase == BoostPhase.Sustain)
        {
            if (m_PhaseTimer >= boostDuration)
            {
                RevokeActivePowerup();
                m_Phase = BoostPhase.Settle;
                m_PhaseTimer = 0f;
                BoostEnded?.Invoke();
            }
            return;
        }

        // Settle phase: force horizontal speed back down to base top speed, since ArcadeKart
        // never clamps over-max-speed velocity on its own while the throttle is held.
        Vector3 velocity = m_Rigidbody.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float horizontalSpeed = horizontal.magnitude;
        float baseTopSpeed = m_Kart.baseStats.TopSpeed;

        if (horizontalSpeed <= baseTopSpeed || m_PhaseTimer >= settleTimeout)
        {
            m_Phase = BoostPhase.Idle;
            return;
        }

        Vector3 horizontalDirection = horizontal / horizontalSpeed;
        float newHorizontalSpeed = Mathf.MoveTowards(horizontalSpeed, baseTopSpeed, settleDeceleration * Time.fixedDeltaTime);
        m_Rigidbody.linearVelocity = horizontalDirection * newHorizontalSpeed + Vector3.up * velocity.y;
    }

    private void RevokeActivePowerup()
    {
        if (m_ActivePowerup == null) return;

        // ArcadeKart exposes no RemovePowerup. Zero the modifiers and push ElapsedTime past
        // MaxTime so TickPowerups() drops it on the next physics step.
        m_ActivePowerup.modifiers = new ArcadeKart.Stats();
        m_ActivePowerup.ElapsedTime = m_ActivePowerup.MaxTime + 1f;
        m_ActivePowerup = null;
    }

    private void OnValidate()
    {
        impulseSpeedGain = Mathf.Max(0f, impulseSpeedGain);
        boostTopSpeedBonus = Mathf.Max(0f, boostTopSpeedBonus);
        boostAccelerationBonus = Mathf.Max(0f, boostAccelerationBonus);
        boostDuration = Mathf.Max(0.01f, boostDuration);
        settleDeceleration = Mathf.Max(0.01f, settleDeceleration);
        settleTimeout = Mathf.Max(0.01f, settleTimeout);
    }
}
