using UnityEngine;

/// <summary>
/// Client-side projectile representation
/// Handles visual rendering and physics-based movement
/// Server spawns, clients simulate deterministically from spawn data
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("Projectile Data")]
    public uint projectileId;
    public uint ownerId;

    [Header("Physics")]
    public Vector3 velocity;
    public float lifetime = 2.0f; // Seconds before self-destruct

    [Header("Visual")]
    public float radius = 0.2f;

    // Internal state
    private float spawnTime;
    private float elapsedTime = 0f;

    /// <summary>
    /// Initializes the projectile with spawn message data
    /// Called immediately after instantiation by GameNetworkManager
    /// </summary>
    public void Initialize(ProjectileSpawnMessage spawnMsg)
    {
        this.projectileId = spawnMsg.projectileId;
        this.ownerId = spawnMsg.ownerId;
        this.velocity = spawnMsg.velocity;
        this.spawnTime = spawnMsg.spawnTime;

        // Set initial position
        transform.position = spawnMsg.startPosition;

        UnityEngine.Debug.Log($"[Projectile] Spawned projectile {projectileId} from player {ownerId} at {spawnMsg.startPosition}");
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

        // Linear trajectory (Session 1 - basic version)
        // Session 2 will replace this with arc trajectory
        transform.position += velocity * Time.deltaTime;

        // Self-destruct after lifetime expires
        if (elapsedTime >= lifetime)
        {
            UnityEngine.Debug.Log($"[Projectile] Projectile {projectileId} expired after {lifetime}s");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Creates a simple sphere visual for the projectile
    /// </summary>
    private void CreateVisual()
    {
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
    }

    /// <summary>
    /// Gets the current age of the projectile
    /// </summary>
    public float GetAge()
    {
        return elapsedTime;
    }

    /// <summary>
    /// Checks if projectile should be destroyed
    /// </summary>
    public bool IsExpired()
    {
        return elapsedTime >= lifetime;
    }
}
