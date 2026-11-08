using System.Collections;
using UnityEngine;

/// <summary>
/// Hızlı ve çevik Sürü Düşman İHA'sı (Swarm Interceptor Drone).
/// Belirli filolar halinde ekranın üstünden akın eder, formasyon manevraları yapar
/// ve yok edildiğinde taktik hasar sayıları, puan ve altın bırakır.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class SwarmEnemy : MonoBehaviour
{
    public enum MovementPattern
    {
        StraightDown,
        SineWave,
        DivePincer
    }

    [Header("Formasyon & Hareket")]
    public MovementPattern pattern = MovementPattern.StraightDown;
    public float moveSpeed = 4.2f;
    public float sineFrequency = 3.2f;
    public float sineAmplitude = 1.3f;
    public Vector2 diveDirection = Vector2.down;

    [Header("Dayanıklılık")]
    public float maxHealth = 30f;
    public float currentHealth { get; private set; }

    [Header("Ateş Gücü")]
    public bool canShoot = true;
    public float fireDelay = 0.8f;
    public float bulletDamage = 10f;
    public float bulletSpeed = 7.5f;

    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private Color _originalColor = Color.white;
    private float _spawnTime;
    private float _baseX;
    private bool _isDead = false;
    private Coroutine _flashCoroutine;
    private float _nextShootTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.42f;

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
        }
    }

    public void Initialize(MovementPattern movePattern, float speed, float hp, Sprite droneSprite = null, Color? tint = null)
    {
        pattern = movePattern;
        moveSpeed = speed;
        maxHealth = hp;
        currentHealth = hp;
        _spawnTime = Time.time;
        _baseX = transform.position.x;
        _nextShootTime = Time.time + fireDelay + Random.Range(0.2f, 0.6f);

        if (droneSprite != null && _sr != null)
        {
            _sr.sprite = droneSprite;
        }

        if (tint.HasValue)
        {
            _originalColor = tint.Value;
        }
        else
        {
            _originalColor = new Color(1f, 0.35f, 0.35f, 1f); // Taktiksel kırmızı tehdit tonu
        }

        if (_sr != null)
        {
            _sr.color = _originalColor;
            _sr.sortingOrder = 12;
        }

        // İHA'nın yönü varsayılan olarak aşağıya (Oyuncuya) bakar (180 derece)
        transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        transform.localScale = Vector3.one * 0.72f; // Standart uçaktan daha küçük ve seri
    }

    private void Update()
    {
        if (_isDead) return;

        UpdateMovement();
        CheckShooting();

        // Ekranın altından çıktıysa sessizce temizle
        if (transform.position.y < -6.5f)
        {
            EnemyFormationManager.Instance?.OnSwarmEnemyEscaped(this);
            Destroy(gameObject);
        }
    }

    private void UpdateMovement()
    {
        float elapsed = Time.time - _spawnTime;

        switch (pattern)
        {
            case MovementPattern.StraightDown:
                _rb.linearVelocity = Vector2.down * moveSpeed;
                break;

            case MovementPattern.SineWave:
                float targetX = _baseX + Mathf.Sin(elapsed * sineFrequency) * sineAmplitude;
                float newY = transform.position.y - moveSpeed * Time.deltaTime;
                transform.position = new Vector3(targetX, newY, 0f);
                // Kıvrılma yönüne göre uçağı hafif yatır (Bank Angle)
                float bankAngle = 180f + Mathf.Cos(elapsed * sineFrequency) * 22f;
                transform.rotation = Quaternion.Euler(0f, 0f, bankAngle);
                break;

            case MovementPattern.DivePincer:
                _rb.linearVelocity = diveDirection.normalized * moveSpeed;
                float angle = Mathf.Atan2(diveDirection.y, diveDirection.x) * Mathf.Rad2Deg + 90f;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
                break;
        }
    }

    private void CheckShooting()
    {
        if (!canShoot || _isDead) return;

        // Yalnızca ekranın üst yarısındayken (y > 0.5f) 1 kez ateş etsin
        if (Time.time >= _nextShootTime && transform.position.y > 0.5f && transform.position.y < 5.5f)
        {
            _nextShootTime = Time.time + 999f; // Her sürü uçağı dalga başına 1 kez salvoda bulunur
            ShootLightBullet();
        }
    }

    private void ShootLightBullet()
    {
        // Standart mermi prefab'ı veya prosedürel mermi fırlat
        PlaneShooter shooter = FindObjectOfType<PlaneShooter>();
        GameObject prefab = shooter != null ? shooter.bulletPrefab : null;

        Vector3 spawnPos = transform.position + Vector3.down * 0.45f;
        Quaternion rot = Quaternion.Euler(0f, 0f, 180f);

        GameObject bulletObj;
        if (prefab != null)
        {
            bulletObj = Instantiate(prefab, spawnPos, rot);
        }
        else
        {
            bulletObj = new GameObject("SwarmBullet");
            bulletObj.transform.position = spawnPos;
            bulletObj.transform.rotation = rot;
        }

        Bullet b = bulletObj.GetComponent<Bullet>();
        if (b == null) b = bulletObj.AddComponent<Bullet>();

        b.speed = bulletSpeed;
        b.damage = bulletDamage;
        b.owner = gameObject;

        SpriteRenderer bSr = bulletObj.GetComponent<SpriteRenderer>();
        if (bSr != null)
        {
            bSr.color = new Color(1f, 0.25f, 0.15f); // Kırmızı izli mermi
            bulletObj.transform.localScale = Vector3.one * 0.85f;
        }
    }

    public void TakeDamage(float amount, bool isCrit = false, Vector3 hitPoint = default)
    {
        if (_isDead) return;

        if (hitPoint == default)
        {
            hitPoint = transform.position + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f), 0f);
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        // Taktik Hasar Sayısı Gösterimi
        DamageNumberManager.EnsureInstance()?.SpawnDamageNumber(hitPoint, amount, isCrit, isPlayerDamage: false);

        // Vuruş Flaş Efekti
        TriggerHitFlash();
        VFXManager.SpawnHitSparks(hitPoint, isCrit ? new Color(1f, 0.95f, 0.1f) : new Color(1f, 0.8f, 0.2f));

        if (currentHealth <= 0f)
        {
            Die(killedByPlayer: true);
        }
    }

    private void TriggerHitFlash()
    {
        if (_sr == null) return;
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        _sr.color = Color.white;
        yield return new WaitForSeconds(0.045f);
        if (_sr != null)
        {
            _sr.color = _originalColor;
        }
        _flashCoroutine = null;
    }

    private void Die(bool killedByPlayer)
    {
        if (_isDead) return;
        _isDead = true;

        // Görsel Patlama & Ses Efekti
        VFXManager.SpawnExplosion(transform.position, isBoss: false);
        AudioManager.Instance?.PlayExplosion(isBoss: false);

        if (killedByPlayer)
        {
            // Oyuncuya küçük ödül (Altın & Ulti)
            PlayerUpgradeManager.Instance?.AddGold(Random.Range(4, 10));
            PlayerCombatManeuvers.Instance?.AddUltiEnergy(3.5f);

            // %25 İhtimalle Ganimet Kutusu Bırak
            if (Random.value < 0.25f)
            {
                LootDropManager.TryDropLoot(transform.position, isBoss: false);
            }

            EnemyFormationManager.Instance?.OnSwarmEnemyKilled(this);
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isDead) return;

        // Oyuncu uçağına gövde çarpışması (Ramming)
        PlaneController pc = other.GetComponent<PlaneController>();
        if (pc != null && pc.isPlayer1)
        {
            PlaneHealth health = pc.GetComponent<PlaneHealth>();
            if (health != null && !health.isInvulnerable)
            {
                health.TakeDamage(18f, false, transform.position);
            }
            Die(killedByPlayer: false);
        }
    }
}

