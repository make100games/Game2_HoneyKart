using UnityEngine;

public class CoinRotation : MonoBehaviour
{
    [Header("Rotation Reference")]
    [Tooltip("Reference to the shared rotation controller")]
    public SharedRotationController rotationController;
    
    [Header("Settings")]
    [Tooltip("If true, finds the rotation controller automatically by tag")]
    public bool autoFindController = true;
    
    [Tooltip("Tag to search for rotation controller (only used if autoFindController is true)")]
    public string controllerTag = "RotationController";
    
    private void Start()
    {
        if (autoFindController && rotationController == null)
        {
            GameObject controllerObject = GameObject.FindGameObjectWithTag(controllerTag);
            if (controllerObject != null)
            {
                rotationController = controllerObject.GetComponent<SharedRotationController>();
            }
            else
            {
                Debug.LogWarning($"CoinRotation: No GameObject found with tag '{controllerTag}'. Please assign rotation controller manually or create one.");
            }
        }
    }
    
    private void Update()
    {
        if (rotationController != null)
        {
            transform.rotation = rotationController.CurrentRotation;
        }
    }
}
