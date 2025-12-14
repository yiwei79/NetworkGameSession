using UnityEngine;
using System.IO;

/// <summary>
/// Binary serialization utilities for network messages
/// Uses BinaryWriter/BinaryReader for efficient byte[] conversion
/// </summary>
public static class Serializer
{
    #region ClientInputMessage Serialization

    /// <summary>
    /// Serializes a ClientInputMessage to byte array
    /// Format: [1 byte: type][4 bytes: playerId][4 bytes: sequenceNumber][4 bytes: moveX][4 bytes: moveY][1 byte: shootButton]
    /// Total: 18 bytes (FIX 3: Added sequenceNumber)
    /// </summary>
    public static byte[] SerializeClientInput(ClientInputMessage msg)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)msg.messageType);
                writer.Write(msg.playerId);
                writer.Write(msg.sequenceNumber); // FIX 3: Write sequence number
                writer.Write(msg.moveDirection.x);
                writer.Write(msg.moveDirection.y);
                writer.Write(msg.shootButton);

                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Deserializes byte array to ClientInputMessage
    /// </summary>
    public static ClientInputMessage DeserializeClientInput(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data))
        {
            using (BinaryReader reader = new BinaryReader(ms))
            {
                ClientInputMessage msg = new ClientInputMessage();
                msg.messageType = (MessageType)reader.ReadByte();
                msg.playerId = reader.ReadUInt32();
                msg.sequenceNumber = reader.ReadUInt32(); // FIX 3: Read sequence number
                float moveX = reader.ReadSingle();
                float moveY = reader.ReadSingle();
                msg.moveDirection = new Vector2(moveX, moveY);
                msg.shootButton = reader.ReadBoolean();

                return msg;
            }
        }
    }

    #endregion

    #region ServerStateUpdateMessage Serialization

    /// <summary>
    /// Serializes a ServerStateUpdateMessage to byte array
    /// Format: [1 byte: type][4 bytes: serverTime][1 byte: playerCount][PlayerSnapshot array]
    /// Total: 6 + (28 * playerCount) bytes
    /// </summary>
    public static byte[] SerializeServerState(ServerStateUpdateMessage msg)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)msg.messageType);
                writer.Write(msg.serverTime);
                writer.Write(msg.playerCount);
                
                // Serialize each player snapshot
                for (int i = 0; i < msg.playerCount; i++)
                {
                    SerializePlayerSnapshot(writer, msg.players[i]);
                }
                
                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Deserializes byte array to ServerStateUpdateMessage
    /// </summary>
    public static ServerStateUpdateMessage DeserializeServerState(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data))
        {
            using (BinaryReader reader = new BinaryReader(ms))
            {
                ServerStateUpdateMessage msg = new ServerStateUpdateMessage();
                msg.messageType = (MessageType)reader.ReadByte();
                msg.serverTime = reader.ReadSingle();
                msg.playerCount = reader.ReadByte();
                
                // Deserialize player snapshots
                msg.players = new PlayerSnapshot[msg.playerCount];
                for (int i = 0; i < msg.playerCount; i++)
                {
                    msg.players[i] = DeserializePlayerSnapshot(reader);
                }
                
                return msg;
            }
        }
    }

    #endregion

    #region PlayerSnapshot Serialization Helpers

    /// <summary>
    /// Serializes a PlayerSnapshot using an existing BinaryWriter
    /// Format: [4 bytes: playerId][12 bytes: position][12 bytes: velocity]
    /// Total: 28 bytes
    /// </summary>
    private static void SerializePlayerSnapshot(BinaryWriter writer, PlayerSnapshot snapshot)
    {
        writer.Write(snapshot.playerId);
        
        // Position (Vector3 = 3 floats)
        writer.Write(snapshot.position.x);
        writer.Write(snapshot.position.y);
        writer.Write(snapshot.position.z);
        
        // Velocity (Vector3 = 3 floats)
        writer.Write(snapshot.velocity.x);
        writer.Write(snapshot.velocity.y);
        writer.Write(snapshot.velocity.z);
    }

    /// <summary>
    /// Deserializes a PlayerSnapshot using an existing BinaryReader
    /// </summary>
    private static PlayerSnapshot DeserializePlayerSnapshot(BinaryReader reader)
    {
        PlayerSnapshot snapshot = new PlayerSnapshot();
        snapshot.playerId = reader.ReadUInt32();
        
        // Position
        float posX = reader.ReadSingle();
        float posY = reader.ReadSingle();
        float posZ = reader.ReadSingle();
        snapshot.position = new Vector3(posX, posY, posZ);
        
        // Velocity
        float velX = reader.ReadSingle();
        float velY = reader.ReadSingle();
        float velZ = reader.ReadSingle();
        snapshot.velocity = new Vector3(velX, velY, velZ);
        
        return snapshot;
    }

    #endregion

    #region ConnectMessage Serialization

    /// <summary>
    /// Serializes a ConnectMessage to byte array
    /// Format: [1 byte: type][4 bytes: playerId]
    /// Total: 5 bytes
    /// </summary>
    public static byte[] SerializeConnect(ConnectMessage msg)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)msg.messageType);
                writer.Write(msg.requestedPlayerId);
                
                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Deserializes byte array to ConnectMessage
    /// </summary>
    public static ConnectMessage DeserializeConnect(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data))
        {
            using (BinaryReader reader = new BinaryReader(ms))
            {
                ConnectMessage msg = new ConnectMessage();
                msg.messageType = (MessageType)reader.ReadByte();
                msg.requestedPlayerId = reader.ReadUInt32();
                
                return msg;
            }
        }
    }

    #endregion

    #region ProjectileSpawnMessage Serialization

    /// <summary>
    /// Serializes a ProjectileSpawnMessage to byte array
    /// Format: [1 byte: type][4 bytes: projectileId][4 bytes: ownerId][12 bytes: startPosition][12 bytes: velocity][12 bytes: targetPosition][4 bytes: arcHeight][4 bytes: flightTime]
    /// Total: 53 bytes
    /// </summary>
    public static byte[] SerializeProjectileSpawn(ProjectileSpawnMessage msg)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)msg.messageType);
                writer.Write(msg.projectileId);
                writer.Write(msg.ownerId);

                // Start position (Vector3 = 3 floats)
                writer.Write(msg.startPosition.x);
                writer.Write(msg.startPosition.y);
                writer.Write(msg.startPosition.z);

                // Velocity (Vector3 = 3 floats)
                writer.Write(msg.velocity.x);
                writer.Write(msg.velocity.y);
                writer.Write(msg.velocity.z);

                // Target position (Vector3 = 3 floats)
                writer.Write(msg.targetPosition.x);
                writer.Write(msg.targetPosition.y);
                writer.Write(msg.targetPosition.z);

                // Arc parameters
                writer.Write(msg.arcHeight);
                writer.Write(msg.flightTime);

                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Deserializes byte array to ProjectileSpawnMessage
    /// </summary>
    public static ProjectileSpawnMessage DeserializeProjectileSpawn(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data))
        {
            using (BinaryReader reader = new BinaryReader(ms))
            {
                ProjectileSpawnMessage msg = new ProjectileSpawnMessage();
                msg.messageType = (MessageType)reader.ReadByte();
                msg.projectileId = reader.ReadUInt32();
                msg.ownerId = reader.ReadUInt32();

                // Start position
                float startX = reader.ReadSingle();
                float startY = reader.ReadSingle();
                float startZ = reader.ReadSingle();
                msg.startPosition = new Vector3(startX, startY, startZ);

                // Velocity
                float velX = reader.ReadSingle();
                float velY = reader.ReadSingle();
                float velZ = reader.ReadSingle();
                msg.velocity = new Vector3(velX, velY, velZ);

                // Target position
                float targetX = reader.ReadSingle();
                float targetY = reader.ReadSingle();
                float targetZ = reader.ReadSingle();
                msg.targetPosition = new Vector3(targetX, targetY, targetZ);

                // Arc parameters
                msg.arcHeight = reader.ReadSingle();
                msg.flightTime = reader.ReadSingle();

                return msg;
            }
        }
    }

    #endregion

    #region ProjectileHitMessage Serialization

    /// <summary>
    /// Serializes a ProjectileHitMessage to byte array
    /// Format: [1 byte: type][4 bytes: projectileId][4 bytes: targetPlayerId][12 bytes: hitPosition]
    /// Total: 21 bytes
    /// </summary>
    public static byte[] SerializeProjectileHit(ProjectileHitMessage msg)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)msg.messageType);
                writer.Write(msg.projectileId);
                writer.Write(msg.targetPlayerId);

                // Hit position (Vector3 = 3 floats)
                writer.Write(msg.hitPosition.x);
                writer.Write(msg.hitPosition.y);
                writer.Write(msg.hitPosition.z);

                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Deserializes byte array to ProjectileHitMessage
    /// </summary>
    public static ProjectileHitMessage DeserializeProjectileHit(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data))
        {
            using (BinaryReader reader = new BinaryReader(ms))
            {
                ProjectileHitMessage msg = new ProjectileHitMessage();
                msg.messageType = (MessageType)reader.ReadByte();
                msg.projectileId = reader.ReadUInt32();
                msg.targetPlayerId = reader.ReadUInt32();

                // Hit position
                float hitX = reader.ReadSingle();
                float hitY = reader.ReadSingle();
                float hitZ = reader.ReadSingle();
                msg.hitPosition = new Vector3(hitX, hitY, hitZ);

                return msg;
            }
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Peeks at the message type without consuming the byte array
    /// Useful for routing messages to appropriate handlers
    /// </summary>
    public static MessageType PeekMessageType(byte[] data)
    {
        if (data == null || data.Length < 1)
        {
            UnityEngine.Debug.LogError("Cannot peek message type from null or empty data");
            return MessageType.ClientInput; // Default fallback
        }

        return (MessageType)data[0];
    }

    #endregion
}

