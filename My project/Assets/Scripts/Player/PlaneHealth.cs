using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Uçağın can sistemi. Hasar alır, ölür, UI'ı günceller.
/// </summary>
public class PlaneHealth : MonoBehaviour
{
    [Header("Can Ayarları")]
    public float maxHealth = 100f;
    public float currentHealth { get; private set; }

    [Header("Görsel Efektler")]
    public GameObject explosionPrefab;

    [Header("Olaylar")]
    public UnityEvent<float> onHealthChanged;
    public UnityEvent onDeath;

    public bool isInvulnerable { get; set; } = false;

    private bool _isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (_isDead || isInvulnerable) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        onHealthChanged?.Invoke(currentHealth / maxHealth);

        GameUI ui = FindObjectOfType<GameUI>();
        ui?.UpdateHealthBars();

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        onDeath?.Invoke();

        GameManager.Instance?.OnPlaneDead(this);

        PlaneController pc = GetComponent<PlaneController>();
        if (pc != null) pc.FreezeMovement();

        PlaneShooter ps = GetComponent<PlaneShooter>();
        if (ps != null) ps.enabled = false;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    /// <summary>
    /// Uçağın canını yeni dalga için sıfırlar ve tüm bileşenlerini yeniden aktif eder.
    /// </summary>
    public void ResetHealth(float newMaxHealth)
    {
        maxHealth = newMaxHealth;
        currentHealth = newMaxHealth;
        _isDead = false;

        PlaneController pc = GetComponent<PlaneController>();
        if (pc != null) pc.enabled = true;

        PlaneShooter ps = GetComponent<PlaneShooter>();
        if (ps != null) ps.enabled = true;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = true;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        onHealthChanged?.Invoke(1f);

        GameUI ui = FindObjectOfType<GameUI>();
        ui?.UpdateHealthBars();
    }

    /// <summary>
    /// Düşman geçildiğinde oyuncuya can tamiri ödülü verir.
    /// </summary>
    public void Heal(float amount)
    {
        if (_isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        onHealthChanged?.Invoke(currentHealth / maxHealth);

        GameUI ui = FindObjectOfType<GameUI>();
        ui?.UpdateHealthBars();
    }

    /// <summary>
    /// Yeniden doğuşta uçağa kısa süreli dokunulmazlık ve yanıp sönme efekti verir.
    /// </summary>
    public void TriggerInvulnerability(float duration)
    {
        StartCoroutine(InvulnerabilityRoutine(duration));
    }

    private System.Collections.IEnumerator InvulnerabilityRoutine(float duration)
    {
        isInvulnerable = true;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (sr != null)
                sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(0.12f);
            elapsed += 0.12f;
        }
        if (sr != null) sr.enabled = true;
        isInvulnerable = false;
    }

    public bool IsDead => _isDead;
    public float HealthPercent => maxHealth > 0f ? (currentHealth / maxHealth) : 0f;
}
