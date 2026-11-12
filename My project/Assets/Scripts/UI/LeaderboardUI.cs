using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Küresel As Pilotlar ve Canlı Çevrimiçi Liderlik Tablosu Arayüzü:
/// - 4 Farklı Oyun Modu Sekmesi: Sonsuz İt Dalaşı, Zaman Taarruzu, Mermi Cehennemi, Boss Rush.
/// - Canlı bulut sunucusu (Dreamlo) üzerinden diğer gerçek oyuncuların ve elit asların skorlarını listeler.
/// - Oyuncunun kendi pilot çağrı kodunu (Callsign) düzenleme ve kişisel rekor derecesini gösterme desteği.
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

        Canvas canvas = FindObjectOfType<Canvas>();
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

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _panelRoot = new GameObject("LeaderboardPanelRoot", typeof(RectTransform));
        _panelRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = _panelRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // Arka Plan (Aero-glass yarı saydam kokpit)
        Image bg = _panelRoot.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.04f, 0.08f, 0.88f);

        // Ana Pencere (Modal Box) - 45° Chamfered HUD Glass Frame
        GameObject boxObj = new GameObject("LeaderboardBox", typeof(RectTransform));
        boxObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform boxRt = boxObj.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.sizeDelta = new Vector2(920f, 630f);

        Image boxBg = boxObj.AddComponent<Image>();
        boxBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.85f), new Color(0.03f, 0.06f, 0.12f, 0.95f), false);
        boxBg.type = Image.Type.Sliced;
        boxBg.color = Color.white;

        // 1. ÜST BAŞLIK - HUD Visor Plate
        GameObject visorObj = new GameObject("HeaderVisor", typeof(RectTransform));
        visorObj.transform.SetParent(boxObj.transform, false);
        RectTransform vRt = visorObj.GetComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0f, 1f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.pivot = new Vector2(0.5f, 1f);
        vRt.anchoredPosition = new Vector2(0f, -14f);
        vRt.sizeDelta = new Vector2(880f, 54f);

        Image vImg = visorObj.AddComponent<Image>();
        vImg.sprite = MainMenuManager.GetHudSprite("hud_header_visor", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.04f, 0.09f, 0.16f, 0.95f), true);
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
        titleTMP.text = "TACTICAL // KÜRESEL LİDERLİK & REKOR VERİTABANI\n<size=58%><color=#8AB4F8>GLOBAL DEFCON AS PİLOTLARI SIRALAMASI • GERÇEK ZAMANLI VERİ HATTI</color></size>";
        titleTMP.fontSize = 20f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 2.0f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;
        titleTMP.raycastTarget = false;

        // 2. PİLOT ÇAĞRI KODU (CALLSIGN) & İSİM DEĞİŞTİRME SATIRI
        GameObject csObj = new GameObject("CallsignBar", typeof(RectTransform));
        csObj.transform.SetParent(boxObj.transform, false);
        RectTransform csRt = csObj.GetComponent<RectTransform>();
        csRt.anchorMin = new Vector2(0f, 1f);
        csRt.anchorMax = new Vector2(1f, 1f);
        csRt.pivot = new Vector2(0.5f, 1f);
        csRt.anchoredPosition = new Vector2(0f, -74f);
        csRt.sizeDelta = new Vector2(880f, 38f);

        _callsignTMP = csObj.AddComponent<TextMeshProUGUI>();
        _callsignTMP.font = MainMenuManager.GetTacticalFont();
        _callsignTMP.fontSize = 15f;
        _callsignTMP.fontStyle = FontStyles.Bold;
        _callsignTMP.characterSpacing = 1.4f;
        _callsignTMP.alignment = TextAlignmentOptions.Left;
        _callsignTMP.color = Color.white;
        _callsignTMP.raycastTarget = false;

        // İsim Değiştir Butonu - Tactical HUD Button
        GameObject editBtnObj = new GameObject("EditCallsignBtn", typeof(RectTransform));
        editBtnObj.transform.SetParent(csObj.transform, false);
        RectTransform eRt = editBtnObj.GetComponent<RectTransform>();
        eRt.anchorMin = new Vector2(1f, 0.5f);
        eRt.anchorMax = new Vector2(1f, 0.5f);
        eRt.pivot = new Vector2(1f, 0.5f);
        eRt.anchoredPosition = new Vector2(0f, 0f);
        eRt.sizeDelta = new Vector2(170f, 34f);

        Image eImg = editBtnObj.AddComponent<Image>();
        eImg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f, 1f), new Color(0.04f, 0.12f, 0.20f, 1f), true);
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
        eTmp.fontSize = 12f;
        eTmp.fontStyle = FontStyles.Bold;
        eTmp.characterSpacing = 1.4f;
        eTmp.alignment = TextAlignmentOptions.Center;
        eTmp.color = Color.white;
        eTmp.raycastTarget = false;

        // 3. OYUN MODU SEKME BUTONLARI (5 Adet)
        GameObject tabsObj = new GameObject("TabsContainer", typeof(RectTransform));
        tabsObj.transform.SetParent(boxObj.transform, false);
        RectTransform tabRt = tabsObj.GetComponent<RectTransform>();
        tabRt.anchorMin = new Vector2(0f, 1f);
        tabRt.anchorMax = new Vector2(1f, 1f);
        tabRt.pivot = new Vector2(0.5f, 1f);
        tabRt.anchoredPosition = new Vector2(0f, -116f);
        tabRt.sizeDelta = new Vector2(880f, 40f);

        float tabW = 170f;
        float startX = -350f;

        _tabEndlessBtn = CreateTabBtn(tabsObj.transform, "TabEndless", "SONSUZ", startX + (tabW + 6f) * 0, () => SetTab(LeaderboardManager.BoardMode.Endless), out _tabEndlessTMP);
        _tabHardcoreBtn = CreateTabBtn(tabsObj.transform, "TabHardcore", "[!] HARDCORE", startX + (tabW + 6f) * 1, () => SetTab(LeaderboardManager.BoardMode.Hardcore), out _tabHardcoreTMP);
        _tabBossRushBtn = CreateTabBtn(tabsObj.transform, "TabBossRush", "BOSS RUSH", startX + (tabW + 6f) * 2, () => SetTab(LeaderboardManager.BoardMode.BossRush), out _tabBossRushTMP);
        _tabTimeAttackBtn = CreateTabBtn(tabsObj.transform, "TabTimeAttack", "ZAMAN TAARRUZU", startX + (tabW + 6f) * 3, () => SetTab(LeaderboardManager.BoardMode.TimeAttack), out _tabTimeAttackTMP);
        _tabBulletHellBtn = CreateTabBtn(tabsObj.transform, "TabBulletHell", "MERMİ CEHENNEMİ", startX + (tabW + 6f) * 4, () => SetTab(LeaderboardManager.BoardMode.BulletHell), out _tabBulletHellTMP);

        // 4. SENİN DERECEN / KİŞİSEL EN İYİ REKOR KARTI
        GameObject myCard = new GameObject("PlayerBestCard", typeof(RectTransform));
        myCard.transform.SetParent(boxObj.transform, false);
        RectTransform mcRt = myCard.GetComponent<RectTransform>();
        mcRt.anchorMin = new Vector2(0f, 1f);
        mcRt.anchorMax = new Vector2(1f, 1f);
        mcRt.pivot = new Vector2(0.5f, 1f);
        mcRt.anchoredPosition = new Vector2(0f, -162f);
        mcRt.sizeDelta = new Vector2(880f, 42f);

        Image mcBg = myCard.AddComponent<Image>();
        mcBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0.2f, 0.95f, 0.4f, 0.85f), new Color(0.04f, 0.14f, 0.08f, 0.92f), false);
        mcBg.type = Image.Type.Sliced;
        mcBg.color = Color.white;

        GameObject mcTxtObj = new GameObject("Text", typeof(RectTransform));
        mcTxtObj.transform.SetParent(myCard.transform, false);
        RectTransform mcTxtRt = mcTxtObj.GetComponent<RectTransform>();
        mcTxtRt.anchorMin = Vector2.zero;
        mcTxtRt.anchorMax = Vector2.one;
        mcTxtRt.offsetMin = new Vector2(10f, 0f);
        mcTxtRt.offsetMax = new Vector2(-10f, 0f);

        _playerBestRankTMP = mcTxtObj.AddComponent<TextMeshProUGUI>();
        _playerBestRankTMP.font = MainMenuManager.GetTacticalFont();
        _playerBestRankTMP.fontSize = 15f;
        _playerBestRankTMP.fontStyle = FontStyles.Bold;
        _playerBestRankTMP.characterSpacing = 1.4f;
        _playerBestRankTMP.alignment = TextAlignmentOptions.Center;
        _playerBestRankTMP.color = Color.white;

        // 5. TABLO BAŞLIK SATIRI (Sıra, Pilot Çağrı Kodu, Skor, Rütbe)
        GameObject headerObj = new GameObject("TableHeader", typeof(RectTransform));
        headerObj.transform.SetParent(boxObj.transform, false);
        RectTransform hRt = headerObj.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -210f);
        hRt.sizeDelta = new Vector2(880f, 30f);

        Image hBg = headerObj.AddComponent<Image>();
        hBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.5f), new Color(0.04f, 0.09f, 0.16f, 0.92f), false);
        hBg.type = Image.Type.Sliced;
        hBg.color = Color.white;

        GameObject hTxtObj = new GameObject("Text", typeof(RectTransform));
        hTxtObj.transform.SetParent(headerObj.transform, false);
        RectTransform hTxtRt = hTxtObj.GetComponent<RectTransform>();
        hTxtRt.anchorMin = Vector2.zero;
        hTxtRt.anchorMax = Vector2.one;
        hTxtRt.offsetMin = new Vector2(10f, 0f);
        hTxtRt.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI hTmp = hTxtObj.AddComponent<TextMeshProUGUI>();
        hTmp.font = MainMenuManager.GetTacticalFont();
        hTmp.text = "   SIRA         PİLOT ÇAĞRI KODU                                     SKOR / DERECE                    ASKERİ UNVAN";
        hTmp.fontSize = 13f;
        hTmp.fontStyle = FontStyles.Bold;
        hTmp.characterSpacing = 1.3f;
        hTmp.alignment = TextAlignmentOptions.Left;
        hTmp.color = new Color(0.6f, 0.85f, 1f);

        // 6. SKOR SATIRLARI KONTEYNERİ (Scroll Area)
        GameObject listObj = new GameObject("RowsContainer", typeof(RectTransform));
        listObj.transform.SetParent(boxObj.transform, false);
        RectTransform lRt = listObj.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 1f);
        lRt.offsetMin = new Vector2(20f, 75f);
        lRt.offsetMax = new Vector2(-20f, -244f);

        _rowsContainer = listObj.transform;

        // 7. ALT BİLGİ VE BUTONLAR
        // Bağlantı durumu
        GameObject statusObj = new GameObject("ConnectionStatus", typeof(RectTransform));
        statusObj.transform.SetParent(boxObj.transform, false);
        RectTransform stRt = statusObj.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0f, 0f);
        stRt.anchorMax = new Vector2(0.55f, 0f);
        stRt.pivot = new Vector2(0f, 0f);
        stRt.anchoredPosition = new Vector2(24f, 22f);
        stRt.sizeDelta = new Vector2(480f, 36f);

        _connectionStatusTMP = statusObj.AddComponent<TextMeshProUGUI>();
        _connectionStatusTMP.font = MainMenuManager.GetTacticalFont();
        _connectionStatusTMP.fontSize = 13f;
        _connectionStatusTMP.fontStyle = FontStyles.Bold;
        _connectionStatusTMP.characterSpacing = 1.3f;
        _connectionStatusTMP.alignment = TextAlignmentOptions.Left;
        _connectionStatusTMP.color = new Color(0.2f, 0.9f, 0.5f);

        // Yenile Butonu
        CreateActionButton(boxObj.transform, "RefreshBtn", "YENİLE", new Vector2(590f, 20f), new Vector2(140f, 44f), "hud_btn_blue", new Color(0f, 0.75f, 1f, 1f), new Color(0.06f, 0.16f, 0.28f, 1f), () => RefreshScores());

        // Kapat Butonu
        CreateActionButton(boxObj.transform, "CloseBtn", "« GERİ DÖN", new Vector2(745f, 20f), new Vector2(150f, 44f), "hud_btn_red", new Color(1f, 0.35f, 0.35f, 1f), new Color(0.26f, 0.05f, 0.05f, 1f), CloseLeaderboard);

        // İsim Değiştirme Modalını Hazırla
        SetupNameChangeModal();
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
        rt.sizeDelta = new Vector2(168f, 38f);

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
        labelTmp.fontSize = 13f;
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
        tmp.fontSize = 14f;
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
        float rowH = 28f;
        int maxRows = Mathf.Min(10, entries.Count);

        for (int i = 0; i < maxRows; i++)
        {
            var e = entries[i];
            GameObject rowObj = CreateRowElement(i, e, rowH);
            _spawnedRows.Add(rowObj);
        }
    }

    private GameObject CreateRowElement(int index, LeaderboardManager.LeaderboardEntry entry, float rowHeight)
    {
        GameObject row = new GameObject($"Row_{entry.rank}", typeof(RectTransform));
        row.transform.SetParent(_rowsContainer, false);

        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -index * (rowHeight + 4f));
        rt.sizeDelta = new Vector2(880f, rowHeight);

        Image bg = row.AddComponent<Image>();

        if (entry.isPlayer)
        {
            // Oyuncunun kendi satırı: Neon yeşil/cyan HUD çerçevesi
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
            1 => "<color=#FFD700>[#1]</color>",
            2 => "<color=#C0C0C0>[#2]</color>",
            3 => "<color=#CD7F32>[#3]</color>",
            _ => $"    #{entry.rank}"
        };

        string playerTag = entry.isPlayer ? " <color=#00E5FF><b>[SEN]</b></color>" : "";
        string displayName = $"{entry.playerName}{playerTag}";

        GameObject rowTxtObj = new GameObject("Text", typeof(RectTransform));
        rowTxtObj.transform.SetParent(row.transform, false);
        RectTransform rowTxtRt = rowTxtObj.GetComponent<RectTransform>();
        rowTxtRt.anchorMin = Vector2.zero;
        rowTxtRt.anchorMax = Vector2.one;
        rowTxtRt.offsetMin = new Vector2(10f, 0f);
        rowTxtRt.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI tmp = rowTxtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = $" {rankIcon,-8}   {displayName,-38}   <color=#FFD54F><b>{entry.formattedScore,-22}</b></color>   <color=#8AB4F8>{entry.rankTitle}</color>";
        tmp.fontSize = 13f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.3f;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return row;
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
        bRt.sizeDelta = new Vector2(520f, 260f);

        Image bBg = boxObj.AddComponent<Image>();
        bBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.04f, 0.09f, 0.16f, 0.98f), false);
        bBg.type = Image.Type.Sliced;
        bBg.color = Color.white;

        // Başlık
        GameObject tObj = new GameObject("Title", typeof(RectTransform));
        tObj.transform.SetParent(boxObj.transform, false);
        RectTransform tRt = tObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -20f);
        tRt.sizeDelta = new Vector2(480f, 38f);

        TextMeshProUGUI titleTMP = tObj.AddComponent<TextMeshProUGUI>();
        titleTMP.font = MainMenuManager.GetTacticalFont();
        titleTMP.text = "DEFCON // PİLOT ÇAĞRI KODU BELİRLE";
        titleTMP.fontSize = 17f;
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
        sRt.anchoredPosition = new Vector2(0f, -58f);
        sRt.sizeDelta = new Vector2(480f, 26f);

        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.font = MainMenuManager.GetTacticalFont();
        subTMP.text = "Liderlik tablosunda diğer pilotların göreceği çağrı kodu:";
        subTMP.fontSize = 12f;
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
        inRt.sizeDelta = new Vector2(380f, 44f);

        Image inBg = inpObj.AddComponent<Image>();
        inBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.7f), new Color(0.02f, 0.05f, 0.09f, 1f), false);
        inBg.type = Image.Type.Sliced;
        inBg.color = Color.white;

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(inpObj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 2f);
        textRt.offsetMax = new Vector2(-10f, -2f);

        TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
        inputText.font = MainMenuManager.GetTacticalFont();
        inputText.fontSize = 16f;
        inputText.fontStyle = FontStyles.Bold;
        inputText.characterSpacing = 1.6f;
        inputText.alignment = TextAlignmentOptions.Center;
        inputText.color = Color.white;

        _nameInputField = inpObj.AddComponent<TMP_InputField>();
        _nameInputField.textComponent = inputText;
        _nameInputField.characterLimit = 14;
        _nameInputField.text = LeaderboardManager.PlayerCallsign;

        // Kaydet Butonu
        CreateActionButton(boxObj.transform, "SaveNameBtn", "KAYDET", new Vector2(90f, 22f), new Vector2(150f, 42f), "hud_btn_emerald", new Color(0.2f, 0.95f, 0.4f, 1f), new Color(0.04f, 0.16f, 0.08f, 1f), SaveCallsignAndClose);

        // İptal Butonu
        CreateActionButton(boxObj.transform, "CancelNameBtn", "İPTAL", new Vector2(280f, 22f), new Vector2(150f, 42f), "hud_btn_slate", new Color(0.5f, 0.6f, 0.7f, 0.9f), new Color(0.08f, 0.12f, 0.18f, 1f), () => _nameChangeModal.SetActive(false));

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

