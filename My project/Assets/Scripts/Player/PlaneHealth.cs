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

        // Otomatik Görsel Efekt Bileşeni (Jet İzi, Hit Flash, Duman)
        if (GetComponent<PlaneVisualEffects>() == null)
            gameObject.AddComponent<PlaneVisualEffects>();
    }

    public void TakeDamage(float amount)
    {
        if (_isDead || isInvulnerable) return;

        PlaneController pc = GetComponent<PlaneController>();
        PlaneVisualEffects pve = GetComponent<PlaneVisualEffects>();

        // Player 1 için aktif enerji kalkanı hasar soğurması
        if (pc != null && pc.isPlayer1 && PlayerUpgradeManager.Instance != null)
        {
            amount = PlayerUpgradeManager.Instance.AbsorbDamage(amount);
            if (amount <= 0f)
            {
                // Tüm darbe kalkan tarafından emildi! Can düşmez.
                pve?.TriggerHitFlash(isShieldHit: true);
                VFXManager.SpawnHitSparks(transform.position, Color.cyan);
                CameraShake.ShakeHit();

                GameUI uiShield = FindObjectOfType<GameUI>();
                uiShield?.UpdateHealthBars();
                return;
            }
        }

        // Hasar alındı - Vuruş Flaş Efekti ve Mermi Kıvılcımı
        pve?.TriggerHitFlash(isShieldHit: false);
        VFXManager.SpawnHitSparks(transform.position, new Color(1f, 0.85f, 0.2f));

        if (pc != null && pc.isPlayer1)
            CameraShake.ShakeHit();

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

        PlaneController pc = GetComponent<PlaneController>();
        bool isBoss = (pc != null && !pc.isPlayer1 && GameManager.Instance != null && GameManager.Instance.IsBossPlane(GameManager.Instance.currentPlaneIndex));
        bool isFinalBoss = (pc != null && !pc.isPlayer1 && GameManager.Instance != null &&
            ((GameManager.Instance.CurrentMode == GameManager.GameMode.BossRush && GameManager.Instance.currentPlaneIndex >= 4) ||
             (GameManager.Instance.CurrentMode == GameManager.GameMode.SoloVsAI && GameManager.Instance.currentPlaneIndex >= GameManager.MaxPlanes)));

        // Sinematik Prosedürel Patlama & Sarsıntı (Final Boss ise ilk darbe, ardından zincirleme patlama GameManager tarafından yönetilir)
        if (!isFinalBoss)
        {
            VFXManager.SpawnExplosion(transform.position, isBoss);
        }

        // Düşman uçağı düştüğünde ganimet (Loot) bırak
        if (pc != null && !pc.isPlayer1)
        {
            LootDropManager.TryDropLoot(transform.position, isBoss);
        }

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        onDeath?.Invoke();

        if (pc != null) pc.FreezeMovement();

        PlaneShooter ps = GetComponent<PlaneShooter>();
        if (ps != null) ps.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Normal düşmanlarda uçağı hemen gizle; Final Boss için ağır çekim patlama sekansı bitince gizlenecek!
        if (!isFinalBoss)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }

        GameManager.Instance?.OnPlaneDead(this);
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

        GetComponent<PlaneVisualEffects>()?.ResetVisuals();

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
