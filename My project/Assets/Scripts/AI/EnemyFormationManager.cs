using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Düşman Filoları ve Sürü Akınları Yöneticisi (Enemy Formation & Squadron Director).
/// Gökyüzünde ana it dalaşı devam ederken belirli aralıklarla:
/// 1. İHA Sürü Akınları (V-Düzeni, Yılan, Kıskaç, Kamikaze, Ağır Zırhlı)
/// 2. Askeri Savaş Jeti Filoları (MiG-29 Kurt Kapanı, Su-35 Ok Başı, Tu-22M Stratejik Bombardıman, S-37 Süpersonik Yıldırım)
/// gönderir. Filo tamamen temizlendiğinde oyuncuya taktik altın ve kombo ödülü sunar.
/// </summary>
public class EnemyFormationManager : MonoBehaviour
{
    public static EnemyFormationManager Instance { get; private set; }

    [Header("Zamanlama Ayarları")]
    public float initialDelay = 6.0f;
    public float minWaveInterval = 12.0f;
    public float maxWaveInterval = 18.0f;

    [Header("Görsel Varlıklar")]
    private Sprite[] _droneSprites;
    private Sprite _sprMig29;
    private Sprite _sprSu35;
    private Sprite _sprBomber;
    private Sprite _sprS37;

    private readonly List<SwarmEnemy> _activeWaveEnemies = new List<SwarmEnemy>();
    private int _currentWaveTotalCount = 0;
    private int _currentWaveKilledCount = 0;
    private bool _isCurrentFormationJetSquadron = false;
    private Coroutine _directorCoroutine;
    private bool _isDirectorRunning = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadAssets();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static EnemyFormationManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("EnemyFormationManager");
        if (go == null)
        {
            go = new GameObject("EnemyFormationManager");
        }

