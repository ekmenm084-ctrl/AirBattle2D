using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Günlük Operasyon Görevleri ve Kalıcı Pilot Madalya sistemini yönetir.
/// Oyuncu ilerlemesini PlayerPrefs üzerinde kalıcı olarak saklar ve Altın, Anahtar ve EXP ödülleri verir.
/// </summary>
public class DailyOpsManager : MonoBehaviour
{
    public static DailyOpsManager Instance { get; private set; }

    public enum OpsEventType
    {
        EnemyPlaneKill,
        SwarmDroneKill,
        BossKill,
        PerfectDodge,
        OverdriveActivated,
        LootCollected,
        WaveCleared
    }

    public struct MissionTemplate
    {
        public string id;
        public string tag;
        public string title;
        public string description;
        public OpsEventType eventType;
        public int targetCount;
        public int goldReward;
        public int expReward;
        public bool hasKeyReward;
        public HangarManager.KeyType keyType;
        public int keyCount;
    }

    public class DailyMissionState
    {
        public int slotIndex;
        public MissionTemplate template;
        public int progress;
        public bool claimed;

        public bool IsCompleted => progress >= template.targetCount;
        public bool CanClaim => IsCompleted && !claimed;
        public int ClampedProgress => Mathf.Min(progress, template.targetCount);
        public float ProgressRatio => template.targetCount > 0
            ? Mathf.Clamp01((float)progress / template.targetCount)
            : 1f;
    }

    public struct MedalTierReward
    {
        public int target;
        public int goldReward;
        public int expReward;
        public bool hasKeyReward;
        public HangarManager.KeyType keyType;
        public int keyCount;
    }

    public class PilotMedalDef
    {
        public string id;
        public string code;
        public string title;
        public string description;
        public string statKey;
        public MedalTierReward[] tiers;
    }

    public class PilotMedalState
    {
        public PilotMedalDef def;
        public int currentStat;
        public int claimedTier; // 0 = Kademe I bekliyor, 3 = Tümü alındı

        public bool IsMaxed => claimedTier >= def.tiers.Length;
        public int ActiveTierIndex => Mathf.Clamp(claimedTier, 0, def.tiers.Length - 1);
        public MedalTierReward ActiveTier => def.tiers[ActiveTierIndex];
        public bool CanClaim => !IsMaxed && currentStat >= ActiveTier.target;
        public int ClampedProgress => IsMaxed ? ActiveTier.target : Mathf.Min(currentStat, ActiveTier.target);
        public float ProgressRatio => ActiveTier.target > 0
            ? Mathf.Clamp01((float)ClampedProgress / ActiveTier.target)
            : 1f;
    }

    public event Action OnDailyOpsChanged;

    public const int DAILY_SLOT_COUNT = 4;

    private const string PREF_ACTIVE_DATE = "DailyOps_ActiveDate";
    private const string PREF_SLOT_ID = "DailyOps_SlotId_";
    private const string PREF_SLOT_PROG = "DailyOps_SlotProg_";
    private const string PREF_SLOT_CLAIMED = "DailyOps_SlotClaimed_";
    private const string PREF_MEDAL_TIER = "DailyOps_MedalTier_";
    private const string PREF_MEDAL_NOTIFIED = "DailyOps_MedalNotified_";

    private const string PREF_STAT_SWARM = "DailyOps_Stat_SwarmKills";
    private const string PREF_STAT_DODGE = "DailyOps_Stat_PerfectDodges";
    private const string PREF_STAT_OVERDRIVE = "DailyOps_Stat_OverdrivesUsed";
    private const string PREF_STAT_LOOT = "DailyOps_Stat_LootsCollected";
    private const string PREF_STAT_DAILY_DONE = "DailyOps_Stat_DailyCompleted";

    private static readonly string[] TierRoman = { "I", "II", "III" };

