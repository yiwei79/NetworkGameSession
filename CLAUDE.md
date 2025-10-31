# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **Unity educational project** for learning network game programming concepts. The repository contains:

1. **Lab Sessions** - Completed foundational exercises (Threading, TCP/UDP, Serialization)
2. **Final Project "Loving Away"** - Multiplayer physics-based arena shooter (in progress)

**Current Status**: Deliverable 3 (Serialization) - COMPLETE ✅  
**Unity Version**: 6000.2.6f1 (Unity 6)  
**Platform**: macOS (Darwin)  
**Input System**: New Input System (UnityEngine.InputSystem)

## Project Structure

```
NetworkGameSession/
├── Docs/                          # All documentation organized here
│   ├── Lab Session 1/             # Threading lab materials
│   ├── Lab Session 2 TCP:UDP/     # Networking basics
│   ├── Lab Session 3/             # Serialization exercises
│   ├── Lab Session 4/             # NAT concepts
│   ├── Deliverable 3/             # ⭐ AI-GENERATED DOCS (READ HERE)
│   │   ├── README.md              # Documentation index
│   │   ├── DELIVERABLE_3_README.md  # Quick start
│   │   ├── DELIVERABLE_3_SETUP.md   # Unity scene setup
│   │   ├── TESTING_GUIDE.md       # Testing & troubleshooting
│   │   ├── INPUT_SYSTEM_SETUP.md  # New Input System guide
│   │   └── DELIVERABLE_3_SUMMARY.md # Technical details
│   ├── Final Project/             # Project proposal & plan
│   │   ├── Technical_Implementation_Plan.md
│   │   └── Yiwei_Ye_Deliverable_0_Project_Proposal
│   └── LAB_SESSION_*_SUMMARY.md   # Lab summaries
│
├── Pre-NetNet/                    # Lab 1: Threading project
│   └── Assets/Scripts/BubbleSort.cs
│
└── Loving Away/                   # ⭐ MAIN PROJECT (Final Project)
    └── Loving Away(Network Game)/
        └── Assets/
            └── Scripts/
                ├── Network/       # UDP networking & serialization
                │   ├── NetworkProtocol.cs
                │   ├── Serializer.cs
                │   └── GameNetworkManager.cs
                ├── Gameplay/      # Game logic & player control
                │   ├── ServerGameState.cs
                │   ├── SimplePlayerController.cs
                │   └── ShootVisualFeedback.cs
                ├── TCPTest.cs     # Lab 2: TCP example
                └── UDPTest.cs     # Lab 2: UDP example
```

**IMPORTANT**: All AI-generated documentation is in `Docs/Deliverable 3/`. Start there for setup and testing guides.

## Critical Unity-Specific Constraints

### Thread Safety Rules
1. **Never call Unity API from worker threads**: `Instantiate()`, `Destroy()`, `transform`, `GameObject`, any `Component` methods will crash
2. **Safe for worker threads**: Pure C# data structures (arrays, lists), math operations, file I/O, network operations
3. **Pattern in this codebase**: Worker thread modifies data (float array), main thread reads data and updates Unity objects in `Update()`

### Namespace Conflicts
- `System.Diagnostics.Debug` conflicts with `UnityEngine.Debug`
- Always use `UnityEngine.Debug.Log()` explicitly when `System.Diagnostics` is imported
- Required because we use `System.Diagnostics.Stopwatch` for performance timing

## Key Architecture Pattern: Computation-Visualization Split

The `BubbleSort.cs` script demonstrates the fundamental threading pattern for Unity:

```
[Worker Thread]          [Shared Memory]          [Main Thread/Update()]
bubbleSort()       --->  float[] array      <---  updateHeights()
quickSort()              (write-only)             (read + Unity API calls)
```

**Why this works without locks:**
- Only ONE thread writes (worker)
- Main thread only reads
- No write-write or read-write conflicts
- Float operations are atomic in C#

## Working with BubbleSort.cs

### Inspector-Exposed Fields
- `public GameObject prefab` - Must be assigned to Cube Green or Cube Red prefab before running

### Testing Configurations

**Multi-threaded BubbleSort (default):**
```csharp
Thread sortThread = new Thread(bubbleSort);
```

**Multi-threaded QuickSort (faster):**
```csharp
// Comment line 39, uncomment:
Thread sortThread = new Thread(quickSortWrapper);
```

