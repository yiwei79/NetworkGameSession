# Deliverable 3: Serialization - Setup & Quick Start

## Overview
This deliverable implements a 2-player multiplayer demo using UDP networking with binary serialization. Players can move around an arena and see each other in real-time.

---

## 📋 Prerequisites

Before you begin, ensure you have:

### ✅ Unity Version
- **Required:** Unity 6000.2.6f1 (or Unity 6.x)
- **Check:** Open Unity Hub → Projects → Version column

### ✅ Input System Package

This project uses Unity's **New Input System** (not the legacy Input Manager).

**Quick Check:**
1. In Unity, go to `Window > Package Manager`
2. Look for "Input System" in the list
3. If shows "Installed" → ✅ You're good!
4. If not installed → Follow installation steps below

**Installation:**
1. In Package Manager, click `+ (plus icon)` in top-left
2. Select `Add package by name...`
3. Enter: `com.unity.inputsystem`
4. Click `Add` and wait for installation

**Why New Input System?**
- ✅ No conflicts with Player Settings
- ✅ Cleaner API (`Keyboard.current`)
- ✅ Future gamepad/mobile support ready
- ✅ Rebindable controls for final game

**Code Example:**
```csharp
// New Input System (what we use)
var keyboard = Keyboard.current;
if (keyboard != null && keyboard.wKey.isPressed)
    vertical += 1f;

// OLD way (don't use):
// if (Input.GetKey(KeyCode.W)) // ❌ Will error
```

---

## 🚀 Quick Start (TL;DR)

**For experienced Unity developers:**

1. **Install Input System** package (`com.unity.inputsystem`)
2. **Create scene:** Plane arena + Camera (top-down view)
3. **Create prefab:** Cube named "Player"
4. **Add components** to empty GameObject:
   - `GameNetworkManager` (Is Server = true, Port = 9050)
   - `SimplePlayerController` (assign prefab)
5. **Press Play** - Should see green player, move with WASD
6. **Build executable** with different player ID for second instance

**See detailed instructions below** if you need step-by-step guidance.

---

## 📂 Files Created

**Location:** `Loving Away(Network Game)/Assets/Scripts/`

### Network Scripts (`Network/`)
1. **NetworkProtocol.cs** - Message type definitions
   - `MessageType` enum
   - `ClientInputMessage` struct (**18 bytes** - includes sequence number)
   - `ServerStateUpdateMessage` struct (variable size)
   - `PlayerSnapshot` struct (28 bytes)
   - `ConnectMessage` struct (5 bytes)

2. **Serializer.cs** - Binary serialization utilities
   - `SerializeClientInput()` / `DeserializeClientInput()`
   - `SerializeServerState()` / `DeserializeServerState()`
   - Uses BinaryWriter/BinaryReader as per Lab Session 3 requirements

3. **GameNetworkManager.cs** - Core networking component
   - UDP server thread (20 Hz tick rate)
   - UDP client thread (30 Hz send rate)
   - Thread-safe message queues
   - Connection handling and player ID assignment

### Gameplay Scripts (`Gameplay/`)
1. **ServerGameState.cs** - Server-authoritative game state
   - Player state management
   - Input processing
   - Position updates with simple physics
   - Arena boundary constraints

2. **SimplePlayerController.cs** - Client-side controller
   - Input collection (WASD, Spacebar)
   - Sends input to server via GameNetworkManager
   - Renders all players from server state
   - Debug UI display
   - **Enhanced:** Client-side prediction for instant local response (Nov 2025)

---

## 🎮 Unity Scene Setup (Step-by-Step)

### Step 1: Create MultiplayerTest Scene

1. In Unity, create a new scene: `File > New Scene`
2. Save as `Assets/Scenes/MultiplayerTest.unity`

### Step 2: Create Arena

1. Create a Plane: `GameObject > 3D Object > Plane`
   - Name: "Arena"
   - Position: (0, 0, 0)
   - Scale: (3, 1, 3) - creates 30x30 unit arena

2. Optional: Create boundary visualization
   - Create empty GameObject "Boundaries"
   - Add multiple thin cubes or cylinders as walls

### Step 3: Create Player Prefab

