using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 2 Oyunculu Taktik Hava Düellosu Yöneticisi (Competitive PvP Duel System):
/// 1. Best of 3 Seri Formatı: İlk 2 raundu kazanan pilot maçı şampiyon olarak tamamlar.
/// 2. Tam Dengeli & Adil Uçak İstatistikleri: Can (100 HP), hız ve namlu hasarı her iki oyuncu için eşitlenir.
/// 3. Canlı Raunt Skorbordu: Ekranın üst-orta avionik panelinde neon LED galibiyet pipsleri ([●][○]).
/// 4. Ortak Taktik Hava İkmali (Arena Supply Airdrop): Her rauntta merkez hava sahasına inen ve ilk kapanın
///    yararlandığı fırsat sandıkları (+35 HP Tamir, Çift Namlu Ateşi veya Plazma Savunma Kalkanı).
/// 5. Raunt Geçişleri & Şampiyonluk Fanfarı: Raunt aralarında dinamik geri sayım ve altın ödüllü final ekranı.
/// </summary>
public class TwoPlayerDuelManager : MonoBehaviour
{
    public static TwoPlayerDuelManager Instance { get; private set; }

    public const int ROUNDS_TO_WIN = 2; // Best of 3 (2 Galibiyet alan kazanır)

    [Header("Düello Durumu")]
    public bool IsDuelActive { get; private set; } = false;
    public int p1Score { get; private set; } = 0;
    public int p2Score { get; private set; } = 0;
    public int currentRound { get; private set; } = 1;
    public bool isRoundTransitioning { get; private set; } = false;

    [Header("Uçak Referansları")]
    public PlaneHealth plane1Health;
    public PlaneHealth plane2Health;

    private Coroutine _airdropCoroutine;
    private Coroutine _roundTransitionCoroutine;
    private readonly List<GameObject> _activeAirdrops = new List<GameObject>();

    private GameObject _duelScoreboardObj;
    private TextMeshProUGUI _scoreboardTMP;

    private static Texture2D _boxTex;
    private static Sprite _boxSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public static TwoPlayerDuelManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("TwoPlayerDuelManager");
        if (go == null)
            go = new GameObject("TwoPlayerDuelManager");

        Instance = go.GetComponent<TwoPlayerDuelManager>();
        if (Instance == null)
            Instance = go.AddComponent<TwoPlayerDuelManager>();

