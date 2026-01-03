# Session 1: Projectile Foundation - COMPLETE ✅

**Date:** 2025-11-20
**Branch:** Phase_4
**Estimated Time:** 2-3 hours
**Status:** Code complete, ready for testing

---

## What Was Implemented

### 1. Network Protocol Changes
**File:** `NetworkProtocol.cs` ([view](../../Loving%20Away/Loving%20Away(Network%20Game)/Assets/Scripts/Network/NetworkProtocol.cs))

- Added `MessageType.ProjectileSpawn = 5` (line 11)
- Added `ProjectileSpawnMessage` struct (lines 92-115)
  - Fields: `projectileId`, `ownerId`, `startPosition`, `velocity`, `spawnTime`
  - Size: **37 bytes** (1 + 4 + 4 + 12 + 12 + 4)
  - Constructor auto-assigns `MessageType.ProjectileSpawn`

### 2. Serialization Methods
**File:** `Serializer.cs` ([view](../../Loving%20Away/Loving%20Away(Network%20Game)/Assets/Scripts/Network/Serializer.cs))

- Added `SerializeProjectileSpawn()` method (lines 211-236)
  - Binary format: [type][id][ownerID][startPos(xyz)][velocity(xyz)][time]
- Added `DeserializeProjectileSpawn()` method (lines 241-269)
- Follows existing pattern: uses BinaryWriter/BinaryReader with MemoryStream

### 3. Projectile GameObject Component
**File:** `Projectile.cs` ([NEW](../../Loving%20Away/Loving%20Away(Network%20Game)/Assets/Scripts/Gameplay/Projectile.cs))

**Features:**
- **Initialize()**: Sets up projectile from spawn message
- **Linear trajectory**: Moves at constant velocity (Session 2 will add arc)
- **Auto-destruct**: 2-second lifetime
- **Visual representation**: Creates yellow emissive sphere (0.2 radius)
- **Thread-safe**: All Unity API calls in main thread

**Key Properties:**
```csharp
public uint projectileId;
public uint ownerId;
public Vector3 velocity;
public float lifetime = 2.0f;
```

### 4. Server-Side Projectile Spawning
**File:** `ServerGameState.cs` ([view](../../Loving%20Away/Loving%20Away(Network%20Game)/Assets/Scripts/Gameplay/ServerGameState.cs))

**New Fields (lines 21-27):**
- `pendingProjectileSpawns` queue
- `nextProjectileId` counter
- `projectileCooldown = 0.5f` (500ms between shots)
- `lastShootTime` dictionary (tracks per-player cooldown)
- `projectileSpeed = 15.0f`
- `projectileHeight = 2.0f` (launch height above player)

**New Methods:**
- `SpawnProjectile()` (lines 208-245): Creates projectile when player shoots
  - Direction: Uses movement direction, or forward (Z+) if stationary
  - Cooldown enforcement: 500ms minimum between shots
- `GetPendingProjectileSpawns()` (lines 263-268): Returns and clears spawn queue
- `GetLastShootTime()` (lines 250-257): Helper for cooldown tracking

**Integration (lines 185-195):**
- Added shooting logic in `UpdateState()` loop
- Checks `isShootPressed` flag from `ProcessInput()`
- Spawns projectile if cooldown ready

### 5. Network Manager Changes
**File:** `GameNetworkManager.cs` ([view](../../Loving%20Away/Loving%20Away(Network%20Game)/Assets/Scripts/Network/GameNetworkManager.cs))

**New Queues & Locks:**
- `incomingProjectileQueue` (line 43)
- `projectileQueueLock` (line 49)

**Server Broadcast (lines 263-296):**
- `BroadcastProjectileSpawns()`: Called after `BroadcastState()`
- Sends to all remote clients via UDP
- Queues for local client (server sees own projectiles)

**Client Receive (lines 403-411):**
- Added `case MessageType.ProjectileSpawn` in `HandleClientReceive()`
- Deserializes and queues for main thread

**Event System (lines 493-502):**
- Added `OnProjectileSpawn` event delegate
- `BroadcastProjectileSpawn()` notifies subscribers

**Main Thread Processing (lines 450-459):**
- `UpdateClient()` processes projectile queue
- Invokes `OnProjectileSpawn` on main thread (Unity-safe)

### 6. Client-Side Projectile Rendering
**File:** `SimplePlayerController.cs` ([view](../../Loving%20Away/Loving%20Away(Network%20Game)/Assets/Scripts/Gameplay/SimplePlayerController.cs))

**New Fields:**
- `public GameObject projectilePrefab` (line 16) - Optional, creates dynamically if null
- `private Dictionary<uint, GameObject> projectileObjects` (line 33)

**Event Subscription (line 90):**
```csharp
networkManager.OnProjectileSpawn += HandleProjectileSpawn;
```

**Handler Method (lines 413-443):**
- `HandleProjectileSpawn()`: Instantiates projectile GameObject
  - Uses `projectilePrefab` if assigned, otherwise creates empty GameObject
  - Adds `Projectile` component if not present
  - Calls `Initialize()` with spawn message data
  - Tracks in `projectileObjects` dictionary

