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

    public enum GameMode { SoloVsAI, TwoPlayer }
    public GameMode CurrentMode { get; private set; } = GameMode.SoloVsAI;

    [Header("20 Uçaklık İlerleme Modu")]
    public int currentPlaneIndex = 1;
    public const int MaxPlanes = 20;

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
        ApplyGameMode();
    }

    /// <summary>
    /// Menüden seçilen Solo veya Online (İki Oyunculu) modunu sahneye uygular.
    /// </summary>
    public void ApplyGameMode()
    {
        gameEnded = false;

        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);
        CurrentMode = isOnline ? GameMode.TwoPlayer : GameMode.SoloVsAI;

        FindPlanesIfNull();

        if (plane2Health != null)
        {
            _plane2OriginalScale = plane2Health.transform.localScale;
            var ai = plane2Health.GetComponent<AIController>();
            if (ai != null)
            {
                ai.enabled = !isOnline; // Online ise AI kapalı, Solo ise açık
            }
        }

        // P2 mobil kontrollerini (Joystick ve Ateş butonu) aktif et
        var p2Joystick = FindObjectOfType<TwoPlayerInputRouter>();
        if (p2Joystick == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                p2Joystick = canvas.gameObject.AddComponent<TwoPlayerInputRouter>();
        }
        if (p2Joystick != null)
            p2Joystick.SetTwoPlayerMode(isOnline);

        // Solo modda 1. uçaktan başla ve 3 can hakkı ver
        if (CurrentMode == GameMode.SoloVsAI)
        {
            player1Lives = MaxPlayerLives;
            currentPlaneIndex = 1;
            PlayerUpgradeManager.EnsureInstance();
            UpgradeSelectionUI.EnsureInstance();
            ApplyEnemyWave(currentPlaneIndex);
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

    public static bool IsBossPlane(int index) => (index % 5 == 0);

    public static string GetPlaneTitle(int index)
    {
        switch (index)
        {
            case 1: return "DÜŞMAN KEŞİF JETİ";
            case 2: return "HAFİF AVCI";
            case 3: return "TAARRUZ AVCISI";
            case 4: return "ÖNCÜ İT DALAŞÇISI";
            case 5: return "⚔️ BOSS 1: AĞIR TAARRUZ JETİ";
            case 6: return "ÇİFT MOTORLU AVCI";
            case 7: return "MİG İNTERSEPTÖR";
            case 8: return "KOBRA JETİ";
            case 9: return "HAYALET FİLO LİDERİ";
            case 10: return "☠️ BOSS 2: HAYALET AVCI (PHANTOM)";
            case 11: return "AĞIR SÜPERSONİK JET";
            case 12: return "FIRTINA AVCI";
            case 13: return "GECE AVCISI";
            case 14: return "TİTANYUM PENÇE";
            case 15: return "🔥 BOSS 3: CEHENNEM KUŞU (FIREBIRD)";
            case 16: return "SİBER CELLAT";
            case 17: return "PLAZMA AVCI";
            case 18: return "HAVA HAKİMİYET JETİ";
            case 19: return "KARA DUL PROTO TİPİ";
            case 20: return "👑 FİNAL BOSS: ASLARIN ASİ GÖK HAKİMİ";
            default: return $"DÜŞMAN JETİ #{index}";
        }
    }

    public static float GetDifficultyHealthMultiplier()
    {
        switch (GameSettings.SelectedDifficulty)
        {
            case GameSettings.Difficulty.Kolay: return 0.75f; // Kolay modda %25 daha az can
            case GameSettings.Difficulty.Zor:   return 1.30f; // Zor modda %30 daha fazla can
            default:                            return 1.00f; // Normal mod
        }
    }

    /// <summary>
    /// Uçağın canını dalga numarasına VE seçilen menü zorluğuna (Kolay/Normal/Zor) göre hesaplar.
    /// Boss canları 2 kademe kolaylaştırılmıştır.
    /// </summary>
    public static float GetPlaneHealth(int index)
    {
        float baseHp = 0f;
        switch (index)
        {
            case 5:  baseHp = 160f; break; // Boss 1 (Eski 260 HP yerine dengeli 160 HP)
            case 10: baseHp = 240f; break; // Boss 2 (Eski 380 HP yerine dengeli 240 HP)
            case 15: baseHp = 320f; break; // Boss 3 (Eski 480 HP yerine dengeli 320 HP)
            case 20: baseHp = 420f; break; // Final Boss (Eski 650 HP yerine dengeli 420 HP)
            default: baseHp = 70f + (index * 6f); break; // 1. uçak 76 HP -> 19. uçak 184 HP
        }

        return Mathf.Round(baseHp * GetDifficultyHealthMultiplier());
    }

    /// <summary>
    /// Belirtilen dalgaya göre Uçak 2'nin canını, boyutunu, rengini ve yapay zekasını yapılandırır.
    /// </summary>
    public void ApplyEnemyWave(int waveIndex)
    {
        if (plane2Health == null) return;

        bool isBoss = IsBossPlane(waveIndex);
        float hp = GetPlaneHealth(waveIndex);

        // Canı ve bileşenleri sıfırla
        plane2Health.ResetHealth(hp);

        // Uçağın konumunu üst merkeze al
        plane2Health.transform.position = new Vector3(0f, 3.5f, 0f);

        // Görsel Boss Ölçeklendirmesi ve Renk Tonlaması
        Transform planeTransform = plane2Health.transform;
        SpriteRenderer sr = plane2Health.GetComponent<SpriteRenderer>();

        if (isBoss)
        {
            float scaleMultiplier = (waveIndex == 20) ? 1.55f : (waveIndex == 15 ? 1.45f : (waveIndex == 10 ? 1.38f : 1.28f));
            planeTransform.localScale = _plane2OriginalScale * scaleMultiplier;

            if (sr != null)
            {
                if (waveIndex == 5) sr.color = new Color(1f, 0.85f, 0.45f); // Altın Sarısı / Taktik
                else if (waveIndex == 10) sr.color = new Color(0.9f, 0.45f, 1f); // Mor Gölge Phantom
                else if (waveIndex == 15) sr.color = new Color(1f, 0.35f, 0.2f); // Kor Alev Lav
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
            ai.ConfigureWave(waveIndex, isBoss);
        }

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateHealthBars();
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

            // SOLO MOD CAN HAKKI KONTROLÜ (Toplam 3 Can Hakkı)
            player1Lives--;

            var gameUI = FindObjectOfType<GameUI>();
            gameUI?.UpdateHealthBars();

            if (player1Lives > 0)
            {
                // Can hakkı hala var: Yeniden Doğuş!
                gameUI?.ShowWaveAlert($"💥 UÇAĞINIZ DÜŞTÜ!\n<color=#F1C40F>KALAN CAN HAKKI: {player1Lives} / {MaxPlayerLives}</color>", new Color(1f, 0.4f, 0.2f), 2.0f);
                StartCoroutine(RespawnPlayer1Routine(1.2f));
            }
            else
            {
                // 3 Can Hakkı da tükendi: Oyun biter, önceki gibi sıfırlanıp ana menüye atar!
                gameEnded = true;
                string defeatMsg = $"GÖREV BAŞARISIZ!\nTÜM CAN HAKLARI TÜKENDİ!\nİLERLEME: UÇAK {currentPlaneIndex} / {MaxPlanes}";

                if (gameOverUI != null)
                    gameOverUI.Show(defeatMsg);

                StartCoroutine(ReturnToMainMenuRoutine(2.0f));
            }
            return;
        }

        // 2. DÜŞMAN (UÇAK 2) ÖLDÜĞÜNDE:
        if (CurrentMode == GameMode.TwoPlayer)
        {
            // İki oyunculu modda tek rauntluk düello biter
            gameEnded = true;
            if (gameOverUI != null)
                gameOverUI.Show("OYUNCU 1 KAZANDI!");

            StartCoroutine(ReturnToMainMenuRoutine(1.8f));
        }
        else
        {
            // SOLO MOD: 20 UÇAKLIK İLERLEME SİSTEMİ!
            if (currentPlaneIndex >= MaxPlanes)
            {
                // 20. UÇAK (FİNAL BOSS) DA DÜŞTÜ! TAM ZAFER!
                gameEnded = true;

                if (gameOverUI != null)
                    gameOverUI.Show("🏆 BÜYÜK HAREKAT ZAFERİ! 🏆\n20 DÜŞMAN UÇAĞI VE TÜM BOSS'LAR İMHA EDİLDİ!");

                StartCoroutine(ReturnToMainMenuRoutine(3.0f));
            }
            else
            {
                int justDefeated = currentPlaneIndex;
                bool wasBoss = IsBossPlane(justDefeated);

                if (wasBoss)
                {
                    // BOSS İMHA EDİLDİ! OYUNCUYA HANGAR GÜÇLENDİRME SEÇİMİ SUN!
                    var gameUI = FindObjectOfType<GameUI>();
                    gameUI?.ShowWaveAlert($"💥 BOSS DÜŞÜRÜLDÜ!\n<color=#F1C40F>HANGAR GÜÇLENDİRMESİ AÇILIYOR...</color>", new Color(1f, 0.85f, 0.2f), 2.0f);

                    StartCoroutine(OpenUpgradeHangarRoutine(justDefeated));
                }
                else
                {
                    // Standart düşman imha edildi: Oyuncuya +35 HP can tamiri ver
                    plane1Health?.Heal(35f);

                    var gameUI = FindObjectOfType<GameUI>();
                    currentPlaneIndex++;
                    bool nextIsBoss = IsBossPlane(currentPlaneIndex);

                    if (nextIsBoss)
                    {
                        gameUI?.ShowWaveAlert($"⚠️ DİKKAT! {GetPlaneTitle(currentPlaneIndex)} YAKLAŞIYOR! ⚠️", new Color(1f, 0.3f, 0.2f), 2.2f);
                    }
                    else
                    {
                        gameUI?.ShowWaveAlert($"🎯 UÇAK [{justDefeated}/{MaxPlanes}] İMHA EDİLDİ!\n<size=70%><color=#2ECC71>+35 CAN TAMİR EDİLDİ</color></size>", new Color(0.2f, 0.9f, 1f), 1.6f);
                    }

                    // 1.3 saniye sonra yeni düşman uçağını sahneye sok
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
                gameUI?.ShowWaveAlert($"⚠️ DİKKAT! {GetPlaneTitle(currentPlaneIndex)} YAKLAŞIYOR! ⚠️", new Color(1f, 0.3f, 0.2f), 2.2f);
            }
            else
            {
                gameUI?.ShowWaveAlert($"🚀 GÜÇLENDİRİLMİŞ UÇAK SAVAŞA DÖNDÜ!\n<color=#00E5FF>HEDEF: UÇAK {currentPlaneIndex}/{MaxPlanes}</color>", new Color(0.2f, 0.9f, 1f), 2.0f);
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

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
