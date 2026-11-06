using System;
using UnityEngine;

/// <summary>
/// Oyuncu Askeri Pilot Profili, Rütbe ve Kademe Seviye / EXP Yöneticisi:
/// - Düşman uçağı vurdukça, dalgaları geçtikçe ve boss yendikçe kademe kademe EXP birikir.
/// - EXP barı dolduğunda seviye atlanır, rütbe yükselir ve altın/İHA güçlendirme ödülleri verilir.
/// </summary>
public class PlayerProfileManager : MonoBehaviour
{
    public static PlayerProfileManager Instance { get; private set; }

    private const string PREF_PILOT_LEVEL = "AirBattle_Pilot_Level";
    private const string PREF_PILOT_EXP = "AirBattle_Pilot_Exp";
    private const string PREF_TOTAL_KILLS = "AirBattle_Total_Kills";
    private const string PREF_TOTAL_BOSSES = "AirBattle_Total_Bosses";
    private const string PREF_TOTAL_WAVES = "AirBattle_Total_Waves";

    [Header("Profil Bilgileri")]
    public int level = 1;
    public int currentExp = 0;
    public int totalKills = 0;
    public int totalBosses = 0;
    public int totalWavesCleared = 0;

    public event Action<int, int> OnLevelUp;       // (yeniSeviye, kazanılanAltın)
    public event Action<int, string> OnExpAdded;   // (kazanılanMiktar, sebep)

