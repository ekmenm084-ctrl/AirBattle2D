using System;
using UnityEngine;

/// <summary>
/// Hangar ve Kalıcı Geliştirme Yöneticisi (Meta-Progression):
/// 1. Uçak Filosu (F-16 Viper, A-10 Tank Buster, F-22 Raptor)
/// 2. Kalıcı Atölye Yükseltmeleri (Zırh, Hasar, Hız, Kritik Şans)
/// 3. Altın Kasası ve PlayerPrefs Kayıt Sistemi
/// </summary>
public class HangarManager : MonoBehaviour
{
    public static HangarManager Instance { get; private set; }

    public enum AircraftType
    {
        F16Viper,       // Kademe 1: Dengeli Taktik Avcı (Seviye 1+)
        A10TankBuster,  // Kademe 2: Ağır Zırhlı Taarruz Uçağı (Seviye 15+)
        F22Raptor,      // Kademe 3: 5. Nesil Hayalet Süpersonik Jet (Seviye 30+)
        Su57Felon,      // Kademe 4: 5. Nesil Süper-Manevra Ağır Avcı (Seviye 50+)
        F35Lightning,   // Kademe 5: 5. Nesil Çok Amaçlı Üçlü Salvo Jeti (Seviye 70+)
        KaanApex        // Kademe 6: 5.+ Nesil Hava Hâkimiyeti Zirvesi (Seviye 90+)
    }

    public enum CrateType
    {
        Bronze,     // 150 G - Bronz İkmal Kasası
        Silver,     // 450 G - Gümüş Cephane Kasası
        GoldAce     // 1100 G - Altın As Sandığı
    }

    public enum KeyType
    {
        Bronze,     // Bronz Sandık Anahtarı
        Silver,     // Gümüş Cephane Anahtarı
        Gold        // Altın As Sandık Anahtarı (1/100 Efsanevi Düşüş)
    }

    [System.Serializable]
    public class CrateReward
    {
        public string title;
        public string description;
        public int goldGain;
        public string rarityColorHex;
    }

    private const string PREF_GOLD = "AirBattle_Gold";
    private const string PREF_ACTIVE_PLANE = "AirBattle_ActivePlane";
    private const string PREF_UNLOCKED_A10 = "AirBattle_Unlocked_A10";
    private const string PREF_UNLOCKED_F22 = "AirBattle_Unlocked_F22";
    private const string PREF_UNLOCKED_SU57 = "AirBattle_Unlocked_Su57";
    private const string PREF_UNLOCKED_F35 = "AirBattle_Unlocked_F35";
    private const string PREF_UNLOCKED_KAAN = "AirBattle_Unlocked_Kaan";

    private const string PREF_LVL_ARMOR = "AirBattle_Lvl_Armor";
    private const string PREF_LVL_DAMAGE = "AirBattle_Lvl_Damage";
    private const string PREF_LVL_SPEED = "AirBattle_Lvl_Speed";
    private const string PREF_LVL_CRIT = "AirBattle_Lvl_Crit";

    // Sandık Anahtarları Prefs
    private const string PREF_KEY_BRONZE = "AirBattle_Key_Bronze";
    private const string PREF_KEY_SILVER = "AirBattle_Key_Silver";
    private const string PREF_KEY_GOLD = "AirBattle_Key_Gold";
    private const string PREF_SUPER_PLANES = "AirBattle_SingleUse_SuperPlanes";

    // Drone Prefs (5 Aşamalı Model & Can / Dayanıklılık Sistemi)
    public const string PREF_DRONE_LEFT = "AirBattle_Drone_Left";
    public const string PREF_DRONE_RIGHT = "AirBattle_Drone_Right";
    public const string PREF_DRONE_UNLOCKED_PREFIX = "AirBattle_Drone_Unl_";
    public const string PREF_DRONE_HP_PREFIX = "AirBattle_Drone_HP_";
    public const string PREF_DRONE_DEATHS_PREFIX = "AirBattle_Drone_Deaths_";
    public const int MAX_DRONE_DEATHS = 2;

    // Kalıcı Çip Bonusları
    private const string PREF_CHIP_BONUS_DMG = "AirBattle_Chip_Bonus_Dmg";
    private const string PREF_CHIP_BONUS_HP = "AirBattle_Chip_Bonus_Hp";
    private const string PREF_TOTAL_CRATES = "AirBattle_Total_Crates";

    [Header("Mevcut Durum")]
    public AircraftType activeAircraft = AircraftType.F16Viper;
    public int gold = 250; // Başlangıç hoş geldin hediyesi
    public int totalCratesOpened = 0;

    [Header("Sandık Anahtarları (20+ Seviye Boss Ganimetleri)")]
    public int bronzeKeys = 0;
    public int silverKeys = 0;
    public int goldKeys = 0;

    [Header("Tek Kullanımlık Prototip Süper Uçaklar")]
    public int singleUseSuperPlanes = 0;
    public bool isSingleUseSuperPlaneActive = true;

    [Header("Kalıcı Atölye Seviyeleri (Maks 5)")]
    public int armorLevel = 0;
    public int damageLevel = 0;
    public int speedLevel = 0;
    public int criticalLevel = 0;

    [Header("Destek Droneları")]
    public DroneCompanion.DroneType leftDroneType = DroneCompanion.DroneType.None;
    public DroneCompanion.DroneType rightDroneType = DroneCompanion.DroneType.None;
    public int laserDroneLevel = 1;
    public int missileDroneLevel = 1;
    public int shieldDroneLevel = 1;

    [Header("Kalıcı Sandık Çip Bonusları")]
    public float chipBonusDamage = 0f;
    public float chipBonusHealth = 0f;

    public const int MAX_UPGRADE_LEVEL = 5;
    public const int MAX_DRONE_LEVEL = 5;

    public static readonly int[] ArmorCosts = { 100, 200, 350, 500, 750 };
    public static readonly int[] DamageCosts = { 120, 250, 400, 600, 850 };
    public static readonly int[] SpeedCosts = { 90, 180, 300, 450, 650 };
    public static readonly int[] CritCosts = { 150, 300, 500, 750, 1000 };

    public const int A10_UNLOCK_COST = 1500;
    public const int F22_UNLOCK_COST = 5000;
    public const int SU57_UNLOCK_COST = 15000;
    public const int F35_UNLOCK_COST = 35000;
    public const int KAAN_UNLOCK_COST = 75000;

    // Drone Maliyetleri
    public const int DRONE_LASER_COST = 350;
    public const int DRONE_MISSILE_COST = 650;
    public const int DRONE_SHIELD_COST = 950;
    public static readonly int[] DroneUpgradeCosts = { 150, 300, 500, 800 }; // 1->2, 2->3, 3->4, 4->5

