# Session 3: Server-Side Hit Detection & Knockback - COMPLETE ✅

**Date:** 2025-12-14
**Branch:** Phase_4
**Status:** Code complete, tested in Unity Editor

---

## What Was Implemented

### 1. ProjectileHit Message Protocol

**Files Modified:**
- `NetworkProtocol.cs` (lines 6-13, 123-142)

**Changes:**
- Added `ProjectileHit = 6` to `MessageType` enum
- Created `ProjectileHitMessage` struct:
  ```csharp
  public struct ProjectileHitMessage
  {
      public MessageType messageType;  // 1 byte
      public uint projectileId;        // 4 bytes
      public uint targetPlayerId;      // 4 bytes
      public Vector3 hitPosition;      // 12 bytes
      // TOTAL: 21 bytes
  }
  ```

### 2. Hit Message Serialization

**Files Modified:**
- `Serializer.cs` (lines 288-340)

**Changes:**
- Added `SerializeProjectileHit()` method - converts struct to 21-byte array
- Added `DeserializeProjectileHit()` method - parses byte array back to struct
- Follows existing binary serialization pattern (BinaryWriter/BinaryReader)

### 3. Server-Side Projectile Tracking

**Files Modified:**
- `ServerGameState.cs` (lines 23-35, 42-44, 263-273, 305-332, 392-404)

**New Data Structures:**
```csharp
// Struct definition (lines 392-404)
public struct ServerProjectile
{
    public uint projectileId;
    public uint ownerId;
    public Vector3 startPosition;
    public Vector3 targetPosition;
    public float arcHeight;
    public float flightTime;
    public float spawnTime;  // Server timestamp
}

// Tracking dictionary (line 24)
private Dictionary<uint, ServerProjectile> activeProjectiles;

// Hit message queue (line 23)
private Queue<ProjectileHitMessage> pendingHitMessages;

// Hit detection parameters (lines 34-35)
private float collisionRadius = 0.7f;  // projectile + player radius
private float knockbackForce = 12.0f;  // units per second
```

**Modified Methods:**
- `SpawnProjectile()` (lines 263-273) - Now adds projectile to `activeProjectiles` dictionary
- Added `GetPendingHitMessages()` (lines 305-310) - Returns and clears hit queue
- Added `CalculateProjectilePosition()` (lines 316-332) - Replicates client arc formula server-side

### 4. Collision Detection System

**Files Modified:**
- `ServerGameState.cs` (lines 215, 337-411)

**New Method:** `CheckProjectileCollisions()` (lines 337-411)

**Algorithm:**
```csharp
foreach (projectile in activeProjectiles)
{
    // Check expiration
    if (elapsedTime >= flightTime)
    {
        Remove projectile
        Continue
    }

    // Calculate current position using arc formula
    Vector3 projPos = CalculateProjectilePosition(projectile, serverTime)

    foreach (player in players)
    {
        // Skip owner (no self-hits)
        if (player.playerId == projectile.ownerId) continue

        // 3D distance check
        float distance = Vector3.Distance(projPos, player.position)

        if (distance < 0.7f)  // Collision!
        {
            // Calculate knockback direction (away from projectile)
            Vector3 knockbackDir = (player.position - projPos).normalized

            // Apply knockback to player velocity
            player.velocity += knockbackDir * 12.0f

            // Create hit message
            Queue ProjectileHitMessage(projectileId, playerId, projPos)

            // Remove projectile
            Mark for removal
            Break  // Only one hit per projectile
        }
    }
}

// Remove all marked projectiles
```

**Integration:**
- Called from `UpdateState()` (line 215) after all player positions updated
- Runs at 20 Hz (server tick rate)

**Arc Position Formula (CRITICAL - Must Match Client):**
```csharp
float elapsedTime = currentTime - proj.spawnTime;
float t = Mathf.Clamp01(elapsedTime / proj.flightTime);

// Horizontal: Linear interpolation
Vector3 horizontal = Vector3.Lerp(proj.startPosition, proj.targetPosition, t);

// Vertical: Parabolic arc
float heightOffset = proj.arcHeight * 4f * t * (1f - t);

return new Vector3(horizontal.x, horizontal.y + heightOffset, horizontal.z);
```

### 5. Hit Message Broadcasting

**Files Modified:**
- `GameNetworkManager.cs` (lines 48, 55, 79, 278, 316-349)

**Changes:**
- Added `incomingHitQueue` and `hitQueueLock` (lines 48, 55)
- Initialized queue in `Start()` (line 79)
- Added `BroadcastProjectileHits()` method (lines 316-349):
  - Gets pending hits from server game state
  - Serializes each hit message (21 bytes)
  - Sends to all remote clients via UDP
  - Queues for local client (server also needs to see hits)
- Integrated into `BroadcastState()` (line 278) - called every tick

