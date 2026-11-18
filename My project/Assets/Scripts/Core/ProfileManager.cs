using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Çoklu Oyuncu / Bağımsız Pilot Profili Yöneticisi:
/// - Aynı cihaz üzerinde birden fazla oyuncunun tamamen bağımsız profillere sahip olmasını sağlar.
/// - Her oyuncunun Seviyesi (Level), Altını, Hangardaki Uçakları, İHA/Dronları, Sandıkları,
///   Askeri Parçaları ve Dalga Rekorları kendi pilot çağrı koduna özel (namespaced) saklanır.
/// - Farklı bir kullanıcı adı girildiğinde veya seçildiğinde tüm envanter ve kariyer verileri anında o profile geçer.
/// - Eski tek-profilli verileri mevcut aktif pilota otomatik ve güvenle taşır (veri kaybı sıfır).
/// </summary>
public static class ProfileManager
{
    private const string PREF_ACTIVE_PROFILE = "AirBattle_ActiveProfile";
    private const string PREF_PROFILES_LIST = "AirBattle_ProfilesList";

    public static event Action<string> OnProfileSwitched;

    public static string ActiveProfile
    {
        get
        {
            string p = PlayerPrefs.GetString(PREF_ACTIVE_PROFILE, "");
            if (string.IsNullOrEmpty(p))
            {
                p = PlayerPrefs.GetString("AirBattle_PlayerCallsign", "");
                if (string.IsNullOrEmpty(p))
                {
                    p = "Pilot_" + UnityEngine.Random.Range(100, 999);
                }
                SetActiveProfile(p);
            }
            return p;
        }
    }

    /// <summary>
    /// Verilen profil için anahtara profil önekini (P_KullaniciAdi_anahtar) ekler.
    /// </summary>
    public static string GetPrefKey(string baseKey)
    {
        return $"P_{ActiveProfile}_{baseKey}";
    }

    public static string GetPrefKeyFor(string profile, string baseKey)
    {
        string clean = CleanProfileName(profile);
        return $"P_{clean}_{baseKey}";
    }

