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
        // Get player renderer
        playerRenderer = GetComponent<Renderer>();
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
        
        // Make it semi-transparent
        Renderer renderer = chargeIndicator.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(chargeColor.r, chargeColor.g, chargeColor.b, 0.5f);
        mat.SetFloat("_Mode", 3); // Transparent mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        renderer.material = mat;
        
        // Remove collider
        Destroy(chargeIndicator.GetComponent<Collider>());
    }
    
    void CreateMuzzleFlash()
    {
        muzzleFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        muzzleFlash.name = "MuzzleFlash";
        muzzleFlash.transform.SetParent(transform);
        muzzleFlash.transform.localPosition = new Vector3(0, 0, 0.7f); // In front of player
        muzzleFlash.transform.localScale = Vector3.zero;
        
        // Bright white material
        Renderer renderer = muzzleFlash.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = shootFlashColor;
        mat.SetFloat("_Mode", 3);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", shootFlashColor * 2f);
        renderer.material = mat;
        
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

