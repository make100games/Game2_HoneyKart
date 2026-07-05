using UnityEngine;

/// <summary>
/// Added at runtime to coins spawned by an explosion. Drives the coin's
/// airborne phase (real gravity + a solid, non-trigger collider so it flies and
/// bounces) and, once it lands, restores it to the resting state shared by
/// spawner coins: a kinematic trigger with gravity disabled that a kart can
/// collect. While airborne the coin reports itself as non-collectable so karts
/// can bump into it physically without picking it up.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ExplosionCoin : MonoBehaviour
{
    private const float GroundContactOffset = 0.05f;
    private const float GroundProbeDistance = 50f;

    private Rigidbody coinRigidbody;
    private Collider coinCollider;
    private LayerMask groundLayers;
    private float restingHalfHeight;
    private bool landed;

    /// <summary>True once the coin has landed and may be picked up by a kart.</summary>
    public bool IsCollectable => landed;

    /// <summary>
    /// Switches the coin from its resting kinematic-trigger state into a
    /// physical projectile and launches it with the given world-space velocity.
    /// Must be called immediately after the component is added.
    /// </summary>
    /// <param name="velocity">Initial world-space velocity for the coin.</param>
    /// <param name="groundLayerMask">Layers treated as ground for landing detection.</param>
    public void Launch(Vector3 velocity, LayerMask groundLayerMask)
    {
        coinRigidbody = GetComponent<Rigidbody>();
        coinCollider = GetComponent<Collider>();
        groundLayers = groundLayerMask;

        // World-space half-height of the collider so we can rest the coin on top
        // of the ground rather than sinking its center into the surface.
        restingHalfHeight = coinCollider.bounds.extents.y;

        // Enter the physical projectile state.
        coinCollider.isTrigger = false;
        coinRigidbody.isKinematic = false;
        coinRigidbody.useGravity = true;
        coinRigidbody.constraints = RigidbodyConstraints.None;
        coinRigidbody.linearVelocity = velocity;
    }

    private void FixedUpdate()
    {
        if (landed)
            return;

        // Only test for a landing once the coin is falling (or at its apex).
        if (coinRigidbody.linearVelocity.y > 0f)
            return;

        // Probe the ground directly beneath the coin. Landing is detected when
        // the bottom of the coin reaches the surface. A raycast keeps this correct
        // on uneven terrain and independent of the layer collision matrix, and
        // resting at hit.point + halfHeight seats the coin on top of the ground.
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, GroundProbeDistance, groundLayers)
            && hit.distance <= restingHalfHeight + GroundContactOffset)
        {
            Land(hit.point.y + restingHalfHeight);
        }
    }

    private void Land(float restY)
    {
        landed = true;

        // Return to the resting state used by spawner coins: a kinematic trigger
        // with no gravity that can be collected and won't push karts around.
        coinRigidbody.linearVelocity = Vector3.zero;
        coinRigidbody.angularVelocity = Vector3.zero;
        coinRigidbody.useGravity = false;
        coinRigidbody.isKinematic = true;
        coinRigidbody.constraints = RigidbodyConstraints.FreezeAll;

        if (coinCollider != null)
            coinCollider.isTrigger = true;

        // Settle exactly on the ground so it doesn't hover or clip through.
        Vector3 restingPosition = transform.position;
        restingPosition.y = restY;
        transform.position = restingPosition;
    }
}
