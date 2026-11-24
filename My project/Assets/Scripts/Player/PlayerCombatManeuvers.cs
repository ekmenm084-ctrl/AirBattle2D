using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// İleri Düzey Savaş Mekanikleri Yöneticisi:
/// 1. Akrobatik Kaçış (Barrel Roll / Fıçı Tono & Dokunulmazlık Dash):
///    - Anlık yüksek hızlı manevra (I-Frames ile mermilerden sıyrılma).
///    - 360° fıçı tono rotasyonu ve rüzgar/hayalet iz efekti (Ghost Afterimage).
///    - Mermi içinden sıyrılmada "Mükemmel Kaçış" bonusu (+%6 Ulti enerjisi).
/// 
/// 2. Süper Yetenek / Ulti Barı (Sky Domination Overdrive):
///    - Mermi vurdukça, uçak düşürdükçe ve sıyrıldıkça dolan %0-100 Ulti Barı.
///    - %100 dolduğunda [E] tuşu veya Ulti Butonu ile aktifleşir.
///    - Tüm ekrandaki düşman mermilerini yok eden EMP Şok Dalgası + 160 Alan Hasarı.
///    - 6 Saniye boyunca 4'lü Süper Plazma Salvosu, +%45 Hız ve Altın Plazma Aurası!
/// </summary>
public class PlayerCombatManeuvers : MonoBehaviour
{
    public static PlayerCombatManeuvers Instance { get; private set; }

    [Header("Akrobatik Kaçış Ayarları")]
    public float dodgeCooldown = 3.0f;
    public float dodgeDuration = 0.42f;
    public float dodgeSpeed = 16.5f;

    [Header("Ulti / Süper Yetenek Ayarları")]
    public float maxUlti = 100f;
    public float currentUlti { get; private set; } = 0f;
    public float ultiChargeMultiplier = 1.0f;
    public float overdriveDuration = 6.0f;
    public float overdriveRemaining { get; private set; } = 0f;
    public bool isOverdriveActive { get; private set; } = false;

    public bool isDodging { get; private set; } = false;

    private float _lastDodgeTime = -999f;
    private PlaneController _controller;
    private PlaneHealth _health;
    private PlaneShooter _shooter;
    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private Vector3 _originalScale;
    private Coroutine _dodgeCoroutine;
    private Coroutine _overdriveCoroutine;
    private GameObject _overdriveAuraObj;

    public float DodgeCooldownRemaining => Mathf.Max(0f, (_lastDodgeTime + dodgeCooldown) - Time.time);
    public bool CanDodge => (DodgeCooldownRemaining <= 0f) && !isDodging && (_health == null || !_health.IsDead);
    public bool IsUltiReady => (currentUlti >= maxUlti) && !isOverdriveActive;

    private void Awake()
    {
        var pc = GetComponent<PlaneController>();
        if (pc == null || pc.isPlayer1)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        _controller = GetComponent<PlaneController>();
        _health = GetComponent<PlaneHealth>();
        _shooter = GetComponent<PlaneShooter>();
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();

        _originalScale = transform.localScale;
    }

    private void Update()
    {
        if (_health != null && _health.IsDead) return;
        if (Time.timeScale <= 0.01f) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameEnded) return;

        bool isP1 = (_controller == null || _controller.isPlayer1);
        bool isTwoPlayer = (GameSettings.SelectedMode == GameSettings.GameMode.Online) ||
                           (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);

        if (isP1)
        {
            // P1: Klavye Kısayolları ile Kaçış (LeftShift veya C — Space sadece ateş içindir)
            try
            {
                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.C))
                {
                    TryDodge();
                }

