using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Çevresel Hava Sahası Olayları ve Arena Tehlikeleri Yöneticisi:
/// 1. Yan Rüzgar Fırtınası (CrosswindStorm): Yatay rüzgar akıntısı ve rüzgar partikülleri.
/// 2. Elektromanyetik Yıldırım Koridoru (LightningCorridor): 1.6 sn ön uyarılı dikey şimşek şeritleri (hem oyuncuyu hem düşmanı çarpabilir).
/// 3. Taktik Hava Mayını Yağmuru (SkyMineDrop): Vurularak altın/combo alınabilen veya sıyrılınabilen hava mayınları.
/// 4. İyon Enerji Koridoru (IonUpdraftBuff): İçinde kalan oyuncuya +Ulti enerjisi ve +Kalkan/Tamir sağlayan fırsat akımı.
/// </summary>
public class AirspaceHazardDirector : MonoBehaviour
{
    public static AirspaceHazardDirector Instance { get; private set; }

    public enum AirspaceEventType
    {
        None,
        CrosswindStorm,
        LightningCorridor,
        SkyMineDrop,
        IonUpdraftBuff
    }

    [Header("Olay Zamanlaması")]
    public float initialDelay = 10.0f;
    public float minEventInterval = 13.0f;
    public float maxEventInterval = 18.0f;

    public AirspaceEventType ActiveEvent { get; private set; } = AirspaceEventType.None;
    public string ActiveBannerText { get; private set; } = string.Empty;
    public Color ActiveBannerColor { get; private set; } = Color.cyan;
    public bool HasActiveBanner => !string.IsNullOrEmpty(ActiveBannerText);

    private Coroutine _directorCoroutine;
    private Coroutine _activeEventCoroutine;
    private bool _isRunning = false;
    private readonly List<GameObject> _spawnedVisuals = new List<GameObject>();
    private int _lastEventIndex = -1;

    private static Texture2D _solidTex;
    private static Sprite _solidSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public static AirspaceHazardDirector EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("AirspaceHazardDirector");
        if (go == null)
            go = new GameObject("AirspaceHazardDirector");

        Instance = go.GetComponent<AirspaceHazardDirector>();
        if (Instance == null)
            Instance = go.AddComponent<AirspaceHazardDirector>();

