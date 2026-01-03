# Loving Away 🎮

A multiplayer physics-based arena shooter built from scratch to learn network game programming. Features custom UDP networking, client-side prediction, input reliability, and smooth interpolation—all implemented without using pre-built networking frameworks.

**Unity 6** | **Server-Authoritative** | **UDP Networking** | **2-4 Players**

---

## What is this?

Educational project demonstrating production-grade network game programming concepts:
- 🌐 Custom UDP client-server architecture
- ⚡ Client-side prediction with server reconciliation
- 🔄 Automatic input retransmission (0% loss at 30% packet loss)
- 🎯 60 FPS remote player interpolation
- 🎨 Cute chibi characters with charge-to-shoot combat

**Core Mechanic:** Hold Space for 0-2 seconds to charge your shot—longer charge = longer range, higher arc, faster projectile.

---

## Quick Start

### Play the Game

1. **Clone & Open in Unity 6**
   ```bash
   git clone https://github.com/yourusername/NetworkGameSession.git
   cd "Loving Away/Loving Away(Network Game)"
   ```
   Open in Unity Hub (version 6000.2.6f1)

2. **Run Local Test**
   - Press Play in Unity Editor
   - Choose "Host Server" in connection dialog
   - Controls: WASD (move), Space (charge/shoot), Tab (debug UI)

3. **Multiplayer Test**
   - Build executable (File → Build Settings)
   - Run Editor as server, Build as client
   - Enter server IP in connection dialog

---

## Key Features

### Gameplay
- **Charge-to-Shoot:** Variable range (5-20u), arc height (2-6u), speed (8-12 u/s)
- **Health System:** 5 HP, 1 damage per hit, color-coded health bars
- **Knockback Physics:** Projectiles push players toward arena boundary
- **Death & Respawn:** Instant death at boundary or 0 HP → 3 second respawn

### Networking (Labs 6-9)
- **UDP Sockets:** Custom implementation, 20 Hz server tick, 30 Hz client send
- **Binary Serialization:** Efficient protocol (22-82 bytes per message)
- **Piggybacked ACKs:** Input acknowledgments in state updates (Lab 8)
- **Input Redundancy:** Automatic retransmission within 100ms (Lab 8)
- **Interpolation:** Smooth 60 FPS rendering from 20 Hz snapshots (Lab 9)
- **Client Prediction:** Instant local response with blend reconciliation (Lab 9)

### Performance
- ✅ **0% input loss** at 30% packet loss (tested)
- ✅ **58-60 FPS** with 4 players
- ✅ **14 Kbps** bandwidth per client
- ✅ **<50ms** input latency

---

## Documentation

### For Quick Understanding
📄 **[Project Overview](Docs/Deliverable%205/PROJECT_OVERVIEW.md)** - 3-page summary with architecture, testing, and file guide

### For Detailed Technical Info
📄 **[Deliverable 5 Overview](Docs/Deliverable%205/DELIVERABLE_5_OVERVIEW.md)** - Full technical report (7,500 words)
📄 **[Session 6 Summary](Docs/Deliverable%205/SESSION_6_SUMMARY.md)** - Implementation notes for Labs 8-9

### For Development Context
📄 **[Project Status](Docs/Workflow/PROJECT_STATUS.md)** - Real-time progress tracking

---

## Project Structure

```
NetworkGameSession/
├── Loving Away/
│   └── Loving Away(Network Game)/    # Main Unity project
│       └── Assets/Scripts/
│           ├── Network/               # UDP, serialization, ACKs, interpolation
│           │   ├── NetworkProtocol.cs
│           │   ├── GameNetworkManager.cs
│           │   ├── InputHistoryBuffer.cs    # Lab 8: Retransmission
│           │   └── SnapshotBuffer.cs        # Lab 9: Interpolation
│           ├── Gameplay/              # Server logic, player control
│           │   ├── ServerGameState.cs       # Server-authoritative
│           │   └── SimplePlayerController.cs # Client prediction
│           └── UI/
│               └── ConnectionUI.cs          # IP/port dialog
└── Docs/
    ├── Deliverable 5/             # Final deliverable (Labs 8-9)
    └── Workflow/                  # Development tracking
```

---

## Technical Highlights

### Network Message Protocol
| Message | Size | Purpose |
|---------|------|---------|
| ClientInputMessage | 22 bytes | Player input + sequence# |
| ServerStateUpdateMessage | 6 + 38n bytes | Game state + ACKs |
| ProjectileSpawnMessage | 53 bytes | Trajectory parameters |

### Architecture
```
Client (30 Hz)  ──[Input + Seq#]──>  Server (20 Hz)
                                        ↓
                                   Processes inputs
                                   Updates physics
                                   Detects collisions
                                        ↓
Client (60 FPS) <──[State + ACKs]───  Broadcasts
    ↓
Predicts local player
Interpolates remote players
Renders @ 60 FPS
```

### Critical Files
- **Lab 8 (Reliability):** `InputHistoryBuffer.cs`, `ServerGameState.cs` (ACK tracking)
- **Lab 9 (Latency):** `SnapshotBuffer.cs`, `SimplePlayerController.cs` (interpolation)
- **Debug Tools:** `NetworkSimulator.cs` (packet loss testing), `ConnectionUI.cs`

---

## Testing Network Robustness

The game includes a built-in network simulator for testing:

1. **Enable Debug UI:** Press Tab in-game
2. **Toggle Network Simulator:** Check "Enable Network Simulation"
3. **Adjust Packet Loss:** Use slider (0-50%)
4. **Observe:** Retransmissions logged in console, gameplay stays smooth

**Expected behavior at 30% packet loss:**
- ~9 retransmissions per second
- 0% input loss (all inputs eventually delivered)
- Fully playable gameplay

---

## Development Journey

| Deliverable | Status | Key Features |
|-------------|--------|-------------|
| D3 (Labs 1-3) | ✅ Complete | UDP networking, binary serialization, threading |
| D4 (Lab 7) | ✅ Complete | Passive replication, full gameplay, client prediction |
| D5 (Labs 8-9) | ✅ Complete | ACK system, input redundancy, interpolation |

**Total Development Time:** ~40 hours across 6 sessions (Nov 2025 - Jan 2026)

---

## Gains

### Technical Skills
- UDP socket programming with thread-safe architecture
- Binary protocol design and serialization
- Client-side prediction and server reconciliation
- Reliability over unreliable protocols (ACKs, retransmission)
- Interpolation and latency hiding techniques

### Critical Debugging
- **Thread.Sleep() freeze:** Never block network threads
- **Retransmission spam:** Must track lastRetransmitTime to prevent loops
- **Input queue buildup:** Match client send rate to server capacity

### Key Takeaways
> "Retransmission is harder than it looks—you need careful timestamp management."
>
> "Interpolation makes a huge difference—remote players at 60 FPS vs 20 Hz is night and day."

---

## Resources

- [Gabriel Gambetta's Multiplayer Networking](https://www.gabrielgambetta.com/client-server-game-architecture.html)
- [Valve's Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)
- [Unity Documentation](https://docs.unity3d.com/)

---

## License

Educational project - MIT License

---