### 6. Client Hit Message Handling

**Files Modified:**
- `GameNetworkManager.cs` (lines 466-474, 524-533, 587-596)

**Changes:**
- Added `ProjectileHit` case in `HandleClientReceive()` (lines 466-474):
  - Deserializes hit message
  - Adds to `incomingHitQueue` with lock
- Added hit processing in `UpdateClient()` (lines 524-533):
  - Dequeues hit messages on main thread
  - Broadcasts to listeners via event
- Added event delegate and broadcaster (lines 587-596):
  ```csharp
  public delegate void ProjectileHitHandler(ProjectileHitMessage hitMsg);
  public event ProjectileHitHandler OnProjectileHit;
  ```

### 7. Client-Side Hit Feedback

**Files Modified:**
- `SimplePlayerController.cs` (lines 109, 637-670)

**Changes:**
- Subscribed to `OnProjectileHit` event (line 109)
- Added `HandleProjectileHit()` method (lines 637-670):
  ```csharp
  void HandleProjectileHit(ProjectileHitMessage hitMsg)
  {
      // Destroy projectile GameObject
      if (projectileObjects.ContainsKey(hitMsg.projectileId))
      {
          Destroy(projectileObjects[hitMsg.projectileId]);
          projectileObjects.Remove(hitMsg.projectileId);
      }

      // Check if local player was hit
      bool isLocalPlayerHit = (hitMsg.targetPlayerId == localPlayerId);
      bool isSecondPlayerHit = (enableSecondLocalPlayer && hitMsg.targetPlayerId == secondLocalPlayerId);

      if (isLocalPlayerHit)
      {
          UnityEngine.Debug.Log("<color=yellow>[HIT!] You were hit!</color>");
          // TODO: Visual feedback (Session 4)
      }
      else if (isSecondPlayerHit)
      {
          UnityEngine.Debug.Log("<color=cyan>[HIT!] Player 2 was hit!</color>");
      }

      // TODO: Spawn explosion effect at hitMsg.hitPosition (Session 4)
  }
  ```

---

## Architecture Flow

```
[SERVER TICK - 20 Hz]
         ↓
UpdateState(deltaTime)
  ├─ Update player positions
  └─ CheckProjectileCollisions()
         ↓
     For each active projectile:
       ├─ Check expiration (t >= flightTime)
       ├─ Calculate position: CalculateProjectilePosition()
       └─ For each player:
             ├─ Skip if owner
             ├─ Distance = Vector3.Distance(projPos, playerPos)
             └─ If distance < 0.7f:
                   ├─ Apply knockback: player.velocity += knockbackDir * 12.0f
                   ├─ Queue ProjectileHitMessage(projId, playerId, hitPos)
                   └─ Remove projectile from activeProjectiles
         ↓
BroadcastState()
  ├─ Broadcast player states
  ├─ Broadcast projectile spawns
  └─ BroadcastProjectileHits() ← NEW
         ↓
     For each pending hit:
       ├─ Serialize to 21 bytes
       ├─ Send via UDP to all clients
       └─ Queue for local client
         ↓
[CLIENT RECEIVE THREAD]
         ↓
HandleClientReceive(data)
  ├─ Case: ProjectileHit
  └─ Deserialize → Queue to incomingHitQueue
         ↓
[CLIENT MAIN THREAD - Unity Update()]
         ↓
UpdateClient()
  └─ Process incomingHitQueue
         ↓
     BroadcastProjectileHit(hitMsg)
         ↓
     OnProjectileHit event fires
         ↓
SimplePlayerController.HandleProjectileHit()
  ├─ Destroy projectile GameObject
  ├─ Remove from projectileObjects dictionary
  ├─ Log colored "[HIT!]" message if local player
  └─ [TODO: Spawn explosion effect]
```

---

## Testing Checklist

### Server-Side Hit Detection
- [x] Projectiles are added to `activeProjectiles` when spawned
- [x] Projectiles are removed when they expire (t >= flightTime)
- [x] Collision detection triggers when player within 0.7 units
- [x] Projectiles do NOT hit their owner (self-hit prevention)
- [x] Hit is detected at correct arc position (not just ground)
- [x] Multiple projectiles can exist and be tracked simultaneously

### Knockback Physics
- [x] Player velocity changes when hit
- [x] Knockback direction is away from projectile impact point
- [x] Knockback magnitude feels appropriate (~12 units/second)
- [x] Knockback doesn't cause player to teleport
- [x] Multiple hits accumulate velocity correctly

### Network Messages
- [x] `ProjectileHitMessage` serializes to 21 bytes
- [x] Hit messages broadcast to all clients
- [x] Clients receive and deserialize hit messages
- [x] Projectile GameObjects destroyed on client when hit

