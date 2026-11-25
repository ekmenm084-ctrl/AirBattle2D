using UnityEngine;

/// <summary>
/// Yüksek hızlı gerçekçi taktik izli mermi (Autocannon Realistic Tracer Round).
/// Her uçağın kendine has kalibre boyutu, aerodinamik şekli, duman/lazer iz çizgisi (TrailRenderer),
/// yanan akkor çekirdek titreşim animasyonu ve isabet kıvılcımları bulunur.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Mermi Ayarları")]
    public float speed = 20f;          // Merminin namlu çıkış hızı
    public float lifetime = 2f;
    public float damage = 20f;
    public bool isCritical = false;    // 2 Kat Hasar veren altın kritik vuruş

    [Header("Güdümlü Füze (Opsiyonel)")]
    public Transform homingTarget;
    public float homingTurnSpeed = 0f; // Derece/sn (0 = düz mermi)
    public float homingDuration = 1.6f;

    [HideInInspector]
    public GameObject owner;

    // Dinamik Uçak Özel Görsel ve Animasyon Değişkenleri
    public HangarManager.AircraftType? aircraftStyle;
    private Color _sparkColor = new Color(1f, 0.85f, 0.25f);
    private Color _baseSpriteColor = Color.white;
    private bool _isA10Heavy = false;
    private bool _isSupersonic = false;
    private float _emberTimer = 0f;
    private float _animSeed = 0f;
    private TrailRenderer _trailRenderer;
    private SpriteRenderer _sr;

    private Rigidbody2D _rb;
    private float _age = 0f;
    private bool _hasGrazedPlayer = false;
    private GameObject _checkedOwner;
    private bool _ownerIsPlayer1 = false;

    // Sprite Önbelleği (Resources üzerinden tek seferde yüklenir)
    private static Sprite _sprF16;
    private static Sprite _sprA10;
    private static Sprite _sprF22;
    private static Sprite _sprSu57;
    private static Sprite _sprF35;
    private static Sprite _sprKaan;
    private static Sprite _sprEnemy;
    private static Sprite _sprBoss;
    private static Sprite _sprTracerDefault;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _sr = GetComponent<SpriteRenderer>();
        _animSeed = Random.Range(0f, 100f);

        // Çarpışma kutusunu aerodinamik izli mermi boyutuna çek
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = new Vector2(0.26f, 0.85f);
            box.isTrigger = true;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            circle.radius = 0.3f;
            circle.isTrigger = true;
        }
    }

    private void Start()
    {
        if (isCritical)
        {
            transform.localScale = transform.localScale * 1.35f;
            _sparkColor = new Color(1f, 0.95f, 0.2f);
        }

        if (_rb != null)
        {
            _rb.linearVelocity = transform.up * speed;
        }

        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Uçağın türüne göre merminin sprite'ını, hızını, boyutunu, duman/lazer izini ve efektlerini ayarlar.
    /// </summary>
    public void ApplyAircraftStyle(HangarManager.AircraftType type)
    {
        aircraftStyle = type;
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();

        EnsureSpriteCache();
        Sprite chosenSprite = null;
        Vector3 targetScale = Vector3.one;
        Color trailStartColor = Color.white;
        Color trailMidColor = Color.yellow;
        Color trailEndColor = new Color(1f, 0.5f, 0f, 0f);
        float trailWidth = 0.20f;
        float trailTime = 0.11f;

        switch (type)
        {
            case HangarManager.AircraftType.A10TankBuster:
                // A-10C GAU-8 Avenger (30mm Seyreltilmiş Uranyum / Tok ve Ağır Kor-Duman)
                chosenSprite = _sprA10 != null ? _sprA10 : _sprTracerDefault;
                speed = 19.5f;
                targetScale = new Vector3(1.30f, 1.25f, 1f);
                _sparkColor = new Color(1.0f, 0.38f, 0.10f); // Ağır kızıl-bakır erimiş şarapnel
                _baseSpriteColor = Color.white;
                _isA10Heavy = true;
                trailStartColor = new Color(1.0f, 0.88f, 0.65f, 1.0f);
                trailMidColor = new Color(1.0f, 0.28f, 0.08f, 0.85f);
                trailEndColor = new Color(0.40f, 0.10f, 0.05f, 0.0f);
                trailWidth = 0.32f;
                trailTime = 0.15f;
                break;

            case HangarManager.AircraftType.F22Raptor:
                // F-22A Raptor (20mm M61A2 Süpersonik Kinetik / Elektrik Cyan & Buz Beyazı)
                chosenSprite = _sprF22 != null ? _sprF22 : _sprTracerDefault;
                speed = 26.0f;
                targetScale = new Vector3(0.85f, 1.35f, 1f);
                _sparkColor = new Color(0.15f, 0.95f, 1.0f); // Elektrik mavisi süpersonik kıvılcım
                _baseSpriteColor = Color.white;
                _isSupersonic = true;
                trailStartColor = new Color(0.90f, 0.98f, 1.0f, 1.0f);
                trailMidColor = new Color(0.0f, 0.85f, 1.0f, 0.85f);
                trailEndColor = new Color(0.05f, 0.20f, 0.85f, 0.0f);
                trailWidth = 0.16f;
                trailTime = 0.09f;
                break;

            case HangarManager.AircraftType.Su57Felon:
                // Su-57 Felon (GSh-30-1 Rus Ağır Plazma / Yakut Kırmızısı & Menekşe Kor)
                chosenSprite = _sprSu57 != null ? _sprSu57 : _sprTracerDefault;
                speed = 23.5f;
                targetScale = new Vector3(1.10f, 1.25f, 1f);
                _sparkColor = new Color(1.0f, 0.18f, 0.55f); // Yakut-mor plazma patlaması
                _baseSpriteColor = Color.white;
                trailStartColor = new Color(1.0f, 0.80f, 0.95f, 0.95f);
                trailMidColor = new Color(1.0f, 0.10f, 0.35f, 0.85f);
                trailEndColor = new Color(0.65f, 0.05f, 0.95f, 0.0f);
                trailWidth = 0.24f;
                trailTime = 0.13f;
                break;

            case HangarManager.AircraftType.F35Lightning:
                // F-35A Lightning (GAU-22/A 25mm / Siber Zümrüt Yeşili & Altın İyonizasyon)
                chosenSprite = _sprF35 != null ? _sprF35 : _sprTracerDefault;
                speed = 25.0f;
                targetScale = new Vector3(0.95f, 1.20f, 1f);
                _sparkColor = new Color(0.20f, 1.0f, 0.55f); // Neon siber zümrüt kıvılcımları
                _baseSpriteColor = Color.white;
                trailStartColor = new Color(0.95f, 1.0f, 0.85f, 1.0f);
                trailMidColor = new Color(0.0f, 0.95f, 0.45f, 0.85f);
                trailEndColor = new Color(0.85f, 0.85f, 0.05f, 0.0f);
                trailWidth = 0.19f;
                trailTime = 0.11f;
                break;

            case HangarManager.AircraftType.KaanApex:
                // TAI KAAN Apex (Milli 5.+ Nesil / Şanlı Turkuaz & İmparatorluk Altını Zirve İz)
                chosenSprite = _sprKaan != null ? _sprKaan : _sprTracerDefault;
                speed = 27.5f;
                targetScale = new Vector3(1.08f, 1.35f, 1f);
                _sparkColor = new Color(0.0f, 0.95f, 0.95f); // Türk Turkuazı zafer kıvılcımları
                _baseSpriteColor = Color.white;
                _isSupersonic = true;
                trailStartColor = new Color(0.95f, 1.0f, 1.0f, 1.0f);
                trailMidColor = new Color(0.0f, 0.90f, 0.95f, 0.90f);
                trailEndColor = new Color(1.0f, 0.85f, 0.15f, 0.0f); // Altın-turkuaz çift renkli görkemli iz!
                trailWidth = 0.24f;
                trailTime = 0.13f;
                break;

            default: // F-16 Viper
                // F-16C Viper (20mm M61A1 Vulcan / Sıcak Altın-Kehribar İzli Taktik Avcı)
                chosenSprite = _sprF16 != null ? _sprF16 : _sprTracerDefault;
                speed = 22.5f;
                targetScale = new Vector3(0.95f, 1.15f, 1f);
                _sparkColor = new Color(1.0f, 0.85f, 0.25f); // Sıcak altın-sarı kıvılcım
                _baseSpriteColor = Color.white;
                trailStartColor = new Color(1.0f, 0.95f, 0.70f, 0.95f);
                trailMidColor = new Color(1.0f, 0.65f, 0.15f, 0.80f);
                trailEndColor = new Color(1.0f, 0.35f, 0.05f, 0.0f);
                trailWidth = 0.20f;
                trailTime = 0.11f;
                break;
        }

        // Seçili kokpit teması Siber Mavi dışında ise oyuncu mermi izlerini ve kıvılcımlarını temayla uyumlu hale getir
        if (GameSettings.SelectedCockpitTheme != GameSettings.CockpitTheme.CyberBlue && !isCritical)
        {
            Color themePrimary = GameSettings.GetCockpitHpBarColor();
            Color themeSecondary = GameSettings.GetCockpitUltiBarColor();
            _sparkColor = themeSecondary;
            trailStartColor = Color.Lerp(Color.white, themeSecondary, 0.35f);
            trailMidColor = new Color(themePrimary.r, themePrimary.g, themePrimary.b, 0.90f);
            trailEndColor = new Color(themePrimary.r * 0.45f, themePrimary.g * 0.45f, themePrimary.b * 0.45f, 0f);
            _baseSpriteColor = Color.Lerp(Color.white, themeSecondary, 0.30f);
        }

        if (_sr != null && chosenSprite != null)
        {
            _sr.sprite = chosenSprite;
            _sr.color = _baseSpriteColor;
        }
        transform.localScale = targetScale;

        // Aerodinamik İz Çizgisi (TrailRenderer) Kurulumu
        SetupTrailRenderer(trailStartColor, trailMidColor, trailEndColor, trailWidth, trailTime);

        // Hız değiştiyse Rigidbody hızını anında güncelle
        if (_rb != null)
        {
            _rb.linearVelocity = transform.up * speed;
        }
    }

    /// <summary>
    /// Düşman uçakları ve Boss salvosu için görsel stil ayarlar.
    /// </summary>
    public void ApplyEnemyStyle(bool isHeavyBoss, bool isSniper, Color? customColor)
    {
        EnsureSpriteCache();
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();

        if (isHeavyBoss)
        {
            if (_sr != null) _sr.sprite = _sprBoss != null ? _sprBoss : _sprTracerDefault;
            speed = 13.5f;
            transform.localScale = new Vector3(1.35f, 1.30f, 1f);
            _sparkColor = new Color(1.0f, 0.25f, 0.1f);
            _baseSpriteColor = Color.white;
            SetupTrailRenderer(new Color(1f, 0.8f, 0.5f, 1f), new Color(1f, 0.2f, 0.1f, 0.8f), new Color(0.6f, 0.05f, 0.3f, 0f), 0.32f, 0.18f);
        }
        else if (isSniper)
        {
            if (_sr != null) _sr.sprite = _sprF22 != null ? _sprF22 : _sprTracerDefault;
            speed = 21.0f;
            transform.localScale = new Vector3(0.9f, 1.4f, 1f);
            _sparkColor = new Color(0.2f, 0.95f, 1f);
            _baseSpriteColor = Color.white;
            SetupTrailRenderer(Color.white, new Color(0.2f, 0.85f, 1f, 0.85f), new Color(0.05f, 0.2f, 0.9f, 0f), 0.18f, 0.12f);
        }
        else
        {
            if (_sr != null) _sr.sprite = _sprEnemy != null ? _sprEnemy : _sprTracerDefault;
            speed = 15.0f;
            transform.localScale = new Vector3(1.0f, 1.15f, 1f);
            Color baseCol = customColor ?? new Color(1.0f, 0.35f, 0.15f);
            _sparkColor = baseCol;
            _baseSpriteColor = Color.white;
            SetupTrailRenderer(Color.white, baseCol, new Color(baseCol.r * 0.5f, baseCol.g * 0.2f, 0f, 0f), 0.20f, 0.11f);
        }

        if (_rb != null)
        {
            _rb.linearVelocity = transform.up * speed;
        }
    }

    /// <summary>
    /// Özel mermi desenleri (Bullet Hell) için renk ve hızı uygular.
    /// </summary>
    public void ApplyCustomStyle(Color customColor, float customSpeed)
    {
        EnsureSpriteCache();
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null)
        {
            _sr.sprite = _sprTracerDefault;
            _sr.color = customColor;
        }
        speed = customSpeed;
        _baseSpriteColor = customColor;
        _sparkColor = customColor;

        SetupTrailRenderer(Color.white, customColor, new Color(customColor.r * 0.5f, customColor.g * 0.5f, customColor.b * 0.5f, 0f), 0.20f, 0.12f);

        if (_rb != null)
        {
            _rb.linearVelocity = transform.up * speed;
        }
    }

    private void SetupTrailRenderer(Color startCol, Color midCol, Color endCol, float width, float time)
    {
        if (_trailRenderer == null)
        {
            _trailRenderer = gameObject.GetComponent<TrailRenderer>();
            if (_trailRenderer == null)
            {
                _trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }
        }

        _trailRenderer.material = VFXManager.GetParticleMaterial();
        _trailRenderer.time = time;
        _trailRenderer.startWidth = width;
        _trailRenderer.endWidth = 0.02f;
        _trailRenderer.sortingOrder = 6; // Merminin hemen arkasında
        _trailRenderer.autodestruct = false;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(startCol, 0.0f),
                new GradientColorKey(midCol, 0.45f),
                new GradientColorKey(endCol, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(startCol.a, 0.0f),
                new GradientAlphaKey(midCol.a, 0.45f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        _trailRenderer.colorGradient = grad;
    }

    private static void EnsureSpriteCache()
    {
        if (_sprTracerDefault == null)
        {
            _sprF16 = Resources.Load<Sprite>("bullet_f16");
            _sprA10 = Resources.Load<Sprite>("bullet_a10");
            _sprF22 = Resources.Load<Sprite>("bullet_f22");
            _sprSu57 = Resources.Load<Sprite>("bullet_su57");
            _sprF35 = Resources.Load<Sprite>("bullet_f35");
            _sprKaan = Resources.Load<Sprite>("bullet_kaan");
            _sprEnemy = Resources.Load<Sprite>("bullet_enemy");
            _sprBoss = Resources.Load<Sprite>("bullet_boss");
            _sprTracerDefault = Resources.Load<Sprite>("bullet_tracer");

            if (_sprTracerDefault == null)
            {
                _sprTracerDefault = _sprF16;
            }
        }
    }

    private void Update()
    {
        _age += Time.deltaTime;

        // Uçuş Sırasında Dinamik Animasyon ve Efektler
        UpdateInFlightAnimation();

        // Güdümlü Füze Yönelimi (Varsa)
        if (homingTarget != null && homingTurnSpeed > 0f && _age < homingDuration && _rb != null)
        {
            Vector2 toTarget = ((Vector2)(homingTarget.position - transform.position)).normalized;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                float currentAngle = Mathf.Atan2(transform.up.y, transform.up.x) * Mathf.Rad2Deg;
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, homingTurnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, 0f, nextAngle - 90f);
                _rb.linearVelocity = transform.up * speed;
            }
        }

        // Ekran sınırları kontrolü
        if (Mathf.Abs(transform.position.y) > 6.5f || Mathf.Abs(transform.position.x) > 10.5f)
        {
            Destroy(gameObject);
            return;
        }

        CheckBulletGraze();
    }

    /// <summary>
    /// Merminin havadaki yanma titreşimini ve uçağa has mikro partiküllerini canlandırır.
    /// </summary>
    private void UpdateInFlightAnimation()
    {
        if (_sr != null && _baseSpriteColor != Color.clear)
        {
            // Gerçek piroteknik iz yakıtının yüksek frekanslı alev dalgalanması (45-60 Hz titreşim)
            float flicker = 0.90f + 0.18f * Mathf.Sin(Time.time * 52f + _animSeed);
            _sr.color = new Color(
                Mathf.Clamp01(_baseSpriteColor.r * flicker),
                Mathf.Clamp01(_baseSpriteColor.g * flicker),
                Mathf.Clamp01(_baseSpriteColor.b * flicker),
                _baseSpriteColor.a
            );
        }

        // A-10 Avenger: Havada ilerlerken arkasında akkor kor/duman kıvılcımları döker
        if (_isA10Heavy)
        {
            _emberTimer += Time.deltaTime;
            if (_emberTimer >= 0.055f)
            {
                _emberTimer = 0f;
                Vector3 sparkPos = transform.position - transform.up * 0.35f;
                VFXManager.SpawnHitSparks(sparkPos, new Color(1.0f, 0.40f, 0.12f));
            }
        }
    }

    private void CheckBulletGraze()
    {
        if (_hasGrazedPlayer || Time.timeScale <= 0.01f) return;

        if (owner != _checkedOwner)
        {
            _checkedOwner = owner;
            _ownerIsPlayer1 = (owner != null && owner.GetComponent<PlaneController>()?.isPlayer1 == true);
        }
        if (_ownerIsPlayer1) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsGameEnded || gm.CurrentMode == GameManager.GameMode.TwoPlayer) return;

        PlaneHealth p1 = gm.plane1Health;
        if (p1 == null || p1.IsDead) return;

        bool isDodging = PlayerCombatManeuvers.Instance != null && PlayerCombatManeuvers.Instance.isDodging;
        float grazeRadius = isDodging ? 1.55f : 1.28f;

        Vector2 toPlayer = (Vector2)(p1.transform.position - transform.position);
        float distSq = toPlayer.sqrMagnitude;
        if (distSq > grazeRadius * grazeRadius) return;

        Vector2 vel = (_rb != null && _rb.linearVelocity.sqrMagnitude > 0.01f)
            ? _rb.linearVelocity
            : (Vector2)transform.up * speed;

        if (isDodging || p1.isInvulnerable || Vector2.Dot(vel, toPlayer) <= 0f)
        {
            _hasGrazedPlayer = true;
            CombatComboManager.EnsureInstance().RegisterBulletGraze(transform.position, isDodging);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HitTarget(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HitTarget(collision.gameObject);
    }

    private void HitTarget(GameObject target)
    {
        if (target == owner) return;

        // Çevresel Hava Mayınına çarptı mı?
        SkyMineHazard mine = target.GetComponent<SkyMineHazard>();
        if (mine != null)
        {
            bool friendlyShot = (owner != null &&
                (owner.GetComponent<PlaneController>()?.isPlayer1 == true || owner.GetComponent<DroneCompanion>() != null));
            if (friendlyShot)
            {
                PlayerCombatManeuvers.Instance?.AddUltiEnergy(1.5f);
                CombatComboManager.Instance?.RefreshComboTimer(2.6f);
                mine.TakeDamage(damage, isCritical, transform.position);
                SpawnTargetHitSparks(transform.position);
                Destroy(gameObject);
            }
            return;
        }

        // Drona çarptı mı?
        DroneCompanion drone = target.GetComponent<DroneCompanion>();
        if (drone != null && drone.GetPlayer() != null && drone.GetPlayer().gameObject != owner)
        {
            SpawnTargetHitSparks(transform.position);
            DamageNumberManager.EnsureInstance()?.SpawnDamageNumber(transform.position, damage, isCritical, false);
            drone.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Sürü Düşman İHA'sına çarptı mı?
        SwarmEnemy swarm = target.GetComponent<SwarmEnemy>();
        if (swarm != null && swarm.gameObject != owner)
        {
            if (owner != null && owner.GetComponent<PlaneController>()?.isPlayer1 == true)
            {
                PlayerCombatManeuvers.Instance?.AddUltiEnergy(2.0f);
                CombatComboManager.Instance?.RefreshComboTimer(2.6f);
            }

            SpawnTargetHitSparks(transform.position);
            swarm.TakeDamage(damage, isCritical, transform.position);
            Destroy(gameObject);
            return;
        }

        PlaneHealth health = target.GetComponent<PlaneHealth>();
        if (health != null)
        {
            if (owner != null && owner.GetComponent<PlaneController>()?.isPlayer1 == true)
            {
                PlayerCombatManeuvers.Instance?.AddUltiEnergy(1.8f);
                CombatComboManager.Instance?.RefreshComboTimer(2.6f);
            }

            if (health == GameManager.Instance?.plane1Health && !health.isInvulnerable)
            {
                _hasGrazedPlayer = true;
            }

            SpawnTargetHitSparks(transform.position);

            // A-10 ağır zırh delici mermisi için ekstra vuruş mikro sarsıntısı
            if (_isA10Heavy)
            {
                CameraShake.Shake(0.08f, 0.05f);
            }

            health.TakeDamage(damage, isCritical, transform.position);
            Destroy(gameObject);
        }
    }

    private void SpawnTargetHitSparks(Vector3 pos)
    {
        Color effectCol = isCritical ? new Color(1.0f, 0.95f, 0.15f) : _sparkColor;
        VFXManager.SpawnHitSparks(pos, effectCol);

        // KAAN için çift katmanlı Turkuaz ve Altın zafer kıvılcımları
        if (aircraftStyle == HangarManager.AircraftType.KaanApex)
        {
            VFXManager.SpawnHitSparks(pos, new Color(1.0f, 0.85f, 0.20f));
        }
    }
}
