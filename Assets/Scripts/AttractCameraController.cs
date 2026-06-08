using Cinemachine;
using UnityEngine;

/// <summary>
/// Drives the attract VCam along a CinemachineSmoothPath dolly, accelerating from rest to maxSpeed.
/// Interpolates the look target between a point directly below the cart (at start) and a point
/// ahead on the path (at full speed), producing a cinematic tilt-up effect.
/// </summary>
public class AttractCameraController : MonoBehaviour
{
    [Tooltip("The CinemachineVirtualCamera to drive along the path.")]
    public CinemachineVirtualCamera vcam;

    [Tooltip("The CinemachineSmoothPath that defines the camera's orbit route.")]
    public CinemachineSmoothPath path;

    [Tooltip("Empty Transform whose world position is driven at runtime to serve as the VCam LookAt target.")]
    public Transform lookTarget;

    [Tooltip("Maximum path units per second once fully accelerated.")]
    public float maxSpeed = 8f;

    [Tooltip("Seconds to ramp from 0 to maxSpeed.")]
    public float accelerationDuration = 8f;

    [Tooltip("Ease curve controlling the speed ramp. Evaluated 0–1 over accelerationDuration.")]
    public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Metres below the dolly cart position when looking down at the start of the sequence.")]
    public float lookTargetBelowOffset = 3f;

    [Tooltip("Path units ahead of the cart for the forward look target at full speed.")]
    public float lookTargetForwardUnits = 10f;

    private CinemachineTrackedDolly m_Dolly;
    private float m_Elapsed;

    void Start()
    {
        if (vcam != null)
            m_Dolly = vcam.GetCinemachineComponent<CinemachineTrackedDolly>();
    }

    void Update()
    {
        if (m_Dolly == null || path == null || lookTarget == null)
            return;

        // Advance elapsed time and evaluate the speed curve
        m_Elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(m_Elapsed / accelerationDuration);
        float normalizedSpeed = speedCurve.Evaluate(t);
        float currentSpeed = maxSpeed * normalizedSpeed;

        // Advance position along the dolly path (CinemachineSmoothPath wraps when looped)
        m_Dolly.m_PathPosition += currentSpeed * Time.deltaTime;

        // Compute the cart's current world position on the path
        Vector3 cartPos = path.EvaluatePositionAtUnit(m_Dolly.m_PathPosition, CinemachinePathBase.PositionUnits.PathUnits);

        // Look-down target: directly below the cart
        Vector3 belowPos = cartPos + Vector3.down * lookTargetBelowOffset;

        // Look-forward target: ahead on the path
        float aheadPathPos = m_Dolly.m_PathPosition + lookTargetForwardUnits * normalizedSpeed;
        Vector3 forwardPos = path.EvaluatePositionAtUnit(aheadPathPos, CinemachinePathBase.PositionUnits.PathUnits);

        // Lerp the look target from below to forward as speed increases
        lookTarget.position = Vector3.Lerp(belowPos, forwardPos, normalizedSpeed);
    }
}
