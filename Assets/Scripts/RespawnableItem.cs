using UnityEngine;

/// <summary>
/// Lightweight component attached to every object spawned by
/// <see cref="TerrainObjectSpawner"/>. Holds a back-reference to the owning
/// spawner and the slot index this instance occupies, and exposes the hook the
/// collecting mechanic calls when the item is picked up.
/// </summary>
public class RespawnableItem : MonoBehaviour
{
    private TerrainObjectSpawner owner;
    private int slotIndex;
    private bool hasBeenCollected;

    /// <summary>
    /// Wires this item to its spawner and slot. Called by the spawner right
    /// after the instance is created or an editor-placed child is adopted.
    /// </summary>
    /// <param name="owner">The spawner that manages this item's slot.</param>
    /// <param name="slotIndex">Index of the slot this instance occupies.</param>
    public void Initialize(TerrainObjectSpawner owner, int slotIndex)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
        hasBeenCollected = false;
    }

    /// <summary>
    /// Notifies the owning spawner that this item has been collected so the
    /// slot can be respawned after the configured delay. Safe to call only
    /// once; subsequent calls are ignored.
    /// </summary>
    public void NotifyCollected()
    {
        if (hasBeenCollected || owner == null)
            return;

        hasBeenCollected = true;
        owner.HandleItemCollected(slotIndex);
    }
}
