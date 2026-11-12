using System.Collections;
using UnityEngine;

/// <summary>
/// Boss Uçakları İçin Çok Fazlı Özel Saldırı ve Arketip Denetleyicisi.
/// - Her Boss benzersiz bir savaş doktrinine sahiptir (DemirKanat, FırtınaDokuyucu, KızılLazer, FüzeKomutanı).
/// - Can %50'nin altına düştüğünde otomatik Faz 2 (Aşırı Yük) aktifleşir ve desenler sertleşir.
/// - Dalga sıfırlandığında veya uçak devre dışı kaldığında tüm korutinler ve lazer çizgileri güvenle temizlenir.
/// </summary>
public class BossPatternController : MonoBehaviour
{
    public enum BossArchetype
    {
        DemirKanat,      // Geniş yelpaze salvo + ağır enerji kalkanı
        FirtinaDokuyucu, // Dönen radyal spiral mermi halkaları
        KizilLazer,      // Nişan uyarı çizgili termal lazer ışını
        FuzeKomutani     // Güdümlü füze salvosu + Faz 2 kanat eskort İHA'ları
    }

    private PlaneHealth _health;
    private PlaneShooter _shooter;
    private AIController _ai;
    private PlaneVisualEffects _visuals;
    private LineRenderer _laserLine;

    private bool _isBossActive = false;
    private bool _phase2Triggered = false;
    private int _waveIndex = 1;
    private BossArchetype _archetype = BossArchetype.DemirKanat;
    private Color _baseBossTint = Color.white;

    private float _nextSpecialTime = 0f;
    private float _spiralAngleOffset = 0f;
    private Coroutine _attackCoroutine;

