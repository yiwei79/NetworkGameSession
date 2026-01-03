# Session 4: Death/Respawn System & Arena Boundaries - PLAN

**Goal:** Implement server-authoritative death/respawn mechanics and arena boundary elimination to complete the gameplay loop.

**Branch:** Phase_4
**Estimated Complexity:** Medium (2-3 hours)

---

## Pre-requisites

### Files to Read First
- [x] `ServerGameState.cs` - Current boundary logic (lines 183-194), player state management
- [x] `NetworkProtocol.cs` - Message protocol structures
- [x] `SESSION_3_SUMMARY.md` - Hit detection and knockback implementation

### Understanding Required
- Current arena boundary enforcement (soft push-back at 15u radius)
- Player state tracking (Dictionary<uint, PlayerState>)
- Server tick rate (20 Hz) and timing
- Binary serialization patterns
- Event delegate pattern for client notifications

---

## Architecture Decisions

### 1. Death Trigger Conditions

**Two ways to die:**
1. **Projectile hit** - Immediate death on any hit (no health system for simplicity)
2. **Arena boundary violation** - Death when player distance from center > 15 units

**Rationale:**
- Simple instant-death model keeps implementation focused
- Health system deferred to polish phase (Phase 5)
- Knockback can push players out of bounds for tactical gameplay

### 2. Player State Extension

**Add to PlayerState struct:**
```csharp
public struct PlayerState
{
    // Existing fields...
    public bool isAlive;
    public float deathTime;  // Server timestamp when player died (0 if alive)
    public float respawnTime; // Server timestamp when player can respawn
}
```

**Design Decisions:**
- `isAlive` flag controls input processing and collision
- `deathTime` and `respawnTime` enable 3-second respawn timer
- Dead players remain in `players` dictionary (no remove/re-add complexity)

### 3. Death Message Protocol

**PlayerDeathMessage:**
```csharp
public struct PlayerDeathMessage
{
    public MessageType messageType;  // 1 byte (enum value: 7)
    public uint playerId;            // 4 bytes
    public Vector3 deathPosition;    // 12 bytes (for visual effects in Session 4.5)
    // TOTAL: 17 bytes
}
```

**Design Choice:** Include death position for future particle effects

### 4. Respawn Message Protocol

**PlayerRespawnMessage:**
```csharp
public struct PlayerRespawnMessage
{
    public MessageType messageType;  // 1 byte (enum value: 8)
    public uint playerId;            // 4 bytes
    public Vector3 respawnPosition;  // 12 bytes
    // TOTAL: 17 bytes
}
```

**Why separate message:**
- Client needs to teleport player to exact respawn position (server-authoritative)
- Respawn happens 3 seconds after death (not immediate)
- Allows future extension (choose respawn point, invincibility frames)

### 5. Respawn Logic

**Server-side respawn flow:**
1. Player dies → set `isAlive = false`, `respawnTime = serverTime + 3.0f`
2. Every tick, check if `serverTime >= respawnTime` for dead players
3. On respawn trigger:
   - Set `isAlive = true`
   - Set `position` to valid spawn point (use `GetSpawnPosition()`)
   - Reset `velocity` to zero
   - Queue `PlayerRespawnMessage`

**Spawn point selection:**
- Reuse existing `GetSpawnPosition(playerId)` method
- Guarantees no overlap (players spawn at different angles)
- Always inside arena (5-unit radius from center)

### 6. Arena Boundary Enforcement

**Current implementation (lines 183-194 in ServerGameState.cs):**
- Soft boundary: Players pushed back when exceeding 15-unit radius
- Velocity reduced to 50% on boundary contact

**New implementation:**
- **Death boundary:** Distance from center > 15 units = instant death
- Remove soft push-back (boundary is now lethal)
- Visual warning deferred to Session 4.5

**Design Choice:** Hard boundary simplifies logic and makes knockback tactical

### 7. Dead Player Behavior

**Server-side:**
- Ignore input from dead players (skip in `ProcessInput()`)
- Dead players don't collide with projectiles
- Dead players don't move (velocity frozen at zero)
- Position remains at death location until respawn

**Client-side:**
- Disable local input when dead
- Render dead player at death position (or hide)
- Visual feedback deferred to Session 4.5 (particle effects, camera fade)

