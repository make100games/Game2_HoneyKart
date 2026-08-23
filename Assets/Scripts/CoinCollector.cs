using Action = System.Action;
using UnityEngine;
using KartGame.KartSystems;

public class CoinCollector : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the ArcadeKart component")]
    public ArcadeKart kart;
    
    [Tooltip("Reference to the Sparkles GameObject containing particle systems")]
    public GameObject sparklesObject;
    
    [Header("Coin Settings")]
    [Tooltip("Layer of the coin objects")]
    public LayerMask coinLayer;

    [Header("Sound Effects")]
    [Tooltip("Shared spatial AudioSource on this kart used for gameplay sound effects.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Sound effect played when a coin is collected, with its own volume and attenuation controls.")]
    [SerializeField] private SoundEffectSettings coinCollectSound;
    
    private ParticleSystem[] particleSystems;
    public ParticleSystem systemToManuallyEmit;

    /// <summary>Fired when this kart accepts and collects a coin.</summary>
    public event Action CoinCollected;

    private BoostMeter m_BoostMeter;

    private void Start()
    {
        if (kart == null)
        {
            kart = GetComponentInParent<ArcadeKart>();
        }

        m_BoostMeter = GetComponent<BoostMeter>();
        if (m_BoostMeter == null)
        {
            m_BoostMeter = GetComponentInParent<BoostMeter>();
        }
        
        if (sparklesObject != null)
        {
            particleSystems = sparklesObject.GetComponentsInChildren<ParticleSystem>();
            Debug.Log($"CoinCollector: Found {particleSystems.Length} particle systems");
        }
        
        if (coinLayer == 0)
        {
            coinLayer = LayerMask.GetMask("Coin");
        }
        
        Debug.Log($"CoinCollector initialized on {gameObject.name}");
        Debug.Log($"Kart reference: {(kart != null ? "Found" : "NULL")}");
        Debug.Log($"Coin layer mask value: {coinLayer.value}");
        Debug.Log($"This GameObject layer: {LayerMask.LayerToName(gameObject.layer)}");
        
        Rigidbody rb = GetComponent<Rigidbody>();
        Debug.Log($"Rigidbody on this GameObject: {(rb != null ? "Found" : "NULL")}");
        
        Collider[] colliders = GetComponentsInChildren<Collider>();
        Debug.Log($"Found {colliders.Length} colliders in children");
        foreach (Collider col in colliders)
        {
            Debug.Log($"  - {col.gameObject.name}: {col.GetType().Name}, isTrigger={col.isTrigger}, layer={LayerMask.LayerToName(col.gameObject.layer)}");
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        //Debug.Log($"OnCollisionEnter with {collision.gameObject.name}, layer: {collision.gameObject.layer}");
        if (IsCoinLayer(collision.gameObject.layer))
        {
            Debug.Log("Coin collision detected!");
            CollectCoin(collision.gameObject);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"OnTriggerEnter with {other.gameObject.name}, layer: {other.gameObject.layer}");
        if (IsCoinLayer(other.gameObject.layer))
        {
            Debug.Log("Coin trigger detected!");
            CollectCoin(other.gameObject);
        }
    }
    
    private bool IsCoinLayer(int layer)
    {
        return (coinLayer.value & (1 << layer)) != 0;
    }
    
    private void CollectCoin(GameObject coin)
    {
        // Coins scattered by an explosion can't be picked up while still
        // airborne — they only become collectable once they land.
        ExplosionCoin explosionCoin = coin.GetComponent<ExplosionCoin>();
        if (explosionCoin != null && !explosionCoin.IsCollectable)
        {
            return;
        }

        CoinCollected?.Invoke();

        if (m_BoostMeter != null)
        {
            m_BoostMeter.AddChargeForCoin();
        }

        PlaySparkles();
        PlayCoinCollectSound();

        // Notify the owning spawner so the coin's slot respawns after its delay.
        // Coins managed by a TerrainObjectSpawner carry a RespawnableItem; coins
        // without one (e.g. explosion-scattered) are simply destroyed.
        RespawnableItem respawnable = coin.GetComponent<RespawnableItem>();
        if (respawnable != null)
        {
            respawnable.NotifyCollected();
        }

        Destroy(coin);
    }
    
    private void PlaySparkles()
    {
        if (particleSystems == null || particleSystems.Length == 0)
        {
            return;
        }
        
        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps != null && ps != systemToManuallyEmit)
            {
                ps.Play();
            }
            else if(ps != null && ps == systemToManuallyEmit) {
                ps.Emit(1);
                ps.Play();
            }
        }
    }

    /// <summary>
    /// Hard-stops any pending/current cue on this kart's shared source, applies the coin
    /// collect cue's 3D attenuation only when the source is spatial, then plays the clip at
    /// its configured volume via PlayOneShot, which is not clamped to the AudioSource's own
    /// 0-1 volume ceiling.
    /// </summary>
    private void PlayCoinCollectSound()
    {
        if (sfxSource == null || coinCollectSound == null || coinCollectSound.Clip == null)
        {
            Debug.LogWarning("CoinCollector: sfxSource, coinCollectSound, or its clip is unassigned — skipping coin collect sound.", this);
            return;
        }

        if (sfxSource.spatialBlend > 0f)
        {
            coinCollectSound.ApplySpatialSettings(sfxSource);
        }

        sfxSource.Stop();
        sfxSource.PlayOneShot(coinCollectSound.Clip, coinCollectSound.VolumeScale);
    }
}
