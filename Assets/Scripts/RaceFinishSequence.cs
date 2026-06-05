using System.Collections;
using Cinemachine;
using KartGame.KartSystems;
using UnityEngine;

public class RaceFinishSequence : MonoBehaviour
{
    [Tooltip("The existing follow VCam to freeze in place when the race ends.")]
    public CinemachineVirtualCamera followCamera;

    [Tooltip("The orbit VCam to activate after the static delay.")]
    public CinemachineVirtualCamera orbitCamera;

    [Tooltip("The pivot that orbitCamera follows.")]
    public OrbitPivot orbitPivot;

    [Tooltip("Seconds the frozen follow camera is held before the orbit camera blends in.")]
    public float staticCameraDelay = 3f;

    [Tooltip("Rigidbody linear damping applied to produce a coast-to-stop.")]
    public float coastingLinearDamping = 3f;

    [Tooltip("Rigidbody angular damping applied when the race ends.")]
    public float coastingAngularDamping = 5f;

    [Tooltip("Priority raised on orbitCamera — must exceed the follow camera's priority of 10.")]
    public int orbitCameraPriority = 20;

    private bool m_Triggered;

    void OnEnable()  => GameFlowManager.OnRaceFinished += HandleRaceFinished;
    void OnDisable() => GameFlowManager.OnRaceFinished -= HandleRaceFinished;

    void HandleRaceFinished(ArcadeKart kart)
    {
        Debug.Log("Race finished");
        if (m_Triggered) return;
        m_Triggered = true;

        if (followCamera == null || orbitCamera == null || orbitPivot == null)
        {
            Debug.LogWarning("[RaceFinishSequence] One or more camera references are not assigned.", this);
            return;
        }

        // Step 1 — disable controls and apply coasting deceleration
        kart.SetCanMove(false);
        kart.Rigidbody.linearDamping  = coastingLinearDamping;
        kart.Rigidbody.angularDamping = coastingAngularDamping;

        // Step 2 — freeze the follow camera in its current world position
        followCamera.Follow = null;
        followCamera.LookAt = null;

        // Step 3 — switch to the orbit camera after the static delay
        StartCoroutine(ActivateOrbitCameraRoutine(kart));
    }

    IEnumerator ActivateOrbitCameraRoutine(ArcadeKart kart)
    {
        yield return new WaitForSeconds(staticCameraDelay);
        orbitPivot.StartOrbiting(kart.transform);
        orbitCamera.Priority = orbitCameraPriority;
    }
}