        Instance = go.GetComponent<EnemyFormationManager>();
        if (Instance == null)
        {
            Instance = go.AddComponent<EnemyFormationManager>();
        }
        Instance.LoadAssets();
        return Instance;
    }

    private void LoadAssets()
    {
        if (_sprMig29 == null)
        {
            _sprMig29 = Resources.Load<Sprite>("enemy_mig29");
            _sprSu35 = Resources.Load<Sprite>("enemy_su35");
            _sprBomber = Resources.Load<Sprite>("enemy_bomber");
            _sprS37 = Resources.Load<Sprite>("enemy_s37");
        }

        if (_droneSprites != null && _droneSprites.Length > 0) return;

        List<Sprite> list = new List<Sprite>();
        for (int i = 1; i <= 5; i++)
        {
            Sprite s = Resources.Load<Sprite>("drone_mk" + i);
            if (s != null) list.Add(s);
        }

        if (list.Count == 0)
        {
            Sprite fallback = Resources.Load<Sprite>("plane2");
            if (fallback != null) list.Add(fallback);
        }

        _droneSprites = list.ToArray();
    }

    private void Start()
    {
        StartDirector();
    }

    public void StartDirector()
    {
        if (_isDirectorRunning) return;

        // 2 Oyunculu lokal düelloda sürü akınlarını başlatma
        if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer)
        {
            return;
        }

        _isDirectorRunning = true;
        _directorCoroutine = StartCoroutine(FormationDirectorRoutine());
    }

    public void StopDirector()
    {
        _isDirectorRunning = false;
        if (_directorCoroutine != null)
        {
            StopCoroutine(_directorCoroutine);
            _directorCoroutine = null;
        }
        ClearAllSwarmEnemies();
    }

    private IEnumerator FormationDirectorRoutine()
    {
        yield return new WaitForSeconds(initialDelay);

        while (_isDirectorRunning)
        {
            if (IsGamePausedOrEnded())
            {
                yield return new WaitForSeconds(1.0f);
                continue;
            }

            // Bir önceki filonun bitmesini bekle
            if (_activeWaveEnemies.Count > 0)
            {
                yield return new WaitForSeconds(2.0f);
                continue;
            }

            yield return StartCoroutine(SpawnRandomFormationRoutine());

            float waitTime = Random.Range(minWaveInterval, maxWaveInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private bool IsGamePausedOrEnded()
    {
        if (Time.timeScale <= 0.01f) return true;
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.plane1Health == null || GameManager.Instance.plane1Health.IsDead)
                return true;
        }
        return false;
    }

    private IEnumerator SpawnRandomFormationRoutine()
    {
        _activeWaveEnemies.Clear();
        _currentWaveKilledCount = 0;

        int currentWave = GameManager.Instance != null ? GameManager.Instance.currentPlaneIndex : 1;
        float baseHp = 25f + Mathf.Min(currentWave * 2.5f, 35f);
        float baseSpeed = 4.0f + Mathf.Min(currentWave * 0.1f, 1.8f);

        Sprite droneSprite = GetRandomDroneSprite();
        // 0..4: İHA Formasyonları, 5..8: Askeri Savaş Jeti Filoları!
        int formationChoice = Random.Range(0, 9);
        _isCurrentFormationJetSquadron = (formationChoice >= 5);

        GameUI ui = FindObjectOfType<GameUI>();
        string alertMsg = "! DÜŞMAN İHA FİLOSU YAKLAŞIYOR !";
        Color alertCol = new Color(1f, 0.5f, 0.15f);

        switch (formationChoice)
        {
            case 3:
                alertMsg = "! DİKKAT: KAMİKAZE DRONE SÜRÜSÜ !";
                alertCol = new Color(1f, 0.25f, 0.1f);
                AudioManager.Instance?.PlayWarningAlarm();
                break;

            case 4:
                alertMsg = "! AĞIR ZIRHLI İHA ESKORTU GİRDİ !";
                alertCol = new Color(0.25f, 0.8f, 1f);
                break;

            case 5:
                alertMsg = "! RADAR ALARMI: 4'LÜ MiG-29 KURT KAPANI SORTİSİ !";
                alertCol = new Color(1f, 0.35f, 0.20f);
                AudioManager.Instance?.PlayWarningAlarm();
                break;

            case 6:
                alertMsg = "! HAVA TEHDİDİ: Su-35 ÖNCÜLÜĞÜNDE 5'Lİ JET AKINI !";
                alertCol = new Color(0.20f, 0.85f, 1.0f);
                AudioManager.Instance?.PlayWarningAlarm();
                break;

            case 7:
                alertMsg = "! AĞIR HAVA TEHDİDİ: Tu-22M BOMBARDIMAN ESKORTU !";
                alertCol = new Color(1f, 0.20f, 0.20f);
                AudioManager.Instance?.PlayWarningAlarm();
                break;

            case 8:
                alertMsg = "! ACİL ALARM: S-37 BERKUT SÜPERSONİK AVCI SORTİSİ !";
                alertCol = new Color(0.0f, 0.95f, 0.95f);
                AudioManager.Instance?.PlayWarningAlarm();
                break;
        }

        ui?.ShowWaveAlert(alertMsg, alertCol, 2.2f);
        yield return new WaitForSeconds(0.4f);

        switch (formationChoice)
        {
            case 0:
                SpawnVFormation(droneSprite, baseHp, baseSpeed);
                break;

            case 1:
                yield return StartCoroutine(SpawnSineWaveFormationRoutine(droneSprite, baseHp, baseSpeed));
                break;

            case 2:
                yield return StartCoroutine(SpawnPincerFormationRoutine(droneSprite, baseHp, baseSpeed));
                break;

            case 3:
                yield return StartCoroutine(SpawnKamikazeSquadRoutine(droneSprite, baseHp, baseSpeed));
                break;

            case 4:
                yield return StartCoroutine(SpawnHeavyEscortRoutine(droneSprite, baseHp, baseSpeed));
                break;

            case 5:
                yield return StartCoroutine(SpawnWolfpackFormationRoutine(baseHp, baseSpeed));
                break;

            case 6:
                SpawnArrowheadJetStrike(baseHp, baseSpeed);
                break;

            case 7:
                yield return StartCoroutine(SpawnCarpetBombingRaidRoutine(baseHp, baseSpeed));
                break;

            case 8:
                yield return StartCoroutine(SpawnSupersonicBlitzRoutine(baseHp, baseSpeed));
                break;
        }
    }

    // 1. Standart İHA Formasyonları (V, Yılan, Kıskaç, Kamikaze, Ağır Zırhlı)
    private void SpawnVFormation(Sprite droneSprite, float hp, float speed)
    {
        Vector2[] offsets = new Vector2[]
        {
            new Vector2(0f, 6.4f),
            new Vector2(-1.1f, 7.2f),
            new Vector2(1.1f, 7.2f),
            new Vector2(-2.2f, 8.0f),
            new Vector2(2.2f, 8.0f)
        };

        _currentWaveTotalCount = offsets.Length;

        for (int i = 0; i < offsets.Length; i++)
        {
            SwarmEnemy enemy = CreateDroneObject("SwarmDrone_V_" + i, offsets[i]);
            enemy.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed, hp, droneSprite);
            _activeWaveEnemies.Add(enemy);
        }
    }

    private IEnumerator SpawnSineWaveFormationRoutine(Sprite droneSprite, float hp, float speed)
    {
        int count = 4;
        _currentWaveTotalCount = count;
        float startX = (Random.value < 0.5f) ? -1.8f : 1.8f;

        for (int i = 0; i < count; i++)
        {
            if (IsGamePausedOrEnded()) yield break;

            Vector2 spawnPos = new Vector2(startX, 6.4f);
            SwarmEnemy enemy = CreateDroneObject("SwarmDrone_Sine_" + i, spawnPos);
            enemy.Initialize(SwarmEnemy.MovementPattern.SineWave, speed, hp, droneSprite);
            _activeWaveEnemies.Add(enemy);

            yield return new WaitForSeconds(0.38f);
        }
    }

    private IEnumerator SpawnPincerFormationRoutine(Sprite droneSprite, float hp, float speed)
    {
        _currentWaveTotalCount = 4;

        Vector2[] leftSpawns = new Vector2[] { new Vector2(-2.6f, 6.4f), new Vector2(-3.2f, 7.1f) };
        Vector2 leftDir = new Vector2(1.2f, -3.5f).normalized;

        Vector2[] rightSpawns = new Vector2[] { new Vector2(2.6f, 6.4f), new Vector2(3.2f, 7.1f) };
        Vector2 rightDir = new Vector2(-1.2f, -3.5f).normalized;

        for (int i = 0; i < 2; i++)
        {
            SwarmEnemy leftEnemy = CreateDroneObject("SwarmDrone_Pincer_L_" + i, leftSpawns[i]);
            leftEnemy.diveDirection = leftDir;
            leftEnemy.Initialize(SwarmEnemy.MovementPattern.DivePincer, speed * 1.15f, hp, droneSprite);
            _activeWaveEnemies.Add(leftEnemy);

            SwarmEnemy rightEnemy = CreateDroneObject("SwarmDrone_Pincer_R_" + i, rightSpawns[i]);
            rightEnemy.diveDirection = rightDir;
            rightEnemy.Initialize(SwarmEnemy.MovementPattern.DivePincer, speed * 1.15f, hp, droneSprite);
            _activeWaveEnemies.Add(rightEnemy);

            yield return new WaitForSeconds(0.42f);
        }
    }

    private IEnumerator SpawnKamikazeSquadRoutine(Sprite droneSprite, float hp, float speed)
    {
        float[] xs = new float[] { -2.4f, 2.4f, -1.1f, 1.1f, 0f };
        _currentWaveTotalCount = xs.Length;

        for (int i = 0; i < xs.Length; i++)
        {
            if (IsGamePausedOrEnded()) yield break;

            Vector2 spawnPos = new Vector2(xs[i], 6.4f);
            SwarmEnemy enemy = CreateDroneObject("SwarmDrone_Kamikaze_" + i, spawnPos);
            enemy.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed * 1.05f, hp, droneSprite, null, SwarmEnemy.DroneClass.Kamikaze);
            _activeWaveEnemies.Add(enemy);

            yield return new WaitForSeconds(0.32f);
        }
    }

    private IEnumerator SpawnHeavyEscortRoutine(Sprite droneSprite, float hp, float speed)
    {
        _currentWaveTotalCount = 4;

        Vector2[] heavyPos = new Vector2[] { new Vector2(-1.35f, 6.4f), new Vector2(1.35f, 6.4f) };
        for (int i = 0; i < heavyPos.Length; i++)
        {
            SwarmEnemy heavy = CreateDroneObject("SwarmDrone_Heavy_" + i, heavyPos[i]);
            heavy.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed * 0.95f, hp, droneSprite, null, SwarmEnemy.DroneClass.HeavyShielded);
            _activeWaveEnemies.Add(heavy);
        }

        yield return new WaitForSeconds(0.45f);

        Vector2[] bomberPos = new Vector2[] { new Vector2(-2.6f, 6.4f), new Vector2(2.6f, 6.4f) };
        for (int i = 0; i < bomberPos.Length; i++)
        {
            SwarmEnemy bomber = CreateDroneObject("SwarmDrone_Bomber_" + i, bomberPos[i]);
            bomber.Initialize(SwarmEnemy.MovementPattern.SineWave, speed * 0.85f, hp, droneSprite, null, SwarmEnemy.DroneClass.PlasmaBomber);
            _activeWaveEnemies.Add(bomber);
        }
    }

    // 2. YENİ ASKERİ SAVAŞ JETİ FİLOLARI (MiG-29, Su-35, Tu-22M, S-37)

    /// <summary>
    /// 4'lü MiG-29 Kurt Kapanı (Çapraz dalarak oyuncuyu ortada sıkıştıran taktik it dalaşı sortisi).
    /// </summary>
    private IEnumerator SpawnWolfpackFormationRoutine(float hp, float speed)
    {
        _currentWaveTotalCount = 4;
        Sprite jetSprite = _sprMig29 != null ? _sprMig29 : GetRandomDroneSprite();

        Vector2[] leftSpawns = new Vector2[] { new Vector2(-2.8f, 6.5f), new Vector2(-3.4f, 7.3f) };
        Vector2 leftDir = new Vector2(1.35f, -3.2f).normalized;

        Vector2[] rightSpawns = new Vector2[] { new Vector2(2.8f, 6.5f), new Vector2(3.4f, 7.3f) };
        Vector2 rightDir = new Vector2(-1.35f, -3.2f).normalized;

        for (int i = 0; i < 2; i++)
        {
            if (IsGamePausedOrEnded()) yield break;

            SwarmEnemy leftJet = CreateDroneObject("MiG29_Wolfpack_L_" + i, leftSpawns[i]);
            leftJet.diveDirection = leftDir;
            leftJet.Initialize(SwarmEnemy.MovementPattern.DivePincer, speed * 1.25f, hp * 1.35f, jetSprite, Color.white, SwarmEnemy.DroneClass.Scout, 0.95f);
            _activeWaveEnemies.Add(leftJet);

            SwarmEnemy rightJet = CreateDroneObject("MiG29_Wolfpack_R_" + i, rightSpawns[i]);
            rightJet.diveDirection = rightDir;
            rightJet.Initialize(SwarmEnemy.MovementPattern.DivePincer, speed * 1.25f, hp * 1.35f, jetSprite, Color.white, SwarmEnemy.DroneClass.Scout, 0.95f);
            _activeWaveEnemies.Add(rightJet);

            yield return new WaitForSeconds(0.42f);
        }
    }

    /// <summary>
    /// 1 Su-35 Ağır Flanker Lideri + 4 MiG-29 Eskort Uçağı (5'li Ok Başı Taarruz Akını).
    /// </summary>
    private void SpawnArrowheadJetStrike(float hp, float speed)
    {
        _currentWaveTotalCount = 5;
        Sprite su35Sprite = _sprSu35 != null ? _sprSu35 : GetRandomDroneSprite();
        Sprite mig29Sprite = _sprMig29 != null ? _sprMig29 : su35Sprite;

        // Merkez Lider Jet (Su-35 Flanker)
        SwarmEnemy leader = CreateDroneObject("Su35_Arrowhead_Leader", new Vector2(0f, 6.5f));
        leader.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed * 1.05f, hp * 2.8f, su35Sprite, Color.white, SwarmEnemy.DroneClass.HeavyShielded, 1.10f);
        _activeWaveEnemies.Add(leader);

        // Kanat Eskortları (MiG-29)
        Vector2[] wingOffsets = new Vector2[]
        {
            new Vector2(-1.25f, 7.4f),
            new Vector2(1.25f, 7.4f),
            new Vector2(-2.50f, 8.3f),
            new Vector2(2.50f, 8.3f)
        };

        for (int i = 0; i < wingOffsets.Length; i++)
        {
            SwarmEnemy wingman = CreateDroneObject("MiG29_Arrowhead_Wing_" + i, wingOffsets[i]);
            wingman.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed * 1.05f, hp * 1.25f, mig29Sprite, Color.white, SwarmEnemy.DroneClass.Scout, 0.92f);
            _activeWaveEnemies.Add(wingman);
        }
    }

    /// <summary>
    /// 1 Tu-22M Ağır Stratejik Bombardıman Jeti + 2 Su-35 Flanker Eskortu (Halı Bombardımanı Baskını).
    /// </summary>
    private IEnumerator SpawnCarpetBombingRaidRoutine(float hp, float speed)
    {
        _currentWaveTotalCount = 3;
        Sprite bomberSprite = _sprBomber != null ? _sprBomber : GetRandomDroneSprite();
        Sprite escortSprite = _sprSu35 != null ? _sprSu35 : bomberSprite;

        // Tu-22M Ağır Stratejik Bombardıman Jeti (Merkez)
        SwarmEnemy bomber = CreateDroneObject("Tu22M_Carpet_Bomber", new Vector2(0f, 6.6f));
        bomber.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed * 0.72f, hp * 4.2f, bomberSprite, Color.white, SwarmEnemy.DroneClass.PlasmaBomber, 1.35f);
        _activeWaveEnemies.Add(bomber);

        yield return new WaitForSeconds(0.35f);

        // İkiz Su-35 Eskortları
        Vector2[] escortPos = new Vector2[] { new Vector2(-2.4f, 7.2f), new Vector2(2.4f, 7.2f) };
        for (int i = 0; i < escortPos.Length; i++)
        {
            SwarmEnemy escort = CreateDroneObject("Su35_Bomber_Escort_" + i, escortPos[i]);
            escort.Initialize(SwarmEnemy.MovementPattern.SineWave, speed * 0.85f, hp * 1.6f, escortSprite, Color.white, SwarmEnemy.DroneClass.HeavyShielded, 1.0f);
            _activeWaveEnemies.Add(escort);
        }
    }

    /// <summary>
    /// 3'lü S-37 Berkut İleri Ok Kanatlı Süpersonik Önleme Filosu (Mach 2+ Yıldırım Geçişi).
    /// </summary>
    private IEnumerator SpawnSupersonicBlitzRoutine(float hp, float speed)
    {
        _currentWaveTotalCount = 3;
        Sprite s37Sprite = _sprS37 != null ? _sprS37 : GetRandomDroneSprite();

        float[] xs = new float[] { -2.0f, 0f, 2.0f };
        for (int i = 0; i < xs.Length; i++)
        {
            if (IsGamePausedOrEnded()) yield break;

            Vector2 spawnPos = new Vector2(xs[i], 6.5f);
            SwarmEnemy blitz = CreateDroneObject("S37_Supersonic_Blitz_" + i, spawnPos);
            blitz.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed * 1.60f, hp * 1.45f, s37Sprite, Color.white, SwarmEnemy.DroneClass.Scout, 0.98f);
            _activeWaveEnemies.Add(blitz);

            yield return new WaitForSeconds(0.28f);
        }
    }

    public void SpawnEscortWingmen(Vector3 bossPos, int waveIndex)
    {
        if (IsGamePausedOrEnded()) return;

        float hp = 26f + Mathf.Min(waveIndex * 2.5f, 40f);
        float speed = 4.2f + Mathf.Min(waveIndex * 0.1f, 1.5f);
        Sprite jetSprite = _sprMig29 != null ? _sprMig29 : GetRandomDroneSprite();

        float[] offsetsX = new float[] { -1.6f, 1.6f };
        for (int i = 0; i < offsetsX.Length; i++)
        {
            float spawnX = Mathf.Clamp(bossPos.x + offsetsX[i], -3.6f, 3.6f);
            Vector2 spawnPos = new Vector2(spawnX, Mathf.Max(bossPos.y + 0.5f, 4.8f));
            SwarmEnemy wingman = CreateDroneObject("BossEscort_" + i, spawnPos);
            SwarmEnemy.DroneClass cls = (i == 0) ? SwarmEnemy.DroneClass.HeavyShielded : SwarmEnemy.DroneClass.Kamikaze;
            wingman.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed, hp, jetSprite, Color.white, cls, 0.92f);
            _activeWaveEnemies.Add(wingman);
        }
    }

    private SwarmEnemy CreateDroneObject(string name, Vector2 position)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;

        SwarmEnemy enemy = go.AddComponent<SwarmEnemy>();
        return enemy;
    }

    private Sprite GetRandomDroneSprite()
    {
        if (_droneSprites != null && _droneSprites.Length > 0)
        {
            return _droneSprites[Random.Range(0, _droneSprites.Length)];
        }
        return null;
    }

    public void OnSwarmEnemyKilled(SwarmEnemy enemy)
    {
        _currentWaveKilledCount++;
        _activeWaveEnemies.Remove(enemy);

        if (_currentWaveTotalCount > 0 && _currentWaveKilledCount >= _currentWaveTotalCount)
        {
            OnFormationWipedOutBonus();
        }
    }

    public void OnSwarmEnemyEscaped(SwarmEnemy enemy)
    {
        if (_activeWaveEnemies.Contains(enemy))
        {
            _activeWaveEnemies.Remove(enemy);
        }
    }

    private void OnFormationWipedOutBonus()
    {
        CombatComboManager combo = CombatComboManager.EnsureInstance();
        int comboGain = _isCurrentFormationJetSquadron ? 3 : 2;
        combo.AddCombo(comboGain);

        int baseReward = _isCurrentFormationJetSquadron ? 120 : 50;
        int bonusGold = combo.ApplyGoldMultiplier(baseReward, out int comboBonus);

        HangarManager.EnsureInstance()?.AddGold(bonusGold);
        if (GameManager.Instance != null)
            GameManager.Instance.sessionGoldEarned += bonusGold;

        AudioManager.Instance?.PlayRewardFanfare();
        RadioChatterManager.Instance?.PlaySquadronClear();

        string multTag = comboBonus > 0 ? $" [x{combo.GetMultiplier():0.0}]" : "";
        string titleTag = _isCurrentFormationJetSquadron ? ">> DÜŞMAN JET FİLOSU İMHA EDİLDİ! <<" : ">> İHA FİLOSU TEMİZLENDİ! <<";
        Color bannerCol = _isCurrentFormationJetSquadron ? new Color(1f, 0.85f, 0.2f) : new Color(0.2f, 1f, 0.45f);

        GameUI ui = FindObjectOfType<GameUI>();
        ui?.ShowWaveAlert($"{titleTag}\n<color=#FFE082>+{bonusGold} ALTIN{multTag}</color>", bannerCol, 2.5f);
    }

    public void ClearAllSwarmEnemies()
    {
        for (int i = _activeWaveEnemies.Count - 1; i >= 0; i--)
        {
            if (_activeWaveEnemies[i] != null)
            {
                Destroy(_activeWaveEnemies[i].gameObject);
            }
        }
        _activeWaveEnemies.Clear();
        _currentWaveTotalCount = 0;
        _currentWaveKilledCount = 0;
    }

    private void OnDisable()
    {
        StopDirector();
    }
}
