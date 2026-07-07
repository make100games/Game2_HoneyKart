using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TerrainObjectSpawner : MonoBehaviour
{
    private const float DefaultRespawnDelaySeconds = 3f;

    [Header("Prefab Settings")]
    [Tooltip("Prefab to spawn on the terrain")]
    public GameObject prefab;
    
    [Header("Spawn Area")]
    [Tooltip("Width of the spawn rectangle (X-axis in local space)")]
    public float width = 10f;
    
    [Tooltip("Height of the spawn rectangle (Z-axis in local space)")]
    public float height = 10f;
    
    [Header("Spawn Configuration")]
    [Tooltip("Spawn mode: Grid for 2D table layout, Line for 1D row")]
    public SpawnMode spawnMode = SpawnMode.Grid;
    
    [Tooltip("Number of objects along X-axis")]
    public int countX = 5;
    
    [Tooltip("Number of objects along Z-axis (only used in Grid mode)")]
    public int countZ = 5;
    
    [Header("Terrain Settings")]
    [Tooltip("Minimum distance above the terrain surface")]
    public float minDistanceFromSurface = 0.1f;
    
    [Tooltip("Maximum raycast distance")]
    public float raycastDistance = 100f;
    
    [Tooltip("Layer mask for raycasting (select terrain layers)")]
    public LayerMask terrainLayer = -1;
    
    [Header("Respawn Settings")]
    [Tooltip("Delay in seconds before a collected object respawns at its slot")]
    [SerializeField] private float respawnDelay = DefaultRespawnDelaySeconds;
    
    private readonly List<SpawnSlot> slots = new();
    private bool hasAdoptedChildren;
    
    public enum SpawnMode
    {
        Grid,
        Line
    }
    
    /// <summary>
    /// Tracks a single spawn location so a collected object can be respawned
    /// at its recorded transform without re-raycasting the terrain.
    /// </summary>
    private class SpawnSlot
    {
        public Vector3 worldPosition;
        public Quaternion rotation;
        public GameObject currentInstance;
        public bool respawnPending;
    }
    
    public void SpawnObjects()
    {
        if (prefab == null)
        {
            Debug.LogWarning("No prefab assigned to TerrainObjectSpawner!");
            return;
        }
        
        ClearObjects();
        
        if (spawnMode == SpawnMode.Grid)
        {
            SpawnGrid();
        }
        else
        {
            SpawnLine();
        }
    }
    
    private void SpawnGrid()
    {
        for (int x = 0; x < countX; x++)
        {
            for (int z = 0; z < countZ; z++)
            {
                Vector3 localPosition = CalculateLocalGridPosition(x, z);
                SpawnAtPosition(localPosition);
            }
        }
    }
    
    private void SpawnLine()
    {
        for (int x = 0; x < countX; x++)
        {
            Vector3 localPosition = CalculateLocalLinePosition(x);
            SpawnAtPosition(localPosition);
        }
    }
    
    private Vector3 CalculateLocalGridPosition(int x, int z)
    {
        float normalizedX = countX > 1 ? (float)x / (countX - 1) : 0.5f;
        float normalizedZ = countZ > 1 ? (float)z / (countZ - 1) : 0.5f;
        
        float localX = (normalizedX - 0.5f) * width;
        float localZ = (normalizedZ - 0.5f) * height;
        
        return new Vector3(localX, 0f, localZ);
    }
    
    private Vector3 CalculateLocalLinePosition(int x)
    {
        float normalizedX = countX > 1 ? (float)x / (countX - 1) : 0.5f;
        float localX = (normalizedX - 0.5f) * width;
        
        return new Vector3(localX, 0f, 0f);
    }
    
    private void SpawnAtPosition(Vector3 localPosition)
    {
        Vector3 worldPosition = transform.TransformPoint(localPosition);
        Vector3 rayOrigin = worldPosition;
        Vector3 rayDirection = Vector3.down;
        
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, raycastDistance, terrainLayer))
        {
            Vector3 spawnPosition = hit.point + Vector3.up * minDistanceFromSurface;
            int slotIndex = slots.Count;
            
            SpawnSlot slot = new SpawnSlot
            {
                worldPosition = spawnPosition,
                rotation = Quaternion.identity
            };
            
            GameObject spawnedObject = Instantiate(prefab, spawnPosition, slot.rotation, transform);
            spawnedObject.name = $"{prefab.name}_{slotIndex}";
            slot.currentInstance = spawnedObject;
            slots.Add(slot);
            
            AttachRespawnable(spawnedObject, slotIndex);
        }
        else
        {
            Debug.LogWarning($"Raycast missed terrain at position {worldPosition}");
        }
    }
    
    public void ClearObjects()
    {
        foreach (SpawnSlot slot in slots)
        {
            if (slot.currentInstance != null)
            {
                DestroyImmediate(slot.currentInstance);
            }
        }
        slots.Clear();
    }
    
    /// <summary>
    /// Ensures the given instance carries a <see cref="RespawnableItem"/> wired
    /// back to this spawner and its slot.
    /// </summary>
    private void AttachRespawnable(GameObject instance, int slotIndex)
    {
        RespawnableItem respawnable = instance.GetComponent<RespawnableItem>();
        if (respawnable == null)
        {
            respawnable = instance.AddComponent<RespawnableItem>();
        }
        respawnable.Initialize(this, slotIndex);
    }
    
    private void Awake()
    {
        AdoptExistingChildren();
    }
    
    /// <summary>
    /// Runtime-only: rebuilds respawn slots from editor-placed children so
    /// objects spawned at edit time participate in respawning. Because the slot
    /// list is not serialized it is empty at Play start, triggering adoption.
    /// </summary>
    private void AdoptExistingChildren()
    {
        if (hasAdoptedChildren || slots.Count > 0 || transform.childCount == 0)
            return;
        
        hasAdoptedChildren = true;
        
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            SpawnSlot slot = new SpawnSlot
            {
                worldPosition = child.position,
                rotation = child.rotation,
                currentInstance = child.gameObject
            };
            slots.Add(slot);
            AttachRespawnable(child.gameObject, i);
        }
    }
    
    /// <summary>
    /// Called (via <see cref="RespawnableItem.NotifyCollected"/>) when the item
    /// in the given slot is collected. Schedules a respawn after the configured
    /// delay. Ignored if the slot is out of range or already awaiting respawn.
    /// </summary>
    /// <param name="slotIndex">Index of the collected slot.</param>
    public void HandleItemCollected(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return;
        
        SpawnSlot slot = slots[slotIndex];
        if (slot.respawnPending)
            return;
        
        slot.respawnPending = true;
        slot.currentInstance = null;
        StartCoroutine(RespawnAfterDelay(slotIndex));
    }
    
    private IEnumerator RespawnAfterDelay(int slotIndex)
    {
        yield return new WaitForSeconds(respawnDelay);
        
        if (prefab == null)
            yield break;
        
        SpawnSlot slot = slots[slotIndex];
        GameObject instance = Instantiate(prefab, slot.worldPosition, slot.rotation, transform);
        instance.name = $"{prefab.name}_{slotIndex}";
        slot.currentInstance = instance;
        slot.respawnPending = false;
        
        AttachRespawnable(instance, slotIndex);
    }
    
    private void OnValidate()
    {
        respawnDelay = Mathf.Max(0f, respawnDelay);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        
        Vector3 center = transform.position;
        Vector3 halfExtents = new Vector3(width * 0.5f, 0.1f, height * 0.5f);
        
        Vector3 topLeft = transform.TransformPoint(new Vector3(-width * 0.5f, 0f, height * 0.5f));
        Vector3 topRight = transform.TransformPoint(new Vector3(width * 0.5f, 0f, height * 0.5f));
        Vector3 bottomLeft = transform.TransformPoint(new Vector3(-width * 0.5f, 0f, -height * 0.5f));
        Vector3 bottomRight = transform.TransformPoint(new Vector3(width * 0.5f, 0f, -height * 0.5f));
        
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
        
        Gizmos.color = Color.green;
        if (spawnMode == SpawnMode.Grid)
        {
            for (int x = 0; x < countX; x++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    Vector3 localPos = CalculateLocalGridPosition(x, z);
                    Vector3 worldPos = transform.TransformPoint(localPos);
                    Gizmos.DrawWireSphere(worldPos, 0.2f);
                    Gizmos.DrawRay(worldPos, Vector3.down * 5f);
                }
            }
        }
        else
        {
            for (int x = 0; x < countX; x++)
            {
                Vector3 localPos = CalculateLocalLinePosition(x);
                Vector3 worldPos = transform.TransformPoint(localPos);
                Gizmos.DrawWireSphere(worldPos, 0.2f);
                Gizmos.DrawRay(worldPos, Vector3.down * 5f);
            }
        }
    }
}
