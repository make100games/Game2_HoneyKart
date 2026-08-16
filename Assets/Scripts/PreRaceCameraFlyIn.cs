using System.Collections;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Orchestrates the 4-camera pre-race fly-in sequence via Cinemachine priorities and per-phase
/// camera motion, then triggers the race countdown via <see cref="GameFlowManager.BeginRaceCountdown"/>.
/// Attach to a GameObject under /RaceState/RaceRoot. The sequence self-starts in OnEnable.
/// </summary>
public class PreRaceCameraFlyIn : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Priority constants (follow cam stays at 10, so it wins when all fly-in
    // cams are at InactiveCameraPriority)
    // -------------------------------------------------------------------------
    private const int ActiveCameraPriority   = 30;
    private const int InactiveCameraPriority = 0;

    // -------------------------------------------------------------------------
    // Camera references
    // -------------------------------------------------------------------------
    [Header("Cameras")]
    [Tooltip("Camera 1 — high above the start line, descends straight down during its phase.")]
    public CinemachineVirtualCamera descendCamera;

    [Tooltip("Camera 2 — dolly flythrough driven by the AttractCameraController on this VCam.")]
    public CinemachineVirtualCamera flythroughCamera;

    [Tooltip("Camera 3 — positioned down the track facing the racers; moves forward and looks at the player kart.")]
    public CinemachineVirtualCamera approachCamera;

    [Tooltip("Camera 4 — the existing race follow cam (/CinemachineVirtualCamera). Already bound by RaceState.Enter().")]
    public CinemachineVirtualCamera raceFollowCamera;

    // -------------------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------------------
    [Header("Dependencies")]
    [Tooltip("Reference to GameFlowManager so BeginRaceCountdown() can be called after the fly-in completes.")]
    public GameFlowManager gameFlowManager;

    // -------------------------------------------------------------------------
    // Phase timings
    // -------------------------------------------------------------------------
    [Header("Phase Durations (seconds)")]
    [Tooltip("How long Camera 1 (descend) is active.")]
    public float descendDuration = 2f;

    [Tooltip("How long Camera 2 (flythrough) is active.")]
    public float flythroughDuration = 2f;

    [Tooltip("How long Camera 3 (approach) is active.")]
    public float approachDuration = 2f;

    [Tooltip("How long Camera 4 (race follow) idles before the countdown is triggered.")]
    public float followIdleDuration = 4f;

    // -------------------------------------------------------------------------
    // Motion parameters
    // -------------------------------------------------------------------------
    [Header("Motion")]
    [Tooltip("Units per second at which Camera 1 descends straight down.")]
    public float descendSpeed = 3f;

    [Tooltip("Units per second at which Camera 3 moves along its own forward direction toward the kart lineup.")]
    public float approachSpeed = 15f;

    // -------------------------------------------------------------------------
    // Public phase events — UI subscribers (e.g. RaceUIPolishController) hook
    // into these to synchronize presentation with the camera sequence without
    // this component taking on any UI responsibilities.
    // -------------------------------------------------------------------------

    /// <summary>Raised immediately after the fly-through camera (Phase 2) becomes active.</summary>
    public event System.Action FlythroughStarted;

    /// <summary>Raised immediately after all fly-in cameras are lowered and the race follow camera takes over (Phase 4 handoff).</summary>
    public event System.Action FollowCameraActivated;

    // -------------------------------------------------------------------------
    // MonoBehaviour
    // -------------------------------------------------------------------------

    void OnEnable()
    {
        StartCoroutine(RunFlyInSequenceRoutine());
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs all four camera phases in sequence then calls
    /// <see cref="GameFlowManager.BeginRaceCountdown"/> to start the race.
    /// </summary>
    private IEnumerator RunFlyInSequenceRoutine()
    {
        // Validation — abort if critical references are missing.
        if (gameFlowManager == null)
        {
            Debug.LogWarning("[PreRaceCameraFlyIn] gameFlowManager is not assigned — aborting fly-in. The race countdown will not start.");
            yield break;
        }

        // ------------------------------------------------------------------
        // Phase 1: Descend camera — high above start line, drops straight down
        // ------------------------------------------------------------------
        if (descendCamera != null)
        {
            SetActiveCamera(descendCamera);
            float elapsed = 0f;
            while (elapsed < descendDuration)
            {
                descendCamera.transform.position += Vector3.down * descendSpeed * Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            Debug.LogWarning("[PreRaceCameraFlyIn] descendCamera is null — skipping Phase 1.");
        }

        // ------------------------------------------------------------------
        // Phase 2: Flythrough camera — dolly along the shared attract path
        // (motion is driven independently by AttractCameraController on this VCam)
        // ------------------------------------------------------------------
        if (flythroughCamera != null)
        {
            SetActiveCamera(flythroughCamera);
            FlythroughStarted?.Invoke();
            yield return new WaitForSeconds(flythroughDuration);
        }
        else
        {
            Debug.LogWarning("[PreRaceCameraFlyIn] flythroughCamera is null — skipping Phase 2.");
        }

        // ------------------------------------------------------------------
        // Phase 3: Approach camera — moves toward the kart lineup, looks at
        // the player kart via raceFollowCamera.Follow
        // ------------------------------------------------------------------
        if (approachCamera != null)
        {
            SetActiveCamera(approachCamera);

            // Bind look target to the player kart transform that RaceState already set.
            if (raceFollowCamera != null && raceFollowCamera.Follow != null)
                approachCamera.LookAt = raceFollowCamera.Follow;
            else
                Debug.LogWarning("[PreRaceCameraFlyIn] raceFollowCamera or its Follow target is null — approach camera will not look at the kart.");

            float elapsed = 0f;
            while (elapsed < approachDuration)
            {
                approachCamera.transform.position += approachCamera.transform.forward * approachSpeed * Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            Debug.LogWarning("[PreRaceCameraFlyIn] approachCamera is null — skipping Phase 3.");
        }

        // ------------------------------------------------------------------
        // Phase 4: Hand off to the race follow cam — all fly-in cams go to
        // InactiveCameraPriority; follow cam at priority 10 takes over.
        // ------------------------------------------------------------------
        SetActiveCamera(null);
        FollowCameraActivated?.Invoke();
        yield return new WaitForSeconds(followIdleDuration);

        gameFlowManager.BeginRaceCountdown();
    }

    /// <summary>
    /// Sets <paramref name="active"/> to <see cref="ActiveCameraPriority"/> and all other
    /// fly-in cameras to <see cref="InactiveCameraPriority"/>. Pass null to deactivate all.
    /// </summary>
    private void SetActiveCamera(CinemachineVirtualCamera active)
    {
        SetPriority(descendCamera,    active == descendCamera    ? ActiveCameraPriority : InactiveCameraPriority);
        SetPriority(flythroughCamera, active == flythroughCamera ? ActiveCameraPriority : InactiveCameraPriority);
        SetPriority(approachCamera,   active == approachCamera   ? ActiveCameraPriority : InactiveCameraPriority);
    }

    private static void SetPriority(CinemachineVirtualCamera vcam, int priority)
    {
        if (vcam != null)
            vcam.Priority = priority;
    }
}
