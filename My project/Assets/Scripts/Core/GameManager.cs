using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyunun genel akışını yönetir:
/// - Solo Mod: 20 uçaklık ilerlemeli it dalaşı seferi (Wave/Score progression).
///   * 1'den 20'ye doğru her uçakta zorluk artar.
///   * Her 5. uçakta özel BOSS uçakları gelir (5, 10, 15, 20. uçaklar).
///   * Her düşman düştüğünde oyuncu can tamiri kazanır ve sonraki uçak sahneye girer.
/// - Online Mod: İki oyunculu klasik düello.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameMode { SoloVsAI, TwoPlayer, Endless, BossRush, BulletHell, TimeAttack, Hardcore }
    public GameMode CurrentMode { get; private set; } = GameMode.SoloVsAI;

    [Header("20 Uçaklık İlerleme Modu")]
    public int currentPlaneIndex = 1;
    public const int MaxPlanes = 20;

    [Header("Mermi Cehennemi (Bullet Hell)")]
    public float bulletHellTime = 0f;
    public int bulletHellTier = 1;
    private Coroutine _bulletHellCoroutine;

    [Header("Zaman Taarruzu (Time Attack)")]
    public float timeAttackRemaining = 60f;
    public int timeAttackKills = 0;
    private Coroutine _timeAttackCoroutine;

    [Header("Seferde Kazanılan Altın")]
    public int sessionGoldEarned = 0;

    [Header("Oyuncu Can Hakları (Solo Mod)")]
    public int player1Lives = 3;
    public const int MaxPlayerLives = 3;

    [Header("Referanslar")]
    public PlaneHealth plane1Health;
    public PlaneHealth plane2Health;

    [Header("UI")]
    public GameOverUI gameOverUI;

    private bool gameEnded = false;
    public bool IsGameEnded => gameEnded;
    public void SetGameEnded(bool ended) { gameEnded = ended; }
    private Vector3 _plane2OriginalScale = Vector3.one;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        GameSettings.LoadSettings();

        // Eğer sahnede Ana Menü mevcutsa (veya Instance varsa), oyunu ve alt yazıyı burada başlatma!
        // Yalnızca oyuncu ana menüden "HAREKATA BAŞLA" butonuna bastığında ApplyGameMode() çalışacaktır.
        if (MainMenuManager.Instance != null || FindObjectOfType<MainMenuManager>() != null)
        {
            return;
        }

        ApplyGameMode();
    }

    private void Update()
    {
        // Eğer oyun duraklatılmamışsa, menüde değilse veya oyun bitmemişse,
        // Time.timeScale'in anormal şekilde ağır çekimde takılı kalmasını mutlak olarak engelle
        if (!gameEnded && Time.timeScale > 0.001f && Time.timeScale < 0.95f)
        {
            // Boss Rush final sinematik ağır çekimi hariç zamanı daima 1.0x hızına sabitle
            if (CurrentMode != GameMode.BossRush || currentPlaneIndex < 4)
            {
                Time.timeScale = 1.0f;
                Time.fixedDeltaTime = 0.02f;
            }
        }
    }

    /// <summary>
    /// Menüden seçilen Modu (Solo, 2 Oyunculu, Sonsuz İt Dalaşı, Boss Rush) sahneye uygular.
    /// </summary>
    public void ApplyGameMode()
    {
        gameEnded = false;
        sessionGoldEarned = 0;
        GameSettings.LoadSettings();
        CombatComboManager.EnsureInstance().ResetSession();

        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);
        if (isOnline)
            CurrentMode = GameMode.TwoPlayer;
        else if (GameSettings.SelectedMode == GameSettings.GameMode.Endless)
            CurrentMode = GameMode.Endless;
        else if (GameSettings.SelectedMode == GameSettings.GameMode.BossRush)
            CurrentMode = GameMode.BossRush;
        else if (GameSettings.SelectedMode == GameSettings.GameMode.BulletHell)
            CurrentMode = GameMode.BulletHell;
        else if (GameSettings.SelectedMode == GameSettings.GameMode.TimeAttack)
            CurrentMode = GameMode.TimeAttack;
        else if (GameSettings.SelectedMode == GameSettings.GameMode.Hardcore)
            CurrentMode = GameMode.Hardcore;
        else
            CurrentMode = GameMode.SoloVsAI;

        // Arka plan savaş müziğini ve ses ayar butonunu başlat
        AudioManager.EnsureInstance();
        AudioSettingsUI.EnsureInstance();

        FindPlanesIfNull();

        if (plane1Health != null)
        {
            var p1Controller = plane1Health.GetComponent<PlaneController>();
            if (p1Controller != null)
            {
                HangarManager.EnsureInstance()?.ApplyActiveAircraftStats(p1Controller, isActualCombatStart: true);
            }
            DroneManager.EnsureInstance()?.SpawnDronesForPlayer(plane1Health.transform);
        }

        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas != null)
        {
            PlayerProfileUI.EnsureUI()?.SetupHUDProfileBadge(mainCanvas.transform);
            PlayerProfileUI.Instance?.SetHUDBadgeActive(true);
            PlayerProfileUI.Instance?.SetBadgeActive(false);
        }

        if (plane2Health != null)
        {
            plane2Health.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
            _plane2OriginalScale = plane2Health.transform.localScale;
            var ai = plane2Health.GetComponent<AIController>();
            if (ai != null)
            {
                ai.enabled = !isOnline; // Online ise AI kapalı, diğer modlarda açık
            }
        }

        // Dokunmatik kontroller (Joystick ve Ateş butonu): Sadece 2 Oyunculu Online modda görünür
        var p2Joystick = FindObjectOfType<TwoPlayerInputRouter>();
        if (p2Joystick == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                p2Joystick = canvas.gameObject.AddComponent<TwoPlayerInputRouter>();
        }
        if (p2Joystick != null)
            p2Joystick.SetTwoPlayerMode(isOnline);

        MainMenuManager.SetTouchControlsVisibility(showP1Controls: true, showEnemyControls: isOnline);

        if (CurrentMode == GameMode.TwoPlayer)
            AirspaceHazardDirector.Instance?.StopDirector();
        else
            AirspaceHazardDirector.EnsureInstance()?.StartDirector();

        if (CurrentMode == GameMode.TwoPlayer)
        {
            EnemyFormationManager.Instance?.StopDirector();
            ShowActiveModeIntroBanner();
            TwoPlayerDuelManager.EnsureInstance()?.SetupDuelMatch();
        }
        else if (CurrentMode == GameMode.BulletHell)
        {
            EnemyFormationManager.EnsureInstance()?.StartDirector();
            ShowActiveModeIntroBanner();
            StartBulletHellMode();
        }
        else if (CurrentMode == GameMode.TimeAttack)
        {
            EnemyFormationManager.EnsureInstance()?.StartDirector();
            ShowActiveModeIntroBanner();
            StartTimeAttackMode();
        }
        else if (CurrentMode == GameMode.Hardcore)
        {
            player1Lives = 1; // SADECE 1 CAN HAKKI (Permadeath)
            currentPlaneIndex = 1;
            if (plane1Health != null)
            {
                plane1Health.ResetHealth(200f); // UÇAĞIMIZIN CAN BARI 200 OLACAK
            }
            PlayerUpgradeManager.EnsureInstance();
            UpgradeSelectionUI.EnsureInstance();
            EnemyFormationManager.EnsureInstance()?.StartDirector();
            ShowActiveModeIntroBanner();
            ApplyEnemyWave(currentPlaneIndex);
        }
        else
        {
            player1Lives = MaxPlayerLives;
            currentPlaneIndex = 1;
            PlayerUpgradeManager.EnsureInstance();
            UpgradeSelectionUI.EnsureInstance();
            EnemyFormationManager.EnsureInstance()?.StartDirector();

            if (CurrentMode == GameMode.SoloVsAI)
            {
                // Solo Seferde 1. Dalga öncesinde hikaye brifingini oynat
                StoryCampaignManager.EnsureInstance()?.PlayCampaignIntro(() =>
                {
                    ShowActiveModeIntroBanner();
                    ApplyEnemyWave(currentPlaneIndex);
                });
            }
            else
            {
                // Sonsuz İt Dalaşı veya Boss Rush doğrudan başlar
                ShowActiveModeIntroBanner();
                ApplyEnemyWave(currentPlaneIndex);
            }
        }

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateHealthBars();
    }

    private void FindPlanesIfNull()
    {
        if (plane1Health == null || plane2Health == null)
        {
            var planes = FindObjectsOfType<PlaneHealth>();
            foreach (var p in planes)
            {
                var pc = p.GetComponent<PlaneController>();
                if (pc != null)
                {
                    if (pc.isPlayer1) plane1Health = p;
                    else plane2Health = p;
                }
            }
        }
    }

    public int GetTotalPlanesForMode()
    {
        if (CurrentMode == GameMode.BossRush) return 4;
        if (CurrentMode == GameMode.Endless || CurrentMode == GameMode.TimeAttack || CurrentMode == GameMode.Hardcore) return 999;
        if (CurrentMode == GameMode.BulletHell) return 1;
        return MaxPlanes;
    }

    public int GetEffectiveWaveIndex(int index)
    {
        if (CurrentMode == GameMode.BossRush)
        {
            // Boss Rush: 1 -> 5, 2 -> 10, 3 -> 15, 4 -> 20
            return Mathf.Clamp(index, 1, 4) * 5;
        }
        if (CurrentMode == GameMode.TimeAttack)
        {
            return Mathf.Clamp(index, 1, 20);
        }
        return index;
    }

    public bool IsBossPlane(int index)
    {
        if (CurrentMode == GameMode.BossRush) return true;
        if (CurrentMode == GameMode.BulletHell) return true;
        if (CurrentMode == GameMode.Hardcore) return true; // HER GELEN DÜŞMAN UÇAĞI BOSS!
        return (index % 5 == 0);
    }

    public string GetCurrentPlaneTitle(int index)
    {
        if (CurrentMode == GameMode.Hardcore)
        {
            return $"HARDCORE BOSS #{index}";
        }
        int effectiveIndex = GetEffectiveWaveIndex(index);
        return StoryCampaignManager.GetPlaneTitle(effectiveIndex);
    }

    public static float GetDifficultyHealthMultiplier()
    {
        switch (GameSettings.SelectedDifficulty)
        {
            case GameSettings.Difficulty.Kolay: return 0.85f; // Kolay mod
            case GameSettings.Difficulty.Zor:   return 1.80f; // Zor modda %80 daha fazla can (gerçek bir meydan okuma!)
            default:                            return 1.20f; // Normal modda %20 daha dayanıklı
        }
    }

    /// <summary>
    /// Uçağın canını dalga numarasına VE seçilen menü zorluğuna (Kolay/Normal/Zor) göre hesaplar.
    /// Düşman uçaklarının dayanıklılığı artırılmıştır.
    /// </summary>
    public float GetPlaneHealthForWave(int index)
    {
        if (CurrentMode == GameMode.Hardcore)
        {
            // Hardcore Modu: 1. Boss 245 HP, her gelen boss +85 HP daha fazla cana sahip!
            float hcBaseHp = 160f + (index * 85f);
            return Mathf.Round(hcBaseHp * GetDifficultyHealthMultiplier());
        }

        int eff = GetEffectiveWaveIndex(index);
        float baseHp = 0f;
        switch (eff)
        {
            case 5:  baseHp = 250f; break; // Boss 1 (önceki: 160)
            case 10: baseHp = 380f; break; // Boss 2 (önceki: 240)
            case 15: baseHp = 520f; break; // Boss 3 (önceki: 320)
            case 20: baseHp = 700f; break; // Final Boss (önceki: 420)
            default:
                if (eff > 20)
                    baseHp = 350f + ((eff - 20) * 25f); // Sonsuz mod artışı
                else
                    baseHp = 95f + (eff * 11f); // Standart uçaklar (önceki: 70 + eff*6)
                break;
        }

        return Mathf.Round(baseHp * GetDifficultyHealthMultiplier());
    }

    public static float GetPlaneHealth(int index)
    {
        if (Instance != null)
            return Instance.GetPlaneHealthForWave(index);

        return Mathf.Round((95f + (index * 11f)) * GetDifficultyHealthMultiplier());
    }

    public static string GetPlaneTitle(int index)
    {
        if (Instance != null)
            return Instance.GetCurrentPlaneTitle(index);

        return StoryCampaignManager.GetPlaneTitle(index);
    }

    /// <summary>
    /// Belirtilen dalgaya göre Uçak 2'nin canını, boyutunu, rengini ve yapay zekasını yapılandırır.
    /// </summary>
    public void ApplyEnemyWave(int waveIndex)
    {
        if (plane2Health == null) return;

        // Dalga başlangıcında zaman hızının kesinlikle normale (1.0x) dönmesini sağla
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        CombatComboManager.EnsureInstance().OnWaveStarted(waveIndex);

        int effIndex = GetEffectiveWaveIndex(waveIndex);
        bool isBoss = IsBossPlane(waveIndex);
        float hp = GetPlaneHealthForWave(waveIndex);

        // Canı ve bileşenleri sıfırla
        plane2Health.ResetHealth(hp);

        // Boss Rush Final Boss özel girişi (Wave 4)
        bool isBossRushFinal = (CurrentMode == GameMode.BossRush && waveIndex == 4);

        if (isBossRushFinal)
        {
            // Uçağı ekranın tepesinde gizli başlat
            plane2Health.transform.position = new Vector3(0f, 7.5f, 0f);
            var aiComp = plane2Health.GetComponent<AIController>();
            if (aiComp != null) aiComp.enabled = false;
            var psComp = plane2Health.GetComponent<PlaneShooter>();
            if (psComp != null) psComp.enabled = false;
        }
        else
        {
            // Uçağın konumunu üst merkeze al
            plane2Health.transform.position = new Vector3(0f, 3.5f, 0f);
        }

        // Görsel Boss Ölçeklendirmesi ve Renk Tonlaması
        Transform planeTransform = plane2Health.transform;
        SpriteRenderer sr = plane2Health.GetComponent<SpriteRenderer>();

        if (isBoss)
        {
            float scaleMultiplier = (CurrentMode == GameMode.Hardcore)
                ? Mathf.Min(1.68f, 1.28f + (waveIndex * 0.035f))
                : ((effIndex >= 20) ? 1.55f : (effIndex >= 15 ? 1.45f : (effIndex >= 10 ? 1.38f : 1.28f)));
            planeTransform.localScale = _plane2OriginalScale * scaleMultiplier;

            if (sr != null)
            {
                if (CurrentMode == GameMode.Hardcore)
                {
                    Color[] hcColors = new Color[]
                    {
                        new Color(1f, 0.38f, 0.32f),  // 1: Kan Kırmızısı
                        new Color(1f, 0.62f, 0.15f),  // 2: Lav Turuncusu
                        new Color(0.85f, 0.28f, 1.0f), // 3: Mor Kıyamet
                        new Color(0.18f, 0.95f, 0.85f), // 4: Plazma Şimşek
                        new Color(1f, 0.12f, 0.12f),  // 5: Saf Kızıl Şeytan
                        new Color(1f, 0.88f, 0.18f)   // 6+: İmparatorluk Altını
                    };
                    sr.color = hcColors[(waveIndex - 1) % hcColors.Length];
                }
                else
                {
                    if (effIndex <= 5) sr.color = new Color(1f, 0.85f, 0.45f); // Altın Sarısı / Taktik
                    else if (effIndex <= 10) sr.color = new Color(0.9f, 0.45f, 1f); // Mor Gölge Phantom
                    else if (effIndex <= 15) sr.color = new Color(1f, 0.35f, 0.2f); // Kor Alev Lav
                    else sr.color = new Color(1f, 0.8f, 0.15f); // İmparatorluk Altını Final Boss
                }
            }
        }
        else
        {
            planeTransform.localScale = _plane2OriginalScale;
            if (sr != null) sr.color = Color.white;
        }

        // Yapay zeka zorluğunu ayarla
        var ai = plane2Health.GetComponent<AIController>();
        if (ai != null)
        {
            ai.ConfigureWave(waveIndex, isBoss);
        }

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateHealthBars();

        // Boss dalgası ise bildirim veya tanıtım sinematiğini oynat
        if (isBoss)
        {
            if (CurrentMode == GameMode.Hardcore)
            {
                gameUI?.ShowWaveAlert($"[!] HARDCORE BOSS #{waveIndex} GİRDİ! [!]\n<color=#FF2222>ZIRH: {hp} HP • TEK CANIN VAR!</color>", new Color(1f, 0.2f, 0.2f), 2.2f);
                CameraShake.Shake(0.28f, 0.35f);
                AudioManager.Instance?.PlayExplosion(isBoss: true);
                AudioManager.Instance?.PlayWarningAlarm();
            }
            else if (isBossRushFinal)
            {
                if (StoryCampaignManager.EnsureInstance() != null)
                {
                    StoryCampaignManager.EnsureInstance().PlayBossIntro(effIndex, () =>
                    {
                        StartCoroutine(FinalBossArrivalRoutine());
                    });
                }
                else
                {
                    StartCoroutine(FinalBossArrivalRoutine());
                }
            }
            else
            {
                StoryCampaignManager.EnsureInstance()?.PlayBossIntro(effIndex, null);
            }
        }
    }

    /// <summary>
    /// Boss Rush modunun Final Boss'u için Red Zone alarmı, ekran sarsıntısı ve uçak giriş sekansı.
    /// </summary>
    private IEnumerator FinalBossArrivalRoutine()
    {
        if (plane2Health == null) yield break;

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.TriggerRedZoneWarning(2.8f);

        // Şiddetli ekran sarsıntısı ve nükleer alarm hissi
        CameraShake.Shake(0.32f, 2.8f);
        AudioManager.Instance?.PlayGunfire(true);

        Vector3 startPos = new Vector3(0f, 7.5f, 0f);
        Vector3 targetPos = new Vector3(0f, 3.5f, 0f);
        plane2Health.transform.position = startPos;

        float duration = 2.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Yumuşak iniş eğrisi (Cubic Ease-Out)
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            plane2Health.transform.position = Vector3.Lerp(startPos, targetPos, ease);

            // Motor egzozlarından kıvılcım ve alev izi
            if (elapsed % 0.12f < Time.deltaTime)
            {
                Vector3 engineL = plane2Health.transform.position + new Vector3(-0.4f, -0.9f, 0f);
                Vector3 engineR = plane2Health.transform.position + new Vector3(0.4f, -0.9f, 0f);
                VFXManager.SpawnHitSparks(engineL, new Color(1f, 0.45f, 0.1f));
                VFXManager.SpawnHitSparks(engineR, new Color(1f, 0.45f, 0.1f));
            }

            yield return null;
        }

        plane2Health.transform.position = targetPos;

        // Sahaya iniş şok dalgası ve patlama sesi
        CameraShake.Shake(0.45f, 0.45f);
        AudioManager.Instance?.PlayExplosion(isBoss: true);

        // AI ve atış mekanizmalarını savaşa hazır hale getir
        var ai = plane2Health.GetComponent<AIController>();
        if (ai != null) ai.enabled = true;
        var ps = plane2Health.GetComponent<PlaneShooter>();
        if (ps != null) ps.enabled = true;
    }

    public void OnPlaneDead(PlaneHealth deadPlane)
    {
        if (gameEnded) return;

        bool player1Died = (deadPlane == plane1Health);

        // 1. OYUNCU 1 (SEN) ÖLDÜĞÜNDE:
        if (player1Died)
        {
            if (CurrentMode == GameMode.TwoPlayer)
            {
                if (TwoPlayerDuelManager.Instance != null && TwoPlayerDuelManager.Instance.IsDuelActive)
                {
                    TwoPlayerDuelManager.Instance.OnPlaneDied(isPlayer1: true);
                    return;
                }

                gameEnded = true;
                if (gameOverUI != null)
                    gameOverUI.Show("OYUNCU 2 KAZANDI!");
                else
                    StartCoroutine(ReturnToMainMenuRoutine(1.8f));
                return;
            }

            if (CurrentMode == GameMode.BulletHell)
            {
                player1Lives--;
                var gameUI = FindObjectOfType<GameUI>();
                gameUI?.UpdateHealthBars();

                if (player1Lives > 0)
                {
                    gameUI?.ShowWaveAlert($"UÇAĞINIZ DÜŞTÜ!\n<color=#FF4081>KALAN CAN HAKKI: {player1Lives}</color>", new Color(1f, 0.4f, 0.2f), 1.8f);
                    StartCoroutine(RespawnPlayer1Routine(1.2f));
                }
                else
                {
                    gameEnded = true;
                    if (_bulletHellCoroutine != null) StopCoroutine(_bulletHellCoroutine);
                    CleanUpBulletHellPlane();

                    float bestTime = PlayerPrefs.GetFloat("AirBattle_BulletHellBestTime", 0f);
                    bool newRecord = bulletHellTime > bestTime;
                    if (newRecord)
                    {
                        PlayerPrefs.SetFloat("AirBattle_BulletHellBestTime", bulletHellTime);
                        PlayerPrefs.Save();
                    }
                    LeaderboardManager.EnsureInstance()?.SubmitScore(LeaderboardManager.BoardMode.BulletHell, (int)bulletHellTime, bulletHellTime);

                    int reward = (int)(bulletHellTime * 4f) + (newRecord ? 200 : 50);
                    HangarManager.EnsureInstance()?.AddGold(reward);
                    sessionGoldEarned += reward;

                    int m = (int)(bulletHellTime / 60);
                    int s = (int)(bulletHellTime % 60);
                    int bm = (int)(bestTime / 60);
                    int bs = (int)(bestTime % 60);

                    string defeatMsg = newRecord
                        ? $"<b><color=#2ECC71>YENİ HAYATTA KALMA REKORU!</color></b>\nSÜRE: {m:00}:{s:00}\nÖNCEKİ REKOR: {bm:00}:{bs:00}\n<color=#FFE082>+{reward} ALTIN KAZANILDI!</color>"
                        : $"HAYATTA KALMA SONLANDI!\nSÜRE: {m:00}:{s:00}\nEN İYİ REKOR: {bm:00}:{bs:00}\n<color=#FFE082>+{reward} ALTIN KAZANILDI!</color>";

                    if (gameOverUI != null)
                        gameOverUI.Show(AppendComboSummary(defeatMsg));
                    else
                        StartCoroutine(ReturnToMainMenuRoutine(2.8f));
                }
                return;
            }
            else if (CurrentMode == GameMode.TimeAttack)
            {
                player1Lives--;
                var gameUI = FindObjectOfType<GameUI>();
                gameUI?.UpdateHealthBars();

                if (player1Lives > 0)
                {
                    gameUI?.ShowWaveAlert($"UÇAĞINIZ DÜŞTÜ! KALAN CAN: {player1Lives}\n<color=#FFD54F>SÜRE DEVAM EDİYOR!</color>", new Color(1f, 0.4f, 0.2f), 1.5f);
                    StartCoroutine(RespawnPlayer1Routine(1.0f));
                }
                else
                {
                    EndTimeAttackGame(isTimeUp: false);
                }
                return;
            }

            // CAN HAKKI KONTROLÜ (Toplam 3 Can Hakkı)
            player1Lives--;

            var normalGameUI = FindObjectOfType<GameUI>();
            normalGameUI?.UpdateHealthBars();

            if (player1Lives > 0)
            {
                normalGameUI?.ShowWaveAlert($"UÇAĞINIZ DÜŞTÜ!\n<color=#F1C40F>KALAN CAN HAKKI: {player1Lives}</color>", new Color(1f, 0.4f, 0.2f), 2.0f);
                StartCoroutine(RespawnPlayer1Routine(1.2f));
            }
            else
            {
                // Can Hakkı tükendi: Oyun biter
                gameEnded = true;
                string defeatMsg = "";

                if (CurrentMode == GameMode.Endless)
                {
                    int bestWave = PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 1);
                    if (currentPlaneIndex > bestWave)
                    {
                        PlayerPrefs.SetInt("AirBattle_EndlessHighScore", currentPlaneIndex);
                        PlayerPrefs.Save();
                        defeatMsg = $"YENİ REKOR KIRILDI!\nSONSUZ MOD: DALGA {currentPlaneIndex}\nÖNCEKİ REKOR: {bestWave}";
                    }
                    else
                    {
                        defeatMsg = $"GÖREV BAŞARISIZ!\nSONSUZ MOD: DALGA {currentPlaneIndex}\nEN YÜKSEK REKOR: {bestWave}";
                    }
                    LeaderboardManager.EnsureInstance()?.SubmitScore(LeaderboardManager.BoardMode.Endless, currentPlaneIndex);
                }
                else if (CurrentMode == GameMode.BossRush)
                {
                    int bossScore = Mathf.Max(0, currentPlaneIndex - 1) * 100;
                    LeaderboardManager.EnsureInstance()?.SubmitScore(LeaderboardManager.BoardMode.BossRush, bossScore);
                    defeatMsg = $"BOSS RUSH BAŞARISIZ!\nELENEN BOSS: {currentPlaneIndex - 1} / 4";
                }
                else if (CurrentMode == GameMode.Hardcore)
                {
                    int defeatedBosses = Mathf.Max(0, currentPlaneIndex - 1);
                    int bestHardcore = PlayerPrefs.GetInt("AirBattle_HardcoreHighScore", 0);
                    bool newRecord = defeatedBosses > bestHardcore;
                    if (newRecord)
                    {
                        PlayerPrefs.SetInt("AirBattle_HardcoreHighScore", defeatedBosses);
                        PlayerPrefs.Save();
                    }
                    LeaderboardManager.EnsureInstance()?.SubmitScore(LeaderboardManager.BoardMode.Hardcore, defeatedBosses);

                    int rewardGold = defeatedBosses * 60;
                    HangarManager.EnsureInstance()?.AddGold(rewardGold);
                    sessionGoldEarned += rewardGold;

                    defeatMsg = newRecord
                        ? $"[!] <b><color=#FF2222>YENİ HARDCORE REKORU!</color></b> [!]\n<color=#FFD54F>İMHA EDİLEN BOSS: {defeatedBosses}</color>\nÖNCEKİ REKOR: {bestHardcore}\n<color=#2ECC71>+{rewardGold} ALTIN KAZANILDI!</color>"
                        : $"[!] <b>HARDCORE GÖREVİ SONA ERDİ</b> [!]\nİMHA EDİLEN BOSS: {defeatedBosses}\nEN İYİ REKOR: {bestHardcore}\n<color=#FFE082>+{rewardGold} ALTIN KAZANILDI!</color>";
                }
                else
                {
                    defeatMsg = $"GÖREV BAŞARISIZ!\nTÜM CAN HAKLARI TÜKENDİ!\nİLERLEME: UÇAK {currentPlaneIndex} / {MaxPlanes}";
                }

                if (gameOverUI != null)
                    gameOverUI.Show(AppendComboSummary(defeatMsg));
                else
                    StartCoroutine(ReturnToMainMenuRoutine(2.0f));
            }
            return;
        }

        // 2. DÜŞMAN (UÇAK 2) ÖLDÜĞÜNDE:
        if (CurrentMode == GameMode.TwoPlayer)
        {
            if (TwoPlayerDuelManager.Instance != null && TwoPlayerDuelManager.Instance.IsDuelActive)
            {
                TwoPlayerDuelManager.Instance.OnPlaneDied(isPlayer1: false);
                return;
            }

            gameEnded = true;
            if (gameOverUI != null)
                gameOverUI.Show("OYUNCU 1 KAZANDI!");
            else
                StartCoroutine(ReturnToMainMenuRoutine(1.8f));
        }
        else
        {
            if (CurrentMode == GameMode.BulletHell)
            {
                gameEnded = true;
                if (_bulletHellCoroutine != null) StopCoroutine(_bulletHellCoroutine);
                CleanUpBulletHellPlane();

                int reward = 2500;
                HangarManager.EnsureInstance()?.AddGold(reward);
                sessionGoldEarned += reward;
                PlayerProfileManager.EnsureInstance()?.RecordKill(isBoss: true);

                float bestTime = PlayerPrefs.GetFloat("AirBattle_BulletHellBestTime", 0f);
                if (bulletHellTime > bestTime)
                {
                    PlayerPrefs.SetFloat("AirBattle_BulletHellBestTime", bulletHellTime);
                    PlayerPrefs.Save();
                }
                LeaderboardManager.EnsureInstance()?.SubmitScore(LeaderboardManager.BoardMode.BulletHell, (int)bulletHellTime, bulletHellTime);

                int m = (int)(bulletHellTime / 60f);
                int s = (int)(bulletHellTime % 60f);
                if (gameOverUI != null)
                    gameOverUI.Show(AppendComboSummary($"İNANILMAZ ZAFER!\nCEHENNEM KALESİ İMHA EDİLDİ!\nSÜRE: {m:00}:{s:00}\n<color=#FFE082>+{reward} ALTIN KAZANILDI!</color>"));

                AudioManager.Instance?.PlayRewardFanfare();
                StartCoroutine(ReturnToMainMenuRoutine(3.5f));
                return;
            }
            else if (CurrentMode == GameMode.TimeAttack)
            {
                timeAttackKills++;
                bool isBoss = (timeAttackKills % 5 == 0);
                PlayerCombatManeuvers.Instance?.AddUltiEnergy(isBoss ? 20f : 10f);
                float bonusSec = isBoss ? 8.0f : 4.0f;
                timeAttackRemaining += bonusSec;

                CombatComboManager combo = CombatComboManager.EnsureInstance();
                combo.AddCombo(isBoss ? 5 : 3, deadPlane != null ? deadPlane.transform.position : default);

                int baseKillReward = 15 + (timeAttackKills * 2) + (isBoss ? 25 : 0);
                int killReward = combo.ApplyGoldMultiplier(baseKillReward, out int comboBonus);
                HangarManager.EnsureInstance()?.AddGold(killReward);
                sessionGoldEarned += killReward;

                var flawless = combo.EvaluateFlawlessWaveBonus(timeAttackKills, isBoss);
                if (flawless.isFlawless)
                    AudioManager.Instance?.PlayRewardFanfare();

                PlayerProfileManager.EnsureInstance()?.RecordKill(isBoss: isBoss, isElite: isBoss);
                plane1Health?.Heal(25f);

                var gameUI = FindObjectOfType<GameUI>();
                string multTag = comboBonus > 0 ? $" <color=#FFD54F>[x{combo.GetMultiplier():0.0}]</color>" : "";
                string flawlessTag = flawless.isFlawless
                    ? $"\n<size=75%><color=#00FFAA>HASARSIZ DALGA! (+{flawless.goldBonus}G • +{flawless.ultiBonus:0}% ULTİ)</color></size>"
                    : "";
                string bonusMsg = isBoss
                    ? $"[BOSS] İMHA! (+{bonusSec:0} SN!) (+{killReward}G{multTag})\n<color=#2ECC71>TOPLAM İMHA: {timeAttackKills}</color>{flawlessTag}"
                    : $"[+] UÇAK İMHA! (+{bonusSec:0} SN!) (+{killReward}G{multTag})\n<color=#00E5FF>TOPLAM İMHA: {timeAttackKills}</color>{flawlessTag}";
                gameUI?.ShowWaveAlert(bonusMsg, isBoss ? new Color(1f, 0.85f, 0.2f) : new Color(0.2f, 0.95f, 0.4f), 1.35f);

                currentPlaneIndex = timeAttackKills + 1;
                StartCoroutine(SpawnNextEnemyRoutine(0.35f));
                return;
            }

            int totalPlanes = GetTotalPlanesForMode();

            // BOSS RUSH VE SOLO MOD ZAFER KONTROLÜ
            if (CurrentMode == GameMode.BossRush && currentPlaneIndex >= 4)
            {
                CombatComboManager combo = CombatComboManager.EnsureInstance();
                combo.AddCombo(5, deadPlane != null ? deadPlane.transform.position : default);
                combo.EvaluateFlawlessWaveBonus(currentPlaneIndex, isBoss: true);

                // BOSS RUSH TAMAMLANDI: Ağır çekim ve zincirleme devasa patlama sekansını başlat
                StartCoroutine(FinalBossDefeatSlowMoRoutine(deadPlane));
            }
            else if (CurrentMode == GameMode.SoloVsAI && currentPlaneIndex >= MaxPlanes)
            {
                // 20. UÇAK (FİNAL BOSS) DA DÜŞTÜ! TAM ZAFER!
                gameEnded = true;
                CombatComboManager combo = CombatComboManager.EnsureInstance();
                combo.AddCombo(5, deadPlane != null ? deadPlane.transform.position : default);
                var flawless = combo.EvaluateFlawlessWaveBonus(MaxPlanes, isBoss: true);

                int campaignReward = combo.ApplyGoldMultiplier(1000, out _);
                HangarManager.EnsureInstance()?.AddGold(campaignReward);
                sessionGoldEarned += campaignReward;

                PlayerProfileManager.EnsureInstance()?.RecordKill(isBoss: true);
                PlayerProfileManager.EnsureInstance()?.RecordWaveCleared(20);

                if (flawless.isFlawless)
                {
                    var gameUI = FindObjectOfType<GameUI>();
                    gameUI?.ShowWaveAlert($"FİNAL BOSS HASARSIZ GEÇİLDİ! (+{flawless.goldBonus}G)", new Color(0f, 1f, 0.65f), 2.0f);
                }

                StoryCampaignManager.AdvanceToNextCampaign();
                StoryCampaignManager.EnsureInstance()?.PlayCampaignEnding(() => { });
            }
            else
            {
                int justDefeated = currentPlaneIndex;
                bool wasBoss = IsBossPlane(justDefeated);
                CombatComboManager combo = CombatComboManager.EnsureInstance();
                Vector3 deadPos = deadPlane != null ? deadPlane.transform.position : default;

                if (wasBoss)
                {
                    PlayerCombatManeuvers.Instance?.AddUltiEnergy(25f);
                    combo.AddCombo(5, deadPos);

                    int baseBossReward = (CurrentMode == GameMode.BossRush) ? 250 : (100 + justDefeated * 10);
                    int bossReward = combo.ApplyGoldMultiplier(baseBossReward, out int comboBonus);
                    HangarManager.EnsureInstance()?.AddGold(bossReward);
                    sessionGoldEarned += bossReward;

                    var flawless = combo.EvaluateFlawlessWaveBonus(justDefeated, isBoss: true);
                    if (flawless.isFlawless)
                        AudioManager.Instance?.PlayRewardFanfare();

                    PlayerProfileManager.EnsureInstance()?.RecordKill(isBoss: true);
                    PlayerProfileManager.EnsureInstance()?.RecordWaveCleared(justDefeated);

                    var gameUI = FindObjectOfType<GameUI>();
                    string multTag = comboBonus > 0 ? $" [x{combo.GetMultiplier():0.0}]" : "";
                    string flawlessTag = flawless.isFlawless
                        ? $"\n<size=72%><color=#00FFAA>HASARSIZ DALGA (x{flawless.streak}): +{flawless.goldBonus}G • +{flawless.ultiBonus:0}% ULTİ</color></size>"
                        : "";

                    if (CurrentMode == GameMode.Hardcore)
                    {
                        plane1Health?.Heal(50f); // 200 HP can barı için gövde tamiri ödülü
                        gameUI?.ShowWaveAlert($"[BOSS #{justDefeated} İMHA EDİLDİ!] (+{bossReward}G{multTag})\n<color=#FF3333>+50 HP TAMİR EDİLDİ • HANGAR AÇILIYOR</color>{flawlessTag}", new Color(1f, 0.25f, 0.25f), 2.4f);
                    }
                    else if (CurrentMode == GameMode.Endless && (justDefeated % 20 == 0 || (justDefeated / 5) % 20 == 0))
                    {
                        player1Lives++;
                        AudioManager.Instance?.PlayRewardFanfare();
                        gameUI?.ShowWaveAlert($"[+] 20. BOSS İMHA EDİLDİ! (+{bossReward}G{multTag})\n<color=#2ECC71><b>OTOMATİK +1 YEDEK CAN EKLENDİ!</b></color>\n(TOPLAM CAN HAKKI: {player1Lives}){flawlessTag}", new Color(0.2f, 0.95f, 0.4f), 3.0f);
                        gameUI?.UpdateHealthBars();
                    }
                    else
                    {
                        gameUI?.ShowWaveAlert($"BOSS DÜŞÜRÜLDÜ! (+{bossReward}G{multTag})\n<color=#F1C40F>HANGAR GÜÇLENDİRMESİ AÇILIYOR...</color>{flawlessTag}", new Color(1f, 0.85f, 0.2f), 2.2f);
                    }

                    StartCoroutine(OpenUpgradeHangarRoutine(justDefeated));
                }
                else
                {
                    bool isElite = (justDefeated % 5 == 4);
                    PlayerCombatManeuvers.Instance?.AddUltiEnergy(isElite ? 15f : 10f);
                    combo.AddCombo(isElite ? 4 : 3, deadPos);

                    int baseKillReward = 15 + (justDefeated * 2) + (isElite ? 15 : 0);
                    int killReward = combo.ApplyGoldMultiplier(baseKillReward, out int comboBonus);
                    HangarManager.EnsureInstance()?.AddGold(killReward);
                    sessionGoldEarned += killReward;

                    var flawless = combo.EvaluateFlawlessWaveBonus(justDefeated, isBoss: false);
                    if (flawless.isFlawless)
                        AudioManager.Instance?.PlayRewardFanfare();

                    PlayerProfileManager.EnsureInstance()?.RecordKill(isBoss: false, isElite: isElite);
                    PlayerProfileManager.EnsureInstance()?.RecordWaveCleared(justDefeated);

                    plane1Health?.Heal(35f);

                    var gameUI = FindObjectOfType<GameUI>();
                    currentPlaneIndex++;
                    bool nextIsBoss = IsBossPlane(currentPlaneIndex);

                    string multTag = comboBonus > 0 ? $" [x{combo.GetMultiplier():0.0}]" : "";
                    string flawlessLine = flawless.isFlawless
                        ? $"\n<size=68%><color=#00FFAA>HASARSIZ DALGA (x{flawless.streak}): +{flawless.goldBonus}G • +{flawless.ultiBonus:0}% ULTİ</color></size>"
                        : "";

                    if (nextIsBoss)
                    {
                        gameUI?.ShowWaveAlert($"DİKKAT! {GetCurrentPlaneTitle(currentPlaneIndex)} YAKLAŞIYOR!\n<size=70%><color=#FFE082>ÖNCEKİ ÖDÜL: +{killReward}G{multTag}</color></size>{flawlessLine}", new Color(1f, 0.3f, 0.2f), 2.2f);
                    }
                    else
                    {
                        string waveLabel = (CurrentMode == GameMode.Endless) ? $"DALGA {justDefeated}" : $"UÇAK [{justDefeated}/{totalPlanes}]";
                        gameUI?.ShowWaveAlert($"{waveLabel} İMHA EDİLDİ! (+{killReward}G{multTag})\n<size=70%><color=#2ECC71>+35 CAN TAMİR EDİLDİ</color></size>{flawlessLine}", new Color(0.2f, 0.9f, 1f), 1.8f);
                    }

                    StartCoroutine(SpawnNextEnemyRoutine(1.3f));
                }
            }
        }
    }

    private IEnumerator OpenUpgradeHangarRoutine(int justDefeated)
    {
        EnemyFormationManager.Instance?.ClearAllSwarmEnemies();
        yield return new WaitForSeconds(1.0f);

        UpgradeSelectionUI upgradeUI = UpgradeSelectionUI.EnsureInstance();
        upgradeUI.ShowUpgradeSelection(() =>
        {
            // Oyuncu hangar seçimini yaptıktan sonra:
            currentPlaneIndex++;
            bool nextIsBoss = IsBossPlane(currentPlaneIndex);

            var gameUI = FindObjectOfType<GameUI>();
            if (nextIsBoss)
            {
                gameUI?.ShowWaveAlert($"DİKKAT! {GetPlaneTitle(currentPlaneIndex)} YAKLAŞIYOR!", new Color(1f, 0.3f, 0.2f), 2.2f);
            }
            else
            {
                gameUI?.ShowWaveAlert($"GÜÇLENDİRİLMİŞ UÇAK SAVAŞA DÖNDÜ!\n<color=#00E5FF>HEDEF: UÇAK {currentPlaneIndex}/{MaxPlanes}</color>", new Color(0.2f, 0.9f, 1f), 2.0f);
            }

            StartCoroutine(SpawnNextEnemyRoutine(1.2f));
        });
    }

    private IEnumerator SpawnNextEnemyRoutine(float delay)
    {
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        yield return new WaitForSeconds(delay);

        if (!gameEnded)
        {
            ApplyEnemyWave(currentPlaneIndex);
        }
    }

    private IEnumerator RespawnPlayer1Routine(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!gameEnded && plane1Health != null)
        {
            // Oyuncuyu başlangıç pozisyonuna yerleştir
            plane1Health.transform.position = new Vector3(0f, -3.5f, 0f);

            // Mevcut maksimum gövde canını (Hangar/parça bonusu dahil) koruyarak canlandır ve 1.8 saniye dokunulmazlık ver
            float restoreHp = Mathf.Max(100f, plane1Health.maxHealth);
            plane1Health.ResetHealth(restoreHp);
            plane1Health.TriggerInvulnerability(1.8f);

            // Destek dronelarını yeniden kanatlara yerleştir
            DroneManager.EnsureInstance()?.SpawnDronesForPlayer(plane1Health.transform);

            var gameUI = FindObjectOfType<GameUI>();
            gameUI?.UpdateHealthBars();
        }
    }

    private IEnumerator ReturnToMainMenuRoutine(float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            elapsed += Time.unscaledDeltaTime;
            // Ekrana dokunursa beklemeden ana ekrana geçsin
            if (elapsed > 0.4f && (Input.anyKeyDown || Input.touchCount > 0))
            {
                break;
            }
            yield return null;
        }

        EnemyFormationManager.Instance?.StopDirector();
        AirspaceHazardDirector.Instance?.StopDirector();
        DroneManager.EnsureInstance()?.ClearDrones();
        PlayerProfileUI.Instance?.SetHUDBadgeActive(false);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Boss Rush modunun Final Boss'u düştüğünde sinematik ağır çekim (Slow-Mo) ve zincirleme gövde patlamaları sekansı.
    /// </summary>
    private IEnumerator FinalBossDefeatSlowMoRoutine(PlaneHealth bossPlane)
    {
        gameEnded = true;

        // 1. Ağır Çekim (Slow-Motion) Başlat: Zamanı 0.18x hızına çek
        Time.timeScale = 0.18f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        Vector3 bossPos = (bossPlane != null) ? bossPlane.transform.position : new Vector3(0f, 3.5f, 0f);
        SpriteRenderer sr = (bossPlane != null) ? bossPlane.GetComponent<SpriteRenderer>() : null;
        Color originalColor = (sr != null) ? sr.color : Color.white;

        // 2. İlk Şiddetli Sarsıntı ve Patlama Sesi
        CameraShake.Shake(0.50f, 3.0f);
        AudioManager.Instance?.PlayExplosion(isBoss: true);

        // 3. Gövde Üzerinde Sırayla Patlayan 8 Kademeli Zincirleme Alev & Şok Dalgası Noktası
        Vector2[] offsets = new Vector2[]
        {
            new Vector2(-0.75f, -0.3f), // Sol Kanat
            new Vector2(0.75f, -0.3f),  // Sağ Kanat
            new Vector2(0.0f, 0.8f),    // Kokpit / Burun
            new Vector2(-0.4f, -0.9f),  // Sol Motor
            new Vector2(0.4f, -0.9f),   // Sağ Motor
            new Vector2(-0.35f, 0.2f),  // Sol Gövde
            new Vector2(0.35f, 0.2f),   // Sağ Gövde
            new Vector2(0.0f, -0.2f)    // Gövde Merkezi
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 point = bossPos + (Vector3)offsets[i];
            VFXManager.SpawnMegaExplosion(point, scaleMultiplier: 1.8f);
            AudioManager.Instance?.PlayExplosion(isBoss: true);

            // Uçak alev alırken yanıp sönme efekti
            if (sr != null)
            {
                sr.color = (i % 2 == 0) ? new Color(1f, 0.35f, 0.1f) : new Color(1f, 0.95f, 0.85f);
            }

            yield return new WaitForSecondsRealtime(0.20f);
        }

        // 4. Nihai Devasa Patlama ve Sarsıntı Zirvesi
        VFXManager.SpawnMegaExplosion(bossPos, scaleMultiplier: 3.5f);
        CameraShake.Shake(0.75f, 1.2f);
        AudioManager.Instance?.PlayExplosion(isBoss: true);

        // Uçak enkaza dönüşüp alevlerin ardında kaybolur
        if (sr != null)
        {
            sr.enabled = false;
            sr.color = originalColor;
        }

        // Ağır çekimin sinematik etkisini hissettirmek için kısa bir an bekle
        yield return new WaitForSecondsRealtime(0.70f);

        // 5. Zamanı Normale Döndür
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        // 6. Zafer & Ödül
        int rushReward = 1500;
        HangarManager.EnsureInstance()?.AddGold(rushReward);
        sessionGoldEarned += rushReward;

        PlayerProfileManager.EnsureInstance()?.RecordKill(isBoss: true);
        PlayerProfileManager.EnsureInstance()?.RecordWaveCleared(4);
        LeaderboardManager.EnsureInstance()?.SubmitScore(LeaderboardManager.BoardMode.BossRush, 400);

        AudioManager.Instance?.PlayRewardFanfare();

        if (gameOverUI != null)
            gameOverUI.Show(AppendComboSummary($"TEBRİKLER AS PİLOT!\nBOSS RUSH TAMAMLANDI!\n+{rushReward} ALTIN KAZANILDI!"));
        else
            StartCoroutine(ReturnToMainMenuRoutine(3.0f));
    }

    // ==========================================
    // MERMİ CEHENNEMİ (BULLET HELL) YÖNETİCİSİ
    // ==========================================
    private void StartBulletHellMode()
    {
        player1Lives = MaxPlayerLives;
        bulletHellTime = 0f;
        bulletHellTier = 1;
        currentPlaneIndex = 1;

        if (plane1Health != null)
        {
            plane1Health.ResetHealth(100f);
            plane1Health.transform.position = new Vector3(0f, -3.5f, 0f);
            plane1Health.gameObject.SetActive(true);
        }

        if (plane2Health != null)
        {
            plane2Health.gameObject.SetActive(true);
            plane2Health.ResetHealth(8000f);
            plane2Health.transform.position = new Vector3(0f, 3.4f, 0f);
            plane2Health.transform.localScale = _plane2OriginalScale * 1.65f;

            SpriteRenderer sr = plane2Health.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.9f, 0.2f, 0.5f);

            var ai = plane2Health.GetComponent<AIController>();
            if (ai != null) ai.enabled = false; // BulletHellDirector mermileri yönetir

            var pc = plane2Health.GetComponent<PlaneController>();
            if (pc != null) pc.enabled = false;

            var rb = plane2Health.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert("[!] [MERMİ CEHENNEMİ BAŞLADI!] [!]\n<color=#FF4081>NE KADAR DAYANABİLİRSİN? HAYATTA KAL!</color>", new Color(1f, 0.2f, 0.5f), 2.8f);

        if (_bulletHellCoroutine != null) StopCoroutine(_bulletHellCoroutine);
        _bulletHellCoroutine = StartCoroutine(BulletHellDirectorRoutine());
    }

    private void CleanUpBulletHellPlane()
    {
        if (plane2Health != null)
        {
            plane2Health.transform.localScale = _plane2OriginalScale;
            SpriteRenderer sr = plane2Health.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.white;

            var pc = plane2Health.GetComponent<PlaneController>();
            if (pc != null) pc.enabled = true;

            var rb = plane2Health.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private IEnumerator BulletHellDirectorRoutine()
    {
        PlaneShooter shooter = (plane2Health != null) ? plane2Health.GetComponent<PlaneShooter>() : null;
        float spiralAngle = 0f;
        float radialTimer = 0f;
        float fanTimer = 0f;
        float spiralTimer = 0f;
        float goldTimer = 0f;

        while (!gameEnded && plane2Health != null)
        {
            float dt = Time.deltaTime;
            bulletHellTime += dt;
            radialTimer += dt;
            fanTimer += dt;
            spiralTimer += dt;
            goldTimer += dt;

            int prevTier = bulletHellTier;
            if (bulletHellTime < 18f) bulletHellTier = 1;
            else if (bulletHellTime < 40f) bulletHellTier = 2;
            else if (bulletHellTime < 70f) bulletHellTier = 3;
            else bulletHellTier = 4;

            if (bulletHellTier > prevTier)
            {
                var ui = FindObjectOfType<GameUI>();
                ui?.ShowWaveAlert($"[!] <b>[KADEME {bulletHellTier} CEHENNEM!]</b> [!]\n<color=#FF1744>MERMİ YOĞUNLUĞU VE SALVO ARTTI!</color>", new Color(1f, 0.1f, 0.3f), 2.0f);
                CameraShake.Shake(0.35f, 1.2f);
                AudioManager.Instance?.PlayExplosion(isBoss: true);
                AudioManager.Instance?.PlayWarningAlarm();
            }

            // Uçak 2'yi sinüs dalgası ile sağa sola akıcı ve sarsıntısız süzdürelim (60 FPS)
            float moveX = Mathf.Sin(bulletHellTime * 1.25f) * 2.2f;
            plane2Health.transform.position = new Vector3(moveX, 3.4f, 0f);

            Vector3 spawnPos = plane2Health.transform.position;

            // 1. Kademe: Dairesel Mermi Çemberi (Radial Burst)
            float radialInterval = (bulletHellTier >= 4) ? 1.35f : ((bulletHellTier >= 3) ? 1.7f : 2.1f);
            if (radialTimer >= radialInterval && shooter != null)
            {
                radialTimer = 0f;
                int bulletCount = (bulletHellTier >= 4) ? 14 : ((bulletHellTier >= 2) ? 10 : 8);
                float step = 360f / bulletCount;
                float startRot = Random.Range(0f, 30f);
                Color ringColor = (bulletHellTier >= 3) ? new Color(1f, 0.25f, 0.1f) : new Color(1f, 0.35f, 0.85f);
                float bulletSpeed = (bulletHellTier >= 3) ? 7.2f : 5.8f;

                for (int i = 0; i < bulletCount; i++)
                {
                    float angle = (startRot + i * step) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    shooter.SpawnCustomBullet(spawnPos, dir, bulletSpeed, 12f, ringColor);
                }

                AudioManager.Instance?.PlayGunfire(true);
            }

            // 2. ve 3. Kademe: Dönen Spiral Mermiler
            float spiralInterval = (bulletHellTier >= 4) ? 0.08f : 0.12f;
            if (bulletHellTier >= 2 && shooter != null && spiralTimer >= spiralInterval)
            {
                spiralTimer = 0f;
                float spiralStep = (bulletHellTier >= 4) ? 22f : 25f;
                spiralAngle += spiralStep;

                float rad1 = spiralAngle * Mathf.Deg2Rad;
                Vector2 dir1 = new Vector2(Mathf.Cos(rad1), Mathf.Sin(rad1));
                shooter.SpawnCustomBullet(spawnPos, dir1, 6.5f, 10f, new Color(0.75f, 0.2f, 1f));

                // Kademe 3+: Ters Yöne Dönen İkinci Spiral
                if (bulletHellTier >= 3)
                {
                    float rad2 = (-spiralAngle * 1.15f) * Mathf.Deg2Rad;
                    Vector2 dir2 = new Vector2(Mathf.Cos(rad2), Mathf.Sin(rad2));
                    shooter.SpawnCustomBullet(spawnPos, dir2, 6.0f, 10f, new Color(0.2f, 0.85f, 1f));
                }
            }

            // Hedefli Yelpaze Atışı (Targeted Fan Burst)
            float fanInterval = (bulletHellTier >= 3) ? 2.2f : 3.0f;
            if (bulletHellTier >= 2 && fanTimer >= fanInterval && shooter != null && plane1Health != null)
            {
                fanTimer = 0f;
                Vector2 toPlayer = (plane1Health.transform.position - spawnPos).normalized;
                float baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
                int fanCount = (bulletHellTier >= 3) ? 5 : 3;
                float fanSpread = 18f;

                for (int i = 0; i < fanCount; i++)
                {
                    float offset = (i - (fanCount - 1) * 0.5f) * fanSpread;
                    float finalRad = (baseAngle + offset) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(finalRad), Mathf.Sin(finalRad));
                    shooter.SpawnCustomBullet(spawnPos, dir, 7.5f, 12f, new Color(1f, 0.9f, 0.2f));
                }
            }

            // 5 Saniyede Bir Hayatta Kalma Altın Ödülü
            if (goldTimer >= 5.0f)
            {
                goldTimer = 0f;
                int goldAmount = 15 + (bulletHellTier * 5);
                HangarManager.EnsureInstance()?.AddGold(goldAmount);
                sessionGoldEarned += goldAmount;
            }

            yield return null;
        }
    }

    // ==========================================
    // ZAMAN TAARRUZU (TIME ATTACK) YÖNETİCİSİ
    // ==========================================
    private void StartTimeAttackMode()
    {
        player1Lives = MaxPlayerLives;
        timeAttackRemaining = 60f;
        timeAttackKills = 0;
        currentPlaneIndex = 1;

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert("[ZAMAN] [ZAMAN TAARRUZU BAŞLADI!]\n<color=#FFD54F>60 SANİYEN VAR! DÜŞMANLARI HIZLA AVLA!</color>", new Color(1f, 0.85f, 0.2f), 2.5f);

        ApplyEnemyWave(1);

        if (_timeAttackCoroutine != null) StopCoroutine(_timeAttackCoroutine);
        _timeAttackCoroutine = StartCoroutine(TimeAttackCountdownRoutine());
    }

    private IEnumerator TimeAttackCountdownRoutine()
    {
        while (!gameEnded && timeAttackRemaining > 0f)
        {
            timeAttackRemaining -= Time.deltaTime;

            if (timeAttackRemaining <= 0f)
            {
                timeAttackRemaining = 0f;
                EndTimeAttackGame(isTimeUp: true);
                yield break;
            }

            yield return null;
        }
    }

    private void EndTimeAttackGame(bool isTimeUp)
    {
        if (gameEnded) return;
        gameEnded = true;

        if (_timeAttackCoroutine != null) StopCoroutine(_timeAttackCoroutine);

        int bestKills = PlayerPrefs.GetInt("AirBattle_TimeAttackHighScore", 0);
        bool newRecord = timeAttackKills > bestKills;
        if (newRecord)
        {
            PlayerPrefs.SetInt("AirBattle_TimeAttackHighScore", timeAttackKills);
            PlayerPrefs.Save();
        }
        LeaderboardManager.EnsureInstance()?.SubmitScore(LeaderboardManager.BoardMode.TimeAttack, timeAttackKills);

        int reward = (timeAttackKills * 25) + (newRecord ? 250 : 50);
        HangarManager.EnsureInstance()?.AddGold(reward);
        sessionGoldEarned += reward;

        PlayerProfileManager.EnsureInstance()?.RecordWaveCleared(timeAttackKills);
        AudioManager.Instance?.PlayRewardFanfare();

        string title = isTimeUp ? "SÜRE DOLDU! HAREKAT BİTTİ!" : "GÖREV BAŞARISIZ! UÇAK DÜŞTÜ!";
        string recordMsg = newRecord
            ? $"<b><color=#2ECC71>YENİ REKOR!</color></b>\nTOPLAM İMHA: {timeAttackKills} UÇAK"
            : $"TOPLAM İMHA: {timeAttackKills} UÇAK\nEN YÜKSEK REKOR: {bestKills}";

        if (gameOverUI != null)
            gameOverUI.Show(AppendComboSummary($"{title}\n{recordMsg}\n<color=#FFE082>+{reward} ALTIN KAZANILDI!</color>"));
        else
            StartCoroutine(ReturnToMainMenuRoutine(3.0f));
    }

    private string AppendComboSummary(string baseMessage)
    {
        if (CombatComboManager.Instance == null || CurrentMode == GameMode.TwoPlayer)
            return baseMessage;

        return baseMessage + "\n" + CombatComboManager.Instance.BuildSessionSummaryLine();
    }

    /// <summary>
    /// Oyuna girildiğinde seçilen oyun modunu tanıtan brifing banner'ını ekranda gösterir.
    /// </summary>
    public void ShowActiveModeIntroBanner()
    {
        var gameUI = FindObjectOfType<GameUI>();
        if (gameUI == null) return;

        string title;
        string subtitle;
        string desc;
        Color color;

        switch (CurrentMode)
        {
            case GameMode.SoloVsAI:
                if (StoryCampaignManager.CurrentCampaign == StoryCampaignManager.CampaignType.OperationBlackDawn)
                {
                    title = "BÖLÜM 1: OPERASYON KARA ŞAFAK";
                    subtitle = "VATAN SAVUNMASI & HAVA İNTİKAMI • 20 HAVA TAARRUZU";
                    desc = "Düşman işgal filosu sınır hattını yardı! Hava üssünü savun ve 4 ana generali düşür!";
                    color = new Color(0.2f, 0.9f, 0.6f);
                }
                else if (StoryCampaignManager.CurrentCampaign == StoryCampaignManager.CampaignType.ProjectChimera)
                {
                    title = "BÖLÜM 2: PROJE KİMERAS";
                    subtitle = "YAPAY ZEKA İSYANI & SİBER SAVAŞ • 20 HAVA TAARRUZU";
                    desc = "Kontrolden çıkan otonom savaş filosu gökyüzünü ele geçirdi! Kimera çekirdeğini imha et!";
                    color = new Color(0.2f, 0.75f, 1f);
                }
                else
                {
                    title = "BÖLÜM 3: GÖKYÜZÜ PARALI ASKERİ";
                    subtitle = "ACE OF ACES & SERBEST KONTRATLAR • 20 HAVA TAARRUZU";
                    desc = "En tehlikeli hava korsanları ve paralı pilot filoları karşına çıkıyor! Gökyüzünün ası ol!";
                    color = new Color(1f, 0.65f, 0.2f);
                }
                break;

            case GameMode.Endless:
                int bestWave = PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 1);
                title = "SONSUZ İT DALAŞI";
                subtitle = $"SINIRSIZ HAVA AKINI • EN İYİ REKOR: DALGA {bestWave}";
                desc = "Düşmanlar aralıksız ve her dalgada güçlenerek geliyor! Her 20. Boss'ta +1 Can Hakkı kazanırsın!";
                color = new Color(0.2f, 0.95f, 0.4f);
                break;

            case GameMode.BossRush:
                title = "BOSS RUSH (DEVLER LİGİ)";
                subtitle = "4 AMANSIZ SAVAŞ MAKİNESİ ARKA ARKAYA";
                desc = "Sıradan uçaklar yok, sadece 4 dev Boss! Finalde Kırmızı Bölge Alarmı ve Dev Boss seni bekliyor!";
                color = new Color(1f, 0.25f, 0.25f);
                break;

            case GameMode.BulletHell:
                float bestTime = PlayerPrefs.GetFloat("AirBattle_BulletHellBestTime", 0f);
                int bm = (int)(bestTime / 60);
                int bs = (int)(bestTime % 60);
                title = "MERMİ CEHENNEMİ (BULLET HELL)";
                subtitle = $"HİPER YOĞUN SALVO KALKAN SAVUNMASI • EN İYİ REKOR: {bm:00}:{bs:00}";
                desc = "Binlerce merminin arasından sıyrıl, manevra kabiliyetini göster ve Cehennem Kalesini devir!";
                color = new Color(1f, 0.25f, 0.8f);
                break;

            case GameMode.TimeAttack:
                int bestKills = PlayerPrefs.GetInt("AirBattle_TimeAttackHighScore", 0);
                title = "ZAMAN TAARRUZU (TIME ATTACK)";
                subtitle = $"60 SANİYELİK YILDIRIM OPERASYONU • EN İYİ REKOR: {bestKills} İMHA";
                desc = "Süre daralıyor! 60 saniye içinde indirebildiğin kadar uçak avla, her imhada ek süre kazan!";
                color = new Color(1f, 0.85f, 0.2f);
                break;

            case GameMode.Hardcore:
                int bestHc = PlayerPrefs.GetInt("AirBattle_HardcoreHighScore", 0);
                title = "[!] HARDCORE MODU [!]";
                subtitle = $"TEK CAN • 200 HP GÖVDE • EN İYİ REKOR: {bestHc} BOSS";
                desc = "Her gelen düşman bir öncekinden daha zırhlı ve güçlü bir Boss! İkinci bir şansın yok, tek canla savaş!";
                color = new Color(1f, 0.18f, 0.18f);
                break;

            case GameMode.TwoPlayer:
                title = "2 OYUNCULU İT DALAŞI";
                subtitle = "AYNI CİHAZDA ÇİFT KONTROLLÜ HAVA DÜELLOSU";
                desc = "Mavi Kanat (P1) vs Kırmızı Avcı (P2)! Gökyüzünün tek ve mutlak hâkimi kim olacak?";
                color = new Color(0.35f, 0.75f, 1f);
                break;

            default:
                title = "HAVA MUHAREBESİ";
                subtitle = "GÖREV BAŞLADI";
                desc = "Düşman unsurlarını hava sahasından temizleyin!";
                color = Color.white;
                break;
        }

        gameUI.ShowModeIntroBanner(title, subtitle, desc, color, 3.2f);
    }

    public void RestartGame()
    {
        MainMenuManager.OpenHangarOnSceneLoad = false;
        MainMenuManager.AutoPlayOnSceneLoad = true;
        TwoPlayerDuelManager.Instance?.StopDuel();
        CleanUpBulletHellPlane();
        EnemyFormationManager.Instance?.StopDirector();
        AirspaceHazardDirector.Instance?.StopDirector();
        DroneManager.EnsureInstance()?.ClearDrones();
        PlayerProfileUI.Instance?.SetHUDBadgeActive(false);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        MainMenuManager.AutoPlayOnSceneLoad = false;
        TwoPlayerDuelManager.Instance?.StopDuel();
        CleanUpBulletHellPlane();
        EnemyFormationManager.Instance?.StopDirector();
        AirspaceHazardDirector.Instance?.StopDirector();
        DroneManager.EnsureInstance()?.ClearDrones();
        PlayerProfileUI.Instance?.SetHUDBadgeActive(false);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDisable()
    {
        CleanUpBulletHellPlane();
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (_bulletHellCoroutine != null) StopCoroutine(_bulletHellCoroutine);
        if (_timeAttackCoroutine != null) StopCoroutine(_timeAttackCoroutine);
    }

    private void OnDestroy()
    {
        CleanUpBulletHellPlane();
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (_bulletHellCoroutine != null) StopCoroutine(_bulletHellCoroutine);
        if (_timeAttackCoroutine != null) StopCoroutine(_timeAttackCoroutine);
    }
}