    private static readonly MissionTemplate[] MissionPool =
    {
        new MissionTemplate
        {
            id = "m_kills_12",
            tag = "HAVA DEVRİYESİ",
            title = "GÖKYÜZÜ DEVRİYESİ",
            description = "12 düşman savaş uçağı imha et",
            eventType = OpsEventType.EnemyPlaneKill,
            targetCount = 12,
            goldReward = 180,
            expReward = 90,
            hasKeyReward = false
        },
        new MissionTemplate
        {
            id = "m_kills_25",
            tag = "TAARRUZ HATTI",
            title = "HAVA SAHASI TEMİZLİĞİ",
            description = "25 düşman savaş uçağı imha et",
            eventType = OpsEventType.EnemyPlaneKill,
            targetCount = 25,
            goldReward = 340,
            expReward = 160,
            hasKeyReward = true,
            keyType = HangarManager.KeyType.Bronze,
            keyCount = 1
        },
        new MissionTemplate
        {
            id = "m_swarm_15",
            tag = "DRONE SAVUNMASI",
            title = "SÜRÜ AVCISI",
            description = "15 düşman sürü dronu imha et",
            eventType = OpsEventType.SwarmDroneKill,
            targetCount = 15,
            goldReward = 180,
            expReward = 90,
            hasKeyReward = false
        },
        new MissionTemplate
        {
            id = "m_swarm_30",
            tag = "DRONE SÜPÜRME",
            title = "İHA İMHA TİMİ",
            description = "30 düşman sürü dronu imha et",
            eventType = OpsEventType.SwarmDroneKill,
            targetCount = 30,
            goldReward = 350,
            expReward = 165,
            hasKeyReward = true,
            keyType = HangarManager.KeyType.Bronze,
            keyCount = 1
        },
        new MissionTemplate
        {
            id = "m_boss_1",
            tag = "ÖNCELİKLİ HEDEF",
            title = "KOMUTA GEMİSİ AVI",
            description = "1 Boss savaş gemisi imha et",
            eventType = OpsEventType.BossKill,
            targetCount = 1,
            goldReward = 300,
            expReward = 150,
            hasKeyReward = true,
            keyType = HangarManager.KeyType.Bronze,
            keyCount = 1
        },
        new MissionTemplate
        {
            id = "m_boss_2",
            tag = "KRİTİK İMHA",
            title = "ÇİFTE BOSS DARBESİ",
            description = "2 Boss savaş gemisi imha et",
            eventType = OpsEventType.BossKill,
            targetCount = 2,
            goldReward = 520,
            expReward = 260,
            hasKeyReward = true,
            keyType = HangarManager.KeyType.Silver,
            keyCount = 1
        },
        new MissionTemplate
        {
            id = "m_dodge_3",
            tag = "KAÇINMA MANEVRASI",
            title = "ÇEVİK PİLOT",
            description = "3 kez Mükemmel Sıyrılma gerçekleştir",
            eventType = OpsEventType.PerfectDodge,
            targetCount = 3,
            goldReward = 200,
            expReward = 100,
            hasKeyReward = false
        },
        new MissionTemplate
        {
            id = "m_dodge_6",
            tag = "HAYALET PİLOT",
            title = "HAYALET REFLEKS",
            description = "6 kez Mükemmel Sıyrılma gerçekleştir",
            eventType = OpsEventType.PerfectDodge,
            targetCount = 6,
            goldReward = 360,
            expReward = 170,
            hasKeyReward = true,
            keyType = HangarManager.KeyType.Silver,
            keyCount = 1
        },
        new MissionTemplate
        {
            id = "m_overdrive_3",
            tag = "SİLAH PROTOKOLÜ",
            title = "AŞIRI YÜK ATEŞİ",
            description = "Overdrive gücünü 3 kez etkinleştir",
            eventType = OpsEventType.OverdriveActivated,
            targetCount = 3,
            goldReward = 210,
            expReward = 105,
            hasKeyReward = false
        },
        new MissionTemplate
        {
            id = "m_loot_8",
            tag = "İKMAL HATTI",
            title = "SAHA İKMALİ",
            description = "Savaş alanından 8 ganimet kutusu topla",
            eventType = OpsEventType.LootCollected,
            targetCount = 8,
            goldReward = 200,
            expReward = 100,
            hasKeyReward = false
        },
        new MissionTemplate
        {
            id = "m_wave_5",
            tag = "SEKTÖR HATTI",
            title = "CEPHE YARMA",
            description = "5 saldırı dalgasını başarıyla geç",
            eventType = OpsEventType.WaveCleared,
            targetCount = 5,
            goldReward = 260,
            expReward = 130,
            hasKeyReward = true,
            keyType = HangarManager.KeyType.Bronze,
            keyCount = 1
        },
        new MissionTemplate
        {
            id = "m_wave_10",
            tag = "UZUN MENZİL",
            title = "KESİNTİSİZ HAREKAT",
            description = "10 saldırı dalgasını başarıyla geç",
            eventType = OpsEventType.WaveCleared,
            targetCount = 10,
            goldReward = 450,
            expReward = 220,
            hasKeyReward = true,
            keyType = HangarManager.KeyType.Silver,
            keyCount = 1
        }
    };

