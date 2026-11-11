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
    private TextMeshProUGUI _dossierStatsTMP;
    private TextMeshProUGUI _dossierRankTMP;
    private TextMeshProUGUI _dossierPerksTMP;

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
        PlayerProfileManager pm = PlayerProfileManager.EnsureInstance();
        pm.OnLevelUp += HandleLevelUp;
        pm.OnExpAdded += HandleExpAdded;
        RefreshBadgeVisuals();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (PlayerProfileManager.Instance != null)
        {
            PlayerProfileManager.Instance.OnLevelUp -= HandleLevelUp;
            PlayerProfileManager.Instance.OnExpAdded -= HandleExpAdded;
        }
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
        bRt.anchoredPosition = new Vector2(30f, -20f);
        bRt.sizeDelta = new Vector2(390f, 84f);

        Image bg = _badgeRoot.AddComponent<Image>();
        bg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.85f), new Color(0.03f, 0.07f, 0.12f, 0.94f), false);
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        Button badgeBtn = _badgeRoot.AddComponent<Button>();
        badgeBtn.onClick.AddListener(OpenDossierModal);

        // Seviye Kare Rozeti (Sol Taraf)
        GameObject lvlObj = new GameObject("LevelBox", typeof(RectTransform));
        lvlObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform lRt = lvlObj.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0.5f);
        lRt.anchorMax = new Vector2(0f, 0.5f);
        lRt.pivot = new Vector2(0f, 0.5f);
        lRt.anchoredPosition = new Vector2(10f, 0f);
        lRt.sizeDelta = new Vector2(64f, 64f);

        Image lBg = lvlObj.AddComponent<Image>();
        lBg.sprite = MainMenuManager.GetHudSprite("hud_btn_amber", new Color(1f, 0.85f, 0.2f, 1f), new Color(0.08f, 0.15f, 0.24f, 1f), false);
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
        _levelBadgeTMP.fontSize = 18f;
        _levelBadgeTMP.fontStyle = FontStyles.Bold;
        _levelBadgeTMP.characterSpacing = 1.5f;
        _levelBadgeTMP.alignment = TextAlignmentOptions.Center;
        _levelBadgeTMP.color = new Color(1f, 0.88f, 0.25f);
        _levelBadgeTMP.raycastTarget = false;

        // Rütbe Başlığı
        GameObject rObj = new GameObject("RankTitle", typeof(RectTransform));
        rObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform rRt = rObj.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 1f);
        rRt.anchorMax = new Vector2(0f, 1f);
        rRt.pivot = new Vector2(0f, 1f);
        rRt.anchoredPosition = new Vector2(86f, -10f);
        rRt.sizeDelta = new Vector2(290f, 28f);

        _rankTitleTMP = rObj.AddComponent<TextMeshProUGUI>();
        _rankTitleTMP.font = MainMenuManager.GetTacticalFont();
        _rankTitleTMP.fontSize = 19f;
        _rankTitleTMP.fontStyle = FontStyles.Bold;
        _rankTitleTMP.characterSpacing = 1.8f;
        _rankTitleTMP.alignment = TextAlignmentOptions.Left;
        _rankTitleTMP.color = Color.white;
        _rankTitleTMP.raycastTarget = false;
        _rankTitleTMP.enableAutoSizing = true;
        _rankTitleTMP.fontSizeMin = 14f;
        _rankTitleTMP.fontSizeMax = 20f;

        // EXP Bar Çerçevesi
        GameObject barBgObj = new GameObject("ExpBarBg", typeof(RectTransform));
        barBgObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform barRt = barBgObj.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(0f, 0f);
        barRt.pivot = new Vector2(0f, 0f);
        barRt.anchoredPosition = new Vector2(86f, 10f);
        barRt.sizeDelta = new Vector2(290f, 26f);

        Image barBg = barBgObj.AddComponent<Image>();
        barBg.color = new Color(0.02f, 0.04f, 0.07f, 1f);

        // EXP Bar Dolgu
        GameObject fillObj = new GameObject("ExpBarFill", typeof(RectTransform));
        fillObj.transform.SetParent(barBgObj.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        _expBarFill = fillObj.AddComponent<Image>();
        _expBarFill.color = new Color(0f, 0.85f, 1f, 0.95f);
        _expBarFill.type = Image.Type.Filled;
        _expBarFill.fillMethod = Image.FillMethod.Horizontal;
        _expBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        // EXP Bar Yazısı
        GameObject barTxtObj = new GameObject("ExpText", typeof(RectTransform));
        barTxtObj.transform.SetParent(barBgObj.transform, false);
        RectTransform btRt = barTxtObj.GetComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        _expBarTMP = barTxtObj.AddComponent<TextMeshProUGUI>();
        _expBarTMP.font = MainMenuManager.GetTacticalFont();
        _expBarTMP.fontSize = 13f;
        _expBarTMP.fontStyle = FontStyles.Bold;
        _expBarTMP.characterSpacing = 1.4f;
        _expBarTMP.alignment = TextAlignmentOptions.Center;
        _expBarTMP.color = Color.white;
        _expBarTMP.raycastTarget = false;
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
        gRt.anchoredPosition = new Vector2(-30f, -20f);
        gRt.sizeDelta = new Vector2(260f, 54f);

        Image gBg = _goldBadgeRoot.AddComponent<Image>();
        gBg.sprite = MainMenuManager.GetHudSprite("hud_btn_amber", new Color(1f, 0.85f, 0.2f, 0.95f), new Color(0.06f, 0.05f, 0.02f, 0.95f), true);
        gBg.type = Image.Type.Sliced;
        gBg.color = Color.white;

        Button gBtn = _goldBadgeRoot.AddComponent<Button>();
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
        _goldBadgeTMP.characterSpacing = 2.0f;
        _goldBadgeTMP.alignment = TextAlignmentOptions.Center;
        _goldBadgeTMP.color = new Color(1f, 0.88f, 0.25f);
        _goldBadgeTMP.raycastTarget = false;
    }

    /// <summary>
    /// Savaş sahnesinde sağ üst köşede (Altın sayacının hemen altında) mini seviye ve EXP barı oluşturur.
    /// </summary>
    public void SetupHUDProfileBadge(Transform canvasTransform)
    {
        if (_hudBadgeRoot != null)
        {
            _hudBadgeRoot.SetActive(true);
            RefreshBadgeVisuals();
            return;
        }

        Transform existing = canvasTransform.Find("HUD_PilotProfileBadge");
        _hudBadgeRoot = existing != null ? existing.gameObject : new GameObject("HUD_PilotProfileBadge", typeof(RectTransform));
        _hudBadgeRoot.transform.SetParent(canvasTransform, false);

        RectTransform rt = _hudBadgeRoot.GetComponent<RectTransform>();
        if (rt != null)
        {
            // Sağ üst köşe, Altın kutusunun hemen altı (Kompakt ve zarif)
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-96f, -78f);
            rt.sizeDelta = new Vector2(220f, 34f);
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
        _hudLevelTMP.fontSize = 14f;
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
        _hudExpTMP.fontSize = 12f;
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
            _levelBadgeTMP.text = $"<size=70%><color=#8AB4F8>SEV.</color></size>\n<b>{pm.level}</b>";

        if (_rankTitleTMP != null)
        {
            Color c = PlayerProfileManager.GetRankColor(pm.level);
            string callsign = LeaderboardManager.PlayerCallsign;
            _rankTitleTMP.text = $"<color=#00E5FF><b>[{callsign}]</b></color> <color=#{ColorUtility.ToHtmlStringRGB(c)}>{pm.RankTitle.ToUpper()}</color>";
        }

        if (_expBarFill != null)
            _expBarFill.fillAmount = pm.ExpProgress;

        if (_expBarTMP != null)
        {
            int percent = Mathf.RoundToInt(pm.ExpProgress * 100f);
            _expBarTMP.text = $"<b>{pm.currentExp:N0} / {pm.ExpForNextLevel:N0} EXP  (%{percent})</b>";
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
            int currentGold = (hm != null) ? hm.gold : PlayerPrefs.GetInt("AirBattle_Gold", 250);
            _goldBadgeTMP.text = $"🪙 ALTIN: <b>{currentGold:N0} G</b>";
        }
    }

    public void SetBadgeActive(bool active)
    {
        if (active) ShowMenuStatusBar();
        else HideMenuStatusBar();
    }

    public void SetHUDBadgeActive(bool active)
    {
        if (_hudBadgeRoot != null) _hudBadgeRoot.SetActive(active);
        if (active) RefreshBadgeVisuals();
    }

    /// <summary>
    /// Detaylı Askeri Pilot Kimlik Kartı & Hizmet Belgesi Penceresi
    /// </summary>
    public void OpenDossierModal()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

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
        _dossierModal = new GameObject("PilotDossierModal", typeof(RectTransform));
        _dossierModal.transform.SetParent(parent, false);

        RectTransform mRt = _dossierModal.GetComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0.5f, 0.5f);
        mRt.anchorMax = new Vector2(0.5f, 0.5f);
        mRt.pivot = new Vector2(0.5f, 0.5f);
        mRt.anchoredPosition = Vector2.zero;
        mRt.sizeDelta = new Vector2(880f, 680f);

        Image bg = _dossierModal.AddComponent<Image>();
        bg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.03f, 0.06f, 0.12f, 0.96f), false);
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        // Üst Başlık Visor Plate
        GameObject visorObj = new GameObject("HeaderVisor", typeof(RectTransform));
        visorObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform vRt = visorObj.GetComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0f, 1f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.pivot = new Vector2(0.5f, 1f);
        vRt.anchoredPosition = new Vector2(0f, -14f);
        vRt.sizeDelta = new Vector2(840f, 52f);

        Image vImg = visorObj.AddComponent<Image>();
        vImg.sprite = MainMenuManager.GetHudSprite("hud_header_visor", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.04f, 0.10f, 0.18f, 1f), true);
        vImg.type = Image.Type.Sliced;
        vImg.color = Color.white;

        GameObject tObj = new GameObject("Title", typeof(RectTransform));
        tObj.transform.SetParent(visorObj.transform, false);
        RectTransform tRt = tObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tTMP = tObj.AddComponent<TextMeshProUGUI>();
        tTMP.font = MainMenuManager.GetTacticalFont();
        tTMP.text = "DEFCON // HAVA KUVVETLERİ ASKERİ PİLOT HİZMET BELGESİ";
        tTMP.fontSize = 20f;
        tTMP.fontStyle = FontStyles.Bold;
        tTMP.characterSpacing = 2.0f;
        tTMP.alignment = TextAlignmentOptions.Center;
        tTMP.color = Color.white;

        // Rütbe ve Seviye Alanı
        GameObject rObj = new GameObject("RankArea", typeof(RectTransform));
        rObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform rRt = rObj.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.04f, 0.73f);
        rRt.anchorMax = new Vector2(0.96f, 0.89f);
        rRt.offsetMin = Vector2.zero;
        rRt.offsetMax = Vector2.zero;

        Image rBg = rObj.AddComponent<Image>();
        rBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(1f, 0.85f, 0.2f, 0.85f), new Color(0.05f, 0.10f, 0.16f, 0.95f), false);
        rBg.type = Image.Type.Sliced;
        rBg.color = Color.white;

        GameObject rTxtObj = new GameObject("Text", typeof(RectTransform));
        rTxtObj.transform.SetParent(rObj.transform, false);
        RectTransform rtRt = rTxtObj.GetComponent<RectTransform>();
        rtRt.anchorMin = Vector2.zero;
        rtRt.anchorMax = Vector2.one;
        rtRt.offsetMin = Vector2.zero;
        rtRt.offsetMax = Vector2.zero;

        _dossierRankTMP = rTxtObj.AddComponent<TextMeshProUGUI>();
        _dossierRankTMP.font = MainMenuManager.GetTacticalFont();
        _dossierRankTMP.fontSize = 21f;
        _dossierRankTMP.fontStyle = FontStyles.Bold;
        _dossierRankTMP.characterSpacing = 1.6f;
        _dossierRankTMP.alignment = TextAlignmentOptions.Center;
        _dossierRankTMP.color = Color.white;

        // Savaş İstatistikleri Kutusu (Sol)
        GameObject sObj = new GameObject("StatsBox", typeof(RectTransform));
        sObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform sRt = sObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.04f, 0.20f);
        sRt.anchorMax = new Vector2(0.485f, 0.70f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        Image sBg = sObj.AddComponent<Image>();
        sBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.7f), new Color(0.03f, 0.07f, 0.12f, 0.92f), false);
        sBg.type = Image.Type.Sliced;
        sBg.color = Color.white;

        GameObject sTxtObj = new GameObject("Text", typeof(RectTransform));
        sTxtObj.transform.SetParent(sObj.transform, false);
        RectTransform stRt = sTxtObj.GetComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero;
        stRt.anchorMax = Vector2.one;
        stRt.offsetMin = Vector2.zero;
        stRt.offsetMax = Vector2.zero;

        _dossierStatsTMP = sTxtObj.AddComponent<TextMeshProUGUI>();
        _dossierStatsTMP.font = MainMenuManager.GetTacticalFont();
        _dossierStatsTMP.fontSize = 17f;
        _dossierStatsTMP.characterSpacing = 1.3f;
        _dossierStatsTMP.lineSpacing = 6f;
        _dossierStatsTMP.alignment = TextAlignmentOptions.MidlineLeft;
        _dossierStatsTMP.margin = new Vector4(20f, 15f, 15f, 15f);
        _dossierStatsTMP.color = new Color(0.88f, 0.94f, 1f);

        // İHA ve Seviye Bonusları Kutusu (Sağ)
        GameObject pObj = new GameObject("PerksBox", typeof(RectTransform));
        pObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform pRt = pObj.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.515f, 0.20f);
        pRt.anchorMax = new Vector2(0.96f, 0.70f);
        pRt.offsetMin = Vector2.zero;
        pRt.offsetMax = Vector2.zero;

        Image pBg = pObj.AddComponent<Image>();
        pBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.7f), new Color(0.03f, 0.07f, 0.12f, 0.92f), false);
        pBg.type = Image.Type.Sliced;
        pBg.color = Color.white;

        GameObject pTxtObj = new GameObject("Text", typeof(RectTransform));
        pTxtObj.transform.SetParent(pObj.transform, false);
        RectTransform ptRt = pTxtObj.GetComponent<RectTransform>();
        ptRt.anchorMin = Vector2.zero;
        ptRt.anchorMax = Vector2.one;
        ptRt.offsetMin = Vector2.zero;
        ptRt.offsetMax = Vector2.zero;

        _dossierPerksTMP = pTxtObj.AddComponent<TextMeshProUGUI>();
        _dossierPerksTMP.font = MainMenuManager.GetTacticalFont();
        _dossierPerksTMP.fontSize = 17f;
        _dossierPerksTMP.characterSpacing = 1.3f;
        _dossierPerksTMP.lineSpacing = 5f;
        _dossierPerksTMP.alignment = TextAlignmentOptions.MidlineLeft;
        _dossierPerksTMP.margin = new Vector4(20f, 15f, 15f, 15f);
        _dossierPerksTMP.color = new Color(0.88f, 0.94f, 1f);

        // Çağrı Kodunu Değiştir Butonu
        GameObject editNameObj = new GameObject("EditNameBtn", typeof(RectTransform));
        editNameObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform enRt = editNameObj.GetComponent<RectTransform>();
        enRt.anchorMin = new Vector2(0.12f, 0.04f);
        enRt.anchorMax = new Vector2(0.48f, 0.14f);
        enRt.offsetMin = Vector2.zero;
        enRt.offsetMax = Vector2.zero;

        Image enImg = editNameObj.AddComponent<Image>();
        enImg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f, 1f), new Color(0.04f, 0.12f, 0.20f, 1f), true);
        enImg.type = Image.Type.Sliced;
        enImg.color = Color.white;

        Button enBtn = editNameObj.AddComponent<Button>();
        enBtn.onClick.AddListener(() =>
        {
            PilotLoginUI.EnsureUI()?.OpenLoginModal(() =>
            {
                UpdateDossierContent();
                RefreshBadgeVisuals();
            });
        });

        GameObject enTxtObj = new GameObject("Text", typeof(RectTransform));
        enTxtObj.transform.SetParent(editNameObj.transform, false);
        RectTransform entRt = enTxtObj.GetComponent<RectTransform>();
        entRt.anchorMin = Vector2.zero;
        entRt.anchorMax = Vector2.one;
        entRt.offsetMin = Vector2.zero;
        entRt.offsetMax = Vector2.zero;

        TextMeshProUGUI enTMP = enTxtObj.AddComponent<TextMeshProUGUI>();
        enTMP.font = MainMenuManager.GetTacticalFont();
        enTMP.text = "✏️ ÇAĞRI KODUNU DEĞİŞTİR";
        enTMP.fontSize = 15f;
        enTMP.fontStyle = FontStyles.Bold;
        enTMP.characterSpacing = 1.4f;
        enTMP.alignment = TextAlignmentOptions.Center;
        enTMP.color = Color.white;

        // Kapat Butonu
        GameObject closeObj = new GameObject("CloseBtn", typeof(RectTransform));
        closeObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform cRt = closeObj.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.52f, 0.04f);
        cRt.anchorMax = new Vector2(0.88f, 0.14f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        Image cImg = closeObj.AddComponent<Image>();
        cImg.sprite = MainMenuManager.GetHudSprite("hud_btn_red", new Color(1f, 0.35f, 0.35f, 1f), new Color(0.24f, 0.05f, 0.05f, 1f), true);
        cImg.type = Image.Type.Sliced;
        cImg.color = Color.white;

        Button cBtn = closeObj.AddComponent<Button>();
        cBtn.onClick.AddListener(CloseDossierModal);

        GameObject cTxtObj = new GameObject("Text", typeof(RectTransform));
        cTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform ctRt = cTxtObj.GetComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.offsetMin = Vector2.zero;
        ctRt.offsetMax = Vector2.zero;

        TextMeshProUGUI ctTMP = cTxtObj.AddComponent<TextMeshProUGUI>();
        ctTMP.font = MainMenuManager.GetTacticalFont();
        ctTMP.text = "◀ KAPAT";
        ctTMP.fontSize = 18f;
        ctTMP.fontStyle = FontStyles.Bold;
        ctTMP.characterSpacing = 1.6f;
        ctTMP.alignment = TextAlignmentOptions.Center;
        ctTMP.color = Color.white;
    }

    private void UpdateDossierContent()
    {
        PlayerProfileManager pm = PlayerProfileManager.EnsureInstance();
        if (pm == null) return;

        if (_dossierRankTMP != null)
        {
            Color c = PlayerProfileManager.GetRankColor(pm.level);
            string callsign = LeaderboardManager.PlayerCallsign;
            _dossierRankTMP.text = $"<b>PİLOT ÇAĞRI KODU:</b> <color=#00E5FF><b>[{callsign}]</b></color>   |   <b>RÜTBE:</b> <color=#{ColorUtility.ToHtmlStringRGB(c)}>{pm.RankTitle}</color>\n<size=75%><color=#8AB4F8>KADEME SEVİYESİ: {pm.level} | TOPLAM EXP: {pm.currentExp} / {pm.ExpForNextLevel}</color></size>";
        }

        if (_dossierStatsTMP != null)
        {
            _dossierStatsTMP.text = "<b>SAVAŞ KAYITLARI:</b>\n\n" +
                                    $"• İmha Edilen Uçak: <color=#F1C40F>{pm.totalKills:N0}</color>\n" +
                                    $"• Düşürülen Bosslar: <color=#E74C3C>{pm.totalBosses:N0}</color>\n" +
                                    $"• Geçilen Toplam Dalga: <color=#00E5FF>{pm.totalWavesCleared:N0}</color>\n" +
                                    $"• Kasa Altını: <color=#F1C40F>{HangarManager.EnsureInstance()?.gold:N0} G</color>";
        }

        if (_dossierPerksTMP != null)
        {
            float dmgBonus = (pm.GetDroneDamageMultiplier() - 1f) * 100f;
            float spdBonus = (pm.GetDroneFireRateMultiplier() - 1f) * 100f;

            _dossierPerksTMP.text = "<b>İHA & DRON KADEME BONUSLARI:</b>\n\n" +
                                    $"• Dron Saldırı Gücü: <color=#00E5FF>+%{dmgBonus:F0} Hasar</color>\n" +
                                    $"• Dron Atış Hızı: <color=#00E5FF>+%{spdBonus:F0} Hız</color>\n" +
                                    $"• Seviye Başı Ödül: <color=#F1C40F>+{pm.level * 100}G Altın</color>\n" +
                                    "<size=75%><color=#8AB4F8>Daha fazla uçak vurdukça profil seviyeniz ve dron bonuslarınız artar!</color></size>";
        }
    }
}
