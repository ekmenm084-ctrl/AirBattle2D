using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uçak güçlendirme türleri (Hangar Yükseltmeleri).
/// </summary>
public enum UpgradeType
{
    TwinCannons,        // Çift / Üçlü Namlulu Ağır Top
    EnergyShield,       // Plazma Enerji Kalkanı
    ReinforcedArmor,    // Titanyum Gövde Zırhı & Tamir
    RapidFire,          // Aşırı Yüklemeli Seri Ateş
    PlasmaThrusters,    // Plazma İtici Motor (Hız & Manevra)
    ExtraLife           // Yedek Uçak / Ekstra Can Hakkı
}

[System.Serializable]
public class UpgradeOption
{
    public UpgradeType type;
    public string title;
    public string badge;
    public string description;
    public string statBenefit;
    public string icon;
    public Color themeColor;

    public UpgradeOption(UpgradeType type, string title, string badge, string description, string statBenefit, string icon, Color color)
    {
        this.type = type;
        this.title = title;
        this.badge = badge;
        this.description = description;
        this.statBenefit = statBenefit;
        this.icon = icon;
        this.themeColor = color;
    }
}

/// <summary>
/// Oyuncu 1 için kalıcı hangar yükseltmelerini ve kalkan durumunu yönetir.
/// </summary>
public class PlayerUpgradeManager : MonoBehaviour
{
    public static PlayerUpgradeManager Instance { get; private set; }

    [Header("Kalkan Sistemi")]
    public bool hasShield = false;
    public float maxShield = 75f;
    public float currentShield = 0f;

    [Header("Yükseltme Seviyeleri")]
    public int weaponLevel = 1;        // 1: Tekli, 2: Çiftli, 3: Üçlü, 4+: Süper Üçlü
    public int armorLevel = 0;         // Her seviye +60 Max HP & Tamir
    public int rapidFireLevel = 0;     // Her seviye %25 daha seri ateş & +6 Hasar
    public int thrusterLevel = 0;      // Her seviye +2.5 Hareket hızı
    public int extraLivesGranted = 0;

    [Header("Görsel Referanslar")]
    public Sprite shieldSprite;
    private GameObject _shieldVisualObj;
    private SpriteRenderer _shieldRenderer;

    private PlaneHealth _playerHealth;
    private PlaneShooter _playerShooter;
    private PlaneController _playerController;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    public static PlayerUpgradeManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("PlayerUpgradeManager");
        if (go == null)
        {
            go = new GameObject("PlayerUpgradeManager");
        }

