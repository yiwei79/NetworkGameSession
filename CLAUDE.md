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

| Attribute | Value |
|-----------|-------|
| Unity Version | 6000.2.6f1 (Unity 6) |
| Platform | macOS (Darwin) / Windows |
| Input System | New Input System (`UnityEngine.InputSystem`) |
| Network Model | Server-authoritative, UDP, passive replication |
| Max Players | 4 |

### Current Status

> **See [PROJECT_STATUS.md](Docs/Workflow/PROJECT_STATUS.md) for real-time progress**

| Deliverable | Status | Notes |
|-------------|--------|-------|
| Deliverable 3 | ✅ Complete | Serialization + Input delay fixes |
| **Deliverable 4** | ⏳ In Progress | World State Replication |
| Deliverable 5 | ❌ Not Started | Final Demo |

| Phase | Status | Progress |
|-------|--------|----------|
| Phase 1-2 | ✅ Complete | Movement, UDP networking |
| **Phase 3** | ⏳ In Progress | Projectiles (55% done) - Arc trajectory complete |
| Phase 4 | ⏳ Partial | Prediction done, dual local player done, interpolation pending |

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
| ClientInputMessage | 18 bytes | Client → Server | WASD + shoot + sequence |
| ServerStateUpdateMessage | 6 + 28n bytes | Server → Clients | Player positions |
| ProjectileSpawnMessage | 53 bytes | Server → Clients | Arc projectile creation |
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

### Phase 3: Projectile System (In Progress)

**Completed:**
- ✅ ProjectileSpawnMessage protocol (53 bytes with arc data)
- ✅ Binary serialization for projectiles
- ✅ Server spawning (0.5s cooldown, facing-direction based)
- ✅ Client rendering (arc trajectory with trail)
- ✅ Parametric arc trajectory (3u height, 10u range)
- ✅ Trail renderer (yellow → orange gradient)

**Pending:**
- ❌ Hit detection
- ❌ Knockback

### Phase 4: Optimization (Partial)

**Completed:**
- ✅ Client-side prediction (local player)
- ✅ Sequence numbers (foundation for reconciliation)
- ✅ Input rate limiting (30Hz)
- ✅ Dual local player testing (P1: WASD+Space, P2: Arrows+RShift)

**Pending:**
- ❌ Interpolation buffer
- ❌ Remote player interpolation
- ❌ Server reconciliation
- ❌ Lag compensation

---

## Course Materials Integration

| Lab | Concepts | Applied In |
|-----|----------|------------|
| Lab 6 | ClientProxy pattern, Ping/Pong | GameNetworkManager connection |
| Lab 7 | Passive replication, ReplicationManager | ServerStateUpdate broadcast |
| Lab 8 | ACK system, sequence numbers | ClientInputMessage.sequenceNumber |

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

*Last Updated: 2025-12-14 | Last Change: Added official Claude Code agents integration*
