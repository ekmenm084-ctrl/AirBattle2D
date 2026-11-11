using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Günlük Operasyon Görevleri ve Pilot Madalyaları tam ekran Taktik HUD arayüzü.
/// Ana menü, Hangar ve Lojistik Ekranı ile aynı aviyonik görsel dili paylaşır.
/// </summary>
public class DailyOpsUI : MonoBehaviour
{
    public static DailyOpsUI Instance { get; private set; }

    private enum OpsTab
    {
        DailyMissions,
        PilotMedals
    }

    private struct CardViewRefs
    {
        public GameObject root;
        public TextMeshProUGUI tagText;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descText;
        public RectTransform progressFill;
        public TextMeshProUGUI progressText;
        public TextMeshProUGUI rewardText;
        public Button actionButton;
        public Image actionBtnBg;
        public TextMeshProUGUI actionBtnText;
    }

    private static readonly Color ColBgBackdrop   = new Color(0.03f, 0.06f, 0.11f, 0.96f);
    private static readonly Color ColCardGlass    = new Color(0.05f, 0.09f, 0.16f, 0.90f);
    private static readonly Color ColCardReadyBg  = new Color(0.11f, 0.09f, 0.03f, 0.94f);
    private static readonly Color ColTeal         = new Color(0.18f, 0.85f, 0.92f, 1f);
    private static readonly Color ColGold         = new Color(1.00f, 0.78f, 0.18f, 1f);
    private static readonly Color ColEmerald      = new Color(0.16f, 0.92f, 0.56f, 1f);
    private static readonly Color ColMuted        = new Color(0.55f, 0.65f, 0.76f, 1f);
    private static readonly Color ColTrack        = new Color(0.08f, 0.13f, 0.22f, 0.95f);

    private bool _built;
    private OpsTab _activeTab = OpsTab.DailyMissions;
    private Action _onCloseCallback;
    private float _countdownTimer;

    private TextMeshProUGUI _subtitleText;
    private Button _dailyTabBtn;
    private Image _dailyTabBg;
    private TextMeshProUGUI _dailyTabText;

    private Button _medalsTabBtn;
    private Image _medalsTabBg;
    private TextMeshProUGUI _medalsTabText;

    private Button _claimAllBtn;
    private Image _claimAllBg;
    private TextMeshProUGUI _claimAllText;

    private ScrollRect _scrollRect;
    private RectTransform _contentRect;
    private TextMeshProUGUI _statusToastText;
    private Button _backButton;

    private readonly List<GameObject> _spawnedCards = new List<GameObject>();

    public static DailyOpsUI EnsureUI()
    {
        if (Instance != null)
        {
            if (!Instance._built) Instance.BuildUI();
            return Instance;
        }

        DailyOpsUI existing = FindAnyObjectByType<DailyOpsUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            if (!existing._built) existing.BuildUI();
            return existing;
        }

        Canvas canvas = null;
        MainMenuManager menu = FindAnyObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (menu != null) canvas = menu.GetComponentInParent<Canvas>(true);
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

        if (canvas == null)
        {
            Debug.LogWarning("[DailyOpsUI] Sahne üzerinde Canvas bulunamadı.");
            return null;
        }

        GameObject panelObj = new GameObject("DailyOpsPanel", typeof(RectTransform), typeof(Image), typeof(DailyOpsUI));
        panelObj.transform.SetParent(canvas.transform, false);
        MainMenuManager.MakeFullScreenRect(panelObj.GetComponent<RectTransform>());

