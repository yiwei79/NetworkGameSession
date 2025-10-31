# Loving Away - Technical Implementation Plan
**Multiplayer Physics-Based Arena Shooter**

This document serves as the complete technical reference for implementing the game. Use this as the source of truth for architecture decisions, implementation details, and mathematical formulas.

---

## Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Project Structure](#project-structure)
3. [Core Systems](#core-systems)
4. [Network Protocol](#network-protocol)
5. [Implementation Phases](#implementation-phases)
6. [Testing Strategy](#testing-strategy)
7. [Performance Targets](#performance-targets)

---

## Architecture Overview

### High-Level Design
```
Client-Server Architecture (Player-Hosted)
- One player acts as both client and server (host)
- Other players are clients only
- Server is authoritative for all game state
- Clients send inputs, receive state updates
```

### Design Principles
1. **Server Authority**: Server is the single source of truth
2. **Deterministic Simulation**: Same inputs always produce same outputs
3. **Minimal State**: Only sync what's necessary (positions, velocities)
4. **Client Prediction**: Local player predicts their own movement (optional phase 2)
5. **Interpolation**: Smooth visual representation between updates

### Technology Stack
- **Unity**: 6.2.6f2 LTS
- **Language**: C# (.NET Standard 2.1)
- **Networking**: System.Net.Sockets (TCP for lobby, UDP for gameplay)
- **Serialization**: Binary serialization (custom protocol)
- **Threading**: Separate network receive thread, Unity main thread for game logic

---

## Project Structure

### Folder Organization
```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs          // Main game loop coordinator
│   │   ├── NetworkManager.cs       // Network connection manager
│   │   └── InputManager.cs         // Input collection and buffering
│   ├── Gameplay/
│   │   ├── PlayerController.cs     // Local input and prediction
│   │   ├── PlayerState.cs          // Server-side player simulation
│   │   ├── Projectile.cs           // Projectile behavior and math
│   │   └── ArenaManager.cs         // Boundary checking
│   ├── Network/
│   │   ├── Server.cs               // Server game loop and state
│   │   ├── Client.cs               // Client network communication
│   │   ├── NetworkProtocol.cs      // Message definitions
│   │   └── Serializer.cs           // Binary serialization utilities
│   ├── UI/
│   │   ├── LobbyUI.cs              // Lobby interface
│   │   ├── GameUI.cs               // In-game HUD
│   │   └── ConnectionUI.cs         // Connection dialogs
│   └── Utilities/
│       ├── MathUtils.cs            // Vector math helpers
│       └── NetworkUtils.cs         // Network helper functions
├── Scenes/
│   ├── MainMenu.unity
│   ├── Lobby.unity
│   └── GameArena.unity
├── Prefabs/
│   ├── Player.prefab
│   ├── Projectile.prefab
│   └── NetworkManager.prefab
└── Materials/
    └── (Visual assets)
```

### Key Classes Hierarchy
```
GameManager (Singleton)
├── NetworkManager (Singleton)
│   ├── Server (if hosting)
│   │   └── ServerGameState
│   │       ├── List<PlayerState>
│   │       └── List<Projectile>
│   └── Client (always)
│       └── ClientGameState
│           ├── Dictionary<uint, PlayerController>
│           └── List<Projectile>
├── InputManager
└── ArenaManager
```

---

## Core Systems

### 1. Kinematic Character Movement System

#### PlayerState.cs (Server-Side)
```csharp
public class PlayerState {
    // Identity
    public uint playerId;
    public string playerName;
    
    // Transform
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 facingDirection;
    
    // Movement Parameters (tunable)
    public float maxSpeed = 5f;
    public float acceleration = 8f;
    public float deceleration = 12f;
    public float drag = 1.5f;
    public float turnSpeed = 10f;
    public float slideThreshold = 3f;
    
    // Shooting
    public float shootChargeTime = 0f;
    public float maxChargeTime = 2f;
    
    // State
    public bool isAlive = true;
    public float respawnTimer = 0f;
    
    public void Update(float deltaTime, Vector2 inputDirection, bool shootButton) {
        if (!isAlive) {
            // Handle respawn timer
            respawnTimer -= deltaTime;
            if (respawnTimer <= 0) {
                Respawn();
            }
            return;
        }
        
        // Movement
        UpdateMovement(deltaTime, inputDirection);
        
        // Shooting charge
        UpdateShooting(deltaTime, shootButton);
    }
    
    private void UpdateMovement(float deltaTime, Vector2 inputDirection) {
        Vector3 inputDir3D = new Vector3(inputDirection.x, 0, inputDirection.y);
        
        if (inputDirection.magnitude > 0.1f) {
            // Player is inputting direction
            Vector3 targetVelocity = inputDir3D.normalized * maxSpeed;
            
            // Check for sharp turns (creates slide effect)
            float currentSpeed = velocity.magnitude;
            if (currentSpeed > slideThreshold) {
                Vector3 currentDir = velocity.normalized;
                float directionDot = Vector3.Dot(currentDir, inputDir3D.normalized);
                
                // Reduce control during sharp turns
                if (directionDot < 0.5f) {
                    // Sliding - reduced acceleration
                    velocity = Vector3.MoveTowards(
                        velocity, 
                        targetVelocity, 
                        acceleration * 0.3f * deltaTime
                    );
                } else {
                    // Normal acceleration
                    velocity = Vector3.MoveTowards(
                        velocity, 
                        targetVelocity, 
                        acceleration * deltaTime
                    );
                }
            } else {
                // Low speed - full control
                velocity = Vector3.MoveTowards(
                    velocity, 
                    targetVelocity, 
                    acceleration * deltaTime
                );
            }
            
            // Update facing direction (gradual turn)
            facingDirection = Vector3.Slerp(
                facingDirection, 
                inputDir3D.normalized, 
                turnSpeed * deltaTime
            );
        } else {
            // No input - decelerate
            velocity = Vector3.MoveTowards(
                velocity, 
                Vector3.zero, 
                deceleration * deltaTime
            );
        }
        
        // Apply drag
        velocity *= (1f - drag * deltaTime);
        
        // Update position
        position += velocity * deltaTime;
    }
    
    private void UpdateShooting(float deltaTime, bool shootButton) {
        if (shootButton && shootChargeTime < maxChargeTime) {
            shootChargeTime += deltaTime;
        }
    }
    
    public void ApplyKnockback(Vector3 direction, float force) {
        velocity += direction.normalized * force;
        
        // Cap velocity
        if (velocity.magnitude > maxSpeed * 2f) {
            velocity = velocity.normalized * maxSpeed * 2f;
        }
    }
    
    public void ApplyShootbackForce(float chargeAmount) {
        float pushbackForce = 2f + (chargeAmount * 3f);
        velocity += -facingDirection * pushbackForce;
    }
    
    public void Die() {
        isAlive = false;
        respawnTimer = 3f;
        velocity = Vector3.zero;
    }
    
    public void Respawn() {
        isAlive = true;
        position = GetRandomSpawnPoint();
        velocity = Vector3.zero;
        shootChargeTime = 0f;
    }
}
```

#### Movement Tuning Parameters
```csharp
// Recommended starting values
public class MovementPresets {
    // Default (balanced)
    public static readonly MovementParams Default = new MovementParams {
        maxSpeed = 5f,
        acceleration = 8f,
        deceleration = 12f,
        drag = 1.5f,
        turnSpeed = 10f,
        slideThreshold = 3f
    };
    
    // Heavy (like Animal Party)
    public static readonly MovementParams Heavy = new MovementParams {
        maxSpeed = 4.5f,
        acceleration = 6f,
        deceleration = 9f,
        drag = 1.2f,
        turnSpeed = 8f,
        slideThreshold = 2.5f
    };
    
    // Light (responsive)
    public static readonly MovementParams Light = new MovementParams {
        maxSpeed = 6f,
        acceleration = 12f,
        deceleration = 15f,
        drag = 2f,
        turnSpeed = 15f,
        slideThreshold = 4f
    };
}
```

### 2. Projectile System

#### Projectile.cs
```csharp
public class Projectile {
    public uint projectileId;
    public uint ownerId;
    
    // Trajectory parameters (immutable after creation)
    public Vector3 startPosition;
    public Vector3 endPosition;
    public float height;
    public float duration = 1.0f;
    
    // Timing
    public float spawnTime;  // Server timestamp
    public float timeAlive;
    
    // Hit detection
    public float hitRadius = 0.5f;
    public float knockbackForce = 8f;
    public bool hasHit = false;
    
    public static Projectile Create(
        uint id, 
        uint owner, 
        Vector3 start, 
        Vector3 shootDir, 
        float chargeAmount, 
        float serverTime
    ) {
        Projectile proj = new Projectile();
        proj.projectileId = id;
        proj.ownerId = owner;
        proj.startPosition = start;
        proj.spawnTime = serverTime;
        proj.timeAlive = 0f;
        
        // Calculate trajectory based on charge
        float minDistance = 3f;
        float maxDistance = 12f;
        float distance = minDistance + (maxDistance - minDistance) * chargeAmount;
        
        proj.endPosition = start + shootDir * distance;
        
        // Height also scales with charge
        float minHeight = 1f;
        float maxHeight = 4f;
        proj.height = minHeight + (maxHeight - minHeight) * chargeAmount;
        
        return proj;
    }
    
    public Vector3 GetPosition(float currentTime) {
        float t = (currentTime - spawnTime) / duration;
        t = Mathf.Clamp01(t);
        
        // Horizontal interpolation (XZ plane)
        Vector3 horizontalPos = Vector3.Lerp(startPosition, endPosition, t);
        
        // Vertical arc (parabola: peaks at t=0.5)
        float verticalOffset = 4f * height * t * (1f - t);
        
        return new Vector3(horizontalPos.x, verticalOffset, horizontalPos.z);
    }
    
    public bool IsAlive(float currentTime) {
        return (currentTime - spawnTime) < duration && !hasHit;
    }
    
    public bool CheckHit(Vector3 targetPosition, float currentTime) {
        Vector3 projPos = GetPosition(currentTime);
        
        // Check if projectile is at ground level (about to land)
        if (projPos.y > 0.3f) return false;
        
        // 2D distance check on ground plane
        float distance = Vector2.Distance(
            new Vector2(projPos.x, projPos.z),
            new Vector2(targetPosition.x, targetPosition.z)
        );
        
        return distance < hitRadius;
    }
}
```

#### Projectile Math Formulas
```
Parametric Arc Equation:
- t = normalized time [0, 1]
- P(t) = position at time t

Horizontal (XZ plane):
  P_xz(t) = lerp(start_xz, end_xz, t)
  
Vertical (Y axis):
  P_y(t) = 4 * height * t * (1 - t)
  
Properties:
  - P_y(0) = 0 (starts at ground)
  - P_y(0.5) = height (peaks at midpoint)
  - P_y(1) = 0 (lands at ground)
  - Symmetric parabola
```

### 3. Arena Boundary System

#### ArenaManager.cs
```csharp
public class ArenaManager {
    // Arena bounds
    public Vector3 arenaCenter = Vector3.zero;
    public float arenaRadius = 15f;
    
    // Danger zone
    public float dangerZoneWidth = 2f;
    public float damagePerSecond = 20f;
    
    public bool IsInsideArena(Vector3 position) {
        float distanceFromCenter = Vector3.Distance(
            new Vector3(position.x, 0, position.z),
            new Vector3(arenaCenter.x, 0, arenaCenter.z)
        );
        return distanceFromCenter <= arenaRadius;
    }
    
    public bool IsInDangerZone(Vector3 position) {
        float distanceFromCenter = Vector3.Distance(
            new Vector3(position.x, 0, position.z),
            new Vector3(arenaCenter.x, 0, arenaCenter.z)
        );
        return distanceFromCenter > (arenaRadius - dangerZoneWidth) 
            && distanceFromCenter <= arenaRadius;
    }
    
    public void ApplyDangerZoneDamage(PlayerState player, float deltaTime) {
        if (IsInDangerZone(player.position)) {
            // No health system - just push back or kill after time
            player.dangerZoneTime += deltaTime;
            
            if (player.dangerZoneTime > 2f) {
                player.Die();
            }
            
            // Push toward center
            Vector3 toCenter = (arenaCenter - player.position).normalized;
            player.ApplyKnockback(toCenter, 5f * deltaTime);
        } else {
            player.dangerZoneTime = 0f;
        }
    }
}
```

---

## Network Protocol

### Message Types
```csharp
public enum MessageType : byte {
    // Lobby Messages (TCP)
    LobbyJoinRequest = 1,
    LobbyJoinResponse = 2,
    LobbyPlayerList = 3,
    LobbyStartGame = 4,
    
    // Game Messages (UDP)
    ClientInput = 10,
    ServerStateUpdate = 11,
    ServerProjectileSpawn = 12,
    ServerPlayerHit = 13,
    ServerPlayerDeath = 14,
    
    // Connection
    Ping = 20,
    Pong = 21,
    Disconnect = 22
}
```

### Message Structures

#### ClientInput (Client -> Server)
```csharp
struct ClientInputMessage {
    MessageType type = MessageType.ClientInput;
    uint playerId;
    uint sequence;           // Input sequence number
    float timestamp;         // Client timestamp
    Vector2 moveDirection;   // Normalized input vector
    bool shootButton;        // Is shoot button held
    bool shootReleased;      // Did shoot button get released this frame
}

// Binary format (18 bytes):
// [1 byte: type][4 bytes: playerId][4 bytes: sequence][4 bytes: timestamp]
// [4 bytes: moveX][4 bytes: moveY][1 byte: buttons]
```

#### ServerStateUpdate (Server -> All Clients)
```csharp
struct ServerStateUpdateMessage {
    MessageType type = MessageType.ServerStateUpdate;
    uint sequence;               // Server tick number
    float serverTime;            // Authoritative server time
    byte playerCount;
    PlayerSnapshot[] players;    // Array of player states
}

struct PlayerSnapshot {
    uint playerId;
    Vector3 position;
    Vector3 velocity;
    Vector3 facingDirection;
    byte flags;  // Bit flags: alive, charging, etc.
}

// Binary format (variable size):
// [1 byte: type][4 bytes: sequence][4 bytes: serverTime][1 byte: playerCount]
// For each player: [4 + 12 + 12 + 12 + 1 = 41 bytes]
// Total for 4 players: 9 + (41 * 4) = 173 bytes
```

#### ServerProjectileSpawn (Server -> All Clients)
```csharp
struct ProjectileSpawnMessage {
    MessageType type = MessageType.ServerProjectileSpawn;
    uint projectileId;
    uint ownerId;
    Vector3 startPos;
    Vector3 endPos;
    float height;
    float spawnTime;
}

// Binary format (37 bytes):
// [1 + 4 + 4 + 12 + 12 + 4 + 4 = 37 bytes]
```

### Network Timing
```
Server Tickrate: 30 Hz (33.3ms per tick)
Client Send Rate: 60 Hz (16.7ms per input)
State Update Rate: 30 Hz (every tick)

Client Interpolation: 100ms delay (3 ticks)
  - Render state from 100ms ago
  - Smooth interpolation between buffered states
```

### Serialization Example
```csharp
public static class Serializer {
    public static byte[] SerializeClientInput(ClientInputMessage msg) {
        using (MemoryStream ms = new MemoryStream()) {
            using (BinaryWriter writer = new BinaryWriter(ms)) {
                writer.Write((byte)msg.type);
                writer.Write(msg.playerId);
                writer.Write(msg.sequence);
                writer.Write(msg.timestamp);
                writer.Write(msg.moveDirection.x);
                writer.Write(msg.moveDirection.y);
                
                byte buttons = 0;
                if (msg.shootButton) buttons |= 0x01;
                if (msg.shootReleased) buttons |= 0x02;
                writer.Write(buttons);
                
                return ms.ToArray();
            }
        }
    }
    
    public static ClientInputMessage DeserializeClientInput(byte[] data) {
        using (MemoryStream ms = new MemoryStream(data)) {
            using (BinaryReader reader = new BinaryReader(ms)) {
                ClientInputMessage msg = new ClientInputMessage();
                msg.type = (MessageType)reader.ReadByte();
                msg.playerId = reader.ReadUInt32();
                msg.sequence = reader.ReadUInt32();
                msg.timestamp = reader.ReadSingle();
                float moveX = reader.ReadSingle();
                float moveY = reader.ReadSingle();
                msg.moveDirection = new Vector2(moveX, moveY);
                
                byte buttons = reader.ReadByte();
                msg.shootButton = (buttons & 0x01) != 0;
                msg.shootReleased = (buttons & 0x02) != 0;
                
                return msg;
            }
        }
    }
}
```

---

## Implementation Phases

### Phase 1: Single-Player Foundation (Weeks 1-2)
**Goal**: Fully working single-player game with all mechanics

**Tasks**:
1. Set up Unity project with proper folder structure
2. Implement PlayerState with kinematic movement
3. Implement Projectile system with arc trajectory
4. Create basic arena with visual boundaries
5. Implement shooting with charge mechanic
6. Add knockback and shootback forces
7. Tune movement to feel "heavy" and satisfying
8. Add basic UI (charge indicator, simple HUD)
9. Implement arena boundaries and danger zone
10. Polish and bug-fix until perfect

**Success Criteria**:
- Can control character with WASD smoothly
- Movement feels weighted and momentum-based
- Can charge and shoot projectiles
- Projectiles follow proper arc
- Getting hit applies knockback
- Going out of bounds eliminates player
- No bugs or glitches

### Phase 2: Network Foundation (Weeks 3-4)
**Goal**: Basic client-server connection and player position sync

**Tasks**:
1. Implement NetworkManager singleton
2. Create Server class with UDP socket
3. Create Client class with UDP socket
4. Implement basic lobby system (TCP connection)
5. Implement ClientInput message serialization
6. Implement ServerStateUpdate message serialization
7. Server game loop at fixed tickrate (30 Hz)
8. Client sends inputs at 60 Hz
9. Server simulates PlayerState based on inputs
10. Server broadcasts state to all clients
11. Clients render received positions

**Success Criteria**:
- Two instances can connect over localhost
- Player movements sync between clients
- No major lag on localhost
- Basic lobby shows connected players

### Phase 3: Full Gameplay Sync (Weeks 5-6)
**Goal**: Complete gameplay working in multiplayer

**Tasks**:
1. Implement ProjectileSpawn message
2. Server creates projectiles on shoot release
3. Clients render projectiles from server data
4. Server performs hit detection
5. Server sends hit/knockback events
6. Clients apply received knockback
7. Implement player death and respawn
8. Sync arena boundaries
9. Add username display
10. Test and fix synchronization bugs

**Success Criteria**:
- Shooting works in multiplayer
- Hits register correctly
- Knockback feels consistent
- Players can eliminate each other
- Game is playable and fun
- **Ready for Mid-Term Demo**

### Phase 4: Optimization & Interpolation (Weeks 7-9)
**Goal**: Smooth gameplay with network compensation

**Tasks**:
1. Implement client-side interpolation
2. Add interpolation buffer (100ms delay)
3. Smooth rendering between state updates
4. Add basic lag compensation for shots
5. Implement disconnect handling
6. Add connection quality indicators
7. Optimize packet sizes
8. Test on actual LAN (not just localhost)
9. Profile and optimize performance

**Success Criteria**:
- Movement looks smooth despite 30Hz updates
- Game feels responsive on LAN
- Handles minor packet loss gracefully
- Shows connection quality to players

### Phase 5: Polish & Final Features (Weeks 10-12)
**Goal**: Complete, polished game ready for final demo

**Tasks**:
1. Add visual effects (shooting, impacts, movement)
2. Implement lobby chat (bonus)
3. Add sound effects
4. Create proper UI/UX
5. Add game modes (time limit, score limit)
6. Implement spectator mode for eliminated players
7. Add network statistics display (for debugging)
8. Extensive playtesting and bug fixes
9. Performance optimization
10. Final polish pass

**Success Criteria**:
- Game looks and feels polished
- All bonus features working
- No known bugs
- Fun to play
- **Ready for Final Demo**

---

## Testing Strategy

### Unit Testing
```csharp
// Test kinematic movement
[Test]
public void TestMovement_ZeroInput_Decelerates() {
    PlayerState player = new PlayerState();
    player.velocity = new Vector3(5, 0, 0);
    
    player.UpdateMovement(0.1f, Vector2.zero);
    
    Assert.Less(player.velocity.magnitude, 5f);
}

// Test projectile arc
[Test]
public void TestProjectile_ArcPeaksAtMidpoint() {
    Projectile proj = Projectile.Create(1, 1, Vector3.zero, Vector3.forward, 0.5f, 0f);
    
    Vector3 midPos = proj.GetPosition(0.5f);
    Vector3 startPos = proj.GetPosition(0f);
    Vector3 endPos = proj.GetPosition(1f);
    
    Assert.Greater(midPos.y, startPos.y);
    Assert.Greater(midPos.y, endPos.y);
}
```

### Integration Testing
1. **Localhost Testing**: Run server and 2-4 clients on same machine
2. **LAN Testing**: Test on actual network between computers
3. **Latency Testing**: Use network emulation tools to simulate lag
4. **Packet Loss Testing**: Simulate 1-5% packet loss
5. **Stress Testing**: Test with maximum players moving/shooting

### Performance Testing
- **Target Frame Rate**: 60 FPS on mid-range hardware
- **Network Usage**: < 10 KB/s per client at 30 Hz update rate
- **Memory**: < 500 MB total
- **CPU**: < 50% on single core

---

## Performance Targets

### Network Performance
```
Packet Size Targets:
- ClientInput: 18 bytes
- ServerStateUpdate (4 players): ~175 bytes
- ProjectileSpawn: 37 bytes

Bandwidth Usage (30 Hz updates):
- Per client sending: 18 bytes * 60 Hz = 1.08 KB/s
- Server sending (4 clients): 175 bytes * 30 Hz = 5.25 KB/s per client
- Total per client: ~6.3 KB/s
- Server total: ~25 KB/s
```

### Frame Budget (at 60 FPS)
```
Total: 16.67ms per frame

Breakdown:
- Input Collection: 0.5ms
- Network Send: 1ms
- Game Logic: 3ms
- Physics/Collision: 2ms
- Rendering: 8ms
- Buffer: 2.17ms
```

---

## Development Best Practices

### Code Style
- Use C# naming conventions
- Comment complex algorithms
- Use regions to organize large files
- Prefer composition over inheritance
- Keep classes focused (single responsibility)

### Git Workflow
```
Branches:
- main: Stable, working builds only
- develop: Active development
- feature/: Individual features

Commits:
- Commit frequently
- Write descriptive messages
- Never commit broken code to main
```

### Unity Best Practices
- Use prefabs for all game objects
- Separate logic from MonoBehaviours when possible
- Use ScriptableObjects for configuration
- Organize scenes clearly
- Use proper .gitignore for Unity

### Network Development Tips
1. Always test on localhost first
2. Add logging for all network messages
3. Visualize network state in-editor (Gizmos)
4. Test with artificial lag early
5. Keep state updates minimal
6. Validate all received data

---

## Debugging Tools

### Network Debug Display
```csharp
public class NetworkDebugUI : MonoBehaviour {
    // Show in-game
    void OnGUI() {
        GUILayout.Label($"Ping: {networkManager.ping}ms");
        GUILayout.Label($"Packet Loss: {networkManager.packetLoss}%");
        GUILayout.Label($"Server Tick: {serverTick}");
        GUILayout.Label($"Client Buffer: {interpolationBuffer.Count}");
    }
}
```

### Console Commands
```csharp
public class DebugCommands {
    [Command("simulate_lag")]
    public void SimulateLag(int ms) {
        // Add artificial delay to network
    }
    
    [Command("simulate_packet_loss")]
    public void SimulatePacketLoss(float percentage) {
        // Randomly drop packets
    }
    
    [Command("show_trajectories")]
    public void ShowTrajectories() {
        // Draw projectile paths with Gizmos
    }
}
```

---

## Known Challenges & Solutions

### Challenge 1: Players Desyncing
**Problem**: Server and client positions diverge over time
**Solution**: 
- Server is always authoritative
- Client renders interpolated past state
- Periodically snap client to server position if error > threshold

### Challenge 2: Projectiles Missing Due to Lag
**Problem**: Lag causes projectiles to fire from wrong position
**Solution**:
- Server rewinds player positions when checking hits
- Use player position from when they shot, not current position

### Challenge 3: Movement Feels Sluggish Online
**Problem**: 30Hz updates create choppy movement
**Solution**:
- Client-side interpolation between updates
- Render 100ms in the past for smooth interpolation
- Local player can use prediction (optional)

### Challenge 4: Shooting Doesn't Feel Responsive
**Problem**: Waiting for server confirmation feels slow
**Solution**:
- Client immediately shows projectile locally
- Server validates and corrects if needed
- Visual feedback (recoil, muzzle flash) happens instantly

---

## Configuration Files

### NetworkConfig.json
```json
{
  "serverTickRate": 30,
  "clientSendRate": 60,
  "interpolationDelay": 100,
  "maxPlayers": 4,
  "tcpPort": 7777,
  "udpPort": 7778,
  "timeout": 5000
}
```

### GameplayConfig.json
```json
{
  "movement": {
    "maxSpeed": 5.0,
    "acceleration": 8.0,
    "deceleration": 12.0,
    "drag": 1.5,
    "turnSpeed": 10.0,
    "slideThreshold": 3.0
  },
  "shooting": {
    "minDistance": 3.0,
    "maxDistance": 12.0,
    "minHeight": 1.0,
    "maxHeight": 4.0,
    "maxChargeTime": 2.0,
    "knockbackForce": 8.0,
    "shootbackMin": 2.0,
    "shootbackMax": 5.0
  },
  "arena": {
    "radius": 15.0,
    "dangerZoneWidth": 2.0,
    "killTime": 2.0
  }
}
```

---

## Next Steps

1. Review this entire document
2. Set up Unity project with folder structure
3. Start Phase 1: Single-player foundation
4. Reference this document for implementation details
5. Update this document if design changes

**Remember**: Build iteratively, test frequently, and keep the scope manageable. The goal is a working, fun multiplayer game that demonstrates network programming concepts, not a AAA production.
