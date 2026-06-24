using System.Collections.Generic;
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
    [SerializeField] private float blastRadius = 5f;
    [SerializeField] private LayerMask kartLayers;

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
        Debug.Log("Bomb collided with something: " + collision.gameObject);
        if ((explodeLayers.value & (1 << collision.gameObject.layer)) != 0)
        {
            Debug.Log("Expode!");
            Explode();
        }
    }

    private void Explode()
    {
        if (hasExploded)
            return;

        hasExploded = true;
        CancelInvoke(nameof(Explode));

        // Notify all karts within blast radius.
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, blastRadius, kartLayers);
        HashSet<KartCombatHandler> notifiedHandlers = new HashSet<KartCombatHandler>();

        foreach (Collider hitCollider in hitColliders)
        {
            KartCombatHandler handler = hitCollider.GetComponentInParent<KartCombatHandler>();
            if (handler == null || !notifiedHandlers.Add(handler))
                continue;

            handler.OnHitByExplosion(transform.position);
        }

        Destroy(gameObject);
    }
}