---

## Tasks (In Order)

### Task 1: Add Death/Respawn Message Protocols
**Complexity:** Low
**Files:** `NetworkProtocol.cs`

1. Add `PlayerDeath = 7` and `PlayerRespawn = 8` to `MessageType` enum
2. Create `PlayerDeathMessage` struct (17 bytes)
3. Create `PlayerRespawnMessage` struct (17 bytes)
4. Add constructors following existing pattern

**Estimated Lines:** ~40 lines

---

### Task 2: Add Death/Respawn Serialization
**Complexity:** Low
**Files:** `Serializer.cs`

1. Add `SerializePlayerDeath()` and `DeserializePlayerDeath()` methods
2. Add `SerializePlayerRespawn()` and `DeserializePlayerRespawn()` methods
3. Follow existing pattern: messageType (1 byte) + playerId (4) + Vector3 (12)

**Estimated Lines:** ~100 lines

---

### Task 3: Extend PlayerState for Death Tracking
**Complexity:** Low
**Files:** `ServerGameState.cs`

1. Add `isAlive`, `deathTime`, `respawnTime` fields to `PlayerState` struct (line ~461)
2. Initialize `isAlive = true` in `AddPlayer()` method (line ~56)
3. Add constants for respawn delay:
   ```csharp
   private float respawnDelay = 3.0f; // 3 seconds
   ```

**Estimated Lines:** ~10 lines

---

### Task 4: Implement Death Trigger on Projectile Hit
**Complexity:** Low
**Files:** `ServerGameState.cs`

1. Modify `CheckProjectileCollisions()` method (line ~377)
2. After applying knockback, trigger death:
   ```csharp
   // Inside hit detection block (after line 386)
   TriggerPlayerDeath(player.playerId, projectilePosition);
   ```
3. Create `TriggerPlayerDeath(uint playerId, Vector3 deathPosition)` helper method:
   - Set `player.isAlive = false`
   - Set `player.deathTime = serverTime`
   - Set `player.respawnTime = serverTime + respawnDelay`
   - Set `player.velocity = Vector3.zero` (freeze movement)
   - Queue `PlayerDeathMessage`
   - Log death event

**Estimated Lines:** ~30 lines

---

### Task 5: Implement Arena Boundary Death
**Complexity:** Low
**Files:** `ServerGameState.cs`

1. Replace current boundary push-back logic (lines 183-194) with death trigger:
   ```csharp
   // In UpdateState() method
   if (player.isAlive)
   {
       float distanceFromCenter = positionXZ.magnitude;
       if (distanceFromCenter > arenaRadius)
       {
           TriggerPlayerDeath(player.playerId, player.position);
       }
   }
   ```
2. Remove velocity reduction and push-back code (no longer needed)

**Estimated Lines:** ~5 lines (net reduction)

---

### Task 6: Implement Respawn Timer Logic
**Complexity:** Medium
**Files:** `ServerGameState.cs`

1. Add `CheckRespawns()` method (called from `UpdateState()`):
   ```csharp
   private void CheckRespawns()
   {
       foreach (var kvp in players)
       {
           PlayerState player = kvp.Value;

           // Check if dead player can respawn
           if (!player.isAlive && serverTime >= player.respawnTime)
           {
               RespawnPlayer(player.playerId);
           }
       }
   }
   ```

2. Add `RespawnPlayer(uint playerId)` method:
   - Set `isAlive = true`
   - Set `position = GetSpawnPosition(playerId)`
   - Set `velocity = Vector3.zero`
   - Set `deathTime = 0f`
   - Queue `PlayerRespawnMessage`
   - Log respawn event

3. Call `CheckRespawns()` at end of `UpdateState()` (after line 216)

**Estimated Lines:** ~50 lines

---

### Task 7: Add Dead Player Input Filtering
**Complexity:** Low
**Files:** `ServerGameState.cs`

1. Modify `ProcessInput()` method (line ~103)
2. Skip input processing if player is dead:
   ```csharp
   if (!player.isAlive)
   {
       return; // Dead players can't move
   }
   ```

**Estimated Lines:** ~5 lines

---

### Task 8: Add Death/Respawn Message Broadcasting
**Complexity:** Low
**Files:** `ServerGameState.cs`, `GameNetworkManager.cs`

