using UnityEngine;

/// <summary>
/// Plain (non-MonoBehaviour) attack/decay intensity envelope, shared by the FOV punch and the
/// camera-lag push-back in <see cref="BoostCameraEffect"/>. Ramps a normalized 0-1
/// <see cref="Intensity"/> up fast on <see cref="Trigger"/>, decays it to zero over a total
/// duration, and supports easing back to zero early via <see cref="Release"/> (e.g. a bomb
/// cancels the boost mid-flight). Owned and ticked by a MonoBehaviour.
/// </summary>
public sealed class BoostCameraKick
{
    private const float MinDecayDuration = 0.01f;

    private float m_AttackDuration;
    private float m_TotalDuration;

    private bool m_IsReleasing;
    private float m_ElapsedTime;
    private float m_AttackStartIntensity;
    private float m_ReleaseStartIntensity;
    private float m_ReleaseDuration;
    private float m_ReleaseElapsedTime;

    /// <summary>Current 0-1 intensity driven by the envelope.</summary>
    public float Intensity { get; private set; }

    /// <summary>True while Intensity is still being driven by an attack, decay, or release.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Constructs the envelope with its attack and total durations. Both are clamped so the
    /// decay phase can never divide by zero — see <see cref="Configure"/>.
    /// </summary>
    /// <param name="attackDuration">Seconds to ramp from the current intensity up to 1.</param>
    /// <param name="totalDuration">Seconds from trigger to fully decayed back to 0.</param>
    public BoostCameraKick(float attackDuration, float totalDuration)
    {
        Configure(attackDuration, totalDuration);
    }

    /// <summary>Pushes updated serialized values into the envelope without reallocating.</summary>
    /// <param name="attackDuration">Seconds to ramp from the current intensity up to 1.</param>
    /// <param name="totalDuration">Seconds from trigger to fully decayed back to 0.</param>
    public void Configure(float attackDuration, float totalDuration)
    {
        m_AttackDuration = Mathf.Max(0f, attackDuration);
        m_TotalDuration = Mathf.Max(m_AttackDuration + MinDecayDuration, totalDuration);
    }

    /// <summary>
    /// Starts (or restarts) the attack from the current Intensity, not from zero, so
    /// retriggering mid-decay never visibly pops. Clears any in-progress release.
    /// </summary>
    public void Trigger()
    {
        m_AttackStartIntensity = Intensity;
        m_IsReleasing = false;
        m_ElapsedTime = 0f;
        IsActive = true;
    }

    /// <summary>
    /// Eases Intensity down to zero over releaseDuration instead of following the normal decay
    /// curve. A no-op on an inactive kick; treated as an immediate Reset() if releaseDuration is
    /// zero.
    /// </summary>
    /// <param name="releaseDuration">Seconds to ease down to zero.</param>
    public void Release(float releaseDuration)
    {
        if (!IsActive) return;

        if (releaseDuration <= 0f)
        {
            Reset();
            return;
        }

        m_ReleaseStartIntensity = Intensity;
        m_ReleaseDuration = releaseDuration;
        m_ReleaseElapsedTime = 0f;
        m_IsReleasing = true;
    }

    /// <summary>Snaps Intensity to 0 and goes inactive.</summary>
    public void Reset()
    {
        Intensity = 0f;
        IsActive = false;
        m_IsReleasing = false;
    }

    /// <summary>Advances the envelope. Call from the owner's Update.</summary>
    /// <param name="deltaTime">Elapsed time since the last tick.</param>
    public void Tick(float deltaTime)
    {
        if (!IsActive) return;

        if (m_IsReleasing)
        {
            m_ReleaseElapsedTime += deltaTime;
            if (m_ReleaseElapsedTime >= m_ReleaseDuration)
            {
                Reset();
                return;
            }

            Intensity = Mathf.Lerp(m_ReleaseStartIntensity, 0f, EaseOutCubic(m_ReleaseElapsedTime / m_ReleaseDuration));
            return;
        }

        m_ElapsedTime += deltaTime;

        if (m_ElapsedTime < m_AttackDuration)
        {
            Intensity = Mathf.Lerp(m_AttackStartIntensity, 1f, EaseOutCubic(m_ElapsedTime / m_AttackDuration));
            return;
        }

        float decayDuration = m_TotalDuration - m_AttackDuration;
        float decayNormalizedTime = (m_ElapsedTime - m_AttackDuration) / decayDuration;

        if (decayNormalizedTime >= 1f)
        {
            Reset();
            return;
        }

        Intensity = 1f - EaseInOutSine(decayNormalizedTime);
    }

    private static float EaseOutCubic(float normalizedTime)
    {
        return 1f - Mathf.Pow(1f - Mathf.Clamp01(normalizedTime), 3f);
    }

    private static float EaseInOutSine(float normalizedTime)
    {
        return -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(normalizedTime)) - 1f) / 2f;
    }
}
