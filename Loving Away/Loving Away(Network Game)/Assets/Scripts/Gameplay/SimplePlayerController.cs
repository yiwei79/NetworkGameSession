using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Client-side player controller
/// Collects local input, sends to server via GameNetworkManager
/// Renders all players based on received server state
/// Uses new Input System for better control and future gamepad support
/// </summary>
public class SimplePlayerController : MonoBehaviour
{
    [Header("References")]
    public GameNetworkManager networkManager;
    public GameObject playerPrefab;
    public GameObject projectilePrefab; // Optional: if null, creates dynamically
    public VisualEffectsManager visualEffectsManager; // Session 4.5: Visual effects
    
    [Header("Local Player Settings")]
    public uint localPlayerId = 0;
    public Color localPlayerColor = Color.green;
    
    [Header("Remote Player Settings")]
    public Color remotePlayerColor = Color.red;

    [Header("Second Local Player (Testing)")]
    public bool enableSecondLocalPlayer = false;
    public uint secondLocalPlayerId = 1;
    public Color secondLocalPlayerColor = Color.blue;

    [Header("Debug UI")]
    public bool showDebugUI = true;
    
    // Player GameObjects (visual representation)
    private Dictionary<uint, GameObject> playerObjects;
    private Dictionary<uint, ShootVisualFeedback> playerVisualFeedback;
    private Dictionary<uint, PlayerVisualController> playerVisualControllers; // Session 5A: Visual dressing
    private Dictionary<uint, PlayerHealthBar> playerHealthBars; // Phase 3: Health bars

    // Projectile GameObjects
    private Dictionary<uint, GameObject> projectileObjects;

    // Phase 5.5: Cooldown tracking (last shot time per player)
    private Dictionary<uint, float> lastProjectileSpawnTime;
    
    // Input state (Player 1: WASD + Space)
    private Vector2 currentInput;
    private bool shootButtonPressed;

    // Second player input state (Player 2: Arrow Keys + Right Shift)
    private Vector2 secondPlayerInput;
    private bool secondPlayerShootPressed;

    // Phase 2: Charge mechanic state (Player 1)
    private bool wasShootingLastFrame = false;
    private float chargeStartTime = 0f;
    private float currentChargeValue = 0f;  // 0.0-1.0
    private const float maxChargeTime = 2f; // 2 seconds to full charge
    private bool shootSignalPending = false; // Shoot signal waiting to be sent
    private float pendingChargeValue = 0f;   // Charge value captured when shoot triggered

    // Phase 2: Charge mechanic state (Player 2)
    private bool wasSecondPlayerShootingLastFrame = false;
    private float secondPlayerChargeStartTime = 0f;
    private float secondPlayerChargeValue = 0f;
    private bool secondPlayerShootSignalPending = false; // Shoot signal waiting to be sent
    private float secondPlayerPendingChargeValue = 0f;   // Charge value captured when shoot triggered

    // Input rate limiting (FIX 1: Prevent input over-queuing)
    [Header("Network Settings")]
    public float inputSendRate = 30f; // Hz - how many times per second to send input
    private float lastInputSendTime = 0f;

    // Client-side prediction (FIX 2: Instant local response)
    [Header("Prediction Settings")]
    public bool enablePrediction = true;
    public float predictionBlendSpeed = 10f; // How fast to reconcile with server

    // Movement parameters (must match server - ServerGameState.cs)
    // Phase 5.6: Heavier "Animal Party" feel
    private float moveSpeed = 3.5f;     // Was 5.0f - 30% slower, more deliberate
    private float acceleration = 25.0f; // Was 50.0f - 50% slower, more momentum
    private float arenaRadius = 15f;

    // Local player prediction state
    private Vector3 predictedVelocity = Vector3.zero;
    private Vector3 predictedPosition = Vector3.zero;
    private bool hasInitializedPrediction = false;
    private bool isLocalPlayerAlive = true; // Phase 5.6: Track alive state

    // Second local player prediction state
    private Vector3 secondPredictedVelocity = Vector3.zero;
    private Vector3 secondPredictedPosition = Vector3.zero;
    private bool hasInitializedSecondPrediction = false;
    private bool isSecondPlayerAlive = true; // Phase 5.6: Track alive state

    // Network stats
    private int packetsSent;
    private int packetsReceived;
    private uint currentSequence; // FIX 3: Current input sequence number
    private float lastServerTime;
    private float lastStateUpdateTime;
    private float connectionTimeout = 5f;

    // Lab 8: Input history and ACK tracking
    private InputHistoryBuffer localInputHistory = new InputHistoryBuffer();
    private InputHistoryBuffer secondInputHistory = new InputHistoryBuffer();
    private uint lastAckedSequenceP1 = 0;
    private uint lastAckedSequenceP2 = 0;
    private float retransmissionTimeout = 0.1f; // 100ms (2 server ticks @ 20Hz)
    private float lastRetransmitCheckTime = 0f; // Rate limit retransmission checks
    private float retransmitCheckInterval = 0.05f; // Only check every 50ms (not every frame!)

    // Lab 9: Snapshot buffer for interpolation
    private SnapshotBuffer snapshotBuffer = new SnapshotBuffer();
    private float interpolationDelay = 0.1f; // 100ms - render remote players in the past for smooth interpolation
    
