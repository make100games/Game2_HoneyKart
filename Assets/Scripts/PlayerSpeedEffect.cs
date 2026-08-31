using UnityEngine;

/// <summary>
/// Attached to Main Camera. Drives the SpeedEffect particle child, bound at race start to the
/// human player's KartBoost only — never auto-discovered, so opponent karts can never trigger
/// it. RaceState.InitializeBoostMeterUI is the single injection point that hands over the
/// selected kart's components; this component follows the same Bind/Unbind contract as
/// BoostMeterUI.
/// </summary>
public class PlayerSpeedEffect : MonoBehaviour
{
    private const float DefaultLoopDuration = 1f;
    private const float DefaultDeactivateTimeout = 4f;
    private const string DefaultSpeedEffectChildName = "SpeedEffect";

    [Tooltip("Root of the SpeedEffect particle subtree. Optional; resolved by name under this transform when unset.")]
    [SerializeField] private Transform speedEffectRoot;

    [Tooltip("How long the looping speed-line systems stay looped before being cut, in seconds.")]
    [SerializeField] private float loopDuration = DefaultLoopDuration;

    [Tooltip("Safety cap on how long to wait for particles to die before force-deactivating the effect root.")]
    [SerializeField] private float deactivateTimeout = DefaultDeactivateTimeout;

    [Tooltip("Name of the SpeedEffect child used by the fallback lookup when speedEffectRoot is unset.")]
    [SerializeField] private string speedEffectChildName = DefaultSpeedEffectChildName;

    private BoostEffectRunner m_Runner;
    private KartBoost m_BoundBoost;

    private void Awake()
    {
        if (speedEffectRoot == null)
            speedEffectRoot = transform.Find(speedEffectChildName);

        m_Runner = new BoostEffectRunner(speedEffectRoot, loopDuration, deactivateTimeout);

        if (!m_Runner.IsValid)
            Debug.LogWarning("PlayerSpeedEffect: SpeedEffect child could not be found under Main Camera.", this);
    }

    /// <summary>
    /// Binds the effect to the given kart's boost events, unsubscribing from any previous kart
    /// first so this is safe to call more than once (RaceState.Enter() calls its initializer
    /// twice). Passing null acts as Unbind().
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

        m_Runner?.StopImmediate();
    }

    /// <summary>Unsubscribes from the bound kart and stops the effect immediately. Call on race exit.</summary>
    public void Unbind()
    {
        Bind(null);
    }

    private void Update()
    {
        m_Runner?.Tick(Time.deltaTime);
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void HandleBoostStarted()
    {
        m_Runner.Trigger();
    }

    private void HandleBoostCancelled()
    {
        m_Runner.CutLoops();
    }

    private void OnValidate()
    {
        loopDuration = Mathf.Max(0f, loopDuration);
        deactivateTimeout = Mathf.Max(0f, deactivateTimeout);
    }
}