        return Instance;
    }

    /// <summary>
    /// 2 Oyunculu Düello maçını tamamen dengeli ve adil kurallarla başlatır.
    /// </summary>
    public void SetupDuelMatch()
    {
        StopAllCoroutines();
        CleanStrayObjects();

        p1Score = 0;
        p2Score = 0;
        currentRound = 1;
        isRoundTransitioning = false;
        IsDuelActive = true;

        FindPlanes();

        // 1. Oyuncu 1 (Mavi) ve Oyuncu 2 (Kırmızı) Eşitlenmesi
        EqualizeAircraft(plane1Health, isPlayer1: true);
        EqualizeAircraft(plane2Health, isPlayer1: false);

        // Skorbord HUD'ını hazırla
        SetupScoreboardHUD();
        UpdateScoreboardHUD();

        // 1. Raundu Başlat
        StartRound(1);
    }

    private void FindPlanes()
    {
        var planes = FindObjectsByType<PlaneHealth>(FindObjectsSortMode.None);
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

    /// <summary>
    /// Karşı uçağın (Oyuncu 2) modelini, spritenı, canını, hızını, mermi salvosunu ve hasarını
    /// 1. Uçakla (oyuncunun seçtiği jet) BİREBİR AYNI yapar!
    /// </summary>
    private void EqualizeAircraft(PlaneHealth ph, bool isPlayer1)
    {
        if (ph == null) return;

        var pc = ph.GetComponent<PlaneController>();
        var ps = ph.GetComponent<PlaneShooter>();
        var sr = ph.GetComponent<SpriteRenderer>();

        // 1. Oyuncunun Hangarda seçtiği aktif uçak modelinin tüm özelliklerini (Sprite, Can, Hız, Namlu Hasarı, Çoklu Mermi) her iki uçağa da uygula!
        if (HangarManager.Instance != null && pc != null)
        {
            HangarManager.Instance.ApplyActiveAircraftStats(pc, isActualCombatStart: false);
        }

        // 2. Can değerini kesin olarak uçağın gerçek canına eşitle
        float targetHp = (ph.maxHealth > 10f) ? ph.maxHealth : 100f;
        ph.ResetHealth(targetHp);

        // 3. Manevra bileşenini garanti et (Akrobatik Kaçış / Fıçı Tono)
        var maneuvers = ph.GetComponent<PlayerCombatManeuvers>();
        if (maneuvers == null)
            maneuvers = ph.gameObject.AddComponent<PlayerCombatManeuvers>();

        // 4. Başlangıç pozisyonları, ideal çevik ölçek ve renk tonları
        ph.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
        if (isPlayer1)
        {
            ph.transform.position = new Vector3(0f, -3.8f, 0f);
            ph.transform.rotation = Quaternion.identity;
            if (sr != null) sr.color = Color.white;
        }
        else
        {
            ph.transform.position = new Vector3(0f, 3.8f, 0f);
            ph.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            // P2 için kızıl/turuncu rakip yarış kamuflajı tonu
            if (sr != null) sr.color = new Color(1f, 0.42f, 0.42f, 1f);
        }
    }

    /// <summary>
    /// Belirtilen raundu başlatır, uçakları başlangıç noktalarına çeker ve geri sayım yapar.
    /// </summary>
    public void StartRound(int roundNumber)
    {
        currentRound = roundNumber;
        isRoundTransitioning = false;

        CleanStrayObjects();

        FindPlanes();

        // Uçakları yeniden konumlandır ve canlarını tazele
        if (plane1Health != null)
        {
            plane1Health.transform.position = new Vector3(0f, -3.8f, 0f);
            plane1Health.transform.rotation = Quaternion.identity;
            plane1Health.ResetHealth(plane1Health.maxHealth);
            plane1Health.TriggerInvulnerability(1.4f);
        }

        if (plane2Health != null)
        {
            plane2Health.transform.position = new Vector3(0f, 3.8f, 0f);
            plane2Health.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            plane2Health.ResetHealth(plane2Health.maxHealth);
            plane2Health.TriggerInvulnerability(1.4f);
        }

        UpdateScoreboardHUD();

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateHealthBars();

        // Ses ve Dalga Bildirimi
        AudioManager.Instance?.PlayWarningAlarm();

        string bannerMsg;
        Color bannerColor;

        if (roundNumber == 1)
        {
            bannerMsg = "<size=28><color=#FFE082>[1. RAUNT] BAŞLA!</color></size>\n<color=#00E5FF>MAVİ KANAT (P1)</color> vs <color=#FF5252>KIRMIZI AVCI (P2)</color>";
            bannerColor = new Color(0.2f, 0.9f, 1f);
        }
        else if (p1Score == 1 && p2Score == 1)
        {
            bannerMsg = "<size=30><color=#FF2222>★ FİNAL RAUNDU (MATCH POINT)! ★</color></size>\n<color=#FFE082>KAZANAN ŞAMPİYON OLUR!</color>";
            bannerColor = new Color(1f, 0.2f, 0.2f);
        }
        else
        {
            bannerMsg = $"<size=28><color=#FFE082>[{roundNumber}. RAUNT] BAŞLA!</color></size>\n<color=#00E5FF>MAVİ: {p1Score}</color>  |  <color=#FF5252>KIRMIZI: {p2Score}</color>";
            bannerColor = new Color(1f, 0.85f, 0.2f);
        }

        gameUI?.ShowWaveAlert(bannerMsg, bannerColor, 2.2f);

        // Ortak Taktik Hava İkmali Rutini (Maç başladıktan 8-11 sn sonra iner)
        if (_airdropCoroutine != null) StopCoroutine(_airdropCoroutine);
        _airdropCoroutine = StartCoroutine(AirdropSpawnerRoutine());
    }

    /// <summary>
    /// Bir uçak öldüğünde GameManager tarafından çağrılır.
    /// </summary>
    public void OnPlaneDied(bool isPlayer1)
    {
        if (!IsDuelActive || isRoundTransitioning) return;
        isRoundTransitioning = true;

        if (_airdropCoroutine != null)
        {
            StopCoroutine(_airdropCoroutine);
            _airdropCoroutine = null;
        }

        string winnerName;
        Color winColor;

        if (isPlayer1)
        {
            p2Score++;
            winnerName = "KIRMIZI AVCI (OYUNCU 2)";
            winColor = new Color(1f, 0.35f, 0.35f);
        }
        else
        {
            p1Score++;
            winnerName = "MAVİ KANAT (OYUNCU 1)";
            winColor = new Color(0.15f, 0.85f, 1f);
        }

        UpdateScoreboardHUD();
        AudioManager.Instance?.PlayExplosion(isBoss: true);

        // Şampiyonluk kontrolü (2 galibiyete ulaşıldı mı?)
        if (p1Score >= ROUNDS_TO_WIN || p2Score >= ROUNDS_TO_WIN)
        {
            bool p1Champion = (p1Score >= ROUNDS_TO_WIN);
            if (_roundTransitionCoroutine != null) StopCoroutine(_roundTransitionCoroutine);
            _roundTransitionCoroutine = StartCoroutine(MatchChampionRoutine(p1Champion));
        }
        else
        {
            if (_roundTransitionCoroutine != null) StopCoroutine(_roundTransitionCoroutine);
            _roundTransitionCoroutine = StartCoroutine(RoundTransitionRoutine(winnerName, winColor));
        }
    }

    private IEnumerator RoundTransitionRoutine(string roundWinnerName, Color winColor)
    {
        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert($"<size=26><color=#FFE082>{currentRound}. RAUNT BİTTİ!</color></size>\n<b><color=#{ColorUtility.ToHtmlStringRGB(winColor)}>KAZANAN: {roundWinnerName}</color></b>\n<color=#FFFFFF>SERİ DURUMU: {p1Score} - {p2Score}</color>", winColor, 2.5f);

        yield return new WaitForSeconds(2.4f);

        StartRound(currentRound + 1);
    }

    private IEnumerator MatchChampionRoutine(bool p1Won)
    {
        IsDuelActive = false;
        isRoundTransitioning = false;

        string championTitle = p1Won ? "MAVİ KANAT (OYUNCU 1)" : "KIRMIZI AVCI (OYUNCU 2)";
        Color champColor = p1Won ? new Color(0.15f, 0.9f, 1f) : new Color(1f, 0.35f, 0.35f);

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert($"<size=30><color=#FFD54F>★ ŞAMPİYON: {championTitle}! ★</color></size>\n<color=#FFFFFF>SERİ SKORU: {p1Score} - {p2Score} (Best of 3)</color>", champColor, 3.5f);

        AudioManager.Instance?.PlayRewardFanfare();

        // Hangar Altını Ödülü
        int goldPrize = 1500;
        HangarManager.EnsureInstance()?.AddGold(goldPrize);

        yield return new WaitForSeconds(2.8f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameEnded(true);
        }

        GameOverUI gameOver = FindObjectOfType<GameOverUI>();
        if (gameOver != null)
        {
            string msg = $"★ 2 OYUNCULU HAVA DÜELLOSU TAMAMLANDI ★\n\n" +
                         $"ŞAMPİYON: <color=#{ColorUtility.ToHtmlStringRGB(champColor)}><b>{championTitle}</b></color>\n" +
                         $"FİNAL SKORU: {p1Score} - {p2Score} (Best of 3)\n\n" +
                         $"<color=#FFE082>+{goldPrize} HANGAR ALTINI KAZANILDI!</color>";
            gameOver.Show(msg);
        }
    }

    public void StopDuel()
    {
        IsDuelActive = false;
        isRoundTransitioning = false;
        StopAllCoroutines();
        CleanStrayObjects();

        if (_duelScoreboardObj != null)
            _duelScoreboardObj.SetActive(false);
    }

    private void CleanStrayObjects()
    {
        // Kalan mermileri temizle
        var bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
        foreach (var b in bullets)
        {
            if (b != null) Destroy(b.gameObject);
        }

        // Kalan airdropları temizle
        for (int i = _activeAirdrops.Count - 1; i >= 0; i--)
        {
            if (_activeAirdrops[i] != null) Destroy(_activeAirdrops[i]);
        }
        _activeAirdrops.Clear();
    }

    // ==================== ORTAK TAKTİK HAVA İKMALİ (AIRDROP) ====================

    private IEnumerator AirdropSpawnerRoutine()
    {
        // Raunt başladıktan 8.5 sn sonra iner
        yield return new WaitForSeconds(8.5f);

        if (!IsDuelActive || isRoundTransitioning) yield break;

        SpawnNeutralAirdrop();
    }

    private void SpawnNeutralAirdrop()
    {
        float spawnX = Random.Range(-2.2f, 2.2f);
        Vector3 spawnPos = new Vector3(spawnX, 4.6f, 0f);

        GameObject dropObj = new GameObject("DuelAirdropCrate");
        dropObj.transform.position = spawnPos;

        SpriteRenderer sr = dropObj.AddComponent<SpriteRenderer>();
        sr.sprite = GetBoxSprite();
        sr.color = new Color(1f, 0.85f, 0.2f);
        sr.sortingOrder = 8;
        dropObj.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

        CircleCollider2D col = dropObj.AddComponent<CircleCollider2D>();
        col.radius = 0.55f;
        col.isTrigger = true;

        var airdropComp = dropObj.AddComponent<DuelAirdropCrate>();
        airdropComp.targetY = 0f; // Ekranın tam ortasına süzülür

        _activeAirdrops.Add(dropObj);

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert("[ORTAK İKMAL İNDİ!]\n<color=#FFE082>İKMAL KUTUSUNA İLK ULAŞAN GÜÇLENİR!</color>", new Color(1f, 0.85f, 0.2f), 2.0f);
        AudioManager.Instance?.PlayWarningAlarm();
    }

    // ==================== SKORBORD HUD ====================

    private void SetupScoreboardHUD()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("HUD_DuelScoreboard");
        if (existing != null)
        {
            _duelScoreboardObj = existing.gameObject;
        }
        else
        {
            _duelScoreboardObj = new GameObject("HUD_DuelScoreboard", typeof(RectTransform));
            _duelScoreboardObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = _duelScoreboardObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -25f);
            rt.sizeDelta = new Vector2(540f, 44f);

            Image bg = _duelScoreboardObj.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.08f, 0.14f, 0.90f);

            Outline outline = _duelScoreboardObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.40f, 0.35f, 0.85f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(_duelScoreboardObj.transform, false);

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            _scoreboardTMP = textObj.AddComponent<TextMeshProUGUI>();
            _scoreboardTMP.fontSize = 20f;
            _scoreboardTMP.fontStyle = FontStyles.Bold;
            _scoreboardTMP.alignment = TextAlignmentOptions.Center;
            _scoreboardTMP.color = Color.white;
            _scoreboardTMP.raycastTarget = false;
        }

        _duelScoreboardObj.SetActive(true);
        _duelScoreboardObj.transform.SetAsLastSibling();
    }

    public void UpdateScoreboardHUD()
    {
        if (_scoreboardTMP == null)
        {
            SetupScoreboardHUD();
            if (_scoreboardTMP == null) return;
        }

        string p1Pips = GetPipsString(p1Score, isPlayer1: true);
        string p2Pips = GetPipsString(p2Score, isPlayer1: false);

        string roundStr = (p1Score == 1 && p2Score == 1)
            ? "<color=#FF3333>FİNAL RAUNDU</color>"
            : $"RAUNT {currentRound}/3";

        _scoreboardTMP.text = $"<color=#00E5FF>MAVİ</color> {p1Pips}   <color=#FFE082>[ {roundStr} ]</color>   {p2Pips} <color=#FF5252>KIRMIZI</color>";
    }

    private string GetPipsString(int score, bool isPlayer1)
    {
        string filledColor = isPlayer1 ? "#00E5FF" : "#FF5252";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < ROUNDS_TO_WIN; i++)
        {
            if (i < score)
                sb.Append($"<color={filledColor}>●</color> ");
            else
                sb.Append("<color=#556677>○</color> ");
        }

        return sb.ToString().TrimEnd();
    }

    private static Sprite GetBoxSprite()
    {
        if (_boxSprite != null) return _boxSprite;

        int size = 32;
        _boxTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color border = new Color(1f, 0.85f, 0.2f, 1f);
        Color fill = new Color(0.2f, 0.15f, 0.05f, 0.95f);
        Color cross = new Color(1f, 0.95f, 0.4f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = (x < 2 || x >= size - 2 || y < 2 || y >= size - 2);
                bool isCross = (x == y || x == (size - 1 - y));

                if (isBorder) _boxTex.SetPixel(x, y, border);
                else if (isCross) _boxTex.SetPixel(x, y, cross);
                else _boxTex.SetPixel(x, y, fill);
            }
        }
        _boxTex.Apply();

        _boxSprite = Sprite.Create(_boxTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return _boxSprite;
    }
}

