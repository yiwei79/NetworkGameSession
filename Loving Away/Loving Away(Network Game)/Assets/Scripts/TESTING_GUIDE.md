# Deliverable 3: Testing & Integration Guide

## Quick Start Testing

### Option 1: Single Instance Test (Verify Scripts Work)

This test verifies all scripts compile and basic functionality works.

1. Open Unity and load the `MultiplayerTest` scene
2. Select the NetworkManager GameObject
3. Ensure these settings:
   - Is Server: ✓ (checked)
   - Server Address: "127.0.0.1"
   - Server Port: 9050
   - Local Player Id: 0
4. Press Play
5. **Expected Result:**
   - Console shows: "Server is running on port 9050"
   - Console shows: "Client connected to server"
   - Green player cube appears in the arena
   - You can move with WASD
   - Spacebar shows yellow charging indicator
   - Debug UI shows network stats

**This proves:** Serialization works (client→server→client loop), UDP networking works, visual feedback works.

---

### Option 2: Two Instance Test (Full 2-Player Demo)

This is the full deliverable requirement: 2 players seeing each other.

#### Setup Instance 1: Server/Host (Unity Editor)

1. In Unity, open `MultiplayerTest` scene
2. NetworkManager settings:
   - Is Server: ✓
   - Server Address: "127.0.0.1"
   - Server Port: 9050
   - Local Player Id: 0
3. SimplePlayerController settings:
   - Local Player Id: 0
   - Local Player Color: Green
4. **Save the scene**
5. Press Play

#### Setup Instance 2: Client (Built Executable)

**Before Building:**
1. Duplicate the scene or create a new one: `MultiplayerTest_Client`
2. In the new scene, change NetworkManager:
   - Is Server: ✗ (unchecked)
   - Server Address: "127.0.0.1"
   - Server Port: 9050
   - Local Player Id: 1
3. SimplePlayerController settings:
   - Local Player Id: 1
   - Local Player Color: Blue
4. Go to `File > Build Settings`
5. Add `MultiplayerTest_Client` scene (or set it as the only scene)
6. Select platform (Windows/Mac/Linux)
7. Click "Build and Run"
8. Save as `LovingAway_Client.exe` (or .app)

**Running the Test:**
1. Start Instance 1 (Unity Editor) first
2. Wait for "Server is running" message
3. Start Instance 2 (Built executable)
4. **Expected Result:**
   - Both instances show 2 players
   - Editor shows: Green player (you) + Red player (client)
   - Build shows: Blue player (you) + Red player (server)
   - Moving in one instance updates position in the other
   - ~50-100ms latency on localhost
   - Debug UI shows packets sent/received increasing

---

## What You're Testing

### Lab Requirements Verification

#### 40% - Client→Server Serialization
- **Test:** Move your character with WASD in client build
- **Verify in server console:** "Received input from player 1"
- **Verify visually:** Red player cube moves in server instance
- **What's happening:**
  1. Client collects Vector2 input (WASD)
  2. Packs into ClientInputMessage struct
  3. Serializes with BinaryWriter (14 bytes)
  4. Sends via UDP to server
  5. Server deserializes with BinaryReader
  6. Server updates player position

#### 25% - Server→Client Serialization  
- **Test:** Move your character in server (Unity Editor)
- **Verify in client build:** Green/Blue player moves
- **What's happening:**
  1. Server updates all player positions
  2. Creates ServerStateUpdateMessage with snapshots
  3. Serializes with BinaryWriter (~34 bytes for 2 players)
  4. Broadcasts via UDP to all clients
  5. Clients deserialize with BinaryReader
  6. Clients update GameObject positions

#### 25% - Extras
- **Shoot button visual feedback:** Hold SPACE, see yellow sphere grow
- **Complete moveset:** 8-directional WASD movement works smoothly
- **Debug UI:** Shows ping, packets sent/received, connection status
- **Disconnection handling:** Close client, server logs "player disconnected"
- **Game experience:** Can play for 1-2 minutes, feels responsive

#### 10% - Clean Code
- All files are well-commented
- Clear separation: Network/ and Gameplay/ folders
- Follows C# naming conventions
- Thread-safe queue patterns for worker threads

---

## Debugging Common Issues

### Issue: "No GameNetworkManager found!"
**Cause:** SimplePlayerController can't find NetworkManager
**Fix:** 
- Ensure NetworkManager GameObject exists in scene
- Drag NetworkManager to SimplePlayerController's "Network Manager" field
- Or ensure both components are on same GameObject

### Issue: Player doesn't appear
**Cause:** Player prefab not assigned
**Fix:**
- Create a Cube, save as prefab in Assets/Prefabs/
- Drag prefab to SimplePlayerController's "Player Prefab" field

### Issue: Second player doesn't connect
**Cause:** Port conflict or Local Player ID mismatch
**Fix:**
- Check both instances use port 9050
- Server uses Local Player Id: 0
- Client uses Local Player Id: 1
- Check Windows Firewall allows Unity
- Try disabling antivirus temporarily

