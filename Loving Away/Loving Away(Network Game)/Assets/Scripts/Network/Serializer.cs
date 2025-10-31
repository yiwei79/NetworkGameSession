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
    /// Format: [1 byte: type][4 bytes: playerId][4 bytes: moveX][4 bytes: moveY][1 byte: shootButton]
    /// Total: 14 bytes
    /// </summary>
    public static byte[] SerializeClientInput(ClientInputMessage msg)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((byte)msg.messageType);
                writer.Write(msg.playerId);
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

    #region Utility Methods

    /// <summary>
    /// Peeks at the message type without consuming the byte array
    /// Useful for routing messages to appropriate handlers
    /// </summary>
    public static MessageType PeekMessageType(byte[] data)
    {
        if (data == null || data.Length < 1)
        {
            Debug.LogError("Cannot peek message type from null or empty data");
            return MessageType.ClientInput; // Default fallback
        }
        
        return (MessageType)data[0];
    }

    #endregion
}

