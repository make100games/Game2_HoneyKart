using UnityEngine;
using KartGame.KartSystems;

public class CoinCollector : MonoBehaviour
{
    [Header("Speed Boost Settings")]
    [Tooltip("Speed increase per coin collected")]
    public float speedBoostPerCoin = 2f;
    
    [Tooltip("Duration of speed boost in seconds")]
    public float boostDuration = 5f;
    
    [Tooltip("Maximum total speed the kart can reach")]
    public float absoluteMaxSpeed = 30f;
    
    [Header("References")]
    [Tooltip("Reference to the ArcadeKart component")]
    public ArcadeKart kart;
    
    [Tooltip("Reference to the Sparkles GameObject containing particle systems")]
    public GameObject sparklesObject;
    
    [Header("Coin Settings")]
    [Tooltip("Layer of the coin objects")]
    public LayerMask coinLayer;
    
    private ParticleSystem[] particleSystems;
    public ParticleSystem systemToManuallyEmit;
    
    private void Start()
    {
        if (kart == null)
        {
            kart = GetComponentInParent<ArcadeKart>();
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

        ApplySpeedBoost();
        PlaySparkles();
        Destroy(coin);
    }
    
    private void ApplySpeedBoost()
    {
        if (kart == null)
        {
            Debug.LogWarning("CoinCollector: No ArcadeKart reference assigned!");
            return;
        }
        
        float boostAmount = speedBoostPerCoin;
        
        if (kart.baseStats.TopSpeed + speedBoostPerCoin > absoluteMaxSpeed)
        {
            boostAmount = Mathf.Max(0f, absoluteMaxSpeed - kart.baseStats.TopSpeed);
        }
        
        if (boostAmount > 0f)
        {
            ArcadeKart.StatPowerup speedBoost = new ArcadeKart.StatPowerup
            {
                PowerUpID = "CoinSpeedBoost",
                MaxTime = boostDuration,
                ElapsedTime = 0f,
                modifiers = new ArcadeKart.Stats
                {
                    TopSpeed = boostAmount,
                    Acceleration = 0f,
                    AccelerationCurve = 0f,
                    Braking = 0f,
                    CoastingDrag = 0f,
                    AddedGravity = 0f,
                    Grip = 0f,
                    ReverseAcceleration = 0f,
                    ReverseSpeed = 0f,
                    Steer = 0f
                }
            };
            
            kart.AddPowerup(speedBoost);
        }
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
}
