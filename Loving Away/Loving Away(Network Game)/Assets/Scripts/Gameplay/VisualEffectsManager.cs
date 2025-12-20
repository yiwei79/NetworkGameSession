using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Centralized visual effects manager for hit, death, and respawn effects.
/// Uses object pooling to avoid per-effect Instantiate() calls.
/// Session 4.5: Visual Effects System
/// </summary>
public class VisualEffectsManager : MonoBehaviour
{
    // Singleton instance for easy access
    public static VisualEffectsManager Instance { get; private set; }

    [Header("Pool Settings")]
    public int hitEffectPoolSize = 10;
    public int deathEffectPoolSize = 5;
    public int respawnEffectPoolSize = 5;

    [Header("Hit Effect Settings")]
    public Color hitColorStart = new Color(1f, 0.9f, 0.2f, 1f);  // Yellow
    public Color hitColorEnd = new Color(1f, 0.5f, 0.1f, 0f);    // Orange -> Transparent
    public float hitDuration = 0.5f;
    public int hitParticleCount = 30;
    public float hitParticleSpeed = 5f;
    public float hitParticleSize = 0.15f;

    [Header("Death Effect Settings")]
    public Color deathColorStart = new Color(1f, 0.2f, 0.1f, 1f); // Red
    public Color deathColorEnd = new Color(0.5f, 0.1f, 0.1f, 0f); // Dark Red -> Transparent
    public float deathDuration = 1.0f;
    public int deathParticleCount = 50;
    public float deathParticleSpeed = 4f;
    public float deathParticleSize = 0.25f;

    [Header("Respawn Effect Settings")]
    public Color respawnColorStart = new Color(0.2f, 1f, 0.4f, 1f); // Green
    public Color respawnColorEnd = new Color(0.3f, 1f, 1f, 0f);     // Cyan -> Transparent
    public float respawnDuration = 0.8f;
    public int respawnParticleCount = 40;
    public float respawnParticleSpeed = 2f;
    public float respawnParticleSize = 0.12f;

    [Header("Screen Shake Settings")]
    public float defaultShakeIntensity = 0.3f;
    public float defaultShakeDuration = 0.2f;

    // Object pools
    private Queue<ParticleSystem> hitEffectPool;
    private Queue<ParticleSystem> deathEffectPool;
    private Queue<ParticleSystem> respawnEffectPool;

    // Screen shake state
    private Coroutine activeShake;
    private Vector3 originalCameraPosition;

    // Shared material for particles
    private Material particleMaterial;

    void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize pools
        hitEffectPool = new Queue<ParticleSystem>();
        deathEffectPool = new Queue<ParticleSystem>();
        respawnEffectPool = new Queue<ParticleSystem>();

        // Create shared material
        CreateParticleMaterial();

        // Pre-populate pools
        InitializePools();

