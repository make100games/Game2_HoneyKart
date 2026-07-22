using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to the bomb prefab. Hitting the ground starts a fuse timer while
/// the bomb keeps rolling under physics; hitting a player kart detonates
/// immediately. On detonation the mesh is hidden, the explosion effect plays,
/// nearby karts are notified, and the GameObject is destroyed after a delay.
/// Ignores the kart that fired it to prevent self-detonation on launch.
/// </summary>
public class BombProjectile : MonoBehaviour
{
    private const float DefaultGroundFuseSeconds = 2f;
    private const float DefaultDestroyDelaySeconds = 3f;

    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private LayerMask playerLayers;
    [SerializeField] private float groundFuseSeconds = DefaultGroundFuseSeconds;
    [SerializeField] private float destroyDelaySeconds = DefaultDestroyDelaySeconds;
    [SerializeField] private float maxLifetime = 6f;
    [SerializeField] private float blastRadius = 5f;
    [SerializeField] private LayerMask kartLayers;
    [SerializeField] private MeshRenderer bombMeshRenderer;
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private GameObject[] fuseEffects;

    private Rigidbody rb;
    private bool hasExploded;
    private bool fuseStarted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // Safety fallback for bombs that fly off the map and never touch ground.
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
    if (hasExploded)
        return;

    // Detonate on any kart, regardless of which child collider was hit.
    if (collision.collider.GetComponentInParent<KartCombatHandler>() != null)
    {
        Explode();
        return;
    }

    int layer = collision.gameObject.layer;
    if ((groundLayers.value & (1 << layer)) != 0)
        StartFuse();
}


    private void StartFuse()
    {
        if (fuseStarted)
            return;

        fuseStarted = true;
        Invoke(nameof(Explode), groundFuseSeconds);
    }

    private void Explode()
    {
        if (hasExploded)
            return;

        hasExploded = true;
        CancelInvoke();

        // Hide the bomb mesh but leave explosion child renderers visible.
        if (bombMeshRenderer != null)
            bombMeshRenderer.enabled = false;

        // Freeze in place during the destroy delay (kinematic prevents gravity re-accelerating).
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Stop fuse visuals.
        if (fuseEffects != null)
        {
            foreach (GameObject fuseEffect in fuseEffects)
            {
                if (fuseEffect != null)
                    fuseEffect.SetActive(false);
            }
        }

        // Play explosion visual.
        if (explosionEffect != null)
        {
            explosionEffect.SetActive(true);
            foreach (ParticleSystem ps in explosionEffect.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Play();
            }
        }

        // Notify all karts within blast radius immediately.
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, blastRadius, kartLayers);
        HashSet<KartCombatHandler> notifiedHandlers = new HashSet<KartCombatHandler>();

        foreach (Collider hitCollider in hitColliders)
        {
            KartCombatHandler handler = hitCollider.GetComponentInParent<KartCombatHandler>();
            if (handler == null || !notifiedHandlers.Add(handler))
                continue;

            handler.OnHitByExplosion(transform.position);
        }

        Destroy(gameObject, destroyDelaySeconds);
    }

    private void OnValidate()
    {
        groundFuseSeconds = Mathf.Max(0f, groundFuseSeconds);
        destroyDelaySeconds = Mathf.Max(0f, destroyDelaySeconds);

        if (bombMeshRenderer == null)
            Debug.LogWarning("BombProjectile: bombMeshRenderer is not assigned.", this);

        if (explosionEffect == null)
            Debug.LogWarning("BombProjectile: explosionEffect is not assigned.", this);
    }
}
