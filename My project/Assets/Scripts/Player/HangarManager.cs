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
        F16Viper,       // Dengeli Taktik Avcı
        A10TankBuster,  // Ağır Zırhlı Taarruz Uçağı
        F22Raptor       // Görünmez Hayalet Süpersonik Jet
    }

    public enum CrateType
    {
        Bronze,     // 150 G - Bronz İkmal Kasası
        Silver,     // 450 G - Gümüş Cephane Kasası
        GoldAce     // 1100 G - Altın As Sandığı
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

    private const string PREF_LVL_ARMOR = "AirBattle_Lvl_Armor";
    private const string PREF_LVL_DAMAGE = "AirBattle_Lvl_Damage";
    private const string PREF_LVL_SPEED = "AirBattle_Lvl_Speed";
    private const string PREF_LVL_CRIT = "AirBattle_Lvl_Crit";

    // Drone Prefs (5 Aşamalı Model & Can / Dayanıklılık Sistemi)
    public const string PREF_DRONE_LEFT = "AirBattle_Drone_Left";
    public const string PREF_DRONE_RIGHT = "AirBattle_Drone_Right";
    public const string PREF_DRONE_UNLOCKED_PREFIX = "AirBattle_Drone_Unl_";
    public const string PREF_DRONE_HP_PREFIX = "AirBattle_Drone_HP_";

    // Kalıcı Çip Bonusları
    private const string PREF_CHIP_BONUS_DMG = "AirBattle_Chip_Bonus_Dmg";
    private const string PREF_CHIP_BONUS_HP = "AirBattle_Chip_Bonus_Hp";
    private const string PREF_TOTAL_CRATES = "AirBattle_Total_Crates";

    [Header("Mevcut Durum")]
    public AircraftType activeAircraft = AircraftType.F16Viper;
    public int gold = 25; // Başlangıç hoş geldin hediyesi
    public int totalCratesOpened = 0;

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

    public const int A10_UNLOCK_COST = 400;
    public const int F22_UNLOCK_COST = 900;

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
        gold = PlayerPrefs.GetInt(PREF_GOLD, 25);
        activeAircraft = (AircraftType)PlayerPrefs.GetInt(PREF_ACTIVE_PLANE, (int)AircraftType.F16Viper);

        armorLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LVL_ARMOR, 0), 0, MAX_UPGRADE_LEVEL);
        damageLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LVL_DAMAGE, 0), 0, MAX_UPGRADE_LEVEL);
        speedLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LVL_SPEED, 0), 0, MAX_UPGRADE_LEVEL);
        criticalLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LVL_CRIT, 0), 0, MAX_UPGRADE_LEVEL);

        leftDroneType = (DroneCompanion.DroneType)PlayerPrefs.GetInt(PREF_DRONE_LEFT, (int)DroneCompanion.DroneType.None);
        rightDroneType = (DroneCompanion.DroneType)PlayerPrefs.GetInt(PREF_DRONE_RIGHT, (int)DroneCompanion.DroneType.None);

        // Canı bitmiş veya alınmamış drone takılı kalamaz
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

        PlayerPrefs.Save();
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
    }

    public bool IsAircraftUnlocked(AircraftType type)
    {
        switch (type)
        {
            case AircraftType.F16Viper: return true;
            case AircraftType.A10TankBuster: return PlayerPrefs.GetInt(PREF_UNLOCKED_A10, 0) == 1;
            case AircraftType.F22Raptor: return PlayerPrefs.GetInt(PREF_UNLOCKED_F22, 0) == 1;
            default: return false;
        }
    }

    public bool TryUnlockAircraft(AircraftType type)
    {
        int cost = (type == AircraftType.A10TankBuster) ? A10_UNLOCK_COST : F22_UNLOCK_COST;
        if (gold >= cost && !IsAircraftUnlocked(type))
        {
            gold -= cost;
            if (type == AircraftType.A10TankBuster) PlayerPrefs.SetInt(PREF_UNLOCKED_A10, 1);
            if (type == AircraftType.F22Raptor) PlayerPrefs.SetInt(PREF_UNLOCKED_F22, 1);

            activeAircraft = type;
            SaveHangarData();
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
            DroneCompanion.DroneType.MK1_Scout       => 20,
            DroneCompanion.DroneType.MK2_Interceptor => 30,
            DroneCompanion.DroneType.MK3_Striker     => 50,
            DroneCompanion.DroneType.MK4_Missile     => 75,
            DroneCompanion.DroneType.MK5_TitanAegis  => 100,
            _ => 20
        };
    }

    public static int GetDroneCost(DroneCompanion.DroneType type)
    {
        return type switch
        {
            DroneCompanion.DroneType.MK1_Scout       => 1000,
            DroneCompanion.DroneType.MK2_Interceptor => 5000,
            DroneCompanion.DroneType.MK3_Striker     => 15000,
            DroneCompanion.DroneType.MK4_Missile     => 40000,
            DroneCompanion.DroneType.MK5_TitanAegis  => 100000,
            _ => 1000
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
            DroneCompanion.DroneType.MK1_Scout       => 200,
            DroneCompanion.DroneType.MK2_Interceptor => 1000,
            DroneCompanion.DroneType.MK3_Striker     => 3000,
            DroneCompanion.DroneType.MK4_Missile     => 8000,
            DroneCompanion.DroneType.MK5_TitanAegis  => 20000,
            _ => 200
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

    public bool IsDroneUnlocked(DroneCompanion.DroneType type)
    {
        if (type == DroneCompanion.DroneType.None) return false;
        return PlayerPrefs.GetInt(PREF_DRONE_UNLOCKED_PREFIX + (int)type, 0) == 1 && GetDroneHealth(type) > 0f;
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
            // Canı bitti! Drone parçalandı, kanattan çıkar ve kilidi kapat (yeniden satın alınabilsin)
            PlayerPrefs.SetInt(PREF_DRONE_UNLOCKED_PREFIX + (int)type, 0);
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
        if (!IsDroneUnlocked(type)) return false;
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
        SetDroneHealth(type, 0f);
    }

    // Geriye dönük uyumluluk yardımcıları
    public int GetDroneUnlockCost(DroneCompanion.DroneType type) => GetDroneCost(type);
    public bool TryUnlockDrone(DroneCompanion.DroneType type) => TryBuyDrone(type);
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
    public CrateReward OpenCrate(CrateType type)
    {
        int cost = type switch
        {
            CrateType.Bronze  => CRATE_BRONZE_COST,
            CrateType.Silver  => CRATE_SILVER_COST,
            CrateType.GoldAce => CRATE_GOLD_COST,
            _                 => 0
        };

        if (gold < cost) return null;

        gold -= cost;
        CrateReward reward = new CrateReward();
        float roll = UnityEngine.Random.value;

        if (type == CrateType.Bronze)
        {
            if (roll < 0.40f)
            {
                int goldWon = UnityEngine.Random.Range(100, 250);
                gold += goldWon;
                reward.title = "BRONZ KASA İKRAMİYESİ!";
                reward.description = $"+{goldWon} Altın kasaya aktarıldı!";
                reward.goldGain = goldWon;
                reward.rarityColorHex = "#2ECC71"; // Yeşil
            }
            else if (roll < 0.75f)
            {
                chipBonusDamage += 1.0f;
                reward.title = "TAKTİK MERMİ ÇİPİ!";
                reward.description = "Tüm uçaklara kalıcı +1.0 Mermi Hasarı eklendi!";
                reward.rarityColorHex = "#3498DB"; // Mavi
            }
            else
            {
                chipBonusHealth += 10f;
                reward.title = "ÇELİK GÖVDE ÇİPİ!";
                reward.description = "Tüm uçaklara kalıcı +10 Can eklendi!";
                reward.rarityColorHex = "#3498DB";
            }
        }
        else if (type == CrateType.Silver)
        {
            if (roll < 0.40f)
            {
                int goldWon = UnityEngine.Random.Range(400, 900);
                gold += goldWon;
                reward.title = "GÜMÜŞ KASA BÜYÜK ÖDÜL!";
                reward.description = $"+{goldWon} Altın kasaya aktarıldı!";
                reward.goldGain = goldWon;
                reward.rarityColorHex = "#3498DB";
            }
            else if (roll < 0.70f)
            {
                if (IsDroneUnlocked(DroneCompanion.DroneType.MK2_Interceptor))
                {
                    TryRepairDrone(DroneCompanion.DroneType.MK2_Interceptor);
                    reward.title = "İHA ONARIM KİTİ!";
                    reward.description = "MK-2 Avcı Dronu tamir edildi ve dayanıklılığı yenilendi!";
                }
                else
                {
                    chipBonusDamage += 2.5f;
                    reward.title = "GELİŞMİŞ AS ÇİPİ!";
                    reward.description = "Tüm uçaklara kalıcı +2.5 Mermi Hasarı eklendi!";
                }
                reward.rarityColorHex = "#9B59B6"; // Mor
            }
            else
            {
                chipBonusHealth += 25f;
                reward.title = "KOMPOZİT TİTANYUM ZIRH!";
                reward.description = "Tüm uçaklara kalıcı +25 Can eklendi!";
                reward.rarityColorHex = "#9B59B6";
            }
        }
        else // GoldAce Crate
        {
            if (roll < 0.35f)
            {
                int goldWon = UnityEngine.Random.Range(1200, 2500);
                gold += goldWon;
                reward.title = "EFSANEVİ BÜYÜK İKRAMİYE!";
                reward.description = $"+{goldWon} Altın doğrudan kasaya aktarıldı!";
                reward.goldGain = goldWon;
                reward.rarityColorHex = "#F1C40F"; // Altın Sarı
            }
            else if (roll < 0.70f)
            {
                if (!IsAircraftUnlocked(AircraftType.A10TankBuster))
                {
                    PlayerPrefs.SetInt(PREF_UNLOCKED_A10, 1);
                    reward.title = "A-10 TANK BUSTER AÇILDI!";
                    reward.description = "Efsanevi A-10 Tank Buster uçağının kilidi ücretsiz açıldı!";
                }
                else if (!IsAircraftUnlocked(AircraftType.F22Raptor))
                {
                    PlayerPrefs.SetInt(PREF_UNLOCKED_F22, 1);
                    reward.title = "F-22 RAPTOR AÇILDI!";
                    reward.description = "Süpersonik Hayalet F-22 uçağının kilidi ücretsiz açıldı!";
                }
                else
                {
                    chipBonusDamage += 5.0f;
                    chipBonusHealth += 35f;
                    reward.title = "EFSANEVİ AS PROTOKOLÜ!";
                    reward.description = "Tüm uçaklara kalıcı +5.0 Hasar & +35 Can eklendi!";
                }
                reward.rarityColorHex = "#E74C3C"; // Efsanevi Kırmızı
            }
            else
            {
                chipBonusDamage += 4.0f;
                chipBonusHealth += 30f;
                reward.title = "KARA ŞAFAK AS ÇİPİ!";
                reward.description = "Tüm filoya kalıcı +4.0 Hasar ve +30 Can eklendi!";
                reward.rarityColorHex = "#F1C40F";
            }
        }

        totalCratesOpened++;
        if (reward.goldGain <= 0)
        {
            reward.description += "\n<color=#00E5FF>✦ Hangar Envanterine Eklendi!</color>";
        }

        SaveHangarData();

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateGoldHUD(gold, reward.goldGain);

        return reward;
    }

    /// <summary>
    /// Seçili uçağın temel özelliklerini ve kalıcı atölye geliştirmelerini oyuncunun uçağına uygular.
    /// </summary>
    public void ApplyActiveAircraftStats(PlaneController pc)
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
        Color planeTint = Color.white;

        switch (activeAircraft)
        {
            case AircraftType.A10TankBuster:
                baseHp = 160f;
                baseSpd = 6.2f;
                baseDmg = 28f;
                baseBullets = 1;
                planeTint = new Color(0.68f, 0.82f, 0.60f); // Kamuflaj Yeşili
                break;

            case AircraftType.F22Raptor:
                baseHp = 80f;
                baseSpd = 10.5f;
                baseDmg = 22f;
                baseBullets = 2; // Çift Namlu
                planeTint = new Color(0.55f, 0.68f, 0.88f); // Hayalet Mavi-Gri
                break;

            default: // F-16 Viper
                baseHp = 100f;
                baseSpd = 8.0f;
                baseDmg = 20f;
                baseBullets = 1;
                planeTint = Color.white;
                break;
        }

        // Atölye ve Sandık kalıcı çip bonusları eklenir
        float finalHp = baseHp + (armorLevel * 15f) + chipBonusHealth;
        float finalSpd = baseSpd + (speedLevel * 0.6f);
        float finalDmg = baseDmg + (damageLevel * 3f) + chipBonusDamage;
        float finalCrit = criticalLevel * 0.06f; // Seviye başına +%6

        pc.moveSpeed = finalSpd;

        if (health != null)
        {
            health.ResetHealth(finalHp);
        }

        if (shooter != null)
        {
            shooter.bulletDamage = finalDmg;
            shooter.bulletsPerShot = baseBullets;
            shooter.criticalChance = finalCrit;
        }

        if (sr != null)
        {
            sr.color = planeTint;
        }

        // Destek Droneları'nı Kanatlara Tak
        DroneManager.EnsureInstance()?.SpawnDronesForPlayer(pc.transform);
    }
}

