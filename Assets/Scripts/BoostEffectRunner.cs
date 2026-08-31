using UnityEngine;

/// <summary>
/// Plain (non-MonoBehaviour) runner that owns one particle-effect subtree and drives the
/// activate -> loop -> unloop -> settle -> deactivate cycle. Owned and ticked by a
/// MonoBehaviour (e.g. <see cref="KartBoostEffects"/> or <see cref="PlayerSpeedEffect"/>),
/// since a disabled effect root would never receive its own Awake/Update.
/// </summary>
public sealed class BoostEffectRunner
{
    private enum RunnerState
    {
        Idle,
        Looping,
        Fading
    }

    private readonly Transform m_EffectRoot;
    private readonly ParticleSystem[] m_AllSystems;
    private readonly ParticleSystem[] m_LoopingSystems;
    private readonly ParticleSystem[] m_OneShotSystems;
    private readonly float m_LoopDuration;
    private readonly float m_DeactivateTimeout;

    private RunnerState m_State = RunnerState.Idle;
    private float m_LoopTimer;
    private float m_FadeTimer;

    /// <summary>False when the effect root is null or its subtree contains no ParticleSystem.</summary>
    public bool IsValid { get; }

    /// <summary>True while the effect is looping or fading out; false once fully idle.</summary>
    public bool IsRunning => m_State != RunnerState.Idle;

    /// <summary>
    /// Captures the effect subtree and its authored loop flags. The authored main.loop value
    /// is read once here, because Trigger()/CutLoops() mutate it at runtime.
    /// </summary>
    /// <param name="effectRoot">Root transform of the particle-effect subtree.</param>
    /// <param name="loopDuration">Seconds the looping systems stay looped before being cut.</param>
    /// <param name="deactivateTimeout">Safety cap on how long to wait for particles to die before force-deactivating.</param>
    public BoostEffectRunner(Transform effectRoot, float loopDuration, float deactivateTimeout)
    {
        m_EffectRoot = effectRoot;
        m_LoopDuration = Mathf.Max(0f, loopDuration);
        m_DeactivateTimeout = Mathf.Max(0f, deactivateTimeout);

        if (m_EffectRoot == null)
        {
            m_AllSystems = System.Array.Empty<ParticleSystem>();
            m_LoopingSystems = System.Array.Empty<ParticleSystem>();
            m_OneShotSystems = System.Array.Empty<ParticleSystem>();
            IsValid = false;
            return;
        }

        m_AllSystems = m_EffectRoot.GetComponentsInChildren<ParticleSystem>(true);

        int loopingCount = 0;
        for (int i = 0; i < m_AllSystems.Length; i++)
        {
            if (m_AllSystems[i].main.loop)
                loopingCount++;
        }

        m_LoopingSystems = new ParticleSystem[loopingCount];
        m_OneShotSystems = new ParticleSystem[m_AllSystems.Length - loopingCount];
        int loopingIndex = 0;
        int oneShotIndex = 0;
        for (int i = 0; i < m_AllSystems.Length; i++)
        {
            ParticleSystem particleSystem = m_AllSystems[i];
            if (particleSystem.main.loop)
                m_LoopingSystems[loopingIndex++] = particleSystem;
            else
                m_OneShotSystems[oneShotIndex++] = particleSystem;
        }

        IsValid = m_AllSystems.Length > 0;
    }

    /// <summary>Hard restart: activates the root and replays every system from frame zero.</summary>
    public void Trigger()
    {
        if (!IsValid) return;

        for (int i = 0; i < m_LoopingSystems.Length; i++)
        {
            ParticleSystem.MainModule main = m_LoopingSystems[i].main;
            main.loop = true;
        }

        m_EffectRoot.gameObject.SetActive(true);

        for (int i = 0; i < m_AllSystems.Length; i++)
        {
            ParticleSystem particleSystem = m_AllSystems[i];
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(false);
        }

        m_LoopTimer = 0f;
        m_FadeTimer = 0f;
        m_State = RunnerState.Looping;
    }

    /// <summary>Immediately clears main.loop on the looping systems so they peter out now (e.g. bomb hit).</summary>
    public void CutLoops()
    {
        if (!IsValid) return;

        for (int i = 0; i < m_LoopingSystems.Length; i++)
        {
            ParticleSystem.MainModule main = m_LoopingSystems[i].main;
            main.loop = false;
        }
    }

    /// <summary>Clears, stops, and deactivates the root immediately with no fade-out tail.</summary>
    public void StopImmediate()
    {
        if (!IsValid) return;

        for (int i = 0; i < m_AllSystems.Length; i++)
        {
            m_AllSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        m_EffectRoot.gameObject.SetActive(false);
        m_State = RunnerState.Idle;
        m_LoopTimer = 0f;
        m_FadeTimer = 0f;
    }

    /// <summary>Advances the loop timer and the deactivation check. Call from the owner's Update.</summary>
    /// <param name="deltaTime">Elapsed time since the last tick.</param>
    public void Tick(float deltaTime)
    {
        if (!IsValid || m_State == RunnerState.Idle) return;

        if (m_State == RunnerState.Looping)
        {
            m_LoopTimer += deltaTime;
            if (m_LoopTimer >= m_LoopDuration)
            {
                CutLoops();
                m_FadeTimer = 0f;
                m_State = RunnerState.Fading;
            }
            return;
        }

        // Fading: wait for every system to finish emitting/living, or hit the safety timeout.
        m_FadeTimer += deltaTime;

        bool anyAlive = false;
        for (int i = 0; i < m_AllSystems.Length; i++)
        {
            if (m_AllSystems[i].IsAlive(false))
            {
                anyAlive = true;
                break;
            }
        }

        if (!anyAlive || m_FadeTimer >= m_DeactivateTimeout)
        {
            m_EffectRoot.gameObject.SetActive(false);
            m_State = RunnerState.Idle;
        }
    }
}
