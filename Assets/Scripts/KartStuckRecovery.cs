using KartGame.AI;
using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Deterministic scripted recovery for an AI kart wedged against an obstacle (typically the track
/// fence). Detects a stall, reverses, then steers away from the obstacle, and hands control back to
/// the policy — entirely by pushing an InputData override into KartAgent.SetInputOverride/
/// ClearInputOverride. KartAgent never references this type in any form (no GetComponent, no field,
/// no interface satisfied only by it), mirroring AgentCoinRewardBridge -> KartAgent.NotifyCoinCollected():
/// the dependency edge only ever runs from /Assets/Scripts towards /Assets/Karting, never back.
/// </summary>
public class KartStuckRecovery : MonoBehaviour
{
    private enum RecoveryState
    {
        Idle,
        Reversing,
        TurningAway,
        Cooldown
    }

    private const float StuckSpeedThreshold = 0.05f;
    private const float StuckDetectionSeconds = 0.75f;
    private const float ReverseDurationSeconds = 0.9f;
    private const float TurnAwayDurationSeconds = 0.7f;
    private const float CooldownSeconds = 1.0f;
    private const float ResumeSpeedThreshold = 0.25f;
    private const float SustainedResumeSeconds = 1.0f;
    private const float RearProbeAbortDistance = 0.5f; // fraction of probeDistance

    [SerializeField] private KartAgent kartAgent;
    [SerializeField] private ArcadeKart kart;

    [Tooltip("Length of the escape-direction side probes.")]
    [SerializeField] private float probeDistance = 6f;

    [Tooltip("Layers considered obstacles for the escape probes. Should mirror KartAgent.Mask, including Fence.")]
    [SerializeField] private LayerMask obstacleMask;

    [Tooltip("After this many consecutive failed recovery cycles, fall back to a checkpoint teleport.")]
    [SerializeField] private int maxConsecutiveAttempts = 3;

    [Tooltip("When false (default), the recovery state machine only runs while KartAgent.Mode is Inferencing — " +
             "during training the non-terminal contact penalty should shape the policy, not a scripted override.")]
    [SerializeField] private bool enableDuringTraining = false;

    /// <summary>True while a scripted recovery manoeuvre (reverse or turn-away) is active. For diagnostics/HUD.</summary>
    public bool IsRecovering => m_State == RecoveryState.Reversing || m_State == RecoveryState.TurningAway;

    private RecoveryState m_State = RecoveryState.Idle;
    private float m_StateTimer;
    private float m_StuckTimer;
    private float m_SustainedResumeTimer;
    private int m_ConsecutiveAttempts;
    private float m_EscapeSteer = 1f;
    private bool m_LoggedMissingMask;

    private void Awake()
    {
        if (kartAgent == null) kartAgent = GetComponent<KartAgent>();
        if (kart == null) kart = GetComponent<ArcadeKart>();

        if (kartAgent == null || kart == null)
        {
            Debug.LogError($"KartStuckRecovery: missing KartAgent or ArcadeKart on {gameObject.name} — disabling.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        kartAgent?.ClearInputOverride();
    }

    private void OnDestroy()
    {
        kartAgent?.ClearInputOverride();
    }

    private void Update()
    {
        if (kartAgent == null || kart == null) return;

        if (obstacleMask.value == 0)
        {
            if (!m_LoggedMissingMask)
            {
                Debug.LogWarning($"KartStuckRecovery: obstacleMask is empty on {gameObject.name} — recovery disabled.", this);
                m_LoggedMissingMask = true;
            }
            return;
        }

        bool allowed = kartAgent.Mode == AgentMode.Inferencing || enableDuringTraining;
        if (!allowed || !kart.CanMove)
        {
            m_State = RecoveryState.Idle;
            m_StuckTimer = 0f;
            kartAgent.ClearInputOverride();
            return;
        }

        switch (m_State)
        {
            case RecoveryState.Idle:
                TickIdle();
                kartAgent.ClearInputOverride();
                break;
            case RecoveryState.Reversing:
                TickReversing();
                break;
            case RecoveryState.TurningAway:
                TickTurningAway();
                break;
            case RecoveryState.Cooldown:
                TickCooldown();
                kartAgent.ClearInputOverride();
                break;
        }
    }

    /// <summary>
    /// Accumulates a stall timer while the kart is barely moving despite the policy asking to
    /// accelerate. Reads only the cached LastPolicyInput — never GenerateInput, which would either
    /// recurse through this component's own override or desync the policy's input cadence.
    /// </summary>
    private void TickIdle()
    {
        bool stalled = Mathf.Abs(kart.LocalSpeed()) < StuckSpeedThreshold && kartAgent.LastPolicyInput.Accelerate;

        if (stalled)
        {
            m_StuckTimer += Time.deltaTime;
            if (m_StuckTimer >= StuckDetectionSeconds)
            {
                m_EscapeSteer = ComputeEscapeSteer();
                m_State = RecoveryState.Reversing;
                m_StateTimer = 0f;
                m_StuckTimer = 0f;
            }
        }
        else
        {
            m_StuckTimer = 0f;
        }
    }

    /// <summary>
    /// ArcadeKart.MoveVehicle computes accelInput = -1 while reversing and applies
    /// turnAngle * transform.forward * accelInput, so steering sign inverts under reverse thrust —
    /// hence the negated escape steer here swings the nose toward the escape side.
    /// </summary>
    private void TickReversing()
    {
        m_StateTimer += Time.deltaTime;
        kartAgent.SetInputOverride(new InputData { Accelerate = false, Brake = true, TurnInput = -m_EscapeSteer });

        bool rearBlocked = ProbeDistance(-transform.forward) < probeDistance * RearProbeAbortDistance;

        if (m_StateTimer >= ReverseDurationSeconds || rearBlocked)
        {
            m_State = RecoveryState.TurningAway;
            m_StateTimer = 0f;
        }
    }

    private void TickTurningAway()
    {
        m_StateTimer += Time.deltaTime;
        kartAgent.SetInputOverride(new InputData { Accelerate = true, Brake = false, TurnInput = m_EscapeSteer });

        bool forwardClear = ProbeDistance(transform.forward) >= probeDistance * 0.9f;
        bool resumedSpeed = Mathf.Abs(kart.LocalSpeed()) > ResumeSpeedThreshold;

        if (m_StateTimer >= TurnAwayDurationSeconds || (resumedSpeed && forwardClear))
        {
            m_State = RecoveryState.Cooldown;
            m_StateTimer = 0f;
            m_SustainedResumeTimer = 0f;
        }
    }

    private void TickCooldown()
    {
        m_StateTimer += Time.deltaTime;

        bool stalledAgain = Mathf.Abs(kart.LocalSpeed()) < StuckSpeedThreshold && kartAgent.LastPolicyInput.Accelerate;

        if (Mathf.Abs(kart.LocalSpeed()) > ResumeSpeedThreshold)
        {
            m_SustainedResumeTimer += Time.deltaTime;
            if (m_SustainedResumeTimer >= SustainedResumeSeconds)
                m_ConsecutiveAttempts = 0;
        }
        else
        {
            m_SustainedResumeTimer = 0f;
        }

        if (m_StateTimer < CooldownSeconds)
        {
            if (stalledAgain)
            {
                m_ConsecutiveAttempts++;
                if (m_ConsecutiveAttempts > maxConsecutiveAttempts)
                {
                    kartAgent.RepositionToCurrentCheckpoint();
                    m_ConsecutiveAttempts = 0;
                    m_State = RecoveryState.Idle;
                    m_StateTimer = 0f;
                }
            }
            return;
        }

        m_State = RecoveryState.Idle;
        m_StateTimer = 0f;
    }

    /// <summary>
    /// Casts probes at -60, -30, +30, +60 degrees about the kart's up axis from its forward direction,
    /// and sums the free (unobstructed) distance per side. Returns +1 to escape toward the roomier
    /// right side, -1 for the left. Ties (including both sides fully blocked) break toward the racing
    /// line via the sign of the dot between right and the direction to the next checkpoint, defaulting
    /// to +1 when that is also zero.
    /// </summary>
    private float ComputeEscapeSteer()
    {
        float rightFree = ProbeDistance(RotateAboutUp(30f)) + ProbeDistance(RotateAboutUp(60f));
        float leftFree = ProbeDistance(RotateAboutUp(-30f)) + ProbeDistance(RotateAboutUp(-60f));

        if (!Mathf.Approximately(rightFree, leftFree))
            return rightFree > leftFree ? 1f : -1f;

        float tieBreak = Mathf.Sign(Vector3.Dot(transform.right, kartAgent.DirectionToNextCheckpoint));
        return Mathf.Approximately(tieBreak, 0f) ? 1f : tieBreak;
    }

    private Vector3 RotateAboutUp(float degrees) => Quaternion.AngleAxis(degrees, transform.up) * transform.forward;

    /// <summary>Returns the free distance along direction from the sensor origin, up to probeDistance.</summary>
    private float ProbeDistance(Vector3 direction)
    {
        Vector3 origin = kartAgent.AgentSensorTransform != null ? kartAgent.AgentSensorTransform.position : transform.position;
        if (Physics.Raycast(origin, direction, out var hitInfo, probeDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            return hitInfo.distance;
        return probeDistance;
    }
}
