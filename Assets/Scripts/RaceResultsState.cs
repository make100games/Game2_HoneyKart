using System.Collections;
using Cinemachine;
using KartGame.KartSystems;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Race results state — absorbs the RaceFinishSequence logic. Freezes the follow camera,
/// applies coast-to-stop physics damping, shows the finish overlay, and blends to the orbit camera.
/// After the orbit camera activates the results UI panels slide up from offscreen.
/// Button listeners wire Restart, Change Character, and Quit transitions.
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

    [Header("Results UI")]
    [SerializeField] private RectTransform bottomPanel;
    [SerializeField] private RectTransform buttonRestartRect;
    [SerializeField] private RectTransform buttonChangeCharacterRect;
    [SerializeField] private RectTransform buttonQuitRect;
    [SerializeField] private Button buttonRestart;
    [SerializeField] private Button buttonChangeCharacter;
    [SerializeField] private Button buttonQuit;

    private const float SlideInDuration = 0.4f;
    private const float SlideInOffscreenOffset = 300f;

    private Vector2 bottomPanelTargetPos;
    private Vector2 buttonRestartTargetPos;
    private Vector2 buttonChangeCharacterTargetPos;
    private Vector2 buttonQuitTargetPos;

    private float m_OriginalLinearDamping;
    private float m_OriginalAngularDamping;

    private ArcadeKart m_Kart;

    void Awake()
    {
        if (bottomPanel != null)
            bottomPanelTargetPos = bottomPanel.anchoredPosition;
        if (buttonRestartRect != null)
            buttonRestartTargetPos = buttonRestartRect.anchoredPosition;
        if (buttonChangeCharacterRect != null)
            buttonChangeCharacterTargetPos = buttonChangeCharacterRect.anchoredPosition;
        if (buttonQuitRect != null)
            buttonQuitTargetPos = buttonQuitRect.anchoredPosition;

        if (buttonRestart != null)
            buttonRestart.onClick.AddListener(OnRestartPressed);
        if (buttonChangeCharacter != null)
            buttonChangeCharacter.onClick.AddListener(OnChangeCharacterPressed);
        if (buttonQuit != null)
            buttonQuit.onClick.AddListener(OnQuitPressed);
    }

    /// <summary>Stores the finishing kart reference. Must be called before Enter().</summary>
    public void PrepareEntry(ArcadeKart kart)
    {
        m_Kart = kart;
    }

    /// <summary>
    /// Activates the results hierarchy, stops the kart, freezes the follow camera,
    /// shows the overlay, offsets the results UI offscreen, and starts the orbit camera transition routine.
    /// </summary>
    public override void Enter()
    {
        gameObject.SetActive(true);

        if (m_Kart == null)
        {
            Debug.LogWarning("[RaceResultsState] No kart reference — call PrepareEntry() before Enter().", this);
            return;
        }

        // Cache original damping values before overwriting.
        m_OriginalLinearDamping = m_Kart.Rigidbody.linearDamping;
        m_OriginalAngularDamping = m_Kart.Rigidbody.angularDamping;

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

        // Push results UI panels fully offscreen below the canvas.
        if (bottomPanel != null)
            bottomPanel.anchoredPosition = bottomPanelTargetPos + Vector2.down * SlideInOffscreenOffset;
        if (buttonRestartRect != null)
            buttonRestartRect.anchoredPosition = buttonRestartTargetPos + Vector2.down * SlideInOffscreenOffset;
        if (buttonChangeCharacterRect != null)
            buttonChangeCharacterRect.anchoredPosition = buttonChangeCharacterTargetPos + Vector2.down * SlideInOffscreenOffset;
        if (buttonQuitRect != null)
            buttonQuitRect.anchoredPosition = buttonQuitTargetPos + Vector2.down * SlideInOffscreenOffset;

        StartCoroutine(ActivateOrbitCameraRoutine());
    }

    /// <summary>Restores kart state, stops all coroutines, and deactivates this state.</summary>
    public override void Exit()
    {
        if (m_Kart != null)
        {
            m_Kart.SetCanMove(true);
            m_Kart.Rigidbody.linearDamping = m_OriginalLinearDamping;
            m_Kart.Rigidbody.angularDamping = m_OriginalAngularDamping;
        }

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

        StartCoroutine(SlideInResultsUI());
    }

    /// <summary>Slides all four results UI elements from their offscreen positions to their target positions.</summary>
    private IEnumerator SlideInResultsUI()
    {
        Vector2 bottomStart = bottomPanel != null ? bottomPanel.anchoredPosition : Vector2.zero;
        Vector2 restartStart = buttonRestartRect != null ? buttonRestartRect.anchoredPosition : Vector2.zero;
        Vector2 changeCharStart = buttonChangeCharacterRect != null ? buttonChangeCharacterRect.anchoredPosition : Vector2.zero;
        Vector2 quitStart = buttonQuitRect != null ? buttonQuitRect.anchoredPosition : Vector2.zero;

        float elapsed = 0f;
        while (elapsed < SlideInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / SlideInDuration);

            if (bottomPanel != null)
                bottomPanel.anchoredPosition = Vector2.Lerp(bottomStart, bottomPanelTargetPos, t);
            if (buttonRestartRect != null)
                buttonRestartRect.anchoredPosition = Vector2.Lerp(restartStart, buttonRestartTargetPos, t);
            if (buttonChangeCharacterRect != null)
                buttonChangeCharacterRect.anchoredPosition = Vector2.Lerp(changeCharStart, buttonChangeCharacterTargetPos, t);
            if (buttonQuitRect != null)
                buttonQuitRect.anchoredPosition = Vector2.Lerp(quitStart, buttonQuitTargetPos, t);

            yield return null;
        }

        // Snap to final positions.
        if (bottomPanel != null)
            bottomPanel.anchoredPosition = bottomPanelTargetPos;
        if (buttonRestartRect != null)
            buttonRestartRect.anchoredPosition = buttonRestartTargetPos;
        if (buttonChangeCharacterRect != null)
            buttonChangeCharacterRect.anchoredPosition = buttonChangeCharacterTargetPos;
        if (buttonQuitRect != null)
            buttonQuitRect.anchoredPosition = buttonQuitTargetPos;
    }

    private void OnRestartPressed() => GameStateManager.Instance.EnterRace();
    private void OnChangeCharacterPressed() => GameStateManager.Instance.StartGame();
    private void OnQuitPressed() => GameStateManager.Instance.RestartGame();
}
