using KartGame.AI;
using UnityEngine;

/// <summary>
/// Attached to an AI-controlled kart. Listens for CoinCollector.CoinCollected and forwards it to
/// KartAgent.NotifyCoinCollected(), keeping the coin-collection reward hook entirely in game code —
/// KartGame.AI never references CoinCollector.
/// </summary>
public class AgentCoinRewardBridge : MonoBehaviour
{
    [SerializeField] private CoinCollector coinCollector;
    [SerializeField] private KartAgent kartAgent;

    [Tooltip("Log a message whenever a coin collection is forwarded to the agent.")]
    [SerializeField] private bool verboseLogging;

    private bool isSubscribed;

    private void Awake()
    {
        if (coinCollector == null)
            coinCollector = GetComponent<CoinCollector>();

        if (kartAgent == null)
            kartAgent = GetComponent<KartAgent>();

        if (coinCollector == null)
        {
            Debug.LogWarning($"AgentCoinRewardBridge: no CoinCollector found on {gameObject.name} — disabling.", this);
            enabled = false;
            return;
        }

        // A human-driven kart has a CoinCollector but no KartAgent — that combination is
        // expected, so disable silently without warning.
        if (kartAgent == null)
        {
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        if (isSubscribed || coinCollector == null)
            return;

        coinCollector.CoinCollected += HandleCoinCollected;
        isSubscribed = true;
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || coinCollector == null)
            return;

        coinCollector.CoinCollected -= HandleCoinCollected;
        isSubscribed = false;
    }

    private void HandleCoinCollected()
    {
        kartAgent.NotifyCoinCollected();

        if (verboseLogging)
            Debug.Log($"AgentCoinRewardBridge: forwarded coin collection to {kartAgent.name}.", this);
    }
}
