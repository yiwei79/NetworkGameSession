using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Collections.Generic;

// Challenge 2: Pokemon class for nested object serialization
[System.Serializable]
public class Pokemon
{
    public string name;
    public int level;
    public float health;

    public Pokemon(string name, int level, float health)
    {
        this.name = name;
        this.level = level;
        this.health = health;
    }

    public override string ToString()
    {
        return $"Pokemon(name={name}, level={level}, health={health})";
    }
}

// Challenge 1 & 2: DTO struct for network serialization
public struct GameStateDTO
{
    public int playerId;
    public float posX;
    public float posY;
    public float posZ;
    // Challenge 2: Changed from List<int> to List<Pokemon>
    public List<Pokemon> pokemons;

    public override string ToString()
    {
        string pokemonList = "";
        if (pokemons != null)
        {
            foreach (var pokemon in pokemons)
            {
                pokemonList += pokemon.ToString() + ", ";
            }
        }
        return $"GameStateDTO(playerId={playerId}, pos=({posX}, {posY}, {posZ}), pokemons=[{pokemonList}])";
    }
}

public class SerializationTest : MonoBehaviour
{
    Thread m_serverThread;
    Thread m_clientThread;

    private Socket serverSocket;
    private bool isRunning = true;

    void Start()
    {
        m_clientThread = new Thread(ClientProcess);
        m_serverThread = new Thread(ServerProcess);

        m_serverThread.Start();
    }

    // Challenge 1: Serialize GameStateDTO to byte array using BinaryWriter
    byte[] Serialize(GameStateDTO dto)
    {
        MemoryStream stream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(stream);

        try
        {
            // Write primitive fields
            writer.Write(dto.playerId);
            writer.Write(dto.posX);
            writer.Write(dto.posY);
            writer.Write(dto.posZ);

            // Challenge 2: Serialize List<Pokemon>
            // Write the count first, then each Pokemon
            if (dto.pokemons != null)
            {
                writer.Write(dto.pokemons.Count);
                foreach (var pokemon in dto.pokemons)
                {
                    writer.Write(pokemon.name);
                    writer.Write(pokemon.level);
                    writer.Write(pokemon.health);
                }
            }
            else
            {
                writer.Write(0); // No pokemons
            }

            byte[] result = stream.ToArray();
            UnityEngine.Debug.Log($"Serialized {result.Length} bytes");
            return result;
        }
        finally
        {
            writer.Close();
            stream.Close();
        }
    }

    // Challenge 1: Deserialize byte array back to GameStateDTO using BinaryReader
    GameStateDTO Deserialize(byte[] data)
    {
        MemoryStream stream = new MemoryStream(data);
        BinaryReader reader = new BinaryReader(stream);

        try
        {
            stream.Seek(0, SeekOrigin.Begin);

            GameStateDTO dto = new GameStateDTO();

            // Read primitive fields in the same order they were written
            dto.playerId = reader.ReadInt32();
            dto.posX = reader.ReadSingle();
            dto.posY = reader.ReadSingle();
            dto.posZ = reader.ReadSingle();

            // Challenge 2: Deserialize List<Pokemon>
            // Read count first, then reconstruct each Pokemon
            int pokemonCount = reader.ReadInt32();
            dto.pokemons = new List<Pokemon>();
            
            for (int i = 0; i < pokemonCount; i++)
            {
                string name = reader.ReadString();
                int level = reader.ReadInt32();
                float health = reader.ReadSingle();
                dto.pokemons.Add(new Pokemon(name, level, health));
            }

            UnityEngine.Debug.Log($"Deserialized: {dto.ToString()}");
            return dto;
        }
        finally
        {
            reader.Close();
            stream.Close();
        }
    }

    void ServerProcess()
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 9051);
        serverSocket.Bind(endPoint);
        serverSocket.Listen(10);
        UnityEngine.Debug.Log("Serialization Server is running on port 9051");

        m_clientThread.Start();
        Socket clientHandler = serverSocket.Accept();
        UnityEngine.Debug.Log("Client accepted");

        // Receive serialized data from client
        byte[] buffer = new byte[4096]; // Larger buffer for serialized data
        while (isRunning)
        {
            int bytesRead = clientHandler.Receive(buffer);

            if (bytesRead == 0)
            {
                UnityEngine.Debug.Log("Client disconnected");
                break;
            }

            UnityEngine.Debug.Log($"Server received {bytesRead} bytes");

            // Deserialize the received data
            byte[] receivedData = new byte[bytesRead];
            System.Array.Copy(buffer, receivedData, bytesRead);
            
            GameStateDTO receivedDTO = Deserialize(receivedData);
            UnityEngine.Debug.Log($"Server successfully deserialized: {receivedDTO.ToString()}");

            // Send acknowledgment back to client
            string response = "Server received and deserialized successfully!";
            byte[] responseBuffer = System.Text.Encoding.UTF8.GetBytes(response);
            clientHandler.Send(responseBuffer);

            Thread.Sleep(2000);
        }
    }

    void ClientProcess()
    {
        Thread.Sleep(1000);
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, 9051);
        
        try
        {
            // Connect to the server
            clientSocket.Connect(serverEndPoint);
            UnityEngine.Debug.Log("Client connected to server!");

            byte[] buffer = new byte[1024];
            int messageCount = 0;
            
            while (isRunning)
            {
                messageCount++;

                // Challenge 2: Create dummy GameStateDTO with Pokemon data
                GameStateDTO dto = new GameStateDTO();
                dto.playerId = 1000 + messageCount;
                dto.posX = 10.5f + messageCount;
                dto.posY = 20.3f + messageCount;
                dto.posZ = 30.7f + messageCount;
                
                // Add at least 2 Pokemon as required
                dto.pokemons = new List<Pokemon>();
                dto.pokemons.Add(new Pokemon("Pikachu", 25 + messageCount, 100.0f));
                dto.pokemons.Add(new Pokemon("Charizard", 50 + messageCount, 150.5f));
                dto.pokemons.Add(new Pokemon("Bulbasaur", 15 + messageCount, 80.3f));

                UnityEngine.Debug.Log($"Client sending: {dto.ToString()}");

                // Serialize and send
                byte[] serializedData = Serialize(dto);
                clientSocket.Send(serializedData);
                UnityEngine.Debug.Log($"Client sent {serializedData.Length} bytes");

                // Receive acknowledgment from server
                int bytesRead = clientSocket.Receive(buffer);
                if (bytesRead > 0)
                {
                    string receivedMessage = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    UnityEngine.Debug.Log($"Server response: {receivedMessage}");
                }

                Thread.Sleep(2000);
            }
        }
        catch (SocketException e)
        {
            UnityEngine.Debug.LogError($"Client connection failed: {e.Message}");
        }
        finally
        {
            clientSocket.Close();
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;

        if (serverSocket != null)
        {
            serverSocket.Close();
        }

        if (m_serverThread != null && m_serverThread.IsAlive)
        {
            m_serverThread.Abort();
        }

        if (m_clientThread != null && m_clientThread.IsAlive)
        {
            m_clientThread.Abort();
        }
    }
}

