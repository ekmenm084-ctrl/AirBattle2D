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
        GameObject go = existing != null ? existing.gameObject : new GameObject("LeaderboardUI");
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

        _onCloseCallback?.Invoke();
        _onCloseCallback = null;
    }

    private void EnsurePanelStructure()
    {
        if (_panelRoot != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _panelRoot = new GameObject("LeaderboardPanelRoot");
        _panelRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = _panelRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // Arka Plan (Koyu Askeri Kokpit)
        Image bg = _panelRoot.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.06f, 0.11f, 0.96f);

        // Ana Pencere (Modal Box)
        GameObject boxObj = new GameObject("LeaderboardBox");
        boxObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform boxRt = boxObj.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.sizeDelta = new Vector2(880f, 620f);

        Image boxBg = boxObj.AddComponent<Image>();
        boxBg.color = new Color(0.05f, 0.10f, 0.16f, 0.98f);

        Outline boxOutline = boxObj.AddComponent<Outline>();
        boxOutline.effectColor = new Color(0f, 0.85f, 1f, 0.85f);
        boxOutline.effectDistance = new Vector2(3f, -3f);

        // 1. ÜST BAŞLIK
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(boxObj.transform, false);
        RectTransform tRt = titleObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -14f);
        tRt.sizeDelta = new Vector2(840f, 55f);

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "🏆 <color=#FFD54F>KÜRESEL LİDERLİK & REKOR TABLOSU</color> 🏆\n<size=55%><color=#8AB4F8>DÜNYA AS PİLOTLARI SIRALAMASI • GERÇEK ZAMANLI BULUT VERİTABANI</color></size>";
        titleTMP.fontSize = 24f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;

        // 2. PİLOT ÇAĞRI KODU (CALLSIGN) & İSİM DEĞİŞTİRME SATIRI
        GameObject csObj = new GameObject("CallsignBar");
        csObj.transform.SetParent(boxObj.transform, false);
        RectTransform csRt = csObj.AddComponent<RectTransform>();
        csRt.anchorMin = new Vector2(0f, 1f);
        csRt.anchorMax = new Vector2(1f, 1f);
        csRt.pivot = new Vector2(0.5f, 1f);
        csRt.anchoredPosition = new Vector2(0f, -72f);
        csRt.sizeDelta = new Vector2(840f, 38f);

        _callsignTMP = csObj.AddComponent<TextMeshProUGUI>();
        _callsignTMP.fontSize = 15f;
        _callsignTMP.alignment = TextAlignmentOptions.Left;
        _callsignTMP.color = Color.white;

        // İsim Değiştir Butonu
        GameObject editBtnObj = new GameObject("EditCallsignBtn");
        editBtnObj.transform.SetParent(csObj.transform, false);
        RectTransform eRt = editBtnObj.AddComponent<RectTransform>();
        eRt.anchorMin = new Vector2(1f, 0.5f);
        eRt.anchorMax = new Vector2(1f, 0.5f);
        eRt.pivot = new Vector2(1f, 0.5f);
        eRt.anchoredPosition = new Vector2(0f, 0f);
        eRt.sizeDelta = new Vector2(160f, 32f);

        Image eImg = editBtnObj.AddComponent<Image>();
        eImg.color = new Color(0.18f, 0.35f, 0.55f, 0.9f);

        Button eBtn = editBtnObj.AddComponent<Button>();
        eBtn.targetGraphic = eImg;
        eBtn.onClick.AddListener(OpenCallsignEditorModal);

        GameObject eTxt = new GameObject("Text");
        eTxt.transform.SetParent(editBtnObj.transform, false);
        RectTransform etRt = eTxt.AddComponent<RectTransform>();
        etRt.anchorMin = Vector2.zero;
        etRt.anchorMax = Vector2.one;
        etRt.offsetMin = Vector2.zero;
        etRt.offsetMax = Vector2.zero;

        TextMeshProUGUI eTmp = eTxt.AddComponent<TextMeshProUGUI>();
        eTmp.text = "✏️ ADI DEĞİŞTİR";
        eTmp.fontSize = 13f;
        eTmp.fontStyle = FontStyles.Bold;
        eTmp.alignment = TextAlignmentOptions.Center;
        eTmp.color = Color.white;

        // 3. OYUN MODU SEKME BUTONLARI (4 Adet)
        GameObject tabsObj = new GameObject("TabsContainer");
        tabsObj.transform.SetParent(boxObj.transform, false);
        RectTransform tabRt = tabsObj.AddComponent<RectTransform>();
        tabRt.anchorMin = new Vector2(0f, 1f);
        tabRt.anchorMax = new Vector2(1f, 1f);
        tabRt.pivot = new Vector2(0.5f, 1f);
        tabRt.anchoredPosition = new Vector2(0f, -114f);
        tabRt.sizeDelta = new Vector2(840f, 40f);

        float tabW = 162f;
        float startX = -336f;

        _tabEndlessBtn = CreateTabBtn(tabsObj.transform, "TabEndless", "SONSUZ", startX + (tabW + 6f) * 0, () => SetTab(LeaderboardManager.BoardMode.Endless), out _tabEndlessTMP);
        _tabHardcoreBtn = CreateTabBtn(tabsObj.transform, "TabHardcore", "☠ HARDCORE", startX + (tabW + 6f) * 1, () => SetTab(LeaderboardManager.BoardMode.Hardcore), out _tabHardcoreTMP);
        _tabBossRushBtn = CreateTabBtn(tabsObj.transform, "TabBossRush", "BOSS RUSH", startX + (tabW + 6f) * 2, () => SetTab(LeaderboardManager.BoardMode.BossRush), out _tabBossRushTMP);
        _tabTimeAttackBtn = CreateTabBtn(tabsObj.transform, "TabTimeAttack", "ZAMAN TAARRUZU", startX + (tabW + 6f) * 3, () => SetTab(LeaderboardManager.BoardMode.TimeAttack), out _tabTimeAttackTMP);
        _tabBulletHellBtn = CreateTabBtn(tabsObj.transform, "TabBulletHell", "MERMİ CEHENNEMİ", startX + (tabW + 6f) * 4, () => SetTab(LeaderboardManager.BoardMode.BulletHell), out _tabBulletHellTMP);

        // 4. SENİN DERECEN / KİŞİSEL EN İYİ REKOR KARTI
        GameObject myCard = new GameObject("PlayerBestCard");
        myCard.transform.SetParent(boxObj.transform, false);
        RectTransform mcRt = myCard.AddComponent<RectTransform>();
        mcRt.anchorMin = new Vector2(0f, 1f);
        mcRt.anchorMax = new Vector2(1f, 1f);
        mcRt.pivot = new Vector2(0.5f, 1f);
        mcRt.anchoredPosition = new Vector2(0f, -160f);
        mcRt.sizeDelta = new Vector2(840f, 42f);

        Image mcBg = myCard.AddComponent<Image>();
        mcBg.color = new Color(0.12f, 0.22f, 0.15f, 0.95f);

        Outline mcOutline = myCard.AddComponent<Outline>();
        mcOutline.effectColor = new Color(0.2f, 0.95f, 0.4f, 0.85f);
        mcOutline.effectDistance = new Vector2(2f, -2f);

        _playerBestRankTMP = myCard.AddComponent<TextMeshProUGUI>();
        _playerBestRankTMP.fontSize = 15f;
        _playerBestRankTMP.fontStyle = FontStyles.Bold;
        _playerBestRankTMP.alignment = TextAlignmentOptions.Center;
        _playerBestRankTMP.color = Color.white;

        // 5. TABLO BAŞLIK SATIRI (Sıra, Pilot Çağrı Kodu, Skor, Rütbe)
        GameObject headerObj = new GameObject("TableHeader");
        headerObj.transform.SetParent(boxObj.transform, false);
        RectTransform hRt = headerObj.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -208f);
        hRt.sizeDelta = new Vector2(840f, 28f);

        Image hBg = headerObj.AddComponent<Image>();
        hBg.color = new Color(0.08f, 0.14f, 0.22f, 0.95f);

        TextMeshProUGUI hTmp = headerObj.AddComponent<TextMeshProUGUI>();
        hTmp.text = "   SIRA         PİLOT ÇAĞRI KODU                                     SKOR / DERECE                    ASKERİ UNVAN";
        hTmp.fontSize = 13f;
        hTmp.fontStyle = FontStyles.Bold;
        hTmp.alignment = TextAlignmentOptions.Left;
        hTmp.color = new Color(0.6f, 0.85f, 1f);

        // 6. SKOR SATIRLARI KONTEYNERİ (Scroll Area)
        GameObject listObj = new GameObject("RowsContainer");
        listObj.transform.SetParent(boxObj.transform, false);
        RectTransform lRt = listObj.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 1f);
        lRt.offsetMin = new Vector2(20f, 75f);
        lRt.offsetMax = new Vector2(-20f, -240f);

        _rowsContainer = listObj.transform;

        // 7. ALT BİLGİ VE BUTONLAR
        // Bağlantı durumu
        GameObject statusObj = new GameObject("ConnectionStatus");
        statusObj.transform.SetParent(boxObj.transform, false);
        RectTransform stRt = statusObj.AddComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0f, 0f);
        stRt.anchorMax = new Vector2(0.6f, 0f);
        stRt.pivot = new Vector2(0f, 0f);
        stRt.anchoredPosition = new Vector2(24f, 22f);
        stRt.sizeDelta = new Vector2(480f, 36f);

        _connectionStatusTMP = statusObj.AddComponent<TextMeshProUGUI>();
        _connectionStatusTMP.fontSize = 13f;
        _connectionStatusTMP.alignment = TextAlignmentOptions.Left;
        _connectionStatusTMP.color = new Color(0.2f, 0.9f, 0.5f);

        // Yenile Butonu
        CreateActionButton(boxObj.transform, "RefreshBtn", "🔄 YENİLE", new Vector2(570f, 22f), new Vector2(130f, 44f), new Color(0.18f, 0.45f, 0.65f), () => RefreshScores());

        // Kapat Butonu
        CreateActionButton(boxObj.transform, "CloseBtn", "KAPAT [X]", new Vector2(715f, 22f), new Vector2(140f, 44f), new Color(0.65f, 0.20f, 0.20f), CloseLeaderboard);

        // İsim Değiştirme Modalını Hazırla
        SetupNameChangeModal();
    }

    private Button CreateTabBtn(Transform parent, string name, string text, float posX, UnityEngine.Events.UnityAction action, out TextMeshProUGUI labelTmp)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(posX, 0f);
        rt.sizeDelta = new Vector2(202f, 38f);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.10f, 0.18f, 0.28f, 0.90f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(btnObj.transform, false);
        RectTransform trt = tObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        labelTmp = tObj.AddComponent<TextMeshProUGUI>();
        labelTmp.text = text;
        labelTmp.fontSize = 13f;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = Color.white;
        labelTmp.raycastTarget = false;

        return btn;
    }

    private void CreateActionButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject bObj = new GameObject(name);
        bObj.transform.SetParent(parent, false);
        RectTransform rt = bObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = bObj.AddComponent<Image>();
        img.color = color;

        Outline outline = bObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.3f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = bObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(bObj.transform, false);
        RectTransform trt = tObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
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
        Outline outline = btn.GetComponent<Outline>() ?? btn.gameObject.AddComponent<Outline>();

        if (isSelected)
        {
            if (img != null) img.color = new Color(0.18f, 0.45f, 0.72f, 1f);
            if (tmp != null) tmp.color = Color.white;
            outline.effectColor = new Color(0f, 0.95f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
        else
        {
            if (img != null) img.color = new Color(0.08f, 0.14f, 0.20f, 0.85f);
            if (tmp != null) tmp.color = new Color(0.7f, 0.7f, 0.7f);
            outline.effectColor = new Color(0.2f, 0.3f, 0.4f, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);
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
            _connectionStatusTMP.text = "🔄 BULUT SUNUCUSUNA BAĞLANILIYOR...";
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
                ? "🟢 <b>CANLI BULUT BAĞLANTISI AKTİF</b> (Gerçek Oyuncu Veritabanı)"
                : "📡 <b>KÜRESEL ASLAR ARŞİVİ</b> (Çevrimdışı / Yerel Yedek)";
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
                    1 => "🥇 1. SIRA (ALTIN KUPA)",
                    2 => "🥈 2. SIRA (GÜMÜŞ MADALYA)",
                    3 => "🥉 3. SIRA (BRONZ MADALYA)",
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
        GameObject row = new GameObject($"Row_{entry.rank}");
        row.transform.SetParent(_rowsContainer, false);

        RectTransform rt = row.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -index * (rowHeight + 3f));
        rt.sizeDelta = new Vector2(840f, rowHeight);

        Image bg = row.AddComponent<Image>();

        if (entry.isPlayer)
        {
            // Oyuncunun kendi satırı: Neon yeşil/cyan vurgulu
            bg.color = new Color(0.10f, 0.28f, 0.22f, 0.95f);
            Outline o = row.AddComponent<Outline>();
            o.effectColor = new Color(0.2f, 1f, 0.6f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }
        else
        {
            bg.color = (index % 2 == 0)
                ? new Color(0.06f, 0.11f, 0.18f, 0.85f)
                : new Color(0.04f, 0.08f, 0.14f, 0.85f);
        }

        // Madalya / Sıra İkonu
        string rankIcon = entry.rank switch
        {
            1 => "<color=#FFD700>🥇 #1</color>",
            2 => "<color=#C0C0C0>🥈 #2</color>",
            3 => "<color=#CD7F32>🥉 #3</color>",
            _ => $"    #{entry.rank}"
        };

        string playerTag = entry.isPlayer ? " <color=#00E5FF><b>[SEN]</b></color>" : "";
        string displayName = $"{entry.playerName}{playerTag}";

        TextMeshProUGUI tmp = row.AddComponent<TextMeshProUGUI>();
        tmp.text = $" {rankIcon,-8}   {displayName,-38}   <color=#FFD54F><b>{entry.formattedScore,-22}</b></color>   <color=#8AB4F8>{entry.rankTitle}</color>";
        tmp.fontSize = 13f;
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

        _nameChangeModal = new GameObject("NameChangeModal");
        _nameChangeModal.transform.SetParent(_panelRoot.transform, false);

        RectTransform mRt = _nameChangeModal.AddComponent<RectTransform>();
        mRt.anchorMin = Vector2.zero;
        mRt.anchorMax = Vector2.one;
        mRt.offsetMin = Vector2.zero;
        mRt.offsetMax = Vector2.zero;

        Image mBg = _nameChangeModal.AddComponent<Image>();
        mBg.color = new Color(0f, 0f, 0f, 0.82f);

        GameObject boxObj = new GameObject("DialogBox");
        boxObj.transform.SetParent(_nameChangeModal.transform, false);
        RectTransform bRt = boxObj.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 0.5f);
        bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.sizeDelta = new Vector2(500f, 240f);

        Image bBg = boxObj.AddComponent<Image>();
        bBg.color = new Color(0.08f, 0.14f, 0.22f, 0.98f);

        Outline bOutline = boxObj.AddComponent<Outline>();
        bOutline.effectColor = new Color(0f, 0.85f, 1f, 0.9f);
        bOutline.effectDistance = new Vector2(3f, -3f);

        // Başlık
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(boxObj.transform, false);
        RectTransform tRt = tObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -20f);
        tRt.sizeDelta = new Vector2(460f, 40f);

        TextMeshProUGUI titleTMP = tObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "PİLOT ÇAĞRI KODUNUZU BELİRLEYİN";
        titleTMP.fontSize = 17f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0f, 0.85f, 1f);

        // Açıklama
        GameObject subObj = new GameObject("Sub");
        subObj.transform.SetParent(boxObj.transform, false);
        RectTransform sRt = subObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, -55f);
        sRt.sizeDelta = new Vector2(460f, 25f);

        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.text = "Liderlik tablosunda diğer pilotların göreceği isim:";
        subTMP.fontSize = 13f;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = Color.white;

        // Input Field
        GameObject inpObj = new GameObject("InputField");
        inpObj.transform.SetParent(boxObj.transform, false);
        RectTransform inRt = inpObj.AddComponent<RectTransform>();
        inRt.anchorMin = new Vector2(0.5f, 0.5f);
        inRt.anchorMax = new Vector2(0.5f, 0.5f);
        inRt.pivot = new Vector2(0.5f, 0.5f);
        inRt.anchoredPosition = new Vector2(0f, -10f);
        inRt.sizeDelta = new Vector2(360f, 44f);

        Image inBg = inpObj.AddComponent<Image>();
        inBg.color = new Color(0.04f, 0.08f, 0.12f, 1f);

        Outline inOutline = inpObj.AddComponent<Outline>();
        inOutline.effectColor = new Color(0f, 0.85f, 1f, 0.6f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inpObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 2f);
        textRt.offsetMax = new Vector2(-10f, -2f);

        TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 16f;
        inputText.fontStyle = FontStyles.Bold;
        inputText.alignment = TextAlignmentOptions.Center;
        inputText.color = Color.white;

        _nameInputField = inpObj.AddComponent<TMP_InputField>();
        _nameInputField.textComponent = inputText;
        _nameInputField.characterLimit = 14;
        _nameInputField.text = LeaderboardManager.PlayerCallsign;

        // Kaydet Butonu
        CreateActionButton(boxObj.transform, "SaveNameBtn", "KAYDET", new Vector2(90f, 22f), new Vector2(140f, 40f), new Color(0.18f, 0.55f, 0.32f), SaveCallsignAndClose);

        // İptal Butonu
        CreateActionButton(boxObj.transform, "CancelNameBtn", "İPTAL", new Vector2(270f, 22f), new Vector2(140f, 40f), new Color(0.50f, 0.25f, 0.25f), () => _nameChangeModal.SetActive(false));

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

