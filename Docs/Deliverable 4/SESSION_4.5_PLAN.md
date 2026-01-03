# Session 4.5 Plan: Visual Effects System

## Objective

Add visual polish to the core gameplay loop by implementing particle effects for hits, deaths, and respawns, plus screen shake feedback for local players.

## Context

- **Current Phase:** Phase 4 (Optimization/Polish)
- **Deliverable:** 4 (World State Replication)
- **Dependencies:** Session 4 complete (death/respawn system, arena boundaries)
- **Unity Version:** 6000.2.6f1 (Unity 6)
- **Input System:** New Input System (UnityEngine.InputSystem)

### What Already Exists

| Component | File | Notes |
|-----------|------|-------|
| ShootVisualFeedback | `ShootVisualFeedback.cs` | Charge indicator + muzzle flash pattern |
| TrailRenderer | `Projectile.cs` | Dynamically created trail effect |
| Event handlers | `SimplePlayerController.cs` | HandleProjectileHit, HandlePlayerDeath, HandlePlayerRespawn |
| TODO markers | `SimplePlayerController.cs` | Lines 671, 683, 695, 707, 719 |

### Core Gameplay Loop (Already Working)

```
Move -> Shoot -> Hit -> Knockback -> Death -> Respawn
(WASD)  (Space) (Arc)   (12 u/s)   (3s timer)
```

---

## Architecture Design

### Visual Effects Manager

Create a centralized `VisualEffectsManager` component that:
1. Pre-instantiates pooled particle effects
2. Provides methods to trigger effects at world positions
3. Handles screen shake for camera
4. Auto-destroys effects after completion

**Why a manager?**
- Thread safety: All Unity API calls must happen on main thread (already satisfied by event handlers)
- Performance: Object pooling avoids per-hit Instantiate() calls
- Clean API: Single point of contact for SimplePlayerController

### Particle System Approach

Unity 6 supports:
1. **Built-in Particle System** (Shuriken) - Best for simple effects, no extra setup
2. **VFX Graph** - Overkill for this project, requires additional packages

**Decision:** Use built-in ParticleSystem created via code (no prefabs required)

### Screen Shake Approach

Two options:
1. **Camera.main reference** - Simple but couples to camera hierarchy
2. **Cinemachine Impulse** - Powerful but requires package

**Decision:** Direct Camera.main manipulation with shake coroutine (simpler, no dependencies)

---

## Architecture Impact

### Network Protocol Changes

**None required.** All visual effects are client-side only based on existing network events:
- `OnProjectileHit` - Already provides `hitPosition`
- `OnPlayerDeath` - Already provides `deathPosition`
- `OnPlayerRespawn` - Already provides `respawnPosition`

### File Changes

| File | Changes |
|------|---------|
| `VisualEffectsManager.cs` | **NEW** - Centralized effects manager with pooling |
| `SimplePlayerController.cs` | Add VisualEffectsManager reference, call effect methods from handlers |
| (Optional) `MultiplayerTest.unity` | Attach VisualEffectsManager to GameController or separate GameObject |

### Threading Considerations

- **Main thread only:** All particle effects triggered from event handlers which already run on main thread
- **No worker thread access:** VisualEffectsManager only called from SimplePlayerController.Update() context
- **Safe:** Event handlers (HandleProjectileHit, HandlePlayerDeath, HandlePlayerRespawn) are invoked from GameNetworkManager.Update() which is main thread

---

## Implementation Steps

### Step 1: Create VisualEffectsManager.cs

**File:** `/Users/yiwei/GithubRepos/NetworkGameSession/Loving Away/Loving Away(Network Game)/Assets/Scripts/Gameplay/VisualEffectsManager.cs`

