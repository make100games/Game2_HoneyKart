using UnityEngine;
using System.Collections.Generic;

public class TerrainObjectSpawner : MonoBehaviour
{
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
    
    private List<GameObject> spawnedObjects = new List<GameObject>();
    
    public enum SpawnMode
    {
        Grid,
        Line
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
            GameObject spawnedObject = Instantiate(prefab, spawnPosition, Quaternion.identity, transform);
            spawnedObject.name = $"{prefab.name}_{spawnedObjects.Count}";
            spawnedObjects.Add(spawnedObject);
        }
        else
        {
            Debug.LogWarning($"Raycast missed terrain at position {worldPosition}");
        }
    }
    
    public void ClearObjects()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        spawnedObjects.Clear();
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
