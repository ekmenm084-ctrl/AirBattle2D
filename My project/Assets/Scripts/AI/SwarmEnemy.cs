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

    public enum DroneClass
    {
        Scout,          // Hızlı klasik gözcü İHA
        Kamikaze,       // Hedefe kilitlenip dalışa geçen intihar dronu
        HeavyShielded,  // Çift namlulu ağır zırhlı dronu
        PlasmaBomber    // Ağır plazma küresi bırakan bombardıman dronu
    }

    [Header("Formasyon & Hareket")]
    public DroneClass droneClass = DroneClass.Scout;
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
    private int _shotsRemaining = 1;

    // Kamikaze kilitlenme durumu
    private int _kamikazePhase = 0; // 0: alçalma, 1: kilitlenme beklemesi, 2: dalış
    private float _kamikazePhaseEndTime;
    private Vector2 _kamikazeDiveDir = Vector2.down;

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

    public void Initialize(MovementPattern movePattern, float speed, float hp, Sprite droneSprite = null, Color? tint = null, DroneClass cls = DroneClass.Scout)
    {
        droneClass = cls;
        pattern = movePattern;
        moveSpeed = speed;
        _spawnTime = Time.time;
        _baseX = transform.position.x;
        _kamikazePhase = 0;

        float scale = 0.72f;
        float hpMul = 1f;
        Color defaultTint = new Color(1f, 0.35f, 0.35f, 1f);

        switch (droneClass)
        {
            case DroneClass.Kamikaze:
                canShoot = false;
                _shotsRemaining = 0;
                hpMul = 0.85f;
                scale = 0.68f;
                defaultTint = new Color(1f, 0.45f, 0.12f, 1f);
                break;

            case DroneClass.HeavyShielded:
                canShoot = true;
                _shotsRemaining = 2;
                hpMul = 2.15f;
                scale = 0.94f;
                bulletDamage = 12f;
                defaultTint = new Color(0.28f, 0.78f, 1f, 1f);
                break;

            case DroneClass.PlasmaBomber:
                canShoot = true;
                _shotsRemaining = 2;
                hpMul = 1.55f;
                scale = 0.88f;
                moveSpeed *= 0.72f;
                bulletDamage = 18f;
                bulletSpeed = 5.6f;
                defaultTint = new Color(0.88f, 0.32f, 1f, 1f);
                break;

            default:
                canShoot = true;
                _shotsRemaining = 1;
                break;
        }

        maxHealth = hp * hpMul;
        currentHealth = maxHealth;
        _nextShootTime = Time.time + fireDelay + Random.Range(0.2f, 0.55f);

        if (droneSprite != null && _sr != null)
        {
            _sr.sprite = droneSprite;
        }

        _originalColor = tint.HasValue ? tint.Value : defaultTint;

        if (_sr != null)
        {
            _sr.color = _originalColor;
            _sr.sortingOrder = 12;
        }

        // İHA'nın yönü varsayılan olarak aşağıya (Oyuncuya) bakar (180 derece)
        transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        transform.localScale = Vector3.one * scale;
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
        if (droneClass == DroneClass.Kamikaze)
        {
            UpdateKamikazeMovement();
            return;
        }

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

    private void UpdateKamikazeMovement()
    {
        if (_kamikazePhase == 0)
        {
            _rb.linearVelocity = Vector2.down * (moveSpeed * 0.9f);
            if (transform.position.y <= 2.4f)
            {
                _kamikazePhase = 1;
                _kamikazePhaseEndTime = Time.time + 0.55f;
                _rb.linearVelocity = Vector2.zero;

                Transform p1 = FindPlayerTarget();
                _kamikazeDiveDir = (p1 != null)
                    ? ((Vector2)(p1.position - transform.position)).normalized
                    : Vector2.down;
                if (_kamikazeDiveDir.y > -0.25f)
                    _kamikazeDiveDir = new Vector2(_kamikazeDiveDir.x, -0.65f).normalized;
            }
        }
        else if (_kamikazePhase == 1)
        {
            _rb.linearVelocity = Vector2.zero;
            float angle = Mathf.Atan2(_kamikazeDiveDir.y, _kamikazeDiveDir.x) * Mathf.Rad2Deg + 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (_sr != null)
            {
                float flash = Mathf.PingPong(Time.time * 14f, 1f);
                _sr.color = Color.Lerp(_originalColor, Color.white, flash * 0.7f);
            }

            if (Time.time >= _kamikazePhaseEndTime)
            {
                _kamikazePhase = 2;
                if (_sr != null) _sr.color = _originalColor;
            }
        }
        else
        {
            _rb.linearVelocity = _kamikazeDiveDir * (moveSpeed * 2.15f);
        }
    }

    private Transform FindPlayerTarget()
    {
        PlaneController[] planes = FindObjectsOfType<PlaneController>();
        foreach (PlaneController p in planes)
        {
            if (p != null && p.isPlayer1) return p.transform;
        }
        return null;
    }

    private void CheckShooting()
    {
        if (!canShoot || _isDead || _shotsRemaining <= 0) return;

        if (Time.time >= _nextShootTime && transform.position.y > 0.4f && transform.position.y < 5.2f)
        {
            _shotsRemaining--;
            _nextShootTime = Time.time + (droneClass == DroneClass.PlasmaBomber ? 1.15f : 0.95f);
            ShootClassSalvo();
        }
    }

    private void ShootClassSalvo()
    {
        Vector3 spawnPos = transform.position + Vector3.down * 0.45f;

        if (droneClass == DroneClass.HeavyShielded)
        {
            Color twinColor = new Color(0.25f, 0.85f, 1f);
            FireDroneBullet(spawnPos + Vector3.left * 0.18f, new Vector2(-0.18f, -1f).normalized, twinColor, 1.0f, 1.0f, 0.85f);
            FireDroneBullet(spawnPos + Vector3.right * 0.18f, new Vector2(0.18f, -1f).normalized, twinColor, 1.0f, 1.0f, 0.85f);
        }
        else if (droneClass == DroneClass.PlasmaBomber)
        {
            FireDroneBullet(spawnPos, Vector2.down, new Color(0.95f, 0.35f, 1f), 0.78f, 1.4f, 1.35f);
        }
        else
        {
            FireDroneBullet(spawnPos, Vector2.down, new Color(1f, 0.25f, 0.15f), 1.0f, 1.0f, 0.85f);
        }
    }

    private void FireDroneBullet(Vector3 spawnPos, Vector2 direction, Color color, float speedScale, float dmgScale, float scale)
    {
        PlaneShooter shooter = FindObjectOfType<PlaneShooter>();
        GameObject prefab = shooter != null ? shooter.bulletPrefab : null;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        GameObject bulletObj = (prefab != null) ? Instantiate(prefab, spawnPos, rot) : new GameObject("SwarmBullet");
        if (prefab == null)
        {
            bulletObj.transform.position = spawnPos;
            bulletObj.transform.rotation = rot;
        }

        Bullet b = bulletObj.GetComponent<Bullet>();
        if (b == null) b = bulletObj.AddComponent<Bullet>();

        b.speed = bulletSpeed * speedScale;
        b.damage = bulletDamage * dmgScale;
        b.owner = gameObject;

        SpriteRenderer bSr = bulletObj.GetComponent<SpriteRenderer>();
        if (bSr != null)
        {
            bSr.color = color;
            bulletObj.transform.localScale = Vector3.one * scale;
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
            int minGold = (droneClass == DroneClass.HeavyShielded) ? 12 : (droneClass == DroneClass.PlasmaBomber ? 9 : 4);
            int maxGold = (droneClass == DroneClass.HeavyShielded) ? 22 : (droneClass == DroneClass.PlasmaBomber ? 16 : 10);
            float ultiGain = (droneClass == DroneClass.HeavyShielded || droneClass == DroneClass.PlasmaBomber) ? 6.5f : 3.5f;
            float lootChance = (droneClass == DroneClass.HeavyShielded || droneClass == DroneClass.PlasmaBomber) ? 0.45f : 0.25f;
            int comboStep = (droneClass == DroneClass.HeavyShielded || droneClass == DroneClass.PlasmaBomber) ? 2 : 1;

            CombatComboManager combo = CombatComboManager.EnsureInstance();
            combo.AddCombo(comboStep, transform.position);

            int baseGold = Random.Range(minGold, maxGold);
            int finalGold = combo.ApplyGoldMultiplier(baseGold, out _);

            HangarManager.EnsureInstance()?.AddGold(finalGold);
            if (GameManager.Instance != null)
                GameManager.Instance.sessionGoldEarned += finalGold;

            PlayerCombatManeuvers.Instance?.AddUltiEnergy(ultiGain);
            DailyOpsManager.EnsureInstance()?.RecordEvent(DailyOpsManager.OpsEventType.SwarmDroneKill, 1);

            if (Random.value < lootChance)
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
                float ramDmg = (droneClass == DroneClass.Kamikaze) ? 24f : 18f;
                health.TakeDamage(ramDmg, false, transform.position);
            }
            Die(killedByPlayer: false);
        }
    }
}

