using UnityEngine;

public class PhysicsLayerDebugger : MonoBehaviour
{
    private void Start()
    {
        int kartLayer = LayerMask.NameToLayer("Kart");
        int coinLayer = LayerMask.NameToLayer("Coin");
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        
        Debug.Log($"=== Physics Layer Collision Debug ===");
        Debug.Log($"Kart layer index: {kartLayer}");
        Debug.Log($"Coin layer index: {coinLayer}");
        Debug.Log($"Ignore Raycast layer index: {ignoreRaycastLayer}");
        
        bool kartCoinCollision = !Physics.GetIgnoreLayerCollision(kartLayer, coinLayer);
        bool ignoreRaycastCoinCollision = !Physics.GetIgnoreLayerCollision(ignoreRaycastLayer, coinLayer);
        
        Debug.Log($"Kart <-> Coin collision enabled: {kartCoinCollision}");
        Debug.Log($"Ignore Raycast <-> Coin collision enabled: {ignoreRaycastCoinCollision}");
        
        if (!kartCoinCollision)
        {
            Debug.LogWarning("Kart and Coin layers cannot collide! Enabling collision...");
            Physics.IgnoreLayerCollision(kartLayer, coinLayer, false);
        }
        
        if (!ignoreRaycastCoinCollision)
        {
            Debug.LogWarning("Ignore Raycast and Coin layers cannot collide! Enabling collision...");
            Physics.IgnoreLayerCollision(ignoreRaycastLayer, coinLayer, false);
        }
    }
}
