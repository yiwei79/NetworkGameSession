# Deliverable 3: Serialization - Setup Instructions

## Overview
This deliverable implements a 2-player multiplayer demo using UDP networking with binary serialization. Players can move around an arena and see each other in real-time.

## Files Created

### Network Scripts (`Assets/Scripts/Network/`)
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

### Gameplay Scripts (`Assets/Scripts/Gameplay/`)
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

## Unity Scene Setup Instructions

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
   - **Is Server**: Check this for the host instance
   - **Server Address**: "127.0.0.1" (localhost for testing)
   - **Server Port**: 9050
   - **Server Tick Rate**: 20
   - **Client Send Rate**: 30
   - **Local Player Id**: 0 (for server/host), 1 (for client)

3. Add component: `SimplePlayerController`
   - **Network Manager**: Drag NetworkManager GameObject here
   - **Player Prefab**: Drag Player prefab from Assets/Prefabs/
   - **Local Player Id**: 0 (matches NetworkManager setting)
   - **Local Player Color**: Green
   - **Remote Player Color**: Red
   - **Show Debug UI**: Checked

### Step 6: Testing

#### Single Instance Test (Server Only)
1. In NetworkManager component, set **Is Server** = true
2. Press Play in Unity Editor
3. You should see:
   - Server starts on port 9050
   - Client connects
   - Green player cube appears
   - Debug UI shows connection info

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

2. Before building, create a duplicate scene or edit:
   - NetworkManager: Is Server = ✗ (unchecked)
   - NetworkManager: Local Player Id = 1
   - SimplePlayerController: Local Player Id = 1

3. Run the built executable
4. Both instances should connect and see each other

## Controls

- **W/A/S/D**: Move your player
- **SPACE**: Shoot button (currently just visual feedback)
- **ESC**: Quit application

## Expected Behavior

### Minimum Requirements ✓
1. **Serialization (40%)**: Client serializes Vector2 input + bool and sends via UDP
2. **Server Processing (25%)**: Server receives, deserializes, and moves player objects
3. **2-Player Experience (25%)**: Both players can see each other move in real-time
4. **Clean Code (10%)**: Well-commented, organized structure

### How It Works

**Client → Server:**
- Client collects WASD input (Vector2) and Spacebar (bool)
- Packs into `ClientInputMessage` struct
- Serializes to 14-byte binary format using BinaryWriter
- Sends via UDP to server at 30 Hz

**Server Processing:**
- Receives UDP packets, deserializes using BinaryReader
- Processes input and updates player position/velocity
- Applies simple physics (acceleration, deceleration)
- Enforces arena boundaries
- Creates `ServerStateUpdateMessage` with all player snapshots
- Serializes to binary format
- Broadcasts to all clients at 20 Hz

**Server → Client:**
- Client receives UDP packets with server state
- Deserializes player snapshots
- Updates or creates player GameObjects
- Renders at received positions

## Debug Information

The debug UI displays:
- Local Player ID
- Number of connected players
- Packets sent/received
- Server time
- Current input values
- Shoot button state

## Input System Requirement

**IMPORTANT:** This project uses Unity's **New Input System**.

If you get an error about `UnityEngine.Input` when pressing Play:

1. **Install Input System Package:**
   - Go to `Window > Package Manager`
   - Search for "Input System"
   - Click `Install`

2. **Or see detailed instructions:**
   - Open `Assets/Scripts/INPUT_SYSTEM_SETUP.md`

The code has been updated to use `Keyboard.current` from the new Input System, which is better for future gamepad/mobile support.

## Troubleshooting

### "InvalidOperationException: You are trying to read Input..."
- **Solution:** Install Input System package (see above)
- Or check `INPUT_SYSTEM_SETUP.md` for step-by-step guide

### "No GameNetworkManager found!"
- Make sure GameNetworkManager component is attached to a GameObject in the scene

### Player doesn't move
- Check that Local Player ID matches between NetworkManager and SimplePlayerController
- Verify Is Server is checked for host instance
- Check console for network errors
- Make sure Input System package is installed

### Players don't see each other
- Ensure both instances use same Server Port (9050)
- Check firewall settings if not on localhost
- Verify Server Address is correct in client instance

### "Player prefab not assigned!"
- Drag Player prefab from Assets/Prefabs/ to SimplePlayerController's Player Prefab field

## Network Statistics

**Bandwidth Usage:** (updated Nov 2025 with sequence numbers)
- Client upload: ~540 bytes/sec (**18 bytes** * 30 Hz)
- Server download per client: ~540 bytes/sec
- Server upload per client: ~680 bytes/sec (34 bytes * 20 Hz for 2 players)
- Total per client: ~1.2 KB/sec

**Packet Sizes:**
- ClientInput: **18 bytes** (includes sequence number)
- ServerStateUpdate (2 players): 34 bytes
- ConnectMessage: 5 bytes

## Future Enhancements (Deliverable 4+)

- Client-side prediction for local player
- Interpolation between state updates
- Lag compensation
- Physics-based movement with momentum
- Projectile shooting mechanics
- Better visual effects and polish

