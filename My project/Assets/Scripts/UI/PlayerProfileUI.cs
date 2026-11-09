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
        GameObject go = existing != null ? existing.gameObject : new GameObject("PlayerProfileUI");
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

        _statusBarRoot = new GameObject("GlobalMenuStatusBar");
        _statusBarRoot.transform.SetParent(canvas.transform, false);

        RectTransform sbRt = _statusBarRoot.AddComponent<RectTransform>();
        MainMenuManager.MakeFullScreenRect(sbRt);

        // 1. Sol Üst: Pilot Profil Rozeti & Canlı EXP Barı
        BuildProfileLevelBadge(_statusBarRoot.transform);

        // 2. Sağ Üst: Altın Kasası Rozeti
        BuildMenuGoldBadge(_statusBarRoot.transform);

        _statusBarRoot.transform.SetAsLastSibling();
        RefreshBadgeVisuals();
    }

    public void ShowMenuStatusBar()
    {
        if (_statusBarRoot == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas != null) SetupMenuStatusBar(canvas);
        }

        if (_statusBarRoot != null)
        {
            _statusBarRoot.SetActive(true);
            _statusBarRoot.transform.SetAsLastSibling();
            RefreshBadgeVisuals();
        }
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

        _badgeRoot = new GameObject("PilotProfileBadge");
        _badgeRoot.transform.SetParent(parent, false);

        RectTransform bRt = _badgeRoot.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 1f);
        bRt.anchorMax = new Vector2(0f, 1f);
        bRt.pivot = new Vector2(0f, 1f);
        bRt.anchoredPosition = new Vector2(30f, -20f);
        bRt.sizeDelta = new Vector2(390f, 84f);

        Image bg = _badgeRoot.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.14f, 0.95f);

        Outline outline = _badgeRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.8f);
        outline.effectDistance = new Vector2(2.2f, -2.2f);

        Button badgeBtn = _badgeRoot.AddComponent<Button>();
        badgeBtn.onClick.AddListener(OpenDossierModal);

        // Seviye Kare Rozeti (Sol Taraf)
        GameObject lvlObj = new GameObject("LevelBox");
        lvlObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform lRt = lvlObj.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0.5f);
        lRt.anchorMax = new Vector2(0f, 0.5f);
        lRt.pivot = new Vector2(0f, 0.5f);
        lRt.anchoredPosition = new Vector2(10f, 0f);
        lRt.sizeDelta = new Vector2(64f, 64f);

        Image lBg = lvlObj.AddComponent<Image>();
        lBg.color = new Color(0.08f, 0.15f, 0.24f, 1f);

        Outline lOutline = lvlObj.AddComponent<Outline>();
        lOutline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
        lOutline.effectDistance = new Vector2(1.8f, -1.8f);

        GameObject lTxtObj = new GameObject("Text");
        lTxtObj.transform.SetParent(lvlObj.transform, false);
        RectTransform ltRt = lTxtObj.AddComponent<RectTransform>();
        ltRt.anchorMin = Vector2.zero;
        ltRt.anchorMax = Vector2.one;
        ltRt.offsetMin = Vector2.zero;
        ltRt.offsetMax = Vector2.zero;

        _levelBadgeTMP = lTxtObj.AddComponent<TextMeshProUGUI>();
        _levelBadgeTMP.fontSize = 18f;
        _levelBadgeTMP.fontStyle = FontStyles.Bold;
        _levelBadgeTMP.alignment = TextAlignmentOptions.Center;
        _levelBadgeTMP.color = new Color(1f, 0.88f, 0.25f);
        _levelBadgeTMP.raycastTarget = false;

        // Rütbe Başlığı
        GameObject rObj = new GameObject("RankTitle");
        rObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform rRt = rObj.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 1f);
        rRt.anchorMax = new Vector2(0f, 1f);
        rRt.pivot = new Vector2(0f, 1f);
        rRt.anchoredPosition = new Vector2(86f, -10f);
        rRt.sizeDelta = new Vector2(290f, 28f);

        _rankTitleTMP = rObj.AddComponent<TextMeshProUGUI>();
        _rankTitleTMP.fontSize = 19f;
        _rankTitleTMP.fontStyle = FontStyles.Bold;
        _rankTitleTMP.alignment = TextAlignmentOptions.Left;
        _rankTitleTMP.color = Color.white;
        _rankTitleTMP.raycastTarget = false;
        _rankTitleTMP.enableAutoSizing = true;
        _rankTitleTMP.fontSizeMin = 14f;
        _rankTitleTMP.fontSizeMax = 20f;

        // EXP Bar Çerçevesi
        GameObject barBgObj = new GameObject("ExpBarBg");
        barBgObj.transform.SetParent(_badgeRoot.transform, false);
        RectTransform barRt = barBgObj.AddComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(0f, 0f);
        barRt.pivot = new Vector2(0f, 0f);
        barRt.anchoredPosition = new Vector2(86f, 10f);
        barRt.sizeDelta = new Vector2(290f, 26f);

        Image barBg = barBgObj.AddComponent<Image>();
        barBg.color = new Color(0.02f, 0.04f, 0.07f, 1f);

        // EXP Bar Dolgu
        GameObject fillObj = new GameObject("ExpBarFill");
        fillObj.transform.SetParent(barBgObj.transform, false);
        RectTransform fillRt = fillObj.AddComponent<RectTransform>();
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
        GameObject barTxtObj = new GameObject("ExpText");
        barTxtObj.transform.SetParent(barBgObj.transform, false);
        RectTransform btRt = barTxtObj.AddComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        _expBarTMP = barTxtObj.AddComponent<TextMeshProUGUI>();
        _expBarTMP.fontSize = 13f;
        _expBarTMP.fontStyle = FontStyles.Bold;
        _expBarTMP.alignment = TextAlignmentOptions.Center;
        _expBarTMP.color = Color.white;
        _expBarTMP.raycastTarget = false;
    }

    private void BuildMenuGoldBadge(Transform parent)
    {
        if (_goldBadgeRoot != null) return;

        _goldBadgeRoot = new GameObject("MenuGoldBadge");
        _goldBadgeRoot.transform.SetParent(parent, false);

        RectTransform gRt = _goldBadgeRoot.AddComponent<RectTransform>();
        gRt.anchorMin = new Vector2(1f, 1f);
        gRt.anchorMax = new Vector2(1f, 1f);
        gRt.pivot = new Vector2(1f, 1f);
        gRt.anchoredPosition = new Vector2(-30f, -20f);
        gRt.sizeDelta = new Vector2(260f, 54f);

        Image gBg = _goldBadgeRoot.AddComponent<Image>();
        gBg.color = new Color(0.04f, 0.08f, 0.14f, 0.95f);

        Outline gOutline = _goldBadgeRoot.AddComponent<Outline>();
        gOutline.effectColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        gOutline.effectDistance = new Vector2(2f, -2f);

        Button gBtn = _goldBadgeRoot.AddComponent<Button>();
        gBtn.onClick.AddListener(() =>
        {
            MainMenuManager.Instance?.OpenStorePanel();
        });

        GameObject goldTxtObj = new GameObject("Text");
        goldTxtObj.transform.SetParent(_goldBadgeRoot.transform, false);
        RectTransform gtRt = goldTxtObj.AddComponent<RectTransform>();
        gtRt.anchorMin = Vector2.zero;
        gtRt.anchorMax = Vector2.one;
        gtRt.offsetMin = Vector2.zero;
        gtRt.offsetMax = Vector2.zero;

        _goldBadgeTMP = goldTxtObj.AddComponent<TextMeshProUGUI>();
        _goldBadgeTMP.fontSize = 20f;
        _goldBadgeTMP.fontStyle = FontStyles.Bold;
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
        _hudBadgeRoot = existing != null ? existing.gameObject : new GameObject("HUD_PilotProfileBadge");
        _hudBadgeRoot.transform.SetParent(canvasTransform, false);

        RectTransform rt = _hudBadgeRoot.GetComponent<RectTransform>();
        if (rt == null) rt = _hudBadgeRoot.AddComponent<RectTransform>();

        // Sağ üst köşe, Altın kutusunun hemen altı (Kompakt ve zarif)
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-96f, -78f);
        rt.sizeDelta = new Vector2(220f, 34f);

        Image bg = _hudBadgeRoot.GetComponent<Image>();
        if (bg == null) bg = _hudBadgeRoot.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.14f, 0.90f);

        Outline outline = _hudBadgeRoot.GetComponent<Outline>();
        if (outline == null) outline = _hudBadgeRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.8f);
        outline.effectDistance = new Vector2(2.2f, -2.2f);

        // Seviye Kutusu (Sol)
        Transform lvlChild = _hudBadgeRoot.transform.Find("LvlTxt");
        GameObject lvlObj = lvlChild != null ? lvlChild.gameObject : new GameObject("LvlTxt");
        lvlObj.transform.SetParent(_hudBadgeRoot.transform, false);

        RectTransform lRt = lvlObj.GetComponent<RectTransform>();
        if (lRt == null) lRt = lvlObj.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(0.32f, 1f);
        lRt.offsetMin = new Vector2(2f, 1f);
        lRt.offsetMax = new Vector2(-1f, -1f);

        _hudLevelTMP = lvlObj.GetComponent<TextMeshProUGUI>();
        if (_hudLevelTMP == null) _hudLevelTMP = lvlObj.AddComponent<TextMeshProUGUI>();
        _hudLevelTMP.fontSize = 14f;
        _hudLevelTMP.fontStyle = FontStyles.Bold;
        _hudLevelTMP.alignment = TextAlignmentOptions.Center;
        _hudLevelTMP.color = new Color(1f, 0.85f, 0.2f);
        _hudLevelTMP.raycastTarget = false;

        // EXP Bar Dolgu Arka Planı (Sağ)
        Transform expBgChild = _hudBadgeRoot.transform.Find("ExpBarBg");
        GameObject expBgObj = expBgChild != null ? expBgChild.gameObject : new GameObject("ExpBarBg");
        expBgObj.transform.SetParent(_hudBadgeRoot.transform, false);

        RectTransform ebRt = expBgObj.GetComponent<RectTransform>();
        if (ebRt == null) ebRt = expBgObj.AddComponent<RectTransform>();
        ebRt.anchorMin = new Vector2(0.32f, 0f);
        ebRt.anchorMax = new Vector2(1f, 1f);
        ebRt.offsetMin = new Vector2(1f, 2f);
        ebRt.offsetMax = new Vector2(-2f, -2f);

        Image ebImg = expBgObj.GetComponent<Image>();
        if (ebImg == null) ebImg = expBgObj.AddComponent<Image>();
        ebImg.color = new Color(0.02f, 0.04f, 0.08f, 1f);

        // EXP Bar İçi (Filled)
        Transform fillChild = expBgObj.transform.Find("Fill");
        GameObject fillObj = fillChild != null ? fillChild.gameObject : new GameObject("Fill");
        fillObj.transform.SetParent(expBgObj.transform, false);

        RectTransform fRt = fillObj.GetComponent<RectTransform>();
        if (fRt == null) fRt = fillObj.AddComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;

        _hudExpFill = fillObj.GetComponent<Image>();
        if (_hudExpFill == null) _hudExpFill = fillObj.AddComponent<Image>();
        _hudExpFill.color = new Color(0f, 0.85f, 1f, 0.95f);
        _hudExpFill.type = Image.Type.Filled;
        _hudExpFill.fillMethod = Image.FillMethod.Horizontal;
        _hudExpFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        // EXP Bar Yazısı
        Transform txtChild = expBgObj.transform.Find("Text");
        GameObject txtObj = txtChild != null ? txtChild.gameObject : new GameObject("Text");
        txtObj.transform.SetParent(expBgObj.transform, false);

        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        if (tRt == null) tRt = txtObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

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
        _dossierModal = new GameObject("PilotDossierModal");
        _dossierModal.transform.SetParent(parent, false);

        RectTransform mRt = _dossierModal.AddComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0.5f, 0.5f);
        mRt.anchorMax = new Vector2(0.5f, 0.5f);
        mRt.pivot = new Vector2(0.5f, 0.5f);
        mRt.anchoredPosition = Vector2.zero;
        mRt.sizeDelta = new Vector2(850f, 680f);

        Image bg = _dossierModal.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.06f, 0.12f, 0.98f);

        Outline outline = _dossierModal.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);

        // Başlık
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform tRt = tObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -25f);
        tRt.sizeDelta = new Vector2(800f, 50f);

        TextMeshProUGUI tTMP = tObj.AddComponent<TextMeshProUGUI>();
        tTMP.text = "<b>HAVA KUVVETLERİ ASKERİ PİLOT HİZMET BELGESİ</b>";
        tTMP.fontSize = 28f;
        tTMP.fontStyle = FontStyles.Bold;
        tTMP.alignment = TextAlignmentOptions.Center;
        tTMP.color = Color.white;

        // Rütbe ve Seviye Alanı
        GameObject rObj = new GameObject("RankArea");
        rObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform rRt = rObj.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.06f, 0.72f);
        rRt.anchorMax = new Vector2(0.94f, 0.90f);
        rRt.offsetMin = Vector2.zero;
        rRt.offsetMax = Vector2.zero;

        Image rBg = rObj.AddComponent<Image>();
        rBg.color = new Color(0.06f, 0.11f, 0.19f, 0.9f);

        Outline rOutline = rObj.AddComponent<Outline>();
        rOutline.effectColor = new Color(1f, 0.85f, 0.2f, 0.85f);
        rOutline.effectDistance = new Vector2(2f, -2f);

        GameObject rTxtObj = new GameObject("Text");
        rTxtObj.transform.SetParent(rObj.transform, false);
        RectTransform rtRt = rTxtObj.AddComponent<RectTransform>();
        rtRt.anchorMin = Vector2.zero;
        rtRt.anchorMax = Vector2.one;
        rtRt.offsetMin = Vector2.zero;
        rtRt.offsetMax = Vector2.zero;

        _dossierRankTMP = rTxtObj.AddComponent<TextMeshProUGUI>();
        _dossierRankTMP.fontSize = 24f;
        _dossierRankTMP.alignment = TextAlignmentOptions.Center;
        _dossierRankTMP.color = Color.white;

        // Savaş İstatistikleri Kutusu (Sol)
        GameObject sObj = new GameObject("StatsBox");
        sObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform sRt = sObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.06f, 0.22f);
        sRt.anchorMax = new Vector2(0.48f, 0.70f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        Image sBg = sObj.AddComponent<Image>();
        sBg.color = new Color(0.05f, 0.09f, 0.16f, 0.9f);

        GameObject sTxtObj = new GameObject("Text");
        sTxtObj.transform.SetParent(sObj.transform, false);
        RectTransform stRt = sTxtObj.AddComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero;
        stRt.anchorMax = Vector2.one;
        stRt.offsetMin = Vector2.zero;
        stRt.offsetMax = Vector2.zero;

        _dossierStatsTMP = sTxtObj.AddComponent<TextMeshProUGUI>();
        _dossierStatsTMP.fontSize = 20f;
        _dossierStatsTMP.lineSpacing = 6f;
        _dossierStatsTMP.alignment = TextAlignmentOptions.MidlineLeft;
        _dossierStatsTMP.margin = new Vector4(20f, 15f, 15f, 15f);
        _dossierStatsTMP.color = new Color(0.88f, 0.94f, 1f);

        // İHA ve Seviye Bonusları Kutusu (Sağ)
        GameObject pObj = new GameObject("PerksBox");
        pObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform pRt = pObj.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.52f, 0.22f);
        pRt.anchorMax = new Vector2(0.94f, 0.70f);
        pRt.offsetMin = Vector2.zero;
        pRt.offsetMax = Vector2.zero;

        Image pBg = pObj.AddComponent<Image>();
        pBg.color = new Color(0.05f, 0.09f, 0.16f, 0.9f);

        GameObject pTxtObj = new GameObject("Text");
        pTxtObj.transform.SetParent(pObj.transform, false);
        RectTransform ptRt = pTxtObj.AddComponent<RectTransform>();
        ptRt.anchorMin = Vector2.zero;
        ptRt.anchorMax = Vector2.one;
        ptRt.offsetMin = Vector2.zero;
        ptRt.offsetMax = Vector2.zero;

        _dossierPerksTMP = pTxtObj.AddComponent<TextMeshProUGUI>();
        _dossierPerksTMP.fontSize = 19f;
        _dossierPerksTMP.lineSpacing = 5f;
        _dossierPerksTMP.alignment = TextAlignmentOptions.MidlineLeft;
        _dossierPerksTMP.margin = new Vector4(20f, 15f, 15f, 15f);
        _dossierPerksTMP.color = new Color(0.88f, 0.94f, 1f);

        // Çağrı Kodunu Değiştir Butonu
        GameObject editNameObj = new GameObject("EditNameBtn");
        editNameObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform enRt = editNameObj.AddComponent<RectTransform>();
        enRt.anchorMin = new Vector2(0.18f, 0.05f);
        enRt.anchorMax = new Vector2(0.48f, 0.16f);
        enRt.offsetMin = Vector2.zero;
        enRt.offsetMax = Vector2.zero;

        Image enImg = editNameObj.AddComponent<Image>();
        enImg.color = new Color(0.12f, 0.45f, 0.65f);

        Outline enOutline = editNameObj.AddComponent<Outline>();
        enOutline.effectColor = new Color(0f, 0.85f, 1f, 1f);
        enOutline.effectDistance = new Vector2(2f, -2f);

        Button enBtn = editNameObj.AddComponent<Button>();
        enBtn.onClick.AddListener(() =>
        {
            PilotLoginUI.EnsureUI()?.OpenLoginModal(() =>
            {
                UpdateDossierContent();
                RefreshBadgeVisuals();
            });
        });

        GameObject enTxtObj = new GameObject("Text");
        enTxtObj.transform.SetParent(editNameObj.transform, false);
        RectTransform entRt = enTxtObj.AddComponent<RectTransform>();
        entRt.anchorMin = Vector2.zero;
        entRt.anchorMax = Vector2.one;
        entRt.offsetMin = Vector2.zero;
        entRt.offsetMax = Vector2.zero;

        TextMeshProUGUI enTMP = enTxtObj.AddComponent<TextMeshProUGUI>();
        enTMP.text = "<b>✏️ ÇAĞRI KODUNU DEĞİŞTİR</b>";
        enTMP.fontSize = 17f;
        enTMP.fontStyle = FontStyles.Bold;
        enTMP.alignment = TextAlignmentOptions.Center;
        enTMP.color = Color.white;

        // Kapat Butonu
        GameObject closeObj = new GameObject("CloseBtn");
        closeObj.transform.SetParent(_dossierModal.transform, false);
        RectTransform cRt = closeObj.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.52f, 0.05f);
        cRt.anchorMax = new Vector2(0.82f, 0.16f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        Image cImg = closeObj.AddComponent<Image>();
        cImg.color = new Color(0.20f, 0.32f, 0.48f);

        Outline cOutline = closeObj.AddComponent<Outline>();
        cOutline.effectColor = new Color(0f, 0.85f, 1f, 1f);
        cOutline.effectDistance = new Vector2(2f, -2f);

        Button cBtn = closeObj.AddComponent<Button>();
        cBtn.onClick.AddListener(CloseDossierModal);

        GameObject cTxtObj = new GameObject("Text");
        cTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform ctRt = cTxtObj.AddComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.offsetMin = Vector2.zero;
        ctRt.offsetMax = Vector2.zero;

        TextMeshProUGUI ctTMP = cTxtObj.AddComponent<TextMeshProUGUI>();
        ctTMP.text = "<b>KAPAT</b>";
        ctTMP.fontSize = 22f;
        ctTMP.fontStyle = FontStyles.Bold;
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
