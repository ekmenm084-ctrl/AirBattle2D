using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Askeri Hangar, Atölye, Destek Droneları ve Askeri Envanter Arayüzü:
/// Tab 1: UÇAK FİLOSU (F-16, A-10, F-22 Görev Seçimi & Kilit Açma)
/// Tab 2: ATÖLYE GELİŞTİRMELERİ (Zırh, Hasar, Hız, Kritik Kalıcı Yükseltmeler)
/// Tab 3: DESTEK DRONELARI (Kanat İHA'ları, Kuşanma & Seviye Artışı)
/// Tab 4: ASKERİ ENVANTER (Sandıklardan Çıkan Kalıcı As Çipleri, Modüller & Muharebe Gücü)
/// </summary>
public class HangarUI : MonoBehaviour
{
    public static HangarUI Instance { get; private set; }

    private GameObject _panelRoot;
    private TextMeshProUGUI _goldTMP;

    // 3 Sekme (Uçak Filosu, Destek Droneları & Askeri Envanter)
    public enum HangarTab { Planes, Drones, Inventory }
    private HangarTab _currentTab = HangarTab.Planes;

    private Button _tabPlanesBtn, _tabDronesBtn, _tabInventoryBtn;
    private GameObject _planesContainer, _dronesContainer;

    // Tab 1: Uçak Filosu (1:1 Hangar Vitrini)
    private int _carouselStartIndex = 0;
    private HangarManager.AircraftType _currentShowcaseType = HangarManager.AircraftType.F16Viper;
    private GameObject _hudDynamicOverlay;
    private TextMeshProUGUI _hudPlaneNameTMP;
    private TextMeshProUGUI _hudPlaneRoleTMP;
    private TextMeshProUGUI _hudUniqueTraitTMP;
    private TextMeshProUGUI _hudTierTMP;
    private Image _hudSpeedFill, _hudManeuverFill, _hudArmorFill, _hudFirepowerFill;
    private TextMeshProUGUI _hudSpeedValTMP, _hudManeuverValTMP, _hudArmorValTMP, _hudFirepowerValTMP;
    private Button _hudActionBtn;
    private TextMeshProUGUI _hudActionBtnTMP;
    private GameObject _selectionBorderObj;

    private Button[] _carouselSlotButtons;
    private GameObject[] _carouselSlotOverlays;
    private Image[] _carouselSlotImages;
    private TextMeshProUGUI[] _carouselSlotTitles;
    private TextMeshProUGUI[] _carouselSlotTiers;
    private GameObject[] _carouselSlotFooters;
    private TextMeshProUGUI[] _carouselSlotFooterTMPs;
    private GameObject[] _carouselSlotLockBadges;
    private Image[] _carouselSlotLockBadgeBgs;
    private TextMeshProUGUI[] _carouselSlotLockBadgeTMPs;

    private readonly HangarManager.AircraftType[] _allAircraftList = new HangarManager.AircraftType[]
    {
        HangarManager.AircraftType.F16Viper,
        HangarManager.AircraftType.A10TankBuster,
        HangarManager.AircraftType.F22Raptor,
        HangarManager.AircraftType.Su57Felon,
        HangarManager.AircraftType.F35Lightning,
        HangarManager.AircraftType.KaanApex
    };

    private static readonly System.Collections.Generic.Dictionary<HangarManager.AircraftType, Sprite> _hangarFullCardSprites
        = new System.Collections.Generic.Dictionary<HangarManager.AircraftType, Sprite>();

    private static readonly System.Collections.Generic.Dictionary<HangarManager.AircraftType, Sprite> _hangar3DCardSprites
        = new System.Collections.Generic.Dictionary<HangarManager.AircraftType, Sprite>();

