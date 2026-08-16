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

    [Tooltip("Game title — starts at alpha 0.")]
    public Image titleLabel;

    [Tooltip("Make100GamesLogo")]
    public Image make100gamesLogo;

    [Tooltip("Menu panel containing the Start Game / Controls / Credits buttons — initially inactive.")]
    public GameObject menuPanel;

    [Tooltip("Button that starts the race.")]
    public Button startGameButton;

    [Tooltip("Button that opens the Controls submenu.")]
    public Button controlsButton;

    [Tooltip("Button that opens the Credits submenu.")]
    public Button creditsButton;

    [Header("Attract Submenus")]
    [Tooltip("CanvasGroup on the main menu (Start/Controls/Credits) — mirrors menuPanel's GameObject.")]
    public CanvasGroup mainMenuGroup;

    [Tooltip("CanvasGroup on the Controls submenu.")]
    public CanvasGroup controlsMenuGroup;

    [Tooltip("CanvasGroup on the Credits submenu.")]
    public CanvasGroup creditsMenuGroup;

    [Tooltip("Button that returns from the Controls submenu to the main menu.")]
    public Button controlsBackButton;

    [Tooltip("Button that returns from the Credits submenu to the main menu.")]
    public Button creditsBackButton;

    [Tooltip("Seconds for a submenu cross-fade transition. Must be non-negative.")]
    public float menuTransitionDuration = 0.2f;

    [Header("Effects")]
    [Tooltip("Particle system played when the player presses any key to reveal the menu.")]
    public ParticleSystem selectedEffect;

    [Header("Title Fade")]
    [Tooltip("Seconds after activation before the title begins to fade in.")]
    public float titleDelay = 5f;

    [Tooltip("Seconds for the title to fade from alpha 0 to 1.")]
    public float titleFadeDuration = 1.5f;

    [Header("Start Prompt")]
    [Tooltip("CanvasGroup on the 'Click anywhere to start' prompt — initially inactive at alpha 0.")]
    public CanvasGroup startPromptGroup;

    [Tooltip("Text component on the start prompt (for validation/reference only).")]
    public TMP_Text startPromptText;

    [Tooltip("Seconds after the title fully appears before the prompt begins to show. Clamped to non-negative.")]
    public float promptDelay = 1f;

    [Tooltip("Seconds for one smooth pulse cycle (dim to bright and back). Clamped to non-negative; zero holds the prompt at max alpha.")]
    public float promptPulseDuration = 1f;

    [Tooltip("Minimum alpha reached during the pulse. Clamped to [0,1].")]
    public float promptMinAlpha = 0.35f;

    [Tooltip("Maximum alpha reached during the pulse. Clamped to [0,1].")]
    public float promptMaxAlpha = 1f;

    [Header("Attract Music")]
    [Tooltip("Shared scene AudioSource used for all music cues.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("Looping attract-mode music cue.")]
    [SerializeField] private AudioClip attractMusicClip;

    [Tooltip("Seconds to wait before starting the attract music cue.")]
    [SerializeField] private float attractMusicDelay = 2f;

    [Header("Sound Effects")]
    [Tooltip("Shared root-level 2D AudioSource used for all menu selection sound effects.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Sound effect played when Start, Controls, or Credits is pressed.")]
    [SerializeField] private AudioClip menuSelectClip;

    [Tooltip("Sound effect played when a submenu Back button is pressed.")]
    [SerializeField] private AudioClip menuBackClip;

    private bool m_TitleVisible;
    private bool m_MenuShown;
    private CanvasGroup m_CurrentMenuGroup;
    private Coroutine m_MenuTransitionCoroutine;
    private Coroutine m_StartPromptCoroutine;
    private bool m_PromptMissingWarned;

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
        if (controlsBackButton != null)
            controlsBackButton.onClick.AddListener(OnControlsBackClicked);
        if (creditsBackButton != null)
            creditsBackButton.onClick.AddListener(OnCreditsBackClicked);
    }

    void OnEnable()
    {
        // Reset state flags and camera priority for clean re-entry.
        if (attractVCam != null)
            attractVCam.Priority = AttractVCamPriority;

        if (titleLabel != null && make100gamesLogo != null)
        {
            Color c = titleLabel.color;
            c.a = 0f;
            titleLabel.color = c;
            make100gamesLogo.color = c;
        }

        m_TitleVisible = false;
        m_MenuShown = false;
        m_PromptMissingWarned = false;
        ResetMenuGroups();
        HideStartPrompt();
    }

    void Update()
    {
        if (!m_TitleVisible || m_MenuShown)
            return;

        if (WasMenuRevealPressedThisFrame())
            ShowMenu();
    }

    /// <summary>Activates this state and begins the title sequence coroutine.</summary>
    public override void Enter()
    {
        gameObject.SetActive(true);
        StartCoroutine(TitleSequenceCoroutine());
        PlayAttractMusic();
    }

    /// <summary>Stops all coroutines and deactivates this state and all its children.</summary>
    public override void Exit()
    {
        StopAllCoroutines();
        m_MenuTransitionCoroutine = null;
        m_StartPromptCoroutine = null;
        ResetMenuGroups();
        HideStartPrompt();
        gameObject.SetActive(false);
    }

    /// <summary>Button callback — delegates race start to GameStateManager.</summary>
    public void StartGame()
    {
        PlayMenuSound(menuSelectClip, "Start");
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
            if (titleLabel != null && make100gamesLogo != null)
            {
                Color c = titleLabel.color;
                c.a = Mathf.Clamp01(elapsed / titleFadeDuration);
                titleLabel.color = c;
                make100gamesLogo.color = c;
            }
            yield return null;
        }

        if (titleLabel != null && make100gamesLogo != null)
        {
            Color final = titleLabel.color;
            final.a = 1f;
            titleLabel.color = final;
            make100gamesLogo.color = final;
        }

        m_TitleVisible = true;
        m_StartPromptCoroutine = StartCoroutine(ShowStartPromptCoroutine());
    }

    /// <summary>Reveals the main menu panel and plays the selected effect particle system.</summary>
    private void ShowMenu()
    {
        m_MenuShown = true;
        HideStartPrompt();
        PlayMenuSound(menuSelectClip, "Reveal Menu");

        if (menuPanel != null)
            menuPanel.SetActive(true);
        if (mainMenuGroup != null)
        {
            m_CurrentMenuGroup = mainMenuGroup;
            SetMenuGroupState(mainMenuGroup, true, 1f, true);
        }
        if (selectedEffect != null)
            selectedEffect.Play();
    }

    /// <summary>
    /// Waits for the configured delay after the title becomes visible, then loops a smooth
    /// alpha pulse on the start prompt until it is cancelled by <see cref="HideStartPrompt"/>
    /// (called from <see cref="ShowMenu"/> or <see cref="Exit"/>). The prompt's GameObject stays
    /// active at all times — only alpha is toggled — so its TextMeshPro label, font asset, and
    /// shader variant are warmed up on scene load instead of on first appearance.
    /// </summary>
    private IEnumerator ShowStartPromptCoroutine()
    {
        if (startPromptGroup == null)
        {
            if (!m_PromptMissingWarned)
            {
                Debug.LogWarning("[AttractModeState] startPromptGroup is unassigned — skipping start prompt.", this);
                m_PromptMissingWarned = true;
            }
            yield break;
        }

        float delay = Mathf.Max(0f, promptDelay);
        yield return new WaitForSeconds(delay);

        float duration = Mathf.Max(0f, promptPulseDuration);
        float minAlpha = Mathf.Clamp01(promptMinAlpha);
        float maxAlpha = Mathf.Clamp01(promptMaxAlpha);

        if (duration <= 0f)
        {
            startPromptGroup.alpha = maxAlpha;
            yield break;
        }

        float elapsedTime = 0f;
        while (true)
        {
            elapsedTime += Time.deltaTime;
            float phase = (Mathf.Sin(elapsedTime / duration * Mathf.PI * 2f) + 1f) * 0.5f;
            startPromptGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, phase);
            yield return null;
        }
    }

    /// <summary>
    /// Checks for any keyboard key or primary/secondary mouse button pressed this frame.
    /// Uses the legacy Input class because the com.unity.inputsystem package is not present
    /// in this project (Assembly-CSharp has no reference to it); this project's Input handling
    /// setting still permits legacy Input calls.
    /// </summary>
    private bool WasMenuRevealPressedThisFrame()
    {
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
    }

    /// <summary>
    /// Cancels the pending/running start-prompt coroutine and resets it to alpha zero.
    /// The prompt's GameObject is left active — only alpha is reset — so the label never
    /// re-triggers TMP's Awake/OnEnable or a first-draw shader compile.
    /// </summary>
    private void HideStartPrompt()
    {
        if (m_StartPromptCoroutine != null)
        {
            StopCoroutine(m_StartPromptCoroutine);
            m_StartPromptCoroutine = null;
        }

        if (startPromptGroup != null)
            startPromptGroup.alpha = 0f;
    }

    /// <summary>
    /// Hard-stops any pending/current cue on the shared source, then schedules the looping
    /// attract-mode music from sample zero after the configured delay. Music is intentionally
    /// left running on Exit() so it continues through character selection.
    /// </summary>
    private void PlayAttractMusic()
    {
        if (musicSource == null || attractMusicClip == null)
        {
            Debug.LogWarning("[AttractModeState] musicSource or attractMusicClip is unassigned — skipping attract music.", this);
            return;
        }

        float delay = Mathf.Max(0f, attractMusicDelay);

        musicSource.Stop();
        musicSource.clip = attractMusicClip;
        musicSource.loop = true;
        musicSource.time = 0f;

        if (delay > 0f)
            musicSource.PlayDelayed(delay);
        else
            musicSource.Play();
    }

    private void OnControlsClicked()
    {
        PlayMenuSound(menuSelectClip, "Controls");
        TransitionToMenu(controlsMenuGroup);
    }

    private void OnCreditsClicked()
    {
        PlayMenuSound(menuSelectClip, "Credits");
        TransitionToMenu(creditsMenuGroup);
    }

    private void OnControlsBackClicked()
    {
        PlayMenuSound(menuBackClip, "Controls Back");
        TransitionToMenu(mainMenuGroup);
    }

    private void OnCreditsBackClicked()
    {
        PlayMenuSound(menuBackClip, "Credits Back");
        TransitionToMenu(mainMenuGroup);
    }

    /// <summary>
    /// Requests a cross-fade transition from the current menu group to targetGroup. Rejects
    /// invalid configuration (missing group) or a request to show the already-current group,
    /// and guards against overlapping transitions.
    /// </summary>
    private void TransitionToMenu(CanvasGroup targetGroup)
    {
        if (targetGroup == null)
        {
            Debug.LogWarning("[AttractModeState] Requested menu group is unassigned — ignoring navigation request.", this);
            return;
        }

        if (targetGroup == m_CurrentMenuGroup)
            return;

        if (m_MenuTransitionCoroutine != null)
            return;

        CanvasGroup outgoingGroup = m_CurrentMenuGroup;
        m_CurrentMenuGroup = targetGroup;
        m_MenuTransitionCoroutine = StartCoroutine(CrossFadeMenuCoroutine(outgoingGroup, targetGroup));
    }

    /// <summary>
    /// Disables outgoing input immediately, activates the incoming group at alpha zero, then
    /// interpolates both alpha values with unscaled time before deactivating the outgoing group
    /// and enabling interaction/raycast blocking only on the incoming group.
    /// </summary>
    private IEnumerator CrossFadeMenuCoroutine(CanvasGroup outgoingGroup, CanvasGroup incomingGroup)
    {
        if (outgoingGroup != null)
            SetMenuGroupState(outgoingGroup, true, outgoingGroup.alpha, false);

        SetMenuGroupState(incomingGroup, true, 0f, false);

        float duration = Mathf.Max(0f, menuTransitionDuration);
        if (duration > 0f)
        {
            float elapsed = 0f;
            float startAlpha = outgoingGroup != null ? outgoingGroup.alpha : 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (outgoingGroup != null)
                    outgoingGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                incomingGroup.alpha = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }
        }

        if (outgoingGroup != null)
            SetMenuGroupState(outgoingGroup, false, 0f, false);

        SetMenuGroupState(incomingGroup, true, 1f, true);

        m_MenuTransitionCoroutine = null;
    }

    /// <summary>
    /// Restores deterministic alpha, active, interactable, and raycast states for all three
    /// attract menu groups. Controls and Credits start hidden; the main group remains hidden
    /// until the title sequence reveals it via ShowMenu().
    /// </summary>
    private void ResetMenuGroups()
    {
        if (mainMenuGroup != null)
            SetMenuGroupState(mainMenuGroup, false, 0f, false);
        if (controlsMenuGroup != null)
            SetMenuGroupState(controlsMenuGroup, false, 0f, false);
        if (creditsMenuGroup != null)
            SetMenuGroupState(creditsMenuGroup, false, 0f, false);

        m_CurrentMenuGroup = null;
    }

    /// <summary>Centralizes active state, alpha, interactable, and raycast updates for a menu CanvasGroup.</summary>
    private void SetMenuGroupState(CanvasGroup group, bool active, float alpha, bool acceptsInput)
    {
        if (group == null)
            return;

        group.gameObject.SetActive(active);
        group.alpha = alpha;
        group.interactable = acceptsInput;
        group.blocksRaycasts = acceptsInput;
    }

    /// <summary>
    /// Hard-stops any pending/current cue on the shared sound-effects source, then plays
    /// the given clip from sample zero. Missing source/clip is non-fatal to navigation.
    /// </summary>
    private void PlayMenuSound(AudioClip clip, string cueName)
    {
        if (sfxSource == null || clip == null)
        {
            Debug.LogWarning($"[AttractModeState] sfxSource or {cueName} clip is unassigned — skipping menu sound.", this);
            return;
        }

        sfxSource.Stop();
        sfxSource.clip = clip;
        sfxSource.time = 0f;
        sfxSource.Play();
    }
}
