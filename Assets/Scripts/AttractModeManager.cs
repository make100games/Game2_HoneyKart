using System.Collections;
using Cinemachine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Orchestrates attract mode: flies the dolly camera, fades in the title, waits for any key to reveal
/// the main menu, then hands off to the race by activating GameManager and RaceRoot exactly once.
/// This component lives on the /AttractMode GameObject, which is separate from GameManager.
/// GameManager and RaceRoot start inactive — their Awake/Start run fresh on StartGame().
/// </summary>
public class AttractModeManager : MonoBehaviour
{
    [Header("Race Systems")]
    [Tooltip("The GameManager GameObject — starts inactive, activated on StartGame().")]
    public GameObject gameManager;

    [Tooltip("The RaceRoot GameObject — starts inactive, activated on StartGame().")]
    public GameObject raceRoot;

    [Tooltip("Resets AI opponent karts to their spawn positions before the race begins.")]
    public OpponentSpawnManager opponentSpawnManager;

    [Header("Attract Camera")]
    [Tooltip("The CinemachineVirtualCamera used for the attract dolly flythrough.")]
    public CinemachineVirtualCamera attractVCam;

    [Header("Attract UI")]
    [Tooltip("Root canvas for all attract UI — deactivated on StartGame().")]
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
    [Tooltip("Seconds after scene load before the title begins to fade in.")]
    public float titleDelay = 5f;

    [Tooltip("Seconds for the title to fade from alpha 0 to 1.")]
    public float titleFadeDuration = 1.5f;

    private bool m_TitleVisible;
    private bool m_MenuShown;

    private const int AttractVCamPriority = 50;

    void Awake()
    {
        // Elevate attract VCam above the follow VCam so the dolly shot takes over immediately.
        if (attractVCam != null)
            attractVCam.Priority = AttractVCamPriority;

        // Title starts invisible; TitleSequenceCoroutine fades it in after titleDelay.
        if (titleLabel != null)
        {
            Color c = titleLabel.color;
            c.a = 0f;
            titleLabel.color = c;
        }
    }

    void Start()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartGame);
        if (controlsButton != null)
            controlsButton.onClick.AddListener(OnControlsClicked);
        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsClicked);

        StartCoroutine(TitleSequenceCoroutine());
    }

    void Update()
    {
        if (!m_TitleVisible || m_MenuShown)
            return;

        if (Input.anyKeyDown)
            ShowMenu();
    }

    /// <summary>
    /// Transitions from attract mode into race mode. Activates GameManager and RaceRoot (triggering
    /// their deferred Awake/Start), disables the attract camera, then self-deactivates to cleanly
    /// stop all coroutines and Update.
    /// </summary>
    public void StartGame()
    {
        GameModeState.IsAttractMode = false;

        if (opponentSpawnManager != null)
            opponentSpawnManager.ResetToSpawn();

        if (gameManager != null)
            gameManager.SetActive(true);
        if (raceRoot != null)
            raceRoot.SetActive(true);
        if (attractVCam != null)
            attractVCam.gameObject.SetActive(false);
        if (attractCanvas != null)
            attractCanvas.SetActive(false);

        // Self-deactivate: stops Update and all running coroutines without needing StopAllCoroutines().
        gameObject.SetActive(false);
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