**Single-threaded (freezing demo):**
```csharp
// Uncomment line 31, comment lines 39-45
bubbleSort();  // Blocks main thread
```

### Performance Expectations
- **BubbleSort**: 10-30 seconds for 30,000 elements (O(n²))
- **QuickSort**: 50-500ms for 30,000 elements (O(n log n))
- Game maintains 60 FPS during threaded operations

## Common Development Workflow

### Testing in Unity
1. Open `Pre-NetNet/Assets/Scenes/S_ThreadingExercise.unity`
2. Select GameObject with BubbleSort component
3. Assign prefab in Inspector
4. Press Play
5. Check Console for timing logs

### Modifying Threading Code
When editing threading logic:
- Worker thread methods: `bubbleSort()`, `quickSortWrapper()`, `quickSort()`, `partition()`
- Main thread methods: `Update()`, `updateHeights()`, `spawnObjs()`
- Shared data: `float[] array`, `List<GameObject> mainObjects`

### Adding New Sorting Algorithms
1. Create method with signature: `void MySort()` (no parameters, returns void)
2. Add timing with `stopwatch.Start()` and `stopwatch.Stop()`
3. Log with `UnityEngine.Debug.Log()`
4. Create thread: `Thread sortThread = new Thread(MySort);`
5. Start thread: `sortThread.Start();`

## Deliverable 3: Serialization (Current Work)

### What's Implemented

A 2-player multiplayer demo using UDP networking with binary serialization:
- **Network Protocol**: Custom binary message format (14-byte ClientInput, 34-byte ServerState)
- **Serialization**: BinaryWriter/BinaryReader for efficient packet transmission
- **Server-Authoritative**: Server owns all game state, clients send input commands
- **Threading**: Separate worker threads for network I/O, main thread for Unity API
- **Visual Feedback**: Charging mechanic with sphere indicators and muzzle flash

### Documentation Location

**All setup and testing guides are in `Docs/Deliverable 3/`** - Read these files:
1. `DELIVERABLE_3_README.md` - Start here for quick overview
2. `DELIVERABLE_3_SETUP.md` - Unity scene setup instructions
3. `TESTING_GUIDE.md` - Testing procedures and troubleshooting
4. `INPUT_SYSTEM_SETUP.md` - New Input System requirements

### Key Architecture: Client-Server with Binary Serialization

```
CLIENT                          NETWORK (UDP)                    SERVER
┌──────────────────┐                                    ┌──────────────────┐
│ SimplePlayer     │   ClientInputMessage (14 bytes)    │ GameNetwork      │
│ Controller       │ ──────────────────────────────────>│ Manager          │
│ - Collects WASD  │                                    │ - Receives input │
│ - Spacebar input │                                    │ - Queues for     │
└──────────────────┘                                    │   main thread    │
         ↓                                               └─────────┬────────┘
┌──────────────────┐                                             ↓
│ GameNetwork      │                                    ┌──────────────────┐
│ Manager          │                                    │ ServerGameState  │
│ - Serializes     │                                    │ - Processes input│
│   with Binary    │                                    │ - Updates physics│
│   Writer (14B)   │                                    │ - 20 Hz tick rate│
│ - Sends UDP      │                                    └─────────┬────────┘
└──────────────────┘                                             ↓
         ↑                                               ┌──────────────────┐
┌──────────────────┐   ServerStateUpdate (34 bytes)    │ GameNetwork      │
│ SimplePlayer     │ <──────────────────────────────────│ Manager          │
│ Controller       │                                    │ - Creates        │
│ - Deserializes   │                                    │   snapshots      │
│ - Renders players│                                    │ - Serializes     │
│ - Updates visuals│                                    │ - Broadcasts     │
└──────────────────┘                                    └──────────────────┘
```

## Critical Patterns & Constraints

### Unity Thread Safety (Still Applies)

1. **Never call Unity API from worker threads**: `Instantiate()`, `Destroy()`, `transform`, etc. will crash
2. **Safe for worker threads**: Socket operations, BinaryWriter/BinaryReader, data structures
3. **Pattern used**: Worker threads handle network I/O, main thread handles Unity API via queues

### Binary Serialization Pattern

