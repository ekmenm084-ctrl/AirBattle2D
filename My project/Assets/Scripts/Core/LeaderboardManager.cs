using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Canlı Çevrimiçi Bulut ve Küresel As Pilotlar Liderlik Tablosu Yöneticisi:
/// - Supabase / PostgreSQL REST API (PostgREST) ile dünya genelindeki gerçek oyuncu skorlarını canlı senkronize eder.
/// - UPSERT desteği sayesinde aynı oyuncunun en iyi skoru otomatik güncellenir.
/// - İnternet yoksa veya yapılandırma henüz yapılmamışsa simüle elit as pilotlar listesini devreye sokar.
/// - Oyuncunun pilot çağrı kodunu (Callsign), kişisel rekorunu ve dünya sıralamasını takip eder.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    public enum BoardMode
    {
        Endless = 0,     // Sonsuz İt Dalaşı (Dalga Sayısı)
        TimeAttack = 1,  // Zaman Taarruzu (60s İmha Sayısı)
        BulletHell = 2,  // Mermi Cehennemi (Hayatta Kalma Süresi)
        BossRush = 3,    // Boss Rush (Elenen Boss / Süre Skoru)
        Hardcore = 4     // Hardcore Modu (Tek Can, 200 HP, İlerlenen Boss Sayısı)
    }

    [Serializable]
    public class LeaderboardEntry
    {
        public int rank;
        public string playerName;
        public int score;
        public float timeInSeconds;
        public string formattedScore;
        public string rankTitle;
        public bool isPlayer;
        public string extraInfo;
        public string modeDisplayName;
        public string stageDisplayText;
    }

    [Serializable]
    public class SupabaseScoreRecord
    {
        public string player_name;
        public string mode;
        public int score;
        public float time_seconds;
        public string formatted_score;
        public string rank_title;
        public string extra_info;
    }

    [Serializable]
    public class SupabaseConfig
    {
        public string url = "";
        public string anonKey = "";
    }

    private const string PREF_CALLSIGN = "AirBattle_PlayerCallsign";
    private const string PREF_CALLSIGN_SET = "AirBattle_CallsignSet";
    private const string PREF_SUPABASE_URL = "AirBattle_SupabaseUrl";
    private const string PREF_SUPABASE_KEY = "AirBattle_SupabaseKey";

    [Header("Supabase Yapılandırması")]
    public string supabaseUrl = "";
    public string supabaseAnonKey = "";

    public static bool HasSetCustomCallsign => (PlayerPrefs.GetInt(PREF_CALLSIGN_SET, 0) == 1) && !string.IsNullOrEmpty(ProfileManager.ActiveProfile);

    public static string PlayerCallsign
    {
        get => ProfileManager.ActiveProfile;
        set => ProfileManager.SetActiveProfile(value);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSupabaseConfig();
            SyncPlayerCampaignHistory();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void SyncPlayerCampaignHistory()
    {
        // Eski BossRush *100 puan formatını (100..400) gerçek aşama (1..4) formatına normalize et
        int bossRaw = ProfileManager.GetInt("AirBattle_BossRushHighScore", 0);
        if (bossRaw >= 100)
        {
            ProfileManager.SetInt("AirBattle_BossRushHighScore", Mathf.Clamp(bossRaw / 100, 1, 4));
            ProfileManager.Save();
        }
    }

    private static bool _isQuitting = false;

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    public static LeaderboardManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        if (_isQuitting) return null;

        GameObject go = GameObject.Find("LeaderboardManager");
        if (go == null)
        {
            #if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return null;
            #endif
            go = new GameObject("LeaderboardManager");
        }
        Instance = go.GetComponent<LeaderboardManager>();
        if (Instance == null) Instance = go.AddComponent<LeaderboardManager>();
        DontDestroyOnLoad(go);
        Instance.LoadSupabaseConfig();
        return Instance;
    }

    /// <summary>
    /// Supabase bağlantı bilgilerini Resources/supabase_config.json veya PlayerPrefs'ten yükler.
    /// </summary>
    public void LoadSupabaseConfig()
    {
        // 1. Resources üzerinden kontrol
        TextAsset configAsset = Resources.Load<TextAsset>("supabase_config");
        if (configAsset != null && !string.IsNullOrEmpty(configAsset.text))
        {
            try
            {
                SupabaseConfig cfg = JsonUtility.FromJson<SupabaseConfig>(configAsset.text);
                if (cfg != null && !string.IsNullOrEmpty(cfg.url))
                {
                    supabaseUrl = SanitizeSupabaseUrl(cfg.url);
                    supabaseAnonKey = cfg.anonKey.Trim();
                    return;
                }
            }
            catch { }
        }

        // 2. PlayerPrefs üzerinden kontrol
        string prefUrl = PlayerPrefs.GetString(PREF_SUPABASE_URL, "");
        string prefKey = PlayerPrefs.GetString(PREF_SUPABASE_KEY, "");
        if (!string.IsNullOrEmpty(prefUrl))
        {
            supabaseUrl = SanitizeSupabaseUrl(prefUrl);
            supabaseAnonKey = prefKey.Trim();
        }
    }

    public static string SanitizeSupabaseUrl(string rawUrl)
    {
        if (string.IsNullOrEmpty(rawUrl)) return "";
        string clean = rawUrl.Trim().TrimEnd('/');
        if (clean.EndsWith("/rest/v1", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(0, clean.Length - "/rest/v1".Length);
        }
        return clean.TrimEnd('/');
    }

    public void SaveSupabaseCredentials(string url, string anonKey)
    {
        supabaseUrl = SanitizeSupabaseUrl(url);
        supabaseAnonKey = anonKey.Trim();
        PlayerPrefs.SetString(PREF_SUPABASE_URL, supabaseUrl);
        PlayerPrefs.SetString(PREF_SUPABASE_KEY, supabaseAnonKey);
        PlayerPrefs.Save();
    }

    public bool IsSupabaseConfigured => !string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseAnonKey) && !supabaseUrl.Contains("YOUR_PROJECT_ID");

    public static string GetModeKey(BoardMode mode)
    {
        return mode switch
        {
            BoardMode.Endless => "endless",
            BoardMode.TimeAttack => "time_attack",
            BoardMode.BulletHell => "bullet_hell",
            BoardMode.BossRush => "boss_rush",
            BoardMode.Hardcore => "hardcore",
            _ => "endless"
        };
    }

    /// <summary>
    /// Mod bitiminde skoru yerel PlayerPrefs'e kaydeder ve yeni bir rekor ise Supabase'e gönderir.
    /// </summary>
    public void SubmitScore(BoardMode mode, int score, float timeSeconds = 0f, string extra = "")
    {
        bool isNewRecord = UpdateLocalRecord(mode, score, timeSeconds);

        if (isNewRecord || IsSupabaseConfigured)
        {
            StartCoroutine(UploadScoreToSupabaseRoutine(mode, score, timeSeconds, extra));
        }
    }

    private bool UpdateLocalRecord(BoardMode mode, int score, float timeSeconds)
    {
        bool isNew = false;
        switch (mode)
        {
            case BoardMode.Endless:
                int bestWave = ProfileManager.GetInt("AirBattle_EndlessHighScore", 0);
                if (score > bestWave)
                {
                    ProfileManager.SetInt("AirBattle_EndlessHighScore", score);
                    ProfileManager.Save();
                    isNew = true;
                }
                break;

            case BoardMode.TimeAttack:
                int bestKills = ProfileManager.GetInt("AirBattle_TimeAttackHighScore", 0);
                if (score > bestKills)
                {
                    ProfileManager.SetInt("AirBattle_TimeAttackHighScore", score);
                    ProfileManager.Save();
                    isNew = true;
                }
                break;

            case BoardMode.BulletHell:
                float bestTime = ProfileManager.GetFloat("AirBattle_BulletHellBestTime", 0f);
                if (timeSeconds > bestTime)
                {
                    ProfileManager.SetFloat("AirBattle_BulletHellBestTime", timeSeconds);
                    ProfileManager.Save();
                    isNew = true;
                }
                break;

            case BoardMode.BossRush:
                int bestBossScore = ProfileManager.GetInt("AirBattle_BossRushHighScore", 0);
                if (score > bestBossScore)
                {
                    ProfileManager.SetInt("AirBattle_BossRushHighScore", score);
                    ProfileManager.Save();
                    isNew = true;
                }
                break;

            case BoardMode.Hardcore:
                int bestHc = ProfileManager.GetInt("AirBattle_HardcoreHighScore", 0);
                if (score > bestHc)
                {
                    ProfileManager.SetInt("AirBattle_HardcoreHighScore", score);
                    ProfileManager.Save();
                    isNew = true;
                }
                break;
        }
        return isNew;
    }

    public int GetLocalRecordScore(BoardMode mode)
    {
        return mode switch
        {
            BoardMode.Endless => ProfileManager.GetInt("AirBattle_EndlessHighScore", 0),
            BoardMode.TimeAttack => ProfileManager.GetInt("AirBattle_TimeAttackHighScore", 0),
            BoardMode.BulletHell => Mathf.RoundToInt(ProfileManager.GetFloat("AirBattle_BulletHellBestTime", 0f)),
            BoardMode.BossRush => ProfileManager.GetInt("AirBattle_BossRushHighScore", 0),
            BoardMode.Hardcore => ProfileManager.GetInt("AirBattle_HardcoreHighScore", 0),
            _ => 0
        };
    }

    private IEnumerator UploadScoreToSupabaseRoutine(BoardMode mode, int score, float timeSeconds, string extra)
    {
        if (!IsSupabaseConfigured) yield break;

        string endpoint = $"{supabaseUrl}/rest/v1/leaderboard";
        string modeKey = GetModeKey(mode);
        string rankTitle = GetPilotRankByScore(mode, score);
        string formatted = FormatScoreText(mode, score, timeSeconds);

        SupabaseScoreRecord record = new SupabaseScoreRecord
        {
            player_name = PlayerCallsign,
            mode = modeKey,
            score = score,
            time_seconds = timeSeconds,
            formatted_score = formatted,
            rank_title = rankTitle,
            extra_info = extra
        };

        string jsonBody = JsonUtility.ToJson(record);

        using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 5;

            req.SetRequestHeader("apikey", supabaseAnonKey);
            req.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Prefer", "resolution=merge-duplicates"); // UPSERT: player_name ve mode çakışırsa güncelle!

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[LeaderboardManager] Supabase skoru başarıyla yüklendi: {PlayerCallsign} - {formatted}");
            }
            else
            {
                Debug.LogWarning($"[LeaderboardManager] Supabase yükleme hatası: {req.error} | Detay: {req.downloadHandler?.text}");
            }
        }
    }

    public static string GetModeDisplayName(string modeKey, string extraInfo = "")
    {
        if (!string.IsNullOrEmpty(extraInfo) && extraInfo.IndexOf("HAREKAT", StringComparison.OrdinalIgnoreCase) >= 0)
            return "HAREKAT MODU";

        return (modeKey ?? "").ToLowerInvariant() switch
        {
            "campaign" or "story" or "harekat" => "HAREKAT MODU",
            "endless" => "SONSUZ İT DALAŞI",
            "boss_rush" => "BOSS HAREKATI",
            "time_attack" => "ZAMAN TAARRUZU",
            "bullet_hell" => "MERMİ CEHENNEMİ",
            "hardcore" => "HARDCORE MODU",
            _ => "HAREKAT MODU"
        };
    }

    public static string GetStageDisplayText(string modeKey, int score, float timeSeconds = 0f, string extraInfo = "")
    {
        string modeDisp = GetModeDisplayName(modeKey, extraInfo);
        if (modeDisp == "HAREKAT MODU")
            return $"{Mathf.Max(1, score)}. BÖLÜM";
        if (modeDisp == "SONSUZ İT DALAŞI")
            return $"{Mathf.Max(1, score)}. DALGA";
        if (modeDisp == "BOSS HAREKATI" || modeDisp == "HARDCORE MODU")
            return $"{Mathf.Max(1, score)}. AŞAMA";
        if (modeDisp == "ZAMAN TAARRUZU")
            return $"{Mathf.Max(1, score)} İMHA";
        if (modeDisp == "MERMİ CEHENNEMİ")
        {
            int sec = score > 0 ? score : Mathf.RoundToInt(timeSeconds);
            return $"{Mathf.Max(1, sec)} SN";
        }
        return $"{Mathf.Max(1, score)}. BÖLÜM";
    }

    private static bool IsSamplePlaceholderName(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        string n = name.Trim().ToUpperInvariant();
        return n == "AS_VIPER" || n == "SHADOW_FLIGHT" || n == "TIGER_PILOT" || n == "ROOKIE_ONE";
    }

    /// <summary>
    /// Tüm oyun modlarındaki gerçek oyuncu rekorlarını Supabase veritabanından çeker
    /// ve cihazdaki gerçek kayıtlı kullanıcı profilleriyle senkronize eder.
    /// </summary>
    public void FetchAllModesLeaderboard(Action<List<LeaderboardEntry>, bool> onComplete)
    {
        StartCoroutine(FetchAllModesLeaderboardRoutine(onComplete));
    }

    private IEnumerator FetchAllModesLeaderboardRoutine(Action<List<LeaderboardEntry>, bool> onComplete)
    {
        string activeUser = PlayerCallsign;
        List<string> knownProfiles = ProfileManager.GetKnownProfiles();

        // Cihazdaki tüm gerçek profillerin doğru rekorlarını Supabase'e UPSERT ederek eski hatalı (örn. totalWaves=69) kayıtları ez
        if (IsSupabaseConfigured)
        {
            foreach (string prof in knownProfiles)
            {
                if (string.IsNullOrEmpty(prof) || IsSamplePlaceholderName(prof)) continue;
                GetBestRecordForProfile(prof, out BoardMode pMode, out int pScore, out string pExtra);
                int uploadScore = Mathf.Max(1, pScore);
                yield return StartCoroutine(UploadProfileScoreToSupabaseRoutine(prof, pMode, uploadScore, 0f, pExtra));
            }
        }

        List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
        bool isLiveOnline = false;

        if (IsSupabaseConfigured)
        {
            string endpoint = $"{supabaseUrl}/rest/v1/leaderboard?select=*&order=score.desc,time_seconds.asc&limit=30";
            using (UnityWebRequest req = UnityWebRequest.Get(endpoint))
            {
                req.timeout = 5;
                req.SetRequestHeader("apikey", supabaseAnonKey);
                req.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
                req.SetRequestHeader("Accept", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
                {
                    try
                    {
                        SupabaseScoreRecord[] records = JsonHelper.FromJson<SupabaseScoreRecord>(req.downloadHandler.text);
                        if (records != null && records.Length > 0)
                        {
                            isLiveOnline = true;
                            var seenUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < records.Length; i++)
                            {
                                var r = records[i];
                                if (r == null || string.IsNullOrEmpty(r.player_name)) continue;
                                if (IsSamplePlaceholderName(r.player_name)) continue;

                                if (seenUsers.Contains(r.player_name)) continue;
                                seenUsers.Add(r.player_name);

                                int cleanScore = NormalizeModeScore(r.mode, r.score);
                                bool isMe = r.player_name.Equals(activeUser, StringComparison.OrdinalIgnoreCase);
                                entries.Add(new LeaderboardEntry
                                {
                                    rank = entries.Count + 1,
                                    playerName = r.player_name,
                                    score = cleanScore,
                                    timeInSeconds = r.time_seconds,
                                    formattedScore = GetStageDisplayText(r.mode, cleanScore, r.time_seconds, r.extra_info),
                                    rankTitle = r.rank_title,
                                    isPlayer = isMe,
                                    extraInfo = r.extra_info,
                                    modeDisplayName = GetModeDisplayName(r.mode, r.extra_info),
                                    stageDisplayText = GetStageDisplayText(r.mode, cleanScore, r.time_seconds, r.extra_info)
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LeaderboardManager] Supabase tüm modlar JSON hatası: {ex.Message}");
                    }
                }
            }
        }

        // Cihazda kayıtlı gerçek kullanıcı profillerinin yerel doğru değerlerini tabloya kesin olarak uygula
        foreach (string prof in knownProfiles)
        {
            if (string.IsNullOrEmpty(prof) || IsSamplePlaceholderName(prof)) continue;
            GetBestRecordForProfile(prof, out BoardMode pMode, out int pScore, out string pExtra);
            int finalLocalScore = Mathf.Max(1, pScore);

            string pModeKey = GetModeKey(pMode);
            LeaderboardEntry existing = entries.Find(e => e.playerName.Equals(prof, StringComparison.OrdinalIgnoreCase));
            bool isMe = prof.Equals(activeUser, StringComparison.OrdinalIgnoreCase);

            if (existing != null)
            {
                if (isMe) existing.isPlayer = true;
                // Yerel profilin gerçek rekorunu her zaman esas al (veritabanında eski şişkin bir sayı kalmışsa bile düzelt)
                existing.score = finalLocalScore;
                existing.extraInfo = pExtra;
                existing.modeDisplayName = GetModeDisplayName(pModeKey, pExtra);
                existing.stageDisplayText = GetStageDisplayText(pModeKey, finalLocalScore, 0f, pExtra);
                existing.formattedScore = existing.stageDisplayText;
            }
            else
            {
                entries.Add(new LeaderboardEntry
                {
                    rank = entries.Count + 1,
                    playerName = prof,
                    score = finalLocalScore,
                    timeInSeconds = 0f,
                    formattedScore = GetStageDisplayText(pModeKey, finalLocalScore, 0f, pExtra),
                    rankTitle = GetPilotRankByScore(pMode, finalLocalScore),
                    isPlayer = isMe,
                    extraInfo = pExtra,
                    modeDisplayName = GetModeDisplayName(pModeKey, pExtra),
                    stageDisplayText = GetStageDisplayText(pModeKey, finalLocalScore, 0f, pExtra)
                });
            }
        }

        // Skorlara göre büyükten küçüğe sırala
        entries.Sort((a, b) => b.score.CompareTo(a.score));
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].rank = i + 1;
        }

        onComplete?.Invoke(entries, isLiveOnline);
    }

    private static int NormalizeModeScore(string modeKey, int rawScore)
    {
        string m = (modeKey ?? "").ToLowerInvariant();
        if (m == "boss_rush")
        {
            if (rawScore >= 100) return Mathf.Clamp(rawScore / 100, 1, 4);
            return Mathf.Clamp(rawScore, 1, 4);
        }
        return Mathf.Max(1, rawScore);
    }

    private IEnumerator UploadProfileScoreToSupabaseRoutine(string profileName, BoardMode mode, int score, float timeSeconds, string extra)
    {
        if (!IsSupabaseConfigured || string.IsNullOrEmpty(profileName)) yield break;

        string endpoint = $"{supabaseUrl}/rest/v1/leaderboard";
        string modeKey = GetModeKey(mode);
        string rankTitle = GetPilotRankByScore(mode, score);
        string formatted = GetStageDisplayText(modeKey, score, timeSeconds, extra);

        SupabaseScoreRecord record = new SupabaseScoreRecord
        {
            player_name = profileName,
            mode = modeKey,
            score = score,
            time_seconds = timeSeconds,
            formatted_score = formatted,
            rank_title = rankTitle,
            extra_info = extra
        };

        string jsonBody = JsonUtility.ToJson(record);

        using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 5;

            req.SetRequestHeader("apikey", supabaseAnonKey);
            req.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Prefer", "resolution=merge-duplicates");

            yield return req.SendWebRequest();
        }
    }

    private void GetBestRecordForProfile(string profileName, out BoardMode bestMode, out int bestScore, out string extraInfo)
    {
        // Yalnızca gerçek tek-seferlik oyun modu rekorlarını oku (AirBattle_Total_Waves gibi kümülatif toplamları ASLA kullanma!)
        int campaign = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_CampaignHighScore"), 0);
        int endless = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_EndlessHighScore"), 0);
        int timeAtk = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_TimeAttackHighScore"), 0);
        int bulletHell = Mathf.RoundToInt(PlayerPrefs.GetFloat(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_BulletHellBestTime"), 0f));
        int bossRushRaw = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_BossRushHighScore"), 0);
        int bossRush = bossRushRaw >= 100 ? Mathf.Clamp(bossRushRaw / 100, 1, 4) : Mathf.Clamp(bossRushRaw, 0, 4);
        int hardcore = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_HardcoreHighScore"), 0);

        // Eğer aktif profil ise oyuncunun şu an seçili olan aktif oyun modundaki gerçek rekoruna öncelik ver
        bool isActiveProfile = profileName.Equals(PlayerCallsign, StringComparison.OrdinalIgnoreCase);
        if (isActiveProfile)
        {
            switch (GameSettings.SelectedMode)
            {
                case GameSettings.GameMode.Endless:
                    bestMode = BoardMode.Endless;
                    bestScore = Mathf.Max(1, endless);
                    extraInfo = "SONSUZ";
                    return;

                case GameSettings.GameMode.Solo:
                    bestMode = BoardMode.Endless;
                    int stage = campaign > 0 ? Mathf.Clamp(campaign, 1, 20) : Mathf.Clamp(Mathf.Max(1, endless), 1, 20);
                    bestScore = stage;
                    extraInfo = "HAREKAT";
                    return;

                case GameSettings.GameMode.BossRush:
                    if (bossRush > 0)
                    {
                        bestMode = BoardMode.BossRush;
                        bestScore = bossRush;
                        extraInfo = "BOSS";
                        return;
                    }
                    break;

                case GameSettings.GameMode.Hardcore:
                    if (hardcore > 0)
                    {
                        bestMode = BoardMode.Hardcore;
                        bestScore = hardcore;
                        extraInfo = "HARDCORE";
                        return;
                    }
                    break;

                case GameSettings.GameMode.TimeAttack:
                    if (timeAtk > 0)
                    {
                        bestMode = BoardMode.TimeAttack;
                        bestScore = timeAtk;
                        extraInfo = "TIME";
                        return;
                    }
                    break;

                case GameSettings.GameMode.BulletHell:
                    if (bulletHell > 0)
                    {
                        bestMode = BoardMode.BulletHell;
                        bestScore = bulletHell;
                        extraInfo = "BULLET";
                        return;
                    }
                    break;
            }
        }

        // Varsayılan: Harekat Modu veya Sonsuz İt Dalaşı gerçek rekoru
        if (campaign > 0 && campaign >= endless)
        {
            bestMode = BoardMode.Endless;
            bestScore = Mathf.Clamp(campaign, 1, 20);
            extraInfo = "HAREKAT";
        }
        else if (endless > 0)
        {
            bestMode = BoardMode.Endless;
            bestScore = endless;
            bool isEndlessSelected = (GameSettings.SelectedMode == GameSettings.GameMode.Endless);
            extraInfo = (isEndlessSelected || endless > 20) ? "SONSUZ" : "HAREKAT";
        }
        else if (bossRush > 0)
        {
            bestMode = BoardMode.BossRush;
            bestScore = bossRush;
            extraInfo = "BOSS";
        }
        else if (hardcore > 0)
        {
            bestMode = BoardMode.Hardcore;
            bestScore = hardcore;
            extraInfo = "HARDCORE";
        }
        else if (timeAtk > 0)
        {
            bestMode = BoardMode.TimeAttack;
            bestScore = timeAtk;
            extraInfo = "TIME";
        }
        else if (bulletHell > 0)
        {
            bestMode = BoardMode.BulletHell;
            bestScore = bulletHell;
            extraInfo = "BULLET";
        }
        else
        {
            bestMode = BoardMode.Endless;
            bestScore = 1;
            extraInfo = (GameSettings.SelectedMode == GameSettings.GameMode.Endless) ? "SONSUZ" : "HAREKAT";
        }
    }

    /// <summary>
    /// Belirtilen oyun modunun canlı liderlik tablosunu Supabase bulutundan çeker.
    /// Bağlantı yoksa simüle elit as pilotlar listesini devreye sokar.
    /// </summary>
    public void FetchLeaderboard(BoardMode mode, Action<List<LeaderboardEntry>, bool> onComplete)
    {
        StartCoroutine(FetchLeaderboardRoutine(mode, onComplete));
    }

    private IEnumerator FetchLeaderboardRoutine(BoardMode mode, Action<List<LeaderboardEntry>, bool> onComplete)
    {
        List<LeaderboardEntry> entries = null;
        bool isLiveOnline = false;

        if (IsSupabaseConfigured)
        {
            string modeKey = GetModeKey(mode);
            // Supabase REST sorgusu: mode eşleşen, score azalan sırada ilk 15 skor
            string endpoint = $"{supabaseUrl}/rest/v1/leaderboard?mode=eq.{modeKey}&order=score.desc,time_seconds.asc&limit=15";

            using (UnityWebRequest req = UnityWebRequest.Get(endpoint))
            {
                req.timeout = 4;
                req.SetRequestHeader("apikey", supabaseAnonKey);
                req.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
                req.SetRequestHeader("Accept", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
                {
                    entries = ParseSupabaseJsonData(req.downloadHandler.text, mode);
                    if (entries != null && entries.Count > 0)
                    {
                        isLiveOnline = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"[LeaderboardManager] Supabase okuma başarısız: {req.error} (Yedek liste devreye giriyor)");
                }
            }
        }

        // Sadece gerçek kayıtlar: Supabase bulutundan çekilenler veya yerel gerçek rekor
        if (entries == null)
        {
            entries = new List<LeaderboardEntry>();
        }

        // Oyuncunun kendi yerel gerçek rekorunu listeye entegre et (varsa)
        IntegratePlayerScore(entries, mode);

        onComplete?.Invoke(entries, isLiveOnline);
    }

    private List<LeaderboardEntry> ParseSupabaseJsonData(string jsonText, BoardMode mode)
    {
        try
        {
            SupabaseScoreRecord[] records = JsonHelper.FromJson<SupabaseScoreRecord>(jsonText);
            if (records == null || records.Length == 0) return null;

            var list = new List<LeaderboardEntry>();
            string myName = PlayerCallsign.ToLower();

            for (int i = 0; i < records.Length; i++)
            {
                var r = records[i];
                bool isMe = r.player_name.ToLower() == myName;

                list.Add(new LeaderboardEntry
                {
                    rank = i + 1,
                    playerName = r.player_name,
                    score = r.score,
                    timeInSeconds = r.time_seconds,
                    formattedScore = !string.IsNullOrEmpty(r.formatted_score) ? r.formatted_score : FormatScoreText(mode, r.score, r.time_seconds),
                    rankTitle = !string.IsNullOrEmpty(r.rank_title) ? r.rank_title : GetPilotRankByScore(mode, r.score),
                    isPlayer = isMe,
                    extraInfo = r.extra_info
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LeaderboardManager] Supabase JSON çözümleme hatası: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Oyuncunun yerel rekoru buluttan gelen skorlardan iyiyse veya listede yoksa doğru sıraya entegre eder.
    /// </summary>
    private void IntegratePlayerScore(List<LeaderboardEntry> entries, BoardMode mode)
    {
        int myLocalScore = GetLocalRecordScore(mode);
        if (myLocalScore <= 0) return;

        string myName = PlayerCallsign;
        LeaderboardEntry existing = null;

        foreach (var e in entries)
        {
            if (e.playerName.Equals(myName, StringComparison.OrdinalIgnoreCase))
            {
                existing = e;
                e.isPlayer = true;
                if (myLocalScore > e.score)
                {
                    e.score = myLocalScore;
                    e.formattedScore = FormatScoreText(mode, myLocalScore, 0f);
                    e.rankTitle = GetPilotRankByScore(mode, myLocalScore);
                }
                break;
            }
        }

        if (existing == null)
        {
            entries.Add(new LeaderboardEntry
            {
                rank = entries.Count + 1,
                playerName = myName,
                score = myLocalScore,
                timeInSeconds = 0f,
                formattedScore = FormatScoreText(mode, myLocalScore, 0f),
                rankTitle = GetPilotRankByScore(mode, myLocalScore),
                isPlayer = true,
                extraInfo = "Yerel"
            });
        }

        // Skorlara göre yeniden sırala
        entries.Sort((a, b) => b.score.CompareTo(a.score));

        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].rank = i + 1;
        }
    }

    public static string FormatScoreText(BoardMode mode, int score, float timeSeconds)
    {
        switch (mode)
        {
            case BoardMode.Endless:
                return $"DALGA {score}";
            case BoardMode.TimeAttack:
                return $"{score} İMHA";
            case BoardMode.BulletHell:
                int m = (int)(timeSeconds / 60f);
                int s = (int)(timeSeconds % 60f);
                return $"{m:00}:{s:00} SN";
            case BoardMode.BossRush:
                return $"{score:N0} PUAN";
            case BoardMode.Hardcore:
                return $"BOSS #{score}";
            default:
                return score.ToString();
        }
    }

    public static string GetPilotRankByScore(BoardMode mode, int score)
    {
        switch (mode)
        {
            case BoardMode.Endless:
                if (score >= 40) return "BAŞKOMUTAN AS";
                if (score >= 25) return "HAVA ALBAYI";
                if (score >= 15) return "FİLO BİNBAŞISI";
                if (score >= 8)  return "KIDEMLİ YÜZBAŞI";
                return "TEĞMEN PİLOT";

            case BoardMode.TimeAttack:
                if (score >= 45) return "ÖLÜMCÜL AVCI";
                if (score >= 30) return "TAARRUZ UZMANI";
                if (score >= 20) return "SERİ ATIŞÇI";
                return "HÜCUM PİLOTU";

            case BoardMode.BulletHell:
                if (score >= 140) return "DOKUNULMAZ AS";
                if (score >= 90)  return "MANEVRA USTASI";
                if (score >= 50)  return "ÇEVİK PİLOT";
                return "HAYATTA KALAN";

            case BoardMode.BossRush:
                if (score >= 1200) return "TİTAN KATİLİ";
                if (score >= 800)  return "BOSS CELLADI";
                if (score >= 400)  return "ELİT İMHA ASİ";
                return "AVCILIK FİLOSU";

            case BoardMode.Hardcore:
                if (score >= 10) return "ÇELİK İRADELİ AS";
                if (score >= 6)  return "KORKUSUZ PİLOT";
                if (score >= 3)  return "SAVAŞ GAZİSİ";
                return "CESUR PİLOT";

            default:
                return "AS PİLOT";
        }
    }

    /// <summary>
    /// Unity JsonUtility ile JSON dizilerini ayrıştırmak için hafif yardımcı sınıf.
    /// </summary>
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string wrapper = "{\"Items\":" + json + "}";
            Wrapper<T> w = JsonUtility.FromJson<Wrapper<T>>(wrapper);
            return w != null ? w.Items : new T[0];
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] Items;
        }
    }
}
