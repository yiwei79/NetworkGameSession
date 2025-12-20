using UnityEngine;

/// <summary>
/// Network message type identifiers for the game protocol
/// </summary>
public enum MessageType : byte
{
    ClientInput = 1,
    ServerStateUpdate = 2,
    Connect = 3,
    Disconnect = 4,
    ProjectileSpawn = 5,
    ProjectileHit = 6,
    PlayerDeath = 7,
    PlayerRespawn = 8
}

/// <summary>
/// Client input message sent from client to server
/// Contains player input commands (WASD movement and shoot button)
/// Size: 1 + 4 + 4 + 8 + 1 = 18 bytes (FIX 3: Added sequenceNumber)
/// </summary>
public struct ClientInputMessage
{
    public MessageType messageType;
    public uint playerId;
    public uint sequenceNumber;    // FIX 3: Sequence number for tracking inputs
    public Vector2 moveDirection;  // Normalized input vector (x, y)
    public bool shootButton;       // Is shoot button pressed

    public ClientInputMessage(uint playerId, uint sequenceNumber, Vector2 moveDirection, bool shootButton)
    {
        this.messageType = MessageType.ClientInput;
        this.playerId = playerId;
        this.sequenceNumber = sequenceNumber;
        this.moveDirection = moveDirection;
        this.shootButton = shootButton;
    }
}

/// <summary>
/// Server state update message sent from server to all clients
/// Contains snapshot of all player states
/// Size: 1 + 4 + 1 + (PlayerSnapshot size * playerCount)
/// </summary>
public struct ServerStateUpdateMessage
{
    public MessageType messageType;
    public float serverTime;       // Server timestamp for synchronization
    public byte playerCount;       // Number of players in the snapshot array
    public PlayerSnapshot[] players;

    public ServerStateUpdateMessage(float serverTime, PlayerSnapshot[] players)
    {
        this.messageType = MessageType.ServerStateUpdate;
        this.serverTime = serverTime;
        this.playerCount = (byte)players.Length;
        this.players = players;
    }
}

/// <summary>
/// Snapshot of a single player's state at a specific moment
/// Size: 4 + 12 + 12 + 1 = 29 bytes per player (Session 4: added isAlive)
/// </summary>
public struct PlayerSnapshot
{
    public uint playerId;
    public Vector3 position;
    public Vector3 velocity;
    public bool isAlive;  // Session 4: Death/respawn state

    public PlayerSnapshot(uint playerId, Vector3 position, Vector3 velocity, bool isAlive)
    {
        this.playerId = playerId;
        this.position = position;
        this.velocity = velocity;
        this.isAlive = isAlive;
    }
}

/// <summary>
/// Connection request message sent from client to server
/// Size: 1 + 4 = 5 bytes (playerId assigned by client initially, server may reassign)
/// </summary>
public struct ConnectMessage
{
    public MessageType messageType;
    public uint requestedPlayerId;

    public ConnectMessage(uint requestedPlayerId)
    {
        this.messageType = MessageType.Connect;
        this.requestedPlayerId = requestedPlayerId;
    }
}

/// <summary>
/// Projectile spawn message sent from server to clients
/// Contains all data needed to spawn and simulate a projectile with arc trajectory
/// Size: 1 + 4 + 4 + 12 + 12 + 12 + 4 + 4 = 53 bytes
/// </summary>
public struct ProjectileSpawnMessage
{
    public MessageType messageType;
    public uint projectileId;       // Unique ID for this projectile
    public uint ownerId;            // Player who fired the projectile
    public Vector3 startPosition;   // Launch position
    public Vector3 velocity;        // Initial velocity vector (kept for direction reference)
    public Vector3 targetPosition;  // Where projectile will land
    public float arcHeight;         // Peak height of arc trajectory
    public float flightTime;        // Total flight duration in seconds

    public ProjectileSpawnMessage(uint projectileId, uint ownerId, Vector3 startPosition, Vector3 velocity, Vector3 targetPosition, float arcHeight, float flightTime)
    {
        this.messageType = MessageType.ProjectileSpawn;
        this.projectileId = projectileId;
        this.ownerId = ownerId;
        this.startPosition = startPosition;
        this.velocity = velocity;
        this.targetPosition = targetPosition;
        this.arcHeight = arcHeight;
        this.flightTime = flightTime;
    }
}

/// <summary>
/// Projectile hit message sent from server to clients
/// Contains hit event data for visual effects and projectile cleanup
/// Size: 1 + 4 + 4 + 12 = 21 bytes
/// </summary>
public struct ProjectileHitMessage
{
    public MessageType messageType;
    public uint projectileId;       // ID of projectile that hit
    public uint targetPlayerId;     // Player who was hit
    public Vector3 hitPosition;     // Position where collision occurred (for visual effects)

    public ProjectileHitMessage(uint projectileId, uint targetPlayerId, Vector3 hitPosition)
    {
        this.messageType = MessageType.ProjectileHit;
        this.projectileId = projectileId;
        this.targetPlayerId = targetPlayerId;
        this.hitPosition = hitPosition;
    }
}

/// <summary>
/// Player death message sent from server to clients
/// Indicates a player has died (from projectile hit or boundary violation)
/// Size: 1 + 4 + 12 = 17 bytes
/// </summary>
public struct PlayerDeathMessage
{
    public MessageType messageType;
    public uint playerId;           // ID of player who died
    public Vector3 deathPosition;   // Position where death occurred (for visual effects)

    public PlayerDeathMessage(uint playerId, Vector3 deathPosition)
    {
        this.messageType = MessageType.PlayerDeath;
        this.playerId = playerId;
        this.deathPosition = deathPosition;
    }
}

/// <summary>
/// Player respawn message sent from server to clients
/// Indicates a player has respawned after death timer
/// Size: 1 + 4 + 12 = 17 bytes
/// </summary>
public struct PlayerRespawnMessage
{
    public MessageType messageType;
    public uint playerId;           // ID of player who respawned
    public Vector3 respawnPosition; // Position where player respawned

    public PlayerRespawnMessage(uint playerId, Vector3 respawnPosition)
    {
        this.messageType = MessageType.PlayerRespawn;
        this.playerId = playerId;
        this.respawnPosition = respawnPosition;
    }
}

