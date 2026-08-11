using UnityEngine;

/// <summary>
/// Serializable settings bundle that keeps a sound effect's clip together with its
/// playback and 3D-attenuation controls. Assign one instance per gameplay cue on the
/// owning MonoBehaviour so each effect can be tuned independently without adding
/// dedicated AudioSources.
/// </summary>
[System.Serializable]
public class SoundEffectSettings
{
    private const float DefaultVolumeScale = 1.3f;
    private const float DefaultMinDistance = 1f;
    private const float DefaultMaxDistance = 10f;
    private const float MinAllowedDistance = 0.01f;

    [Tooltip("Sound effect clip played for this cue.")]
    [SerializeField] private AudioClip clip;

    [Tooltip("Volume scale passed to PlayOneShot; not clamped to the AudioSource's own 0-1 volume ceiling.")]
    [SerializeField] private float volumeScale = DefaultVolumeScale;

    [Tooltip("Minimum distance at which this cue stops growing louder. Only applied when the target source is spatial (spatialBlend > 0).")]
    [SerializeField] private float minDistance = DefaultMinDistance;

    [Tooltip("Maximum distance at which this cue becomes inaudible. Only applied when the target source is spatial (spatialBlend > 0).")]
    [SerializeField] private float maxDistance = DefaultMaxDistance;

    [Tooltip("Rolloff curve used while this cue plays. Only applied when the target source is spatial (spatialBlend > 0).")]
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    /// <summary>The clip played for this cue.</summary>
    public AudioClip Clip => clip;

    /// <summary>The validated, non-negative PlayOneShot volume scale configured for this cue.</summary>
    public float VolumeScale => Mathf.Max(0f, volumeScale);

    /// <summary>The validated minimum attenuation distance, always greater than zero.</summary>
    private float ValidatedMinDistance => Mathf.Max(MinAllowedDistance, minDistance);

    /// <summary>The validated maximum attenuation distance, never lower than the minimum distance.</summary>
    private float ValidatedMaxDistance => Mathf.Max(ValidatedMinDistance, maxDistance);

    /// <summary>
    /// Applies this cue's validated minDistance, maxDistance, and rolloffMode to the given
    /// 3D AudioSource immediately before playback. Safely does nothing if the source is null.
    /// This is only meaningful for spatial sources (spatialBlend > 0); callers should skip it
    /// for 2D sources so shared settings code cannot accidentally reintroduce attenuation.
    /// </summary>
    /// <param name="source">The reusable AudioSource that is about to play this cue.</param>
    public void ApplySpatialSettings(AudioSource source)
    {
        if (source == null)
            return;

        source.minDistance = ValidatedMinDistance;
        source.maxDistance = ValidatedMaxDistance;
        source.rolloffMode = rolloffMode;
    }
}
