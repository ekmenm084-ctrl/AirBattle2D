using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Savaş Sonu & "UÇAK DÜŞTÜ" Taktik Uyarı Menüsü (1:1 Referans Tasarım):
/// - Üst uyarı başlığı: [▲ BAĞLANTI KESİLDİ] ve kesik iç kırmızı çerçeve
/// - Ana başlık: "UÇAK DÜŞTÜ" / Alt başlık: "GÖREV SONLANDI" (Zafer durumunda yeşil/altın tema)
/// - 2x2 İstatistik Kutusu: SKOR | EN İYİ ● / [Dişli Altın] ALTIN: +X | [Nişangah] İMHA: Y
/// - Yeşil Ana Buton: [▶ REKLAM İZLE VE DEVAM ET (Canlan + 3 sn Kalkan)]
/// - Alt İkili Butonlar: [↻ TEKRAR DENE] ve [⌂ HANGAR]
/// </summary>
public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [Header("Eski Referans Uyumluluğu")]
    public GameObject panel;
    public Text winnerText;
    public Button restartButton;
    public Button mainMenuButton;

    private CanvasGroup _backdropGroup;
    private RectTransform _cardRt;
    private Image _cardOuterImg;
    private Image _cardInnerFrameImg;
    private Image _topHeaderMaskImg;
    private Image _warnIconImg;
    private TextMeshProUGUI _topWarnTMP;
    private TextMeshProUGUI _mainTitleTMP;
    private TextMeshProUGUI _subTitleTMP;

    private TextMeshProUGUI _scoreTMP;
    private TextMeshProUGUI _bestScoreTMP;
    private Image _bestDotImg;
    private TextMeshProUGUI _goldTMP;
    private TextMeshProUGUI _killsTMP;

    private GameObject _reviveBtnObj;
    private Button _reviveBtn;
    private Image _reviveBtnImg;
    private Image _reviveIconImg;
    private TextMeshProUGUI _reviveBtnTMP;

    private Button _hangarDirectBtn;
    private Coroutine _openAnimCoroutine;
    private Coroutine _reviveFlowCoroutine;
    private bool _isReviving = false;

    private static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");

    // Prosedürel İkon Önbellekleri (Font glif hatası olmadan %100 keskin çizim)
    private static Sprite _sprWarnTriangle;
    private static Sprite _sprGoldGearCoin;
    private static Sprite _sprRedCrosshair;
    private static Sprite _sprFilmPlay;
    private static Sprite _sprRefreshArrow;
    private static Sprite _sprHangarHome;
    private static Sprite _sprGoldDot;

    public bool IsShowing => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static GameOverUI EnsureInstance()
    {
        if (Instance != null) return Instance;

        var existing = FindObjectOfType<GameOverUI>(true);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        Canvas canvas = FindMainCanvas();
        if (canvas == null) return null;

        GameObject host = new GameObject("GameOverUI_Controller", typeof(RectTransform));
        host.transform.SetParent(canvas.transform, false);
        RectTransform rt = host.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Instance = host.AddComponent<GameOverUI>();
        return Instance;
    }

    private static Canvas FindMainCanvas()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c != null && c.isRootCanvas && c.gameObject.scene.isLoaded)
                return c;
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }

    /// <summary>
    /// Eski çağrılarla geriye dönük uyumluluk: mesaj içeriğine göre Zafer veya "UÇAK DÜŞTÜ" ekranını açar.
    /// </summary>
    public void Show(string message)
    {
        bool victory = !string.IsNullOrEmpty(message) &&
                       (message.Contains("ZAFER") || message.Contains("TEBRİKLER") || message.Contains("KAZANDI") || message.Contains("ŞAMPİYON"));

        if (victory)
        {
            ShowScreen(isVictory: true, customTitle: "GÖREV TAMAMLANDI", customSubtitle: "HAVA SAHASI TEMİZLENDİ", customTopAlert: "OPERASYON BAŞARILI");
        }
        else
        {
            ShowCrashScreen();
        }
    }

    /// <summary>
    /// Referans görseldeki 1:1 "UÇAK DÜŞTÜ / GÖREV SONLANDI" uyarı menüsünü açar.
    /// </summary>
    public void ShowCrashScreen(string customTitle = "UÇAK DÜŞTÜ", string customSubtitle = "GÖREV SONLANDI", string customTopAlert = "BAĞLANTI KESİLDİ")
    {
        ShowScreen(isVictory: false, customTitle: customTitle, customSubtitle: customSubtitle, customTopAlert: customTopAlert);
    }

    /// <summary>
    /// Zafer veya düello sonu ekranını aynı modern taktik kart yapısıyla açar.
    /// </summary>
    public void ShowVictory(string customTitle = "ZAFER KAZANILDI", string customSubtitle = "GÖREV BAŞARIYLA TAMAMLANDI")
    {
        ShowScreen(isVictory: true, customTitle: customTitle, customSubtitle: customSubtitle, customTopAlert: "MERKEZ KOMUTANLIĞI");
    }

    private void ShowScreen(bool isVictory, string customTitle, string customSubtitle, string customTopAlert)
    {
        _isReviving = false;
        EnsureModernPanel();
        if (panel == null) return;

        // Savaş zamanını durdur ve paneli en üst katmana al
        Time.timeScale = 0f;
        panel.SetActive(true);
        transform.SetAsLastSibling();
        panel.transform.SetAsLastSibling();

        TouchArrowControls.Instance?.UpdateVisibility();
        if (AudioSettingsUI.Instance != null && AudioSettingsUI.Instance.soundButtonObj != null)
            AudioSettingsUI.Instance.soundButtonObj.SetActive(false);

        ApplyThemeAndTexts(isVictory, customTitle, customSubtitle, customTopAlert);
        PopulateMatchStats();
        ConfigureReviveButtonState(isVictory);

        if (_openAnimCoroutine != null) StopCoroutine(_openAnimCoroutine);
        _openAnimCoroutine = StartCoroutine(AnimateOpenRoutine());
    }

    public void Hide()
    {
        if (_openAnimCoroutine != null) StopCoroutine(_openAnimCoroutine);
        if (_reviveFlowCoroutine != null) StopCoroutine(_reviveFlowCoroutine);
        _isReviving = false;

        if (panel != null)
            panel.SetActive(false);
    }

    private void ApplyThemeAndTexts(bool isVictory, string title, string subtitle, string topAlert)
    {
        Color cardFill = new Color(0.082f, 0.114f, 0.153f, 0.98f); // #151D27
        Color borderOuter = isVictory
            ? new Color(0.16f, 0.72f, 0.48f, 0.92f)
            : new Color(0.70f, 0.21f, 0.23f, 0.92f); // #B3363B kızıl çerçeve
        Color borderInner = isVictory
            ? new Color(0.16f, 0.65f, 0.45f, 0.40f)
            : new Color(0.58f, 0.19f, 0.21f, 0.42f); // İnce iç kızıl çerçeve

        if (_cardOuterImg != null)
        {
            string outerKey = isVictory ? "gameover_card_outer_win" : "gameover_card_outer_loss";
            _cardOuterImg.sprite = MainMenuManager.GetRoundedBoxSprite(outerKey, cardFill, borderOuter, 22, 2);
            _cardOuterImg.type = Image.Type.Sliced;
            _cardOuterImg.color = Color.white;
        }

        if (_cardInnerFrameImg != null)
        {
            string innerKey = isVictory ? "gameover_card_inner_win" : "gameover_card_inner_loss";
            _cardInnerFrameImg.sprite = MainMenuManager.GetRoundedBoxSprite(innerKey, new Color(0f, 0f, 0f, 0f), borderInner, 16, 1);
            _cardInnerFrameImg.type = Image.Type.Sliced;
            _cardInnerFrameImg.color = Color.white;
        }

        if (_topHeaderMaskImg != null)
            _topHeaderMaskImg.color = cardFill;

        if (_warnIconImg != null)
            _warnIconImg.color = isVictory ? new Color(0.30f, 0.90f, 0.62f, 1f) : new Color(0.80f, 0.45f, 0.44f, 1f);

        if (_topWarnTMP != null)
        {
            _topWarnTMP.text = topAlert;
            _topWarnTMP.color = isVictory ? new Color(0.45f, 0.92f, 0.68f, 1f) : new Color(0.80f, 0.46f, 0.45f, 1f);
        }

        if (_mainTitleTMP != null)
        {
            _mainTitleTMP.text = title;
            _mainTitleTMP.color = isVictory ? new Color(0.20f, 0.92f, 0.56f, 1f) : new Color(0.95f, 0.23f, 0.21f, 1f); // #F23B35 parlak kırmızı
        }

        if (_subTitleTMP != null)
        {
            _subTitleTMP.text = subtitle;
            _subTitleTMP.color = new Color(0.69f, 0.75f, 0.82f, 1f); // #B0BFC1
        }
    }

    private void PopulateMatchStats()
    {
        GameManager gm = GameManager.Instance;
        int score = 0;
        int bestScore = ProfileManager.GetInt("AirBattle_BestCombatScore", 0);
        int earnedGold = 0;
        int kills = 0;

        if (gm != null)
        {
            earnedGold = Mathf.Max(0, gm.sessionGoldEarned);
            kills = Mathf.Max(0, gm.sessionKills);
            score = Mathf.Max(0, gm.GetDisplayCombatScore());

            if (score > bestScore)
            {
                bestScore = score;
                ProfileManager.SetInt("AirBattle_BestCombatScore", bestScore);
                ProfileManager.Save();
            }
        }

        string scoreFormatted = score.ToString("N0", TrCulture);
        string bestFormatted = bestScore.ToString("N0", TrCulture);

        if (_scoreTMP != null)
            _scoreTMP.text = $"<color=#A8B8C8>SKOR:</color> <color=#FFFFFF><b>{scoreFormatted}</b></color>";

        if (_bestScoreTMP != null)
            _bestScoreTMP.text = $"<color=#A8B8C8>EN İYİ:</color> <color=#FFFFFF><b>{bestFormatted}</b></color>";

        if (_goldTMP != null)
            _goldTMP.text = $"<color=#CBD5E1>ALTIN:</color> <color=#FACC15><b>+{earnedGold.ToString("N0", TrCulture)}</b></color>";

        if (_killsTMP != null)
            _killsTMP.text = $"<color=#CBD5E1>İMHA:</color> <color=#FFFFFF><b>{kills}</b></color>";
    }

    private void ConfigureReviveButtonState(bool isVictory)
    {
        if (_reviveBtnObj == null || _reviveBtn == null) return;

        GameManager gm = GameManager.Instance;
        bool isTwoPlayer = (gm != null && gm.CurrentMode == GameManager.GameMode.TwoPlayer);

        if (isVictory || isTwoPlayer)
        {
            _reviveBtnObj.SetActive(false);
            return;
        }

        _reviveBtnObj.SetActive(true);
        bool alreadyUsed = (gm != null && gm.sessionReviveUsed);

        if (alreadyUsed)
        {
            _reviveBtn.interactable = false;
            if (_reviveBtnImg != null)
                _reviveBtnImg.color = new Color(0.32f, 0.42f, 0.38f, 0.72f);
            if (_reviveIconImg != null)
                _reviveIconImg.color = new Color(1f, 1f, 1f, 0.45f);
            if (_reviveBtnTMP != null)
                _reviveBtnTMP.text = "<b>CANLANMA HAKKI KULLANILDI</b>\n<size=70%><color=#CBD5E1>(Her sortide 1 kez kullanılabilir)</color></size>";
        }
        else
        {
            _reviveBtn.interactable = true;
            if (_reviveBtnImg != null)
                _reviveBtnImg.color = Color.white;
            if (_reviveIconImg != null)
                _reviveIconImg.color = Color.white;
            if (_reviveBtnTMP != null)
                _reviveBtnTMP.text = "<b>REKLAM İZLE VE DEVAM ET</b>\n<size=72%><color=#E2FCEF>(Canlan + 3 sn Kalkan)</color></size>";
        }
    }

    private IEnumerator AnimateOpenRoutine()
    {
        if (_backdropGroup != null) _backdropGroup.alpha = 0f;
        if (_cardRt != null) _cardRt.localScale = Vector3.one * 0.88f;

        float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            if (_backdropGroup != null) _backdropGroup.alpha = ease;
            if (_cardRt != null) _cardRt.localScale = Vector3.Lerp(Vector3.one * 0.88f, Vector3.one, ease);
            yield return null;
        }

        if (_backdropGroup != null) _backdropGroup.alpha = 1f;
        if (_cardRt != null) _cardRt.localScale = Vector3.one;
        _openAnimCoroutine = null;
    }

    private void OnReviveClicked()
    {
        if (_isReviving) return;
        if (GameManager.Instance == null || GameManager.Instance.sessionReviveUsed) return;

        AudioManager.Instance?.PlayButtonClick();
        _reviveFlowCoroutine = StartCoroutine(ReviveSequenceRoutine());
    }

    private IEnumerator ReviveSequenceRoutine()
    {
        _isReviving = true;
        if (_reviveBtn != null) _reviveBtn.interactable = false;

        // Kısa taktik destek yayını geri sayımı (Harici reklam SDK yoksa çökmeden simüle eder ve anında canlandırır)
        float waitTotal = 1.1f;
        float elapsed = 0f;
        while (elapsed < waitTotal)
        {
            elapsed += Time.unscaledDeltaTime;
            float pct = Mathf.Clamp01(elapsed / waitTotal) * 100f;
            if (_reviveBtnTMP != null)
            {
                _reviveBtnTMP.text = $"<b>DESTEK YAYINI BAĞLANIYOR... %{(int)pct}</b>\n<size=72%><color=#E2FCEF>(Uçak Onarılıyor + 3 sn Kalkan)</color></size>";
            }
            yield return null;
        }

        Hide();
        GameManager.Instance?.RevivePlayerWithShield(3.0f);
        _reviveFlowCoroutine = null;
    }

    private void OnRestartClicked()
    {
        if (_isReviving) return;
        AudioManager.Instance?.PlayButtonClick();
        Hide();
        GameManager.Instance?.RestartGame();
    }

    private void OnHangarClicked()
    {
        if (_isReviving) return;
        AudioManager.Instance?.PlayButtonClick();
        Hide();
        MainMenuManager.OpenHangarOnSceneLoad = true;
        GameManager.Instance?.GoToMainMenu();
    }

    // =========================================================================
    // 1:1 REFERANS GÖRSEL PANEL İNŞASI
    // =========================================================================
    private void EnsureModernPanel()
    {
        if (panel != null && _cardRt != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? FindMainCanvas();
        if (canvas == null) return;

        // Varsa eski ilkel paneli temizle
        if (panel != null && _cardRt == null)
        {
            Destroy(panel);
            panel = null;
        }

        TMP_FontAsset font = MainMenuManager.GetTacticalFont();

        // 1. Tam Ekran Karartmalı Arka Plan (Sisli savaş alanı atmosferi)
        panel = new GameObject("GameOverPanel_1to1", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rootRt = panel.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image backdropImg = panel.AddComponent<Image>();
        backdropImg.color = new Color(0.055f, 0.082f, 0.125f, 0.86f);
        backdropImg.raycastTarget = true;

        _backdropGroup = panel.AddComponent<CanvasGroup>();
        _backdropGroup.interactable = true;
        _backdropGroup.blocksRaycasts = true;

        // 2. Merkez Ana Taktik Kart (820 x 880)
        GameObject cardObj = new GameObject("ModalCard", typeof(RectTransform));
        cardObj.transform.SetParent(panel.transform, false);
        _cardRt = cardObj.GetComponent<RectTransform>();
        _cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        _cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        _cardRt.pivot = new Vector2(0.5f, 0.5f);
        _cardRt.anchoredPosition = Vector2.zero;
        _cardRt.sizeDelta = new Vector2(820f, 880f);

        Color cardFill = new Color(0.082f, 0.114f, 0.153f, 0.98f);
        Color outerRed = new Color(0.70f, 0.21f, 0.23f, 0.92f);
        _cardOuterImg = cardObj.AddComponent<Image>();
        _cardOuterImg.sprite = MainMenuManager.GetRoundedBoxSprite("gameover_card_outer_loss", cardFill, outerRed, 22, 2);
        _cardOuterImg.type = Image.Type.Sliced;

        // İç İnce Kızıl Çerçeve (14px içeriden, üstte BAĞLANTI KESİLDİ hizasında kesik)
        GameObject innerFrameObj = new GameObject("InnerHairlineFrame", typeof(RectTransform));
        innerFrameObj.transform.SetParent(cardObj.transform, false);
        RectTransform ifRt = innerFrameObj.GetComponent<RectTransform>();
        ifRt.anchorMin = Vector2.zero;
        ifRt.anchorMax = Vector2.one;
        ifRt.offsetMin = new Vector2(14f, 14f);
        ifRt.offsetMax = new Vector2(-14f, -26f);

        _cardInnerFrameImg = innerFrameObj.AddComponent<Image>();
        _cardInnerFrameImg.sprite = MainMenuManager.GetRoundedBoxSprite("gameover_card_inner_loss", new Color(0f, 0f, 0f, 0f), new Color(0.58f, 0.19f, 0.21f, 0.42f), 16, 1);
        _cardInnerFrameImg.type = Image.Type.Sliced;
        _cardInnerFrameImg.raycastTarget = false;

        // Üst çerçeve çizgisini "▲ BAĞLANTI KESİLDİ" etrafında temizce bölen maske şeridi
        GameObject maskObj = new GameObject("TopHeaderLineMask", typeof(RectTransform));
        maskObj.transform.SetParent(cardObj.transform, false);
        RectTransform mRt = maskObj.GetComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0.5f, 1f);
        mRt.anchorMax = new Vector2(0.5f, 1f);
        mRt.pivot = new Vector2(0.5f, 0.5f);
        mRt.anchoredPosition = new Vector2(0f, -26f);
        mRt.sizeDelta = new Vector2(360f, 34f);

        _topHeaderMaskImg = maskObj.AddComponent<Image>();
        _topHeaderMaskImg.color = cardFill;
        _topHeaderMaskImg.raycastTarget = false;

        // 3. Üst Uyarı Satırı: [▲] BAĞLANTI KESİLDİ
        GameObject warnRowObj = new GameObject("TopWarningHeader", typeof(RectTransform));
        warnRowObj.transform.SetParent(cardObj.transform, false);
        RectTransform wrRt = warnRowObj.GetComponent<RectTransform>();
        wrRt.anchorMin = new Vector2(0.5f, 1f);
        wrRt.anchorMax = new Vector2(0.5f, 1f);
        wrRt.pivot = new Vector2(0.5f, 0.5f);
        wrRt.anchoredPosition = new Vector2(0f, -42f);
        wrRt.sizeDelta = new Vector2(360f, 40f);

        GameObject warnIconObj = new GameObject("WarnTriangleIcon", typeof(RectTransform));
        warnIconObj.transform.SetParent(warnRowObj.transform, false);
        RectTransform wiRt = warnIconObj.GetComponent<RectTransform>();
        wiRt.anchorMin = new Vector2(0.5f, 0.5f);
        wiRt.anchorMax = new Vector2(0.5f, 0.5f);
        wiRt.pivot = new Vector2(0.5f, 0.5f);
        wiRt.anchoredPosition = new Vector2(-132f, 1f);
        wiRt.sizeDelta = new Vector2(34f, 30f);

        _warnIconImg = warnIconObj.AddComponent<Image>();
        _warnIconImg.sprite = GetWarningTriangleSprite();
        _warnIconImg.preserveAspect = true;
        _warnIconImg.color = new Color(0.80f, 0.45f, 0.44f, 1f);
        _warnIconImg.raycastTarget = false;

        GameObject warnTxtObj = new GameObject("WarnText", typeof(RectTransform));
        warnTxtObj.transform.SetParent(warnRowObj.transform, false);
        RectTransform wtRt = warnTxtObj.GetComponent<RectTransform>();
        wtRt.anchorMin = Vector2.zero;
        wtRt.anchorMax = Vector2.one;
        wtRt.offsetMin = new Vector2(44f, 0f);
        wtRt.offsetMax = Vector2.zero;

        _topWarnTMP = warnTxtObj.AddComponent<TextMeshProUGUI>();
        _topWarnTMP.font = font;
        _topWarnTMP.text = "BAĞLANTI KESİLDİ";
        _topWarnTMP.fontSize = 23f;
        _topWarnTMP.fontStyle = FontStyles.Bold;
        _topWarnTMP.characterSpacing = 2.0f;
        _topWarnTMP.alignment = TextAlignmentOptions.Center;
        _topWarnTMP.color = new Color(0.80f, 0.46f, 0.45f, 1f);
        _topWarnTMP.raycastTarget = false;

        // 4. Dev Ana Başlık: "UÇAK DÜŞTÜ"
        GameObject titleObj = new GameObject("MainTitle", typeof(RectTransform));
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.5f, 1f);
        tRt.anchorMax = new Vector2(0.5f, 1f);
        tRt.pivot = new Vector2(0.5f, 0.5f);
        tRt.anchoredPosition = new Vector2(0f, -132f);
        tRt.sizeDelta = new Vector2(740f, 92f);

        _mainTitleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        _mainTitleTMP.font = font;
        _mainTitleTMP.text = "UÇAK DÜŞTÜ";
        _mainTitleTMP.enableAutoSizing = true;
        _mainTitleTMP.fontSizeMin = 42f;
        _mainTitleTMP.fontSizeMax = 74f;
        _mainTitleTMP.fontStyle = FontStyles.Bold;
        _mainTitleTMP.characterSpacing = 2.2f;
        _mainTitleTMP.alignment = TextAlignmentOptions.Center;
        _mainTitleTMP.color = new Color(0.95f, 0.23f, 0.21f, 1f);
        _mainTitleTMP.raycastTarget = false;

        Shadow titleShadow = titleObj.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        titleShadow.effectDistance = new Vector2(0f, -4f);

        // 5. Alt Başlık: "GÖREV SONLANDI"
        GameObject subObj = new GameObject("SubTitle", typeof(RectTransform));
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 1f);
        subRt.anchorMax = new Vector2(0.5f, 1f);
        subRt.pivot = new Vector2(0.5f, 0.5f);
        subRt.anchoredPosition = new Vector2(0f, -198f);
        subRt.sizeDelta = new Vector2(700f, 42f);

        _subTitleTMP = subObj.AddComponent<TextMeshProUGUI>();
        _subTitleTMP.font = font;
        _subTitleTMP.text = "GÖREV SONLANDI";
        _subTitleTMP.fontSize = 26f;
        _subTitleTMP.fontStyle = FontStyles.Bold;
        _subTitleTMP.characterSpacing = 2.0f;
        _subTitleTMP.alignment = TextAlignmentOptions.Center;
        _subTitleTMP.color = new Color(0.69f, 0.75f, 0.82f, 1f);
        _subTitleTMP.raycastTarget = false;

        // 6. Koyu İç 2x2 İstatistik Kutusu (684 x 252)
        GameObject statsBoxObj = new GameObject("StatsGridBox", typeof(RectTransform));
        statsBoxObj.transform.SetParent(cardObj.transform, false);
        RectTransform sbRt = statsBoxObj.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(0.5f, 0.5f);
        sbRt.anchorMax = new Vector2(0.5f, 0.5f);
        sbRt.pivot = new Vector2(0.5f, 0.5f);
        sbRt.anchoredPosition = new Vector2(0f, 44f);
        sbRt.sizeDelta = new Vector2(684f, 252f);

        Image sbImg = statsBoxObj.AddComponent<Image>();
        sbImg.sprite = MainMenuManager.GetRoundedBoxSprite("gameover_stats_inner_box", new Color(0.043f, 0.067f, 0.098f, 0.96f), new Color(0.14f, 0.20f, 0.27f, 0.65f), 16, 1);
        sbImg.type = Image.Type.Sliced;
        sbImg.raycastTarget = false;

        // Yatay & Dikey Ayırıcı Çizgiler (+)
        CreateDividerLine(statsBoxObj.transform, "HorizDivider", new Vector2(636f, 2f));
        CreateDividerLine(statsBoxObj.transform, "VertDivider", new Vector2(2f, 204f));

        // 6A. Sol Üst Hücre: SKOR
        _scoreTMP = CreateStatCellText(statsBoxObj.transform, "ScoreCell",
            new Vector2(0f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, "SKOR: 0", font);

        // 6B. Sağ Üst Hücre: EN İYİ + Altın Nokta
        GameObject bestCellObj = new GameObject("BestScoreCell", typeof(RectTransform));
        bestCellObj.transform.SetParent(statsBoxObj.transform, false);
        RectTransform bcRt = bestCellObj.GetComponent<RectTransform>();
        bcRt.anchorMin = new Vector2(0.5f, 0.5f);
        bcRt.anchorMax = new Vector2(1f, 1f);
        bcRt.offsetMin = new Vector2(12f, 6f);
        bcRt.offsetMax = new Vector2(-34f, -6f);

        _bestScoreTMP = bestCellObj.AddComponent<TextMeshProUGUI>();
        _bestScoreTMP.font = font;
        _bestScoreTMP.enableAutoSizing = true;
        _bestScoreTMP.fontSizeMin = 20f;
        _bestScoreTMP.fontSizeMax = 28f;
        _bestScoreTMP.fontStyle = FontStyles.Bold;
        _bestScoreTMP.alignment = TextAlignmentOptions.Center;
        _bestScoreTMP.textWrappingMode = TextWrappingModes.NoWrap;
        _bestScoreTMP.raycastTarget = false;

        GameObject dotObj = new GameObject("BestGoldDot", typeof(RectTransform));
        dotObj.transform.SetParent(statsBoxObj.transform, false);
        RectTransform dotRt = dotObj.GetComponent<RectTransform>();
        dotRt.anchorMin = new Vector2(1f, 0.75f);
        dotRt.anchorMax = new Vector2(1f, 0.75f);
        dotRt.pivot = new Vector2(0.5f, 0.5f);
        dotRt.anchoredPosition = new Vector2(-34f, 0f);
        dotRt.sizeDelta = new Vector2(16f, 16f);

        _bestDotImg = dotObj.AddComponent<Image>();
        _bestDotImg.sprite = GetGoldDotSprite();
        _bestDotImg.preserveAspect = true;
        _bestDotImg.raycastTarget = false;

        // 6C. Sol Alt Hücre: [Dişli Altın İkonu] ALTIN: +320
        _goldTMP = CreateIconStatCell(statsBoxObj.transform, "GoldCell",
            new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
            GetGoldGearCoinSprite(), iconXOffset: -112f, textLeftPad: 52f, font: font);

        // 6D. Sağ Alt Hücre: [Kırmızı Nişangah İkonu] İMHA: 24
        _killsTMP = CreateIconStatCell(statsBoxObj.transform, "KillsCell",
            new Vector2(0.5f, 0f), new Vector2(1f, 0.5f),
            GetRedCrosshairSprite(), iconXOffset: -94f, textLeftPad: 46f, font: font);

        // 7. Yeşil Ana Buton: [▶ REKLAM İZLE VE DEVAM ET (Canlan + 3 sn Kalkan)]
        _reviveBtnObj = new GameObject("ReviveWatchAdButton", typeof(RectTransform));
        _reviveBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform revRt = _reviveBtnObj.GetComponent<RectTransform>();
        revRt.anchorMin = new Vector2(0.5f, 0.5f);
        revRt.anchorMax = new Vector2(0.5f, 0.5f);
        revRt.pivot = new Vector2(0.5f, 0.5f);
        revRt.anchoredPosition = new Vector2(0f, -184f);
        revRt.sizeDelta = new Vector2(684f, 114f);

        _reviveBtnImg = _reviveBtnObj.AddComponent<Image>();
        _reviveBtnImg.sprite = MainMenuManager.GetRoundedBoxSprite(
            "gameover_btn_revive_green",
            new Color(0.094f, 0.663f, 0.420f, 1f),   // #18A96B zümrüt yeşili
            new Color(0.22f, 0.82f, 0.56f, 0.95f),
            18, 2);
        _reviveBtnImg.type = Image.Type.Sliced;

        _reviveBtn = _reviveBtnObj.AddComponent<Button>();
        _reviveBtn.targetGraphic = _reviveBtnImg;
        _reviveBtn.onClick.AddListener(OnReviveClicked);
        ApplyTactileButtonColors(_reviveBtn, new Color(0.88f, 1f, 0.94f, 1f), new Color(0.72f, 0.92f, 0.82f, 1f));

        // Film-Play İkonu (Sol)
        GameObject filmObj = new GameObject("FilmPlayIcon", typeof(RectTransform));
        filmObj.transform.SetParent(_reviveBtnObj.transform, false);
        RectTransform fmRt = filmObj.GetComponent<RectTransform>();
        fmRt.anchorMin = new Vector2(0.5f, 0.5f);
        fmRt.anchorMax = new Vector2(0.5f, 0.5f);
        fmRt.pivot = new Vector2(0.5f, 0.5f);
        fmRt.anchoredPosition = new Vector2(-222f, 0f);
        fmRt.sizeDelta = new Vector2(62f, 48f);

        _reviveIconImg = filmObj.AddComponent<Image>();
        _reviveIconImg.sprite = GetFilmPlaySprite();
        _reviveIconImg.preserveAspect = true;
        _reviveIconImg.raycastTarget = false;

        // Buton Metni
        GameObject revTxtObj = new GameObject("Text", typeof(RectTransform));
        revTxtObj.transform.SetParent(_reviveBtnObj.transform, false);
        RectTransform rtRt = revTxtObj.GetComponent<RectTransform>();
        rtRt.anchorMin = Vector2.zero;
        rtRt.anchorMax = Vector2.one;
        rtRt.offsetMin = new Vector2(72f, 8f);
        rtRt.offsetMax = new Vector2(-20f, -8f);

        _reviveBtnTMP = revTxtObj.AddComponent<TextMeshProUGUI>();
        _reviveBtnTMP.font = font;
        _reviveBtnTMP.text = "<b>REKLAM İZLE VE DEVAM ET</b>\n<size=72%><color=#E2FCEF>(Canlan + 3 sn Kalkan)</color></size>";
        _reviveBtnTMP.fontSize = 26.5f;
        _reviveBtnTMP.lineSpacing = -4f;
        _reviveBtnTMP.alignment = TextAlignmentOptions.Center;
        _reviveBtnTMP.color = Color.white;
        _reviveBtnTMP.raycastTarget = false;

        // 8. Alt İkili Taktik Butonlar: [↻ TEKRAR DENE] ve [⌂ HANGAR]
        restartButton = CreateSecondaryOutlinedButton(
            cardObj.transform,
            "RestartButton",
            "TEKRAR DENE",
            new Vector2(-178f, -328f),
            new Vector2(328f, 78f),
            GetRefreshArrowSprite(),
            iconOffsetX: -98f,
            font: font,
            onClick: OnRestartClicked);

        _hangarDirectBtn = CreateSecondaryOutlinedButton(
            cardObj.transform,
            "HangarButton",
            "HANGAR",
            new Vector2(178f, -328f),
            new Vector2(328f, 78f),
            GetHangarHomeSprite(),
            iconOffsetX: -76f,
            font: font,
            onClick: OnHangarClicked);

        mainMenuButton = _hangarDirectBtn;
    }

    private static void CreateDividerLine(Transform parent, string name, Vector2 size)
    {
        GameObject lineObj = new GameObject(name, typeof(RectTransform));
        lineObj.transform.SetParent(parent, false);
        RectTransform rt = lineObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Image img = lineObj.AddComponent<Image>();
        img.color = new Color(0.20f, 0.27f, 0.36f, 0.85f);
        img.raycastTarget = false;
    }

    private static TextMeshProUGUI CreateStatCellText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetX, string defaultText, TMP_FontAsset font)
    {
        GameObject cellObj = new GameObject(name, typeof(RectTransform));
        cellObj.transform.SetParent(parent, false);
        RectTransform rt = cellObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(14f + offsetX.x, 6f);
        rt.offsetMax = new Vector2(-14f + offsetX.y, -6f);

        TextMeshProUGUI tmp = cellObj.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = defaultText;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 20f;
        tmp.fontSizeMax = 28f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static TextMeshProUGUI CreateIconStatCell(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Sprite iconSprite, float iconXOffset, float textLeftPad, TMP_FontAsset font)
    {
        GameObject cellObj = new GameObject(name, typeof(RectTransform));
        cellObj.transform.SetParent(parent, false);
        RectTransform rt = cellObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(cellObj.transform, false);
        RectTransform iRt = iconObj.GetComponent<RectTransform>();
        iRt.anchorMin = new Vector2(0.5f, 0.5f);
        iRt.anchorMax = new Vector2(0.5f, 0.5f);
        iRt.pivot = new Vector2(0.5f, 0.5f);
        iRt.anchoredPosition = new Vector2(iconXOffset, 0f);
        iRt.sizeDelta = new Vector2(40f, 40f);

        Image iImg = iconObj.AddComponent<Image>();
        iImg.sprite = iconSprite;
        iImg.preserveAspect = true;
        iImg.raycastTarget = false;

        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(cellObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(textLeftPad, 6f);
        tRt.offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 20f;
        tmp.fontSizeMax = 28f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateSecondaryOutlinedButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Sprite icon, float iconOffsetX, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = btnObj.AddComponent<Image>();
        bg.sprite = MainMenuManager.GetRoundedBoxSprite(
            "gameover_btn_secondary_slate",
            new Color(0.106f, 0.157f, 0.216f, 0.96f), // #1B2837
            new Color(0.353f, 0.459f, 0.565f, 0.92f), // #5A7590 çelik mavi çerçeve
            10, 2);
        bg.type = Image.Type.Sliced;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);
        ApplyTactileButtonColors(btn, new Color(0.85f, 0.94f, 1f, 1f), new Color(0.68f, 0.80f, 0.92f, 1f));

        if (icon != null)
        {
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iRt = iconObj.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0.5f, 0.5f);
            iRt.anchorMax = new Vector2(0.5f, 0.5f);
            iRt.pivot = new Vector2(0.5f, 0.5f);
            iRt.anchoredPosition = new Vector2(iconOffsetX, 0f);
            iRt.sizeDelta = new Vector2(34f, 34f);

            Image iImg = iconObj.AddComponent<Image>();
            iImg.sprite = icon;
            iImg.preserveAspect = true;
            iImg.color = new Color(0.58f, 0.70f, 0.82f, 1f);
            iImg.raycastTarget = false;
        }

        GameObject txtObj = new GameObject("Label", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(44f, 4f);
        tRt.offsetMax = new Vector2(-12f, -4f);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.91f, 0.96f, 1f);
        tmp.raycastTarget = false;

        return btn;
    }

    private static void ApplyTactileButtonColors(Button btn, Color highlighted, Color pressed)
    {
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = highlighted;
        cb.pressedColor = pressed;
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.65f);
        btn.colors = cb;
    }

    // =========================================================================
    // YÜKSEK ÇÖZÜNÜRLÜKLÜ PROSEDÜREL VEKTÖR İKONLAR (SIFIR KIRILMA / SIFIR KUTUCUK)
    // =========================================================================

    private static Texture2D CreateBlankTex(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] clear = new Color[w * h];
        tex.SetPixels(clear);
        return tex;
    }

    private static Sprite GetGoldDotSprite()
    {
        if (_sprGoldDot != null) return _sprGoldDot;
        int s = 32;
        Texture2D tex = CreateBlankTex(s, s);
        Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                if (d <= 9.5f)
                {
                    float a = Mathf.Clamp01((9.5f - d) * 1.4f);
                    tex.SetPixel(x, y, new Color(1f, 0.82f, 0.16f, a));
                }
                else if (d <= 15f)
                {
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - (d - 9.5f) / 5.5f), 2f) * 0.45f;
                    tex.SetPixel(x, y, new Color(1f, 0.85f, 0.25f, glow));
                }
            }
        }
        tex.Apply();
        _sprGoldDot = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _sprGoldDot;
    }

    private static Sprite GetWarningTriangleSprite()
    {
        if (_sprWarnTriangle != null) return _sprWarnTriangle;
        int s = 64;
        Texture2D tex = CreateBlankTex(s, s);
        float cx = (s - 1) * 0.5f;

        for (int y = 6; y <= 56; y++)
        {
            float t = (56f - y) / 50f; // 0 (tepe) -> 1 (taban)
            float halfW = Mathf.Lerp(2.2f, 27f, t);
            for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Abs(x - cx);
                if (dx <= halfW)
                {
                    float edge = halfW - dx;
                    float alpha = Mathf.Clamp01(edge * 1.3f);
                    // Ortadaki ünlem (!) boşluğu
                    bool isExclamBar = (dx <= 2.7f && y >= 23 && y <= 41);
                    bool isExclamDot = (Vector2.Distance(new Vector2(x, y), new Vector2(cx, 15.5f)) <= 3.1f);
                    if (!isExclamBar && !isExclamDot)
                    {
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
            }
        }
        tex.Apply();
        _sprWarnTriangle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _sprWarnTriangle;
    }

    private static Sprite GetGoldGearCoinSprite()
    {
        if (_sprGoldGearCoin != null) return _sprGoldGearCoin;
        int s = 64;
        Texture2D tex = CreateBlankTex(s, s);
        Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);

        Color brightGold = new Color(1f, 0.82f, 0.22f, 1f);
        Color warmAmber = new Color(0.92f, 0.60f, 0.10f, 1f);
        Color darkWell = new Color(0.32f, 0.18f, 0.04f, 1f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x, y) - c;
                float d = p.magnitude;
                if (d > 29.5f) continue;

                float outerA = Mathf.Clamp01((29.5f - d) * 1.4f);
                Color col;

                if (d >= 22.5f)
                {
                    // Dış altın sikke halkası
                    float bevel = Mathf.Clamp01((y - x * 0.3f + s * 0.5f) / s);
                    col = Color.Lerp(warmAmber, brightGold, bevel);
                }
                else
                {
                    // İç koyu yuva + 8 dişli altın çark (referans görseldekiyle birebir)
                    float angle = Mathf.Atan2(p.y, p.x);
                    float toothWave = Mathf.Cos(angle * 8f);
                    float gearOuter = 16.2f + (toothWave > 0.2f ? 3.2f : 0f);

                    if (d <= gearOuter && d >= 6.2f)
                        col = brightGold;
                    else
                        col = darkWell;
                }

                col.a = outerA;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        _sprGoldGearCoin = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _sprGoldGearCoin;
    }

    private static Sprite GetRedCrosshairSprite()
    {
        if (_sprRedCrosshair != null) return _sprRedCrosshair;
        int s = 64;
        Texture2D tex = CreateBlankTex(s, s);
        Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
        Color red = new Color(0.92f, 0.28f, 0.28f, 1f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x, y) - c;
                float d = p.magnitude;
                float ringDist = Mathf.Abs(d - 19.5f);
                float alpha = 0f;

                // Halka
                if (ringDist <= 2.1f)
                    alpha = Mathf.Max(alpha, Mathf.Clamp01((2.1f - ringDist) * 1.5f));

                // Artı (+) nişangah kolları (ortada hafif boşluklu taktik retikül)
                bool onAxis = (Mathf.Abs(p.x) <= 1.8f && Mathf.Abs(p.y) >= 5.5f && Mathf.Abs(p.y) <= 27.5f) ||
                              (Mathf.Abs(p.y) <= 1.8f && Mathf.Abs(p.x) >= 5.5f && Mathf.Abs(p.x) <= 27.5f);
                if (onAxis)
                    alpha = Mathf.Max(alpha, 0.98f);

                // Merkez nokta
                if (d <= 2.6f)
                    alpha = Mathf.Max(alpha, Mathf.Clamp01((2.6f - d) * 1.5f));

                if (alpha > 0.01f)
                    tex.SetPixel(x, y, new Color(red.r, red.g, red.b, alpha));
            }
        }
        tex.Apply();
        _sprRedCrosshair = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _sprRedCrosshair;
    }

    private static Sprite GetFilmPlaySprite()
    {
        if (_sprFilmPlay != null) return _sprFilmPlay;
        int w = 80;
        int h = 62;
        Texture2D tex = CreateBlankTex(w, h);
        float r = 9f;

        for (int y = 2; y < h - 2; y++)
        {
            for (int x = 2; x < w - 2; x++)
            {
                float cx = Mathf.Clamp(x, 2 + r, w - 3 - r);
                float cy = Mathf.Clamp(y, 2 + r, h - 3 - r);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                if (d > r + 0.8f) continue;

                float alpha = Mathf.Clamp01((r + 0.8f - d) * 1.4f);

                // Üst ve alt film şeridi delikleri (5'er adet)
                bool isSprocket = false;
                if ((y >= 6 && y <= 12) || (y >= h - 13 && y <= h - 7))
                {
                    int relX = x - 10;
                    if (relX >= 0 && relX < 60 && (relX % 13) <= 6)
                        isSprocket = true;
                }

                // Ortadaki Play (▶) üçgeni boşluğu
                bool isPlayTri = false;
                if (x >= 31 && x <= 51)
                {
                    float pt = (x - 31f) / 20f; // 0 (sol taban) -> 1 (sağ uç)
                    float halfH = Mathf.Lerp(11.5f, 0.5f, pt);
                    if (Mathf.Abs(y - (h * 0.5f)) <= halfH)
                        isPlayTri = true;
                }

                if (!isSprocket && !isPlayTri)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        tex.Apply();
        _sprFilmPlay = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return _sprFilmPlay;
    }

    private static Sprite GetRefreshArrowSprite()
    {
        if (_sprRefreshArrow != null) return _sprRefreshArrow;
        int s = 64;
        Texture2D tex = CreateBlankTex(s, s);
        Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x, y) - c;
                float d = p.magnitude;
                float angleDeg = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg; // -180..180

                // Sağ üstte 50 derecelik açıklık bırakan dairesel yay
                bool inGap = (angleDeg > 5f && angleDeg < 55f);
                float ringDist = Mathf.Abs(d - 20f);
                if (!inGap && ringDist <= 2.6f)
                {
                    float a = Mathf.Clamp01((2.6f - ringDist) * 1.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

                // Ok başı (sağ üst uçta: angle ~ 55 derece civarı)
                Vector2 tip = c + new Vector2(17.5f, 11.5f);
                Vector2 v = new Vector2(x, y) - tip;
                // Saat yönü ok ucu kanatları
                float seg1 = DistanceToSegment(new Vector2(x, y), tip, tip + new Vector2(-11f, 2f));
                float seg2 = DistanceToSegment(new Vector2(x, y), tip, tip + new Vector2(-1f, 11f));
                float minSeg = Mathf.Min(seg1, seg2);
                if (minSeg <= 2.5f)
                {
                    float a = Mathf.Clamp01((2.5f - minSeg) * 1.4f);
                    Color cur = tex.GetPixel(x, y);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(cur.a, a)));
                }
            }
        }
        tex.Apply();
        _sprRefreshArrow = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _sprRefreshArrow;
    }

    private static Sprite GetHangarHomeSprite()
    {
        if (_sprHangarHome != null) return _sprHangarHome;
        int s = 64;
        Texture2D tex = CreateBlankTex(s, s);

        Vector2 roofTop = new Vector2(32f, 54f);
        Vector2 roofL = new Vector2(8f, 33f);
        Vector2 roofR = new Vector2(56f, 33f);

        Vector2 wallTL = new Vector2(15f, 35f);
        Vector2 wallBL = new Vector2(15f, 11f);
        Vector2 wallBR = new Vector2(49f, 11f);
        Vector2 wallTR = new Vector2(49f, 35f);

        Vector2 doorBL = new Vector2(26f, 11f);
        Vector2 doorTL = new Vector2(26f, 25f);
        Vector2 doorTR = new Vector2(38f, 25f);
        Vector2 doorBR = new Vector2(38f, 11f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Vector2 pt = new Vector2(x, y);
                float d = Mathf.Min(
                    DistanceToSegment(pt, roofL, roofTop),
                    DistanceToSegment(pt, roofTop, roofR)
                );
                d = Mathf.Min(d, DistanceToSegment(pt, wallTL, wallBL));
                d = Mathf.Min(d, DistanceToSegment(pt, wallTR, wallBR));
                d = Mathf.Min(d, DistanceToSegment(pt, wallBL, doorBL));
                d = Mathf.Min(d, DistanceToSegment(pt, doorBR, wallBR));
                d = Mathf.Min(d, DistanceToSegment(pt, doorBL, doorTL));
                d = Mathf.Min(d, DistanceToSegment(pt, doorTL, doorTR));
                d = Mathf.Min(d, DistanceToSegment(pt, doorTR, doorBR));

                if (d <= 2.3f)
                {
                    float a = Mathf.Clamp01((2.3f - d) * 1.45f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
        }
        tex.Apply();
        _sprHangarHome = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _sprHangarHome;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        return Vector2.Distance(p, a + ab * t);
    }
}
