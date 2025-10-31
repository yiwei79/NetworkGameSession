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
    
    // Network stats
    private int packetsSent;
    private int packetsReceived;
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
            networkManager = FindObjectOfType<GameNetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("[SimplePlayerController] No GameNetworkManager found!");
                return;
            }
        }
        
        // Set local player ID from network manager
        localPlayerId = networkManager.localPlayerId;
        
        // Subscribe to state updates from network manager
        networkManager.OnStateUpdate += HandleStateUpdate;
        
        lastStateUpdateTime = Time.time;
        
        Debug.Log($"[SimplePlayerController] Initialized for player {localPlayerId}");
    }
    
    void Update()
    {
        CollectInput();
        SendInputToServer();
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
        // Send input to network manager
        networkManager.SendInput(currentInput, shootButtonPressed);
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
            // Directly set position (no interpolation for now - will add in extras)
            playerObj.transform.position = snapshot.position;
            
            // Optionally rotate based on velocity direction
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
    
    void CreatePlayerObject(uint playerId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[SimplePlayerController] Player prefab not assigned!");
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
        Debug.Log($"[SimplePlayerController] Created visual for player {playerId}");
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
                Debug.Log($"[SimplePlayerController] Removed player {playerId}");
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
            Debug.LogWarning("[SimplePlayerController] Connection timeout! No state updates received.");
            // Could show UI message or attempt reconnection here
        }
    }
    
    #endregion
    
    #region Debug UI
    
    void OnGUI()
    {
        if (!showDebugUI) return;
        
        // Get network stats
        networkManager.GetNetworkStats(out packetsSent, out packetsReceived);
        
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

