using UnityEngine;

// Combat handler for karts — receives events from projectile and area-effect systems.
/// <summary>
/// Attached to each kart root. Receives combat events from gameplay systems
/// such as <see cref="BombProjectile"/>. Acts as the single point of entry
/// for all damage and physics-impulse reactions on a kart.
/// </summary>
public class KartCombatHandler : MonoBehaviour
{
    /// <summary>
    /// Called by <see cref="BombProjectile"/> when this kart is within the
    /// explosion blast radius. The <paramref name="explosionOrigin"/> is the
    /// world-space position of the explosion and is forwarded here so a future
    /// step can derive the impulse direction for the fly-into-air behaviour.
    /// </summary>
    public void OnHitByExplosion(Vector3 explosionOrigin)
    {
        Debug.Log($"[KartCombatHandler] {gameObject.name} was hit by an explosion from {explosionOrigin}");
    }
}
