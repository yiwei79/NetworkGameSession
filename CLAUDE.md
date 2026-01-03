# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Quick Start for New Sessions

```
/session-start
```

This command loads project context and shows current status. See [Agentic Workflow System](#agentic-workflow-system) below.

---

## Project Overview

**"Loving Away"** is a 2-4 player multiplayer physics-based arena shooter built as an educational project for learning network game programming. Players compete in a circular arena using a charge-and-shoot mechanic with momentum-based movement.

### Core Gameplay Features
- **Charge-to-Shoot:** Hold Space 0-2 seconds for variable range (5-20u), arc height (2-6u), and speed (8-12 u/s)
- **Health System:** 5 HP per player, 1 damage per projectile hit, color-coded health bars (green → yellow → red)
- **Knockback Physics:** Projectile hits apply knockback force (12 u/s), pushing players toward arena boundary
- **Boundary Instant Death:** Players die immediately when crossing 15u arena boundary (bypasses health system)
- **Chibi Character Design:** Big head (60%), small body (40%), cute proportions (1.5u tall)
- **Visual Feedback:** Charge indicators, muzzle flash, cooldown bars (0.5s), hit/death particle effects

| Attribute | Value |
|-----------|-------|
| Unity Version | 6000.2.6f1 (Unity 6) |
| Platform | macOS (Darwin) / Windows |
| Input System | New Input System (`UnityEngine.InputSystem`) |
| Network Model | Server-authoritative, UDP, passive replication |
| Max Players | 4 |
| Movement Speed | 3.5 u/s (heavier "Animal Party" feel) |
| Acceleration | 25 u/s² (momentum-based) |

### Current Status

> **See [PROJECT_STATUS.md](Docs/Workflow/PROJECT_STATUS.md) for real-time progress**

| Deliverable | Status | Notes |
|-------------|--------|-------|
| Deliverable 3 | ✅ Complete | Serialization + Input delay fixes |
| Deliverable 4 | ✅ Complete | World State Replication + Gameplay Features |
| **Deliverable 5** | ✅ Complete | Network Robustness (Labs 8-9) |

| Phase | Status | Progress |
|-------|--------|----------|
| Phase 1-2 | ✅ Complete | Movement, UDP networking |
| Phase 3 | ✅ Complete | Projectile system with arc trajectory, hit detection, health/damage |
| Phase 4 | ✅ Complete | Client prediction, dual local player, visual dressing |
| Phase 5 | ✅ Complete | Charge-to-shoot, chibi characters, cooldown indicators, game feel polish |
| Phase 6 | ✅ Complete | ACK system, input redundancy, interpolation (Labs 8-9) |

---

## Agentic Workflow System

This project uses a **hybrid system** combining:
1. **Slash Commands** (`.claude/commands/`) - Manual workflow triggers
2. **Official Claude Code Agents** (`.claude/agents/`) - Specialized AI experts

### Available Slash Commands

| Command | Purpose | When to Use |
|---------|---------|-------------|
| `/session-start` | Load context, show status | Start of any session |
| `/status` | Quick progress check | Anytime |
| `/plan` | Create session plan | Before implementation |
| `/implement` | Execute session plan | After plan approved |
| `/document` | Update documentation | After implementation |
| `/test` | Validate implementation | After implementation |
| `/session-end` | Back-propagate context | End of session |

### Available Agents

| Agent | Model | Purpose | Auto-Invoked When |
|-------|-------|---------|-------------------|
| `session-coordinator-opus` | Opus | Workflow orchestration | Session start/end, coordination needed |
| `planning-agent-opus` | Opus | Implementation planning | New features, architecture decisions |
| `implementation-agent-sonnet` | Sonnet | Code writing | Feature implementation, bug fixes |
| `testing-agent-sonnet` | Sonnet | Quality assurance | After implementation, debugging |
| `documentation-agent-sonnet` | Sonnet | Documentation updates | Session end, docs need updating |

**See `.claude/AGENTS_GUIDE.md` for detailed agent usage.**

### Workflow Patterns

**Traditional (Manual):**
```
/session-start  →  /plan  →  /implement  →  /test  →  /document  →  /session-end
```

**Agent-Driven (Automatic):**
```
"Start session" → "Implement X" → "Test it" → "Document changes" → "End session"
[Agents auto-delegate between planning/coding/testing/docs phases]
```

**Hybrid (Recommended):**
Mix manual slash commands with automatic agent delegation as needed.

### Back-Propagation Mechanism

Every `/session-end` updates:
1. `Docs/Workflow/PROJECT_STATUS.md` - Phase progress, next steps
2. `Docs/Deliverable X/SESSION_X_SUMMARY.md` - Session handoff document
3. This file (CLAUDE.md) - Only when new patterns established

**Result:** Any future session can run `/session-start` and immediately understand project state.

### Workflow Files Location

```
.claude/
├── commands/               # Slash command definitions
├── agents/                 # Official Claude Code agents ⭐ NEW
└── AGENTS_GUIDE.md         # How to use agents ⭐ NEW

Docs/Workflow/
├── PROJECT_STATUS.md       # Living status (updated each session)
├── CONTEXT_CHECKLIST.md    # What new sessions should read
```

---

## Project Structure

```
NetworkGameSession/
├── .claude/
│   ├── commands/                  # ⭐ SLASH COMMANDS
│   │   ├── session-start.md
│   │   ├── session-end.md
│   │   ├── plan.md
│   │   ├── implement.md
│   │   ├── document.md
│   │   ├── test.md
│   │   └── status.md
│   │
│   ├── agents/                    # ⭐ OFFICIAL CLAUDE CODE AGENTS
│   │   ├── session-coordinator-opus.md
│   │   ├── planning-agent-opus.md
│   │   ├── implementation-agent-sonnet.md
│   │   ├── testing-agent-sonnet.md
│   │   └── documentation-agent-sonnet.md
│   │
│   └── AGENTS_GUIDE.md            # Agent usage guide
│
├── Docs/
│   ├── Workflow/                  # ⭐ AGENTIC WORKFLOW
│   │   ├── PROJECT_STATUS.md      # Real-time progress
│   │   └── CONTEXT_CHECKLIST.md   # New session guide
│   │
│   ├── Deliverable 3/             # Serialization (COMPLETE)
│   │   ├── INPUT_DELAY_FIXES.md   # Phase 4 early work
│   │   └── [other docs]
│   │
│   ├── Deliverable 4/             # World State Replication (IN PROGRESS)
│   │   └── SESSION_1_SUMMARY.md   # Projectile foundation
│   │
│   ├── Final Project/
│   │   └── Technical_Implementation_Plan.md
│   │
│   └── Materials/                 # Course PDFs (Lab 6, 7, 8)
│
├── Loving Away/                   # ⭐ MAIN GAME PROJECT
│   └── Loving Away(Network Game)/
│       └── Assets/Scripts/
│           ├── Network/
│           │   ├── NetworkProtocol.cs     # Message structs
│           │   ├── Serializer.cs          # Binary serialization
│           │   └── GameNetworkManager.cs  # UDP networking
│           │
│           └── Gameplay/
│               ├── ServerGameState.cs         # Server logic (NOT MonoBehaviour)
│               ├── SimplePlayerController.cs  # Client input & rendering
│               ├── Projectile.cs              # Projectile behavior (NEW)
│               └── ShootVisualFeedback.cs     # Visual effects
│
└── CLAUDE.md                      # This file (master context)
```

---

## Critical Constraints

### Thread Safety (CRITICAL)

| Thread Type | Allowed | Forbidden |
|-------------|---------|-----------|
| Worker threads (`ServerProcess`, `ClientProcess`) | Socket ops, BinaryWriter, Queue with locks, pure C# | Unity API (`Instantiate`, `Destroy`, `transform`, `Time.deltaTime`) |
| Main thread (`Update`, event handlers) | Everything | - |

**Pattern:** Worker thread queues data → Main thread processes queue in `Update()`

### Namespace Conflicts

```csharp
using System.Diagnostics;  // Has Debug class - conflicts with Unity
// Always use:
UnityEngine.Debug.Log("message");  // NOT Debug.Log()
```

### Input System

```csharp
using UnityEngine.InputSystem;

// Correct:
var keyboard = Keyboard.current;
if (keyboard.wKey.isPressed) { }

// Wrong (old system):
// Input.GetKey(KeyCode.W)  // ❌ Will error
```

---

## Network Architecture

### Message Protocol

| Message | Size | Direction | Purpose |
|---------|------|-----------|---------|
| ClientInputMessage | 22 bytes | Client → Server | WASD + shoot + charge value + sequence |
| ServerStateUpdateMessage | 6 + 30n + 8n bytes | Server → Clients | Player positions + health + alive state + ACKs (Lab 8) |
| ProjectileSpawnMessage | 53 bytes | Server → Clients | Arc projectile creation with trajectory |
| ProjectileHitMessage | 23 bytes | Server → Clients | Hit notification with damage |
| PlayerDeathMessage | 17 bytes | Server → Clients | Death notification |
| PlayerRespawnMessage | 17 bytes | Server → Clients | Respawn notification |
| ConnectMessage | 5 bytes | Client → Server | Initial connection |

### Timing

| Parameter | Value | Notes |
|-----------|-------|-------|
| Server Tick Rate | 20 Hz | 50ms per tick |
| Client Send Rate | 30 Hz | Rate-limited (was 60Hz) |
| State Broadcast | 20 Hz | Every server tick |

### Data Flow

```
[PLAYER INPUT]
      ↓
SimplePlayerController.CollectInput()
      ↓
ClientInputMessage (18 bytes, UDP)
      ↓
GameNetworkManager.HandleServerReceive()
      ↓
ServerGameState.ProcessInput() + UpdateState()
      ↓
ServerStateUpdateMessage (62 bytes for 2 players)
      ↓
SimplePlayerController.HandleStateUpdate()
      ↓
[RENDER PLAYERS]
```

---

## Code Patterns

### Adding New Network Messages

1. **Protocol:** Add enum value to `NetworkProtocol.cs` → `MessageType`
2. **Struct:** Create struct with constructor setting `messageType`
3. **Serialization:** Add `Serialize/Deserialize` methods to `Serializer.cs`
4. **Handler:** Add case to `HandleServerReceive()` or `HandleClientReceive()`
5. **Event:** Add delegate + event if clients need to react

### Binary Serialization Pattern

```csharp
// Serialize
public static byte[] SerializeX(XMessage msg)
{
    using (MemoryStream ms = new MemoryStream())
    using (BinaryWriter writer = new BinaryWriter(ms))
    {
        writer.Write((byte)msg.messageType);
        writer.Write(msg.field1);
        // ... more fields
        return ms.ToArray();
    }
}

// Deserialize
public static XMessage DeserializeX(byte[] data)
{
    using (MemoryStream ms = new MemoryStream(data))
    using (BinaryReader reader = new BinaryReader(ms))
    {
        XMessage msg = new XMessage();
        msg.messageType = (MessageType)reader.ReadByte();
        msg.field1 = reader.ReadXXX();
        return msg;
    }
}
```

---

## Phase Implementation Details

### Phase 3: Projectile System ✅ COMPLETE

**Implemented:**
- ✅ ProjectileSpawnMessage protocol (53 bytes with arc data)
- ✅ Binary serialization for projectiles
- ✅ Server spawning (0.5s cooldown, facing-direction based, charge-based trajectory)
- ✅ Client rendering (arc trajectory with trail)
- ✅ Parametric arc trajectory (2-6u height, 5-20u range based on charge)
- ✅ Trail renderer (yellow → orange gradient)
- ✅ Hit detection (1.5u collision radius, matches chibi character size)
- ✅ Knockback system (12 u/s force away from projectile impact)
- ✅ Health system (5 HP, 1 damage per hit, death at 0 HP)
- ✅ Health bars (color-coded: green → yellow → red)

### Phase 4: Optimization ✅ COMPLETE

**Implemented:**
- ✅ Client-side prediction (local player, momentum-based)
- ✅ Sequence numbers (foundation for reconciliation)
- ✅ Input rate limiting (30Hz)
- ✅ Dual local player testing (P1: WASD+Space, P2: Arrows+RShift)
- ✅ Visual dressing (PlayerVisualController separation)
- ✅ Chibi character design (big head 60%, small body 40%)
- ✅ Dead player handling (stop prediction when dead)

**Deferred to Phase 5:**
- ⏸ Interpolation buffer (not needed for current scope)
- ⏸ Remote player interpolation (direct server position works well)
- ⏸ Server reconciliation (blending approach sufficient)
- ⏸ Lag compensation (charge-to-shoot reduces need)

### Phase 5: Gameplay Polish ✅ COMPLETE

**Session 5A - Visual Dressing:**
- ✅ PlayerVisualController (separates visuals from network logic)
- ✅ Chibi character proportions (1.5u tall, head 0.45 radius, body 0.35 radius)
- ✅ Color theming (body color, brighter head, white eye)

**Session 5B - Charge-to-Shoot:**
- ✅ Charge mechanic (hold 0-2s, scales range/arc/speed)
- ✅ Charge value persistence (captured at button release, sent at 30Hz)
- ✅ Server trajectory calculation (server-authoritative)
- ✅ Charge visual feedback (growing indicator, body tint)

**Session 5C - Cooldown Visual:**
- ✅ Client-side cooldown tracking (0.5s server cooldown)
- ✅ Cooldown bar UI (fills red→green over 0.5s)
- ✅ "Game sense" feedback (players can feel shooting rhythm)

**Session 5D - Game Feel Refinements:**
- ✅ Heavier physics (3.5 u/s movement, 25 u/s² acceleration)
- ✅ Slower projectiles (8-12 u/s instead of 12-18 u/s)
- ✅ Grounded characters (Y=0.0, no floating)
- ✅ Boundary instant death (removed client bounce, server-authoritative)
- ✅ Larger hit detection (1.5u radius for easier hits)
- ✅ Bug fixes (boundary death struct overwrite, projectile landing height)

### Phase 6: Network Robustness (Labs 8-9) ✅ COMPLETE

**Session 6 - ACK System & Interpolation:**
- ✅ Lab 8: Piggybacked ACK system (ACKs in ServerStateUpdateMessage)
- ✅ Lab 8: Input history buffer with retransmission (100ms timeout)
- ✅ Lab 8: Server-side deduplication (prevents duplicate input processing)
- ✅ Lab 9: Snapshot buffer for interpolation (3 snapshots, 100ms delay)
- ✅ Lab 9: Remote player interpolation (smooth 60 FPS rendering)
- ✅ Lab 9: Enhanced reconciliation (ACK-aware blend speed adjustment)
- ✅ Debug Tools: NetworkSimulator (packet loss simulation)
- ✅ Debug Tools: ConnectionUI (easy IP/port configuration)

**Critical Fixes:**
- ✅ Removed Thread.Sleep() latency simulation (caused game freeze)
- ✅ Fixed retransmission spam (Dictionary-based input history with lastRetransmitTime tracking)
- ✅ Added rate limiting to retransmission checks (50ms interval)

**New Files:**
- `InputHistoryBuffer.cs` - Stores sent inputs for retransmission with timeout tracking
- `SnapshotBuffer.cs` - Circular buffer for interpolating remote player positions
- `NetworkSimulator.cs` - Packet loss simulation for testing reliability
- `ConnectionUI.cs` - Simple OnGUI connection dialog for easy multiplayer playtesting

---

## Course Materials Integration

| Lab | Concepts | Applied In |
|-----|----------|------------|
| Lab 6 | ClientProxy pattern, Ping/Pong | GameNetworkManager connection |
| Lab 7 | Passive replication, ReplicationManager | ServerStateUpdate broadcast |
| Lab 8 | ACK system, input redundancy | Piggybacked ACKs, InputHistoryBuffer, retransmission |
| Lab 9 | Interpolation, reconciliation | SnapshotBuffer, interpolated remote players |

---

## Known Deviations from Original Plan

| Deviation | Reason | Impact |
|-----------|--------|--------|
| Client input 30Hz (not 60Hz) | Prevent server queue buildup | 50% bandwidth reduction |
| Phase 4 tasks done early | Needed for playable Deliverable 3 | Phase 3/4 interleaved |
| No Unity physics engine | Simpler network sync | Custom kinematic formulas |

---

## Quick Reference

### File Locations

| Component | File |
|-----------|------|
| Message structs | `Assets/Scripts/Network/NetworkProtocol.cs` |
| Serialization | `Assets/Scripts/Network/Serializer.cs` |
| Network I/O | `Assets/Scripts/Network/GameNetworkManager.cs` |
| Server logic | `Assets/Scripts/Gameplay/ServerGameState.cs` |
| Client logic | `Assets/Scripts/Gameplay/SimplePlayerController.cs` |
| Projectile | `Assets/Scripts/Gameplay/Projectile.cs` |
| Player visuals | `Assets/Scripts/Gameplay/PlayerVisualController.cs` |
| Charge/cooldown feedback | `Assets/Scripts/Gameplay/ShootVisualFeedback.cs` |
| Health bar UI | `Assets/Scripts/UI/PlayerHealthBar.cs` |
| Arena setup | `Assets/Scripts/Gameplay/ArenaSetup.cs` |
| **Lab 8: Input reliability** | `Assets/Scripts/Network/InputHistoryBuffer.cs` |
| **Lab 9: Interpolation** | `Assets/Scripts/Network/SnapshotBuffer.cs` |
| **Debug: Network sim** | `Assets/Scripts/Network/NetworkSimulator.cs` |
| **Debug: Connection UI** | `Assets/Scripts/UI/ConnectionUI.cs` |

### Testing

1. **Single player:** Unity Editor with `Is Server = true`
2. **Two players:** Editor (server) + Built executable (client)
3. **Full test guide:** `Docs/Deliverable 3/TESTING_GUIDE.md`

---

## Context for New Sessions

When starting a new Claude Code session:

1. Run `/session-start` (loads context automatically)
2. Or manually read:
   - `Docs/Workflow/PROJECT_STATUS.md` (current phase)
   - Latest `SESSION_X_SUMMARY.md` (previous session context)

**See:** `Docs/Workflow/CONTEXT_CHECKLIST.md` for full reading list.

---

*Last Updated: 2026-01-03 | Last Change: Completed Phase 6 / Deliverable 5 (Labs 8-9: ACK System, Input Redundancy, Interpolation)*
