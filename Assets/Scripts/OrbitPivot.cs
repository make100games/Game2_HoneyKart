using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class OrbitPivot : MonoBehaviour
{
    [SerializeField] private float radius = 8f;
    [SerializeField] private float height = 3f;
    [SerializeField] private float orbitSpeed = 20f; // degrees per second

    private CinemachineVirtualCamera virtualCamera;
    private Transform target;

    private float angle;

    /// <summary>Assigns the orbit target and enables the orbiting behaviour.</summary>
    public void StartOrbiting(Transform orbitTarget)
    {
        target = orbitTarget;
        enabled = true;
    }

    private void Awake()
    {
        virtualCamera = GetComponent<CinemachineVirtualCamera>();
        // target is assigned at runtime via StartOrbiting() once the selected kart is known.
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        angle += orbitSpeed * Time.deltaTime;

        float radians = angle * Mathf.Deg2Rad;

        Vector3 position = target.position + new Vector3(
            Mathf.Sin(radians) * radius,
            height,
            Mathf.Cos(radians) * radius
        );

        transform.position = position;
        transform.LookAt(target.position);
    }
}