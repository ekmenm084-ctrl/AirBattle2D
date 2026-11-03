using System.Collections;
using UnityEngine;

/// <summary>
/// Oyuncu uçağının kanatlarında uçan Destek Dronu (Wingman İHA):
/// 5 Aşamalı Güç Düzeyine Sahip Modeller:
/// - MK-1 İZCİ: Hafif Keşif İHA (Seri hafif mermiler)
/// - MK-2 AVCI: Hızlı Avcı İHA (Seri çift siber lazer)
/// - MK-3 TAARRUZ: Ağır Taarruz Platformu (Ağır kinetik delici mermi)
/// - MK-4 VALKYRIE: Güdümlü Taarruz İHA (Akıllı mikro roketler)
/// - MK-5 TİTAN AEGIS: Apex Nihai İHA (Ölümcül plazma salvosu + mermi yutan enerji kalkanı)
///
/// Can / Dayanıklılık Sistemi:
/// - Düşman mermilerini emer, çatışmada yıpranır.
/// - Canı bitince parçalanır ve kanattan düşer; Hangar'dan tekrar satın alınabilir!
/// </summary>
public class DroneCompanion : MonoBehaviour
{
    public enum DroneType
    {
        None = 0,
        MK1_Scout = 1,
        MK2_Interceptor = 2,
        MK3_Striker = 3,
        MK4_Missile = 4,
        MK5_TitanAegis = 5,

        // Geriye dönük uyumluluk takma adları:
        Laser = 2,
        Missile = 4,
        Shield = 5
    }

    [Header("Drone Ayarları")]
    public DroneType droneType = DroneType.MK1_Scout;
    public float maxHealth = 90f;
    public float currentHealth = 90f;
    public bool isLeftWing = true;

    private Transform _playerTransform;
    private Vector3 _offset;
    private Vector3 _lastPlayerPos;
    private float _nextFireTime = 0f;
    private float _wearTimer = 0f;
    private SpriteRenderer _sr;
    private SpriteRenderer _flameSr;
    private GameObject _shieldBubble;

    // Mini Can Barı
    private Transform _hpBarRoot;
    private SpriteRenderer _hpFillSr;

