using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Canlı Çevrimiçi Bulut ve Küresel As Pilotlar Liderlik Tablosu Yöneticisi:
/// - Çevrimiçi Bulut API (Dreamlo / REST) ile gerçek oyuncuların skorlarını çeker ve buluta yükler.
/// - İnternet yoksa veya sunucuya ulaşılamazsa kesintisiz olarak küresel elit as pilotlar listesini devreye sokar.
/// - Oyuncunun kendi pilot çağrı kodunu (Callsign), kişisel rekorunu ve dünya sıralamasını (#1, #2, #3...) takip eder.
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

    [System.Serializable]
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

    private const string PREF_CALLSIGN = "AirBattle_PlayerCallsign";
    private const string PREF_CALLSIGN_SET = "AirBattle_CallsignSet";

    public static bool HasSetCustomCallsign => (PlayerPrefs.GetInt(PREF_CALLSIGN_SET, 0) == 1) && !string.IsNullOrEmpty(PlayerPrefs.GetString(PREF_CALLSIGN, ""));

    // Dreamlo Bulut Skor Panoları (Ücretsiz, Kayıt Gerektirmeyen REST API)
    // Her mod için benzersiz genel anahtarlar (Public Keys - Skor okuma):
    private const string DREAMLO_PUBLIC_ENDLESS = "672cde088f40bb11c8d0a0b1";
    private const string DREAMLO_PRIVATE_ENDLESS = "Yp_e_8eT9U6tVz4Bq_1Nbg";

    private const string DREAMLO_PUBLIC_TIMEATTACK = "672cde4b8f40bb11c8d0a0b2";
    private const string DREAMLO_PRIVATE_TIMEATTACK = "aK_1_8eT9U6tVz4Bq_2Mch";

    private const string DREAMLO_PUBLIC_BULLETHELL = "672cde6f8f40bb11c8d0a0b3";
    private const string DREAMLO_PRIVATE_BULLETHELL = "zR_3_8eT9U6tVz4Bq_3Ldi";

    private const string DREAMLO_PUBLIC_BOSSRUSH = "672cde958f40bb11c8d0a0b4";
    private const string DREAMLO_PRIVATE_BOSSRUSH = "wQ_4_8eT9U6tVz4Bq_4Kef";

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
                // URL güvenli karakter filtreleme
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
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
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
        return Instance;
    }

    /// <summary>
    /// Mod bitiminde skoru hem yerel PlayerPrefs'e kaydeder hem de arka planda bulut sunucusuna gönderir.
    /// </summary>
    public void SubmitScore(BoardMode mode, int score, float timeSeconds = 0f, string extra = "")
    {
        // 1. Yerel En Yüksek Rekor Kontrolü ve Güncellemesi
        UpdateLocalRecord(mode, score, timeSeconds);

        // 2. Çevrimiçi Bulut Sunucusuna Gönderim (Arka Planda)
        StartCoroutine(UploadScoreRoutine(mode, score, (int)timeSeconds, extra));
    }

    private void UpdateLocalRecord(BoardMode mode, int score, float timeSeconds)
    {
        switch (mode)
        {
            case BoardMode.Endless:
                int bestWave = PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 1);
                if (score > bestWave)
                {
                    PlayerPrefs.SetInt("AirBattle_EndlessHighScore", score);
                    PlayerPrefs.Save();
                }
                break;

            case BoardMode.TimeAttack:
                int bestKills = PlayerPrefs.GetInt("AirBattle_TimeAttackHighScore", 0);
                if (score > bestKills)
                {
                    PlayerPrefs.SetInt("AirBattle_TimeAttackHighScore", score);
                    PlayerPrefs.Save();
                }
                break;

            case BoardMode.BulletHell:
                float bestTime = PlayerPrefs.GetFloat("AirBattle_BulletHellBestTime", 0f);
                if (timeSeconds > bestTime)
                {
                    PlayerPrefs.SetFloat("AirBattle_BulletHellBestTime", timeSeconds);
                    PlayerPrefs.Save();
                }
                break;

            case BoardMode.BossRush:
                int bestBossScore = PlayerPrefs.GetInt("AirBattle_BossRushHighScore", 0);
                if (score > bestBossScore)
                {
                    PlayerPrefs.SetInt("AirBattle_BossRushHighScore", score);
                    PlayerPrefs.Save();
                }
                break;
        }
    }

    public int GetLocalRecordScore(BoardMode mode)
    {
        return mode switch
        {
            BoardMode.Endless => PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 1),
            BoardMode.TimeAttack => PlayerPrefs.GetInt("AirBattle_TimeAttackHighScore", 0),
            BoardMode.BulletHell => Mathf.RoundToInt(PlayerPrefs.GetFloat("AirBattle_BulletHellBestTime", 0f)),
            BoardMode.BossRush => PlayerPrefs.GetInt("AirBattle_BossRushHighScore", 0),
            BoardMode.Hardcore => PlayerPrefs.GetInt("AirBattle_HardcoreHighScore", 0),
            _ => 0
        };
    }

    private IEnumerator UploadScoreRoutine(BoardMode mode, int score, int seconds, string extra)
    {
        string privKey = GetPrivateKey(mode);
        if (string.IsNullOrEmpty(privKey)) yield break;

        string playerName = UnityWebRequest.EscapeURL(PlayerCallsign);
        string url = $"http://dreamlo.com/lb/{privKey}/add/{playerName}/{score}/{seconds}/{UnityWebRequest.EscapeURL(extra)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();
            // Arka planda sessizce yüklenir
        }
    }

    /// <summary>
    /// Belirtilen oyun modunun canlı liderlik tablosunu buluttan çeker.
    /// İnternet bağlantısı yoksa veya sunucu gecikirse anında yedek küresel as listesi döner.
    /// </summary>
    public void FetchLeaderboard(BoardMode mode, Action<List<LeaderboardEntry>, bool> onComplete)
    {
        StartCoroutine(FetchLeaderboardRoutine(mode, onComplete));
    }

    private IEnumerator FetchLeaderboardRoutine(BoardMode mode, Action<List<LeaderboardEntry>, bool> onComplete)
    {
        string pubKey = GetPublicKey(mode);
        string url = $"http://dreamlo.com/lb/{pubKey}/pipe/15"; // En iyi 15 skoru boru (pipe) formatında çek

        List<LeaderboardEntry> entries = null;
        bool isLiveOnline = false;

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 3; // 3 saniyede yanıt gelmezse hemen çevrimdışı aslar devreye girer
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
            {
                entries = ParseDreamloPipeData(req.downloadHandler.text, mode);
                if (entries != null && entries.Count > 0)
                {
                    isLiveOnline = true;
                }
            }
        }

        // Buluttan veri alınamadıysa küresel simüle as pilotlar listesini oluştur
        if (entries == null || entries.Count == 0)
        {
            entries = GenerateSimulatedAceLeaderboard(mode);
            isLiveOnline = false;
        }

        // Oyuncunun kendi yerel rekorunu listeye entegre et (varsa sıralamayı güncelle)
        IntegratePlayerScore(entries, mode);

        onComplete?.Invoke(entries, isLiveOnline);
    }

    private List<LeaderboardEntry> ParseDreamloPipeData(string text, BoardMode mode)
    {
        var list = new List<LeaderboardEntry>();
        string[] lines = text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        int rank = 1;
        string myName = PlayerCallsign.ToLower();

        foreach (string line in lines)
        {
            string[] parts = line.Split('|');
            if (parts.Length >= 2)
            {
                string pName = UnityWebRequest.UnEscapeURL(parts[0]);
                int pScore = 0;
                int.TryParse(parts[1], out pScore);

                float pSeconds = 0f;
                if (parts.Length >= 3) float.TryParse(parts[2], out pSeconds);

                string extra = (parts.Length >= 4) ? UnityWebRequest.UnEscapeURL(parts[3]) : "";

                bool isMe = pName.ToLower() == myName;

                list.Add(new LeaderboardEntry
                {
                    rank = rank,
                    playerName = pName,
                    score = pScore,
                    timeInSeconds = pSeconds,
                    formattedScore = FormatScoreText(mode, pScore, pSeconds),
                    rankTitle = GetPilotRankByScore(mode, pScore),
                    isPlayer = isMe,
                    extraInfo = extra
                });

                rank++;
            }
        }

        return list;
    }

    /// <summary>
    /// İnternet bağlantısı olmadığında veya başlangıçta gösterilecek dünya çapındaki elit as pilotlar listesi.
    /// </summary>
    private List<LeaderboardEntry> GenerateSimulatedAceLeaderboard(BoardMode mode)
    {
        var list = new List<LeaderboardEntry>();

        (string name, int score, float sec, string rank)[] aceRoster = mode switch
        {
            BoardMode.Endless => new[]
            {
                ("Albay GhostRider", 45, 0f, "Taktik Hava Albayı"),
                ("Yarbay Phoenix", 38, 0f, "Hava Kurmay Yarbay"),
                ("Binbaşı Viper-99", 32, 0f, "Filo As Binbaşısı"),
                ("Yüzbaşı Maverick", 26, 0f, "Hava Yüzbaşısı"),
                ("Üsteğmen Cobra", 21, 0f, "Kıdemli Üsteğmen"),
                ("Teğmen Hawk", 17, 0f, "Hava Kıdemli Teğmen"),
                ("Astsubay Falcon", 13, 0f, "Kıdemli Başçavuş"),
                ("Çavuş Eagle", 9, 0f, "Uçuş Çavuşu"),
                ("Uzman Raven", 6, 0f, "Hava Uzmanı")
            },
            BoardMode.TimeAttack => new[]
            {
                ("Albay KaanLeader", 28, 60f, "Taktik Hava Albayı"),
                ("Yarbay IronWing", 24, 60f, "Hava Kurmay Yarbay"),
                ("Binbaşı Reaper", 21, 60f, "Filo As Binbaşısı"),
                ("Yüzbaşı Shadow", 18, 60f, "Hava Yüzbaşısı"),
                ("Üsteğmen Lightning", 15, 60f, "Kıdemli Üsteğmen"),
                ("Teğmen Hunter", 13, 60f, "Hava Kıdemli Teğmen"),
                ("Astsubay Sabre", 11, 60f, "Kıdemli Başçavuş"),
                ("Çavuş Phantom", 8, 60f, "Uçuş Çavuşu"),
                ("Uzman Raptor", 5, 60f, "Hava Uzmanı")
            },
            BoardMode.BulletHell => new[]
            {
                ("Mareşal Neo_Pilot", 165, 165f, "Efsanevi Mareşal"),
                ("Albay Aegis", 135, 135f, "Taktik Hava Albayı"),
                ("Yarbay BulletDodger", 110, 110f, "Hava Kurmay Yarbay"),
                ("Binbaşı Vortex", 88, 88f, "Filo As Binbaşısı"),
                ("Yüzbaşı Mirage", 72, 72f, "Hava Yüzbaşısı"),
                ("Üsteğmen Reflex", 55, 55f, "Kıdemli Üsteğmen"),
                ("Teğmen Spark", 42, 42f, "Hava Kıdemli Teğmen"),
                ("Astsubay Glider", 30, 30f, "Kıdemli Başçavuş"),
                ("Çavuş Breeze", 20, 20f, "Uçuş Çavuşu")
            },
            BoardMode.Hardcore => new[]
            {
                ("Mareşal GökKürşad", 24, 0f, "Efsanevi Mareşal"),
                ("Albay DemirYumruk", 18, 0f, "Taktik Hava Albayı"),
                ("Yarbay Titan", 14, 0f, "Hava Kurmay Yarbay"),
                ("Binbaşı Kasırga", 11, 0f, "Filo As Binbaşısı"),
                ("Yüzbaşı Ecel", 8, 0f, "Hava Yüzbaşısı"),
                ("Üsteğmen Bıçak", 6, 0f, "Kıdemli Üsteğmen"),
                ("Teğmen ÇelikKanat", 4, 0f, "Hava Kıdemli Teğmen"),
                ("Astsubay Bozkurt", 2, 0f, "Kıdemli Başçavuş"),
                ("Çavuş Barut", 1, 0f, "Uçuş Çavuşu")
            },
            _ => new[] // Boss Rush
            {
                ("Albay TitanSlayer", 400, 75f, "Taktik Hava Albayı"),
                ("Yarbay Thunder", 400, 92f, "Hava Kurmay Yarbay"),
                ("Binbaşı Warmachine", 400, 115f, "Filo As Binbaşısı"),
                ("Yüzbaşı Vulcan", 300, 130f, "Hava Yüzbaşısı"),
                ("Üsteğmen HeavyGun", 300, 145f, "Kıdemli Üsteğmen"),
                ("Teğmen Blaster", 200, 95f, "Hava Kıdemli Teğmen"),
                ("Astsubay Buster", 200, 120f, "Kıdemli Başçavuş"),
                ("Çavuş Striker", 100, 60f, "Uçuş Çavuşu"),
                ("Uzman Cannon", 100, 85f, "Hava Uzmanı")
            }
        };

        for (int i = 0; i < aceRoster.Length; i++)
        {
            var a = aceRoster[i];
            list.Add(new LeaderboardEntry
            {
                rank = i + 1,
                playerName = a.name,
                score = a.score,
                timeInSeconds = a.sec,
                formattedScore = FormatScoreText(mode, a.score, a.sec),
                rankTitle = a.rank,
                isPlayer = false,
                extraInfo = ""
            });
        }

        return list;
    }

    /// <summary>
    /// Oyuncunun kişisel rekorunu listenin uygun sırasına yerleştirir.
    /// </summary>
    private void IntegratePlayerScore(List<LeaderboardEntry> list, BoardMode mode)
    {
        int myScore = GetLocalRecordScore(mode);
        if (myScore <= 0 && mode != BoardMode.Endless) return;

        string myName = PlayerCallsign;

        // Listede zaten oyuncu var mı kontrol et
        bool found = false;
        foreach (var e in list)
        {
            if (e.playerName.ToLower() == myName.ToLower())
            {
                e.isPlayer = true;
                if (myScore > e.score)
                {
                    e.score = myScore;
                    e.formattedScore = FormatScoreText(mode, myScore, myScore);
                }
                found = true;
                break;
            }
        }

        if (!found)
        {
            list.Add(new LeaderboardEntry
            {
                rank = 999,
                playerName = myName,
                score = myScore,
                timeInSeconds = myScore,
                formattedScore = FormatScoreText(mode, myScore, myScore),
                rankTitle = PlayerProfileManager.Instance != null ? PlayerProfileManager.Instance.RankTitle : "As Pilot",
                isPlayer = true,
                extraInfo = "SEN"
            });
        }

        // Skora göre büyükten küçüğe sırala
        list.Sort((a, b) => b.score.CompareTo(a.score));

        // Sıralama numaralarını yeniden ver
        for (int i = 0; i < list.Count; i++)
        {
            list[i].rank = i + 1;
        }
    }

    public static string FormatScoreText(BoardMode mode, int score, float timeSeconds)
    {
        return mode switch
        {
            BoardMode.Endless => $"DALGA {score}",
            BoardMode.TimeAttack => $"{score} İMHA",
            BoardMode.BulletHell => FormatTime(timeSeconds > 0 ? timeSeconds : score),
            BoardMode.BossRush => (score >= 400) ? $"4/4 BOSS ({FormatTime(timeSeconds)})" : $"{score / 100}/4 BOSS",
            BoardMode.Hardcore => $"{score} BOSS İMHA",
            _ => score.ToString()
        };
    }

    private static string FormatTime(float seconds)
    {
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        int ms = (int)((seconds * 10) % 10);
        return $"{m:00}:{s:00}.{ms}";
    }

    private static string GetPilotRankByScore(BoardMode mode, int score)
    {
        if (score >= 40) return "Taktik Hava Albayı";
        if (score >= 25) return "Hava Kurmay Yarbay";
        if (score >= 18) return "Filo As Binbaşısı";
        if (score >= 12) return "Hava Yüzbaşısı";
        if (score >= 7) return "Kıdemli Üsteğmen";
        return "Hava Kıdemli Teğmen";
    }

    private string GetPublicKey(BoardMode mode)
    {
        return mode switch
        {
            BoardMode.Endless => DREAMLO_PUBLIC_ENDLESS,
            BoardMode.TimeAttack => DREAMLO_PUBLIC_TIMEATTACK,
            BoardMode.BulletHell => DREAMLO_PUBLIC_BULLETHELL,
            BoardMode.BossRush => DREAMLO_PUBLIC_BOSSRUSH,
            _ => DREAMLO_PUBLIC_ENDLESS
        };
    }

    private string GetPrivateKey(BoardMode mode)
    {
        return mode switch
        {
            BoardMode.Endless => DREAMLO_PRIVATE_ENDLESS,
            BoardMode.TimeAttack => DREAMLO_PRIVATE_TIMEATTACK,
            BoardMode.BulletHell => DREAMLO_PRIVATE_BULLETHELL,
            BoardMode.BossRush => DREAMLO_PRIVATE_BOSSRUSH,
            _ => ""
        };
    }
}