    void Start()
    {
        playerObjects = new Dictionary<uint, GameObject>();
        playerVisualFeedback = new Dictionary<uint, ShootVisualFeedback>();
        playerVisualControllers = new Dictionary<uint, PlayerVisualController>(); // Session 5A: Visual dressing
        playerHealthBars = new Dictionary<uint, PlayerHealthBar>(); // Phase 3: Health bars
        projectileObjects = new Dictionary<uint, GameObject>();
        lastProjectileSpawnTime = new Dictionary<uint, float>(); // Phase 5.5: Cooldown tracking

        // Find network manager if not assigned
        if (networkManager == null)
        {
            // Use FindFirstObjectByType instead of deprecated FindObjectOfType (Unity 2023.1+)
            networkManager = FindFirstObjectByType<GameNetworkManager>();
            if (networkManager == null)
            {
                UnityEngine.Debug.LogError("[SimplePlayerController] No GameNetworkManager found!");
                return;
            }
        }

        // Set local player ID from network manager
        localPlayerId = networkManager.localPlayerId;

        // Sync second local player settings from network manager
        enableSecondLocalPlayer = networkManager.enableSecondLocalPlayer;
        secondLocalPlayerId = networkManager.secondLocalPlayerId;

        // Subscribe to network events
        networkManager.OnStateUpdate += HandleStateUpdate;
        networkManager.OnProjectileSpawn += HandleProjectileSpawn;
        networkManager.OnProjectileHit += HandleProjectileHit;
        networkManager.OnPlayerDeath += HandlePlayerDeath;
        networkManager.OnPlayerRespawn += HandlePlayerRespawn;

        // Session 4.5: Auto-find VisualEffectsManager if not assigned
        if (visualEffectsManager == null)
        {
            visualEffectsManager = FindFirstObjectByType<VisualEffectsManager>();
            if (visualEffectsManager == null)
            {
                UnityEngine.Debug.LogWarning("[SimplePlayerController] No VisualEffectsManager found - visual effects disabled");
            }
        }

        lastStateUpdateTime = Time.time;

        UnityEngine.Debug.Log($"[SimplePlayerController] Initialized for player {localPlayerId}");
        if (enableSecondLocalPlayer)
        {
            UnityEngine.Debug.Log($"[SimplePlayerController] Second local player enabled: {secondLocalPlayerId}");
        }
    }
    
    void Update()
    {
        CollectInput();
        SendInputToServer();

        // Lab 8: Check for inputs needing retransmission
        CheckRetransmissions();

        // FIX 2: Predict local player movement immediately
        if (enablePrediction)
        {
            PredictLocalPlayerMovement();

            // Also predict second local player if enabled
            if (enableSecondLocalPlayer)
            {
                PredictSecondPlayerMovement();
            }
        }

        UpdateVisualFeedback();
        CheckConnectionTimeout();
    }
    
    #region Input Collection
    
    void CollectInput()
    {
        // NEW INPUT SYSTEM - Uses Keyboard.current for better control
        var keyboard = Keyboard.current;

        if (keyboard != null)
        {
            // ===== PLAYER 1: WASD + Space =====
            float horizontal1 = 0f;
            float vertical1 = 0f;

            if (keyboard.wKey.isPressed) vertical1 += 1f;
            if (keyboard.sKey.isPressed) vertical1 -= 1f;
            if (keyboard.aKey.isPressed) horizontal1 -= 1f;
            if (keyboard.dKey.isPressed) horizontal1 += 1f;

            currentInput = new Vector2(horizontal1, vertical1);

            // Normalize to prevent faster diagonal movement
            if (currentInput.magnitude > 1f)
            {
                currentInput.Normalize();
            }

            // Phase 2: Charge detection (Spacebar)
            bool shootNow = keyboard.spaceKey.isPressed;

            if (shootNow && !wasShootingLastFrame)
            {
                // Just pressed - start charging
                chargeStartTime = Time.time;
                currentChargeValue = 0f;
            }
            else if (shootNow && wasShootingLastFrame)
            {
                // Still holding - update charge
                float chargeTime = Time.time - chargeStartTime;
                currentChargeValue = Mathf.Clamp01(chargeTime / maxChargeTime);
            }
            else if (!shootNow && wasShootingLastFrame)
            {
                // Just released - finalize charge (this is when we shoot)
                float chargeTime = Time.time - chargeStartTime;
                currentChargeValue = Mathf.Clamp01(chargeTime / maxChargeTime);
            }
            else
            {
                // Not pressing - reset charge
                currentChargeValue = 0f;
            }

            // Phase 2: Only trigger shoot on button RELEASE, not while holding
            // Require minimum 0.05s charge to avoid accidental taps
            bool justReleased = !shootNow && wasShootingLastFrame;
            bool hasMinimumCharge = (Time.time - chargeStartTime) > 0.05f;

            // Set pending flag when shoot is triggered AND capture charge value
            if (justReleased && hasMinimumCharge)
            {
                shootSignalPending = true;
                pendingChargeValue = currentChargeValue; // Capture charge value NOW before it's reset
                UnityEngine.Debug.Log($"[Client] Shoot triggered! Charge: {pendingChargeValue:F2}");
            }

            // shootButtonPressed is true if we have a pending signal
            shootButtonPressed = shootSignalPending;

            wasShootingLastFrame = shootNow;

            // ===== PLAYER 2: Arrow Keys + Right Shift =====
            if (enableSecondLocalPlayer)
            {
                float horizontal2 = 0f;
                float vertical2 = 0f;

                if (keyboard.upArrowKey.isPressed) vertical2 += 1f;
                if (keyboard.downArrowKey.isPressed) vertical2 -= 1f;
                if (keyboard.leftArrowKey.isPressed) horizontal2 -= 1f;
                if (keyboard.rightArrowKey.isPressed) horizontal2 += 1f;

                secondPlayerInput = new Vector2(horizontal2, vertical2);

                // Normalize to prevent faster diagonal movement
                if (secondPlayerInput.magnitude > 1f)
                {
                    secondPlayerInput.Normalize();
                }

                // Phase 2: Charge detection (Right Shift)
                bool secondShootNow = keyboard.rightShiftKey.isPressed;

                if (secondShootNow && !wasSecondPlayerShootingLastFrame)
                {
                    // Just pressed - start charging
                    secondPlayerChargeStartTime = Time.time;
                    secondPlayerChargeValue = 0f;
                }
                else if (secondShootNow && wasSecondPlayerShootingLastFrame)
                {
                    // Still holding - update charge
                    float chargeTime = Time.time - secondPlayerChargeStartTime;
                    secondPlayerChargeValue = Mathf.Clamp01(chargeTime / maxChargeTime);
                }
                else if (!secondShootNow && wasSecondPlayerShootingLastFrame)
                {
                    // Just released - finalize charge (this is when we shoot)
                    float chargeTime = Time.time - secondPlayerChargeStartTime;
                    secondPlayerChargeValue = Mathf.Clamp01(chargeTime / maxChargeTime);
                }
                else
                {
                    // Not pressing - reset charge
                    secondPlayerChargeValue = 0f;
                }

                // Phase 2: Only trigger shoot on button RELEASE, not while holding
                // Require minimum 0.05s charge to avoid accidental taps
                bool secondJustReleased = !secondShootNow && wasSecondPlayerShootingLastFrame;
                bool secondHasMinimumCharge = (Time.time - secondPlayerChargeStartTime) > 0.05f;

                // Set pending flag when shoot is triggered AND capture charge value
                if (secondJustReleased && secondHasMinimumCharge)
                {
                    secondPlayerShootSignalPending = true;
                    secondPlayerPendingChargeValue = secondPlayerChargeValue; // Capture charge value NOW before it's reset
                }

                // secondPlayerShootPressed is true if we have a pending signal
                secondPlayerShootPressed = secondPlayerShootSignalPending;

                wasSecondPlayerShootingLastFrame = secondShootNow;
            }
        }
        else
        {
            // No keyboard detected - clear input
            currentInput = Vector2.zero;
            shootButtonPressed = false;
            secondPlayerInput = Vector2.zero;
            secondPlayerShootPressed = false;
        }
    }
    