### Issue: Players are choppy/laggy on localhost
**Cause:** Not actually a problem! This is normal network behavior
**Fix:** This is expected - server updates at 20 Hz, so you see discrete jumps
**For smooth movement (future work):** Implement interpolation in Deliverable 4

### Issue: "SocketException: Address already in use"
**Cause:** Previous instance didn't close properly, port 9050 still bound
**Fix:**
- Close all Unity instances
- Close all built executables
- Wait 30 seconds for OS to release port
- Restart Unity

### Issue: Both players are same color
**Cause:** Local Player Id not set correctly
**Fix:**
- Server instance: Local Player Id = 0, Color = Green
- Client instance: Local Player Id = 1, Color = Blue
- Rebuild client with correct settings

---

## Performance Verification

### Expected Network Stats

**On Localhost (0ms ping):**
- Packets sent: Increases by ~30 per second (client) or ~20 per second (server)
- Packets received: Increases by ~20 per second (client)
- Bandwidth: ~1-2 KB/s per client
- Ping: <5ms

**With 2 Players:**
- Total server bandwidth: ~2-3 KB/s
- CPU usage: <5% per instance
- Memory: ~200-300 MB per instance
- Frame rate: 60 FPS (should not drop)

### Stress Test

Try these to verify robustness:

1. **Rapid input:** Mash WASD keys randomly
   - Should handle 60 inputs/sec smoothly
   
2. **Hold Spacebar:** Charge shot for 5+ seconds
   - Indicator should cap at max size
   - No errors or memory leaks

3. **Run for 5 minutes:** Let both instances run idle
   - Should maintain connection
   - No packet loss
   - Packet counters increase steadily

4. **Disconnect/Reconnect:** Close client, reopen
   - Server should remove player
   - Client should reconnect cleanly

---

## Binary Serialization Verification

### Manual Packet Inspection

To verify binary serialization is working correctly:

1. **Check packet sizes in Debug UI:**
   - Client sends ~420 bytes/sec (14 bytes * 30 Hz)
   - Server sends ~680 bytes/sec (34 bytes * 20 Hz)

2. **Compare to text-based approach:**
   - If we sent "playerId:0,x:1.5,y:0.3" as string: 23 bytes
   - Our binary format: 14 bytes (38% size reduction)
   - ServerState as JSON: ~150 bytes for 2 players
   - Our binary format: 34 bytes (77% size reduction)

3. **Verify data integrity:**
   - Move in precise patterns (square, circle)
   - Other player should follow exact same path
   - No position jitter or teleporting (beyond normal 20Hz updates)

### Endianness Note

Our implementation uses C# BinaryWriter/BinaryReader which handles endianness automatically (little-endian on most systems). This is fine for same-machine testing, but for cross-platform multiplayer, you'd need to specify byte order explicitly.

---

## Demo Day Preparation

### What to Show

1. **Start both instances** (Editor + Build)
2. **Show the setup:** Point out NetworkManager settings
3. **Move both players:** Demonstrate bidirectional sync
4. **Show debug UI:** Highlight packets sent/received
5. **Hold Spacebar:** Show charging visual effect
6. **Explain architecture:**
   - "Client sends input commands (not positions)"
   - "Server is authoritative, calculates positions"
   - "Binary serialization keeps packets small"
   - "20 Hz server tick, 30 Hz client send rate"

### Talking Points

**Serialization (40%):**
"The client serializes WASD input into a 14-byte binary packet using BinaryWriter. The server deserializes it with BinaryReader and updates the player position. This is 38% smaller than sending the same data as text."

**Server Authority (25%):**
"The server owns all positions. Clients just send input and render what the server tells them. This prevents cheating and keeps everyone synchronized."

**Extras (25%):**
"I added visual feedback for shooting, a debug UI showing network stats, and connection timeout handling. The game maintains 60 FPS even with network updates running on separate threads."

**Clean Code (10%):**
"The code is organized into Network/ and Gameplay/ folders, uses thread-safe queues for communication between worker threads and Unity's main thread, and follows the same patterns as our TCP/UDP labs."

---

## Next Steps (Deliverable 4)

After this deliverable is complete, future improvements:

1. **Client-side prediction:** Local player moves immediately
2. **Interpolation:** Smooth movement between 20Hz updates
3. **Lag compensation:** Rewind server state for hit detection
4. **Projectile system:** Actually shoot objects
5. **Physics-based movement:** Momentum, drag, sliding
6. **Better arena:** Obstacles, power-ups
7. **Lobby system:** Proper connection UI

---

## Checklist

Before submitting/demoing, verify:

- [ ] All scripts compile with no errors
- [ ] Single instance test works (green player moves)
- [ ] Two instance test works (both players see each other)
- [ ] WASD movement works in all directions
- [ ] Spacebar shows charging visual effect
- [ ] Debug UI displays network stats correctly
- [ ] Connection status shows "Connected" (green)
- [ ] Both players maintain 60 FPS
- [ ] No memory leaks after 5 minutes
- [ ] Code is commented and organized
- [ ] DELIVERABLE_3_SETUP.md is complete
- [ ] Can explain how serialization works

**Ready for Mid-Term Demo! 🎉**