1. Create a Cube: `GameObject > 3D Object > Cube`
   - Name: "Player"
   - Position: (0, 0.5, 0)
   - Scale: (1, 1, 1)

2. Create prefab:
   - Create folder `Assets/Prefabs/` if doesn't exist
   - Drag "Player" from Hierarchy to `Assets/Prefabs/` folder
   - Delete the cube from the scene (prefab will be instantiated at runtime)

### Step 4: Setup Camera

1. Select Main Camera
2. Set position: (0, 15, -10)
3. Set rotation: (45, 0, 0) - angled top-down view
4. Adjust as needed for better view of the arena

### Step 5: Create Network Manager GameObject

1. Create empty GameObject: `GameObject > Create Empty`
   - Name: "NetworkManager"
   - Position: (0, 0, 0)

2. Add component: `GameNetworkManager`
   - **Is Server**: ✓ Check this for the host instance
   - **Server Address**: "127.0.0.1" (localhost for testing)
   - **Server Port**: 9050
   - **Server Tick Rate**: 20
   - **Client Send Rate**: 30
   - **Local Player Id**: 0 (for server/host), 1 (for client)

3. Add component: `SimplePlayerController`
   - **Network Manager**: Drag NetworkManager GameObject here
   - **Player Prefab**: Drag Player prefab from Assets/Prefabs/
   - **Local Player Id**: 0 (must match NetworkManager setting)
   - **Local Player Color**: Green
   - **Remote Player Color**: Red
   - **Show Debug UI**: ✓ Checked
   - **Input Send Rate**: 30 (default)
   - **Enable Prediction**: ✓ Checked (for instant local response)

### Step 6: Testing

#### Single Instance Test (Server Only)
1. In NetworkManager component, set **Is Server** = ✓ true
2. Press Play in Unity Editor
3. You should see:
   - Console: "Server started on port 9050"
   - Console: "Client connected to..."
   - Green player cube appears
   - Debug UI in top-left showing:
     - Local Player ID: 0
     - Players Connected: 1
     - Connection status
     - Packets sent/received
     - Input sequence number
4. Test WASD movement - should be instant and smooth
5. Hold Spacebar - should see charging visual effect

#### Two Player Test (Editor + Build)

**Instance 1 (Server/Host - Unity Editor):**
1. NetworkManager settings:
   - Is Server: ✓ (checked)
   - Server Address: "127.0.0.1"
   - Server Port: 9050
   - Local Player Id: 0
2. SimplePlayerController settings:
   - Local Player Id: 0
3. Press Play

**Instance 2 (Client - Built Executable):**
1. Build the game: `File > Build Settings`
   - Select Windows/Mac/Linux
   - Click "Build"
   - Save as "LovingAway_Client.exe" (or .app)

2. Before building, create a duplicate scene or edit NetworkManager:
   - Is Server = ✗ (unchecked)
   - Local Player Id = 1
   - SimplePlayerController: Local Player Id = 1

3. Run the built executable
4. Both instances should connect and see each other
5. Each player sees themselves as green, other as red

---

## 🕹️ Controls

- **W/A/S/D**: Move your player
- **SPACE**: Shoot button (currently shows charging visual)
- **ESC**: Quit application

---

## ✅ Expected Behavior

### Lab Requirements Met
1. **40% - Client→Server Serialization**: Client serializes Vector2 input + bool, sends via UDP
2. **25% - Server→Client Serialization**: Server sends position/velocity snapshots via UDP
3. **25% - 2-Player Experience**: Both players see each other move in real-time
4. **10% - Clean Code**: Well-commented, organized, thread-safe

### Architectural Flow

**Client → Server:**
- Client collects WASD input (Vector2) and Spacebar (bool)
- Packs into `ClientInputMessage` struct (18 bytes)
- Serializes with BinaryWriter
- Sends via UDP at 30 Hz

**Server Processing:**
- Receives UDP packets, deserializes with BinaryReader
- Processes input, updates player position/velocity
- Applies physics (acceleration, deceleration, boundaries)
- Creates `ServerStateUpdateMessage` with all player snapshots
- Serializes to binary
- Broadcasts to all clients at 20 Hz

