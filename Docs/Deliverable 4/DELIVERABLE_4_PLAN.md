# Deliverable 4: World State Replication - Implementation Plan

> **Deadline:** December 10, 2025
> **Created:** 2025-11-20
> **Last Updated:** 2025-11-20
> **Status:** In Progress (Session 1 Complete)

---

## Overview

Deliverable 4 implements **World State Replication** as defined in Lab Session 7. This builds on Deliverable 3 (serialization) to add projectile gameplay, smooth rendering via interpolation, and network optimization.

### Academic Requirements (Lab 7)

| Requirement | Points | Status | How We Meet It |
|-------------|--------|--------|----------------|
| Explicit replication model (active/passive) | - | ✅ Done | Passive replication (server-authoritative) |
| Replication packet with 3+ data types | 60% | ⏳ Partial | Position, velocity, ~~projectiles~~, ~~health~~ |
| Explicit replication manager | - | ✅ Done | `GameNetworkManager.cs` + `ServerGameState.cs` |
| Accept 2+ clients | - | ✅ Done | Supports 2-4 players |
| All UDP communication | - | ✅ Done | No TCP in gameplay |
| Playability (demo-able) | 10% | ⏳ Pending | Need projectile hit detection |
| Code quality | 10% | ✅ Good | Clean, documented, organized |
| Robustness/improvements | 20% | ✅ Good | Input delay fixes from D3 |

### Project-Specific Goals

Beyond academic requirements, "Loving Away" needs:
- Charge-and-shoot projectile mechanic with arc trajectory
- Hit detection with knockback
- Smooth remote player rendering (interpolation)
- Arena boundary elimination

---

## Session Breakdown

### Session 1: Projectile Foundation ✅ COMPLETE

**Goal:** Basic projectile system (spawn, network, render)

**What Was Done:**
- Added `ProjectileSpawnMessage` (37 bytes) to protocol
- Server spawns projectiles on shoot (0.5s cooldown)
- Clients receive and render projectiles
- Linear trajectory (2s lifetime)

**Files Modified:** NetworkProtocol.cs, Serializer.cs, ServerGameState.cs, GameNetworkManager.cs, SimplePlayerController.cs, Projectile.cs (NEW)

**Documentation:** [SESSION_1_SUMMARY.md](SESSION_1_SUMMARY.md)

---

### Session 2: Arc Trajectory 📋 PLANNED

**Goal:** Replace linear trajectory with parabolic arc

**Tasks:**
1. Expand `ProjectileSpawnMessage` (add `targetPosition`, `arcHeight`, `flightTime`)
   - New size: ~53 bytes (37 + 12 + 4)
2. Update `Serializer.cs` with new fields
3. Modify `ServerGameState.SpawnProjectile()` to calculate target based on range
4. Replace `Projectile.Update()` with parametric arc:
   ```csharp
   float t = elapsedTime / flightTime;
   Vector3 horizontal = Vector3.Lerp(startPos, targetPos, t);
   float height = arcHeight * 4f * t * (1f - t);
   transform.position = horizontal + Vector3.up * height;
   ```
5. Add trail renderer for visual feedback

**Files to Modify:** NetworkProtocol.cs, Serializer.cs, ServerGameState.cs, Projectile.cs

**Estimated Time:** 2 hours

**Success Criteria:**
- Projectiles follow visible arc trajectory
- Arc height varies with charge time (future: Session 5)
- Landing point predictable based on velocity

---

### Session 3: Hit Detection & Knockback 📋 PLANNED

**Goal:** Server-side collision detection and knockback physics

**Tasks:**
1. Add `ProjectileHitMessage` to protocol
   - Fields: projectileId, targetPlayerId, hitPosition, knockbackForce
2. Server tracks active projectiles (Dictionary)
3. Each tick, check projectile-player collisions
4. On hit: Queue knockback event, destroy projectile
5. Client receives hit event, applies visual feedback (screen shake, flash)
6. Knockback physics: Apply impulse to player velocity

**New Files:** May need `ServerProjectile` struct for server-side tracking

**Files to Modify:** NetworkProtocol.cs, Serializer.cs, ServerGameState.cs, GameNetworkManager.cs, SimplePlayerController.cs

**Estimated Time:** 3 hours

**Success Criteria:**
- Projectiles damage players on contact
- Hit players are pushed backward
- Both players see the hit effect

---

### Session 4: Interpolation Buffer 📋 PLANNED

**Goal:** Smooth remote player rendering despite 20Hz updates

**Tasks:**
1. Create `InterpolationBuffer` class
   - Stores last 5-10 `ServerStateUpdateMessage` with timestamps
   - Methods: `AddSnapshot()`, `GetInterpolatedState()`
2. Modify `SimplePlayerController` to use buffer for remote players
3. Render remote players at `currentTime - interpolationDelay` (100ms)
4. Interpolate between two closest snapshots using `Vector3.Lerp()`
5. Handle edge cases: buffer empty, single snapshot, extrapolation

**New Files:** InterpolationBuffer.cs (or add to SimplePlayerController.cs)

**Files to Modify:** SimplePlayerController.cs

**Estimated Time:** 2-3 hours

**Success Criteria:**
- Remote players move smoothly at 60 FPS (not 20 FPS jerky)
- 100ms render delay is imperceptible
- No jitter or teleporting

---

### Session 5: Charge Mechanic & Visual Polish 📋 PLANNED

