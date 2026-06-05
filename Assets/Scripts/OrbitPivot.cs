using UnityEngine;

public class OrbitPivot : MonoBehaviour
{
    public float orbitSpeed = 25f;
    public float orbitRadius = 4f;
    public float orbitHeight = 0f;

    private Transform m_Target;
    private float m_CurrentAngle;

    void Awake() => enabled = false;

    /// <summary>Begins orbiting around the given target starting 180 degrees behind its facing direction.</summary>
    public void StartOrbiting(Transform target)
    {
        m_Target = target;
        m_CurrentAngle = target.eulerAngles.y + 180f;
        enabled = true;
    }

    void Update()
    {
        if (m_Target == null) return;

        m_CurrentAngle += orbitSpeed * Time.deltaTime;
        float rad = m_CurrentAngle * Mathf.Deg2Rad;
        transform.position = m_Target.position + new Vector3(
            Mathf.Sin(rad) * orbitRadius,
            orbitHeight,
            Mathf.Cos(rad) * orbitRadius
        );

        Vector3 toTarget = m_Target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
    }
}
