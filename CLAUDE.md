# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **Unity educational project** for learning network game programming concepts, currently focused on **Lab Session 1: Threading**. The project demonstrates multithreading patterns in Unity through visual sorting algorithm demonstrations.

**Unity Version**: 6000.2.6f1 (Unity 6)
**Platform**: macOS (Darwin)

## Project Structure

```
NetworkGameSession/
├── Pre-NetNet/                    # Main Unity project
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   └── S_ThreadingExercise.unity  # Main lab scene
│   │   ├── Scripts/
│   │   │   └── BubbleSort.cs     # Threading demo script
│   │   ├── Prefabs/              # Cube Green/Red prefabs
│   │   └── Materials/            # Green/Red materials
│   └── ProjectSettings/
└── Lab Session Threads/
    ├── Lab_Session_1_Threads.pdf  # Lab instructions
    └── P1_Handout.unitypackage    # Lab materials
```

## Critical Unity-Specific Constraints

### Thread Safety Rules
1. **Never call Unity API from worker threads**: `Instantiate()`, `Destroy()`, `transform`, `GameObject`, any `Component` methods will crash
2. **Safe for worker threads**: Pure C# data structures (arrays, lists), math operations, file I/O, network operations
3. **Pattern in this codebase**: Worker thread modifies data (float array), main thread reads data and updates Unity objects in `Update()`

### Namespace Conflicts
- `System.Diagnostics.Debug` conflicts with `UnityEngine.Debug`
- Always use `UnityEngine.Debug.Log()` explicitly when `System.Diagnostics` is imported
- Required because we use `System.Diagnostics.Stopwatch` for performance timing

## Key Architecture Pattern: Computation-Visualization Split

The `BubbleSort.cs` script demonstrates the fundamental threading pattern for Unity:

```
[Worker Thread]          [Shared Memory]          [Main Thread/Update()]
bubbleSort()       --->  float[] array      <---  updateHeights()
quickSort()              (write-only)             (read + Unity API calls)
```

**Why this works without locks:**
- Only ONE thread writes (worker)
- Main thread only reads
- No write-write or read-write conflicts
- Float operations are atomic in C#

## Working with BubbleSort.cs

### Inspector-Exposed Fields
- `public GameObject prefab` - Must be assigned to Cube Green or Cube Red prefab before running

### Testing Configurations

**Multi-threaded BubbleSort (default):**
```csharp
Thread sortThread = new Thread(bubbleSort);
```

**Multi-threaded QuickSort (faster):**
```csharp
// Comment line 39, uncomment:
Thread sortThread = new Thread(quickSortWrapper);
```

**Single-threaded (freezing demo):**
```csharp
// Uncomment line 31, comment lines 39-45
bubbleSort();  // Blocks main thread
```

### Performance Expectations
- **BubbleSort**: 10-30 seconds for 30,000 elements (O(n²))
- **QuickSort**: 50-500ms for 30,000 elements (O(n log n))
- Game maintains 60 FPS during threaded operations

## Common Development Workflow

### Testing in Unity
1. Open `Pre-NetNet/Assets/Scenes/S_ThreadingExercise.unity`
2. Select GameObject with BubbleSort component
3. Assign prefab in Inspector
4. Press Play
5. Check Console for timing logs

### Modifying Threading Code
When editing threading logic:
- Worker thread methods: `bubbleSort()`, `quickSortWrapper()`, `quickSort()`, `partition()`
- Main thread methods: `Update()`, `updateHeights()`, `spawnObjs()`
- Shared data: `float[] array`, `List<GameObject> mainObjects`

### Adding New Sorting Algorithms
1. Create method with signature: `void MySort()` (no parameters, returns void)
2. Add timing with `stopwatch.Start()` and `stopwatch.Stop()`
3. Log with `UnityEngine.Debug.Log()`
4. Create thread: `Thread sortThread = new Thread(MySort);`
5. Start thread: `sortThread.Start();`

## Lab Session Context

This codebase is part of a structured learning curriculum. Each lab session builds on previous concepts:
- **Lab 1 (current)**: Threading fundamentals, algorithm visualization
- Future labs will cover: Networking, multiplayer, game sessions

The `LAB_SESSION_1_SUMMARY.md` file contains complete documentation of all TODOs and learning objectives.

## File Editing Notes

### BubbleSort.cs Specifics
- Line 141: Must use `UnityEngine.Debug.Log()` not `Debug.Log()` (namespace conflict)
- Lines 39-45: Algorithm selection area (comment/uncomment to switch)
- Line 31: Single-threaded test (normally commented out)
- `updateHeights()`: Returns bool for optimization (stops updating when no changes)

### Unity Package Imports
Lab materials come as `.unitypackage` files. These must be imported through Unity Editor (Assets > Import Package > Custom Package), not extracted manually.

## Git Status (Current Branch: main)

Modified files:
- `Pre-NetNet/Assets/Scripts/BubbleSort.cs` - All TODOs completed
- `Pre-NetNet/Assets/Scenes/S_ThreadingExercise.unity` - Lab scene

Recent commits focused on threading implementation and visualization system.