**Features:**
```csharp
public class VisualEffectsManager : MonoBehaviour
{
    // Singleton pattern for easy access
    public static VisualEffectsManager Instance { get; private set; }

    // Pool settings
    [Header("Pool Settings")]
    public int hitEffectPoolSize = 10;
    public int deathEffectPoolSize = 5;
    public int respawnEffectPoolSize = 5;

    // Effect customization
    [Header("Hit Effect")]
    public Color hitColor = Color.yellow;
    public float hitDuration = 0.5f;
    public int hitParticleCount = 30;

    [Header("Death Effect")]
    public Color deathColor = Color.red;
    public float deathDuration = 1.0f;
    public int deathParticleCount = 50;

    [Header("Respawn Effect")]
    public Color respawnColor = Color.green;
    public float respawnDuration = 0.8f;
    public int respawnParticleCount = 40;

    [Header("Screen Shake")]
    public float shakeIntensity = 0.3f;
    public float shakeDuration = 0.2f;

    // Pools
    private Queue<ParticleSystem> hitEffectPool;
    private Queue<ParticleSystem> deathEffectPool;
    private Queue<ParticleSystem> respawnEffectPool;

    // Methods
    public void PlayHitEffect(Vector3 position);
    public void PlayDeathEffect(Vector3 position);
    public void PlayRespawnEffect(Vector3 position);
    public void TriggerScreenShake(float intensity = -1, float duration = -1);
}
```

**Implementation Details:**

1. **Awake():** Initialize pools, create particle systems programmatically
2. **CreateParticleSystem():** Helper to build ParticleSystem with settings
3. **GetFromPool() / ReturnToPool():** Pool management
4. **Screen shake:** Coroutine that displaces Camera.main over time

**Particle System Configuration (per effect):**

```csharp
// Hit Effect (explosion burst)
- Shape: Sphere (radius 0.1)
- Emission: Burst mode, 30 particles
- Lifetime: 0.3-0.5s
- Speed: 3-6 m/s
- Size: 0.1-0.2
- Color: Yellow -> Orange -> Transparent
- Gravity: 2.0 (particles fall slightly)

// Death Effect (larger explosion)
- Shape: Sphere (radius 0.3)
- Emission: Burst mode, 50 particles
- Lifetime: 0.5-1.0s
- Speed: 2-5 m/s
- Size: 0.15-0.3
- Color: Red -> Dark Red -> Transparent
- Gravity: 1.0

// Respawn Effect (upward sparkles)
- Shape: Sphere (radius 0.5)
- Emission: Burst mode, 40 particles
- Lifetime: 0.5-0.8s
- Speed: 1-3 m/s (mostly upward)
- Size: 0.08-0.15
- Color: Green -> Cyan -> Transparent
- Gravity: -1.0 (float upward)
```

**Testing:** Create manager, call each effect method manually in Inspector, verify particles spawn and auto-return to pool.

**Estimated Complexity:** Medium (60-80 lines)

---

### Step 2: Implement Screen Shake

**File:** `VisualEffectsManager.cs` (same file)

**Implementation:**

```csharp
private Coroutine activeShake;
private Vector3 originalCameraPosition;

public void TriggerScreenShake(float intensity = -1, float duration = -1)
{
    if (Camera.main == null) return;

    // Use defaults if not specified
    float actualIntensity = intensity > 0 ? intensity : shakeIntensity;
    float actualDuration = duration > 0 ? duration : shakeDuration;

    // Stop any existing shake
    if (activeShake != null)
    {
        StopCoroutine(activeShake);
        Camera.main.transform.localPosition = originalCameraPosition;
    }

    activeShake = StartCoroutine(ShakeCoroutine(actualIntensity, actualDuration));
}

private IEnumerator ShakeCoroutine(float intensity, float duration)
{
    originalCameraPosition = Camera.main.transform.localPosition;
    float elapsed = 0f;

    while (elapsed < duration)
    {
        float dampening = 1f - (elapsed / duration); // Fade out
        float x = Random.Range(-1f, 1f) * intensity * dampening;
        float y = Random.Range(-1f, 1f) * intensity * dampening;

        Camera.main.transform.localPosition = originalCameraPosition + new Vector3(x, y, 0);

        elapsed += Time.deltaTime;
        yield return null;
    }

    Camera.main.transform.localPosition = originalCameraPosition;
    activeShake = null;
}
```

**Testing:** Call TriggerScreenShake() from console or button, verify camera moves and returns to original position.

**Estimated Complexity:** Simple (25 lines)

---

### Step 3: Integrate with SimplePlayerController

**File:** `/Users/yiwei/GithubRepos/NetworkGameSession/Loving Away/Loving Away(Network Game)/Assets/Scripts/Gameplay/SimplePlayerController.cs`

**Changes:**

1. **Add reference field:**
```csharp
[Header("Visual Effects")]
public VisualEffectsManager visualEffectsManager;
```