**Goal:** Implement charge-based shooting (hold spacebar)

**Tasks:**
1. Modify `ClientInputMessage` to include `chargeTime` (float)
2. Client tracks charge duration (hold spacebar)
3. Server calculates projectile range/arc based on charge:
   - Min charge (0s): 3 unit range, 1 unit arc height
   - Max charge (2s): 12 unit range, 4 unit arc height
4. Visual feedback: Growing charge sphere (already in ShootVisualFeedback.cs)
5. Muzzle flash on release

**Files to Modify:** NetworkProtocol.cs, Serializer.cs, SimplePlayerController.cs, ServerGameState.cs, ShootVisualFeedback.cs

**Estimated Time:** 2 hours

**Success Criteria:**
- Holding spacebar charges shot
- Longer charge = farther shot
- Visual indicator shows charge level

---

### Session 6: Arena Boundary & Death System 📋 PLANNED

**Goal:** Complete gameplay loop with elimination

**Tasks:**
1. Define danger zone at arena edge (radius 14-15)
2. Players in danger zone take continuous damage or instant elimination
3. Add `PlayerDeathMessage` to protocol
4. Server tracks player alive/dead state
5. Respawn logic (optional for demo)
6. Visual: Arena boundary indicator, death effect

**Files to Modify:** NetworkProtocol.cs, Serializer.cs, ServerGameState.cs, GameNetworkManager.cs, SimplePlayerController.cs

**Estimated Time:** 2 hours

**Success Criteria:**
- Players can be eliminated
- Knockback can push into danger zone
- Clear visual feedback for danger zone

---

### Session 7: Testing & Polish 📋 PLANNED

**Goal:** LAN testing, bug fixes, demo preparation

**Tasks:**
1. Build for Windows (test MacBook ↔ Windows PC)
2. Test with real network latency
3. Add latency simulation option in debug UI
4. Fix any discovered bugs
5. Performance profiling
6. Prepare demo script

**Estimated Time:** 3+ hours

**Success Criteria:**
- Game works on LAN (not just localhost)
- Handles 100ms+ latency gracefully
- Ready for professor demo

---

## Timeline

| Session | Content | Est. Time | Target Date |
|---------|---------|-----------|-------------|
| 1 | Projectile Foundation | 2h | ✅ Nov 20 |
| 2 | Arc Trajectory | 2h | Nov 22-24 |
| 3 | Hit Detection & Knockback | 3h | Nov 25-27 |
| 4 | Interpolation Buffer | 3h | Nov 28-30 |
| 5 | Charge Mechanic | 2h | Dec 1-3 |
| 6 | Arena Boundary & Death | 2h | Dec 4-6 |
| 7 | Testing & Polish | 3h | Dec 7-10 |

**Total Estimated:** ~17 hours
**Deadline:** December 10, 2025

---

## Priority Order (If Time Constrained)

**Must Have (60% grade - Replication):**
1. ✅ Session 1: Projectile Foundation
2. Session 3: Hit Detection (proves replication works)
3. Session 4: Interpolation (smooth rendering)

**Should Have (10% grade - Playability):**
4. Session 2: Arc Trajectory (better gameplay)
5. Session 6: Arena Boundary (win/lose condition)

**Nice to Have (20% grade - Polish):**
6. Session 5: Charge Mechanic (depth)
7. Session 7: LAN Testing (robustness)

---

## Technical Notes

### Packet Size Budget

| Message | Current | After Changes |
|---------|---------|---------------|
| ClientInputMessage | 18 bytes | ~22 bytes (+ chargeTime) |
| ServerStateUpdate | 6 + 28n bytes | Same |
| ProjectileSpawnMessage | 37 bytes | ~53 bytes (+ target, arc, time) |
| ProjectileHitMessage | NEW | ~21 bytes |
| PlayerDeathMessage | NEW | ~9 bytes |

### Architecture Decisions

1. **Server-authoritative hit detection:** Server checks collisions, not clients
2. **Interpolation delay:** 100ms (3 snapshots at 20Hz) - balance between smoothness and responsiveness
3. **No Unity physics:** Keep kinematic formulas for predictable network sync
4. **Projectile lifetime:** Server-managed, destroyed on hit or timeout

---

## How to Use This Plan

### Starting a Session

1. Open new Claude Code chat
2. Run `/session-start`
3. Tell Claude: "Continue with Session X from DELIVERABLE_4_PLAN.md"
4. Claude will read this plan and the previous session summary

### During a Session

1. Run `/plan` to create SESSION_X_PLAN.md (detailed task breakdown)
2. Run `/implement` to execute
3. Run `/test` to validate
4. Run `/document` to update docs

### Ending a Session

1. Run `/session-end`
2. This updates PROJECT_STATUS.md and creates SESSION_X_SUMMARY.md
3. Commit changes

---

## Dependencies

```
Session 1 (Foundation) ←── Session 2 (Arc)
         ↓
Session 3 (Hit Detection) ←── Session 5 (Charge)
         ↓
Session 4 (Interpolation)
         ↓
Session 6 (Arena/Death)
         ↓
Session 7 (Testing)
```

**Critical Path:** 1 → 3 → 4 → 6 → 7
**Can be parallel:** Session 2 (Arc) can be done anytime after Session 1

---

*This plan is the roadmap for Deliverable 4. Update after each session completion.*
