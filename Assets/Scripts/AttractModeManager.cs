using System.Collections;
using Cinemachine;
using KartGame.KartSystems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central orchestrator for attract mode. Runs before all other scripts via DefaultExecutionOrder(-100).
/// In attract mode: hides the player kart, disables race managers, flies the dolly camera, fades in the
/// title, and waits for any key to reveal the main menu. Pressing Start Game reloads into race mode.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AttractModeManager : MonoBehaviour
{
    [Header("Race Objects")]
    [Tooltip("The player's ArcadeKart — hidden in attract mode.")]
    public ArcadeKart playerKart;

    [Tooltip("The GameFlowManager component — disabled in attract mode.")]
    public GameFlowManager gameFlowManager;

    [Tooltip("The ObjectiveManager component — disabled in attract mode.")]
    public ObjectiveManager objectiveManager;

    [Tooltip("The GameHUD root GameObject — deactivated in attract mode.")]
    public GameObject gameHUD;

    [Tooltip("The InGameMenu root GameObject — deactivated in attract mode.")]
    public GameObject inGameMenu;

    [Tooltip("The FinishPositionOverlay root GameObject — deactivated in attract mode.")]
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
            // Suppress normal race flow
            if (playerKart != null)
                playerKart.gameObject.SetActive(false);

            if (gameFlowManager != null)
                gameFlowManager.enabled = false;

            if (objectiveManager != null)
                objectiveManager.enabled = false;

            if (gameHUD != null)
                gameHUD.SetActive(false);

            if (inGameMenu != null)
                inGameMenu.SetActive(false);

            if (finishPositionOverlay != null)
                finishPositionOverlay.SetActive(false);

            // Elevate attract VCam priority above the follow VCam (priority 10)
            if (attractVCam != null)
                attractVCam.Priority = AttractVCamPriority;

            // Allow all AI karts to move immediately (GameFlowManager is disabled so won't call SetCanMove)
            ArcadeKart[] allKarts = FindObjectsByType<ArcadeKart>(FindObjectsSortMode.None);
            foreach (ArcadeKart kart in allKarts)
            {
                kart.SetCanMove(true);
            }
        }
        else
        {
            // Normal race mode — disable the attract camera so it never interferes
            if (attractVCam != null)
                attractVCam.gameObject.SetActive(false);

            // Also disable this manager — it has no work to do in race mode
            enabled = false;
        }
    }

    void Start()
    {
        // Wire button listeners
        if (startGameButton != null)
            startGameButton.onClick.AddListener(GameModeState.StartGame);

        if (controlsButton != null)
            controlsButton.onClick.AddListener(OnControlsClicked);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsClicked);

        if (GameModeState.IsAttractMode)
        {
            // Ensure title is fully transparent at start
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

    /// <summary>Waits for titleDelay seconds then fades the title label in over titleFadeDuration.</summary>
    private IEnumerator TitleSequenceCoroutine()
    {
        yield return new WaitForSeconds(titleDelay);

        float elapsed = 0f;
        while (elapsed < titleFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / titleFadeDuration);
            if (titleLabel != null)
            {
                Color c = titleLabel.color;
                c.a = alpha;
                titleLabel.color = c;
            }
            yield return null;
        }

        // Ensure fully opaque
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
        // Stub — reserved for future controls screen
        Debug.Log("[AttractModeManager] Controls button clicked (stub).");
    }

    private void OnCreditsClicked()
    {
        // Stub — reserved for future credits screen
        Debug.Log("[AttractModeManager] Credits button clicked (stub).");
    }
}
