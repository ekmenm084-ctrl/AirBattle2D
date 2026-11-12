using System.Collections;
using UnityEngine;

/// <summary>
/// Yatay düzlemde savaşan Yapay Zeka (AI).
/// - 20 Uçaklık İlerleme Seferinde dalga numarasına göre zorluğu ve düşman arketipini (Avcı, Keskin Nişancı, Bombacı, Elit As) ayarlar.
/// - Boss uçaklarında BossPatternController ile özel saldırı desenlerini ve Faz 2'yi yönetir.
/// </summary>
[RequireComponent(typeof(PlaneController))]
public class AIController : MonoBehaviour
{
    public enum EnemyArchetype
    {
        Interceptor, // Çevik it dalaşı avcısı
        Sniper,      // Lazer nişangahlı yüksek hızlı keskin nişancı
        Bomber,      // 3'lü yelpaze salvosu + hafif kalkanlı ağır uçak
        EliteAce     // Boss öncesi çevik elit muhafız + enerji kalkanı
    }

    [Header("Hedef")]
    public Transform target;

    [Header("Ateş & Nişan")]
    public float fireInterval = 0.8f;
    public float aimToleranceX = 0.8f;

    [Header("Yapay Zeka Karakteri")]
    [Range(0f, 1f)]
    public float dodgeChance = 0.35f;
    public float reactionSpeed = 0.15f;

    public EnemyArchetype currentArchetype { get; private set; } = EnemyArchetype.Interceptor;

    private PlaneController _controller;
    private PlaneShooter _shooter;
    private PlaneHealth _health;
    private SpriteRenderer _sr;
    private PlaneVisualEffects _visuals;
    private BossPatternController _bossController;
    private LineRenderer _sniperSightLine;

    private bool _isBossWave = false;
    private float _nextDecisionTime;
    private float _nextFireTime;
    private float _nextSpecialTime;
    private float _currentMoveX;
    private bool _isDodging = false;
    private float _dodgeEndTime;
    private Coroutine _archetypeCoroutine;

    private void Awake()
    {
        _controller = GetComponent<PlaneController>();
        _shooter = GetComponent<PlaneShooter>();
        _health = GetComponent<PlaneHealth>();
        _sr = GetComponent<SpriteRenderer>();
        _visuals = GetComponent<PlaneVisualEffects>();
    }

    private void Start()
    {
        GameSettings.LoadSettings();
        ApplyDifficultySettings();
        FindTargetIfNull();
    }

    /// <summary>
    /// Menüden seçilen Kolay / Normal / Zor ayarına göre AI parametrelerini ayarlar.
    /// </summary>
    public void ApplyDifficultySettings()
    {
        switch (GameSettings.SelectedDifficulty)
        {
            case GameSettings.Difficulty.Kolay:
                reactionSpeed = 0.35f;
                fireInterval = 1.15f;
                aimToleranceX = 0.9f;
                dodgeChance = 0.15f;
                if (_controller != null) _controller.moveSpeed = 4.2f;
                if (_shooter != null) _shooter.bulletDamage = 14f;
                break;

            case GameSettings.Difficulty.Normal:
                reactionSpeed = 0.15f;
                fireInterval = 0.65f;
                aimToleranceX = 1.4f;
                dodgeChance = 0.35f;
                if (_controller != null) _controller.moveSpeed = 6.2f;
                if (_shooter != null) _shooter.bulletDamage = 20f;
                break;

            case GameSettings.Difficulty.Zor:
                reactionSpeed = 0.05f; // Anlık refleksler
                fireInterval = 0.30f;  // Seri ve baskı kuran acımasız ateş
                aimToleranceX = 2.0f;  // Geniş nişan toleransı (ekrandan kaçışı zorlaştırır)
                dodgeChance = 0.70f;   // Çok çevik ve agresif kaçış manevrası
                if (_controller != null) _controller.moveSpeed = 9.2f; // Yüksek takip hızı
                if (_shooter != null) _shooter.bulletDamage = 28f;     // Ezici mermi hasarı
                break;
        }
    }

