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

    public enum GameMode { SoloVsAI, TwoPlayer, Endless, BossRush }
    public GameMode CurrentMode { get; private set; } = GameMode.SoloVsAI;

    [Header("20 Uçaklık İlerleme Modu")]
    public int currentPlaneIndex = 1;
    public const int MaxPlanes = 20;

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

        // Eğer sahnede Ana Menü açıksa oyunu ve alt yazıyı burada başlatma!
        // Yalnızca oyuncu menüden "HAREKATA BAŞLA" butonuna bastığında ApplyGameMode() çalışacaktır.
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        if (menu != null && menu.gameObject.activeInHierarchy)
        {
            return;
        }

        ApplyGameMode();
    }

    /// <summary>
    /// Menüden seçilen Modu (Solo, 2 Oyunculu, Sonsuz İt Dalaşı, Boss Rush) sahneye uygular.
    /// </summary>
    public void ApplyGameMode()
    {
        gameEnded = false;
        sessionGoldEarned = 0;
        GameSettings.LoadSettings();

        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);
        if (isOnline)
            CurrentMode = GameMode.TwoPlayer;
        else if (GameSettings.SelectedMode == GameSettings.GameMode.Endless)
            CurrentMode = GameMode.Endless;
        else if (GameSettings.SelectedMode == GameSettings.GameMode.BossRush)
            CurrentMode = GameMode.BossRush;
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
                HangarManager.EnsureInstance()?.ApplyActiveAircraftStats(p1Controller);
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
        {
            ApplyEnemyWave(1);
        }
        else
        {
            player1Lives = MaxPlayerLives;
            currentPlaneIndex = 1;
            PlayerUpgradeManager.EnsureInstance();
            UpgradeSelectionUI.EnsureInstance();

            if (CurrentMode == GameMode.SoloVsAI)
            {
                // Solo Seferde 1. Dalga öncesinde hikaye brifingini oynat
                StoryCampaignManager.EnsureInstance()?.PlayCampaignIntro(() =>
                {
                    ApplyEnemyWave(currentPlaneIndex);
                });
            }
            else
            {
                // Sonsuz İt Dalaşı veya Boss Rush doğrudan başlar
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
        if (CurrentMode == GameMode.Endless) return 999;
        return MaxPlanes;
    }

    public int GetEffectiveWaveIndex(int index)
    {
        if (CurrentMode == GameMode.BossRush)
        {
            // Boss Rush: 1 -> 5, 2 -> 10, 3 -> 15, 4 -> 20
            return Mathf.Clamp(index, 1, 4) * 5;
        }
        return index;
    }

    public bool IsBossPlane(int index)
    {
        if (CurrentMode == GameMode.BossRush) return true;
        return (index % 5 == 0);
    }

    public string GetCurrentPlaneTitle(int index)
    {
        int effectiveIndex = GetEffectiveWaveIndex(index);
        return StoryCampaignManager.GetPlaneTitle(effectiveIndex);
    }

    public static float GetDifficultyHealthMultiplier()
    {
        switch (GameSettings.SelectedDifficulty)
        {
            case GameSettings.Difficulty.Kolay: return 0.70f; // Kolay modda %30 daha az can
            case GameSettings.Difficulty.Zor:   return 1.45f; // Zor modda %45 daha fazla can
            default:                            return 1.00f; // Normal mod
        }
    }

    /// <summary>
    /// Uçağın canını dalga numarasına VE seçilen menü zorluğuna (Kolay/Normal/Zor) göre hesaplar.
    /// </summary>
    public float GetPlaneHealthForWave(int index)
    {
        int eff = GetEffectiveWaveIndex(index);
        float baseHp = 0f;
        switch (eff)
        {
            case 5:  baseHp = 160f; break; // Boss 1
            case 10: baseHp = 240f; break; // Boss 2
            case 15: baseHp = 320f; break; // Boss 3
            case 20: baseHp = 420f; break; // Final Boss
            default:
                if (eff > 20)
                    baseHp = 220f + ((eff - 20) * 15f); // Sonsuz mod artışı
                else
                    baseHp = 70f + (eff * 6f);
                break;
        }

        return Mathf.Round(baseHp * GetDifficultyHealthMultiplier());
    }

    public static float GetPlaneHealth(int index)
    {
        if (Instance != null)
            return Instance.GetPlaneHealthForWave(index);

        return Mathf.Round((70f + (index * 6f)) * GetDifficultyHealthMultiplier());
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
            float scaleMultiplier = (effIndex >= 20) ? 1.55f : (effIndex >= 15 ? 1.45f : (effIndex >= 10 ? 1.38f : 1.28f));
            planeTransform.localScale = _plane2OriginalScale * scaleMultiplier;

            if (sr != null)
            {
                if (effIndex <= 5) sr.color = new Color(1f, 0.85f, 0.45f); // Altın Sarısı / Taktik
                else if (effIndex <= 10) sr.color = new Color(0.9f, 0.45f, 1f); // Mor Gölge Phantom
                else if (effIndex <= 15) sr.color = new Color(1f, 0.35f, 0.2f); // Kor Alev Lav
                else sr.color = new Color(1f, 0.8f, 0.15f); // İmparatorluk Altını Final Boss
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
            ai.ConfigureWave(effIndex, isBoss);
        }

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateHealthBars();

        // Boss dalgası ise geçişli cümleli Boss Tanıtım sinematiğini oynat
        if (isBoss)
        {
            if (isBossRushFinal)
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
                gameEnded = true;
                if (gameOverUI != null)
                    gameOverUI.Show("OYUNCU 2 KAZANDI!");

                StartCoroutine(ReturnToMainMenuRoutine(1.8f));
                return;
            }

            // CAN HAKKI KONTROLÜ (Toplam 3 Can Hakkı)
            player1Lives--;

            var gameUI = FindObjectOfType<GameUI>();
            gameUI?.UpdateHealthBars();

            if (player1Lives > 0)
            {
                gameUI?.ShowWaveAlert($"UÇAĞINIZ DÜŞTÜ!\n<color=#F1C40F>KALAN CAN HAKKI: {player1Lives}</color>", new Color(1f, 0.4f, 0.2f), 2.0f);
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
                }
                else if (CurrentMode == GameMode.BossRush)
                {
                    defeatMsg = $"BOSS RUSH BAŞARISIZ!\nELENEN BOSS: {currentPlaneIndex - 1} / 4";
                }
                else
                {
                    defeatMsg = $"GÖREV BAŞARISIZ!\nTÜM CAN HAKLARI TÜKENDİ!\nİLERLEME: UÇAK {currentPlaneIndex} / {MaxPlanes}";
                }

                if (gameOverUI != null)
                    gameOverUI.Show(defeatMsg);

                StartCoroutine(ReturnToMainMenuRoutine(2.0f));
            }
            return;
        }

        // 2. DÜŞMAN (UÇAK 2) ÖLDÜĞÜNDE:
        if (CurrentMode == GameMode.TwoPlayer)
        {
            gameEnded = true;
            if (gameOverUI != null)
                gameOverUI.Show("OYUNCU 1 KAZANDI!");

            StartCoroutine(ReturnToMainMenuRoutine(1.8f));
        }
        else
        {
            int totalPlanes = GetTotalPlanesForMode();

            // BOSS RUSH VE SOLO MOD ZAFER KONTROLÜ
            if (CurrentMode == GameMode.BossRush && currentPlaneIndex >= 4)
            {
                // BOSS RUSH TAMAMLANDI: Ağır çekim ve zincirleme devasa patlama sekansını başlat
                StartCoroutine(FinalBossDefeatSlowMoRoutine(deadPlane));
            }
            else if (CurrentMode == GameMode.SoloVsAI && currentPlaneIndex >= MaxPlanes)
            {
                // 20. UÇAK (FİNAL BOSS) DA DÜŞTÜ! TAM ZAFER!
                gameEnded = true;
                int campaignReward = 1000;
                HangarManager.EnsureInstance()?.AddGold(campaignReward);
                sessionGoldEarned += campaignReward;

                PlayerProfileManager.EnsureInstance()?.RecordKill(isBoss: true);
                PlayerProfileManager.EnsureInstance()?.RecordWaveCleared(20);

                StoryCampaignManager.AdvanceToNextCampaign();
                StoryCampaignManager.EnsureInstance()?.PlayCampaignEnding(() => { });
            }
            else
            {
                int justDefeated = currentPlaneIndex;
                bool wasBoss = IsBossPlane(justDefeated);

                if (wasBoss)
                {
                    int bossReward = (CurrentMode == GameMode.BossRush) ? 250 : (100 + justDefeated * 10);
                    HangarManager.EnsureInstance()?.AddGold(bossReward);
                    sessionGoldEarned += bossReward;

                    PlayerProfileManager.EnsureInstance()?.RecordKill(isBoss: true);
                    PlayerProfileManager.EnsureInstance()?.RecordWaveCleared(justDefeated);

                    var gameUI = FindObjectOfType<GameUI>();

                    // SONSUZ İT DALAŞI MODU ÖZEL CAN ÖDÜLÜ:
                    // Her 20. Boss zaferinde (ve 20'nin katı olan her büyük boss dalgasında) sorulmadan doğrudan +1 Can Hakkı ekle!
                    if (CurrentMode == GameMode.Endless && (justDefeated % 20 == 0 || (justDefeated / 5) % 20 == 0))
                    {
                        player1Lives++;
                        AudioManager.Instance?.PlayRewardFanfare();
                        gameUI?.ShowWaveAlert($"[+] 20. BOSS İMHA EDİLDİ!\n<color=#2ECC71><b>OTOMATİK +1 YEDEK CAN EKLENDİ!</b></color>\n(TOPLAM CAN HAKKI: {player1Lives})", new Color(0.2f, 0.95f, 0.4f), 3.0f);
                        gameUI?.UpdateHealthBars();
                    }
                    else
                    {
                        gameUI?.ShowWaveAlert($"BOSS DÜŞÜRÜLDÜ! (+{bossReward}G)\n<color=#F1C40F>HANGAR GÜÇLENDİRMESİ AÇILIYOR...</color>", new Color(1f, 0.85f, 0.2f), 2.0f);
                    }

                    StartCoroutine(OpenUpgradeHangarRoutine(justDefeated));
                }
                else
                {
                    bool isElite = (justDefeated % 5 == 4);
                    int killReward = 15 + (justDefeated * 2) + (isElite ? 15 : 0);
                    HangarManager.EnsureInstance()?.AddGold(killReward);
                    sessionGoldEarned += killReward;

                    PlayerProfileManager.EnsureInstance()?.RecordKill(isBoss: false, isElite: isElite);
                    PlayerProfileManager.EnsureInstance()?.RecordWaveCleared(justDefeated);

                    plane1Health?.Heal(35f);

                    var gameUI = FindObjectOfType<GameUI>();
                    currentPlaneIndex++;
                    bool nextIsBoss = IsBossPlane(currentPlaneIndex);

                    if (nextIsBoss)
                    {
                        gameUI?.ShowWaveAlert($"DİKKAT! {GetCurrentPlaneTitle(currentPlaneIndex)} YAKLAŞIYOR!", new Color(1f, 0.3f, 0.2f), 2.2f);
                    }
                    else
                    {
                        string waveLabel = (CurrentMode == GameMode.Endless) ? $"DALGA {justDefeated}" : $"UÇAK [{justDefeated}/{totalPlanes}]";
                        gameUI?.ShowWaveAlert($"{waveLabel} İMHA EDİLDİ! (+{killReward}G)\n<size=70%><color=#2ECC71>+35 CAN TAMİR EDİLDİ</color></size>", new Color(0.2f, 0.9f, 1f), 1.6f);
                    }

                    StartCoroutine(SpawnNextEnemyRoutine(1.3f));
                }
            }
        }
    }

    private IEnumerator OpenUpgradeHangarRoutine(int justDefeated)
    {
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

            // Canını 100 HP olarak tazele ve 1.8 saniye dokunulmazlık ver
            plane1Health.ResetHealth(100f);
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

        AudioManager.Instance?.PlayRewardFanfare();

        if (gameOverUI != null)
            gameOverUI.Show($"TEBRİKLER AS PİLOT!\nBOSS RUSH TAMAMLANDI!\n+{rushReward} ALTIN KAZANILDI!");

        StartCoroutine(ReturnToMainMenuRoutine(3.0f));
    }

    public void RestartGame()
    {
        DroneManager.EnsureInstance()?.ClearDrones();
        PlayerProfileUI.Instance?.SetHUDBadgeActive(false);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        DroneManager.EnsureInstance()?.ClearDrones();
        PlayerProfileUI.Instance?.SetHUDBadgeActive(false);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
