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

    private const string PREF_GOLD = "AirBattle_Gold";
    private const string PREF_ACTIVE_PLANE = "AirBattle_ActivePlane";
    private const string PREF_UNLOCKED_A10 = "AirBattle_Unlocked_A10";
    private const string PREF_UNLOCKED_F22 = "AirBattle_Unlocked_F22";

    private const string PREF_LVL_ARMOR = "AirBattle_Lvl_Armor";
    private const string PREF_LVL_DAMAGE = "AirBattle_Lvl_Damage";
    private const string PREF_LVL_SPEED = "AirBattle_Lvl_Speed";
    private const string PREF_LVL_CRIT = "AirBattle_Lvl_Crit";

    [Header("Mevcut Durum")]
    public AircraftType activeAircraft = AircraftType.F16Viper;
    public int gold = 150; // Başlangıç hoş geldin hediyesi

    [Header("Kalıcı Atölye Seviyeleri (Maks 5)")]
    public int armorLevel = 0;
    public int damageLevel = 0;
    public int speedLevel = 0;
    public int criticalLevel = 0;

    public const int MAX_UPGRADE_LEVEL = 5;

    public static readonly int[] ArmorCosts = { 100, 200, 350, 500, 750 };
    public static readonly int[] DamageCosts = { 120, 250, 400, 600, 850 };
    public static readonly int[] SpeedCosts = { 90, 180, 300, 450, 650 };
    public static readonly int[] CritCosts = { 150, 300, 500, 750, 1000 };

    public const int A10_UNLOCK_COST = 400;
    public const int F22_UNLOCK_COST = 900;

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
        gold = PlayerPrefs.GetInt(PREF_GOLD, 150);
        activeAircraft = (AircraftType)PlayerPrefs.GetInt(PREF_ACTIVE_PLANE, (int)AircraftType.F16Viper);

        armorLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LVL_ARMOR, 0), 0, MAX_UPGRADE_LEVEL);
        damageLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LVL_DAMAGE, 0), 0, MAX_UPGRADE_LEVEL);
        speedLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LVL_SPEED, 0), 0, MAX_UPGRADE_LEVEL);
        criticalLevel = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LVL_CRIT, 0), 0, MAX_UPGRADE_LEVEL);
    }

    public void SaveHangarData()
    {
        PlayerPrefs.SetInt(PREF_GOLD, gold);
        PlayerPrefs.SetInt(PREF_ACTIVE_PLANE, (int)activeAircraft);

        PlayerPrefs.SetInt(PREF_LVL_ARMOR, armorLevel);
        PlayerPrefs.SetInt(PREF_LVL_DAMAGE, damageLevel);
        PlayerPrefs.SetInt(PREF_LVL_SPEED, speedLevel);
        PlayerPrefs.SetInt(PREF_LVL_CRIT, criticalLevel);

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

        // Atölye bonusları eklenir
        float finalHp = baseHp + (armorLevel * 15f);
        float finalSpd = baseSpd + (speedLevel * 0.6f);
        float finalDmg = baseDmg + (damageLevel * 3f);
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
    }
}