2. **Find manager in Start():**
```csharp
// In Start(), after finding networkManager:
if (visualEffectsManager == null)
{
    visualEffectsManager = FindFirstObjectByType<VisualEffectsManager>();
    if (visualEffectsManager == null)
    {
        UnityEngine.Debug.LogWarning("[SimplePlayerController] No VisualEffectsManager found - effects disabled");
    }
}
```

3. **Update HandleProjectileHit() (line ~671):**
```csharp
void HandleProjectileHit(ProjectileHitMessage hitMsg)
{
    // ... existing code ...

    // Session 4.5: Visual effects
    if (visualEffectsManager != null)
    {
        // Spawn explosion at hit position
        visualEffectsManager.PlayHitEffect(hitMsg.hitPosition);

        // Screen shake for local player if they were hit
        if (isLocalPlayerHit || isSecondPlayerHit)
        {
            visualEffectsManager.TriggerScreenShake();
        }
    }
}
```

4. **Update HandlePlayerDeath() (line ~695):**
```csharp
void HandlePlayerDeath(PlayerDeathMessage deathMsg)
{
    // ... existing code ...

    // Session 4.5: Visual effects
    if (visualEffectsManager != null)
    {
        visualEffectsManager.PlayDeathEffect(deathMsg.deathPosition);

        // Extra screen shake for local player death
        if (isLocalPlayerDeath || isSecondPlayerDeath)
        {
            visualEffectsManager.TriggerScreenShake(0.5f, 0.4f); // Stronger shake
        }
    }
}
```

5. **Update HandlePlayerRespawn() (line ~719):**
```csharp
void HandlePlayerRespawn(PlayerRespawnMessage respawnMsg)
{
    // ... existing code ...

    // Session 4.5: Visual effects
    if (visualEffectsManager != null)
    {
        visualEffectsManager.PlayRespawnEffect(respawnMsg.respawnPosition);
    }
}
```

**Testing:** Start multiplayer test, shoot player, verify explosion appears at hit location. Die to boundary, verify death particles. Respawn, verify sparkle effect.

**Estimated Complexity:** Simple (30 lines of changes)

---

### Step 4: Scene Setup

**File:** `MultiplayerTest.unity`

**Changes:**
1. Create empty GameObject named "VisualEffectsManager"
2. Attach VisualEffectsManager.cs script
3. (Optional) Assign to SimplePlayerController.visualEffectsManager field, or leave null for auto-find

**Testing:** Enter play mode, verify no errors in console about missing manager.

**Estimated Complexity:** Simple (scene modification only)

---

### Step 5: (Optional) Arena Boundary Warning

**File:** `VisualEffectsManager.cs` or `SimplePlayerController.cs`

**Feature:** Show visual indicator when player is within 2 units of arena boundary (15u radius).

**Implementation Options:**

**Option A: Tint player color red when near edge**
```csharp
// In SimplePlayerController.PredictLocalPlayerMovement():
float distanceFromCenter = new Vector3(predictedPosition.x, 0, predictedPosition.z).magnitude;
float warningThreshold = arenaRadius - 2f; // 13 units

if (distanceFromCenter > warningThreshold)
{
    float danger = (distanceFromCenter - warningThreshold) / 2f; // 0-1
    // Flash player red
    if (playerObjects.ContainsKey(localPlayerId))
    {
        Renderer r = playerObjects[localPlayerId].GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = Color.Lerp(localPlayerColor, Color.red, danger * Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f);
        }
    }
}
```

**Option B: UI warning text**
```csharp
// Show "DANGER: NEAR BOUNDARY!" text when close to edge
```

**Recommendation:** Option A is simpler and requires no UI setup. Defer Option B to Phase 5 if time permits.

**Estimated Complexity:** Simple (15 lines) - Optional, implement only if core effects work smoothly

---

## Testing Strategy

### Single Player Tests (Editor Mode)

| Test Case | Steps | Expected Result |
|-----------|-------|-----------------|
| Hit effect spawns | Fire projectile at ground | Yellow/orange particles burst at impact |
| Death effect spawns | Walk into boundary (>15u) | Red particles at death position |
| Respawn effect spawns | Wait 3s after death | Green/cyan particles at spawn |
| Screen shake on hit | Get hit by projectile | Camera shakes briefly |
| Screen shake on death | Die to boundary | Stronger, longer camera shake |
| Effects auto-cleanup | Trigger many effects | No memory leak, pools recycle |

### Multiplayer Tests (Editor + Build)