    void SendInputToServer()
    {
        // FIX 1: Rate-limit input sending to prevent over-queuing
        // Only send input at the configured rate (default 30 Hz)
        float timeSinceLastSend = Time.time - lastInputSendTime;
        float sendInterval = 1f / inputSendRate;

        if (timeSinceLastSend >= sendInterval)
        {
            // Phase 2: Send Player 1 input with PENDING charge value (captured at button release)
            networkManager.SendInput(currentInput, shootButtonPressed, pendingChargeValue);

            // Lab 8: Store sent input in history for retransmission
            uint sentSequence = networkManager.GetLastSequenceNumber();
            ClientInputMessage sentInput = new ClientInputMessage(
                localPlayerId,
                sentSequence,
                currentInput,
                shootButtonPressed,
                pendingChargeValue
            );
            localInputHistory.AddInput(sentInput, Time.time);

            // Clear Player 1 shoot signal after sending
            if (shootSignalPending)
            {
                UnityEngine.Debug.Log($"[Client] Shoot signal SENT to server (charge: {pendingChargeValue:F2})");
                shootSignalPending = false;
                pendingChargeValue = 0f; // Reset pending charge after sending
            }

            // Phase 2: Send Player 2 input with PENDING charge value (captured at button release)
            if (enableSecondLocalPlayer)
            {
                networkManager.SendInputForPlayer(secondLocalPlayerId, secondPlayerInput, secondPlayerShootPressed, secondPlayerPendingChargeValue);

                // Lab 8: Store Player 2 input in history
                uint sentSequenceP2 = networkManager.GetLastSequenceNumber();
                ClientInputMessage sentInputP2 = new ClientInputMessage(
                    secondLocalPlayerId,
                    sentSequenceP2,
                    secondPlayerInput,
                    secondPlayerShootPressed,
                    secondPlayerPendingChargeValue
                );
                secondInputHistory.AddInput(sentInputP2, Time.time);

                // Clear Player 2 shoot signal after sending
                if (secondPlayerShootSignalPending)
                {
                    secondPlayerShootSignalPending = false;
                    secondPlayerPendingChargeValue = 0f; // Reset pending charge after sending
                }
            }

            lastInputSendTime = Time.time;
        }
    }

    /// <summary>
    /// Lab 8: Checks for inputs that need retransmission due to missing ACKs
    /// Called every frame but rate-limited to prevent spam
    /// FIX: Now marks inputs as retransmitted to prevent spam
    /// </summary>
    void CheckRetransmissions()
    {
        // FIX: Rate limit retransmission checks (don't check every frame!)
        if (Time.time - lastRetransmitCheckTime < retransmitCheckInterval)
        {
            return; // Too soon, skip this frame
        }
        lastRetransmitCheckTime = Time.time;

        // Player 1 retransmissions
        var toRetransmitP1 = localInputHistory.GetInputsForRetransmit(
            lastAckedSequenceP1,
            Time.time,
            retransmissionTimeout
        );

        if (toRetransmitP1.Count > 0)
        {
            UnityEngine.Debug.Log($"[Retransmit] P1: {toRetransmitP1.Count} inputs need retransmit (lastAck={lastAckedSequenceP1}, currentSeq={networkManager.GetLastSequenceNumber()})");
        }

        foreach (var (input, oldSendTime) in toRetransmitP1)
        {
            UnityEngine.Debug.Log($"  [Retransmit P1] Seq {input.sequenceNumber} (sent {Time.time - oldSendTime:F3}s ago)");
            networkManager.ResendInput(input);
            localInputHistory.MarkAsRetransmitted(input.sequenceNumber, Time.time);
        }

        // Player 2 retransmissions (if enabled)
        if (enableSecondLocalPlayer)
        {
            var toRetransmitP2 = secondInputHistory.GetInputsForRetransmit(
                lastAckedSequenceP2,
                Time.time,
                retransmissionTimeout
            );

            if (toRetransmitP2.Count > 0)
            {
                UnityEngine.Debug.Log($"[Retransmit] P2: {toRetransmitP2.Count} inputs need retransmit (lastAck={lastAckedSequenceP2})");
            }

            foreach (var (input, oldSendTime) in toRetransmitP2)
            {
                UnityEngine.Debug.Log($"  [Retransmit P2] Seq {input.sequenceNumber} (sent {Time.time - oldSendTime:F3}s ago)");
                networkManager.ResendInput(input);
                secondInputHistory.MarkAsRetransmitted(input.sequenceNumber, Time.time);
            }
        }
    }

