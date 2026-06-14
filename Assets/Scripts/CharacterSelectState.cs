using Cinemachine;
using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Character selection screen state. Raises the VCam priority on entry, detects clicks
/// on the 3D character karts via Physics.Raycast, stores the selection in
/// PlayerCharacterSelection, then transitions to the race via GameStateManager.EnterRace().
/// </summary>
public class CharacterSelectState : GameStateBase
{
    public Camera mainCamera;
    [SerializeField] private CinemachineVirtualCamera characterSelectVCam;
    [SerializeField] private ArcadeKart[] selectableCharacters;

    private const int CharacterSelectVCamPriority = 50;

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

        PlayerCharacterSelection.SelectedIndex = index;
        GameStateManager.Instance.EnterRace();
    }

    /// <summary>Activates this state and raises the VCam priority via OnEnable.</summary>
    public override void Enter()
    {
        gameObject.SetActive(true);
    }

    /// <summary>Stops all coroutines and deactivates this state and all its children.</summary>
    public override void Exit()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }
}
