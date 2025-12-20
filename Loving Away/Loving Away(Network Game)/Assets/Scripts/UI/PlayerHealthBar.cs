using UnityEngine;

/// <summary>
/// Simple health bar UI displayed above player
/// Shows current HP with color gradient (green → yellow → red)
/// Phase 3: Health System
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    [Header("Health Bar Settings")]
    public float barWidth = 1.0f;
    public float barHeight = 0.1f;
    public float heightAbovePlayer = 1.8f;

    [Header("Colors")]
    public Color fullHealthColor = Color.green;
    public Color midHealthColor = Color.yellow;
    public Color lowHealthColor = Color.red;

    // Internal components
    private GameObject barBackground;
    private GameObject barForeground;
    private Renderer foregroundRenderer;

    // Health tracking
    private byte maxHealth = 5;
    private byte currentHealth = 5;

    void Start()
    {
        CreateHealthBar();
    }

    /// <summary>
    /// Creates the health bar visuals using primitives
    /// </summary>
    void CreateHealthBar()
    {
        // Background (dark gray bar)
        barBackground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barBackground.name = "HealthBarBackground";
        barBackground.transform.SetParent(transform);
        barBackground.transform.localPosition = new Vector3(0, heightAbovePlayer, 0);
        barBackground.transform.localScale = new Vector3(barWidth, barHeight, 0.05f);

        Renderer bgRenderer = barBackground.GetComponent<Renderer>();
        if (bgRenderer != null && bgRenderer.material != null)
        {
            bgRenderer.material.color = new Color(0.2f, 0.2f, 0.2f); // Dark gray
        }

        Destroy(barBackground.GetComponent<Collider>());

        // Foreground (colored health bar)
        barForeground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barForeground.name = "HealthBarForeground";
        barForeground.transform.SetParent(transform);
        barForeground.transform.localPosition = new Vector3(0, heightAbovePlayer, -0.03f); // Slightly in front
        barForeground.transform.localScale = new Vector3(barWidth, barHeight, 0.05f);

        foregroundRenderer = barForeground.GetComponent<Renderer>();
        if (foregroundRenderer != null && foregroundRenderer.material != null)
        {
            foregroundRenderer.material.color = fullHealthColor;
        }

        Destroy(barForeground.GetComponent<Collider>());
    }

    /// <summary>
    /// Updates the health bar display
    /// Call this when player health changes
    /// </summary>
    public void SetHealth(byte health, byte maxHp = 5)
    {
        currentHealth = health;
        maxHealth = maxHp;

        // Calculate health percentage
        float healthPercent = (float)currentHealth / (float)maxHealth;

        // Scale foreground bar width based on health
        Vector3 scale = barForeground.transform.localScale;
        scale.x = barWidth * healthPercent;
        barForeground.transform.localScale = scale;

        // Adjust position to keep bar left-aligned
        Vector3 pos = barForeground.transform.localPosition;
        pos.x = -barWidth * 0.5f + (barWidth * healthPercent * 0.5f);
        barForeground.transform.localPosition = pos;

        // Update color based on health
        if (foregroundRenderer != null && foregroundRenderer.material != null)
        {
            Color barColor;
            if (healthPercent > 0.6f)
            {
                // Full health: green
                barColor = fullHealthColor;
            }
            else if (healthPercent > 0.3f)
            {
                // Mid health: yellow (lerp from green to yellow)
                float t = (healthPercent - 0.3f) / 0.3f;
                barColor = Color.Lerp(midHealthColor, fullHealthColor, t);
            }
            else
            {
                // Low health: red (lerp from red to yellow)
                float t = healthPercent / 0.3f;
                barColor = Color.Lerp(lowHealthColor, midHealthColor, t);
            }

            foregroundRenderer.material.color = barColor;
        }
    }

    /// <summary>
    /// Makes health bar face the camera (billboard effect)
    /// Call this from Update() if you want the bar to always face camera
    /// </summary>
    public void FaceCamera()
    {
        if (Camera.main != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - barBackground.transform.position;
            directionToCamera.y = 0; // Keep bar upright

            if (directionToCamera.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                barBackground.transform.rotation = targetRotation;
                barForeground.transform.rotation = targetRotation;
            }
        }
    }

    void Update()
    {
        // Always face camera for better visibility
        FaceCamera();
    }

    void OnDestroy()
    {
        if (barBackground != null)
        {
            Destroy(barBackground);
        }
        if (barForeground != null)
        {
            Destroy(barForeground);
        }
    }
}
