using UnityEngine;
using KartGame.KartSystems;

/// <summary>
/// Attached to each kart root. Receives combat events from gameplay systems
/// such as <see cref="BombProjectile"/> and drives the kart's reaction to an
/// explosion: a vertical knockback with an aerial flip that always lands the
/// kart right-side up, plus a ring of collectable coins scattered around it.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KartCombatHandler : MonoBehaviour
{
    private const float DefaultKnockbackVelocity = 8f;
    private const float DefaultFlipCount = 1f;
    private const float DefaultMaxAirborneSeconds = 3f;
    private const int DefaultCoinCount = 5;
    private const float DefaultCoinRingRadius = 1f;
    private const float DefaultCoinUpwardVelocity = 4f;
    private const float DefaultCoinOutwardVelocity = 3f;
    private const float DefaultCoinSpawnHeight = 0.5f;
    private const float GravityMagnitude = 9.81f;

    [Header("Knockback")]
    [Tooltip("Upward velocity (m/s) applied to the kart when hit by an explosion.")]
    [SerializeField] private float knockbackVelocity = DefaultKnockbackVelocity;

    [Tooltip("Number of full flips the kart performs while airborne.")]
    [SerializeField] private float flipCount = DefaultFlipCount;

    [Tooltip("Safety cap on the airborne state in case a landing is never detected.")]
    [SerializeField] private float maxAirborneSeconds = DefaultMaxAirborneSeconds;

    [Header("Coins")]
    [Tooltip("Coin prefab spawned around the kart when it is hit.")]
    [SerializeField] private GameObject coinPrefab;

    [Tooltip("Number of coins spawned in a ring around the kart.")]
    [SerializeField] private int coinCount = DefaultCoinCount;

    [Tooltip("Radius of the ring the coins spawn on (kept small so they cluster).")]
    [SerializeField] private float coinRingRadius = DefaultCoinRingRadius;

    [Tooltip("Upward velocity applied to each spawned coin.")]
    [SerializeField] private float coinUpwardVelocity = DefaultCoinUpwardVelocity;

    [Tooltip("Outward (away from the kart) velocity applied to each spawned coin.")]
    [SerializeField] private float coinOutwardVelocity = DefaultCoinOutwardVelocity;

    [Tooltip("Height above the kart at which coins spawn so they don't start embedded in the ground.")]
    [SerializeField] private float coinSpawnHeight = DefaultCoinSpawnHeight;

    [Tooltip("Layers treated as ground for the flip landing and coin landing.")]
    [SerializeField] private LayerMask groundLayers;

    private Rigidbody kartRigidbody;
    private ArcadeKart kart;
    private bool isKnockedBack;
    private bool hasLeftGround;
    private float airborneTimer;
    private Vector3 flipAxis;
    private float flipDegreesPerSecond;

    private void Awake()
    {
        kartRigidbody = GetComponent<Rigidbody>();
        kart = GetComponent<ArcadeKart>();
    }

    /// <summary>
    /// Called by <see cref="BombProjectile"/> when this kart is within an
    /// explosion blast radius. Launches the kart into an aerial flip and
    /// scatters a ring of coins around it.
    /// </summary>
    /// <param name="explosionOrigin">World-space position of the explosion.</param>
    public void OnHitByExplosion(Vector3 explosionOrigin)
    {
        if (isKnockedBack)
            return;

        LaunchKnockback();
        SpawnCoins();
    }

    private void LaunchKnockback()
    {
        isKnockedBack = true;
        hasLeftGround = false;
        airborneTimer = 0f;

        // Stop the kart driving itself so it doesn't fight the flip or trigger
        // its built-in airborne self-righting.
        if (kart != null)
            kart.SetCanMove(false);

        if (kartRigidbody != null)
        {
            kartRigidbody.angularVelocity = Vector3.zero;
            // Mass-independent upward launch.
            kartRigidbody.AddForce(Vector3.up * knockbackVelocity, ForceMode.VelocityChange);
        }

        // Pick a random horizontal axis to flip around for variety.
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        flipAxis = new Vector3(randomDir.x, 0f, randomDir.y);
        if (flipAxis.sqrMagnitude < 0.001f)
            flipAxis = Vector3.right;

        // Spin fast enough to complete roughly flipCount rotations before landing.
        float estimatedAirtime = EstimateAirtime();
        flipDegreesPerSecond = estimatedAirtime > 0f ? (360f * flipCount) / estimatedAirtime : 0f;
    }

    private float EstimateAirtime()
    {
        // Karts don't use Unity gravity; ArcadeKart adds gravity scaled by
        // AddedGravity while fully airborne. Time up and back down = 2v / a.
        float gravity = GravityMagnitude;
        if (kart != null && kart.baseStats.AddedGravity > 0f)
            gravity *= kart.baseStats.AddedGravity;

        if (gravity <= 0f)
            return maxAirborneSeconds;

        return Mathf.Min((2f * knockbackVelocity) / gravity, maxAirborneSeconds);
    }

    private void Update()
    {
        if (!isKnockedBack)
            return;

        airborneTimer += Time.deltaTime;

        // Drive the flip programmatically rather than through physics.
        if (flipDegreesPerSecond > 0f)
            transform.Rotate(flipAxis * (flipDegreesPerSecond * Time.deltaTime), Space.World);

        bool grounded = kart != null && kart.GroundPercent > 0f;

        // Only start watching for a landing once the launch has lifted the kart.
        if (!hasLeftGround && !grounded)
            hasLeftGround = true;

        bool landed = hasLeftGround && grounded;
        if (landed || airborneTimer >= maxAirborneSeconds)
            FinishKnockback();
    }

    private void FinishKnockback()
    {
        isKnockedBack = false;

        // Snap upright, preserving heading, so the kart always ends right-side
        // up even if the flip didn't complete before touching down.
        Vector3 euler = transform.eulerAngles;
        euler.x = 0f;
        euler.z = 0f;
        transform.eulerAngles = euler;

        if (kart != null)
            kart.SetCanMove(true);
    }

    private void SpawnCoins()
    {
        if (coinPrefab == null || coinCount <= 0)
            return;

        float angleStep = 360f / coinCount;

        for (int i = 0; i < coinCount; i++)
        {
            float angleRadians = angleStep * i * Mathf.Deg2Rad;
            Vector3 outwardDir = new Vector3(Mathf.Cos(angleRadians), 0f, Mathf.Sin(angleRadians));
            Vector3 spawnPosition = transform.position + outwardDir * coinRingRadius + Vector3.up * coinSpawnHeight;

            GameObject coin = Instantiate(coinPrefab, spawnPosition, coinPrefab.transform.rotation);

            ExplosionCoin explosionCoin = coin.GetComponent<ExplosionCoin>();
            if (explosionCoin == null)
                explosionCoin = coin.AddComponent<ExplosionCoin>();

            Vector3 launchVelocity = outwardDir * coinOutwardVelocity + Vector3.up * coinUpwardVelocity;
            explosionCoin.Launch(launchVelocity, groundLayers);
        }
    }

    private void OnValidate()
    {
        knockbackVelocity = Mathf.Max(0f, knockbackVelocity);
        flipCount = Mathf.Max(0f, flipCount);
        maxAirborneSeconds = Mathf.Max(0.1f, maxAirborneSeconds);
        coinCount = Mathf.Max(0, coinCount);
        coinRingRadius = Mathf.Max(0f, coinRingRadius);

        if (coinPrefab == null)
            Debug.LogWarning("KartCombatHandler: coinPrefab is not assigned.", this);
    }
}