    private static readonly PilotMedalDef[] MedalDefs =
    {
        new PilotMedalDef
        {
            id = "medal_ace",
            code = "NİŞAN-01",
            title = "HAVA HAKİMİYETİ",
            description = "Toplam düşman savaş uçağı imhası",
            statKey = "totalKills",
            tiers = new[]
            {
                new MedalTierReward { target = 25,  goldReward = 250,  expReward = 120, hasKeyReward = true, keyType = HangarManager.KeyType.Bronze, keyCount = 1 },
                new MedalTierReward { target = 100, goldReward = 650,  expReward = 320, hasKeyReward = true, keyType = HangarManager.KeyType.Silver, keyCount = 1 },
                new MedalTierReward { target = 350, goldReward = 1600, expReward = 750, hasKeyReward = true, keyType = HangarManager.KeyType.Gold,   keyCount = 1 }
            }
        },
        new PilotMedalDef
        {
            id = "medal_boss",
            code = "NİŞAN-02",
            title = "DEV AVCISI",
            description = "Toplam Boss savaş gemisi imhası",
            statKey = "totalBosses",
            tiers = new[]
            {
                new MedalTierReward { target = 2,  goldReward = 300,  expReward = 150, hasKeyReward = true, keyType = HangarManager.KeyType.Bronze, keyCount = 1 },
                new MedalTierReward { target = 8,  goldReward = 800,  expReward = 380, hasKeyReward = true, keyType = HangarManager.KeyType.Silver, keyCount = 1 },
                new MedalTierReward { target = 25, goldReward = 2000, expReward = 900, hasKeyReward = true, keyType = HangarManager.KeyType.Gold,   keyCount = 2 }
            }
        },
        new PilotMedalDef
        {
            id = "medal_wave",
            code = "NİŞAN-03",
            title = "SEKTÖR FATİHİ",
            description = "Toplam temizlenen saldırı dalgası",
            statKey = "totalWavesCleared",
            tiers = new[]
            {
                new MedalTierReward { target = 10,  goldReward = 240,  expReward = 120, hasKeyReward = false },
                new MedalTierReward { target = 35,  goldReward = 600,  expReward = 300, hasKeyReward = true, keyType = HangarManager.KeyType.Silver, keyCount = 1 },
                new MedalTierReward { target = 100, goldReward = 1500, expReward = 700, hasKeyReward = true, keyType = HangarManager.KeyType.Gold,   keyCount = 1 }
            }
        },
        new PilotMedalDef
        {
            id = "medal_rank",
            code = "NİŞAN-04",
            title = "KIDEMLİ PİLOT",
            description = "Ulaşılan en yüksek pilot seviyesi",
            statKey = "level",
            tiers = new[]
            {
                new MedalTierReward { target = 3,  goldReward = 280,  expReward = 100, hasKeyReward = true, keyType = HangarManager.KeyType.Bronze, keyCount = 1 },
                new MedalTierReward { target = 7,  goldReward = 750,  expReward = 260, hasKeyReward = true, keyType = HangarManager.KeyType.Silver, keyCount = 1 },
                new MedalTierReward { target = 15, goldReward = 1800, expReward = 600, hasKeyReward = true, keyType = HangarManager.KeyType.Gold,   keyCount = 1 }
            }
        },
        new PilotMedalDef
        {
            id = "medal_swarm",
            code = "NİŞAN-05",
            title = "DRONE SÜPÜRÜCÜ",
            description = "Toplam imha edilen sürü dronu sayısı",
            statKey = "swarmKills",
            tiers = new[]
            {
                new MedalTierReward { target = 20,  goldReward = 220,  expReward = 110, hasKeyReward = false },
                new MedalTierReward { target = 80,  goldReward = 580,  expReward = 290, hasKeyReward = true, keyType = HangarManager.KeyType.Silver, keyCount = 1 },
                new MedalTierReward { target = 250, goldReward = 1400, expReward = 650, hasKeyReward = true, keyType = HangarManager.KeyType.Gold,   keyCount = 1 }
            }
        },
        new PilotMedalDef
        {
            id = "medal_dodge",
            code = "NİŞAN-06",
            title = "HAYALET MANEVRA",
            description = "Toplam Mükemmel Sıyrılma sayısı",
            statKey = "perfectDodges",
            tiers = new[]
            {
                new MedalTierReward { target = 5,  goldReward = 260,  expReward = 130, hasKeyReward = true, keyType = HangarManager.KeyType.Bronze, keyCount = 1 },
                new MedalTierReward { target = 20, goldReward = 680,  expReward = 340, hasKeyReward = true, keyType = HangarManager.KeyType.Silver, keyCount = 1 },
                new MedalTierReward { target = 60, goldReward = 1650, expReward = 780, hasKeyReward = true, keyType = HangarManager.KeyType.Gold,   keyCount = 1 }
            }
        },
        new PilotMedalDef
        {
            id = "medal_overdrive",
            code = "NİŞAN-07",
            title = "AŞIRI YÜK PİLOTU",
            description = "Toplam Overdrive aktivasyon sayısı",
            statKey = "overdrivesUsed",
            tiers = new[]
            {
                new MedalTierReward { target = 5,  goldReward = 240,  expReward = 120, hasKeyReward = false },
                new MedalTierReward { target = 20, goldReward = 620,  expReward = 300, hasKeyReward = true, keyType = HangarManager.KeyType.Silver, keyCount = 1 },
                new MedalTierReward { target = 50, goldReward = 1450, expReward = 680, hasKeyReward = true, keyType = HangarManager.KeyType.Gold,   keyCount = 1 }
            }
        },
        new PilotMedalDef
        {
            id = "medal_loot",
            code = "NİŞAN-08",
            title = "LOJİSTİK TOPLAYICI",
            description = "Toplam toplanan savaş ganimeti",
            statKey = "lootsCollected",
            tiers = new[]
            {
                new MedalTierReward { target = 15,  goldReward = 220,  expReward = 110, hasKeyReward = false },
                new MedalTierReward { target = 50,  goldReward = 550,  expReward = 270, hasKeyReward = true, keyType = HangarManager.KeyType.Silver, keyCount = 1 },
                new MedalTierReward { target = 150, goldReward = 1350, expReward = 620, hasKeyReward = true, keyType = HangarManager.KeyType.Gold,   keyCount = 1 }
            }
        },
        new PilotMedalDef
        {
            id = "medal_ops",
            code = "NİŞAN-09",
            title = "OPERASYON UZMANI",
            description = "Tamamlanan toplam günlük operasyon",
            statKey = "dailyCompletedTotal",
            tiers = new[]
            {
                new MedalTierReward { target = 4,  goldReward = 320,  expReward = 160, hasKeyReward = true, keyType = HangarManager.KeyType.Bronze, keyCount = 1 },
                new MedalTierReward { target = 16, goldReward = 850,  expReward = 420, hasKeyReward = true, keyType = HangarManager.KeyType.Silver, keyCount = 1 },
                new MedalTierReward { target = 40, goldReward = 2000, expReward = 950, hasKeyReward = true, keyType = HangarManager.KeyType.Gold,   keyCount = 2 }
            }
        }
    };