    /// <summary>
    /// 6 uçağın tamamı (F-16, A-10, F-22, Su-57, F-35, KAAN TF-X) için birebir aynı 3D açılı gerçek uçak
    /// modellerine ve kart çerçevesine sahip tam kart görselini (326x204) döner.
    /// </summary>
    public static Sprite GetHangarFullCardSprite(HangarManager.AircraftType type)
    {
        if (_hangarFullCardSprites.TryGetValue(type, out Sprite cached) && cached != null)
            return cached;

        int idx = (int)type;
        string[] paths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, $"Sprites/hangar_card_{idx}.png"),
            System.IO.Path.Combine(Application.dataPath, $"Resources/hangar_card_{idx}.png")
        };

        Sprite cardSpr = null;
        foreach (var p in paths)
        {
            if (System.IO.File.Exists(p))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(p);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (tex.LoadImage(bytes))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    cardSpr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    break;
                }
            }
        }

        if (cardSpr == null)
        {
            cardSpr = Resources.Load<Sprite>($"hangar_card_{idx}");
        }

        if (cardSpr != null)
        {
            _hangarFullCardSprites[type] = cardSpr;
        }
        return cardSpr;
    }

    /// <summary>
    /// 6 uçağın tamamının 3D açılı gerçek uçak modelini kart görselinden keserek
    /// Teknik Özellikler penceresinde de birebir aynı 3D modelin gösterilmesini sağlar.
    /// </summary>
    public static Sprite GetHangar3DCardSprite(HangarManager.AircraftType type)
    {
        if (_hangar3DCardSprites.TryGetValue(type, out Sprite cached) && cached != null)
            return cached;

        Sprite fullCard = GetHangarFullCardSprite(type);
        if (fullCard != null && fullCard.texture != null)
        {
            Texture2D cTex = fullCard.texture;
            int cx = Mathf.Clamp(18, 0, cTex.width - 10);
            int cy = Mathf.Clamp(56, 0, cTex.height - 10);
            int cw = Mathf.Clamp(290, 10, cTex.width - cx);
            int ch = Mathf.Clamp(98, 10, cTex.height - cy);
            Sprite modelSpr = Sprite.Create(cTex, new Rect(cx, cy, cw, ch), new Vector2(0.5f, 0.5f), 100f);
            _hangar3DCardSprites[type] = modelSpr;
            return modelSpr;
        }

        return HangarManager.GetAircraftSprite(type);
    }

    // Tab 2: Destek Droneları (1:1 3D İHA/SİHA Hangar Vitrini & Karusel)
    private Image _panelBgImg;
    private int _droneCarouselStartIndex = 0;
    private DroneCompanion.DroneType _currentShowcaseDrone = DroneCompanion.DroneType.MK1_Scout;
    private TextMeshProUGUI _droneHudHeaderTMP;
    private Image _droneSpeedFill, _droneManeuverFill, _droneArmorFill, _droneFirepowerFill;
    private TextMeshProUGUI _droneSpeedValTMP, _droneManeuverValTMP, _droneArmorValTMP, _droneFirepowerValTMP;
    private Button _droneActionLeftBtn, _droneActionRightBtn;
    private TextMeshProUGUI _droneActionLeftTMP, _droneActionRightTMP;
    private GameObject _droneSelectionBorderObj;
    private Button[] _droneSlotButtons;
    private GameObject[] _droneSlotOverlays;
    private Image[] _droneSlotImages;
    private GameObject[] _droneSlotLockBadges;
    private Image[] _droneSlotLockBadgeBgs;
    private TextMeshProUGUI[] _droneSlotLockBadgeTMPs;
    private GameObject[] _droneSlotFooters;
    private TextMeshProUGUI[] _droneSlotFooterTMPs;

    private readonly DroneCompanion.DroneType[] _allDroneList = new DroneCompanion.DroneType[]
    {
        DroneCompanion.DroneType.MK1_Scout,
        DroneCompanion.DroneType.MK2_Interceptor,
        DroneCompanion.DroneType.MK3_Striker,
        DroneCompanion.DroneType.MK4_Missile,
        DroneCompanion.DroneType.MK5_TitanAegis
    };

    private static readonly System.Collections.Generic.Dictionary<DroneCompanion.DroneType, Sprite> _droneFullCardSprites
        = new System.Collections.Generic.Dictionary<DroneCompanion.DroneType, Sprite>();

    private static readonly System.Collections.Generic.Dictionary<DroneCompanion.DroneType, Sprite> _drone3DCardSprites
        = new System.Collections.Generic.Dictionary<DroneCompanion.DroneType, Sprite>();

    public static Sprite GetDroneFullCardSprite(DroneCompanion.DroneType type)
    {
        if (_droneFullCardSprites.TryGetValue(type, out Sprite cached) && cached != null)
            return cached;

        int idx = Mathf.Clamp((int)type - 1, 0, 4);
        string[] paths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, $"Sprites/drone_card_{idx}.png"),
            System.IO.Path.Combine(Application.dataPath, $"Resources/drone_card_{idx}.png")
        };

        Sprite cardSpr = null;
        foreach (var p in paths)
        {
            if (System.IO.File.Exists(p))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(p);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (tex.LoadImage(bytes))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    cardSpr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    break;
                }
            }
        }

        if (cardSpr == null)
        {
            cardSpr = Resources.Load<Sprite>($"drone_card_{idx}");
        }

        if (cardSpr != null)
        {
            _droneFullCardSprites[type] = cardSpr;
        }
        return cardSpr;
    }

    public static Sprite GetDrone3DCardSprite(DroneCompanion.DroneType type)
    {
        if (_drone3DCardSprites.TryGetValue(type, out Sprite cached) && cached != null)
            return cached;

        Sprite fullCard = GetDroneFullCardSprite(type);
        if (fullCard != null && fullCard.texture != null)
        {
            Texture2D cTex = fullCard.texture;
            int cx = Mathf.Clamp(16, 0, cTex.width - 10);
            int cy = Mathf.Clamp(56, 0, cTex.height - 10);
            int cw = Mathf.Clamp(236, 10, cTex.width - cx);
            int ch = Mathf.Clamp(96, 10, cTex.height - cy);
            Sprite modelSpr = Sprite.Create(cTex, new Rect(cx, cy, cw, ch), new Vector2(0.5f, 0.5f), 100f);
            _drone3DCardSprites[type] = modelSpr;
            return modelSpr;
        }
        return null;
    }

    private static Sprite _droneHangarBgSprite;
    public static Sprite GetDroneHangarBgSprite()
    {
        if (_droneHangarBgSprite != null) return _droneHangarBgSprite;

        string[] paths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, "Sprites/drone_hangar_background.jpg"),
            System.IO.Path.Combine(Application.dataPath, "Resources/drone_hangar_background.jpg")
        };
        foreach (var p in paths)
        {
            if (System.IO.File.Exists(p))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(p);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (tex.LoadImage(bytes))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    _droneHangarBgSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    break;
                }
            }
        }

        if (_droneHangarBgSprite == null)
        {
            _droneHangarBgSprite = Resources.Load<Sprite>("drone_hangar_background");
        }
        return _droneHangarBgSprite != null ? _droneHangarBgSprite : GetHangarBgSprite();
    }

    // Taktik Detay & Özellikler Modalı ("Az ve Öz" Taktik Kart)
    private GameObject _specsModalObj;
    private Image _specsModalBoxBg;
    private Image _specsVisorBg;
    private Outline _specsModalBoxOutline;
    private TextMeshProUGUI _specsTitleTMP;
    private TextMeshProUGUI _specsRoleTMP;
    private Image _specsShowcaseBg;
    private Image _specsImage;
    private TextMeshProUGUI _specsDetailsTMP;

    private static RectTransform GetRect(GameObject go)
    {
        if (go == null) return null;
        return go.GetComponent<RectTransform>();
    }

    private static Sprite _hangarBgSprite;
    public static Sprite GetHangarBgSprite()
    {
        if (_hangarBgSprite != null) return _hangarBgSprite;

        string[] paths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, "Sprites/hangar_background.jpg"),
            System.IO.Path.Combine(Application.dataPath, "Resources/hangar_background.jpg")
        };
        foreach (var p in paths)
        {
            if (System.IO.File.Exists(p))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(p);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (tex.LoadImage(bytes))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    _hangarBgSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    break;
                }
            }
        }

        if (_hangarBgSprite == null)
        {
            _hangarBgSprite = Resources.Load<Sprite>("hangar_background");
        }
        return _hangarBgSprite;
    }

    /// <summary>
    /// 1920x1080 koordinatlarını (merkez 0,0) ekran çözünürlüğü veya en-boy oranından (Free Aspect, 16:9, 16:10, 4:3)
    /// bağımsız olarak arka plan görseline %100 kilitleyen normalize (0..1) anchor koordinatlarına dönüştürür.
    /// </summary>
    public static void SetNormalizedRect1080p(RectTransform rt, Vector2 centerPos1080, Vector2 size1080)
    {
        if (rt == null) return;
        float minX = (960f + centerPos1080.x - size1080.x * 0.5f) / 1920f;
        float maxX = (960f + centerPos1080.x + size1080.x * 0.5f) / 1920f;
        float minY = (540f + centerPos1080.y - size1080.y * 0.5f) / 1080f;
        float maxY = (540f + centerPos1080.y + size1080.y * 0.5f) / 1080f;

        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    public static Button FormatPixelExactButton(
        string objName,
        Transform parent,
        Vector2 pos,
        Vector2 size,
        Color hoverColor,
        Color pressColor,
        UnityEngine.Events.UnityAction onClick)
    {
        if (parent == null) return null;
        Transform existing = parent.Find(objName);
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject(objName, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>() ?? btnObj.AddComponent<RectTransform>();
        SetNormalizedRect1080p(rt, pos, size);

        Image img = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
        img.sprite = null;
        img.color = Color.white;
        img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        Outline outline = btnObj.GetComponent<Outline>();
        if (outline != null) Destroy(outline);

        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(hoverColor.r, hoverColor.g, hoverColor.b, Mathf.Min(0.10f, hoverColor.a));
        cb.pressedColor = new Color(pressColor.r, pressColor.g, pressColor.b, Mathf.Min(0.16f, pressColor.a));
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        cb.disabledColor = new Color(1f, 1f, 1f, 0f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.06f;
        btn.colors = cb;

        for (int i = btnObj.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = btnObj.transform.GetChild(i);
            child.gameObject.SetActive(false);
        }

        if (onClick != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(onClick);
        }
        btnObj.transform.SetAsLastSibling();
        return btn;
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

    public static HangarUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("HangarUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("HangarUI", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        MakeFullScreenRect(GetRect(go));
        Instance = go.GetComponent<HangarUI>();
        if (Instance == null) Instance = go.AddComponent<HangarUI>();
        if (Instance._panelRoot == null) Instance.BuildUI();
        return Instance;
    }

    public void OpenHangar()
    {
        OpenHangarOnTab(HangarTab.Planes);
    }

    public void OpenHangarOnTab(HangarTab targetTab)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        MakeFullScreenRect(GetRect(gameObject));

        if (_panelRoot == null)
            BuildUI();

        MakeFullScreenRect(GetRect(_panelRoot));
        _panelRoot.SetActive(true);
        PlayerProfileUI.EnsureUI()?.HideMenuStatusBar();

        HangarManager hm = HangarManager.EnsureInstance();
        _currentShowcaseType = (hm != null) ? hm.activeAircraft : HangarManager.AircraftType.F16Viper;

        SwitchTab(targetTab);
        RefreshAllVisuals();
    }

    public void CloseHangar()
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
            CloseSpecsModal();
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
            if (gameObject != null)
                gameObject.SetActive(false);
        }
        catch { }
    }

    public void SwitchTab(HangarTab tab)
    {
        CloseSpecsModal();
        if (tab == HangarTab.Inventory)
        {
            OpenInventoryDirectly();
            return;
        }
        _currentTab = tab;

        if (_planesContainer != null) _planesContainer.SetActive(tab == HangarTab.Planes);
        if (_dronesContainer != null) _dronesContainer.SetActive(tab == HangarTab.Drones);
        if (_panelBgImg != null)
        {
            _panelBgImg.sprite = (tab == HangarTab.Drones) ? GetDroneHangarBgSprite() : GetHangarBgSprite();
        }

        UpdateTabButtonVisual(_tabPlanesBtn, "UÇAK FİLOSU", tab == HangarTab.Planes);
        UpdateTabButtonVisual(_tabDronesBtn, "DESTEK DRONELARI", tab == HangarTab.Drones);
        UpdateTabButtonVisual(_tabInventoryBtn, "ASKERİ ENVANTER", false);

        RefreshAllVisuals();
    }

    private void OpenInventoryDirectly()
    {
        CloseDirectly();
        InventoryUI.EnsureUI()?.OpenInventory(onBack: () => EnsureUI()?.OpenHangar());
    }

    private void UpdateTabButtonVisual(Button btn, string title, bool isActive)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = Color.white;
        }
    }

    public void RefreshAllVisuals()
    {
        HangarManager hm = HangarManager.EnsureInstance();
        hm.LoadHangarData();

        // 1. Üst Altın Kasası
        if (_goldTMP != null)
            _goldTMP.text = $"ALTIN: {hm.gold:N0} G";

        // 2. Tab 1: Uçaklar
        RefreshPlaneTabVisuals(hm);

        // 3. Tab 2: Destek Droneları
        RefreshDroneTabVisuals(hm);
    }

    private void RefreshDroneTabVisuals(HangarManager hm)
    {
        SelectShowcaseDrone(_currentShowcaseDrone);
    }
    private void RefreshPlaneTabVisuals(HangarManager hm)
    {
        SelectShowcaseAircraft(_currentShowcaseType);
    }

    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        _panelRoot = new GameObject("HangarPanelRoot", typeof(RectTransform));
        _panelRoot.transform.SetParent(transform, false);
        MakeFullScreenRect(GetRect(_panelRoot));

        _panelBgImg = _panelRoot.AddComponent<Image>();
        _panelBgImg.sprite = GetHangarBgSprite();
        _panelBgImg.color = Color.white;
        _panelBgImg.raycastTarget = false;

        // 1. ÜST PANEL & ALTIN ROZETİ & PROFİL
        BuildHeader();

        // 2. SEKME BUTONLARI (UÇAKLAR, DRONELAR & ENVANTER)
        BuildTabsHeader();

        // 3. TAB 1 CONTAINER (UÇAK FİLOSU VİTRİNİ)
        BuildPlanesContainer();

        // 4. TAB 2 CONTAINER (DESTEK DRONELARI)
        BuildDronesContainer();

        // 5. ALT NAVİGASYON (GERİ DÖN, MAĞAZA, ENVANTER, İKMAL SANDIKLARI)
        BuildBottomBar();

        _panelRoot.SetActive(false);
    }

    private void BuildHeader()
    {
        // 1. Üst Sağ Canlı Altın Göstergesi (Normalize anchor ile her ekran oranında altın ikonunun tam yanına oturur)
        GameObject goldBadgeObj = new GameObject("LiveGoldBadge", typeof(RectTransform));
        goldBadgeObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform gRt = GetRect(goldBadgeObj);
        SetNormalizedRect1080p(gRt, new Vector2(685.0f, 526.0f), new Vector2(215.0f, 30.0f));

        GameObject goldTxtObj = new GameObject("Text", typeof(RectTransform));
        goldTxtObj.transform.SetParent(goldBadgeObj.transform, false);
        MakeFullScreenRect(GetRect(goldTxtObj));

        _goldTMP = goldTxtObj.AddComponent<TextMeshProUGUI>();
        _goldTMP.font = MainMenuManager.GetTacticalFont();
        _goldTMP.text = "ALTIN: 0 G";
        _goldTMP.fontSize = 18f;
        _goldTMP.characterSpacing = 1.6f;
        _goldTMP.fontStyle = FontStyles.Bold;
        _goldTMP.alignment = TextAlignmentOptions.Left;
        _goldTMP.color = new Color(1f, 0.88f, 0.25f);
        _goldTMP.raycastTarget = false;

        // 2. Üst Sol Profil Kartı Tetikleyicisi
        FormatPixelExactButton(
            "ProfileBadgeTrigger",
            _panelRoot.transform,
            new Vector2(-565.6f, 513.7f),
            new Vector2(361.0f, 52.6f),
            new Color(0f, 0.85f, 1f, 0.10f),
            new Color(0f, 0.7f, 0.95f, 0.18f),
            () => PlayerProfileUI.EnsureUI()?.OpenDossierModal());
    }

    private void BuildTabsHeader()
    {
        // 1. UÇAK FİLOSU TAB BUTONU (Merkezde simetrik 368x64)
        _tabPlanesBtn = FormatPixelExactButton(
            "TabBtn_Planes",
            _panelRoot.transform,
            new Vector2(-200.0f, 436.0f),
            new Vector2(368.0f, 64.0f),
            new Color(0f, 0.85f, 1f, 0.18f),
            new Color(0f, 0.7f, 0.95f, 0.38f),
            () => SwitchTab(HangarTab.Planes));

        // 2. DESTEK DRONELARI TAB BUTONU (Merkezde simetrik 368x64 - Uçak Filosu ile birebir aynı boyutta)
        _tabDronesBtn = FormatPixelExactButton(
            "TabBtn_Drones",
            _panelRoot.transform,
            new Vector2(200.0f, 436.0f),
            new Vector2(368.0f, 64.0f),
            new Color(0f, 0.85f, 1f, 0.18f),
            new Color(0f, 0.7f, 0.95f, 0.38f),
            () => SwitchTab(HangarTab.Drones));
    }

    // ==================== TAB 1: UÇAK FİLOSU (1:1 VİTRİN & NAVİGASYON) ====================
    private void BuildPlanesContainer()
    {
        _planesContainer = new GameObject("PlanesContainer", typeof(RectTransform));
        _planesContainer.transform.SetParent(_panelRoot.transform, false);
        MakeFullScreenRect(GetRect(_planesContainer));

        // 1. Orta Sol ve Sağ Navigasyon Okları (Uçaklar arasında geçiş)
        FormatPixelExactButton(
            "MidArrowLeft",
            _planesContainer.transform,
            new Vector2(-743.5f, 58.0f),
            new Vector2(48.0f, 56.0f),
            new Color(0f, 0.85f, 1f, 0.25f),
            new Color(0f, 0.7f, 0.95f, 0.45f),
            () => CycleShowcaseAircraft(-1));

        FormatPixelExactButton(
            "MidArrowRight",
            _planesContainer.transform,
            new Vector2(743.5f, 58.0f),
            new Vector2(48.0f, 56.0f),
            new Color(0f, 0.85f, 1f, 0.25f),
            new Color(0f, 0.7f, 0.95f, 0.45f),
            () => CycleShowcaseAircraft(1));

        // 2. Alt Karusel Navigasyon Okları (Karusel listesini kaydırır ve uçak değiştirir)
        FormatPixelExactButton(
            "CarArrowLeft",
            _planesContainer.transform,
            new Vector2(-713.0f, -309.0f),
            new Vector2(48.0f, 180.0f),
            new Color(0f, 0.85f, 1f, 0.25f),
            new Color(0f, 0.7f, 0.95f, 0.45f),
            () => ScrollCarouselWindow(-1));

        FormatPixelExactButton(
            "CarArrowRight",
            _planesContainer.transform,
            new Vector2(711.5f, -309.0f),
            new Vector2(48.0f, 180.0f),
            new Color(0f, 0.85f, 1f, 0.25f),
            new Color(0f, 0.7f, 0.95f, 0.45f),
            () => ScrollCarouselWindow(1));

        // 3. Karusel 4 Kart Slotu
        Vector2[] slotPositions = new Vector2[]
        {
            new Vector2(-503.0f, -309.0f), // Slot 0
            new Vector2(-165.5f, -309.0f), // Slot 1
            new Vector2(169.5f, -309.0f),  // Slot 2
            new Vector2(503.5f, -309.0f)   // Slot 3
        };
        Vector2[] slotSizes = new Vector2[]
        {
            new Vector2(328.0f, 204.0f),
            new Vector2(326.0f, 204.0f),
            new Vector2(326.0f, 204.0f),
            new Vector2(326.0f, 204.0f)
        };

        _carouselSlotButtons = new Button[4];
        _carouselSlotOverlays = new GameObject[4];
        _carouselSlotImages = new Image[4];
        _carouselSlotTitles = new TextMeshProUGUI[4];
        _carouselSlotTiers = new TextMeshProUGUI[4];
        _carouselSlotFooters = new GameObject[4];
        _carouselSlotFooterTMPs = new TextMeshProUGUI[4];
        _carouselSlotLockBadges = new GameObject[4];
        _carouselSlotLockBadgeBgs = new Image[4];
        _carouselSlotLockBadgeTMPs = new TextMeshProUGUI[4];

        for (int i = 0; i < 4; i++)
        {
            int slotIdx = i;
            _carouselSlotButtons[i] = FormatPixelExactButton(
                $"CardSlot_{i}",
                _planesContainer.transform,
                slotPositions[i],
                slotSizes[i],
                new Color(0f, 0.85f, 1f, 0.08f),
                new Color(0f, 0.7f, 0.95f, 0.14f),
                () => OnCardSlotClicked(slotIdx));

            // A) Dinamik 3D tam kart görseli (Karusel sağa kaydırıldığında 3D açılı gerçek uçak kartını birebir çizer)
            GameObject overlay = new GameObject($"SlotOverlay_{i}", typeof(RectTransform));
            overlay.transform.SetParent(_planesContainer.transform, false);
            RectTransform oRt = GetRect(overlay);
            SetNormalizedRect1080p(oRt, slotPositions[i], slotSizes[i]);

            Image oBg = overlay.AddComponent<Image>();
            oBg.type = Image.Type.Simple;
            oBg.color = Color.white;
            oBg.raycastTarget = false;

            _carouselSlotOverlays[i] = overlay;
            _carouselSlotImages[i] = oBg;
            overlay.SetActive(false);

            // B) Her Kartın Sağ Üst Kilit / Açık Rozeti (Kart başlığına değmeyecek şekilde sağ üst köşeye hizalı)
            GameObject lockBadgeObj = new GameObject($"SlotLockBadge_{i}", typeof(RectTransform));
            lockBadgeObj.transform.SetParent(_planesContainer.transform, false);
            RectTransform lbRt = GetRect(lockBadgeObj);
            SetNormalizedRect1080p(
                lbRt,
                slotPositions[i] + new Vector2(slotSizes[i].x * 0.415f, slotSizes[i].y * 0.375f),
                new Vector2(38f, 21f));

            Image lbBg = lockBadgeObj.AddComponent<Image>();
            lbBg.color = new Color(0.06f, 0.12f, 0.20f, 0.90f);
            lbBg.raycastTarget = false;

            GameObject lbTxtObj = new GameObject("Text", typeof(RectTransform));
            lbTxtObj.transform.SetParent(lockBadgeObj.transform, false);
            MakeFullScreenRect(GetRect(lbTxtObj));
            TextMeshProUGUI lbTMP = lbTxtObj.AddComponent<TextMeshProUGUI>();
            lbTMP.font = MainMenuManager.GetTacticalFont();
            lbTMP.fontSize = 10f;
            lbTMP.fontStyle = FontStyles.Bold;
            lbTMP.alignment = TextAlignmentOptions.Center;
            lbTMP.color = Color.white;
            lbTMP.raycastTarget = false;

            _carouselSlotLockBadges[i] = lockBadgeObj;
            _carouselSlotLockBadgeBgs[i] = lbBg;
            _carouselSlotLockBadgeTMPs[i] = lbTMP;

            // C) Her Kartın Alt Kademe & Seviye Kilidi Metni (Şeffaf arka planla doğrudan kartın alt bölümünün içine oturur)
            GameObject footerObj = new GameObject($"SlotFooter_{i}", typeof(RectTransform));
            footerObj.transform.SetParent(_planesContainer.transform, false);
            RectTransform fRt = GetRect(footerObj);
            SetNormalizedRect1080p(
                fRt,
                slotPositions[i] + new Vector2(0f, -66.0f),
                new Vector2(slotSizes[i].x - 24f, 52f));

            GameObject fTxtObj = new GameObject("FooterText", typeof(RectTransform));
            fTxtObj.transform.SetParent(footerObj.transform, false);
            MakeFullScreenRect(GetRect(fTxtObj));

            TextMeshProUGUI fTMP = fTxtObj.AddComponent<TextMeshProUGUI>();
            fTMP.font = MainMenuManager.GetTacticalFont();
            fTMP.fontSize = 13.5f;
            fTMP.fontStyle = FontStyles.Bold;
            fTMP.characterSpacing = 1.1f;
            fTMP.lineSpacing = 1.0f;
            fTMP.alignment = TextAlignmentOptions.Center;
            fTMP.color = Color.white;
            fTMP.raycastTarget = false;

            _carouselSlotFooters[i] = footerObj;
            _carouselSlotFooterTMPs[i] = fTMP;
            _carouselSlotTiers[i] = fTMP;
        }

        // 4. Seçili Kart Vurgu Çerçevesi (İçi %100 ŞEFFAF çerçeve — uçağın üzerine tıkladığında ASLA gölge/karartı yapmaz!)
        _selectionBorderObj = new GameObject("SelectionBorder", typeof(RectTransform));
        _selectionBorderObj.transform.SetParent(_planesContainer.transform, false);
        RectTransform bRt = GetRect(_selectionBorderObj);
        SetNormalizedRect1080p(bRt, slotPositions[0], slotSizes[0] + new Vector2(4f, 4f));

        Image bImg = _selectionBorderObj.AddComponent<Image>();
        bImg.sprite = MainMenuManager.GetHudSprite("hud_hangar_hollow_border_v3", new Color(0f, 0.95f, 1f, 1.0f), new Color(0f, 0f, 0f, 0f), false);
        bImg.type = Image.Type.Sliced;
        bImg.color = Color.white;
        bImg.raycastTarget = false;
        _selectionBorderObj.SetActive(true);

        // 5. Sol Dinamik HUD Paneli (Normalize anchor ile sol HUD kartı çerçevesinin içine %100 kilitli)
        BuildLeftHudDynamicOverlay();
    }

    private void BuildLeftHudDynamicOverlay()
    {
        _hudDynamicOverlay = new GameObject("LeftHudDynamicOverlay", typeof(RectTransform));
        _hudDynamicOverlay.transform.SetParent(_planesContainer.transform, false);

        RectTransform hRt = GetRect(_hudDynamicOverlay);
        SetNormalizedRect1080p(hRt, new Vector2(-539.0f, 76.5f), new Vector2(348.0f, 395.0f));

        // Sol HUD kartının çerçevesi ve cam zemini zaten temizlenmiş arka planda mevcuttur; ikinci bir çerçeve bindirmeyiz!
        Image hudBg = _hudDynamicOverlay.AddComponent<Image>();
        hudBg.color = new Color(0f, 0f, 0f, 0f);
        hudBg.raycastTarget = false;

        // 1. Model Başlığı
        GameObject titleObj = new GameObject("PlaneTitle", typeof(RectTransform));
        titleObj.transform.SetParent(_hudDynamicOverlay.transform, false);
        RectTransform tRt = GetRect(titleObj);
        tRt.anchorMin = new Vector2(0.04f, 0.88f);
        tRt.anchorMax = new Vector2(0.96f, 0.97f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        _hudPlaneNameTMP = titleObj.AddComponent<TextMeshProUGUI>();
        _hudPlaneNameTMP.font = MainMenuManager.GetTacticalFont();
        _hudPlaneNameTMP.fontSize = 20f;
        _hudPlaneNameTMP.fontStyle = FontStyles.Bold;
        _hudPlaneNameTMP.characterSpacing = 1.8f;
        _hudPlaneNameTMP.alignment = TextAlignmentOptions.Center;
        _hudPlaneNameTMP.color = Color.white;
        _hudPlaneNameTMP.raycastTarget = false;

        // 2. Alt Rol Tanımı
        GameObject roleObj = new GameObject("PlaneRole", typeof(RectTransform));
        roleObj.transform.SetParent(_hudDynamicOverlay.transform, false);
        RectTransform rRt = GetRect(roleObj);
        rRt.anchorMin = new Vector2(0.04f, 0.81f);
        rRt.anchorMax = new Vector2(0.96f, 0.88f);
        rRt.offsetMin = Vector2.zero;
        rRt.offsetMax = Vector2.zero;

        _hudPlaneRoleTMP = roleObj.AddComponent<TextMeshProUGUI>();
        _hudPlaneRoleTMP.font = MainMenuManager.GetTacticalFont();
        _hudPlaneRoleTMP.fontSize = 12f;
        _hudPlaneRoleTMP.fontStyle = FontStyles.Bold;
        _hudPlaneRoleTMP.characterSpacing = 1.2f;
        _hudPlaneRoleTMP.alignment = TextAlignmentOptions.Center;
        _hudPlaneRoleTMP.color = new Color(0.65f, 0.80f, 0.96f);
        _hudPlaneRoleTMP.raycastTarget = false;

        // 3. Teknik Özellikler Butonu
        GameObject specsBtnObj = new GameObject("SpecsBtn", typeof(RectTransform));
        specsBtnObj.transform.SetParent(_hudDynamicOverlay.transform, false);
        RectTransform sRt = GetRect(specsBtnObj);
        sRt.anchorMin = new Vector2(0.08f, 0.68f);
        sRt.anchorMax = new Vector2(0.92f, 0.79f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        Image sImg = specsBtnObj.AddComponent<Image>();
        sImg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.88f, 1f), new Color(0.04f, 0.13f, 0.23f, 0.96f), true);
        sImg.type = Image.Type.Sliced;
        sImg.color = Color.white;

        Button sBtn = specsBtnObj.AddComponent<Button>();
        sBtn.targetGraphic = sImg;
        sBtn.onClick.AddListener(() => OpenAircraftSpecsModal(_currentShowcaseType));

        GameObject stObj = new GameObject("Text", typeof(RectTransform));
        stObj.transform.SetParent(specsBtnObj.transform, false);
        MakeFullScreenRect(GetRect(stObj));
        TextMeshProUGUI stTMP = stObj.AddComponent<TextMeshProUGUI>();
        stTMP.font = MainMenuManager.GetTacticalFont();
        stTMP.text = "[i] TEKNİK ÖZELLİKLER";
        stTMP.fontSize = 14.5f;
        stTMP.fontStyle = FontStyles.Bold;
        stTMP.characterSpacing = 1.4f;
        stTMP.alignment = TextAlignmentOptions.Center;
        stTMP.color = new Color(0.55f, 0.96f, 1f);
        stTMP.raycastTarget = false;

        // 4. İstatistik Barları (Hız, Manevra, Zırh, Ateş Gücü) — hem gerçek bar uzunluğu hem de sayısal değer!
        _hudSpeedFill = CreateStatBarRow(_hudDynamicOverlay.transform, "Hız", 0.59f, out _hudSpeedValTMP);
        _hudManeuverFill = CreateStatBarRow(_hudDynamicOverlay.transform, "Manevra", 0.51f, out _hudManeuverValTMP);
        _hudArmorFill = CreateStatBarRow(_hudDynamicOverlay.transform, "Zırh", 0.43f, out _hudArmorValTMP);
        _hudFirepowerFill = CreateStatBarRow(_hudDynamicOverlay.transform, "Ateş Gücü", 0.35f, out _hudFirepowerValTMP);

        // 5. Kendine Has Özel Yetenek Satırı
        GameObject traitObj = new GameObject("UniqueTraitText", typeof(RectTransform));
        traitObj.transform.SetParent(_hudDynamicOverlay.transform, false);
        RectTransform trRt = GetRect(traitObj);
        trRt.anchorMin = new Vector2(0.04f, 0.23f);
        trRt.anchorMax = new Vector2(0.96f, 0.31f);
        trRt.offsetMin = Vector2.zero;
        trRt.offsetMax = Vector2.zero;

        _hudUniqueTraitTMP = traitObj.AddComponent<TextMeshProUGUI>();
        _hudUniqueTraitTMP.font = MainMenuManager.GetTacticalFont();
        _hudUniqueTraitTMP.fontSize = 11.5f;
        _hudUniqueTraitTMP.fontStyle = FontStyles.Bold;
        _hudUniqueTraitTMP.characterSpacing = 0.8f;
        _hudUniqueTraitTMP.alignment = TextAlignmentOptions.Center;
        _hudUniqueTraitTMP.color = new Color(1f, 0.85f, 0.25f);
        _hudUniqueTraitTMP.raycastTarget = false;

        // 6. Kademe & Seviye Rozeti Metni
        GameObject tierObj = new GameObject("TierBadge", typeof(RectTransform));
        tierObj.transform.SetParent(_hudDynamicOverlay.transform, false);
        RectTransform tierRt = GetRect(tierObj);
        tierRt.anchorMin = new Vector2(0.04f, 0.15f);
        tierRt.anchorMax = new Vector2(0.96f, 0.23f);
        tierRt.offsetMin = Vector2.zero;
        tierRt.offsetMax = Vector2.zero;

        _hudTierTMP = tierObj.AddComponent<TextMeshProUGUI>();
        _hudTierTMP.font = MainMenuManager.GetTacticalFont();
        _hudTierTMP.fontSize = 13.5f;
        _hudTierTMP.fontStyle = FontStyles.Bold;
        _hudTierTMP.characterSpacing = 1.4f;
        _hudTierTMP.alignment = TextAlignmentOptions.Center;
        _hudTierTMP.color = new Color(0.85f, 0.90f, 0.95f);
        _hudTierTMP.raycastTarget = false;

        // 7. Göreve Seç / Seviye Kilidi / Kilidi Aç Butonu
        GameObject actObj = new GameObject("HudActionButton", typeof(RectTransform));
        actObj.transform.SetParent(_hudDynamicOverlay.transform, false);
        RectTransform actRt = GetRect(actObj);
        actRt.anchorMin = new Vector2(0.08f, 0.03f);
        actRt.anchorMax = new Vector2(0.92f, 0.14f);
        actRt.offsetMin = Vector2.zero;
        actRt.offsetMax = Vector2.zero;

        Image actBg = actObj.AddComponent<Image>();
        actBg.sprite = MainMenuManager.GetHudSprite("hud_btn_emerald", new Color(0.18f, 0.85f, 0.45f), new Color(0.04f, 0.14f, 0.08f, 0.95f), true);
        actBg.type = Image.Type.Sliced;
        actBg.color = Color.white;

        _hudActionBtn = actObj.AddComponent<Button>();
        _hudActionBtn.targetGraphic = actBg;
        _hudActionBtn.onClick.AddListener(OnHudActionButtonClicked);

        GameObject atObj = new GameObject("Text", typeof(RectTransform));
        atObj.transform.SetParent(actObj.transform, false);
        MakeFullScreenRect(GetRect(atObj));

        _hudActionBtnTMP = atObj.AddComponent<TextMeshProUGUI>();
        _hudActionBtnTMP.font = MainMenuManager.GetTacticalFont();
        _hudActionBtnTMP.fontSize = 14.5f;
        _hudActionBtnTMP.fontStyle = FontStyles.Bold;
        _hudActionBtnTMP.characterSpacing = 1.4f;
        _hudActionBtnTMP.alignment = TextAlignmentOptions.Center;
        _hudActionBtnTMP.color = Color.white;
        _hudActionBtnTMP.raycastTarget = false;

        _hudDynamicOverlay.SetActive(true);
    }

    private Image CreateStatBarRow(Transform parent, string labelText, float anchorY, out TextMeshProUGUI valTMP)
    {
        GameObject lblObj = new GameObject("Lbl_" + labelText, typeof(RectTransform));
        lblObj.transform.SetParent(parent, false);
        RectTransform lRt = GetRect(lblObj);
        lRt.anchorMin = new Vector2(0.06f, anchorY - 0.032f);
        lRt.anchorMax = new Vector2(0.32f, anchorY + 0.032f);
        lRt.offsetMin = Vector2.zero;
        lRt.offsetMax = Vector2.zero;

        TextMeshProUGUI lbl = lblObj.AddComponent<TextMeshProUGUI>();
        lbl.font = MainMenuManager.GetTacticalFont();
        lbl.text = labelText;
        lbl.fontSize = 13.5f;
        lbl.alignment = TextAlignmentOptions.Left;
        lbl.color = new Color(0.85f, 0.92f, 1f);
        lbl.raycastTarget = false;

        GameObject trackObj = new GameObject("Track_" + labelText, typeof(RectTransform));
        trackObj.transform.SetParent(parent, false);
        RectTransform tRt = GetRect(trackObj);
        tRt.anchorMin = new Vector2(0.33f, anchorY - 0.011f);
        tRt.anchorMax = new Vector2(0.74f, anchorY + 0.011f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        Image trackImg = trackObj.AddComponent<Image>();
        trackImg.color = new Color(0.10f, 0.17f, 0.26f, 0.95f);
        trackImg.raycastTarget = false;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(trackObj.transform, false);
        RectTransform fRt = GetRect(fillObj);
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = new Vector2(0.25f, 1f);
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;

        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.20f, 0.90f, 1f, 1f);
        fillImg.raycastTarget = false;

        // Sağ tarafta sayısal değer göstergesi (Örn: 6.5M, 100 HP, 18 DMG)
        GameObject valObj = new GameObject("Val_" + labelText, typeof(RectTransform));
        valObj.transform.SetParent(parent, false);
        RectTransform vRt = GetRect(valObj);
        vRt.anchorMin = new Vector2(0.75f, anchorY - 0.032f);
        vRt.anchorMax = new Vector2(0.95f, anchorY + 0.032f);
        vRt.offsetMin = Vector2.zero;
        vRt.offsetMax = Vector2.zero;

        valTMP = valObj.AddComponent<TextMeshProUGUI>();
        valTMP.font = MainMenuManager.GetTacticalFont();
        valTMP.text = "";
        valTMP.fontSize = 12f;
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.alignment = TextAlignmentOptions.Right;
        valTMP.color = new Color(0.45f, 0.95f, 1f);
        valTMP.raycastTarget = false;

        return fillImg;
    }

    private static void SetBarFillAmount(Image fillImg, float amount)
    {
        if (fillImg == null) return;
        RectTransform rt = fillImg.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp(amount, 0.05f, 1f), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    private void ScrollCarouselWindow(int direction)
    {
        int maxStart = Mathf.Max(0, _allAircraftList.Length - 4);
        int newStart = Mathf.Clamp(_carouselStartIndex + direction, 0, maxStart);
        if (newStart != _carouselStartIndex)
        {
            _carouselStartIndex = newStart;
            int currentIdx = System.Array.IndexOf(_allAircraftList, _currentShowcaseType);
            if (currentIdx < _carouselStartIndex)
                _currentShowcaseType = _allAircraftList[_carouselStartIndex];
            else if (currentIdx >= _carouselStartIndex + 4)
                _currentShowcaseType = _allAircraftList[_carouselStartIndex + 3];

            SelectShowcaseAircraft(_currentShowcaseType);
        }
        else
        {
            CycleShowcaseAircraft(direction);
        }
    }

    private void CycleShowcaseAircraft(int direction)
    {
        int currentIndex = System.Array.IndexOf(_allAircraftList, _currentShowcaseType);
        if (currentIndex < 0) currentIndex = 0;
        int nextIndex = (currentIndex + direction + _allAircraftList.Length) % _allAircraftList.Length;

        if (nextIndex < _carouselStartIndex)
            _carouselStartIndex = nextIndex;
        else if (nextIndex >= _carouselStartIndex + 4)
            _carouselStartIndex = nextIndex - 3;

        SelectShowcaseAircraft(_allAircraftList[nextIndex]);
    }

    private void OnCardSlotClicked(int slotIndex)
    {
        int planeIdx = _carouselStartIndex + slotIndex;
        if (planeIdx >= 0 && planeIdx < _allAircraftList.Length)
        {
            HangarManager.AircraftType chosen = _allAircraftList[planeIdx];
            if (_currentShowcaseType == chosen)
            {
                OnHudActionButtonClicked();
            }
            else
            {
                SelectShowcaseAircraft(chosen);
            }
        }
    }

    public void SelectShowcaseAircraft(HangarManager.AircraftType type)
    {
        _currentShowcaseType = type;
        HangarManager hm = HangarManager.EnsureInstance();
        int playerLevel = PlayerProfileManager.EnsureInstance()?.level ?? 1;

        if (_hudDynamicOverlay != null)
        {
            _hudDynamicOverlay.SetActive(true);
            AircraftSpecsData data = GetAircraftSpecsData(type);
            HangarManager.AircraftCombatStats stats = HangarManager.GetAircraftBaseStats(type);

            if (_hudPlaneNameTMP != null) _hudPlaneNameTMP.text = data.displayName.ToUpper();
            if (_hudPlaneRoleTMP != null) _hudPlaneRoleTMP.text = stats.roleTag.ToUpper();
            if (_hudUniqueTraitTMP != null) _hudUniqueTraitTMP.text = $"» {stats.uniqueTraitTitle}";
            if (_hudTierTMP != null) _hudTierTMP.text = $"KADEME {stats.tier} (SEV. {stats.requiredLevel}+)";

            MilitaryStoreManager.LoadoutStats loadout = default;
            if (MilitaryStoreManager.EnsureInstance() != null)
            {
                loadout = MilitaryStoreManager.EnsureInstance().GetLoadoutStats(type);
            }

            float speedBarVal = Mathf.Clamp01(stats.speedBar + (loadout.bonusSpeed / 12.6f));
            float maneuverBonus = (loadout.dodgeCdReduction / 2.2f) + (loadout.bonusSpeed / 25f) + (loadout.ultiBonusPercent * 0.4f);
            float maneuverBarVal = Mathf.Clamp01(stats.maneuverBar + maneuverBonus);
            float armorBonusBar = ((loadout.bonusHp + loadout.bonusShield) / 350f) + (loadout.damageReductionPercent * 0.8f);
            float armorBarVal = Mathf.Clamp01(stats.armorBar + armorBonusBar);
            float baseSalvoDmg = stats.bulletDamage * stats.bulletsPerShot;
            float bonusSalvoDmg = loadout.bonusDamage * stats.bulletsPerShot;
            float totalSalvoDmg = baseSalvoDmg + bonusSalvoDmg;
            float fireBonusBar = (bonusSalvoDmg / 114f) + (loadout.bonusCrit / 100f);
            float firepowerBarVal = Mathf.Clamp01(stats.firepowerBar + fireBonusBar);

            SetBarFillAmount(_hudSpeedFill, speedBarVal);
            SetBarFillAmount(_hudManeuverFill, maneuverBarVal);
            SetBarFillAmount(_hudArmorFill, armorBarVal);
            SetBarFillAmount(_hudFirepowerFill, firepowerBarVal);

            float totalSpeed = stats.baseSpeed + loadout.bonusSpeed;
            float totalHpAndShield = stats.baseHp + loadout.bonusHp + loadout.bonusShield;
            if (_hudSpeedValTMP != null)
                _hudSpeedValTMP.text = loadout.bonusSpeed > 0.01f ? $"<color=#4ADE80>{totalSpeed:0.0}M</color>" : $"{stats.baseSpeed:0.0}M";
            if (_hudManeuverValTMP != null)
                _hudManeuverValTMP.text = maneuverBonus > 0.005f ? $"<color=#4ADE80>%{maneuverBarVal * 100f:0}</color>" : $"%{stats.maneuverBar * 100f:0}";
            if (_hudArmorValTMP != null)
                _hudArmorValTMP.text = (loadout.bonusHp + loadout.bonusShield) > 0.1f ? $"<color=#4ADE80>{totalHpAndShield:0}HP</color>" : $"{stats.baseHp:0}HP";
            if (_hudFirepowerValTMP != null)
                _hudFirepowerValTMP.text = (bonusSalvoDmg > 0.1f || loadout.bonusCrit > 0.1f) ? $"<color=#4ADE80>{totalSalvoDmg:0}DMG</color>" : $"{baseSalvoDmg:0}DMG";

            UpdateHudActionButtonVisual(hm, type, playerLevel);
        }

        UpdateCarouselVisuals(hm);
    }

    private void UpdateHudActionButtonVisual(HangarManager hm, HangarManager.AircraftType type, int playerLevel)
    {
        if (_hudActionBtn == null || _hudActionBtnTMP == null) return;
        Image bg = _hudActionBtn.GetComponent<Image>();
        bool isUnlocked = (hm != null && hm.IsAircraftUnlocked(type));
        bool isEquipped = (hm != null && hm.activeAircraft == type);
        int reqLvl = HangarManager.GetAircraftRequiredLevel(type);
        int unlockCost = HangarManager.GetAircraftUnlockCost(type);

        if (isEquipped)
        {
            _hudActionBtnTMP.text = "[AKTİF] GÖREVDE";
            _hudActionBtnTMP.color = Color.white;
            _hudActionBtn.interactable = true;
            if (bg != null) bg.sprite = MainMenuManager.GetHudSprite("hud_btn_emerald", new Color(0.18f, 0.85f, 0.45f), new Color(0.04f, 0.14f, 0.08f, 0.95f), true);
        }
        else if (isUnlocked)
        {
            _hudActionBtnTMP.text = "GÖREVE SEÇ";
            _hudActionBtnTMP.color = Color.white;
            _hudActionBtn.interactable = true;
            if (bg != null) bg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f), new Color(0.04f, 0.10f, 0.18f, 0.95f), true);
        }
        else if (playerLevel < reqLvl)
        {
            _hudActionBtnTMP.text = $"SEV. {reqLvl} KİLİTLİ ({unlockCost:N0}G)";
            _hudActionBtnTMP.color = new Color(0.95f, 0.55f, 0.55f);
            _hudActionBtn.interactable = false;
            if (bg != null) bg.sprite = MainMenuManager.GetHudSprite("hud_btn_slate", new Color(0.45f, 0.25f, 0.30f), new Color(0.10f, 0.05f, 0.07f, 0.95f), true);
        }
        else
        {
            _hudActionBtnTMP.text = $"KİLİDİ AÇ ({unlockCost:N0}G)";
            bool canAfford = (hm != null && hm.gold >= unlockCost);
            _hudActionBtnTMP.color = canAfford ? Color.yellow : new Color(0.95f, 0.55f, 0.55f);
            _hudActionBtn.interactable = canAfford;
            if (bg != null) bg.sprite = MainMenuManager.GetHudSprite(canAfford ? "hud_btn_amber" : "hud_btn_slate", canAfford ? new Color(1f, 0.82f, 0.2f) : new Color(0.4f, 0.35f, 0.35f), new Color(0.12f, 0.08f, 0.04f, 0.95f), true);
        }
    }

    private void OnHudActionButtonClicked()
    {
        HangarManager hm = HangarManager.EnsureInstance();
        if (hm == null) return;

        if (hm.IsAircraftUnlocked(_currentShowcaseType))
        {
            hm.SelectAircraft(_currentShowcaseType);
            RefreshAllVisuals();
        }
        else
        {
            int playerLevel = PlayerProfileManager.EnsureInstance()?.level ?? 1;
            int reqLvl = HangarManager.GetAircraftRequiredLevel(_currentShowcaseType);
            if (playerLevel >= reqLvl)
            {
                if (hm.TryUnlockAircraft(_currentShowcaseType))
                {
                    RefreshAllVisuals();
                }
            }
        }
    }

    private void UpdateCarouselVisuals(HangarManager hm)
    {
        Vector2[] slotPositions = new Vector2[]
        {
            new Vector2(-503.0f, -309.0f),
            new Vector2(-165.5f, -309.0f),
            new Vector2(169.5f, -309.0f),
            new Vector2(503.5f, -309.0f)
        };
        Vector2[] slotSizes = new Vector2[]
        {
            new Vector2(328.0f, 204.0f),
            new Vector2(326.0f, 204.0f),
            new Vector2(326.0f, 204.0f),
            new Vector2(326.0f, 204.0f)
        };

        int playerLevel = PlayerProfileManager.EnsureInstance()?.level ?? 1;
        bool hasOffset = (_carouselStartIndex > 0);

        for (int i = 0; i < 4; i++)
        {
            int pIdx = _carouselStartIndex + i;
            if (pIdx >= _allAircraftList.Length) continue;

            HangarManager.AircraftType t = _allAircraftList[pIdx];
            AircraftSpecsData d = GetAircraftSpecsData(t);
            HangarManager.AircraftCombatStats s = HangarManager.GetAircraftBaseStats(t);
            bool isUnlocked = (hm != null && hm.IsAircraftUnlocked(t));
            bool isActive = (hm != null && hm.activeAircraft == t);
            bool isLevelLocked = (playerLevel < s.requiredLevel);

            // 1. Tam kart örtüsü (Karusel sağa kaydırıldığında 6 uçağın tamamı birebir aynı 3D açılı kart modeliyle çizilir)
            if (_carouselSlotOverlays != null && _carouselSlotOverlays[i] != null)
            {
                _carouselSlotOverlays[i].SetActive(hasOffset);
                if (hasOffset && _carouselSlotImages[i] != null)
                {
                    _carouselSlotImages[i].sprite = GetHangarFullCardSprite(t);
                }
            }

            // 2. Sağ Üst Kilit / Açık Rozeti (Görseldeki gri kilit ikonunu gerçek durumla günceller)
            if (_carouselSlotLockBadges != null && _carouselSlotLockBadges[i] != null)
            {
                _carouselSlotLockBadges[i].SetActive(true);
                if (isUnlocked)
                {
                    _carouselSlotLockBadgeBgs[i].color = new Color(0.08f, 0.35f, 0.20f, 0.96f);
                    _carouselSlotLockBadgeTMPs[i].text = "<color=#4ADE80>AÇIK</color>";
                }
                else
                {
                    _carouselSlotLockBadgeBgs[i].color = new Color(0.28f, 0.10f, 0.12f, 0.96f);
                    _carouselSlotLockBadgeTMPs[i].text = $"<color=#FCA5A5>SV.{s.requiredLevel}</color>";
                }
            }

            // 3. Alt Durum ve Seviye Kilidi Plakası (Her kartın altında seviye kilidini ve kademesini net gösterir)
            if (_carouselSlotFooterTMPs != null && _carouselSlotFooterTMPs[i] != null)
            {
                if (isActive)
                {
                    _carouselSlotFooterTMPs[i].text = $"<color=#4ADE80>[ AKTİF ]</color>\n<color=#4ADE80>KADEME {s.tier} (SEV. {s.requiredLevel}+)</color>";
                }
                else if (isUnlocked)
                {
                    _carouselSlotFooterTMPs[i].text = $"<color=#00E5FF>[ GÖREVE HAZIR ]</color>\n<color=#94A3B8>KADEME {s.tier} (SEV. {s.requiredLevel}+)</color>";
                }
                else if (isLevelLocked)
                {
                    _carouselSlotFooterTMPs[i].text = $"<color=#F87171>SEV. {s.requiredLevel} KİLİDİ ({s.unlockCost:N0}G)</color>\n<color=#94A3B8>KADEME {s.tier} (SEV. {s.requiredLevel}+)</color>";
                }
                else
                {
                    _carouselSlotFooterTMPs[i].text = $"<color=#FACC15>KİLİDİ AÇ ({s.unlockCost:N0}G)</color>\n<color=#94A3B8>KADEME {s.tier} (SEV. {s.requiredLevel}+)</color>";
                }
            }
        }

        int selectedSlot = -1;
        for (int i = 0; i < 4; i++)
        {
            if (_carouselStartIndex + i < _allAircraftList.Length && _allAircraftList[_carouselStartIndex + i] == _currentShowcaseType)
            {
                selectedSlot = i;
                break;
            }
        }

        if (_selectionBorderObj != null)
        {
            if (selectedSlot >= 0)
            {
                _selectionBorderObj.SetActive(true);
                RectTransform bRt = GetRect(_selectionBorderObj);
                SetNormalizedRect1080p(bRt, slotPositions[selectedSlot], slotSizes[selectedSlot] + new Vector2(4f, 4f));
                _selectionBorderObj.transform.SetAsLastSibling();
            }
            else
            {
                _selectionBorderObj.SetActive(false);
            }
        }

        if (_hudDynamicOverlay != null)
        {
            _hudDynamicOverlay.transform.SetAsLastSibling();
        }
    }

    // ==================== TAB 2: DESTEK DRONELARI (1:1 3D İHA/SİHA HANGAR VİTRİNİ & KARUSEL) ====================
    private void BuildDronesContainer()
    {
        _dronesContainer = new GameObject("DronesContainer", typeof(RectTransform));
        _dronesContainer.transform.SetParent(_panelRoot.transform, false);
        MakeFullScreenRect(GetRect(_dronesContainer));

        // 1. Sol Alt "TEKNİK ÖZELLİKLER" Kutusu (Arka plan görselindeki x=108..552, y=747..951 çerçevesinin tam içi)
        GameObject droneHudBox = new GameObject("DroneSpecsHudBox", typeof(RectTransform));
        droneHudBox.transform.SetParent(_dronesContainer.transform, false);
        RectTransform hudRt = GetRect(droneHudBox);
        SetNormalizedRect1080p(hudRt, new Vector2(-630.0f, -309.0f), new Vector2(444.0f, 204.0f));

        // Başlık ("TEKNİK ÖZELLİKLER" - Tıklayınca 3D Drone Detay Modalını Açar)
        GameObject headerBtnObj = new GameObject("HeaderSpecsBtn", typeof(RectTransform));
        headerBtnObj.transform.SetParent(droneHudBox.transform, false);
        RectTransform hBtnRt = GetRect(headerBtnObj);
        hBtnRt.anchorMin = new Vector2(0.04f, 0.76f);
        hBtnRt.anchorMax = new Vector2(0.96f, 0.96f);
        hBtnRt.offsetMin = Vector2.zero;
        hBtnRt.offsetMax = Vector2.zero;

        Image hBtnBg = headerBtnObj.AddComponent<Image>();
        hBtnBg.color = new Color(1f, 1f, 1f, 0.01f);
        Button hBtn = headerBtnObj.AddComponent<Button>();
        hBtn.targetGraphic = hBtnBg;
        hBtn.onClick.AddListener(() => OpenDroneSpecsModal(_currentShowcaseDrone));

        _droneHudHeaderTMP = CreateButtonTMP(headerBtnObj, "TEKNİK ÖZELLİKLER");
        _droneHudHeaderTMP.fontSize = 18.5f;
        _droneHudHeaderTMP.characterSpacing = 1.4f;
        _droneHudHeaderTMP.color = new Color(0.92f, 0.96f, 1f);

        // Başlık Altı İnce Ayırıcı Çizgi
        GameObject divObj = new GameObject("HeaderDivider", typeof(RectTransform));
        divObj.transform.SetParent(droneHudBox.transform, false);
        RectTransform divRt = GetRect(divObj);
        divRt.anchorMin = new Vector2(0.05f, 0.735f);
        divRt.anchorMax = new Vector2(0.95f, 0.745f);
        divRt.offsetMin = Vector2.zero;
        divRt.offsetMax = Vector2.zero;
        Image divImg = divObj.AddComponent<Image>();
        divImg.color = new Color(0.25f, 0.55f, 0.78f, 0.65f);
        divImg.raycastTarget = false;

        // 4 Dinamik Özellik Çubuğu (Hız, Manevra, Zırh, Ateş Gücü - Görseldekiyle birebir aynı dizilim)
        _droneSpeedFill = CreateStatBarRow(droneHudBox.transform, "Hız", 0.62f, out _droneSpeedValTMP);
        _droneManeuverFill = CreateStatBarRow(droneHudBox.transform, "Manevra", 0.46f, out _droneManeuverValTMP);
        _droneArmorFill = CreateStatBarRow(droneHudBox.transform, "Zırh", 0.30f, out _droneArmorValTMP);
        _droneFirepowerFill = CreateStatBarRow(droneHudBox.transform, "Ateş Gücü", 0.14f, out _droneFirepowerValTMP);

        // Sol Alt Kutunun Hemen Üstünde Kompakt Kanat Kuşanma / Kilit Açma Barı
        GameObject actionRowObj = new GameObject("DroneActionRow", typeof(RectTransform));
        actionRowObj.transform.SetParent(_dronesContainer.transform, false);
        RectTransform actRowRt = GetRect(actionRowObj);
        SetNormalizedRect1080p(actRowRt, new Vector2(-630.0f, -186.0f), new Vector2(444.0f, 34.0f));

        // Sol Aksiyon Butonu (Kilit Aç / Sol Kanat Tak)
        GameObject leftActObj = new GameObject("DroneLeftActionBtn", typeof(RectTransform));
        leftActObj.transform.SetParent(actionRowObj.transform, false);
        RectTransform laRt = GetRect(leftActObj);
        laRt.anchorMin = new Vector2(0.0f, 0.0f);
        laRt.anchorMax = new Vector2(0.49f, 1.0f);
        laRt.offsetMin = Vector2.zero;
        laRt.offsetMax = Vector2.zero;
        Image laBg = leftActObj.AddComponent<Image>();
        laBg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f), new Color(0.04f, 0.10f, 0.18f, 0.94f), true);
        laBg.type = Image.Type.Sliced;
        _droneActionLeftBtn = leftActObj.AddComponent<Button>();
        _droneActionLeftBtn.targetGraphic = laBg;
        _droneActionLeftBtn.onClick.AddListener(OnDroneLeftActionClicked);
        _droneActionLeftTMP = CreateButtonTMP(leftActObj, "SOL KANAT TAK");
        _droneActionLeftTMP.fontSize = 13.5f;

        // Sağ Aksiyon Butonu (Teknik Detay / Sağ Kanat Tak)
        GameObject rightActObj = new GameObject("DroneRightActionBtn", typeof(RectTransform));
        rightActObj.transform.SetParent(actionRowObj.transform, false);
        RectTransform raRt = GetRect(rightActObj);
        raRt.anchorMin = new Vector2(0.51f, 0.0f);
        raRt.anchorMax = new Vector2(1.0f, 1.0f);
        raRt.offsetMin = Vector2.zero;
        raRt.offsetMax = Vector2.zero;
        Image raBg = rightActObj.AddComponent<Image>();
        raBg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f), new Color(0.04f, 0.10f, 0.18f, 0.94f), true);
        raBg.type = Image.Type.Sliced;
        _droneActionRightBtn = rightActObj.AddComponent<Button>();
        _droneActionRightBtn.targetGraphic = raBg;
        _droneActionRightBtn.onClick.AddListener(OnDroneRightActionClicked);
        _droneActionRightTMP = CreateButtonTMP(rightActObj, "SAĞ KANAT TAK");
        _droneActionRightTMP.fontSize = 13.5f;

        // 2. Sağ Alt Drone Karuseli Sol ve Sağ Ok Tuşları
        FormatPixelExactButton(
            "DroneCarArrowLeft",
            _dronesContainer.transform,
            new Vector2(-369.5f, -309.0f),
            new Vector2(44.0f, 180.0f),
            new Color(0f, 0.85f, 1f, 0.25f),
            new Color(0f, 0.7f, 0.95f, 0.45f),
            () => ScrollDroneCarouselWindow(-1));

        FormatPixelExactButton(
            "DroneCarArrowRight",
            _dronesContainer.transform,
            new Vector2(819.0f, -309.0f),
            new Vector2(44.0f, 180.0f),
            new Color(0f, 0.85f, 1f, 0.25f),
            new Color(0f, 0.7f, 0.95f, 0.45f),
            () => ScrollDroneCarouselWindow(1));

        // 3. Sağ Alt 4 Drone Kart Slotu (x0 = [624, 906, 1188, 1470], y0 = 747..951 -> size = 268x204, Uçak kartlarıyla aynı Y hizası)
        Vector2[] dSlotPositions = new Vector2[]
        {
            new Vector2(-202.0f, -309.0f),
            new Vector2(80.0f, -309.0f),
            new Vector2(362.0f, -309.0f),
            new Vector2(644.0f, -309.0f)
        };
        Vector2 dSlotSize = new Vector2(268.0f, 204.0f);

        _droneSlotButtons = new Button[4];
        _droneSlotOverlays = new GameObject[4];
        _droneSlotImages = new Image[4];
        _droneSlotLockBadges = new GameObject[4];
        _droneSlotLockBadgeBgs = new Image[4];
        _droneSlotLockBadgeTMPs = new TextMeshProUGUI[4];
        _droneSlotFooters = new GameObject[4];
        _droneSlotFooterTMPs = new TextMeshProUGUI[4];

        for (int i = 0; i < 4; i++)
        {
            int slotIdx = i;
            _droneSlotButtons[i] = FormatPixelExactButton(
                $"DroneCardSlot_{i}",
                _dronesContainer.transform,
                dSlotPositions[i],
                dSlotSize,
                new Color(0f, 0.85f, 1f, 0.08f),
                new Color(0f, 0.7f, 0.95f, 0.14f),
                () => OnDroneCardSlotClicked(slotIdx));

            // A) Dinamik 3D Tam Drone Kart Görseli (Karusel sağa kaydırıldığında devreye girer)
            GameObject overlay = new GameObject($"DroneSlotOverlay_{i}", typeof(RectTransform));
            overlay.transform.SetParent(_dronesContainer.transform, false);
            SetNormalizedRect1080p(GetRect(overlay), dSlotPositions[i], dSlotSize);

            Image oBg = overlay.AddComponent<Image>();
            oBg.type = Image.Type.Simple;
            oBg.color = Color.white;
            oBg.raycastTarget = false;

            _droneSlotOverlays[i] = overlay;
            _droneSlotImages[i] = oBg;
            overlay.SetActive(false);

            // B) Sağ Üst Kilit / Açık Rozeti
            GameObject lockBadgeObj = new GameObject($"DroneSlotLockBadge_{i}", typeof(RectTransform));
            lockBadgeObj.transform.SetParent(_dronesContainer.transform, false);
            SetNormalizedRect1080p(
                GetRect(lockBadgeObj),
                dSlotPositions[i] + new Vector2(dSlotSize.x * 0.405f, dSlotSize.y * 0.375f),
                new Vector2(38f, 21f));

            Image lbBg = lockBadgeObj.AddComponent<Image>();
            lbBg.color = new Color(0.06f, 0.12f, 0.20f, 0.90f);
            lbBg.raycastTarget = false;

            GameObject lbTxtObj = new GameObject("Text", typeof(RectTransform));
            lbTxtObj.transform.SetParent(lockBadgeObj.transform, false);
            MakeFullScreenRect(GetRect(lbTxtObj));
            TextMeshProUGUI lbTMP = lbTxtObj.AddComponent<TextMeshProUGUI>();
            lbTMP.font = MainMenuManager.GetTacticalFont();
            lbTMP.fontSize = 10f;
            lbTMP.fontStyle = FontStyles.Bold;
            lbTMP.alignment = TextAlignmentOptions.Center;
            lbTMP.color = Color.white;
            lbTMP.raycastTarget = false;

            _droneSlotLockBadges[i] = lockBadgeObj;
            _droneSlotLockBadgeBgs[i] = lbBg;
            _droneSlotLockBadgeTMPs[i] = lbTMP;

            // C) Alt Durum ve Seviye Kilidi Metni
            GameObject footerObj = new GameObject($"DroneSlotFooter_{i}", typeof(RectTransform));
            footerObj.transform.SetParent(_dronesContainer.transform, false);
            SetNormalizedRect1080p(
                GetRect(footerObj),
                dSlotPositions[i] + new Vector2(0f, -66.0f),
                new Vector2(dSlotSize.x - 16f, 52f));

            GameObject fTxtObj = new GameObject("FooterText", typeof(RectTransform));
            fTxtObj.transform.SetParent(footerObj.transform, false);
            MakeFullScreenRect(GetRect(fTxtObj));

            TextMeshProUGUI fTMP = fTxtObj.AddComponent<TextMeshProUGUI>();
            fTMP.font = MainMenuManager.GetTacticalFont();
            fTMP.fontSize = 12.5f;
            fTMP.fontStyle = FontStyles.Bold;
            fTMP.characterSpacing = 1.0f;
            fTMP.lineSpacing = 1.0f;
            fTMP.alignment = TextAlignmentOptions.Center;
            fTMP.color = Color.white;
            fTMP.raycastTarget = false;

            _droneSlotFooters[i] = footerObj;
            _droneSlotFooterTMPs[i] = fTMP;
        }

        // Seçili Drone Kartı Çerçevesi (İçi %100 şeffaf parlayan neon çerçeve)
        _droneSelectionBorderObj = new GameObject("SelectedDroneCardBorder", typeof(RectTransform));
        _droneSelectionBorderObj.transform.SetParent(_dronesContainer.transform, false);
        Image selImg = _droneSelectionBorderObj.AddComponent<Image>();
        selImg.sprite = MainMenuManager.GetHudSprite("hud_hangar_hollow_border_v3", new Color(0f, 0.95f, 1f, 0.95f), new Color(0f, 0f, 0f, 0f), false);
        selImg.type = Image.Type.Sliced;
        selImg.color = Color.white;
        selImg.raycastTarget = false;

        _dronesContainer.SetActive(false);
    }

    private void ScrollDroneCarouselWindow(int direction)
    {
        int maxStart = Mathf.Max(0, _allDroneList.Length - 4);
        int newStart = Mathf.Clamp(_droneCarouselStartIndex + direction, 0, maxStart);
        if (newStart != _droneCarouselStartIndex)
        {
            _droneCarouselStartIndex = newStart;
            int currentIdx = System.Array.IndexOf(_allDroneList, _currentShowcaseDrone);
            if (currentIdx < _droneCarouselStartIndex)
                _currentShowcaseDrone = _allDroneList[_droneCarouselStartIndex];
            else if (currentIdx >= _droneCarouselStartIndex + 4)
                _currentShowcaseDrone = _allDroneList[_droneCarouselStartIndex + 3];

            SelectShowcaseDrone(_currentShowcaseDrone);
        }
        else
        {
            CycleShowcaseDrone(direction);
        }
    }

    private void CycleShowcaseDrone(int direction)
    {
        int currentIndex = System.Array.IndexOf(_allDroneList, _currentShowcaseDrone);
        if (currentIndex < 0) currentIndex = 0;
        int nextIndex = (currentIndex + direction + _allDroneList.Length) % _allDroneList.Length;

        if (nextIndex < _droneCarouselStartIndex)
            _droneCarouselStartIndex = nextIndex;
        else if (nextIndex >= _droneCarouselStartIndex + 4)
            _droneCarouselStartIndex = nextIndex - 3;

        SelectShowcaseDrone(_allDroneList[nextIndex]);
    }

    private void OnDroneCardSlotClicked(int slotIndex)
    {
        int dIdx = _droneCarouselStartIndex + slotIndex;
        if (dIdx >= 0 && dIdx < _allDroneList.Length)
        {
            DroneCompanion.DroneType chosen = _allDroneList[dIdx];
            if (_currentShowcaseDrone == chosen)
            {
                OnDroneLeftActionClicked();
            }
            else
            {
                SelectShowcaseDrone(chosen);
            }
        }
    }

    private void OnDroneLeftActionClicked()
    {
        HangarManager hm = HangarManager.EnsureInstance();
        if (hm == null) return;

        bool isOwned = hm.IsDroneOwned(_currentShowcaseDrone);
        bool isUnlocked = hm.IsDroneUnlocked(_currentShowcaseDrone);
        float currentHp = hm.GetDroneHealth(_currentShowcaseDrone);
        int playerLevel = PlayerProfileManager.EnsureInstance()?.level ?? 1;
        int reqLvl = HangarManager.GetDroneRequiredLevel(_currentShowcaseDrone);

        if (playerLevel < reqLvl) return;

        if (isOwned && currentHp <= 0f)
        {
            if (hm.TryRepairDrone(_currentShowcaseDrone))
                RefreshAllVisuals();
        }
        else if (!isUnlocked)
        {
            if (hm.TryBuyDrone(_currentShowcaseDrone))
                RefreshAllVisuals();
        }
        else
        {
            hm.EquipDrone(_currentShowcaseDrone, isLeft: true);
            RefreshAllVisuals();
        }
    }

    private void OnDroneRightActionClicked()
    {
        HangarManager hm = HangarManager.EnsureInstance();
        if (hm == null) return;

        if (hm.IsDroneUnlocked(_currentShowcaseDrone))
        {
            hm.EquipDrone(_currentShowcaseDrone, isLeft: false);
            RefreshAllVisuals();
        }
        else
        {
            OpenDroneSpecsModal(_currentShowcaseDrone);
        }
    }

    public void SelectShowcaseDrone(DroneCompanion.DroneType type)
    {
        _currentShowcaseDrone = type;
        HangarManager hm = HangarManager.EnsureInstance();
        int playerLevel = PlayerProfileManager.EnsureInstance()?.level ?? 1;
        DroneSpecsData data = GetDroneSpecsData(type);

        if (_droneHudHeaderTMP != null)
            _droneHudHeaderTMP.text = $"TEKNİK ÖZELLİKLER  <size=80%><color=#00E5FF>[ℹ {data.displayName}]</color></size>";

        // 5 Drone Kademesine Göre Artan Bar Oranları (MK-1 %25 -> MK-2 %44 -> MK-3 %64 -> MK-4 %82 -> MK-5 %100)
        float speedBar = 0.25f, manBar = 0.25f, armorBar = 0.25f, fireBar = 0.25f;
        float dmgVal = 5f;
        switch (type)
        {
            case DroneCompanion.DroneType.MK1_Scout:
                speedBar = 0.25f; manBar = 0.28f; armorBar = 0.25f; fireBar = 0.22f; dmgVal = 5f; break;
            case DroneCompanion.DroneType.MK2_Interceptor:
                speedBar = 0.45f; manBar = 0.48f; armorBar = 0.44f; fireBar = 0.44f; dmgVal = 8f; break;
            case DroneCompanion.DroneType.MK3_Striker:
                speedBar = 0.64f; manBar = 0.62f; armorBar = 0.66f; fireBar = 0.65f; dmgVal = 14f; break;
            case DroneCompanion.DroneType.MK4_Missile:
                speedBar = 0.84f; manBar = 0.82f; armorBar = 0.80f; fireBar = 0.85f; dmgVal = 26f; break;
            case DroneCompanion.DroneType.MK5_TitanAegis:
                speedBar = 1.00f; manBar = 1.00f; armorBar = 1.00f; fireBar = 1.00f; dmgVal = 36f; break;
        }

        SetBarFillAmount(_droneSpeedFill, speedBar);
        SetBarFillAmount(_droneManeuverFill, manBar);
        SetBarFillAmount(_droneArmorFill, armorBar);
        SetBarFillAmount(_droneFirepowerFill, fireBar);

        float maxHp = HangarManager.GetDroneMaxHealth(type);
        if (_droneSpeedValTMP != null) _droneSpeedValTMP.text = $"%{speedBar * 100f:0}";
        if (_droneManeuverValTMP != null) _droneManeuverValTMP.text = $"%{manBar * 100f:0}";
        if (_droneArmorValTMP != null) _droneArmorValTMP.text = $"{maxHp:0}HP";
        if (_droneFirepowerValTMP != null) _droneFirepowerValTMP.text = $"{dmgVal:0}DMG";

        // Aksiyon Butonlarını Güncelle
        bool isOwned = (hm != null && hm.IsDroneOwned(type));
        bool isUnlocked = (hm != null && hm.IsDroneUnlocked(type));
        float currentHp = (hm != null) ? hm.GetDroneHealth(type) : maxHp;
        int reqLvl = HangarManager.GetDroneRequiredLevel(type);
        int cost = HangarManager.GetDroneCost(type);
        int repCost = HangarManager.GetDroneRepairCost(type);
        bool isLeft = (hm != null && hm.leftDroneType == type);
        bool isRight = (hm != null && hm.rightDroneType == type);

        if (_droneActionLeftBtn != null && _droneActionLeftTMP != null)
        {
            if (playerLevel < reqLvl)
            {
                _droneActionLeftTMP.text = $"SEV. {reqLvl} KİLİTLİ ({cost:N0}G)";
                _droneActionLeftTMP.color = new Color(0.95f, 0.55f, 0.55f);
                _droneActionLeftBtn.interactable = false;
            }
            else if (isOwned && currentHp <= 0f)
            {
                _droneActionLeftTMP.text = $"ONAR ({repCost:N0}G)";
                _droneActionLeftTMP.color = Color.yellow;
                _droneActionLeftBtn.interactable = (hm != null && hm.gold >= repCost);
            }
            else if (!isUnlocked)
            {
                _droneActionLeftTMP.text = $"KİLİDİ AÇ ({cost:N0}G)";
                _droneActionLeftTMP.color = Color.yellow;
                _droneActionLeftBtn.interactable = (hm != null && hm.gold >= cost);
            }
            else
            {
                _droneActionLeftTMP.text = isLeft ? "[AKTİF] SOL KANAT" : "SOL KANAT TAK";
                _droneActionLeftTMP.color = isLeft ? new Color(0.3f, 1f, 0.5f) : Color.white;
                _droneActionLeftBtn.interactable = true;
            }
        }

        if (_droneActionRightBtn != null && _droneActionRightTMP != null)
        {
            if (isUnlocked)
            {
                _droneActionRightTMP.text = isRight ? "[AKTİF] SAĞ KANAT" : "SAĞ KANAT TAK";
                _droneActionRightTMP.color = isRight ? new Color(0.3f, 1f, 0.5f) : Color.white;
                _droneActionRightBtn.interactable = true;
            }
            else
            {
                _droneActionRightTMP.text = "[ℹ] TEKNİK DETAY";
                _droneActionRightTMP.color = new Color(0f, 0.92f, 1f);
                _droneActionRightBtn.interactable = true;
            }
        }

        UpdateDroneCarouselVisuals(hm, playerLevel);
    }

    private void UpdateDroneCarouselVisuals(HangarManager hm, int playerLevel)
    {
        Vector2[] dSlotPositions = new Vector2[]
        {
            new Vector2(-202.0f, -309.0f),
            new Vector2(80.0f, -309.0f),
            new Vector2(362.0f, -309.0f),
            new Vector2(644.0f, -309.0f)
        };
        Vector2 dSlotSize = new Vector2(268.0f, 204.0f);
        bool hasOffset = (_droneCarouselStartIndex > 0);

        for (int i = 0; i < 4; i++)
        {
            int dIdx = _droneCarouselStartIndex + i;
            if (dIdx >= _allDroneList.Length) continue;

            DroneCompanion.DroneType t = _allDroneList[dIdx];
            bool isUnlocked = (hm != null && hm.IsDroneUnlocked(t));
            bool isLeft = (hm != null && hm.leftDroneType == t);
            bool isRight = (hm != null && hm.rightDroneType == t);
            bool isActive = (isLeft || isRight);
            int reqLvl = HangarManager.GetDroneRequiredLevel(t);
            int cost = HangarManager.GetDroneCost(t);
            int tier = (int)t;
            bool isLevelLocked = (playerLevel < reqLvl);

            if (_droneSlotOverlays != null && _droneSlotOverlays[i] != null)
            {
                _droneSlotOverlays[i].SetActive(hasOffset);
                if (hasOffset && _droneSlotImages[i] != null)
                {
                    _droneSlotImages[i].sprite = GetDroneFullCardSprite(t);
                }
            }

            if (_droneSlotLockBadges != null && _droneSlotLockBadges[i] != null)
            {
                _droneSlotLockBadges[i].SetActive(true);
                if (isUnlocked)
                {
                    _droneSlotLockBadgeBgs[i].color = new Color(0.08f, 0.35f, 0.20f, 0.96f);
                    _droneSlotLockBadgeTMPs[i].text = "<color=#4ADE80>AÇIK</color>";
                }
                else
                {
                    _droneSlotLockBadgeBgs[i].color = new Color(0.28f, 0.10f, 0.12f, 0.96f);
                    _droneSlotLockBadgeTMPs[i].text = $"<color=#FCA5A5>SV.{reqLvl}</color>";
                }
            }

            if (_droneSlotFooterTMPs != null && _droneSlotFooterTMPs[i] != null)
            {
                if (isActive)
                {
                    string wingTag = (isLeft && isRight) ? "[AKTİF: ÇİFT KANAT]" : (isLeft ? "[AKTİF: SOL KANAT]" : "[AKTİF: SAĞ KANAT]");
                    _droneSlotFooterTMPs[i].text = $"<color=#4ADE80>{wingTag}</color>\n<color=#4ADE80>KADEME {tier} (SEV. {reqLvl}+)</color>";
                }
                else if (isUnlocked)
                {
                    _droneSlotFooterTMPs[i].text = $"<color=#00E5FF>[ GÖREVE HAZIR ]</color>\n<color=#94A3B8>KADEME {tier} (SEV. {reqLvl}+)</color>";
                }
                else if (isLevelLocked)
                {
                    _droneSlotFooterTMPs[i].text = $"<color=#F87171>SEV. {reqLvl} KİLİDİ ({cost:N0}G)</color>\n<color=#94A3B8>KADEME {tier} (SEV. {reqLvl}+)</color>";
                }
                else
                {
                    _droneSlotFooterTMPs[i].text = $"<color=#FACC15>KİLİDİ AÇ ({cost:N0}G)</color>\n<color=#94A3B8>KADEME {tier} (SEV. {reqLvl}+)</color>";
                }
            }
        }

        int selectedSlot = -1;
        for (int i = 0; i < 4; i++)
        {
            if (_droneCarouselStartIndex + i < _allDroneList.Length && _allDroneList[_droneCarouselStartIndex + i] == _currentShowcaseDrone)
            {
                selectedSlot = i;
                break;
            }
        }

        if (_droneSelectionBorderObj != null)
        {
            if (selectedSlot >= 0)
            {
                _droneSelectionBorderObj.SetActive(true);
                SetNormalizedRect1080p(GetRect(_droneSelectionBorderObj), dSlotPositions[selectedSlot], dSlotSize + new Vector2(4f, 4f));
                _droneSelectionBorderObj.transform.SetAsLastSibling();
            }
            else
            {
                _droneSelectionBorderObj.SetActive(false);
            }
        }
    }

    private TextMeshProUGUI CreateButtonTMP(GameObject parent, string text)
    {
        GameObject tObj = new GameObject("Text", typeof(RectTransform));
        tObj.transform.SetParent(parent.transform, false);
        RectTransform rt = GetRect(tObj);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = 22f;
        tmp.characterSpacing = 1.8f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    // ==================== TAKTİK ÖZELLİKLER & ŞARTNAME MODAL SAYFASI ====================
    private struct AircraftSpecsData
    {
        public string displayName;
        public string roleTag;
        public string tierTag;
        public string armorText;
        public string speedText;
        public string weaponText;
        public string ammoText;
        public string perkText;
        public Color accentColor;
    }

    private static AircraftSpecsData GetAircraftSpecsData(HangarManager.AircraftType type)
    {
        switch (type)
        {
            case HangarManager.AircraftType.A10TankBuster:
                return new AircraftSpecsData
                {
                    displayName = "A-10 TANK BUSTER",
                    roleTag = "AĞIR ZIRHLI TAARRUZ JETİ",
                    tierTag = "KADEME 2 (SEVİYE 15+ • 1.500 ALTIN)",
                    armorText = "150 HP + %8 Kalıcı Hasar Azaltma (Titanyum Küvet Zırh)",
                    speedText = "7.6 Mach (Çift TF34 Ağır Taarruz İtkisi)",
                    weaponText = "30mm GAU-8 Avenger (32 DMG x 1 Namlu • 0.25sn)",
                    ammoText = "Ağır Uranyum Kor-Şarapnel & %10 Kritik Darbe",
                    perkText = "TİTANYUM KÜVET ZIRH: Alınan tüm hasarı %8 azaltır ve kor mermileri düşmanı sarsar.",
                    accentColor = new Color(0.25f, 0.65f, 0.40f)
                };
            case HangarManager.AircraftType.F22Raptor:
                return new AircraftSpecsData
                {
                    displayName = "F-22 RAPTOR",
                    roleTag = "5. NESİL SÜPERSONİK HAYALET AVCI",
                    tierTag = "KADEME 3 (SEVİYE 30+ • 5.000 ALTIN)",
                    armorText = "190 HP + %13 Hasar Azaltma + 25 HP Hayalet Kalkan",
                    speedText = "9.0 Mach (Süperseyir 2D Vektör İtki)",
                    weaponText = "Çift M61A2 Süpersonik Vulcan (24 x 2 = 48 DMG • 0.22sn)",
                    ammoText = "Mach Şok Dalgalı Buz Mavisi İz & %16 Kritik Darbe",
                    perkText = "SÜPERSEYİR & HAYALET KALKAN: +25 HP kalkan, çift namlu ateşi ve %25 daha hızlı kaçış.",
                    accentColor = new Color(0.60f, 0.35f, 0.75f)
                };
            case HangarManager.AircraftType.Su57Felon:
                return new AircraftSpecsData
                {
                    displayName = "SU-57 FELON",
                    roleTag = "5. NESİL SÜPER-MANEVRA & PLAZMA AVCISI",
                    tierTag = "KADEME 4 (SEVİYE 50+ • 15.000 ALTIN)",
                    armorText = "235 HP + %17 Hasar Azaltma + 45 HP Plazma Kalkan",
                    speedText = "10.3 Mach (3D Süper-Vektör İtki)",
                    weaponText = "Çift 30mm GSh-30-1 Plazma (33 x 2 = 66 DMG • 0.19sn)",
                    ammoText = "Yakut-Mor Plazma Kor & %22 Yüksek Kritik Darbe",
                    perkText = "3D VEKTÖR & KRİTİK PLAZMA: +45 HP Kalkan, çift plazma salvosu, %22 kritik ve +%25 hızlı Ulti.",
                    accentColor = new Color(0.15f, 0.75f, 0.85f)
                };
            case HangarManager.AircraftType.F35Lightning:
                return new AircraftSpecsData
                {
                    displayName = "F-35 LIGHTNING II",
                    roleTag = "5. NESİL ÇOK AMAÇLI 3'LÜ SALVO JETİ",
                    tierTag = "KADEME 5 (SEVİYE 75+ • 35.000 ALTIN)",
                    armorText = "285 HP + %21 Hasar Azaltma + 70 HP Başlangıç Kalkanı",
                    speedText = "11.4 Mach (Gelişmiş F135 İyon İtkisi)",
                    weaponText = "Üçlü 25mm GAU-22/A (29 x 3 = 87 DMG • 0.17sn)",
                    ammoText = "3'lü Siber Zümrüt & Altın İyon Salvosu (%28 Kritik)",
                    perkText = "360° DAS & ENERJİ KALKANI: Savaşa +70 HP kalkanla başlar, 3'lü yaylım ateşi ve %30 hızlı füze.",
                    accentColor = new Color(1.0f, 0.65f, 0.15f)
                };
            case HangarManager.AircraftType.KaanApex:
                return new AircraftSpecsData
                {
                    displayName = "KAAN TF-X",
                    roleTag = "5.+ NESİL MİLLİ HAVA HÂKİMİYETİ ZİRVESİ",
                    tierTag = "KADEME 6 (SEVİYE 100+ • 75.000 ALTIN)",
                    armorText = "350 HP + %26 Hasar Azaltma + 110 HP Kuantum Kalkan",
                    speedText = "12.6 Mach (Milli Adaptif Zirve Süpersonik İtki)",
                    weaponText = "Üçlü Milli Kinetik Batarya (38 x 3 = 114 DMG • 0.14sn)",
                    ammoText = "3'lü Turkuaz & Altın Zirve Plazma Salvosu (%35 Kritik)",
                    perkText = "MİLLİ HAVA HÂKİMİYETİ: +110 HP Kuantum Kalkan, %35 Kritik, +%45 Hızlı Ulti ve Zirve Salvo!",
                    accentColor = new Color(0.95f, 0.20f, 0.25f)
                };
            case HangarManager.AircraftType.F16Viper:
            default:
                return new AircraftSpecsData
                {
                    displayName = "F-16 VIPER",
                    roleTag = "BAŞLANGIÇ DEVRİYE & TAKTİK AVCISI",
                    tierTag = "KADEME 1 (SEVİYE 1+ • BAŞLANGIÇ JETİ)",
                    armorText = "100 HP (Temel Standart Alüminyum Gövde)",
                    speedText = "6.5 Mach (Standart Başlangıç İtkisi)",
                    weaponText = "Tekli 20mm M61A1 Vulcan (18 DMG x 1 Namlu • 0.30sn)",
                    ammoText = "Standart İzli Mermi (%5 Başlangıç Kritik Şansı)",
                    perkText = "TEMEL EĞİTİM & DEVRİYE JETİ: 1. seviye başlangıç uçağı; üst kademe uçaklara göre en temel özelliklere sahiptir.",
                    accentColor = new Color(0.25f, 0.55f, 0.85f)
                };
        }
    }

    private struct DroneSpecsData
    {
        public string displayName;
        public string roleTag;
        public string tierTag;
        public string hpText;
        public string damageText;
        public string fireRateText;
        public string perkText;
        public Color accentColor;
    }

    private static DroneSpecsData GetDroneSpecsData(DroneCompanion.DroneType type)
    {
        switch (type)
        {
            case DroneCompanion.DroneType.MK2_Interceptor:
                return new DroneSpecsData
                {
                    displayName = "MK-2 AVCI",
                    roleTag = "SİBER LAZER SALVO İHA",
                    tierTag = "KADEME 2 (SEVİYE 20+)",
                    hpText = "140 HP (Güçlendirilmiş Zırh)",
                    damageText = "8 Hasar (DMG)",
                    fireRateText = "0.45 Saniye (Çift Namlu)",
                    perkText = "Keskin Siber Lazer & Yüksek İsabet Oranı",
                    accentColor = new Color(0.0f, 0.85f, 1f)
                };
            case DroneCompanion.DroneType.MK3_Striker:
                return new DroneSpecsData
                {
                    displayName = "MK-3 TAARRUZ",
                    roleTag = "AĞIR KİNETİK TOP İHA",
                    tierTag = "KADEME 3 (SEVİYE 30+)",
                    hpText = "180 HP (Ağır Zırh)",
                    damageText = "14 Hasar (DMG)",
                    fireRateText = "0.70 Saniye (Ağır Kinetik)",
                    perkText = "Zırh Delici Mermi & Ağır Baskı Ateşi",
                    accentColor = new Color(0.95f, 0.25f, 0.2f)
                };
            case DroneCompanion.DroneType.MK4_Missile:
                return new DroneSpecsData
                {
                    displayName = "MK-4 VALKYRIE",
                    roleTag = "GÜDÜMLÜ MİKRO ROKET İHA",
                    tierTag = "KADEME 4 (SEVİYE 50+)",
                    hpText = "220 HP (Yüksek Zırh)",
                    damageText = "26 Hasar (DMG)",
                    fireRateText = "2.20 Saniye (Roket Salvosu)",
                    perkText = "Isı Güdümlü Takip Eden Mikro Roketler",
                    accentColor = new Color(1f, 0.55f, 0.12f)
                };
            case DroneCompanion.DroneType.MK5_TitanAegis:
                return new DroneSpecsData
                {
                    displayName = "MK-5 TİTAN AEGIS",
                    roleTag = "APEX KALKAN & PLAZMA İHA",
                    tierTag = "KADEME 5 (SEVİYE 75+)",
                    hpText = "300 HP (Plazma Kalkan Zırhı)",
                    damageText = "18 Hasar (DMG Plazma)",
                    fireRateText = "1.20 Saniye & 1.55m Savunma Bariyeri",
                    perkText = "Düşman Mermilerini Yutan Enerji Savunma Kalkanı",
                    accentColor = new Color(0.75f, 0.35f, 1f)
                };
            case DroneCompanion.DroneType.MK1_Scout:
            default:
                return new DroneSpecsData
                {
                    displayName = "MK-1 İZCİ",
                    roleTag = "HAFİF KEŞİF & DESTEK İHA",
                    tierTag = "KADEME 1 (SEVİYE 1+)",
                    hpText = "100 HP (Standart Hafif Zırh)",
                    damageText = "5 Hasar (DMG)",
                    fireRateText = "0.65 Saniye (Seri Tekli Atış)",
                    perkText = "Seri Hafif Atış & Düşman Taciz Ateşi",
                    accentColor = new Color(0.2f, 0.95f, 0.42f)
                };
        }
    }

    private void EnsureSpecsModal()
    {
        if (_specsModalObj != null) return;

        // 1. Tam Ekran Karartma Perdesi (Boşluğa tıklayınca da kapanır)
        _specsModalObj = new GameObject("SpecsDetailModal", typeof(RectTransform));
        _specsModalObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform rootRt = GetRect(_specsModalObj);
        MakeFullScreenRect(rootRt);

        Image backdrop = _specsModalObj.AddComponent<Image>();
        backdrop.color = new Color(0.01f, 0.03f, 0.07f, 0.88f);

        Button backDropBtn = _specsModalObj.AddComponent<Button>();
        backDropBtn.targetGraphic = backdrop;
        backDropBtn.onClick.AddListener(CloseSpecsModal);

        // 2. Taktik Bilgi Kutusu (Merkezi Panel)
        GameObject boxObj = new GameObject("DialogBox", typeof(RectTransform));
        boxObj.transform.SetParent(_specsModalObj.transform, false);

        RectTransform boxRt = GetRect(boxObj);
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.anchoredPosition = new Vector2(0f, 10f);
        boxRt.sizeDelta = new Vector2(920f, 620f);

        _specsModalBoxBg = boxObj.AddComponent<Image>();
        _specsModalBoxBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f), new Color(0.03f, 0.06f, 0.12f, 0.98f), false);
        _specsModalBoxBg.type = Image.Type.Sliced;
        _specsModalBoxBg.color = Color.white;

        _specsModalBoxOutline = boxObj.AddComponent<Outline>();
        _specsModalBoxOutline.effectColor = new Color(0f, 0.85f, 1f, 0.5f);
        _specsModalBoxOutline.effectDistance = new Vector2(2f, -2f);

        // 3. Üst Başlık Vizör Plakası
        GameObject visorObj = new GameObject("HeaderVisor", typeof(RectTransform));
        visorObj.transform.SetParent(boxObj.transform, false);
        RectTransform vRt = GetRect(visorObj);
        vRt.anchorMin = new Vector2(0f, 1f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.pivot = new Vector2(0.5f, 1f);
        vRt.anchoredPosition = new Vector2(0f, -14f);
        vRt.sizeDelta = new Vector2(880f, 56f);

        _specsVisorBg = visorObj.AddComponent<Image>();
        _specsVisorBg.sprite = MainMenuManager.GetHudSprite("hud_header_visor", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.04f, 0.10f, 0.18f, 1f), true);
        _specsVisorBg.type = Image.Type.Sliced;
        _specsVisorBg.color = Color.white;

        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(visorObj.transform, false);
        RectTransform tRt = GetRect(titleObj);
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(20f, 0f);
        tRt.offsetMax = new Vector2(-60f, 0f);

        _specsTitleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        _specsTitleTMP.font = MainMenuManager.GetTacticalFont();
        _specsTitleTMP.fontSize = 22f;
        _specsTitleTMP.fontStyle = FontStyles.Bold;
        _specsTitleTMP.characterSpacing = 2.0f;
        _specsTitleTMP.alignment = TextAlignmentOptions.Center;
        _specsTitleTMP.color = Color.white;

        // Üst Sağ [X] Kapat Butonu
        GameObject closeObj = new GameObject("CloseBtn", typeof(RectTransform));
        closeObj.transform.SetParent(visorObj.transform, false);
        RectTransform cRt = GetRect(closeObj);
        cRt.anchorMin = new Vector2(1f, 0.5f);
        cRt.anchorMax = new Vector2(1f, 0.5f);
        cRt.pivot = new Vector2(1f, 0.5f);
        cRt.anchoredPosition = new Vector2(-10f, 0f);
        cRt.sizeDelta = new Vector2(40f, 40f);

        Image cImg = closeObj.AddComponent<Image>();
        cImg.sprite = MainMenuManager.GetHudSprite("hud_btn_slate", new Color(0.85f, 0.25f, 0.25f), new Color(0.18f, 0.05f, 0.07f, 0.95f), true);
        cImg.type = Image.Type.Sliced;
        cImg.color = Color.white;

        Button closeBtn = closeObj.AddComponent<Button>();
        closeBtn.targetGraphic = cImg;
        closeBtn.onClick.AddListener(CloseSpecsModal);

        GameObject cTxtObj = new GameObject("Text", typeof(RectTransform));
        cTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform ctRt = GetRect(cTxtObj);
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.offsetMin = Vector2.zero;
        ctRt.offsetMax = Vector2.zero;

        TextMeshProUGUI ctTMP = cTxtObj.AddComponent<TextMeshProUGUI>();
        ctTMP.font = MainMenuManager.GetTacticalFont();
        ctTMP.text = "<b>X</b>";
        ctTMP.fontSize = 20f;
        ctTMP.fontStyle = FontStyles.Bold;
        ctTMP.alignment = TextAlignmentOptions.Center;
        ctTMP.color = new Color(1f, 0.6f, 0.6f);
        ctTMP.raycastTarget = false;

        // 4. Sol Panel: Model Görsel Vitrini
        GameObject scObj = new GameObject("VisualShowcase", typeof(RectTransform));
        scObj.transform.SetParent(boxObj.transform, false);
        RectTransform scRt = GetRect(scObj);
        scRt.anchorMin = new Vector2(0f, 1f);
        scRt.anchorMax = new Vector2(0f, 1f);
        scRt.pivot = new Vector2(0f, 1f);
        scRt.anchoredPosition = new Vector2(24f, -80f);
        scRt.sizeDelta = new Vector2(330f, 430f);

        _specsShowcaseBg = scObj.AddComponent<Image>();
        _specsShowcaseBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.6f), new Color(0.03f, 0.06f, 0.10f, 0.95f), false);
        _specsShowcaseBg.type = Image.Type.Sliced;
        _specsShowcaseBg.color = Color.white;

        GameObject imgObj = new GameObject("ModelImage", typeof(RectTransform));
        imgObj.transform.SetParent(scObj.transform, false);
        RectTransform iRt = GetRect(imgObj);
        iRt.anchorMin = new Vector2(0.5f, 0.58f);
        iRt.anchorMax = new Vector2(0.5f, 0.58f);
        iRt.pivot = new Vector2(0.5f, 0.5f);
        iRt.anchoredPosition = Vector2.zero;
        iRt.sizeDelta = new Vector2(220f, 220f);

        _specsImage = imgObj.AddComponent<Image>();
        _specsImage.preserveAspect = true;

        GameObject roleObj = new GameObject("RoleTag", typeof(RectTransform));
        roleObj.transform.SetParent(scObj.transform, false);
        RectTransform rRt = GetRect(roleObj);
        rRt.anchorMin = new Vector2(0f, 0f);
        rRt.anchorMax = new Vector2(1f, 0.22f);
        rRt.offsetMin = new Vector2(10f, 10f);
        rRt.offsetMax = new Vector2(-10f, -10f);

        _specsRoleTMP = roleObj.AddComponent<TextMeshProUGUI>();
        _specsRoleTMP.font = MainMenuManager.GetTacticalFont();
        _specsRoleTMP.fontSize = 18f;
        _specsRoleTMP.fontStyle = FontStyles.Bold;
        _specsRoleTMP.characterSpacing = 1.4f;
        _specsRoleTMP.alignment = TextAlignmentOptions.Center;
        _specsRoleTMP.color = Color.white;

        // 5. Sağ Panel: Taktik Özellikler (Az ve Öz, Net Şartname)
        GameObject detailsObj = new GameObject("DetailsPanel", typeof(RectTransform));
        detailsObj.transform.SetParent(boxObj.transform, false);
        RectTransform dRt = GetRect(detailsObj);
        dRt.anchorMin = new Vector2(0f, 1f);
        dRt.anchorMax = new Vector2(0f, 1f);
        dRt.pivot = new Vector2(0f, 1f);
        dRt.anchoredPosition = new Vector2(370f, -80f);
        dRt.sizeDelta = new Vector2(525f, 430f);

        Image dBg = detailsObj.AddComponent<Image>();
        dBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0.25f, 0.35f, 0.50f, 0.8f), new Color(0.02f, 0.05f, 0.09f, 0.95f), false);
        dBg.type = Image.Type.Sliced;
        dBg.color = Color.white;

        GameObject dTxtObj = new GameObject("DetailsText", typeof(RectTransform));
        dTxtObj.transform.SetParent(detailsObj.transform, false);
        RectTransform dtRt = GetRect(dTxtObj);
        dtRt.anchorMin = Vector2.zero;
        dtRt.anchorMax = Vector2.one;
        dtRt.offsetMin = new Vector2(24f, 20f);
        dtRt.offsetMax = new Vector2(-24f, -20f);

        _specsDetailsTMP = dTxtObj.AddComponent<TextMeshProUGUI>();
        _specsDetailsTMP.font = MainMenuManager.GetTacticalFont();
        _specsDetailsTMP.fontSize = 19f;
        _specsDetailsTMP.lineSpacing = 4f;
        _specsDetailsTMP.alignment = TextAlignmentOptions.TopLeft;
        _specsDetailsTMP.color = new Color(0.92f, 0.96f, 1f);

        // 6. Alt Bölüm: Geri Dönüş Butonu (« GERİ DÖN)
        GameObject backBtnObj = new GameObject("BackBtn", typeof(RectTransform));
        backBtnObj.transform.SetParent(boxObj.transform, false);
        RectTransform bRt = GetRect(backBtnObj);
        bRt.anchorMin = new Vector2(0.5f, 0f);
        bRt.anchorMax = new Vector2(0.5f, 0f);
        bRt.pivot = new Vector2(0.5f, 0f);
        bRt.anchoredPosition = new Vector2(0f, 20f);
        bRt.sizeDelta = new Vector2(300f, 48f);

        Image bImg = backBtnObj.AddComponent<Image>();
        bImg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f), new Color(0.04f, 0.12f, 0.22f, 0.95f), true);
        bImg.type = Image.Type.Sliced;
        bImg.color = Color.white;

        Button backBtn = backBtnObj.AddComponent<Button>();
        backBtn.targetGraphic = bImg;
        backBtn.onClick.AddListener(CloseSpecsModal);

        GameObject bTxt = new GameObject("Text", typeof(RectTransform));
        bTxt.transform.SetParent(backBtnObj.transform, false);
        RectTransform btRt = GetRect(bTxt);
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        TextMeshProUGUI bTMP = bTxt.AddComponent<TextMeshProUGUI>();
        bTMP.font = MainMenuManager.GetTacticalFont();
        bTMP.text = "<b>« GERİ DÖN</b>";
        bTMP.fontSize = 20f;
        bTMP.fontStyle = FontStyles.Bold;
        bTMP.characterSpacing = 2.0f;
        bTMP.alignment = TextAlignmentOptions.Center;
        bTMP.color = Color.white;
        bTMP.raycastTarget = false;

        _specsModalObj.SetActive(false);
    }

    public void OpenAircraftSpecsModal(HangarManager.AircraftType type)
    {
        EnsureSpecsModal();
        AircraftSpecsData data = GetAircraftSpecsData(type);

        if (_specsTitleTMP != null)
            _specsTitleTMP.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(data.accentColor)}>{data.displayName}</color>  |  TEKNİK ŞARTNAME</b>";

        if (_specsRoleTMP != null)
            _specsRoleTMP.text = $"<b>{data.tierTag}</b>\n<color=#8AB4F8>{data.roleTag}</color>";

        if (_specsImage != null)
        {
            _specsImage.sprite = GetHangar3DCardSprite(type);
            _specsImage.color = Color.white;
        }

        if (_specsDetailsTMP != null)
        {
            MilitaryStoreManager.LoadoutStats loadout = default;
            if (MilitaryStoreManager.EnsureInstance() != null)
            {
                loadout = MilitaryStoreManager.EnsureInstance().GetLoadoutStats(type);
            }
            bool hasLoadout = loadout.bonusSpeed > 0.01f || loadout.bonusHp > 0.1f || loadout.bonusDamage > 0.1f ||
                              loadout.bonusCrit > 0.1f || loadout.bonusShield > 0.1f || loadout.dodgeCdReduction > 0.01f ||
                              loadout.damageReductionPercent > 0.001f || loadout.ultiBonusPercent > 0.001f;

            string loadoutSection = hasLoadout
                ? $"\n\n<color=#4ADE80><b>» TAKILI ASKERİ MAĞAZA DONANIM BONUSLARI</b></color>\n" +
                  $"  • <color=#7DD3FC>+{loadout.bonusSpeed:0.#} Hız</color> | <color=#4ADE80>+{loadout.bonusHp:0} HP</color> | <color=#F87171>+{loadout.bonusDamage:0} Dmg</color> | <color=#FACC15>+%{loadout.bonusCrit:0} Kritik</color> | <color=#38BDF8>+{loadout.bonusShield:0} Kalkan</color> | <color=#A3E635>-%{loadout.damageReductionPercent * 100f:0} Hasar Emilimi</color>"
                : "";

            _specsDetailsTMP.text =
                $"<color=#00E5FF><b>» GÖVDE & ZIRH SİSTEMİ</b></color>\n" +
                $"  • <b>Gövde Zırhı:</b> <color=#2ECC71>{data.armorText}</color>\n" +
                $"  • <b>Maksimum İtme Hızı:</b> <color=#00E5FF>{data.speedText}</color>\n\n" +
                $"<color=#FFE082><b>» SİLAH & MÜHİMMAT KAPASİTESİ</b></color>\n" +
                $"  • <b>Ana Silah:</b> <color=#FFFFFF>{data.weaponText}</color>\n" +
                $"  • <b>Mühimmat Salvosu:</b> <color=#E2E8F0>{data.ammoText}</color>\n\n" +
                $"<color=#{ColorUtility.ToHtmlStringRGB(data.accentColor)}><b>» TAKTİK HARP ÜSTÜNLÜĞÜ</b></color>\n" +
                $"  • <b>Operasyonel Rol:</b> <color=#FFFFFF>{data.perkText}</color>" +
                loadoutSection;
        }

        if (_specsModalBoxBg != null)
            _specsModalBoxBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", data.accentColor, new Color(0.03f, 0.06f, 0.12f, 0.98f), false);

        if (_specsShowcaseBg != null)
            _specsShowcaseBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(data.accentColor.r, data.accentColor.g, data.accentColor.b, 0.7f), new Color(0.02f, 0.05f, 0.09f, 0.95f), false);

        if (_specsModalBoxOutline != null)
            _specsModalBoxOutline.effectColor = new Color(data.accentColor.r, data.accentColor.g, data.accentColor.b, 0.6f);

        _specsModalObj.SetActive(true);
        _specsModalObj.transform.SetAsLastSibling();
    }

    public void OpenDroneSpecsModal(DroneCompanion.DroneType type)
    {
        EnsureSpecsModal();
        DroneSpecsData data = GetDroneSpecsData(type);

        if (_specsTitleTMP != null)
            _specsTitleTMP.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(data.accentColor)}>{data.displayName}</color>  |  OTONOM İHA VERİLERİ</b>";

        if (_specsRoleTMP != null)
            _specsRoleTMP.text = $"<b>{data.tierTag}</b>\n<color=#8AB4F8>{data.roleTag}</color>";

        if (_specsImage != null)
        {
            _specsImage.sprite = GetDrone3DCardSprite(type);
            _specsImage.color = Color.white;
        }

        if (_specsDetailsTMP != null)
        {
            _specsDetailsTMP.text =
                $"<color=#00E5FF><b>» DAYANIKLILIK & GÖREV POZİSYONU</b></color>\n" +
                $"  • <b>Gövde Dayanıklılığı:</b> <color=#2ECC71>{data.hpText}</color>\n" +
                $"  • <b>Montaj Konumu:</b> <color=#00E5FF>Sol Kanat / Sağ Kanat Destek</color>\n\n" +
                $"<color=#FFE082><b>» ATEŞ GÜCÜ & SALDIRI HIZI</b></color>\n" +
                $"  • <b>Atış Hasarı:</b> <color=#FF5252>{data.damageText}</color>\n" +
                $"  • <b>Ateşleme Aralığı:</b> <color=#FFFFFF>{data.fireRateText}</color>\n\n" +
                $"<color=#{ColorUtility.ToHtmlStringRGB(data.accentColor)}><b>» ÖZEL OTONOM YETENEK</b></color>\n" +
                $"  • <b>Taktiksel İşlev:</b> <color=#FFFFFF>{data.perkText}</color>";
        }

        if (_specsModalBoxBg != null)
            _specsModalBoxBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", data.accentColor, new Color(0.03f, 0.06f, 0.12f, 0.98f), false);

        if (_specsShowcaseBg != null)
            _specsShowcaseBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(data.accentColor.r, data.accentColor.g, data.accentColor.b, 0.7f), new Color(0.02f, 0.05f, 0.09f, 0.95f), false);

        if (_specsModalBoxOutline != null)
            _specsModalBoxOutline.effectColor = new Color(data.accentColor.r, data.accentColor.g, data.accentColor.b, 0.6f);

        _specsModalObj.SetActive(true);
        _specsModalObj.transform.SetAsLastSibling();
    }

    public void CloseSpecsModal()
    {
        if (_specsModalObj != null)
            _specsModalObj.SetActive(false);
    }

    private void BuildBottomBar()
    {
        // 1. GERİ DÖN BUTONU (« GERİ DÖN)
        FormatPixelExactButton(
            "BackFromHangarButton",
            _panelRoot.transform,
            new Vector2(-640.0f, -504.7f),
            new Vector2(176.0f, 58.7f),
            new Color(1f, 0.35f, 0.35f, 0.25f),
            new Color(1f, 0.2f, 0.2f, 0.45f),
            CloseHangar);

        // 2. İKMAL SANDIKLARI MERKEZİ BUTONU (Askeri Mağaza butonu Hangar menüsünden kaldırıldı)
        FormatPixelExactButton(
            "OpenCratesMenuButton",
            _panelRoot.transform,
            new Vector2(585.0f, -504.7f),
            new Vector2(294.0f, 62.0f),
            new Color(1.0f, 0.80f, 0.22f, 0.22f),
            new Color(0.9f, 0.7f, 0.15f, 0.45f),
            () =>
            {
                CloseDirectly();
                CratesUI.EnsureUI()?.OpenCratesPanel(() => EnsureUI()?.OpenHangar());
            });
    }
}
