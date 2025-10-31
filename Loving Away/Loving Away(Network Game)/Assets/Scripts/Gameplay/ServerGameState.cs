using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Server-authoritative game state manager
/// Processes client inputs and updates player positions at fixed tick rate (20 Hz)
/// </summary>
public class ServerGameState
{
    // Player state storage
    private Dictionary<uint, PlayerState> players;
    
    // Game timing
    private float serverTime;
    private float fixedDeltaTime = 0.05f; // 20 Hz tick rate
    
    // Movement parameters
    private float moveSpeed = 5.0f;
    private float acceleration = 10.0f;
    
    public ServerGameState()
    {
        players = new Dictionary<uint, PlayerState>();
        serverTime = 0f;
    }
    
    #region Player Management
    
    /// <summary>
    /// Adds a new player to the game state
    /// </summary>
    public void AddPlayer(uint playerId)
    {
        if (!players.ContainsKey(playerId))
        {
            PlayerState newPlayer = new PlayerState
            {
                playerId = playerId,
                position = GetSpawnPosition(playerId),
                velocity = Vector3.zero
            };
            
            players[playerId] = newPlayer;
            UnityEngine.Debug.Log($"[ServerGameState] Added player {playerId} at {newPlayer.position}");
        }
    }
    
    /// <summary>
    /// Removes a player from the game state
    /// </summary>
    public void RemovePlayer(uint playerId)
    {
        if (players.ContainsKey(playerId))
        {
            players.Remove(playerId);
            UnityEngine.Debug.Log($"[ServerGameState] Removed player {playerId}");
        }
    }
    
    /// <summary>
    /// Gets spawn position based on player ID (spreads players around arena)
    /// </summary>
    private Vector3 GetSpawnPosition(uint playerId)
    {
        float angle = (playerId * 90f) * Mathf.Deg2Rad; // 90 degrees apart
        float radius = 5f;
        return new Vector3(
            Mathf.Cos(angle) * radius,
            0.5f, // Slightly above ground
            Mathf.Sin(angle) * radius
        );
    }
    
    #endregion
    
    #region Input Processing
    
    /// <summary>
    /// Processes a client input message and updates player state
    /// </summary>
    public void ProcessInput(ClientInputMessage input)
    {
        if (!players.ContainsKey(input.playerId))
        {
            UnityEngine.Debug.LogWarning($"[ServerGameState] Received input for unknown player {input.playerId}");
            // Auto-add player if they don't exist (might happen if connection happens before AddPlayer)
            AddPlayer(input.playerId);
            return;
        }
        
        PlayerState player = players[input.playerId];
        
        // Update player based on input direction
        if (input.moveDirection.magnitude > 0.1f)
        {
            // Normalize input to prevent faster diagonal movement
            Vector2 normalizedInput = input.moveDirection.normalized;
            Vector3 inputDir3D = new Vector3(normalizedInput.x, 0, normalizedInput.y);
            
            // Calculate target velocity
            Vector3 targetVelocity = inputDir3D * moveSpeed;
            
            // Apply acceleration towards target velocity
            player.velocity = Vector3.MoveTowards(
                player.velocity,
                targetVelocity,
                acceleration * fixedDeltaTime
            );
        }
        else
        {
            // No input - decelerate to stop
            player.velocity = Vector3.MoveTowards(
                player.velocity,
                Vector3.zero,
                acceleration * fixedDeltaTime
            );
        }
        
        // Store shoot button state (for future use)
        player.isShootPressed = input.shootButton;
        
        players[input.playerId] = player;
    }
    
    #endregion
    
    #region State Update
    
    /// <summary>
    /// Updates all player positions based on their velocities
    /// Called at fixed tick rate (20 Hz)
    /// </summary>
    public void UpdateState(float deltaTime)
    {
        serverTime += deltaTime;
        
        // Create list of keys to avoid modification during enumeration
        List<uint> playerIds = new List<uint>(players.Keys);
        
        foreach (uint playerId in playerIds)
        {
            if (!players.ContainsKey(playerId))
            {
                continue; // Player was removed during iteration
            }
            
            PlayerState player = players[playerId];
            
            // Update position based on velocity
            player.position += player.velocity * deltaTime;
            
            // Apply basic boundary constraints (keep players in arena)
            float arenaRadius = 15f;
            Vector3 positionXZ = new Vector3(player.position.x, 0, player.position.z);
            if (positionXZ.magnitude > arenaRadius)
            {
                // Push player back inside arena
                positionXZ = positionXZ.normalized * arenaRadius;
                player.position = new Vector3(positionXZ.x, player.position.y, positionXZ.z);
                
                // Reduce velocity when hitting boundary
                player.velocity *= 0.5f;
            }
            
            // Keep player at ground level
            player.position.y = 0.5f;
            
            players[playerId] = player;
        }
    }
    
    #endregion
    
    #region State Query
    
    /// <summary>
    /// Gets current server time
    /// </summary>
    public float GetServerTime()
    {
        return serverTime;
    }
    
    /// <summary>
    /// Creates a snapshot of all player states for network transmission
    /// </summary>
    public PlayerSnapshot[] GetPlayerSnapshots()
    {
        PlayerSnapshot[] snapshots = new PlayerSnapshot[players.Count];
        int index = 0;
        
        foreach (var kvp in players)
        {
            PlayerState player = kvp.Value;
            snapshots[index] = new PlayerSnapshot(
                player.playerId,
                player.position,
                player.velocity
            );
            index++;
        }
        
        return snapshots;
    }
    
    /// <summary>
    /// Gets the number of connected players
    /// </summary>
    public int GetPlayerCount()
    {
        return players.Count;
    }
    
    #endregion
}

/// <summary>
/// Internal player state representation on the server
/// </summary>
public struct PlayerState
{
    public uint playerId;
    public Vector3 position;
    public Vector3 velocity;
    public bool isShootPressed;
}

