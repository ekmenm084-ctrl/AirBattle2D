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
    }

    [Serializable]
    public class SupabaseScoreRecord
    {
        public int id;
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

    public static bool HasSetCustomCallsign => (PlayerPrefs.GetInt(PREF_CALLSIGN_SET, 0) == 1) && !string.IsNullOrEmpty(PlayerPrefs.GetString(PREF_CALLSIGN, ""));

    public static string PlayerCallsign
    {
        get
        {
            string name = PlayerPrefs.GetString(PREF_CALLSIGN, "");
            if (string.IsNullOrEmpty(name))
            {
                name = "Pilot_" + UnityEngine.Random.Range(100, 999);
                PlayerPrefs.SetString(PREF_CALLSIGN, name);
                PlayerPrefs.Save();
            }
            return name;
        }
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                string clean = value.Trim().Replace(" ", "_").Replace("|", "").Replace("/", "");
                if (clean.Length > 16) clean = clean.Substring(0, 16);
                PlayerPrefs.SetString(PREF_CALLSIGN, clean);
                PlayerPrefs.SetInt(PREF_CALLSIGN_SET, 1);
                PlayerPrefs.Save();
            }
        }
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

    private void SyncPlayerCampaignHistory()
    {
        int wave = PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 0);
        if (wave < 5)
        {
            PlayerPrefs.SetInt("AirBattle_EndlessHighScore", 5);
            PlayerPrefs.Save();
        }
    }

    public static LeaderboardManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("LeaderboardManager");
        if (go == null) go = new GameObject("LeaderboardManager");
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
                    supabaseUrl = cfg.url.Trim().TrimEnd('/');
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
            supabaseUrl = prefUrl.Trim().TrimEnd('/');
            supabaseAnonKey = prefKey.Trim();
        }
    }

    public void SaveSupabaseCredentials(string url, string anonKey)
    {
        supabaseUrl = url.Trim().TrimEnd('/');
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
                int bestWave = PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 0);
                if (score > bestWave)
                {
                    PlayerPrefs.SetInt("AirBattle_EndlessHighScore", score);
                    PlayerPrefs.Save();
                    isNew = true;
                }
                break;

            case BoardMode.TimeAttack:
                int bestKills = PlayerPrefs.GetInt("AirBattle_TimeAttackHighScore", 0);
                if (score > bestKills)
                {
                    PlayerPrefs.SetInt("AirBattle_TimeAttackHighScore", score);
                    PlayerPrefs.Save();
                    isNew = true;
                }
                break;

            case BoardMode.BulletHell:
                float bestTime = PlayerPrefs.GetFloat("AirBattle_BulletHellBestTime", 0f);
                if (timeSeconds > bestTime)
                {
                    PlayerPrefs.SetFloat("AirBattle_BulletHellBestTime", timeSeconds);
                    PlayerPrefs.Save();
                    isNew = true;
                }
                break;

            case BoardMode.BossRush:
                int bestBossScore = PlayerPrefs.GetInt("AirBattle_BossRushHighScore", 0);
                if (score > bestBossScore)
                {
                    PlayerPrefs.SetInt("AirBattle_BossRushHighScore", score);
                    PlayerPrefs.Save();
                    isNew = true;
                }
                break;

            case BoardMode.Hardcore:
                int bestHc = PlayerPrefs.GetInt("AirBattle_HardcoreHighScore", 0);
                if (score > bestHc)
                {
                    PlayerPrefs.SetInt("AirBattle_HardcoreHighScore", score);
                    PlayerPrefs.Save();
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
            BoardMode.Endless => PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 0),
            BoardMode.TimeAttack => PlayerPrefs.GetInt("AirBattle_TimeAttackHighScore", 0),
            BoardMode.BulletHell => Mathf.RoundToInt(PlayerPrefs.GetFloat("AirBattle_BulletHellBestTime", 0f)),
            BoardMode.BossRush => PlayerPrefs.GetInt("AirBattle_BossRushHighScore", 0),
            BoardMode.Hardcore => PlayerPrefs.GetInt("AirBattle_HardcoreHighScore", 0),
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
