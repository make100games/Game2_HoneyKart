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

    public void StartOrbiting(Transform target)
    {
        enabled = true;
    }

    private void Awake()
    {
        virtualCamera = GetComponent<CinemachineVirtualCamera>();
        target = virtualCamera.LookAt;

        if (target == null)
        {
            Debug.LogError("Virtual Camera does not have a Look At target assigned.", this);
        }
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