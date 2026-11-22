using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 1:1 Günlük Operasyon Görevleri ve Pilot Madalyaları tam ekran Taktik HUD arayüzü (OPERASYON MERKEZİ // GÖREV & MADALYA).
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
        public Image cardBg;
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

    private static readonly Color ColTeal    = new Color(0.18f, 0.88f, 0.96f, 1f);
    private static readonly Color ColGold    = new Color(0.88f, 0.64f, 0.24f, 1f);
    private static readonly Color ColEmerald = new Color(0.24f, 0.90f, 0.52f, 1f);
    private static readonly Color ColMuted   = new Color(0.58f, 0.68f, 0.78f, 1f);
    private static readonly Color ColTrack   = new Color(0.08f, 0.13f, 0.21f, 0.96f);

    private static Sprite _cachedBgSprite;
    private static Sprite _cachedTabActiveSprite;
    private static Sprite _cachedTabInactiveSprite;
    private static Sprite _cachedCardCyanSprite;
    private static Sprite _cachedCardEmeraldSprite;
    private static Sprite _cachedBtnSlateSprite;
    private static Sprite _cachedBtnEmeraldSprite;

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

    private readonly List<GameObject> _spawnedCards = new List<GameObject>();

    private static Sprite LoadOpsSprite(string fileName, string resName, ref Sprite cache)
    {
        if (cache != null) return cache;

        string path = Path.Combine(Application.dataPath, "Sprites/" + fileName);
        if (File.Exists(path))
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                cache = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return cache;
            }
        }

        Sprite res = Resources.Load<Sprite>(resName);
        if (res != null)
        {
            cache = res;
            return res;
        }

        Texture2D resTex = Resources.Load<Texture2D>(resName);
        if (resTex != null)
        {
            cache = Sprite.Create(resTex, new Rect(0, 0, resTex.width, resTex.height), new Vector2(0.5f, 0.5f), 100f);
            return cache;
        }

        return null;
    }

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

        // 1. Tam ekran 1:1 ops_background.jpg arka planı
        Image bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        Sprite bgSpr = LoadOpsSprite("ops_background.jpg", "ops_background", ref _cachedBgSprite);
        if (bgSpr != null)
        {
            bg.sprite = bgSpr;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.color = Color.white;
        }
        else
        {
            bg.color = new Color(0.03f, 0.06f, 0.11f, 0.96f);
        }
        bg.raycastTarget = true;

        // 2. Üst Alt Başlık: GÜNLÜK ROTASYON YENİLENMESİ: 02sa 06dk | TÜM GÖREVLER GÜNCEL (y = 110..146 -> centerY = +412.0f)
        GameObject subObj = CreatePanelChild("SubtitleBox", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 412.0f), new Vector2(960f, 36f));
        _subtitleText = CreateTMP("SubtitleText", subObj.transform, "", 21, new Color(0.68f, 0.78f, 0.88f, 1f), FontStyles.Normal, TextAlignmentOptions.Center);
        StretchChild(_subtitleText.rectTransform, 0f, 0f, 0f, 0f);

        // 3. 3 Üst Sekme / Aksiyon Butonu (1920x1080 exact: y = 160..215 -> centerY = +352.5f, height = 55)
        // [+] GÜNLÜK GÖREVLER (4): x = 258..698 -> centerX = -482.0f, width = 440
        _dailyTabBtn = Create1To1TabButton(
            "DailyTabBtn", transform,
            new Vector2(-482.0f, 352.5f), new Vector2(440f, 55f),
            "[+] GÜNLÜK GÖREVLER (4)", 22,
            out _dailyTabBg, out _dailyTabText,
            () => SwitchTab(OpsTab.DailyMissions)
        );

        // [#] PİLOT MADALYALARI (9): x = 736..1186 -> centerX = +1.0f, width = 450
        _medalsTabBtn = Create1To1TabButton(
            "MedalsTabBtn", transform,
            new Vector2(1.0f, 352.5f), new Vector2(450f, 55f),
            "[#] PİLOT MADALYALARI (9)", 22,
            out _medalsTabBg, out _medalsTabText,
            () => SwitchTab(OpsTab.PilotMedals)
        );

        // [—] BEKLEYEN ÖDÜL YOK: x = 1239..1662 -> centerX = +490.5f, width = 423
        _claimAllBtn = Create1To1TabButton(
            "ClaimAllBtn", transform,
            new Vector2(490.5f, 352.5f), new Vector2(423f, 55f),
            "[—] BEKLEYEN ÖDÜL YOK", 21,
            out _claimAllBg, out _claimAllText,
            OnClaimAllClicked
        );

        // 4. Ana Görev / Madalya Çerçevesi (1920x1080 exact: x = 268..1652, y = 236..943 -> center = (0.0, -49.5), size = (1384, 707))
        GameObject scrollFrame = new GameObject("OpsScrollFrame", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollFrame.transform.SetParent(transform, false);
        RectTransform scrollFrameRt = scrollFrame.GetComponent<RectTransform>();
        scrollFrameRt.anchorMin = new Vector2(0.5f, 0.5f);
        scrollFrameRt.anchorMax = new Vector2(0.5f, 0.5f);
        scrollFrameRt.pivot = new Vector2(0.5f, 0.5f);
        scrollFrameRt.anchoredPosition = new Vector2(0f, -49.5f);
        scrollFrameRt.sizeDelta = new Vector2(1384f, 707f);

        Image frameBg = scrollFrame.GetComponent<Image>();
        frameBg.color = new Color(0f, 0f, 0f, 0.01f);
        frameBg.raycastTarget = true;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollFrame.transform, false);
        RectTransform vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(14f, 8f);
        vpRt.offsetMax = new Vector2(-14f, -14f);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        _contentRect = content.GetComponent<RectTransform>();
        _contentRect.anchorMin = new Vector2(0f, 1f);
        _contentRect.anchorMax = new Vector2(1f, 1f);
        _contentRect.pivot = new Vector2(0.5f, 1f);
        _contentRect.anchoredPosition = Vector2.zero;
        _contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 11f;
        vlg.padding = new RectOffset(0, 0, 0, 4);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect = scrollFrame.GetComponent<ScrollRect>();
        _scrollRect.content = _contentRect;
        _scrollRect.viewport = vpRt;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 36f;

        // 5. Alt Ödül Bildirim Satırı
        GameObject toastObj = new GameObject("StatusToast", typeof(RectTransform));
        toastObj.transform.SetParent(transform, false);
        RectTransform toastRt = toastObj.GetComponent<RectTransform>();
        toastRt.anchorMin = new Vector2(0.5f, 0.5f);
        toastRt.anchorMax = new Vector2(0.5f, 0.5f);
        toastRt.pivot = new Vector2(0.5f, 0.5f);
        toastRt.anchoredPosition = new Vector2(0f, -458.0f);
        toastRt.sizeDelta = new Vector2(880f, 44f);

        _statusToastText = toastObj.AddComponent<TextMeshProUGUI>();
        _statusToastText.font = MainMenuManager.GetTacticalFont();
        _statusToastText.fontSize = 22;
        _statusToastText.fontStyle = FontStyles.Bold;
        _statusToastText.alignment = TextAlignmentOptions.Center;
        _statusToastText.color = ColEmerald;
        _statusToastText.raycastTarget = false;
        _statusToastText.text = "";

        // 6. Sol Alt << GERİ DÖN Tuşu (1920x1080 exact: x = 54..294, y = 962..1034 -> center = (-786.0, -458.0), size = (240, 72))
        HangarUI.FormatPixelExactButton(
            "BackFromOpsBtn",
            transform,
            new Vector2(-786.0f, -458.0f),
            new Vector2(240.0f, 72.0f),
            new Color(0.25f, 0.65f, 1.0f, 0.18f),
            new Color(0.20f, 0.55f, 0.95f, 0.35f),
            CloseOpsPanel);

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

    public void RefreshAll()
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
                ? $"  |  <color=#FACC15>{totalClaimable} ÖDÜL TOPLANMAYA HAZIR</color>"
                : "  |  TÜM GÖREVLER GÜNCEL";
            _subtitleText.text = $"GÜNLÜK ROTASYON YENİLENMESİ: <color=#FFFFFF>{resetIn}</color>{readyTag}";
        }

        bool dailySelected = _activeTab == OpsTab.DailyMissions;
        Style1To1TabButton(
            _dailyTabBg, _dailyTabText,
            selected: dailySelected,
            badgeCount: dailyClaimable,
            baseLabel: "[+] GÜNLÜK GÖREVLER (4)"
        );
        Style1To1TabButton(
            _medalsTabBg, _medalsTabText,
            selected: !dailySelected,
            badgeCount: medalClaimable,
            baseLabel: "[#] PİLOT MADALYALARI (9)"
        );

        if (_claimAllBtn != null && _claimAllBg != null && _claimAllText != null)
        {
            bool anyClaimable = totalClaimable > 0;
            _claimAllBtn.interactable = anyClaimable;
            Sprite tabActiveSpr = LoadOpsSprite("ops_tab_active.png", "ops_tab_active", ref _cachedTabActiveSprite);
            Sprite tabInactiveSpr = LoadOpsSprite("ops_tab_inactive.png", "ops_tab_inactive", ref _cachedTabInactiveSprite);
            _claimAllBg.sprite = anyClaimable ? tabActiveSpr : tabInactiveSpr;
            _claimAllBg.color = Color.white;
            _claimAllText.color = anyClaimable ? new Color(1f, 0.84f, 0.25f) : new Color(0.62f, 0.70f, 0.78f, 1f);
            _claimAllText.text = anyClaimable
                ? $"[»] TÜM ÖDÜLLERİ AL ({totalClaimable})"
                : "[—] BEKLEYEN ÖDÜL YOK";
        }
    }

    private static void Style1To1TabButton(Image bg, TextMeshProUGUI label, bool selected, int badgeCount, string baseLabel)
    {
        if (bg == null || label == null) return;
        Sprite activeSpr = LoadOpsSprite("ops_tab_active.png", "ops_tab_active", ref _cachedTabActiveSprite);
        Sprite inactiveSpr = LoadOpsSprite("ops_tab_inactive.png", "ops_tab_inactive", ref _cachedTabInactiveSprite);
        bg.sprite = selected ? activeSpr : inactiveSpr;
        bg.color = Color.white;
        label.color = selected ? Color.white : (badgeCount > 0 ? new Color(1f, 0.84f, 0.25f) : new Color(0.65f, 0.73f, 0.82f, 1f));
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

                bool isDoneOrClaimable = m.claimed || m.CanClaim;
                Color cardAccent = m.claimed ? ColEmerald : (m.CanClaim ? new Color(1f, 0.84f, 0.25f) : ColTeal);

                CardViewRefs refs = CreateOpsCardView($"MissionCard_{i}", 162f, isDoneOrClaimable);
                refs.tagText.text = $"{m.template.tag}  •  GÖREV {i + 1}/4";
                refs.tagText.color = cardAccent;
                refs.titleText.text = m.template.title;
                refs.descText.text = m.template.description;

                SetProgressBar(
                    refs.progressFill, refs.progressText,
                    m.ProgressRatio,
                    m.claimed ? "TAMAMLANDI — ÖDÜL ALINDI" : $"{m.ClampedProgress} / {m.template.targetCount}",
                    m.claimed ? new Color(0.24f, 0.88f, 0.52f, 1f) : new Color(0.18f, 0.86f, 0.96f, 1f)
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
                bool isDoneOrClaimable = medal.IsMaxed || medal.CanClaim;
                Color cardAccent = medal.IsMaxed ? ColEmerald : (medal.CanClaim ? new Color(1f, 0.84f, 0.25f) : ColTeal);

                CardViewRefs refs = CreateOpsCardView($"MedalCard_{medal.def.id}", 162f, isDoneOrClaimable);

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
                    medal.IsMaxed ? new Color(0.24f, 0.88f, 0.52f, 1f) : new Color(0.18f, 0.86f, 0.96f, 1f)
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
                    doneLabel: "ALINDI",
                    progressLabel: $"%{Mathf.RoundToInt(medal.ProgressRatio * 100f)}",
                    onClick: () => OnClaimMedalClicked(capturedId, capturedTitle)
                );
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
        Canvas.ForceUpdateCanvases();
    }

    private CardViewRefs CreateOpsCardView(string name, float height, bool useEmeraldFrame)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(_contentRect, false);
        RectTransform rt = card.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, height);

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        Image cardBg = card.GetComponent<Image>();
        Sprite cyanSpr = LoadOpsSprite("ops_card_cyan.png", "ops_card_cyan", ref _cachedCardCyanSprite);
        Sprite emeraldSpr = LoadOpsSprite("ops_card_emerald.png", "ops_card_emerald", ref _cachedCardEmeraldSprite);
        cardBg.sprite = useEmeraldFrame ? emeraldSpr : cyanSpr;
        cardBg.type = Image.Type.Simple;
        cardBg.preserveAspect = false;
        cardBg.color = Color.white;
        cardBg.raycastTarget = false;

        _spawnedCards.Add(card);

        // Sol Taraf: Etiket, Başlık, Açıklama, İlerleme Çubuğu, Ödül Metni (1:1 referans koordinatları)
        TextMeshProUGUI tagTmp = CreateTMP("Tag", card.transform, "", 17, ColTeal, FontStyles.Normal, TextAlignmentOptions.Left);
        SetTopLeftRect(tagTmp.rectTransform, 22f, -7f, 1000f, 24f);

        TextMeshProUGUI titleTmp = CreateTMP("Title", card.transform, "", 27, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        SetTopLeftRect(titleTmp.rectTransform, 22f, -31f, 1000f, 34f);

        TextMeshProUGUI descTmp = CreateTMP("Desc", card.transform, "", 19, new Color(0.82f, 0.88f, 0.94f, 1f), FontStyles.Normal, TextAlignmentOptions.Left);
        SetTopLeftRect(descTmp.rectTransform, 22f, -66f, 1000f, 26f);

        // İlerleme Çubuğu (1920x1080 exact: x_rel = 22..708 -> width = 686, y_rel = -96, height = 25)
        GameObject trackObj = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image));
        trackObj.transform.SetParent(card.transform, false);
        RectTransform trackRt = trackObj.GetComponent<RectTransform>();
        SetTopLeftRect(trackRt, 22f, -96f, 686f, 25f);
        Image trackImg = trackObj.GetComponent<Image>();
        trackImg.color = ColTrack;
        trackImg.raycastTarget = false;

        GameObject fillObj = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(trackObj.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.color = ColTeal;
        fillImg.raycastTarget = false;

        TextMeshProUGUI progTmp = CreateTMP("ProgressLabel", trackObj.transform, "", 16, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        MainMenuManager.MakeFullScreenRect(progTmp.rectTransform);

        // Ödül Metni
        TextMeshProUGUI rewardTmp = CreateTMP("RewardText", card.transform, "", 18, ColGold, FontStyles.Bold, TextAlignmentOptions.Left);
        SetTopLeftRect(rewardTmp.rectTransform, 22f, -126f, 1020f, 26f);

        // Sağ Taraf: Ödül Toplama / Yüzde Butonu (width = 264, height = 58, rightOffset = -20)
        GameObject btnObj = new GameObject("ActionBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(card.transform, false);
        SetAnchoredBox(btnObj.GetComponent<RectTransform>(), 1f, 1f, 0.5f, 0.5f, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(264f, 58f));

        Image actionBg = btnObj.GetComponent<Image>();
        Button actionBtn = btnObj.GetComponent<Button>();
        actionBtn.targetGraphic = actionBg;

        TextMeshProUGUI actionTmp = CreateTMP("Label", btnObj.transform, "", 22, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        StretchChild(actionTmp.rectTransform, 8f, -8f, 2f, -2f);

        return new CardViewRefs
        {
            root = card,
            cardBg = cardBg,
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
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
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
        Sprite slateSpr = LoadOpsSprite("ops_btn_slate.png", "ops_btn_slate", ref _cachedBtnSlateSprite);
        Sprite emeraldSpr = LoadOpsSprite("ops_btn_emerald.png", "ops_btn_emerald", ref _cachedBtnEmeraldSprite);

        if (canClaim)
        {
            refs.actionButton.interactable = true;
            refs.actionBtnBg.sprite = emeraldSpr;
            refs.actionBtnBg.color = Color.white;
            refs.actionBtnText.color = new Color(1f, 0.88f, 0.28f, 1f);
            refs.actionBtnText.text = claimLabel;
            if (onClick != null) refs.actionButton.onClick.AddListener(onClick);
        }
        else if (isCompletedAndClaimed)
        {
            refs.actionButton.interactable = false;
            refs.actionBtnBg.sprite = emeraldSpr;
            refs.actionBtnBg.color = Color.white;
            refs.actionBtnText.color = new Color(0.28f, 0.92f, 0.56f, 1f);
            refs.actionBtnText.text = doneLabel;
        }
        else
        {
            refs.actionButton.interactable = false;
            refs.actionBtnBg.sprite = slateSpr;
            refs.actionBtnBg.color = Color.white;
            refs.actionBtnText.color = new Color(0.86f, 0.91f, 0.96f, 1f);
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
        tmp.characterSpacing = 0.8f;
        tmp.color = color;
        tmp.alignment = align;
        tmp.text = text;
        tmp.raycastTarget = false;
        tmp.extraPadding = true;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static Button Create1To1TabButton(
        string name, Transform parent,
        Vector2 centerPos1080, Vector2 size1080,
        string label, int fontSize,
        out Image bgImg, out TextMeshProUGUI labelTmp,
        UnityAction onClick)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        SetAnchoredBox(obj.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, new Vector2(0.5f, 0.5f), centerPos1080, size1080);

        bgImg = obj.GetComponent<Image>();
        bgImg.sprite = LoadOpsSprite("ops_tab_inactive.png", "ops_tab_inactive", ref _cachedTabInactiveSprite);
        bgImg.type = Image.Type.Simple;
        bgImg.preserveAspect = false;
        bgImg.color = Color.white;
        bgImg.raycastTarget = true;

        Button btn = obj.GetComponent<Button>();
        btn.targetGraphic = bgImg;
        if (onClick != null) btn.onClick.AddListener(onClick);

        labelTmp = CreateTMP("Label", obj.transform, label, fontSize, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        StretchChild(labelTmp.rectTransform, 10f, -10f, 2f, -2f);

        return btn;
    }
}
