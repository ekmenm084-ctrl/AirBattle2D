using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Askeri Havacılık Tedarik ve Parça Mağazası Arayüzü:
/// - Gerçek savaş uçağı parçaları (Motor, Radar, Zırh, Elektronik Harp).
/// - 6 Uçak modeli arasında geçiş ve uçak donatım (Loadout) yönetimi.
/// - Seviye/Kademe Uyumluluk Denetimi (Level Incompatibility Warning Box).
/// - Altın ile satın alma, envanter takibi, kuşanma ve çıkarma.
/// </summary>
public class MilitaryStoreUI : MonoBehaviour
{
    public static MilitaryStoreUI Instance { get; private set; }

    public enum StoreTab
    {
        Engine,
        Radar,
        Airframe,
        ElectronicWarfare,
        CurrentLoadout
    }

    private GameObject _panelRoot;
    private TextMeshProUGUI _goldTMP;
    private TextMeshProUGUI _planeInfoTMP;
    private Button _activePlaneToggleBtn;
    private TextMeshProUGUI _activePlaneToggleTMP;

    private StoreTab _currentTab = StoreTab.Engine;
    private HangarManager.AircraftType _selectedAircraft = HangarManager.AircraftType.F16Viper;

    // Tab Butonları
    private Button _tabEngineBtn, _tabRadarBtn, _tabAirframeBtn, _tabEwBtn, _tabLoadoutBtn;
    private GameObject _cardsScrollContainer;
    private GameObject _loadoutContainer;
    private Transform _cardsContentTransform;

    // Uyumsuzluk / Aşırı Yükleme Modal Penceresi
    private GameObject _warningModalObj;
    private TextMeshProUGUI _warningTitleTMP;
    private TextMeshProUGUI _warningDescTMP;

    private static RectTransform GetRect(GameObject go)
    {
        if (go == null) return null;
        return go.GetComponent<RectTransform>();
    }

    public static void MakeFullScreenRect(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static MilitaryStoreUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("MilitaryStoreUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("MilitaryStoreUI", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        MakeFullScreenRect(GetRect(go));
        Instance = go.GetComponent<MilitaryStoreUI>();
        if (Instance == null) Instance = go.AddComponent<MilitaryStoreUI>();
        if (Instance._panelRoot == null) Instance.BuildUI();
        return Instance;
    }

    public void OpenStore()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        MakeFullScreenRect(GetRect(gameObject));

        HangarManager hm = HangarManager.EnsureInstance();
        if (hm != null)
            _selectedAircraft = hm.activeAircraft;

        if (_panelRoot == null)
            BuildUI();

        MakeFullScreenRect(GetRect(_panelRoot));
        _panelRoot.SetActive(true);
        PlayerProfileUI.EnsureUI()?.ShowMenuStatusBar(showGoldBadge: false);

        MilitaryStoreManager.EnsureInstance();

        SwitchTab(_currentTab);
        RefreshAllVisuals();
    }

    public void CloseStore()
    {
        CloseDirectly();
        MainMenuManager.Instance?.OpenMainPanel();
        if (MainMenuManager.Instance == null)
        {
            MainMenuManager menu = FindObjectOfType<MainMenuManager>();
            menu?.OpenMainPanel();
        }
    }

