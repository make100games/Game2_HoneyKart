using Cinemachine;
using UnityEngine;

/// <summary>
/// Attached to the race follow VCam. Drives a brief field-of-view punch and a camera-lag
/// push-back/damping-spike on the human player's boost, bound at race start to the selected
/// kart's KartBoost only — never auto-discovered, so opponent karts can never move the
/// camera. RaceState.InitializeBoostCameraEffect is the single injection point and follows
/// the same Bind/Unbind contract as PlayerSpeedEffect.
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class BoostCameraEffect : MonoBehaviour
{
    private const float DefaultFieldOfViewMultiplier = 1.15f;
    private const float DefaultFieldOfViewDuration = 1.5f;
    private const float DefaultFieldOfViewAttackDuration = 0.18f;
    private const float DefaultFollowPushBackDistance = 1.6f;
    private const float DefaultLagDuration = 1.2f;
    private const float DefaultLagAttackDuration = 0.1f;
    private const float DefaultZDampingMultiplier = 3f;
    private const float DefaultCancelReleaseDuration = 0.35f;

    [Header("Field Of View")]
    [Tooltip("Multiplier applied to the vcam's base field of view at full intensity (e.g. 1.15 turns 60 into 69).")]
    [SerializeField] private float fieldOfViewMultiplier = DefaultFieldOfViewMultiplier;

    [Tooltip("Full punch-out-and-back duration for the field-of-view widen, in seconds.")]
    [SerializeField] private float fieldOfViewDuration = DefaultFieldOfViewDuration;

    [Tooltip("How fast the field-of-view widening snaps in, in seconds.")]
    [SerializeField] private float fieldOfViewAttackDuration = DefaultFieldOfViewAttackDuration;

    [Header("Camera Lag")]
    [Tooltip("Extra units the camera drops behind the kart at full intensity, on top of the transposer's baseline follow offset.")]
    [SerializeField] private float followPushBackDistance = DefaultFollowPushBackDistance;

    [Tooltip("Full drop-back-and-catch-up duration, in seconds. Deliberately shorter than fieldOfViewDuration.")]
    [SerializeField] private float lagDuration = DefaultLagDuration;

    [Tooltip("How fast the camera drops back, in seconds.")]
    [SerializeField] private float lagAttackDuration = DefaultLagAttackDuration;

    [Tooltip("Multiplier applied to the transposer's Z damping at full intensity, making the catch-up read as reluctant rather than mechanical.")]
    [SerializeField] private float zDampingMultiplier = DefaultZDampingMultiplier;

    [Header("Cancellation")]
    [Tooltip("How fast the field-of-view and lag effects ease back to normal when the boost is cancelled early (e.g. a bomb hit).")]
    [SerializeField] private float cancelReleaseDuration = DefaultCancelReleaseDuration;

    [Tooltip("The race follow VCam this effect drives. Resolved via GetComponent when unset.")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    private BoostCameraKick m_FieldOfViewKick;
    private BoostCameraKick m_LagKick;
    private KartBoost m_BoundBoost;

    private CinemachineTransposer m_Transposer;
    private bool m_BaselinesCaptured;
    private bool m_IsPerturbed;
    private bool m_RigWarningLogged;
    private float m_BaseFieldOfView;
    private Vector3 m_BaseFollowOffset;
    private float m_BaseZDamping;

    private void Awake()
    {
        if (virtualCamera == null)
            virtualCamera = GetComponent<CinemachineVirtualCamera>();

        if (virtualCamera == null)
        {
            Debug.LogWarning("[BoostCameraEffect] virtualCamera could not be resolved — disabling.", this);
            enabled = false;
            return;
        }

        m_FieldOfViewKick = new BoostCameraKick(fieldOfViewAttackDuration, fieldOfViewDuration);
        m_LagKick = new BoostCameraKick(lagAttackDuration, lagDuration);
    }

    /// <summary>
    /// Binds the effect to the given kart's boost events, unsubscribing from any previous kart
    /// first so this is safe to call more than once (RaceState calls its initializer twice).
    /// Passing null acts as Unbind().
    /// </summary>
    /// <param name="playerBoost">The human player's KartBoost, or null to unbind.</param>
    public void Bind(KartBoost playerBoost)
    {
        if (m_BoundBoost != null)
        {
            m_BoundBoost.BoostStarted -= HandleBoostStarted;
            m_BoundBoost.BoostCancelled -= HandleBoostCancelled;
        }

        m_BoundBoost = playerBoost;

        if (m_BoundBoost != null)
        {
            m_BoundBoost.BoostStarted += HandleBoostStarted;
            m_BoundBoost.BoostCancelled += HandleBoostCancelled;
        }

        RestoreBaseline();
        m_IsPerturbed = false;
    }

    /// <summary>Unsubscribes from the bound kart and restores the baseline lens/offset. Call on race exit.</summary>
    public void Unbind()
    {
        Bind(null);
    }

    private void Update()
    {
        if (m_FieldOfViewKick == null || m_LagKick == null) return;

        m_FieldOfViewKick.Configure(fieldOfViewAttackDuration, fieldOfViewDuration);
        m_LagKick.Configure(lagAttackDuration, lagDuration);

        m_FieldOfViewKick.Tick(Time.deltaTime);
        m_LagKick.Tick(Time.deltaTime);

        if (!m_FieldOfViewKick.IsActive && !m_LagKick.IsActive)
        {
            if (m_IsPerturbed)
            {
                RestoreBaseline();
                m_IsPerturbed = false;
            }
            return;
        }

        if (!TryResolveRig()) return;

        LensSettings lens = virtualCamera.m_Lens;
        lens.FieldOfView = m_BaseFieldOfView * Mathf.Lerp(1f, fieldOfViewMultiplier, m_FieldOfViewKick.Intensity);
        virtualCamera.m_Lens = lens;

        if (m_Transposer != null)
        {
            Vector3 followOffset = m_BaseFollowOffset;
            followOffset.z -= followPushBackDistance * m_LagKick.Intensity;
            m_Transposer.m_FollowOffset = followOffset;
            m_Transposer.m_ZDamping = m_BaseZDamping * Mathf.Lerp(1f, zDampingMultiplier, m_LagKick.Intensity);
        }

        m_IsPerturbed = true;
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void HandleBoostStarted()
    {
        m_FieldOfViewKick.Trigger();
        m_LagKick.Trigger();
    }

    private void HandleBoostCancelled()
    {
        m_FieldOfViewKick.Release(cancelReleaseDuration);
        m_LagKick.Release(cancelReleaseDuration);
    }

    /// <summary>
    /// Lazily resolves the transposer and captures baselines exactly once. Must not run in
    /// Awake — Cinemachine builds its hidden pipeline in its own Awake/OnEnable, so
    /// GetCinemachineComponent can legitimately return null before that.
    /// </summary>
    /// <returns>True once the vcam's field of view baseline is available (transposer optional).</returns>
    private bool TryResolveRig()
    {
        if (m_BaselinesCaptured) return true;

        if (virtualCamera == null) return false;

        m_Transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();

        if (m_Transposer == null && !m_RigWarningLogged)
        {
            Debug.LogWarning("[BoostCameraEffect] No CinemachineTransposer found on the Body — camera-lag push-back disabled, field-of-view punch still active.", this);
            m_RigWarningLogged = true;
        }

        m_BaseFieldOfView = virtualCamera.m_Lens.FieldOfView;

        if (m_Transposer != null)
        {
            m_BaseFollowOffset = m_Transposer.m_FollowOffset;
            m_BaseZDamping = m_Transposer.m_ZDamping;
        }

        m_BaselinesCaptured = true;
        return true;
    }

    /// <summary>Writes the captured baselines back to the lens and transposer, and resets both kicks.</summary>
    private void RestoreBaseline()
    {
        m_FieldOfViewKick?.Reset();
        m_LagKick?.Reset();

        if (!m_BaselinesCaptured || virtualCamera == null) return;

        LensSettings lens = virtualCamera.m_Lens;
        lens.FieldOfView = m_BaseFieldOfView;
        virtualCamera.m_Lens = lens;

        if (m_Transposer != null)
        {
            m_Transposer.m_FollowOffset = m_BaseFollowOffset;
            m_Transposer.m_ZDamping = m_BaseZDamping;
        }
    }

    private void OnValidate()
    {
        fieldOfViewMultiplier = Mathf.Max(1f, fieldOfViewMultiplier);
        fieldOfViewAttackDuration = Mathf.Max(0f, fieldOfViewAttackDuration);
        fieldOfViewDuration = Mathf.Max(fieldOfViewAttackDuration + 0.01f, fieldOfViewDuration);

        lagAttackDuration = Mathf.Max(0f, lagAttackDuration);
        lagDuration = Mathf.Max(lagAttackDuration + 0.01f, lagDuration);
        followPushBackDistance = Mathf.Max(0f, followPushBackDistance);
        zDampingMultiplier = Mathf.Max(1f, zDampingMultiplier);

        cancelReleaseDuration = Mathf.Max(0f, cancelReleaseDuration);
    }
}