    public static EnemyArchetype GetStandardArchetype(int wave)
    {
        if (wave > 0 && wave % 5 == 4) return EnemyArchetype.EliteAce;
        int mod = Mathf.Max(0, wave - 1) % 3;
        switch (mod)
        {
            case 1: return EnemyArchetype.Sniper;
            case 2: return EnemyArchetype.Bomber;
            default: return EnemyArchetype.Interceptor;
        }
    }

    public static string GetArchetypeTag(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Sniper: return "NİŞANCI";
            case EnemyArchetype.Bomber: return "BOMBACI";
            case EnemyArchetype.EliteAce: return "ELİT AS";
            default: return "AVCI";
        }
    }

    private void EnsureSniperLine()
    {
        if (_sniperSightLine != null) return;
        GameObject obj = new GameObject("SniperSightLine");
        obj.transform.SetParent(transform, false);
        _sniperSightLine = obj.AddComponent<LineRenderer>();
        _sniperSightLine.useWorldSpace = true;
        _sniperSightLine.positionCount = 2;
        _sniperSightLine.sortingOrder = 14;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) _sniperSightLine.material = new Material(shader);
        _sniperSightLine.enabled = false;
    }

    private void StopArchetypeRoutine()
    {
        if (_archetypeCoroutine != null)
        {
            StopCoroutine(_archetypeCoroutine);
            _archetypeCoroutine = null;
        }
        if (_sniperSightLine != null)
            _sniperSightLine.enabled = false;
    }

    private void OnDisable()
    {
        StopArchetypeRoutine();
    }

    /// <summary>
    /// 20 Uçaklık İlerleme Modunda dalga numarasına VE seçilen zorluğa (Kolay/Normal/Zor)
    /// göre zorluk parametrelerini dinamik olarak dengeler.
    /// </summary>
    public void ConfigureWave(int wave, bool isBoss)
    {
        if (_controller == null) _controller = GetComponent<PlaneController>();
        if (_shooter == null) _shooter = GetComponent<PlaneShooter>();
        if (_health == null) _health = GetComponent<PlaneHealth>();
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_visuals == null) _visuals = GetComponent<PlaneVisualEffects>();
        if (_bossController == null) _bossController = GetComponent<BossPatternController>();
        if (_bossController == null) _bossController = gameObject.AddComponent<BossPatternController>();

        StopArchetypeRoutine();
        _isBossWave = isBoss;

        float progress = Mathf.Clamp01((wave - 1f) / 19f); // 0 (1. uçak) -> 1 (20. uçak)

        // 1. ZORLUK MODU ÇARPANLARI (Kolay, Normal, Zor)
        float speedMod = 1f;
        float fireRateMod = 1f;
        float reactionMod = 1f;
        float dodgeMod = 1f;
        float damageMod = 1f;

        switch (GameSettings.SelectedDifficulty)
        {
            case GameSettings.Difficulty.Kolay:
                speedMod = 0.85f;
                fireRateMod = 1.25f;
                reactionMod = 1.30f;
                dodgeMod = 0.40f;
                damageMod = 0.85f;
                break;

            case GameSettings.Difficulty.Normal:
                speedMod = 1.05f;
                fireRateMod = 0.90f;
                reactionMod = 0.90f;
                dodgeMod = 1.10f;
                damageMod = 1.15f;
                break;

            case GameSettings.Difficulty.Zor:
                speedMod = 1.35f;     // %35 daha hızlı ve çevik hareket
                fireRateMod = 0.50f;  // %50 daha seri ve acımasız ateş (aralık yarıya iner!)
                reactionMod = 0.45f;  // %55 daha keskin tepki
                dodgeMod = 1.85f;     // Çok yüksek kaçış ve manevra
                damageMod = 1.55f;    // %55 daha fazla mermi hasarı
                break;
        }

        // 2. HARDCORE MODU ÖZEL BOSS ÖLÇEKLENDİRMESİ
        if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Hardcore)
        {
            // Her gelen düşman uçağı bir öncekinden daha çevik, seri ve güçlü bir BOSS!
            float hcProgress = Mathf.Clamp01((wave - 1f) / 10f); // 1. Boss'tan 10. Boss'a doğru maksimuma tırmanış
            reactionSpeed = Mathf.Lerp(0.14f, 0.04f, hcProgress) * reactionMod;
            fireInterval = Mathf.Lerp(0.65f, 0.28f, hcProgress) * fireRateMod;
            aimToleranceX = Mathf.Lerp(1.20f, 2.20f, hcProgress);
            dodgeChance = Mathf.Lerp(0.35f, 0.75f, hcProgress) * dodgeMod;

            if (_controller != null)
                _controller.moveSpeed = Mathf.Lerp(6.5f, 9.5f, hcProgress) * speedMod;

            if (_shooter != null)
            {
                _shooter.fireRate = fireInterval;
                // Boss 1: 2 Namlu, Boss 2-4: 3 Namlu, Boss 5+: 4 Namlu!
                _shooter.bulletsPerShot = (wave >= 5) ? 4 : (wave >= 2 ? 3 : 2);
                _shooter.bulletSpread = 0.42f;
                _shooter.bulletDamage = (18f + Mathf.Min(wave * 3.0f, 30f)) * damageMod;
            }

            Color hcTint = _sr != null ? _sr.color : Color.white;
            _visuals?.SetBaseColor(hcTint);
            _bossController?.ConfigureBoss(wave, hcTint);
            return;
        }

        // 3. BOSS VEYA STANDART UÇAK PARAMETRELERİ
        bool isZor = (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Zor);

        if (isBoss)
        {
            // Bosslar: Zor modda ve ilerleyen dalgalarda gerçek bir mermi cehennemi
            reactionSpeed = Mathf.Lerp(0.18f, 0.06f, progress) * reactionMod;
            fireInterval = Mathf.Lerp(0.75f, 0.35f, progress) * fireRateMod;
            aimToleranceX = Mathf.Lerp(1.20f, 2.20f, progress);
            dodgeChance = Mathf.Lerp(0.30f, 0.60f, progress) * dodgeMod;

            if (_controller != null)
                _controller.moveSpeed = Mathf.Lerp(5.5f, 8.5f, progress) * speedMod;

            if (_shooter != null)
            {
                _shooter.fireRate = fireInterval;
                // Zor modda Boss 1 dahil en az 3'lü atar! İleri dalgalarda veya Zor modda 4'lü salvo!
                if (wave >= 15 || (isZor && wave >= 10))
                    _shooter.bulletsPerShot = 4;
                else if (isZor || wave >= 10)
                    _shooter.bulletsPerShot = 3;
                else
                    _shooter.bulletsPerShot = 2;

                _shooter.bulletSpread = 0.42f;
                float bossBaseDmg = 18f + Mathf.Min(wave * 1.0f, 22f);
                _shooter.bulletDamage = bossBaseDmg * damageMod;
            }

            Color bossTint = _sr != null ? _sr.color : Color.white;
            _visuals?.SetBaseColor(bossTint);
            _bossController?.ConfigureBoss(wave, bossTint);
        }
        else
        {
            _bossController?.ResetPatterns();
            currentArchetype = GetStandardArchetype(wave);

            // Standart Uçaklar (Arketipe göre özelleştirilmiş davranış)
            reactionSpeed = Mathf.Lerp(0.28f, 0.07f, progress) * reactionMod;
            fireInterval = Mathf.Lerp(1.05f, 0.40f, progress) * fireRateMod;
            aimToleranceX = Mathf.Lerp(0.90f, 1.80f, progress);
            dodgeChance = Mathf.Lerp(0.15f, 0.45f, progress) * dodgeMod;

            float moveSpeed = Mathf.Lerp(4.5f, 7.8f, progress) * speedMod;
            Color enemyTint = Color.white;

            switch (currentArchetype)
            {
                case EnemyArchetype.Sniper:
                    moveSpeed *= 0.88f;
                    fireInterval *= 1.18f;
                    enemyTint = new Color(0.62f, 0.92f, 1f);
                    EnsureSniperLine();
                    break;

                case EnemyArchetype.Bomber:
                    moveSpeed *= 0.78f;
                    fireInterval *= 1.25f;
                    dodgeChance *= 0.55f;
                    enemyTint = new Color(1f, 0.74f, 0.48f);
                    if (_health != null) _health.SetEnemyShield((16f + wave * 2.2f) * (isZor ? 1.3f : 1f));
                    break;

                case EnemyArchetype.EliteAce:
                    moveSpeed *= 1.14f;
                    fireInterval *= 0.88f;
                    dodgeChance = Mathf.Min(0.85f, dodgeChance * 1.35f);
                    enemyTint = new Color(0.88f, 0.66f, 1f);
                    if (_health != null) _health.SetEnemyShield((28f + wave * 3.0f) * (isZor ? 1.3f : 1f));
                    break;

                default: // Interceptor
                    moveSpeed *= 1.06f;
                    enemyTint = new Color(1f, 0.94f, 0.94f);
                    break;
            }

            if (_controller != null)
                _controller.moveSpeed = moveSpeed;

            _visuals?.SetBaseColor(enemyTint);

            if (_shooter != null)
            {
                _shooter.fireRate = fireInterval;
                if (isZor && wave >= 11)
                    _shooter.bulletsPerShot = 3;
                else if ((isZor && wave >= 2) || wave >= 7 || currentArchetype == EnemyArchetype.EliteAce)
                    _shooter.bulletsPerShot = 2;
                else
                    _shooter.bulletsPerShot = 1;

                _shooter.bulletSpread = 0.32f;
                float planeBaseDmg = 16f + Mathf.Min(wave * 0.65f, 14f);
                _shooter.bulletDamage = planeBaseDmg * damageMod;
            }

            _nextSpecialTime = Time.time + Random.Range(2.0f, 3.0f);
        }
    }

    private void FindTargetIfNull()
    {
        if (target != null) return;
        var planes = FindObjectsOfType<PlaneController>();
        foreach (var p in planes)
        {
            if (p != null && p.isPlayer1 && p.gameObject != gameObject)
            {
                target = p.transform;
                break;
            }
        }
    }

    private void Update()
    {
        if (_health != null && _health.IsDead) return;

        if (target == null)
        {
            FindTargetIfNull();
            if (target == null) return;
        }

        if (Time.time >= _nextDecisionTime)
        {
            _nextDecisionTime = Time.time + reactionSpeed;
            DecideMovement();
        }

        _controller.SetAIInput(new Vector2(_currentMoveX, 0f));

        float diffX = Mathf.Abs(target.position.x - transform.position.x);
        if (diffX <= aimToleranceX && Time.time >= _nextFireTime && _archetypeCoroutine == null)
        {
            _nextFireTime = Time.time + fireInterval;
            _shooter?.TryFire();
        }

        // Standart düşman arketip özel saldırıları (Sniper, Bomber, EliteAce)
        if (!_isBossWave && _shooter != null && _shooter.enabled && _archetypeCoroutine == null && Time.time >= _nextSpecialTime)
        {
            TriggerStandardArchetypeSpecial();
        }
    }

    private void TriggerStandardArchetypeSpecial()
    {
        if (currentArchetype == EnemyArchetype.Sniper)
        {
            _archetypeCoroutine = StartCoroutine(SniperAimedShotRoutine());
            _nextSpecialTime = Time.time + 3.4f;
        }
        else if (currentArchetype == EnemyArchetype.Bomber)
        {
            FireBomberSpread();
            _nextSpecialTime = Time.time + 2.6f;
        }
        else if (currentArchetype == EnemyArchetype.EliteAce)
        {
            FireEliteAimedBurst();
            _nextSpecialTime = Time.time + 2.4f;
        }
        else
        {
            _nextSpecialTime = Time.time + 99f;
        }
    }

    private IEnumerator SniperAimedShotRoutine()
    {
        EnsureSniperLine();
        Vector2 lockedDir = target != null
            ? ((Vector2)(target.position - transform.position)).normalized
            : Vector2.down;
        if (lockedDir.y > -0.25f) lockedDir = new Vector2(lockedDir.x, -0.7f).normalized;

        float lockTime = 0.62f;
        float elapsed = 0f;
        _sniperSightLine.enabled = true;

        while (elapsed < lockTime)
        {
            if (_health == null || _health.IsDead || !enabled)
            {
                _sniperSightLine.enabled = false;
                _archetypeCoroutine = null;
                yield break;
            }

            Vector3 origin = transform.position + Vector3.down * 0.4f;
            _sniperSightLine.SetPosition(0, origin);
            _sniperSightLine.SetPosition(1, origin + (Vector3)lockedDir * 12f);

            float flash = Mathf.PingPong(Time.time * 10f, 1f);
            _sniperSightLine.startWidth = 0.035f;
            _sniperSightLine.endWidth = 0.035f;
            Color c = Color.Lerp(new Color(0.2f, 0.9f, 1f, 0.55f), new Color(1f, 0.3f, 0.3f, 0.9f), flash);
            _sniperSightLine.startColor = c;
            _sniperSightLine.endColor = c;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _sniperSightLine.enabled = false;

        if (_health != null && !_health.IsDead && _shooter != null)
        {
            GameObject shot = _shooter.SpawnCustomBullet(transform.position + Vector3.down * 0.45f, lockedDir, 13.8f, _shooter.bulletDamage * 1.4f, new Color(0.35f, 0.95f, 1f));
            if (shot != null) shot.transform.localScale = Vector3.one * 1.15f;
            AudioManager.Instance?.PlayGunfire(true);
        }

        _archetypeCoroutine = null;
    }

    private void FireBomberSpread()
    {
        if (_shooter == null) return;
        float[] angles = new float[] { -110f, -90f, -70f };
        for (int i = 0; i < angles.Length; i++)
        {
            float rad = angles[i] * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _shooter.SpawnCustomBullet(transform.position + Vector3.down * 0.45f, dir, 7.6f, _shooter.bulletDamage * 0.9f, new Color(1f, 0.62f, 0.15f));
        }
        AudioManager.Instance?.PlayGunfire(true);
    }

    private void FireEliteAimedBurst()
    {
        if (_shooter == null || target == null) return;
        Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
        if (dir.y > -0.25f) dir = Vector2.down;

        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float[] offsets = new float[] { -8f, 8f };
        for (int i = 0; i < offsets.Length; i++)
        {
            float rad = (baseAngle + offsets[i]) * Mathf.Deg2Rad;
            Vector2 burstDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _shooter.SpawnCustomBullet(transform.position + Vector3.down * 0.45f, burstDir, 10.5f, _shooter.bulletDamage * 0.95f, new Color(0.88f, 0.45f, 1f));
        }
        AudioManager.Instance?.PlayGunfire(false);
    }

    private void DecideMovement()
    {
        if (target == null) return;

        if (_isDodging)
        {
            if (Time.time >= _dodgeEndTime)
                _isDodging = false;
            else
                return;
        }

        if (Random.value < dodgeChance * 0.18f)
        {
            _isDodging = true;
            _dodgeEndTime = Time.time + Random.Range(0.35f, 0.75f);
            _currentMoveX = Random.value > 0.5f ? 1f : -1f;
            return;
        }

        float diffX = target.position.x - transform.position.x;
        if (Mathf.Abs(diffX) > 0.18f)
        {
            _currentMoveX = Mathf.Sign(diffX);
        }
        else
        {
            _currentMoveX = 0f;
        }
    }
}
