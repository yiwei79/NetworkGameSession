using UnityEngine;

/// <summary>
/// Creates and manages arena visual elements (ground, boundary ring, decorations).
/// Session 5A - Visual Dressing
///
/// Design Pattern: Nullable prefabs allow easy replacement of primitives with asset models.
/// If prefab field is null → creates primitive fallback
/// If prefab field is assigned → instantiates prefab instead
/// </summary>
public class ArenaSetup : MonoBehaviour
{
    [Header("Arena Settings")]
    [Tooltip("Radius of the playable arena (elimination boundary)")]
    public float arenaRadius = 15f;

    [Header("Ground Settings")]
    [Tooltip("Optional prefab for ground. If null, creates primitive cylinder")]
    public GameObject groundPrefab;
    public Material groundMaterial;
    public Color groundColor = new Color(0.4f, 0.6f, 0.3f); // Grass green

    [Header("Boundary Ring Settings")]
    [Tooltip("Show visual ring at arena boundary")]
    public bool showBoundaryRing = true;
    public Color boundaryRingColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange, semi-transparent
    public float boundaryRingWidth = 0.2f;

    [Header("Decoration Settings")]
    public bool generateDecorations = true;
    [Tooltip("Minimum distance from arena center for decorations (should be > arenaRadius)")]
    public float decorationMinRadius = 16f;
    [Tooltip("Maximum distance from arena center for decorations")]
    public float decorationMaxRadius = 22f;

    [Header("Tree Decorations")]
    public int treeCount = 8;
    public GameObject[] treePrefabs; // Optional prefabs

    [Header("Rock Decorations")]
    public int rockCount = 12;
    public GameObject[] rockPrefabs; // Optional prefabs

    [Header("Mushroom Decorations")]
    public int mushroomCount = 6;
    public GameObject[] mushroomPrefabs; // Optional prefabs

    void Start()
    {
        CreateGround();
        if (showBoundaryRing) CreateBoundaryRing();
        if (generateDecorations) CreateDecorations();
    }

