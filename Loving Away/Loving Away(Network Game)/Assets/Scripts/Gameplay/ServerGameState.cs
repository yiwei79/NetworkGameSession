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

    // Movement parameters
    private float moveSpeed = 5.0f;
    private float acceleration = 50.0f; // Increased for more responsive movement
    private float maxDeltaTime = 0.1f; // Cap delta time to prevent huge jumps

    // Projectile system
    private Queue<ProjectileSpawnMessage> pendingProjectileSpawns;
    private uint nextProjectileId = 1;
    private float projectileCooldown = 0.5f; // 0.5 seconds between shots
    private Dictionary<uint, float> lastShootTime; // Track last shoot time per player
    private float projectileSpeed = 15.0f; // Units per second
    private float projectileHeight = 2.0f; // Launch height above player
    private float projectileRange = 10.0f; // How far projectiles travel horizontally
    private float projectileArcHeight = 3.0f; // Peak height of arc trajectory
    
    public ServerGameState()
    {
        players = new Dictionary<uint, PlayerState>();
        serverTime = 0f;
        pendingProjectileSpawns = new Queue<ProjectileSpawnMessage>();
        lastShootTime = new Dictionary<uint, float>();
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
                velocity = Vector3.zero,
                facingDirection = new Vector3(0, 0, 1) // Default facing forward (positive Z)
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
    /// Processes a client input message and stores it for the next physics update
    /// Inputs are stored immediately but applied during UpdateState with proper deltaTime
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
        
        // Store the latest input - it will be applied during UpdateState with proper deltaTime
        // This prevents input processing from being frame-rate dependent
        player.currentInput = input.moveDirection;
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
        // Cap delta time to prevent huge position jumps
        deltaTime = Mathf.Min(deltaTime, maxDeltaTime);
        
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
            
            // Apply input to velocity (frame-rate independent)
            if (player.currentInput.magnitude > 0.1f)
            {
                // Player is providing input - accelerate towards target velocity
                Vector2 normalizedInput = player.currentInput.normalized;
                Vector3 inputDir3D = new Vector3(normalizedInput.x, 0, normalizedInput.y);
                Vector3 targetVelocity = inputDir3D * moveSpeed;

                // Update facing direction to match movement direction
                player.facingDirection = inputDir3D;

                // Apply acceleration with deltaTime for frame-rate independence
                float accelStep = acceleration * deltaTime;
                player.velocity = Vector3.MoveTowards(
                    player.velocity,
                    targetVelocity,
                    accelStep
                );
            }
            else
            {
                // No input - decelerate to stop
                float decelStep = acceleration * deltaTime * 0.6f; // Slightly slower deceleration for smooth stopping
                player.velocity = Vector3.MoveTowards(
                    player.velocity,
                    Vector3.zero,
                    decelStep
                );
            }
            
            // Update position based on velocity (frame-rate independent)
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

            // Handle shooting
            if (player.isShootPressed)
            {
                // Check cooldown
                float timeSinceLastShot = serverTime - GetLastShootTime(playerId);
                if (timeSinceLastShot >= projectileCooldown)
                {
                    SpawnProjectile(playerId, player.position, player.facingDirection);
                    lastShootTime[playerId] = serverTime;
                }
            }

            players[playerId] = player;
        }
    }
    
    #endregion

    #region Projectile System

    /// <summary>
    /// Spawns a projectile from a player with arc trajectory
    /// </summary>
    private void SpawnProjectile(uint playerId, Vector3 playerPosition, Vector3 facingDirection)
    {
        uint projectileId = nextProjectileId++;

        // Calculate launch position (above player)
        Vector3 startPosition = playerPosition + new Vector3(0, projectileHeight, 0);

        // Use player's facing direction (always horizontal)
        Vector3 shootDirection = new Vector3(facingDirection.x, 0, facingDirection.z).normalized;

        // Fallback to forward if facing direction is zero (shouldn't happen)
        if (shootDirection.magnitude < 0.1f)
        {
            shootDirection = new Vector3(0, 0, 1);
        }

        // Calculate target position (where projectile lands)
        Vector3 targetPosition = playerPosition + shootDirection * projectileRange;
        targetPosition.y = 0.5f; // Land at ground level

        // Calculate flight time based on range and speed
        float flightTime = projectileRange / projectileSpeed;

        // Set projectile velocity (for direction reference, arc uses targetPosition)
        Vector3 projectileVelocity = shootDirection * projectileSpeed;

        // Create spawn message with arc parameters
        ProjectileSpawnMessage spawnMsg = new ProjectileSpawnMessage(
            projectileId,
            playerId,
            startPosition,
            projectileVelocity,
            targetPosition,
            projectileArcHeight,
            flightTime
        );

        // Queue for broadcasting
        pendingProjectileSpawns.Enqueue(spawnMsg);

        UnityEngine.Debug.Log($"[ServerGameState] Player {playerId} spawned projectile {projectileId} at {startPosition} -> {targetPosition} (arc height: {projectileArcHeight})");
    }

    /// <summary>
    /// Gets the last shoot time for a player (returns 0 if never shot)
    /// </summary>
    private float GetLastShootTime(uint playerId)
    {
        if (lastShootTime.ContainsKey(playerId))
        {
            return lastShootTime[playerId];
        }
        return 0f;
    }

    /// <summary>
    /// Gets all pending projectile spawns and clears the queue
    /// Called by GameNetworkManager to broadcast spawns to clients
    /// </summary>
    public ProjectileSpawnMessage[] GetPendingProjectileSpawns()
    {
        ProjectileSpawnMessage[] spawns = pendingProjectileSpawns.ToArray();
        pendingProjectileSpawns.Clear();
        return spawns;
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
    public Vector3 facingDirection; // Last movement direction (for shooting when stationary)
    public bool isShootPressed;
    public Vector2 currentInput; // Latest input from client (stored, applied during UpdateState)
}