        return Instance;
    }

    public void StartDirector()
    {
        if (_isRunning) return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer)
            return;

        _isRunning = true;
        _directorCoroutine = StartCoroutine(DirectorLoopRoutine());
    }

    public void StopDirector()
    {
        _isRunning = false;
        if (_directorCoroutine != null)
        {
            StopCoroutine(_directorCoroutine);
            _directorCoroutine = null;
        }
        if (_activeEventCoroutine != null)
        {
            StopCoroutine(_activeEventCoroutine);
            _activeEventCoroutine = null;
        }

        ClearActiveState();
    }

    private void ClearActiveState()
    {
        ActiveEvent = AirspaceEventType.None;
        ActiveBannerText = string.Empty;
        ApplyPlayerWindDrift(0f);

        for (int i = _spawnedVisuals.Count - 1; i >= 0; i--)
        {
            if (_spawnedVisuals[i] != null)
                Destroy(_spawnedVisuals[i]);
        }
        _spawnedVisuals.Clear();

        SkyMineHazard[] leftoverMines = FindObjectsOfType<SkyMineHazard>();
        for (int i = 0; i < leftoverMines.Length; i++)
        {
            if (leftoverMines[i] != null)
                Destroy(leftoverMines[i].gameObject);
        }
    }

    private void Update()
    {
        if (_isRunning && GameManager.Instance != null && GameManager.Instance.IsGameEnded)
        {
            StopDirector();
        }
    }

    private IEnumerator DirectorLoopRoutine()
    {
        float firstWait = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.BulletHell)
            ? 6.0f
            : initialDelay;
        yield return new WaitForSeconds(firstWait);

        while (_isRunning)
        {
            if (IsPausedOrUnavailable())
            {
                yield return new WaitForSeconds(1.0f);
                continue;
            }

            int eventIdx = PickNextEventIndex();
            switch (eventIdx)
            {
                case 0:
                    _activeEventCoroutine = StartCoroutine(CrosswindStormRoutine());
                    break;
                case 1:
                    _activeEventCoroutine = StartCoroutine(LightningCorridorRoutine());
                    break;
                case 2:
                    _activeEventCoroutine = StartCoroutine(SkyMineDropRoutine());
                    break;
                default:
                    _activeEventCoroutine = StartCoroutine(IonUpdraftRoutine());
                    break;
            }

            yield return _activeEventCoroutine;
            _activeEventCoroutine = null;

            float cooldown = Random.Range(minEventInterval, maxEventInterval);
            if (GameManager.Instance != null &&
                (GameManager.Instance.CurrentMode == GameManager.GameMode.BulletHell ||
                 GameManager.Instance.CurrentMode == GameManager.GameMode.Hardcore))
            {
                cooldown *= 0.80f;
            }
            yield return new WaitForSeconds(cooldown);
        }
    }

    private int PickNextEventIndex()
    {
        int pick;
        int guard = 0;
        do
        {
            // Pozitif iyon koridoru biraz daha nadir olsun
            float r = Random.value;
            if (r < 0.28f) pick = 0;       // Crosswind
            else if (r < 0.56f) pick = 1;  // Lightning
            else if (r < 0.84f) pick = 2;  // Sky Mines
            else pick = 3;                 // Ion Updraft
            guard++;
        }
        while (pick == _lastEventIndex && guard < 6);

        _lastEventIndex = pick;
        return pick;
    }

    private bool IsPausedOrUnavailable()
    {
        if (Time.timeScale <= 0.01f) return true;
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.IsGameEnded) return true;
        if (gm.CurrentMode == GameManager.GameMode.TwoPlayer) return true;
        if (gm.plane1Health == null || gm.plane1Health.IsDead) return true;
        return false;
    }

    private void GetPlayfieldBoundsX(out float minX, out float maxX)
    {
        GameManager gm = GameManager.Instance;
        PlaneController p1 = (gm != null && gm.plane1Health != null) ? gm.plane1Health.GetComponent<PlaneController>() : null;
        if (p1 != null)
        {
            minX = p1.minX;
            maxX = p1.maxX;
            return;
        }
        minX = -6.8f;
        maxX = 6.8f;
    }

    private void ApplyPlayerWindDrift(float windX)
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.plane1Health != null)
        {
            PlaneController pc = gm.plane1Health.GetComponent<PlaneController>();
            if (pc != null)
                pc.externalDriftX = windX;
        }
    }

    // =========================================================
    // 1. YAN RÜZGAR FIRTINASI (Crosswind Storm)
    // =========================================================
    private IEnumerator CrosswindStormRoutine()
    {
        ActiveEvent = AirspaceEventType.CrosswindStorm;
        float dir = Random.value < 0.5f ? 1f : -1f;
        string arrow = dir > 0f ? ">>> SAĞA" : "<<< SOLA";

        ActiveBannerColor = new Color(0.45f, 0.88f, 1f);
        ActiveBannerText = $"[UYARI] YAN RÜZGAR AKINTISI ({arrow})";
        AudioManager.Instance?.PlayWarningAlarm();

        // 1.4 sn telegraphed rüzgar partikülü
        GameObject windFx = SpawnWindParticles(dir);
        yield return new WaitForSeconds(1.4f);

        float duration = 7.0f;
        float elapsed = 0f;
        float windSpeed = dir * 2.05f;

        while (elapsed < duration && !IsPausedOrUnavailable())
        {
            elapsed += Time.deltaTime;
            float rem = Mathf.Max(0f, duration - elapsed);
            ActiveBannerText = $"[RÜZGAR] {arrow} AKINTI • {rem:0.0}s";
            ApplyPlayerWindDrift(windSpeed);
            yield return null;
        }

        ApplyPlayerWindDrift(0f);
        if (windFx != null)
        {
            _spawnedVisuals.Remove(windFx);
            Destroy(windFx);
        }

        ActiveEvent = AirspaceEventType.None;
        ActiveBannerText = string.Empty;
    }

    // =========================================================
    // 2. ELEKTROMANYETİK YILDIRIM KORİDORU (Lightning Corridor)
    // =========================================================
    private IEnumerator LightningCorridorRoutine()
    {
        ActiveEvent = AirspaceEventType.LightningCorridor;
        GetPlayfieldBoundsX(out float minX, out float maxX);

        float lane1 = Random.Range(minX + 0.9f, maxX - 0.9f);
        float lane2 = lane1;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float candidate = Random.Range(minX + 0.9f, maxX - 0.9f);
            if (Mathf.Abs(candidate - lane1) >= 2.8f)
            {
                lane2 = candidate;
                break;
            }
        }

        float[] lanes = (Mathf.Abs(lane2 - lane1) >= 2.2f)
            ? new float[] { lane1, lane2 }
            : new float[] { lane1 };

        ActiveBannerColor = new Color(1f, 0.88f, 0.20f);
        ActiveBannerText = "[TEHLİKE] ŞİMŞEK KORİDORU TESPİT EDİLDİ!";
        AudioManager.Instance?.PlayWarningAlarm();

        List<GameObject> telegraphs = new List<GameObject>();
        List<SpriteRenderer> telegraphSprites = new List<SpriteRenderer>();
        for (int i = 0; i < lanes.Length; i++)
        {
            GameObject strip = CreateVerticalStripVisual(
                $"LightningTelegraph_{i}",
                lanes[i],
                1.35f,
                new Color(1f, 0.85f, 0.15f, 0.22f),
                order: 3
            );
            telegraphs.Add(strip);
            telegraphSprites.Add(strip.GetComponent<SpriteRenderer>());
        }

        float warnDuration = 1.65f;
        float elapsed = 0f;
        while (elapsed < warnDuration)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameEnded) break;
            elapsed += Time.deltaTime;
            float rem = Mathf.Max(0f, warnDuration - elapsed);
            ActiveBannerText = $"[YILDIRIM] ŞERİTLERDEN ÇIK! ({rem:0.0}s)";

            float pulse = Mathf.PingPong(elapsed * 9f, 1f);
            for (int i = 0; i < telegraphSprites.Count; i++)
            {
                if (telegraphSprites[i] != null)
                    telegraphSprites[i].color = new Color(1f, Mathf.Lerp(0.85f, 0.35f, pulse), 0.12f, Mathf.Lerp(0.14f, 0.42f, pulse));
            }
            yield return null;
        }

        // Telegrapları temizle ve gerçek yıldırım çakmasını başlat
        for (int i = 0; i < telegraphs.Count; i++)
        {
            if (telegraphs[i] != null)
            {
                _spawnedVisuals.Remove(telegraphs[i]);
                Destroy(telegraphs[i]);
            }
        }

        if (!IsPausedOrUnavailable())
        {
            ActiveBannerColor = new Color(0.55f, 0.95f, 1f);
            ActiveBannerText = "[YILDIRIM] YÜKSEK GERİLİM DARBESİ!";
            AudioManager.Instance?.PlayThunder();
            CameraShake.Shake(0.24f, 0.26f);

            List<GameObject> bolts = new List<GameObject>();
            for (int i = 0; i < lanes.Length; i++)
            {
                bolts.Add(SpawnLightningBoltVisual(lanes[i]));
            }

            EvaluateLightningHits(lanes);
            yield return new WaitForSeconds(0.42f);

            for (int i = 0; i < bolts.Count; i++)
            {
                if (bolts[i] != null)
                {
                    _spawnedVisuals.Remove(bolts[i]);
                    Destroy(bolts[i]);
                }
            }
        }

        ActiveEvent = AirspaceEventType.None;
        ActiveBannerText = string.Empty;
    }

    private void EvaluateLightningHits(float[] lanes)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        const float hitHalfWidth = 0.72f;
        const float grazeOuterWidth = 1.50f;

        if (gm.plane1Health != null && !gm.plane1Health.IsDead)
        {
            float px = gm.plane1Health.transform.position.x;
            bool hit = false;
            bool closeEscape = false;

            for (int i = 0; i < lanes.Length; i++)
            {
                float dx = Mathf.Abs(px - lanes[i]);
                if (dx <= hitHalfWidth) hit = true;
                else if (dx <= grazeOuterWidth) closeEscape = true;
            }

            if (hit && !gm.plane1Health.isInvulnerable)
            {
                gm.plane1Health.TakeDamage(18f, false, gm.plane1Health.transform.position);
            }
            else if ((hit && gm.plane1Health.isInvulnerable) || closeEscape)
            {
                CombatComboManager.EnsureInstance().RegisterBulletGraze(
                    new Vector3(px, gm.plane1Health.transform.position.y + 0.5f, 0f),
                    gm.plane1Health.isInvulnerable
                );
            }
        }

        // Yıldırım düşman uçağını da yakalarsa taktik hasar verir!
        if (gm.plane2Health != null && !gm.plane2Health.IsDead)
        {
            float ex = gm.plane2Health.transform.position.x;
            for (int i = 0; i < lanes.Length; i++)
            {
                if (Mathf.Abs(ex - lanes[i]) <= 0.95f)
                {
                    gm.plane2Health.TakeDamage(35f, true, gm.plane2Health.transform.position);
                    break;
                }
            }
        }
    }

    // =========================================================
    // 3. TAKTİK HAVA MAYINI YAĞMURU (Sky Mine Drop)
    // =========================================================
    private IEnumerator SkyMineDropRoutine()
    {
        ActiveEvent = AirspaceEventType.SkyMineDrop;
        GetPlayfieldBoundsX(out float minX, out float maxX);

        ActiveBannerColor = new Color(1f, 0.42f, 0.22f);
        ActiveBannerText = "[TEHLİKE] HAVA MAYINI YAĞMURU (VUR VEYA KAÇIN!)";
        AudioManager.Instance?.PlayWarningAlarm();

        int count = Random.Range(3, 5);
        List<float> dropXs = new List<float>();
        List<GameObject> laneHints = new List<GameObject>();

        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(minX + 0.9f, maxX - 0.9f, (i + 0.5f) / count) + Random.Range(-0.65f, 0.65f);
            x = Mathf.Clamp(x, minX + 0.6f, maxX - 0.6f);
            dropXs.Add(x);

            GameObject hint = CreateVerticalStripVisual(
                $"MineHint_{i}",
                x,
                0.55f,
                new Color(1f, 0.28f, 0.14f, 0.20f),
                order: 2
            );
            laneHints.Add(hint);
        }

        yield return new WaitForSeconds(1.05f);

        for (int i = 0; i < laneHints.Count; i++)
        {
            if (laneHints[i] != null)
            {
                _spawnedVisuals.Remove(laneHints[i]);
                Destroy(laneHints[i]);
            }
        }

        for (int i = 0; i < dropXs.Count; i++)
        {
            if (IsPausedOrUnavailable()) break;

            GameObject mineObj = new GameObject($"SkyMine_{i}");
            mineObj.transform.position = new Vector3(dropXs[i], 5.65f, 0f);
            SkyMineHazard mine = mineObj.AddComponent<SkyMineHazard>();
            mine.Initialize(Random.Range(4.1f, 5.1f), 42f, 20f);

            yield return new WaitForSeconds(0.45f);
        }

        yield return new WaitForSeconds(1.2f);
        ActiveEvent = AirspaceEventType.None;
        ActiveBannerText = string.Empty;
    }

    // =========================================================
    // 4. İYON ENERJİ KORİDORU (Ion Updraft Buff)
    // =========================================================
    private IEnumerator IonUpdraftRoutine()
    {
        ActiveEvent = AirspaceEventType.IonUpdraftBuff;
        GetPlayfieldBoundsX(out float minX, out float maxX);

        float zoneX = Random.Range(minX + 1.2f, maxX - 1.2f);
        ActiveBannerColor = new Color(0.20f, 1f, 0.65f);
        ActiveBannerText = "[FIRSAT] İYON ENERJİ KORİDORU (+ULTİ & TAMİR)";

        GameObject corridorObj = CreateVerticalStripVisual(
            "IonUpdraftZone",
            zoneX,
            1.95f,
            new Color(0.15f, 1f, 0.68f, 0.20f),
            order: 3
        );
        SpriteRenderer sr = corridorObj.GetComponent<SpriteRenderer>();

        float duration = 7.0f;
        float elapsed = 0f;
        float driftDir = (zoneX > 0f) ? -0.45f : 0.45f;
        float healAccum = 0f;

        while (elapsed < duration && !IsPausedOrUnavailable())
        {
            elapsed += Time.deltaTime;
            zoneX = Mathf.Clamp(zoneX + driftDir * Time.deltaTime, minX + 1.0f, maxX - 1.0f);
            if (corridorObj != null)
                corridorObj.transform.position = new Vector3(zoneX, 0f, 0f);

            float rem = Mathf.Max(0f, duration - elapsed);
            bool playerInside = false;

            GameManager gm = GameManager.Instance;
            if (gm != null && gm.plane1Health != null && !gm.plane1Health.IsDead)
            {
                playerInside = Mathf.Abs(gm.plane1Health.transform.position.x - zoneX) <= 1.08f;
                if (playerInside)
                {
                    PlayerCombatManeuvers.Instance?.AddUltiEnergy(Time.deltaTime * 5.5f);
                    CombatComboManager.Instance?.RefreshComboTimer(3.0f);

                    healAccum += Time.deltaTime * 3.5f;
                    if (healAccum >= 1f)
                    {
                        int healInt = Mathf.FloorToInt(healAccum);
                        healAccum -= healInt;
                        gm.plane1Health.Heal(healInt);
                    }
                }
            }

            if (sr != null)
            {
                float pulse = Mathf.PingPong(elapsed * 4.5f, 1f);
                sr.color = playerInside
                    ? new Color(0.28f, 1f, 0.72f, Mathf.Lerp(0.28f, 0.45f, pulse))
                    : new Color(0.14f, 0.92f, 0.62f, Mathf.Lerp(0.14f, 0.26f, pulse));
            }

            ActiveBannerText = playerInside
                ? $"[İYON AKTİF] +ULTİ & TAMİR KAZANILIYOR! ({rem:0.0}s)"
                : $"[FIRSAT] İYON KORİDORUNA GİR! (+ULTİ & TAMİR) ({rem:0.0}s)";

            yield return null;
        }

        if (corridorObj != null)
        {
            _spawnedVisuals.Remove(corridorObj);
            Destroy(corridorObj);
        }

        ActiveEvent = AirspaceEventType.None;
        ActiveBannerText = string.Empty;
    }

    // =========================================================
    // PROSEDÜREL GÖRSELLER
    // =========================================================
    private GameObject CreateVerticalStripVisual(string objName, float xPos, float width, Color color, int order)
    {
        GameObject obj = new GameObject(objName);
        obj.transform.position = new Vector3(xPos, 0f, 0f);
        obj.transform.localScale = new Vector3(width, 12.0f, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = GetSolidSprite();
        sr.color = color;
        sr.sortingOrder = order;

        _spawnedVisuals.Add(obj);
        return obj;
    }

    private GameObject SpawnLightningBoltVisual(float xPos)
    {
        GameObject boltObj = new GameObject("LightningBolt");
        LineRenderer lr = boltObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sortingOrder = 18;
        lr.startWidth = 0.24f;
        lr.endWidth = 0.14f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0.75f, 0.96f, 1f, 1f);
        lr.endColor = new Color(1f, 0.95f, 0.45f, 0.95f);

        const int segments = 12;
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / (segments - 1);
            float y = Mathf.Lerp(5.8f, -5.8f, t);
            float jitterX = (i == 0 || i == segments - 1) ? 0f : Random.Range(-0.35f, 0.35f);
            lr.SetPosition(i, new Vector3(xPos + jitterX, y, 0f));
        }

        VFXManager.SpawnHitSparks(new Vector3(xPos, -3.5f, 0f), new Color(0.55f, 0.95f, 1f));
        _spawnedVisuals.Add(boltObj);
        return boltObj;
    }

    private GameObject SpawnWindParticles(float dir)
    {
        GameObject windObj = new GameObject("CrosswindParticles");
        windObj.transform.position = new Vector3(dir > 0f ? -9.0f : 9.0f, 0f, 0f);

        ParticleSystem ps = windObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = windObj.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            Material m = new Material(shader);
            psr.material = m;
        }
        psr.sortingOrder = 14;

        var main = ps.main;
        main.startLifetime = 1.15f;
        main.startSpeed = 16.5f;
        main.startSize = 0.12f;
        main.startColor = new Color(0.70f, 0.92f, 1f, 0.55f);
        main.maxParticles = 90;

        var emission = ps.emission;
        emission.rateOverTime = 38f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.5f, 10.5f, 0.1f);
        windObj.transform.rotation = Quaternion.Euler(0f, 0f, dir > 0f ? -90f : 90f);

        var tr = ps.trails;
        tr.enabled = true;
        tr.ratio = 1f;
        tr.lifetime = 0.18f;
        tr.widthOverTrail = 0.45f;

        _spawnedVisuals.Add(windObj);
        return windObj;
    }

    private static Sprite GetSolidSprite()
    {
        if (_solidSprite != null) return _solidSprite;

        _solidTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] cols = new Color[16];
        for (int i = 0; i < 16; i++) cols[i] = Color.white;
        _solidTex.SetPixels(cols);
        _solidTex.Apply();

        _solidSprite = Sprite.Create(_solidTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _solidSprite;
    }

    private void OnDisable()
    {
        StopDirector();
    }
}
