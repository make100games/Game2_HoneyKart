using KartGame.KartSystems;
using UnityEngine;

/// <summary>
/// Records each AI kart's spawn transform on Awake and restores it on demand.
/// Attach to the Opponents parent GameObject so it captures positions before attract-mode driving begins.
/// </summary>
public class OpponentSpawnManager : MonoBehaviour
{
    private struct SpawnData
    {
        public ArcadeKart kart;
        public Vector3 position;
        public Quaternion rotation;
    }

    private SpawnData[] m_SpawnData;

    void Awake()
    {
        ArcadeKart[] karts = GetComponentsInChildren<ArcadeKart>();
        m_SpawnData = new SpawnData[karts.Length];
        for (int i = 0; i < karts.Length; i++)
        {
            m_SpawnData[i] = new SpawnData
            {
                kart     = karts[i],
                position = karts[i].transform.position,
                rotation = karts[i].transform.rotation
            };
        }
    }

    /// <summary>
    /// Teleports all AI karts back to their recorded spawn transforms, zeroes their Rigidbody velocity,
    /// and disables movement so GameFlowManager's countdown can gate the race start cleanly.
    /// </summary>
    public void ResetToSpawn()
    {
        foreach (SpawnData data in m_SpawnData)
        {
            if (data.kart == null) continue;

            Rigidbody rb = data.kart.Rigidbody;
            if (rb == null) continue;

            // Zero velocities first so the physics solver starts clean at the new position.
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Set position and rotation directly on the Rigidbody rather than toggling isKinematic.
            // WheelColliders require a non-kinematic Rigidbody; toggling isKinematic puts them in an
            // undefined state and can cause suspension forces to fight the teleport on the next step.
            rb.position = data.position;
            rb.rotation = data.rotation;

            // Freeze driving — GameFlowManager.Start() will unfreeze after its countdown.
            data.kart.SetCanMove(false);
        }
    }
}
