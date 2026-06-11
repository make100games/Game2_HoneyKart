using UnityEngine;

/// <summary>
/// Abstract base class for all game states. Lives on a dedicated root GameObject
/// that starts inactive; GameStateManager calls Enter/Exit to drive transitions.
/// </summary>
public abstract class GameStateBase : MonoBehaviour
{
    /// <summary>
    /// Called by GameStateManager when this state becomes active.
    /// Implementations must call gameObject.SetActive(true) first so that
    /// coroutines and OnEnable fire before any further setup.
    /// </summary>
    public abstract void Enter();

    /// <summary>
    /// Called by GameStateManager before the next state enters.
    /// Implementations must call gameObject.SetActive(false) at the end to
    /// cascade deactivation to all child GameObjects.
    /// </summary>
    public abstract void Exit();
}