```csharp
// Write (Serialize)
using (MemoryStream ms = new MemoryStream())
using (BinaryWriter writer = new BinaryWriter(ms))
{
    writer.Write((byte)messageType);
    writer.Write(playerId);
    writer.Write(moveDirection.x);
    writer.Write(moveDirection.y);
    return ms.ToArray();
}

// Read (Deserialize)
using (MemoryStream ms = new MemoryStream(data))
using (BinaryReader reader = new BinaryReader(ms))
{
    messageType = (MessageType)reader.ReadByte();
    playerId = reader.ReadUInt32();
    float x = reader.ReadSingle();
    float y = reader.ReadSingle();
}
```

### Input System (NEW!)

This project uses **Unity's New Input System** (not legacy Input Manager):

```csharp
using UnityEngine.InputSystem;

// Correct way to read input:
var keyboard = Keyboard.current;
if (keyboard != null)
{
    if (keyboard.wKey.isPressed) vertical += 1f;
    // ...
}

// OLD WAY (don't use):
// if (Input.GetKey(KeyCode.W)) // ❌ Will error
```

**Why**: Better for gamepad support, rebindable controls, and future mobile/multiplayer features.

## Lab Session Context

Completed lab sessions:
- ✅ **Lab 1**: Threading fundamentals (BubbleSort.cs visualization)
- ✅ **Lab 2**: TCP/UDP networking (TCPTest.cs, UDPTest.cs)
- ✅ **Lab 3**: Serialization basics (binary, JSON, XML)
- 📖 **Lab 4**: NAT concepts (read-only, no code)

Current work:
- ✅ **Deliverable 3**: Serialization + UDP for 2-player multiplayer (COMPLETE)

Next work:
- ⏳ **Deliverable 4**: World State Replication with interpolation
- ⏳ **Deliverable 5**: Latency/jitter mitigation (Final Demo)

## Working with "Loving Away" Project

### Main Scripts

**Network Layer** (`Assets/Scripts/Network/`):
- `NetworkProtocol.cs` - Message definitions (structs, enums)
- `Serializer.cs` - Binary serialization utilities
- `GameNetworkManager.cs` - UDP client/server, threading, MonoBehaviour component

**Gameplay Layer** (`Assets/Scripts/Gameplay/`):
- `ServerGameState.cs` - Server-side game logic (NOT a MonoBehaviour)
- `SimplePlayerController.cs` - Client-side input & rendering (MonoBehaviour)
- `ShootVisualFeedback.cs` - Visual effects (MonoBehaviour, added dynamically)

### Common Edits

**Adding new network messages:**
1. Define struct in `NetworkProtocol.cs`
2. Add serialize/deserialize methods in `Serializer.cs`
3. Add case in `GameNetworkManager.HandleServerReceive()` or `HandleClientReceive()`

**Modifying movement:**
1. Server-side: Edit `ServerGameState.ProcessInput()` or `UpdateState()`
2. Client-side: Edit `SimplePlayerController.CollectInput()`

**Adding visual effects:**
1. Create new component like `ShootVisualFeedback.cs`
2. Attach in `SimplePlayerController.CreatePlayerObject()`

### Testing Workflow

1. **Single instance**: Unity Editor with "Is Server" checked
2. **Two players**: Unity Editor (server) + Built executable (client)
3. **See**: `Docs/Deliverable 3/TESTING_GUIDE.md` for detailed procedures

### Namespace Conflicts to Watch

```csharp
// Still applies from Lab 1:
using System.Diagnostics;  // Has Debug class
// ...
UnityEngine.Debug.Log("Use fully qualified name");

// New in Deliverable 3:
using UnityEngine.InputSystem;  // Keyboard class
// Don't use: Input.GetKey() - that's the old system
```

## Git Workflow & Documentation

### Documentation Organization

**All AI-generated documentation is in `Docs/Deliverable 3/`** to keep code folders clean.

When creating new documentation:
- Lab summaries → `Docs/`
- Deliverable docs → `Docs/Deliverable X/`
- Project-wide guides → Root or `Docs/`

### What NOT to Commit

Unity automatically generates `.meta` files - these are tracked by git.
Large files in `Library/` and `Temp/` are gitignored automatically.

## Next Steps

**For continuing development:**
1. Read `Docs/Final Project/Technical_Implementation_Plan.md` - Complete roadmap
2. Current phase: Phase 2 complete (basic networking)
3. Next phase: Phase 3 (full gameplay sync with projectiles)

**For testing current deliverable:**
1. Read `Docs/Deliverable 3/DELIVERABLE_3_README.md`
2. Follow `DELIVERABLE_3_SETUP.md` to create Unity scene
3. Use `TESTING_GUIDE.md` for testing procedures
