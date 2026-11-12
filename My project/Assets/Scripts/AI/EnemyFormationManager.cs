using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Düşman Filoları ve Sürü Akınları Yöneticisi (Enemy Formation & Swarm Wave Director).
/// Gökyüzünde ana it dalaşı devam ederken belirli aralıklarla V-düzeni, Yılan kıvrımı ve
/// Kıskaç taarruzu gibi taktik formasyonlarda İHA filoları gönderir.
/// Filo tamamen temizlendiğinde oyuncuya ekstra altın, puan ve görsel takdir sunar.
/// </summary>
public class EnemyFormationManager : MonoBehaviour
{
    public static EnemyFormationManager Instance { get; private set; }

    [Header("Zamanlama Ayarları")]
    public float initialDelay = 7.0f;
    public float minWaveInterval = 14.0f;
    public float maxWaveInterval = 20.0f;

    [Header("Görsel Varlıklar")]
    private Sprite[] _droneSprites;

    private readonly List<SwarmEnemy> _activeWaveEnemies = new List<SwarmEnemy>();
    private int _currentWaveTotalCount = 0;
    private int _currentWaveKilledCount = 0;
    private Coroutine _directorCoroutine;
    private bool _isDirectorRunning = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadDroneSprites();
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
        Instance.LoadDroneSprites();
        return Instance;
    }

    private void LoadDroneSprites()
    {
        if (_droneSprites != null && _droneSprites.Length > 0) return;

        List<Sprite> list = new List<Sprite>();
        for (int i = 1; i <= 5; i++)
        {
            Sprite s = Resources.Load<Sprite>("drone_mk" + i);
            if (s != null) list.Add(s);
        }

        if (list.Count == 0)
        {
            // Eğer Resources içinde bulunamazsa Sprites altındaki uçak veya dronları dene
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
            // Eğer oyun bitti veya durdurulduysa bekle
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

            // Yeni formasyon akınını başlat
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
        int formationChoice = Random.Range(0, 5);

        // Radar / Taktik Uyarı Bildirimi (Her sınıf için özel uyarı)
        GameUI ui = FindObjectOfType<GameUI>();
        string alertMsg = "! DÜŞMAN İHA FİLOSU YAKLAŞIYOR !";
        Color alertCol = new Color(1f, 0.5f, 0.15f);
        if (formationChoice == 3)
        {
            alertMsg = "! DİKKAT: KAMİKAZE DRONE SÜRÜSÜ !";
            alertCol = new Color(1f, 0.25f, 0.1f);
        }
        else if (formationChoice == 4)
        {
            alertMsg = "! AĞIR ZIRHLI İHA ESKORTU GİRDİ !";
            alertCol = new Color(0.25f, 0.8f, 1f);
        }
        ui?.ShowWaveAlert(alertMsg, alertCol, 1.9f);

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
        }
    }

    private void SpawnVFormation(Sprite droneSprite, float hp, float speed)
    {
        // 5'li V-Formasyonu (Ok Ucu Düzeni)
        Vector2[] offsets = new Vector2[]
        {
            new Vector2(0f, 6.4f),       // Lider
            new Vector2(-1.1f, 7.2f),    // Sol Kanat 1
            new Vector2(1.1f, 7.2f),     // Sağ Kanat 1
            new Vector2(-2.2f, 8.0f),    // Sol Kanat 2
            new Vector2(2.2f, 8.0f)      // Sağ Kanat 2
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
        // 4'lü Yılan / Kıvrım Akını (S-Curve)
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
        // 4'lü Çift Kanat Kıskaç Taarruzu (Pincer Dive)
        _currentWaveTotalCount = 4;

        // Sol kanattan sağ alta dalış
        Vector2[] leftSpawns = new Vector2[] { new Vector2(-2.6f, 6.4f), new Vector2(-3.2f, 7.1f) };
        Vector2 leftDir = new Vector2(1.2f, -3.5f).normalized;

        // Sağ kanattan sol alta dalış
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
        // 2 Ağır Zırhlı Öncü + 2 Plazma Bombacısı Eskort
        _currentWaveTotalCount = 4;

        Vector2[] heavyPos = new Vector2[] { new Vector2(-1.35f, 6.4f), new Vector2(1.35f, 6.4f) };
        for (int i = 0; i < heavyPos.Length; i++)
        {
            SwarmEnemy heavy = CreateDroneObject("SwarmDrone_Heavy_" + i, heavyPos[i]);
            heavy.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed * 0.65f, hp, droneSprite, null, SwarmEnemy.DroneClass.HeavyShielded);
            _activeWaveEnemies.Add(heavy);
        }

        yield return new WaitForSeconds(0.55f);
        if (IsGamePausedOrEnded()) yield break;

        Vector2[] bomberPos = new Vector2[] { new Vector2(-2.6f, 6.4f), new Vector2(2.6f, 6.4f) };
        for (int i = 0; i < bomberPos.Length; i++)
        {
            SwarmEnemy bomber = CreateDroneObject("SwarmDrone_Bomber_" + i, bomberPos[i]);
            bomber.Initialize(SwarmEnemy.MovementPattern.SineWave, speed * 0.85f, hp, droneSprite, null, SwarmEnemy.DroneClass.PlasmaBomber);
            _activeWaveEnemies.Add(bomber);
        }
    }

    /// <summary>
    /// Boss Faz 2 veya seçkin dalgalar sırasında boss'un yan kanatlarından eskort İHA'lar konuşlandırır.
    /// </summary>
    public void SpawnEscortWingmen(Vector3 bossPos, int waveIndex)
    {
        if (IsGamePausedOrEnded()) return;

        float hp = 26f + Mathf.Min(waveIndex * 2.5f, 40f);
        float speed = 4.2f + Mathf.Min(waveIndex * 0.1f, 1.5f);
        Sprite droneSprite = GetRandomDroneSprite();

        float[] offsetsX = new float[] { -1.6f, 1.6f };
        for (int i = 0; i < offsetsX.Length; i++)
        {
            float spawnX = Mathf.Clamp(bossPos.x + offsetsX[i], -3.6f, 3.6f);
            Vector2 spawnPos = new Vector2(spawnX, Mathf.Max(bossPos.y + 0.5f, 4.8f));
            SwarmEnemy wingman = CreateDroneObject("BossEscort_" + i, spawnPos);
            SwarmEnemy.DroneClass cls = (i == 0) ? SwarmEnemy.DroneClass.HeavyShielded : SwarmEnemy.DroneClass.Kamikaze;
            wingman.Initialize(SwarmEnemy.MovementPattern.StraightDown, speed, hp, droneSprite, null, cls);
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
        if (_activeWaveEnemies.Contains(enemy))
        {
            _activeWaveEnemies.Remove(enemy);
            _currentWaveKilledCount++;

            // Tüm filo başarıyla imha edildi mi?
            if (_currentWaveKilledCount >= _currentWaveTotalCount && _currentWaveTotalCount > 0)
            {
                OnFormationWipedOutBonus();
            }
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
        // Mükemmel Temizlik Bonusu
        int bonusGold = 50;
        PlayerUpgradeManager.Instance?.AddGold(bonusGold);
        AudioManager.Instance?.PlayRewardFanfare();

        GameUI ui = FindObjectOfType<GameUI>();
        ui?.ShowWaveAlert(">> FİLO TAMAMEN İMHA EDİLDİ! (+50 ALTIN) <<", new Color(0.2f, 1f, 0.45f), 2.5f);
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

