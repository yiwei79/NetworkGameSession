using UnityEngine;

/// <summary>
/// Client-side projectile representation
/// Handles visual rendering and arc trajectory movement
/// Server spawns, clients simulate deterministically from spawn data
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("Projectile Data")]
    public uint projectileId;
    public uint ownerId;

    [Header("Arc Trajectory")]
    public Vector3 startPosition;
    public Vector3 targetPosition;
    public float arcHeight;
    public float flightTime;

    [Header("Visual")]
    public float radius = 0.2f;

    // Internal state
    private float elapsedTime = 0f;
    private Vector3 velocity; // Kept for direction reference

    /// <summary>
    /// Initializes the projectile with spawn message data
    /// Called immediately after instantiation by GameNetworkManager
    /// </summary>
    public void Initialize(ProjectileSpawnMessage spawnMsg)
    {
        this.projectileId = spawnMsg.projectileId;
        this.ownerId = spawnMsg.ownerId;
        this.velocity = spawnMsg.velocity;

        // Arc trajectory parameters
        this.startPosition = spawnMsg.startPosition;
        this.targetPosition = spawnMsg.targetPosition;
        this.arcHeight = spawnMsg.arcHeight;
        this.flightTime = spawnMsg.flightTime;

        // Set initial position
        transform.position = spawnMsg.startPosition;

        UnityEngine.Debug.Log($"[Projectile] Spawned projectile {projectileId} from player {ownerId}: {startPosition} -> {targetPosition} (arc: {arcHeight}, time: {flightTime}s)");
    }

    void Start()
    {
        // Create visual representation if none exists
        // Check if we already have a child renderer
        if (GetComponentInChildren<MeshRenderer>() == null)
        {
            CreateVisual();
        }
    }

    void Update()
    {
        // Update elapsed time
        elapsedTime += Time.deltaTime;

        // Parametric arc trajectory
        // t goes from 0 (start) to 1 (target)
        float t = Mathf.Clamp01(elapsedTime / flightTime);

        // Horizontal: Linear interpolation from start to target
        Vector3 horizontal = Vector3.Lerp(startPosition, targetPosition, t);

        // Vertical: Parabolic arc (0 -> peak -> 0)
        // The formula 4 * t * (1 - t) creates a parabola that:
        // - equals 0 at t=0 and t=1
        // - peaks at 1.0 when t=0.5
        float heightOffset = arcHeight * 4f * t * (1f - t);

        // Combine horizontal movement with vertical arc
        transform.position = new Vector3(horizontal.x, horizontal.y + heightOffset, horizontal.z);

        // Destroy when arc is complete
        if (t >= 1.0f)
        {
            UnityEngine.Debug.Log($"[Projectile] Projectile {projectileId} landed at {targetPosition}");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Creates a simple sphere visual for the projectile with trail effect
    /// </summary>
    private void CreateVisual()
    {
        // Create sphere
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(transform);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * radius * 2; // Diameter

        // Make it visually distinct (bright color)
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.yellow;
            mat.SetFloat("_Metallic", 0.5f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.yellow * 0.5f);
            renderer.material = mat;
        }

        // Remove collider (we'll do collision detection on server)
        Collider col = sphere.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        // Add trail renderer for visual polish
        CreateTrailRenderer();
    }

    /// <summary>
    /// Creates a trail renderer for the projectile
    /// </summary>
    private void CreateTrailRenderer()
    {
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();

        // Trail timing
        trail.time = 0.3f; // Trail persists for 0.3 seconds

        // Trail width (tapers from start to end)
        trail.startWidth = 0.15f;
        trail.endWidth = 0.0f;

        // Trail material (simple sprite shader for smooth rendering)
        trail.material = new Material(Shader.Find("Sprites/Default"));

        // Trail color (yellow fading to transparent)
        trail.startColor = new Color(1f, 0.9f, 0.2f, 1f); // Bright yellow
        trail.endColor = new Color(1f, 0.6f, 0f, 0f);      // Orange, transparent

        // Ensure trail renders properly
        trail.minVertexDistance = 0.1f;
        trail.autodestruct = false;
    }

    /// <summary>
    /// Gets the current age of the projectile
    /// </summary>
    public float GetAge()
    {
        return elapsedTime;
    }

    /// <summary>
    /// Gets the flight progress (0 = start, 1 = landed)
    /// </summary>
    public float GetProgress()
    {
        return Mathf.Clamp01(elapsedTime / flightTime);
    }

    /// <summary>
    /// Checks if projectile has completed its arc
    /// </summary>
    public bool IsExpired()
    {
        return elapsedTime >= flightTime;
    }
}
