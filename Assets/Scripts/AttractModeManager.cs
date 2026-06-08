using System.Collections;
using Cinemachine;
using KartGame.KartSystems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central orchestrator for attract mode. Runs before all other scripts via DefaultExecutionOrder(-100).
/// In attract mode: deactivates the entire race subsystem via a single RaceRoot.SetActive(false) call,
/// suppressing all descendant lifecycles cleanly. Also disables prefab-bound components that cannot be
/// moved to RaceRoot. Flies the dolly camera, fades in the title, and waits for any key to reveal the
/// main menu. Pressing Start Game reloads the scene into race mode.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AttractModeManager : MonoBehaviour
{
    [Header("Race Systems")]
    [Tooltip("Parent of all race-only scene objects. SetActive(false) in attract mode suppresses the entire subsystem lifecycle — no partial initialization, no null delegates.")]
    public GameObject raceRoot;

    [Header("GameManager Race Components")]
    [Tooltip("Disabled in attract mode. Lives on the GameManager prefab and cannot be moved to RaceRoot.")]
    public GameFlowManager gameFlowManager;

    [Tooltip("Disabled in attract mode. Lives on the GameManager prefab and cannot be moved to RaceRoot.")]
    public ObjectiveManager objectiveManager;

    [Header("GameManager HUD Children")]
    [Tooltip("Deactivated in attract mode. Child of the GameManager prefab.")]
    public GameObject gameHUD;

    [Tooltip("Deactivated in attract mode. Child of the GameManager prefab.")]
    public GameObject inGameMenu;

    [Tooltip("Deactivated in attract mode. Child of the GameManager prefab.")]
    public GameObject finishPositionOverlay;

    [Header("Attract Camera")]
    [Tooltip("The CinemachineVirtualCamera used for the attract dolly flythrough.")]
    public CinemachineVirtualCamera attractVCam;

    [Header("Attract UI")]
    [Tooltip("TMP label that shows the game title — starts at alpha 0.")]
    public TMP_Text titleLabel;

    [Tooltip("Menu panel containing the Start Game / Controls / Credits buttons — initially inactive.")]
    public GameObject menuPanel;

    [Tooltip("Button that starts the race.")]
    public Button startGameButton;

    [Tooltip("Button that shows controls (stub).")]
    public Button controlsButton;

    [Tooltip("Button that shows credits (stub).")]
    public Button creditsButton;

    [Header("Title Fade")]
    [Tooltip("Seconds after scene load before the title begins to fade in.")]
    public float titleDelay = 5f;

    [Tooltip("Seconds for the title to fade from alpha 0 to 1.")]
    public float titleFadeDuration = 1.5f;

    private bool m_TitleVisible;
    private bool m_MenuShown;

    private const int AttractVCamPriority = 50;

    void Awake()
    {
        if (GameModeState.IsAttractMode)
        {
            // One call deactivates the entire race subsystem — player kart, checkpoints, race triggers,
            // victory camera. All descendants have their Awake/OnEnable/Start suppressed completely.
            if (raceRoot != null)
                raceRoot.SetActive(false);

            // Disable race-only components that live on the GameManager prefab and cannot be moved to RaceRoot.
            if (gameFlowManager != null)
                gameFlowManager.enabled = false;
            if (objectiveManager != null)
                objectiveManager.enabled = false;

            // Disable all Objective subcomponents on this same GameObject (e.g. LapObjective).
            // Prevents their Start() from running and invoking the unsubscribed static RegisterObjective delegate.
            foreach (Objective obj in GetComponents<Objective>())
                obj.enabled = false;

            // Deactivate HUD children of the GameManager prefab.
            if (gameHUD != null)
                gameHUD.SetActive(false);
            if (inGameMenu != null)
                inGameMenu.SetActive(false);
            if (finishPositionOverlay != null)
                finishPositionOverlay.SetActive(false);

            // Elevate attract VCam priority above the follow VCam (priority 10).
            if (attractVCam != null)
                attractVCam.Priority = AttractVCamPriority;

            // RaceRoot is now inactive, so FindObjectsByType returns only the AI karts.
            foreach (ArcadeKart kart in FindObjectsByType<ArcadeKart>(FindObjectsSortMode.None))
                kart.SetCanMove(true);
        }
        else
        {
            // Normal race mode — deactivate the attract camera so it never interferes.
            if (attractVCam != null)
                attractVCam.gameObject.SetActive(false);

            enabled = false;
        }
    }

    void Start()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(GameModeState.StartGame);
        if (controlsButton != null)
            controlsButton.onClick.AddListener(OnControlsClicked);
        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsClicked);

        if (GameModeState.IsAttractMode)
        {
            if (titleLabel != null)
            {
                Color c = titleLabel.color;
                c.a = 0f;
                titleLabel.color = c;
            }

            StartCoroutine(TitleSequenceCoroutine());
        }
    }

    void Update()
    {
        if (!GameModeState.IsAttractMode || !m_TitleVisible || m_MenuShown)
            return;

        if (Input.anyKeyDown)
            ShowMenu();
    }

    /// <summary>Waits titleDelay seconds then fades the title label in over titleFadeDuration.</summary>
    private IEnumerator TitleSequenceCoroutine()
    {
        yield return new WaitForSeconds(titleDelay);

        float elapsed = 0f;
        while (elapsed < titleFadeDuration)
        {
            elapsed += Time.deltaTime;
            if (titleLabel != null)
            {
                Color c = titleLabel.color;
                c.a = Mathf.Clamp01(elapsed / titleFadeDuration);
                titleLabel.color = c;
            }
            yield return null;
        }

        if (titleLabel != null)
        {
            Color final = titleLabel.color;
            final.a = 1f;
            titleLabel.color = final;
        }

        m_TitleVisible = true;
    }

    /// <summary>Reveals the main menu panel.</summary>
    private void ShowMenu()
    {
        m_MenuShown = true;
        if (menuPanel != null)
            menuPanel.SetActive(true);
    }

    private void OnControlsClicked()
    {
        Debug.Log("[AttractModeManager] Controls button clicked (stub).");
    }

    private void OnCreditsClicked()
    {
        Debug.Log("[AttractModeManager] Credits button clicked (stub).");
    }
}
