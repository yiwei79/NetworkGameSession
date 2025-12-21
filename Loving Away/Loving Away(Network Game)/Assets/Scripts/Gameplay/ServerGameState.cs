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

    // Movement parameters (Phase 5.6: Heavier "Animal Party" feel)
    private float moveSpeed = 3.5f;     // Was 5.0f - 30% slower, more deliberate
    private float acceleration = 25.0f; // Was 50.0f - 50% slower, more momentum
    private float maxDeltaTime = 0.1f;  // Cap delta time to prevent huge jumps

    // Projectile system
    private Queue<ProjectileSpawnMessage> pendingProjectileSpawns;
    private Queue<ProjectileHitMessage> pendingHitMessages;
    private Dictionary<uint, ServerProjectile> activeProjectiles; // Track active projectiles for hit detection
    private uint nextProjectileId = 1;
    private float projectileCooldown = 0.5f; // 0.5 seconds between shots
    private Dictionary<uint, float> lastShootTime; // Track last shoot time per player
    private float projectileSpeed = 15.0f; // Units per second
    private float projectileHeight = 2.0f; // Launch height above player
    private float projectileRange = 10.0f; // How far projectiles travel horizontally
    private float projectileArcHeight = 3.0f; // Peak height of arc trajectory

    // Hit detection parameters
    private float collisionRadius = 0.7f; // Combined projectile (0.2) + player (0.5) radius
    private float knockbackForce = 12.0f; // Units per second

    // Death/respawn system
    private Queue<PlayerDeathMessage> pendingDeathMessages;
    private Queue<PlayerRespawnMessage> pendingRespawnMessages;
    private float respawnDelay = 3.0f; // 3 seconds to respawn

    public ServerGameState()
    {
        players = new Dictionary<uint, PlayerState>();
        serverTime = 0f;
        pendingProjectileSpawns = new Queue<ProjectileSpawnMessage>();
        pendingHitMessages = new Queue<ProjectileHitMessage>();
        pendingDeathMessages = new Queue<PlayerDeathMessage>();
        pendingRespawnMessages = new Queue<PlayerRespawnMessage>();
        activeProjectiles = new Dictionary<uint, ServerProjectile>();
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
                facingDirection = new Vector3(0, 0, 1), // Default facing forward (positive Z)
                isAlive = true,
                health = 5,     // Phase 3: Start with full HP
                deathTime = 0f,
                respawnTime = 0f
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
            0.0f, // Phase 5.6: At ground level (was 0.5f - caused floating)
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

        // Dead players can't move or shoot (Session 4)
        if (!player.isAlive)
        {
            return;
        }

        // Store the latest input - it will be applied during UpdateState with proper deltaTime
        // This prevents input processing from being frame-rate dependent
        player.currentInput = input.moveDirection;
        player.isShootPressed = input.shootButton;
        player.chargeValue = input.chargeValue; // Phase 2: Store charge for projectile scaling

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

            // Arena boundary death (Session 4)
            float arenaRadius = 15f;
            Vector3 positionXZ = new Vector3(player.position.x, 0, player.position.z);
            float distanceFromCenter = positionXZ.magnitude;

            if (player.isAlive && distanceFromCenter > arenaRadius)
            {
                // Player crossed boundary - trigger death
                TriggerPlayerDeath(player.playerId, player.position);
            }

            // Phase 5.6: Keep player at ground level (was 0.5f - caused floating)
            player.position.y = 0.0f;

            // Handle shooting (Phase 2: Pass chargeValue for trajectory scaling)
            if (player.isShootPressed)
            {
                // Check cooldown
                float timeSinceLastShot = serverTime - GetLastShootTime(playerId);
                if (timeSinceLastShot >= projectileCooldown)
                {
                    SpawnProjectile(playerId, player.position, player.facingDirection, player.chargeValue);
                    lastShootTime[playerId] = serverTime;
                }
                else
                {
                    // Debug: Shot blocked by cooldown
                    UnityEngine.Debug.Log($"[ServerGameState] Player {playerId} shoot blocked by cooldown ({timeSinceLastShot:F2}s < {projectileCooldown}s)");
                }
            }

            players[playerId] = player;
        }

        // Check projectile collisions after all player positions updated
        CheckProjectileCollisions();

        // Check if any dead players should respawn (Session 4)
        CheckRespawns();
    }

    #endregion

    #region Projectile System

    /// <summary>
    /// Spawns a projectile from a player with arc trajectory
    /// Phase 2: Scales range, arc height, and speed based on chargeValue (0.0-1.0)
    /// </summary>
    private void SpawnProjectile(uint playerId, Vector3 playerPosition, Vector3 facingDirection, float chargeValue)
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

        // Phase 2: Scale projectile parameters based on charge (0.0-1.0)
        float scaledRange = Mathf.Lerp(5f, 20f, chargeValue);      // 5u → 20u
        float scaledArcHeight = Mathf.Lerp(2f, 6f, chargeValue);   // 2u → 6u
        float scaledSpeed = Mathf.Lerp(8f, 12f, chargeValue);      // 8u/s → 12u/s (Phase 5.6: Heavier feel)

        // Calculate target position (where projectile lands)
        Vector3 targetPosition = playerPosition + shootDirection * scaledRange;
        targetPosition.y = 0.5f; // Land at ground level

        // Calculate flight time based on scaled range and speed
        float flightTime = scaledRange / scaledSpeed;

        // Set projectile velocity (for direction reference, arc uses targetPosition)
        Vector3 projectileVelocity = shootDirection * scaledSpeed;

        // Create spawn message with arc parameters
        ProjectileSpawnMessage spawnMsg = new ProjectileSpawnMessage(
            projectileId,
            playerId,
            startPosition,
            projectileVelocity,
            targetPosition,
            scaledArcHeight,  // Phase 2: Use scaled arc height
            flightTime
        );

        // Queue for broadcasting
        pendingProjectileSpawns.Enqueue(spawnMsg);

        // Add to active projectiles for server-side hit detection
        ServerProjectile serverProjectile = new ServerProjectile
        {
            projectileId = projectileId,
            ownerId = playerId,
            startPosition = startPosition,
            targetPosition = targetPosition,
            arcHeight = scaledArcHeight,  // Phase 2: Use scaled arc height
            flightTime = flightTime,
            spawnTime = serverTime
        };
        activeProjectiles[projectileId] = serverProjectile;

        UnityEngine.Debug.Log($"[ServerGameState] Player {playerId} spawned projectile {projectileId} at {startPosition} -> {targetPosition} (charge: {chargeValue:F2}, range: {scaledRange:F1}u, arc: {scaledArcHeight:F1}u)");
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

    /// <summary>
    /// Gets all pending hit messages and clears the queue
    /// Called by GameNetworkManager to broadcast hits to clients
    /// </summary>
    public ProjectileHitMessage[] GetPendingHitMessages()
    {
        ProjectileHitMessage[] hits = pendingHitMessages.ToArray();
        pendingHitMessages.Clear();
        return hits;
    }

    /// <summary>
    /// Calculates the current position of a projectile using arc trajectory formula
    /// CRITICAL: Must match Projectile.cs client-side rendering formula exactly
    /// </summary>
    private Vector3 CalculateProjectilePosition(ServerProjectile proj, float currentTime)
    {
        float elapsedTime = currentTime - proj.spawnTime;
        float t = Mathf.Clamp01(elapsedTime / proj.flightTime);

        // Horizontal (XZ plane): Linear interpolation
        Vector3 horizontal = Vector3.Lerp(proj.startPosition, proj.targetPosition, t);

        // Vertical (Y axis): Parabolic arc
        // Formula: heightOffset = arcHeight * 4 * t * (1 - t)
        // - At t=0: heightOffset = 0 (starts at ground)
        // - At t=0.5: heightOffset = arcHeight (peaks at midpoint)
        // - At t=1.0: heightOffset = 0 (returns to ground)
        float heightOffset = proj.arcHeight * 4f * t * (1f - t);

        return new Vector3(horizontal.x, horizontal.y + heightOffset, horizontal.z);
    }

    /// <summary>
    /// Checks for projectile collisions with players
    /// Called every server tick (20 Hz) after player positions are updated
    /// </summary>
    private void CheckProjectileCollisions()
    {
        // Create list of projectile IDs to remove (can't modify dictionary during iteration)
        List<uint> projectilesToRemove = new List<uint>();

        foreach (var kvp in activeProjectiles)
        {
            ServerProjectile projectile = kvp.Value;
            float elapsedTime = serverTime - projectile.spawnTime;

            // Check if projectile has expired
            if (elapsedTime >= projectile.flightTime)
            {
                projectilesToRemove.Add(projectile.projectileId);
                UnityEngine.Debug.Log($"[ServerGameState] Projectile {projectile.projectileId} expired (flight time: {projectile.flightTime}s)");
                continue;
            }

            // Calculate current projectile position
            Vector3 projectilePosition = CalculateProjectilePosition(projectile, serverTime);

            // Check collision with all players
            foreach (var playerKvp in players)
            {
                PlayerState player = playerKvp.Value;

                // Skip owner (can't hit yourself)
                if (player.playerId == projectile.ownerId)
                {
                    continue;
                }

                // Calculate 3D distance between projectile and player
                float distance = Vector3.Distance(projectilePosition, player.position);

                // Check if collision detected
                if (distance < collisionRadius)
                {
                    // HIT DETECTED!
                    UnityEngine.Debug.Log($"[ServerGameState] HIT! Projectile {projectile.projectileId} hit player {player.playerId} at distance {distance:F2}");

                    // Calculate knockback direction (away from projectile)
                    Vector3 knockbackDirection = (player.position - projectilePosition).normalized;

                    // Apply knockback to player velocity
                    player.velocity += knockbackDirection * knockbackForce;

                    // Phase 3: Apply damage (1 HP per hit)
                    if (player.health > 0)
                    {
                        player.health--;
                        UnityEngine.Debug.Log($"[ServerGameState] Player {player.playerId} took damage! Health: {player.health}/5");
                    }

                    // Save updated player state
                    players[player.playerId] = player;

                    // Create hit message for clients
                    ProjectileHitMessage hitMsg = new ProjectileHitMessage(
                        projectile.projectileId,
                        player.playerId,
                        projectilePosition
                    );
                    pendingHitMessages.Enqueue(hitMsg);

                    // Phase 3: Only trigger death if HP reaches 0
                    if (player.health == 0)
                    {
                        TriggerPlayerDeath(player.playerId, projectilePosition);
                    }

                    // Mark projectile for removal
                    projectilesToRemove.Add(projectile.projectileId);

                    // Break inner loop - projectile can only hit one player
                    break;
                }
            }
        }

        // Remove expired and hit projectiles
        foreach (uint projectileId in projectilesToRemove)
        {
            activeProjectiles.Remove(projectileId);
        }
    }

    #endregion

    #region Death/Respawn System

    /// <summary>
    /// Triggers player death and queues death message
    /// </summary>
    private void TriggerPlayerDeath(uint playerId, Vector3 deathPosition)
    {
        if (!players.ContainsKey(playerId))
        {
            return;
        }

        PlayerState player = players[playerId];

        // Check if player is already dead (prevent double death)
        if (!player.isAlive)
        {
            return;
        }

        // Mark player as dead
        player.isAlive = false;
        player.deathTime = serverTime;
        player.respawnTime = serverTime + respawnDelay;
        player.velocity = Vector3.zero; // Freeze movement

        players[playerId] = player;

        // Queue death message for clients
        PlayerDeathMessage deathMsg = new PlayerDeathMessage(playerId, deathPosition);
        pendingDeathMessages.Enqueue(deathMsg);

        UnityEngine.Debug.Log($"[ServerGameState] Player {playerId} died at {deathPosition}. Respawn in {respawnDelay}s");
    }

    /// <summary>
    /// Respawns a dead player at a valid spawn position
    /// </summary>
    private void RespawnPlayer(uint playerId)
    {
        if (!players.ContainsKey(playerId))
        {
            return;
        }

        PlayerState player = players[playerId];

        // Set player to alive with full health (Phase 3)
        player.isAlive = true;
        player.health = 5;  // Phase 3: Restore full HP on respawn
        player.position = GetSpawnPosition(playerId);
        player.velocity = Vector3.zero;
        player.deathTime = 0f;
        player.respawnTime = 0f;

        players[playerId] = player;

        // Queue respawn message for clients
        PlayerRespawnMessage respawnMsg = new PlayerRespawnMessage(playerId, player.position);
        pendingRespawnMessages.Enqueue(respawnMsg);

        UnityEngine.Debug.Log($"[ServerGameState] Player {playerId} respawned at {player.position}");
    }

    /// <summary>
    /// Checks if any dead players should respawn
    /// Called every tick from UpdateState()
    /// </summary>
    private void CheckRespawns()
    {
        List<uint> playerIds = new List<uint>(players.Keys);

        foreach (uint playerId in playerIds)
        {
            if (!players.ContainsKey(playerId))
            {
                continue;
            }

            PlayerState player = players[playerId];

            // Check if dead player can respawn
            if (!player.isAlive && serverTime >= player.respawnTime)
            {
                RespawnPlayer(playerId);
            }
        }
    }

    /// <summary>
    /// Gets all pending death messages and clears the queue
    /// Called by GameNetworkManager to broadcast deaths to clients
    /// </summary>
    public PlayerDeathMessage[] GetPendingDeathMessages()
    {
        PlayerDeathMessage[] deaths = pendingDeathMessages.ToArray();
        pendingDeathMessages.Clear();
        return deaths;
    }

    /// <summary>
    /// Gets all pending respawn messages and clears the queue
    /// Called by GameNetworkManager to broadcast respawns to clients
    /// </summary>
    public PlayerRespawnMessage[] GetPendingRespawnMessages()
    {
        PlayerRespawnMessage[] respawns = pendingRespawnMessages.ToArray();
        pendingRespawnMessages.Clear();
        return respawns;
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
                player.velocity,
                player.isAlive,  // Session 4: Include alive state
                player.health    // Phase 3: Include health
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
    public float chargeValue;   // Phase 2: Charge amount 0.0-1.0 for projectile scaling

    // Death/respawn tracking
    public bool isAlive;
    public byte health;         // Phase 3: Current HP (0-5, max 5)
    public float deathTime;     // Server timestamp when player died (0 if alive)
    public float respawnTime;   // Server timestamp when player can respawn
}

/// <summary>
/// Server-side projectile tracking structure for hit detection
/// </summary>
public struct ServerProjectile
{
    public uint projectileId;
    public uint ownerId;
    public Vector3 startPosition;
    public Vector3 targetPosition;
    public float arcHeight;
    public float flightTime;
    public float spawnTime; // Server timestamp when projectile was spawned
}

