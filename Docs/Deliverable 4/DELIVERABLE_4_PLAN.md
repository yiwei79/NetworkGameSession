# Deliverable 4: World State Replication - Implementation Plan

> **Deadline:** December 10, 2025
> **Created:** 2025-11-20
> **Last Updated:** 2025-12-20
> **Status:** In Progress (Session 4 Complete - Core Gameplay Done)

---

## Overview

Deliverable 4 implements **World State Replication** as defined in Lab Session 7. This builds on Deliverable 3 (serialization) to add projectile gameplay, smooth rendering via interpolation, and network optimization.

### Academic Requirements (Lab 7)

| Requirement | Points | Status | How We Meet It |
|-------------|--------|--------|----------------|
| Explicit replication model (active/passive) | - | ✅ Done | Passive replication (server-authoritative) |
| Replication packet with 3+ data types | 60% | ✅ Done | Position, velocity, isAlive, projectiles, death/respawn |
| Explicit replication manager | - | ✅ Done | `GameNetworkManager.cs` + `ServerGameState.cs` |
| Accept 2+ clients | - | ✅ Done | Supports 2-4 players |
| All UDP communication | - | ✅ Done | No TCP in gameplay |
| Playability (demo-able) | 10% | ✅ Done | Hit detection, knockback, death/respawn all working |
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

### Session 2: Arc Trajectory + Dual Local Player ✅ COMPLETE

**Goal:** Replace linear trajectory with parabolic arc + add dual local player testing

**What Was Done:**
1. Expanded `ProjectileSpawnMessage` (37 → 53 bytes)
   - Added `targetPosition`, `arcHeight`, `flightTime`
2. Updated `Serializer.cs` with new fields
3. Implemented parametric arc trajectory in `Projectile.cs`
4. Added trail renderer (yellow → orange gradient)
5. **Bonus:** Dual local player testing mode (P1: WASD+Space, P2: Arrows+RShift)
6. **Bonus:** Fixed facing direction (projectiles shoot in last movement direction)

**Files Modified:** NetworkProtocol.cs, Serializer.cs, ServerGameState.cs, Projectile.cs, GameNetworkManager.cs, SimplePlayerController.cs

**Documentation:** [SESSION_2_SUMMARY.md](SESSION_2_SUMMARY.md)

---

### Session 3: Hit Detection & Knockback ✅ COMPLETE

**Goal:** Server-side collision detection and knockback physics

**What Was Done:**
- Added `ProjectileHitMessage` (21 bytes) to protocol
- Created `ServerProjectile` struct for server-side tracking
- Implemented 3D collision detection (0.7u combined radius)
- Knockback physics: 12 u/s impulse away from impact point
- Event system for client-side hit notifications

**Files Modified:** NetworkProtocol.cs, Serializer.cs, ServerGameState.cs, GameNetworkManager.cs, SimplePlayerController.cs

**Documentation:** [SESSION_3_SUMMARY.md](SESSION_3_SUMMARY.md)

---

### Session 4: Death/Respawn & Arena Boundaries ✅ COMPLETE

**Goal:** Complete gameplay loop with death and respawn mechanics

**What Was Done:**
- Added `PlayerDeathMessage` (17 bytes) and `PlayerRespawnMessage` (17 bytes)
- Death triggers: projectile hit + arena boundary violation (>15u from center)
- 3-second respawn timer, spawn at valid position
- Dead players can't move or shoot (server ignores input)
- Updated `PlayerSnapshot` with `isAlive` field (28→29 bytes)
- Client event handlers for death/respawn notifications

**Files Modified:** NetworkProtocol.cs, Serializer.cs, ServerGameState.cs, GameNetworkManager.cs, SimplePlayerController.cs

**Documentation:** [SESSION_4_SUMMARY.md](SESSION_4_SUMMARY.md)

---

### Session 4.5: Visual Effects ✅ COMPLETE

**Goal:** Add visual feedback for hits, death, and respawn

**What Was Done:**
- Created `VisualEffectsManager.cs` with object pooling for particle effects
- Implemented hit effect (yellow/orange explosion particles)
- Implemented death effect (red particles)
- Implemented respawn effect (green/cyan upward sparkles)
- Added screen shake on hit and death (stronger on death)
- All effects auto-return to pool after animation completes

**Files Created:** VisualEffectsManager.cs (NEW)
**Files Modified:** SimplePlayerController.cs

**Documentation:** [SESSION_4.5_PLAN.md](SESSION_4.5_PLAN.md)

---

### Session 5: Interpolation Buffer 📋 PLANNED (Moved from Session 4)

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

### Session 6: Charge Mechanic & Visual Polish 📋 PLANNED (Renumbered)

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

### ~~Session 6: Arena Boundary & Death System~~ ✅ MERGED INTO SESSION 4

> This session's goals were combined with Session 4 (Death/Respawn & Arena Boundaries).
> See [SESSION_4_SUMMARY.md](SESSION_4_SUMMARY.md) for implementation details.

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
| 2 | Arc Trajectory + Dual Local Player | 2h | ✅ Dec 14 |
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
2. ✅ Session 2: Arc Trajectory (better gameplay)
3. Session 3: Hit Detection (proves replication works)
4. Session 4: Interpolation (smooth rendering)

**Should Have (10% grade - Playability):**
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
