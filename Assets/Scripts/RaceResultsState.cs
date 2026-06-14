using System.Collections;
using Cinemachine;
using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Race results state — absorbs the RaceFinishSequence logic. Freezes the follow camera,
/// applies coast-to-stop physics damping, shows the finish overlay, and blends to the orbit camera.
/// This state is terminal; the game remains here until the player quits.
/// </summary>
public class RaceResultsState : GameStateBase
{
    [Tooltip("The shared follow VCam at scene root — frozen in place when the race ends.")]
    public CinemachineVirtualCamera followCamera;

    [Tooltip("Child orbit VCam on this state root — activated after the static delay.")]
    public CinemachineVirtualCamera orbitCamera;

    [Tooltip("The pivot component on OrbitCam that drives the circular orbit.")]
    public OrbitPivot orbitPivot;

    [Tooltip("Overlay showing the player's finish position.")]
    public GameObject finishPositionOverlay;

    [Tooltip("UI component on the overlay that animates the position label.")]
    public FinishPositionUI finishPositionUI;

    [Tooltip("Seconds the frozen follow camera is held before the orbit camera blends in.")]
    public float staticCameraDelay = 3f;

    [Tooltip("Rigidbody linear damping applied to produce a coast-to-stop.")]
    public float coastingLinearDamping = 3f;

    [Tooltip("Rigidbody angular damping applied when the race ends.")]
    public float coastingAngularDamping = 5f;

    [Tooltip("Priority raised on orbitCamera — must exceed the follow camera's priority of 10.")]
    public int orbitCameraPriority = 20;

    private ArcadeKart m_Kart;

    /// <summary>Stores the finishing kart reference. Must be called before Enter().</summary>
    public void PrepareEntry(ArcadeKart kart)
    {
        m_Kart = kart;
    }

    /// <summary>
    /// Activates the results hierarchy, stops the kart, freezes the follow camera,
    /// shows the overlay, and starts the orbit camera transition routine.
    /// </summary>
    public override void Enter()
    {
        gameObject.SetActive(true);

        if (m_Kart == null)
        {
            Debug.LogWarning("[RaceResultsState] No kart reference — call PrepareEntry() before Enter().", this);
            return;
        }

        // Stop kart controls and apply coasting deceleration.
        m_Kart.SetCanMove(false);
        m_Kart.Rigidbody.linearDamping = coastingLinearDamping;
        m_Kart.Rigidbody.angularDamping = coastingAngularDamping;

        // Freeze follow camera in its current world position.
        if (followCamera != null)
        {
            followCamera.Follow = null;
            followCamera.LookAt = null;
        }

        // Show the finish position overlay and trigger the slide-in animation.
        if (finishPositionOverlay != null)
            finishPositionOverlay.SetActive(true);

        if (finishPositionUI != null)
            finishPositionUI.TriggerSlideIn(m_Kart);

        StartCoroutine(ActivateOrbitCameraRoutine());
    }

    /// <summary>Stops all coroutines and deactivates this state. Implemented for future extensibility.</summary>
    public override void Exit()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    private IEnumerator ActivateOrbitCameraRoutine()
    {
        yield return new WaitForSeconds(staticCameraDelay);

        if (orbitPivot != null)
            orbitPivot.StartOrbiting(m_Kart.transform);

        if (orbitCamera != null)
        {
            orbitCamera.LookAt = m_Kart.transform;
            orbitCamera.Priority = orbitCameraPriority;
        }
    }
}
