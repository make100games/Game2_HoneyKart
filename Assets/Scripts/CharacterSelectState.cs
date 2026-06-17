using System.Collections;
using Cinemachine;
using KartGame.KartSystems;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Character selection screen state. Raises the VCam priority on entry, detects clicks
/// on the 3D character karts via Physics.Raycast, plays a SelectedEffect particle, updates
/// the character name sprite, and slides in the bottom UI panels on first selection.
/// The race is started by pressing StartGameButton, which calls GameStateManager.EnterRace().
/// </summary>
public class CharacterSelectState : GameStateBase
{
    public Camera mainCamera;
    [SerializeField] private CinemachineVirtualCamera characterSelectVCam;
    [SerializeField] private ArcadeKart[] selectableCharacters;

    [Header("UI Panels")]
    [SerializeField] private RectTransform bottomPanel;
    [SerializeField] private RectTransform characterNameElement;
    [SerializeField] private RectTransform startGameButtonRect;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Image characterNameImage;

    [Header("Character Name Sprites")]
    [SerializeField] private Sprite[] characterNameSprites;

    [Header("Selected Effects")]
    [SerializeField] private ParticleSystem[] selectedEffects;

    private const int CharacterSelectVCamPriority = 50;
    private const float SlideInDuration = 0.4f;
    private const float SlideInOffscreenOffset = 300f;

    private Vector2 bottomPanelTargetPos;
    private Vector2 characterNameTargetPos;
    private Vector2 startGameButtonTargetPos;
    private bool hasSlideInOccurred;
    private int previousSelectedIndex = -1;

    void Awake()
    {
        bottomPanelTargetPos = bottomPanel.anchoredPosition;
        characterNameTargetPos = characterNameElement.anchoredPosition;
        startGameButtonTargetPos = startGameButtonRect.anchoredPosition;

        startGameButton.onClick.AddListener(OnStartGamePressed);
    }

    void OnEnable()
    {
        if (characterSelectVCam != null)
            characterSelectVCam.Priority = CharacterSelectVCamPriority;
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        ArcadeKart kart = hit.collider.GetComponentInParent<ArcadeKart>();
        if (kart == null)
            return;

        int index = System.Array.IndexOf(selectableCharacters, kart);
        if (index < 0)
            return;

        if (previousSelectedIndex >= 0)
            selectedEffects[previousSelectedIndex].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        PlayerCharacterSelection.SelectedIndex = index;
        selectedEffects[index].Play();
        characterNameImage.sprite = characterNameSprites[index];

        if (!hasSlideInOccurred)
        {
            hasSlideInOccurred = true;
            StartCoroutine(SlideInPanels());
        }

        previousSelectedIndex = index;
    }

    /// <summary>Activates this state, moves UI panels offscreen, and resets selection state.</summary>
    public override void Enter()
    {
        gameObject.SetActive(true);

        bottomPanel.anchoredPosition = bottomPanelTargetPos + Vector2.down * SlideInOffscreenOffset;
        characterNameElement.anchoredPosition = characterNameTargetPos + Vector2.down * SlideInOffscreenOffset;
        startGameButtonRect.anchoredPosition = startGameButtonTargetPos + Vector2.down * SlideInOffscreenOffset;

        hasSlideInOccurred = false;
        previousSelectedIndex = -1;

        foreach (ParticleSystem ps in selectedEffects)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>Stops all coroutines and deactivates this state and all its children.</summary>
    public override void Exit()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    /// <summary>Slides all three UI elements from their offscreen positions to their target positions.</summary>
    private IEnumerator SlideInPanels()
    {
        Vector2 bottomStart = bottomPanel.anchoredPosition;
        Vector2 nameStart = characterNameElement.anchoredPosition;
        Vector2 buttonStart = startGameButtonRect.anchoredPosition;

        float elapsed = 0f;
        while (elapsed < SlideInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / SlideInDuration);

            bottomPanel.anchoredPosition = Vector2.Lerp(bottomStart, bottomPanelTargetPos, t);
            characterNameElement.anchoredPosition = Vector2.Lerp(nameStart, characterNameTargetPos, t);
            startGameButtonRect.anchoredPosition = Vector2.Lerp(buttonStart, startGameButtonTargetPos, t);

            yield return null;
        }

        bottomPanel.anchoredPosition = bottomPanelTargetPos;
        characterNameElement.anchoredPosition = characterNameTargetPos;
        startGameButtonRect.anchoredPosition = startGameButtonTargetPos;
    }

    /// <summary>Called by StartGameButton's onClick to transition to the race state.</summary>
    private void OnStartGamePressed()
    {
        GameStateManager.Instance.EnterRace();
    }
}
