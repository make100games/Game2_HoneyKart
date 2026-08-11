using KartGame.AI;
using UnityEngine;

/// <summary>
/// Attached to the player kart. Listens for Space input, calculates a
/// physics-based launch velocity, and fires bomb projectiles from a
/// configurable LaunchPoint transform. The bomb inherits the kart's
/// current velocity and is given a random spin on launch.
/// </summary>
public class BombLauncher : MonoBehaviour
{
    private const float DefaultMinSpinDegreesPerSecond = 90f;
    private const float DefaultMaxSpinDegreesPerSecond = 720f;

    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private Transform launchPoint;
    [SerializeField] private int maxBombs = 10;
    [SerializeField] private float targetRange = 30f;
    [SerializeField] [Range(5f, 85f)] private float launchAngle = 35f;
    [SerializeField] private float minSpinDegreesPerSecond = DefaultMinSpinDegreesPerSecond;
    [SerializeField] private float maxSpinDegreesPerSecond = DefaultMaxSpinDegreesPerSecond;

    [Header("Sound Effects")]
    [Tooltip("Shared spatial AudioSource on this kart used for gameplay sound effects.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Sound effect played when this kart collects an item box, with its own volume and attenuation controls.")]
    [SerializeField] private SoundEffectSettings itemCollectedSound;

    [Tooltip("Sound effect played when this kart fires a bomb, with its own volume and attenuation controls.")]
    [SerializeField] private SoundEffectSettings bombFireSound;

    /// <summary>Current number of bombs available to fire.</summary>
    public int RemainingBombs { get; private set; }

    private KartAgent kartAgent;
    private bool isAIControlled;

    /// <summary>
    /// Grants additional bombs to this kart, capping the total at
    /// <see cref="maxBombs"/>. Non-positive counts are ignored.
    /// </summary>
    /// <param name="count">Number of bombs to add.</param>
    public void AddBombs(int count)
    {
        if (count <= 0)
            return;

        RemainingBombs = Mathf.Min(RemainingBombs + count, maxBombs);
        NotifyAgentBombCount();
    }

    private Rigidbody kartRigidbody;

    /// <summary>
    /// Plays this kart's item-collected sound effect. Called by <see cref="ItemBox"/> once
    /// after a successful bomb grant so the collecting kart's spatial source carries the cue.
    /// </summary>
    public void PlayItemCollectedSound()
    {
        PlaySfx(itemCollectedSound);
    }

    private void Awake()
    {
        RemainingBombs = 0;
        kartRigidbody = GetComponent<Rigidbody>();
        kartAgent = GetComponent<KartAgent>();
        isAIControlled = kartAgent != null;

        if (isAIControlled)
        {
            kartAgent.FireRequested += HandleFireRequested;
            kartAgent.SetAvailableBombs(RemainingBombs);
        }
    }

    private void Update()
    {
        if (!isAIControlled && Input.GetKeyDown(KeyCode.Space))
            TryFireBomb();
    }

    private void FireBomb()
    {
        RemainingBombs--;

        GameObject instance = Instantiate(bombPrefab, launchPoint.position, launchPoint.rotation);

        BombProjectile projectile = instance.GetComponent<BombProjectile>();
        if (projectile != null)
        {
            projectile.SetLauncherColliders(GetComponentsInChildren<Collider>());
        }

        PlaySfx(bombFireSound);

        float angleRad = launchAngle * Mathf.Deg2Rad;
        float gravity = Physics.gravity.magnitude;
        float speed = Mathf.Sqrt(targetRange * gravity / Mathf.Sin(2f * angleRad));
        Vector3 velocity = (transform.forward * Mathf.Cos(angleRad) + Vector3.up * Mathf.Sin(angleRad)) * speed;

        // Inherit the firing kart's momentum so the bomb travels with the kart.
        if (kartRigidbody != null)
        {
            velocity += kartRigidbody.linearVelocity;
        }

        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = velocity;

            // Random tumble. angularVelocity is radians/second, so convert from the
            // designer-facing degrees/second range.
            float spinDegreesPerSecond = Random.Range(minSpinDegreesPerSecond, maxSpinDegreesPerSecond);
            rb.angularVelocity = Random.onUnitSphere * (spinDegreesPerSecond * Mathf.Deg2Rad);
        }

        NotifyAgentBombCount();
    }

    private void TryFireBomb()
    {
        if (RemainingBombs > 0)
            FireBomb();
    }

    private void HandleFireRequested() => TryFireBomb();

    private void NotifyAgentBombCount()
    {
        if (kartAgent != null)
            kartAgent.SetAvailableBombs(RemainingBombs);
    }

    /// <summary>
    /// Hard-stops any pending/current cue on this kart's shared source, applies the given
    /// cue's 3D attenuation only when the source is spatial, then plays the clip at its
    /// configured volume via PlayOneShot, which is not clamped to the AudioSource's own 0-1
    /// volume ceiling.
    /// </summary>
    private void PlaySfx(SoundEffectSettings settings)
    {
        if (sfxSource == null || settings == null || settings.Clip == null)
        {
            Debug.LogWarning("BombLauncher: sfxSource, settings, or its clip is unassigned — skipping sound effect.", this);
            return;
        }

        if (sfxSource.spatialBlend > 0f)
        {
            settings.ApplySpatialSettings(sfxSource);
        }

        sfxSource.Stop();
        sfxSource.PlayOneShot(settings.Clip, settings.VolumeScale);
    }

    private void OnDestroy()
    {
        if (kartAgent != null)
            kartAgent.FireRequested -= HandleFireRequested;
    }

    private void OnValidate()
    {
        launchAngle = Mathf.Clamp(launchAngle, 5f, 85f);
        minSpinDegreesPerSecond = Mathf.Max(0f, minSpinDegreesPerSecond);
        maxSpinDegreesPerSecond = Mathf.Max(minSpinDegreesPerSecond, maxSpinDegreesPerSecond);

        if (bombPrefab == null)
            Debug.LogWarning("BombLauncher: bombPrefab is not assigned.", this);

        if (launchPoint == null)
            Debug.LogWarning("BombLauncher: launchPoint is not assigned.", this);
    }
}