1. **ServerGameState.cs:**
   - Add `Queue<PlayerDeathMessage> pendingDeathMessages`
   - Add `Queue<PlayerRespawnMessage> pendingRespawnMessages`
   - Initialize in constructor
   - Add `GetPendingDeathMessages()` method
   - Add `GetPendingRespawnMessages()` method

2. **GameNetworkManager.cs:**
   - Add `BroadcastPlayerDeaths()` method (similar to `BroadcastProjectileHits()`)
   - Add `BroadcastPlayerRespawns()` method
   - Call both in broadcast cycle (after `BroadcastProjectileHits()`)

**Estimated Lines:** ~100 lines

---

### Task 9: Add Client-Side Death/Respawn Handling
**Complexity:** Medium
**Files:** `GameNetworkManager.cs`, `SimplePlayerController.cs`

1. **GameNetworkManager.cs:**
   - Add death/respawn queues with locks
   - Add `case MessageType.PlayerDeath` in `HandleClientReceive()`
   - Add `case MessageType.PlayerRespawn` in `HandleClientReceive()`
   - Add event delegates: `OnPlayerDeath`, `OnPlayerRespawn`
   - Add broadcast methods: `BroadcastPlayerDeath()`, `BroadcastPlayerRespawn()`

2. **SimplePlayerController.cs:**
   - Subscribe to death/respawn events in `Start()`
   - Add `HandlePlayerDeath(PlayerDeathMessage msg)` method:
     - Check if local player died → disable input collection
     - Log death event (colored console output)
     - Visual feedback deferred to Session 4.5
   - Add `HandlePlayerRespawn(PlayerRespawnMessage msg)` method:
     - Check if local player respawned → re-enable input
     - Teleport player GameObject to respawn position
     - Log respawn event
     - Visual feedback deferred to Session 4.5

**Estimated Lines:** ~150 lines

---

### Task 10: Update PlayerSnapshot to Include Alive State
**Complexity:** Low
**Files:** `NetworkProtocol.cs`, `Serializer.cs`, `ServerGameState.cs`

1. **NetworkProtocol.cs:**
   - Add `isAlive` field to `PlayerSnapshot` struct (line ~76)

2. **Serializer.cs:**
   - Update `SerializeServerStateUpdate()` to write `isAlive` (1 byte per player)
   - Update `DeserializeServerStateUpdate()` to read `isAlive`
   - Adjust packet size documentation (28 → 29 bytes per player)

3. **ServerGameState.cs:**
   - Update `GetPlayerSnapshots()` to include `isAlive` flag (line ~436)

**Estimated Lines:** ~15 lines

**Note:** This ensures remote players render correctly (dead or alive) even before death message arrives.

---

## Testing Checklist

### Death Triggers
- [ ] Player dies when hit by projectile
- [ ] Player dies when leaving arena boundary (> 15 units from center)
- [ ] Dead players stop moving (velocity frozen)
- [ ] Dead players ignore input
- [ ] Death messages broadcast to all clients

### Respawn Logic
- [ ] Dead players respawn after 3 seconds
- [ ] Respawn position is inside arena (5-unit radius)
- [ ] Respawn resets velocity to zero
- [ ] Respawn messages broadcast to all clients
- [ ] Multiple players can die/respawn independently