---

## Architecture Flow

```
[PLAYER PRESSES SPACEBAR]
         ↓
SimplePlayerController.CollectInput() → shootButtonPressed = true
         ↓
SimplePlayerController.SendInputToServer() → ClientInputMessage
         ↓
GameNetworkManager.SendInput() → Queues outgoing input
         ↓
[CLIENT THREAD] → Sends UDP packet (18 bytes)
         ↓
[SERVER THREAD] → Receives packet
         ↓
GameNetworkManager.HandleServerReceive() → Queues for main thread
         ↓
[MAIN THREAD] GameNetworkManager.UpdateServer()
         ↓
ServerGameState.ProcessInput() → Sets player.isShootPressed = true
         ↓
ServerGameState.UpdateState() → Detects shoot button + cooldown ready
         ↓
ServerGameState.SpawnProjectile()
  - Assigns projectileId (increments counter)
  - Calculates start position (player.pos + Vector3.up * 2)
  - Calculates velocity (movement direction * 15.0f)
  - Queues ProjectileSpawnMessage
         ↓
GameNetworkManager.BroadcastProjectileSpawns()
  - Serializes to 37-byte packet
  - Sends to all clients (remote + local)
         ↓
[CLIENT THREAD] → Receives projectile spawn packet
         ↓
GameNetworkManager.HandleClientReceive() → Queues for main thread
         ↓
[MAIN THREAD] GameNetworkManager.UpdateClient()
         ↓
GameNetworkManager.BroadcastProjectileSpawn() → Invokes OnProjectileSpawn event
         ↓
SimplePlayerController.HandleProjectileSpawn()
  - Instantiates GameObject
  - Adds Projectile component
  - Calls Initialize()
         ↓
Projectile.Initialize()
  - Sets position, velocity, IDs
  - Starts Update() loop
         ↓
[EVERY FRAME] Projectile.Update()
  - position += velocity * deltaTime
  - Check if lifetime expired → Destroy()
```

---

## Testing Checklist

### Unity Scene Setup

**Before testing:**
1. Open scene: `Loving Away/Loving Away(Network Game)/Assets/Scenes/[YourScene].unity`
2. Ensure GameNetworkManager GameObject exists with:
   - `Is Server` = true (for server instance)
   - `Server Port` = 9050
   - `Server Tick Rate` = 20
3. Ensure SimplePlayerController GameObject exists with:
   - Reference to GameNetworkManager
   - Player Prefab assigned
   - (Optional) Projectile Prefab assigned

### Test 1: Single Player (Server Only)

**Steps:**
1. Press Play in Unity Editor
2. Ensure server starts (check Console for "[Server] UDP Server listening on port 9050")
3. Wait for player spawn (green cube should appear)
4. Move with WASD (verify movement works)
5. **Press SPACEBAR**

**Expected Results:**
- Console shows: `[ServerGameState] Player 0 spawned projectile 1 at [position]`
- Console shows: `[SimplePlayerController] Spawned projectile 1 from player 0`
- **Yellow glowing sphere appears** above player
- Projectile flies in movement direction (or forward if stationary)
- Projectile disappears after 2 seconds
- Console shows: `[Projectile] Projectile 1 expired after 2s`

**Cooldown Test:**
- Hold SPACEBAR continuously
- New projectiles should spawn every 0.5 seconds (not every frame)

### Test 2: Two Players (Server + Remote Client)

**Setup:**
1. Build executable: `File → Build Settings → Build`
2. Place build in `Builds/` folder
3. In Unity Editor:
   - GameNetworkManager: `Is Server` = true
   - SimplePlayerController: `Local Player Id` = 0
4. In Built Executable:
   - Edit config or in-game UI: `Is Server` = false
   - `Server Address` = "127.0.0.1" (or LAN IP if separate machines)
   - `Local Player Id` = 1 (will be auto-assigned by server)

**Steps:**
1. Start Unity Editor (server) first
2. Start built executable (client) second
3. Wait for connection (check Console: "[Server] New client connected")
4. Both players should see each other (green = local, red = remote)
5. **Player 0 (server): Press SPACEBAR**
6. **Player 1 (client): Press SPACEBAR**

**Expected Results:**
- ✅ Both players see Player 0's projectiles
- ✅ Both players see Player 1's projectiles
- ✅ Projectiles shoot in movement direction
- ✅ Cooldown prevents spam (0.5s minimum between shots)
- ✅ No lag/jitter (projectiles move smoothly at 60 FPS even though server is 20Hz)

### Test 3: Network Stress Test

**Steps:**
1. With 2 players connected
2. Both players hold SPACEBAR while moving in circles
3. Run for 30 seconds

**Expected Results:**
- ✅ No crashes or errors
- ✅ Projectiles continue spawning at regular intervals
- ✅ Frame rate stays stable (60 FPS)
- ✅ No memory leaks (projectiles destroyed after 2s)
- ✅ Packet counts increase steadily (check debug UI)

---

## Known Limitations (To Be Fixed in Session 2+)

