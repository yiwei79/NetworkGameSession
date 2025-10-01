# Lab Session 1: Threads - Summary Document

**Course**: Network Game Sessions
**Topic**: Threading in Unity
**Date Completed**: October 1, 2025

---

## 📋 Learning Objectives

This lab session focuses on:
- Understanding the difference between **Coroutines** (simulated parallelism) and **Threads** (true parallelism)
- Implementing multithreading in Unity using `System.Threading`
- Managing thread safety and data consistency with proper patterns
- Comparing algorithm performance (BubbleSort vs QuickSort)
- Understanding Unity's main thread restrictions for API calls

---

## ✅ Completed Tasks

### TODO 1: Print Array Function (`logArray()`)
**Implementation:**
```csharp
void logArray()
{
    string text = "";
    for (int i = 0; i < array.Length; i++)
    {
        text += array[i].ToString("F2") + " ";
    }
    Debug.Log(text);
}
```

**What it does:**
- Iterates through the entire array
- Formats each float value to 2 decimal places
- Concatenates into a single string
- Prints to Unity console for debugging

**Why it's useful:**
- Helps verify initial random array generation
- Can be called after sorting to confirm results
- Simple debugging tool for array visualization

---

### TODO 2: Spawn GameObjects (`spawnObjs()`)
**Implementation:**
```csharp
void spawnObjs()
{
    for (int i = 0; i < array.Length; i++)
    {
        GameObject obj = Instantiate(prefab, new Vector3((float)i / 1000,
            this.gameObject.GetComponent<Transform>().position.y, 0), Quaternion.identity);
        mainObjects.Add(obj);
    }
}
```

**What it does:**
- Creates 30,000 cube GameObjects from the prefab
- Positions them horizontally (x-axis divided by 1000 for spacing)
- **Stores each instantiated object in `mainObjects` list** for later access

**Key Insight:**
The division by 1000 prevents objects from being too spread out. With 30,000 objects, spacing them 1 unit apart would create a 30km-long visualization!

---

### TODO 3: Update Heights (`updateHeights()`)
**Implementation:**
```csharp
bool updateHeights()
{
    bool changed = false;
    for (int i = 0; i < array.Length; i++)
    {
        Vector3 currentScale = mainObjects[i].transform.localScale;
        if (currentScale.y != array[i])
        {
            mainObjects[i].transform.localScale = new Vector3(currentScale.x, array[i], currentScale.z);
            changed = true;
        }
    }
    return changed;
}
```

**What it does:**
- Synchronizes each GameObject's Y-scale with its corresponding array value
- Only updates if the value has changed (performance optimization)
- Returns boolean indicating whether any changes occurred

**Why the optimization matters:**
- Called every frame in `Update()`
- Without the change check, would update 30,000 transforms unnecessarily
- Once sorting completes, returns `false` to avoid wasted operations

---

### TODO 4: Single-Threaded Approach
**Implementation:**
```csharp
void Start()
{
    // ... initialization ...
    logArray();        // Print initial state
    spawnObjs();       // Create visual representation

    // SINGLE-THREADED (blocks main thread)
    // bubbleSort();   // Uncomment to test
}
```

**What happens:**
- All sorting happens on Unity's main thread
- Game **freezes completely** during sort (no rendering, no input)
- For 30,000 elements, could freeze for several seconds

**When to use:**
- Quick operations that finish in <16ms (one frame at 60 FPS)
- When you need guaranteed completion before next line executes
- Not suitable for heavy computations like sorting large arrays

---

### TODO 5: Multi-Threaded Approach
**Implementation:**
```csharp
void Start()
{
    // ... initialization ...

    // MULTI-THREADED (non-blocking)
    Thread sortThread = new Thread(bubbleSort);
    sortThread.Start();
}
```

**What happens:**
- Sorting runs on a **separate thread** from Unity's main thread
- Game continues running smoothly (60 FPS maintained)
- Visual updates happen in real-time via `updateHeights()`
- Thread completes independently without blocking gameplay

**Critical Understanding:**
- `Thread.Start()` is **non-blocking** - code continues immediately
- The thread runs **in parallel** with the main game loop
- Cannot call Unity API functions (Transform, GameObject, etc.) from worker thread

---

### TODO 6: Update Loop Integration
**Implementation:**
```csharp
void Update()
{
    updateHeights();
}
```

**What it does:**
- Called every frame by Unity (60 times per second)
- Safely accesses Unity API on the main thread
- Provides real-time visual feedback of sorting progress

