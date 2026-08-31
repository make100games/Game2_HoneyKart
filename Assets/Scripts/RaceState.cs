using System.Collections;
using Cinemachine;
using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Race state — activates the GameManager and RaceRoot hierarchy, subscribes to the race
/// finished event, and forwards completion to GameStateManager with a one-frame defer.
/// </summary>
public class RaceState : GameStateBase
{
    [Tooltip("The opponents that the player races against. Enabled once you enter this state")]
    [SerializeField] private GameObject opponents;

    [Tooltip("Manages AI kart spawn positions; ResetToSpawn() is called on every race entry.")]
    [SerializeField] private OpponentSpawnManager opponentSpawnManager;

    [Tooltip("GameFlowManager component on the GameManager child — assigned playerKart before Start() fires.")]
    [SerializeField] private GameFlowManager gameFlowManager;

    [Tooltip("One player kart per character (Brutus=0, Elvis=1, HoneyBee=2, Squirrel=3). All must start inactive in the Editor.")]
    [SerializeField] private ArcadeKart[] playerKartOptions;

    [Tooltip("Main follow VCam at scene root — Follow and LookAt are pointed at the selected kart on entry.")]
    [SerializeField] private CinemachineVirtualCamera raceCamera;

    [Tooltip("Victory VCam inside RaceRoot — LookAt is pointed at the selected kart on entry.")]
    [SerializeField] private CinemachineVirtualCamera victoryCamera;

    [Tooltip("LapObjective on GameManager — playerTracker is assigned before the race hierarchy activates.")]
    [SerializeField] private LapObjective lapObjective;

    [Tooltip("Live race position HUD component — playerTracker is assigned alongside lapObjective on race entry.")]
    [SerializeField] private RacePositionUI racePositionUI;

    [Tooltip("Item HUD component — playerLauncher is assigned alongside racePositionUI on race entry.")]
    [SerializeField] private ItemHUD itemHUD;

    [Tooltip("Lap counter HUD component — playerTracker is assigned alongside racePositionUI on race entry.")]
    [SerializeField] private LapCounterUI lapCounterUI;

    [Header("Boost Meter HUD")]
    [Tooltip("Existing top HUD reveal group. The boost meter is created beneath it so it shares the fly-through reveal.")]
    [SerializeField] private RectTransform topHudGroup;

    [Tooltip("Coin feedback sprite shown when the player collects a coin.")]
    [SerializeField] private Sprite boostCoinSprite;

    [Tooltip("Bomb feedback sprite shown when the player is hit by an explosion.")]
    [SerializeField] private Sprite boostBombSprite;

    [Tooltip("Boost text sprite shown when a full meter fires.")]
    [SerializeField] private Sprite boostTextSprite;

    [Tooltip("Speed-line effect on Main Camera — bound to the selected kart's KartBoost on race entry, unbound on race exit. Falls back to Camera.main's component when unset.")]
    [SerializeField] private PlayerSpeedEffect playerSpeedEffect;

    [Tooltip("The spawn slot of the human player")]
    [SerializeField] private Transform spawnSlot;

