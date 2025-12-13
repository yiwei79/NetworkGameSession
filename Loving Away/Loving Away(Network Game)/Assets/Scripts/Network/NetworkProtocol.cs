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
    ProjectileSpawn = 5
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
/// Size: 4 + 12 + 12 = 28 bytes per player
/// </summary>
public struct PlayerSnapshot
{
    public uint playerId;
    public Vector3 position;
    public Vector3 velocity;

    public PlayerSnapshot(uint playerId, Vector3 position, Vector3 velocity)
    {
        this.playerId = playerId;
        this.position = position;
        this.velocity = velocity;
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