### Visual Feedback
- [x] Projectile disappears when hit detected
- [x] Hit player sees feedback (colored console log)
- [x] No duplicate hit detections (projectile removed after first hit)

### Edge Cases
- [x] Projectile hitting multiple players (only hits first detected)
- [x] Projectile expiring mid-flight (no crash)
- [x] Dual local player mode works with hit detection
- [x] Rapid-fire projectiles (cooldown prevents spam)

### Tested Scenarios (Unity Editor)
- [x] Dual local player mode (P1: WASD+Space, P2: Arrows+RShift)
- [x] Shooting at stationary target
- [x] Shooting at moving target
- [x] Projectiles arc correctly before hitting
- [x] Knockback pushes players away from impact
- [x] Self-shooting doesn't cause hits
- [x] Console shows colored "[HIT!]" messages

---

## Known Limitations (Session 3 Scope)

1. **No visual hit effects** - Only console logs (defer to Session 4)
   - No explosion particles at impact point
   - No screen shake/flash for hit players
   - No projectile fade-out animation

2. **No death system** - Players just get knocked back infinitely
   - No health tracking
   - No elimination/respawn
   - No game-over conditions

3. **No lag compensation** - Hits detected at current positions only
   - High-latency clients may see incorrect hits
   - No server-side rewind for hit validation
   - No client-side hit prediction

4. **Simple knockback** - No player stun or control interruption
   - Players can still move/shoot while being knocked back
   - No invincibility frames after being hit
   - Knockback doesn't interrupt charging

5. **No hit sounds** - Audio deferred to polish phase

---

## Troubleshooting

### Issue: Projectiles not hitting despite visual collision
**Cause:** Server arc calculation differs from client rendering
**Fix:** Verify `CalculateProjectilePosition()` in ServerGameState.cs exactly matches `Projectile.cs` Update() formula
**Check:** Lines 316-332 in ServerGameState.cs vs lines 74-76 in Projectile.cs

### Issue: Self-hits occurring
**Cause:** Owner ID comparison failing
**Fix:** Verify `projectile.ownerId` is set correctly in `SpawnProjectile()`
**Check:** Line 266 in ServerGameState.cs - `ownerId = playerId`

### Issue: Projectiles not disappearing after hit
**Cause:** Client not receiving hit messages
**Fix:**
1. Check `BroadcastProjectileHits()` is called in `BroadcastState()` (line 278)
2. Verify `OnProjectileHit` event subscription (line 109 in SimplePlayerController.cs)
3. Check `projectileObjects` dictionary contains the projectile ID

### Issue: Hits detected through walls or at wrong positions
**Cause:** Collision detection using wrong position
**Fix:** Verify `CalculateProjectilePosition()` uses `serverTime - spawnTime` for elapsed time
**Check:** Line 318 in ServerGameState.cs

### Issue: Knockback too weak or too strong
**Adjust:** Change `knockbackForce` value in ServerGameState.cs line 35
- Current: 12.0f units/second
- Weaker: Try 8.0f
- Stronger: Try 15.0f

---

## Files Modified Summary

| File | Lines Changed | Type | Key Changes |
|------|---------------|------|-------------|
| `NetworkProtocol.cs` | +21 | Protocol | ProjectileHit enum, ProjectileHitMessage struct |
| `Serializer.cs` | +51 | Serialization | Serialize/Deserialize hit messages |
| `ServerGameState.cs` | +142 | Server Logic | Projectile tracking, collision detection, knockback |
| `GameNetworkManager.cs` | +57 | Networking | Hit message broadcast/receive, event system |
| `SimplePlayerController.cs` | +45 | Client Logic | Hit feedback, projectile destruction |
| **Total** | **~315 lines** | | |

---

## Git Commit Message Template

```bash
git add .
git commit -m "feat(Phase4-Session3): Server-side hit detection and knockback

Server-Side Changes:
- Added ServerProjectile tracking in ServerGameState (activeProjectiles dictionary)
- Implemented CheckProjectileCollisions() with 3D distance check (0.7u radius)
- Arc position calculation server-side (matches client formula exactly)
- Knockback physics: 12 u/s impulse away from impact point
- Projectile expiration tracking (0.667s flight time)

Network Protocol:
- ProjectileHitMessage struct (21 bytes: projId, targetId, hitPos)
- Binary serialization/deserialization in Serializer.cs
- Hit message broadcasting to all clients via UDP
- Thread-safe hit queue with locks

Client-Side Changes:
- OnProjectileHit event subscription in SimplePlayerController
- Projectile GameObject destruction on hit
- Colored console log feedback for local player hits
- Dual local player hit detection support

Testing:
- Dual local player mode (P1: WASD+Space, P2: Arrows+RShift)
- Hit detection at all arc heights (not just ground)
- Self-hit prevention verified
- Knockback stacking on multiple hits

Known Limitations (deferred to Session 4):
- No visual effects (explosion particles, screen shake)
- No death/respawn system
- No lag compensation
- Audio not implemented

Next: Session 4 - Visual effects, death/respawn, arena boundaries
"
```

