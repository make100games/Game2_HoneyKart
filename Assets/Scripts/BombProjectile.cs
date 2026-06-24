using UnityEngine;

/// <summary>
/// Attached to the bomb prefab. Explodes on contact with matching layers,
/// or after a fallback lifetime. Ignores the kart that fired it to prevent
/// self-detonation on launch.
/// </summary>
public class BombProjectile : MonoBehaviour
{
    [SerializeField] private LayerMask explodeLayers;
    [SerializeField] private float maxLifetime = 6f;

    private bool hasExploded;

    private void Start()
    {
        Invoke(nameof(Explode), maxLifetime);
    }

    /// <summary>
    /// Registers the launcher's colliders so Physics ignores collisions between
    /// them and this bomb's colliders. Must be called immediately after instantiation.
    /// </summary>
    public void SetLauncherColliders(Collider[] launcherColliders)
    {
        Collider[] bombColliders = GetComponentsInChildren<Collider>();

        foreach (Collider launcherCol in launcherColliders)
        {
            foreach (Collider bombCol in bombColliders)
            {
                Physics.IgnoreCollision(launcherCol, bombCol);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if ((explodeLayers.value & (1 << collision.gameObject.layer)) != 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (hasExploded)
            return;

        hasExploded = true;
        CancelInvoke(nameof(Explode));

        // Hook: spawn VFX and deal area damage here before destroying.
        Destroy(gameObject);
    }
}