                // P1: Klavye Kısayolları ile Ulti (E veya R — Q/X füze içindir)
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.R))
                {
                    TryActivateUltimate();
                }
            }
            catch { }
        }
        else if (isTwoPlayer)
        {
            // P2: Klavye Kısayolları:
            // Kaçış: Yukarı Ok, Keypad 1, Sağ Shift, veya I
            // Ulti: Keypad 2, O, veya Sağ Alt
            try
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.I))
                {
                    TryDodge();
                }

                if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.O) || Input.GetKeyDown(KeyCode.RightAlt))
                {
                    TryActivateUltimate();
                }
            }
            catch { }
        }

        // Pasif Yavaş Ulti Dolumu (Savaşta olmasa dahi saniyede +0.75 enerji)
        if (!isOverdriveActive && currentUlti < maxUlti)
        {
            AddUltiEnergy(Time.deltaTime * 0.75f);
        }
    }

    /// <summary>
    /// Akrobatik Kaçış (Barrel Roll / Fıçı Tono) manevrasını başlatır.
    /// </summary>
    public bool TryDodge()
    {
        if (!CanDodge) return false;

        // Kaçış yönünü belirle:
        // Eğer oyuncu bir yöne yöneliyorsa o yöne, duruyorsa ekran merkezinden uzağa veya son hareket yönüne
        float rollDir = 0f;
        if (_controller != null)
        {
            Vector2 inDir = _controller.InputDir;
            if (Mathf.Abs(inDir.x) > 0.1f)
            {
                rollDir = Mathf.Sign(inDir.x);
            }
        }

        // Giriş yoksa klavye A/D veya Sol/Sağ kontrol et
        if (Mathf.Abs(rollDir) < 0.1f)
        {
            if (_controller != null && !_controller.isPlayer1)
            {
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Keypad4) || Input.GetKey(KeyCode.J)) rollDir = -1f;
                else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Keypad6) || Input.GetKey(KeyCode.L)) rollDir = 1f;
            }
            else
            {
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) rollDir = -1f;
                else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) rollDir = 1f;
            }
        }

        // Hâlâ sıfırsa ekran pozisyonuna göre ters duvara kaç
        if (Mathf.Abs(rollDir) < 0.1f)
        {
            rollDir = (transform.position.x > 0f) ? -1f : 1f;
        }

        if (_dodgeCoroutine != null) StopCoroutine(_dodgeCoroutine);
        _dodgeCoroutine = StartCoroutine(DodgeRollRoutine(rollDir));
        return true;
    }

    private IEnumerator DodgeRollRoutine(float dir)
    {
        isDodging = true;
        _lastDodgeTime = Time.time;

        if (_health != null)
            _health.isInvulnerable = true;

        // Jet Motoru İtki Sesi
        AudioManager.Instance?.PlayGunfire(isHeavy: true);

        // Ekran Sarsıntısı (Hafif çevik tepki)
        CameraShake.Shake(0.16f, 0.25f);

        float elapsed = 0f;
        float lastGhostTime = -999f;
        bool hadNearMiss = false;

        Quaternion baseRot = transform.rotation;
        Vector3 baseScale = _originalScale;

        while (elapsed < dodgeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dodgeDuration);

            // Yatay Atılma (Dash Displacement)
            float vx = dir * dodgeSpeed;
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(vx, 0f);
            }

            // Ekran sınırlarını koru
            if (_controller != null)
            {
                float clampedX = Mathf.Clamp(transform.position.x, _controller.minX, _controller.maxX);
                transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
            }

            // 360 Derece Fıçı Tono (Barrel Roll) Rotasyonu ve 3D Perspektif Derinliği
            float currentRollAngle = Mathf.Lerp(0f, -dir * 360f, t);
            transform.rotation = baseRot * Quaternion.Euler(0f, 0f, currentRollAngle);

            // Y ekseni sıkışma efekti (Jetin kendi ekseninde dönen 3D görüntüsü)
            float squash = 1f - Mathf.Sin(t * Mathf.PI) * 0.40f;
            transform.localScale = new Vector3(baseScale.x * Mathf.Max(0.2f, squash), baseScale.y, baseScale.z);

            // Hayalet İzi (Ghost Trail / Afterimage) Üret
            if (Time.time >= lastGhostTime + 0.07f)
            {
                lastGhostTime = Time.time;
                SpawnAfterimage(transform.position, transform.rotation, transform.localScale);
            }

            // Yakın Mermi Algılama ("Mükemmel Kaçış / Close Call"):
            if (!hadNearMiss)
            {
                var bullets = FindObjectsOfType<Bullet>();
                foreach (var b in bullets)
                {
                    if (b != null && b.owner != gameObject)
                    {
                        float dist = Vector2.Distance(transform.position, b.transform.position);
                        if (dist < 1.7f)
                        {
                            hadNearMiss = true;
                            AddUltiEnergy(6.0f);
                            DailyOpsManager.EnsureInstance()?.RecordEvent(DailyOpsManager.OpsEventType.PerfectDodge, 1);
                            VFXManager.SpawnHitSparks(transform.position, new Color(0.2f, 1f, 0.85f));

                            var gameUI = FindObjectOfType<GameUI>();
                            gameUI?.ShowWaveAlert("MÜKEMMEL SIYRILMA! (+%6 ULTİ)", new Color(0.2f, 1f, 0.8f), 1.2f);
                            break;
                        }
                    }
                }
            }

            yield return null;
        }

        // Bitiş ve normale dönüş
        transform.rotation = baseRot;
        transform.localScale = baseScale;

        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        if (_health != null) _health.isInvulnerable = false;

        isDodging = false;
        _dodgeCoroutine = null;
    }

    /// <summary>
    /// Kaçış anında uçağın arkasında sönümlenen neon hayalet izi oluşturur.
    /// </summary>
    private void SpawnAfterimage(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        if (_sr == null || _sr.sprite == null) return;

        GameObject ghost = new GameObject("DodgeAfterimage");
        ghost.transform.position = pos;
        ghost.transform.rotation = rot;
        ghost.transform.localScale = scale;

        SpriteRenderer gSr = ghost.AddComponent<SpriteRenderer>();
        gSr.sprite = _sr.sprite;
        gSr.color = new Color(0f, 0.85f, 1f, 0.65f);
        gSr.sortingLayerID = _sr.sortingLayerID;
        gSr.sortingOrder = _sr.sortingOrder - 1;

        StartCoroutine(FadeOutAndDestroyGhost(ghost, gSr, 0.28f));
    }

    private IEnumerator FadeOutAndDestroyGhost(GameObject ghost, SpriteRenderer gSr, float duration)
    {
        float elapsed = 0f;
        Color initialColor = gSr.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(initialColor.a, 0f, elapsed / duration);
            if (gSr != null) gSr.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
            yield return null;
        }

        if (ghost != null) Destroy(ghost);
    }

    public HangarManager.AircraftType GetResolvedAircraftType()
    {
        if (_shooter != null) return _shooter.aircraftType;
        if (HangarManager.Instance != null) return HangarManager.Instance.activeAircraft;
        return HangarManager.AircraftType.F16Viper;
    }

    public static void GetUltimateTitleAndSubtitle(HangarManager.AircraftType type, out string title, out string subtitle, out Color themeColor)
    {
        switch (type)
        {
            case HangarManager.AircraftType.A10TankBuster:
                title = "30MM CEHENNEM YAĞMURU & TİTANYUM ZIRH";
                subtitle = "+45 HP ONARIM • +60 KALKAN • 5 NAMLU URANYUM FIRTINASI";
                themeColor = new Color(1.0f, 0.65f, 0.15f);
                break;
            case HangarManager.AircraftType.F22Raptor:
                title = "HAYALET PUSU & SÜPERSONİK FÜZE SALVOSU";
                subtitle = "6.5SN TAM GÖRÜNMEZLİK (DOKUNULMAZLIK) • 4x AMRAAM FÜZESİ";
                themeColor = new Color(0.72f, 0.42f, 1.0f);
                break;
            case HangarManager.AircraftType.Su57Felon:
                title = "KOBRA PLAZMA FIRTINASI & VAMPİRİK SİFON";
                subtitle = "+35 HP & +45 KALKAN • 6x GÜDÜMLÜ PLAZMA FÜZESİ • 4'LÜ PLAZMA";
                themeColor = new Color(0.98f, 0.25f, 0.62f);
                break;
            case HangarManager.AircraftType.F35Lightning:
                title = "360° DAS YILDIRIM AĞI & İYON KUBBESİ";
                subtitle = "270 YILDIRIM HASARI • +85 KALKAN • 6x FÜZE & AKTİF LAZER KUBBESİ";
                themeColor = new Color(0.15f, 0.95f, 0.60f);
                break;
            case HangarManager.AircraftType.KaanApex:
                title = "MİLLİ GÖKBORA HÂKİMİYETİ (KAAN TF-X)";
                subtitle = "360 KUANTUM ŞOKU • +60 HP & +120 KALKAN • 8x GÖKDOĞAN FÜZESİ";
                themeColor = new Color(0.0f, 0.96f, 1.0f);
                break;
            case HangarManager.AircraftType.F16Viper:
            default:
                title = "ŞAHİN HÜCUMU (VIPER SALVO)";
                subtitle = "EMP MERMİ TEMİZLİĞİ • 2x AMRAAM FÜZESİ • 3 NAMLU SERİ VULCAN";
                themeColor = new Color(0.25f, 0.80f, 1.0f);
                break;
        }
    }

    /// <summary>
    /// Düşmana mermi isabet ettiğinde, düşman öldüğünde veya sıyrılmada Ulti barına enerji ekler.
    /// </summary>
    public void AddUltiEnergy(float amount)
    {
        if (isOverdriveActive) return;

        amount *= Mathf.Max(0.1f, ultiChargeMultiplier);

        bool wasReady = IsUltiReady;
        currentUlti = Mathf.Clamp(currentUlti + amount, 0f, maxUlti);

        // %100'e ulaştığında uçağa özel görsel ve işitsel bildirim
        if (!wasReady && IsUltiReady)
        {
            AudioManager.Instance?.PlayRewardFanfare();
            CameraShake.Shake(0.2f, 0.3f);

            GetUltimateTitleAndSubtitle(GetResolvedAircraftType(), out string ultiTitle, out _, out Color ultiColor);
            var gameUI = FindObjectOfType<GameUI>();
            gameUI?.ShowWaveAlert($"[ULTİ HAZIR] {ultiTitle}\n<color=#FFD54F>[E] / ULTİ BUTONU İLE AKTİF ET!</color>", ultiColor, 2.3f);
        }
    }

    /// <summary>
    /// Uçağa özel aktif Süper Yetenek / Ulti patlamasını tetikler.
    /// </summary>
    public bool TryActivateUltimate()
    {
        if (!IsUltiReady) return false;
        if (_health != null && _health.IsDead) return false;

        currentUlti = 0f;
        DailyOpsManager.EnsureInstance()?.RecordEvent(DailyOpsManager.OpsEventType.OverdriveActivated, 1);

        if (_overdriveCoroutine != null) StopCoroutine(_overdriveCoroutine);
        _overdriveCoroutine = StartCoroutine(UltimateOverdriveRoutine());
        return true;
    }

    private void ClearEnemyBullets(float radius, Color sparkColor)
    {
        var bullets = FindObjectsOfType<Bullet>();
        Vector3 myPos = transform.position;
        float rSq = radius * radius;
        foreach (var b in bullets)
        {
            if (b != null && b.owner != gameObject)
            {
                if (radius <= 0f || (b.transform.position - myPos).sqrMagnitude <= rSq)
                {
                    VFXManager.SpawnHitSparks(b.transform.position, sparkColor);
                    Destroy(b.gameObject);
                }
            }
        }
    }

    private void SpawnUltiMissileVolley(int count, float missileDamage, float totalSpreadDeg)
    {
        if (count <= 0) return;
        AudioManager.Instance?.PlayGunfire();

        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0.5f : (float)i / (count - 1);
            float angleDeg = Mathf.Lerp(-totalSpreadDeg * 0.5f, totalSpreadDeg * 0.5f, t);
            float xOffset = Mathf.Lerp(-0.55f, 0.55f, t);

            GameObject mObj = new GameObject($"UltiHomingMissile_{i}");
            mObj.transform.position = transform.position + transform.right * xOffset + transform.up * 0.35f;
            mObj.transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, angleDeg);

            HomingMissile hm = mObj.AddComponent<HomingMissile>();
            hm.owner = gameObject;
            hm.damage = missileDamage;
        }
    }

    private void GrantAircraftUltiSustainment(float healHull, float shieldAmount)
    {
        if (healHull > 0f && _health != null && !_health.IsDead)
        {
            _health.Heal(healHull);
        }

        if (shieldAmount > 0f && (_controller == null || _controller.isPlayer1))
        {
            PlayerUpgradeManager.Instance?.GrantShield(shieldAmount);
        }
    }

    private IEnumerator UltimateOverdriveRoutine()
    {
        HangarManager.AircraftType planeType = GetResolvedAircraftType();
        GetUltimateTitleAndSubtitle(planeType, out string ultiTitle, out string ultiSubtitle, out Color themeColor);

        // Uçağa özel Ulti süresi, alan hasarı, iyileştirme/kalkan ve silah çarpanları
        float customDuration = 6.0f;
        float areaBlastDamage = 160f;
        float duelBlastDamage = 30f;
        float healHull = 0f;
        float grantShield = 0f;
        int initialMissiles = 0;
        float missileDamage = 65f;
        float missileSpread = 32f;

        int targetBarrels = 4;
        float targetFireRate = 0.085f;
        float damageMultiplier = 1.65f;
        float speedMultiplier = 1.45f;
        float invulnerableDuration = 0f;
        bool stealthShimmer = false;
        bool periodicMissileWaves = false;
        float pointDefenseInterval = 0f;
        float pointDefenseRadius = 0f; // <=0 ise tam ekran, >0 ise kubbe yarıçapı
        bool delayedSecondMissileWave = false;

        switch (planeType)
        {
            case HangarManager.AircraftType.F16Viper:
                customDuration = 5.5f;
                areaBlastDamage = 150f;
                duelBlastDamage = 26f;
                initialMissiles = 2;
                missileDamage = 65f;
                missileSpread = 24f;
                targetBarrels = 3;
                targetFireRate = 0.09f;
                damageMultiplier = 1.55f;
                speedMultiplier = 1.40f;
                break;

            case HangarManager.AircraftType.A10TankBuster:
                customDuration = 6.5f;
                areaBlastDamage = 200f;
                duelBlastDamage = 34f;
                healHull = 45f;
                grantShield = 60f;
                targetBarrels = 5;
                targetFireRate = 0.085f;
                damageMultiplier = 2.10f;
                speedMultiplier = 1.25f;
                invulnerableDuration = 2.5f;
                break;

            case HangarManager.AircraftType.F22Raptor:
                customDuration = 6.5f;
                areaBlastDamage = 190f;
                duelBlastDamage = 32f;
                initialMissiles = 4;
                missileDamage = 75f;
                missileSpread = 42f;
                targetBarrels = 4;
                targetFireRate = 0.08f;
                damageMultiplier = 1.80f;
                speedMultiplier = 1.65f;
                invulnerableDuration = 6.5f; // Süre boyunca tam Hayalet Pusu dokunulmazlığı!
                stealthShimmer = true;
                break;

            case HangarManager.AircraftType.Su57Felon:
                customDuration = 6.5f;
                areaBlastDamage = 230f;
                duelBlastDamage = 38f;
                healHull = 35f;
                grantShield = 45f;
                initialMissiles = 2;
                missileDamage = 80f;
                missileSpread = 28f;
                targetBarrels = 4;
                targetFireRate = 0.075f;
                damageMultiplier = 1.95f;
                speedMultiplier = 1.50f;
                periodicMissileWaves = true; // Aktif fazda 2 ek füze dalgası (toplam 6 plazma füzesi)
                break;

            case HangarManager.AircraftType.F35Lightning:
                customDuration = 7.0f;
                areaBlastDamage = 270f;
                duelBlastDamage = 44f;
                grantShield = 85f;
                initialMissiles = 6;
                missileDamage = 82f;
                missileSpread = 65f;
                targetBarrels = 5;
                targetFireRate = 0.075f;
                damageMultiplier = 2.05f;
                speedMultiplier = 1.50f;
                pointDefenseInterval = 0.25f;
                pointDefenseRadius = 3.8f; // 360° DAS Yakın Hava Savunma Kubbesi
                break;

            case HangarManager.AircraftType.KaanApex:
                customDuration = 7.5f;
                areaBlastDamage = 360f;
                duelBlastDamage = 55f;
                healHull = 60f;
                grantShield = 120f;
                initialMissiles = 4;
                missileDamage = 90f;
                missileSpread = 52f;
                delayedSecondMissileWave = true; // 1.2sn sonra +4 GÖKDOĞAN füzesi daha (toplam 8 füze)
                targetBarrels = 5;
                targetFireRate = 0.068f;
                damageMultiplier = 2.35f;
                speedMultiplier = 1.60f;
                invulnerableDuration = 4.0f;
                pointDefenseInterval = 0.30f;
                pointDefenseRadius = 0f; // Tam ekran Kuantum Mermi Buharlaştırıcı
                break;
        }

        isOverdriveActive = true;
        overdriveDuration = customDuration;
        overdriveRemaining = customDuration;
        RadioChatterManager.Instance?.PlayOverdrive();

        // ==============================================================
        // 1. FAZ: UÇAĞA ÖZEL ANLIK ŞOK DALGASI, ONARIM/KALKAN & FÜZE SALVOSU
        // ==============================================================
        ClearEnemyBullets(0f, themeColor);

        var swarms = FindObjectsOfType<SwarmEnemy>();
        foreach (var s in swarms)
        {
            if (s != null)
            {
                VFXManager.SpawnHitSparks(s.transform.position, themeColor);
                s.TakeDamage(areaBlastDamage, true, s.transform.position);
            }
        }

        var planes = FindObjectsOfType<PlaneHealth>();
        bool isTwoPlayerDuel = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);
        float finalPlaneDamage = isTwoPlayerDuel ? duelBlastDamage : areaBlastDamage;

        foreach (var p in planes)
        {
            if (p != null && p.gameObject != gameObject)
            {
                VFXManager.SpawnHitSparks(p.transform.position, themeColor);
                p.TakeDamage(finalPlaneDamage, true, p.transform.position);
            }
        }

        // Uçağa özel Zırh Onarımı & Enerji Kalkanı yüklemesi
        GrantAircraftUltiSustainment(healHull, grantShield);

        // Uçağa özel Güdümlü Füze Salvosu
        if (initialMissiles > 0)
        {
            SpawnUltiMissileVolley(initialMissiles, missileDamage, missileSpread);
        }

        // Taktik Görsel & Ses Patlaması
        AudioManager.Instance?.PlayExplosion(isBoss: true);
        CameraShake.Shake(0.65f, 1.85f);

        // Sinematik Kısa Ağır Çekim Etkisi (0.26 saniye)
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(0.26f);
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        string hexColor = ColorUtility.ToHtmlStringRGB(themeColor);
        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert($"[{ultiTitle}]\n<color=#{hexColor}>{ultiSubtitle}</color>", themeColor, 2.9f);

        // ==============================================================
        // 2. FAZ: UÇAĞA ÖZEL AKTİF GÜÇLENDİRME FAZI
        // ==============================================================
        float originalMoveSpeed = _controller != null ? _controller.moveSpeed : 7f;
        int originalBullets = _shooter != null ? _shooter.bulletsPerShot : 1;
        float originalFireRate = _shooter != null ? _shooter.fireRate : 0.28f;
        float originalDamage = _shooter != null ? _shooter.bulletDamage : 20f;
        Color originalSrColor = _sr != null ? _sr.color : Color.white;

        if (_controller != null) _controller.moveSpeed = originalMoveSpeed * speedMultiplier;
        if (_shooter != null)
        {
            _shooter.bulletsPerShot = Mathf.Max(originalBullets, targetBarrels);
            _shooter.fireRate = Mathf.Min(originalFireRate, targetFireRate);
            _shooter.bulletDamage = originalDamage * damageMultiplier;
        }

        CreateOverdriveAura(themeColor);

        float elapsed = 0f;
        float nextMissileWaveTimer = 1.8f;
        int extraMissileWavesLeft = periodicMissileWaves ? 2 : 0;
        bool secondKaanWaveFired = false;
        float pointDefenseTimer = pointDefenseInterval;

        while (overdriveRemaining > 0f)
        {
            float dt = Time.deltaTime;
            overdriveRemaining -= dt;
            elapsed += dt;

            // Dokunulmazlık (A-10 ilk 2.5sn, KAAN ilk 4.0sn, F-22 tüm 6.5sn)
            if (_health != null && invulnerableDuration > 0f && !isDodging)
            {
                _health.isInvulnerable = (elapsed < invulnerableDuration);
            }

            // F-22 Raptor Hayalet Pusu yarı saydam kamuflaj dalgalanması
            if (stealthShimmer && _sr != null)
            {
                float alpha = 0.42f + Mathf.Sin(Time.time * 16f) * 0.16f;
                _sr.color = new Color(0.78f, 0.88f, 1.0f, alpha);
            }

            // Su-57 Felon periyodik Kobra Plazma Füzesi dalgaları
            if (extraMissileWavesLeft > 0 && elapsed >= nextMissileWaveTimer)
            {
                extraMissileWavesLeft--;
                nextMissileWaveTimer += 1.8f;
                SpawnUltiMissileVolley(2, missileDamage, 30f);
            }

            // KAAN TF-X 2. GÖKDOĞAN füze dalgası (4 füze daha -> toplam 8 füze)
            if (delayedSecondMissileWave && !secondKaanWaveFired && elapsed >= 1.2f)
            {
                secondKaanWaveFired = true;
                SpawnUltiMissileVolley(4, missileDamage, 64f);
            }

            // F-35 DAS Lazer Kubbesi & KAAN Kuantum Mermi Buharlaştırıcı
            if (pointDefenseInterval > 0f)
            {
                pointDefenseTimer -= dt;
                if (pointDefenseTimer <= 0f)
                {
                    pointDefenseTimer = pointDefenseInterval;
                    ClearEnemyBullets(pointDefenseRadius, themeColor);
                }
            }

            if (_overdriveAuraObj != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.12f;
                _overdriveAuraObj.transform.localScale = Vector3.one * 1.55f * pulse;
            }

            yield return null;
        }

        // ==============================================================
        // 3. FAZ: NORMALE DÖNÜŞ VE SİSTEMLERİN SOĞUMASI
        // ==============================================================
        if (_controller != null) _controller.moveSpeed = originalMoveSpeed;
        if (_shooter != null)
        {
            _shooter.bulletsPerShot = originalBullets;
            _shooter.fireRate = originalFireRate;
            _shooter.bulletDamage = originalDamage;
        }
        if (_health != null && !isDodging)
        {
            _health.isInvulnerable = false;
        }
        if (_sr != null && stealthShimmer)
        {
            _sr.color = originalSrColor;
        }

        DestroyOverdriveAura();

        isOverdriveActive = false;
        overdriveRemaining = 0f;
        _overdriveCoroutine = null;
    }

    private void CreateOverdriveAura(Color auraColor)
    {
        if (_overdriveAuraObj != null) Destroy(_overdriveAuraObj);

        _overdriveAuraObj = new GameObject("OverdriveAura");
        _overdriveAuraObj.transform.SetParent(transform, false);
        _overdriveAuraObj.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = _overdriveAuraObj.AddComponent<SpriteRenderer>();
        if (_sr != null && _sr.sprite != null)
        {
            sr.sprite = _sr.sprite;
            sr.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0.58f);
            sr.sortingLayerID = _sr.sortingLayerID;
            sr.sortingOrder = _sr.sortingOrder - 1;
        }
    }

    private void DestroyOverdriveAura()
    {
        if (_overdriveAuraObj != null)
        {
            Destroy(_overdriveAuraObj);
            _overdriveAuraObj = null;
        }
    }

    private void OnDisable()
    {
        DestroyOverdriveAura();
        isDodging = false;
        isOverdriveActive = false;
    }

    private void OnDestroy()
    {
        DestroyOverdriveAura();
    }
}