    private void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _shooter = GetComponent<PlaneShooter>();
        _ai = GetComponent<AIController>();
        _visuals = GetComponent<PlaneVisualEffects>();
        EnsureLaserRenderer();
    }

    private void EnsureLaserRenderer()
    {
        if (_laserLine != null) return;

        GameObject lineObj = new GameObject("BossThermalLaser");
        lineObj.transform.SetParent(transform, false);
        _laserLine = lineObj.AddComponent<LineRenderer>();
        _laserLine.useWorldSpace = true;
        _laserLine.positionCount = 2;
        _laserLine.sortingOrder = 18;
        _laserLine.numCapVertices = 4;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            _laserLine.material = new Material(shader);

        _laserLine.enabled = false;
    }

    public static BossArchetype GetBossArchetype(int waveIndex)
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentMode == GameManager.GameMode.Hardcore ||
                GameManager.Instance.CurrentMode == GameManager.GameMode.BossRush)
            {
                return (BossArchetype)(Mathf.Max(0, waveIndex - 1) % 4);
            }
        }

        int bossTier = Mathf.Max(0, (waveIndex / 5) - 1);
        return (BossArchetype)(bossTier % 4);
    }

    public static string GetArchetypeLabel(BossArchetype archetype)
    {
        switch (archetype)
        {
            case BossArchetype.DemirKanat: return "DEMİR KANAT";
            case BossArchetype.FirtinaDokuyucu: return "FIRTINA DOKUYUCU";
            case BossArchetype.KizilLazer: return "KIZIL LAZER";
            case BossArchetype.FuzeKomutani: return "FÜZE KOMUTANI";
            default: return "KOMUTA BOSS";
        }
    }

    public void ConfigureBoss(int waveIndex, Color baseTint)
    {
        if (_health == null) _health = GetComponent<PlaneHealth>();
        if (_shooter == null) _shooter = GetComponent<PlaneShooter>();
        if (_ai == null) _ai = GetComponent<AIController>();
        if (_visuals == null) _visuals = GetComponent<PlaneVisualEffects>();
        EnsureLaserRenderer();

        ResetPatterns();

        _waveIndex = waveIndex;
        _archetype = GetBossArchetype(waveIndex);
        _baseBossTint = baseTint;
        _isBossActive = true;
        _phase2Triggered = false;
        _nextSpecialTime = Time.time + 2.1f;

        // Zorluğa göre Boss başlangıç enerji kalkanı
        if (_health != null)
        {
            float diffMul = (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Zor) ? 1.35f
                : (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Kolay ? 0.65f : 1.0f);
            float archMul = (_archetype == BossArchetype.DemirKanat) ? 1.45f : 1.0f;
            float shield = (40f + Mathf.Min(waveIndex * 6f, 120f)) * diffMul * archMul;
            _health.SetEnemyShield(shield);
        }
    }

    public void ResetPatterns()
    {
        _isBossActive = false;
        _phase2Triggered = false;

        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        if (_laserLine != null)
            _laserLine.enabled = false;
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
            return;
        }

        if (_ai == null || !_ai.enabled || _shooter == null || !_shooter.enabled)
            return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.BulletHell)
            return;

        // Can %50 altına indiğinde Faz 2 Aşırı Yük tetiklenir
        if (!_phase2Triggered && _health.HealthPercent <= 0.50f)
        {
            TriggerPhase2();
        }

        if (_attackCoroutine == null && Time.time >= _nextSpecialTime)
        {
            _attackCoroutine = StartCoroutine(ExecuteArchetypePatternRoutine());
        }
    }

    private void TriggerPhase2()
    {
        _phase2Triggered = true;

        Color enragedTint = Color.Lerp(_baseBossTint, new Color(1f, 0.22f, 0.15f), 0.55f);
        _visuals?.SetBaseColor(enragedTint);

        float pulseShield = Mathf.Max(_health.currentEnemyShield, _health.maxHealth * 0.16f);
        _health.SetEnemyShield(pulseShield);

        CameraShake.Shake(0.25f, 0.32f);
        AudioManager.Instance?.PlayExplosion(isBoss: true);
        AudioManager.Instance?.PlayWarningAlarm();

        GameUI ui = FindObjectOfType<GameUI>();
        ui?.ShowWaveAlert($"[FAZ 2: {GetArchetypeLabel(_archetype)}] AŞIRI YÜK MODU AKTİF!", new Color(1f, 0.28f, 0.12f), 2.3f);

        // Faz geçişi şok halkası
        FireRadialRing(10, 6.2f, 14f, new Color(1f, 0.35f, 0.15f), 0f);

        // Füze Komutanı Faz 2'de iki kanat eskort İHA çağırır
        if (_archetype == BossArchetype.FuzeKomutani)
        {
            EnemyFormationManager.EnsureInstance()?.SpawnEscortWingmen(transform.position, _waveIndex);
        }

        _nextSpecialTime = Time.time + 1.25f;
    }

    private IEnumerator ExecuteArchetypePatternRoutine()
    {
        float diffRate = (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Zor) ? 0.78f
            : (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Kolay ? 1.22f : 1.0f);
        float phaseRate = _phase2Triggered ? 0.75f : 1.0f;

        switch (_archetype)
        {
            case BossArchetype.DemirKanat:
                yield return StartCoroutine(DemirKanatFanRoutine());
                _nextSpecialTime = Time.time + 2.8f * diffRate * phaseRate;
                break;

            case BossArchetype.FirtinaDokuyucu:
                yield return StartCoroutine(FirtinaSpiralRoutine());
                _nextSpecialTime = Time.time + 3.0f * diffRate * phaseRate;
                break;

            case BossArchetype.KizilLazer:
                yield return StartCoroutine(KizilLazerSweepRoutine());
                _nextSpecialTime = Time.time + 3.5f * diffRate * phaseRate;
                break;

            case BossArchetype.FuzeKomutani:
                yield return StartCoroutine(FuzeSalvoRoutine());
                _nextSpecialTime = Time.time + 3.3f * diffRate * phaseRate;
                break;
        }

        _attackCoroutine = null;
    }

    private IEnumerator DemirKanatFanRoutine()
    {
        int waves = _phase2Triggered ? 2 : 1;
        int count = _phase2Triggered ? 7 : 5;
        float spreadDeg = _phase2Triggered ? 84f : 64f;
        float dmg = (_shooter != null ? _shooter.bulletDamage * 0.85f : 16f);

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
                _shooter.SpawnCustomBullet(transform.position + Vector3.down * 0.45f, dir, 8.2f, dmg, new Color(1f, 0.78f, 0.2f));
            }

            AudioManager.Instance?.PlayGunfire(true);
            if (w + 1 < waves) yield return new WaitForSeconds(0.34f);
        }
    }

    private IEnumerator FirtinaSpiralRoutine()
    {
        int pulses = _phase2Triggered ? 3 : 2;
        int ringCount = _phase2Triggered ? 12 : 8;
        float dmg = (_shooter != null ? _shooter.bulletDamage * 0.78f : 14f);

        for (int p = 0; p < pulses; p++)
        {
            if (_health == null || _health.IsDead) yield break;

            Color orbCol = (p % 2 == 0) ? new Color(0.92f, 0.32f, 1f) : new Color(0.45f, 0.75f, 1f);
            FireRadialRing(ringCount, _phase2Triggered ? 6.8f : 5.8f, dmg, orbCol, _spiralAngleOffset);
            _spiralAngleOffset += (p % 2 == 0) ? 22f : -16f;
            AudioManager.Instance?.PlayGunfire(false);

            if (p + 1 < pulses) yield return new WaitForSeconds(0.30f);
        }
    }

    private IEnumerator KizilLazerSweepRoutine()
    {
        EnsureLaserRenderer();
        Vector2 lockedDir = GetAimedDirection(clampMaxDeg: 22f);
        float warnTime = _phase2Triggered ? 0.78f : 0.98f;
        float elapsed = 0f;

        // 1. Nişan Uyarı Hattı (Oyuncunun sıyrılabilmesi için görsel telgraf)
        _laserLine.enabled = true;
        while (elapsed < warnTime)
        {
            if (_health == null || _health.IsDead || !_isBossActive)
            {
                _laserLine.enabled = false;
                yield break;
            }

            Vector3 origin = transform.position + Vector3.down * 0.48f;
            Vector3 end = origin + (Vector3)lockedDir * 13f;
            _laserLine.SetPosition(0, origin);
            _laserLine.SetPosition(1, end);

            float pulse = Mathf.PingPong(Time.time * 12f, 1f);
            float w = Mathf.Lerp(0.04f, 0.11f, pulse);
            _laserLine.startWidth = w;
            _laserLine.endWidth = w;
            Color warnCol = Color.Lerp(new Color(1f, 0.25f, 0.1f, 0.55f), new Color(1f, 0.85f, 0.25f, 0.95f), pulse);
            _laserLine.startColor = warnCol;
            _laserLine.endColor = warnCol;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2. Aktif Termal Lazer Işını
        float beamDuration = _phase2Triggered ? 0.52f : 0.42f;
        float tickTimer = 0f;
        float beamElapsed = 0f;
        float beamDmg = _phase2Triggered ? 14f : 11f;

        CameraShake.Shake(0.18f, 0.28f);
        AudioManager.Instance?.PlayGunfire(true);

        while (beamElapsed < beamDuration)
        {
            if (_health == null || _health.IsDead || !_isBossActive)
            {
                _laserLine.enabled = false;
                yield break;
            }

            Vector3 origin = transform.position + Vector3.down * 0.48f;
            Vector3 end = origin + (Vector3)lockedDir * 13f;
            _laserLine.SetPosition(0, origin);
            _laserLine.SetPosition(1, end);
            _laserLine.startWidth = 0.44f;
            _laserLine.endWidth = 0.52f;
            _laserLine.startColor = new Color(1f, 0.92f, 0.75f, 1f);
            _laserLine.endColor = new Color(1f, 0.18f, 0.12f, 0.95f);

            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer = 0.12f;
                ApplyLaserHitCheck(origin, lockedDir, 0.48f, beamDmg);
            }

            beamElapsed += Time.deltaTime;
            yield return null;
        }

        _laserLine.enabled = false;
    }

    private IEnumerator FuzeSalvoRoutine()
    {
        int missiles = _phase2Triggered ? 3 : 2;
        float dmg = (_shooter != null ? _shooter.bulletDamage * 1.15f : 22f);

        for (int i = 0; i < missiles; i++)
        {
            if (_health == null || _health.IsDead) yield break;

            float sideOffset = (i - (missiles - 1) * 0.5f) * 0.55f;
            Vector3 spawnPos = transform.position + new Vector3(sideOffset, -0.45f, 0f);
            Vector2 initDir = new Vector2(sideOffset * 0.35f, -1f).normalized;

            GameObject missileObj = _shooter.SpawnCustomBullet(spawnPos, initDir, 5.5f, dmg, new Color(1f, 0.22f, 0.42f));
            if (missileObj != null)
            {
                missileObj.transform.localScale = Vector3.one * 1.4f;
                Bullet b = missileObj.GetComponent<Bullet>();
                if (b != null)
                {
                    b.homingTarget = _ai != null ? _ai.target : null;
                    b.homingTurnSpeed = _phase2Triggered ? 105f : 78f;
                    b.homingDuration = 1.85f;
                }
            }

            AudioManager.Instance?.PlayMissileLaunch();
            yield return new WaitForSeconds(0.22f);
        }
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
        if (proj < 0f || proj > 13f) return;

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