    [Header("Race Fly-In Music")]
    [Tooltip("Shared scene AudioSource used for all music cues.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("Non-looping fly-in music cue played when the race state activates.")]
    [SerializeField] private AudioClip raceFlyInMusicClip;

    [Tooltip("Seconds to wait before starting the fly-in music cue.")]
    [SerializeField] private float raceFlyInMusicDelay = 0f;

    private ArcadeKart m_SelectedKart;
    private BoostMeterUI m_BoostMeterUI;

    /// <summary>
    /// Activates the selected player kart, assigns it to GameFlowManager before Start() fires,
    /// resets AI karts to spawn, activates the race hierarchy, and subscribes to the race finished event.
    /// </summary>
    public override void Enter()
    {
        opponents.SetActive(true);
        if (gameFlowManager == null)
        {
            Debug.LogWarning("[RaceState] gameFlowManager reference is null — player kart will not be assigned.");
        }

        if (playerKartOptions == null || playerKartOptions.Length == 0)
        {
            Debug.LogWarning("[RaceState] playerKartOptions is empty — no player kart will be activated.");
        }
        else
        {
            int selectedIndex = PlayerCharacterSelection.SelectedIndex;
            for (int i = 0; i < playerKartOptions.Length; i++)
            {
                if (playerKartOptions[i] != null)
                    playerKartOptions[i].gameObject.SetActive(i == selectedIndex);
            }

            if (gameFlowManager != null && selectedIndex < playerKartOptions.Length && playerKartOptions[selectedIndex] != null)
            {
                gameFlowManager.autoFindKarts = false;
                gameFlowManager.playerKart = playerKartOptions[selectedIndex];
                m_SelectedKart = playerKartOptions[selectedIndex];
                Debug.LogWarning("Selected kart: " + m_SelectedKart.gameObject.name);

                Transform selectedTransform = m_SelectedKart.transform;

                if (raceCamera != null)
                {
                    raceCamera.Follow = selectedTransform;
                    raceCamera.LookAt = selectedTransform;
                }

                if (victoryCamera != null)
                    victoryCamera.LookAt = selectedTransform;

                if (lapObjective != null)
                    lapObjective.playerTracker = playerKartOptions[selectedIndex].GetComponent<LapTracker>();

                if (racePositionUI != null)
                    racePositionUI.playerTracker = playerKartOptions[selectedIndex].GetComponent<LapTracker>();

                if (lapCounterUI != null)
                    lapCounterUI.playerTracker = playerKartOptions[selectedIndex].GetComponent<LapTracker>();

                if (itemHUD != null)
                    itemHUD.playerLauncher = playerKartOptions[selectedIndex].GetComponent<BombLauncher>();

                InitializeBoostMeterUI(playerKartOptions[selectedIndex]);
            }
        }
        opponentSpawnManager.PositionOpponentsAtSpawnSlots(m_SelectedKart.gameObject.name);

        // Place human player near starting line on the spawn slot
        var playerBr = m_SelectedKart.gameObject.GetComponent<Rigidbody>();
        playerBr.linearVelocity = Vector3.zero;
        playerBr.angularVelocity = Vector3.zero;
        playerBr.constraints = RigidbodyConstraints.FreezeAll;

        // Set position and rotation directly on the Rigidbody rather than toggling isKinematic.
        // WheelColliders require a non-kinematic Rigidbody; toggling isKinematic puts them in an
        // undefined state and can cause suspension forces to fight the teleport on the next step.
        playerBr.position = spawnSlot.position;
        playerBr.rotation = spawnSlot.rotation;
        //m_SelectedKart.gameObject.transform.position = spawnSlot.position;
        //m_SelectedKart.gameObject.transform.rotation = spawnSlot.rotation;

        gameObject.SetActive(true);

        if (m_SelectedKart != null)
            InitializeBoostMeterUI(m_SelectedKart);

        PlayRaceFlyInMusic();

        if (RaceManager.Instance != null)
            RaceManager.Instance.RegisterAllActiveRacers();

        GameFlowManager.OnRaceFinished += HandleRaceFinished;
    }

    /// <summary>
    /// Unsubscribes from the race finished event and deactivates the entire race hierarchy.
    /// </summary>
    public override void Exit()
    {
        GameFlowManager.OnRaceFinished -= HandleRaceFinished;

        // Main Camera lives outside the race hierarchy this deactivates, so it must be
        // unbound explicitly or the speed lines could keep running into the results screen.
        playerSpeedEffect?.Unbind();

        gameObject.SetActive(false);
    }

    private void HandleRaceFinished(ArcadeKart kart)
    {
        // Unsubscribe immediately to prevent double-fire.
        GameFlowManager.OnRaceFinished -= HandleRaceFinished;
        StartCoroutine(TransitionToResultsRoutine(kart));
    }

    /// <summary>Creates the boost meter beneath TopHUDGroup once and binds it to the selected player kart.</summary>
    private void InitializeBoostMeterUI(ArcadeKart selectedKart)
    {
        InitializePlayerSpeedEffect(selectedKart);

        if (topHudGroup == null && itemHUD != null)
            topHudGroup = itemHUD.transform.parent as RectTransform;

        if (topHudGroup == null)
        {
            GameObject topHudObject = GameObject.Find("TopHUDGroup");
            if (topHudObject != null)
                topHudGroup = topHudObject.GetComponent<RectTransform>();
        }

        if (topHudGroup == null)
        {
            Debug.LogWarning("[RaceState] TopHUDGroup could not be found — boost meter UI cannot be created.", this);
            return;
        }

        if (boostCoinSprite == null)
            boostCoinSprite = Resources.Load<Sprite>("Textures/UI/Coin-UI-icon");
        if (boostBombSprite == null)
            boostBombSprite = Resources.Load<Sprite>("Textures/UI/BombItemUI");
        if (boostTextSprite == null)
            boostTextSprite = Resources.Load<Sprite>("Textures/UI/Boost-Text");

        if (m_BoostMeterUI == null)
            m_BoostMeterUI = BoostMeterUI.Create(topHudGroup, boostCoinSprite, boostBombSprite, boostTextSprite);

        m_BoostMeterUI.Bind(
            selectedKart.GetComponent<BoostMeter>(),
            selectedKart.GetComponent<CoinCollector>(),
            selectedKart.GetComponent<KartCombatHandler>(),
            selectedKart.GetComponent<KartBoost>());
    }

    /// <summary>
    /// Binds the camera's speed-line effect to the selected kart's KartBoost. Called from the
    /// same two sites as InitializeBoostMeterUI, so Bind()'s unsubscribe-first guard keeps this
    /// idempotent despite the double call.
    /// </summary>
    private void InitializePlayerSpeedEffect(ArcadeKart selectedKart)
    {
        if (playerSpeedEffect == null)
            playerSpeedEffect = Camera.main != null ? Camera.main.GetComponent<PlayerSpeedEffect>() : null;

        if (playerSpeedEffect == null)
        {
            Debug.LogWarning("[RaceState] playerSpeedEffect could not be resolved — speed-line effect will not play.", this);
            return;
        }

        playerSpeedEffect.Bind(selectedKart.GetComponent<KartBoost>());
    }

    /// <summary>
    /// Defers CompleteRace by one frame so the OnRaceFinished event dispatch chain on
    /// GameFlowManager finishes before its GameObject is deactivated.
    /// </summary>
    private IEnumerator TransitionToResultsRoutine(ArcadeKart kart)
    {
        yield return null;
        GameStateManager.Instance.CompleteRace(kart);
    }

    /// <summary>
    /// Hard-stops any pending/current cue on the shared source, then schedules the non-looping
    /// fly-in music from sample zero after the configured delay.
    /// </summary>
    private void PlayRaceFlyInMusic()
    {
        if (musicSource == null || raceFlyInMusicClip == null)
        {
            Debug.LogWarning("[RaceState] musicSource or raceFlyInMusicClip is unassigned — skipping fly-in music.", this);
            return;
        }

        float delay = Mathf.Max(0f, raceFlyInMusicDelay);

        musicSource.Stop();
        musicSource.clip = raceFlyInMusicClip;
        musicSource.loop = false;
        musicSource.time = 0f;

        if (delay > 0f)
            musicSource.PlayDelayed(delay);
        else
            musicSource.Play();
    }
}