    void PredictLocalPlayerMovement()
    {
        // FIX 2: Client-side prediction for local player
        // This method applies the same movement logic as ServerGameState.UpdateState()
        // to give instant visual feedback for the local player

        // Phase 5.6: Don't predict if player is dead
        if (!isLocalPlayerAlive)
        {
            return;
        }

        // Check if local player object exists
        if (!playerObjects.ContainsKey(localPlayerId))
        {
            return; // Local player not spawned yet
        }

        GameObject localPlayerObj = playerObjects[localPlayerId];
        if (localPlayerObj == null)
        {
            return;
        }

        // Initialize prediction from current server position (first frame only)
        if (!hasInitializedPrediction)
        {
            predictedPosition = localPlayerObj.transform.position;
            predictedVelocity = Vector3.zero;
            hasInitializedPrediction = true;
        }

        // Apply the same movement logic as server (ServerGameState.cs lines 129-154)
        float deltaTime = Time.deltaTime;

        if (currentInput.magnitude > 0.1f)
        {
            // Player is providing input - accelerate towards target velocity
            Vector2 normalizedInput = currentInput.normalized;
            Vector3 inputDir3D = new Vector3(normalizedInput.x, 0, normalizedInput.y);
            Vector3 targetVelocity = inputDir3D * moveSpeed;

            // Apply acceleration with deltaTime for frame-rate independence
            float accelStep = acceleration * deltaTime;
            predictedVelocity = Vector3.MoveTowards(
                predictedVelocity,
                targetVelocity,
                accelStep
            );
        }
        else
        {
            // No input - decelerate to stop
            float decelStep = acceleration * deltaTime * 0.6f;
            predictedVelocity = Vector3.MoveTowards(
                predictedVelocity,
                Vector3.zero,
                decelStep
            );
        }

        // Update predicted position based on velocity
        predictedPosition += predictedVelocity * deltaTime;

        // Phase 5.6: Removed boundary bounce - let server handle instant death
        // (Server kills players at boundary in ServerGameState.cs)

        // Apply predicted position to visual (instant response!)
        localPlayerObj.transform.position = predictedPosition;
    }

    void PredictSecondPlayerMovement()
    {
        // Client-side prediction for second local player
        // Mirror of PredictLocalPlayerMovement() but for player 2

        // Phase 5.6: Don't predict if player is dead
        if (!isSecondPlayerAlive)
        {
            return;
        }

        // Check if second player object exists
        if (!playerObjects.ContainsKey(secondLocalPlayerId))
        {
            return; // Second player not spawned yet
        }

        GameObject secondPlayerObj = playerObjects[secondLocalPlayerId];
        if (secondPlayerObj == null)
        {
            return;
        }

        // Initialize prediction from current server position (first frame only)
        if (!hasInitializedSecondPrediction)
        {
            secondPredictedPosition = secondPlayerObj.transform.position;
            secondPredictedVelocity = Vector3.zero;
            hasInitializedSecondPrediction = true;
        }

        // Apply the same movement logic as server
        float deltaTime = Time.deltaTime;

        if (secondPlayerInput.magnitude > 0.1f)
        {
            // Player is providing input - accelerate towards target velocity
            Vector2 normalizedInput = secondPlayerInput.normalized;
            Vector3 inputDir3D = new Vector3(normalizedInput.x, 0, normalizedInput.y);
            Vector3 targetVelocity = inputDir3D * moveSpeed;

            // Apply acceleration with deltaTime for frame-rate independence
            float accelStep = acceleration * deltaTime;
            secondPredictedVelocity = Vector3.MoveTowards(
                secondPredictedVelocity,
                targetVelocity,
                accelStep
            );
        }
        else
        {
            // No input - decelerate to stop
            float decelStep = acceleration * deltaTime * 0.6f;
            secondPredictedVelocity = Vector3.MoveTowards(
                secondPredictedVelocity,
                Vector3.zero,
                decelStep
            );
        }

        // Update predicted position based on velocity
        secondPredictedPosition += secondPredictedVelocity * deltaTime;

        // Phase 5.6: Removed boundary bounce - let server handle instant death
        // (Server kills players at boundary in ServerGameState.cs)

        // Apply predicted position to visual (instant response!)
        secondPlayerObj.transform.position = secondPredictedPosition;
    }

    #endregion

    #region State Update Handling
    