    // Sandık Maliyetleri
    public const int CRATE_BRONZE_COST = 500;
    public const int CRATE_SILVER_COST = 2000;
    public const int CRATE_GOLD_COST = 6000;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadHangarData();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static HangarManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("HangarManager");
        if (go == null) go = new GameObject("HangarManager");
        Instance = go.GetComponent<HangarManager>();
        if (Instance == null) Instance = go.AddComponent<HangarManager>();
        Instance.LoadHangarData();
        return Instance;
    }

    public void LoadHangarData()
    {
        gold = PlayerPrefs.GetInt(PREF_GOLD, 250);
        activeAircraft = (AircraftType)PlayerPrefs.GetInt(PREF_ACTIVE_PLANE, (int)AircraftType.F16Viper);

        armorLevel = 0;
        damageLevel = 0;
        speedLevel = 0;
        criticalLevel = 0;

        leftDroneType = (DroneCompanion.DroneType)PlayerPrefs.GetInt(PREF_DRONE_LEFT, (int)DroneCompanion.DroneType.None);
        rightDroneType = (DroneCompanion.DroneType)PlayerPrefs.GetInt(PREF_DRONE_RIGHT, (int)DroneCompanion.DroneType.None);

        // 1. Drone (MK-1 İzci) öncelikli olarak oyuncunun ilk başlangıç drone'u olarak kullanıma açılır
        const string PREF_FIRST_DRONE_INIT = "AirBattle_Drone_Priority_V3";
        if (PlayerPrefs.GetInt(PREF_FIRST_DRONE_INIT, 0) == 0)
        {
            PlayerPrefs.SetInt(PREF_FIRST_DRONE_INIT, 1);
            PlayerPrefs.SetInt(PREF_DRONE_UNLOCKED_PREFIX + (int)DroneCompanion.DroneType.MK1_Scout, 1);
            PlayerPrefs.SetFloat(PREF_DRONE_HP_PREFIX + (int)DroneCompanion.DroneType.MK1_Scout, GetDroneMaxHealth(DroneCompanion.DroneType.MK1_Scout));

            // Eğer oyuncu seviyesi 20'nin altındaysa, eski hatalı kayıttan kalan MK-2'yi kitle
            int curLvl = PlayerProfileManager.EnsureInstance()?.level ?? 1;
            if (curLvl < GetDroneRequiredLevel(DroneCompanion.DroneType.MK2_Interceptor))
            {
                PlayerPrefs.SetInt(PREF_DRONE_UNLOCKED_PREFIX + (int)DroneCompanion.DroneType.MK2_Interceptor, 0);
                PlayerPrefs.SetFloat(PREF_DRONE_HP_PREFIX + (int)DroneCompanion.DroneType.MK2_Interceptor, 0f);
            }

            // Sol kanada 1. drone olan MK-1'i otomatik tak
            if (leftDroneType == DroneCompanion.DroneType.None || leftDroneType == DroneCompanion.DroneType.MK2_Interceptor)
            {
                leftDroneType = DroneCompanion.DroneType.MK1_Scout;
                PlayerPrefs.SetInt(PREF_DRONE_LEFT, (int)leftDroneType);
            }
            if (rightDroneType == DroneCompanion.DroneType.MK2_Interceptor)
            {
                rightDroneType = DroneCompanion.DroneType.None;
                PlayerPrefs.SetInt(PREF_DRONE_RIGHT, (int)rightDroneType);
            }
        }

        // Seviye şartı karşılanmayan veya canı bitmiş drone takılı kalamaz
        if (leftDroneType != DroneCompanion.DroneType.None && !IsDroneUnlocked(leftDroneType))
        {
            leftDroneType = DroneCompanion.DroneType.None;
            PlayerPrefs.SetInt(PREF_DRONE_LEFT, (int)DroneCompanion.DroneType.None);
        }
        if (rightDroneType != DroneCompanion.DroneType.None && !IsDroneUnlocked(rightDroneType))
        {
            rightDroneType = DroneCompanion.DroneType.None;
            PlayerPrefs.SetInt(PREF_DRONE_RIGHT, (int)DroneCompanion.DroneType.None);
        }

        chipBonusDamage = PlayerPrefs.GetFloat(PREF_CHIP_BONUS_DMG, 0f);
        chipBonusHealth = PlayerPrefs.GetFloat(PREF_CHIP_BONUS_HP, 0f);
        totalCratesOpened = PlayerPrefs.GetInt(PREF_TOTAL_CRATES, 0);

        bronzeKeys = PlayerPrefs.GetInt(PREF_KEY_BRONZE, 0);
        silverKeys = PlayerPrefs.GetInt(PREF_KEY_SILVER, 0);
        goldKeys = PlayerPrefs.GetInt(PREF_KEY_GOLD, 0);
        singleUseSuperPlanes = PlayerPrefs.GetInt(PREF_SUPER_PLANES, 0);
    }

    public void SaveHangarData()
    {
        PlayerPrefs.SetInt(PREF_GOLD, gold);
        PlayerPrefs.SetInt(PREF_ACTIVE_PLANE, (int)activeAircraft);
        PlayerPrefs.SetInt(PREF_TOTAL_CRATES, totalCratesOpened);

        PlayerPrefs.SetInt(PREF_LVL_ARMOR, armorLevel);
        PlayerPrefs.SetInt(PREF_LVL_DAMAGE, damageLevel);
        PlayerPrefs.SetInt(PREF_LVL_SPEED, speedLevel);
        PlayerPrefs.SetInt(PREF_LVL_CRIT, criticalLevel);

        PlayerPrefs.SetInt(PREF_DRONE_LEFT, (int)leftDroneType);
        PlayerPrefs.SetInt(PREF_DRONE_RIGHT, (int)rightDroneType);

        PlayerPrefs.SetFloat(PREF_CHIP_BONUS_DMG, chipBonusDamage);
        PlayerPrefs.SetFloat(PREF_CHIP_BONUS_HP, chipBonusHealth);

        PlayerPrefs.SetInt(PREF_KEY_BRONZE, bronzeKeys);
        PlayerPrefs.SetInt(PREF_KEY_SILVER, silverKeys);
        PlayerPrefs.SetInt(PREF_KEY_GOLD, goldKeys);
        PlayerPrefs.SetInt(PREF_SUPER_PLANES, singleUseSuperPlanes);

        PlayerPrefs.Save();
    }

    public int GetKeyCount(KeyType type)
    {
        LoadHangarData();
        return type switch
        {
            KeyType.Bronze => bronzeKeys,
            KeyType.Silver => silverKeys,
            KeyType.Gold   => goldKeys,
            _              => 0
        };
    }

    public void AddKey(KeyType type, int amount = 1)
    {
        LoadHangarData();
        switch (type)
        {
            case KeyType.Bronze: bronzeKeys = Mathf.Max(0, bronzeKeys + amount); break;
            case KeyType.Silver: silverKeys = Mathf.Max(0, silverKeys + amount); break;
            case KeyType.Gold:   goldKeys = Mathf.Max(0, goldKeys + amount); break;
        }
        SaveHangarData();
    }

    public bool ConsumeKey(KeyType type)
    {
        LoadHangarData();
        if (GetKeyCount(type) <= 0) return false;
        switch (type)
        {
            case KeyType.Bronze: bronzeKeys = Mathf.Max(0, bronzeKeys - 1); break;
            case KeyType.Silver: silverKeys = Mathf.Max(0, silverKeys - 1); break;
            case KeyType.Gold:   goldKeys = Mathf.Max(0, goldKeys - 1); break;
        }
        SaveHangarData();
        return true;
    }

    public void AddSingleUseSuperPlane(int count = 1)
    {
        LoadHangarData();
        singleUseSuperPlanes = Mathf.Max(0, singleUseSuperPlanes + count);
        SaveHangarData();
    }

    public bool ConsumeSingleUseSuperPlane()
    {
        LoadHangarData();
        if (singleUseSuperPlanes <= 0) return false;
        singleUseSuperPlanes = Mathf.Max(0, singleUseSuperPlanes - 1);
        SaveHangarData();
        return true;
    }

    /// <summary>
    /// Kasaya altın ekler, kalıcı olarak kaydeder ve ekrandaki HUD altın sayacını günceller.
    /// </summary>
    public void AddGold(int amount)
    {
        LoadHangarData();
        gold = Mathf.Max(0, gold + amount);
        SaveHangarData();

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateGoldHUD(gold, amount);
        PlayerProfileUI.Instance?.RefreshBadgeVisuals();
    }

    /// <summary>
    /// Kasadan altın harcar, yeterli bakiye yoksa false döner.
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        LoadHangarData();
        if (gold < amount) return false;
        gold -= amount;
        SaveHangarData();

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateGoldHUD(gold, -amount);
        PlayerProfileUI.Instance?.RefreshBadgeVisuals();
        return true;
    }

    private static readonly System.Collections.Generic.Dictionary<AircraftType, Sprite> _aircraftSpriteCache 
        = new System.Collections.Generic.Dictionary<AircraftType, Sprite>();

    public static Sprite GetAircraftSprite(AircraftType type)
    {
        if (_aircraftSpriteCache.TryGetValue(type, out Sprite cached) && cached != null)
            return cached;

        // 1. Önce gerçek askeri model dosyasını (Assets/Sprites/plane_*.png) yükle
        string fileName = type switch
        {
            AircraftType.F16Viper      => "plane_f16.png",
            AircraftType.A10TankBuster => "plane_a10.png",
            AircraftType.F22Raptor     => "plane_f22.png",
            AircraftType.Su57Felon     => "plane_su57.png",
            AircraftType.F35Lightning  => "plane_f35.png",
            AircraftType.KaanApex      => "plane_kaan.png",
            _                          => "plane_f16.png"
        };

        // Düşman uçağı boyutu (~1.83 birim) ve ekran oranlarına göre her uçağa özel dengeli ölçekleme:
        // F-16: Çevik & hafif kompakt avcı (~1.70 birim boy) -> 560 PPU
        // A-10: Geniş kanatlı ağır taarruz jeti (~1.82 birim boy, 1.86 birim en) -> 520 PPU
        // F-22: Uzun stealth hava üstünlüğü jeti (~1.91 birim boy, 1.60 birim en) -> 500 PPU
        // Su-57: Geniş gövdeli ağır hayalet avcı (~1.96 birim boy) -> 490 PPU
        // F-35: Kompakt tek motorlu çok rollü taarruz jeti (~1.78 birim boy) -> 530 PPU
        // KAAN: Zirve çift motorlu milli hava hâkimiyeti jeti (~2.05 birim boy) -> 470 PPU
        float ppu = type switch
        {
            AircraftType.F16Viper      => 560f,
            AircraftType.A10TankBuster => 520f,
            AircraftType.F22Raptor     => 500f,
            AircraftType.Su57Felon     => 490f,
            AircraftType.F35Lightning  => 530f,
            AircraftType.KaanApex      => 470f,
            _                          => 520f
        };

        string resName = System.IO.Path.GetFileNameWithoutExtension(fileName);
        Sprite resSpr = Resources.Load<Sprite>(resName);
        if (resSpr != null)
        {
            _aircraftSpriteCache[type] = resSpr;
            return resSpr;
        }

        string fullPath = System.IO.Path.Combine(Application.dataPath, "Sprites", fileName);
        if (System.IO.File.Exists(fullPath))
        {
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                Texture2D loadedTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (loadedTex.LoadImage(bytes))
                {
                    loadedTex.filterMode = FilterMode.Bilinear;
                    loadedTex.wrapMode = TextureWrapMode.Clamp;
                    Sprite loadedSpr = Sprite.Create(loadedTex, new Rect(0, 0, loadedTex.width, loadedTex.height), new Vector2(0.5f, 0.5f), ppu);
                    _aircraftSpriteCache[type] = loadedSpr;
                    return loadedSpr;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[HangarManager] Uçak görseli yüklenemedi: " + ex.Message);
            }
        }

        // 2. Dosya bulunamazsa yedek usulü oluştur
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] clearBlock = new Color[size * size];
        for (int i = 0; i < clearBlock.Length; i++) clearBlock[i] = clear;
        tex.SetPixels(clearBlock);

        float center = (size - 1) * 0.5f;

        if (type == AircraftType.F16Viper)
        {
            // F-16 VIPER: Çevik Deniz Kuvvetleri Gri & Kobalt Mavi Taktik Avcı
            Color cBody = new Color(0.47f, 0.57f, 0.69f, 1f);       // Naval Slate Blue-Grey
            Color cBodyDark = new Color(0.33f, 0.43f, 0.55f, 1f);   // Gölgelendirme
            Color cBodyLight = new Color(0.65f, 0.75f, 0.86f, 1f);  // Panel Işığı
            Color cAccent = new Color(0.0f, 0.82f, 1.0f, 1f);       // Siber Mavi Şerit
            Color cCockpit = new Color(0.06f, 0.12f, 0.20f, 1f);    // Kokpit Camı
            Color cCanopyGlint = new Color(0.22f, 0.74f, 0.97f, 1f);// Cam Parıltısı
            Color cMissile = new Color(0.92f, 0.94f, 0.96f, 1f);    // Sidewinder Beyaz
            Color cMissileTip = new Color(0.94f, 0.27f, 0.27f, 1f); // Kızıl Arayıcı Başlık
            Color cNozzle = new Color(0.22f, 0.25f, 0.32f, 1f);     // Titanyum Egzoz
            Color cBurner = new Color(1.0f, 0.55f, 0.0f, 1f);       // Art Yakıcı Alevi
            Color cBurnerCore = new Color(1.0f, 1.0f, 0.78f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);

                    // Art Yakıcı Egzoz Alevi (y: 8..24)
                    if (y >= 8 && y <= 24 && dx <= (y - 8) * 0.45f + 1.0f)
                    {
                        tex.SetPixel(x, y, (dx <= 2f && y <= 18) ? cBurnerCore : cBurner);
                        continue;
                    }

                    // Egzoz Nozülü (y: 20..28)
                    if (y >= 20 && y <= 28 && dx <= 7.0f)
                    {
                        tex.SetPixel(x, y, cNozzle);
                        continue;
                    }

                    // Yatay Arka Kanatçıklar (Stabilators y: 18..36)
                    if (y >= 18 && y <= 36 && dx <= (36 - y) * 1.1f + 8.0f)
                    {
                        tex.SetPixel(x, y, dx > 15f ? cBodyDark : cBody);
                        continue;
                    }

                    // Delta Ana Kanatlar (y: 38..78)
                    float wingMaxDx = (78 - y) * 1.15f + 10.0f;
                    if (y >= 38 && y <= 78 && dx <= wingMaxDx && dx >= 8.0f)
                    {
                        if (dx >= wingMaxDx - 2.5f)
                            tex.SetPixel(x, y, cBodyDark);
                        else if (dx >= 38f && dx <= 41f && y >= 44 && y <= 72)
                            tex.SetPixel(x, y, y >= 68 ? cMissileTip : cMissile);
                        else if (y >= 54 && y <= 57 && dx <= 32f)
                            tex.SetPixel(x, y, cAccent);
                        else
                            tex.SetPixel(x, y, dx < 22f ? cBodyLight : cBody);
                        continue;
                    }

                    // Ana Gövde (y: 26..95)
                    float fuseW = (y < 40) ? 6.0f : ((y < 75) ? 8.5f : 6.5f);
                    if (y >= 26 && y <= 95 && dx <= fuseW)
                    {
                        if (y >= 72 && y <= 92 && dx <= 4.0f)
                        {
                            tex.SetPixel(x, y, (dx <= 1.5f && y >= 76 && y <= 88) ? cCanopyGlint : cCockpit);
                        }
                        else if (y >= 55 && y <= 68 && dx <= 1.5f)
                        {
                            tex.SetPixel(x, y, cAccent);
                        }
                        else
                        {
                            tex.SetPixel(x, y, dx <= 3.0f ? cBodyLight : cBody);
                        }
                        continue;
                    }

                    // İğne Burun Konisi (y: 95..122)
                    if (y >= 95 && y <= 122 && dx <= (122 - y) * 0.25f + 0.8f)
                    {
                        tex.SetPixel(x, y, y >= 115 ? new Color(0.12f, 0.16f, 0.23f, 1f) : (dx <= 1.0f ? cBodyLight : cBody));
                        continue;
                    }
                }
            }
        }
        else if (type == AircraftType.A10TankBuster)
        {
            // A-10 TANK BUSTER: Ağır Askeri Kamuflaj, Çift Üst Motor & GAU-8 30mm Döner Top
            Color cCamo1 = new Color(0.29f, 0.41f, 0.22f, 1f);      // Heavy Olive Drab
            Color cCamo2 = new Color(0.22f, 0.31f, 0.16f, 1f);      // Dark Woodland
            Color cCamoLight = new Color(0.43f, 0.57f, 0.31f, 1f);  // Işık Alan Zırh
            Color cArmorDark = new Color(0.14f, 0.18f, 0.12f, 1f);  // Zırh Paneli Kenarı
            Color cCockpit = new Color(0.85f, 0.47f, 0.02f, 1f);    // Titanyum Küvet Kokpit
            Color cCanopyCore = new Color(0.99f, 0.83f, 0.30f, 1f); // Altın Zırhlı Cam Yansıması
            Color cEngineIntake = new Color(0.06f, 0.09f, 0.16f, 1f);// Fan Girişi
            Color cEngineRim = new Color(0.58f, 0.64f, 0.72f, 1f);   // Titanyum Gövde Kılıfı
            Color cGatling = new Color(0.08f, 0.08f, 0.09f, 1f);     // 30mm GAU-8 Avenger
            Color cGatlingTip = new Color(0.96f, 0.62f, 0.04f, 1f);  // Namlu Ucu Pirinç Halka
            Color cBomb = new Color(0.71f, 0.33f, 0.04f, 1f);        // Ağır Bomba Pylonları
            Color cBombFin = new Color(0.96f, 0.62f, 0.04f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);

                    // Çift Üst Monteli Turbofan Motorlar (y: 38..65, dx: 13..27)
                    if (y >= 38 && y <= 65 && dx >= 13.0f && dx <= 27.0f)
                    {
                        float edx = Mathf.Abs(dx - 20.0f);
                        if (edx <= 6.5f)
                        {
                            if (y >= 61)
                                tex.SetPixel(x, y, edx <= 4.5f ? cEngineIntake : cEngineRim);
                            else if (y <= 41)
                                tex.SetPixel(x, y, edx <= 3.5f ? new Color(1f, 0.39f, 0f, 1f) : cEngineRim);
                            else
                                tex.SetPixel(x, y, edx >= 5.0f ? cEngineRim : cCamo2);
                            continue;
                        }
                    }

                    // Çift Dikey Kuyruk Kanatçıkları (y: 10..32, dx: 31..37)
                    if (y >= 10 && y <= 32 && dx >= 31.0f && dx <= 37.0f)
                    {
                        tex.SetPixel(x, y, dx >= 35.0f ? cCamo2 : cCamo1);
                        continue;
                    }

                    // Yatay Kuyruk Kanadı (y: 16..28, dx <= 33)
                    if (y >= 16 && y <= 28 && dx <= 33.0f)
                    {
                        tex.SetPixel(x, y, cCamo1);
                        continue;
                    }

                    // GAU-8 Avenger 30mm Ağır Döner Top (y: 112..125, dx <= 3.5)
                    if (y >= 112 && y <= 125 && dx <= 3.5f)
                    {
                        tex.SetPixel(x, y, y >= 122 ? cGatlingTip : cGatling);
                        continue;
                    }

                    // Düz Geniş Ağır Kanatlar (y: 48..74, dx <= 56)
                    if (y >= 48 && y <= 74 && dx <= 56.0f)
                    {
                        if (dx >= 53.0f)
                        {
                            tex.SetPixel(x, y, cArmorDark);
                        }
                        else if (y >= 50 && y <= 68 && (Mathf.Abs(dx - 36.0f) <= 2.0f || Mathf.Abs(dx - 46.0f) <= 2.0f))
                        {
                            tex.SetPixel(x, y, y >= 64 ? cBombFin : cBomb);
                        }
                        else
                        {
                            if ((x + y) % 18 < 8) tex.SetPixel(x, y, cCamo2);
                            else if ((x * 2 - y) % 22 < 6) tex.SetPixel(x, y, cCamoLight);
                            else tex.SetPixel(x, y, cCamo1);
                        }
                        continue;
                    }

                    // Ağır Titanyum Zırhlı Gövde (y: 24..112)
                    float fuseW = (y < 50) ? 8.5f : ((y < 90) ? 11.0f : (9.5f - (y - 90) * 0.25f));
                    if (y >= 24 && y <= 112 && dx <= fuseW)
                    {
                        if (y >= 75 && y <= 96 && dx <= 6.0f)
                        {
                            tex.SetPixel(x, y, (dx <= 2.5f && y >= 79 && y <= 92) ? cCanopyCore : cCockpit);
                        }
                        else if (y >= 98)
                        {
                            tex.SetPixel(x, y, dx >= fuseW - 2.0f ? cArmorDark : cCamo1);
                        }
                        else
                        {
                            if (dx >= fuseW - 1.5f || y % 14 == 0)
                                tex.SetPixel(x, y, cArmorDark);
                            else
                                tex.SetPixel(x, y, cCamo1);
                        }
                        continue;
                    }
                }
            }
        }
        else // F-22 RAPTOR
        {
            // F-22 RAPTOR: 5. Nesil Hayalet Geometrisi, Mat Karbon & Çift Vektör Nozul
            Color cStealth = new Color(0.18f, 0.22f, 0.28f, 1f);     // Radar Soğurucu Mat Gri
            Color cStealthLight = new Color(0.33f, 0.39f, 0.49f, 1f);// Elmas Faset Yansıması
            Color cStealthDark = new Color(0.10f, 0.13f, 0.18f, 1f); // Gölge Faset
            Color cEdge = new Color(0.58f, 0.64f, 0.72f, 1f);        // Düşük Görünürlük Gümüş Kenar
            Color cCanopy = new Color(0.58f, 0.20f, 0.92f, 1f);      // İridyum Hayalet Mor Cam
            Color cCanopyGold = new Color(0.96f, 0.62f, 0.04f, 1f);  // Altın İndiyum Stealth Kaplama
            Color cReheat = new Color(0.75f, 0.52f, 0.99f, 1f);      // Plazma Vektör Egzozu
            Color cReheatCore = new Color(1.0f, 1.0f, 1.0f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);

                    // Çift 2D Düz Vektör Stealth Egzoz Nozülü (y: 8..26, dx: 3..17)
                    if (y >= 8 && y <= 26 && dx >= 3.0f && dx <= 17.0f)
                    {
                        float ndx = Mathf.Abs(dx - 10.0f);
                        if (ndx <= 5.5f)
                        {
                            if (y <= 16)
                                tex.SetPixel(x, y, ndx <= 2.0f ? cReheatCore : cReheat);
                            else
                                tex.SetPixel(x, y, ndx <= 4.0f ? new Color(0.06f, 0.09f, 0.16f, 1f) : cEdge);
                            continue;
                        }
                    }

                    // Dışa Açılı Çift Dikey Kuyruk Kanadı (y: 16..42)
                    float finCenter = 14.0f + (y - 16) * 0.55f;
                    if (y >= 16 && y <= 42 && Mathf.Abs(dx - finCenter) <= 3.0f)
                    {
                        tex.SetPixel(x, y, dx > finCenter ? cStealthLight : cStealthDark);
                        continue;
                    }

                    // Yatay Stealth Arka Kanatçıklar (y: 14..36)
                    float stabW = (36 - y) * 0.95f + 16.0f;
                    if (y >= 14 && y <= 36 && dx <= stabW && dx >= 10.0f)
                    {
                        tex.SetPixel(x, y, dx >= stabW - 2.0f ? cStealthDark : cStealth);
                        continue;
                    }

                    // 48 Derece Açılı Trapezoid Stealth Kanatlar (y: 38..82)
                    float wingMaxDx = (82 - y) * 1.05f + 14.0f;
                    float wingMinY = 38.0f + (dx - 14.0f) * 0.15f;
                    if (y >= wingMinY && y <= 82 && dx <= wingMaxDx && dx >= 11.0f)
                    {
                        if (dx >= wingMaxDx - 2.0f)
                            tex.SetPixel(x, y, cEdge);
                        else if (dx >= 28.0f && dx <= 30.0f && y >= 50 && y <= 68)
                            tex.SetPixel(x, y, cCanopy);
                        else
                            tex.SetPixel(x, y, ((x < center && y > 60) || (x > center && y < 60)) ? cStealthLight : cStealth);
                        continue;
                    }

                    // Elmas Kesim Gövde (y: 26..96)
                    float fuseW = (y < 45) ? 7.0f : (14.0f - Mathf.Abs(y - 65) * 0.18f);
                    if (y >= 26 && y <= 96 && dx <= fuseW)
                    {
                        if (y >= 72 && y <= 96 && dx <= 5.5f)
                        {
                            tex.SetPixel(x, y, (dx <= 2.0f && y >= 78 && y <= 90) ? cCanopyGold : cCanopy);
                        }
                        else if (y >= 50 && y <= 68 && Mathf.Abs(dx - 5.5f) <= 1.0f)
                        {
                            tex.SetPixel(x, y, cStealthDark);
                        }
                        else
                        {
                            tex.SetPixel(x, y, dx <= 3.0f ? cStealthLight : cStealth);
                        }
                        continue;
                    }

                    // Bıçak Sırtı Stealth Burun Konisi (y: 96..124)
                    if (y >= 96 && y <= 124 && dx <= (124 - y) * 0.28f + 0.6f)
                    {
                        tex.SetPixel(x, y, y >= 118 ? cEdge : (dx <= 1.5f ? cStealthLight : cStealth));
                        continue;
                    }
                }
            }
        }

        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
        _aircraftSpriteCache[type] = spr;
        return spr;
    }

    public static int GetAircraftRequiredLevel(AircraftType type)
    {
        return type switch
        {
            AircraftType.F16Viper       => 1,   // Başlangıç Jeti
            AircraftType.A10TankBuster  => 15,  // Ağır Zırhlı Taarruz
            AircraftType.F22Raptor      => 30,  // 5. Nesil Hayalet Vulcan
            AircraftType.Su57Felon      => 50,  // 5. Nesil Süper-Manevra Plazma
            AircraftType.F35Lightning   => 70,  // 5. Nesil 3'lü Taktik Salvo
            AircraftType.KaanApex       => 90,  // 5.+ Nesil Hava Hâkimiyeti Zirvesi
            _ => 1
        };
    }

    public static int GetAircraftUnlockCost(AircraftType type)
    {
        return type switch
        {
            AircraftType.F16Viper       => 0,
            AircraftType.A10TankBuster  => A10_UNLOCK_COST,
            AircraftType.F22Raptor      => F22_UNLOCK_COST,
            AircraftType.Su57Felon      => SU57_UNLOCK_COST,
            AircraftType.F35Lightning   => F35_UNLOCK_COST,
            AircraftType.KaanApex       => KAAN_UNLOCK_COST,
            _ => 0
        };
    }

    public static string GetAircraftDisplayName(AircraftType type)
    {
        return type switch
        {
            AircraftType.F16Viper       => "F-16 Viper",
            AircraftType.A10TankBuster  => "A-10 Tank Buster",
            AircraftType.F22Raptor      => "F-22 Raptor",
            AircraftType.Su57Felon      => "Su-57 Felon",
            AircraftType.F35Lightning   => "F-35 Lightning II",
            AircraftType.KaanApex       => "KAAN Apex Fighter",
            _ => "Bilinmeyen Jet"
        };
    }

    public bool IsAircraftUnlocked(AircraftType type)
    {
        if (type == AircraftType.F16Viper) return true;

        return type switch
        {
            AircraftType.A10TankBuster => PlayerPrefs.GetInt(PREF_UNLOCKED_A10, 0) == 1,
            AircraftType.F22Raptor     => PlayerPrefs.GetInt(PREF_UNLOCKED_F22, 0) == 1,
            AircraftType.Su57Felon     => PlayerPrefs.GetInt(PREF_UNLOCKED_SU57, 0) == 1,
            AircraftType.F35Lightning  => PlayerPrefs.GetInt(PREF_UNLOCKED_F35, 0) == 1,
            AircraftType.KaanApex      => PlayerPrefs.GetInt(PREF_UNLOCKED_KAAN, 0) == 1,
            _ => false
        };
    }

    public bool TryUnlockAircraft(AircraftType type)
    {
        if (type == AircraftType.F16Viper) return true;

        int playerLvl = PlayerProfileManager.EnsureInstance()?.level ?? 1;
        int reqLvl = GetAircraftRequiredLevel(type);
        if (playerLvl < reqLvl) return false;

        int cost = GetAircraftUnlockCost(type);
        if (gold >= cost && !IsAircraftUnlocked(type))
        {
            gold -= cost;
            switch (type)
            {
                case AircraftType.A10TankBuster: PlayerPrefs.SetInt(PREF_UNLOCKED_A10, 1); break;
                case AircraftType.F22Raptor:     PlayerPrefs.SetInt(PREF_UNLOCKED_F22, 1); break;
                case AircraftType.Su57Felon:     PlayerPrefs.SetInt(PREF_UNLOCKED_SU57, 1); break;
                case AircraftType.F35Lightning:  PlayerPrefs.SetInt(PREF_UNLOCKED_F35, 1); break;
                case AircraftType.KaanApex:      PlayerPrefs.SetInt(PREF_UNLOCKED_KAAN, 1); break;
            }

            activeAircraft = type;
            SaveHangarData();
            var gameUI = FindObjectOfType<GameUI>();
            gameUI?.UpdateGoldHUD(gold, -cost);
            return true;
        }
        return false;
    }

    public void SelectAircraft(AircraftType type)
    {
        if (IsAircraftUnlocked(type))
        {
            activeAircraft = type;
            SaveHangarData();
        }
    }

    public bool TryUpgradeArmor()
    {
        if (armorLevel < MAX_UPGRADE_LEVEL && gold >= ArmorCosts[armorLevel])
        {
            gold -= ArmorCosts[armorLevel];
            armorLevel++;
            SaveHangarData();
            return true;
        }
        return false;
    }

    public bool TryUpgradeDamage()
    {
        if (damageLevel < MAX_UPGRADE_LEVEL && gold >= DamageCosts[damageLevel])
        {
            gold -= DamageCosts[damageLevel];
            damageLevel++;
            SaveHangarData();
            return true;
        }
        return false;
    }

    public bool TryUpgradeSpeed()
    {
        if (speedLevel < MAX_UPGRADE_LEVEL && gold >= SpeedCosts[speedLevel])
        {
            gold -= SpeedCosts[speedLevel];
            speedLevel++;
            SaveHangarData();
            return true;
        }
        return false;
    }

    public bool TryUpgradeCritical()
    {
        if (criticalLevel < MAX_UPGRADE_LEVEL && gold >= CritCosts[criticalLevel])
        {
            gold -= CritCosts[criticalLevel];
            criticalLevel++;
            SaveHangarData();
            return true;
        }
        return false;
    }

    // ==================== DRONE YÖNETİMİ (KADEME KİLİTLERİ, KAT KAT FİYATLAR & DAYANIKLILIK) ====================
    public static int GetDroneRequiredLevel(DroneCompanion.DroneType type)
    {
        return type switch
        {
            DroneCompanion.DroneType.MK1_Scout       => 1,
            DroneCompanion.DroneType.MK2_Interceptor => 20,
            DroneCompanion.DroneType.MK3_Striker     => 30,
            DroneCompanion.DroneType.MK4_Missile     => 50,
            DroneCompanion.DroneType.MK5_TitanAegis  => 75,
            _ => 1
        };
    }

    public static int GetDroneCost(DroneCompanion.DroneType type)
    {
        return type switch
        {
            DroneCompanion.DroneType.MK1_Scout       => 400,
            DroneCompanion.DroneType.MK2_Interceptor => 2500,
            DroneCompanion.DroneType.MK3_Striker     => 7500,
            DroneCompanion.DroneType.MK4_Missile     => 20000,
            DroneCompanion.DroneType.MK5_TitanAegis  => 50000,
            _ => 400
        };
    }

    public static float GetDroneMaxHealth(DroneCompanion.DroneType type)
    {
        return type switch
        {
            DroneCompanion.DroneType.MK1_Scout       => 90f,
            DroneCompanion.DroneType.MK2_Interceptor => 150f,
            DroneCompanion.DroneType.MK3_Striker     => 240f,
            DroneCompanion.DroneType.MK4_Missile     => 340f,
            DroneCompanion.DroneType.MK5_TitanAegis  => 500f,
            _ => 100f
        };
    }

    public static int GetDroneRepairCost(DroneCompanion.DroneType type)
    {
        return type switch
        {
            DroneCompanion.DroneType.MK1_Scout       => 80,
            DroneCompanion.DroneType.MK2_Interceptor => 500,
            DroneCompanion.DroneType.MK3_Striker     => 1500,
            DroneCompanion.DroneType.MK4_Missile     => 4000,
            DroneCompanion.DroneType.MK5_TitanAegis  => 10000,
            _ => 80
        };
    }

    public static string GetDroneDisplayName(DroneCompanion.DroneType type)
    {
        return type switch
        {
            DroneCompanion.DroneType.MK1_Scout       => "MK-1 İzci",
            DroneCompanion.DroneType.MK2_Interceptor => "MK-2 Avcı",
            DroneCompanion.DroneType.MK3_Striker     => "MK-3 Taarruz",
            DroneCompanion.DroneType.MK4_Missile     => "MK-4 Valkyrie",
            DroneCompanion.DroneType.MK5_TitanAegis  => "MK-5 Titan Aegis",
            _                                        => "BOŞ"
        };
    }

    public bool IsDroneOwned(DroneCompanion.DroneType type)
    {
        if (type == DroneCompanion.DroneType.None) return false;

        // Seviye şartı karşılanmıyorsa drone kesinlikle kilitlidir
        int playerLvl = PlayerProfileManager.EnsureInstance()?.level ?? 1;
        if (playerLvl < GetDroneRequiredLevel(type)) return false;

        return PlayerPrefs.GetInt(PREF_DRONE_UNLOCKED_PREFIX + (int)type, 0) == 1;
    }

    public bool IsDroneUnlocked(DroneCompanion.DroneType type)
    {
        // Operasyonel ve savaşa hazır: Sahip olunmalı ve canı 0'dan büyük olmalı
        return IsDroneOwned(type) && GetDroneHealth(type) > 0f;
    }

    public int GetDroneDeaths(DroneCompanion.DroneType type)
    {
        if (type == DroneCompanion.DroneType.None) return 0;
        return PlayerPrefs.GetInt(PREF_DRONE_DEATHS_PREFIX + (int)type, 0);
    }

    public void SetDroneDeaths(DroneCompanion.DroneType type, int deaths)
    {
        if (type == DroneCompanion.DroneType.None) return;
        PlayerPrefs.SetInt(PREF_DRONE_DEATHS_PREFIX + (int)type, Mathf.Clamp(deaths, 0, MAX_DRONE_DEATHS));
        PlayerPrefs.Save();
    }

    public float GetDroneHealth(DroneCompanion.DroneType type)
    {
        if (type == DroneCompanion.DroneType.None) return 0f;
        return PlayerPrefs.GetFloat(PREF_DRONE_HP_PREFIX + (int)type, 0f);
    }

    public void SetDroneHealth(DroneCompanion.DroneType type, float hp)
    {
        if (type == DroneCompanion.DroneType.None) return;
        float maxHp = GetDroneMaxHealth(type);
        float clamped = Mathf.Clamp(hp, 0f, maxHp);
        PlayerPrefs.SetFloat(PREF_DRONE_HP_PREFIX + (int)type, clamped);

        if (clamped <= 0f)
        {
            // Canı bitti! Savaş kanadından düşer
            if (leftDroneType == type) leftDroneType = DroneCompanion.DroneType.None;
            if (rightDroneType == type) rightDroneType = DroneCompanion.DroneType.None;
            SaveHangarData();
        }
        else
        {
            PlayerPrefs.Save();
        }
    }

    public bool TryBuyDrone(DroneCompanion.DroneType type)
    {
        int playerLvl = PlayerProfileManager.EnsureInstance()?.level ?? 1;
        int reqLvl = GetDroneRequiredLevel(type);
        if (playerLvl < reqLvl) return false; // Seviye yetersiz, satın alınamaz!

        int cost = GetDroneCost(type);
        if (gold >= cost)
        {
            gold -= cost;
            PlayerPrefs.SetInt(PREF_DRONE_UNLOCKED_PREFIX + (int)type, 1);
            SetDroneDeaths(type, 0); // Yeni satın alımda can hakkı sıfırlanır (2 hak hazır)
            SetDroneHealth(type, GetDroneMaxHealth(type));

            // Otomatik boş kanada kuşan
            if (leftDroneType == DroneCompanion.DroneType.None) leftDroneType = type;
            else if (rightDroneType == DroneCompanion.DroneType.None) rightDroneType = type;

            SaveHangarData();
            var gameUI = FindObjectOfType<GameUI>();
            gameUI?.UpdateGoldHUD(gold, -cost);
            return true;
        }
        return false;
    }

    public bool TryRepairDrone(DroneCompanion.DroneType type)
    {
        if (!IsDroneOwned(type)) return false;
        float currentHp = GetDroneHealth(type);
        float maxHp = GetDroneMaxHealth(type);
        if (currentHp >= maxHp) return false;

        int cost = GetDroneRepairCost(type);
        if (gold >= cost)
        {
            gold -= cost;
            SetDroneHealth(type, maxHp);
            SaveHangarData();
            var gameUI = FindObjectOfType<GameUI>();
            gameUI?.UpdateGoldHUD(gold, -cost);
            return true;
        }
        return false;
    }

    public void OnDroneDestroyed(DroneCompanion.DroneType type)
    {
        if (type == DroneCompanion.DroneType.None) return;

        SetDroneHealth(type, 0f);

        int deaths = GetDroneDeaths(type) + 1;
        SetDroneDeaths(type, deaths);

        if (deaths >= MAX_DRONE_DEATHS)
        {
            // 2. İmha: Drone tamamen sıfırlanır, tekrar satın alınmalı (0/2 hak)
            PlayerPrefs.SetInt(PREF_DRONE_UNLOCKED_PREFIX + (int)type, 0);
            SetDroneDeaths(type, 0);
            Debug.Log($"[HangarManager] {type} 2. kez imha oldu! Tamamen sıfırlandı ve kilitlendi.");
        }
        else
        {
            // 1. İmha: Drone ağır hasarlı/enkaz durumunda Hangar'da onarım bekler (1/2 hak)
            Debug.Log($"[HangarManager] {type} 1. kez imha oldu! Hangar'da altın karşılığı onarılabilir.");
        }

        SaveHangarData();
    }

    // Geriye dönük uyumluluk yardımcıları
    public int GetDroneUnlockCost(DroneCompanion.DroneType type) => GetDroneCost(type);
    public bool TryUnlockDrone(DroneCompanion.DroneType type) => TryBuyDrone(type);
    public void UnlockDrone(DroneCompanion.DroneType type)
    {
        if (type == DroneCompanion.DroneType.None) return;
        PlayerPrefs.SetInt(PREF_DRONE_UNLOCKED_PREFIX + (int)type, 1);
        SetDroneDeaths(type, 0);
        SetDroneHealth(type, GetDroneMaxHealth(type));
        SaveHangarData();
    }
    public int GetDroneLevel(DroneCompanion.DroneType type) => (int)type;
    public bool TryUpgradeDrone(DroneCompanion.DroneType type) => TryRepairDrone(type);

    public void EquipDrone(DroneCompanion.DroneType type, bool isLeft)
    {
        if (!IsDroneUnlocked(type)) return;

        if (isLeft)
            leftDroneType = (leftDroneType == type) ? DroneCompanion.DroneType.None : type;
        else
            rightDroneType = (rightDroneType == type) ? DroneCompanion.DroneType.None : type;

        SaveHangarData();
    }

    // ==================== ASKERİ İKMAL SANDIKLARI (GACHA / LOOT) ====================
    /// <summary>
    /// Sandıklar artık kesinlikle ilgili anahtar (Bronz, Gümüş, Altın) gerektirir.
    /// Altınla açma bypass'ı tamamen kaldırılmıştır; sadece anahtarla açılır.
    /// </summary>
    public CrateReward OpenCrate(CrateType type)
    {
        return OpenCrateWithKey(type);
    }

    /// <summary>
    /// Sandığı özel düşen anahtar (Bronz, Gümüş, Altın) ile açar.
    /// Her sandıktan çok üst düzey tek kullanımlık uçaklar, en üst düzey dronelar (MK-5 Titan),
    /// veya 10.000 - 30.000 Altınlık en nadir askeri geliştirme parçaları çıkar!
    /// </summary>
    public CrateReward OpenCrateWithKey(CrateType type)
    {
        LoadHangarData();
        KeyType keyNeeded = type switch
        {
            CrateType.Bronze  => KeyType.Bronze,
            CrateType.Silver  => KeyType.Silver,
            CrateType.GoldAce => KeyType.Gold,
            _                 => KeyType.Bronze
        };

        if (GetKeyCount(keyNeeded) <= 0) return null;
        if (!ConsumeKey(keyNeeded)) return null;

        CrateReward reward = new CrateReward();
        MilitaryStoreManager store = MilitaryStoreManager.EnsureInstance();
        float roll = UnityEngine.Random.value;

        if (type == CrateType.GoldAce)
        {
            // ==========================================
            // ALTIN ANAHTAR (1/100 Efsanevi Anahtar Ödülleri)
            // ==========================================
            if (roll < 0.35f)
            {
                // ÖDÜL 1: Çok Üst Düzey Tek Kullanımlık Süper Uçak (veya kalıcı süper jet kilidi!)
                if (!IsAircraftUnlocked(AircraftType.KaanApex) && UnityEngine.Random.value < 0.40f)
                {
                    PlayerPrefs.SetInt(PREF_UNLOCKED_KAAN, 1);
                    reward.title = "[EFSANE] 5.+ NESİL KAAN APEX AÇILDI!";
                    reward.description = "MİLLİ HAVA HÂKİMİYETİ ZİRVESİ! KAAN Apex savaş uçağı filonuza kalıcı olarak katıldı!";
                    reward.rarityColorHex = "#FFD700";
                }
                else
                {
                    AddSingleUseSuperPlane(1);
                    reward.title = "[PROTOTİP] 6. NESİL DARKSTAR SÜPER AVCI!";
                    reward.description = "Tek kullanımlık gizli 6. Nesil Prototip Jet kazandınız!\nBir sonraki harekâtta devasa +160 Can, 3'lü Plazma Salvosu ve zırh kalkanı ile uçacaksınız!";
                    reward.rarityColorHex = "#00FFFF";
                }
            }
            else if (roll < 0.68f)
            {
                // ÖDÜL 2: En Üst Düzey Drone (MK-5 Titan Aegis - Apex Nihai İHA)
                UnlockDrone(DroneCompanion.DroneType.MK5_TitanAegis);
                SetDroneHealth(DroneCompanion.DroneType.MK5_TitanAegis, GetDroneMaxHealth(DroneCompanion.DroneType.MK5_TitanAegis));

                if (rightDroneType != DroneCompanion.DroneType.MK5_TitanAegis)
                    rightDroneType = DroneCompanion.DroneType.MK5_TitanAegis;
                else
                    leftDroneType = DroneCompanion.DroneType.MK5_TitanAegis;

                PlayerPrefs.SetInt(PREF_DRONE_LEFT, (int)leftDroneType);
                PlayerPrefs.SetInt(PREF_DRONE_RIGHT, (int)rightDroneType);

                reward.title = "[NİHAİ İHA] MK-5 TİTAN AEGIS AÇILDI!";
                reward.description = "Oyunun en üst düzey Apex Dronu! Düşman mermilerini yutan enerji kalkanı ve plazma salvosuyla donatıldı!";
                reward.rarityColorHex = "#FF0055";
            }
            else
            {
                // ÖDÜL 3: 26.000 - 30.000 Altınlık En Pahalı ve Nadir Askeri Parça
                string[] highTierParts = {
                    "air_kaan_graphene",      // 30.000G
                    "ew_kaan_cyber_suite",   // 28.000G
                    "eng_kaan_adaptive",     // 26.000G
                    "radar_aselsan_murad",   // 25.000G
                    "eng_al41_3dtvc"         // 10.500G
                };

                string awardedPartId = highTierParts[0];
                foreach (var pid in highTierParts)
                {
                    if (store != null && !store.IsPartUnlocked(pid))
                    {
                        awardedPartId = pid;
                        break;
                    }
                }

                AircraftPart partObj = store?.GetPartById(awardedPartId);
                store?.UnlockPartDirectly(awardedPartId);

                string pName = partObj != null ? partObj.name : "KAAN Grafen Kompozit Zırh";
                reward.title = "[EFSANEVİ] ASKERİ PARÇA HEDİYESİ!";
                reward.description = $"{pName} (Değer: 30.000 Altın) envanterinize kalıcı olarak eklendi!";
                reward.rarityColorHex = "#FFA500";
            }
        }
        else if (type == CrateType.Silver)
        {
            // ==========================================
            // GÜMÜŞ ANAHTAR Ödülleri
            // ==========================================
            if (roll < 0.35f)
            {
                AddSingleUseSuperPlane(1);
                reward.title = "[SORTİ] PROTOTİP SÜPER AVCI KUPONU!";
                reward.description = "Tek kullanımlık 6. Nesil Süper Avcı Sortisi kazandınız! Savaşa devasa güçlendirmeyle başlayacaksınız.";
                reward.rarityColorHex = "#00E5FF";
            }
            else if (roll < 0.68f)
            {
                // Üst Düzey Drone (MK-4 Valkyrie Roket İHA)
                DroneCompanion.DroneType awardedDrone = DroneCompanion.DroneType.MK4_Missile;
                UnlockDrone(awardedDrone);
                SetDroneHealth(awardedDrone, GetDroneMaxHealth(awardedDrone));
                if (rightDroneType == DroneCompanion.DroneType.None)
                    rightDroneType = awardedDrone;

                reward.title = "[FÜZELİ İHA] MK-4 VALKYRIE AÇILDI!";
                reward.description = "Güdümlü mikro roketlerle donatılmış ağır taarruz dronu filonuza katıldı!";
                reward.rarityColorHex = "#9B59B6";
            }
            else
            {
                // 5.000 - 11.500 Altınlık Tier 3-4 Parça
                string[] midTierParts = {
                    "air_carbon_matrix",    // 11.500G
                    "ew_aaq24_dircm",      // 11.000G
                    "ew_drfm_jammer",       // 5.000G
                    "eng_f119_2dtvc"        // 4.800G
                };
                string awardedPartId = midTierParts[UnityEngine.Random.Range(0, midTierParts.Length)];
                foreach (var pid in midTierParts)
                {
                    if (store != null && !store.IsPartUnlocked(pid))
                    {
                        awardedPartId = pid;
                        break;
                    }
                }
                AircraftPart partObj = store?.GetPartById(awardedPartId);
                store?.UnlockPartDirectly(awardedPartId);

                string pName = partObj != null ? partObj.name : "Karbon Nanotüp Matris Gövde";
                reward.title = "[DONANIM] GELİŞMİŞ PARÇA HEDİYESİ!";
                reward.description = $"{pName} ücretsiz açıldı ve askeri envantere eklendi!";
                reward.rarityColorHex = "#3498DB";
            }
        }
        else // Bronze Key
        {
            // ==========================================
            // BRONZ ANAHTAR Ödülleri
            // ==========================================
            if (roll < 0.35f)
            {
                // MK-2 Avcı Drone
                DroneCompanion.DroneType awardedDrone = DroneCompanion.DroneType.MK2_Interceptor;
                UnlockDrone(awardedDrone);
                SetDroneHealth(awardedDrone, GetDroneMaxHealth(awardedDrone));
                reward.title = "[İHA] MK-2 AVCI DRONU HEDİYESİ!";
                reward.description = "Seri çift siber lazerli avcı destek dronu kanadınıza monte edildi!";
                reward.rarityColorHex = "#2ECC71";
            }
            else if (roll < 0.70f)
            {
                // 1.800 - 2.000 Altınlık Donatım Parçası
                string[] lowTierParts = {
                    "eng_tf34_turbofan",    // 1.800G
                    "ew_alq131_pod",        // 1.900G
                    "air_titanium_tub"      // 2.000G
                };
                string awardedPartId = lowTierParts[UnityEngine.Random.Range(0, lowTierParts.Length)];
                foreach (var pid in lowTierParts)
                {
                    if (store != null && !store.IsPartUnlocked(pid))
                    {
                        awardedPartId = pid;
                        break;
                    }
                }
                AircraftPart partObj = store?.GetPartById(awardedPartId);
                store?.UnlockPartDirectly(awardedPartId);

                string pName = partObj != null ? partObj.name : "GE TF34 Çift Turbofan";
                reward.title = "[PARÇA] ASKERİ DONATIM PARÇASI!";
                reward.description = $"{pName} envanterinize eklendi!";
                reward.rarityColorHex = "#CD7F32";
            }
            else
            {
                int goldWon = UnityEngine.Random.Range(1500, 3000);
                gold += goldWon;
                reward.title = "[ALTIN] ASKERİ ÖZEL İKMAL ALTINI!";
                reward.description = $"+{goldWon:N0} Altın doğrudan kasaya aktarıldı!";
                reward.goldGain = goldWon;
                reward.rarityColorHex = "#F1C40F";
            }
        }

        totalCratesOpened++;
        reward.description += "\n<color=#FFD700>» ÖZEL ANAHTAR ÖDÜLÜ «</color>";
        SaveHangarData();

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateGoldHUD(gold, reward.goldGain);

        return reward;
    }

    /// <summary>
    /// Seçili uçağın temel özelliklerini ve kalıcı atölye geliştirmelerini oyuncunun uçağına uygular.
    /// </summary>
    public void ApplyActiveAircraftStats(PlaneController pc, bool isActualCombatStart = false)
    {
        if (pc == null || !pc.isPlayer1) return;

        LoadHangarData();

        PlaneHealth health = pc.GetComponent<PlaneHealth>();
        PlaneShooter shooter = pc.GetComponent<PlaneShooter>();
        SpriteRenderer sr = pc.GetComponent<SpriteRenderer>();

        float baseHp = 100f;
        float baseSpd = 8.0f;
        float baseDmg = 20f;
        int baseBullets = 1;
        float baseCrit = 0f;
        Color planeTint = Color.white;

        switch (activeAircraft)
        {
            case AircraftType.A10TankBuster:
                baseHp = 180f;
                baseSpd = 6.2f;
                baseDmg = 32f;
                baseBullets = 1;
                planeTint = new Color(0.68f, 0.82f, 0.60f); // Kamuflaj Yeşili
                break;

            case AircraftType.F22Raptor:
                baseHp = 120f;
                baseSpd = 10.5f;
                baseDmg = 24f;
                baseBullets = 2; // Çift Namlu Vulcan Topu
                planeTint = new Color(0.55f, 0.68f, 0.88f); // Hayalet Mavi-Gri
                break;

            case AircraftType.Su57Felon:
                baseHp = 160f;
                baseSpd = 9.8f;
                baseDmg = 28f;
                baseBullets = 2; // Çift Ağır Plazma Topu
                baseCrit = 15f; // %15 Kritik Vuruş Şansı
                planeTint = new Color(0.45f, 0.60f, 0.75f); // Rus Dijital Kamuflaj
                break;

            case AircraftType.F35Lightning:
                baseHp = 150f;
                baseSpd = 11.2f;
                baseDmg = 22f;
                baseBullets = 3; // Üçlü Taktik Yaylım Atışı
                baseCrit = 10f; // %10 Kritik Vuruş Şansı
                planeTint = new Color(0.40f, 0.45f, 0.55f); // Karbon Hayalet Gri
                break;

            case AircraftType.KaanApex:
                baseHp = 220f;
                baseSpd = 12.0f;
                baseDmg = 34f;
                baseBullets = 3; // Üçlü Ağır Plazma & Merkez Ağır Salvo
                baseCrit = 25f; // %25 Kritik Vuruş Şansı
                planeTint = new Color(0.50f, 0.55f, 0.65f); // Milli As Titanyum
                break;

            default: // F-16 Viper
                baseHp = 100f;
                baseSpd = 8.0f;
                baseDmg = 20f;
                baseBullets = 1;
                baseCrit = 0f;
                planeTint = Color.white;
                break;
        }

        // Sandık kalıcı çip bonusları ve Askeri Mağaza donatım parçaları eklenir
        MilitaryStoreManager.LoadoutStats loadout = default;
        if (MilitaryStoreManager.Instance != null || MilitaryStoreManager.EnsureInstance() != null)
        {
            loadout = MilitaryStoreManager.EnsureInstance().GetLoadoutStats(activeAircraft);
        }

        float finalHp = baseHp + chipBonusHealth + loadout.bonusHp;
        float finalSpd = baseSpd + loadout.bonusSpeed;
        float finalDmg = baseDmg + chipBonusDamage + loadout.bonusDamage;
        float finalCrit = baseCrit + loadout.bonusCrit;

        // TEK KULLANIMLIK 6. NESİL SÜPER AVCI SORTİSİ KONTROLÜ (Sadece gerçek savaş başladığında uygulanır)
        if (isActualCombatStart && singleUseSuperPlanes > 0 && isSingleUseSuperPlaneActive)
        {
            ConsumeSingleUseSuperPlane();
            finalHp += 160f;
            finalSpd += 3.0f;
            finalDmg += 15f;
            baseBullets = Mathf.Max(baseBullets, 3);
            finalCrit += 25f;

            var gameUI = FindObjectOfType<GameUI>();
            gameUI?.ShowWaveAlert("[6. NESİL] <b>[ PROTOTİP SÜPER AVCI SORTİSİ AKTİF! ]</b>\n<color=#FFD700>+160 Can | +15 Hasar | 3'lü Plazma Salvosu</color>", new Color(0f, 1f, 0.9f), 3.5f);
        }

        pc.moveSpeed = finalSpd;

        if (health != null)
        {
            health.damageReductionPercent = Mathf.Clamp(loadout.damageReductionPercent, 0f, 0.50f);
            health.ResetHealth(finalHp);
        }

        if (shooter != null)
        {
            shooter.bulletDamage = finalDmg;
            shooter.bulletsPerShot = baseBullets;
            shooter.criticalChance = finalCrit;
        }

        // Askeri Zırh / EW Kalkanı Uygula
        if (loadout.bonusShield > 0f)
        {
            PlayerUpgradeManager.EnsureInstance()?.GrantShield(loadout.bonusShield);
        }

        // İtki Yönlendirme ve Elektronik Harp Manevra Bonusları
        if (PlayerCombatManeuvers.Instance != null)
        {
            if (loadout.dodgeCdReduction > 0f)
            {
                PlayerCombatManeuvers.Instance.dodgeCooldown = Mathf.Max(1.0f, 3.0f - loadout.dodgeCdReduction);
            }
            if (loadout.ultiBonusPercent > 0f)
            {
                PlayerCombatManeuvers.Instance.ultiChargeMultiplier = 1.0f + loadout.ultiBonusPercent;
            }
        }

        // Uçak ölçeğini düşman uçağı ve ekranla kusursuz uyumlu kıl
        pc.transform.localScale = Vector3.one;

        if (sr != null)
        {
            sr.sprite = GetAircraftSprite(activeAircraft);
            sr.color = Color.white;
        }

        // Destek Droneları'nı Kanatlara Tak
        DroneManager.EnsureInstance()?.SpawnDronesForPlayer(pc.transform);
    }
}

