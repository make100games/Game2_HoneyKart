using System.Collections;
using Cinemachine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attract mode state — flies the dolly camera, fades in the title, waits for any key to
/// reveal the main menu, then delegates to GameStateManager to start the race.
/// Extends GameStateBase; entry and exit are driven by GameStateManager.
/// </summary>
public class AttractModeState : GameStateBase
{
    [Header("Attract Camera")]
    [Tooltip("The CinemachineVirtualCamera used for the attract dolly flythrough.")]
    public CinemachineVirtualCamera attractVCam;

    [Header("Attract UI")]
    [Tooltip("Root canvas for all attract UI — deactivated via hierarchy on Exit().")]
    public GameObject attractCanvas;

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
    [Tooltip("Seconds after activation before the title begins to fade in.")]
    public float titleDelay = 5f;

    [Tooltip("Seconds for the title to fade from alpha 0 to 1.")]
    public float titleFadeDuration = 1.5f;

    private bool m_TitleVisible;
    private bool m_MenuShown;

    private const int AttractVCamPriority = 50;

    void Awake()
    {
        // One-time button listener setup — runs once regardless of how many times we re-enter.
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartGame);
        if (controlsButton != null)
            controlsButton.onClick.AddListener(OnControlsClicked);
        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsClicked);
    }

    void OnEnable()
    {
        // Reset state flags and camera priority for clean re-entry.
        if (attractVCam != null)
            attractVCam.Priority = AttractVCamPriority;

        if (titleLabel != null)
        {
            Color c = titleLabel.color;
            c.a = 0f;
            titleLabel.color = c;
        }

        m_TitleVisible = false;
        m_MenuShown = false;
    }

    void Update()
    {
        if (!m_TitleVisible || m_MenuShown)
            return;

        if (Input.anyKeyDown)
            ShowMenu();
    }

    /// <summary>Activates this state and begins the title sequence coroutine.</summary>
    public override void Enter()
    {
        gameObject.SetActive(true);
        StartCoroutine(TitleSequenceCoroutine());
    }

    /// <summary>Stops all coroutines and deactivates this state and all its children.</summary>
    public override void Exit()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    /// <summary>Button callback — delegates race start to GameStateManager.</summary>
    public void StartGame()
    {
        GameStateManager.Instance.StartGame();
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
        Debug.Log("[AttractModeState] Controls button clicked (stub).");
    }

    private void OnCreditsClicked()
    {
        Debug.Log("[AttractModeState] Credits button clicked (stub).");
    }
}
