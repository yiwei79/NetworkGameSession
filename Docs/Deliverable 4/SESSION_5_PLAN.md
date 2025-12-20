# Session 5 Plan: Visual Polish and UI System

## Objective

Add visual dressing and UI system to make the game presentable for Deliverable 4 grading (10% "Playability" criteria).

## Context

- **Current Phase:** Phase 4 (Polish and Testing)
- **Deliverable:** D4 - World State Replication (Lab 7)
- **Dependencies:** All gameplay features complete (movement, projectiles, death/respawn, visual effects)
- **Timeline:** 1-2 implementation sessions (6-12 hours total)

## Current Visual State

| Element | Current | Target |
|---------|---------|--------|
| Players | Cubes with color | Distinct characters or polished primitives |
| Arena | Flat plane | Textured ground with decorations |
| Projectiles | Yellow sphere + trail | Already good (keep as-is) |
| Effects | Particle systems | Already good (keep as-is) |
| UI | Debug-only OnGUI | Main menu, pause menu, minimal HUD |

---

## Architecture Decisions

### Key Principle: Separation of Visuals from Network Logic

The player visual representation must be completely decoupled from `SimplePlayerController` networking code. This ensures visual changes cannot break the network system.

### Pattern: PlayerVisualController Component

```
Hierarchy:
  Player_0 (GameObject)
    ├── SimplePlayerController reads position/rotation from server
    ├── PlayerVisualController (NEW) - manages visual representation
    │     ├── References visual child object
    │     ├── Syncs rotation to facingDirection
    │     └── Handles death/respawn visibility
    └── VisualModel (Child GameObject)
          ├── 3D model or primitive assembly
          └── Materials, animations (if any)
```

**Why this pattern:**
1. `SimplePlayerController` continues to set `transform.position` as normal
2. `PlayerVisualController` reads from parent and applies to visual child
3. Swapping visuals = replacing the child GameObject only
4. Zero changes to network code

### Scene Management Strategy

**Two-Scene Approach:**
1. `MainMenu.unity` - Title screen, join/host UI
2. `MultiplayerTest.unity` (existing) - Gameplay scene

**Scene Transition:**
- MainMenu collects server/client settings
- Uses `SceneManager.LoadScene()` with `DontDestroyOnLoad` for settings object
- GameNetworkManager reads settings on Start()

**Why not single scene:**
- Cleaner separation
- MainMenu can have different camera, lighting
- No need to hide/show complex UI at runtime

### Pause Menu Architecture

**Client-Side Only Pause:**
- ESC toggles pause panel visibility
- Sets `Time.timeScale = 0` for local pause effect
- **Does NOT pause server** - other players continue normally
- Cursor becomes visible in pause state

**Why not networked pause:**
- Scope creep - requires new message type
- One player shouldn't freeze others
- Common pattern in multiplayer games

---

## Implementation Tasks

### Task 1: Arena Visual Dressing (1.5-2 hours)

**Goal:** Replace flat plane with textured ground and add scene decorations

**File Changes:**

| File | Action | Description |
|------|--------|-------------|
| `Assets/Scripts/Gameplay/ArenaSetup.cs` | NEW | Runtime arena creation script |
| `MultiplayerTest.unity` | MODIFY | Add ArenaSetup object |

**Implementation Details:**

**ArenaSetup.cs:**
```csharp
public class ArenaSetup : MonoBehaviour
{
    [Header("Arena Settings")]
    public float arenaRadius = 15f;
    public Material groundMaterial;
    public Color groundColor = new Color(0.4f, 0.6f, 0.3f); // Grass green

    [Header("Decorations")]
    public bool generateDecorations = true;
    public int treeCount = 8;
    public int rockCount = 12;

    void Start()
    {
        CreateGround();
        CreateBoundaryVisual();
        if (generateDecorations) CreateDecorations();
    }

    void CreateGround()
    {
        // Create circular arena floor using cylinder with flat top
        // Scale: radius * 2 for diameter, 0.1 height
        // Apply material or procedural color
    }

    void CreateBoundaryVisual()
    {
        // Create subtle ring at arena edge (radius = 15)
        // Warns players of elimination zone
    }

    void CreateDecorations()
    {
        // Generate trees/rocks outside playable area (radius > 16)
        // Use primitive combinations (capsule + sphere for tree)
        // Random positions, rotations
    }
}
```

