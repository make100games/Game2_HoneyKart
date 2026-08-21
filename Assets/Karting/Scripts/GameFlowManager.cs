using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using KartGame.KartSystems;
using UnityEngine.SceneManagement;

public enum GameState{Play, Won, Lost}

public class GameFlowManager : MonoBehaviour
{
    [Header("Parameters")]
    [Tooltip("Duration of the fade-to-black at the end of the game")]
    public float endSceneLoadDelay = 3f;
    [Tooltip("The canvas group of the fade-to-black screen")]
    public CanvasGroup endGameFadeCanvasGroup;

    [Header("Win")]
    [Tooltip("This string has to be the name of the scene you want to load when winning")]
    public string winSceneName = "WinScene";
    [Tooltip("Duration of delay before the fade-to-black, if winning")]
    public float delayBeforeFadeToBlack = 4f;
    [Tooltip("Duration of delay before the win message")]
    public float delayBeforeWinMessage = 2f;
    [Tooltip("Sound played on win")]
    public AudioClip victorySound;

    [Tooltip("Prefab for the win game message")]
    public DisplayMessage winDisplayMessage;

    public PlayableDirector raceCountdownTrigger;

    [Header("Race Music")]
    [Tooltip("Shared scene AudioSource used for all music cues.")]
    public AudioSource musicSource;

    [Tooltip("Looping race music cue restarted when kart movement is enabled after the countdown.")]
    public AudioClip raceMusicClip;

    [Tooltip("Seconds to wait before restarting the race music cue.")]
    public float raceMusicDelay = 0f;

    [Header("Lose")]
    [Tooltip("This string has to be the name of the scene you want to load when losing")]
    public string loseSceneName = "LoseScene";
    [Tooltip("Prefab for the lose game message")]
    public DisplayMessage loseDisplayMessage;


    /// <summary>Fired once when all laps are completed. Carries the player kart for observers.</summary>
    public static event System.Action<ArcadeKart> OnRaceFinished;

    /// <summary>True from the moment the countdown ends (karts can move) until the race finishes
    /// or ends. Used to restrict the pause menu to being togglable only during active racing.</summary>
    public static bool IsRaceActive { get; private set; }

    public GameState gameState { get; private set; }

    public bool autoFindKarts = true;
    public ArcadeKart playerKart;

    ArcadeKart[] karts;
    ObjectiveManager m_ObjectiveManager;
    TimeManager m_TimeManager;
    float m_TimeLoadEndGameScene;
    string m_SceneToLoad;
    float elapsedTimeBeforeEndScene = 0;
    private bool m_RaceFinished = false;
    private bool m_CountdownStarted = false;

    void Start()
    {
        if (autoFindKarts)
        {
            karts = FindObjectsOfType<ArcadeKart>();
            if (karts.Length > 0)
            {
                if (!playerKart) playerKart = karts[0];
            }
            DebugUtility.HandleErrorIfNullFindObject<ArcadeKart, GameFlowManager>(playerKart, this);
        }
        else
        {
            // autoFindKarts is false — playerKart is explicitly assigned.
            // Still find all karts so SetCanMove is applied to opponents as well.
            karts = FindObjectsOfType<ArcadeKart>();
            if (playerKart == null)
                Debug.LogWarning("[GameFlowManager] autoFindKarts is false but playerKart is not assigned.");
        }

        m_ObjectiveManager = FindObjectOfType<ObjectiveManager>();
		DebugUtility.HandleErrorIfNullFindObject<ObjectiveManager, GameFlowManager>(m_ObjectiveManager, this);

        m_TimeManager = FindObjectOfType<TimeManager>();
        DebugUtility.HandleErrorIfNullFindObject<TimeManager, GameFlowManager>(m_TimeManager, this);

        AudioUtility.SetMasterVolume(1);

        winDisplayMessage.gameObject.SetActive(false);
        loseDisplayMessage.gameObject.SetActive(false);

        m_TimeManager.StopRace();
        IsRaceActive = false;
        foreach (ArcadeKart k in karts)
        {
			k.SetCanMove(false);
        }

        // Countdown is deferred — BeginRaceCountdown() is called by PreRaceCameraFlyIn
        // after the fly-in sequence completes (~10 s after race entry).
    }