**Why this pattern:**
- Worker thread modifies the **array data** (thread-safe primitive types)
- Main thread reads array and updates **GameObjects** (Unity API)
- Clean separation of concerns: computation vs visualization

---

### DELIVERABLE 1: QuickSort Implementation
**Implementation:**
```csharp
void quickSortWrapper()
{
    stopwatch.Start();
    UnityEngine.Debug.Log("QuickSort started...");
    quickSort(0, array.Length - 1);
    stopwatch.Stop();
    UnityEngine.Debug.Log($"QuickSort completed in {stopwatch.ElapsedMilliseconds}ms");
}

void quickSort(int low, int high)
{
    if (low < high)
    {
        int pi = partition(low, high);
        quickSort(low, pi - 1);
        quickSort(pi + 1, high);
    }
}

int partition(int low, int high)
{
    float pivot = array[high];
    int i = (low - 1);
    for (int j = low; j < high; j++)
    {
        if (array[j] < pivot)
        {
            i++;
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
    (array[i + 1], array[high]) = (array[high], array[i + 1]);
    return i + 1;
}
```

**Performance Comparison:**

| Algorithm   | Time Complexity | Expected Time (30K elements) | Visual Effect |
|-------------|----------------|------------------------------|---------------|
| BubbleSort  | O(n²)          | ~10-30 seconds               | Slow, gradual sorting wave |
| QuickSort   | O(n log n)     | ~50-500 milliseconds         | Almost instant completion |

**Why such a huge difference?**
- BubbleSort: 30,000² = 900,000,000 comparisons (worst case)
- QuickSort: 30,000 × log₂(30,000) ≈ 450,000 comparisons
- QuickSort is approximately **2000x faster**!

---

## 🎯 Key Concepts & Insights

### `★ Insight ─────────────────────────────────────`
**Thread vs Coroutine Decision Making:**

- **Use Threads when:**
  - Heavy CPU computation (pathfinding, procedural generation, sorting)
  - Operations that can run completely independent of Unity
  - You need true parallelism across CPU cores

- **Use Coroutines when:**
  - Spreading work across frames (time-slicing)
  - Waiting for conditions or time delays
  - Need to call Unity API functions during execution
  - Simpler debugging and less complexity

**Pattern in this lab:**
The worker thread handles pure computation (array sorting) while the main thread handles visualization (GameObject updates). This separation is the foundation of efficient multithreaded game architecture.
`─────────────────────────────────────────────────`

### Data Consistency Pattern
The code demonstrates a clean pattern for thread-safe data access:

```
[Worker Thread]          [Shared Data]          [Main Thread]
    bubbleSort()  ───►    float[] array    ◄───  updateHeights()
    (writes)              (shared memory)         (reads + Unity API)
```

**Why it's safe:**
- Only ONE thread writes to the array (bubbleSort)
- Main thread only reads the array
- No simultaneous write/write or read/write conflicts
- Float reads/writes are atomic operations in C#

**When you'd need locks:**
If both threads were writing, you'd need:
```csharp
object myLock = new object();

// In thread:
lock(myLock) {
    array[i] = newValue;
}

// In Update():
lock(myLock) {
    value = array[i];
}
```

---

## 🛠️ Unity Setup Instructions

### 1. Open the Scene
- Navigate to: `Assets/Scenes/S_ThreadingExercise.unity`
- Double-click to open

### 2. Assign the Prefab
- In the Hierarchy, find the GameObject with the `BubbleSort` script
- In the Inspector, locate the `Prefab` field (currently empty)
- From `Assets/Prefabs/`, drag either:
  - `Cube Green.prefab` (recommended)
  - `Cube Red.prefab`

### 3. Test Configuration Options

**Option A: Multi-threaded BubbleSort (Default)**
- No changes needed
- Run and observe smooth framerate during sorting
- Check console for timing: "BubbleSort completed in XXXXms"

**Option B: Multi-threaded QuickSort (Fast)**
- In `BubbleSort.cs`, line 40: Comment out BubbleSort
- Line 44: Uncomment QuickSort
```csharp
// Thread sortThread = new Thread(bubbleSort);
Thread sortThread = new Thread(quickSortWrapper);
```

**Option C: Single-threaded (Freezing Demo)**
- Line 32: Uncomment `bubbleSort();`
- Lines 40-46: Comment out thread creation
- Run and observe complete freeze

---

## 🧪 Testing & Expected Results

### Test 1: Multi-threaded BubbleSort
**Steps:**
1. Use default configuration
2. Press Play in Unity
3. Observe the console and Scene view

