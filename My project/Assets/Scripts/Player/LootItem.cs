using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Düşman uçaklarından düşen ve sahada süzülen yüksek çözünürlüklü taktik ganimet / güçlendirme küresi:
/// - Health (Gövde Tamiri): +35 Can Onarımı, Askeri Yeşil Tamir Kiti simgesi ve [ +TAMİR ] etiketi.
/// - Shield (Plazma Kalkan): +60 Plazma Enerji Kalkanı, Fütüristik Kalkan simgesi ve [ KALKAN ] etiketi.
/// - Overdrive (Süper Ateş): 7 Saniye 3'lü Hiper Seri Ateş, Alevli Mühimmat Sandığı ve [ SÜPER ATEŞ ] etiketi.
/// - Gold (Pilot As Altını): +50 Hangar Altını, 3D Kanatlı Askeri Madalyon ve [ +50 ALTIN ] etiketi.
/// </summary>
public class LootItem : MonoBehaviour
{
    public enum LootType
    {
        Health,
        Shield,
        Overdrive,
        Gold,
        BronzeKey,
        SilverKey,
        GoldKey
    }

    public LootType type = LootType.Health;
    public float floatSpeed = 0.95f;
    public float magnetRadius = 2.8f;
    public float lifetime = 14f;

    private Transform _playerTransform;
    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private SpriteRenderer _haloSr;
    private TextMeshPro _labelTMP;
    private Color _themeColor = Color.green;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 0.46f;
        col.isTrigger = true;
    }

    private void Start()
    {
        FindPlayer();
        SetupVisuals();
        SetupFloatingTag();
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (_playerTransform == null || !_playerTransform.gameObject.activeInHierarchy)
            FindPlayer();

        // Aşağı doğru hafif süzülme
        Vector2 velocity = new Vector2(0f, -floatSpeed);

        // Manyetik Çekim: Oyuncuya yaklaştığında hızlanarak oyuncuya doğru akar
        if (_playerTransform != null)
        {
            float dist = Vector2.Distance(transform.position, _playerTransform.position);
            if (dist < magnetRadius)
            {
                Vector2 dir = ((Vector2)_playerTransform.position - _rb.position).normalized;
                velocity = Vector2.Lerp(velocity, dir * 5.2f, (magnetRadius - dist) / magnetRadius);
            }
        }

        _rb.linearVelocity = velocity;

        // Nefes alma / nabız animasyonu
        float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.08f;
        if (_sr != null)
        {
            _sr.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        // Parıltı halesi nabız efekti
        if (_haloSr != null)
        {
            float haloPulse = 1.25f + Mathf.Sin(Time.time * 6f) * 0.18f;
            _haloSr.transform.localScale = new Vector3(haloPulse, haloPulse, 1f);
            float alpha = 0.35f + Mathf.Sin(Time.time * 6f) * 0.20f;
            Color c = _themeColor;
            c.a = Mathf.Clamp01(alpha);
            _haloSr.color = c;
        }

        // Etiket yukarı-aşağı hafif süzülüşü (bobbing)
        if (_labelTMP != null)
        {
            float bob = 0.46f + Mathf.Sin(Time.time * 4f) * 0.04f;
            _labelTMP.transform.localPosition = new Vector3(0f, bob, 0f);
        }
    }

    private void FindPlayer()
    {
        var planes = FindObjectsOfType<PlaneController>();
        foreach (var p in planes)
        {
            if (p.isPlayer1)
            {
                _playerTransform = p.transform;
                break;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlaneController pc = other.GetComponent<PlaneController>();
        if (pc == null || !pc.isPlayer1) return;

        PlaneHealth health = other.GetComponent<PlaneHealth>();
        PlaneShooter shooter = other.GetComponent<PlaneShooter>();
        var gameUI = FindObjectOfType<GameUI>();

        switch (type)
        {
            case LootType.Health:
                health?.Heal(35f);
                gameUI?.ShowWaveAlert("[+] <b>[ +35 GÖVDE TAMİRİ EDİLDİ ]</b>", new Color(0.1f, 1f, 0.4f), 1.4f);
                break;

            case LootType.Shield:
                PlayerUpgradeManager.Instance?.GrantShield(60f);
                gameUI?.ShowWaveAlert("[KALKAN] <b>[ +60 PLAZMA ENERJİ KALKANI AKTİF ]</b>", new Color(0f, 0.9f, 1f), 1.4f);
                break;

            case LootType.Overdrive:
                shooter?.ActivateOverdrive(7.0f);
                gameUI?.ShowWaveAlert("[AŞIRI YÜK] <b>[ 3'LÜ HİPER ATEŞ (7s) ]</b>", new Color(1f, 0.85f, 0.1f), 1.6f);
                break;

            case LootType.Gold:
                int dropGold = 50;
                PlayerUpgradeManager.Instance?.AddGold(dropGold);
                gameUI?.ShowWaveAlert($"[ALTIN] <b>[ +{dropGold} HANGAR ALTINI KAZANILDI ]</b>", new Color(1f, 0.88f, 0.2f), 1.4f);
                break;

            case LootType.BronzeKey:
                HangarManager.EnsureInstance()?.AddKey(HangarManager.KeyType.Bronze, 1);
                gameUI?.ShowWaveAlert("[ANAHTAR] <b>[ BRONZ SANDIK ANAHTARI KAZANILDI! ]</b>", new Color(0.85f, 0.55f, 0.25f), 2.5f);
                break;

            case LootType.SilverKey:
                HangarManager.EnsureInstance()?.AddKey(HangarManager.KeyType.Silver, 1);
                gameUI?.ShowWaveAlert("[ANAHTAR] <b>[ GÜMÜŞ CEPHANE ANAHTARI KAZANILDI! ]</b>", new Color(0.35f, 0.80f, 1f), 2.8f);
                break;

            case LootType.GoldKey:
                HangarManager.EnsureInstance()?.AddKey(HangarManager.KeyType.Gold, 1);
                gameUI?.ShowWaveAlert("[EFSANEVİ] <b>[ ALTIN AS ANAHTARI DÜŞTÜ! ]</b>", new Color(1f, 0.85f, 0.15f), 3.2f);
                CameraShake.Shake(0.3f, 0.12f);
                break;
        }

        DailyOpsManager.EnsureInstance()?.RecordEvent(DailyOpsManager.OpsEventType.LootCollected, 1);

        // Parlak kıvılcım & ses efekti
        VFXManager.SpawnHitSparks(transform.position, _themeColor);
        bool isKeyLoot = (type == LootType.GoldKey || type == LootType.SilverKey || type == LootType.BronzeKey);
        AudioManager.Instance?.PlayLootPickup(isKeyLoot);

        Destroy(gameObject);
    }

    private void SetupVisuals()
    {
        // 1. Ana Sprite Renderer
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

        string resName = "";
        switch (type)
        {
            case LootType.Health:
                _themeColor = new Color(0.1f, 1f, 0.4f);
                resName = "loot_health";
                break;
            case LootType.Shield:
                _themeColor = new Color(0f, 0.88f, 1f);
                resName = "loot_shield";
                break;
            case LootType.Overdrive:
                _themeColor = new Color(1f, 0.70f, 0.1f);
                resName = "loot_overdrive";
                break;
            case LootType.Gold:
                _themeColor = new Color(1f, 0.85f, 0.15f);
                resName = "loot_gold";
                break;
            case LootType.BronzeKey:
                _themeColor = new Color(0.85f, 0.50f, 0.20f);
                resName = "loot_gold";
                break;
            case LootType.SilverKey:
                _themeColor = new Color(0.70f, 0.85f, 0.98f);
                resName = "loot_shield";
                break;
            case LootType.GoldKey:
                _themeColor = new Color(1f, 0.85f, 0.15f);
                resName = "loot_gold";
                break;
        }

        // Resources klasöründen yüksek kaliteli saydam sprite'ı yükle
        Sprite loadedSpr = Resources.Load<Sprite>(resName);
        if (loadedSpr != null)
        {
            _sr.sprite = loadedSpr;
            // 128x128 sprite'ı sahneye mükemmel oranda oturt (0.68f)
            transform.localScale = new Vector3(0.68f, 0.68f, 1f);
        }
        else
        {
            // Eğer kaynak bulunamazsa şık prosedürel çizim devreye girer
            GenerateProceduralFallback();
        }

        _sr.sortingOrder = 16;

        // 2. Arka Plan Parlayan Enerji Halesi (Halo)
        GameObject haloObj = new GameObject("HaloAura");
        haloObj.transform.SetParent(transform, false);
        haloObj.transform.localPosition = Vector3.zero;

        _haloSr = haloObj.AddComponent<SpriteRenderer>();
        _haloSr.sprite = CreateGlowCircleSprite();
        _haloSr.sortingOrder = 14; // Ana sprite'ın arkasında
        Color haloC = _themeColor;
        haloC.a = 0.45f;
        _haloSr.color = haloC;
        _haloSr.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
    }

    /// <summary>
    /// Eşyanın hemen üstünde beliren ve havada ne olduğunu 100% net gösteren dünya uzayı metin etiketi.
    /// </summary>
    private void SetupFloatingTag()
    {
        GameObject tagObj = new GameObject("LootTag");
        tagObj.transform.SetParent(transform, false);
        tagObj.transform.localPosition = new Vector3(0f, 0.46f, 0f);

        _labelTMP = tagObj.AddComponent<TextMeshPro>();
        _labelTMP.alignment = TextAlignmentOptions.Center;
        _labelTMP.fontSize = 2.4f;
        _labelTMP.fontStyle = FontStyles.Bold;
        _labelTMP.sortingOrder = 22;
        _labelTMP.extraPadding = true;
        _labelTMP.outlineWidth = 0.28f;
        _labelTMP.outlineColor = Color.black;

        switch (type)
        {
            case LootType.Health:
                _labelTMP.text = "<color=#00FF66><b>[ +TAMİR ]</b></color>";
                break;
            case LootType.Shield:
                _labelTMP.text = "<color=#00E5FF><b>[ KALKAN ]</b></color>";
                break;
            case LootType.Overdrive:
                _labelTMP.text = "<color=#FFA000><b>[ SÜPER ATEŞ ]</b></color>";
                break;
            case LootType.Gold:
                _labelTMP.text = "<color=#FFD700><b>[ +50 ALTIN ]</b></color>";
                break;
            case LootType.BronzeKey:
                _labelTMP.text = "<color=#CD7F32><b>[ BRONZ ANAHTAR ]</b></color>";
                break;
            case LootType.SilverKey:
                _labelTMP.text = "<color=#C0C0C0><b>[ GÜMÜŞ ANAHTAR ]</b></color>";
                break;
            case LootType.GoldKey:
                _labelTMP.text = "<color=#FFD700><b>[ ALTIN ANAHTAR ]</b></color>";
                break;
        }

        // Metin boyutu dengesi
        RectTransform rt = tagObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(3.5f, 1f);
        }
    }

    private static Sprite _cachedGlowCircle;
    private static Sprite CreateGlowCircleSprite()
    {
        if (_cachedGlowCircle != null) return _cachedGlowCircle;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist > radius)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float factor = 1f - (dist / radius);
                    float alpha = Mathf.SmoothStep(0f, 1f, factor * factor);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        tex.Apply();
        _cachedGlowCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        return _cachedGlowCircle;
    }

    private void GenerateProceduralFallback()
    {
        int size = 48;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist > radius)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    tex.SetPixel(x, y, _themeColor);
                }
            }
        }
        tex.Apply();
        _sr.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 48f);
    }
}
