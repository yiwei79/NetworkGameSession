using UnityEngine;

/// <summary>
/// Visual feedback component for shooting action
/// Shows charging effect and muzzle flash when player shoots
/// Attach to player GameObject
/// </summary>
public class ShootVisualFeedback : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color chargeColor = Color.yellow;
    public Color shootFlashColor = Color.white;
    public float maxChargeScale = 1.5f;

    [Header("Cooldown Bar Settings")]
    public float cooldownBarWidth = 1.0f;
    public float cooldownBarHeight = 0.08f;
    public float cooldownBarYOffset = 1.7f; // Below name tag, above health bar

    // Internal state
    private GameObject chargeIndicator;
    private GameObject muzzleFlash;
    private Renderer playerRenderer;
    private Color originalColor;
    private bool wasShootingLastFrame = false;
    private float chargeTime = 0f;

    // Muzzle flash
    private float flashTimer = 0f;
    private float flashDuration = 0.1f;

    // Cooldown bar (Phase 5.5)
    private GameObject cooldownBarBackground;
    private GameObject cooldownBarForeground;
    private Renderer cooldownForegroundRenderer;
    
    void Start()
    {
        // Phase 2: Get player body renderer from PlayerVisualController
        // Look for "Body" GameObject under VisualModel
        Transform visualModel = transform.Find("VisualModel");
        if (visualModel != null)
        {
            Transform body = visualModel.Find("Body");
            if (body != null)
            {
                playerRenderer = body.GetComponent<Renderer>();
            }
        }

        // Fallback: try to get renderer from root (backward compatibility)
        if (playerRenderer == null)
        {
            playerRenderer = GetComponent<Renderer>();
        }

        if (playerRenderer != null)
        {
            originalColor = playerRenderer.material.color;
        }

        // Create charge indicator (sphere that grows while charging)
        CreateChargeIndicator();

        // Create muzzle flash
        CreateMuzzleFlash();

        // Phase 5.5: Create cooldown bar
        CreateCooldownBar();
    }
    
    void CreateChargeIndicator()
    {
        chargeIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        chargeIndicator.name = "ChargeIndicator";
        chargeIndicator.transform.SetParent(transform);
        chargeIndicator.transform.localPosition = Vector3.zero;
        chargeIndicator.transform.localScale = Vector3.zero;

        // Phase 2: Use existing material to avoid shader issues
        Renderer renderer = chargeIndicator.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            // Set color directly on existing material
            renderer.material.color = new Color(chargeColor.r, chargeColor.g, chargeColor.b, 0.5f);

            // Try to set transparency mode (may not work in all pipelines, but won't cause pink)
            renderer.material.SetFloat("_Mode", 3);
            renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            renderer.material.SetInt("_ZWrite", 0);
            renderer.material.DisableKeyword("_ALPHATEST_ON");
            renderer.material.EnableKeyword("_ALPHABLEND_ON");
            renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            renderer.material.renderQueue = 3000;
        }

        // Remove collider
        Destroy(chargeIndicator.GetComponent<Collider>());
    }
    
    void CreateMuzzleFlash()
    {
        muzzleFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        muzzleFlash.name = "MuzzleFlash";
        muzzleFlash.transform.SetParent(transform);
        muzzleFlash.transform.localPosition = new Vector3(0, 0, 1.5f); // In front of player
        muzzleFlash.transform.localScale = Vector3.zero;

        // Phase 2: Use existing material to avoid shader issues
        Renderer renderer = muzzleFlash.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.color = shootFlashColor;
            // Try to set emission (may not work in all pipelines)
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", shootFlashColor * 2f);
        }

        // Remove collider
        Destroy(muzzleFlash.GetComponent<Collider>());
    }

    /// <summary>
    /// Phase 5.5: Creates the cooldown bar (UI bar above head)
    /// Shows when player can shoot again after cooldown
    /// </summary>
    void CreateCooldownBar()
    {
        // Background (dark gray bar)
        cooldownBarBackground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cooldownBarBackground.name = "CooldownBarBackground";
        cooldownBarBackground.transform.SetParent(transform);
        cooldownBarBackground.transform.localPosition = new Vector3(0, cooldownBarYOffset, 0);
        cooldownBarBackground.transform.localScale = new Vector3(cooldownBarWidth, cooldownBarHeight, 0.05f);

        Renderer bgRenderer = cooldownBarBackground.GetComponent<Renderer>();
        if (bgRenderer != null && bgRenderer.material != null)
        {
            bgRenderer.material.color = new Color(0.2f, 0.2f, 0.2f); // Dark gray
        }

        Destroy(cooldownBarBackground.GetComponent<Collider>());

        // Foreground (colored cooldown bar)
        cooldownBarForeground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cooldownBarForeground.name = "CooldownBarForeground";
        cooldownBarForeground.transform.SetParent(transform);
        cooldownBarForeground.transform.localPosition = new Vector3(0, cooldownBarYOffset, -0.03f); // Slightly in front
        cooldownBarForeground.transform.localScale = new Vector3(cooldownBarWidth, cooldownBarHeight, 0.05f);

        cooldownForegroundRenderer = cooldownBarForeground.GetComponent<Renderer>();
        if (cooldownForegroundRenderer != null && cooldownForegroundRenderer.material != null)
        {
            cooldownForegroundRenderer.material.color = Color.green; // Start green (ready)
        }

        Destroy(cooldownBarForeground.GetComponent<Collider>());

        // Hide cooldown bar initially (only show after first shot)
        cooldownBarBackground.SetActive(false);
        cooldownBarForeground.SetActive(false);
    }

    /// <summary>
    /// Call this from SimplePlayerController to update visual feedback
    /// Phase 5.5: Now accepts cooldownPercent (0.0 = just shot, 1.0 = ready)
    /// </summary>
    public void UpdateFeedback(bool isShooting, float cooldownPercent)
    {
        if (isShooting)
        {
            // Charging - grow indicator
            chargeTime += Time.deltaTime;
            float chargePercent = Mathf.Clamp01(chargeTime / 2f); // 2 seconds max charge
            
            float scale = Mathf.Lerp(0.8f, maxChargeScale, chargePercent);
            chargeIndicator.transform.localScale = Vector3.one * scale;
            
            // Pulse effect
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.1f;
            chargeIndicator.transform.localScale *= pulse;
            
            // Change player color to indicate charging
            if (playerRenderer != null)
            {
                playerRenderer.material.color = Color.Lerp(originalColor, chargeColor, chargePercent * 0.5f);
            }
            
            wasShootingLastFrame = true;
        }
        else
        {
            // Released - show muzzle flash if was charging
            if (wasShootingLastFrame && chargeTime > 0.1f)
            {
                TriggerMuzzleFlash();
            }
            
            // Hide charge indicator
            chargeIndicator.transform.localScale = Vector3.zero;
            chargeTime = 0f;
            
            // Restore original color
            if (playerRenderer != null)
            {
                playerRenderer.material.color = originalColor;
            }
            
            wasShootingLastFrame = false;
        }
        
        // Update muzzle flash
        UpdateMuzzleFlash();

        // Phase 5.5: Update cooldown bar
        UpdateCooldownBar(cooldownPercent);
    }
    
    void TriggerMuzzleFlash()
    {
        flashTimer = flashDuration;
        muzzleFlash.transform.localScale = Vector3.one * 0.5f;
    }
    
    void UpdateMuzzleFlash()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;

            // Fade out
            float t = flashTimer / flashDuration;
            muzzleFlash.transform.localScale = Vector3.one * 0.5f * t;
        }
        else
        {
            muzzleFlash.transform.localScale = Vector3.zero;
        }
    }

    /// <summary>
    /// Phase 5.5: Updates the cooldown bar based on cooldown percent
    /// </summary>
    void UpdateCooldownBar(float cooldownPercent)
    {
        bool onCooldown = (cooldownPercent < 1.0f);

        if (onCooldown)
        {
            // Show cooldown bar
            if (cooldownBarBackground != null) cooldownBarBackground.SetActive(true);
            if (cooldownBarForeground != null) cooldownBarForeground.SetActive(true);

            // Scale bar width based on cooldown percent (0.0 → 1.0)
            Vector3 scale = cooldownBarForeground.transform.localScale;
            scale.x = cooldownBarWidth * cooldownPercent;
            cooldownBarForeground.transform.localScale = scale;

            // Adjust position to keep bar left-aligned
            Vector3 pos = cooldownBarForeground.transform.localPosition;
            pos.x = -cooldownBarWidth * 0.5f + (cooldownBarWidth * cooldownPercent * 0.5f);
            cooldownBarForeground.transform.localPosition = pos;

            // Color transition: Red (0%) → Green (100%)
            if (cooldownForegroundRenderer != null && cooldownForegroundRenderer.material != null)
            {
                Color barColor = Color.Lerp(Color.red, Color.green, cooldownPercent);
                cooldownForegroundRenderer.material.color = barColor;
            }
        }
        else
        {
            // Hide cooldown bar when ready
            if (cooldownBarBackground != null) cooldownBarBackground.SetActive(false);
            if (cooldownBarForeground != null) cooldownBarForeground.SetActive(false);
        }
    }

    /// <summary>
    /// Update original color when player color changes
    /// </summary>
    public void SetOriginalColor(Color color)
    {
        originalColor = color;
    }
    
    void OnDestroy()
    {
        if (chargeIndicator != null)
        {
            Destroy(chargeIndicator);
        }
        if (muzzleFlash != null)
        {
            Destroy(muzzleFlash);
        }
        // Phase 5.5: Cleanup cooldown bar
        if (cooldownBarBackground != null)
        {
            Destroy(cooldownBarBackground);
        }
        if (cooldownBarForeground != null)
        {
            Destroy(cooldownBarForeground);
        }
    }
}