        DailyOpsUI ui = panelObj.GetComponent<DailyOpsUI>();
        Instance = ui;
        ui.BuildUI();
        panelObj.SetActive(false);
        return ui;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        DailyOpsManager mgr = DailyOpsManager.EnsureInstance();
        if (mgr != null)
            mgr.OnDailyOpsChanged += HandleDailyOpsChanged;
    }

    private void OnDisable()
    {
        if (DailyOpsManager.Instance != null)
            DailyOpsManager.Instance.OnDailyOpsChanged -= HandleDailyOpsChanged;
    }

    private void Update()
    {
        if (!_built || !gameObject.activeInHierarchy) return;

        _countdownTimer += Time.unscaledDeltaTime;
        if (_countdownTimer >= 15f)
        {
            _countdownTimer = 0f;
            DailyOpsManager.EnsureInstance()?.CheckDailyRollover();
            UpdateHeaderAndTabs();
        }
    }

    public void OpenOpsPanel(Action onClose = null)
    {
        if (!_built) BuildUI();

        _onCloseCallback = onClose;
        _countdownTimer = 0f;

        DailyOpsManager.EnsureInstance()?.CheckDailyRollover();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (PlayerProfileUI.Instance != null)
        {
            PlayerProfileUI.Instance.ShowMenuStatusBar(showGoldBadge: true);
            PlayerProfileUI.Instance.RefreshBadgeVisuals();
        }

        if (_statusToastText != null) _statusToastText.text = "";
        RefreshAll();

        if (_scrollRect != null)
            _scrollRect.normalizedPosition = new Vector2(0f, 1f);
    }

    public void CloseOpsPanel()
    {
        Action cb = _onCloseCallback;
        _onCloseCallback = null;
        gameObject.SetActive(false);
        cb?.Invoke();
    }

    public void CloseDirectly()
    {
        _onCloseCallback = null;
        gameObject.SetActive(false);
    }

    private void HandleDailyOpsChanged()
    {
        if (_built && gameObject.activeInHierarchy)
            RefreshAll();
    }

    private void BuildUI()
    {
        if (_built) return;
        _built = true;

        bool wasActive = gameObject.activeSelf;
        if (!wasActive) gameObject.SetActive(true);

        // Arkaplan katmanı
        Image bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = ColBgBackdrop;
        bg.raycastTarget = true;

        // Üst Taktik Başlık Kutusu
        GameObject headerObj = CreatePanelChild("HeaderBox", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(880f, 92f));
        Image headerImg = headerObj.AddComponent<Image>();
        headerImg.sprite = MainMenuManager.GetHudSprite("OpsHeader", ColTeal, ColCardGlass, hasLeftBar: true);
        headerImg.type = Image.Type.Sliced;
        headerImg.raycastTarget = false;

        TextMeshProUGUI titleTmp = CreateTMP("TitleText", headerObj.transform, "OPERASYON MERKEZİ // GÖREV & MADALYA", 30, ColTeal, FontStyles.Bold, TextAlignmentOptions.Center);
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMin = 24f;
        titleTmp.fontSizeMax = 30f;
        StretchChild(titleTmp.rectTransform, 20f, -20f, 36f, -2f);
        MainMenuManager.SetOutlineSafe(titleTmp, 0.20f, new Color(0f, 0f, 0f, 0.9f));

        _subtitleText = CreateTMP("SubtitleText", headerObj.transform, "", 19, new Color(0.86f, 0.94f, 1f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
        _subtitleText.enableAutoSizing = true;
        _subtitleText.fontSizeMin = 15f;
        _subtitleText.fontSizeMax = 20f;
        StretchChild(_subtitleText.rectTransform, 20f, -20f, 4f, -54f);
        MainMenuManager.SetOutlineSafe(_subtitleText, 0.14f, Color.black);

        // Sekme ve Hızlı Toplama Çubuğu
        GameObject tabBar = CreatePanelChild("TabBar", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(1420f, 58f));

        _dailyTabBtn = CreateActionButton(
            "DailyTabBtn", tabBar.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(430f, 56f),
            "[+] GÜNLÜK GÖREVLER", 20, ColTeal,
            out _dailyTabBg, out _dailyTabText,
            () => SwitchTab(OpsTab.DailyMissions)
        );

        _medalsTabBtn = CreateActionButton(
            "MedalsTabBtn", tabBar.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(446f, 0f), new Vector2(430f, 56f),
            "[#] PİLOT MADALYALARI", 20, ColMuted,
            out _medalsTabBg, out _medalsTabText,
            () => SwitchTab(OpsTab.PilotMedals)
        );

        _claimAllBtn = CreateActionButton(
            "ClaimAllBtn", tabBar.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(420f, 56f),
            "[»] TÜM ÖDÜLLERİ AL", 20, ColGold,
            out _claimAllBg, out _claimAllText,
            OnClaimAllClicked
        );

        // Ana Kaydırılabilir Görev/Madalya Alanı
        GameObject scrollFrame = new GameObject("OpsScrollFrame", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollFrame.transform.SetParent(transform, false);
        RectTransform scrollFrameRt = scrollFrame.GetComponent<RectTransform>();
        scrollFrameRt.anchorMin = new Vector2(0.5f, 0f);
        scrollFrameRt.anchorMax = new Vector2(0.5f, 1f);
        scrollFrameRt.offsetMin = new Vector2(-710f, 92f);
        scrollFrameRt.offsetMax = new Vector2(710f, -180f);

        Image frameBg = scrollFrame.GetComponent<Image>();
        frameBg.sprite = MainMenuManager.GetHudSprite("OpsScrollFrame", new Color(0.18f, 0.85f, 0.92f, 0.45f), new Color(0.03f, 0.05f, 0.09f, 0.72f), hasLeftBar: false);
        frameBg.type = Image.Type.Sliced;
        frameBg.raycastTarget = true;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollFrame.transform, false);
        RectTransform vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(12f, 12f);
        vpRt.offsetMax = new Vector2(-22f, -12f);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        _contentRect = content.GetComponent<RectTransform>();
        _contentRect.anchorMin = new Vector2(0f, 1f);
        _contentRect.anchorMax = new Vector2(1f, 1f);
        _contentRect.pivot = new Vector2(0.5f, 1f);
        _contentRect.anchoredPosition = Vector2.zero;
        _contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.padding = new RectOffset(4, 4, 4, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Dikey Scrollbar
        GameObject sbObj = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        sbObj.transform.SetParent(scrollFrame.transform, false);
        RectTransform sbRt = sbObj.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(1f, 0f);
        sbRt.anchorMax = new Vector2(1f, 1f);
        sbRt.pivot = new Vector2(1f, 0.5f);
        sbRt.offsetMin = new Vector2(-14f, 12f);
        sbRt.offsetMax = new Vector2(-6f, -12f);
        sbObj.GetComponent<Image>().color = new Color(0.10f, 0.16f, 0.25f, 0.85f);

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObj.transform.SetParent(sbObj.transform, false);
        RectTransform handleRt = handleObj.GetComponent<RectTransform>();
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = Vector2.one;
        handleRt.sizeDelta = Vector2.zero;
        Image handleImg = handleObj.GetComponent<Image>();
        handleImg.color = new Color(0.18f, 0.85f, 0.92f, 0.78f);

        Scrollbar scrollbar = sbObj.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImg;
        scrollbar.handleRect = handleRt;

        _scrollRect = scrollFrame.GetComponent<ScrollRect>();
        _scrollRect.content = _contentRect;
        _scrollRect.viewport = vpRt;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 36f;
        _scrollRect.verticalScrollbar = scrollbar;
        _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // Alt Ödül Bildirim Satırı
        GameObject toastObj = new GameObject("StatusToast", typeof(RectTransform));
        toastObj.transform.SetParent(transform, false);
        RectTransform toastRt = toastObj.GetComponent<RectTransform>();
        toastRt.anchorMin = new Vector2(0.5f, 0f);
        toastRt.anchorMax = new Vector2(0.5f, 0f);
        toastRt.pivot = new Vector2(0.5f, 0f);
        toastRt.anchoredPosition = new Vector2(0f, 30f);
        toastRt.sizeDelta = new Vector2(880f, 44f);

        _statusToastText = toastObj.AddComponent<TextMeshProUGUI>();
        _statusToastText.font = MainMenuManager.GetTacticalFont();
        _statusToastText.fontSize = 22;
        _statusToastText.fontStyle = FontStyles.Bold;
        _statusToastText.alignment = TextAlignmentOptions.Center;
        _statusToastText.color = ColEmerald;
        _statusToastText.raycastTarget = false;
        _statusToastText.text = "";
        MainMenuManager.SetOutlineSafe(_statusToastText, 0.16f, Color.black);

        // Sol Alt Standart Geri Dön Tuşu
        GameObject backObj = new GameObject("BackFromOpsBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        backObj.transform.SetParent(transform, false);
        _backButton = backObj.GetComponent<Button>();

        GameObject backLabelObj = new GameObject("Text", typeof(RectTransform));
        backLabelObj.transform.SetParent(backObj.transform, false);
        MainMenuManager.MakeFullScreenRect(backLabelObj.GetComponent<RectTransform>());
        TextMeshProUGUI backTmp = backLabelObj.AddComponent<TextMeshProUGUI>();
        backTmp.text = "« GERİ DÖN";
        backTmp.font = MainMenuManager.GetTacticalFont();
        backTmp.raycastTarget = false;

        MainMenuManager.FormatBottomLeftBackBtn(_backButton, CloseOpsPanel);

        if (!wasActive) gameObject.SetActive(false);
    }

    private void SwitchTab(OpsTab tab)
    {
        if (_activeTab == tab) return;
        _activeTab = tab;
        RefreshAll();
        if (_scrollRect != null)
            _scrollRect.normalizedPosition = new Vector2(0f, 1f);
    }

    private void RefreshAll()
    {
        UpdateHeaderAndTabs();
        RebuildCardList();
    }

    private void UpdateHeaderAndTabs()
    {
        DailyOpsManager mgr = DailyOpsManager.EnsureInstance();
        int dailyClaimable = mgr != null ? mgr.GetClaimableDailyCount() : 0;
        int medalClaimable = mgr != null ? mgr.GetClaimableMedalCount() : 0;
        int totalClaimable = dailyClaimable + medalClaimable;
        string resetIn = mgr != null ? mgr.GetTimeUntilResetFormatted() : "--";

        if (_subtitleText != null)
        {
            string readyTag = totalClaimable > 0
                ? $"  |  {totalClaimable} ÖDÜL TOPLANMAYA HAZIR"
                : "  |  TÜM GÖREVLER GÜNCEL";
            _subtitleText.text = $"GÜNLÜK ROTASYON YENİLENMESİ: {resetIn}{readyTag}";
        }

        // Sekme stilleri
        bool dailySelected = _activeTab == OpsTab.DailyMissions;
        StyleTabButton(
            _dailyTabBg, _dailyTabText,
            selected: dailySelected,
            badgeCount: dailyClaimable,
            baseLabel: "[+] GÜNLÜK GÖREVLER (4)"
        );
        StyleTabButton(
            _medalsTabBg, _medalsTabText,
            selected: !dailySelected,
            badgeCount: medalClaimable,
            baseLabel: "[#] PİLOT MADALYALARI (9)"
        );

        // Tümünü Al butonu
        if (_claimAllBtn != null && _claimAllBg != null && _claimAllText != null)
        {
            bool anyClaimable = totalClaimable > 0;
            _claimAllBtn.interactable = anyClaimable;
            Color accent = anyClaimable ? ColGold : ColMuted;
            Color glass = anyClaimable ? new Color(0.15f, 0.11f, 0.02f, 0.94f) : new Color(0.05f, 0.08f, 0.13f, 0.80f);
            _claimAllBg.sprite = MainMenuManager.GetHudSprite("ClaimAllBtn_" + anyClaimable, accent, glass, hasLeftBar: true);
            _claimAllText.color = anyClaimable ? ColGold : ColMuted;
            _claimAllText.text = anyClaimable
                ? $"[»] TÜM ÖDÜLLERİ AL ({totalClaimable})"
                : "[—] BEKLEYEN ÖDÜL YOK";
        }
    }

    private static void StyleTabButton(Image bg, TextMeshProUGUI label, bool selected, int badgeCount, string baseLabel)
    {
        if (bg == null || label == null) return;
        Color border = selected ? ColTeal : (badgeCount > 0 ? ColGold : new Color(0.32f, 0.44f, 0.58f, 0.85f));
        Color glass = selected ? new Color(0.08f, 0.19f, 0.28f, 0.95f) : new Color(0.05f, 0.09f, 0.16f, 0.86f);
        bg.sprite = MainMenuManager.GetHudSprite($"OpsTab_{selected}_{badgeCount > 0}", border, glass, hasLeftBar: true);
        label.color = selected ? Color.white : (badgeCount > 0 ? ColGold : new Color(0.75f, 0.83f, 0.92f, 1f));
        label.text = badgeCount > 0 ? $"{baseLabel} [+{badgeCount}]" : baseLabel;
    }

    private void RebuildCardList()
    {
        for (int i = 0; i < _spawnedCards.Count; i++)
        {
            GameObject oldCard = _spawnedCards[i];
            if (oldCard != null)
            {
                oldCard.SetActive(false);
                oldCard.transform.SetParent(null, false);
                Destroy(oldCard);
            }
        }
        _spawnedCards.Clear();

        DailyOpsManager mgr = DailyOpsManager.EnsureInstance();
        if (mgr == null || _contentRect == null) return;

        if (_activeTab == OpsTab.DailyMissions)
        {
            var missions = mgr.GetDailyMissions();
            for (int i = 0; i < missions.Count; i++)
            {
                DailyOpsManager.DailyMissionState m = missions[i];
                if (m == null) continue;

                Color cardAccent = m.CanClaim ? ColGold : (m.claimed ? ColEmerald : ColTeal);
                Color cardGlass = m.CanClaim ? ColCardReadyBg : ColCardGlass;

                CardViewRefs refs = CreateOpsCardView($"MissionCard_{i}", 182f, cardAccent, cardGlass);
                refs.tagText.text = $"{m.template.tag}  •  GÖREV {i + 1}/4";
                refs.tagText.color = cardAccent;
                refs.titleText.text = m.template.title;
                refs.descText.text = m.template.description;

                SetProgressBar(
                    refs.progressFill, refs.progressText,
                    m.ProgressRatio,
                    m.claimed ? "TAMAMLANDI — ÖDÜL ALINDI" : $"{m.ClampedProgress} / {m.template.targetCount}",
                    cardAccent
                );

                refs.rewardText.text = "ÖDÜL: " + FormatRewardText(
                    m.template.goldReward, m.template.expReward,
                    m.template.hasKeyReward, m.template.keyType, m.template.keyCount
                );

                int capturedSlot = m.slotIndex;
                string capturedTitle = m.template.title;
                ConfigureCardActionButton(
                    refs,
                    canClaim: m.CanClaim,
                    isCompletedAndClaimed: m.claimed,
                    claimLabel: "[»] ÖDÜLÜ AL",
                    doneLabel: "ALINDI",
                    progressLabel: $"%{Mathf.RoundToInt(m.ProgressRatio * 100f)}",
                    onClick: () => OnClaimDailyMissionClicked(capturedSlot, capturedTitle)
                );
            }
        }
        else
        {
            var medals = mgr.GetMedalStates();
            for (int i = 0; i < medals.Count; i++)
            {
                DailyOpsManager.PilotMedalState medal = medals[i];
                Color cardAccent = medal.CanClaim ? ColGold : (medal.IsMaxed ? ColEmerald : ColTeal);
                Color cardGlass = medal.CanClaim ? ColCardReadyBg : ColCardGlass;

                CardViewRefs refs = CreateOpsCardView($"MedalCard_{medal.def.id}", 178f, cardAccent, cardGlass);

                string tierBadge = medal.IsMaxed
                    ? "KADEME III/III [TAMAMLANDI]"
                    : $"KADEME {DailyOpsManager.GetTierRoman(medal.ActiveTierIndex)}/III";
                refs.tagText.text = $"{medal.def.code}  •  {tierBadge}  |  {FormatMedalTierPips(medal.claimedTier, medal.def.tiers.Length)}";
                refs.tagText.color = cardAccent;

                refs.titleText.text = medal.def.title;
                refs.descText.text = $"{medal.def.description} (Hedef: {medal.ActiveTier.target})";

                SetProgressBar(
                    refs.progressFill, refs.progressText,
                    medal.ProgressRatio,
                    medal.IsMaxed ? $"AZAMİ SEVİYE ({medal.currentStat})" : $"{medal.ClampedProgress} / {medal.ActiveTier.target}",
                    cardAccent
                );

                DailyOpsManager.MedalTierReward tierReward = medal.ActiveTier;
                refs.rewardText.text = medal.IsMaxed
                    ? $"TOPLAM REKORUNUZ: {medal.currentStat}  •  TÜM KADEMELER TAMAMLANDI"
                    : $"KADEME {DailyOpsManager.GetTierRoman(medal.ActiveTierIndex)} ÖDÜLÜ: " + FormatRewardText(
                        tierReward.goldReward, tierReward.expReward,
                        tierReward.hasKeyReward, tierReward.keyType, tierReward.keyCount
                    );

                string capturedId = medal.def.id;
                string capturedTitle = medal.def.title;
                ConfigureCardActionButton(
                    refs,
                    canClaim: medal.CanClaim,
                    isCompletedAndClaimed: medal.IsMaxed,
                    claimLabel: $"[»] {DailyOpsManager.GetTierRoman(medal.ActiveTierIndex)}. KADEME AL",
                    doneLabel: "TAMAMLANDI",
                    progressLabel: $"%{Mathf.RoundToInt(medal.ProgressRatio * 100f)}",
                    onClick: () => OnClaimMedalClicked(capturedId, capturedTitle)
                );
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
        Canvas.ForceUpdateCanvases();
    }

    private CardViewRefs CreateOpsCardView(string name, float height, Color accentColor, Color glassColor)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(_contentRect, false);
        RectTransform rt = card.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, height);

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        Image cardBg = card.GetComponent<Image>();
        cardBg.sprite = MainMenuManager.GetHudSprite($"OpsCard_{accentColor.GetHashCode()}_{glassColor.GetHashCode()}", accentColor, glassColor, hasLeftBar: true);
        cardBg.type = Image.Type.Sliced;
        cardBg.raycastTarget = false;

        _spawnedCards.Add(card);

        // Sol Taraf: Etiket, Başlık, Açıklama, İlerleme Çubuğu, Ödül Metni
        TextMeshProUGUI tagTmp = CreateTMP("Tag", card.transform, "", 18, accentColor, FontStyles.Bold, TextAlignmentOptions.Left);
        SetTopLeftRect(tagTmp.rectTransform, 28f, -8f, 1020f, 30f);
        MainMenuManager.SetOutlineSafe(tagTmp, 0.14f, Color.black);

        TextMeshProUGUI titleTmp = CreateTMP("Title", card.transform, "", 26, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        SetTopLeftRect(titleTmp.rectTransform, 28f, -38f, 1020f, 40f);
        MainMenuManager.SetOutlineSafe(titleTmp, 0.20f, Color.black);

        TextMeshProUGUI descTmp = CreateTMP("Desc", card.transform, "", 20, new Color(0.84f, 0.91f, 0.98f, 1f), FontStyles.Bold, TextAlignmentOptions.Left);
        SetTopLeftRect(descTmp.rectTransform, 28f, -78f, 1020f, 32f);
        MainMenuManager.SetOutlineSafe(descTmp, 0.14f, Color.black);

        // İlerleme Çubuğu
        GameObject trackObj = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image));
        trackObj.transform.SetParent(card.transform, false);
        RectTransform trackRt = trackObj.GetComponent<RectTransform>();
        SetTopLeftRect(trackRt, 28f, -112f, 690f, 30f);
        Image trackImg = trackObj.GetComponent<Image>();
        trackImg.color = ColTrack;
        trackImg.raycastTarget = false;

        GameObject fillObj = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(trackObj.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.color = accentColor;
        fillImg.raycastTarget = false;

        TextMeshProUGUI progTmp = CreateTMP("ProgressLabel", trackObj.transform, "", 17, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        MainMenuManager.MakeFullScreenRect(progTmp.rectTransform);
        MainMenuManager.SetOutlineSafe(progTmp, 0.16f, Color.black);

        // Ödül Metni
        TextMeshProUGUI rewardTmp = CreateTMP("RewardText", card.transform, "", 19, ColGold, FontStyles.Bold, TextAlignmentOptions.Left);
        SetTopLeftRect(rewardTmp.rectTransform, 28f, -145f, 1020f, 32f);
        MainMenuManager.SetOutlineSafe(rewardTmp, 0.14f, Color.black);

        // Sağ Taraf: Ödül Toplama Butonu
        Button actionBtn = CreateActionButton(
            "ActionBtn", card.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(290f, 64f),
            "", 20, accentColor,
            out Image actionBg, out TextMeshProUGUI actionTmp,
            null
        );

        return new CardViewRefs
        {
            root = card,
            tagText = tagTmp,
            titleText = titleTmp,
            descText = descTmp,
            progressFill = fillRt,
            progressText = progTmp,
            rewardText = rewardTmp,
            actionButton = actionBtn,
            actionBtnBg = actionBg,
            actionBtnText = actionTmp
        };
    }

    private static void SetProgressBar(RectTransform fillRt, TextMeshProUGUI label, float ratio, string text, Color fillColor)
    {
        if (fillRt != null)
        {
            float clamped = Mathf.Clamp01(ratio);
            fillRt.anchorMax = new Vector2(clamped, 1f);
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(clamped > 0.01f ? -2f : 0f, -2f);
            Image img = fillRt.GetComponent<Image>();
            if (img != null) img.color = fillColor;
        }
        if (label != null) label.text = text;
    }

    private static void ConfigureCardActionButton(
        CardViewRefs refs,
        bool canClaim,
        bool isCompletedAndClaimed,
        string claimLabel,
        string doneLabel,
        string progressLabel,
        UnityAction onClick)
    {
        refs.actionButton.onClick.RemoveAllListeners();

        if (canClaim)
        {
            refs.actionButton.interactable = true;
            refs.actionBtnBg.sprite = MainMenuManager.GetHudSprite("CardAct_Claim", ColGold, new Color(0.20f, 0.14f, 0.02f, 0.96f), hasLeftBar: true);
            refs.actionBtnText.color = ColGold;
            refs.actionBtnText.text = claimLabel;
            if (onClick != null) refs.actionButton.onClick.AddListener(onClick);
        }
        else if (isCompletedAndClaimed)
        {
            refs.actionButton.interactable = false;
            refs.actionBtnBg.sprite = MainMenuManager.GetHudSprite("CardAct_Done", ColEmerald, new Color(0.04f, 0.14f, 0.09f, 0.85f), hasLeftBar: true);
            refs.actionBtnText.color = ColEmerald;
            refs.actionBtnText.text = doneLabel;
        }
        else
        {
            refs.actionButton.interactable = false;
            refs.actionBtnBg.sprite = MainMenuManager.GetHudSprite("CardAct_Prog", ColMuted, new Color(0.06f, 0.09f, 0.15f, 0.85f), hasLeftBar: true);
            refs.actionBtnText.color = new Color(0.75f, 0.84f, 0.94f, 1f);
            refs.actionBtnText.text = progressLabel;
        }
    }

    private void OnClaimDailyMissionClicked(int slotIndex, string title)
    {
        DailyOpsManager mgr = DailyOpsManager.EnsureInstance();
        if (mgr != null && mgr.ClaimDailyMission(slotIndex))
            ShowStatusToast($"[ÖDÜL ALINDI] {title} ödülleri envanterinize eklendi!");
    }

    private void OnClaimMedalClicked(string medalId, string title)
    {
        DailyOpsManager mgr = DailyOpsManager.EnsureInstance();
        if (mgr != null && mgr.ClaimMedal(medalId))
            ShowStatusToast($"[MADALYA ÖDÜLÜ ALINDI] {title} kademe ödülü teslim edildi!");
    }

    private void OnClaimAllClicked()
    {
        DailyOpsManager mgr = DailyOpsManager.EnsureInstance();
        if (mgr == null) return;

        int claimed = mgr.ClaimAllReadyRewards();
        if (claimed > 0)
            ShowStatusToast($"[TOPLU TESLİMAT] {claimed} adet görev ve madalya ödülü toplandı!");
    }

    private void ShowStatusToast(string msg)
    {
        if (_statusToastText != null)
            _statusToastText.text = msg;
    }

    private static string FormatRewardText(int gold, int exp, bool hasKey, HangarManager.KeyType keyType, int keyCount)
    {
        var parts = new List<string>(3);
        if (gold > 0) parts.Add($"+{gold} ALTIN");
        if (exp > 0) parts.Add($"+{exp} EXP");
        if (hasKey && keyCount > 0) parts.Add($"+{keyCount} {DailyOpsManager.GetKeyTurkishName(keyType).ToUpperInvariant()}");
        return parts.Count > 0 ? string.Join("  •  ", parts) : "—";
    }

    private static string FormatMedalTierPips(int claimedTier, int totalTiers)
    {
        var parts = new List<string>(totalTiers);
        for (int i = 0; i < totalTiers; i++)
        {
            string roman = DailyOpsManager.GetTierRoman(i);
            parts.Add(i < claimedTier ? $"[{roman}:OK]" : $"[{roman}]");
        }
        return string.Join(" ", parts);
    }

    private static GameObject CreatePanelChild(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetAnchoredBox(go.GetComponent<RectTransform>(), anchorMin.x, anchorMax.x, anchorMin.y, anchorMax.y, pivot, anchoredPos, size);
        return go;
    }

    private static void SetTopLeftRect(RectTransform rt, float left, float topOffset, float width, float height)
    {
        SetAnchoredBox(rt, 0f, 0f, 1f, 1f, new Vector2(0f, 1f), new Vector2(left, topOffset), new Vector2(width, height));
    }

    private static void SetAnchoredBox(RectTransform rt, float minX, float maxX, float minY, float maxY, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void StretchChild(RectTransform rt, float left, float right, float bottom, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    private static TextMeshProUGUI CreateTMP(string name, Transform parent, string text, int size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.characterSpacing = 1.2f;
        tmp.color = color;
        tmp.alignment = align;
        tmp.text = text;
        tmp.raycastTarget = false;
        tmp.extraPadding = true;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static Button CreateActionButton(
        string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
        string label, int fontSize, Color accentColor,
        out Image bgImg, out TextMeshProUGUI labelTmp,
        UnityAction onClick)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        SetAnchoredBox(obj.GetComponent<RectTransform>(), anchorMin.x, anchorMax.x, anchorMin.y, anchorMax.y, pivot, anchoredPos, size);

        bgImg = obj.GetComponent<Image>();
        bgImg.sprite = MainMenuManager.GetHudSprite($"OpsBtn_{name}", accentColor, new Color(0.06f, 0.11f, 0.19f, 0.92f), hasLeftBar: true);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;
        bgImg.raycastTarget = true;

        Button btn = obj.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        cb.pressedColor = new Color(0.80f, 0.85f, 0.90f, 1f);
        cb.disabledColor = new Color(0.85f, 0.85f, 0.85f, 0.85f);
        btn.colors = cb;

        if (onClick != null) btn.onClick.AddListener(onClick);

        labelTmp = CreateTMP("Label", obj.transform, label, fontSize, accentColor, FontStyles.Bold, TextAlignmentOptions.Center);
        labelTmp.enableAutoSizing = true;
        labelTmp.fontSizeMin = Mathf.Max(15, fontSize - 3);
        labelTmp.fontSizeMax = fontSize + 1;
        StretchChild(labelTmp.rectTransform, 10f, -10f, 4f, -4f);
        MainMenuManager.SetOutlineSafe(labelTmp, 0.16f, Color.black);

        return btn;
    }
}
