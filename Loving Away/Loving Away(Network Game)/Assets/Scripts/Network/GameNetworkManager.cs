using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// Main network manager handling UDP client/server communication
/// Manages server and client threads, message queues, and connection state
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    [Header("Network Configuration")]
    public bool isServer = false;
    public string serverAddress = "127.0.0.1";
    public int serverPort = 9050;
    
    [Header("Server Settings")]
    public float serverTickRate = 20f; // Hz (50ms per tick)
    
    [Header("Client Settings")]
    public float clientSendRate = 30f; // Hz (33ms per input)
    public uint localPlayerId = 0;

    [Header("Dual Local Player Testing")]
    public bool enableSecondLocalPlayer = false;
    public uint secondLocalPlayerId = 1;
    
    // Threading
    private Thread serverThread;
    private Thread clientThread;
    private bool isRunning = false;
    
    // Sockets
    private Socket serverSocket;
    private Socket clientSocket;
    
    // Server state
    private ServerGameState serverGameState;
    private List<EndPoint> connectedClients;
    
    // Thread-safe message queues (between worker threads and Unity main thread)
    private Queue<ClientInputMessage> incomingInputQueue;
    private Queue<ServerStateUpdateMessage> incomingStateQueue;
    private Queue<ClientInputMessage> outgoingInputQueue;
    private Queue<ProjectileSpawnMessage> incomingProjectileQueue;
    private Queue<ProjectileHitMessage> incomingHitQueue;
    private Queue<PlayerDeathMessage> incomingDeathQueue;
    private Queue<PlayerRespawnMessage> incomingRespawnQueue;

    // Locks for thread safety
    private object inputQueueLock = new object();
    private object stateQueueLock = new object();
    private object outgoingQueueLock = new object();
    private object projectileQueueLock = new object();
    private object hitQueueLock = new object();
    private object deathQueueLock = new object();
    private object respawnQueueLock = new object();
    
    // Connection tracking
    private Dictionary<string, uint> endpointToPlayerId;
    private uint nextPlayerId = 1;
    
    // Statistics
    private int packetsSent = 0;
    private int packetsReceived = 0;

    // FIX 3: Input sequence tracking
    private uint inputSequenceNumber = 0;
    
    // Timing for worker threads (thread-safe, not Unity Time)
    private Stopwatch serverStopwatch;
    private Stopwatch clientStopwatch;
    
    void Start()
    {
        // Initialize queues
        incomingInputQueue = new Queue<ClientInputMessage>();
        incomingStateQueue = new Queue<ServerStateUpdateMessage>();
        outgoingInputQueue = new Queue<ClientInputMessage>();
        incomingProjectileQueue = new Queue<ProjectileSpawnMessage>();
        incomingHitQueue = new Queue<ProjectileHitMessage>();
        incomingDeathQueue = new Queue<PlayerDeathMessage>();
        incomingRespawnQueue = new Queue<PlayerRespawnMessage>();
        connectedClients = new List<EndPoint>();
        endpointToPlayerId = new Dictionary<string, uint>();
        
        // Start network threads
        isRunning = true;
        
        // Initialize stopwatches for worker thread timing
        serverStopwatch = new Stopwatch();
        clientStopwatch = new Stopwatch();
        
        if (isServer)
        {
            serverGameState = new ServerGameState();
            // Add local player (ID 0) when server starts
            serverGameState.AddPlayer(localPlayerId);
            UnityEngine.Debug.Log($"[GameNetworkManager] Started as SERVER with local player {localPlayerId}");

            // Add second local player if enabled (for testing)
            if (enableSecondLocalPlayer)
            {
                serverGameState.AddPlayer(secondLocalPlayerId);
                UnityEngine.Debug.Log($"[GameNetworkManager] Added second local player {secondLocalPlayerId} for testing");
            }

            serverThread = new Thread(ServerProcess);
            serverThread.Start();
            UnityEngine.Debug.Log("[GameNetworkManager] Started as SERVER");
        }
        
        // Always start client thread (server also acts as a client for local player)
        clientThread = new Thread(ClientProcess);
        clientThread.Start();
            UnityEngine.Debug.Log("[GameNetworkManager] Started CLIENT thread");
    }
    
    void Update()
    {
        if (isServer)
        {
            UpdateServer();
        }
        
        UpdateClient();
    }
    
    #region Server Logic
    
    void ServerProcess()
    {
        try
        {
            // Create and bind UDP server socket
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
            serverSocket.Bind(endPoint);
            UnityEngine.Debug.Log($"[Server] UDP Server listening on port {serverPort}");
            
            // Receive buffer
            byte[] buffer = new byte[1024];
            EndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
            
            // Start stopwatch for server timing (thread-safe)
            serverStopwatch.Start();
            
            double tickInterval = 1000.0 / serverTickRate; // Convert to milliseconds
            double lastTickTime = 0;
            
            while (isRunning)
            {
                // Non-blocking receive
                if (serverSocket.Available > 0)
                {
                    int bytesRead = serverSocket.ReceiveFrom(buffer, ref remoteEndpoint);
                    
                    if (bytesRead > 0)
                    {
                        packetsReceived++;
                        HandleServerReceive(buffer, bytesRead, remoteEndpoint);
                    }
                }
                
                // Server tick - broadcast state at fixed rate
                double currentTime = serverStopwatch.ElapsedMilliseconds;
                double deltaTime = currentTime - lastTickTime;
                
                if (deltaTime >= tickInterval)
                {
                    // Cap deltaTime to prevent huge jumps (max 100ms = 5x normal tick)
                    float deltaTimeSeconds = (float)(deltaTime / 1000.0);
                    float clampedDeltaTime = Mathf.Min(deltaTimeSeconds, 0.1f);
                    ServerTick(clampedDeltaTime);
                    BroadcastState();
                    lastTickTime = currentTime;
                }
                
                Thread.Sleep(10); // Small sleep to prevent CPU spinning
            }
        }
        catch (SocketException e)
        {
            UnityEngine.Debug.LogError($"[Server] Socket error: {e.Message}");
        }
        finally
        {
            if (serverSocket != null)
            {
                serverSocket.Close();
            }
        }
    }
    
    void HandleServerReceive(byte[] buffer, int bytesRead, EndPoint remoteEndpoint)
    {
        // Copy buffer to prevent overwriting
        byte[] data = new byte[bytesRead];
        System.Array.Copy(buffer, data, bytesRead);
        
        MessageType msgType = Serializer.PeekMessageType(data);
        
        switch (msgType)
        {
            case MessageType.Connect:
                HandleClientConnect(remoteEndpoint);
                break;
                
            case MessageType.ClientInput:
                ClientInputMessage input = Serializer.DeserializeClientInput(data);
                
                // Queue for main thread processing
                lock (inputQueueLock)
                {
                    incomingInputQueue.Enqueue(input);
                }
                break;
        }
    }
    
    void HandleClientConnect(EndPoint remoteEndpoint)
    {
        string endpointKey = remoteEndpoint.ToString();
        
        if (!endpointToPlayerId.ContainsKey(endpointKey))
        {
            uint playerId = nextPlayerId++;
            endpointToPlayerId[endpointKey] = playerId;
            connectedClients.Add(remoteEndpoint);
            serverGameState.AddPlayer(playerId);
            
            UnityEngine.Debug.Log($"[Server] New client connected: {endpointKey} assigned ID {playerId}");
        }
    }
    
    void ServerTick(float deltaTime)
    {
        serverGameState.UpdateState(deltaTime);
    }
    
    void BroadcastState()
    {
        // Don't broadcast if no players in game state
        if (serverGameState.GetPlayerCount() == 0) return;

        PlayerSnapshot[] snapshots = serverGameState.GetPlayerSnapshots();
        ServerStateUpdateMessage stateMsg = new ServerStateUpdateMessage(
            serverGameState.GetServerTime(),
            snapshots
        );

        byte[] data = Serializer.SerializeServerState(stateMsg);

        // If we have connected remote clients, send via UDP
        if (connectedClients.Count > 0)
        {
            foreach (EndPoint client in connectedClients)
            {
                try
                {
                    serverSocket.SendTo(data, client);
                    packetsSent++;
                }
                catch (SocketException e)
                {
                    UnityEngine.Debug.LogError($"[Server] Failed to send to {client}: {e.Message}");
                }
            }
        }

        // Even if no remote clients, queue state for local client thread (when running as server+client)
        // This allows the local player to see their own state updates
        lock (stateQueueLock)
        {
            incomingStateQueue.Enqueue(stateMsg);
        }

        // Broadcast pending projectile spawns
        BroadcastProjectileSpawns();

        // Broadcast pending projectile hits
        BroadcastProjectileHits();

        // Broadcast pending player deaths (Session 4)
        BroadcastPlayerDeaths();

        // Broadcast pending player respawns (Session 4)
        BroadcastPlayerRespawns();
    }

    void BroadcastProjectileSpawns()
    {
        ProjectileSpawnMessage[] spawns = serverGameState.GetPendingProjectileSpawns();

        if (spawns.Length == 0) return;

        foreach (ProjectileSpawnMessage spawnMsg in spawns)
        {
            byte[] data = Serializer.SerializeProjectileSpawn(spawnMsg);

            // Send to remote clients
            if (connectedClients.Count > 0)
            {
                foreach (EndPoint client in connectedClients)
                {
                    try
                    {
                        serverSocket.SendTo(data, client);
                        packetsSent++;
                    }
                    catch (SocketException e)
                    {
                        UnityEngine.Debug.LogError($"[Server] Failed to send projectile spawn to {client}: {e.Message}");
                    }
                }
            }

            // Queue for local client (server also needs to see projectiles)
            lock (projectileQueueLock)
            {
                incomingProjectileQueue.Enqueue(spawnMsg);
            }
        }
    }

    void BroadcastProjectileHits()
    {
        ProjectileHitMessage[] hits = serverGameState.GetPendingHitMessages();

        if (hits.Length == 0) return;

        foreach (ProjectileHitMessage hitMsg in hits)
        {
            byte[] data = Serializer.SerializeProjectileHit(hitMsg);

            // Send to remote clients
            if (connectedClients.Count > 0)
            {
                foreach (EndPoint client in connectedClients)
                {
                    try
                    {
                        serverSocket.SendTo(data, client);
                        packetsSent++;
                    }
                    catch (SocketException e)
                    {
                        UnityEngine.Debug.LogError($"[Server] Failed to send projectile hit to {client}: {e.Message}");
                    }
                }
            }

            // Queue for local client (server also needs to see hits)
            lock (hitQueueLock)
            {
                incomingHitQueue.Enqueue(hitMsg);
            }
        }
    }

    void BroadcastPlayerDeaths()
    {
        PlayerDeathMessage[] deaths = serverGameState.GetPendingDeathMessages();

        if (deaths.Length == 0) return;

        foreach (PlayerDeathMessage deathMsg in deaths)
        {
            byte[] data = Serializer.SerializePlayerDeath(deathMsg);

            // Send to remote clients
            if (connectedClients.Count > 0)
            {
                foreach (EndPoint client in connectedClients)
                {
                    try
                    {
                        serverSocket.SendTo(data, client);
                        packetsSent++;
                    }
                    catch (SocketException e)
                    {
                        UnityEngine.Debug.LogError($"[Server] Failed to send player death to {client}: {e.Message}");
                    }
                }
            }

            // Queue for local client (server also needs to see deaths)
            lock (deathQueueLock)
            {
                incomingDeathQueue.Enqueue(deathMsg);
            }
        }
    }

    void BroadcastPlayerRespawns()
    {
        PlayerRespawnMessage[] respawns = serverGameState.GetPendingRespawnMessages();

        if (respawns.Length == 0) return;

        foreach (PlayerRespawnMessage respawnMsg in respawns)
        {
            byte[] data = Serializer.SerializePlayerRespawn(respawnMsg);

            // Send to remote clients
            if (connectedClients.Count > 0)
            {
                foreach (EndPoint client in connectedClients)
                {
                    try
                    {
                        serverSocket.SendTo(data, client);
                        packetsSent++;
                    }
                    catch (SocketException e)
                    {
                        UnityEngine.Debug.LogError($"[Server] Failed to send player respawn to {client}: {e.Message}");
                    }
                }
            }

            // Queue for local client (server also needs to see respawns)
            lock (respawnQueueLock)
            {
                incomingRespawnQueue.Enqueue(respawnMsg);
            }
        }
    }

    void UpdateServer()
    {
        // Process incoming input messages on main thread
        lock (inputQueueLock)
        {
            while (incomingInputQueue.Count > 0)
            {
                ClientInputMessage input = incomingInputQueue.Dequeue();
                serverGameState.ProcessInput(input);
            }
        }
    }
    
    #endregion
    
    #region Client Logic
    
    void ClientProcess()
    {
        Thread.Sleep(500); // Wait for server to start if local
        
        try
        {
            // Create UDP client socket
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(serverAddress), serverPort);
            
            UnityEngine.Debug.Log($"[Client] Connecting to server at {serverAddress}:{serverPort}");
            
            // Send connection request
            ConnectMessage connectMsg = new ConnectMessage(localPlayerId);
            byte[] connectData = Serializer.SerializeConnect(connectMsg);
            clientSocket.SendTo(connectData, serverEndpoint);
            UnityEngine.Debug.Log("[Client] Sent connection request");
            
            // Receive buffer
            byte[] buffer = new byte[1024];
            EndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
            
            // Start stopwatch for client timing (thread-safe)
            clientStopwatch.Start();
            
            double sendInterval = 1000.0 / clientSendRate; // Convert to milliseconds
            double lastSendTime = 0;
            
            while (isRunning)
            {
                // Receive incoming state updates
                if (clientSocket.Available > 0)
                {
                    int bytesRead = clientSocket.ReceiveFrom(buffer, ref remoteEndpoint);
                    
                    if (bytesRead > 0)
                    {
                        packetsReceived++;
                        HandleClientReceive(buffer, bytesRead);
                    }
                }
                
                // Send queued input messages
                double currentTime = clientStopwatch.ElapsedMilliseconds;
                double deltaTime = currentTime - lastSendTime;
                
                if (deltaTime >= sendInterval)
                {
                    SendQueuedInputs(serverEndpoint);
                    lastSendTime = currentTime;
                }
                
                Thread.Sleep(10); // Small sleep to prevent CPU spinning
            }
        }
        catch (SocketException e)
        {
            UnityEngine.Debug.LogError($"[Client] Socket error: {e.Message}");
        }
        finally
        {
            if (clientSocket != null)
            {
                clientSocket.Close();
            }
        }
    }
    
    void HandleClientReceive(byte[] buffer, int bytesRead)
    {
        // Copy buffer to prevent overwriting
        byte[] data = new byte[bytesRead];
        System.Array.Copy(buffer, data, bytesRead);

        MessageType msgType = Serializer.PeekMessageType(data);

        switch (msgType)
        {
            case MessageType.ServerStateUpdate:
                ServerStateUpdateMessage stateMsg = Serializer.DeserializeServerState(data);

                // Queue for main thread processing
                lock (stateQueueLock)
                {
                    incomingStateQueue.Enqueue(stateMsg);
                }
                break;

            case MessageType.ProjectileSpawn:
                ProjectileSpawnMessage projectileMsg = Serializer.DeserializeProjectileSpawn(data);

                // Queue for main thread processing
                lock (projectileQueueLock)
                {
                    incomingProjectileQueue.Enqueue(projectileMsg);
                }
                break;

            case MessageType.ProjectileHit:
                ProjectileHitMessage hitMsg = Serializer.DeserializeProjectileHit(data);

                // Queue for main thread processing
                lock (hitQueueLock)
                {
                    incomingHitQueue.Enqueue(hitMsg);
                }
                break;

            case MessageType.PlayerDeath:
                PlayerDeathMessage deathMsg = Serializer.DeserializePlayerDeath(data);

                // Queue for main thread processing
                lock (deathQueueLock)
                {
                    incomingDeathQueue.Enqueue(deathMsg);
                }
                break;

            case MessageType.PlayerRespawn:
                PlayerRespawnMessage respawnMsg = Serializer.DeserializePlayerRespawn(data);

                // Queue for main thread processing
                lock (respawnQueueLock)
                {
                    incomingRespawnQueue.Enqueue(respawnMsg);
                }
                break;
        }
    }
    
    void SendQueuedInputs(EndPoint serverEndpoint)
    {
        lock (outgoingQueueLock)
        {
            while (outgoingInputQueue.Count > 0)
            {
                ClientInputMessage input = outgoingInputQueue.Dequeue();
                byte[] data = Serializer.SerializeClientInput(input);
                
                try
                {
                    clientSocket.SendTo(data, serverEndpoint);
                    packetsSent++;
                }
                catch (SocketException e)
                {
                    UnityEngine.Debug.LogError($"[Client] Failed to send input: {e.Message}");
                }
            }
        }
    }
    
    void UpdateClient()
    {
        // Process incoming state updates on main thread
        lock (stateQueueLock)
        {
            while (incomingStateQueue.Count > 0)
            {
                ServerStateUpdateMessage stateMsg = incomingStateQueue.Dequeue();
                // Notify listeners (SimplePlayerController will handle this)
                BroadcastStateUpdate(stateMsg);
            }
        }

        // Process incoming projectile spawns on main thread
        lock (projectileQueueLock)
        {
            while (incomingProjectileQueue.Count > 0)
            {
                ProjectileSpawnMessage projectileMsg = incomingProjectileQueue.Dequeue();
                // Notify listeners (SimplePlayerController will handle this)
                BroadcastProjectileSpawn(projectileMsg);
            }
        }

        // Process incoming projectile hits on main thread
        lock (hitQueueLock)
        {
            while (incomingHitQueue.Count > 0)
            {
                ProjectileHitMessage hitMsg = incomingHitQueue.Dequeue();
                // Notify listeners (SimplePlayerController will handle this)
                BroadcastProjectileHit(hitMsg);
            }
        }

        // Process incoming player deaths on main thread (Session 4)
        lock (deathQueueLock)
        {
            while (incomingDeathQueue.Count > 0)
            {
                PlayerDeathMessage deathMsg = incomingDeathQueue.Dequeue();
                // Notify listeners (SimplePlayerController will handle this)
                BroadcastPlayerDeath(deathMsg);
            }
        }

        // Process incoming player respawns on main thread (Session 4)
        lock (respawnQueueLock)
        {
            while (incomingRespawnQueue.Count > 0)
            {
                PlayerRespawnMessage respawnMsg = incomingRespawnQueue.Dequeue();
                // Notify listeners (SimplePlayerController will handle this)
                BroadcastPlayerRespawn(respawnMsg);
            }
        }
    }

    #endregion
    
    #region Public API
    
    /// <summary>
    /// Sends client input to the server (called from SimplePlayerController)
    /// Phase 2: Added chargeValue parameter for charge-to-shoot mechanic
    /// </summary>
    public void SendInput(Vector2 moveDirection, bool shootButton, float chargeValue)
    {
        SendInputForPlayer(localPlayerId, moveDirection, shootButton, chargeValue);
    }

    /// <summary>
    /// Sends client input for a specific player to the server
    /// Used for dual local player testing mode
    /// Phase 2: Added chargeValue parameter for charge-to-shoot mechanic
    /// </summary>
    public void SendInputForPlayer(uint playerId, Vector2 moveDirection, bool shootButton, float chargeValue)
    {
        // FIX 3: Assign and increment sequence number
        uint currentSequence = inputSequenceNumber++;

        ClientInputMessage input = new ClientInputMessage(playerId, currentSequence, moveDirection, shootButton, chargeValue);

        lock (outgoingQueueLock)
        {
            outgoingInputQueue.Enqueue(input);
        }
    }
    
    /// <summary>
    /// Event for state updates received from server
    /// </summary>
    public delegate void StateUpdateHandler(ServerStateUpdateMessage stateMsg);
    public event StateUpdateHandler OnStateUpdate;

    private void BroadcastStateUpdate(ServerStateUpdateMessage stateMsg)
    {
        OnStateUpdate?.Invoke(stateMsg);
    }

    /// <summary>
    /// Event for projectile spawns received from server
    /// </summary>
    public delegate void ProjectileSpawnHandler(ProjectileSpawnMessage spawnMsg);
    public event ProjectileSpawnHandler OnProjectileSpawn;

    private void BroadcastProjectileSpawn(ProjectileSpawnMessage spawnMsg)
    {
        OnProjectileSpawn?.Invoke(spawnMsg);
    }

    /// <summary>
    /// Event for projectile hits received from server
    /// </summary>
    public delegate void ProjectileHitHandler(ProjectileHitMessage hitMsg);
    public event ProjectileHitHandler OnProjectileHit;

    private void BroadcastProjectileHit(ProjectileHitMessage hitMsg)
    {
        OnProjectileHit?.Invoke(hitMsg);
    }

    /// <summary>
    /// Event for player deaths received from server (Session 4)
    /// </summary>
    public delegate void PlayerDeathHandler(PlayerDeathMessage deathMsg);
    public event PlayerDeathHandler OnPlayerDeath;

    private void BroadcastPlayerDeath(PlayerDeathMessage deathMsg)
    {
        OnPlayerDeath?.Invoke(deathMsg);
    }

    /// <summary>
    /// Event for player respawns received from server (Session 4)
    /// </summary>
    public delegate void PlayerRespawnHandler(PlayerRespawnMessage respawnMsg);
    public event PlayerRespawnHandler OnPlayerRespawn;

    private void BroadcastPlayerRespawn(PlayerRespawnMessage respawnMsg)
    {
        OnPlayerRespawn?.Invoke(respawnMsg);
    }

    /// <summary>
    /// Gets network statistics for debug display
    /// FIX 3: Now includes sequence number
    /// </summary>
    public void GetNetworkStats(out int sent, out int received, out uint sequence)
    {
        sent = packetsSent;
        received = packetsReceived;
        sequence = inputSequenceNumber; // FIX 3: Return current sequence number
    }

    /// <summary>
    /// Backwards compatibility overload (for existing code that doesn't need sequence)
    /// </summary>
    public void GetNetworkStats(out int sent, out int received)
    {
        sent = packetsSent;
        received = packetsReceived;
    }
    
    #endregion
    
    void OnApplicationQuit()
    {
        isRunning = false;
        
        if (serverSocket != null)
        {
            serverSocket.Close();
        }
        
        if (clientSocket != null)
        {
            clientSocket.Close();
        }
        
        if (serverThread != null && serverThread.IsAlive)
        {
            serverThread.Join(1000); // Wait up to 1 second
        }
        
        if (clientThread != null && clientThread.IsAlive)
        {
            clientThread.Join(1000);
        }
        
        UnityEngine.Debug.Log("[GameNetworkManager] Shutdown complete");
    }
}