/// <summary>
/// 2 Oyunculu Düello sırasında merkez hava sahasına inen tarafsız taktik ikmal sandığı.
/// İlk dokunan oyuncu (Mavi veya Kırmızı) rastgele 1 bonus kazanır:
/// 1. +35 HP Gövde Tamiri
/// 2. Çift Namlu Ateşi (5 Saniye)
/// 3. Plazma Enerji Kalkanı (40 HP Soğurma)
/// </summary>
public class DuelAirdropCrate : MonoBehaviour
{
    public float targetY = 0f;
    public float fallSpeed = 0.95f;

    private bool _collected = false;

    private void Update()
    {
        if (_collected) return;

        // Hedef merkeze doğru yavaşça süzülür
        if (transform.position.y > targetY)
        {
            transform.position += Vector3.down * (fallSpeed * Time.deltaTime);
        }
        else
        {
            // Merkezde hafifçe salınım yapar
            float bob = Mathf.Sin(Time.time * 3f) * 0.08f;
            transform.position = new Vector3(transform.position.x, targetY + bob, 0f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected) return;

        PlaneController pc = other.GetComponent<PlaneController>();
        if (pc == null) pc = other.GetComponentInParent<PlaneController>();

        if (pc != null)
        {
            _collected = true;
            ApplyAirdropBuff(pc);
            Destroy(gameObject);
        }
    }

    private void ApplyAirdropBuff(PlaneController pc)
    {
        bool isP1 = pc.isPlayer1;
        string pilotTag = isP1 ? "<color=#00E5FF>[MAVİ KANAT]</color>" : "<color=#FF5252>[KIRMIZI AVCI]</color>";

        PlaneHealth health = pc.GetComponent<PlaneHealth>();
        PlaneShooter shooter = pc.GetComponent<PlaneShooter>();

        // Rastgele 1'i seç: 0 = Tamir, 1 = Çift Namlu, 2 = Kalkan
        int roll = Random.Range(0, 3);
        string buffDesc = "";
        Color buffColor = Color.green;

        if (roll == 0)
        {
            // +35 HP Tamir
            if (health != null) health.Heal(35f);
            buffDesc = "+35 HP GÖVDE TAMİRİ!";
            buffColor = new Color(0.2f, 0.9f, 0.4f);
        }
        else if (roll == 1)
        {
            // Çift Namlu Ateşi
            if (shooter != null) shooter.ActivateOverdrive(5.5f);
            buffDesc = "5 SN ÇİFT NAMLU TAARRUZU!";
            buffColor = new Color(1f, 0.6f, 0.15f);
        }
        else
        {
            // Plazma Kalkanı
            if (health != null) health.SetEnemyShield(40f);
            buffDesc = "40 HP PLAZMA SAVUNMA KALKANI!";
            buffColor = new Color(0.1f, 0.85f, 1f);
        }

        AudioManager.Instance?.PlayRewardFanfare();
        VFXManager.SpawnHitSparks(transform.position, buffColor);

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert($"{pilotTag} İKMALİ ALDI!\n<color=#{ColorUtility.ToHtmlStringRGB(buffColor)}><b>{buffDesc}</b></color>", buffColor, 2.0f);
    }
}