    private string _activeDateKey = "";
    private readonly DailyMissionState[] _dailySlots = new DailyMissionState[DAILY_SLOT_COUNT];
    private float _rolloverCheckTimer;

    public static DailyOpsManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        DailyOpsManager existing = FindAnyObjectByType<DailyOpsManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("DailyOpsManager");
        return go.AddComponent<DailyOpsManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadOrRefreshDailyRotation();
    }

    private void Update()
    {
        _rolloverCheckTimer += Time.unscaledDeltaTime;
        if (_rolloverCheckTimer >= 10f)
        {
            _rolloverCheckTimer = 0f;
            CheckDailyRollover();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

    public string GetActiveDateKey() => _activeDateKey;

    public bool CheckDailyRollover()
    {
        string today = GetTodayKey();
        if (_activeDateKey != today)
        {
            LoadOrRefreshDailyRotation();
            OnDailyOpsChanged?.Invoke();
            return true;
        }
        return false;
    }

    public IReadOnlyList<DailyMissionState> GetDailyMissions()
    {
        CheckDailyRollover();
        return _dailySlots;
    }

    public List<PilotMedalState> GetMedalStates()
    {
        var result = new List<PilotMedalState>(MedalDefs.Length);
        for (int i = 0; i < MedalDefs.Length; i++)
        {
            PilotMedalDef def = MedalDefs[i];
            int claimedTier = Mathf.Clamp(PlayerPrefs.GetInt(PREF_MEDAL_TIER + def.id, 0), 0, def.tiers.Length);
            result.Add(new PilotMedalState
            {
                def = def,
                currentStat = GetStatValue(def.statKey),
                claimedTier = claimedTier
            });
        }
        return result;
    }

    public int GetClaimableDailyCount()
    {
        CheckDailyRollover();
        int count = 0;
        for (int i = 0; i < _dailySlots.Length; i++)
        {
            if (_dailySlots[i] != null && _dailySlots[i].CanClaim)
                count++;
        }
        return count;
    }

    public int GetClaimableMedalCount()
    {
        int count = 0;
        for (int i = 0; i < MedalDefs.Length; i++)
        {
            PilotMedalDef def = MedalDefs[i];
            int claimedTier = Mathf.Clamp(PlayerPrefs.GetInt(PREF_MEDAL_TIER + def.id, 0), 0, def.tiers.Length);
            if (claimedTier >= def.tiers.Length) continue;
            if (GetStatValue(def.statKey) >= def.tiers[claimedTier].target)
                count++;
        }
        return count;
    }

    public int GetTotalClaimableCount()
    {
        return GetClaimableDailyCount() + GetClaimableMedalCount();
    }

    public string GetTimeUntilResetFormatted()
    {
        DateTime now = DateTime.Now;
        DateTime nextMidnight = now.Date.AddDays(1);
        TimeSpan remaining = nextMidnight - now;
        if (remaining.TotalSeconds < 0) remaining = TimeSpan.Zero;
        return $"{(int)remaining.TotalHours:00}sa {remaining.Minutes:00}dk";
    }

    public static string GetTierRoman(int zeroBasedTierIndex)
    {
        int idx = Mathf.Clamp(zeroBasedTierIndex, 0, TierRoman.Length - 1);
        return TierRoman[idx];
    }

    public static string GetKeyTurkishName(HangarManager.KeyType keyType)
    {
        switch (keyType)
        {
            case HangarManager.KeyType.Bronze: return "Bronz Anahtar";
            case HangarManager.KeyType.Silver: return "Gümüş Anahtar";
            case HangarManager.KeyType.Gold: return "Altın Anahtar";
            default: return "Anahtar";
        }
    }

    public void RecordEvent(OpsEventType eventType, int amount = 1)
    {
        if (amount <= 0) return;

        // 2 Oyuncu PvP modunda görev ve madalya sayacı ilerletilmez
        if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer)
            return;

        CheckDailyRollover();

        bool changed = false;

        // 1. Özel madalya sayaçlarını güncelle
        string prefKey = GetCounterPrefKey(eventType);
        if (!string.IsNullOrEmpty(prefKey))
        {
            int curVal = PlayerPrefs.GetInt(prefKey, 0) + amount;
            PlayerPrefs.SetInt(prefKey, curVal);
            changed = true;
        }

        // 2. Aktif günlük görevleri ilerlet
        for (int i = 0; i < _dailySlots.Length; i++)
        {
            DailyMissionState slot = _dailySlots[i];
            if (slot == null || slot.template.eventType != eventType) continue;
            if (slot.progress >= slot.template.targetCount) continue;

            bool wasComplete = slot.IsCompleted;
            slot.progress = Mathf.Min(slot.template.targetCount, slot.progress + amount);
            PlayerPrefs.SetInt(PREF_SLOT_PROG + i, slot.progress);
            changed = true;

            if (!wasComplete && slot.IsCompleted)
            {
                IncrementLifetimeStat(PREF_STAT_DAILY_DONE, 1);
                TriggerInFlightAlert($"[GÖREV TAMAM] {slot.template.title} - ÖDÜL HAZIR!", new Color(0.18f, 0.96f, 0.72f));
            }
        }

        // 3. Madalyalarda yeni açılan kademe varsa oyun içi bildirim ver
        CheckNewlyUnlockedMedalAlerts();

        if (changed)
        {
            if (eventType == OpsEventType.BossKill || eventType == OpsEventType.WaveCleared)
                PlayerPrefs.Save();

            OnDailyOpsChanged?.Invoke();
        }
    }

    public bool ClaimDailyMission(int slotIndex)
    {
        CheckDailyRollover();
        if (slotIndex < 0 || slotIndex >= _dailySlots.Length) return false;

        DailyMissionState slot = _dailySlots[slotIndex];
        if (slot == null || !slot.CanClaim) return false;

        slot.claimed = true;
        PlayerPrefs.SetInt(PREF_SLOT_CLAIMED + slotIndex, 1);

        GrantRewards(
            slot.template.goldReward,
            slot.template.expReward,
            slot.template.hasKeyReward,
            slot.template.keyType,
            slot.template.keyCount,
            $"GÖREV: {slot.template.title}"
        );

        AudioManager.Instance?.PlayRewardFanfare();
        PlayerPrefs.Save();
        OnDailyOpsChanged?.Invoke();
        return true;
    }

    public int ClaimAllReadyRewards()
    {
        CheckDailyRollover();

        int claimedCount = 0;
        int totalGold = 0;
        int totalExp = 0;
        int bronzeKeys = 0;
        int silverKeys = 0;
        int goldKeys = 0;

        for (int i = 0; i < _dailySlots.Length; i++)
        {
            DailyMissionState slot = _dailySlots[i];
            if (slot == null || !slot.CanClaim) continue;

            slot.claimed = true;
            PlayerPrefs.SetInt(PREF_SLOT_CLAIMED + i, 1);

            totalGold += slot.template.goldReward;
            totalExp += slot.template.expReward;
            if (slot.template.hasKeyReward)
            {
                if (slot.template.keyType == HangarManager.KeyType.Bronze) bronzeKeys += slot.template.keyCount;
                else if (slot.template.keyType == HangarManager.KeyType.Silver) silverKeys += slot.template.keyCount;
                else if (slot.template.keyType == HangarManager.KeyType.Gold) goldKeys += slot.template.keyCount;
            }
            claimedCount++;
        }

        for (int i = 0; i < MedalDefs.Length; i++)
        {
            PilotMedalDef def = MedalDefs[i];
            int claimedTier = Mathf.Clamp(PlayerPrefs.GetInt(PREF_MEDAL_TIER + def.id, 0), 0, def.tiers.Length);
            int stat = GetStatValue(def.statKey);

            while (claimedTier < def.tiers.Length && stat >= def.tiers[claimedTier].target)
            {
                MedalTierReward reward = def.tiers[claimedTier];
                totalGold += reward.goldReward;
                totalExp += reward.expReward;
                if (reward.hasKeyReward)
                {
                    if (reward.keyType == HangarManager.KeyType.Bronze) bronzeKeys += reward.keyCount;
                    else if (reward.keyType == HangarManager.KeyType.Silver) silverKeys += reward.keyCount;
                    else if (reward.keyType == HangarManager.KeyType.Gold) goldKeys += reward.keyCount;
                }
                claimedTier++;
                claimedCount++;
            }

            PlayerPrefs.SetInt(PREF_MEDAL_TIER + def.id, claimedTier);
        }

        if (claimedCount <= 0) return 0;

        HangarManager hangar = HangarManager.EnsureInstance();
        if (hangar != null)
        {
            if (totalGold > 0) hangar.AddGold(totalGold);
            if (bronzeKeys > 0) hangar.AddKey(HangarManager.KeyType.Bronze, bronzeKeys);
            if (silverKeys > 0) hangar.AddKey(HangarManager.KeyType.Silver, silverKeys);
            if (goldKeys > 0) hangar.AddKey(HangarManager.KeyType.Gold, goldKeys);
        }

        if (totalExp > 0)
            PlayerProfileManager.EnsureInstance()?.AddExp(totalExp, "OPERASYON ÖDÜLLERİ");

        PlayerProfileUI.Instance?.RefreshBadgeVisuals();
        AudioManager.Instance?.PlayRewardFanfare();
        PlayerPrefs.Save();
        OnDailyOpsChanged?.Invoke();
        return claimedCount;
    }

    public bool ClaimMedal(string medalId)
    {
        PilotMedalDef def = FindMedalDef(medalId);
        if (def == null) return false;

        int claimedTier = Mathf.Clamp(PlayerPrefs.GetInt(PREF_MEDAL_TIER + def.id, 0), 0, def.tiers.Length);
        if (claimedTier >= def.tiers.Length) return false;

        int currentStat = GetStatValue(def.statKey);
        MedalTierReward reward = def.tiers[claimedTier];
        if (currentStat < reward.target) return false;

        int nextTier = claimedTier + 1;
        PlayerPrefs.SetInt(PREF_MEDAL_TIER + def.id, nextTier);

        GrantRewards(
            reward.goldReward,
            reward.expReward,
            reward.hasKeyReward,
            reward.keyType,
            reward.keyCount,
            $"MADALYA: {def.title} ({GetTierRoman(claimedTier)})"
        );

        AudioManager.Instance?.PlayRewardFanfare();
        PlayerPrefs.Save();
        OnDailyOpsChanged?.Invoke();
        return true;
    }

    private void GrantRewards(int gold, int exp, bool hasKey, HangarManager.KeyType keyType, int keyCount, string reason)
    {
        HangarManager hangar = HangarManager.EnsureInstance();
        if (hangar != null)
        {
            if (gold > 0) hangar.AddGold(gold);
            if (hasKey && keyCount > 0) hangar.AddKey(keyType, keyCount);
        }

        if (exp > 0)
            PlayerProfileManager.EnsureInstance()?.AddExp(exp, reason);

        PlayerProfileUI.Instance?.RefreshBadgeVisuals();
    }

    private void CheckNewlyUnlockedMedalAlerts()
    {
        for (int i = 0; i < MedalDefs.Length; i++)
        {
            PilotMedalDef def = MedalDefs[i];
            int claimedTier = Mathf.Clamp(PlayerPrefs.GetInt(PREF_MEDAL_TIER + def.id, 0), 0, def.tiers.Length);
            if (claimedTier >= def.tiers.Length) continue;

            int stat = GetStatValue(def.statKey);
            int highestReachedTier = claimedTier;
            for (int t = claimedTier; t < def.tiers.Length; t++)
            {
                if (stat >= def.tiers[t].target)
                    highestReachedTier = t + 1;
                else
                    break;
            }

            if (highestReachedTier > claimedTier)
            {
                string notifiedKey = PREF_MEDAL_NOTIFIED + def.id;
                int lastNotified = PlayerPrefs.GetInt(notifiedKey, 0);
                if (highestReachedTier > lastNotified)
                {
                    PlayerPrefs.SetInt(notifiedKey, highestReachedTier);
                    string tierRoman = GetTierRoman(highestReachedTier - 1);
                    TriggerInFlightAlert(
                        $"[MADALYA KAZANILDI] {def.title} ({tierRoman})!",
                        new Color(1f, 0.84f, 0.25f)
                    );
                }
            }
        }
    }

    private void TriggerInFlightAlert(string message, Color color)
    {
        GameUI gameUI = FindAnyObjectByType<GameUI>();
        if (gameUI != null && gameUI.gameObject.activeInHierarchy)
            gameUI.ShowWaveAlert(message, color, 2.2f);

        AudioManager.Instance?.PlayRewardFanfare();
    }

    private void LoadOrRefreshDailyRotation()
    {
        string today = GetTodayKey();
        string savedDate = PlayerPrefs.GetString(PREF_ACTIVE_DATE, "");

        if (savedDate == today)
        {
            bool allValid = true;
            for (int i = 0; i < DAILY_SLOT_COUNT; i++)
            {
                string tplId = PlayerPrefs.GetString(PREF_SLOT_ID + i, "");
                if (!TryFindTemplateById(tplId, out MissionTemplate tpl))
                {
                    allValid = false;
                    break;
                }

                _dailySlots[i] = new DailyMissionState
                {
                    slotIndex = i,
                    template = tpl,
                    progress = Mathf.Clamp(PlayerPrefs.GetInt(PREF_SLOT_PROG + i, 0), 0, tpl.targetCount),
                    claimed = PlayerPrefs.GetInt(PREF_SLOT_CLAIMED + i, 0) == 1
                };
            }

            if (allValid)
            {
                _activeDateKey = today;
                return;
            }
        }

        // Yeni gün veya ilk kurulum: tarih tabanlı deterministik 4 benzersiz görev seç
        _activeDateKey = today;
        PlayerPrefs.SetString(PREF_ACTIVE_DATE, today);

        List<MissionTemplate> picked = PickDailyTemplates(today);
        for (int i = 0; i < DAILY_SLOT_COUNT; i++)
        {
            MissionTemplate tpl = picked[i];
            _dailySlots[i] = new DailyMissionState
            {
                slotIndex = i,
                template = tpl,
                progress = 0,
                claimed = false
            };

            PlayerPrefs.SetString(PREF_SLOT_ID + i, tpl.id);
            PlayerPrefs.SetInt(PREF_SLOT_PROG + i, 0);
            PlayerPrefs.SetInt(PREF_SLOT_CLAIMED + i, 0);
        }

        PlayerPrefs.Save();
    }

    private static List<MissionTemplate> PickDailyTemplates(string dateKey)
    {
        int n = MissionPool.Length;
        int[] indices = new int[n];
        for (int i = 0; i < n; i++) indices[i] = i;

        uint seed = ComputeStableHash(dateKey);
        for (int i = n - 1; i > 0; i--)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            if (seed == 0) seed = 0x9E3779B9u;
            int j = (int)(seed % (uint)(i + 1));
            int tmp = indices[i];
            indices[i] = indices[j];
            indices[j] = tmp;
        }

        var selected = new List<MissionTemplate>(DAILY_SLOT_COUNT);
        var usedTypes = new HashSet<OpsEventType>();

        for (int i = 0; i < n && selected.Count < DAILY_SLOT_COUNT; i++)
        {
            MissionTemplate candidate = MissionPool[indices[i]];
            if (usedTypes.Add(candidate.eventType))
                selected.Add(candidate);
        }

        for (int i = 0; i < n && selected.Count < DAILY_SLOT_COUNT; i++)
        {
            MissionTemplate candidate = MissionPool[indices[i]];
            if (!selected.Contains(candidate))
                selected.Add(candidate);
        }

        return selected;
    }

    private static uint ComputeStableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            return hash == 0 ? 0x811C9DC5u : hash;
        }
    }

    private static string GetTodayKey()
    {
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    private static bool TryFindTemplateById(string id, out MissionTemplate template)
    {
        for (int i = 0; i < MissionPool.Length; i++)
        {
            if (MissionPool[i].id == id)
            {
                template = MissionPool[i];
                return true;
            }
        }
        template = default;
        return false;
    }

    private static PilotMedalDef FindMedalDef(string id)
    {
        for (int i = 0; i < MedalDefs.Length; i++)
        {
            if (MedalDefs[i].id == id) return MedalDefs[i];
        }
        return null;
    }

    private int GetStatValue(string statKey)
    {
        PlayerProfileManager pm = PlayerProfileManager.EnsureInstance();
        switch (statKey)
        {
            case "totalKills":        return pm != null ? pm.totalKills : 0;
            case "totalBosses":       return pm != null ? pm.totalBosses : 0;
            case "totalWavesCleared": return pm != null ? pm.totalWavesCleared : 0;
            case "level":             return pm != null ? pm.level : 1;
            case "swarmKills":        return PlayerPrefs.GetInt(PREF_STAT_SWARM, 0);
            case "perfectDodges":     return PlayerPrefs.GetInt(PREF_STAT_DODGE, 0);
            case "overdrivesUsed":    return PlayerPrefs.GetInt(PREF_STAT_OVERDRIVE, 0);
            case "lootsCollected":    return PlayerPrefs.GetInt(PREF_STAT_LOOT, 0);
            case "dailyCompletedTotal": return PlayerPrefs.GetInt(PREF_STAT_DAILY_DONE, 0);
            default: return 0;
        }
    }

    private static string GetCounterPrefKey(OpsEventType eventType)
    {
        switch (eventType)
        {
            case OpsEventType.SwarmDroneKill:     return PREF_STAT_SWARM;
            case OpsEventType.PerfectDodge:       return PREF_STAT_DODGE;
            case OpsEventType.OverdriveActivated: return PREF_STAT_OVERDRIVE;
            case OpsEventType.LootCollected:      return PREF_STAT_LOOT;
            default: return null;
        }
    }

    private static void IncrementLifetimeStat(string prefKey, int delta)
    {
        int v = PlayerPrefs.GetInt(prefKey, 0) + delta;
        PlayerPrefs.SetInt(prefKey, v);
    }
}
