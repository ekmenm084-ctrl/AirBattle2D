using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gelişmiş Üst Seviye Boss Saldırı ve Apex Doktrin Denetleyicisi:
/// - 6 Benzersiz Boss Arketipi: DemirKanat, FırtınaDokuyucu, KızılLazer, FüzeKomutanı, GölgeAvcı, KıyametKatı.
/// - Çok Fazlı Savaş Sistemi: Faz 1 (Standart), Faz 2 (%50 Can - Aşırı Yük), Faz 3 (%25 Can - APEX KIYAMET PROTOKOLÜ).
/// - Üst seviyelerde (Wave 10+, 15+, 20+) ve Zor Modda:
///   * Çift Lazer Işınları (Dual Sweep Beam)
///   * Taktik Hava Mayınları (Sky Mine Denial)
///   * Görünmezlik / Süpersonik Gölge Hücumu (Stealth Cloak)
///   * Çift Karşıt Mermi Burguları (Double Helix Bullet Hell)
///   * Reaktif Kalkan Yenileme ve Eskort İHA Filoları
/// </summary>
public class BossPatternController : MonoBehaviour
{
    public enum BossArchetype
    {
        DemirKanat,      // Geniş yelpaze salvo + kinetik kalkan
        FirtinaDokuyucu, // Çift karşıt spiral mermi burguları
        KizilLazer,      // Nişan uyarı çizgili termal süpürme lazeri
        FuzeKomutani,    // Güdümlü füze salvosu + eskort İHA'lar + mayınlar
        GolgeAvci,       // Gizlenme (Cloak) + ani süpersonik hücum + iğne salvosu
        KiyametKati      // Çift kesişen V-lazer + salkım kıyamet salvosu (Final/Titan)
    }

    private PlaneHealth _health;
    private PlaneShooter _shooter;
    private AIController _ai;
    private PlaneVisualEffects _visuals;
    private SpriteRenderer _sr;

    private LineRenderer _laserLine;
    private LineRenderer _laserLine2;

    private bool _isBossActive = false;
    private bool _phase2Triggered = false;
    private bool _phase3Triggered = false;
    private int _waveIndex = 1;
    private BossArchetype _archetype = BossArchetype.DemirKanat;
    private Color _baseBossTint = Color.white;

    private float _nextSpecialTime = 0f;
    private float _spiralAngleOffset = 0f;
    private float _mineCooldown = 0f;
    private Coroutine _attackCoroutine;

    public bool IsPhase2 => _phase2Triggered;
    public bool IsPhase3 => _phase3Triggered;
    public BossArchetype CurrentArchetype => _archetype;

