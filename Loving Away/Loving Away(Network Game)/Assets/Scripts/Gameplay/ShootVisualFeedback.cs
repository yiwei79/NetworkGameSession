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
    /// Call this from SimplePlayerController to update visual feedback
    /// </summary>
    public void UpdateFeedback(bool isShooting)
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
    }
}

