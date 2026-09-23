using UnityEngine;

/// <summary>
/// Attached to each racing kart root. Plays this character's boost voice cue together with the
/// shared boost blast cue on a dedicated per-kart source when this kart's <see cref="KartBoost"/>
/// fires, behaving identically on player and opponent karts. Mirrors the component lifecycle of
/// <see cref="KartBoostEffects"/>.
/// </summary>
public class KartBoostSound : MonoBehaviour
{
    [Tooltip("Dedicated boost-only AudioSource on this kart. Not the shared sfxSource used by CoinCollector/BombLauncher/KartCombatHandler, so the boost cues can layer without being cut off by other cues.")]
    [SerializeField] private AudioSource boostSfxSource;

    [Tooltip("This character's boost voice cue (e.g. Brutus-boost). Owns the attenuation applied to boostSfxSource, since both cues on this component share one source.")]
    [SerializeField] private SoundEffectSettings characterBoostSound;

    [Tooltip("Shared BoostBlastAndWhoosh cue. Only its Clip and VolumeScale are used — its distance/rolloff fields are ignored because it shares boostSfxSource with characterBoostSound, which owns the attenuation.")]
    [SerializeField] private SoundEffectSettings boostBlastSound;

    private KartBoost m_KartBoost;

    private void Awake()
    {
        m_KartBoost = GetComponent<KartBoost>();

        if (m_KartBoost == null)
        {
            Debug.LogWarning("KartBoostSound: No KartBoost component found on this GameObject — disabling.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (m_KartBoost != null)
            m_KartBoost.BoostStarted += PlayBoostSound;
    }

    private void OnDisable()
    {
        if (m_KartBoost != null)
            m_KartBoost.BoostStarted -= PlayBoostSound;
    }

    /// <summary>
    /// Plays the character boost cue and the boost blast cue together on this kart's dedicated
    /// boost source. Exposed publicly so the cue can be triggered manually for tuning.
    /// </summary>
    public void PlayBoostSound()
    {
        bool hasCharacterClip = characterBoostSound != null && characterBoostSound.Clip != null;
        bool hasBlastClip = boostBlastSound != null && boostBlastSound.Clip != null;

        if (boostSfxSource == null || (!hasCharacterClip && !hasBlastClip))
        {
            Debug.LogWarning("KartBoostSound: boostSfxSource is unassigned, or both characterBoostSound and boostBlastSound clips are unassigned — skipping boost sound.", this);
            return;
        }

        if (boostSfxSource.spatialBlend > 0f && hasCharacterClip)
            characterBoostSound.ApplySpatialSettings(boostSfxSource);

        // Intentionally no Stop() call here — unlike other cue players, these two cues must
        // layer on top of each other rather than cut each other off.
        if (hasCharacterClip)
            boostSfxSource.PlayOneShot(characterBoostSound.Clip, characterBoostSound.VolumeScale);

        if (hasBlastClip)
            boostSfxSource.PlayOneShot(boostBlastSound.Clip, boostBlastSound.VolumeScale);
    }
}