    private void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _shooter = GetComponent<PlaneShooter>();
        _ai = GetComponent<AIController>();
        _visuals = GetComponent<PlaneVisualEffects>();
        _sr = GetComponent<SpriteRenderer>();
        EnsureLaserRenderers();
    }

    private void EnsureLaserRenderers()
    {
        if (_laserLine == null)
        {
            GameObject lineObj1 = new GameObject("BossThermalLaser1");
            lineObj1.transform.SetParent(transform, false);
            _laserLine = SetupLineRenderer(lineObj1);
        }

        if (_laserLine2 == null)
        {
            GameObject lineObj2 = new GameObject("BossThermalLaser2");
            lineObj2.transform.SetParent(transform, false);
            _laserLine2 = SetupLineRenderer(lineObj2);
        }
    }

    private LineRenderer SetupLineRenderer(GameObject lineObj)
    {
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.sortingOrder = 18;
        lr.numCapVertices = 4;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            lr.material = new Material(shader);

        lr.enabled = false;
        return lr;
    }

    public static BossArchetype GetBossArchetype(int waveIndex)
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentMode == GameManager.GameMode.Hardcore ||
                GameManager.Instance.CurrentMode == GameManager.GameMode.BossRush)
            {
                return (BossArchetype)(Mathf.Max(0, waveIndex - 1) % 6);
            }
        }

        // Final Wave 20 veya Sonsuz Mod'da her 20 dalga: KIYAMET KATI TİTAN
        if (waveIndex > 0 && waveIndex % 20 == 0)
        {
            return BossArchetype.KiyametKati;
        }

        // Wave 15 veya 15'in katları: GÖLGE AVCI
        if (waveIndex > 0 && waveIndex % 15 == 0)
        {
            return BossArchetype.GolgeAvci;
        }

        int bossTier = Mathf.Max(0, (waveIndex / 5) - 1);
        return (BossArchetype)(bossTier % 6);
    }

    public static string GetArchetypeLabel(BossArchetype archetype)
    {
        switch (archetype)
        {
            case BossArchetype.DemirKanat: return "DEMİR KANAT";
            case BossArchetype.FirtinaDokuyucu: return "FIRTINA DOKUYUCU";
            case BossArchetype.KizilLazer: return "KIZIL LAZER";
            case BossArchetype.FuzeKomutani: return "FÜZE KOMUTANI";
            case BossArchetype.GolgeAvci: return "GÖLGE AVCI";
            case BossArchetype.KiyametKati: return "KIYAMET KATI TİTAN";
            default: return "KOMUTA BOSS";
        }
    }

    public void ConfigureBoss(int waveIndex, Color baseTint)
    {
        if (_health == null) _health = GetComponent<PlaneHealth>();
        if (_shooter == null) _shooter = GetComponent<PlaneShooter>();
        if (_ai == null) _ai = GetComponent<AIController>();
        if (_visuals == null) _visuals = GetComponent<PlaneVisualEffects>();
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        EnsureLaserRenderers();

        ResetPatterns();

        _waveIndex = waveIndex;
        _archetype = GetBossArchetype(waveIndex);
        _baseBossTint = baseTint;
        _isBossActive = true;
        _phase2Triggered = false;
        _phase3Triggered = false;
        _nextSpecialTime = Time.time + 1.8f;
        _mineCooldown = Time.time + 5.0f;

        // Zorluğa ve arketipe göre başlangıç kalkanı
        if (_health != null)
        {
            bool isZor = (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Zor);
            float diffMul = isZor ? 1.45f : (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Kolay ? 0.65f : 1.0f);
            float archMul = (_archetype == BossArchetype.DemirKanat || _archetype == BossArchetype.KiyametKati) ? 1.60f : 1.0f;
            float waveBonus = Mathf.Min(waveIndex * 8f, 180f);
            float shield = (50f + waveBonus) * diffMul * archMul;
            _health.SetEnemyShield(shield);
        }
    }

    public void ResetPatterns()
    {
        _isBossActive = false;
        _phase2Triggered = false;
        _phase3Triggered = false;

        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        if (_laserLine != null) _laserLine.enabled = false;
        if (_laserLine2 != null) _laserLine2.enabled = false;

        if (_sr != null)
        {
            Color c = _sr.color;
            c.a = 1f;
            _sr.color = c;
        }
    }

    private void OnDisable()
    {
        ResetPatterns();
    }

    private void Update()
    {
        if (!_isBossActive || _health == null || _health.IsDead)
        {
            if (_laserLine != null && _laserLine.enabled) _laserLine.enabled = false;
            if (_laserLine2 != null && _laserLine2.enabled) _laserLine2.enabled = false;
            return;
        }

        if (_ai == null || !_ai.enabled || _shooter == null || !_shooter.enabled)
            return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.BulletHell)
            return;

        float hpPct = _health.HealthPercent;
        bool isZor = (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Zor);
        bool isHardcore = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Hardcore);
        bool isHighWave = (_waveIndex >= 10 || isZor || isHardcore);

        // 1. FAZ 2 (%50 Can Altı - Aşırı Yük Modu)
        if (!_phase2Triggered && hpPct <= 0.50f)
        {
            TriggerPhase2();
        }

        // 2. FAZ 3 (%25 Can Altı - APEX KIYAMET PROTOKOLÜ)
        if (_phase2Triggered && !_phase3Triggered && hpPct <= 0.25f && isHighWave)
        {
            TriggerPhase3();
        }

        // 3. Üst Seviyelerde Periyodik Taktik Hava Mayını Bırakma
        if (isHighWave && Time.time >= _mineCooldown)
        {
            _mineCooldown = Time.time + (_phase3Triggered ? 5.5f : 8.5f);
            DropTacticalSkyMine();
        }

        // 4. Özel Saldırı Deseni Tetikleme
        if (_attackCoroutine == null && Time.time >= _nextSpecialTime)
        {
            _attackCoroutine = StartCoroutine(ExecuteArchetypePatternRoutine());
        }
    }

    private void TriggerPhase2()
    {
        _phase2Triggered = true;

        Color enragedTint = Color.Lerp(_baseBossTint, new Color(1f, 0.24f, 0.12f), 0.60f);
        _visuals?.SetBaseColor(enragedTint);

        float pulseShield = Mathf.Max(_health.currentEnemyShield, _health.maxHealth * 0.20f);
        _health.SetEnemyShield(pulseShield);

        CameraShake.Shake(0.30f, 0.38f);
        AudioManager.Instance?.PlayExplosion(isBoss: true);
        AudioManager.Instance?.PlayWarningAlarm();

        GameUI ui = FindAnyObjectByType<GameUI>();
        ui?.ShowWaveAlert($"[FAZ 2: {GetArchetypeLabel(_archetype)}] AŞIRI YÜK SİSTEMLERİ AKTİF!", new Color(1f, 0.32f, 0.10f), 2.2f);

        // Faz 2 Şok Halkası
        FireRadialRing(12, 6.5f, 15f, new Color(1f, 0.35f, 0.15f), 0f);

        // Füze Komutanı Faz 2'de eskort İHA çağırır
        if (_archetype == BossArchetype.FuzeKomutani || _archetype == BossArchetype.KiyametKati)
        {
            EnemyFormationManager.EnsureInstance()?.SpawnEscortWingmen(transform.position, _waveIndex);
        }

        _nextSpecialTime = Time.time + 1.10f;
    }

    private void TriggerPhase3()
    {
        _phase3Triggered = true;

        // Apex Neon Kor Rengi (Kıpkırmızı / Mor Alev)
        Color apexTint = new Color(1f, 0.10f, 0.50f, 1f);
        _visuals?.SetBaseColor(apexTint);

        // Faz 3 Kalkan Aşırı Şarjı
        float apexShield = Mathf.Max(_health.currentEnemyShield, _health.maxHealth * 0.28f);
        _health.SetEnemyShield(apexShield);

        CameraShake.Shake(0.55f, 0.65f);
        AudioManager.Instance?.PlayExplosion(isBoss: true);
        AudioManager.Instance?.PlayWarningAlarm();

        GameUI ui = FindAnyObjectByType<GameUI>();
        ui?.ShowWaveAlert($"<color=#FF0055><b>[FAZ 3: APEX PROTOKOLÜ]</b></color> {GetArchetypeLabel(_archetype)} KIYAMET MODUNA GEÇTİ!", new Color(1f, 0.05f, 0.35f), 3.0f);

        // Çift Katmanlı Dev EMP Şok Halkası
        FireRadialRing(16, 7.5f, 18f, new Color(1f, 0.15f, 0.60f), 0f);
        FireRadialRing(16, 5.2f, 18f, new Color(1f, 0.85f, 0.10f), 11.25f);

        // Savaş alanına anında 2 adet mayın bırak
        DropTacticalSkyMine(offset: -1.4f);
        DropTacticalSkyMine(offset: 1.4f);

        // Kıyamet Katı veya Demir Kanat ek destek dronları çağırır
        EnemyFormationManager.EnsureInstance()?.SpawnEscortWingmen(transform.position, _waveIndex + 5);

        _nextSpecialTime = Time.time + 0.90f;
    }

    private IEnumerator ExecuteArchetypePatternRoutine()
    {
        bool isZor = (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Zor);
        float diffRate = isZor ? 0.72f : (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Kolay ? 1.25f : 1.0f);
        float phaseRate = _phase3Triggered ? 0.58f : (_phase2Triggered ? 0.75f : 1.0f);

        switch (_archetype)
        {
            case BossArchetype.DemirKanat:
                yield return StartCoroutine(DemirKanatFanRoutine());
                _nextSpecialTime = Time.time + 2.6f * diffRate * phaseRate;
                break;

            case BossArchetype.FirtinaDokuyucu:
                yield return StartCoroutine(FirtinaSpiralRoutine());
                _nextSpecialTime = Time.time + 2.8f * diffRate * phaseRate;
                break;

            case BossArchetype.KizilLazer:
                yield return StartCoroutine(KizilLazerSweepRoutine());
                _nextSpecialTime = Time.time + 3.2f * diffRate * phaseRate;
                break;

            case BossArchetype.FuzeKomutani:
                yield return StartCoroutine(FuzeSalvoRoutine());
                _nextSpecialTime = Time.time + 3.0f * diffRate * phaseRate;
                break;

            case BossArchetype.GolgeAvci:
                yield return StartCoroutine(GolgeAvciRoutine());
                _nextSpecialTime = Time.time + 3.1f * diffRate * phaseRate;
                break;

            case BossArchetype.KiyametKati:
                yield return StartCoroutine(KiyametKatiRoutine());
                _nextSpecialTime = Time.time + 3.4f * diffRate * phaseRate;
                break;
        }

        _attackCoroutine = null;
    }

    // 1. DEMİR KANAT (Yelpaze Mermi + Zırh Delici Merkez)
    private IEnumerator DemirKanatFanRoutine()
    {
        int waves = _phase3Triggered ? 3 : (_phase2Triggered ? 2 : 1);
        int count = _phase3Triggered ? 9 : (_phase2Triggered ? 7 : 5);
        float spreadDeg = _phase3Triggered ? 96f : (_phase2Triggered ? 84f : 64f);
        float dmg = (_shooter != null ? _shooter.bulletDamage * 0.85f : 18f);

        for (int w = 0; w < waves; w++)
        {
            if (_health == null || _health.IsDead) yield break;

            Vector2 baseDir = GetAimedDirection(clampMaxDeg: 35f);
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
            float startAngle = baseAngle - spreadDeg * 0.5f;
            float step = count > 1 ? spreadDeg / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float rad = (startAngle + i * step) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Color col = (i == count / 2) ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.78f, 0.2f);
                float spd = (i == count / 2 && _phase3Triggered) ? 11.5f : 8.5f;
                _shooter.SpawnCustomBullet(transform.position + Vector3.down * 0.45f, dir, spd, dmg, col);
            }

            AudioManager.Instance?.PlayGunfire(true);
            if (w + 1 < waves) yield return new WaitForSeconds(0.28f);
        }
    }

    // 2. FIRTINA DOKUYUCU (Çift Karşıt Spiral & Mermi Burgusu)
    private IEnumerator FirtinaSpiralRoutine()
    {
        int pulses = _phase3Triggered ? 4 : (_phase2Triggered ? 3 : 2);
        int ringCount = _phase3Triggered ? 14 : (_phase2Triggered ? 12 : 8);
        float dmg = (_shooter != null ? _shooter.bulletDamage * 0.78f : 16f);

        for (int p = 0; p < pulses; p++)
        {
            if (_health == null || _health.IsDead) yield break;

            Color orbCol = (p % 2 == 0) ? new Color(0.92f, 0.32f, 1f) : new Color(0.45f, 0.75f, 1f);
            FireRadialRing(ringCount, _phase3Triggered ? 7.6f : 6.2f, dmg, orbCol, _spiralAngleOffset);

            // Faz 3'te karşıt yönde ikinci bir burgu salınır (Double Helix)
            if (_phase3Triggered)
            {
                FireRadialRing(ringCount, 5.4f, dmg * 0.85f, new Color(1f, 0.4f, 0.2f), -_spiralAngleOffset);
            }

            _spiralAngleOffset += (p % 2 == 0) ? 24f : -18f;
            AudioManager.Instance?.PlayGunfire(false);

            if (p + 1 < pulses) yield return new WaitForSeconds(0.24f);
        }
    }

    // 3. KIZIL LAZER (Dinamik Süpürme Lazer Işını)
    private IEnumerator KizilLazerSweepRoutine()
    {
        EnsureLaserRenderers();
        Vector2 startDir = GetAimedDirection(clampMaxDeg: 28f);
        float warnTime = _phase3Triggered ? 0.60f : (_phase2Triggered ? 0.78f : 0.95f);
        float elapsed = 0f;

        // 1. Telgraf Nişan Uyarı Çizgisi
        _laserLine.enabled = true;
        while (elapsed < warnTime)
        {
            if (_health == null || _health.IsDead || !_isBossActive)
            {
                _laserLine.enabled = false;
                yield break;
            }

            Vector3 origin = transform.position + Vector3.down * 0.48f;
            Vector3 end = origin + (Vector3)startDir * 14f;
            _laserLine.SetPosition(0, origin);
            _laserLine.SetPosition(1, end);

            float pulse = Mathf.PingPong(Time.time * 14f, 1f);
            float w = Mathf.Lerp(0.04f, 0.12f, pulse);
            _laserLine.startWidth = w;
            _laserLine.endWidth = w;
            Color warnCol = Color.Lerp(new Color(1f, 0.25f, 0.1f, 0.6f), new Color(1f, 0.90f, 0.25f, 1f), pulse);
            _laserLine.startColor = warnCol;
            _laserLine.endColor = warnCol;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2. Aktif Süpürme Lazeri (Sweep Beam)
        float beamDuration = _phase3Triggered ? 0.65f : (_phase2Triggered ? 0.52f : 0.42f);
        float beamElapsed = 0f;
        float tickTimer = 0f;
        float beamDmg = _phase3Triggered ? 18f : (_phase2Triggered ? 14f : 11f);
        float sweepAngle = (_phase2Triggered || _phase3Triggered) ? 18f : 0f;
        float sweepSign = (Random.value > 0.5f) ? 1f : -1f;

        CameraShake.Shake(0.22f, 0.32f);
        AudioManager.Instance?.PlayGunfire(true);

        while (beamElapsed < beamDuration)
        {
            if (_health == null || _health.IsDead || !_isBossActive)
            {
                _laserLine.enabled = false;
                yield break;
            }

            float t = beamElapsed / beamDuration;
            float currentSweep = Mathf.Sin(t * Mathf.PI) * sweepAngle * sweepSign;
            float rad = (Mathf.Atan2(startDir.y, startDir.x) + currentSweep * Mathf.Deg2Rad);
            Vector2 sweepDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            Vector3 origin = transform.position + Vector3.down * 0.48f;
            Vector3 end = origin + (Vector3)sweepDir * 14f;
            _laserLine.SetPosition(0, origin);
            _laserLine.SetPosition(1, end);
            _laserLine.startWidth = 0.46f;
            _laserLine.endWidth = 0.55f;
            _laserLine.startColor = new Color(1f, 0.95f, 0.8f, 1f);
            _laserLine.endColor = new Color(1f, 0.12f, 0.12f, 0.95f);

            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer = 0.10f;
                ApplyLaserHitCheck(origin, sweepDir, 0.50f, beamDmg);
            }

            beamElapsed += Time.deltaTime;
            yield return null;
        }

        _laserLine.enabled = false;
    }

    // 4. FÜZE KOMUTANI (Güdümlü Salkım Füzeler + Alan Kuşatması)
    private IEnumerator FuzeSalvoRoutine()
    {
        int missiles = _phase3Triggered ? 5 : (_phase2Triggered ? 3 : 2);
        float dmg = (_shooter != null ? _shooter.bulletDamage * 1.15f : 24f);

        for (int i = 0; i < missiles; i++)
        {
            if (_health == null || _health.IsDead) yield break;

            float sideOffset = (i - (missiles - 1) * 0.5f) * 0.55f;
            Vector3 spawnPos = transform.position + new Vector3(sideOffset, -0.45f, 0f);
            Vector2 initDir = new Vector2(sideOffset * 0.40f, -1f).normalized;

            GameObject missileObj = _shooter.SpawnCustomBullet(spawnPos, initDir, 5.8f, dmg, new Color(1f, 0.22f, 0.42f));
            if (missileObj != null)
            {
                missileObj.transform.localScale = Vector3.one * 1.45f;
                Bullet b = missileObj.GetComponent<Bullet>();
                if (b != null)
                {
                    b.homingTarget = _ai != null ? _ai.target : null;
                    b.homingTurnSpeed = _phase3Triggered ? 130f : (_phase2Triggered ? 105f : 78f);
                    b.homingDuration = 2.1f;
                }
            }

            AudioManager.Instance?.PlayMissileLaunch();
            yield return new WaitForSeconds(0.18f);
        }
    }

    // 5. GÖLGE AVCI (YENİ ARKETİP: Gizlenme + Ani Süpersonik Hücum + İğne Salvosu)
    private IEnumerator GolgeAvciRoutine()
    {
        if (_health == null || _health.IsDead) yield break;

        // Adım 1: Kamuflaj (Cloaking)
        float cloakTime = 0.85f;
        float elapsed = 0f;
        Color c = _sr != null ? _sr.color : Color.white;

        while (elapsed < cloakTime)
        {
            if (_health == null || _health.IsDead) yield break;
            if (_sr != null)
            {
                c.a = Mathf.Lerp(1f, 0.15f, elapsed / cloakTime);
                _sr.color = c;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Adım 2: Süpersonik Yana Kayış
        float targetX = Random.Range(-2.8f, 2.8f);
        Vector3 newPos = new Vector3(targetX, transform.position.y, transform.position.z);
        transform.position = newPos;
        CameraShake.Shake(0.15f, 0.20f);

        // Adım 3: Kamuflajdan Çıkış ve Ani İğne Salvosu
        if (_sr != null)
        {
            c.a = 1f;
            _sr.color = c;
        }

        int needleCount = _phase3Triggered ? 7 : 5;
        float spread = 36f;
        Vector2 dirToPlayer = GetAimedDirection(clampMaxDeg: 40f);
        float baseAng = Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg;

        for (int i = 0; i < needleCount; i++)
        {
            float ang = baseAng - spread * 0.5f + (i * (spread / (needleCount - 1)));
            float rad = ang * Mathf.Deg2Rad;
            Vector2 shotDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _shooter.SpawnCustomBullet(transform.position + Vector3.down * 0.45f, shotDir, 12.0f, 16f, new Color(0.2f, 0.95f, 0.95f));
        }

        AudioManager.Instance?.PlayGunfire(true);
        yield return new WaitForSeconds(0.35f);
    }

    // 6. KIYAMET KATI TİTAN (YENİ ARKETİP: Çift V-Lazer + Kıyamet Barajı)
    private IEnumerator KiyametKatiRoutine()
    {
        EnsureLaserRenderers();
        float warnTime = _phase3Triggered ? 0.65f : 0.85f;
        float elapsed = 0f;

        Vector2 leftDir = new Vector2(-0.35f, -1f).normalized;
        Vector2 rightDir = new Vector2(0.35f, -1f).normalized;

        _laserLine.enabled = true;
        _laserLine2.enabled = true;

        // Telgraf Uyarısı
        while (elapsed < warnTime)
        {
            if (_health == null || _health.IsDead || !_isBossActive)
            {
                _laserLine.enabled = false;
                _laserLine2.enabled = false;
                yield break;
            }

            Vector3 leftOrigin = transform.position + new Vector3(-0.8f, -0.4f, 0f);
            Vector3 rightOrigin = transform.position + new Vector3(0.8f, -0.4f, 0f);

            _laserLine.SetPosition(0, leftOrigin);
            _laserLine.SetPosition(1, leftOrigin + (Vector3)leftDir * 14f);
            _laserLine2.SetPosition(0, rightOrigin);
            _laserLine2.SetPosition(1, rightOrigin + (Vector3)rightDir * 14f);

            float pulse = Mathf.PingPong(Time.time * 16f, 1f);
            float w = Mathf.Lerp(0.05f, 0.14f, pulse);
            _laserLine.startWidth = w; _laserLine.endWidth = w;
            _laserLine2.startWidth = w; _laserLine2.endWidth = w;

            Color c = Color.Lerp(new Color(1f, 0.1f, 0.4f, 0.6f), new Color(1f, 0.9f, 0.2f, 1f), pulse);
            _laserLine.startColor = c; _laserLine.endColor = c;
            _laserLine2.startColor = c; _laserLine2.endColor = c;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Çift Lazer Ateşi
        float beamDuration = _phase3Triggered ? 0.60f : 0.48f;
        float beamElapsed = 0f;
        float tickTimer = 0f;
        float beamDmg = _phase3Triggered ? 16f : 12f;

        CameraShake.Shake(0.35f, 0.45f);
        AudioManager.Instance?.PlayGunfire(true);

        while (beamElapsed < beamDuration)
        {
            if (_health == null || _health.IsDead || !_isBossActive)
            {
                _laserLine.enabled = false;
                _laserLine2.enabled = false;
                yield break;
            }

            Vector3 leftOrigin = transform.position + new Vector3(-0.8f, -0.4f, 0f);
            Vector3 rightOrigin = transform.position + new Vector3(0.8f, -0.4f, 0f);

            _laserLine.SetPosition(0, leftOrigin);
            _laserLine.SetPosition(1, leftOrigin + (Vector3)leftDir * 14f);
            _laserLine2.SetPosition(0, rightOrigin);
            _laserLine2.SetPosition(1, rightOrigin + (Vector3)rightDir * 14f);

            _laserLine.startWidth = 0.50f; _laserLine.endWidth = 0.60f;
            _laserLine2.startWidth = 0.50f; _laserLine2.endWidth = 0.60f;

            Color beamCol = new Color(1f, 0.12f, 0.35f, 1f);
            _laserLine.startColor = beamCol; _laserLine.endColor = beamCol;
            _laserLine2.startColor = beamCol; _laserLine2.endColor = beamCol;

            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer = 0.10f;
                ApplyLaserHitCheck(leftOrigin, leftDir, 0.55f, beamDmg);
                ApplyLaserHitCheck(rightOrigin, rightDir, 0.55f, beamDmg);
            }

            beamElapsed += Time.deltaTime;
            yield return null;
        }

        _laserLine.enabled = false;
        _laserLine2.enabled = false;

        // Lazer biter bitmez merkezden 14'lü plazma halkası ve 2 güdümlü füze patlat
        FireRadialRing(14, 6.8f, 15f, new Color(1f, 0.25f, 0.85f), 0f);
        yield return new WaitForSeconds(0.20f);
    }

    private void DropTacticalSkyMine(float offset = 0f)
    {
        if (_health == null || _health.IsDead) return;

        float spawnX = Mathf.Clamp(transform.position.x + offset, -3.4f, 3.4f);
        Vector3 pos = new Vector3(spawnX, transform.position.y - 0.55f, 0f);

        GameObject mineObj = new GameObject("BossTacticalSkyMine");
        mineObj.transform.position = pos;
        SkyMineHazard mine = mineObj.AddComponent<SkyMineHazard>();
        float hp = 35f + Mathf.Min(_waveIndex * 2.0f, 35f);
        float dmg = 22f + Mathf.Min(_waveIndex * 1.0f, 15f);
        mine.Initialize(Random.Range(3.5f, 4.8f), hp, dmg);
    }

    private void FireRadialRing(int count, float speed, float damage, Color color, float angleOffsetDeg)
    {
        if (_shooter == null || count <= 0) return;

        Vector3 spawnPos = transform.position;
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float rad = (angleOffsetDeg + i * step) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _shooter.SpawnCustomBullet(spawnPos, dir, speed, damage, color);
        }
    }

    private Vector2 GetAimedDirection(float clampMaxDeg)
    {
        Vector2 down = Vector2.down;
        if (_ai == null || _ai.target == null) return down;

        Vector2 toTarget = ((Vector2)(_ai.target.position - transform.position)).normalized;
        float angleFromDown = Vector2.SignedAngle(down, toTarget);
        float clamped = Mathf.Clamp(angleFromDown, -clampMaxDeg, clampMaxDeg);
        float rad = (-90f + clamped) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void ApplyLaserHitCheck(Vector3 origin, Vector2 dir, float halfWidth, float damage)
    {
        if (_ai == null || _ai.target == null) return;

        Vector2 toTarget = (Vector2)(_ai.target.position - origin);
        float proj = Vector2.Dot(toTarget, dir);
        if (proj < 0f || proj > 14f) return;

        Vector2 closest = (Vector2)origin + dir * proj;
        float dist = Vector2.Distance(_ai.target.position, closest);
        if (dist <= halfWidth)
        {
            PlaneHealth targetHealth = _ai.target.GetComponent<PlaneHealth>();
            if (targetHealth != null && !targetHealth.isInvulnerable && !targetHealth.IsDead)
            {
                targetHealth.TakeDamage(damage, false, _ai.target.position);
            }
        }
    }
}
