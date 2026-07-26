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

    [SerializeField]
    private Transform[] spawnSlots;
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
    /// Positions opponents at the spawn slots. Only positions the opponents that don't match
    /// the character the player chose so that we don't have duplicate characters.
    /// </summary>
    /// <param name="playerCharacterName"></param>
    public void PositionOpponentsAtSpawnSlots(string playerCharacterName)
    {
        Debug.LogWarning("PlayerCharacterName: " + playerCharacterName);
        var spawnIndex = 0;
        ArcadeKart[] karts = GetComponentsInChildren<ArcadeKart>();
        for (int i = 0; i < karts.Length; i++)
        {
            var kart = karts[i];
            Debug.LogWarning("Opponetn name: " + kart.gameObject.name);
            if (kart.gameObject.name == (playerCharacterName + "_Agent"))
            {
                Debug.Log("Disable opponent because player selected this character as their own");
                kart.gameObject.SetActive(false);
                continue;
            }
            var spawnSlot = spawnSlots[spawnIndex];

            // Move opponent to spawn slot and disable all AI inputs and physics to avoid wiggling
            Rigidbody rb = kart.Rigidbody;
            if (rb == null) continue;

            // Zero velocities first so the physics solver starts clean at the new position.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;

            // Set position and rotation directly on the Rigidbody rather than toggling isKinematic.
            // WheelColliders require a non-kinematic Rigidbody; toggling isKinematic puts them in an
            // undefined state and can cause suspension forces to fight the teleport on the next step.
            rb.position = spawnSlot.position;
            rb.rotation = spawnSlot.rotation;

            // Freeze driving — GameFlowManager.Start() will unfreeze after its countdown.
            kart.SetCanMove(false);

            spawnIndex++;
        }
    }
}