1. **Linear trajectory only** - Projectiles fly straight (Session 2 will add arc)
2. **No hit detection** - Projectiles pass through players (Session 3 will add collision)
3. **No visual feedback on shoot** - No muzzle flash (exists in ShootVisualFeedback.cs, needs integration)
4. **No projectile prefab requirement** - Creates simple sphere dynamically
5. **No projectile cleanup tracking** - Relies on self-destruct, no centralized cleanup

---

## Troubleshooting

### Issue: No projectiles appear

**Check:**
1. Console errors? Look for serialization or component errors
2. Is `HandleProjectileSpawn()` being called? (Add breakpoint or Debug.Log)
3. Is `OnProjectileSpawn` event subscribed? (Check line 90 in SimplePlayerController.cs)
4. Is server spawning projectiles? (Check Console for "[ServerGameState] Player X spawned projectile Y")

**Solution:**
- Ensure `shootButtonPressed = true` when spacebar held (check CollectInput())
- Verify cooldown not blocking (try setting `projectileCooldown = 0.1f` in ServerGameState.cs)

### Issue: Projectiles spawn but don't move

**Check:**
1. Is `Projectile.velocity` set correctly? (Add Debug.Log in Initialize())
2. Is `Update()` being called? (Add Debug.Log)

**Solution:**
- Verify `spawnMsg.velocity` is not Vector3.zero
- Check server's `SpawnProjectile()` method calculates velocity correctly

### Issue: Projectiles spawn in wrong location

**Check:**
1. Player position vs projectile startPosition (Debug.Log both)
2. Is `projectileHeight = 2.0f` too high/low?

**Solution:**
- Adjust `projectileHeight` in ServerGameState.cs line 27
- Verify `player.position` is correct in ServerGameState.cs

### Issue: Remote client doesn't see projectiles

**Check:**
1. Is server broadcasting? (Check `BroadcastProjectileSpawns()` called)
2. Is client receiving packets? (Check `HandleClientReceive()` case ProjectileSpawn)
3. Are packets being queued? (Check `incomingProjectileQueue.Count`)

**Solution:**
- Verify `OnProjectileSpawn` event fired on client
- Check network manager's `packetsSent` and `packetsReceived` in debug UI

---

## Files Modified

| File | Lines Changed | Type |
|------|---------------|------|
| `NetworkProtocol.cs` | +25 | Protocol definition |
| `Serializer.cs` | +66 | Binary serialization |
| `Projectile.cs` | +113 | NEW FILE - Component |
| `ServerGameState.cs` | +75 | Server game logic |
| `GameNetworkManager.cs` | +65 | Network layer |
| `SimplePlayerController.cs` | +35 | Client rendering |

**Total:** ~379 lines added

---

## Git Commit Message Template

```bash
git add .
git commit -m "feat(Phase4-Session1): Add projectile foundation

- Added ProjectileSpawnMessage to NetworkProtocol (37 bytes)
- Implemented binary serialization for projectile spawns
- Created Projectile.cs with linear trajectory and 2s lifetime
- Server spawns projectiles on shoot button (0.5s cooldown)
- Clients receive and render projectile spawns via UDP
- Both players see projectiles from all players

Testing: Both players see yellow projectiles when spacebar pressed
Next: Session 2 - Implement arc trajectory with parametric curves
"
```

---

## Next Session Context

**For Session 2: Arc Trajectory & Visual Polish**

### What You'll Need to Know:
1. **Current trajectory:** Projectiles use `position += velocity * deltaTime` (Projectile.cs line 63)
2. **Replace with:** Parametric arc using `startPosition`, `targetPos`, and `arcHeight`
3. **Message changes:** Add `targetPos` and `arcHeight` to `ProjectileSpawnMessage`
4. **Keep:** All networking code from Session 1 (just enhance the message and trajectory)

### Files You'll Modify:
- `NetworkProtocol.cs` - Expand `ProjectileSpawnMessage` (add targetPos, arcHeight)
- `Serializer.cs` - Update serialization (add 2 Vector3s = +24 bytes → 61 bytes total)
- `Projectile.cs` - Replace `Update()` with parametric arc calculation
- `ServerGameState.cs` - Calculate `targetPos` based on velocity + range

### Key Formula for Session 2:
```csharp
// Parametric arc (bezier-like)
float t = elapsedTime / totalFlightTime; // 0.0 to 1.0
Vector3 horizontalPos = Vector3.Lerp(startPos, targetPos, t);
float heightOffset = arcHeight * 4 * t * (1 - t); // Parabola: 0 → peak → 0
Vector3 finalPos = horizontalPos + Vector3.up * heightOffset;
```

---

## Session 1 Completion Status

- [x] Protocol definition (ProjectileSpawnMessage)
- [x] Binary serialization (serialize/deserialize methods)
- [x] Projectile component (GameObject with linear movement)
- [x] Server spawning logic (detect shoot, create spawn message)
- [x] Network broadcasting (server → clients via UDP)
- [x] Client rendering (instantiate GameObject on spawn event)
- [ ] **Testing by user** (pending Unity testing)

**Status:** ✅ CODE COMPLETE - Ready for user testing

---

*This document was auto-generated for Phase 4 Session 1 handoff.*
*Last updated: 2025-11-20*