        UnityEngine.Debug.Log("[VisualEffectsManager] Initialized with pooled effects");
    }

    void CreateParticleMaterial()
    {
        // Try to find a suitable shader
        Shader particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader == null)
        {
            particleShader = Shader.Find("Legacy Shaders/Particles/Additive");
        }
        if (particleShader == null)
        {
            particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }
        if (particleShader == null)
        {
            // Fallback to default
            particleShader = Shader.Find("Sprites/Default");
        }

        if (particleShader != null)
        {
            particleMaterial = new Material(particleShader);
        }
        else
        {
            UnityEngine.Debug.LogWarning("[VisualEffectsManager] Could not find particle shader, effects may not render correctly");
        }
    }

    void InitializePools()
    {
        // Create hit effects
        for (int i = 0; i < hitEffectPoolSize; i++)
        {
            ParticleSystem ps = CreateParticleSystem("HitEffect", hitColorStart, hitColorEnd, hitDuration, hitParticleCount, hitParticleSpeed, hitParticleSize, 2f);
            ps.gameObject.SetActive(false);
            hitEffectPool.Enqueue(ps);
        }

        // Create death effects
        for (int i = 0; i < deathEffectPoolSize; i++)
        {
            ParticleSystem ps = CreateParticleSystem("DeathEffect", deathColorStart, deathColorEnd, deathDuration, deathParticleCount, deathParticleSpeed, deathParticleSize, 1f);
            ps.gameObject.SetActive(false);
            deathEffectPool.Enqueue(ps);
        }

        // Create respawn effects
        for (int i = 0; i < respawnEffectPoolSize; i++)
        {
            ParticleSystem ps = CreateParticleSystem("RespawnEffect", respawnColorStart, respawnColorEnd, respawnDuration, respawnParticleCount, respawnParticleSpeed, respawnParticleSize, -1f);
            ps.gameObject.SetActive(false);
            respawnEffectPool.Enqueue(ps);
        }
    }

    ParticleSystem CreateParticleSystem(string name, Color startColor, Color endColor, float duration, int particleCount, float speed, float size, float gravity)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        // Main module
        var main = ps.main;
        main.duration = duration;
        main.startLifetime = duration;
        main.startSpeed = speed;
        main.startSize = size;
        main.gravityModifier = gravity;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = particleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Emission module - burst mode
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, particleCount)
        });

        // Shape module - sphere
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        // Color over lifetime - fade to transparent
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // Size over lifetime - shrink
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.3f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Renderer
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (particleMaterial != null)
        {
            renderer.material = particleMaterial;
        }

        return ps;
    }

    #region Public API

    /// <summary>
    /// Play explosion effect at hit position (yellow/orange particles)
    /// </summary>
    public void PlayHitEffect(Vector3 position)
    {
        PlayPooledEffect(hitEffectPool, position, hitDuration);
    }

    /// <summary>
    /// Play death effect at death position (red particles)
    /// </summary>
    public void PlayDeathEffect(Vector3 position)
    {
        PlayPooledEffect(deathEffectPool, position, deathDuration);
    }

    /// <summary>
    /// Play respawn effect at spawn position (green/cyan particles floating up)
    /// </summary>
    public void PlayRespawnEffect(Vector3 position)
    {
        PlayPooledEffect(respawnEffectPool, position, respawnDuration);
    }

    /// <summary>
    /// Trigger screen shake effect
    /// </summary>
    /// <param name="intensity">Shake intensity (use -1 for default)</param>
    /// <param name="duration">Shake duration in seconds (use -1 for default)</param>
    public void TriggerScreenShake(float intensity = -1f, float duration = -1f)
    {
        if (Camera.main == null)
        {
            UnityEngine.Debug.LogWarning("[VisualEffectsManager] Camera.main is null, cannot shake");
            return;
        }

        float actualIntensity = intensity > 0 ? intensity : defaultShakeIntensity;
        float actualDuration = duration > 0 ? duration : defaultShakeDuration;

        // Stop any existing shake and reset position
        if (activeShake != null)
        {
            StopCoroutine(activeShake);
            Camera.main.transform.localPosition = originalCameraPosition;
        }

        activeShake = StartCoroutine(ShakeCoroutine(actualIntensity, actualDuration));
    }

    #endregion

    #region Private Helpers

    void PlayPooledEffect(Queue<ParticleSystem> pool, Vector3 position, float duration)
    {
        if (pool.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[VisualEffectsManager] Effect pool empty, effect skipped");
            return;
        }

        ParticleSystem ps = pool.Dequeue();
        ps.transform.position = position;
        ps.gameObject.SetActive(true);
        ps.Clear();
        ps.Play();

        // Return to pool after effect completes
        StartCoroutine(ReturnToPoolAfterDelay(ps, pool, duration + 0.1f));
    }

    IEnumerator ReturnToPoolAfterDelay(ParticleSystem ps, Queue<ParticleSystem> pool, float delay)
    {
        yield return new WaitForSeconds(delay);

        ps.Stop();
        ps.gameObject.SetActive(false);
        pool.Enqueue(ps);
    }

    IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        originalCameraPosition = Camera.main.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Dampening factor - shake fades out over time
            float dampening = 1f - (elapsed / duration);

            // Random offset
            float offsetX = Random.Range(-1f, 1f) * intensity * dampening;
            float offsetY = Random.Range(-1f, 1f) * intensity * dampening;

            Camera.main.transform.localPosition = originalCameraPosition + new Vector3(offsetX, offsetY, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reset to original position
        Camera.main.transform.localPosition = originalCameraPosition;
        activeShake = null;
    }

    #endregion

    void OnDestroy()
    {
        // Cleanup singleton
        if (Instance == this)
        {
            Instance = null;
        }

        // Clean up material
        if (particleMaterial != null)
        {
            Destroy(particleMaterial);
        }
    }
}
