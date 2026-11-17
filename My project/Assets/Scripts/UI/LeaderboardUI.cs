using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Küresel As Pilotlar ve Canlı Çevrimiçi Liderlik Tablosu Arayüzü:
/// - 4 Farklı Oyun Modu Sekmesi: Sonsuz İt Dalaşı, Zaman Taarruzu, Mermi Cehennemi, Boss Rush, Hardcore.
/// - Canlı bulut sunucusu (Dreamlo) üzerinden diğer gerçek oyuncuların ve elit asların skorlarını listeler.
/// - 1920x1080 çözünürlüğe tam oturan, 1280x820 boyutunda ferah ve jilet gibi hizalanmış HUD kutusu.
/// - Ana başlık, pilot çağrı kodu, kişisel rekor kartı ve tüm satırlar kutu sınırlarına milimetrik oturur, asla taşmaz.
/// - Büyük, parlak ve ultra okunaklı taktik font boyutları.
/// - 🥇 Altın, 🥈 Gümüş, 🥉 Bronz kupa madalyaları ve parlayan neon askeri kokpit arayüzü.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    public static LeaderboardUI Instance { get; private set; }

    private GameObject _panelRoot;
    private LeaderboardManager.BoardMode _currentMode = LeaderboardManager.BoardMode.Endless;
    private Action _onCloseCallback;

    // Sekme Butonları
    private Button _tabEndlessBtn, _tabTimeAttackBtn, _tabBulletHellBtn, _tabBossRushBtn, _tabHardcoreBtn;
    private TextMeshProUGUI _tabEndlessTMP, _tabTimeAttackTMP, _tabBulletHellTMP, _tabBossRushTMP, _tabHardcoreTMP;

    // Başlık ve Bilgilendirme
    private TextMeshProUGUI _callsignTMP;
    private TextMeshProUGUI _playerBestRankTMP;
    private TextMeshProUGUI _connectionStatusTMP;

    // Tablo Satırları Konteyneri
    private Transform _rowsContainer;
    private List<GameObject> _spawnedRows = new List<GameObject>();

    // İsim Değiştirme Modal'ı
    private GameObject _nameChangeModal;
    private TMP_InputField _nameInputField;

    private const float BOX_WIDTH = 1280f;
    private const float BOX_HEIGHT = 820f;
    private const float CONTENT_WIDTH = 1220f; // 1280 - 30 sol - 30 sağ = 1220px tam ortalanmış içerik genişliği

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static LeaderboardUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("LeaderboardUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("LeaderboardUI", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        Instance = go.GetComponent<LeaderboardUI>();
        if (Instance == null) Instance = go.AddComponent<LeaderboardUI>();
        return Instance;
    }

    /// <summary>
    /// Liderlik tablosunu istenen mod ile açar.
    /// </summary>
    public void OpenLeaderboard(LeaderboardManager.BoardMode mode = LeaderboardManager.BoardMode.Endless, Action onClose = null)
    {
        _currentMode = mode;
        _onCloseCallback = onClose;

        EnsurePanelStructure();

        if (_panelRoot != null)
        {
            _panelRoot.SetActive(true);
            _panelRoot.transform.SetAsLastSibling();
            PlayerProfileUI.EnsureUI()?.ShowMenuStatusBar(showGoldBadge: false);
        }

        UpdateTabVisuals();
        RefreshScores();
    }

    public void CloseLeaderboard()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        if (_nameChangeModal != null)
            _nameChangeModal.SetActive(false);

        var cb = _onCloseCallback;
        _onCloseCallback = null;
        cb?.Invoke();
    }

    public void CloseDirectly()
    {
        if (this == null) return;
        _onCloseCallback = null;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        if (_nameChangeModal != null)
            _nameChangeModal.SetActive(false);
    }

    private void EnsurePanelStructure()
    {
        if (_panelRoot != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        _panelRoot = new GameObject("LeaderboardPanelRoot", typeof(RectTransform));
        _panelRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = _panelRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // Arka Plan (Aero-glass yarı saydam kokpit karartması)
        Image bg = _panelRoot.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.04f, 0.08f, 0.90f);

        // Ana Pencere (Modal Box) - 1280 x 820 Dikdörtgen Askeri HUD Çerçevesi
        GameObject boxObj = new GameObject("LeaderboardBox", typeof(RectTransform));
        boxObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform boxRt = boxObj.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.sizeDelta = new Vector2(BOX_WIDTH, BOX_HEIGHT);

        Image boxBg = boxObj.AddComponent<Image>();
        boxBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.92f), new Color(0.03f, 0.06f, 0.12f, 0.97f), false);
        boxBg.type = Image.Type.Sliced;
        boxBg.color = Color.white;

        // ==========================================
        // 1. ÜST BAŞLIK - HUD Visor Plate (Tam Hizalı & Büyük Yazı)
        // ==========================================
        GameObject visorObj = new GameObject("HeaderVisor", typeof(RectTransform));
        visorObj.transform.SetParent(boxObj.transform, false);
        RectTransform vRt = visorObj.GetComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0.5f, 1f);
        vRt.anchorMax = new Vector2(0.5f, 1f);
        vRt.pivot = new Vector2(0.5f, 1f);
        vRt.anchoredPosition = new Vector2(0f, -14f);
        vRt.sizeDelta = new Vector2(CONTENT_WIDTH, 72f);

        Image vImg = visorObj.AddComponent<Image>();
        vImg.sprite = MainMenuManager.GetHudSprite("hud_header_visor", new Color(0f, 0.85f, 1f, 0.95f), new Color(0.04f, 0.09f, 0.16f, 0.95f), true);
        vImg.type = Image.Type.Sliced;
        vImg.color = Color.white;

        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(visorObj.transform, false);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.font = MainMenuManager.GetTacticalFont();
        titleTMP.text = "TACTICAL // KÜRESEL LİDERLİK & REKOR VERİTABANI\n<size=65%><color=#8AB4F8>GLOBAL DEFCON AS PİLOTLARI SIRALAMASI • GERÇEK ZAMANLI VERİ HATTI</color></size>";
        titleTMP.fontSize = 26f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 2.0f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;
        titleTMP.raycastTarget = false;

        // ==========================================
        // 2. PİLOT ÇAĞRI KODU (CALLSIGN) & İSİM DEĞİŞTİRME SATIRI (Büyük & Şık)
        // ==========================================
        GameObject csObj = new GameObject("CallsignBar", typeof(RectTransform));
        csObj.transform.SetParent(boxObj.transform, false);
        RectTransform csRt = csObj.GetComponent<RectTransform>();
        csRt.anchorMin = new Vector2(0.5f, 1f);
        csRt.anchorMax = new Vector2(0.5f, 1f);
        csRt.pivot = new Vector2(0.5f, 1f);
        csRt.anchoredPosition = new Vector2(0f, -94f);
        csRt.sizeDelta = new Vector2(CONTENT_WIDTH, 50f);

        Image csBg = csObj.AddComponent<Image>();
        csBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.45f), new Color(0.03f, 0.08f, 0.15f, 0.90f), false);
        csBg.type = Image.Type.Sliced;
        csBg.color = Color.white;

        GameObject csTextObj = new GameObject("Text", typeof(RectTransform));
        csTextObj.transform.SetParent(csObj.transform, false);
        RectTransform csTextRt = csTextObj.GetComponent<RectTransform>();
        csTextRt.anchorMin = Vector2.zero;
        csTextRt.anchorMax = Vector2.one;
        csTextRt.offsetMin = new Vector2(20f, 0f);
        csTextRt.offsetMax = new Vector2(-260f, 0f);

        _callsignTMP = csTextObj.AddComponent<TextMeshProUGUI>();
        _callsignTMP.font = MainMenuManager.GetTacticalFont();
        _callsignTMP.fontSize = 22f;
        _callsignTMP.fontStyle = FontStyles.Bold;
        _callsignTMP.characterSpacing = 1.4f;
        _callsignTMP.alignment = TextAlignmentOptions.Left;
        _callsignTMP.color = Color.white;
        _callsignTMP.raycastTarget = false;

        // İsim Değiştir Butonu
        GameObject editBtnObj = new GameObject("EditCallsignBtn", typeof(RectTransform));
        editBtnObj.transform.SetParent(csObj.transform, false);
        RectTransform eRt = editBtnObj.GetComponent<RectTransform>();
        eRt.anchorMin = new Vector2(1f, 0.5f);
        eRt.anchorMax = new Vector2(1f, 0.5f);
        eRt.pivot = new Vector2(1f, 0.5f);
        eRt.anchoredPosition = new Vector2(-10f, 0f);
        eRt.sizeDelta = new Vector2(240f, 40f);

        Image eImg = editBtnObj.AddComponent<Image>();
        eImg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f, 1f), new Color(0.04f, 0.14f, 0.22f, 1f), true);
        eImg.type = Image.Type.Sliced;
        eImg.color = Color.white;

        Button eBtn = editBtnObj.AddComponent<Button>();
        eBtn.targetGraphic = eImg;
        eBtn.onClick.AddListener(OpenCallsignEditorModal);

        GameObject eTxt = new GameObject("Text", typeof(RectTransform));
        eTxt.transform.SetParent(editBtnObj.transform, false);
        RectTransform etRt = eTxt.GetComponent<RectTransform>();
        etRt.anchorMin = Vector2.zero;
        etRt.anchorMax = Vector2.one;
        etRt.offsetMin = Vector2.zero;
        etRt.offsetMax = Vector2.zero;

        TextMeshProUGUI eTmp = eTxt.AddComponent<TextMeshProUGUI>();
        eTmp.font = MainMenuManager.GetTacticalFont();
        eTmp.text = "ÇAĞRI KODU DEĞİŞ";
        eTmp.fontSize = 16f;
        eTmp.fontStyle = FontStyles.Bold;
        eTmp.characterSpacing = 1.4f;
        eTmp.alignment = TextAlignmentOptions.Center;
        eTmp.color = Color.white;
        eTmp.raycastTarget = false;

        // ==========================================
        // 3. OYUN MODU SEKME BUTONLARI (5 Adet - 1220px İçinde Eşit Dağılım)
        // ==========================================
        GameObject tabsObj = new GameObject("TabsContainer", typeof(RectTransform));
        tabsObj.transform.SetParent(boxObj.transform, false);
        RectTransform tabRt = tabsObj.GetComponent<RectTransform>();
        tabRt.anchorMin = new Vector2(0.5f, 1f);
        tabRt.anchorMax = new Vector2(0.5f, 1f);
        tabRt.pivot = new Vector2(0.5f, 1f);
        tabRt.anchoredPosition = new Vector2(0f, -152f);
        tabRt.sizeDelta = new Vector2(CONTENT_WIDTH, 48f);

        float tabW = 236f;
        float startX = -488f;

        _tabEndlessBtn = CreateTabBtn(tabsObj.transform, "TabEndless", "SONSUZ", startX + (tabW + 8f) * 0, () => SetTab(LeaderboardManager.BoardMode.Endless), out _tabEndlessTMP);
        _tabHardcoreBtn = CreateTabBtn(tabsObj.transform, "TabHardcore", "[!] HARDCORE", startX + (tabW + 8f) * 1, () => SetTab(LeaderboardManager.BoardMode.Hardcore), out _tabHardcoreTMP);
        _tabBossRushBtn = CreateTabBtn(tabsObj.transform, "TabBossRush", "BOSS RUSH", startX + (tabW + 8f) * 2, () => SetTab(LeaderboardManager.BoardMode.BossRush), out _tabBossRushTMP);
        _tabTimeAttackBtn = CreateTabBtn(tabsObj.transform, "TabTimeAttack", "ZAMAN TAARRUZU", startX + (tabW + 8f) * 3, () => SetTab(LeaderboardManager.BoardMode.TimeAttack), out _tabTimeAttackTMP);
        _tabBulletHellBtn = CreateTabBtn(tabsObj.transform, "TabBulletHell", "MERMİ CEHENNEMİ", startX + (tabW + 8f) * 4, () => SetTab(LeaderboardManager.BoardMode.BulletHell), out _tabBulletHellTMP);

        // ==========================================
        // 4. SENİN DERECEN / KİŞİSEL REKOR KARTI (Büyük & Parlak)
        // ==========================================
        GameObject myCard = new GameObject("PlayerBestCard", typeof(RectTransform));
        myCard.transform.SetParent(boxObj.transform, false);
        RectTransform mcRt = myCard.GetComponent<RectTransform>();
        mcRt.anchorMin = new Vector2(0.5f, 1f);
        mcRt.anchorMax = new Vector2(0.5f, 1f);
        mcRt.pivot = new Vector2(0.5f, 1f);
        mcRt.anchoredPosition = new Vector2(0f, -208f);
        mcRt.sizeDelta = new Vector2(CONTENT_WIDTH, 52f);

        Image mcBg = myCard.AddComponent<Image>();
        mcBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0.2f, 0.95f, 0.45f, 0.90f), new Color(0.03f, 0.15f, 0.08f, 0.95f), false);
        mcBg.type = Image.Type.Sliced;
        mcBg.color = Color.white;

        GameObject mcTxtObj = new GameObject("Text", typeof(RectTransform));
        mcTxtObj.transform.SetParent(myCard.transform, false);
        RectTransform mcTxtRt = mcTxtObj.GetComponent<RectTransform>();
        mcTxtRt.anchorMin = Vector2.zero;
        mcTxtRt.anchorMax = Vector2.one;
        mcTxtRt.offsetMin = new Vector2(20f, 0f);
        mcTxtRt.offsetMax = new Vector2(-20f, 0f);

        _playerBestRankTMP = mcTxtObj.AddComponent<TextMeshProUGUI>();
        _playerBestRankTMP.font = MainMenuManager.GetTacticalFont();
        _playerBestRankTMP.fontSize = 22f;
        _playerBestRankTMP.fontStyle = FontStyles.Bold;
        _playerBestRankTMP.characterSpacing = 1.4f;
        _playerBestRankTMP.alignment = TextAlignmentOptions.Center;
        _playerBestRankTMP.color = Color.white;

        // ==========================================
        // 5. TABLO SÜTUN BAŞLIKLARI SATIRI (SKOR, DERECE, ÇAĞRI KODU - Büyük & Net)
        // ==========================================
        GameObject headerObj = new GameObject("TableHeader", typeof(RectTransform));
        headerObj.transform.SetParent(boxObj.transform, false);
        RectTransform hRt = headerObj.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.5f, 1f);
        hRt.anchorMax = new Vector2(0.5f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -268f);
        hRt.sizeDelta = new Vector2(CONTENT_WIDTH, 42f);

        Image hBg = headerObj.AddComponent<Image>();
        hBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.65f), new Color(0.04f, 0.10f, 0.18f, 0.96f), false);
        hBg.type = Image.Type.Sliced;
        hBg.color = Color.white;

        // 4 Sütun Başlığı
        CreateHeaderCell(headerObj.transform, "SIRA", 0f, 0.12f, TextAlignmentOptions.Center);
        CreateHeaderCell(headerObj.transform, "PİLOT ÇAĞRI KODU", 0.12f, 0.50f, TextAlignmentOptions.Left, leftPadding: 25f);
        CreateHeaderCell(headerObj.transform, "SKOR / DERECE", 0.50f, 0.75f, TextAlignmentOptions.Center);
        CreateHeaderCell(headerObj.transform, "ASKERİ UNVAN", 0.75f, 1f, TextAlignmentOptions.Center);

        // ==========================================
        // 6. SKOR SATIRLARI KONTEYNERİ (Tam Dikdörtgen İçinde)
        // ==========================================
        GameObject listObj = new GameObject("RowsContainer", typeof(RectTransform));
        listObj.transform.SetParent(boxObj.transform, false);
        RectTransform lRt = listObj.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0.5f, 1f);
        lRt.anchorMax = new Vector2(0.5f, 1f);
        lRt.pivot = new Vector2(0.5f, 1f);
        lRt.anchoredPosition = new Vector2(0f, -318f);
        lRt.sizeDelta = new Vector2(CONTENT_WIDTH, 420f);

        _rowsContainer = listObj.transform;

        // ==========================================
        // 7. ALT BİLGİ VE BUTONLAR
        // ==========================================
        // Bağlantı durumu
        GameObject statusObj = new GameObject("ConnectionStatus", typeof(RectTransform));
        statusObj.transform.SetParent(boxObj.transform, false);
        RectTransform stRt = statusObj.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0f, 0f);
        stRt.anchorMax = new Vector2(0.55f, 0f);
        stRt.pivot = new Vector2(0f, 0f);
        stRt.anchoredPosition = new Vector2(30f, 22f);
        stRt.sizeDelta = new Vector2(620f, 48f);

        _connectionStatusTMP = statusObj.AddComponent<TextMeshProUGUI>();
        _connectionStatusTMP.font = MainMenuManager.GetTacticalFont();
        _connectionStatusTMP.fontSize = 17f;
        _connectionStatusTMP.fontStyle = FontStyles.Bold;
        _connectionStatusTMP.characterSpacing = 1.3f;
        _connectionStatusTMP.alignment = TextAlignmentOptions.Left;
        _connectionStatusTMP.color = new Color(0.2f, 0.95f, 0.5f);

        // Yenile Butonu
        CreateActionButton(boxObj.transform, "RefreshBtn", "YENİLE", new Vector2(870f, 22f), new Vector2(175f, 48f), "hud_btn_blue", new Color(0f, 0.75f, 1f, 1f), new Color(0.06f, 0.16f, 0.28f, 1f), () => RefreshScores());

        // Kapat Butonu
        CreateActionButton(boxObj.transform, "CloseBtn", "« GERİ DÖN", new Vector2(1065f, 22f), new Vector2(185f, 48f), "hud_btn_red", new Color(1f, 0.35f, 0.35f, 1f), new Color(0.26f, 0.05f, 0.05f, 1f), CloseLeaderboard);

        // İsim Değiştirme Modalını Hazırla
        SetupNameChangeModal();
    }

    private void CreateHeaderCell(Transform parent, string title, float minX, float maxX, TextAlignmentOptions align, float leftPadding = 0f)
    {
        GameObject cell = new GameObject($"Header_{title}", typeof(RectTransform));
        cell.transform.SetParent(parent, false);

        RectTransform rt = cell.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(minX, 0f);
        rt.anchorMax = new Vector2(maxX, 1f);
        rt.offsetMin = new Vector2(leftPadding, 0f);
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = cell.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = title;
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.4f;
        tmp.alignment = align;
        tmp.color = new Color(0.35f, 0.90f, 1f);
        tmp.raycastTarget = false;
    }

    private Button CreateTabBtn(Transform parent, string name, string text, float posX, UnityEngine.Events.UnityAction action, out TextMeshProUGUI labelTmp)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(posX, 0f);
        rt.sizeDelta = new Vector2(236f, 46f);

        Image img = btnObj.AddComponent<Image>();
        img.sprite = MainMenuManager.GetHudSprite("hud_btn_slate", new Color(0.35f, 0.45f, 0.55f, 0.8f), new Color(0.05f, 0.08f, 0.12f, 0.85f), true);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        GameObject tObj = new GameObject("Text", typeof(RectTransform));
        tObj.transform.SetParent(btnObj.transform, false);
        RectTransform trt = tObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        labelTmp = tObj.AddComponent<TextMeshProUGUI>();
        labelTmp.font = MainMenuManager.GetTacticalFont();
        labelTmp.text = text;
        labelTmp.fontSize = 17f;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.characterSpacing = 1.4f;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = Color.white;
        labelTmp.raycastTarget = false;

        return btn;
    }

    private void CreateActionButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, string spriteKey, Color borderColor, Color fillColor, UnityEngine.Events.UnityAction action)
    {
        GameObject bObj = new GameObject(name, typeof(RectTransform));
        bObj.transform.SetParent(parent, false);
        RectTransform rt = bObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = bObj.AddComponent<Image>();
        img.sprite = MainMenuManager.GetHudSprite(spriteKey, borderColor, fillColor, true);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        Button btn = bObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        GameObject tObj = new GameObject("Text", typeof(RectTransform));
        tObj.transform.SetParent(bObj.transform, false);
        RectTransform trt = tObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = 17f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    private void SetTab(LeaderboardManager.BoardMode mode)
    {
        _currentMode = mode;
        UpdateTabVisuals();
        RefreshScores();
    }

    private void UpdateTabVisuals()
    {
        SetTabStyle(_tabEndlessBtn, _tabEndlessTMP, _currentMode == LeaderboardManager.BoardMode.Endless);
        SetTabStyle(_tabHardcoreBtn, _tabHardcoreTMP, _currentMode == LeaderboardManager.BoardMode.Hardcore);
        SetTabStyle(_tabBossRushBtn, _tabBossRushTMP, _currentMode == LeaderboardManager.BoardMode.BossRush);
        SetTabStyle(_tabTimeAttackBtn, _tabTimeAttackTMP, _currentMode == LeaderboardManager.BoardMode.TimeAttack);
        SetTabStyle(_tabBulletHellBtn, _tabBulletHellTMP, _currentMode == LeaderboardManager.BoardMode.BulletHell);
    }

    private void SetTabStyle(Button btn, TextMeshProUGUI tmp, bool isSelected)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();

        if (isSelected)
        {
            if (img != null)
            {
                img.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f, 1f), new Color(0.06f, 0.18f, 0.28f, 1f), true);
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            if (tmp != null)
            {
                tmp.color = Color.white;
            }
        }
        else
        {
            if (img != null)
            {
                img.sprite = MainMenuManager.GetHudSprite("hud_btn_slate", new Color(0.35f, 0.45f, 0.55f, 0.8f), new Color(0.05f, 0.08f, 0.12f, 0.85f), true);
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            if (tmp != null)
            {
                tmp.color = new Color(0.7f, 0.75f, 0.82f);
            }
        }
    }

    public void RefreshScores()
    {
        string callsign = LeaderboardManager.PlayerCallsign;
        if (_callsignTMP != null)
        {
            _callsignTMP.text = $"PİLOT ÇAĞRI KODUNUZ: <color=#00E5FF><b>{callsign}</b></color>";
        }

        if (_connectionStatusTMP != null)
        {
            _connectionStatusTMP.text = "BULUT SUNUCUSUNA BAĞLANILIYOR...";
            _connectionStatusTMP.color = new Color(0.9f, 0.85f, 0.2f);
        }

        LeaderboardManager.EnsureInstance()?.FetchLeaderboard(_currentMode, (entries, isOnline) =>
        {
            DisplayLeaderboardEntries(entries, isOnline);
        });
    }

    private void DisplayLeaderboardEntries(List<LeaderboardManager.LeaderboardEntry> entries, bool isOnline)
    {
        // Eski satırları temizle
        foreach (var r in _spawnedRows)
        {
            if (r != null) Destroy(r);
        }
        _spawnedRows.Clear();

        if (_connectionStatusTMP != null)
        {
            _connectionStatusTMP.text = isOnline
                ? "[ONLINE] <b>CANLI BULUT BAĞLANTISI AKTİF</b> (Gerçek Oyuncu Veritabanı)"
                : "[ARŞİV] <b>KÜRESEL ASLAR ARŞİVİ</b> (Çevrimdışı / Yerel Yedek)";
            _connectionStatusTMP.color = isOnline ? new Color(0.2f, 0.95f, 0.4f) : new Color(0.4f, 0.85f, 1f);
        }

        if (entries == null || entries.Count == 0) return;

        // Oyuncunun listedeki kendi derecesini tespit et
        LeaderboardManager.LeaderboardEntry playerEntry = null;
        foreach (var e in entries)
        {
            if (e.isPlayer)
            {
                playerEntry = e;
                break;
            }
        }

        if (_playerBestRankTMP != null)
        {
            if (playerEntry != null)
            {
                string rankTag = playerEntry.rank switch
                {
                    1 => "#1. SIRA (ALTIN KUPA)",
                    2 => "#2. SIRA (GÜMÜŞ MADALYA)",
                    3 => "#3. SIRA (BRONZ MADALYA)",
                    _ => $"#{playerEntry.rank}. SIRA"
                };
                _playerBestRankTMP.text = $"SENİN DERECEN: <color=#FFD54F>{rankTag}</color>  •  KİŞİSEL EN İYİ: <color=#00E5FF><b>{playerEntry.formattedScore}</b></color>";
            }
            else
            {
                _playerBestRankTMP.text = $"KİŞİSEL EN İYİ REKOR: <color=#00E5FF><b>{LeaderboardManager.FormatScoreText(_currentMode, LeaderboardManager.EnsureInstance().GetLocalRecordScore(_currentMode), 0f)}</b></color>";
            }
        }

        // Satırları üret (En iyi 10 As)
        float rowH = 42f;
        float rowSpacing = 6f;
        int maxRows = Mathf.Min(10, entries.Count);

        for (int i = 0; i < maxRows; i++)
        {
            var e = entries[i];
            GameObject rowObj = CreateRowElement(i, e, rowH, rowSpacing);
            _spawnedRows.Add(rowObj);
        }
    }

    private GameObject CreateRowElement(int index, LeaderboardManager.LeaderboardEntry entry, float rowHeight, float rowSpacing)
    {
        GameObject row = new GameObject($"Row_{entry.rank}", typeof(RectTransform));
        row.transform.SetParent(_rowsContainer, false);

        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -index * (rowHeight + rowSpacing));
        rt.sizeDelta = new Vector2(CONTENT_WIDTH, rowHeight); // Tam 1220px genişlik! Asla dışarı taşmaz!

        Image bg = row.AddComponent<Image>();

        if (entry.isPlayer)
        {
            // Oyuncunun kendi satırı: Neon yeşil/cyan parlak HUD çerçevesi
            bg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0.2f, 1f, 0.6f, 0.95f), new Color(0.04f, 0.16f, 0.10f, 0.95f), false);
        }
        else
        {
            bg.sprite = MainMenuManager.GetHudSprite(
                "hud_panel_frame",
                (index % 2 == 0) ? new Color(0f, 0.85f, 1f, 0.35f) : new Color(0.2f, 0.4f, 0.6f, 0.25f),
                (index % 2 == 0) ? new Color(0.03f, 0.07f, 0.13f, 0.85f) : new Color(0.02f, 0.05f, 0.09f, 0.85f),
                false
            );
        }
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        // Madalya / Sıra İkonu
        string rankIcon = entry.rank switch
        {
            1 => "<color=#FFD700><b>#1 🥇</b></color>",
            2 => "<color=#E0E0E0><b>#2 🥈</b></color>",
            3 => "<color=#CD7F32><b>#3 🥉</b></color>",
            _ => $"#{entry.rank}"
        };

        string playerTag = entry.isPlayer ? " <color=#00E5FF><b>[SEN]</b></color>" : "";
        string displayName = $"{entry.playerName}{playerTag}";

        // 4 NET HİZALANMIŞ SÜTUN HÜCRESİ (Büyük ve ferah)
        // 1. Sütun: SIRA / MADALYA (%12 genişlik)
        CreateRowCell(row.transform, "RankCell", rankIcon, 0f, 0.12f, TextAlignmentOptions.Center, 19f, Color.white);

        // 2. Sütun: PİLOT ÇAĞRI KODU (%38 genişlik, sola hizalı + 25px padding)
        CreateRowCell(row.transform, "NameCell", displayName, 0.12f, 0.50f, TextAlignmentOptions.Left, 19f, entry.isPlayer ? new Color(0.3f, 1f, 0.7f) : Color.white, leftPadding: 25f);

        // 3. Sütun: SKOR / DERECE (%25 genişlik, ortalanmış, parlak altın sarısı)
        CreateRowCell(row.transform, "ScoreCell", $"<color=#FFD54F><b>{entry.formattedScore}</b></color>", 0.50f, 0.75f, TextAlignmentOptions.Center, 20f, Color.white);

        // 4. Sütun: ASKERİ UNVAN (%25 genişlik, ortalanmış, açık mavi)
        CreateRowCell(row.transform, "TitleCell", $"<color=#8AB4F8>{entry.rankTitle}</color>", 0.75f, 1f, TextAlignmentOptions.Center, 17f, Color.white);

        return row;
    }

    private void CreateRowCell(Transform parent, string name, string text, float minX, float maxX, TextAlignmentOptions align, float fontSize, Color col, float leftPadding = 0f)
    {
        GameObject cell = new GameObject(name, typeof(RectTransform));
        cell.transform.SetParent(parent, false);

        RectTransform rt = cell.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(minX, 0f);
        rt.anchorMax = new Vector2(maxX, 1f);
        rt.offsetMin = new Vector2(leftPadding, 0f);
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = cell.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.3f;
        tmp.alignment = align;
        tmp.color = col;
        tmp.raycastTarget = false;
    }

    // ==========================================
    // PİLOT ÇAĞRI KODU (İSİM) DÜZENLEME MODALI
    // ==========================================
    private void SetupNameChangeModal()
    {
        if (_nameChangeModal != null) return;

        _nameChangeModal = new GameObject("NameChangeModal", typeof(RectTransform));
        _nameChangeModal.transform.SetParent(_panelRoot.transform, false);

        RectTransform mRt = _nameChangeModal.GetComponent<RectTransform>();
        mRt.anchorMin = Vector2.zero;
        mRt.anchorMax = Vector2.one;
        mRt.offsetMin = Vector2.zero;
        mRt.offsetMax = Vector2.zero;

        Image mBg = _nameChangeModal.AddComponent<Image>();
        mBg.color = new Color(0f, 0.02f, 0.05f, 0.88f);

        GameObject boxObj = new GameObject("DialogBox", typeof(RectTransform));
        boxObj.transform.SetParent(_nameChangeModal.transform, false);
        RectTransform bRt = boxObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 0.5f);
        bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.sizeDelta = new Vector2(620f, 320f);

        Image bBg = boxObj.AddComponent<Image>();
        bBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.95f), new Color(0.04f, 0.09f, 0.16f, 0.98f), false);
        bBg.type = Image.Type.Sliced;
        bBg.color = Color.white;

        // Başlık
        GameObject tObj = new GameObject("Title", typeof(RectTransform));
        tObj.transform.SetParent(boxObj.transform, false);
        RectTransform tRt = tObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -24f);
        tRt.sizeDelta = new Vector2(560f, 44f);

        TextMeshProUGUI titleTMP = tObj.AddComponent<TextMeshProUGUI>();
        titleTMP.font = MainMenuManager.GetTacticalFont();
        titleTMP.text = "DEFCON // PİLOT ÇAĞRI KODU BELİRLE";
        titleTMP.fontSize = 20f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 1.8f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0f, 0.85f, 1f);

        // Açıklama
        GameObject subObj = new GameObject("Sub", typeof(RectTransform));
        subObj.transform.SetParent(boxObj.transform, false);
        RectTransform sRt = subObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, -72f);
        sRt.sizeDelta = new Vector2(560f, 32f);

        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.font = MainMenuManager.GetTacticalFont();
        subTMP.text = "Liderlik tablosunda diğer pilotların göreceği çağrı kodu:";
        subTMP.fontSize = 15f;
        subTMP.characterSpacing = 1.2f;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = new Color(0.75f, 0.85f, 0.95f);

        // Input Field
        GameObject inpObj = new GameObject("InputField", typeof(RectTransform));
        inpObj.transform.SetParent(boxObj.transform, false);
        RectTransform inRt = inpObj.GetComponent<RectTransform>();
        inRt.anchorMin = new Vector2(0.5f, 0.5f);
        inRt.anchorMax = new Vector2(0.5f, 0.5f);
        inRt.pivot = new Vector2(0.5f, 0.5f);
        inRt.anchoredPosition = new Vector2(0f, -10f);
        inRt.sizeDelta = new Vector2(440f, 50f);

        Image inBg = inpObj.AddComponent<Image>();
        inBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.8f), new Color(0.02f, 0.05f, 0.09f, 1f), false);
        inBg.type = Image.Type.Sliced;
        inBg.color = Color.white;

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(inpObj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(15f, 2f);
        textRt.offsetMax = new Vector2(-15f, -2f);

        TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
        inputText.font = MainMenuManager.GetTacticalFont();
        inputText.fontSize = 19f;
        inputText.fontStyle = FontStyles.Bold;
        inputText.characterSpacing = 1.6f;
        inputText.alignment = TextAlignmentOptions.Center;
        inputText.color = Color.white;

        _nameInputField = inpObj.AddComponent<TMP_InputField>();
        _nameInputField.textComponent = inputText;
        _nameInputField.characterLimit = 14;
        _nameInputField.text = LeaderboardManager.PlayerCallsign;

        // Kaydet Butonu
        CreateActionButton(boxObj.transform, "SaveNameBtn", "KAYDET", new Vector2(110f, 24f), new Vector2(180f, 48f), "hud_btn_emerald", new Color(0.2f, 0.95f, 0.4f, 1f), new Color(0.04f, 0.16f, 0.08f, 1f), SaveCallsignAndClose);

        // İptal Butonu
        CreateActionButton(boxObj.transform, "CancelNameBtn", "İPTAL", new Vector2(330f, 24f), new Vector2(180f, 48f), "hud_btn_slate", new Color(0.5f, 0.6f, 0.7f, 0.9f), new Color(0.08f, 0.12f, 0.18f, 1f), () => _nameChangeModal.SetActive(false));

        _nameChangeModal.SetActive(false);
    }

    private void OpenCallsignEditorModal()
    {
        PilotLoginUI.EnsureUI()?.OpenLoginModal(() => RefreshScores(), isMandatoryFirstTime: false);
    }

    private void SaveCallsignAndClose()
    {
        if (_nameInputField != null && !string.IsNullOrEmpty(_nameInputField.text))
        {
            LeaderboardManager.PlayerCallsign = _nameInputField.text;
        }

        if (_nameChangeModal != null)
            _nameChangeModal.SetActive(false);

        RefreshScores();
    }
}