    public int ExpForNextLevel => GetExpRequiredForLevel(level);
    public float ExpProgress => Mathf.Clamp01((float)currentExp / Mathf.Max(1, ExpForNextLevel));
    public string RankTitle => GetRankTitle(level);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadProfile();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static PlayerProfileManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("PlayerProfileManager");
        if (go == null) go = new GameObject("PlayerProfileManager");
        Instance = go.GetComponent<PlayerProfileManager>();
        if (Instance == null) Instance = go.AddComponent<PlayerProfileManager>();
        Instance.LoadProfile();
        return Instance;
    }

    public void LoadProfile()
    {
        level = Mathf.Max(1, PlayerPrefs.GetInt(PREF_PILOT_LEVEL, 1));
        currentExp = Mathf.Max(0, PlayerPrefs.GetInt(PREF_PILOT_EXP, 0));
        totalKills = PlayerPrefs.GetInt(PREF_TOTAL_KILLS, 0);
        totalBosses = PlayerPrefs.GetInt(PREF_TOTAL_BOSSES, 0);
        totalWavesCleared = PlayerPrefs.GetInt(PREF_TOTAL_WAVES, 0);
    }

    public void SaveProfile()
    {
        PlayerPrefs.SetInt(PREF_PILOT_LEVEL, level);
        PlayerPrefs.SetInt(PREF_PILOT_EXP, currentExp);
        PlayerPrefs.SetInt(PREF_TOTAL_KILLS, totalKills);
        PlayerPrefs.SetInt(PREF_TOTAL_BOSSES, totalBosses);
        PlayerPrefs.SetInt(PREF_TOTAL_WAVES, totalWavesCleared);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Seviye başına gereken EXP formülü:
    /// Seviye 1: 100 EXP, Seviye 2: 220 EXP, Seviye 3: 360 EXP ...
    /// </summary>
    public static int GetExpRequiredForLevel(int lvl)
    {
        int l = Mathf.Max(1, lvl);
        return (int)(150 + (l - 1) * 120 + Mathf.Pow(l - 1, 1.6f) * 35f);
    }

    /// <summary>
    /// Askeri Rütbe İsimlendirmesi
    /// </summary>
    public static string GetRankTitle(int lvl)
    {
        if (lvl <= 5) return "Çaylak As Pilot";
        if (lvl <= 12) return "Hava Kıdemli Teğmen";
        if (lvl <= 19) return "Hava Yüzbaşısı";
        if (lvl <= 29) return "Filo As Binbaşısı";
        if (lvl <= 49) return "Hava Kurmay Yarbay";
        if (lvl <= 74) return "Taktik Hava Albayı";
        if (lvl <= 99) return "Tuğgeneral Gök Ası";
        return "Orgeneral Efsanevi Mareşal";
    }

    /// <summary>
    /// Seviyeye göre profil askeri bröve/rozet rengi
    /// </summary>
    public static Color GetRankColor(int lvl)
    {
        if (lvl <= 5) return new Color(0.7f, 0.85f, 1f);       // Gümüş Mavi
        if (lvl <= 12) return new Color(0.2f, 0.85f, 0.45f);    // Askeri Zümrüt
        if (lvl <= 19) return new Color(0f, 0.85f, 1f);         // Siber Mavi
        if (lvl <= 29) return new Color(0.9f, 0.5f, 1f);       // Mor As (Seviye 20+ MK-1)
        if (lvl <= 49) return new Color(1f, 0.55f, 0.15f);     // Ateş Turuncusu (Seviye 30+ MK-2)
        if (lvl <= 74) return new Color(1f, 0.85f, 0.2f);      // Altın Sarısı (Seviye 50+ MK-3)
        if (lvl <= 99) return new Color(1f, 0.25f, 0.35f);     // Yakut Kırmızı (Seviye 75+ MK-4)
        return new Color(0.2f, 1f, 0.9f);                      // Efsanevi Plazma (Seviye 100+ MK-5)
    }

    /// <summary>
    /// EXP ekler, seviye atlama kontrollerini yapar ve seviye atlandığında ödül verir.
    /// </summary>
    public void AddExp(int amount, string reason = "")
    {
        if (amount <= 0) return;

        currentExp += amount;
        OnExpAdded?.Invoke(amount, reason);

        bool leveledUp = false;
        int totalReward = 0;

        while (currentExp >= ExpForNextLevel)
        {
            currentExp -= ExpForNextLevel;
            level++;
            leveledUp = true;

            int levelReward = Mathf.Max(50, level * 30);
            totalReward += levelReward;
            HangarManager.EnsureInstance()?.AddGold(levelReward);
        }

        SaveProfile();

        if (leveledUp)
        {
            AudioManager.Instance?.PlayRewardFanfare();
            OnLevelUp?.Invoke(level, totalReward);
        }
    }

    /// <summary>
    /// Düşman uçağı vurulduğunda çağrılır
    /// </summary>
    public void RecordKill(bool isBoss, bool isElite = false)
    {
        totalKills++;
        if (isBoss) totalBosses++;

        int expGain = isBoss ? 80 : (isElite ? 25 : 10);
        string tag = isBoss ? "BOSS İMHA EDİLDİ" : (isElite ? "ELİT DÜŞMAN" : "DÜŞMAN İMHA");
        AddExp(expGain, tag);
    }

    /// <summary>
    /// Dalga / Bölüm geçildiğinde çağrılır
    /// </summary>
    public void RecordWaveCleared(int wave)
    {
        totalWavesCleared++;
        int expGain = 20 + (wave * 2);
        AddExp(expGain, $"DALGA {wave} GEÇİLDİ");
    }

    /// <summary>
    /// Seviyeye bağlı İHA/Destek Dronu hasar bonusu çarpanı
    /// </summary>
    public float GetDroneDamageMultiplier()
    {
        float bonus = 1.0f;
        if (level >= 1)  bonus += 0.05f; // +%5  (MK-1 İzci Açılışı - Seviye 1+)
        if (level >= 20) bonus += 0.10f; // +%10 (MK-2 Avcı Açılışı - Seviye 20+)
        if (level >= 30) bonus += 0.15f; // +%15 (MK-3 Taarruz Açılışı - Seviye 30+)
        if (level >= 50) bonus += 0.25f; // +%25 (MK-4 Valkyrie Açılışı - Seviye 50+)
        if (level >= 75) bonus += 0.40f; // +%40 (MK-5 Titan Aegis Açılışı - Seviye 75+)
        return bonus;
    }

    /// <summary>
    /// Seviyeye bağlı İHA atış hızı çarpanı
    /// </summary>
    public float GetDroneFireRateMultiplier()
    {
        float speed = 1.0f;
        if (level >= 20) speed += 0.05f;
        if (level >= 50) speed += 0.10f;
        if (level >= 100) speed += 0.15f;
        return speed;
    }
}