        Instance = go.GetComponent<PlayerUpgradeManager>();
        if (Instance == null)
        {
            Instance = go.AddComponent<PlayerUpgradeManager>();
        }
        return Instance;
    }

    private void Start()
    {
        CachePlayerReferences();
        SetupShieldVisual();
    }

    private void CachePlayerReferences()
    {
        if (_playerHealth == null || _playerShooter == null || _playerController == null)
        {
            var planes = FindObjectsOfType<PlaneController>();
            foreach (var p in planes)
            {
                if (p.isPlayer1)
                {
                    _playerController = p;
                    _playerHealth = p.GetComponent<PlaneHealth>();
                    _playerShooter = p.GetComponent<PlaneShooter>();
                    break;
                }
            }
        }
    }

    private void Update()
    {
        // Kalkan görselini uçağın etrafında döndür ve hafifçe nabız gibi attır
        if (_shieldVisualObj != null && _shieldVisualObj.activeSelf)
        {
            _shieldVisualObj.transform.Rotate(0f, 0f, 60f * Time.unscaledDeltaTime);
            float pulse = 1.0f + Mathf.Sin(Time.unscaledTime * 5f) * 0.05f;
            _shieldVisualObj.transform.localScale = new Vector3(1.4f * pulse, 1.4f * pulse, 1f);
        }
    }

    private void SetupShieldVisual()
    {
        CachePlayerReferences();
        if (_playerController == null) return;

        if (_shieldVisualObj == null)
        {
            _shieldVisualObj = new GameObject("ShieldBubbleVisual");
            _shieldVisualObj.transform.SetParent(_playerController.transform, false);
            _shieldVisualObj.transform.localPosition = Vector3.zero;
            _shieldVisualObj.transform.localScale = new Vector3(1.4f, 1.4f, 1f);

            _shieldRenderer = _shieldVisualObj.AddComponent<SpriteRenderer>();
            _shieldRenderer.sortingOrder = 5; // Uçağın üstünde görünsün

            // Kalkan sprite'ını Resources'tan yükle
            if (shieldSprite == null)
            {
                shieldSprite = Resources.Load<Sprite>("shield_bubble");
            }

            if (shieldSprite != null)
            {
                _shieldRenderer.sprite = shieldSprite;
            }
            else
            {
                // Fallback dinamik parlayan enerji dokusu
                _shieldRenderer.sprite = CreateFallbackShieldSprite();
            }

            _shieldRenderer.color = new Color(0f, 0.9f, 1f, 0.85f);
        }

        _shieldVisualObj.SetActive(hasShield && currentShield > 0f);
    }

    private Sprite CreateFallbackShieldSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f;

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
                    // Halka şeklinde parlayan kenarlar
                    float ring = Mathf.Abs(dist - (radius - 8f)) / 8f;
                    float alpha = Mathf.Clamp01(1f - ring) * 0.9f;
                    tex.SetPixel(x, y, new Color(0f, 0.9f, 1f, alpha));
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Uçak hasar aldığında kalkan darbeyi emer.
    /// Kalkanın ememediği kalan hasarı geri döndürür.
    /// </summary>
    public float AbsorbDamage(float incomingDamage)
    {
        if (!hasShield || currentShield <= 0f)
            return incomingDamage;

        if (currentShield >= incomingDamage)
        {
            currentShield -= incomingDamage;
            StartCoroutine(FlashShieldRoutine(new Color(1f, 1f, 1f, 1f), 0.15f));

            if (currentShield <= 0f)
            {
                BreakShield();
            }
            return 0f; // Tüm hasar kalkan tarafından yutuldu!
        }
        else
        {
            float unabsorbed = incomingDamage - currentShield;
            currentShield = 0f;
            BreakShield();
            return unabsorbed;
        }
    }

    private void BreakShield()
    {
        hasShield = false;
        if (_shieldVisualObj != null)
            _shieldVisualObj.SetActive(false);

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert("⚠️ KALKAN PARÇALANDI!", new Color(1f, 0.3f, 0.2f), 1.5f);
    }

    private IEnumerator FlashShieldRoutine(Color flashColor, float duration)
    {
        if (_shieldRenderer == null) yield break;
        Color original = new Color(0f, 0.9f, 1f, 0.85f);
        _shieldRenderer.color = flashColor;
        yield return new WaitForSecondsRealtime(duration);
        if (_shieldRenderer != null)
            _shieldRenderer.color = original;
    }

    /// <summary>
    /// Seçim ekranında gösterilmek üzere 3 farklı benzersiz yükseltme kartı seçer.
    /// </summary>
    public List<UpgradeOption> GenerateThreeRandomOptions()
    {
        List<UpgradeOption> pool = new List<UpgradeOption>();

        // 1. Silah Yükseltmesi
        if (weaponLevel == 1)
        {
            pool.Add(new UpgradeOption(
                UpgradeType.TwinCannons,
                "ÇİFT NAMLULU VULCAN",
                "[SİLAH SİSTEMİ]",
                "Uçağa ikinci bir paralel lazer namlusu monte eder. Mermi sayısını ve ateş alanını 2 katına çıkarır!",
                "Aynı anda 2 paralel mermi fırlatır",
                "⚔️",
                new Color(0.95f, 0.3f, 0.2f)
            ));
        }
        else if (weaponLevel == 2)
        {
            pool.Add(new UpgradeOption(
                UpgradeType.TwinCannons,
                "ÜÇLÜ SALVO TOPU",
                "[AĞIR SİLAH]",
                "Gövdeye 3. bir ana namlu ekler! Önündeki tüm hava sahasını mermi yağmuruna tutar.",
                "Aynı anda 3 mermi fırlatır",
                "🔱",
                new Color(1f, 0.45f, 0.1f)
            ));
        }
        else
        {
            pool.Add(new UpgradeOption(
                UpgradeType.TwinCannons,
                "PLAZMA ÇEKİRDEK TOPU",
                "[SÜPER SİLAH]",
                "Ağır plazma mermileri! Mermi hasarını kalıcı olarak büyük oranda artırır.",
                "+12 Mermi Hasarı & Genişletilmiş Namlu",
                "⚡",
                new Color(0.8f, 0.2f, 1f)
            ));
        }

        // 2. Enerji Kalkanı
        if (!hasShield || currentShield <= 0f)
        {
            pool.Add(new UpgradeOption(
                UpgradeType.EnergyShield,
                "PLAZMA ENERJİ KALKANI",
                "[DEFANS SİSTEMİ]",
                "Uçağın etrafında 75 HP hasar emen koruyucu bir enerji kubbesi açar. Doğrudan isabetleri engeller!",
                "+75 HP Koruyucu Plazma Kalkanı",
                "🛡️",
                new Color(0f, 0.9f, 1f)
            ));
        }
        else
        {
            pool.Add(new UpgradeOption(
                UpgradeType.EnergyShield,
                "AŞIRI YÜKLÜ KALKAN (+100)",
                "[KALKAN ŞARJI]",
                "Mevcut kalkanı anında %100 doldurur ve azami kalkan kapasitesini +50 HP daha artırır!",
                "Tam Kalkan Şarjı & +50 Kapasite",
                "💠",
                new Color(0.2f, 0.8f, 1f)
            ));
        }

        // 3. Titanyum Gövde Zırhı & Tamir
        pool.Add(new UpgradeOption(
            UpgradeType.ReinforcedArmor,
            "TİTANYUM GÖVDE ZIRHI",
            "[DAYANIKLILIK]",
            "Uçağın gövdesini titanyum alaşımla kaplar. Azami canı kalıcı +60 artırır ve canı anında %100 doldurur!",
            "+60 Azami Can & Anında Tam Tamir",
            "❤️",
            new Color(0.2f, 0.9f, 0.4f)
        ));

        // 4. Aşırı Yüklemeli Seri Ateş
        pool.Add(new UpgradeOption(
            UpgradeType.RapidFire,
            "SERİ ATEŞ AŞIRI YÜKLEMESİ",
            "[SALDIRI HIZI]",
            "Mermi namlu döngüsünü hızlandırır (%30 daha seri ateş) ve her mermiye +6 ilave hasar ekler.",
            "%30 Daha Hızlı Ateş & +6 Hasar",
            "⚡",
            new Color(1f, 0.85f, 0.15f)
        ));

        // 5. Plazma İtici Motor
        pool.Add(new UpgradeOption(
            UpgradeType.PlasmaThrusters,
            "PLAZMA İTİCİ MOTOR",
            "[MANEVRA & HIZ]",
            "Uçağın kaçış ve manevra hızını +3.0 birim artırır. Düşman mermilerinden ve füzelerden sıyrılmayı çok kolaylaştırır!",
            "+3.0 Manevra Hızı",
            "🚀",
            new Color(0.3f, 0.6f, 1f)
        ));

        // 6. Ekstra Can Hakkı (Yedek Uçak)
        pool.Add(new UpgradeOption(
            UpgradeType.ExtraLife,
            "YEDEK SAVAŞ JETİ (+1 CAN)",
            "[HAYATTA KALMA]",
            "Filoya ilave 1 yedek uçak kazandırır! Düşsen bile bir can hakkı daha elde edersin.",
            "+1 Yedek Can Hakkı (❤️)",
            "💖",
            new Color(1f, 0.35f, 0.65f)
        ));

        // Havuzdan rastgele 3 tanesini seç
        Shuffle(pool);
        List<UpgradeOption> selected = new List<UpgradeOption>();
        for (int i = 0; i < Mathf.Min(3, pool.Count); i++)
        {
            selected.Add(pool[i]);
        }
        return selected;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[r];
            list[r] = temp;
        }
    }

    /// <summary>
    /// Oyuncunun seçtiği yükseltmeyi sisteme uygular.
    /// </summary>
    public void ApplyUpgrade(UpgradeType type)
    {
        CachePlayerReferences();

        string upgradeTitle = "";

        switch (type)
        {
            case UpgradeType.TwinCannons:
                weaponLevel++;
                if (_playerShooter != null)
                {
                    if (weaponLevel == 2)
                    {
                        _playerShooter.bulletsPerShot = 2;
                        _playerShooter.bulletSpread = 0.35f;
                        upgradeTitle = "⚔️ ÇİFT NAMLULU VULCAN DONATILDI!";
                    }
                    else if (weaponLevel == 3)
                    {
                        _playerShooter.bulletsPerShot = 3;
                        _playerShooter.bulletSpread = 0.40f;
                        upgradeTitle = "🔱 ÜÇLÜ SALVO SİSTEMİ AKTİF!";
                    }
                    else
                    {
                        _playerShooter.bulletsPerShot = 3;
                        _playerShooter.bulletDamage += 12f;
                        upgradeTitle = "⚡ PLAZMA ÇEKİRDEK TOPU AKTİF (+12 HASAR)!";
                    }
                }
                break;

            case UpgradeType.EnergyShield:
                hasShield = true;
                if (currentShield <= 0f)
                {
                    maxShield = 75f;
                    currentShield = 75f;
                    upgradeTitle = "🛡️ PLAZMA ENERJİ KALKANI AKTİF EDİLDİ!";
                }
                else
                {
                    maxShield += 50f;
                    currentShield = maxShield;
                    upgradeTitle = "💠 KALKAN AŞIRI YÜKLENDİ (%100 ŞARJ)!";
                }
                SetupShieldVisual();
                if (_shieldVisualObj != null)
                    _shieldVisualObj.SetActive(true);
                break;

            case UpgradeType.ReinforcedArmor:
                armorLevel++;
                if (_playerHealth != null)
                {
                    _playerHealth.maxHealth += 60f;
                    _playerHealth.Heal(_playerHealth.maxHealth); // Anında tamir
                    upgradeTitle = $"❤️ GÖVDE ZIRHI TAHKİM EDİLDİ (AZAMİ CAN: {_playerHealth.maxHealth} HP)!";
                }
                break;

            case UpgradeType.RapidFire:
                rapidFireLevel++;
                if (_playerShooter != null)
                {
                    _playerShooter.fireRate = Mathf.Max(0.10f, _playerShooter.fireRate * 0.70f);
                    _playerShooter.bulletDamage += 6f;
                    upgradeTitle = "⚡ SERİ ATEŞ AŞIRI YÜKLENDİ (+%30 HIZ & +6 HASAR)!";
                }
                break;

            case UpgradeType.PlasmaThrusters:
                thrusterLevel++;
                if (_playerController != null)
                {
                    _playerController.moveSpeed += 3.0f;
                    upgradeTitle = $"🚀 PLAZMA İTİCİLER AKTİF (HIZ: {_playerController.moveSpeed:F1})!";
                }
                break;

            case UpgradeType.ExtraLife:
                extraLivesGranted++;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.player1Lives++;
                    upgradeTitle = $"💖 YEDEK UÇAK KAZANILDI (CAN HAKKI: {GameManager.Instance.player1Lives})!";
                }
                break;
        }

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateHealthBars();
        gameUI?.ShowWaveAlert(upgradeTitle, new Color(0.2f, 1f, 0.8f), 2.5f);
    }
}