**Decoration Primitives (no external assets required):**
- **Tree:** Green capsule (trunk) + green sphere (foliage)
- **Rock:** Gray scaled cube or sphere with slight rotation
- **Mushroom:** Small cylinder + half-sphere (cute theme)

**If user has asset package:**
- Add `public GameObject[] decorationPrefabs` field
- Instantiate from prefabs instead of primitives
- Same random placement logic

**Testing:**
- [ ] Ground visible and correctly sized (30-unit diameter)
- [ ] Decorations don't spawn inside playable area
- [ ] No performance impact (decorations are static)

---

### Task 2: Player Character Replacement (2-2.5 hours)

**Goal:** Replace cube players with more visually appealing representation

**File Changes:**

| File | Action | Description |
|------|--------|-------------|
| `Assets/Scripts/Gameplay/PlayerVisualController.cs` | NEW | Visual representation manager |
| `Assets/Scripts/Gameplay/SimplePlayerController.cs` | MODIFY | Use PlayerVisualController for spawning |

**Implementation Details:**

**PlayerVisualController.cs:**
```csharp
public class PlayerVisualController : MonoBehaviour
{
    [Header("Visual References")]
    public Transform visualRoot; // Child containing the visual model

    [Header("Character Settings")]
    public float modelScale = 1f;
    public Vector3 modelOffset = Vector3.zero;

    [Header("State")]
    private bool isAlive = true;
    private Vector3 facingDirection = Vector3.forward;

    public void SetFacingDirection(Vector3 direction)
    {
        if (direction.magnitude > 0.1f)
        {
            facingDirection = direction.normalized;
            if (visualRoot != null)
            {
                visualRoot.rotation = Quaternion.LookRotation(facingDirection);
            }
        }
    }

    public void SetAliveState(bool alive)
    {
        isAlive = alive;
        if (visualRoot != null)
        {
            // Option A: Hide when dead
            visualRoot.gameObject.SetActive(alive);
            // Option B: Ghost effect (semi-transparent)
            // SetGhostMode(!alive);
        }
    }

    public void SetPlayerColor(Color color)
    {
        // Apply color to all renderers in visualRoot
    }
}
```

**Character Visual Options (in order of preference):**

**Option A: Enhanced Primitives (Recommended - No External Assets)**
```
Character Assembly:
  - Body: Capsule (1.0 height, 0.4 radius) - Player color
  - Head: Sphere (0.35 radius) at top - Slightly brighter shade
  - Face: Small sphere (0.08 radius) for single eye/dot - White
  - Shadow: Scaled circle on ground (decal or projector)
```

**Option B: If User Has Character Assets**
- Import low-poly character model
- Apply player color to material
- T-pose is fine (no animation needed)
- Use LOD0 only for performance

**Option C: Emoji Theme (Alternative)**
```
Character Assembly:
  - Body: Large sphere (0.6 radius) - Yellow
  - Face decal: 2D emoji texture on quad - Varies by player
  - Small bounce animation (optional)
```

**Modification to SimplePlayerController.cs:**

In `CreatePlayerObject()` (around line 507):
```csharp
void CreatePlayerObject(uint playerId)
{
    // Instead of simple cube instantiation:
    // 1. Create root GameObject
    // 2. Add PlayerVisualController component
    // 3. Create visual as child (primitives or prefab)
    // 4. PlayerVisualController manages visual updates
}
```

**Integration with existing code:**

In `UpdatePlayerVisual()` (around line 405):
```csharp
void UpdatePlayerVisual(PlayerSnapshot snapshot)
{
    // Existing position update code...

    // NEW: Update visual controller
    PlayerVisualController visual = playerVisuals[snapshot.playerId];
    if (visual != null)
    {
        visual.SetFacingDirection(snapshot.velocity.normalized);
        visual.SetAliveState(snapshot.isAlive);
    }
}
```

**Testing:**
- [ ] Character visuals spawn correctly for all players
- [ ] Colors differentiate local, second local, and remote players
- [ ] Characters rotate to face movement direction
- [ ] Dead characters are hidden/ghosted
- [ ] Network still works (position updates apply correctly)

