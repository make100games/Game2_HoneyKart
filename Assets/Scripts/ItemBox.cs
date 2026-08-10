using System.Collections;
using UnityEngine;

/// <summary>
/// Self-contained collection behaviour placed on the ItemBox prefab root.
/// When a bomb-capable kart drives through the box's trigger it grants bombs,
/// plays the collect effect, hides the box, notifies the respawner, and then
/// destroys itself after a short delay so the particle effect can finish.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class ItemBox : MonoBehaviour
{
    private const int DefaultBombsGranted = 3;
    private const float DefaultDestroyDelaySeconds = 2f;

    [Tooltip("Number of bombs granted to the collecting kart")]
    [SerializeField] private int bombsGranted = DefaultBombsGranted;

    [Tooltip("Seconds the box lingers after collection so the collect effect can finish before it is destroyed")]
    [SerializeField] private float destroyDelaySeconds = DefaultDestroyDelaySeconds;

    private MeshRenderer meshRenderer;
    private Collider boxCollider;
    private ParticleSystem collectEffect;
    private bool hasBeenCollected;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        boxCollider = GetComponent<Collider>();
        collectEffect = GetComponentInChildren<ParticleSystem>(true);

        // Ensure a playOnAwake effect never fires when the box spawns or respawns.
        if (collectEffect != null)
            collectEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenCollected)
            return;

        BombLauncher launcher = other.GetComponentInParent<BombLauncher>();
        if (launcher == null)
            return;

        Collect(launcher);
    }

    private void Collect(BombLauncher launcher)
    {
        hasBeenCollected = true;

        // Block any further triggers immediately.
        if (boxCollider != null)
            boxCollider.enabled = false;

        launcher.AddBombs(bombsGranted);
        launcher.PlayItemCollectedSound();

        if (collectEffect != null)
            collectEffect.Play();

        if (meshRenderer != null)
            meshRenderer.enabled = false;

        // RespawnableItem is added at runtime by TerrainObjectSpawner, so it may
        // be absent for manually placed boxes. Fetch it lazily at collection time.
        RespawnableItem respawnable = GetComponent<RespawnableItem>();
        respawnable?.NotifyCollected();

        StartCoroutine(DestroyAfterDelay());
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelaySeconds);
        Destroy(gameObject);
    }

    private void OnValidate()
    {
        bombsGranted = Mathf.Max(0, bombsGranted);
        destroyDelaySeconds = Mathf.Max(0f, destroyDelaySeconds);
    }
}