    void HandleStateUpdate(ServerStateUpdateMessage stateMsg)
    {
        lastServerTime = stateMsg.serverTime;
        lastStateUpdateTime = Time.time;

        // Lab 9: Store snapshot in buffer for interpolation
        snapshotBuffer.AddSnapshot(stateMsg.serverTime, stateMsg.players);

        // Lab 8: Process ACKs and prune input history
        if (stateMsg.lastProcessedSequence != null && stateMsg.lastProcessedSequence.ContainsKey(localPlayerId))
        {
            uint newAck = stateMsg.lastProcessedSequence[localPlayerId];
            if (newAck > lastAckedSequenceP1)
            {
                uint oldAck = lastAckedSequenceP1;
                lastAckedSequenceP1 = newAck;
                localInputHistory.PruneAckedInputs(newAck);
                UnityEngine.Debug.Log($"[ACK] P1: Received ACK {newAck} (was {oldAck}, pruned {newAck - oldAck} inputs)");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[ACK] P1: No ACK in state update! (dict null? {stateMsg.lastProcessedSequence == null}, contains key? {stateMsg.lastProcessedSequence?.ContainsKey(localPlayerId)})");
        }

        if (enableSecondLocalPlayer && stateMsg.lastProcessedSequence != null && stateMsg.lastProcessedSequence.ContainsKey(secondLocalPlayerId))
        {
            uint newAck = stateMsg.lastProcessedSequence[secondLocalPlayerId];
            if (newAck > lastAckedSequenceP2)
            {
                lastAckedSequenceP2 = newAck;
                secondInputHistory.PruneAckedInputs(newAck);
                UnityEngine.Debug.Log($"[ACK] P2: Received ACK {newAck}");
            }
        }

        // Update all player positions based on server snapshot
        for (int i = 0; i < stateMsg.playerCount; i++)
        {
            PlayerSnapshot snapshot = stateMsg.players[i];
            UpdatePlayerVisual(snapshot);
        }
        
        // Remove disconnected players (players not in snapshot)
        RemoveDisconnectedPlayers(stateMsg);
    }
    
    void UpdatePlayerVisual(PlayerSnapshot snapshot)
    {
        // Create player object if it doesn't exist
        if (!playerObjects.ContainsKey(snapshot.playerId))
        {
            CreatePlayerObject(snapshot.playerId);
        }

        // Update position
        GameObject playerObj = playerObjects[snapshot.playerId];
        if (playerObj != null)
        {
            // Different handling for local players vs remote players
            bool isFirstLocalPlayer = (snapshot.playerId == localPlayerId);
            bool isSecondLocalPlayer = enableSecondLocalPlayer && (snapshot.playerId == secondLocalPlayerId);

            if (isFirstLocalPlayer && enablePrediction)
            {
                // First local player: Reconcile prediction with server state
                ReconcileWithServerState(snapshot);
            }
            else if (isSecondLocalPlayer && enablePrediction)
            {
                // Second local player: Reconcile prediction with server state
                ReconcileSecondPlayerWithServerState(snapshot);
            }
            else
            {
                // Lab 9: Remote player - use interpolation for smooth 60 FPS movement
                // Render time is in the past (serverTime - interpolationDelay) to ensure smooth playback
                float renderTime = lastServerTime - interpolationDelay;
                PlayerSnapshot interpolated = snapshotBuffer.GetInterpolatedSnapshot(
                    snapshot.playerId,
                    renderTime
                );

                playerObj.transform.position = interpolated.position;

                // Smooth rotation based on interpolated velocity
                if (interpolated.velocity.magnitude > 0.1f)
                {
                    Vector3 lookDirection = interpolated.velocity.normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    playerObj.transform.rotation = Quaternion.Slerp(
                        playerObj.transform.rotation,
                        targetRotation,
                        Time.deltaTime * 10f
                    );
                }
            }

            // Local players also need rotation update (for prediction)
            if ((isFirstLocalPlayer || isSecondLocalPlayer) && snapshot.velocity.magnitude > 0.1f)
            {
                GameObject localPlayerObj = playerObjects[snapshot.playerId];
                Vector3 lookDirection = snapshot.velocity.normalized;
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                localPlayerObj.transform.rotation = Quaternion.Slerp(
                    localPlayerObj.transform.rotation,
                    targetRotation,
                    Time.deltaTime * 10f
                );
            }

            // Session 5A: Update visual controller for alive state (rotation inherited from parent)
            if (playerVisualControllers.ContainsKey(snapshot.playerId))
            {
                PlayerVisualController visualController = playerVisualControllers[snapshot.playerId];
                if (visualController != null)
                {
                    // Only update alive state - rotation is inherited from parent GameObject
                    visualController.SetAliveState(snapshot.isAlive);
                }
            }

            // Phase 3: Update health bar
            if (playerHealthBars.ContainsKey(snapshot.playerId))
            {
                PlayerHealthBar healthBar = playerHealthBars[snapshot.playerId];
                if (healthBar != null)
                {
                    healthBar.SetHealth(snapshot.health);
                }
            }
        }
    }

    void ReconcileWithServerState(PlayerSnapshot serverSnapshot)
    {
        // FIX 2: Reconciliation - blend predicted position to match server
        // This corrects any prediction errors while maintaining smooth visuals
        // Lab 9: Enhanced with ACK awareness to handle high-latency scenarios

        // Phase 5.6: Update alive state
        isLocalPlayerAlive = serverSnapshot.isAlive;

        Vector3 serverPosition = serverSnapshot.position;
        Vector3 serverVelocity = serverSnapshot.velocity;

        // Calculate position error
        float positionError = Vector3.Distance(predictedPosition, serverPosition);
        float velocityError = Vector3.Distance(predictedVelocity, serverVelocity);

        // Lab 9: Use ACK data to determine if we're "ahead" of server
        uint currentSequence = networkManager.GetLastSequenceNumber();
        uint unprocessedInputs = currentSequence - lastAckedSequenceP1;

        if (unprocessedInputs > 5)
        {
            // We're way ahead - server is behind (high latency)
            // Be more conservative with corrections to avoid rubber-banding
            float blendFactor = predictionBlendSpeed * Time.deltaTime * 0.5f;
            predictedPosition = Vector3.Lerp(predictedPosition, serverPosition, blendFactor);
            predictedVelocity = Vector3.Lerp(predictedVelocity, serverVelocity, blendFactor);

            UnityEngine.Debug.Log($"[Reconcile] High latency: {unprocessedInputs} unprocessed inputs");
        }
        else if (positionError > 2.0f)
        {
            // Large error - snap to server immediately
            predictedPosition = serverPosition;
            predictedVelocity = serverVelocity;
            UnityEngine.Debug.Log($"[Reconcile] Snap: error {positionError:F2}");
        }
        else
        {
            // Small error - blend smoothly
            float blendFactor = predictionBlendSpeed * Time.deltaTime;
            predictedPosition = Vector3.Lerp(predictedPosition, serverPosition, blendFactor);
            predictedVelocity = Vector3.Lerp(predictedVelocity, serverVelocity, blendFactor);
        }
    }

    void ReconcileSecondPlayerWithServerState(PlayerSnapshot serverSnapshot)
    {
        // Reconciliation for second local player - mirror of ReconcileWithServerState
        // Lab 9: Enhanced with ACK awareness

        // Phase 5.6: Update alive state
        isSecondPlayerAlive = serverSnapshot.isAlive;

        Vector3 serverPosition = serverSnapshot.position;
        Vector3 serverVelocity = serverSnapshot.velocity;

        // Calculate position error
        float positionError = Vector3.Distance(secondPredictedPosition, serverPosition);
        float velocityError = Vector3.Distance(secondPredictedVelocity, serverVelocity);

        // Lab 9: Use ACK data to determine if we're "ahead" of server
        uint currentSequence = networkManager.GetLastSequenceNumber();
        uint unprocessedInputs = currentSequence - lastAckedSequenceP2;

        if (unprocessedInputs > 5)
        {
            // High latency - be conservative
            float blendFactor = predictionBlendSpeed * Time.deltaTime * 0.5f;
            secondPredictedPosition = Vector3.Lerp(secondPredictedPosition, serverPosition, blendFactor);
            secondPredictedVelocity = Vector3.Lerp(secondPredictedVelocity, serverVelocity, blendFactor);

            UnityEngine.Debug.Log($"[Reconcile P2] High latency: {unprocessedInputs} unprocessed inputs");
        }
        else if (positionError > 2.0f)
        {
            // Large error - snap immediately
            secondPredictedPosition = serverPosition;
            secondPredictedVelocity = serverVelocity;
            UnityEngine.Debug.Log($"[Reconcile P2] Snap: error {positionError:F2}");
        }
        else
        {
            // Small error - blend smoothly
            float blendFactor = predictionBlendSpeed * Time.deltaTime;
            secondPredictedPosition = Vector3.Lerp(secondPredictedPosition, serverPosition, blendFactor);
            secondPredictedVelocity = Vector3.Lerp(secondPredictedVelocity, serverVelocity, blendFactor);
        }
    }

    void CreatePlayerObject(uint playerId)
    {
        if (playerPrefab == null)
        {
            UnityEngine.Debug.LogError("[SimplePlayerController] Player prefab not assigned!");
            return;
        }

        GameObject playerObj = Instantiate(playerPrefab);
        playerObj.name = $"Player_{playerId}";

        // Add to dictionary immediately to prevent duplicate creation if errors occur below
        playerObjects[playerId] = playerObj;

        // Set color based on player type (first local, second local, or remote)
        Color playerColor;
        if (playerId == localPlayerId)
        {
            playerColor = localPlayerColor;
        }
        else if (enableSecondLocalPlayer && playerId == secondLocalPlayerId)
        {
            playerColor = secondLocalPlayerColor;
        }
        else
        {
            playerColor = remotePlayerColor;
        }

        // Session 5A: Add PlayerVisualController for enhanced character visuals
        PlayerVisualController visualController = playerObj.AddComponent<PlayerVisualController>();
        visualController.SetPlayerColor(playerColor);
        playerVisualControllers[playerId] = visualController;

        // Keep legacy renderer for backward compatibility (in case prefab still has a renderer)
        Renderer renderer = playerObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = playerColor;
        }

        // Add visual feedback component
        ShootVisualFeedback feedback = playerObj.AddComponent<ShootVisualFeedback>();
        feedback.chargeColor = playerColor * 0.8f;
        feedback.SetOriginalColor(playerColor);
        playerVisualFeedback[playerId] = feedback;

        // Phase 3: Add health bar component (will be initialized when snapshot arrives)
        PlayerHealthBar healthBar = playerObj.AddComponent<PlayerHealthBar>();
        playerHealthBars[playerId] = healthBar;

        // Add name tag (TextMesh above player)
        CreateNameTag(playerObj, playerId);

        UnityEngine.Debug.Log($"[SimplePlayerController] Created visual for player {playerId}");
    }
    
