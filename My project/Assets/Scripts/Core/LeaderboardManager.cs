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
        Hardcore = 4,    // Hardcore Modu (Tek Can, 200 HP, İlerlenen Boss Sayısı)
        Campaign = 5     // Harekat Modu (20 Aşamalı Sefer)
    }

    public enum DifficultyFilter
    {
        All = -1,
        Kolay = 0,
        Normal = 1,
        Zor = 2
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
        public GameSettings.Difficulty difficulty = GameSettings.Difficulty.Normal;
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

    public static string GetBaseModeKey(BoardMode mode)
    {
        return mode switch
        {
            BoardMode.Campaign => "campaign",
            BoardMode.Endless => "endless",
            BoardMode.TimeAttack => "time_attack",
            BoardMode.BulletHell => "bullet_hell",
            BoardMode.BossRush => "boss_rush",
            BoardMode.Hardcore => "hardcore",
            _ => "campaign"
        };
    }

    public static string GetDifficultySuffix(GameSettings.Difficulty diff)
    {
        return diff switch
        {
            GameSettings.Difficulty.Kolay => "easy",
            GameSettings.Difficulty.Zor => "hard",
            _ => "normal"
        };
    }

    public static string GetDifficultyLabel(GameSettings.Difficulty diff)
    {
        return diff switch
        {
            GameSettings.Difficulty.Kolay => "KOLAY",
            GameSettings.Difficulty.Zor => "ZOR",
            _ => "NORMAL"
        };
    }

    public static string GetDifficultyColoredTag(GameSettings.Difficulty diff)
    {
        return diff switch
        {
            GameSettings.Difficulty.Kolay => "<color=#4ADE80>[KOLAY]</color>",
            GameSettings.Difficulty.Zor => "<color=#F87171>[ZOR]</color>",
            _ => "<color=#FACC15>[NORMAL]</color>"
        };
    }

    public static string GetModeKey(BoardMode mode)
    {
        return GetModeKey(mode, GameSettings.SelectedDifficulty);
    }

    public static string GetModeKey(BoardMode mode, GameSettings.Difficulty diff)
    {
        return $"{GetBaseModeKey(mode)}_{GetDifficultySuffix(diff)}";
    }

    public static GameSettings.Difficulty ParseDifficultyFromRecord(string modeKey, string extraInfo)
    {
        string m = (modeKey ?? "").ToLowerInvariant();
        if (m.EndsWith("_easy") || m.Contains("_kolay")) return GameSettings.Difficulty.Kolay;
        if (m.EndsWith("_hard") || m.Contains("_zor")) return GameSettings.Difficulty.Zor;
        if (m.EndsWith("_normal")) return GameSettings.Difficulty.Normal;

        string ex = (extraInfo ?? "").ToUpperInvariant();
        if (ex.Contains("KOLAY") || ex.Contains("EASY")) return GameSettings.Difficulty.Kolay;
        if (ex.Contains("ZOR") || ex.Contains("HARD")) return GameSettings.Difficulty.Zor;
        return GameSettings.Difficulty.Normal;
    }

    public static string StripDifficultySuffix(string modeKey)
    {
        if (string.IsNullOrEmpty(modeKey)) return "";
        string m = modeKey.Trim().ToLowerInvariant();
        if (m.EndsWith("_easy")) return m.Substring(0, m.Length - 5);
        if (m.EndsWith("_normal")) return m.Substring(0, m.Length - 7);
        if (m.EndsWith("_hard")) return m.Substring(0, m.Length - 5);
        return m;
    }

    private static string GetLocalDiffPrefKey(BoardMode mode, GameSettings.Difficulty diff)
    {
        string basePref = mode switch
        {
            BoardMode.Campaign => "AirBattle_CampaignHighScore",
            BoardMode.Endless => "AirBattle_EndlessHighScore",
            BoardMode.TimeAttack => "AirBattle_TimeAttackHighScore",
            BoardMode.BulletHell => "AirBattle_BulletHellBestTime",
            BoardMode.BossRush => "AirBattle_BossRushHighScore",
            BoardMode.Hardcore => "AirBattle_HardcoreHighScore",
            _ => "AirBattle_CampaignHighScore"
        };
        return $"{basePref}_{diff}";
    }

    [Serializable]
    private class SubmitScoreRpcPayload
    {
        public string p_username;
        public string p_mode;
        public int p_score;
        public float p_time_seconds;
        public string p_formatted_score;
        public string p_rank_title;
        public string p_extra_info;
    }

    private static string GetAuthenticatedLeaderboardName()
    {
        if (PilotAuthManager.IsAuthenticatedInSession && !string.IsNullOrEmpty(PilotAuthManager.LastLoggedInUsername))
            return ProfileManager.CleanProfileName(PilotAuthManager.LastLoggedInUsername);
        return PlayerCallsign;
    }

    /// <summary>
    /// Mod bitiminde skoru aktif zorluk derecesine (Kolay/Normal/Zor) özel olarak hem yerel PlayerPrefs'e
    /// hem de Supabase skor tablosuna ayrı kayıt olarak aktarır.
    /// </summary>
    public void SubmitScore(BoardMode mode, int score, float timeSeconds = 0f, string extra = "")
    {
        SubmitScoreWithDifficulty(mode, GameSettings.SelectedDifficulty, score, timeSeconds, extra);
    }

    public void SubmitScoreWithDifficulty(BoardMode mode, GameSettings.Difficulty diff, int score, float timeSeconds = 0f, string extra = "")
    {
        UpdateLocalRecord(mode, diff, score, timeSeconds);

        int bestScore = Mathf.Max(score, GetLocalRecordScore(mode, diff));
        float bestTime = mode == BoardMode.BulletHell
            ? Mathf.Max(timeSeconds, ProfileManager.GetFloat(GetLocalDiffPrefKey(BoardMode.BulletHell, diff), 0f))
            : timeSeconds;

        if (bestScore <= 0 && bestTime <= 0f) return;

        string baseTag = string.IsNullOrEmpty(extra) ? GetBaseModeKey(mode).ToUpperInvariant() : extra;
        string diffTag = GetDifficultyLabel(diff);
        string taggedExtra = baseTag.Contains(diffTag) ? baseTag : $"{baseTag}|{diffTag}";

        if (IsSupabaseConfigured)
        {
            StartCoroutine(UploadScoreToSupabaseRoutine(mode, diff, bestScore, bestTime, taggedExtra));
        }
    }

    /// <summary>
    /// Giriş yapan oyuncunun cihazdaki tüm mod ve zorluk rekorlarını Supabase skor tablosuyla senkronize eder.
    /// </summary>
    public void SyncAllLocalRecordsToSupabase()
    {
        if (!IsSupabaseConfigured) return;

        GameSettings.Difficulty[] diffs = { GameSettings.Difficulty.Kolay, GameSettings.Difficulty.Normal, GameSettings.Difficulty.Zor };
        BoardMode[] modes = { BoardMode.Campaign, BoardMode.Endless, BoardMode.TimeAttack, BoardMode.BulletHell, BoardMode.BossRush, BoardMode.Hardcore };

        foreach (var diff in diffs)
        {
            string dLabel = GetDifficultyLabel(diff);
            foreach (var m in modes)
            {
                if (m == BoardMode.BulletHell)
                {
                    float bhBest = ProfileManager.GetFloat(GetLocalDiffPrefKey(m, diff), 0f);
                    if (bhBest > 1f)
                        StartCoroutine(UploadScoreToSupabaseRoutine(m, diff, Mathf.RoundToInt(bhBest), bhBest, $"BULLET|{dLabel}"));
                }
                else
                {
                    int mBest = GetLocalRecordScore(m, diff);
                    if (mBest > 0)
                    {
                        string prefix = m == BoardMode.Campaign ? "HAREKAT" : (m == BoardMode.Endless ? "SONSUZ" : m.ToString().ToUpperInvariant());
                        StartCoroutine(UploadScoreToSupabaseRoutine(m, diff, mBest, 0f, $"{prefix}|{dLabel}"));
                    }
                }
            }
        }
    }

    private bool UpdateLocalRecord(BoardMode mode, GameSettings.Difficulty diff, int score, float timeSeconds)
    {
        bool isNew = false;
        string diffKey = GetLocalDiffPrefKey(mode, diff);

        if (mode == BoardMode.BulletHell)
        {
            float bestDiffTime = ProfileManager.GetFloat(diffKey, 0f);
            if (timeSeconds > bestDiffTime)
            {
                ProfileManager.SetFloat(diffKey, timeSeconds);
                isNew = true;
            }
            float bestOverall = ProfileManager.GetFloat("AirBattle_BulletHellBestTime", 0f);
            if (timeSeconds > bestOverall)
            {
                ProfileManager.SetFloat("AirBattle_BulletHellBestTime", timeSeconds);
                isNew = true;
            }
            if (isNew) ProfileManager.Save();
            return isNew;
        }

        int curDiffBest = ProfileManager.GetInt(diffKey, 0);
        if (score > curDiffBest)
        {
            ProfileManager.SetInt(diffKey, score);
            isNew = true;
        }

        // Genel özet anahtarı da güncel tut
        string summaryKey = mode switch
        {
            BoardMode.Campaign => "AirBattle_CampaignHighScore",
            BoardMode.Endless => "AirBattle_EndlessHighScore",
            BoardMode.TimeAttack => "AirBattle_TimeAttackHighScore",
            BoardMode.BossRush => "AirBattle_BossRushHighScore",
            BoardMode.Hardcore => "AirBattle_HardcoreHighScore",
            _ => ""
        };
        if (!string.IsNullOrEmpty(summaryKey))
        {
            int curSummary = ProfileManager.GetInt(summaryKey, 0);
            if (score > curSummary)
            {
                ProfileManager.SetInt(summaryKey, score);
                isNew = true;
            }
        }

        if (isNew) ProfileManager.Save();
        return isNew;
    }

    public int GetLocalRecordScore(BoardMode mode)
    {
        return GetLocalRecordScore(mode, GameSettings.SelectedDifficulty);
    }

    public int GetLocalRecordScore(BoardMode mode, GameSettings.Difficulty diff)
    {
        string diffKey = GetLocalDiffPrefKey(mode, diff);
        if (mode == BoardMode.BulletHell)
            return Mathf.RoundToInt(ProfileManager.GetFloat(diffKey, 0f));

        int val = ProfileManager.GetInt(diffKey, 0);
        if (mode == BoardMode.BossRush && val >= 100)
            val = Mathf.Clamp(val / 100, 1, 4);
        return val;
    }

    private IEnumerator UploadScoreToSupabaseRoutine(BoardMode mode, GameSettings.Difficulty diff, int score, float timeSeconds, string extra)
    {
        if (!IsSupabaseConfigured) yield break;

        string activePlayer = GetAuthenticatedLeaderboardName();
        string modeKey = GetModeKey(mode, diff);
        string rankTitle = GetPilotRankByScore(mode, score);
        string formatted = GetStageDisplayText(modeKey, score, timeSeconds, extra);

        // 1. Öncelik: Sunucu tarafında rekor korumalı ve hesap bağlantılı submit_pilot_score RPC'si
        string rpcEndpoint = $"{supabaseUrl}/rest/v1/rpc/submit_pilot_score";
        SubmitScoreRpcPayload rpcPayload = new SubmitScoreRpcPayload
        {
            p_username = activePlayer,
            p_mode = modeKey,
            p_score = score,
            p_time_seconds = timeSeconds,
            p_formatted_score = formatted,
            p_rank_title = rankTitle,
            p_extra_info = extra ?? ""
        };

        bool useFallbackTableUpsert = false;
        using (UnityWebRequest rpcReq = new UnityWebRequest(rpcEndpoint, "POST"))
        {
            byte[] rpcRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(rpcPayload));
            rpcReq.uploadHandler = new UploadHandlerRaw(rpcRaw);
            rpcReq.downloadHandler = new DownloadHandlerBuffer();
            rpcReq.timeout = 8;

            rpcReq.SetRequestHeader("apikey", supabaseAnonKey);
            rpcReq.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            rpcReq.SetRequestHeader("Content-Type", "application/json");

            yield return rpcReq.SendWebRequest();

            if (rpcReq.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[LeaderboardManager] Supabase rekoru başarıyla kaydedildi (RPC): {activePlayer} [{modeKey}] - {formatted}");
                LeaderboardUI.Instance?.RefreshScores();
                yield break;
            }
            else if (rpcReq.responseCode == 404)
            {
                // RPC henüz SQL Editor'de çalıştırılmamışsa doğrudan tablo UPSERT yedeğine geç
                useFallbackTableUpsert = true;
            }
            else
            {
                Debug.LogWarning($"[LeaderboardManager] RPC skor yükleme hatası ({rpcReq.responseCode}): {rpcReq.error}");
                yield break;
            }
        }

        if (!useFallbackTableUpsert) yield break;

        // 2. Yedek: Doğrudan leaderboard tablosuna on_conflict=player_name,mode ile UPSERT
        string endpoint = $"{supabaseUrl}/rest/v1/leaderboard?on_conflict=player_name,mode";
        SupabaseScoreRecord record = new SupabaseScoreRecord
        {
            player_name = activePlayer,
            mode = modeKey,
            score = score,
            time_seconds = timeSeconds,
            formatted_score = formatted,
            rank_title = rankTitle,
            extra_info = extra ?? ""
        };

        string jsonBody = JsonUtility.ToJson(record);

        using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 8;

            req.SetRequestHeader("apikey", supabaseAnonKey);
            req.SetRequestHeader("Authorization", $"Bearer {supabaseAnonKey}");
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Prefer", "resolution=merge-duplicates");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[LeaderboardManager] Supabase skoru başarıyla yüklendi: {activePlayer} - {formatted}");
                LeaderboardUI.Instance?.RefreshScores();
            }
            else
            {
                Debug.LogWarning($"[LeaderboardManager] Supabase yükleme hatası: {req.error} | Detay: {req.downloadHandler?.text}");
            }
        }
    }

    public static string GetBaseModeTitle(string modeKey, string extraInfo = "")
    {
        string baseKey = StripDifficultySuffix(modeKey);
        if (baseKey == "campaign" || baseKey == "story" || baseKey == "harekat")
            return "HAREKAT";

        if (!string.IsNullOrEmpty(extraInfo) && extraInfo.IndexOf("HAREKAT", StringComparison.OrdinalIgnoreCase) >= 0)
            return "HAREKAT";

        return baseKey switch
        {
            "endless" => "SONSUZ",
            "boss_rush" => "BOSS RUSH",
            "time_attack" => "ZAMAN TAARRUZU",
            "bullet_hell" => "MERMİ FIRTINASI",
            "hardcore" => "HARDCORE",
            _ => "HAREKAT"
        };
    }

    public static string GetModeDisplayName(string modeKey, string extraInfo = "")
    {
        string baseTitle = GetBaseModeTitle(modeKey, extraInfo);
        GameSettings.Difficulty diff = ParseDifficultyFromRecord(modeKey, extraInfo);
        return $"{baseTitle} {GetDifficultyColoredTag(diff)}";
    }

    public static string GetStageDisplayText(string modeKey, int score, float timeSeconds = 0f, string extraInfo = "")
    {
        string baseTitle = GetBaseModeTitle(modeKey, extraInfo);
        if (baseTitle == "HAREKAT")
            return $"{Mathf.Max(1, score)}. BÖLÜM";
        if (baseTitle == "SONSUZ")
            return $"{Mathf.Max(1, score)}. DALGA";
        if (baseTitle == "BOSS RUSH" || baseTitle == "HARDCORE")
            return $"{Mathf.Max(1, score)}. AŞAMA";
        if (baseTitle == "ZAMAN TAARRUZU")
            return $"{Mathf.Max(1, score)} İMHA";
        if (baseTitle == "MERMİ FIRTINASI")
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
    /// Tüm oyun modlarındaki gerçek oyuncu rekorlarını zorluk derecesine (Kolay / Normal / Zor / Tümü) göre
    /// birbirinden bağımsız satırlar olarak Supabase ve yerel profillerden çeker.
    /// </summary>
    public void FetchAllModesLeaderboard(Action<List<LeaderboardEntry>, bool> onComplete)
    {
        FetchAllModesLeaderboard(DifficultyFilter.All, onComplete);
    }

    public void FetchAllModesLeaderboard(DifficultyFilter filter, Action<List<LeaderboardEntry>, bool> onComplete)
    {
        StartCoroutine(FetchAllModesLeaderboardRoutine(filter, onComplete));
    }

    private IEnumerator FetchAllModesLeaderboardRoutine(DifficultyFilter filter, Action<List<LeaderboardEntry>, bool> onComplete)
    {
        string activeUser = PlayerCallsign;
        List<string> knownProfiles = ProfileManager.GetKnownProfiles();

        List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
        var entryIndexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        bool isLiveOnline = false;

        if (IsSupabaseConfigured)
        {
            string endpoint = $"{supabaseUrl}/rest/v1/leaderboard?select=*&order=score.desc,time_seconds.asc&limit=60";
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
                            for (int i = 0; i < records.Length; i++)
                            {
                                var r = records[i];
                                if (r == null || string.IsNullOrEmpty(r.player_name)) continue;
                                if (IsSamplePlaceholderName(r.player_name)) continue;

                                GameSettings.Difficulty recDiff = ParseDifficultyFromRecord(r.mode, r.extra_info);
                                if (filter != DifficultyFilter.All && (int)recDiff != (int)filter)
                                    continue;

                                string baseModeTitle = GetBaseModeTitle(r.mode, r.extra_info);
                                // Aynı oyuncunun farklı zorluklardaki (Kolay vs Zor) ve farklı modlardaki rekorları ayrı tutulur!
                                string dedupKey = filter == DifficultyFilter.All
                                    ? $"{r.player_name}|{recDiff}|{baseModeTitle}"
                                    : $"{r.player_name}|{recDiff}";

                                int cleanScore = NormalizeModeScore(r.mode, r.score);
                                if (entryIndexByKey.TryGetValue(dedupKey, out int existIdx))
                                {
                                    if (cleanScore > entries[existIdx].score)
                                    {
                                        entries[existIdx].score = cleanScore;
                                        entries[existIdx].timeInSeconds = r.time_seconds;
                                        entries[existIdx].extraInfo = r.extra_info;
                                        entries[existIdx].modeDisplayName = GetModeDisplayName(r.mode, r.extra_info);
                                        entries[existIdx].stageDisplayText = GetStageDisplayText(r.mode, cleanScore, r.time_seconds, r.extra_info);
                                        entries[existIdx].formattedScore = entries[existIdx].stageDisplayText;
                                    }
                                    continue;
                                }

                                bool isMe = r.player_name.Equals(activeUser, StringComparison.OrdinalIgnoreCase);
                                entryIndexByKey[dedupKey] = entries.Count;
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
                                    stageDisplayText = GetStageDisplayText(r.mode, cleanScore, r.time_seconds, r.extra_info),
                                    difficulty = recDiff
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

        // Cihazdaki gerçek kullanıcı profillerinin her zorluk derecesindeki (Kolay / Normal / Zor) yerel rekorlarını ayrı ayrı entegre et
        GameSettings.Difficulty[] allDiffs = { GameSettings.Difficulty.Kolay, GameSettings.Difficulty.Normal, GameSettings.Difficulty.Zor };
        foreach (string prof in knownProfiles)
        {
            if (string.IsNullOrEmpty(prof) || IsSamplePlaceholderName(prof)) continue;
            bool isMe = prof.Equals(activeUser, StringComparison.OrdinalIgnoreCase);
            bool addedAnyForProf = false;

            foreach (var diff in allDiffs)
            {
                if (filter != DifficultyFilter.All && (int)diff != (int)filter)
                    continue;

                if (TryGetBestProfileRecordForDifficulty(prof, diff, out BoardMode pMode, out int pScore, out float pTime, out string pExtra))
                {
                    addedAnyForProf = true;
                    string pModeKey = GetModeKey(pMode, diff);
                    string baseModeTitle = GetBaseModeTitle(pModeKey, pExtra);
                    string dedupKey = filter == DifficultyFilter.All
                        ? $"{prof}|{diff}|{baseModeTitle}"
                        : $"{prof}|{diff}";

                    if (entryIndexByKey.TryGetValue(dedupKey, out int existIdx))
                    {
                        if (isMe) entries[existIdx].isPlayer = true;
                        if (pScore > entries[existIdx].score)
                        {
                            entries[existIdx].score = pScore;
                            entries[existIdx].timeInSeconds = pTime;
                            entries[existIdx].extraInfo = pExtra;
                            entries[existIdx].modeDisplayName = GetModeDisplayName(pModeKey, pExtra);
                            entries[existIdx].stageDisplayText = GetStageDisplayText(pModeKey, pScore, pTime, pExtra);
                            entries[existIdx].formattedScore = entries[existIdx].stageDisplayText;
                        }
                    }
                    else
                    {
                        entryIndexByKey[dedupKey] = entries.Count;
                        entries.Add(new LeaderboardEntry
                        {
                            rank = entries.Count + 1,
                            playerName = prof,
                            score = pScore,
                            timeInSeconds = pTime,
                            formattedScore = GetStageDisplayText(pModeKey, pScore, pTime, pExtra),
                            rankTitle = GetPilotRankByScore(pMode, pScore),
                            isPlayer = isMe,
                            extraInfo = pExtra,
                            modeDisplayName = GetModeDisplayName(pModeKey, pExtra),
                            stageDisplayText = GetStageDisplayText(pModeKey, pScore, pTime, pExtra),
                            difficulty = diff
                        });
                    }
                }
            }

            // Eski sürümden kalma (zorluk soneki henüz yazılmamış) özet rekor varsa yalnızca Normal zorluk altına yedekle
            if (!addedAnyForProf && (filter == DifficultyFilter.All || filter == DifficultyFilter.Normal))
            {
                GetLegacyBestRecordForProfile(prof, out BoardMode legMode, out int legScore, out string legExtra);
                if (legScore > 0)
                {
                    string legModeKey = GetModeKey(legMode, GameSettings.Difficulty.Normal);
                    string baseModeTitle = GetBaseModeTitle(legModeKey, legExtra);
                    string dedupKey = filter == DifficultyFilter.All
                        ? $"{prof}|{GameSettings.Difficulty.Normal}|{baseModeTitle}"
                        : $"{prof}|{GameSettings.Difficulty.Normal}";

                    if (!entryIndexByKey.ContainsKey(dedupKey))
                    {
                        entryIndexByKey[dedupKey] = entries.Count;
                        entries.Add(new LeaderboardEntry
                        {
                            rank = entries.Count + 1,
                            playerName = prof,
                            score = legScore,
                            timeInSeconds = 0f,
                            formattedScore = GetStageDisplayText(legModeKey, legScore, 0f, legExtra),
                            rankTitle = GetPilotRankByScore(legMode, legScore),
                            isPlayer = isMe,
                            extraInfo = legExtra,
                            modeDisplayName = GetModeDisplayName(legModeKey, legExtra),
                            stageDisplayText = GetStageDisplayText(legModeKey, legScore, 0f, legExtra),
                            difficulty = GameSettings.Difficulty.Normal
                        });
                    }
                }
            }
        }

        // Sıralama: TÜMÜ sekmesinde Zor > Normal > Kolay öncelikli, ardından skor büyüklüğüne göre; tekil zorluk sekmelerinde doğrudan skora göre
        entries.Sort((a, b) =>
        {
            if (filter == DifficultyFilter.All && a.difficulty != b.difficulty)
                return ((int)b.difficulty).CompareTo((int)a.difficulty);
            int cmp = b.score.CompareTo(a.score);
            if (cmp != 0) return cmp;
            return ((int)b.difficulty).CompareTo((int)a.difficulty);
        });

        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].rank = i + 1;
        }

        onComplete?.Invoke(entries, isLiveOnline);
    }

    private static int NormalizeModeScore(string modeKey, int rawScore)
    {
        string m = StripDifficultySuffix(modeKey);
        if (m == "boss_rush")
        {
            if (rawScore >= 100) return Mathf.Clamp(rawScore / 100, 1, 4);
            return Mathf.Clamp(rawScore, 1, 4);
        }
        return Mathf.Max(1, rawScore);
    }

    private bool TryGetBestProfileRecordForDifficulty(
        string profileName,
        GameSettings.Difficulty diff,
        out BoardMode bestMode,
        out int bestScore,
        out float bestTime,
        out string extraInfo)
    {
        bestMode = BoardMode.Campaign;
        bestScore = 0;
        bestTime = 0f;
        string dLabel = GetDifficultyLabel(diff);
        extraInfo = $"HAREKAT|{dLabel}";

        int campaign = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, $"AirBattle_CampaignHighScore_{diff}"), 0);
        int endless = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, $"AirBattle_EndlessHighScore_{diff}"), 0);
        int timeAtk = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, $"AirBattle_TimeAttackHighScore_{diff}"), 0);
        float bhFloat = PlayerPrefs.GetFloat(ProfileManager.GetPrefKeyFor(profileName, $"AirBattle_BulletHellBestTime_{diff}"), 0f);
        int bulletHell = Mathf.RoundToInt(bhFloat);
        int bossRushRaw = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, $"AirBattle_BossRushHighScore_{diff}"), 0);
        int bossRush = bossRushRaw >= 100 ? Mathf.Clamp(bossRushRaw / 100, 1, 4) : Mathf.Clamp(bossRushRaw, 0, 4);
        int hardcore = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, $"AirBattle_HardcoreHighScore_{diff}"), 0);

        if (endless > 0 && endless >= campaign)
        {
            bestMode = BoardMode.Endless;
            bestScore = endless;
            extraInfo = $"SONSUZ|{dLabel}";
            return true;
        }
        if (campaign > 0)
        {
            bestMode = BoardMode.Campaign;
            bestScore = Mathf.Clamp(campaign, 1, 20);
            extraInfo = $"HAREKAT|{dLabel}";
            return true;
        }
        if (hardcore > 0)
        {
            bestMode = BoardMode.Hardcore;
            bestScore = hardcore;
            extraInfo = $"HARDCORE|{dLabel}";
            return true;
        }
        if (bossRush > 0)
        {
            bestMode = BoardMode.BossRush;
            bestScore = bossRush;
            extraInfo = $"BOSS|{dLabel}";
            return true;
        }
        if (timeAtk > 0)
        {
            bestMode = BoardMode.TimeAttack;
            bestScore = timeAtk;
            extraInfo = $"TIME|{dLabel}";
            return true;
        }
        if (bulletHell > 0)
        {
            bestMode = BoardMode.BulletHell;
            bestScore = bulletHell;
            bestTime = bhFloat;
            extraInfo = $"BULLET|{dLabel}";
            return true;
        }

        return false;
    }

    private void GetLegacyBestRecordForProfile(string profileName, out BoardMode bestMode, out int bestScore, out string extraInfo)
    {
        int campaign = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_CampaignHighScore"), 0);
        int endless = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_EndlessHighScore"), 0);
        int timeAtk = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_TimeAttackHighScore"), 0);
        int bulletHell = Mathf.RoundToInt(PlayerPrefs.GetFloat(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_BulletHellBestTime"), 0f));
        int bossRushRaw = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_BossRushHighScore"), 0);
        int bossRush = bossRushRaw >= 100 ? Mathf.Clamp(bossRushRaw / 100, 1, 4) : Mathf.Clamp(bossRushRaw, 0, 4);
        int hardcore = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(profileName, "AirBattle_HardcoreHighScore"), 0);

        if (endless > campaign && endless > 0)
        {
            bestMode = BoardMode.Endless;
            bestScore = endless;
            extraInfo = "SONSUZ|NORMAL";
        }
        else if (campaign > 0)
        {
            bestMode = BoardMode.Campaign;
            bestScore = Mathf.Clamp(campaign, 1, 20);
            extraInfo = "HAREKAT|NORMAL";
        }
        else if (bossRush > 0)
        {
            bestMode = BoardMode.BossRush;
            bestScore = bossRush;
            extraInfo = "BOSS|NORMAL";
        }
        else if (hardcore > 0)
        {
            bestMode = BoardMode.Hardcore;
            bestScore = hardcore;
            extraInfo = "HARDCORE|NORMAL";
        }
        else if (timeAtk > 0)
        {
            bestMode = BoardMode.TimeAttack;
            bestScore = timeAtk;
            extraInfo = "TIME|NORMAL";
        }
        else if (bulletHell > 0)
        {
            bestMode = BoardMode.BulletHell;
            bestScore = bulletHell;
            extraInfo = "BULLET|NORMAL";
        }
        else
        {
            bestMode = BoardMode.Campaign;
            bestScore = 0;
            extraInfo = "HAREKAT|NORMAL";
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
            case BoardMode.Campaign:
                return $"{score}. BÖLÜM";
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
            case BoardMode.Campaign:
                if (score >= 20) return "BAŞKOMUTAN AS";
                if (score >= 15) return "HAVA ALBAYI";
                if (score >= 10) return "FİLO BİNBAŞISI";
                if (score >= 5)  return "KIDEMLİ YÜZBAŞI";
                return "TEĞMEN PİLOT";

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