| Test Case | Steps | Expected Result |
|-----------|-------|-----------------|
| Both players see hit effects | P1 shoots P2 | Both clients see explosion at hit point |
| Correct player gets screen shake | P1 shoots P2 | Only P2's view shakes |
| Effects visible across clients | Any death | Both clients see death particles |
| No desync from effects | Extended gameplay | Effects are purely visual, no state impact |

### Edge Cases

| Test Case | Steps | Expected Result |
|-----------|-------|-----------------|
| Rapid hits | Fire many projectiles quickly | Pool handles burst, no errors |
| Simultaneous deaths | Both players exit boundary | Two death effects play correctly |
| Effect during respawn | Shoot player who just respawned | Hit effect plays normally |
| Camera shake stacking | Multiple hits in quick succession | Shakes blend, camera returns to center |

---

## Success Criteria

- [ ] VisualEffectsManager.cs created with object pooling
- [ ] Hit effect: Yellow/orange explosion particles at hit position
- [ ] Death effect: Red particles at death position
- [ ] Respawn effect: Green/cyan upward particles at spawn position
- [ ] Screen shake: Camera shakes when local player is hit
- [ ] Screen shake: Stronger shake on local player death
- [ ] All effects auto-destroy/recycle after animation
- [ ] No performance impact (pooling prevents per-effect allocation)
- [ ] No errors in console during extended play
- [ ] (Optional) Boundary warning visual when player near edge

---

## Estimated Complexity

**Overall: Medium**

| Component | Complexity | Time Estimate |
|-----------|------------|---------------|
| VisualEffectsManager base class | Medium | 30-45 min |
| Particle system creation code | Medium | 30-45 min |
| Screen shake implementation | Simple | 15-20 min |
| SimplePlayerController integration | Simple | 15-20 min |
| Scene setup | Simple | 5-10 min |
| Testing and tuning | Medium | 30-45 min |
| **Total** | | **2-3 hours** |

---

## File Locations Summary

| File | Path | Action |
|------|------|--------|
| VisualEffectsManager.cs | `/Users/yiwei/GithubRepos/NetworkGameSession/Loving Away/Loving Away(Network Game)/Assets/Scripts/Gameplay/VisualEffectsManager.cs` | CREATE |
| SimplePlayerController.cs | `/Users/yiwei/GithubRepos/NetworkGameSession/Loving Away/Loving Away(Network Game)/Assets/Scripts/Gameplay/SimplePlayerController.cs` | MODIFY (lines ~81-120, ~660-720) |
| MultiplayerTest.unity | `/Users/yiwei/GithubRepos/NetworkGameSession/Loving Away/Loving Away(Network Game)/Assets/Scenes/MultiplayerTest.unity` | MODIFY (add GameObject) |

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Shader.Find() returns null in builds | Medium | Use fallback shader ("Particles/Standard Unlit") or pre-assign material |
| Camera.main is null | Low | Add null check before shake operations |
| Pool exhaustion under heavy load | Low | Log warning, create temporary effect if pool empty |
| Performance impact from particles | Low | Limit particle count, short lifetimes |

---

## Pre-Implementation Checklist

- [x] Reviewed existing code patterns (ShootVisualFeedback, Projectile)
- [x] Identified all TODO markers in SimplePlayerController
- [x] Confirmed thread safety (all calls from main thread)
- [x] Designed pooling architecture
- [x] Defined particle system parameters
- [x] Created testing checklist
- [x] Identified shader fallback strategy

---

## Questions for Review

1. **Does the singleton pattern for VisualEffectsManager align with project conventions?**
   - Alternative: Pass manager reference through events
   - Recommendation: Singleton is simpler, consistent with Unity patterns

2. **Should hit effects be different based on projectile owner?**
   - Current plan: Same effect for all hits
   - Enhancement: Could use shooter's color for their projectile impacts

3. **Is the boundary warning feature worth implementing in this session?**
   - Current plan: Mark as optional
   - Recommendation: Only if core effects complete smoothly

---

## Next Session Preview

After Session 4.5 completes visual effects, the next priorities are:

1. **Phase 4 Remaining:** Interpolation buffer, remote player interpolation
2. **Known Issues:** ISSUE-001 (knockback not visible), ISSUE-002 (dead player jitter)
3. **Phase 5:** Polish, audio, lag compensation, final demo

---

*Plan created: 2025-12-20*
*Ready for implementation with `/implement` command*