---

### Task 3: Main Menu Scene (1.5-2 hours)

**Goal:** Create title screen with host/join options

**File Changes:**

| File | Action | Description |
|------|--------|-------------|
| `Assets/Scripts/UI/MainMenuController.cs` | NEW | Menu logic and UI bindings |
| `Assets/Scripts/UI/NetworkSettings.cs` | NEW | Settings data holder (DontDestroyOnLoad) |
| `Assets/Scenes/MainMenu.unity` | NEW | Menu scene |
| `Build Settings` | MODIFY | Add MainMenu as scene 0 |

**Implementation Details:**

**MainMenuController.cs:**
```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // Or UnityEngine.UI for legacy InputField

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;
    public Button quitButton;
    public TMP_InputField ipInputField; // Or InputField
    public GameObject joinPanel;

    [Header("Scene Settings")]
    public string gameSceneName = "MultiplayerTest";

    void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        // Default IP
        if (ipInputField != null)
            ipInputField.text = "127.0.0.1";

        // Hide join panel initially
        if (joinPanel != null)
            joinPanel.SetActive(false);
    }

    void OnHostClicked()
    {
        // Create settings object and mark DontDestroyOnLoad
        NetworkSettings settings = CreateOrGetSettings();
        settings.isServer = true;
        settings.serverAddress = "127.0.0.1";

        SceneManager.LoadScene(gameSceneName);
    }

    void OnJoinClicked()
    {
        // Toggle join panel visibility
        // When "Connect" is clicked in join panel:
        NetworkSettings settings = CreateOrGetSettings();
        settings.isServer = false;
        settings.serverAddress = ipInputField.text;

        SceneManager.LoadScene(gameSceneName);
    }

    void OnQuitClicked()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    NetworkSettings CreateOrGetSettings()
    {
        NetworkSettings existing = FindFirstObjectByType<NetworkSettings>();
        if (existing != null) return existing;

        GameObject settingsObj = new GameObject("NetworkSettings");
        NetworkSettings settings = settingsObj.AddComponent<NetworkSettings>();
        DontDestroyOnLoad(settingsObj);
        return settings;
    }
}
```

**NetworkSettings.cs:**
```csharp
public class NetworkSettings : MonoBehaviour
{
    public bool isServer = false;
    public string serverAddress = "127.0.0.1";
    public int serverPort = 9050;
}
```

**UI Layout (Canvas):**
```
Canvas (Screen Space - Overlay)
├── Background Panel (dark overlay)
├── Title Text ("Loving Away")
├── Subtitle Text ("A Cozy Multiplayer Shooter")
├── Buttons Panel (Vertical Layout Group)
│   ├── Host Game Button
│   ├── Join Game Button
│   └── Quit Button
└── Join Panel (initially hidden)
    ├── IP Input Field (with placeholder "Enter IP...")
    └── Connect Button
```

**Styling (Modern, Clean):**
- Font: Unity's default TextMeshPro or create simple style
- Colors: Soft pastels or muted tones
- Buttons: Rounded corners (via sprite or shader), hover effects
- Background: Solid color with subtle gradient

**Modification to GameNetworkManager.cs:**

In `Start()`:
```csharp
void Start()
{
    // Check for NetworkSettings from menu
    NetworkSettings settings = FindFirstObjectByType<NetworkSettings>();
    if (settings != null)
    {
        isServer = settings.isServer;
        serverAddress = settings.serverAddress;
        serverPort = settings.serverPort;
        Destroy(settings.gameObject); // Cleanup after reading
    }

    // Rest of existing Start() code...
}
```

**Testing:**
- [ ] Main menu displays correctly
- [ ] "Host Game" loads game scene as server
- [ ] "Join Game" shows IP input, can connect to server
- [ ] "Quit" exits application
- [ ] Settings persist through scene load

---

### Task 4: Pause Menu and HUD (1.5-2 hours)

**Goal:** Add ESC pause menu and minimize debug UI

**File Changes:**

| File | Action | Description |
|------|--------|-------------|
| `Assets/Scripts/UI/PauseMenuController.cs` | NEW | Pause menu toggle and actions |
| `Assets/Scripts/UI/GameHUD.cs` | NEW | Minimal gameplay HUD |
| `Assets/Scripts/Gameplay/SimplePlayerController.cs` | MODIFY | Toggle debug UI with F3 key |

