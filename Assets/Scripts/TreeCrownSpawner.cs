using UnityEngine;

public class TreeCrownSpawner : MonoBehaviour
{
    [Header("Prefab Settings")]
    [Tooltip("Prefab to spawn (if null, creates primitive spheres)")]
    public GameObject prefab;
    
    [Header("Spawn Settings")]
    [Tooltip("Number of balls to spawn")]
    public int ballCount = 10;
    
    [Tooltip("Minimum radius for spawned balls")]
    public float minRadius = 0.1f;
    
    [Tooltip("Maximum radius for spawned balls")]
    public float maxRadius = 0.5f;
    
    [Tooltip("Distance from origin to spawn balls")]
    public float spawnDistance = 2f;

    public void SpawnBalls()
    {
        for (int i = 0; i < ballCount; i++)
        {
            Vector3 randomPosition = GetRandomPositionOnSphere();
            float randomRadius = Random.Range(minRadius, maxRadius);
            
            CreateBall(randomPosition, randomRadius);
        }
    }

    private Vector3 GetRandomPositionOnSphere()
    {
        Vector3 randomDirection = Random.onUnitSphere;
        Vector3 worldPosition = transform.position + randomDirection * spawnDistance;
        
        return worldPosition;
    }

    private void CreateBall(Vector3 position, float radius)
    {
        GameObject ball;
        
        if (prefab != null)
        {
            ball = Instantiate(prefab, position, Quaternion.identity, transform);
            ball.name = $"{prefab.name}_{ball.transform.GetSiblingIndex()}";
        }
        else
        {
            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.position = position;
            ball.transform.parent = transform;
            ball.name = $"Ball_{ball.transform.GetSiblingIndex()}";
        }
        
        ball.transform.localScale = Vector3.one * radius * 2f;
    }
}