    void CreateNameTag(GameObject playerObj, uint playerId)
    {
        GameObject nameTagObj = new GameObject("NameTag");
        nameTagObj.transform.SetParent(playerObj.transform);
        nameTagObj.transform.localPosition = new Vector3(0, 1.5f, 0);

        TextMesh textMesh = nameTagObj.AddComponent<TextMesh>();
        textMesh.fontSize = 20;
        textMesh.characterSize = 0.1f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;

        // Set name and color based on player type
        if (playerId == localPlayerId)
        {
            textMesh.text = "P1 (WASD)";
            textMesh.color = localPlayerColor;
        }
        else if (enableSecondLocalPlayer && playerId == secondLocalPlayerId)
        {
            textMesh.text = "P2 (Arrows)";
            textMesh.color = secondLocalPlayerColor;
        }
        else
        {
            textMesh.text = $"Player {playerId}";
            textMesh.color = remotePlayerColor;
        }
    }
    
    void RemoveDisconnectedPlayers(ServerStateUpdateMessage stateMsg)
    {
        List<uint> playerIdsToRemove = new List<uint>();
        
        // Check which players are no longer in the snapshot
        foreach (var kvp in playerObjects)
        {
            uint playerId = kvp.Key;
            bool foundInSnapshot = false;
            
            for (int i = 0; i < stateMsg.playerCount; i++)
            {
                if (stateMsg.players[i].playerId == playerId)
                {
                    foundInSnapshot = true;
                    break;
                }
            }
            
            if (!foundInSnapshot)
            {
                playerIdsToRemove.Add(playerId);
            }
        }
        
        // Remove disconnected players
        foreach (uint playerId in playerIdsToRemove)
        {
            if (playerObjects.ContainsKey(playerId))
            {
                Destroy(playerObjects[playerId]);
                playerObjects.Remove(playerId);
            }

            // Session 5A: Cleanup visual controller
            if (playerVisualControllers.ContainsKey(playerId))
            {
                playerVisualControllers.Remove(playerId);
            }

            // Cleanup visual feedback
            if (playerVisualFeedback.ContainsKey(playerId))
            {
                playerVisualFeedback.Remove(playerId);
            }

            UnityEngine.Debug.Log($"[SimplePlayerController] Removed player {playerId}");
        }
    }

