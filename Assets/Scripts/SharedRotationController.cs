using UnityEngine;

public class SharedRotationController : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 90f;
    
    [Tooltip("Axis to rotate around")]
    public Vector3 rotationAxis = Vector3.up;
    
    [Header("Current State")]
    [Tooltip("Current rotation (read-only)")]
    [SerializeField]
    private Quaternion currentRotation = Quaternion.identity;
    
    public Quaternion CurrentRotation => currentRotation;
    
    private void Update()
    {
        float rotationAmount = rotationSpeed * Time.deltaTime;
        currentRotation *= Quaternion.AngleAxis(rotationAmount, rotationAxis.normalized);
    }
    
    public void ResetRotation()
    {
        currentRotation = Quaternion.identity;
    }
}