### Network Messages
- [ ] `PlayerDeathMessage` serializes to 17 bytes
- [ ] `PlayerRespawnMessage` serializes to 17 bytes
- [ ] Messages received by all clients (including server's local client)
- [ ] Death/respawn events visible in console logs

### Edge Cases
- [ ] Player dies during knockback (boundary death mid-flight)
- [ ] Multiple players die simultaneously
- [ ] Player disconnects while dead
- [ ] Projectile hits dead player (should not trigger second death)
- [ ] Rapid respawn cycles (die immediately after respawn)

### Dual Local Player Mode
- [ ] Both local players can die independently
- [ ] Death/respawn works for P1 (WASD) and P2 (Arrows)
- [ ] Correct player ID in death messages

---

## Success Criteria

**Code Complete:**
- ✅ PlayerState tracks alive/dead status
- ✅ Two death triggers: projectile hit + boundary violation
- ✅ Respawn timer (3 seconds)
- ✅ Death/respawn messages broadcast
- ✅ Clients handle death/respawn events
- ✅ Dead players excluded from input processing and collisions

**Functional:**
- ✅ Shooting another player kills them
- ✅ Leaving arena boundary kills player
- ✅ Dead players respawn after 3 seconds
- ✅ Respawned players can move and shoot again
- ✅ Works with dual local player mode

**Ready for Session 4.5:**
- ✅ Death/respawn foundation complete
- ✅ Console logs show all events clearly
- ⏭️ Visual effects can be added (particle effects, camera fade, screen shake)
- ⏭️ Audio effects can be added (death sound, respawn sound)

---

## Files Modified Summary

| File | Changes | Lines |
|------|---------|-------|
| `NetworkProtocol.cs` | Add PlayerDeath/Respawn messages + isAlive in PlayerSnapshot | ~50 |
| `Serializer.cs` | Add death/respawn serialize/deserialize + update PlayerSnapshot | ~115 |
| `ServerGameState.cs` | Death triggers, respawn logic, state tracking, message queues | ~150 |
| `GameNetworkManager.cs` | Death/respawn broadcast + receive handling + events | ~100 |
| `SimplePlayerController.cs` | Client death/respawn feedback + input disabling | ~80 |
| **Total** | | **~495 lines** |

---

## Handoff Notes for Session 4.5

After Session 4 completes, Session 4.5 should focus on:

1. **Hit Visual Effects:**
   - Particle system for projectile impact (explosion at `hitPosition`)
   - Screen shake for local player when hit (CameraShake component)
   - Hit flash/tint effect (material color flash)

2. **Death Visual Effects:**
   - Death particle effect at `deathPosition` (larger explosion)
   - Camera fade-out on death (black screen overlay)
   - Death animation (ragdoll or dissolve effect)

3. **Respawn Visual Effects:**
   - Respawn teleport animation (particle beam)
   - Invincibility frames visual indicator (flickering/glow)
   - Camera fade-in on respawn

4. **Arena Boundary Warning:**
   - Visual warning when near edge (screen vignette, danger color)
   - Arena boundary visual (glowing ring on ground)

---

## Known Limitations (Session 4 Scope)

1. **No visual effects** - Just console logs (defer to Session 4.5)
2. **No audio effects** - Sounds deferred to polish phase
3. **No health system** - Instant death model only
4. **No invincibility frames** - Can die immediately after respawn
5. **No respawn point selection** - Fixed spawn positions based on player ID
6. **No spectator mode** - Dead players see frozen death position

---

## Technical Notes

### Death Trigger Priority

If player is hit by projectile while outside boundary:
1. Projectile hit triggers death first (inside `CheckProjectileCollisions()`)
2. Boundary check skipped (player already dead)
3. Only one death message sent

**Implementation:** Check `isAlive` before boundary check in `UpdateState()`

### Respawn Timer Calculation

```csharp
// On death
player.respawnTime = serverTime + respawnDelay;

// Every tick
if (!player.isAlive && serverTime >= player.respawnTime)
{
    RespawnPlayer(player.playerId);
}
```

**Why this works:**
- `serverTime` increments every tick (20 Hz = 50ms per tick)
- `respawnTime` is absolute timestamp (not relative)
- No drift, no cumulative error

### Thread Safety Considerations

- Death/respawn message queues use same lock pattern as projectile hits
- All player state mutations happen in `ServerProcess` thread
- Client event handlers run in main thread (Unity operations safe)

### Network Packet Size Impact

**Before Session 4:**
- ServerStateUpdate: 6 + 28n bytes (n = player count)

**After Session 4:**
- ServerStateUpdate: 6 + 29n bytes (+1 byte per player for `isAlive`)
- PlayerDeathMessage: 17 bytes (rare event)
- PlayerRespawnMessage: 17 bytes (rare event)

**Impact:** ~4 bytes/s increase for 4 players at 20 Hz (negligible)

### Debugging Tips

1. Add colored console logs for death/respawn:
   - Death: Red text with skull emoji
   - Respawn: Green text with sparkle emoji
2. Log respawn timer countdown in `CheckRespawns()`
3. Test boundary death by moving to arena edge
4. Test projectile death by standing still and shooting

---

*This plan was created for Phase 4 Session 4 implementation.*
*Last updated: 2025-12-14*
