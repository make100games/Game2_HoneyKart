using System;
using KartGame.KartSystems;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KartGame.AI
{
    /// <summary>
    /// Sensors hold information such as the position of rotation of the origin of the raycast and its hit threshold
    /// to consider a "crash".
    /// </summary>
    [System.Serializable]
    public struct Sensor
    {
        public Transform Transform;
        public float RayDistance;
        public float HitValidationDistance;
    }

    /// <summary>
    /// We only want certain behaviours when the agent runs.
    /// Training would allow certain functions such as OnAgentReset() be called and execute, while Inferencing will
    /// assume that the agent will continuously run and not reset.
    /// </summary>
    public enum AgentMode
    {
        Training,
        Inferencing
    }

    /// <summary>
    /// The KartAgent will drive the inputs for the KartController.
    /// </summary>
    public class KartAgent : Agent, IInput
    {
#region Training Modes
        [Tooltip("Are we training the agent or is the agent production ready?")]
        public AgentMode Mode = AgentMode.Training;
        [Tooltip("What is the initial checkpoint the agent will go to? This value is only for inferencing.")]
        public ushort InitCheckpointIndex;

#endregion

#region Senses
        [Header("Observation Params")]
        [Tooltip("What objects should the raycasts hit and detect?")]
        public LayerMask Mask;
        [Tooltip("Sensors contain ray information to sense out the world, you can have as many sensors as you need.")]
        public Sensor[] Sensors;
        [Header("Checkpoints"), Tooltip("What are the series of checkpoints for the agent to seek and pass through?")]
        public Collider[] Colliders;
        [Tooltip("What layer are the checkpoints on? This should be an exclusive layer for the agent to use.")]
        public LayerMask CheckpointMask;

        [Space]
        [Tooltip("Would the agent need a custom transform to be able to raycast and hit the track? " +
            "If not assigned, then the root transform will be used.")]
        public Transform AgentSensorTransform;
#endregion

#region Rewards
        [Header("Rewards"), Tooltip("What penatly is given when the agent crashes?")]
        public float HitPenalty = -1f;
        [Tooltip("How much reward is given when the agent successfully passes the checkpoints?")]
        public float PassCheckpointReward;
        [Tooltip("Should typically be a small value, but we reward the agent for moving in the right direction.")]
        public float TowardsCheckpointReward;
        [Tooltip("Typically if the agent moves faster, we want to reward it for finishing the track quickly.")]
        public float SpeedReward;
        [Tooltip("Reward the agent when it keeps accelerating")]
        public float AccelerationReward;
        [Tooltip("Penalty subtracted when a checkpoint is crossed in the backward direction.")]
        public float WrongWayPenalty = 0.5f;
        [Tooltip("When false (default), wall contact applies a per-step penalty instead of ending the episode.")]
        public bool EndEpisodeOnHit = false;
        [Tooltip("Per-step penalty scale while a sensor reads below its HitValidationDistance.")]
        public float ContactPenaltyPerStep = 0.05f;
        [Tooltip("When true, SpeedReward is multiplied by the sign of the checkpoint direction dot, so driving fast the wrong way no longer earns reward.")]
        public bool DirectionGateSpeedReward = true;
        #endregion

        #region ResetParams
        [Header("Inference Reset Params")]
        [Tooltip("What is the unique mask that the agent should detect when it falls out of the track?")]
        public LayerMask OutOfBoundsMask;
        [Tooltip("What are the layers we want to detect for the track and the ground?")]
        public LayerMask TrackMask;
        [Tooltip("How far should the ray be when casted? For larger karts - this value should be larger too.")]
        public float GroundCastDistance;
        [Tooltip("Largest forward checkpoint skip accepted as valid progress; larger jumps are treated as a backward/wrong-way crossing. Matches LapTracker.MaxForwardCheckpointSkip.")]
        public int MaxForwardCheckpointSkip = 12;
#endregion

#region Debugging
        [Header("Debug Option")] [Tooltip("Should we visualize the rays that the agent draws?")]
        public bool ShowRaycasts;
#endregion

#region Weapon
        [Header("Weapon")]
        [Tooltip("When false (default), the agent fires via a heuristic and the existing 2-branch model stays valid.")]
        public bool UseMLFiring = false;
        public float FireTargetRange = 25f;
        [Range(0f, 180f)] public float FireTargetHalfAngle = 30f;
        public float FireCooldownSeconds = 2f;
        [Tooltip("Layers considered other karts for heuristic targeting.")]
        public LayerMask KartMask;

        [Header("Weapon Rewards (ML firing only)")]
        public float FireWithBombReward = 0.5f;
        public float FireWithoutBombPenalty = 0.1f;
#endregion

#region Coins
        [Header("Coin Seeking")]
        [Tooltip("When false (default), coin observations and the towards-coin reward are disabled, keeping the existing 12-observation model valid until retrain.")]
        public bool UseCoinSeeking = false;
        [Tooltip("Layers treated as collectible coins.")]
        public LayerMask CoinMask;
        [Tooltip("Radius of the OverlapSphere used to detect nearby coins.")]
        public float CoinDetectionRange = 25f;
        [Tooltip("Coins outside this forward half-angle (degrees) are ignored entirely.")]
        [Range(0f, 180f)] public float CoinSeekHalfAngle = 70f;
        [Tooltip("Terminal reward granted per coin actually collected. Deliberately less than PassCheckpointReward.")]
        public float CoinCollectReward = 0.3f;
        [Tooltip("Small per-step shaping reward for moving towards a detected coin. Deliberately much smaller than TowardsCheckpointReward.")]
        public float TowardsCoinReward = 0.005f;
        [Tooltip("Minimum dot product between the coin direction and the next-checkpoint direction before any coin shaping reward is granted. Guards against detours off the racing line.")]
        public float CoinCheckpointAlignmentThreshold = 0.6f;
#endregion

        /// <summary>Raised when the agent wants to fire; BombLauncher subscribes.</summary>
        public event Action FireRequested;

        /// <summary>Called by the kart's BombLauncher to keep the agent informed of its current bomb count.</summary>
        public void SetAvailableBombs(int count) => m_AvailableBombs = Mathf.Max(0, count);

        /// <summary>Called by the kart's coin pickup logic to reward the agent for collecting a coin.
        /// The terminal reward only applies when the kart is currently travelling towards the next
        /// checkpoint, so drifting off the racing line to grab a coin while going backward is not
        /// rewarded. The coin count is always incremented, regardless of direction, for diagnostics.</summary>
        public void NotifyCoinCollected()
        {
            m_CoinsCollectedThisEpisode++;

            if (Vector3.Dot(m_Kart.Rigidbody.linearVelocity.normalized, DirectionToNextCheckpoint) > 0f)
                AddReward(CoinCollectReward);
        }

        /// <summary>Number of coins collected by this agent during the current episode. Useful for training diagnostics.</summary>
        public int CoinsCollectedThisEpisode => m_CoinsCollectedThisEpisode;

        /// <summary>Normalised world direction from the kart to the next checkpoint. Zero if the collider is missing.</summary>
        public Vector3 DirectionToNextCheckpoint
        {
            get
            {
                if (Colliders == null || Colliders.Length == 0) return Vector3.zero;
                var next = (m_CheckpointIndex + 1) % Colliders.Length;
                var nextCollider = Colliders[next];
                if (nextCollider == null) return Vector3.zero;
                return (nextCollider.transform.position - m_Kart.transform.position).normalized;
            }
        }

        /// <summary>Index of the checkpoint the agent is currently targeting as "passed".</summary>
        public int CurrentCheckpointIndex => m_CheckpointIndex;

        /// <summary>True when any sensor read below its HitValidationDistance on the most recent CollectObservations call.</summary>
        public bool IsInContact => m_IsInContact;

        /// <summary>The last InputData the policy produced, cached before any override is applied. Lets an
        /// external component read the agent's intent without re-entering GenerateInput.</summary>
        public InputData LastPolicyInput { get; private set; }

        /// <summary>Pushes an input override that GenerateInput will return instead of the policy's own input.
        /// Used by external recovery behaviours; KartAgent never references the pushing component.</summary>
        public void SetInputOverride(InputData input)
        {
            m_OverrideInput = input;
            m_HasInputOverride = true;
        }

        /// <summary>Clears a previously pushed input override so GenerateInput resumes returning policy input.</summary>
        public void ClearInputOverride()
        {
            m_HasInputOverride = false;
        }

        /// <summary>True while an external input override is active.</summary>
        public bool HasInputOverride => m_HasInputOverride;

        ArcadeKart m_Kart;
        bool m_Acceleration;
        bool m_Brake;
        float m_Steering;
        int m_CheckpointIndex;

        bool m_EndEpisode;
        float m_LastAccumulatedReward;
        bool m_IsInContact;

        InputData m_OverrideInput;
        bool m_HasInputOverride;

        bool m_LoggedMissingColliders;

        int m_AvailableBombs;
        float m_LastFireTime = -999f;

        bool m_HasCoinTarget;
        Vector3 m_CoinDirection;
        float m_CoinDistance;
        int m_CoinsCollectedThisEpisode;

        DecisionRequester m_DecisionRequester;

        void Awake()
        {
            m_Kart = GetComponent<ArcadeKart>();
            m_DecisionRequester = GetComponent<DecisionRequester>();
            if (AgentSensorTransform == null) AgentSensorTransform = transform;
        }

        void Start()
        {
            // If the agent is training, then at the start of the simulation, pick a random checkpoint to train the agent.
            OnEpisodeBegin();

            if (Mode == AgentMode.Inferencing) m_CheckpointIndex = InitCheckpointIndex;

            // Pause the ML decision loop until the race countdown finishes and the kart is allowed to move.
            if (m_DecisionRequester != null) m_DecisionRequester.enabled = false;
        }

        void Update()
        {
            // Resume the ML decision loop the moment the countdown ends.
            if (m_DecisionRequester != null && !m_DecisionRequester.enabled && m_Kart.CanMove)
                m_DecisionRequester.enabled = true;

            if (m_EndEpisode)
            {
                m_EndEpisode = false;
                AddReward(m_LastAccumulatedReward);
                EndEpisode();
                OnEpisodeBegin();
            }

            if (!UseMLFiring && m_AvailableBombs > 0 && HasTargetInFront())
                RequestFire();
        }

        void LateUpdate()
        {
            switch (Mode)
            {
                case AgentMode.Inferencing:
                    if (ShowRaycasts) 
                        Debug.DrawRay(transform.position, Vector3.down * GroundCastDistance, Color.cyan);

                    // We want to place the agent back on the track if the agent happens to launch itself outside of the track.
                    // Cast against the combined mask so the ray can hit either surface, then test the actual
                    // hit layer against OutOfBoundsMask — TrackMask and OutOfBoundsMask are disjoint, so
                    // filtering the cast to TrackMask alone (as before) could never satisfy the OutOfBoundsMask test.
                    if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var hit, GroundCastDistance, TrackMask | OutOfBoundsMask)
                        && ((1 << hit.collider.gameObject.layer) & OutOfBoundsMask) > 0)
                    {
                        RepositionToCurrentCheckpoint();
                    }

                    break;
            }
        }

        /// <summary>
        /// Teleports the kart back onto its current checkpoint and clears its motion state.
        /// Extracted so both the inference out-of-bounds recovery and an external recovery
        /// component's failsafe can reuse the exact same reset behaviour.
        /// </summary>
        public void RepositionToCurrentCheckpoint()
        {
            if (Colliders == null || m_CheckpointIndex < 0 || m_CheckpointIndex >= Colliders.Length || Colliders[m_CheckpointIndex] == null)
                return;

            var checkpoint = Colliders[m_CheckpointIndex].transform;
            transform.localRotation = checkpoint.rotation;
            transform.position = checkpoint.position;
            m_Kart.Rigidbody.linearVelocity = default;
            m_Steering = 0f;
            m_Acceleration = m_Brake = false;
        }

        void OnTriggerEnter(Collider other)
        {
            var maskedValue = 1 << other.gameObject.layer;
            var triggered = maskedValue & CheckpointMask;
            if (triggered == 0) return;

            FindCheckpointIndex(other, out var index);
            if (index < 0) return;

            int count = Colliders.Length;
            if (count == 0) return;

            // Ring-modulo forward-progress acceptance, mirroring LapTracker.HandleCheckpointCrossed:
            // a checkpoint crossing is accepted as forward progress when it is within
            // MaxForwardCheckpointSkip steps ahead on the ring. This self-heals a missed checkpoint
            // trigger anywhere on the ring, including the 24 -> 0 wrap, instead of leaving
            // m_CheckpointIndex stuck behind the kart.
            int forwardSkip = ((index - m_CheckpointIndex) % count + count) % count;

            if (forwardSkip == 0)
            {
                // Duplicate re-trigger (e.g. the recovery teleport re-touching its own checkpoint) — ignore silently.
                return;
            }

            if (forwardSkip <= MaxForwardCheckpointSkip)
            {
                AddReward(PassCheckpointReward);
                m_CheckpointIndex = index;
            }
            else
            {
                AddReward(-WrongWayPenalty);
            }
        }

        void FindCheckpointIndex(Collider checkPoint, out int index)
        {
            for (int i = 0; i < Colliders.Length; i++)
            {
                if (Colliders[i].GetInstanceID() == checkPoint.GetInstanceID())
                {
                    index = i;
                    return;
                }
            }
            index = -1;
        }

        /// <summary>
        /// Snaps m_CheckpointIndex to the checkpoint the kart is currently sitting at, so a grid spawn
        /// can never target a checkpoint behind it. Call after teleporting the kart to a spawn slot,
        /// since Start() otherwise assigns m_CheckpointIndex = InitCheckpointIndex before positioning happens.
        /// </summary>
        public void SyncCheckpointIndexToPosition()
        {
            if (Colliders == null || Colliders.Length == 0)
            {
                if (!m_LoggedMissingColliders)
                {
                    Debug.LogWarning("[KartAgent] SyncCheckpointIndexToPosition: Colliders is empty — cannot resolve nearest checkpoint.", this);
                    m_LoggedMissingColliders = true;
                }
                return;
            }

            int closestIndex = -1;
            float closestSqrDistance = float.MaxValue;
            for (int i = 0; i < Colliders.Length; i++)
            {
                if (Colliders[i] == null) continue;
                float sqrDistance = (Colliders[i].transform.position - m_Kart.transform.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestIndex = i;
                }
            }

            if (closestIndex < 0)
            {
                if (!m_LoggedMissingColliders)
                {
                    Debug.LogWarning("[KartAgent] SyncCheckpointIndexToPosition: every Colliders entry is null — cannot resolve nearest checkpoint.", this);
                    m_LoggedMissingColliders = true;
                }
                return;
            }

            int count = Colliders.Length;
            Vector3 toClosest = (Colliders[closestIndex].transform.position - m_Kart.transform.position).normalized;
            if (Vector3.Dot(toClosest, m_Kart.transform.forward) > 0f)
            {
                // The nearest checkpoint is still ahead — step back one so "next" resolves to it.
                closestIndex = ((closestIndex - 1) % count + count) % count;
            }

            m_CheckpointIndex = closestIndex;
        }

        float Sign(float value)
        {
            if (value > 0)
            {
                return 1;
            } 
            if (value < 0)
            {
                return -1;
            }
            return 0;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation(m_Kart.LocalSpeed());

            // Add an observation for direction of the agent to the next checkpoint.
            var next = (m_CheckpointIndex + 1) % Colliders.Length;
            var nextCollider = Colliders[next];

            // The next checkpoint collider should always be assigned, but guard against a
            // missing reference by adding a neutral observation instead of returning early —
            // the observation vector size must stay constant regardless of this state.
            if (nextCollider == null)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
            else
            {
                var direction = (nextCollider.transform.position - m_Kart.transform.position).normalized;
                sensor.AddObservation(Vector3.Dot(m_Kart.Rigidbody.linearVelocity.normalized, direction));

                // Heading observations: a stationary kart has a zero velocity-dot observation regardless
                // of which way it's facing, so these give the agent a direction signal even at a standstill
                // (e.g. wedged against a fence).
                sensor.AddObservation(Vector3.Dot(m_Kart.transform.forward, direction));
                sensor.AddObservation(Vector3.Dot(m_Kart.transform.right, direction));

                if (ShowRaycasts)
                    Debug.DrawLine(AgentSensorTransform.position, nextCollider.transform.position, Color.magenta);
            }

            m_LastAccumulatedReward = 0.0f;
            m_EndEpisode = false;
            m_IsInContact = false;
            for (var i = 0; i < Sensors.Length; i++)
            {
                var current = Sensors[i];
                var xform = current.Transform;
                var hit = Physics.Raycast(AgentSensorTransform.position, xform.forward, out var hitInfo,
                    current.RayDistance, Mask, QueryTriggerInteraction.Ignore);

                if (ShowRaycasts)
                {
                    Debug.DrawRay(AgentSensorTransform.position, xform.forward * current.RayDistance, Color.green);
                    Debug.DrawRay(AgentSensorTransform.position, xform.forward * current.HitValidationDistance, 
                        Color.red);

                    if (hit && hitInfo.distance < current.HitValidationDistance)
                    {
                        Debug.DrawRay(hitInfo.point, Vector3.up * 3.0f, Color.blue);
                    }
                }

                if (hit)
                {
                    if (hitInfo.distance < current.HitValidationDistance)
                    {
                        m_IsInContact = true;

                        if (EndEpisodeOnHit)
                        {
                            m_LastAccumulatedReward += HitPenalty;
                            m_EndEpisode = true;
                        }
                        else
                        {
                            AddReward(-ContactPenaltyPerStep * (1f - hitInfo.distance / current.HitValidationDistance));
                        }
                    }
                }

                sensor.AddObservation(hit ? hitInfo.distance : current.RayDistance);
            }

            sensor.AddObservation(m_Acceleration);

            if (UseMLFiring)
                sensor.AddObservation(m_AvailableBombs > 0 ? 1f : 0f);

            if (UseCoinSeeking)
            {
                m_HasCoinTarget = TryFindCoinTarget(out m_CoinDirection, out m_CoinDistance);
                sensor.AddObservation(m_HasCoinTarget ? 1f : 0f);
                sensor.AddObservation(m_CoinDistance / CoinDetectionRange);
                sensor.AddObservation(m_HasCoinTarget ? Vector3.Dot(transform.forward, m_CoinDirection) : 0f);
                sensor.AddObservation(m_HasCoinTarget ? Vector3.Dot(transform.right, m_CoinDirection) : 0f);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            base.OnActionReceived(actions);
            InterpretDiscreteActions(actions);

            // Find the next checkpoint when registering the current checkpoint that the agent has passed.
            var next = (m_CheckpointIndex + 1) % Colliders.Length;
            var nextCollider = Colliders[next];
            if (nextCollider == null) return;

            var direction = (nextCollider.transform.position - m_Kart.transform.position).normalized;
            var reward = Vector3.Dot(m_Kart.Rigidbody.linearVelocity.normalized, direction);

            if (ShowRaycasts) Debug.DrawRay(AgentSensorTransform.position, m_Kart.Rigidbody.linearVelocity, Color.blue);

            // Add rewards if the agent is heading in the right direction
            AddReward(reward * TowardsCheckpointReward);
            AddReward((m_Acceleration && !m_Brake ? 1.0f : 0.0f) * AccelerationReward);

            // Direction-gate the speed reward so driving fast obliquely the wrong way no longer nets
            // positive reward (Mathf.Sign(0f) == 0f, which already zeroes the term for a stationary kart).
            float speedTerm = m_Kart.LocalSpeed() * SpeedReward;
            if (DirectionGateSpeedReward) speedTerm *= Mathf.Sign(reward);
            AddReward(speedTerm);

            if (UseMLFiring && actions.DiscreteActions.Length > 2)
            {
                bool fire = actions.DiscreteActions[2] >= 1;
                if (fire)
                {
                    if (m_AvailableBombs > 0)
                    {
                        AddReward(FireWithBombReward);
                        RequestFire();
                    }
                    else
                    {
                        AddReward(-FireWithoutBombPenalty);
                    }
                }
            }

            if (UseCoinSeeking && m_HasCoinTarget)
            {
                var toCheckpoint = (nextCollider.transform.position - m_Kart.transform.position).normalized;
                if (Vector3.Dot(m_CoinDirection, toCheckpoint) >= CoinCheckpointAlignmentThreshold)
                    AddReward(Vector3.Dot(m_Kart.Rigidbody.linearVelocity.normalized, m_CoinDirection) * TowardsCoinReward);
            }
        }

        public override void OnEpisodeBegin()
        {
            m_CoinsCollectedThisEpisode = 0;
            m_HasCoinTarget = false;

            switch (Mode)
            {
                case AgentMode.Training:
                    m_CheckpointIndex = Random.Range(0, Colliders.Length);
                    var collider = Colliders[m_CheckpointIndex];
                    transform.localRotation = collider.transform.rotation;
                    transform.position = collider.transform.position;
                    m_Kart.Rigidbody.linearVelocity = default;
                    m_Acceleration = false;
                    m_Brake = false;
                    m_Steering = 0f;
                    break;
                default:
                    break;
            }
        }

        void InterpretDiscreteActions(ActionBuffers actions)
        {
            m_Steering = actions.DiscreteActions[0] - 1f;
            m_Acceleration = actions.DiscreteActions[1] >= 1.0f;
            m_Brake = actions.DiscreteActions[1] < 1.0f;
        }

        void RequestFire()
        {
            if (Time.time - m_LastFireTime < FireCooldownSeconds)
                return;

            m_LastFireTime = Time.time;
            FireRequested?.Invoke();
        }

        bool HasTargetInFront()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, FireTargetRange, KartMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                ArcadeKart otherKart = hit.GetComponentInParent<ArcadeKart>();
                if (otherKart == null || otherKart == m_Kart)
                    continue;

                Vector3 direction = (otherKart.transform.position - transform.position).normalized;
                if (Vector3.Angle(transform.forward, direction) <= FireTargetHalfAngle)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Finds the closest coin within CoinDetectionRange and within the forward CoinSeekHalfAngle cone,
        /// using only layer and transform position — the agent never learns what a coin type is.
        /// </summary>
        bool TryFindCoinTarget(out Vector3 direction, out float distance)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, CoinDetectionRange, CoinMask, QueryTriggerInteraction.Collide);

            bool found = false;
            float closestSqrDistance = float.MaxValue;
            Vector3 closestDirection = Vector3.zero;

            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;

                Vector3 to = hit.transform.position - transform.position;
                Vector3 toNormalized = to.normalized;

                if (Vector3.Angle(transform.forward, toNormalized) > CoinSeekHalfAngle)
                    continue;

                float sqrDistance = to.sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestDirection = toNormalized;
                    found = true;
                }
            }

            if (found)
            {
                direction = closestDirection;
                distance = Mathf.Sqrt(closestSqrDistance);

                if (ShowRaycasts)
                    Debug.DrawLine(transform.position, transform.position + direction * distance, Color.yellow);

                return true;
            }

            direction = Vector3.zero;
            distance = CoinDetectionRange;
            return false;
        }

        public InputData GenerateInput()
        {
            // Report neutral input while the countdown is active so the kart cannot twitch or creep forward.
            // Also drop any stale override so it cannot leak across the countdown freeze.
            if (m_Kart == null || !m_Kart.CanMove)
            {
                m_HasInputOverride = false;
                return new InputData { Accelerate = false, Brake = false, TurnInput = 0f };
            }

            var policyInput = new InputData
            {
                Accelerate = m_Acceleration,
                Brake = m_Brake,
                TurnInput = m_Steering
            };
            LastPolicyInput = policyInput;

            return m_HasInputOverride ? m_OverrideInput : policyInput;
        }
    }
}
