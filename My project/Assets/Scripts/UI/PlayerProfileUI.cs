using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Askeri Pilot Profil Arayüzü:
/// - Ana Menüde sol üstte seviye, rütbe rozeti ve canlı EXP barı gösterir.
/// - Savaş sahnesinde sağ üstte altın sayacının hemen altında mini seviye ve EXP barı gösterir.
/// - Tıklandığında detaylı Pilot Kimlik Kartı ve Hizmet Brövesi penceresini açar.
/// </summary>
public class PlayerProfileUI : MonoBehaviour
{
    public static PlayerProfileUI Instance { get; private set; }

    // Ana Menü & Tüm Menüler Rozeti (Sol Üst)
    private GameObject _statusBarRoot;
    private GameObject _badgeRoot;
    private TextMeshProUGUI _rankTitleTMP;
    private TextMeshProUGUI _levelBadgeTMP;
    private TextMeshProUGUI _expBarTMP;
    private Image _expBarFill;

    // Tüm Menüler Altın Rozeti (Sağ Üst)
    private GameObject _goldBadgeRoot;
    private TextMeshProUGUI _goldBadgeTMP;

    // Savaş İçi HUD Rozeti & EXP Barı
    private GameObject _hudBadgeRoot;
    private TextMeshProUGUI _hudLevelTMP;
    private TextMeshProUGUI _hudExpTMP;
    private Image _hudExpFill;

    // Detaylı Askeri Pilot Dosyası (Modal)
    private GameObject _dossierModal;
    private RectTransform _dossierCardRt;
    private TextMeshProUGUI _dossierStatsTMP;
    private TextMeshProUGUI _dossierRankTMP;
    private TextMeshProUGUI _dossierPerksTMP;

    private static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static Sprite _cachedJetHeaderSprite;
    private static Sprite _cachedInnerChamferFrameSprite;
    private static readonly Dictionary<string, Sprite> _dossierSpriteCache = new Dictionary<string, Sprite>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    public static PlayerProfileUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("PlayerProfileUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("PlayerProfileUI", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        Instance = go.GetComponent<PlayerProfileUI>();
        if (Instance == null) Instance = go.AddComponent<PlayerProfileUI>();

        return Instance;
    }

    private void Start()
    {
        GameSettings.LoadSettings();
        GameSettings.OnCockpitThemeChanged += HandleCockpitThemeChanged;

        PlayerProfileManager pm = PlayerProfileManager.EnsureInstance();
        pm.OnLevelUp += HandleLevelUp;
        pm.OnExpAdded += HandleExpAdded;
        RefreshBadgeVisuals();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        GameSettings.OnCockpitThemeChanged -= HandleCockpitThemeChanged;

        if (PlayerProfileManager.Instance != null)
        {
            PlayerProfileManager.Instance.OnLevelUp -= HandleLevelUp;
            PlayerProfileManager.Instance.OnExpAdded -= HandleExpAdded;
        }
    }

    private void HandleCockpitThemeChanged(GameSettings.CockpitTheme _)
    {
        RefreshBadgeVisuals();
    }

    private void HandleLevelUp(int newLevel, int goldReward)
    {
        RefreshBadgeVisuals();

        var gameUI = FindObjectOfType<GameUI>();
        string rankTitle = PlayerProfileManager.GetRankTitle(newLevel);
        string msg = $"<b>[TERFİ] RÜTBE YÜKSELDİ! SEVİYE {newLevel}</b>\n<color=#F1C40F>YENİ RÜTBE: {rankTitle.ToUpper()} (+{goldReward}G)</color>";
        gameUI?.ShowWaveAlert(msg, new Color(1f, 0.85f, 0.2f), 3.0f);
    }

    private void HandleExpAdded(int amount, string reason)
    {
        RefreshBadgeVisuals();
    }

    /// <summary>
    /// Tüm menülerde (Ana Menü, Hangar, Mağaza, Modlar, Ayarlar, Sandıklar, Envanter, Rekor)
    /// ekranın sol üstünde Pilot Seviye & EXP Barı, sağ üstünde Altın Göstergesi oluşturan genel durum barı.
    /// </summary>
    public void SetupMenuStatusBar(Canvas canvas = null)
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        if (_statusBarRoot != null)
        {
            _statusBarRoot.SetActive(true);
            _statusBarRoot.transform.SetAsLastSibling();
            RefreshBadgeVisuals();
            return;
        }

        Transform existing = canvas.transform.Find("GlobalMenuStatusBar");
        if (existing != null)
        {
            _statusBarRoot = existing.gameObject;
            _statusBarRoot.SetActive(true);
            _statusBarRoot.transform.SetAsLastSibling();
            RefreshBadgeVisuals();
            return;
        }

        _statusBarRoot = new GameObject("GlobalMenuStatusBar", typeof(RectTransform));
        _statusBarRoot.transform.SetParent(canvas.transform, false);

        RectTransform sbRt = _statusBarRoot.GetComponent<RectTransform>();
        MainMenuManager.MakeFullScreenRect(sbRt);

        // 1. Sol Üst: Pilot Profil Rozeti & Canlı EXP Barı
        BuildProfileLevelBadge(_statusBarRoot.transform);

        // 2. Sağ Üst: Altın Kasası Rozeti
        BuildMenuGoldBadge(_statusBarRoot.transform);
        if (_goldBadgeRoot != null)
            _goldBadgeRoot.SetActive(_isGoldBadgeVisible);