---

## Next Session Context

**For Session 4: Visual Effects & Death/Respawn System**

### What You'll Need to Know:

1. **Hit Detection is Working**
   - Server detects collisions at 20 Hz
   - Hit messages (21 bytes) broadcast to clients
   - Knockback applies 12 u/s impulse
   - Projectiles are destroyed after first hit

2. **Current Hit Feedback**
   - Console logs only: `<color=yellow>[HIT!]</color>`
   - No particle effects or screen effects
   - Projectile GameObject simply destroyed (no animation)

3. **TODO Markers Left in Code**
   - `SimplePlayerController.cs` line 661: "TODO (Session 4): Add visual feedback"
   - `SimplePlayerController.cs` line 669: "TODO (Session 4): Spawn explosion effect"

### Key Files to Modify for Session 4:

1. **Visual Effects System**
   - Create new `HitEffect.cs` or `ExplosionEffect.cs`
   - Add particle system at `hitMsg.hitPosition`
   - Implement screen shake for local player (camera shake)
   - Add flash/tint effect when hit

2. **Death/Respawn System**
   - `ServerGameState.cs`: Add player health tracking (or instant-death model)
   - `NetworkProtocol.cs`: Add `PlayerDeathMessage` and `PlayerRespawnMessage`
   - `Serializer.cs`: Add death/respawn serialization
   - `SimplePlayerController.cs`: Handle death animation, respawn countdown

3. **Arena Boundary Elimination**
   - `ServerGameState.cs`: Check if player position outside arena radius
   - Current radius: 15 units (line 176)
   - Death trigger: distance > arenaRadius
   - Respawn: random spawn point within safe zone

### Architecture Considerations:

**Death Message Protocol (Suggested):**
```csharp
public struct PlayerDeathMessage
{
    public MessageType messageType;  // 1 byte
    public uint playerId;            // 4 bytes (who died)
    public uint killerId;            // 4 bytes (who killed, 0 = suicide/boundary)
    public Vector3 deathPosition;    // 12 bytes (for death effect)
    // TOTAL: 21 bytes
}
```

**Respawn Flow:**
1. Server detects death (hit or boundary)
2. Broadcast `PlayerDeathMessage`
3. Clients play death animation/effect
4. Server starts respawn timer (3 seconds)
5. Broadcast `PlayerRespawnMessage` with new position
6. Clients teleport player, play spawn effect

### Testing Strategy for Session 4:

1. **Visual Effects:**
   - Hit explosion appears at correct position
   - Screen shake feels impactful but not nauseating
   - Hit flash doesn't obscure gameplay

2. **Death/Respawn:**
   - Players die when hit (or after multiple hits if using health)
   - Respawn countdown displays correctly
   - Players spawn at valid positions (not inside others)
   - Respawn invincibility period (optional)

3. **Arena Boundaries:**
   - Players pushed out of arena die
   - Boundary is clearly visible (visual ring)
   - Dying at boundary shows correct feedback

### Performance Considerations:

- Particle systems: Pool effects, don't Instantiate() every hit
- Screen shake: Use coroutines, cancel on next hit
- Death effects: Auto-destroy after animation completes
- Respawn: Clear old player GameObject before spawning new

---

## Additional Notes

### Why 21 Bytes for Hit Message?

Message size breakdown:
- MessageType (enum): 1 byte
- projectileId: 4 bytes (uint)
- targetPlayerId: 4 bytes (uint)
- hitPosition: 12 bytes (Vector3 = 3 floats)
- **Total: 21 bytes**

Including `hitPosition` adds 12 bytes but enables:
- Accurate explosion effect placement
- Server-authoritative impact location
- Debugging/replay capabilities

Alternative (13 bytes without position) would require clients to calculate impact point, leading to visual inconsistencies.

### Collision Radius Tuning

Current: 0.7 units (projectile 0.2 + player 0.5)

If hits feel too easy/hard:
- Easier hits: Increase to 0.9 (line 34 in ServerGameState.cs)
- Harder hits: Decrease to 0.5
- Test with dual local player for immediate feedback

### Knockback Force Tuning

Current: 12.0 units/second (line 35 in ServerGameState.cs)

Formula: `player.velocity += knockbackDirection * knockbackForce`

If knockback feels weak:
- Increase to 15.0 or 20.0
- Consider multiplier based on charge (future enhancement)

If knockback too strong:
- Decrease to 8.0 or 10.0
- Add velocity cap (currently uncapped, relies on existing physics)

---

*This document was created for Phase 4 Session 3 handoff.*
*Last updated: 2025-12-14*
*Tested in Unity Editor: Functional*
