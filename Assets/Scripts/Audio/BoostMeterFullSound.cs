using UnityEngine;

/// <summary>
/// Scene-level component that plays the non-spatial meter-full cue for the human player's kart
/// only, bound at race start to the selected kart's <see cref="BoostMeter"/> — never
/// auto-discovered, so opponent karts can never trigger it. RaceState.InitializeBoostMeterUI is
/// the single injection point; this component follows the same Bind/Unbind contract as
/// <see cref="PlayerSpeedEffect"/> and <see cref="BoostCameraEffect"/>.
/// </summary>
public class BoostMeterFullSound : MonoBehaviour
{
    [Tooltip("Dedicated 2D AudioSource for this cue, kept separate from the shared /Game Sound Effects menu source.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Non-spatial cue played the moment the meter reaches full, at the start of the hold before the boost fires.")]
    [SerializeField] private SoundEffectSettings meterFullSound;

    private BoostMeter m_BoundMeter;

    /// <summary>
    /// Binds this cue to the given kart's boost meter, unsubscribing from any previously bound
    /// meter first so this is safe to call more than once (RaceState.Enter() calls its
    /// initializer twice). Passing null acts as Unbind().
    /// </summary>
    /// <param name="boostMeter">The human player's BoostMeter, or null to unbind.</param>
    public void Bind(BoostMeter boostMeter)
    {
        if (m_BoundMeter != null)
            m_BoundMeter.MeterFull -= HandleMeterFull;

        m_BoundMeter = boostMeter;

        if (m_BoundMeter != null)
            m_BoundMeter.MeterFull += HandleMeterFull;
    }

    /// <summary>Unsubscribes from the bound meter. Call on race exit.</summary>
    public void Unbind()
    {
        Bind(null);
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void HandleMeterFull()
    {
        if (sfxSource == null || meterFullSound == null || meterFullSound.Clip == null)
        {
            Debug.LogWarning("BoostMeterFullSound: sfxSource or meterFullSound clip is unassigned — skipping meter-full sound.", this);
            return;
        }

        // ApplySpatialSettings is skipped because this source is 2D, matching the
        // spatialBlend > 0f guard used everywhere else.
        sfxSource.Stop();
        sfxSource.PlayOneShot(meterFullSound.Clip, meterFullSound.VolumeScale);
    }
}