**Implementation Details:**

**PauseMenuController.cs:**
```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pausePanel;
    public Button resumeButton;
    public Button mainMenuButton;
    public Button quitButton;

    [Header("Settings")]
    public string mainMenuSceneName = "MainMenu";

    private bool isPaused = false;

    void Start()
    {
        pausePanel.SetActive(false);

        resumeButton.onClick.AddListener(Resume);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        quitButton.onClick.AddListener(QuitGame);
    }

    void Update()
    {
        // Check for ESC key
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    public void Pause()
    {
        isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
        // Optionally re-hide cursor for gameplay
    }

    void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // Reset before scene change
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void QuitGame()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
```

**GameHUD.cs (Optional - Minimal):**
```csharp
public class GameHUD : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI playerCountText;
    public TextMeshProUGUI connectionStatusText;

    [Header("Settings")]
    public GameNetworkManager networkManager;
    public float updateInterval = 0.5f;

    private float lastUpdateTime;

    void Update()
    {
        if (Time.time - lastUpdateTime > updateInterval)
        {
            UpdateHUD();
            lastUpdateTime = Time.time;
        }
    }

    void UpdateHUD()
    {
        // Update player count, connection status
        // Minimal - just essential info
    }
}
```

**Modification to SimplePlayerController.cs:**

In `Update()` and `OnGUI()`:
```csharp
void Update()
{
    // ... existing code ...

    // Toggle debug UI with F3
    if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
    {
        showDebugUI = !showDebugUI;
    }
}

void OnGUI()
{
    if (!showDebugUI) return; // Early exit if hidden
    // ... existing debug UI code ...
}
```

**Pause Menu UI Layout:**
```
Canvas (Screen Space - Overlay)
├── Pause Panel (centered, dark semi-transparent background)
│   ├── "PAUSED" Title
│   ├── Resume Button
│   ├── Return to Main Menu Button
│   └── Quit Button
```

