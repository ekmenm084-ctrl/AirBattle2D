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
        Instance = this;
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

        // 1. Klavye Kısayolları ile Kaçış (LeftShift, C veya Space)
        try
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Space))
            {
                TryDodge();
            }

            // 2. Klavye Kısayolları ile Ulti (E, R veya X)
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.X))
            {
                TryActivateUltimate();
            }
        }
        catch { }

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
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) rollDir = -1f;
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) rollDir = 1f;
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

    /// <summary>
    /// Düşmana mermi isabet ettiğinde, düşman öldüğünde veya sıyrılmada Ulti barına enerji ekler.
    /// </summary>
    public void AddUltiEnergy(float amount)
    {
        if (isOverdriveActive) return;

        amount *= Mathf.Max(0.1f, ultiChargeMultiplier);

        bool wasReady = IsUltiReady;
        currentUlti = Mathf.Clamp(currentUlti + amount, 0f, maxUlti);

        // %100'e ulaştığında görsel ve işitsel bildirim
        if (!wasReady && IsUltiReady)
        {
            AudioManager.Instance?.PlayRewardFanfare();
            CameraShake.Shake(0.2f, 0.3f);

            var gameUI = FindObjectOfType<GameUI>();
            gameUI?.ShowWaveAlert("[ULTİ] SÜPER YETENEK HAZIR! [E] / ULTİ BUTONU\n<color=#FFD54F>GÖKYÜZÜ HAKİMİYETİ İÇİN AKTİF ET!</color>", new Color(1f, 0.88f, 0.2f), 2.2f);
        }
    }

    /// <summary>
    /// Süper Yetenek / Ulti (Gökyüzü Hakimiyeti Overdrive) patlamasını tetikler.
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

    private IEnumerator UltimateOverdriveRoutine()
    {
        isOverdriveActive = true;
        overdriveRemaining = overdriveDuration;

        // ==============================================================
        // 1. FAZ: EMP ŞOK DALGASI (TÜM EKRANDAKİ DÜŞMAN MERMİLERİNİ TEMİZLE)
        // ==============================================================
        var bullets = FindObjectsOfType<Bullet>();
        int clearedCount = 0;
        foreach (var b in bullets)
        {
            if (b != null && b.owner != gameObject)
            {
                VFXManager.SpawnHitSparks(b.transform.position, new Color(0.2f, 0.85f, 1f));
                Destroy(b.gameObject);
                clearedCount++;
            }
        }

        // Tüm aktif düşman uçaklarına ve İHA sürü filolarına EMP şok hasarı (160 Hasar)
        var swarms = FindObjectsOfType<SwarmEnemy>();
        foreach (var s in swarms)
        {
            if (s != null)
            {
                s.TakeDamage(160f, true, s.transform.position);
            }
        }

        var planes = FindObjectsOfType<PlaneHealth>();
        foreach (var p in planes)
        {
            if (p != null && p.gameObject != gameObject)
            {
                VFXManager.SpawnHitSparks(p.transform.position, Color.yellow);
                p.TakeDamage(160f, true, p.transform.position);
            }
        }

        // Taktik Görsel & Ses Patlaması
        AudioManager.Instance?.PlayExplosion(isBoss: true);
        CameraShake.Shake(0.65f, 1.8f);

        // Sinematik Kısa Ağır Çekim Etkisi (0.28 saniye)
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(0.28f);
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert("[GÖKYÜZÜ HAKİMİYETİ DEVREDE!]\n<color=#00E5FF>EMP TÜM MERMİLERİ SİLDİ!</color> • <color=#FFD54F>4'LÜ SÜPER PLAZMA AŞIRI YÜKLEMESİ</color>", new Color(0f, 0.95f, 1f), 2.8f);

        // ==============================================================
        // 2. FAZ: 6 SANİYELİK HİPER AŞIRI YÜKLEME (HYPER OVERDRIVE)
        // ==============================================================
        float originalMoveSpeed = _controller != null ? _controller.moveSpeed : 7f;
        int originalBullets = _shooter != null ? _shooter.bulletsPerShot : 1;
        float originalFireRate = _shooter != null ? _shooter.fireRate : 0.28f;
        float originalDamage = _shooter != null ? _shooter.bulletDamage : 20f;

        if (_controller != null) _controller.moveSpeed *= 1.45f;
        if (_shooter != null)
        {
            _shooter.bulletsPerShot = 4;
            _shooter.fireRate = 0.085f; // Çok seri plazma ateşi
            _shooter.bulletDamage = originalDamage * 1.6f;
        }

        // Altın Plazma Aurası Oluştur
        CreateOverdriveAura();

        while (overdriveRemaining > 0f)
        {
            overdriveRemaining -= Time.deltaTime;

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

        DestroyOverdriveAura();

        isOverdriveActive = false;
        overdriveRemaining = 0f;
        _overdriveCoroutine = null;
    }

    private void CreateOverdriveAura()
    {
        if (_overdriveAuraObj != null) Destroy(_overdriveAuraObj);

        _overdriveAuraObj = new GameObject("OverdriveAura");
        _overdriveAuraObj.transform.SetParent(transform, false);
        _overdriveAuraObj.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = _overdriveAuraObj.AddComponent<SpriteRenderer>();
        if (_sr != null && _sr.sprite != null)
        {
            sr.sprite = _sr.sprite;
            sr.color = new Color(1f, 0.85f, 0.1f, 0.55f);
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