    public void Initialize(Transform player, DroneType type, bool left)
    {
        _playerTransform = player;
        droneType = type;
        isLeftWing = left;
        _lastPlayerPos = player != null ? player.position : Vector3.zero;

        maxHealth = HangarManager.GetDroneMaxHealth(droneType);
        currentHealth = HangarManager.Instance != null
            ? HangarManager.Instance.GetDroneHealth(droneType)
            : maxHealth;

        if (currentHealth <= 0f) currentHealth = maxHealth;

        // Sol kanat (-1.60f, -0.22f), Sağ kanat (+1.60f, -0.22f) - Uçağın yanında belirgin ayrık ve sabitlenmiş
        _offset = new Vector3(isLeftWing ? -1.60f : 1.60f, -0.22f, 0f);

        CreateProceduralDroneVisual();
        CreateThrusterVisual();
        CreateMiniHealthBar();
        UpdateHpBarVisual();

        // Mermileri ve darbeleri yakalayabilmek için çarpışma kutusu
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null) box = gameObject.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.9f, 0.9f);
        box.isTrigger = true;

        if (droneType == DroneType.MK5_TitanAegis)
        {
            CreateShieldBubble();
        }
    }

    public static Sprite GetDroneSprite(DroneType type)
    {
        int size = 48;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color clear = Color.clear;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        float center = (size - 1) * 0.5f;

        if (type == DroneType.MK1_Scout)
        {
            // MK-1 İZCİ: Askeri Yeşil & Çelik Gri Kompakt Keşif İHA
            Color green = new Color(0.18f, 0.85f, 0.42f, 1f);
            Color dark = new Color(0.15f, 0.22f, 0.18f, 1f);
            Color steel = new Color(0.6f, 0.7f, 0.65f, 1f);
            Color muzzle = new Color(1f, 0.9f, 0.2f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    // Kompakt üçgen delta kanat
                    if (y >= 14 && y <= 34 && dx <= (y - 12) * 0.55f)
                    {
                        tex.SetPixel(x, y, dx < 3f ? green : steel);
                    }
                    // Burun hafif makineli namlusu
                    if (dx <= 1.5f && y >= 34 && y <= 42)
                    {
                        tex.SetPixel(x, y, muzzle);
                    }
                    // Gövde çekirdeği
                    if (dx <= 3f && y >= 18 && y <= 28)
                    {
                        tex.SetPixel(x, y, dark);
                    }
                }
            }
        }
        else if (type == DroneType.MK2_Interceptor)
        {
            // MK-2 AVCI: Siber Kobalt Mavi Çift Lazer Avcısı
            Color cyan = new Color(0.0f, 0.85f, 1f, 1f);
            Color core = new Color(0.9f, 0.98f, 1f, 1f);
            Color dark = new Color(0.08f, 0.22f, 0.45f, 1f);
            Color gun = new Color(1f, 0.95f, 0.3f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = y - center;

                    // Keskin Açılı İkiz Kanat
                    if (y >= 10 && y <= 38 && dx <= (y - 8) * 0.65f)
                    {
                        tex.SetPixel(x, y, dx < 3f ? core : cyan);
                    }
                    // Gövde Kokpiti
                    if (dx <= 4f && Mathf.Abs(dy) <= 12f)
                    {
                        tex.SetPixel(x, y, dx <= 2f ? core : dark);
                    }
                    // Çift Lazer Namluları
                    if ((Mathf.Abs(dx - 14f) <= 2f) && y >= 20 && y <= 42)
                    {
                        tex.SetPixel(x, y, y >= 38 ? gun : cyan);
                    }
                }
            }
        }
        else if (type == DroneType.MK3_Striker)
        {
            // MK-3 TAARRUZ: Ağır Zırhlı Kızıl & Grafit Taarruz Platformu
            Color red = new Color(0.92f, 0.20f, 0.20f, 1f);
            Color carbon = new Color(0.18f, 0.18f, 0.20f, 1f);
            Color amber = new Color(1f, 0.75f, 0.15f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    // Ağır altıgen gövde
                    if (y >= 10 && y <= 36 && dx <= 18f - Mathf.Abs(y - 23) * 0.5f)
                    {
                        tex.SetPixel(x, y, dx < 5f ? carbon : red);
                    }
                    // Çift ağır kinetik namlu
                    if ((Mathf.Abs(dx - 11f) <= 2.5f) && y >= 24 && y <= 43)
                    {
                        tex.SetPixel(x, y, y >= 39 ? amber : carbon);
                    }
                }
            }
        }
        else if (type == DroneType.MK4_Missile)
        {
            // MK-4 VALKYRIE: Taarruz Turuncusu Güdümlü Füze Platformu
            Color orange = new Color(1f, 0.52f, 0.10f, 1f);
            Color dark = new Color(0.25f, 0.15f, 0.12f, 1f);
            Color missile = new Color(0.95f, 0.2f, 0.2f, 1f);
            Color warhead = new Color(1f, 0.9f, 0.3f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    // Ağır Taarruz Kanatları
                    if (y >= 12 && y <= 34 && dx <= 20f - (34 - y) * 0.4f)
                    {
                        tex.SetPixel(x, y, dx < 5f ? dark : orange);
                    }
                    // İkili Mikro Roket Yuvaları
                    if ((Mathf.Abs(dx - 13f) <= 3f) && y >= 16 && y <= 40)
                    {
                        tex.SetPixel(x, y, y >= 36 ? warhead : (y >= 30 ? missile : dark));
                    }
                }
            }
        }
        else if (type == DroneType.MK5_TitanAegis)
        {
            // MK-5 TİTAN AEGIS: Efsanevi Mor & Altın Zırhlı Apex Koruyucu
            Color purple = new Color(0.68f, 0.35f, 0.90f, 1f);
            Color gold = new Color(1f, 0.82f, 0.20f, 1f);
            Color core = new Color(0.95f, 0.95f, 1f, 1f);
            Color dark = new Color(0.18f, 0.10f, 0.28f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float dx = Mathf.Abs(x - center);

                    // Merkez Çekirdek
                    if (dist <= 7f)
                    {
                        tex.SetPixel(x, y, dist <= 3.5f ? core : dark);
                    }
                    // Altın Kalkan Çerçevesi
                    else if (dist >= 14f && dist <= 19f)
                    {
                        tex.SetPixel(x, y, gold);
                    }
                    // Plazma Emitter Kanatları
                    if ((dx <= 4f && Mathf.Abs(y - center) >= 12f && Mathf.Abs(y - center) <= 22f) ||
                        (Mathf.Abs(y - center) <= 4f && dx >= 12f && dx <= 22f))
                    {
                        tex.SetPixel(x, y, purple);
                    }
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void CreateProceduralDroneVisual()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

        _sr.sprite = GetDroneSprite(droneType);
        _sr.sortingOrder = 5;
        transform.localScale = Vector3.one * 0.95f;
    }

    private void CreateThrusterVisual()
    {
        GameObject flameObj = new GameObject("DroneThrusterFlame");
        flameObj.transform.SetParent(transform, false);
        flameObj.transform.localPosition = new Vector3(0f, -0.42f, 0f);

        _flameSr = flameObj.AddComponent<SpriteRenderer>();
        _flameSr.sortingOrder = 4; // Uçağın ve dronun hemen arkasında

        int fw = 8;
        int fh = 16;
        Texture2D fTex = new Texture2D(fw, fh, TextureFormat.RGBA32, false);
        fTex.filterMode = FilterMode.Bilinear;

        Color flameColor = droneType switch
        {
            DroneType.MK1_Scout       => new Color(0.2f, 0.95f, 0.4f, 0.9f),
            DroneType.MK2_Interceptor => new Color(0.1f, 0.85f, 1f, 0.9f),
            DroneType.MK3_Striker     => new Color(0.95f, 0.25f, 0.2f, 0.9f),
            DroneType.MK4_Missile     => new Color(1f, 0.55f, 0.12f, 0.9f),
            DroneType.MK5_TitanAegis  => new Color(0.75f, 0.35f, 1f, 0.9f),
            _                         => new Color(0.2f, 0.9f, 1f, 0.9f)
        };
        Color whiteCore = new Color(1f, 1f, 0.9f, 1f);

        float cx = (fw - 1) * 0.5f;
        for (int y = 0; y < fh; y++)
        {
            float ty = (float)y / (fh - 1);
            float halfW = Mathf.Lerp(0.8f, cx, ty);
            for (int x = 0; x < fw; x++)
            {
                float dx = Mathf.Abs(x - cx);
                if (dx <= halfW)
                {
                    float factor = 1f - (dx / (halfW + 0.01f));
                    Color c = Color.Lerp(flameColor, whiteCore, factor * ty);
                    c.a *= ty * factor;
                    fTex.SetPixel(x, y, c);
                }
                else
                {
                    fTex.SetPixel(x, y, Color.clear);
                }
            }
        }
        fTex.Apply();
        _flameSr.sprite = Sprite.Create(fTex, new Rect(0, 0, fw, fh), new Vector2(0.5f, 1f), 32f);
    }

    private void CreateMiniHealthBar()
    {
        GameObject barRoot = new GameObject("MiniHpBar");
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        _hpBarRoot = barRoot.transform;

        // Arka Plan (Koyu Çerçeve)
        GameObject bgObj = new GameObject("HpBg");
        bgObj.transform.SetParent(barRoot.transform, false);
        SpriteRenderer bgSr = bgObj.AddComponent<SpriteRenderer>();
        bgSr.sortingOrder = 8;
        Texture2D bgTex = new Texture2D(34, 6, TextureFormat.RGBA32, false);
        Color darkBorder = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 34; x++)
                bgTex.SetPixel(x, y, darkBorder);
        bgTex.Apply();
        bgSr.sprite = Sprite.Create(bgTex, new Rect(0, 0, 34, 6), new Vector2(0.5f, 0.5f), 45f);

        // Dolgu Çubuğu
        GameObject fillObj = new GameObject("HpFill");
        fillObj.transform.SetParent(barRoot.transform, false);
        fillObj.transform.localPosition = new Vector3(-0.35f, 0f, 0f);
        _hpFillSr = fillObj.AddComponent<SpriteRenderer>();
        _hpFillSr.sortingOrder = 9;
        Texture2D fillTex = new Texture2D(30, 4, TextureFormat.RGBA32, false);
        Color green = new Color(0.15f, 0.95f, 0.35f, 0.95f);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 30; x++)
                fillTex.SetPixel(x, y, green);
        fillTex.Apply();
        _hpFillSr.sprite = Sprite.Create(fillTex, new Rect(0, 0, 30, 4), new Vector2(0f, 0.5f), 45f);
    }

    public void UpdateHpBarVisual()
    {
        if (_hpFillSr == null) return;
        float pct = Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
        _hpFillSr.transform.localScale = new Vector3(pct, 1f, 1f);
        _hpFillSr.color = pct > 0.5f ? Color.Lerp(Color.yellow, new Color(0.15f, 0.95f, 0.35f), (pct - 0.5f) * 2f)
                                     : Color.Lerp(Color.red, Color.yellow, pct * 2f);
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        HangarManager.Instance?.SetDroneHealth(droneType, currentHealth);
        UpdateHpBarVisual();

        VFXManager.SpawnHitSparks(transform.position, Color.yellow);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        VFXManager.SpawnExplosion(transform.position, false);
        CameraShake.Shake(0.15f, 0.10f);

        var gameUI = FindObjectOfType<GameUI>();
        string modelName = HangarManager.GetDroneDisplayName(droneType);
        gameUI?.ShowWaveAlert($"⚠️ <b>[DESTEK İHA İMHA OLDU!]</b>\n<color=#FF5252>{modelName.ToUpper()} KANATTAN DÜŞTÜ</color>", Color.red, 3f);

        HangarManager.Instance?.OnDroneDestroyed(droneType);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Bullet b = other.GetComponent<Bullet>();
        if (b != null && _playerTransform != null && b.owner != _playerTransform.gameObject && b.owner != gameObject)
        {
            VFXManager.SpawnHitSparks(transform.position, Color.yellow);
            TakeDamage(b.damage);
            Destroy(b.gameObject);
        }
    }

    private void CreateShieldBubble()
    {
        _shieldBubble = new GameObject("DroneShieldBubble");
        _shieldBubble.transform.SetParent(transform, false);

        SpriteRenderer bSr = _shieldBubble.AddComponent<SpriteRenderer>();
        Texture2D bTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                if (dist <= 30f && dist >= 22f)
                {
                    float alpha = Mathf.Sin((dist - 22f) / 8f * Mathf.PI) * 0.7f;
                    bTex.SetPixel(x, y, new Color(0.7f, 0.3f, 1f, alpha));
                }
                else
                {
                    bTex.SetPixel(x, y, Color.clear);
                }
            }
        }
        bTex.Apply();

        bSr.sprite = Sprite.Create(bTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 32f);
        bSr.sortingOrder = 6;
        _shieldBubble.transform.localScale = Vector3.one * 1.55f;

        CircleCollider2D col = _shieldBubble.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.95f;

        var absorber = _shieldBubble.AddComponent<ShieldBulletAbsorber>();
        absorber.drone = this;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        // Kullandıkça aşınma & batarya yıpranması
        _wearTimer += Time.deltaTime;
        if (_wearTimer >= 3.0f)
        {
            _wearTimer = 0f;
            TakeDamage(1.2f);
        }

        float fireRateMult = PlayerProfileManager.EnsureInstance()?.GetDroneFireRateMultiplier() ?? 1f;

        // 5 Drone Modeline Özgü Ateşleme Mantığı
        if (droneType == DroneType.MK1_Scout)
        {
            float interval = 0.65f / Mathf.Max(0.5f, fireRateMult);
            if (Time.time >= _nextFireTime)
            {
                _nextFireTime = Time.time + interval;
                FireLightBullet();
            }
        }
        else if (droneType == DroneType.MK2_Interceptor)
        {
            float interval = 0.45f / Mathf.Max(0.5f, fireRateMult);
            if (Time.time >= _nextFireTime)
            {
                _nextFireTime = Time.time + interval;
                FireLaserBolt();
            }
        }
        else if (droneType == DroneType.MK3_Striker)
        {
            float interval = 0.70f / Mathf.Max(0.5f, fireRateMult);
            if (Time.time >= _nextFireTime)
            {
                _nextFireTime = Time.time + interval;
                FireHeavySlug();
            }
        }
        else if (droneType == DroneType.MK4_Missile)
        {
            float interval = 2.2f / Mathf.Max(0.5f, fireRateMult);
            if (Time.time >= _nextFireTime)
            {
                _nextFireTime = Time.time + interval;
                FireMiniMissile();
            }
        }
        else if (droneType == DroneType.MK5_TitanAegis)
        {
            float interval = 0.52f / Mathf.Max(0.5f, fireRateMult);
            if (Time.time >= _nextFireTime)
            {
                _nextFireTime = Time.time + interval;
                FirePlasmaBurst();
            }
        }
    }

    private void LateUpdate()
    {
        if (_playerTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        // Uçağın yanına direkt sabitlenmiş ayrık pozisyon (lag / savrulma olmadan)
        Vector3 basePos = _playerTransform.position + _offset;

        // Çok ufak uçma efekti: hafif hover süzülüşü ve motor titreşimi
        float hoverY = Mathf.Sin(Time.time * 3.6f + (isLeftWing ? 0f : Mathf.PI)) * 0.035f;
        float hoverX = Mathf.Cos(Time.time * 2.8f + (isLeftWing ? 0f : Mathf.PI)) * 0.015f;
        transform.position = basePos + new Vector3(hoverX, hoverY, 0f);

        // Uçak sağa-sola hareket ederken çok ufak ve tatlı kanat yatırma (bank açısı)
        float moveX = (_playerTransform.position.x - _lastPlayerPos.x) / Mathf.Max(0.0001f, Time.deltaTime);
        _lastPlayerPos = _playerTransform.position;

        float targetBank = Mathf.Clamp(-moveX * 0.7f, -5f, 5f);
        float idleRoll = Mathf.Sin(Time.time * 3f + (isLeftWing ? 0f : 1.57f)) * 1.5f;
        transform.rotation = Quaternion.Euler(0f, 0f, targetBank + idleRoll);

        // Arkadaki itici plazma alevinin mikro titreşimi
        if (_flameSr != null)
        {
            float flicker = 0.85f + Mathf.Sin(Time.time * 24f + (isLeftWing ? 0f : 2.5f)) * 0.25f;
            _flameSr.transform.localScale = new Vector3(1f, flicker, 1f);
        }
    }

    public Transform GetPlayer() => _playerTransform;

    private static Sprite _cachedDroneBulletSprite;

    public static Sprite GetDroneBulletSprite()
    {
        if (_cachedDroneBulletSprite != null) return _cachedDroneBulletSprite;

        int w = 16;
        int h = 48;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color clear = Color.clear;
        float center = (w - 1) * 0.5f;
        float bodyHalfW = 4.2f;

        Color hotCore = new Color(0.95f, 0.98f, 1f, 1f);
        Color plasmaCyan = new Color(0.18f, 0.85f, 1f, 0.95f);
        Color steelRim = new Color(0.06f, 0.40f, 0.75f, 0.90f);
        Color tracerBurn = new Color(1f, 0.85f, 0.35f, 0.85f);

        for (int y = 0; y < h; y++)
        {
            float allowedW;
            if (y < 6)
            {
                float taper = 1.0f - (6 - y) * 0.12f;
                allowedW = bodyHalfW * taper;
            }
            else if (y <= 30)
            {
                allowedW = bodyHalfW;
            }
            else
            {
                float p = (y - 30) / 17.0f;
                allowedW = bodyHalfW * Mathf.Sqrt(Mathf.Max(0f, 1f - p * p));
            }

            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - center);
                if (dx <= allowedW)
                {
                    float distRatio = dx / Mathf.Max(0.5f, allowedW);

                    Color pixelCol;
                    if (y < 5) pixelCol = Color.Lerp(hotCore, tracerBurn, distRatio);
                    else if (distRatio < 0.35f) pixelCol = hotCore;
                    else if (distRatio < 0.75f) pixelCol = plasmaCyan;
                    else pixelCol = steelRim;

                    if (y >= 43 && dx <= 1.5f) pixelCol = Color.white;
                    tex.SetPixel(x, y, pixelCol);
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }
        }

        tex.Apply();
        _cachedDroneBulletSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
        return _cachedDroneBulletSprite;
    }

    // 1. MK-1 İZCİ: Hafif makineli seri atış
    private void FireLightBullet()
    {
        GameObject bolt = new GameObject("DroneLightBolt");
        bolt.transform.position = transform.position + new Vector3(0f, 0.3f, 0f);

        SpriteRenderer bSr = bolt.AddComponent<SpriteRenderer>();
        bSr.sprite = GetDroneBulletSprite();
        bSr.color = new Color(0.7f, 1f, 0.7f);
        bSr.sortingOrder = 7;
        bolt.transform.localScale = Vector3.one * 0.85f;

        BoxCollider2D box = bolt.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.18f, 0.65f);

        float dmgMult = PlayerProfileManager.EnsureInstance()?.GetDroneDamageMultiplier() ?? 1f;
        var bullet = bolt.AddComponent<Bullet>();
        bullet.damage = 5f * dmgMult;
        bullet.speed = 18f;
        bullet.owner = _playerTransform != null ? _playerTransform.gameObject : gameObject;

        Rigidbody2D rb = bolt.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = new Vector2(0f, 18f);

        AudioManager.Instance?.PlayGunfire(false);
    }

    // 2. MK-2 AVCI: Çift siber lazer atışı
    private void FireLaserBolt()
    {
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject bolt = new GameObject("DroneLaserBolt");
            bolt.transform.position = transform.position + new Vector3(i * 0.16f, 0.3f, 0f);

            SpriteRenderer bSr = bolt.AddComponent<SpriteRenderer>();
            bSr.sprite = GetDroneBulletSprite();
            bSr.color = new Color(0.2f, 0.9f, 1f);
            bSr.sortingOrder = 7;

            BoxCollider2D box = bolt.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.18f, 0.65f);

            float dmgMult = PlayerProfileManager.EnsureInstance()?.GetDroneDamageMultiplier() ?? 1f;
            var bullet = bolt.AddComponent<Bullet>();
            bullet.damage = 4.5f * dmgMult;
            bullet.speed = 19f;
            bullet.owner = _playerTransform != null ? _playerTransform.gameObject : gameObject;

            Rigidbody2D rb = bolt.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(0f, 19f);
        }

        AudioManager.Instance?.PlayGunfire(false);
    }

    // 3. MK-3 TAARRUZ: Ağır kinetik delici mermi
    private void FireHeavySlug()
    {
        GameObject bolt = new GameObject("DroneHeavySlug");
        bolt.transform.position = transform.position + new Vector3(0f, 0.35f, 0f);

        SpriteRenderer bSr = bolt.AddComponent<SpriteRenderer>();
        bSr.sprite = GetDroneBulletSprite();
        bSr.color = new Color(1f, 0.3f, 0.2f);
        bSr.sortingOrder = 7;
        bolt.transform.localScale = new Vector3(1.35f, 1.25f, 1f);

        BoxCollider2D box = bolt.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.25f, 0.75f);

        float dmgMult = PlayerProfileManager.EnsureInstance()?.GetDroneDamageMultiplier() ?? 1f;
        var bullet = bolt.AddComponent<Bullet>();
        bullet.damage = 14f * dmgMult;
        bullet.speed = 16f;
        bullet.owner = _playerTransform != null ? _playerTransform.gameObject : gameObject;

        Rigidbody2D rb = bolt.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = new Vector2(0f, 16f);

        AudioManager.Instance?.PlayGunfire(false);
    }

    // 4. MK-4 VALKYRIE: Güdümlü mikro roket
    private void FireMiniMissile()
    {
        GameObject mObj = new GameObject("DroneMiniMissile");
        mObj.transform.position = transform.position + new Vector3(0f, 0.25f, 0f);

        float dmgMult = PlayerProfileManager.EnsureInstance()?.GetDroneDamageMultiplier() ?? 1f;
        var missile = mObj.AddComponent<HomingMissile>();
        missile.damage = 26f * dmgMult;
        missile.owner = _playerTransform != null ? _playerTransform.gameObject : gameObject;

        AudioManager.Instance?.PlayGunfire(true);
    }

    // 5. MK-5 TİTAN AEGIS: Yüksek hasarlı ağır plazma burst
    private void FirePlasmaBurst()
    {
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject bolt = new GameObject("DronePlasmaBurst");
            bolt.transform.position = transform.position + new Vector3(i * 0.2f, 0.35f, 0f);

            SpriteRenderer bSr = bolt.AddComponent<SpriteRenderer>();
            bSr.sprite = GetDroneBulletSprite();
            bSr.color = new Color(0.85f, 0.4f, 1f);
            bSr.sortingOrder = 7;
            bolt.transform.localScale = new Vector3(1.2f, 1.1f, 1f);

            BoxCollider2D box = bolt.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.22f, 0.7f);

            float dmgMult = PlayerProfileManager.EnsureInstance()?.GetDroneDamageMultiplier() ?? 1f;
            var bullet = bolt.AddComponent<Bullet>();
            bullet.damage = 9f * dmgMult;
            bullet.speed = 18f;
            bullet.owner = _playerTransform != null ? _playerTransform.gameObject : gameObject;

            Rigidbody2D rb = bolt.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(0f, 18f);
        }

        AudioManager.Instance?.PlayGunfire(false);
    }
}

/// <summary>
/// Kalkan dronunun mermileri yutmasını sağlayan tetikleyici
/// </summary>
public class ShieldBulletAbsorber : MonoBehaviour
{
    public DroneCompanion drone;

    private void OnTriggerEnter2D(Collider2D col)
    {
        Bullet b = col.GetComponent<Bullet>();
        if (b != null)
        {
            // Eğer mermi oyuncunun kendisine aitse yutma (sadece düşman mermilerini yut)
            if (drone != null && drone.GetPlayer() != null && b.owner == drone.GetPlayer().gameObject)
            {
                return;
            }

            // Düşman mermisini yut, kıvılcım çıkar ve sarsıntı yap
            VFXManager.SpawnHitSparks(col.transform.position, new Color(0.2f, 1f, 0.5f));
            CameraShake.Shake(0.08f, 0.05f);
            Destroy(b.gameObject);
        }
    }
}