    public void CloseDirectly()
    {
        if (this == null) return;
        try
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (gameObject != null) gameObject.SetActive(false);
        }
        catch { }
    }

    // ==========================================
    // UI İNŞASI (PROCEDURAL BUILD)
    // ==========================================
    private void BuildUI()
    {
        if (_panelRoot != null) return;

        _panelRoot = new GameObject("MilitaryStorePanelRoot", typeof(RectTransform));
        _panelRoot.transform.SetParent(transform, false);
        MakeFullScreenRect(GetRect(_panelRoot));

        // Arka Plan
        Image bgImg = _panelRoot.AddComponent<Image>();
        bgImg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.65f), new Color(0.02f, 0.05f, 0.09f, 0.92f), false);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;

        // 1. Üst Başlık & Altın Barı
        BuildHeader();

        // 2. Uçak Seçim Barı (F-16, A-10, F-22, Su-57, F-35, KAAN)
        BuildAircraftSelectorBar();

        // 3. Kategori Sekmeleri (Motor, Radar, Gövde, EW, Mevcut Donatım)
        BuildCategoryTabs();

        // 4. Parça Kartları Scroll Alanı
        BuildCardsScrollView();

        // 5. Mevcut Donatım (Loadout 4 Yuva) Alanı
        BuildLoadoutView();

        // 6. Uyumsuzluk Uyarı Modalı
        BuildWarningModal();

        _panelRoot.SetActive(false);
    }

    private void BuildHeader()
    {
        // Başlık
        GameObject headerObj = new GameObject("HeaderTitle", typeof(RectTransform));
        headerObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform hRt = GetRect(headerObj);
        hRt.anchorMin = new Vector2(0.5f, 1f);
        hRt.anchorMax = new Vector2(0.5f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -14f);
        hRt.sizeDelta = new Vector2(1080f, 66f);

        Image hBg = headerObj.AddComponent<Image>();
        hBg.sprite = MainMenuManager.GetHudSprite("hud_header_visor", new Color(0f, 0.90f, 1f, 0.85f), new Color(0.02f, 0.05f, 0.10f, 0.88f), false);
        hBg.type = Image.Type.Sliced;
        hBg.color = Color.white;
        hBg.raycastTarget = false;

        GameObject textObj = new GameObject("TitleText", typeof(RectTransform));
        textObj.transform.SetParent(headerObj.transform, false);
        RectTransform tRt = GetRect(textObj);
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(10f, 4f);
        tRt.offsetMax = new Vector2(-10f, -4f);

        TextMeshProUGUI titleTMP = textObj.AddComponent<TextMeshProUGUI>();
        titleTMP.font = MainMenuManager.GetTacticalFont();
        titleTMP.text = "<b><color=#00E5FF>AVIONICS // ASKERİ MÜHİMMAT & TEKNOLOJİ MAĞAZASI</color></b>\n<size=52%><color=#94A3B8>GERÇEK SAVAŞ UÇAĞI AVİYONİK, MOTOR, ZIRH & ELEKTRONİK HARP TEDARİK HATTI</color></size>";
        titleTMP.fontSize = 26f;
        titleTMP.characterSpacing = 2.2f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0.92f, 0.96f, 1.0f);
        titleTMP.outlineWidth = 0.10f;
        titleTMP.outlineColor = new Color(0.02f, 0.04f, 0.08f, 0.95f);

        // Geri Dön Butonu (Sol Alt)
        GameObject backBtnObj = new GameObject("StoreBackButton", typeof(RectTransform));
        backBtnObj.transform.SetParent(_panelRoot.transform, false);
        Image bImg = backBtnObj.AddComponent<Image>();
        bImg.raycastTarget = true;
        Button bBtn = backBtnObj.AddComponent<Button>();
        bBtn.targetGraphic = bImg;
        GameObject bTxtObj = new GameObject("Text", typeof(RectTransform));
        bTxtObj.transform.SetParent(backBtnObj.transform, false);
        bTxtObj.AddComponent<TextMeshProUGUI>();
        MainMenuManager.FormatBottomLeftBackBtn(bBtn, CloseStore);
        backBtnObj.transform.SetAsLastSibling();

        // Altın Rozeti (Sağ Üst)
        GameObject goldBadgeObj = new GameObject("GoldBadge", typeof(RectTransform));
        goldBadgeObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform gRt = GetRect(goldBadgeObj);
        gRt.anchorMin = new Vector2(1f, 1f);
        gRt.anchorMax = new Vector2(1f, 1f);
        gRt.pivot = new Vector2(1f, 1f);
        gRt.anchoredPosition = new Vector2(-40f, -16f);
        gRt.sizeDelta = new Vector2(280f, 52f);

        Image gBg = goldBadgeObj.AddComponent<Image>();
        gBg.sprite = MainMenuManager.GetHudSprite("hud_btn_amber", new Color(1f, 0.82f, 0.2f), new Color(0.08f, 0.12f, 0.18f, 0.95f), true);
        gBg.type = Image.Type.Sliced;
        gBg.color = Color.white;

        GameObject goldTxtObj = new GameObject("GoldText", typeof(RectTransform));
        goldTxtObj.transform.SetParent(goldBadgeObj.transform, false);
        RectTransform gtRt = GetRect(goldTxtObj);
        gtRt.anchorMin = Vector2.zero;
        gtRt.anchorMax = Vector2.one;
        gtRt.offsetMin = new Vector2(10f, 0f);
        gtRt.offsetMax = new Vector2(-10f, 0f);

        _goldTMP = goldTxtObj.AddComponent<TextMeshProUGUI>();
        _goldTMP.font = MainMenuManager.GetTacticalFont();
        _goldTMP.text = "🪙 0 G";
        _goldTMP.fontSize = 22f;
        _goldTMP.characterSpacing = 2.0f;
        _goldTMP.fontStyle = FontStyles.Bold;
        _goldTMP.alignment = TextAlignmentOptions.Center;
        _goldTMP.color = new Color(1f, 0.88f, 0.25f);
        _goldTMP.raycastTarget = false;
    }

    private void BuildAircraftSelectorBar()
    {
        GameObject barObj = new GameObject("AircraftSelectorBar", typeof(RectTransform));
        barObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform barRt = GetRect(barObj);
        barRt.anchorMin = new Vector2(0.5f, 1f);
        barRt.anchorMax = new Vector2(0.5f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, -84f);
        barRt.sizeDelta = new Vector2(1200f, 54f);

        Image barBg = barObj.AddComponent<Image>();
        barBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0.20f, 0.50f, 0.80f), new Color(0.04f, 0.08f, 0.14f, 0.90f), false);
        barBg.type = Image.Type.Sliced;
        barBg.color = Color.white;

        // Önceki Uçak Butonu
        GameObject prevBtnObj = new GameObject("PrevPlaneBtn", typeof(RectTransform));
        prevBtnObj.transform.SetParent(barObj.transform, false);
        RectTransform prevRt = GetRect(prevBtnObj);
        prevRt.anchorMin = new Vector2(0f, 0.5f);
        prevRt.anchorMax = new Vector2(0f, 0.5f);
        prevRt.pivot = new Vector2(0f, 0.5f);
        prevRt.anchoredPosition = new Vector2(20f, 0f);
        prevRt.sizeDelta = new Vector2(140f, 40f);

        Image prevImg = prevBtnObj.AddComponent<Image>();
        prevImg.sprite = MainMenuManager.GetHudSprite("hud_btn_blue", new Color(0.35f, 0.65f, 0.95f), new Color(0.05f, 0.10f, 0.18f, 0.92f), true);
        prevImg.type = Image.Type.Sliced;
        prevImg.color = Color.white;

        Button prevBtn = prevBtnObj.AddComponent<Button>();
        prevBtn.onClick.AddListener(SelectPreviousAircraft);

        GameObject prevTxtObj = new GameObject("Text", typeof(RectTransform));
        prevTxtObj.transform.SetParent(prevBtnObj.transform, false);
        MakeFullScreenRect(GetRect(prevTxtObj));
        TextMeshProUGUI prevTMP = prevTxtObj.AddComponent<TextMeshProUGUI>();
        prevTMP.font = MainMenuManager.GetTacticalFont();
        prevTMP.text = "◀ ÖNCEKİ";
        prevTMP.fontSize = 18f;
        prevTMP.characterSpacing = 1.8f;
        prevTMP.fontStyle = FontStyles.Bold;
        prevTMP.alignment = TextAlignmentOptions.Center;
        prevTMP.color = Color.white;

        // Sonraki Uçak Butonu
        GameObject nextBtnObj = new GameObject("NextPlaneBtn", typeof(RectTransform));
        nextBtnObj.transform.SetParent(barObj.transform, false);
        RectTransform nextRt = GetRect(nextBtnObj);
        nextRt.anchorMin = new Vector2(1f, 0.5f);
        nextRt.anchorMax = new Vector2(1f, 0.5f);
        nextRt.pivot = new Vector2(1f, 0.5f);
        nextRt.anchoredPosition = new Vector2(-20f, 0f);
        nextRt.sizeDelta = new Vector2(140f, 40f);

        Image nextImg = nextBtnObj.AddComponent<Image>();
        nextImg.sprite = MainMenuManager.GetHudSprite("hud_btn_blue", new Color(0.35f, 0.65f, 0.95f), new Color(0.05f, 0.10f, 0.18f, 0.92f), true);
        nextImg.type = Image.Type.Sliced;
        nextImg.color = Color.white;

        Button nextBtn = nextBtnObj.AddComponent<Button>();
        nextBtn.onClick.AddListener(SelectNextAircraft);

        GameObject nextTxtObj = new GameObject("Text", typeof(RectTransform));
        nextTxtObj.transform.SetParent(nextBtnObj.transform, false);
        MakeFullScreenRect(GetRect(nextTxtObj));
        TextMeshProUGUI nextTMP = nextTxtObj.AddComponent<TextMeshProUGUI>();
        nextTMP.font = MainMenuManager.GetTacticalFont();
        nextTMP.text = "SONRAKİ ▶";
        nextTMP.fontSize = 18f;
        nextTMP.characterSpacing = 1.8f;
        nextTMP.fontStyle = FontStyles.Bold;
        nextTMP.alignment = TextAlignmentOptions.Center;
        nextTMP.color = Color.white;

        // Ortadaki Uçak Bilgisi
        GameObject infoObj = new GameObject("SelectedPlaneInfo", typeof(RectTransform));
        infoObj.transform.SetParent(barObj.transform, false);
        RectTransform infoRt = GetRect(infoObj);
        infoRt.anchorMin = new Vector2(0.5f, 0.5f);
        infoRt.anchorMax = new Vector2(0.5f, 0.5f);
        infoRt.pivot = new Vector2(0.5f, 0.5f);
        infoRt.anchoredPosition = new Vector2(-70f, 0f);
        infoRt.sizeDelta = new Vector2(620f, 48f);

        _planeInfoTMP = infoObj.AddComponent<TextMeshProUGUI>();
        _planeInfoTMP.font = MainMenuManager.GetTacticalFont();
        _planeInfoTMP.text = "<b>F-16C Viper</b> (SEVİYE 20)";
        _planeInfoTMP.fontSize = 22f;
        _planeInfoTMP.characterSpacing = 1.8f;
        _planeInfoTMP.alignment = TextAlignmentOptions.Center;
        _planeInfoTMP.color = Color.white;

        // Uçağı Aktif Seç Butonu (Harekâtta Kullan)
        GameObject activeToggleObj = new GameObject("ActiveToggleBtn", typeof(RectTransform));
        activeToggleObj.transform.SetParent(barObj.transform, false);
        RectTransform atRt = GetRect(activeToggleObj);
        atRt.anchorMin = new Vector2(0.5f, 0.5f);
        atRt.anchorMax = new Vector2(0.5f, 0.5f);
        atRt.pivot = new Vector2(0.5f, 0.5f);
        atRt.anchoredPosition = new Vector2(330f, 0f);
        atRt.sizeDelta = new Vector2(200f, 40f);

        Image atImg = activeToggleObj.AddComponent<Image>();
        atImg.sprite = MainMenuManager.GetHudSprite("hud_btn_emerald", new Color(0.18f, 0.85f, 0.45f), new Color(0.04f, 0.14f, 0.08f, 0.95f), true);
        atImg.type = Image.Type.Sliced;
        atImg.color = Color.white;

        _activePlaneToggleBtn = activeToggleObj.AddComponent<Button>();
        _activePlaneToggleBtn.onClick.AddListener(ToggleMakeSelectedAircraftActive);

        GameObject atTxtObj = new GameObject("Text", typeof(RectTransform));
        atTxtObj.transform.SetParent(activeToggleObj.transform, false);
        MakeFullScreenRect(GetRect(atTxtObj));
        _activePlaneToggleTMP = atTxtObj.AddComponent<TextMeshProUGUI>();
        _activePlaneToggleTMP.font = MainMenuManager.GetTacticalFont();
        _activePlaneToggleTMP.text = "HAREKÂTA SEÇ";
        _activePlaneToggleTMP.fontSize = 18f;
        _activePlaneToggleTMP.characterSpacing = 1.8f;
        _activePlaneToggleTMP.fontStyle = FontStyles.Bold;
        _activePlaneToggleTMP.alignment = TextAlignmentOptions.Center;
        _activePlaneToggleTMP.color = Color.white;
    }

    private void BuildCategoryTabs()
    {
        GameObject tabsObj = new GameObject("CategoryTabs", typeof(RectTransform));
        tabsObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform tRt = GetRect(tabsObj);
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -148f);
        tRt.sizeDelta = new Vector2(0f, 50f);

        HorizontalLayoutGroup hlg = tabsObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        float tabW = 230f;
        _tabEngineBtn = CreateTabButton(tabsObj.transform, "⚡ MOTOR & İTKİ", tabW, () => SwitchTab(StoreTab.Engine));
        _tabRadarBtn = CreateTabButton(tabsObj.transform, "📡 RADAR / AVİYONİK", tabW, () => SwitchTab(StoreTab.Radar));
        _tabAirframeBtn = CreateTabButton(tabsObj.transform, "🛡️ GÖVDE & ZIRH", tabW, () => SwitchTab(StoreTab.Airframe));
        _tabEwBtn = CreateTabButton(tabsObj.transform, "🛰️ ELEKTRONİK HARP", tabW, () => SwitchTab(StoreTab.ElectronicWarfare));
        _tabLoadoutBtn = CreateTabButton(tabsObj.transform, "📋 MEVCUT DONATIM", tabW, () => SwitchTab(StoreTab.CurrentLoadout));
    }

    private Button CreateTabButton(Transform parent, string title, float width, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("Tab_" + title, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = GetRect(btnObj);
        rt.sizeDelta = new Vector2(width, 46f);

        Image img = btnObj.AddComponent<Image>();
        img.sprite = MainMenuManager.GetHudSprite("hud_btn_slate", new Color(0.40f, 0.50f, 0.65f), new Color(0.03f, 0.05f, 0.09f, 0.88f), true);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        MakeFullScreenRect(GetRect(txtObj));

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = title;
        tmp.fontSize = 19f;
        tmp.characterSpacing = 1.8f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.70f, 0.80f, 0.90f);

        return btn;
    }

    private void BuildCardsScrollView()
    {
        _cardsScrollContainer = new GameObject("CardsScrollContainer", typeof(RectTransform));
        _cardsScrollContainer.transform.SetParent(_panelRoot.transform, false);
        RectTransform sRt = GetRect(_cardsScrollContainer);
        sRt.anchorMin = new Vector2(0f, 0f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.offsetMin = new Vector2(25f, 90f);
        sRt.offsetMax = new Vector2(-25f, -218f);

        Image scrollBg = _cardsScrollContainer.AddComponent<Image>();
        scrollBg.color = new Color(0.06f, 0.09f, 0.14f, 0.85f);
        _cardsScrollContainer.AddComponent<Mask>().showMaskGraphic = true;

        ScrollRect sr = _cardsScrollContainer.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 35f;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(_cardsScrollContainer.transform, false);
        RectTransform cRt = GetRect(content);
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;
        cRt.sizeDelta = new Vector2(0f, 800f);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(16, 16, 16, 24);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = cRt;
        _cardsContentTransform = content.transform;
    }

    private void BuildLoadoutView()
    {
        _loadoutContainer = new GameObject("LoadoutContainer", typeof(RectTransform));
        _loadoutContainer.transform.SetParent(_panelRoot.transform, false);
        RectTransform lRt = GetRect(_loadoutContainer);
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 1f);
        lRt.offsetMin = new Vector2(25f, 90f);
        lRt.offsetMax = new Vector2(-25f, -218f);

        Image lBg = _loadoutContainer.AddComponent<Image>();
        lBg.color = new Color(0.05f, 0.08f, 0.13f, 0.95f);

        // İçerik dinamik olarak RefreshLoadoutView'da doldurulacaktır
        _loadoutContainer.SetActive(false);
    }

    private void BuildWarningModal()
    {
        _warningModalObj = new GameObject("IncompatibilityWarningModal", typeof(RectTransform));
        _warningModalObj.transform.SetParent(_panelRoot.transform, false);
        MakeFullScreenRect(GetRect(_warningModalObj));

        // Karartma Arka Planı
        Image dim = _warningModalObj.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        // Kutu
        GameObject boxObj = new GameObject("Box", typeof(RectTransform));
        boxObj.transform.SetParent(_warningModalObj.transform, false);
        RectTransform bRt = GetRect(boxObj);
        bRt.anchorMin = new Vector2(0.5f, 0.5f);
        bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.sizeDelta = new Vector2(700f, 400f);

        Image bBg = boxObj.AddComponent<Image>();
        bBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0.95f, 0.25f, 0.25f), new Color(0.08f, 0.04f, 0.05f, 0.98f), false);
        bBg.type = Image.Type.Sliced;
        bBg.color = Color.white;

        // Modal Başlık
        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(boxObj.transform, false);
        RectTransform tRt = GetRect(titleObj);
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -22f);
        tRt.sizeDelta = new Vector2(-30f, 48f);

        _warningTitleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        _warningTitleTMP.font = MainMenuManager.GetTacticalFont();
        _warningTitleTMP.text = "⚠️ <b>UYUMSUZLUK / AŞIRI YÜKLEME UYARISI</b>";
        _warningTitleTMP.fontSize = 26f;
        _warningTitleTMP.characterSpacing = 2.0f;
        _warningTitleTMP.fontStyle = FontStyles.Bold;
        _warningTitleTMP.alignment = TextAlignmentOptions.Center;
        _warningTitleTMP.color = new Color(1f, 0.35f, 0.35f);

        // Modal Açıklama
        GameObject descObj = new GameObject("Desc", typeof(RectTransform));
        descObj.transform.SetParent(boxObj.transform, false);
        RectTransform dRt = GetRect(descObj);
        dRt.anchorMin = new Vector2(0f, 0f);
        dRt.anchorMax = new Vector2(1f, 1f);
        dRt.offsetMin = new Vector2(25f, 85f);
        dRt.offsetMax = new Vector2(-25f, -75f);

        _warningDescTMP = descObj.AddComponent<TextMeshProUGUI>();
        _warningDescTMP.font = MainMenuManager.GetTacticalFont();
        _warningDescTMP.text = "Bu parça seçili uçağa monte edilemez!";
        _warningDescTMP.fontSize = 21f;
        _warningDescTMP.characterSpacing = 1.4f;
        _warningDescTMP.lineSpacing = 3.0f;
        _warningDescTMP.alignment = TextAlignmentOptions.Center;
        _warningDescTMP.color = new Color(0.92f, 0.92f, 0.95f);

        // Anladım / Kapat Butonu
        GameObject okBtnObj = new GameObject("OkBtn", typeof(RectTransform));
        okBtnObj.transform.SetParent(boxObj.transform, false);
        RectTransform okRt = GetRect(okBtnObj);
        okRt.anchorMin = new Vector2(0.5f, 0f);
        okRt.anchorMax = new Vector2(0.5f, 0f);
        okRt.pivot = new Vector2(0.5f, 0f);
        okRt.anchoredPosition = new Vector2(0f, 22f);
        okRt.sizeDelta = new Vector2(220f, 50f);

        Image okImg = okBtnObj.AddComponent<Image>();
        okImg.sprite = MainMenuManager.GetHudSprite("hud_btn_red", new Color(0.95f, 0.25f, 0.25f), new Color(0.12f, 0.04f, 0.05f, 0.95f), true);
        okImg.type = Image.Type.Sliced;
        okImg.color = Color.white;

        Button okBtn = okBtnObj.AddComponent<Button>();
        okBtn.onClick.AddListener(() => _warningModalObj.SetActive(false));

        GameObject okTxtObj = new GameObject("Text", typeof(RectTransform));
        okTxtObj.transform.SetParent(okBtnObj.transform, false);
        MakeFullScreenRect(GetRect(okTxtObj));

        TextMeshProUGUI okTMP = okTxtObj.AddComponent<TextMeshProUGUI>();
        okTMP.font = MainMenuManager.GetTacticalFont();
        okTMP.text = "ANLADIM";
        okTMP.fontSize = 22f;
        okTMP.characterSpacing = 2.0f;
        okTMP.fontStyle = FontStyles.Bold;
        okTMP.alignment = TextAlignmentOptions.Center;
        okTMP.color = Color.white;

        _warningModalObj.SetActive(false);
    }

    public void ShowWarningModal(string title, string description)
    {
        if (_warningModalObj == null) return;
        _warningModalObj.SetActive(true);
        _warningModalObj.transform.SetAsLastSibling();
        if (_warningTitleTMP != null) _warningTitleTMP.text = title;
        if (_warningDescTMP != null) _warningDescTMP.text = description;
        AudioManager.Instance?.PlayExplosion(false);
    }

    // ==========================================
    // SEKME & UÇAK SEÇİM MANTIĞI
    // ==========================================
    public void SwitchTab(StoreTab tab)
    {
        _currentTab = tab;

        bool isLoadout = (tab == StoreTab.CurrentLoadout);
        if (_cardsScrollContainer != null) _cardsScrollContainer.SetActive(!isLoadout);
        if (_loadoutContainer != null) _loadoutContainer.SetActive(isLoadout);

        UpdateTabVisual(_tabEngineBtn, "⚡ MOTOR & İTKİ", tab == StoreTab.Engine);
        UpdateTabVisual(_tabRadarBtn, "📡 RADAR / AVİYONİK", tab == StoreTab.Radar);
        UpdateTabVisual(_tabAirframeBtn, "🛡️ GÖVDE & ZIRH", tab == StoreTab.Airframe);
        UpdateTabVisual(_tabEwBtn, "🛰️ ELEKTRONİK HARP", tab == StoreTab.ElectronicWarfare);
        UpdateTabVisual(_tabLoadoutBtn, "📋 MEVCUT DONATIM", tab == StoreTab.CurrentLoadout);

        RefreshAllVisuals();
    }

    private void UpdateTabVisual(Button btn, string title, bool isActive)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();

        if (img != null)
        {
            string spriteName = isActive ? "hud_btn_cyan" : "hud_btn_slate";
            Color borderCol = isActive ? new Color(0f, 0.90f, 1f) : new Color(0.40f, 0.50f, 0.65f);
            Color glassCol = isActive ? new Color(0.04f, 0.12f, 0.20f, 0.95f) : new Color(0.03f, 0.05f, 0.09f, 0.88f);
            img.sprite = MainMenuManager.GetHudSprite(spriteName, borderCol, glassCol, true);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }

        if (tmp != null)
        {
            tmp.font = MainMenuManager.GetTacticalFont();
            tmp.text = isActive ? $"<b>{title}</b>" : title;
            tmp.characterSpacing = 1.8f;
            tmp.color = isActive ? Color.white : new Color(0.70f, 0.80f, 0.90f);
        }
    }

    public void SelectPreviousAircraft()
    {
        int cur = (int)_selectedAircraft;
        cur = (cur - 1 + 6) % 6;
        _selectedAircraft = (HangarManager.AircraftType)cur;
        RefreshAllVisuals();
    }

    public void SelectNextAircraft()
    {
        int cur = (int)_selectedAircraft;
        cur = (cur + 1) % 6;
        _selectedAircraft = (HangarManager.AircraftType)cur;
        RefreshAllVisuals();
    }

    public void ToggleMakeSelectedAircraftActive()
    {
        HangarManager hm = HangarManager.EnsureInstance();
        if (hm == null) return;

        // Uçak kilitli mi kontrol et
        if (!hm.IsAircraftUnlocked(_selectedAircraft))
        {
            ShowWarningModal("⚠️ UÇAK KİLİTLİ!", $"{MilitaryStoreManager.GetAircraftDisplayName(_selectedAircraft)} henüz filonuzda açık değil!\nÖnce Hangar ekranından uçağın kilidini açmalısınız.");
            return;
        }

        hm.SelectAircraft(_selectedAircraft);
        RefreshAllVisuals();
        AudioManager.Instance?.PlayRewardFanfare();
    }

    // ==========================================
    // VERİ YENİLEME (REFRESH VISUALS)
    // ==========================================
    public void RefreshAllVisuals()
    {
        HangarManager hm = HangarManager.EnsureInstance();
        MilitaryStoreManager sm = MilitaryStoreManager.EnsureInstance();
        if (hm == null || sm == null) return;

        // 1. Altın Kasası
        if (_goldTMP != null)
            _goldTMP.text = $"🪙 {hm.gold:N0} G";

        // 2. Seçili Uçak Başlığı
        int planeLevel = MilitaryStoreManager.GetAircraftLevel(_selectedAircraft);
        string planeName = MilitaryStoreManager.GetAircraftDisplayName(_selectedAircraft);
        bool isActiveJet = (hm.activeAircraft == _selectedAircraft);
        bool isUnlockedJet = hm.IsAircraftUnlocked(_selectedAircraft);

        if (_planeInfoTMP != null)
        {
            string activeTag = isActiveJet ? " <color=#4ADE80>[HAREKÂT JETİ]</color>" : "";
            string lockTag = isUnlockedJet ? "" : " <color=#F87171>[HANGARDA KİLİTLİ]</color>";
            _planeInfoTMP.text = $"<b>{planeName}</b>{activeTag}{lockTag}";
        }

        if (_activePlaneToggleBtn != null && _activePlaneToggleTMP != null)
        {
            if (isActiveJet)
            {
                _activePlaneToggleTMP.text = "✔ AKTİF SAVAŞ JETİ";
                _activePlaneToggleBtn.interactable = false;
                _activePlaneToggleBtn.GetComponent<Image>().color = new Color(0.12f, 0.40f, 0.22f);
            }
            else
            {
                _activePlaneToggleTMP.text = isUnlockedJet ? "HAREKÂTA SEÇ" : "KİLİTLİ JET";
                _activePlaneToggleBtn.interactable = isUnlockedJet;
                _activePlaneToggleBtn.GetComponent<Image>().color = isUnlockedJet ? new Color(0.18f, 0.55f, 0.34f) : new Color(0.30f, 0.25f, 0.25f);
            }
        }

        // 3. İçerik Yenileme
        if (_currentTab == StoreTab.CurrentLoadout)
        {
            RefreshLoadoutView(sm, hm);
        }
        else
        {
            RefreshCardsView(sm, hm);
        }
    }

    private void RefreshCardsView(MilitaryStoreManager sm, HangarManager hm)
    {
        if (_cardsContentTransform == null) return;

        // Eski kartları temizle
        for (int i = _cardsContentTransform.childCount - 1; i >= 0; i--)
        {
            Destroy(_cardsContentTransform.GetChild(i).gameObject);
        }

        AircraftPartCategory cat = AircraftPartCategory.Engine;
        switch (_currentTab)
        {
            case StoreTab.Engine: cat = AircraftPartCategory.Engine; break;
            case StoreTab.Radar: cat = AircraftPartCategory.Radar; break;
            case StoreTab.Airframe: cat = AircraftPartCategory.Airframe; break;
            case StoreTab.ElectronicWarfare: cat = AircraftPartCategory.ElectronicWarfare; break;
        }

        List<AircraftPart> parts = sm.GetPartsByCategory(cat);
        int planeLevel = MilitaryStoreManager.GetAircraftLevel(_selectedAircraft);
        string equippedPartId = sm.GetEquippedPartId(_selectedAircraft, cat);

        foreach (var part in parts)
        {
            CreatePartCard(part, planeLevel, equippedPartId, sm, hm);
        }
    }

    private void CreatePartCard(AircraftPart part, int planeLevel, string equippedPartId, MilitaryStoreManager sm, HangarManager hm)
    {
        bool isUnlocked = sm.IsPartUnlocked(part.id);
        bool isEquipped = (equippedPartId == part.id);
        bool isCompatible = (planeLevel >= part.minLevel);

        string compPlanes = MilitaryStoreManager.GetCompatibleAircraftNames(part.minLevel);
        string currentPlaneShort = MilitaryStoreManager.GetShortAircraftName(_selectedAircraft);

        GameObject cardObj = new GameObject("Card_" + part.id, typeof(RectTransform));
        cardObj.transform.SetParent(_cardsContentTransform, false);
        RectTransform cRt = GetRect(cardObj);
        cRt.sizeDelta = new Vector2(0f, 185f);

        Color cardBorderCol = isEquipped ? new Color(0.2f, 0.90f, 0.45f) : (isCompatible ? new Color(0.20f, 0.55f, 0.85f) : new Color(0.85f, 0.25f, 0.25f));
        Image cBg = cardObj.AddComponent<Image>();
        cBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", cardBorderCol, new Color(0.03f, 0.06f, 0.11f, 0.95f), false);
        cBg.type = Image.Type.Sliced;
        cBg.color = Color.white;

        // Başlık ve Uyumlu Uçaklar (Sol Üst)
        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = GetRect(titleObj);
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0f, 1f);
        tRt.anchoredPosition = new Vector2(24f, -12f);
        tRt.sizeDelta = new Vector2(-320f, 56f);

        TextMeshProUGUI tTMP = titleObj.AddComponent<TextMeshProUGUI>();
        tTMP.font = MainMenuManager.GetTacticalFont();
        tTMP.text = $"{part.GetCategoryIcon()} <b>{part.name}</b>\n<size=85%><color=#38BDF8>✈ Uyumlu Uçaklar: <b>{compPlanes}</b></color></size>";
        tTMP.fontSize = 22f;
        tTMP.characterSpacing = 1.6f;
        tTMP.color = Color.white;

        // Gerçek Askeri Bilgi (Lore) (Orta)
        GameObject loreObj = new GameObject("Lore", typeof(RectTransform));
        loreObj.transform.SetParent(cardObj.transform, false);
        RectTransform lRt = GetRect(loreObj);
        lRt.anchorMin = new Vector2(0f, 1f);
        lRt.anchorMax = new Vector2(1f, 1f);
        lRt.pivot = new Vector2(0f, 1f);
        lRt.anchoredPosition = new Vector2(24f, -70f);
        lRt.sizeDelta = new Vector2(-320f, 52f);

        TextMeshProUGUI lTMP = loreObj.AddComponent<TextMeshProUGUI>();
        lTMP.font = MainMenuManager.GetTacticalFont();
        lTMP.text = $"<color=#94A3B8><i>{part.realWorldLore}</i></color>";
        lTMP.fontSize = 17f;

        // Muharebe Bonusları (Sol Alt)
        GameObject statsObj = new GameObject("Stats", typeof(RectTransform));
        statsObj.transform.SetParent(cardObj.transform, false);
        RectTransform stRt = GetRect(statsObj);
        stRt.anchorMin = new Vector2(0f, 0f);
        stRt.anchorMax = new Vector2(1f, 0f);
        stRt.pivot = new Vector2(0f, 0f);
        stRt.anchoredPosition = new Vector2(24f, 14f);
        stRt.sizeDelta = new Vector2(-320f, 36f);

        TextMeshProUGUI stTMP = statsObj.AddComponent<TextMeshProUGUI>();
        stTMP.font = MainMenuManager.GetTacticalFont();
        stTMP.text = $"<b>BONUS:</b> {part.GetStatsSummary()}";
        stTMP.fontSize = 19f;
        stTMP.characterSpacing = 1.4f;

        // Sağ Aksiyon Butonu (Satın Al / Donat / Çıkar / Uyumsuzluk Uyarısı)
        GameObject actBtnObj = new GameObject("ActionButton", typeof(RectTransform));
        actBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform aRt = GetRect(actBtnObj);
        aRt.anchorMin = new Vector2(1f, 0.5f);
        aRt.anchorMax = new Vector2(1f, 0.5f);
        aRt.pivot = new Vector2(1f, 0.5f);
        aRt.anchoredPosition = new Vector2(-22f, 0f);
        aRt.sizeDelta = new Vector2(260f, 90f);

        Image aImg = actBtnObj.AddComponent<Image>();
        aImg.type = Image.Type.Sliced;
        aImg.color = Color.white;
        Button aBtn = actBtnObj.AddComponent<Button>();

        GameObject aTxtObj = new GameObject("Text", typeof(RectTransform));
        aTxtObj.transform.SetParent(actBtnObj.transform, false);
        MakeFullScreenRect(GetRect(aTxtObj));
        TextMeshProUGUI aTMP = aTxtObj.AddComponent<TextMeshProUGUI>();
        aTMP.font = MainMenuManager.GetTacticalFont();
        aTMP.alignment = TextAlignmentOptions.Center;
        aTMP.characterSpacing = 1.6f;
        aTMP.fontStyle = FontStyles.Bold;

        if (!isUnlocked)
        {
            // DURUM 1: SATIN ALINMAMIŞ
            bool canAfford = (hm.gold >= part.price);
            aImg.sprite = MainMenuManager.GetHudSprite(canAfford ? "hud_btn_emerald" : "hud_btn_slate", canAfford ? new Color(0.18f, 0.85f, 0.45f) : new Color(0.40f, 0.40f, 0.45f), new Color(0.04f, 0.10f, 0.06f, 0.95f), true);
            aTMP.text = $"SATIN AL\n<size=88%><color=#FDE047>{part.price:N0} ALTIN</color></size>";
            aTMP.fontSize = 21f;

            aBtn.onClick.AddListener(() =>
            {
                if (sm.BuyPart(part.id, out string msg))
                {
                    AudioManager.Instance?.PlayRewardFanfare();
                    RefreshAllVisuals();
                }
                else
                {
                    ShowWarningModal("YETERSİZ FON!", msg);
                }
            });
        }
        else if (isEquipped)
        {
            // DURUM 2: BU UÇAKTA DONATILMIŞ (ÇIKAR)
            aImg.sprite = MainMenuManager.GetHudSprite("hud_btn_slate", new Color(0.35f, 0.70f, 0.55f), new Color(0.04f, 0.10f, 0.07f, 0.95f), true);
            aTMP.text = "✔ DONATILDI\n<size=80%><color=#FCA5A5>(Çıkarmak İçin Tıkla)</color></size>";
            aTMP.fontSize = 20f;

            aBtn.onClick.AddListener(() =>
            {
                sm.UnequipPart(_selectedAircraft, part.category);
                RefreshAllVisuals();
            });
        }
        else if (isCompatible)
        {
            // DURUM 3: SATIN ALINMIŞ & UYUMLU (DONAT)
            aImg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f), new Color(0.04f, 0.09f, 0.18f, 0.95f), true);
            aTMP.text = "UÇAĞA MONTE ET";
            aTMP.fontSize = 21f;

            aBtn.onClick.AddListener(() =>
            {
                if (sm.EquipPart(_selectedAircraft, part.id, out string res))
                {
                    AudioManager.Instance?.PlayRewardFanfare();
                    RefreshAllVisuals();
                }
            });
        }
        else
        {
            // DURUM 4: SATIN ALINMIŞ ANCAK SEÇİLİ UÇAK DESTEKLEMİYOR
            aImg.sprite = MainMenuManager.GetHudSprite("hud_btn_red", new Color(0.95f, 0.25f, 0.25f), new Color(0.12f, 0.04f, 0.05f, 0.95f), true);
            aTMP.text = $"⚠️ {currentPlaneShort} DESTEKLEMEZ\n<size=80%><color=#FECACA>(Uçak Uyumsuz)</color></size>";
            aTMP.fontSize = 19f;

            aBtn.onClick.AddListener(() =>
            {
                string planeTitle = MilitaryStoreManager.GetAircraftDisplayName(_selectedAircraft);
                string err = $"⚠️ <b>UÇAK DONANIM UYUMSUZLUĞU!</b>\n\n<b>{part.name}</b> donanımı şu uçaklar için özel üretilmiştir:\n<color=#38BDF8><b>{compPlanes}</b></color>\n\nSeçili uçağınız (<b>{planeTitle}</b>) bu parçayı desteklememektedir!\n\nLütfen bu parçayı takabilen bir uçak seçiniz.";
                ShowWarningModal("⚠️ UÇAK UYUMSUZLUĞU", err);
            });
        }
    }

    private void RefreshLoadoutView(MilitaryStoreManager sm, HangarManager hm)
    {
        if (_loadoutContainer == null) return;

        // Eski içeriği temizle
        for (int i = _loadoutContainer.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(_loadoutContainer.transform.GetChild(i).gameObject);
        }

        // Başlık
        GameObject headerObj = new GameObject("LoadoutHeader", typeof(RectTransform));
        headerObj.transform.SetParent(_loadoutContainer.transform, false);
        RectTransform hRt = GetRect(headerObj);
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -10f);
        hRt.sizeDelta = new Vector2(-40f, 48f);

        TextMeshProUGUI hTMP = headerObj.AddComponent<TextMeshProUGUI>();
        hTMP.font = MainMenuManager.GetTacticalFont();
        hTMP.text = $"<b>{MilitaryStoreManager.GetAircraftDisplayName(_selectedAircraft)} — 4 ASKERİ DONATIM YUVASI</b>";
        hTMP.fontSize = 25f;
        hTMP.characterSpacing = 2.0f;
        hTMP.alignment = TextAlignmentOptions.Center;
        hTMP.color = new Color(0.38f, 0.74f, 0.97f);

        // 4 Yuva Paneli
        AircraftPartCategory[] cats = {
            AircraftPartCategory.Engine,
            AircraftPartCategory.Radar,
            AircraftPartCategory.Airframe,
            AircraftPartCategory.ElectronicWarfare
        };

        float startY = -68f;
        float slotHeight = 105f;
        float spacing = 12f;

        for (int i = 0; i < cats.Length; i++)
        {
            var cat = cats[i];
            AircraftPart p = sm.GetEquippedPart(_selectedAircraft, cat);
            CreateSlotRow(_loadoutContainer.transform, cat, p, startY - (i * (slotHeight + spacing)), sm);
        }

        // Toplam Muharebe Statları Özeti (En Altta)
        MilitaryStoreManager.LoadoutStats totalStats = sm.GetLoadoutStats(_selectedAircraft);

        GameObject summaryObj = new GameObject("TotalStatsSummary", typeof(RectTransform));
        summaryObj.transform.SetParent(_loadoutContainer.transform, false);
        RectTransform sRt = GetRect(summaryObj);
        sRt.anchorMin = new Vector2(0f, 0f);
        sRt.anchorMax = new Vector2(1f, 0f);
        sRt.pivot = new Vector2(0.5f, 0f);
        sRt.anchoredPosition = new Vector2(0f, 12f);
        sRt.sizeDelta = new Vector2(-40f, 95f);

        Image sBg = summaryObj.AddComponent<Image>();
        sBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0.20f, 0.85f, 0.60f), new Color(0.04f, 0.08f, 0.14f, 0.95f), false);
        sBg.type = Image.Type.Sliced;
        sBg.color = Color.white;

        GameObject sTxtObj = new GameObject("Text", typeof(RectTransform));
        sTxtObj.transform.SetParent(summaryObj.transform, false);
        MakeFullScreenRect(GetRect(sTxtObj));

        TextMeshProUGUI sTMP = sTxtObj.AddComponent<TextMeshProUGUI>();
        sTMP.font = MainMenuManager.GetTacticalFont();
        sTMP.alignment = TextAlignmentOptions.Center;
        sTMP.fontSize = 20f;
        sTMP.characterSpacing = 1.4f;

        string statsStr = $"<b>TOPLAM DONATIM GÜÇLENDİRMELERİ:</b>\n" +
            $"<color=#38BDF8>+{totalStats.bonusSpeed:0.1f} Hız</color>  |  " +
            $"<color=#4ADE80>+{totalStats.bonusHp:0} Gövde HP</color>  |  " +
            $"<color=#F87171>+{totalStats.bonusDamage:0} Dmg</color>  |  " +
            $"<color=#FBBF24>+%{totalStats.bonusCrit:0} Kritik</color>  |  " +
            $"<color=#67E8F9>+{totalStats.bonusShield:0} Kalkan</color>  |  " +
            $"<color=#34D399>-%{totalStats.damageReductionPercent * 100f:0} Hasar Emilimi</color>  |  " +
            $"<color=#F472B6>+%{totalStats.ultiBonusPercent * 100f:0} Ulti Hızı</color>";
        sTMP.text = statsStr;
    }

    private void CreateSlotRow(Transform parent, AircraftPartCategory cat, AircraftPart part, float yPos, MilitaryStoreManager sm)
    {
        GameObject rowObj = new GameObject("Slot_" + cat, typeof(RectTransform));
        rowObj.transform.SetParent(parent, false);
        RectTransform rRt = GetRect(rowObj);
        rRt.anchorMin = new Vector2(0f, 1f);
        rRt.anchorMax = new Vector2(1f, 1f);
        rRt.pivot = new Vector2(0.5f, 1f);
        rRt.anchoredPosition = new Vector2(0f, yPos);
        rRt.sizeDelta = new Vector2(-40f, 105f);

        Image rBg = rowObj.AddComponent<Image>();
        rBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", (part != null) ? new Color(0.20f, 0.50f, 0.80f) : new Color(0.25f, 0.30f, 0.40f), new Color(0.04f, 0.07f, 0.12f, 0.92f), false);
        rBg.type = Image.Type.Sliced;
        rBg.color = Color.white;

        // Kategori Rozeti (Sol)
        string catIcon = "⚙️";
        string catName = "YUVA";
        switch (cat)
        {
            case AircraftPartCategory.Engine: catIcon = "⚡"; catName = "MOTOR & İTKİ YUVASI"; break;
            case AircraftPartCategory.Radar: catIcon = "📡"; catName = "RADAR & AVİYONİK YUVASI"; break;
            case AircraftPartCategory.Airframe: catIcon = "🛡️"; catName = "GÖVDE & ZIRH YUVASI"; break;
            case AircraftPartCategory.ElectronicWarfare: catIcon = "🛰️"; catName = "ELEKTRONİK HARP YUVASI"; break;
        }

        GameObject textObj = new GameObject("SlotText", typeof(RectTransform));
        textObj.transform.SetParent(rowObj.transform, false);
        RectTransform tRt = GetRect(textObj);
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(24f, 0f);
        tRt.offsetMax = new Vector2(-220f, 0f);

        TextMeshProUGUI tTMP = textObj.AddComponent<TextMeshProUGUI>();
        tTMP.font = MainMenuManager.GetTacticalFont();
        tTMP.alignment = TextAlignmentOptions.MidlineLeft;

        if (part != null)
        {
            tTMP.text = $"<size=85%><color=#7DD3FC>{catIcon} {catName}</color></size>\n<b>{part.name}</b>  •  {part.GetStatsSummary()}";
            tTMP.fontSize = 21f;
            tTMP.characterSpacing = 1.4f;

            // Çıkar Butonu (Sağ)
            GameObject unequipBtnObj = new GameObject("UnequipBtn", typeof(RectTransform));
            unequipBtnObj.transform.SetParent(rowObj.transform, false);
            RectTransform uRt = GetRect(unequipBtnObj);
            uRt.anchorMin = new Vector2(1f, 0.5f);
            uRt.anchorMax = new Vector2(1f, 0.5f);
            uRt.pivot = new Vector2(1f, 0.5f);
            uRt.anchoredPosition = new Vector2(-20f, 0f);
            uRt.sizeDelta = new Vector2(180f, 50f);

            Image uImg = unequipBtnObj.AddComponent<Image>();
            uImg.sprite = MainMenuManager.GetHudSprite("hud_btn_red", new Color(0.95f, 0.25f, 0.25f), new Color(0.12f, 0.04f, 0.05f, 0.95f), true);
            uImg.type = Image.Type.Sliced;
            uImg.color = Color.white;

            Button uBtn = unequipBtnObj.AddComponent<Button>();
            uBtn.onClick.AddListener(() =>
            {
                sm.UnequipPart(_selectedAircraft, cat);
                RefreshAllVisuals();
            });

            GameObject uTxtObj = new GameObject("Text", typeof(RectTransform));
            uTxtObj.transform.SetParent(unequipBtnObj.transform, false);
            MakeFullScreenRect(GetRect(uTxtObj));
            TextMeshProUGUI uTMP = uTxtObj.AddComponent<TextMeshProUGUI>();
            uTMP.font = MainMenuManager.GetTacticalFont();
            uTMP.text = "ÇIKAR";
            uTMP.fontSize = 20f;
            uTMP.characterSpacing = 1.8f;
            uTMP.fontStyle = FontStyles.Bold;
            uTMP.alignment = TextAlignmentOptions.Center;
            uTMP.color = Color.white;
        }
        else
        {
            tTMP.text = $"<size=85%><color=#64748B>{catIcon} {catName}</color></size>\n<color=#94A3B8><i>[BOŞ YUVA - Henüz bir parça monte edilmedi]</i></color>";
            tTMP.fontSize = 20f;

            // Parça Seç / Mağazaya Git Butonu
            GameObject goBtnObj = new GameObject("GoToStoreBtn", typeof(RectTransform));
            goBtnObj.transform.SetParent(rowObj.transform, false);
            RectTransform gRt = GetRect(goBtnObj);
            gRt.anchorMin = new Vector2(1f, 0.5f);
            gRt.anchorMax = new Vector2(1f, 0.5f);
            gRt.pivot = new Vector2(1f, 0.5f);
            gRt.anchoredPosition = new Vector2(-20f, 0f);
            gRt.sizeDelta = new Vector2(180f, 50f);

            Image gImg = goBtnObj.AddComponent<Image>();
            gImg.sprite = MainMenuManager.GetHudSprite("hud_btn_blue", new Color(0.35f, 0.65f, 0.95f), new Color(0.04f, 0.09f, 0.18f, 0.95f), true);
            gImg.type = Image.Type.Sliced;
            gImg.color = Color.white;

            Button gBtn = goBtnObj.AddComponent<Button>();
            StoreTab targetTab = StoreTab.Engine;
            switch (cat)
            {
                case AircraftPartCategory.Engine: targetTab = StoreTab.Engine; break;
                case AircraftPartCategory.Radar: targetTab = StoreTab.Radar; break;
                case AircraftPartCategory.Airframe: targetTab = StoreTab.Airframe; break;
                case AircraftPartCategory.ElectronicWarfare: targetTab = StoreTab.ElectronicWarfare; break;
            }
            gBtn.onClick.AddListener(() => SwitchTab(targetTab));

            GameObject gTxtObj = new GameObject("Text", typeof(RectTransform));
            gTxtObj.transform.SetParent(goBtnObj.transform, false);
            MakeFullScreenRect(GetRect(gTxtObj));
            TextMeshProUGUI gTMP = gTxtObj.AddComponent<TextMeshProUGUI>();
            gTMP.font = MainMenuManager.GetTacticalFont();
            gTMP.text = "PARÇA SEÇ";
            gTMP.fontSize = 20f;
            gTMP.characterSpacing = 1.8f;
            gTMP.fontStyle = FontStyles.Bold;
            gTMP.alignment = TextAlignmentOptions.Center;
            gTMP.color = Color.white;
        }
    }
}