        _statusBarRoot.transform.SetAsLastSibling();
        RefreshBadgeVisuals();
    }

    private bool _isGoldBadgeVisible = true;

    public void ShowMenuStatusBar(bool showGoldBadge = true)
    {
        _isGoldBadgeVisible = showGoldBadge;

        if (_statusBarRoot == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas != null) SetupMenuStatusBar(canvas);
        }

        if (_statusBarRoot != null)
        {
            _statusBarRoot.SetActive(true);
            _statusBarRoot.transform.SetAsLastSibling();
            Transform loginModal = _statusBarRoot.transform.parent != null ? _statusBarRoot.transform.parent.Find("PilotLoginModalRoot") : null;
            if (loginModal != null && loginModal.gameObject.activeSelf)
                loginModal.SetAsLastSibling();
            SetGoldBadgeVisible(_isGoldBadgeVisible);
            RefreshBadgeVisuals();
        }
    }

    public void SetGoldBadgeVisible(bool visible)
    {
        _isGoldBadgeVisible = visible;
        if (_goldBadgeRoot != null)
            _goldBadgeRoot.SetActive(visible);
    }

    public void HideMenuStatusBar()
    {
        if (_statusBarRoot != null)
            _statusBarRoot.SetActive(false);
    }

    public void SetupMainMenuProfileBadge(Transform parent)
    {
        Canvas canvas = (parent != null) ? parent.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        SetupMenuStatusBar(canvas);
    }

    private void BuildProfileLevelBadge(Transform parent)
    {
        if (_badgeRoot != null) return;

        _badgeRoot = new GameObject("PilotProfileBadge", typeof(RectTransform));
        _badgeRoot.transform.SetParent(parent, false);

        RectTransform bRt = _badgeRoot.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 1f);
        bRt.anchorMax = new Vector2(0f, 1f);
        bRt.pivot = new Vector2(0f, 1f);
        bRt.anchoredPosition = new Vector2(24f, -14f);
        bRt.sizeDelta = new Vector2(490f, 78f);

        Image bg = _badgeRoot.AddComponent<Image>();
        bg.sprite = MainMenuManager.GetRoundedBoxSprite("profile_badge_card", new Color(0.04f, 0.08f, 0.14f, 0.96f), new Color(0f, 0.85f, 1f, 0.85f), 12, 2);
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        Button badgeBtn = _badgeRoot.AddComponent<Button>();
        badgeBtn.targetGraphic = bg;
        badgeBtn.onClick.AddListener(OpenDossierModal);

        // Seviye Rozeti Kutusu (Sol Taraf - Büyük ve Net)
        GameObject lvlObj = new GameObject("LevelBox", typeof(RectTransform));
        lvlObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform lRt = lvlObj.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0.5f);
        lRt.anchorMax = new Vector2(0f, 0.5f);
        lRt.pivot = new Vector2(0f, 0.5f);
        lRt.anchoredPosition = new Vector2(10f, 0f);
        lRt.sizeDelta = new Vector2(82f, 60f);

        Image lBg = lvlObj.AddComponent<Image>();
        lBg.sprite = MainMenuManager.GetRoundedBoxSprite("profile_lvl_box", new Color(0.08f, 0.14f, 0.22f, 1f), new Color(1f, 0.82f, 0.22f, 0.95f), 10, 2);
        lBg.type = Image.Type.Sliced;
        lBg.color = Color.white;

        GameObject lTxtObj = new GameObject("Text", typeof(RectTransform));
        lTxtObj.transform.SetParent(lvlObj.transform, false);
        RectTransform ltRt = lTxtObj.GetComponent<RectTransform>();
        ltRt.anchorMin = Vector2.zero;
        ltRt.anchorMax = Vector2.one;
        ltRt.offsetMin = Vector2.zero;
        ltRt.offsetMax = Vector2.zero;

        _levelBadgeTMP = lTxtObj.AddComponent<TextMeshProUGUI>();
        _levelBadgeTMP.font = MainMenuManager.GetTacticalFont();
        _levelBadgeTMP.fontSize = 22f;
        _levelBadgeTMP.fontStyle = FontStyles.Bold;
        _levelBadgeTMP.characterSpacing = 0.8f;
        _levelBadgeTMP.lineSpacing = -6f;
        _levelBadgeTMP.alignment = TextAlignmentOptions.Center;
        _levelBadgeTMP.color = new Color(1f, 0.90f, 0.28f);
        _levelBadgeTMP.raycastTarget = false;

        // Pilot Adı & Rütbe Başlığı (Üst Satır)
        GameObject rObj = new GameObject("RankTitle", typeof(RectTransform));
        rObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform rRt = rObj.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 1f);
        rRt.anchorMax = new Vector2(0f, 1f);
        rRt.pivot = new Vector2(0f, 1f);
        rRt.anchoredPosition = new Vector2(104f, -8f);
        rRt.sizeDelta = new Vector2(280f, 28f);

        _rankTitleTMP = rObj.AddComponent<TextMeshProUGUI>();
        _rankTitleTMP.font = MainMenuManager.GetTacticalFont();
        _rankTitleTMP.fontSize = 19f;
        _rankTitleTMP.fontStyle = FontStyles.Bold;
        _rankTitleTMP.characterSpacing = 1.1f;
        _rankTitleTMP.alignment = TextAlignmentOptions.MidlineLeft;
        _rankTitleTMP.color = Color.white;
        _rankTitleTMP.raycastTarget = false;
        _rankTitleTMP.enableAutoSizing = true;
        _rankTitleTMP.fontSizeMin = 13.5f;
        _rankTitleTMP.fontSizeMax = 19.5f;

        // Sağ Üst Mini "PROFİL" Etiketi (Tıklanabilir profil/değiştirme ipucu)
        GameObject switchHintObj = new GameObject("SwitchHintTag", typeof(RectTransform));
        switchHintObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform shRt = switchHintObj.GetComponent<RectTransform>();
        shRt.anchorMin = new Vector2(1f, 1f);
        shRt.anchorMax = new Vector2(1f, 1f);
        shRt.pivot = new Vector2(1f, 1f);
        shRt.anchoredPosition = new Vector2(-12f, -9f);
        shRt.sizeDelta = new Vector2(88f, 24f);

        Image shBg = switchHintObj.AddComponent<Image>();
        shBg.sprite = MainMenuManager.GetRoundedBoxSprite("profile_switch_hint_pill", new Color(0.06f, 0.16f, 0.25f, 0.95f), new Color(0f, 0.85f, 1f, 0.85f), 6, 1);
        shBg.type = Image.Type.Sliced;
        shBg.color = Color.white;
        shBg.raycastTarget = false;

        GameObject shTxtObj = new GameObject("Text", typeof(RectTransform));
        shTxtObj.transform.SetParent(switchHintObj.transform, false);
        RectTransform shtRt = shTxtObj.GetComponent<RectTransform>();
        shtRt.anchorMin = Vector2.zero;
        shtRt.anchorMax = Vector2.one;
        shtRt.offsetMin = Vector2.zero;
        shtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI shTMP = shTxtObj.AddComponent<TextMeshProUGUI>();
        shTMP.font = MainMenuManager.GetTacticalFont();
        shTMP.text = "PROFİL";
        shTMP.fontSize = 12f;
        shTMP.fontStyle = FontStyles.Bold;
        shTMP.characterSpacing = 1.2f;
        shTMP.alignment = TextAlignmentOptions.Center;
        shTMP.color = new Color(0.55f, 0.94f, 1f, 1f);
        shTMP.raycastTarget = false;

        // EXP Bar Çerçevesi (Alt Satır - Geniş ve Yüksek Okunaklı Bar)
        GameObject barBgObj = new GameObject("ExpBarBg", typeof(RectTransform));
        barBgObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform barRt = barBgObj.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(0f, 0f);
        barRt.pivot = new Vector2(0f, 0f);
        barRt.anchoredPosition = new Vector2(104f, 9f);
        barRt.sizeDelta = new Vector2(374f, 28f);

        Image barBg = barBgObj.AddComponent<Image>();
        barBg.sprite = MainMenuManager.GetRoundedBoxSprite("profile_exp_bg", new Color(0.02f, 0.05f, 0.09f, 1f), new Color(0.16f, 0.28f, 0.40f, 1f), 6, 1);
        barBg.type = Image.Type.Sliced;
        barBg.color = Color.white;

        // EXP Bar Dolgu
        GameObject fillObj = new GameObject("ExpBarFill", typeof(RectTransform));
        fillObj.transform.SetParent(barBgObj.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);

        _expBarFill = fillObj.AddComponent<Image>();
        _expBarFill.color = new Color(0.04f, 0.72f, 0.96f, 0.96f);
        _expBarFill.type = Image.Type.Filled;
        _expBarFill.fillMethod = Image.FillMethod.Horizontal;
        _expBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        // EXP Bar Yazısı (Büyük ve Kontrastlı)
        GameObject barTxtObj = new GameObject("ExpText", typeof(RectTransform));
        barTxtObj.transform.SetParent(barBgObj.transform, false);
        RectTransform btRt = barTxtObj.GetComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        _expBarTMP = barTxtObj.AddComponent<TextMeshProUGUI>();
        _expBarTMP.font = MainMenuManager.GetTacticalFont();
        _expBarTMP.fontSize = 16.5f;
        _expBarTMP.fontStyle = FontStyles.Bold;
        _expBarTMP.characterSpacing = 1.0f;
        _expBarTMP.alignment = TextAlignmentOptions.Center;
        _expBarTMP.color = Color.white;
        _expBarTMP.raycastTarget = false;
        MainMenuManager.SetOutlineSafe(_expBarTMP, 0.16f, new Color(0.01f, 0.03f, 0.07f, 0.98f));
    }

    private void BuildMenuGoldBadge(Transform parent)
    {
        if (_goldBadgeRoot != null) return;

        _goldBadgeRoot = new GameObject("MenuGoldBadge", typeof(RectTransform));
        _goldBadgeRoot.transform.SetParent(parent, false);

        RectTransform gRt = _goldBadgeRoot.GetComponent<RectTransform>();
        gRt.anchorMin = new Vector2(1f, 1f);
        gRt.anchorMax = new Vector2(1f, 1f);
        gRt.pivot = new Vector2(1f, 1f);
        gRt.anchoredPosition = new Vector2(-24f, -14f);
        gRt.sizeDelta = new Vector2(240f, 52f);

        Image gBg = _goldBadgeRoot.AddComponent<Image>();
        gBg.sprite = MainMenuManager.GetRoundedBoxSprite("profile_gold_card", new Color(0.06f, 0.08f, 0.12f, 0.96f), new Color(1f, 0.82f, 0.22f, 0.92f), 10, 2);
        gBg.type = Image.Type.Sliced;
        gBg.color = Color.white;

        Button gBtn = _goldBadgeRoot.AddComponent<Button>();
        gBtn.targetGraphic = gBg;
        gBtn.onClick.AddListener(() =>
        {
            if (MainMenuManager.Instance != null && MainMenuManager.Instance.mainPanel != null && MainMenuManager.Instance.mainPanel.activeSelf)
            {
                MainMenuManager.Instance.OpenStorePanel();
            }
        });

        GameObject goldTxtObj = new GameObject("Text", typeof(RectTransform));
        goldTxtObj.transform.SetParent(_goldBadgeRoot.transform, false);
        RectTransform gtRt = goldTxtObj.GetComponent<RectTransform>();
        gtRt.anchorMin = Vector2.zero;
        gtRt.anchorMax = Vector2.one;
        gtRt.offsetMin = Vector2.zero;
        gtRt.offsetMax = Vector2.zero;

        _goldBadgeTMP = goldTxtObj.AddComponent<TextMeshProUGUI>();
        _goldBadgeTMP.font = MainMenuManager.GetTacticalFont();
        _goldBadgeTMP.fontSize = 20f;
        _goldBadgeTMP.fontStyle = FontStyles.Bold;
        _goldBadgeTMP.characterSpacing = 1.4f;
        _goldBadgeTMP.alignment = TextAlignmentOptions.Center;
        _goldBadgeTMP.color = new Color(1f, 0.88f, 0.25f);
        _goldBadgeTMP.raycastTarget = false;
    }

    /// <summary>
    /// Savaş sahnesinde seviye barı gösterilmez (yalnızca Altın barı ve Pause tuşu gösterilir).
    /// </summary>
    public void SetupHUDProfileBadge(Transform canvasTransform)
    {
        if (canvasTransform != null)
        {
            Transform existing = canvasTransform.Find("HUD_PilotProfileBadge");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }
        if (_hudBadgeRoot != null)
        {
            Destroy(_hudBadgeRoot);
            _hudBadgeRoot = null;
        }
    }

    private void BuildHUDProfileBadgeInternal(Transform canvasTransform)
    {
        if (_hudBadgeRoot != null)
        {
            _hudBadgeRoot.SetActive(false);
            return;
        }

        Transform existing = canvasTransform.Find("HUD_PilotProfileBadge");
        _hudBadgeRoot = existing != null ? existing.gameObject : new GameObject("HUD_PilotProfileBadge", typeof(RectTransform));
        _hudBadgeRoot.transform.SetParent(canvasTransform, false);

        RectTransform rt = _hudBadgeRoot.GetComponent<RectTransform>();
        if (rt != null)
        {
            // Sağ üst köşe, Altın kutusunun hemen altı (Kompakt, hizalı ve Pause tuşundan bağımsız)
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-95f, -68f);
            rt.sizeDelta = new Vector2(210f, 34f);
        }

        Image bg = _hudBadgeRoot.GetComponent<Image>() ?? _hudBadgeRoot.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.14f, 0.90f);

        Outline outline = _hudBadgeRoot.GetComponent<Outline>() ?? _hudBadgeRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.8f);
        outline.effectDistance = new Vector2(2.2f, -2.2f);

        // Seviye Kutusu (Sol)
        Transform lvlChild = _hudBadgeRoot.transform.Find("LvlTxt");
        GameObject lvlObj = lvlChild != null ? lvlChild.gameObject : new GameObject("LvlTxt", typeof(RectTransform));
        lvlObj.transform.SetParent(_hudBadgeRoot.transform, false);

        RectTransform lRt = lvlObj.GetComponent<RectTransform>();
        if (lRt != null)
        {
            lRt.anchorMin = new Vector2(0f, 0f);
            lRt.anchorMax = new Vector2(0.32f, 1f);
            lRt.offsetMin = new Vector2(2f, 1f);
            lRt.offsetMax = new Vector2(-1f, -1f);
        }

        _hudLevelTMP = lvlObj.GetComponent<TextMeshProUGUI>() ?? lvlObj.AddComponent<TextMeshProUGUI>();
        _hudLevelTMP.fontSize = 18f;
        _hudLevelTMP.fontStyle = FontStyles.Bold;
        _hudLevelTMP.alignment = TextAlignmentOptions.Center;
        _hudLevelTMP.color = new Color(1f, 0.85f, 0.2f);
        _hudLevelTMP.raycastTarget = false;

        // EXP Bar Dolgu Arka Planı (Sağ)
        Transform expBgChild = _hudBadgeRoot.transform.Find("ExpBarBg");
        GameObject expBgObj = expBgChild != null ? expBgChild.gameObject : new GameObject("ExpBarBg", typeof(RectTransform));
        expBgObj.transform.SetParent(_hudBadgeRoot.transform, false);

        RectTransform ebRt = expBgObj.GetComponent<RectTransform>();
        if (ebRt != null)
        {
            ebRt.anchorMin = new Vector2(0.32f, 0f);
            ebRt.anchorMax = new Vector2(1f, 1f);
            ebRt.offsetMin = new Vector2(1f, 2f);
            ebRt.offsetMax = new Vector2(-2f, -2f);
        }

        Image ebImg = expBgObj.GetComponent<Image>() ?? expBgObj.AddComponent<Image>();
        ebImg.color = new Color(0.02f, 0.04f, 0.08f, 1f);

        // EXP Bar İçi (Filled)
        Transform fillChild = expBgObj.transform.Find("Fill");
        GameObject fillObj = fillChild != null ? fillChild.gameObject : new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(expBgObj.transform, false);

        RectTransform fRt = fillObj.GetComponent<RectTransform>();
        if (fRt != null)
        {
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
        }

        _hudExpFill = fillObj.GetComponent<Image>() ?? fillObj.AddComponent<Image>();
        _hudExpFill.color = new Color(0f, 0.85f, 1f, 0.95f);
        _hudExpFill.type = Image.Type.Filled;
        _hudExpFill.fillMethod = Image.FillMethod.Horizontal;
        _hudExpFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        // EXP Bar Yazısı
        Transform txtChild = expBgObj.transform.Find("Text");
        GameObject txtObj = txtChild != null ? txtChild.gameObject : new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(expBgObj.transform, false);

        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        if (tRt != null)
        {
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
        }

        _hudExpTMP = txtObj.GetComponent<TextMeshProUGUI>();
        if (_hudExpTMP == null) _hudExpTMP = txtObj.AddComponent<TextMeshProUGUI>();
        _hudExpTMP.fontSize = 20f;
        _hudExpTMP.fontStyle = FontStyles.Bold;
        _hudExpTMP.alignment = TextAlignmentOptions.Center;
        _hudExpTMP.color = Color.white;
        _hudExpTMP.raycastTarget = false;

        RefreshBadgeVisuals();
    }

    public void RefreshBadgeVisuals()
    {
        PlayerProfileManager pm = PlayerProfileManager.EnsureInstance();
        if (pm == null) return;

        // 1. Ana Menü Rozeti
        if (_levelBadgeTMP != null)
            _levelBadgeTMP.text = $"<size=52%><color=#93C5FD>SEVİYE</color></size>\n<b>{pm.level}</b>";

        if (_rankTitleTMP != null)
        {
            Color c = PlayerProfileManager.GetRankColor(pm.level);
            string callsign = LeaderboardManager.PlayerCallsign;
            _rankTitleTMP.text = $"<color=#00E5FF><b>[{callsign}]</b></color> • <color=#{ColorUtility.ToHtmlStringRGB(c)}><b>{pm.RankTitle.ToUpper()}</b></color>";
        }

        if (_expBarFill != null)
        {
            _expBarFill.fillAmount = pm.ExpProgress;
            _expBarFill.color = GameSettings.SelectedCockpitTheme == GameSettings.CockpitTheme.CyberBlue
                ? new Color(0.04f, 0.72f, 0.96f, 0.96f)
                : GameSettings.GetCockpitHpBarColor();
        }

        if (_badgeRoot != null)
        {
            Image badgeBg = _badgeRoot.GetComponent<Image>();
            if (badgeBg != null)
            {
                var theme = GameSettings.SelectedCockpitTheme;
                string cardKey = theme == GameSettings.CockpitTheme.CyberBlue
                    ? "profile_badge_card"
                    : $"profile_badge_card_{(int)theme}";
                Color borderCol = GameSettings.GetCockpitAccentBorderColor(0.85f);
                badgeBg.sprite = MainMenuManager.GetRoundedBoxSprite(cardKey, new Color(0.04f, 0.08f, 0.14f, 0.96f), borderCol, 12, 2);
            }
        }

        if (_expBarTMP != null)
        {
            int percent = Mathf.RoundToInt(pm.ExpProgress * 100f);
            _expBarTMP.text = $"<b>{pm.currentExp:N0} / {pm.ExpForNextLevel:N0} EXP  (%{percent})</b>";
            MainMenuManager.SetOutlineSafe(_expBarTMP, 0.16f, new Color(0.01f, 0.03f, 0.07f, 0.98f));
        }

        // 2. Savaş İçi HUD Rozeti & EXP Barı
        if (_hudLevelTMP != null)
        {
            Color c = PlayerProfileManager.GetRankColor(pm.level);
            _hudLevelTMP.text = $"<color=#{ColorUtility.ToHtmlStringRGB(c)}><b>LV.{pm.level}</b></color>";
        }

        if (_hudExpFill != null)
            _hudExpFill.fillAmount = pm.ExpProgress;

        if (_hudExpTMP != null)
        {
            int percent = Mathf.RoundToInt(pm.ExpProgress * 100f);
            _hudExpTMP.text = $"{pm.currentExp}/{pm.ExpForNextLevel} (%{percent})";
        }

        if (_dossierModal != null && _dossierModal.activeSelf)
        {
            UpdateDossierContent();
        }

        // 3. Menüler Genel Altın Kasası Rozeti
        if (_goldBadgeTMP != null)
        {
            HangarManager hm = HangarManager.EnsureInstance();
            int currentGold = (hm != null) ? hm.gold : ProfileManager.GetInt("AirBattle_Gold", 250);
            _goldBadgeTMP.text = $"ALTIN: <b>{currentGold:N0} G</b>";
        }
    }

    public void SetBadgeActive(bool active)
    {
        if (active) ShowMenuStatusBar();
        else HideMenuStatusBar();
    }

    public void SetHUDBadgeActive(bool active)
    {
        // Savaş anında level/EXP barı kapalı tutulur (yalnızca Altın barı ve Pause butonu kalır)
        if (_hudBadgeRoot != null)
        {
            _hudBadgeRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Detaylı Pilot Hizmet Belgesi Penceresi (1:1 Modern Taktik Kart Tasarımı)
    /// </summary>
    public void OpenDossierModal()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        AudioManager.Instance?.PlayButtonClick();

        if (_dossierModal == null)
        {
            BuildDossierModal(canvas.transform);
        }

        _dossierModal.SetActive(true);
        _dossierModal.transform.SetAsLastSibling();
        UpdateDossierContent();
    }

    public void CloseDossierModal()
    {
        if (_dossierModal != null)
            _dossierModal.SetActive(false);
    }

    private void BuildDossierModal(Transform parent)
    {
        // 1. Tam Ekran Kök Konteyner ve Arka Plan Karartma Katmanı (Dışarı tıklanınca kapatır, kart içine tıklanınca kapanmaz)
        _dossierModal = new GameObject("PilotDossierModal", typeof(RectTransform));
        _dossierModal.transform.SetParent(parent, false);
        RectTransform modalRt = _dossierModal.GetComponent<RectTransform>();
        MainMenuManager.MakeFullScreenRect(modalRt);

        GameObject dimmerObj = new GameObject("BackdropDimmer", typeof(RectTransform));
        dimmerObj.transform.SetParent(_dossierModal.transform, false);
        MainMenuManager.MakeFullScreenRect(dimmerObj.GetComponent<RectTransform>());

        Image dimmerImg = dimmerObj.AddComponent<Image>();
        dimmerImg.color = new Color(0.03f, 0.05f, 0.09f, 0.68f);
        dimmerImg.raycastTarget = true;

        Button backdropCloseBtn = dimmerObj.AddComponent<Button>();
        backdropCloseBtn.transition = Selectable.Transition.None;
        backdropCloseBtn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayButtonClick();
            CloseDossierModal();
        });

        // 2. Ana Taktik Kart Gövdesi (900 x 696)
        GameObject cardObj = new GameObject("DossierCard", typeof(RectTransform));
        cardObj.transform.SetParent(_dossierModal.transform, false);
        _dossierCardRt = cardObj.GetComponent<RectTransform>();
        _dossierCardRt.anchorMin = new Vector2(0.5f, 0.5f);
        _dossierCardRt.anchorMax = new Vector2(0.5f, 0.5f);
        _dossierCardRt.pivot = new Vector2(0.5f, 0.5f);
        _dossierCardRt.anchoredPosition = Vector2.zero;
        _dossierCardRt.sizeDelta = new Vector2(900f, 696f);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.sprite = GetRoundedPanelSprite(
            "dossier_outer_card",
            new Color(0.067f, 0.094f, 0.129f, 0.985f),   // #111821 koyu arduvaz-lacivert gövde
            new Color(0.294f, 0.482f, 0.624f, 0.92f),    // #4B7B9F ince çelik-mavi dış çerçeve
            radius: 18f,
            borderThickness: 2.0f
        );
        cardBg.type = Image.Type.Sliced;
        cardBg.color = Color.white;
        cardBg.raycastTarget = true;

        // Alt köşelerde ince 45° pahlı (chamfered) iç dekoratif çerçeve çizgisi
        GameObject innerFrameObj = new GameObject("InnerChamferAccent", typeof(RectTransform));
        innerFrameObj.transform.SetParent(cardObj.transform, false);
        RectTransform ifRt = innerFrameObj.GetComponent<RectTransform>();
        ifRt.anchorMin = Vector2.zero;
        ifRt.anchorMax = Vector2.one;
        ifRt.offsetMin = new Vector2(7f, 7f);
        ifRt.offsetMax = new Vector2(-7f, -7f);

        Image ifImg = innerFrameObj.AddComponent<Image>();
        ifImg.sprite = GetInnerBottomChamferFrameSprite();
        ifImg.type = Image.Type.Sliced;
        ifImg.color = new Color(0.32f, 0.48f, 0.62f, 0.38f);
        ifImg.raycastTarget = false;

        // 3. Üst Başlık Satırı: [Jet Silüeti İkonu] PİLOT HİZMET BELGESİ
        GameObject headerRow = new GameObject("HeaderRow", typeof(RectTransform));
        headerRow.transform.SetParent(cardObj.transform, false);
        RectTransform hrRt = headerRow.GetComponent<RectTransform>();
        hrRt.anchorMin = new Vector2(0.5f, 1f);
        hrRt.anchorMax = new Vector2(0.5f, 1f);
        hrRt.pivot = new Vector2(0.5f, 1f);
        hrRt.anchoredPosition = new Vector2(0f, -24f);
        hrRt.sizeDelta = new Vector2(420f, 38f);

        GameObject jetIconObj = new GameObject("JetIcon", typeof(RectTransform));
        jetIconObj.transform.SetParent(headerRow.transform, false);
        RectTransform jiRt = jetIconObj.GetComponent<RectTransform>();
        jiRt.anchorMin = new Vector2(0.5f, 0.5f);
        jiRt.anchorMax = new Vector2(0.5f, 0.5f);
        jiRt.pivot = new Vector2(0.5f, 0.5f);
        jiRt.anchoredPosition = new Vector2(-163f, 1f);
        jiRt.sizeDelta = new Vector2(28f, 28f);

        Image jiImg = jetIconObj.AddComponent<Image>();
        jiImg.sprite = GetFighterJetHeaderSprite();
        jiImg.color = new Color(0.55f, 0.65f, 0.76f, 0.95f); // #8CA6C2 çelik gri-mavi jet ikonu
        jiImg.raycastTarget = false;

        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform));
        titleObj.transform.SetParent(headerRow.transform, false);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.5f, 0.5f);
        tRt.anchorMax = new Vector2(0.5f, 0.5f);
        tRt.pivot = new Vector2(0f, 0.5f);
        tRt.anchoredPosition = new Vector2(-137f, 0f);
        tRt.sizeDelta = new Vector2(340f, 36f);

        TextMeshProUGUI tTMP = titleObj.AddComponent<TextMeshProUGUI>();
        tTMP.font = MainMenuManager.GetTacticalFont();
        tTMP.text = "PİLOT HİZMET BELGESİ";
        tTMP.fontSize = 23.5f;
        tTMP.fontStyle = FontStyles.Normal;
        tTMP.characterSpacing = 2.0f;
        tTMP.alignment = TextAlignmentOptions.MidlineLeft;
        tTMP.color = new Color(0.86f, 0.90f, 0.95f, 1f);
        tTMP.raycastTarget = false;

        // 4. Pilot & Rütbe Üst Bilgi Kutusu (824 x 114)
        Color innerBoxFill = new Color(0.114f, 0.149f, 0.192f, 0.98f);   // #1D2631 mat koyu arduvaz
        Color innerBoxBorder = new Color(0.176f, 0.224f, 0.282f, 0.80f); // #2D3948 ince iç kenarlık
        Sprite innerBoxSprite = GetRoundedPanelSprite("dossier_inner_box", innerBoxFill, innerBoxBorder, radius: 12f, borderThickness: 1.2f);

        GameObject rObj = new GameObject("RankArea", typeof(RectTransform));
        rObj.transform.SetParent(cardObj.transform, false);
        RectTransform rRt = rObj.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.5f, 1f);
        rRt.anchorMax = new Vector2(0.5f, 1f);
        rRt.pivot = new Vector2(0.5f, 1f);
        rRt.anchoredPosition = new Vector2(0f, -78f);
        rRt.sizeDelta = new Vector2(824f, 114f);

        Image rBg = rObj.AddComponent<Image>();
        rBg.sprite = innerBoxSprite;
        rBg.type = Image.Type.Sliced;
        rBg.color = Color.white;
        rBg.raycastTarget = false;

        GameObject rTxtObj = new GameObject("Text", typeof(RectTransform));
        rTxtObj.transform.SetParent(rObj.transform, false);
        RectTransform rtRt = rTxtObj.GetComponent<RectTransform>();
        rtRt.anchorMin = Vector2.zero;
        rtRt.anchorMax = Vector2.one;
        rtRt.offsetMin = new Vector2(20f, 8f);
        rtRt.offsetMax = new Vector2(-20f, -8f);

        _dossierRankTMP = rTxtObj.AddComponent<TextMeshProUGUI>();
        _dossierRankTMP.font = MainMenuManager.GetTacticalFont();
        _dossierRankTMP.fontSize = 25.5f;
        _dossierRankTMP.fontStyle = FontStyles.Bold;
        _dossierRankTMP.characterSpacing = 1.0f;
        _dossierRankTMP.lineSpacing = 8f;
        _dossierRankTMP.alignment = TextAlignmentOptions.Center;
        _dossierRankTMP.color = Color.white;
        _dossierRankTMP.raycastTarget = false;

        // 5. Sol İstatistik Kartı: SAVAŞ KAYITLARI (398 x 350)
        GameObject sObj = new GameObject("StatsBox", typeof(RectTransform));
        sObj.transform.SetParent(cardObj.transform, false);
        RectTransform sRt = sObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 1f);
        sRt.anchorMax = new Vector2(0.5f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(-213f, -218f);
        sRt.sizeDelta = new Vector2(398f, 350f);

        Image sBg = sObj.AddComponent<Image>();
        sBg.sprite = innerBoxSprite;
        sBg.type = Image.Type.Sliced;
        sBg.color = Color.white;
        sBg.raycastTarget = false;

        GameObject sTxtObj = new GameObject("Text", typeof(RectTransform));
        sTxtObj.transform.SetParent(sObj.transform, false);
        RectTransform stRt = sTxtObj.GetComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero;
        stRt.anchorMax = Vector2.one;
        stRt.offsetMin = Vector2.zero;
        stRt.offsetMax = Vector2.zero;

        _dossierStatsTMP = sTxtObj.AddComponent<TextMeshProUGUI>();
        _dossierStatsTMP.font = MainMenuManager.GetTacticalFont();
        _dossierStatsTMP.fontSize = 20.5f;
        _dossierStatsTMP.characterSpacing = 0.8f;
        _dossierStatsTMP.lineSpacing = 10f;
        _dossierStatsTMP.alignment = TextAlignmentOptions.TopLeft;
        _dossierStatsTMP.margin = new Vector4(34f, 68f, 22f, 24f);
        _dossierStatsTMP.color = new Color(0.84f, 0.89f, 0.94f, 1f);
        _dossierStatsTMP.raycastTarget = false;

        // 6. Sağ Bonus Kartı: KADEME BONUSLARI (398 x 350)
        GameObject pObj = new GameObject("PerksBox", typeof(RectTransform));
        pObj.transform.SetParent(cardObj.transform, false);
        RectTransform pRt = pObj.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 1f);
        pRt.anchorMax = new Vector2(0.5f, 1f);
        pRt.pivot = new Vector2(0.5f, 1f);
        pRt.anchoredPosition = new Vector2(213f, -218f);
        pRt.sizeDelta = new Vector2(398f, 350f);

        Image pBg = pObj.AddComponent<Image>();
        pBg.sprite = innerBoxSprite;
        pBg.type = Image.Type.Sliced;
        pBg.color = Color.white;
        pBg.raycastTarget = false;

        GameObject pTxtObj = new GameObject("Text", typeof(RectTransform));
        pTxtObj.transform.SetParent(pObj.transform, false);
        RectTransform ptRt = pTxtObj.GetComponent<RectTransform>();
        ptRt.anchorMin = Vector2.zero;
        ptRt.anchorMax = Vector2.one;
        ptRt.offsetMin = Vector2.zero;
        ptRt.offsetMax = Vector2.zero;

        _dossierPerksTMP = pTxtObj.AddComponent<TextMeshProUGUI>();
        _dossierPerksTMP.font = MainMenuManager.GetTacticalFont();
        _dossierPerksTMP.fontSize = 20.5f;
        _dossierPerksTMP.characterSpacing = 0.8f;
        _dossierPerksTMP.lineSpacing = 10f;
        _dossierPerksTMP.alignment = TextAlignmentOptions.TopLeft;
        _dossierPerksTMP.margin = new Vector4(34f, 68f, 22f, 24f);
        _dossierPerksTMP.color = new Color(0.84f, 0.89f, 0.94f, 1f);
        _dossierPerksTMP.raycastTarget = false;

        // 7. Alt Taktik Pahlı (Chamfered) Üçlü Buton Grubu (824px genişlik hizalı: 312 + 24 + 252 + 24 + 212)
        CreateDossierChamferButton(
            cardObj.transform,
            "SwitchProfileBtn",
            "PROFİL DEĞİŞTİR",
            new Vector2(-256f, -598f),
            new Vector2(312f, 62f),
            fillColor: new Color(0.118f, 0.176f, 0.239f, 1f),      // #1E2D3D çelik mavi-gri iç dolgu
            borderColor: new Color(0.306f, 0.482f, 0.620f, 0.96f), // #4E7B9E mavi çerçeve
            innerLineColor: new Color(0.20f, 0.31f, 0.41f, 0.65f),
            textColor: new Color(0.79f, 0.86f, 0.92f, 1f),         // #C9DBE8
            onClick: () =>
            {
                AudioManager.Instance?.PlayButtonClick();
                CloseDossierModal();
                PilotLoginUI.EnsureUI()?.OpenProfileSwitchModal(() =>
                {
                    RefreshBadgeVisuals();
                    UpdateDossierContent();
                });
            }
        );

        CreateDossierChamferButton(
            cardObj.transform,
            "LogoutProfileBtn",
            "ÇIKIŞ YAP",
            new Vector2(50f, -598f),
            new Vector2(252f, 62f),
            fillColor: new Color(0.353f, 0.243f, 0.200f, 1f),      // #5A3E33 sıcak bronz-bakır iç dolgu
            borderColor: new Color(0.675f, 0.471f, 0.341f, 0.96f), // #AC7857 kehribar-bakır çerçeve
            innerLineColor: new Color(0.47f, 0.33f, 0.26f, 0.65f),
            textColor: new Color(0.93f, 0.80f, 0.62f, 1f),         // #EDCC9E
            onClick: () =>
            {
                AudioManager.Instance?.PlayButtonClick();
                PilotAuthManager.EnsureInstance().Logout();
                CloseDossierModal();
                PilotLoginUI.EnsureUI()?.OpenLoginModal(() =>
                {
                    RefreshBadgeVisuals();
                    UpdateDossierContent();
                }, isMandatoryFirstTime: true);
            }
        );

        CreateDossierChamferButton(
            cardObj.transform,
            "CloseBtn",
            "KAPAT",
            new Vector2(306f, -598f),
            new Vector2(212f, 62f),
            fillColor: new Color(0.141f, 0.075f, 0.094f, 1f),      // #241318 koyu bordo-antrasit iç dolgu
            borderColor: new Color(0.573f, 0.176f, 0.208f, 0.96f), // #922D35 kırmızı çerçeve
            innerLineColor: new Color(0.34f, 0.12f, 0.15f, 0.65f),
            textColor: new Color(0.89f, 0.33f, 0.36f, 1f),         // #E3545C
            onClick: () =>
            {
                AudioManager.Instance?.PlayButtonClick();
                CloseDossierModal();
            }
        );
    }

    private Button CreateDossierChamferButton(
        Transform parent,
        string name,
        string label,
        Vector2 topAnchoredPos,
        Vector2 size,
        Color fillColor,
        Color borderColor,
        Color innerLineColor,
        Color textColor,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = topAnchoredPos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.sprite = GetChamferButtonSprite($"dossier_chamfer_{name}", fillColor, borderColor, innerLineColor);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        if (onClick != null)
            btn.onClick.AddListener(onClick);

        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = label;
        tmp.fontSize = 19.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;

        return btn;
    }

    private void UpdateDossierContent()
    {
        PlayerProfileManager pm = PlayerProfileManager.EnsureInstance();
        if (pm == null) return;

        if (_dossierRankTMP != null)
        {
            string callsign = LeaderboardManager.PlayerCallsign;
            Color rankCol = PlayerProfileManager.GetRankColor(pm.level);
            // Başlangıç rütbelerinde (gri/beyaz tonlarda) referans tasarımdaki sıcak altın-kehribar (#DEB053) tonunu kullan
            float sat;
            Color.RGBToHSV(rankCol, out _, out sat, out _);
            string rankHex = sat < 0.25f ? "DEB053" : ColorUtility.ToHtmlStringRGB(rankCol);

            _dossierRankTMP.text =
                $"<color=#F1F5F9>PİLOT:</color> <color=#4FD1C5>[{callsign}]</color>   <color=#94A3B8>|</color>   <color=#F1F5F9>RÜTBE:</color> <color=#{rankHex}>{pm.RankTitle}</color>\n" +
                $"<size=72%><color=#8B9BB0>SEVİYE: {pm.level}   |   EXP: {pm.currentExp.ToString("N0", TrCulture)} / {pm.ExpForNextLevel.ToString("N0", TrCulture)}</color></size>";
        }

        if (_dossierStatsTMP != null)
        {
            int gold = HangarManager.EnsureInstance() != null
                ? HangarManager.Instance.gold
                : ProfileManager.GetInt("AirBattle_Gold", 250);

            _dossierStatsTMP.text =
                "<size=104%><color=#CFD9E6>SAVAŞ KAYITLARI</color></size>\n" +
                $"<size=45%>\n</size>" +
                $"• İmha Edilen: <color=#E5B546>{pm.totalKills.ToString("N0", TrCulture)}</color>\n" +
                $"• Düşürülen Boss: <color=#E05858>{pm.totalBosses.ToString("N0", TrCulture)}</color>\n" +
                $"• Toplam Dalga: <color=#38BDF8>{pm.totalWavesCleared.ToString("N0", TrCulture)}</color>\n" +
                $"• Kasa Altını: <color=#E5B546>{gold.ToString("N0", TrCulture)} G</color>";
        }

        if (_dossierPerksTMP != null)
        {
            float dmgBonus = (pm.GetDroneDamageMultiplier() - 1f) * 100f;
            float spdBonus = (pm.GetDroneFireRateMultiplier() - 1f) * 100f;
            int levelReward = pm.level * 100;

            _dossierPerksTMP.text =
                "<size=104%><color=#CFD9E6>KADEME BONUSLARI</color></size>\n" +
                $"<size=45%>\n</size>" +
                $"• Saldırı Gücü: <color=#4FD1C5>+%{dmgBonus:F0}</color>\n" +
                $"• Atış Hızı: <color=#4FD1C5>+%{spdBonus:F0}</color>\n" +
                $"• Seviye Ödülü: <color=#E5B546>+{levelReward.ToString("N0", TrCulture)}G</color>";
        }
    }

    // =========================================================================
    // PROSEDÜREL YÜKSEK KESKİNLİKLİ (ANTI-ALIASED) KART, İKON VE BUTON ÜRETİCİLERİ
    // =========================================================================

    private static Sprite GetRoundedPanelSprite(string key, Color fill, Color border, float radius, float borderThickness)
    {
        if (_dossierSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = size * 0.5f;
        Vector2 halfExt = new Vector2(half - 1.5f, half - 1.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (halfExt - new Vector2(radius, radius));
                float sdf = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;

                if (sdf > 0.8f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                float outerAlpha = Mathf.Clamp01(0.65f - sdf);
                float borderBlend = Mathf.Clamp01(sdf + borderThickness + 0.5f);
                Color col = Color.Lerp(fill, border, borderBlend);
                col.a *= outerAlpha;
                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply();
        int slice = Mathf.CeilToInt(radius + borderThickness + 4f);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
        _dossierSpriteCache[key] = sp;
        return sp;
    }

    private static Sprite GetChamferButtonSprite(string key, Color fill, Color outerBorder, Color innerBorder)
    {
        if (_dossierSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        int w = 96;
        int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float hw = w * 0.5f - 1.5f;
        float hh = h * 0.5f - 1.5f;
        float cut = 11.5f; // 45 derece taktik pahlı (octagonal) köşe kesimi

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float ax = Mathf.Abs(x + 0.5f - w * 0.5f);
                float ay = Mathf.Abs(y + 0.5f - h * 0.5f);

                float dRight = ax - hw;
                float dTop = ay - hh;
                float dDiag = (ax + ay - (hw + hh - cut)) * 0.70710678f;
                float sdf = Mathf.Max(Mathf.Max(dRight, dTop), dDiag);

                if (sdf > 0.8f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                float outerAlpha = Mathf.Clamp01(0.65f - sdf);
                Color col = fill;

                // İnce iç vurgu çizgisi (sdf ~ -3.8)
                float innerDist = Mathf.Abs(sdf + 3.8f);
                if (innerDist < 1.0f)
                {
                    col = Color.Lerp(col, innerBorder, (1.0f - innerDist) * 0.85f);
                }

                // Ana dış pahlı çerçeve (sdf ~ -1.8 .. 0)
                float borderBlend = Mathf.Clamp01(sdf + 2.0f);
                col = Color.Lerp(col, outerBorder, borderBlend);
                col.a *= outerAlpha;
                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
        _dossierSpriteCache[key] = sp;
        return sp;
    }

    private static Sprite GetInnerBottomChamferFrameSprite()
    {
        if (_cachedInnerChamferFrameSprite != null) return _cachedInnerChamferFrameSprite;

        int s = 96;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = s * 0.5f - 1.5f;
        float topRadius = 13f;
        float bottomCut = 15f;

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float px = x + 0.5f - s * 0.5f;
                float py = y + 0.5f - s * 0.5f;
                float ax = Mathf.Abs(px);

                float sdf;
                if (py >= 0f)
                {
                    Vector2 q = new Vector2(ax, py) - new Vector2(half - topRadius, half - topRadius);
                    sdf = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - topRadius;
                }
                else
                {
                    float ay = -py;
                    float dSide = ax - half;
                    float dBot = ay - half;
                    float dDiag = (ax + ay - (half + half - bottomCut)) * 0.70710678f;
                    sdf = Mathf.Max(Mathf.Max(dSide, dBot), dDiag);
                }

                float lineDist = Mathf.Abs(sdf + 0.8f);
                if (lineDist <= 1.15f)
                {
                    // Alt yarıda daha belirgin, üst yarıda zarif sönümlü ince çerçeve
                    float vertFade = Mathf.Lerp(1.0f, 0.35f, Mathf.Clamp01((y / (float)s)));
                    float a = Mathf.Clamp01((1.15f - lineDist) * 1.2f) * vertFade;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        _cachedInnerChamferFrameSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
        return _cachedInnerChamferFrameSprite;
    }

    private static Sprite GetFighterJetHeaderSprite()
    {
        if (_cachedJetHeaderSprite != null) return _cachedJetHeaderSprite;

        int s = 64;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        // 4x4 alt-piksel süper örnekleme (vektör kalitesinde pürüzsüz savaş uçağı silüeti)
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                {
                    float py = (y + (sy + 0.5f) * 0.25f) / s; // 0..1 (alt -> üst)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        float px = Mathf.Abs(((x + (sx + 0.5f) * 0.25f) / s) - 0.5f) * 2f; // 0 (merkez) .. 1 (kenar)
                        if (IsInsideJetSilhouette(px, py))
                            hits++;
                    }
                }

                if (hits > 0)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, hits / 16f));
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }

        tex.Apply();
        _cachedJetHeaderSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _cachedJetHeaderSprite;
    }

    private static bool IsInsideJetSilhouette(float ax, float y)
    {
        // Burun konisi ve ana gövde (y: 0.14 .. 0.90)
        if (y >= 0.14f && y <= 0.90f)
        {
            float bodyHalfW;
            if (y > 0.62f)
            {
                float t = (0.90f - y) / 0.28f; // 0 (burun ucu) -> 1 (kokpit tabanı)
                bodyHalfW = Mathf.Pow(t, 1.35f) * 0.135f;
            }
            else if (y >= 0.24f)
            {
                bodyHalfW = 0.135f;
            }
            else
            {
                // Çift motor egzoz çıkışı girintisi
                bodyHalfW = 0.125f;
                if (y < 0.19f && ax < 0.035f) return false;
            }

            if (ax <= bodyHalfW) return true;
        }

        // Ana süpürmeli delta kanatlar (y: 0.28 .. 0.62)
        if (y >= 0.28f && y <= 0.62f)
        {
            float leadMaxX = Mathf.Lerp(0.68f, 0.11f, Mathf.Clamp01((y - 0.30f) / 0.32f));
            float trailMinY = Mathf.Lerp(0.35f, 0.28f, Mathf.Clamp01(ax / 0.68f));
            if (y >= trailMinY && ax <= leadMaxX)
                return true;
        }

        // Arka çift kuyruk kanatçıkları (stabilizatörler, y: 0.13 .. 0.30)
        if (y >= 0.13f && y <= 0.30f)
        {
            float tailMaxX = Mathf.Lerp(0.34f, 0.12f, Mathf.Clamp01((y - 0.15f) / 0.15f));
            float tailMinY = Mathf.Lerp(0.18f, 0.13f, Mathf.Clamp01(ax / 0.34f));
            if (y >= tailMinY && ax <= tailMaxX && ax >= 0.045f)
                return true;
        }

        return false;
    }
}
