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
    
    [Header("Local Player Settings")]
    public uint localPlayerId = 0;
    public Color localPlayerColor = Color.green;
    
    [Header("Remote Player Settings")]
    public Color remotePlayerColor = Color.red;
    
    [Header("Debug UI")]
    public bool showDebugUI = true;
    
    // Player GameObjects (visual representation)
    private Dictionary<uint, GameObject> playerObjects;
    private Dictionary<uint, ShootVisualFeedback> playerVisualFeedback;
    
    // Input state
    private Vector2 currentInput;
    private bool shootButtonPressed;

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
        
        // Subscribe to state updates from network manager
        networkManager.OnStateUpdate += HandleStateUpdate;
        
        lastStateUpdateTime = Time.time;
        
        UnityEngine.Debug.Log($"[SimplePlayerController] Initialized for player {localPlayerId}");
    }
    
    void Update()
    {
        CollectInput();
        SendInputToServer();

        // FIX 2: Predict local player movement immediately
        if (enablePrediction)
        {
            PredictLocalPlayerMovement();
        }

        UpdateVisualFeedback();
        CheckConnectionTimeout();
    }
    
    #region Input Collection
    
    void CollectInput()
    {
        // NEW INPUT SYSTEM - Uses Keyboard.current for better control
        // Collect WASD input
        float horizontal = 0f;
        float vertical = 0f;
        
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) vertical += 1f;
            if (keyboard.sKey.isPressed) vertical -= 1f;
            if (keyboard.aKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed) horizontal += 1f;
            
            currentInput = new Vector2(horizontal, vertical);
            
            // Normalize to prevent faster diagonal movement
            if (currentInput.magnitude > 1f)
            {
                currentInput.Normalize();
            }
            
            // Collect shoot button (Spacebar)
            shootButtonPressed = keyboard.spaceKey.isPressed;
        }
        else
        {
            // No keyboard detected - clear input
            currentInput = Vector2.zero;
            shootButtonPressed = false;
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
            // Send input to network manager
            networkManager.SendInput(currentInput, shootButtonPressed);
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
            // FIX 2: Different handling for local vs remote players
            bool isLocalPlayer = (snapshot.playerId == localPlayerId);

            if (isLocalPlayer && enablePrediction)
            {
                // Local player: Reconcile prediction with server state
                // Smoothly blend predicted position towards server's authoritative position
                ReconcileWithServerState(snapshot);
            }
            else
            {
                // Remote player: Direct server position (no prediction)
                playerObj.transform.position = snapshot.position;
            }

            // Rotate based on velocity direction (for both local and remote)
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
    
    void CreatePlayerObject(uint playerId)
    {
        if (playerPrefab == null)
        {
            UnityEngine.Debug.LogError("[SimplePlayerController] Player prefab not assigned!");
            return;
        }
        
        GameObject playerObj = Instantiate(playerPrefab);
        playerObj.name = $"Player_{playerId}";
        
        // Set color based on whether it's local or remote player
        Renderer renderer = playerObj.GetComponent<Renderer>();
        Color playerColor = (playerId == localPlayerId) ? localPlayerColor : remotePlayerColor;
        if (renderer != null)
        {
            renderer.material.color = playerColor;
        }
        
        // Add visual feedback component
        ShootVisualFeedback feedback = playerObj.AddComponent<ShootVisualFeedback>();
        feedback.chargeColor = playerColor * 0.8f;
        feedback.SetOriginalColor(playerColor);
        playerVisualFeedback[playerId] = feedback;
        
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
        textMesh.text = (playerId == localPlayerId) ? "You" : $"Player {playerId}";
        textMesh.fontSize = 20;
        textMesh.characterSize = 0.1f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = (playerId == localPlayerId) ? localPlayerColor : remotePlayerColor;
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
                UnityEngine.Debug.Log($"[SimplePlayerController] Removed player {playerId}");
            }
        }
    }
    
    #endregion
    
    #region Visual Feedback & Connection Management
    
    void UpdateVisualFeedback()
    {
        // Update local player's visual feedback based on shoot button
        if (playerVisualFeedback.ContainsKey(localPlayerId))
        {
            playerVisualFeedback[localPlayerId].UpdateFeedback(shootButtonPressed);
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
        GUILayout.Label($"Local Player ID: {localPlayerId}");
        GUILayout.Label($"Players Connected: {playerObjects.Count}");
        
        // Connection status with color
        GUI.contentColor = statusColor;
        GUILayout.Label($"Status: {connectionStatus}");
        GUI.contentColor = Color.white;
        
        GUILayout.Label($"Ping: ~{pingEstimate}ms");
        GUILayout.Label($"Packets Sent: {packetsSent}");
        GUILayout.Label($"Packets Received: {packetsReceived}");
        GUILayout.Label($"Input Sequence: #{currentSequence}"); // FIX 3: Show sequence number
        GUILayout.Label($"Server Time: {lastServerTime:F2}s");
        GUILayout.Label($"Input: ({currentInput.x:F2}, {currentInput.y:F2})");
        GUILayout.Label($"Shoot: {(shootButtonPressed ? "CHARGING" : "Ready")}");
        GUILayout.EndVertical();
        
        GUILayout.EndArea();
        
        // Instructions
        GUILayout.BeginArea(new Rect(10, Screen.height - 130, 320, 120));
        GUILayout.Box("Controls", headerStyle);
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("WASD - Move your character");
        GUILayout.Label("SPACE - Hold to charge shot");
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
    }
}