    /// <summary>
    /// Phase 5.5: Calculates cooldown percent for a player (0.0 = just shot, 1.0 = ready)
    /// </summary>
    float GetCooldownPercent(uint playerId)
    {
        if (!lastProjectileSpawnTime.ContainsKey(playerId))
        {
            return 1.0f; // No shot yet, ready to shoot
        }

        float timeSinceShot = Time.time - lastProjectileSpawnTime[playerId];
        float cooldownDuration = 0.5f; // Match server cooldown
        return Mathf.Clamp01(timeSinceShot / cooldownDuration);
    }

    void HandleProjectileSpawn(ProjectileSpawnMessage spawnMsg)
    {
        // Create projectile GameObject
        GameObject projectileObj;

        if (projectilePrefab != null)
        {
            // Use assigned prefab
            projectileObj = Instantiate(projectilePrefab);
        }
        else
        {
            // Create empty GameObject for projectile
            projectileObj = new GameObject($"Projectile_{spawnMsg.projectileId}");
        }

        // Add Projectile component and initialize
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile == null)
        {
            projectile = projectileObj.AddComponent<Projectile>();
        }

        // Initialize with spawn message data
        projectile.Initialize(spawnMsg);

        // Track the projectile
        projectileObjects[spawnMsg.projectileId] = projectileObj;

        // Phase 5.5: Track spawn time for cooldown calculation
        lastProjectileSpawnTime[spawnMsg.ownerId] = Time.time;

