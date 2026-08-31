using UnityEngine;

/// <summary>
/// Attached to the kart root. Drives the kart's own BoostEffect particle child in response to
/// this kart's KartBoost, behaving identically on player and opponent karts. The driver itself
/// lives on the always-active kart root because BoostEffect ships disabled — a MonoBehaviour on
/// a disabled GameObject would never receive Awake/Update to re-enable itself.
/// </summary>
public class KartBoostEffects : MonoBehaviour
{
    private const float DefaultLoopDuration = 1f;
    private const float DefaultDeactivateTimeout = 4f;
    private const string DefaultBoostEffectChildName = "BoostEffect";

    [Tooltip("Root of the BoostEffect particle subtree. Optional; resolved by name under this transform when unset.")]
    [SerializeField] private Transform boostEffectRoot;

    [Tooltip("How long the looping thruster systems stay looped before being cut, in seconds.")]
    [SerializeField] private float loopDuration = DefaultLoopDuration;

    [Tooltip("Safety cap on how long to wait for particles to die before force-deactivating the effect root.")]
    [SerializeField] private float deactivateTimeout = DefaultDeactivateTimeout;

    [Tooltip("Name of the BoostEffect child used by the fallback lookup when boostEffectRoot is unset.")]
    [SerializeField] private string boostEffectChildName = DefaultBoostEffectChildName;

    private KartBoost m_KartBoost;
    private BoostEffectRunner m_Runner;

    private void Awake()
    {
        m_KartBoost = GetComponent<KartBoost>();

        if (boostEffectRoot == null)
            boostEffectRoot = transform.Find(boostEffectChildName);

        m_Runner = new BoostEffectRunner(boostEffectRoot, loopDuration, deactivateTimeout);

        if (m_KartBoost == null || !m_Runner.IsValid)
        {
            Debug.LogWarning("KartBoostEffects: KartBoost or BoostEffect child not found on this GameObject — disabling.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (m_KartBoost != null)
        {
            m_KartBoost.BoostStarted += PlayBoostEffect;
            m_KartBoost.BoostCancelled += HandleBoostCancelled;
        }

        m_Runner?.StopImmediate();
    }

    private void OnDisable()
    {
        if (m_KartBoost != null)
        {
            m_KartBoost.BoostStarted -= PlayBoostEffect;
            m_KartBoost.BoostCancelled -= HandleBoostCancelled;
        }

        m_Runner?.StopImmediate();
    }

    private void Update()
    {
        m_Runner.Tick(Time.deltaTime);
    }

    private void HandleBoostCancelled()
    {
        m_Runner.CutLoops();
    }

    /// <summary>Manually triggers the boost particle effect, e.g. from a debug key or sound work.</summary>
    public void PlayBoostEffect()
    {
        m_Runner.Trigger();
    }

    private void OnValidate()
    {
        loopDuration = Mathf.Max(0f, loopDuration);
        deactivateTimeout = Mathf.Max(0f, deactivateTimeout);
    }
}
