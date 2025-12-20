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

    // Phase 2: Charge mechanic state (Player 2)
    private bool wasSecondPlayerShootingLastFrame = false;
    private float secondPlayerChargeStartTime = 0f;
    private float secondPlayerChargeValue = 0f;

    // Input rate limiting (FIX 1: Prevent input over-queuing)
    [Header("Network Settings")]
    public float inputSendRate = 30f; // Hz - how many times per second to send input
    private float lastInputSendTime = 0f;

    // Client-side prediction (FIX 2: Instant local response)
    [Header("Prediction Settings")]
    public bool enablePrediction = true;
    public float predictionBlendSpeed = 10f; // How fast to reconcile with server

    // Movement parameters (must match server - ServerGameState.cs)
    private float moveSpeed = 5.0f;
    private float acceleration = 50.0f;
    private float arenaRadius = 15f;

    // Local player prediction state
    private Vector3 predictedVelocity = Vector3.zero;
    private Vector3 predictedPosition = Vector3.zero;
    private bool hasInitializedPrediction = false;

    // Second local player prediction state
    private Vector3 secondPredictedVelocity = Vector3.zero;
    private Vector3 secondPredictedPosition = Vector3.zero;
    private bool hasInitializedSecondPrediction = false;

    // Network stats
    private int packetsSent;
    private int packetsReceived;
    private uint currentSequence; // FIX 3: Current input sequence number
    private float lastServerTime;
    private float lastStateUpdateTime;
    private float connectionTimeout = 5f;
    
    void Start()
    {
        playerObjects = new Dictionary<uint, GameObject>();
        playerVisualFeedback = new Dictionary<uint, ShootVisualFeedback>();
        playerVisualControllers = new Dictionary<uint, PlayerVisualController>(); // Session 5A: Visual dressing
        playerHealthBars = new Dictionary<uint, PlayerHealthBar>(); // Phase 3: Health bars
        projectileObjects = new Dictionary<uint, GameObject>();

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
            shootButtonPressed = justReleased && hasMinimumCharge;

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
                secondPlayerShootPressed = secondJustReleased && secondHasMinimumCharge;

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
            // Phase 2: Send Player 1 input with charge value
            networkManager.SendInput(currentInput, shootButtonPressed, currentChargeValue);

            // Phase 2: Send Player 2 input with charge value if enabled
            if (enableSecondLocalPlayer)
            {
                networkManager.SendInputForPlayer(secondLocalPlayerId, secondPlayerInput, secondPlayerShootPressed, secondPlayerChargeValue);
            }

            lastInputSendTime = Time.time;
        }
    }

    void PredictLocalPlayerMovement()
    {
        // FIX 2: Client-side prediction for local player
        // This method applies the same movement logic as ServerGameState.UpdateState()
        // to give instant visual feedback for the local player

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

        // Apply arena boundary constraints (same as server)
        Vector3 positionXZ = new Vector3(predictedPosition.x, 0, predictedPosition.z);
        if (positionXZ.magnitude > arenaRadius)
        {
            // Push back inside arena
            positionXZ = positionXZ.normalized * arenaRadius;
            predictedPosition = new Vector3(positionXZ.x, predictedPosition.y, positionXZ.z);

            // Reduce velocity when hitting boundary
            predictedVelocity *= 0.5f;
        }

        // Apply predicted position to visual (instant response!)
        localPlayerObj.transform.position = predictedPosition;
    }

    void PredictSecondPlayerMovement()
    {
        // Client-side prediction for second local player
        // Mirror of PredictLocalPlayerMovement() but for player 2

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

        // Apply arena boundary constraints (same as server)
        Vector3 positionXZ = new Vector3(secondPredictedPosition.x, 0, secondPredictedPosition.z);
        if (positionXZ.magnitude > arenaRadius)
        {
            // Push back inside arena
            positionXZ = positionXZ.normalized * arenaRadius;
            secondPredictedPosition = new Vector3(positionXZ.x, secondPredictedPosition.y, positionXZ.z);

            // Reduce velocity when hitting boundary
            secondPredictedVelocity *= 0.5f;
        }

        // Apply predicted position to visual (instant response!)
        secondPlayerObj.transform.position = secondPredictedPosition;
    }

    #endregion

    #region State Update Handling
    
    void HandleStateUpdate(ServerStateUpdateMessage stateMsg)
    {
        lastServerTime = stateMsg.serverTime;
        lastStateUpdateTime = Time.time;
        
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
                // Remote player: Direct server position (no prediction)
                playerObj.transform.position = snapshot.position;
            }

            // CRITICAL FIX: Player GameObject MUST rotate for shooting/knockback to work
            // The PlayerVisualController is just for enhanced visuals, doesn't replace core rotation
            if (snapshot.velocity.magnitude > 0.1f)
            {
                Vector3 lookDirection = snapshot.velocity.normalized;
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                playerObj.transform.rotation = Quaternion.Slerp(
                    playerObj.transform.rotation,
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

        Vector3 serverPosition = serverSnapshot.position;
        float positionError = Vector3.Distance(predictedPosition, serverPosition);

        // If error is small, gently blend to server position
        // If error is large (e.g., collision or lag spike), snap to server
        float snapThreshold = 2.0f; // If more than 2 units off, something went wrong

        if (positionError > snapThreshold)
        {
            // Large error - snap to server immediately
            predictedPosition = serverPosition;
            predictedVelocity = serverSnapshot.velocity;
            UnityEngine.Debug.LogWarning($"[Prediction] Large error ({positionError:F2}m) - snapping to server");
        }
        else
        {
            // Small error - smoothly blend (this is the "Phase 4-ready" part)
            float blendFactor = predictionBlendSpeed * Time.deltaTime;
            predictedPosition = Vector3.Lerp(predictedPosition, serverPosition, blendFactor);
            predictedVelocity = Vector3.Lerp(predictedVelocity, serverSnapshot.velocity, blendFactor);
        }

        // Note: In Phase 4, this will use input buffering + timestamp-based reconciliation
        // For now, simple blending is sufficient for Deliverable 3
    }

    void ReconcileSecondPlayerWithServerState(PlayerSnapshot serverSnapshot)
    {
        // Reconciliation for second local player - mirror of ReconcileWithServerState

        Vector3 serverPosition = serverSnapshot.position;
        float positionError = Vector3.Distance(secondPredictedPosition, serverPosition);

        float snapThreshold = 2.0f;

        if (positionError > snapThreshold)
        {
            // Large error - snap to server immediately
            secondPredictedPosition = serverPosition;
            secondPredictedVelocity = serverSnapshot.velocity;
            UnityEngine.Debug.LogWarning($"[Prediction] Player 2 large error ({positionError:F2}m) - snapping to server");
        }
        else
        {
            // Small error - smoothly blend
            float blendFactor = predictionBlendSpeed * Time.deltaTime;
            secondPredictedPosition = Vector3.Lerp(secondPredictedPosition, serverPosition, blendFactor);
            secondPredictedVelocity = Vector3.Lerp(secondPredictedVelocity, serverSnapshot.velocity, blendFactor);
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

        // Phase 3: Add health bar component
        PlayerHealthBar healthBar = playerObj.AddComponent<PlayerHealthBar>();
        healthBar.SetHealth(5); // Start with full HP
        playerHealthBars[playerId] = healthBar;

        // Add name tag (TextMesh above player)
        CreateNameTag(playerObj, playerId);

        playerObjects[playerId] = playerObj;
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
        // Update first local player's visual feedback based on shoot button
        if (playerVisualFeedback.ContainsKey(localPlayerId))
        {
            playerVisualFeedback[localPlayerId].UpdateFeedback(shootButtonPressed);
        }

        // Update second local player's visual feedback if enabled
        if (enableSecondLocalPlayer && playerVisualFeedback.ContainsKey(secondLocalPlayerId))
        {
            playerVisualFeedback[secondLocalPlayerId].UpdateFeedback(secondPlayerShootPressed);
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