        UnityEngine.Debug.Log($"[SimplePlayerController] Spawned projectile {spawnMsg.projectileId} from player {spawnMsg.ownerId}");
    }

    void HandleProjectileHit(ProjectileHitMessage hitMsg)
    {
        // Check if this projectile exists locally
        if (projectileObjects.ContainsKey(hitMsg.projectileId))
        {
            // Destroy the projectile GameObject
            GameObject projectileObj = projectileObjects[hitMsg.projectileId];
            Destroy(projectileObj);
            projectileObjects.Remove(hitMsg.projectileId);

            UnityEngine.Debug.Log($"[SimplePlayerController] Projectile {hitMsg.projectileId} hit player {hitMsg.targetPlayerId} at {hitMsg.hitPosition}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[SimplePlayerController] Received hit for unknown projectile {hitMsg.projectileId}");
        }

        // Check if a local player was hit
        bool isLocalPlayerHit = (hitMsg.targetPlayerId == localPlayerId);
        bool isSecondPlayerHit = (enableSecondLocalPlayer && hitMsg.targetPlayerId == secondLocalPlayerId);

        if (isLocalPlayerHit)
        {
            UnityEngine.Debug.Log($"<color=yellow>[HIT!] You (Player {localPlayerId}) were hit by projectile {hitMsg.projectileId}!</color>");
        }
        else if (isSecondPlayerHit)
        {
            UnityEngine.Debug.Log($"<color=cyan>[HIT!] Player {secondLocalPlayerId} was hit by projectile {hitMsg.projectileId}!</color>");
        }

        // Session 4.5: Visual effects
        if (visualEffectsManager != null)
        {
            // Spawn hit explosion at collision position
            visualEffectsManager.PlayHitEffect(hitMsg.hitPosition);

            // Screen shake for local players when hit
            if (isLocalPlayerHit || isSecondPlayerHit)
            {
                visualEffectsManager.TriggerScreenShake();
            }
        }
    }

    void HandlePlayerDeath(PlayerDeathMessage deathMsg)
    {
        // Check if a local player died
        bool isLocalPlayerDeath = (deathMsg.playerId == localPlayerId);
        bool isSecondPlayerDeath = (enableSecondLocalPlayer && deathMsg.playerId == secondLocalPlayerId);

        if (isLocalPlayerDeath)
        {
            UnityEngine.Debug.Log($"<color=red>☠ [DEATH!] You (Player {localPlayerId}) died at {deathMsg.deathPosition}! Respawning in 3 seconds...</color>");
        }
        else if (isSecondPlayerDeath)
        {
            UnityEngine.Debug.Log($"<color=magenta>☠ [DEATH!] Player {secondLocalPlayerId} died at {deathMsg.deathPosition}!</color>");
        }
        else
        {
            UnityEngine.Debug.Log($"[SimplePlayerController] Player {deathMsg.playerId} died at {deathMsg.deathPosition}");
        }

        // Session 4.5: Visual effects
        if (visualEffectsManager != null)
        {
            // Spawn death particle effect
            visualEffectsManager.PlayDeathEffect(deathMsg.deathPosition);

            // Stronger screen shake for local player death
            if (isLocalPlayerDeath || isSecondPlayerDeath)
            {
                visualEffectsManager.TriggerScreenShake(0.5f, 0.4f);
            }
        }
    }

    void HandlePlayerRespawn(PlayerRespawnMessage respawnMsg)
    {
        // Check if a local player respawned
        bool isLocalPlayerRespawn = (respawnMsg.playerId == localPlayerId);
        bool isSecondPlayerRespawn = (enableSecondLocalPlayer && respawnMsg.playerId == secondLocalPlayerId);

        if (isLocalPlayerRespawn)
        {
            UnityEngine.Debug.Log($"<color=green>✨ [RESPAWN!] You (Player {localPlayerId}) respawned at {respawnMsg.respawnPosition}!</color>");
        }
        else if (isSecondPlayerRespawn)
        {
            UnityEngine.Debug.Log($"<color=lime>✨ [RESPAWN!] Player {secondLocalPlayerId} respawned at {respawnMsg.respawnPosition}!</color>");
        }
        else
        {
            UnityEngine.Debug.Log($"[SimplePlayerController] Player {respawnMsg.playerId} respawned at {respawnMsg.respawnPosition}");
        }

        // Session 4.5: Visual effects
        if (visualEffectsManager != null)
        {
            // Spawn respawn particle effect (green/cyan upward sparkles)
            visualEffectsManager.PlayRespawnEffect(respawnMsg.respawnPosition);
        }
    }

    #endregion

    #region Visual Feedback & Connection Management
    
    void UpdateVisualFeedback()
    {
        // Phase 5.5: Update first local player's visual feedback with cooldown state
        if (playerVisualFeedback.ContainsKey(localPlayerId))
        {
            float cooldownPercent = GetCooldownPercent(localPlayerId);
            playerVisualFeedback[localPlayerId].UpdateFeedback(shootButtonPressed, cooldownPercent);
        }

        // Phase 5.5: Update second local player's visual feedback with cooldown state
        if (enableSecondLocalPlayer && playerVisualFeedback.ContainsKey(secondLocalPlayerId))
        {
            float secondCooldownPercent = GetCooldownPercent(secondLocalPlayerId);
            playerVisualFeedback[secondLocalPlayerId].UpdateFeedback(secondPlayerShootPressed, secondCooldownPercent);
        }
    }
    
    void CheckConnectionTimeout()
    {
        // Check if we've lost connection to server
        if (Time.time - lastStateUpdateTime > connectionTimeout)
        {
            UnityEngine.Debug.LogWarning("[SimplePlayerController] Connection timeout! No state updates received.");
            // Could show UI message or attempt reconnection here
        }
    }
    
    #endregion
    
    #region Debug UI
    
    void OnGUI()
    {
        if (!showDebugUI) return;

        // Get network stats (FIX 3: Now includes sequence number)
        networkManager.GetNetworkStats(out packetsSent, out packetsReceived, out currentSequence);

        // Calculate ping estimate (time since last update)
        float timeSinceLastUpdate = Time.time - lastStateUpdateTime;
        int pingEstimate = Mathf.RoundToInt(timeSinceLastUpdate * 1000f);
        
        // Connection status
        string connectionStatus = "Connected";
        Color statusColor = Color.green;
        if (timeSinceLastUpdate > 1f)
        {
            connectionStatus = "Poor Connection";
            statusColor = Color.yellow;
        }
        if (timeSinceLastUpdate > connectionTimeout)
        {
            connectionStatus = "DISCONNECTED";
            statusColor = Color.red;
        }
        
        // Display debug info with styling
        GUILayout.BeginArea(new Rect(10, 10, 320, 240));
        
        // Header
        GUIStyle headerStyle = new GUIStyle(GUI.skin.box);
        headerStyle.fontSize = 14;
        headerStyle.fontStyle = FontStyle.Bold;
        GUILayout.Box("Network Debug Info", headerStyle);
        
        // Stats
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label($"Players Connected: {playerObjects.Count}");

        // Connection status with color
        GUI.contentColor = statusColor;
        GUILayout.Label($"Status: {connectionStatus}");
        GUI.contentColor = Color.white;

        GUILayout.Label($"Ping: ~{pingEstimate}ms");
        GUILayout.Label($"Packets Sent: {packetsSent}");
        GUILayout.Label($"Packets Received: {packetsReceived}");
        GUILayout.Label($"Server Time: {lastServerTime:F2}s");

        // Player 1 info
        GUI.contentColor = localPlayerColor;
        GUILayout.Label($"P1 Input: ({currentInput.x:F2}, {currentInput.y:F2}) {(shootButtonPressed ? "[SHOOT]" : "")}");
        GUI.contentColor = Color.white;

        // Player 2 info (if enabled)
        if (enableSecondLocalPlayer)
        {
            GUI.contentColor = secondLocalPlayerColor;
            GUILayout.Label($"P2 Input: ({secondPlayerInput.x:F2}, {secondPlayerInput.y:F2}) {(secondPlayerShootPressed ? "[SHOOT]" : "")}");
            GUI.contentColor = Color.white;
        }

        GUILayout.EndVertical();

        GUILayout.EndArea();

        // Lab 8-9: Network Simulator Controls (Packet Loss Only)
        NetworkSimulator netSim = networkManager.GetNetworkSimulator();
        GUILayout.BeginArea(new Rect(10, 260, 320, 140));
        GUILayout.Box("Network Simulator (Packet Loss)", headerStyle);
        GUILayout.BeginVertical(GUI.skin.box);

        // Enable/Disable toggle
        netSim.enabled = GUILayout.Toggle(netSim.enabled, netSim.enabled ? "ENABLED (dropping packets)" : "Disabled (normal network)");

        if (netSim.enabled)
        {
            GUILayout.Space(5);
            // Packet Loss slider (0-50%)
            GUILayout.Label($"Packet Loss: {netSim.packetLossPercent:F0}%");
            netSim.packetLossPercent = GUILayout.HorizontalSlider(netSim.packetLossPercent, 0f, 50f);

            GUILayout.Space(5);
            GUILayout.Label("(Latency simulation removed - use real network for latency testing)", GUI.skin.GetStyle("label"));
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();

        // Instructions - adjust height based on whether second player is enabled
        int controlsHeight = enableSecondLocalPlayer ? 160 : 120;
        GUILayout.BeginArea(new Rect(10, Screen.height - controlsHeight - 10, 320, controlsHeight));
        GUILayout.Box("Controls", headerStyle);
        GUILayout.BeginVertical(GUI.skin.box);
        GUI.contentColor = localPlayerColor;
        GUILayout.Label("Player 1: WASD + SPACE");
        GUI.contentColor = Color.white;
        if (enableSecondLocalPlayer)
        {
            GUI.contentColor = secondLocalPlayerColor;
            GUILayout.Label("Player 2: Arrow Keys + Right Shift");
            GUI.contentColor = Color.white;
        }
        GUILayout.Label("ESC - Quit application");
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    
    #endregion
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (networkManager != null)
        {
            networkManager.OnStateUpdate -= HandleStateUpdate;
        }
        
        // Clean up player objects
        foreach (var kvp in playerObjects)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        playerObjects.Clear();
        playerVisualFeedback.Clear();
        playerVisualControllers.Clear(); // Session 5A: Visual dressing cleanup
    }
}