    /// <summary>
    /// Plays the countdown Timeline, shows objectives, and starts the race after the
    /// standard 3-second countdown. Called by <see cref="PreRaceCameraFlyIn"/> at the
    /// end of the fly-in sequence. Guarded so it fires at most once per race entry.
    /// </summary>
    public void BeginRaceCountdown()
    {
        if (m_CountdownStarted)
            return;

        m_CountdownStarted = true;
        ShowRaceCountdownAnimation();
        StartCoroutine(ShowObjectivesRoutine());
        StartCoroutine(CountdownThenStartRaceRoutine());
    }

    IEnumerator CountdownThenStartRaceRoutine() {
        yield return new WaitForSeconds(3f);
        StartRace();
    }

    void StartRace() {
        foreach (ArcadeKart k in karts)
        {
			k.SetCanMove(true);
        }
        m_TimeManager.StartRace();
        IsRaceActive = true;
        PlayRaceMusic();
    }

    /// <summary>
    /// Hard-stops the fly-in cue (or whatever is currently playing on the shared source), then
    /// schedules the looping race music from sample zero after the configured delay.
    /// </summary>
    void PlayRaceMusic()
    {
        if (musicSource == null || raceMusicClip == null)
        {
            Debug.LogWarning("[GameFlowManager] musicSource or raceMusicClip is unassigned — skipping race music.", this);
            return;
        }

        float delay = Mathf.Max(0f, raceMusicDelay);

        musicSource.Stop();
        musicSource.clip = raceMusicClip;
        musicSource.loop = true;
        musicSource.time = 0f;

        if (delay > 0f)
            musicSource.PlayDelayed(delay);
        else
            musicSource.Play();
    }

    void ShowRaceCountdownAnimation() {
        raceCountdownTrigger.Play();
    }

    IEnumerator ShowObjectivesRoutine() {
        while (m_ObjectiveManager.Objectives.Count == 0)
            yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        for (int i = 0; i < m_ObjectiveManager.Objectives.Count; i++)
        {
           if (m_ObjectiveManager.Objectives[i].displayMessage)m_ObjectiveManager.Objectives[i].displayMessage.Display();
           yield return new WaitForSecondsRealtime(1f);
        }
    }


    void Update()
    {

        if (gameState != GameState.Play)
        {
            elapsedTimeBeforeEndScene += Time.deltaTime;
            if(elapsedTimeBeforeEndScene >= endSceneLoadDelay)
            {

                float timeRatio = 1 - (m_TimeLoadEndGameScene - Time.time) / endSceneLoadDelay;
                endGameFadeCanvasGroup.alpha = timeRatio;

                float volumeRatio = Mathf.Abs(timeRatio);
                float volume = Mathf.Clamp(1 - volumeRatio, 0, 1);
                AudioUtility.SetMasterVolume(volume);

                // See if it's time to load the end scene (after the delay)
                if (Time.time >= m_TimeLoadEndGameScene)
                {
                    SceneManager.LoadScene(m_SceneToLoad);
                    gameState = GameState.Play;
                }
            }
        }
        else
        {
            if (!m_RaceFinished && m_ObjectiveManager.AreAllObjectivesCompleted())
            {
                m_RaceFinished = true;
                IsRaceActive = false;
                OnRaceFinished?.Invoke(playerKart);
            }

            if (m_TimeManager.IsFinite && m_TimeManager.IsOver)
                EndGame(false);
        }
    }

    void EndGame(bool win)
    {
        // unlocks the cursor before leaving the scene, to be able to click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        m_TimeManager.StopRace();
        IsRaceActive = false;

        // Remember that we need to load the appropriate end scene after a delay
        gameState = win ? GameState.Won : GameState.Lost;
        endGameFadeCanvasGroup.gameObject.SetActive(true);
        if (win)
        {
            m_SceneToLoad = winSceneName;
            m_TimeLoadEndGameScene = Time.time + endSceneLoadDelay + delayBeforeFadeToBlack;

            // play a sound on win
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = victorySound;
            audioSource.playOnAwake = false;
            audioSource.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.HUDVictory);
            audioSource.PlayScheduled(AudioSettings.dspTime + delayBeforeWinMessage);

            // create a game message
            winDisplayMessage.delayBeforeShowing = delayBeforeWinMessage;
            winDisplayMessage.gameObject.SetActive(true);
        }
        else
        {
            m_SceneToLoad = loseSceneName;
            m_TimeLoadEndGameScene = Time.time + endSceneLoadDelay + delayBeforeFadeToBlack;

            // create a game message
            loseDisplayMessage.delayBeforeShowing = delayBeforeWinMessage;
            loseDisplayMessage.gameObject.SetActive(true);
        }
    }
}