    /// <summary>
    /// Creates the arena ground - either from prefab or procedural cylinder
    /// </summary>
    void CreateGround()
    {
        if (groundPrefab != null)
        {
            // Use provided prefab
            GameObject ground = Instantiate(groundPrefab, transform);
            ground.name = "ArenaGround";
            ground.transform.position = new Vector3(0f, 0f, 0f);
        }
        else
        {
            // Create primitive cylinder as fallback
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.transform.parent = transform;
            ground.name = "ArenaGround_Primitive";

            // Position and scale for circular arena
            // arenaRadius = 15 → diameter = 30
            ground.transform.position = new Vector3(0f, -0.05f, 0f); // Slightly below origin
            ground.transform.localScale = new Vector3(arenaRadius * 2f, 0.1f, arenaRadius * 2f);

            // Apply material or color
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (groundMaterial != null)
                {
                    renderer.material = groundMaterial;
                }
                else
                {
                    // Use existing material and just set color (avoids shader issues)
                    renderer.material.color = groundColor;
                }
            }

            // Disable collider (we don't need physics collision for ground)
            Collider collider = ground.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }
    }

    /// <summary>
    /// Creates a visual ring at the arena boundary to warn players of elimination zone
    /// </summary>
    void CreateBoundaryRing()
    {
        GameObject ringObj = new GameObject("BoundaryRing");
        ringObj.transform.parent = transform;
        ringObj.transform.position = new Vector3(0f, 0.1f, 0f); // Slightly above ground

        LineRenderer lineRenderer = ringObj.AddComponent<LineRenderer>();

        // Configure LineRenderer
        lineRenderer.startWidth = boundaryRingWidth;
        lineRenderer.endWidth = boundaryRingWidth;
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;

        // Set material and color (use default LineRenderer material)
        lineRenderer.startColor = boundaryRingColor;
        lineRenderer.endColor = boundaryRingColor;

        // Generate circle points
        int segments = 64;
        lineRenderer.positionCount = segments;

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * arenaRadius;
            float z = Mathf.Sin(angle) * arenaRadius;
            lineRenderer.SetPosition(i, new Vector3(x, 0f, z));
        }
    }

    /// <summary>
    /// Generates decorations (trees, rocks, mushrooms) around the arena
    /// </summary>
    void CreateDecorations()
    {
        // Create parent object for organization
        GameObject decorationsParent = new GameObject("Decorations");
        decorationsParent.transform.parent = transform;

        // Generate trees
        for (int i = 0; i < treeCount; i++)
        {
            GameObject tree = CreateTree(decorationsParent.transform);
            PositionDecorationRandomly(tree);
        }

        // Generate rocks
        for (int i = 0; i < rockCount; i++)
        {
            GameObject rock = CreateRock(decorationsParent.transform);
            PositionDecorationRandomly(rock);
        }

        // Generate mushrooms
        for (int i = 0; i < mushroomCount; i++)
        {
            GameObject mushroom = CreateMushroom(decorationsParent.transform);
            PositionDecorationRandomly(mushroom);
        }
    }

    /// <summary>
    /// Creates a tree decoration - either from prefab or primitive assembly
    /// </summary>
    GameObject CreateTree(Transform parent)
    {
        // Try to use prefab
        if (treePrefabs != null && treePrefabs.Length > 0)
        {
            GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
            if (prefab != null)
            {
                return Instantiate(prefab, parent);
            }
        }

        // Fallback: Create primitive tree (capsule trunk + sphere foliage)
        GameObject tree = new GameObject("Tree_Primitive");
        tree.transform.parent = parent;

        // Trunk (brown capsule)
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        trunk.transform.parent = tree.transform;
        trunk.name = "Trunk";
        trunk.transform.localPosition = new Vector3(0f, 1f, 0f);
        trunk.transform.localScale = new Vector3(0.3f, 1f, 0.3f);

        Renderer trunkRenderer = trunk.GetComponent<Renderer>();
        if (trunkRenderer != null && trunkRenderer.material != null)
        {
            trunkRenderer.material.color = new Color(0.4f, 0.25f, 0.1f); // Brown
        }

        // Foliage (green sphere)
        GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foliage.transform.parent = tree.transform;
        foliage.name = "Foliage";
        foliage.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        foliage.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

        Renderer foliageRenderer = foliage.GetComponent<Renderer>();
        if (foliageRenderer != null && foliageRenderer.material != null)
        {
            foliageRenderer.material.color = new Color(0.2f, 0.5f, 0.2f); // Dark green
        }

        // Remove colliders (decorations are visual only)
        Destroy(trunk.GetComponent<Collider>());
        Destroy(foliage.GetComponent<Collider>());

        return tree;
    }

    /// <summary>
    /// Creates a rock decoration - either from prefab or primitive
    /// </summary>
    GameObject CreateRock(Transform parent)
    {
        // Try to use prefab
        if (rockPrefabs != null && rockPrefabs.Length > 0)
        {
            GameObject prefab = rockPrefabs[Random.Range(0, rockPrefabs.Length)];
            if (prefab != null)
            {
                return Instantiate(prefab, parent);
            }
        }

        // Fallback: Create primitive rock (rotated/scaled sphere or cube)
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rock.transform.parent = parent;
        rock.name = "Rock_Primitive";

        // Random size and rotation
        float size = Random.Range(0.5f, 1.2f);
        rock.transform.localScale = new Vector3(size, size * 0.7f, size * 0.9f);
        rock.transform.localRotation = Quaternion.Euler(
            Random.Range(0f, 30f),
            Random.Range(0f, 360f),
            Random.Range(0f, 30f)
        );

        // Gray material (use existing material from primitive)
        Renderer renderer = rock.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.color = new Color(0.5f, 0.5f, 0.5f); // Gray
        }

        // Remove collider
        Destroy(rock.GetComponent<Collider>());

        return rock;
    }

    /// <summary>
    /// Creates a mushroom decoration - either from prefab or primitive assembly
    /// </summary>
    GameObject CreateMushroom(Transform parent)
    {
        // Try to use prefab
        if (mushroomPrefabs != null && mushroomPrefabs.Length > 0)
        {
            GameObject prefab = mushroomPrefabs[Random.Range(0, mushroomPrefabs.Length)];
            if (prefab != null)
            {
                return Instantiate(prefab, parent);
            }
        }

        // Fallback: Create primitive mushroom (cylinder stem + sphere cap)
        GameObject mushroom = new GameObject("Mushroom_Primitive");
        mushroom.transform.parent = parent;

        // Stem (white/cream cylinder)
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.transform.parent = mushroom.transform;
        stem.name = "Stem";
        stem.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        stem.transform.localScale = new Vector3(0.15f, 0.2f, 0.15f);

        Renderer stemRenderer = stem.GetComponent<Renderer>();
        if (stemRenderer != null && stemRenderer.material != null)
        {
            stemRenderer.material.color = new Color(0.95f, 0.95f, 0.9f); // Cream
        }

        // Cap (red sphere, slightly flattened)
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.transform.parent = mushroom.transform;
        cap.name = "Cap";
        cap.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        cap.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);

        Renderer capRenderer = cap.GetComponent<Renderer>();
        if (capRenderer != null && capRenderer.material != null)
        {
            capRenderer.material.color = new Color(0.8f, 0.2f, 0.2f); // Red
        }

        // Remove colliders
        Destroy(stem.GetComponent<Collider>());
        Destroy(cap.GetComponent<Collider>());

        return mushroom;
    }

    /// <summary>
    /// Positions a decoration object randomly outside the playable arena
    /// </summary>
    void PositionDecorationRandomly(GameObject decoration)
    {
        // Random angle
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

        // Random distance (between decorationMinRadius and decorationMaxRadius)
        float distance = Random.Range(decorationMinRadius, decorationMaxRadius);

        // Calculate position
        float x = Mathf.Cos(angle) * distance;
        float z = Mathf.Sin(angle) * distance;

        decoration.transform.position = new Vector3(x, 0f, z);

        // Random rotation around Y axis for variety
        decoration.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }
}
