using UnityEngine;

/// <summary>
/// Manages the visual representation of a player character.
/// Session 5A - Visual Dressing
///
/// Design Pattern: Separation of visuals from network logic
/// - SimplePlayerController handles network state and position
/// - PlayerVisualController handles visual representation only
/// - Allows swapping character models without touching network code
/// </summary>
public class PlayerVisualController : MonoBehaviour
{
    [Header("Visual References")]
    [Tooltip("Root transform containing the character visual model")]
    public Transform visualRoot;

    [Header("Character Settings")]
    [Tooltip("Optional prefab for character body. If null, creates primitive assembly")]
    public GameObject characterPrefab;
    public float modelScale = 1f;
    public Vector3 modelOffset = Vector3.zero;

    [Header("Character Colors")]
    public Color bodyColor = Color.white;
    public Color headColor = Color.white;

    [Header("State")]
    private bool isAlive = true;
    private Vector3 facingDirection = Vector3.forward;

    [Header("Visual Parts (Auto-assigned if using primitives)")]
    private GameObject bodyObject;
    private GameObject headObject;
    private GameObject eyeObject;

    void Start()
    {
        // If no visual root exists, create the character model
        if (visualRoot == null)
        {
            CreateCharacterVisual();
        }
    }

    /// <summary>
    /// Creates the character visual - either from prefab or primitive assembly
    /// </summary>
    void CreateCharacterVisual()
    {
        // Create visual root
        GameObject visualRootObj = new GameObject("VisualModel");
        visualRootObj.transform.parent = transform;
        visualRootObj.transform.localPosition = modelOffset;
        visualRootObj.transform.localScale = Vector3.one * modelScale;
        visualRoot = visualRootObj.transform;

        if (characterPrefab != null)
        {
            // Use provided prefab
            Instantiate(characterPrefab, visualRoot);
        }
        else
        {
            // Create primitive assembly (capsule body + sphere head)
            CreatePrimitiveCharacter();
        }
    }

    /// <summary>
    /// Creates a chibi-style character from primitives
    /// Chibi proportions: Big head (60%) + Small body (40%) = Cute!
    /// Total height: 1.5 units
    /// </summary>
    void CreatePrimitiveCharacter()
    {
        // CHIBI BODY: Small capsule (height 0.6, radius 0.35)
        bodyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyObject.transform.parent = visualRoot;
        bodyObject.name = "Body";
        bodyObject.transform.localPosition = new Vector3(0f, 0.3f, 0f); // Center at 0.3 units high
        bodyObject.transform.localScale = new Vector3(0.7f, 0.3f, 0.7f); // Capsule: radius 0.35, height 0.6

        // Apply body color (use existing material to avoid shader issues)
        Renderer bodyRenderer = bodyObject.GetComponent<Renderer>();
        if (bodyRenderer != null && bodyRenderer.material != null)
        {
            bodyRenderer.material.color = bodyColor;
        }

        // Remove collider (SimplePlayerController handles physics/collision)
        Destroy(bodyObject.GetComponent<Collider>());

        // CHIBI HEAD: BIG sphere (radius 0.45) - the star of the show!
        headObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        headObject.transform.parent = visualRoot;
        headObject.name = "Head";
        headObject.transform.localPosition = new Vector3(0f, 1.05f, 0f); // Body top (0.6) + head radius (0.45)
        headObject.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f); // Radius 0.45 (sphere default 0.5)

        // Apply head color (slightly brighter than body, use existing material)
        Renderer headRenderer = headObject.GetComponent<Renderer>();
        if (headRenderer != null && headRenderer.material != null)
        {
            headRenderer.material.color = headColor;
        }

        // Remove collider
        Destroy(headObject.GetComponent<Collider>());