    public static string CleanProfileName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Pilot_01";
        string clean = raw.Trim().Replace(" ", "_").Replace("|", "").Replace("/", "").Replace("\\", "").Replace(",", "").Replace(";", "");
        if (clean.Length > 14) clean = clean.Substring(0, 14);
        return string.IsNullOrEmpty(clean) ? "Pilot_01" : clean;
    }

    // ==========================================
    // PROFİL LİSTESİ VE SEÇİMİ
    // ==========================================

    public static List<string> GetKnownProfiles()
    {
        string raw = PlayerPrefs.GetString(PREF_PROFILES_LIST, "");
        List<string> list = new List<string>();
        if (!string.IsNullOrEmpty(raw))
        {
            string[] parts = raw.Split(',');
            foreach (var p in parts)
            {
                string clean = CleanProfileName(p);
                if (!string.IsNullOrEmpty(clean) && !list.Exists(x => x.Equals(clean, StringComparison.OrdinalIgnoreCase)))
                    list.Add(clean);
            }
        }

        string active = ActiveProfile;
        if (!list.Exists(x => x.Equals(active, StringComparison.OrdinalIgnoreCase)))
        {
            list.Insert(0, active);
            SaveKnownProfiles(list);
        }

        return list;
    }

    public static void SaveKnownProfiles(List<string> list)
    {
        PlayerPrefs.SetString(PREF_PROFILES_LIST, string.Join(",", list));
        PlayerPrefs.Save();
    }

    public static void SetActiveProfile(string profileName)
    {
        string clean = CleanProfileName(profileName);

        PlayerPrefs.SetString(PREF_ACTIVE_PROFILE, clean);
        PlayerPrefs.SetString("AirBattle_PlayerCallsign", clean);
        PlayerPrefs.SetInt("AirBattle_CallsignSet", 1);
        PlayerPrefs.Save();

        var known = GetKnownProfiles();
        if (!known.Exists(x => x.Equals(clean, StringComparison.OrdinalIgnoreCase)))
        {
            known.Add(clean);
            SaveKnownProfiles(known);
        }

        CheckAndMigrateLegacyData(clean);
    }

    /// <summary>
    /// Başka bir kullanıcı adına geçiş yapar ve ilgili tüm verileri yükleyip UI'ı yeniler.
    /// </summary>
    public static void SwitchProfile(string targetProfile)
    {
        string clean = CleanProfileName(targetProfile);
        if (clean.Equals(ActiveProfile, StringComparison.OrdinalIgnoreCase))
        {
            return; // Zaten bu profil aktif
        }

        // 1. Mevcut profilin verilerini diske kaydet
        try
        {
            HangarManager.Instance?.SaveHangarData();
            PlayerProfileManager.Instance?.SaveProfile();
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] Önceki profil kaydedilirken uyarı: {ex.Message}");
        }

        // 2. Yeni profili aktif yap (varsa legacy verileri aktarır, yoksa sıfırdan oluşturur)
        SetActiveProfile(clean);

        // 3. Yöneticilerin yeni profil verilerini hafızaya yüklemesini sağla
        try
        {
            PlayerProfileManager.EnsureInstance()?.LoadProfile();
            HangarManager.EnsureInstance()?.LoadHangarData();
            MilitaryStoreManager.Instance?.NotifyStoreDataChanged();
            DailyOpsManager.EnsureInstance()?.LoadOrRefreshDailyRotation();
            LeaderboardManager.EnsureInstance()?.SyncPlayerCampaignHistory();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ProfileManager] Yeni profil verileri yüklenirken hata: {ex.Message}");
        }

        // 4. Arayüzleri yeni profile göre tazele
        try
        {
            PlayerProfileUI.Instance?.RefreshBadgeVisuals();
            HangarUI.Instance?.RefreshAllVisuals();
            MilitaryStoreUI.Instance?.RefreshAllVisuals();
            LeaderboardUI.Instance?.RefreshScores();
            DailyOpsUI.Instance?.RefreshAll();
            TouchArrowControls.Instance?.UpdateVisibility();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileManager] UI tazeleme uyarısı: {ex.Message}");
        }

        OnProfileSwitched?.Invoke(clean);
    }

    // ==========================================
    // PROFİLE ÖZEL PLAYERPREFS YARDIMCILARI
    // ==========================================

    public static int GetInt(string baseKey, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(GetPrefKey(baseKey), defaultValue);
    }

    public static void SetInt(string baseKey, int value)
    {
        PlayerPrefs.SetInt(GetPrefKey(baseKey), value);
    }

    public static float GetFloat(string baseKey, float defaultValue = 0f)
    {
        return PlayerPrefs.GetFloat(GetPrefKey(baseKey), defaultValue);
    }

    public static void SetFloat(string baseKey, float value)
    {
        PlayerPrefs.SetFloat(GetPrefKey(baseKey), value);
    }

    public static string GetString(string baseKey, string defaultValue = "")
    {
        return PlayerPrefs.GetString(GetPrefKey(baseKey), defaultValue);
    }

    public static void SetString(string baseKey, string value)
    {
        PlayerPrefs.SetString(GetPrefKey(baseKey), value);
    }

    public static bool HasKey(string baseKey)
    {
        return PlayerPrefs.HasKey(GetPrefKey(baseKey));
    }

    public static void DeleteKey(string baseKey)
    {
        PlayerPrefs.DeleteKey(GetPrefKey(baseKey));
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    // ==========================================
    // ESKİ VERİLERİN İLK SEFERE ÖZEL GÖÇÜ (MIGRATION)
    // ==========================================

    private static void CheckAndMigrateLegacyData(string profileName)
    {
        string lvlKey = GetPrefKeyFor(profileName, "AirBattle_Pilot_Level");
        // Eğer bu profil için henüz seviye kaydedilmemişse ve cihazda eski genel kayıtlar varsa aktar
        if (!PlayerPrefs.HasKey(lvlKey))
        {
            // Eski genel anahtarlar cihazda mevcut mu kontrol et
            if (PlayerPrefs.HasKey("AirBattle_Pilot_Level") || PlayerPrefs.HasKey("AirBattle_Gold") || PlayerPrefs.HasKey("AirBattle_EndlessHighScore"))
            {
                // Mevcut oyuncunun kazanımlarını bu profile kopyala
                CopyIntIfExist("AirBattle_Pilot_Level", lvlKey, 1);
                CopyIntIfExist("AirBattle_Pilot_Exp", GetPrefKeyFor(profileName, "AirBattle_Pilot_Exp"), 0);
                CopyIntIfExist("AirBattle_Total_Kills", GetPrefKeyFor(profileName, "AirBattle_Total_Kills"), 0);
                CopyIntIfExist("AirBattle_Total_Bosses", GetPrefKeyFor(profileName, "AirBattle_Total_Bosses"), 0);
                CopyIntIfExist("AirBattle_Total_Waves", GetPrefKeyFor(profileName, "AirBattle_Total_Waves"), 0);

                CopyIntIfExist("AirBattle_Gold", GetPrefKeyFor(profileName, "AirBattle_Gold"), 250);
                CopyIntIfExist("AirBattle_ActiveAircraft", GetPrefKeyFor(profileName, "AirBattle_ActiveAircraft"), 0);
                CopyIntIfExist("AirBattle_Total_Crates", GetPrefKeyFor(profileName, "AirBattle_Total_Crates"), 0);
                CopyIntIfExist("AirBattle_Lvl_Armor", GetPrefKeyFor(profileName, "AirBattle_Lvl_Armor"), 0);
                CopyIntIfExist("AirBattle_Lvl_Damage", GetPrefKeyFor(profileName, "AirBattle_Lvl_Damage"), 0);
                CopyIntIfExist("AirBattle_Lvl_Speed", GetPrefKeyFor(profileName, "AirBattle_Lvl_Speed"), 0);
                CopyIntIfExist("AirBattle_Lvl_Crit", GetPrefKeyFor(profileName, "AirBattle_Lvl_Crit"), 0);

                CopyIntIfExist("AirBattle_Key_Bronze", GetPrefKeyFor(profileName, "AirBattle_Key_Bronze"), 1);
                CopyIntIfExist("AirBattle_Key_Silver", GetPrefKeyFor(profileName, "AirBattle_Key_Silver"), 0);
                CopyIntIfExist("AirBattle_Key_Gold", GetPrefKeyFor(profileName, "AirBattle_Key_Gold"), 0);

                CopyIntIfExist("AirBattle_EndlessHighScore", GetPrefKeyFor(profileName, "AirBattle_EndlessHighScore"), 0);
                CopyIntIfExist("AirBattle_TimeAttackHighScore", GetPrefKeyFor(profileName, "AirBattle_TimeAttackHighScore"), 0);
                CopyFloatIfExist("AirBattle_BulletHellBestTime", GetPrefKeyFor(profileName, "AirBattle_BulletHellBestTime"), 0f);
                CopyIntIfExist("AirBattle_BossRushHighScore", GetPrefKeyFor(profileName, "AirBattle_BossRushHighScore"), 0);
                CopyIntIfExist("AirBattle_HardcoreHighScore", GetPrefKeyFor(profileName, "AirBattle_HardcoreHighScore"), 0);

                CopyIntIfExist("AirBattle_Unl_A10", GetPrefKeyFor(profileName, "AirBattle_Unl_A10"), 0);
                CopyIntIfExist("AirBattle_Unl_F22", GetPrefKeyFor(profileName, "AirBattle_Unl_F22"), 0);
                CopyIntIfExist("AirBattle_Unl_SU57", GetPrefKeyFor(profileName, "AirBattle_Unl_SU57"), 0);
                CopyIntIfExist("AirBattle_Unl_F35", GetPrefKeyFor(profileName, "AirBattle_Unl_F35"), 0);
                CopyIntIfExist("AirBattle_Unl_KAAN", GetPrefKeyFor(profileName, "AirBattle_Unl_KAAN"), 0);

                PlayerPrefs.Save();
            }
            else
            {
                // Yepyeni profil: Başlangıç varsayılanlarını yaz
                PlayerPrefs.SetInt(lvlKey, 1);
                PlayerPrefs.SetInt(GetPrefKeyFor(profileName, "AirBattle_Gold"), 250);
                PlayerPrefs.SetInt(GetPrefKeyFor(profileName, "AirBattle_Key_Bronze"), 1);
                PlayerPrefs.Save();
            }
        }
    }

    private static void CopyIntIfExist(string oldKey, string newKey, int defaultVal)
    {
        int val = PlayerPrefs.GetInt(oldKey, defaultVal);
        PlayerPrefs.SetInt(newKey, val);
    }

    private static void CopyFloatIfExist(string oldKey, string newKey, float defaultVal)
    {
        float val = PlayerPrefs.GetFloat(oldKey, defaultVal);
        PlayerPrefs.SetFloat(newKey, val);
    }
}
