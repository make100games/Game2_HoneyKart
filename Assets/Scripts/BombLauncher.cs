using UnityEngine;

/// <summary>
/// Attached to the player kart. Listens for Space input, calculates a
/// physics-based launch velocity, and fires bomb projectiles from a
/// configurable LaunchPoint transform.
/// </summary>
public class BombLauncher : MonoBehaviour
{
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private Transform launchPoint;
    [SerializeField] private int maxBombs = 10;
    [SerializeField] private float targetRange = 30f;
    [SerializeField] [Range(5f, 85f)] private float launchAngle = 35f;

    /// <summary>Current number of bombs available to fire.</summary>
    public int RemainingBombs { get; private set; }

    private void Awake()
    {
        RemainingBombs = maxBombs;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && RemainingBombs > 0)
        {
            FireBomb();
        }
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

        float angleRad = launchAngle * Mathf.Deg2Rad;
        float gravity = Physics.gravity.magnitude;
        float speed = Mathf.Sqrt(targetRange * gravity / Mathf.Sin(2f * angleRad));
        Vector3 velocity = (transform.forward * Mathf.Cos(angleRad) + Vector3.up * Mathf.Sin(angleRad)) * speed;

        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
    }

    private void OnValidate()
    {
        launchAngle = Mathf.Clamp(launchAngle, 5f, 85f);

        if (bombPrefab == null)
            Debug.LogWarning("BombLauncher: bombPrefab is not assigned.", this);

        if (launchPoint == null)
            Debug.LogWarning("BombLauncher: launchPoint is not assigned.", this);
    }
}
