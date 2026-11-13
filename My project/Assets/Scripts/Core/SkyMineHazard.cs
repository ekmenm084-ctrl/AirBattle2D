using UnityEngine;

/// <summary>
/// Havadan inen ve ateş edilerek yok edilebilen taktik hava mayını / meteor tehlikesi.
/// Oyuncunun mermileriyle vurulup patlatılabilir (+Altın, +Combo ve yakındaki düşmanlara alan hasarı),
/// ya da yanından sıyrılarak geçilebilir.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class SkyMineHazard : MonoBehaviour
{
    public float fallSpeed = 4.6f;
    public float maxHealth = 45f;
    public float damage = 20f;
    public int goldReward = 8;

    private float _currentHealth;
    private float _rotSpeed;
    private float _baseX;
    private float _swayAmp;
    private float _swayFreq;
    private float _spawnTime;
    private bool _isDead = false;
    private bool _hasGrazedPlayer = false;

    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private Color _baseColor = new Color(1f, 0.32f, 0.16f);
    private Coroutine _flashCoroutine;

    private static Sprite _cachedMineSprite;

    public void Initialize(float speed, float hp, float dmg)
    {
        fallSpeed = speed;
        maxHealth = hp;
        _currentHealth = hp;
        damage = dmg;
        _spawnTime = Time.time;
        _baseX = transform.position.x;
        _swayAmp = Random.Range(0.25f, 0.70f);
        _swayFreq = Random.Range(1.6f, 2.8f);
        _rotSpeed = Random.Range(-150f, 150f);
        if (Mathf.Abs(_rotSpeed) < 55f) _rotSpeed = 75f;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.42f;

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = GetOrCreateMineSprite();
        _sr.color = _baseColor;
        _sr.sortingOrder = 11;

        transform.localScale = Vector3.one * 0.92f;
        _currentHealth = maxHealth;
    }

    private void Update()
    {
        if (_isDead || Time.timeScale <= 0.01f) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameEnded)
        {
            Destroy(gameObject);
            return;
        }

        float elapsed = Time.time - _spawnTime;
        float x = _baseX + Mathf.Sin(elapsed * _swayFreq) * _swayAmp;
        float y = transform.position.y - fallSpeed * Time.deltaTime;
        transform.position = new Vector3(x, y, 0f);
        transform.Rotate(0f, 0f, _rotSpeed * Time.deltaTime);

        // Nabız atan tehlike rengi
        if (_sr != null && _flashCoroutine == null)
        {
            float pulse = Mathf.PingPong(Time.time * 5.5f, 1f);
            _sr.color = Color.Lerp(_baseColor, new Color(1f, 0.72f, 0.2f), pulse * 0.5f);
        }

        CheckPlayerProximity();

        if (y < -6.2f)
        {
            Destroy(gameObject);
        }
    }

    private void CheckPlayerProximity()
    {
        if (_hasGrazedPlayer) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsGameEnded || gm.plane1Health == null || gm.plane1Health.IsDead) return;

        PlaneHealth p1 = gm.plane1Health;
        Vector2 toPlayer = (Vector2)(p1.transform.position - transform.position);
        float distSq = toPlayer.sqrMagnitude;
        float grazeRadius = 1.45f;

        if (distSq <= grazeRadius * grazeRadius && (p1.isInvulnerable || transform.position.y <= p1.transform.position.y))
        {
            _hasGrazedPlayer = true;
            CombatComboManager.EnsureInstance().RegisterBulletGraze(transform.position, p1.isInvulnerable);
        }
    }

    public void TakeDamage(float amount, bool isCrit, Vector3 hitPoint)
    {
        if (_isDead) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        DamageNumberManager.EnsureInstance()?.SpawnDamageNumber(hitPoint, amount, isCrit, isPlayerDamage: false);
        VFXManager.SpawnHitSparks(hitPoint, new Color(1f, 0.65f, 0.2f));

        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashRoutine());

        if (_currentHealth <= 0f)
        {
            Explode(killedByPlayer: true);
        }
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        if (_sr != null) _sr.color = Color.white;
        yield return new WaitForSeconds(0.05f);
        if (_sr != null) _sr.color = _baseColor;
        _flashCoroutine = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isDead) return;

        PlaneController pc = other.GetComponent<PlaneController>();
        if (pc != null && pc.isPlayer1)
        {
            PlaneHealth health = pc.GetComponent<PlaneHealth>();
            if (health != null && !health.isInvulnerable && !health.IsDead)
            {
                _hasGrazedPlayer = true;
                health.TakeDamage(damage, false, transform.position);
                Explode(killedByPlayer: false);
            }
        }
    }

    private void Explode(bool killedByPlayer)
    {
        if (_isDead) return;
        _isDead = true;

        VFXManager.SpawnExplosion(transform.position, isBoss: false);
        AudioManager.Instance?.PlayExplosion(isBoss: false);

        if (killedByPlayer)
        {
            CombatComboManager combo = CombatComboManager.EnsureInstance();
            combo.AddCombo(1, transform.position);
            int finalGold = combo.ApplyGoldMultiplier(goldReward, out _);

            HangarManager.EnsureInstance()?.AddGold(finalGold);
            if (GameManager.Instance != null)
                GameManager.Instance.sessionGoldEarned += finalGold;

            PlayerCombatManeuvers.Instance?.AddUltiEnergy(4.0f);
            DamageNumberManager.EnsureInstance()?.SpawnCustomText(
                transform.position + Vector3.up * 0.35f,
                $"MAYIN İMHA! +{finalGold}G",
                new Color(1f, 0.85f, 0.25f),
                1.05f
            );

            // Yakındaki düşman sürü İHA'larına şok dalgası hasarı
            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, 2.3f);
            for (int i = 0; i < nearby.Length; i++)
            {
                SwarmEnemy se = nearby[i].GetComponent<SwarmEnemy>();
                if (se != null && se.currentHealth > 0f)
                {
                    se.TakeDamage(28f, false, se.transform.position);
                }
            }
        }

        Destroy(gameObject);
    }

    private static Sprite GetOrCreateMineSprite()
    {
        if (_cachedMineSprite != null) return _cachedMineSprite;

        const int size = 48;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float spike = 0.72f + 0.24f * Mathf.Abs(Mathf.Cos(angle * 4f));

                if (dist <= 0.42f)
                {
                    tex.SetPixel(x, y, Color.white);
                }
                else if (dist <= spike)
                {
                    float edge = Mathf.Clamp01((spike - dist) * 6f);
                    tex.SetPixel(x, y, new Color(1f, 0.78f, 0.45f, edge));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();

        _cachedMineSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 48f);
        return _cachedMineSprite;
    }
}