**Server → Client:**
- Client receives UDP packets with server state
- Deserializes player snapshots
- **Local player**: Client-side prediction (instant) + server reconciliation
- **Remote players**: Renders at server positions
- Updates GameObjects in scene

---

## 🐛 Troubleshooting

### Input System Issues

| Symptom | Solution |
|---------|----------|
| "InvalidOperationException: You are trying to read Input..." | Install Input System package: `Window > Package Manager` → Search "Input System" → Install |
| Compilation errors mentioning `UnityEngine.Input` | Input System not installed. See installation steps above |
| WASD keys don't work | 1. Check Input System installed<br>2. Check console for errors<br>3. Verify keyboard is connected |

### Setup Issues

| Symptom | Solution |
|---------|----------|
| "No GameNetworkManager found!" | Attach GameNetworkManager component to a GameObject in scene |
| Player doesn't appear | Assign player prefab in SimplePlayerController's "Player Prefab" field |
| Player appears but doesn't move | 1. Check Local Player ID matches in both components<br>2. Verify "Is Server" is checked<br>3. Check Input System installed |

### Networking Issues

| Symptom | Solution |
|---------|----------|
| Players don't see each other | 1. Both instances must use same Server Port (9050)<br>2. Check firewall settings<br>3. Verify Server Address is correct ("127.0.0.1" for localhost) |
| "Address already in use" error | 1. Close all running instances<br>2. Wait 30 seconds for port to release<br>3. Try again |
| Second player same color as first | Set different Local Player IDs (0 and 1) |
| Connection timeout | 1. Check server is running first<br>2. Verify port numbers match<br>3. Check firewall not blocking |

### Performance Issues

| Symptom | Solution |
|---------|----------|
| Input delay / laggy movement | **FIXED** in Nov 2025 update with client-side prediction. Enable "Enable Prediction" in SimplePlayerController if disabled |
| Choppy remote player movement | Normal at 20 Hz server tick. Interpolation coming in Phase 4 |
| High CPU usage | Normal - separate threads for network I/O |

---

## 📊 Network Statistics

**Bandwidth Usage:** (updated Nov 2025)
- Client upload: ~540 bytes/sec (**18 bytes** * 30 Hz)
- Server download per client: ~540 bytes/sec
- Server upload per client: ~680 bytes/sec (34 bytes * 20 Hz for 2 players)
- **Total per client:** ~1.2 KB/sec

**Packet Sizes:**
- ClientInput: **18 bytes** (includes sequence number for future features)
- ServerStateUpdate (2 players): 34 bytes (6 + 28*2)
- ConnectMessage: 5 bytes

**Update Rates:**
- Client sends input: 30 Hz (every 33ms)
- Server ticks: 20 Hz (every 50ms)
- Local player rendering: 60 FPS (predicted, instant)
- Remote player rendering: 20 Hz (server rate)

For detailed bandwidth analysis, see `DELIVERABLE_3_SUMMARY.md`

---

## 🎯 Demo Talking Points

When presenting this deliverable:

1. **Show both instances running** - Point out 2 players seeing each other
2. **Highlight debug UI** - Packets sent/received, sequence numbers, connection status
3. **Demonstrate visual feedback** - Hold spacebar to show charging effect
4. **Explain binary serialization** - "18 bytes per input vs ~50 bytes for JSON (64% smaller)"
5. **Explain server authority** - "Server owns positions, clients just render"
6. **Show responsive controls** - "Client-side prediction makes local player feel instant"

---

## 🔮 Future Enhancements (Deliverable 4+)

Items from Technical Implementation Plan:

- ✅ Client-side prediction for local player (completed Nov 2025)
- ⏳ Interpolation for remote players (Phase 4)
- ⏳ Server reconciliation with sequence numbers (Phase 4)
- ⏳ Lag compensation for shooting (Phase 4)
- ⏳ Actual projectile system with hit detection (Phase 3)
- ⏳ Physics-based movement with momentum
- ⏳ Visual polish and effects

---

**Status:** ✅ Implementation Complete + Enhanced
**Testing:** Requires Unity 6 + Input System package
**Ready For:** Demo and Phase 3 development
**Recent Updates:** Input delay fixes (Nov 2025) - see `INPUT_DELAY_FIXES.md`