        // CHIBI EYE: Bigger, more expressive (radius 0.12)
        eyeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeObject.transform.parent = headObject.transform; // Child of head
        eyeObject.name = "Eye";
        eyeObject.transform.localPosition = new Vector3(0f, 0.05f, 0.42f); // Front of bigger head
        eyeObject.transform.localScale = new Vector3(0.267f, 0.267f, 0.267f); // Radius 0.12 relative to head

        // Apply eye color (white, use existing material)
        Renderer eyeRenderer = eyeObject.GetComponent<Renderer>();
        if (eyeRenderer != null && eyeRenderer.material != null)
        {
            eyeRenderer.material.color = Color.white;
        }

        // Remove collider
        Destroy(eyeObject.GetComponent<Collider>());
    }

    /// <summary>
    /// Updates the facing direction (NOTE: Rotation is now handled by parent GameObject)
    /// This method is kept for future use if we need visual-only rotation effects
    /// </summary>
    public void SetFacingDirection(Vector3 direction)
    {
        if (direction.magnitude > 0.1f)
        {
            facingDirection = direction.normalized;
            // NOTE: Do NOT rotate visualRoot here - parent GameObject handles rotation
            // VisualRoot inherits rotation from parent, which is critical for shooting/knockback
        }
    }

    /// <summary>
    /// Sets the alive state and updates visual visibility
    /// </summary>
    public void SetAliveState(bool alive)
    {
        isAlive = alive;
        if (visualRoot != null)
        {
            // Option A: Hide when dead
            visualRoot.gameObject.SetActive(alive);

            // Option B: Ghost effect (uncomment if preferred)
            // SetGhostMode(!alive);
        }
    }

    /// <summary>
    /// Sets the player color (applied to body and head)
    /// </summary>
    public void SetPlayerColor(Color color)
    {
        bodyColor = color;

        // Make head slightly brighter
        headColor = new Color(
            Mathf.Min(color.r + 0.2f, 1f),
            Mathf.Min(color.g + 0.2f, 1f),
            Mathf.Min(color.b + 0.2f, 1f),
            color.a
        );

        // Apply to existing objects if they exist
        if (bodyObject != null)
        {
            Renderer renderer = bodyObject.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = bodyColor;
            }
        }

        if (headObject != null)
        {
            Renderer renderer = headObject.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = headColor;
            }
        }

        // Also apply to visual root's children if using prefab
        if (visualRoot != null)
        {
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                // Skip eye
                if (rend.gameObject.name == "Eye") continue;

                if (rend.material != null)
                {
                    rend.material.color = bodyColor;
                }
            }
        }
    }

    /// <summary>
    /// Optional ghost effect for dead players (semi-transparent)
    /// </summary>
    void SetGhostMode(bool isGhost)
    {
        if (visualRoot == null) return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend.material != null)
            {
                if (isGhost)
                {
                    // Set to transparent rendering mode
                    rend.material.SetFloat("_Mode", 3); // Transparent
                    rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    rend.material.SetInt("_ZWrite", 0);
                    rend.material.DisableKeyword("_ALPHATEST_ON");
                    rend.material.EnableKeyword("_ALPHABLEND_ON");
                    rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    rend.material.renderQueue = 3000;

                    // Make semi-transparent
                    Color color = rend.material.color;
                    color.a = 0.3f;
                    rend.material.color = color;
                }
                else
                {
                    // Set to opaque
                    rend.material.SetFloat("_Mode", 0); // Opaque
                    rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    rend.material.SetInt("_ZWrite", 1);
                    rend.material.DisableKeyword("_ALPHATEST_ON");
                    rend.material.DisableKeyword("_ALPHABLEND_ON");
                    rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    rend.material.renderQueue = -1;

                    // Make opaque
                    Color color = rend.material.color;
                    color.a = 1f;
                    rend.material.color = color;
                }
            }
        }
    }

    /// <summary>
    /// Called every frame - can be used for animations or dynamic effects
    /// </summary>
    void Update()
    {
        // Placeholder for future animations (e.g., idle bob, walk cycle)
        // Currently unused to keep scope minimal
    }
}