**Expected Results:**
- Console shows: "BubbleSort started..."
- Scene view shows 30,000 small cubes appearing
- Cubes gradually change height as sorting progresses
- Game runs at 60 FPS (check Stats window)
- After 10-30 seconds: "BubbleSort completed in ~20000ms"
- Final result: Cubes sorted from shortest to tallest (gradient effect)

### Test 2: Multi-threaded QuickSort
**Steps:**
1. Switch to QuickSort in code (see Option B above)
2. Press Play

**Expected Results:**
- Console shows: "QuickSort started..."
- Cubes appear
- Almost immediately sorted (might not see animation)
- Console: "QuickSort completed in ~100-500ms"
- **20-60x faster** than BubbleSort!

### Test 3: Single-threaded (Freeze Test)
**Steps:**
1. Switch to single-threaded mode (Option C)
2. Press Play
3. Try to rotate camera or interact

**Expected Results:**
- Cubes appear
- **Complete freeze** for 10-30 seconds
- Cannot rotate camera, no frame updates
- Console shows completion message after freeze ends
- Demonstrates why threading is essential for large operations

---

## 🎓 What You Learned

### Technical Skills
✅ Implementing threads in Unity with `System.Threading.Thread`
✅ Understanding thread lifecycle: Create → Start → Execute → Complete
✅ Separating computation (worker thread) from visualization (main thread)
✅ Performance profiling with `Stopwatch`
✅ Comparing algorithm complexity in practice (O(n²) vs O(n log n))

### Conceptual Understanding
✅ Why Unity restricts API calls to the main thread (thread safety)
✅ The difference between blocking and non-blocking operations
✅ How to design thread-safe data sharing patterns
✅ When to use threads vs coroutines vs synchronous code
✅ Real-world performance implications of algorithm choice

### Game Development Patterns
✅ **The Computation-Visualization Split Pattern**: Heavy calculations on worker thread, Unity API updates on main thread
✅ **Progressive Updates**: Using Update() to poll thread results
✅ **Visual Debugging**: Making abstract data (arrays) visible (cube heights)

---

## 📝 Important Notes & Warnings

### Unity API Restrictions
⚠️ **Never call these from worker threads:**
- `Instantiate()`, `Destroy()`
- `transform.position`, `transform.localScale`
- Any `GameObject` or `Component` methods
- `Debug.Log()` (can cause crashes - use `UnityEngine.Debug.Log()` with caution)

✅ **Safe to use in worker threads:**
- Pure C# data structures (arrays, lists, dictionaries)
- Math operations
- File I/O (with proper locking)
- Network operations
- Custom classes without Unity dependencies

### Thread Lifecycle Management
The current implementation doesn't explicitly join or abort threads. In production code, you should:

```csharp
Thread sortThread;

void OnDestroy()
{
    if (sortThread != null && sortThread.IsAlive)
    {
        sortThread.Join(1000); // Wait up to 1 second
        // or sortThread.Abort(); // Force stop (not recommended)
    }
}
```

### Performance Considerations
- **30,000 GameObjects** is a lot! In real games, use:
  - GPU instancing for visualization
  - Object pooling instead of instantiate
  - Level of Detail (LOD) systems
- The visualization might cause low FPS even though sorting is threaded

---

## 🚀 Next Steps & Extensions

### Challenge Ideas
1. **Add Merge Sort**: Implement another O(n log n) algorithm and compare
2. **Thread Pool**: Use multiple threads to sort different sections simultaneously
3. **Lock Practice**: Add a counter that both threads increment (requires locking!)
4. **Visual Comparison**: Split screen with BubbleSort on left, QuickSort on right
5. **User Control**: Add UI buttons to switch algorithms at runtime

### Related Topics to Explore
- Unity Job System (modern alternative to threads)
- Burst Compiler (SIMD optimization)
- Async/Await pattern in C#
- Producer-Consumer patterns
- Deadlock prevention strategies

---

## 📚 References

- [Unity Threading Documentation](https://docs.unity3d.com/2020.1/Documentation/Manual/JobSystemMultithreading.html)
- [.NET Thread Class](https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread?view=net-6.0)
- [Sorting Algorithms Reference](https://www.geeksforgeeks.org/sorting-algorithms/)
- [Unity Coroutines](https://docs.unity3d.com/Manual/Coroutines.html)

---

**Lab Completed Successfully! 🎉**

All TODOs implemented, QuickSort added, performance comparisons documented. The code is ready for testing in Unity. Remember to assign the prefab before running!