**Important Note - Pause Behavior:**
- `Time.timeScale = 0` pauses local physics/animations
- Network threads continue (they don't use Unity time)
- Server continues normally (other players unaffected)
- This is intentional - multiplayer games typically don't pause the server

**Testing:**
- [ ] ESC opens pause menu
- [ ] Resume closes menu and continues game
- [ ] Return to Main Menu loads menu scene
- [ ] Quit exits application
- [ ] F3 toggles debug UI visibility
- [ ] Network continues working while paused

---

### Task 5: Scene Flow Integration (1 hour)

**Goal:** Ensure smooth transitions between scenes

**File Changes:**

| File | Action | Description |
|------|--------|-------------|
| `Assets/Scripts/Network/GameNetworkManager.cs` | MODIFY | Read NetworkSettings on Start |
| Build Settings | MODIFY | Add both scenes |

**Implementation Details:**

**Scene Order in Build Settings:**
1. MainMenu (index 0) - Loads first
2. MultiplayerTest (index 1) - Gameplay

**GameNetworkManager.cs modifications:**

At the start of `Start()`:
```csharp
void Start()
{
    // Read settings from menu (if came from menu)
    NetworkSettings settings = FindFirstObjectByType<NetworkSettings>();
    if (settings != null)
    {
        isServer = settings.isServer;
        serverAddress = settings.serverAddress;
        serverPort = settings.serverPort;

        // Cleanup settings object
        Destroy(settings.gameObject);

        UnityEngine.Debug.Log($"[GameNetworkManager] Loaded settings - Server: {isServer}, Address: {serverAddress}");
    }
    else
    {
        UnityEngine.Debug.Log("[GameNetworkManager] No NetworkSettings found, using Inspector values");
    }

    // ... rest of existing Start() code ...
}
```

**Cleanup on Scene Transition:**

Add to GameNetworkManager:
```csharp
void OnDestroy()
{
    // Existing cleanup code...

    // Important: Reset Time.timeScale in case paused
    Time.timeScale = 1f;
}
```

**Testing:**
- [ ] Game starts at Main Menu
- [ ] Host creates server and joins as player
- [ ] Client can join server via IP
- [ ] Returning to main menu properly cleans up network
- [ ] Can re-host/rejoin after returning to menu

---

## Network Impact Assessment

### Zero Network Code Changes

| Component | Network Impact | Reason |
|-----------|----------------|--------|
| ArenaSetup | None | Static scene objects, client-side only |
| PlayerVisualController | None | Reads from existing position data, no new messages |
| MainMenuController | None | Configures existing isServer/serverAddress fields |
| PauseMenuController | None | Client-side pause, server unaware |
| GameHUD | None | Reads existing stats, no new messages |

### Thread Safety

All new code runs on Unity main thread:
- UI interactions (button clicks)
- Visual updates (transform manipulation)
- Scene loading (Unity API)

No changes to:
- ServerProcess (worker thread)
- ClientProcess (worker thread)
- Message queues or serialization

---

## File Structure Summary

```
Assets/
├── Scenes/
│   ├── MainMenu.unity (NEW)
│   └── MultiplayerTest.unity (existing, minor changes)
│
├── Scripts/
│   ├── Gameplay/
│   │   ├── ArenaSetup.cs (NEW)
│   │   ├── PlayerVisualController.cs (NEW)
│   │   ├── SimplePlayerController.cs (MODIFIED - F3 toggle, visual spawning)
│   │   └── [existing files unchanged]
│   │
│   ├── Network/
│   │   ├── GameNetworkManager.cs (MODIFIED - read NetworkSettings)
│   │   └── [existing files unchanged]
│   │
│   └── UI/ (NEW FOLDER)
│       ├── MainMenuController.cs (NEW)
│       ├── NetworkSettings.cs (NEW)
│       ├── PauseMenuController.cs (NEW)
│       └── GameHUD.cs (NEW - optional)
```

---

## Risk Assessment

### Low Risk

| Risk | Mitigation | Impact if Occurs |
|------|------------|------------------|
| Visual changes break network | Separation pattern ensures independence | None - architecture prevents this |
| Scene transition loses settings | DontDestroyOnLoad for NetworkSettings | Easy to debug and fix |
| Pause menu conflicts with input | Uses wasPressedThisFrame, distinct from gameplay | Minor - adjust input handling |

### Medium Risk

| Risk | Mitigation | Impact if Occurs |
|------|------------|------------------|
| Time.timeScale affects network | Network uses Stopwatch, not Unity time | Already handled in existing code |
| UI canvas blocks gameplay input | Proper event system configuration | Test and adjust raycast blocking |
| Character assets too complex | Default to primitives if issues | Visual downgrade, functionality intact |

### Scope Creep Warning

**Do NOT add:**
- Animated characters (beyond rotation)
- Complex shaders or post-processing
- Audio system
- Settings menu (volume, controls)
- Player name input
- Server browser
- Chat system

These are all Phase 5 or post-D5 features.

---

## Time Estimates

| Task | Estimated Time | Dependencies |
|------|----------------|--------------|
| Task 1: Arena Dressing | 1.5-2 hours | None |
| Task 2: Player Characters | 2-2.5 hours | Task 1 (scene context) |
| Task 3: Main Menu | 1.5-2 hours | None |
| Task 4: Pause Menu + HUD | 1.5-2 hours | Task 3 (scene structure) |
| Task 5: Scene Flow | 1 hour | Tasks 3, 4 |
| **Total** | **7.5-9.5 hours** | |

**Recommended Session Split:**
- **Session 5A:** Tasks 1, 2 (Arena + Characters) - 3.5-4.5 hours
- **Session 5B:** Tasks 3, 4, 5 (UI + Integration) - 4-5 hours

---

## Testing Strategy

### Per-Task Testing (During Implementation)

Each task has inline testing checklist above.

### Integration Testing (After All Tasks)

**Single Player Flow:**
1. [ ] Launch game - see Main Menu
2. [ ] Click "Host Game" - game starts as server
3. [ ] Arena and character visible
4. [ ] Move with WASD - character moves and rotates
5. [ ] Press ESC - pause menu appears
6. [ ] Click Resume - game continues
7. [ ] Press ESC, click "Return to Main Menu" - returns to menu
8. [ ] Click "Quit" - application closes

**Multiplayer Flow:**
1. [ ] Host game in Editor (isServer = true)
2. [ ] Build client executable
3. [ ] Run client, enter "127.0.0.1" in Join panel
4. [ ] Click Connect - client joins game
5. [ ] Both players visible with correct colors
6. [ ] Shoot projectiles - hit effects work
7. [ ] Die from boundary - respawn works
8. [ ] Client presses ESC - only client pauses
9. [ ] Client returns to menu - server continues

**Edge Cases:**
- [ ] Host game, return to menu, re-host - should work
- [ ] Client fails to connect (bad IP) - should show error
- [ ] Multiple scene transitions - no memory leaks
- [ ] F3 toggles debug UI correctly

---

## Success Criteria

- [ ] Game has visually appealing arena with ground texture and decorations
- [ ] Players are represented by distinct character visuals (not plain cubes)
- [ ] Main menu allows hosting or joining games
- [ ] Pause menu accessible with ESC key
- [ ] Debug UI hidden by default, toggleable with F3
- [ ] All existing network functionality preserved
- [ ] No new bugs introduced
- [ ] Performance maintained (60 FPS with 4 players)

---

## Implementation Order (Recommended)

1. **Start with Task 1 (Arena)** - Sets visual context
2. **Then Task 2 (Characters)** - Builds on arena
3. **Then Task 3 (Main Menu)** - Independent, sets up flow
4. **Then Task 4 (Pause)** - Uses same UI patterns as menu
5. **Finally Task 5 (Integration)** - Connects everything

---

## Estimated Complexity

**Medium** - Multiple independent systems that integrate at the end.

**Justification:**
- No network code changes (reduces risk)
- Standard Unity UI patterns (well-documented)
- Primitive-based visuals (no external asset dependencies)
- Clear separation of concerns (each task independent)

**Complexity Factors:**
- Scene management requires careful testing
- UI Canvas configuration can be finicky
- Need to ensure pause doesn't break network

---

## User Decisions (Approved 2025-12-20)

1. **Character Style:** ✅ **Option A - Enhanced Primitives**
   - Capsule body + sphere head
   - Player color applied to body
   - Simple, cute aesthetic matching emoji theme

2. **Arena Assets:** ✅ **Placeholder Primitives with Easy Replacement**
   - User has ground models and decoration models
   - Implementation will use primitives first
   - Design for easy model swapping (prefab references, modular structure)
   - User can replace primitives with asset models after implementation

3. **Color Scheme:** ✅ **Keep Current**
   - Local Player: Green
   - Second Local Player: Blue
   - Remote Players: Red

4. **Timeline:** ✅ **Approved (7.5-9.5 hours)**
   - Session 5A: Visual dressing (3.5-4.5h)
   - Session 5B: UI system (4-5h)

5. **Implementation Approach:** ✅ **Approve plan, implement in future chat sessions**

---

## Implementation Notes for Future Sessions

### Asset Replacement Strategy

**ArenaSetup.cs Design:**
```csharp
[Header("Ground")]
public GameObject groundPrefab; // Can be null (uses primitive) or assigned prefab
public Material groundMaterial;

[Header("Decorations")]
public GameObject[] treePrefabs; // Null = primitive tree, assigned = use prefab
public GameObject[] rockPrefabs; // Null = primitive rock, assigned = use prefab
```

**Pattern:**
- If prefab field is null → Create primitive
- If prefab field assigned → Instantiate prefab
- Same placement logic regardless of primitive vs prefab

**This allows user to:**
1. Run game with primitives initially
2. Drag asset models into Inspector fields later
3. No code changes needed for asset swap

---

## Approval Checklist

- [x] User approves overall approach
- [x] Character style decided (Option A - Enhanced Primitives)
- [x] Asset package contents known (ground + decorations, use placeholders first)
- [x] Scope confirmed (no additions)
- [x] Time estimates acceptable (7.5-9.5h)
- [x] Asset replacement strategy defined

---

*Created: 2025-12-20*
*Status: ✅ APPROVED*
*Next Step: Implementation in future chat sessions (Session 5A, 5B)*
*Implementation Order: Task 1 → Task 2 → Task 3 → Task 4 → Task 5*
