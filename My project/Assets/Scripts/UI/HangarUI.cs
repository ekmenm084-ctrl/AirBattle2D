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

    // 3 Ferah Sekme
    public enum HangarTab { Planes, Workshop, Drones }
    private HangarTab _currentTab = HangarTab.Planes;

    private Button _tabPlanesBtn, _tabWorkshopBtn, _tabDronesBtn;
    private GameObject _planesContainer, _workshopContainer, _dronesContainer;

    // Tab 1: Uçak Filosu
    private Button _f16Btn, _a10Btn, _f22Btn;
    private TextMeshProUGUI _f16BtnTMP, _a10BtnTMP, _f22BtnTMP;

    // Tab 2: Atölye Geliştirmeleri
    private TextMeshProUGUI _armorLvlTMP, _damageLvlTMP, _speedLvlTMP, _critLvlTMP;
    private Button _armorUpgBtn, _damageUpgBtn, _speedUpgBtn, _critUpgBtn;
    private TextMeshProUGUI _armorCostTMP, _damageCostTMP, _speedCostTMP, _critCostTMP;

    // Tab 3: Destek Droneları (5 Aşamalı Model & Can/Dayanıklılık)
    private TextMeshProUGUI _droneSlotsStatusTMP;

    private class DroneCardRef
    {
        public DroneCompanion.DroneType type;
        public Button buyBtn;
        public TextMeshProUGUI buyTMP;
        public Button equipLBtn;
        public TextMeshProUGUI equipLTMP;
        public Button equipRBtn;
        public TextMeshProUGUI equipRTMP;
        public Button repairBtn;
        public TextMeshProUGUI repairTMP;
        public Image hpFillImg;
        public TextMeshProUGUI hpTextTMP;
        public TextMeshProUGUI tagTMP;
        public string defaultTag;
    }
    private readonly System.Collections.Generic.List<DroneCardRef> _droneCards = new System.Collections.Generic.List<DroneCardRef>();

    private static RectTransform GetRect(GameObject go)
    {
        if (go == null) return null;
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        return rt;
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
        GameObject go = existing != null ? existing.gameObject : new GameObject("HangarUI");
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
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
            if (gameObject != null)
                gameObject.SetActive(false);
        }
        catch { }
    }

    public void SwitchTab(HangarTab tab)
    {
        _currentTab = tab;

        if (_planesContainer != null) _planesContainer.SetActive(tab == HangarTab.Planes);
        if (_workshopContainer != null) _workshopContainer.SetActive(tab == HangarTab.Workshop);
        if (_dronesContainer != null) _dronesContainer.SetActive(tab == HangarTab.Drones);

        UpdateTabButtonVisual(_tabPlanesBtn, "UÇAK FİLOSU", tab == HangarTab.Planes);
        UpdateTabButtonVisual(_tabWorkshopBtn, "ATÖLYE GELİŞTİRMELERİ", tab == HangarTab.Workshop);
        UpdateTabButtonVisual(_tabDronesBtn, "DESTEK DRONELARI", tab == HangarTab.Drones);

        RefreshAllVisuals();
    }

    private void UpdateTabButtonVisual(Button btn, string title, bool isActive)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();

        if (img != null)
            img.color = isActive ? new Color(0.18f, 0.52f, 0.78f) : new Color(0.10f, 0.14f, 0.22f);

        if (tmp != null)
        {
            tmp.text = isActive ? $"<b>{title}</b>" : title;
            tmp.color = isActive ? Color.white : new Color(0.70f, 0.78f, 0.88f);
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
        UpdatePlaneCard(HangarManager.AircraftType.F16Viper, _f16Btn, _f16BtnTMP, 0);
        UpdatePlaneCard(HangarManager.AircraftType.A10TankBuster, _a10Btn, _a10BtnTMP, HangarManager.A10_UNLOCK_COST);
        UpdatePlaneCard(HangarManager.AircraftType.F22Raptor, _f22Btn, _f22BtnTMP, HangarManager.F22_UNLOCK_COST);

        // 3. Tab 2: Atölye
        UpdateUpgradeRow("ZIRH", hm.armorLevel, HangarManager.ArmorCosts, _armorLvlTMP, _armorUpgBtn, _armorCostTMP, hm.gold);
        UpdateUpgradeRow("HASAR", hm.damageLevel, HangarManager.DamageCosts, _damageLvlTMP, _damageUpgBtn, _damageCostTMP, hm.gold);
        UpdateUpgradeRow("HIZ", hm.speedLevel, HangarManager.SpeedCosts, _speedLvlTMP, _speedUpgBtn, _speedCostTMP, hm.gold);
        UpdateUpgradeRow("KRİTİK", hm.criticalLevel, HangarManager.CritCosts, _critLvlTMP, _critUpgBtn, _critCostTMP, hm.gold);

        // 4. Tab 3: Destek Droneları
        RefreshDroneTabVisuals(hm);
    }

    private void RefreshDroneTabVisuals(HangarManager hm)
    {
        if (_droneSlotsStatusTMP != null)
        {
            string leftName = HangarManager.GetDroneDisplayName(hm.leftDroneType);
            string rightName = HangarManager.GetDroneDisplayName(hm.rightDroneType);
            float leftHp = hm.GetDroneHealth(hm.leftDroneType);
            float rightHp = hm.GetDroneHealth(hm.rightDroneType);

            string leftInfo = (hm.leftDroneType != DroneCompanion.DroneType.None) ? $"{leftName} ({leftHp:0} HP)" : "BOŞ";
            string rightInfo = (hm.rightDroneType != DroneCompanion.DroneType.None) ? $"{rightName} ({rightHp:0} HP)" : "BOŞ";

            _droneSlotsStatusTMP.text = $"<b>[KANAT DONANIMI]</b> Sol Kanat: <color=#00E5FF>{leftInfo}</color>  |  Sağ Kanat: <color=#00E5FF>{rightInfo}</color>";
        }

        int playerLevel = PlayerProfileManager.EnsureInstance()?.level ?? 1;

        foreach (var card in _droneCards)
        {
            bool isUnlocked = hm.IsDroneUnlocked(card.type);
            float currentHp = hm.GetDroneHealth(card.type);
            float maxHp = HangarManager.GetDroneMaxHealth(card.type);
            int cost = HangarManager.GetDroneCost(card.type);
            int repCost = HangarManager.GetDroneRepairCost(card.type);
            int reqLvl = HangarManager.GetDroneRequiredLevel(card.type);
            bool isLevelLocked = (playerLevel < reqLvl);

            bool isLeft = (hm.leftDroneType == card.type);
            bool isRight = (hm.rightDroneType == card.type);

            // Dayanıklılık Barı & Metni
            if (card.hpFillImg != null)
            {
                float pct = Mathf.Clamp01(currentHp / Mathf.Max(1f, maxHp));
                card.hpFillImg.fillAmount = isUnlocked ? pct : 0f;
                card.hpFillImg.color = pct > 0.5f ? Color.Lerp(Color.yellow, new Color(0.15f, 0.95f, 0.35f), (pct - 0.5f) * 2f)
                                                  : Color.Lerp(Color.red, Color.yellow, pct * 2f);
            }
            if (card.hpTextTMP != null)
            {
                if (isLevelLocked) card.hpTextTMP.text = $"<color=#FF5252>SEVİYE {reqLvl} KİLİDİ</color>";
                else if (!isUnlocked && currentHp <= 0f) card.hpTextTMP.text = "<color=#FF5252>İMHA OLDU / TÜKENDİ</color>";
                else if (!isUnlocked) card.hpTextTMP.text = $"DAYANIKLILIK: {maxHp:0} HP";
                else card.hpTextTMP.text = $"DAYANIKLILIK: {currentHp:0} / {maxHp:0} HP";
            }

            // Rozet Başlığı
            if (card.tagTMP != null)
            {
                if (isLevelLocked) card.tagTMP.text = $"[ KİLİTLİ: SEV. {reqLvl} ]";
                else card.tagTMP.text = $"[ {card.defaultTag} ]";
            }

            // Satın Alma Butonu (Kilitliyken veya canı bitince görünür)
            if (card.buyBtn != null && card.buyTMP != null)
            {
                card.buyBtn.gameObject.SetActive(!isUnlocked);
                if (!isUnlocked)
                {
                    if (isLevelLocked)
                    {
                        card.buyTMP.text = $"SEV. {reqLvl} KİLİDİ ({cost:N0}G)";
                        card.buyBtn.interactable = false;
                        card.buyBtn.GetComponent<Image>().color = new Color(0.24f, 0.18f, 0.20f);
                    }
                    else
                    {
                        card.buyTMP.text = (currentHp <= 0f && PlayerPrefs.GetInt(HangarManager.PREF_DRONE_UNLOCKED_PREFIX + (int)card.type, 0) == 0)
                            ? $"TEKRAR AL ({cost:N0}G)"
                            : $"SATIN AL ({cost:N0}G)";
                        bool canAfford = (hm.gold >= cost);
                        card.buyBtn.interactable = canAfford;
                        card.buyBtn.GetComponent<Image>().color = canAfford ? new Color(0.15f, 0.65f, 0.32f) : new Color(0.35f, 0.25f, 0.22f);
                    }
                }
            }

            // Kuşanma Butonları (Açık ve canı varsa görünür)
            if (card.equipLBtn != null)
            {
                card.equipLBtn.gameObject.SetActive(isUnlocked);
                if (card.equipLTMP != null) card.equipLTMP.text = isLeft ? "[AKTİF] SOL" : "SOL TAK";
                card.equipLBtn.GetComponent<Image>().color = isLeft ? new Color(0.18f, 0.65f, 0.35f) : new Color(0.20f, 0.35f, 0.50f);
            }

            if (card.equipRBtn != null)
            {
                card.equipRBtn.gameObject.SetActive(isUnlocked);
                if (card.equipRTMP != null) card.equipRTMP.text = isRight ? "[AKTİF] SAĞ" : "SAĞ TAK";
                card.equipRBtn.GetComponent<Image>().color = isRight ? new Color(0.18f, 0.65f, 0.35f) : new Color(0.20f, 0.35f, 0.50f);
            }

            // Onarım Butonu (Hasar almışsa görünür)
            if (card.repairBtn != null)
            {
                bool needsRepair = isUnlocked && currentHp < maxHp && currentHp > 0f;
                card.repairBtn.gameObject.SetActive(needsRepair);
                if (needsRepair && card.repairTMP != null)
                {
                    card.repairTMP.text = $"ONAR ({repCost:N0}G)";
                    card.repairBtn.interactable = (hm.gold >= repCost);
                    card.repairBtn.GetComponent<Image>().color = (hm.gold >= repCost) ? new Color(0.18f, 0.52f, 0.70f) : new Color(0.3f, 0.3f, 0.3f);
                }
            }
        }
    }
    private void UpdatePlaneCard(HangarManager.AircraftType type, Button btn, TextMeshProUGUI btnTMP, int unlockCost)
    {
        if (btn == null || btnTMP == null) return;

        HangarManager hm = HangarManager.EnsureInstance();
        bool isUnlocked = hm.IsAircraftUnlocked(type);
        bool isSelected = (hm.activeAircraft == type);

        Image bg = btn.GetComponent<Image>();

        if (isSelected)
        {
            btnTMP.text = "✓ GÖREVDE";
            btnTMP.color = Color.white;
            if (bg != null) bg.color = new Color(0.18f, 0.55f, 0.32f);
        }
        else if (isUnlocked)
        {
            btnTMP.text = "GÖREVE SEÇ";
            btnTMP.color = Color.white;
            if (bg != null) bg.color = new Color(0.18f, 0.45f, 0.72f);
        }
        else
        {
            btnTMP.text = $"KİLİDİ AÇ ({unlockCost}G)";
            btnTMP.color = (hm.gold >= unlockCost) ? Color.yellow : new Color(0.85f, 0.5f, 0.5f);
            if (bg != null) bg.color = new Color(0.38f, 0.22f, 0.15f);
        }
    }

    private void UpdateUpgradeRow(string label, int level, int[] costs, TextMeshProUGUI lvlTMP, Button upgBtn, TextMeshProUGUI costTMP, int currentGold)
    {
        if (lvlTMP != null)
            lvlTMP.text = (level >= HangarManager.MAX_UPGRADE_LEVEL) ? "[MAKS SEVİYE]" : $"[SEVİYE: {level} / {HangarManager.MAX_UPGRADE_LEVEL}]";

        if (upgBtn != null && costTMP != null)
        {
            if (level >= HangarManager.MAX_UPGRADE_LEVEL)
            {
                costTMP.text = "MAKS SEVİYE";
                upgBtn.interactable = false;
                upgBtn.GetComponent<Image>().color = new Color(0.28f, 0.28f, 0.28f);
            }
            else
            {
                int cost = costs[level];
                costTMP.text = $"GELİŞTİR ({cost}G)";
                bool canAfford = (currentGold >= cost);
                upgBtn.interactable = canAfford;
                upgBtn.GetComponent<Image>().color = canAfford ? new Color(0.18f, 0.55f, 0.32f) : new Color(0.32f, 0.22f, 0.22f);
            }
        }
    }

    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        _panelRoot = new GameObject("HangarPanelRoot");
        _panelRoot.transform.SetParent(transform, false);

        RectTransform rootRt = GetRect(_panelRoot);
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image panelBg = _panelRoot.AddComponent<Image>();
        panelBg.color = new Color(0.02f, 0.05f, 0.09f, 1.0f);

        // 1. ÜST PANEL & ALTIN ROZETİ
        BuildHeader();

        // 2. SEKME BUTONLARI (4 SEKMELİ FERAH YERLEŞİM)
        BuildTabsHeader();

        // 3. Tab 1 Container (Uçak Filosu)
        BuildPlanesContainer();

        // 4. Tab 2 Container (Atölye Geliştirmeleri)
        BuildWorkshopContainer();

        // 5. Tab 3 Container (Destek Droneları)
        BuildDronesContainer();

        // 6. ALT NAVİGASYON (GERİ DÖN, ENVANTER & İKMAL SANDIKLARI)
        BuildBottomBar();

        _panelRoot.SetActive(false);
    }

    private void BuildHeader()
    {
        GameObject headerObj = new GameObject("HeaderTitle");
        headerObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform hRt = GetRect(headerObj);
        hRt.anchorMin = new Vector2(0.5f, 1f);
        hRt.anchorMax = new Vector2(0.5f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -22f);
        hRt.sizeDelta = new Vector2(1000f, 50f);

        TextMeshProUGUI titleTMP = headerObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "<b>ASKERİ HANGAR VE TEKNİK ATÖLYE</b>";
        titleTMP.fontSize = 30f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;

        // Üst Sağ Altın Rozeti
        GameObject goldBadgeObj = new GameObject("GoldBadge");
        goldBadgeObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform gRt = GetRect(goldBadgeObj);
        gRt.anchorMin = new Vector2(1f, 1f);
        gRt.anchorMax = new Vector2(1f, 1f);
        gRt.pivot = new Vector2(1f, 1f);
        gRt.anchoredPosition = new Vector2(-40f, -20f);
        gRt.sizeDelta = new Vector2(320f, 52f);

        Image gBg = goldBadgeObj.AddComponent<Image>();
        gBg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

        Outline gOutline = goldBadgeObj.AddComponent<Outline>();
        gOutline.effectColor = new Color(1f, 0.85f, 0.2f);
        gOutline.effectDistance = new Vector2(2f, -2f);

        GameObject goldTxtObj = new GameObject("GoldText");
        goldTxtObj.transform.SetParent(goldBadgeObj.transform, false);
        RectTransform gtRt = GetRect(goldTxtObj);
        gtRt.anchorMin = Vector2.zero;
        gtRt.anchorMax = Vector2.one;
        gtRt.offsetMin = new Vector2(10f, 0f);
        gtRt.offsetMax = new Vector2(-10f, 0f);

        _goldTMP = goldTxtObj.AddComponent<TextMeshProUGUI>();
        _goldTMP.text = "ALTIN: 0 G";
        _goldTMP.fontSize = 24f;
        _goldTMP.fontStyle = FontStyles.Bold;
        _goldTMP.alignment = TextAlignmentOptions.Center;
        _goldTMP.color = new Color(1f, 0.88f, 0.25f);

        // Üst Sol Ana Menüye Dön Butonu
        GameObject topBackBtnObj = new GameObject("TopBackFromHangarButton");
        topBackBtnObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform tbRt = GetRect(topBackBtnObj);
        tbRt.anchorMin = new Vector2(0f, 1f);
        tbRt.anchorMax = new Vector2(0f, 1f);
        tbRt.pivot = new Vector2(0f, 1f);
        tbRt.anchoredPosition = new Vector2(35f, -20f);
        tbRt.sizeDelta = new Vector2(200f, 52f);

        Image tbImg = topBackBtnObj.AddComponent<Image>();
        tbImg.color = new Color(0.18f, 0.28f, 0.40f);

        Outline tbOutline = topBackBtnObj.AddComponent<Outline>();
        tbOutline.effectColor = new Color(0.35f, 0.75f, 1f);
        tbOutline.effectDistance = new Vector2(2f, -2f);

        Button tbBtn = topBackBtnObj.AddComponent<Button>();
        tbBtn.onClick.AddListener(CloseHangar);

        GameObject tbTxtObj = new GameObject("Text");
        tbTxtObj.transform.SetParent(topBackBtnObj.transform, false);
        RectTransform tbTxtRt = GetRect(tbTxtObj);
        tbTxtRt.anchorMin = Vector2.zero;
        tbTxtRt.anchorMax = Vector2.one;
        tbTxtRt.offsetMin = Vector2.zero;
        tbTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tbTMP = tbTxtObj.AddComponent<TextMeshProUGUI>();
        tbTMP.text = "< ANA MENÜ";
        tbTMP.fontSize = 21f;
        tbTMP.fontStyle = FontStyles.Bold;
        tbTMP.alignment = TextAlignmentOptions.Center;
        tbTMP.color = Color.white;
    }

    private void BuildTabsHeader()
    {
        GameObject tabsObj = new GameObject("TabsHeader");
        tabsObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform tRt = GetRect(tabsObj);
        tRt.anchorMin = new Vector2(0.5f, 1f);
        tRt.anchorMax = new Vector2(0.5f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -80f);
        tRt.sizeDelta = new Vector2(1500f, 50f);

        float tabWidth = 440f;
        float spacing = 470f;

        _tabPlanesBtn = CreateTabButton(tabsObj.transform, -spacing, "UÇAK FİLOSU", () => SwitchTab(HangarTab.Planes), tabWidth);
        _tabWorkshopBtn = CreateTabButton(tabsObj.transform, 0f, "ATÖLYE GELİŞTİRMELERİ", () => SwitchTab(HangarTab.Workshop), tabWidth);
        _tabDronesBtn = CreateTabButton(tabsObj.transform, spacing, "DESTEK DRONELARI", () => SwitchTab(HangarTab.Drones), tabWidth);
    }

    private Button CreateTabButton(Transform parent, float posX, string text, UnityEngine.Events.UnityAction action, float width)
    {
        GameObject btnObj = new GameObject("TabBtn_" + text);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = GetRect(btnObj);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(posX, 0f);
        rt.sizeDelta = new Vector2(width, 48f);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.10f, 0.14f, 0.22f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = GetRect(txtObj);
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    // ==================== TAB 1: UÇAK FİLOSU (FERAH VE GENİŞ) ====================
    private void BuildPlanesContainer()
    {
        _planesContainer = new GameObject("PlanesContainer");
        _planesContainer.transform.SetParent(_panelRoot.transform, false);

        RectTransform rt = GetRect(_planesContainer);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        float cardY = 20f;
        float spacingX = 490f;

        CreatePlaneCard("F16_Card", -spacingX, cardY,
            "F-16 VIPER",
            "DENGELİ TAKTİK AVCI",
            "Gövde Sağlığı: 100 HP\n\nUçuş Hızı: 8.0 Mach\n\nNamlular: Tekli Taktik Top\n\nMermi Hasarı: 20 DMG\n\nÖzellik: Yüksek manevra kabiliyeti",
            new Color(0.25f, 0.55f, 0.85f),
            HangarManager.AircraftType.F16Viper,
            out _f16Btn, out _f16BtnTMP);

        CreatePlaneCard("A10_Card", 0f, cardY,
            "A-10 TANK BUSTER",
            "AĞIR ZIRHLI TAARRUZ JETİ",
            "Gövde Sağlığı: 160 HP\n\nUçuş Hızı: 6.2 Mach\n\nNamlular: 30mm GAU-8 Avenger\n\nMermi Hasarı: 28 DMG\n\nÖzellik: Yüksek dayanıklılık ve ateş gücü",
            new Color(0.25f, 0.65f, 0.40f),
            HangarManager.AircraftType.A10TankBuster,
            out _a10Btn, out _a10BtnTMP);

        CreatePlaneCard("F22_Card", spacingX, cardY,
            "F-22 RAPTOR",
            "HAYALET SÜPERSONİK JET",
            "Gövde Sağlığı: 80 HP\n\nUçuş Hızı: 10.5 Mach\n\nNamlular: Çiftli Vulcan Salvosu\n\nMermi Hasarı: 22 x 2 DMG\n\nÖzellik: Çift mermi atışı & süpersonik hız",
            new Color(0.60f, 0.35f, 0.75f),
            HangarManager.AircraftType.F22Raptor,
            out _f22Btn, out _f22BtnTMP);
    }

    private void CreatePlaneCard(string objName, float posX, float posY, string name, string subtitle, string stats, Color borderColor, HangarManager.AircraftType type, out Button actionBtn, out TextMeshProUGUI actionBtnTMP)
    {
        GameObject cardObj = new GameObject(objName);
        cardObj.transform.SetParent(_planesContainer.transform, false);

        RectTransform cRt = GetRect(cardObj);
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(posX, posY);
        cRt.sizeDelta = new Vector2(440f, 530f);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.06f, 0.10f, 0.17f, 0.95f);

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(3f, -3f);

        // Başlık
        GameObject titleObj = new GameObject("CardTitle");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = GetRect(titleObj);
        tRt.anchorMin = new Vector2(0f, 0.80f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tTMP = titleObj.AddComponent<TextMeshProUGUI>();
        tTMP.text = $"<b>{name}</b>\n<size=72%><color=#8AB4F8>{subtitle}</color></size>";
        tTMP.fontSize = 24f;
        tTMP.alignment = TextAlignmentOptions.Center;
        tTMP.color = Color.white;

        // Özellikler
        GameObject statObj = new GameObject("Stats");
        statObj.transform.SetParent(cardObj.transform, false);
        RectTransform sRt = GetRect(statObj);
        sRt.anchorMin = new Vector2(0.06f, 0.18f);
        sRt.anchorMax = new Vector2(0.94f, 0.78f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        TextMeshProUGUI sTMP = statObj.AddComponent<TextMeshProUGUI>();
        sTMP.text = stats;
        sTMP.fontSize = 20f;
        sTMP.lineSpacing = 5f;
        sTMP.alignment = TextAlignmentOptions.Center;
        sTMP.color = new Color(0.9f, 0.95f, 1f);

        // Aksiyon Butonu
        GameObject btnObj = new GameObject("ActionButton");
        btnObj.transform.SetParent(cardObj.transform, false);
        RectTransform bRt = GetRect(btnObj);
        bRt.anchorMin = new Vector2(0.08f, 0.04f);
        bRt.anchorMax = new Vector2(0.92f, 0.16f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.55f, 0.32f);

        actionBtn = btnObj.AddComponent<Button>();
        actionBtn.onClick.AddListener(() =>
        {
            HangarManager hm = HangarManager.EnsureInstance();
            if (hm.IsAircraftUnlocked(type))
            {
                hm.SelectAircraft(type);
            }
            else
            {
                hm.TryUnlockAircraft(type);
            }
            RefreshAllVisuals();
        });

        GameObject btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        RectTransform btRt = GetRect(btnTxtObj);
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        actionBtnTMP = btnTxtObj.AddComponent<TextMeshProUGUI>();
        actionBtnTMP.text = "GÖREVE SEÇ";
        actionBtnTMP.fontSize = 20f;
        actionBtnTMP.fontStyle = FontStyles.Bold;
        actionBtnTMP.alignment = TextAlignmentOptions.Center;
        actionBtnTMP.color = Color.white;
    }

    // ==================== TAB 2: ATÖLYE GELİŞTİRMELERİ (GENİŞ VE FERAH) ====================
    private void BuildWorkshopContainer()
    {
        _workshopContainer = new GameObject("WorkshopContainer");
        _workshopContainer.transform.SetParent(_panelRoot.transform, false);

        RectTransform rt = GetRect(_workshopContainer);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        float startY = 160f;
        float rowSpacing = 95f;

        CreateUpgradeRow("[ZIRH] TİTANYUM GÖVDE VE BALİSTİK KORUMA (+15 HP)", startY,
            out _armorLvlTMP, out _armorUpgBtn, out _armorCostTMP,
            () => { if (HangarManager.Instance.TryUpgradeArmor()) RefreshAllVisuals(); });

        CreateUpgradeRow("[HASAR] PLAZMA AŞIRI YÜKLEMELİ NAMLULAR (+3 DMG)", startY - rowSpacing,
            out _damageLvlTMP, out _damageUpgBtn, out _damageCostTMP,
            () => { if (HangarManager.Instance.TryUpgradeDamage()) RefreshAllVisuals(); });

        CreateUpgradeRow("[HIZ] ARTIK YAKICILI TURBO İTİCİLER (+0.6 HIZ)", startY - rowSpacing * 2,
            out _speedLvlTMP, out _speedUpgBtn, out _speedCostTMP,
            () => { if (HangarManager.Instance.TryUpgradeSpeed()) RefreshAllVisuals(); });

        CreateUpgradeRow("[KRİTİK] HİPER GELİŞMİŞ NİŞANGAH SİSTEMİ (+%6)", startY - rowSpacing * 3,
            out _critLvlTMP, out _critUpgBtn, out _critCostTMP,
            () => { if (HangarManager.Instance.TryUpgradeCritical()) RefreshAllVisuals(); });

        // Bilgi Kartı
        GameObject infoObj = new GameObject("WorkshopInfoBox");
        infoObj.transform.SetParent(_workshopContainer.transform, false);
        RectTransform iRt = GetRect(infoObj);
        iRt.anchorMin = new Vector2(0.5f, 0.5f);
        iRt.anchorMax = new Vector2(0.5f, 0.5f);
        iRt.pivot = new Vector2(0.5f, 0.5f);
        iRt.anchoredPosition = new Vector2(0f, -220f);
        iRt.sizeDelta = new Vector2(1400f, 65f);

        Image iBg = infoObj.AddComponent<Image>();
        iBg.color = new Color(0.06f, 0.10f, 0.17f, 0.90f);

        GameObject infoTxt = new GameObject("Text");
        infoTxt.transform.SetParent(infoObj.transform, false);
        RectTransform itRt = GetRect(infoTxt);
        itRt.anchorMin = Vector2.zero;
        itRt.anchorMax = Vector2.one;
        itRt.offsetMin = Vector2.zero;
        itRt.offsetMax = Vector2.zero;

        TextMeshProUGUI iTMP = infoTxt.AddComponent<TextMeshProUGUI>();
        iTMP.text = "<color=#F1C40F><b>BİLGİ:</b></color> Atölyede yapılan tüm kalıcı yükseltmeler filonuzdaki tüm uçaklara ve açacağınız yeni jetlere kalıcı olarak eklenir.";
        iTMP.fontSize = 20f;
        iTMP.alignment = TextAlignmentOptions.Center;
        iTMP.color = new Color(0.85f, 0.92f, 1f);
    }

    private void CreateUpgradeRow(string title, float posY, out TextMeshProUGUI lvlTMP, out Button upgBtn, out TextMeshProUGUI costTMP, UnityEngine.Events.UnityAction onUpgrade)
    {
        GameObject rowObj = new GameObject("UpgRow_" + title);
        rowObj.transform.SetParent(_workshopContainer.transform, false);

        RectTransform rRt = GetRect(rowObj);
        rRt.anchorMin = new Vector2(0.5f, 0.5f);
        rRt.anchorMax = new Vector2(0.5f, 0.5f);
        rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.anchoredPosition = new Vector2(0f, posY);
        rRt.sizeDelta = new Vector2(1400f, 72f);

        Image rBg = rowObj.AddComponent<Image>();
        rBg.color = new Color(0.06f, 0.10f, 0.17f, 0.95f);

        Outline rOutline = rowObj.AddComponent<Outline>();
        rOutline.effectColor = new Color(0.2f, 0.35f, 0.55f);
        rOutline.effectDistance = new Vector2(2f, -2f);

        // Başlık
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(rowObj.transform, false);
        RectTransform tRt = GetRect(tObj);
        tRt.anchorMin = new Vector2(0f, 0f);
        tRt.anchorMax = new Vector2(0.54f, 1f);
        tRt.offsetMin = new Vector2(25f, 0f);
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
        tmp.text = title;
        tmp.fontSize = 21f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;

        // Seviye
        GameObject lObj = new GameObject("Level");
        lObj.transform.SetParent(rowObj.transform, false);
        RectTransform lRt = GetRect(lObj);
        lRt.anchorMin = new Vector2(0.54f, 0f);
        lRt.anchorMax = new Vector2(0.74f, 1f);
        lRt.offsetMin = Vector2.zero;
        lRt.offsetMax = Vector2.zero;

        lvlTMP = lObj.AddComponent<TextMeshProUGUI>();
        lvlTMP.text = "[SEVİYE: 0 / 5]";
        lvlTMP.fontSize = 21f;
        lvlTMP.fontStyle = FontStyles.Bold;
        lvlTMP.alignment = TextAlignmentOptions.Center;
        lvlTMP.color = new Color(0f, 0.9f, 1f);

        // Yükselt Butonu
        GameObject bObj = new GameObject("UpgradeBtn");
        bObj.transform.SetParent(rowObj.transform, false);
        RectTransform bRt = GetRect(bObj);
        bRt.anchorMin = new Vector2(0.76f, 0.10f);
        bRt.anchorMax = new Vector2(0.98f, 0.90f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        Image bImg = bObj.AddComponent<Image>();
        bImg.color = new Color(0.18f, 0.55f, 0.32f);

        upgBtn = bObj.AddComponent<Button>();
        upgBtn.onClick.AddListener(onUpgrade);

        GameObject cObj = new GameObject("CostText");
        cObj.transform.SetParent(bObj.transform, false);
        RectTransform cRt = GetRect(cObj);
        cRt.anchorMin = Vector2.zero;
        cRt.anchorMax = Vector2.one;
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        costTMP = cObj.AddComponent<TextMeshProUGUI>();
        costTMP.text = "GELİŞTİR (100G)";
        costTMP.fontSize = 20f;
        costTMP.fontStyle = FontStyles.Bold;
        costTMP.alignment = TextAlignmentOptions.Center;
        costTMP.color = Color.white;
    }

    // ==================== TAB 3: DESTEK DRONELARI (5 AŞAMALI GÜÇ DÜZEYİ & DAYANIKLILIK) ====================
    private void BuildDronesContainer()
    {
        _dronesContainer = new GameObject("DronesContainer");
        _dronesContainer.transform.SetParent(_panelRoot.transform, false);

        RectTransform rt = GetRect(_dronesContainer);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Kanat Yuva Durumu
        GameObject statusObj = new GameObject("DroneSlotsStatus");
        statusObj.transform.SetParent(_dronesContainer.transform, false);
        RectTransform sRt = GetRect(statusObj);
        sRt.anchorMin = new Vector2(0.5f, 1f);
        sRt.anchorMax = new Vector2(0.5f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, -145f);
        sRt.sizeDelta = new Vector2(1440f, 45f);

        _droneSlotsStatusTMP = statusObj.AddComponent<TextMeshProUGUI>();
        _droneSlotsStatusTMP.fontSize = 22f;
        _droneSlotsStatusTMP.alignment = TextAlignmentOptions.Center;
        _droneSlotsStatusTMP.color = Color.white;

        _droneCards.Clear();

        // 5 Ferah Drone Kartı (Azdan Çoka Güç Düzeyi)
        float cardY = 15f;
        float spacingX = 290f;

        CreateDroneCard("MK1ScoutCard", -2f * spacingX, cardY,
            "MK-1 İZCİ",
            "HAFİF KEŞİF İHA",
            "Hasar: 5 DMG\nAtış Aralığı: 0.65s\nÖzellik: Seri Hafif Atış",
            new Color(0.2f, 0.95f, 0.42f),
            DroneCompanion.DroneType.MK1_Scout,
            "KADEME 1 (SEV. 20+)");

        CreateDroneCard("MK2InterceptorCard", -1f * spacingX, cardY,
            "MK-2 AVCI",
            "SİBER LAZER SALVOSU",
            "Hasar: 8 DMG\nAtış: Çift Namlu 0.45s\nÖzellik: Keskin Siber Lazer",
            new Color(0.0f, 0.85f, 1f),
            DroneCompanion.DroneType.MK2_Interceptor,
            "KADEME 2 (SEV. 30+)");

        CreateDroneCard("MK3StrikerCard", 0f, cardY,
            "MK-3 TAARRUZ",
            "AĞIR KİNETİK TOP",
            "Hasar: 14 DMG\nAtış Aralığı: 0.70s\nÖzellik: Zırh Delici Mermi",
            new Color(0.95f, 0.25f, 0.2f),
            DroneCompanion.DroneType.MK3_Striker,
            "KADEME 3 (SEV. 50+)");

        CreateDroneCard("MK4MissileCard", 1f * spacingX, cardY,
            "MK-4 VALKYRIE",
            "GÜDÜMLÜ MİKRO ROKET",
            "Hasar: 26 DMG\nAtış: Isı Güdümlü 2.2s\nÖzellik: Düşmanı Takip Eder",
            new Color(1f, 0.55f, 0.12f),
            DroneCompanion.DroneType.MK4_Missile,
            "KADEME 4 (SEV. 75+)");

        CreateDroneCard("MK5TitanCard", 2f * spacingX, cardY,
            "MK-5 TİTAN AEGIS",
            "APEX KALKAN & PLAZMA",
            "Hasar: 18 DMG Plazma\nBariyer: 1.55m Savunma\nÖzellik: Mermileri Yutar",
            new Color(0.75f, 0.35f, 1f),
            DroneCompanion.DroneType.MK5_TitanAegis,
            "KADEME 5 (SEV. 100+)");
    }

    private void CreateDroneCard(string objName, float posX, float posY, string title, string subtitle, string description, Color accentColor, DroneCompanion.DroneType type, string tierBadge)
    {
        GameObject cardObj = new GameObject(objName);
        cardObj.transform.SetParent(_dronesContainer.transform, false);

        RectTransform cRt = GetRect(cardObj);
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(posX, posY);
        cRt.sizeDelta = new Vector2(275f, 545f);

        Image cBg = cardObj.AddComponent<Image>();
        cBg.color = new Color(0.06f, 0.10f, 0.17f, 0.95f);

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        // 1. Başlık & Alt Başlık
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = GetRect(tObj);
        tRt.anchorMin = new Vector2(0f, 0.88f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tTMP = tObj.AddComponent<TextMeshProUGUI>();
        tTMP.text = $"<b>{title}</b>\n<size=70%><color=#8AB4F8>{subtitle}</color></size>";
        tTMP.fontSize = 20f;
        tTMP.alignment = TextAlignmentOptions.Center;
        tTMP.color = Color.white;

        // 2. Drone Görsel Vitrini
        GameObject showcaseObj = new GameObject("DroneVisualShowcase");
        showcaseObj.transform.SetParent(cardObj.transform, false);
        RectTransform scRt = GetRect(showcaseObj);
        scRt.anchorMin = new Vector2(0.06f, 0.63f);
        scRt.anchorMax = new Vector2(0.94f, 0.87f);
        scRt.offsetMin = Vector2.zero;
        scRt.offsetMax = Vector2.zero;

        Image scBg = showcaseObj.AddComponent<Image>();
        scBg.color = new Color(0.03f, 0.06f, 0.12f, 0.95f);

        Outline scOutline = showcaseObj.AddComponent<Outline>();
        scOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.65f);
        scOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Drone İkonu
        GameObject iconObj = new GameObject("DroneIconImage");
        iconObj.transform.SetParent(showcaseObj.transform, false);
        RectTransform iconRt = GetRect(iconObj);
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(0f, 8f);
        iconRt.sizeDelta = new Vector2(64f, 64f);

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = DroneCompanion.GetDroneSprite(type);
        iconImg.preserveAspect = true;

        // Drone Tip Rozeti
        GameObject tagObj = new GameObject("DroneTag");
        tagObj.transform.SetParent(showcaseObj.transform, false);
        RectTransform tagRt = GetRect(tagObj);
        tagRt.anchorMin = new Vector2(0f, 0f);
        tagRt.anchorMax = new Vector2(1f, 0.28f);
        tagRt.offsetMin = Vector2.zero;
        tagRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tagTMP = tagObj.AddComponent<TextMeshProUGUI>();
        tagTMP.text = $"[ {tierBadge} ]";
        tagTMP.fontSize = 12f;
        tagTMP.fontStyle = FontStyles.Bold;
        tagTMP.alignment = TextAlignmentOptions.Center;
        tagTMP.color = accentColor;

        // 3. Dayanıklılık (Can) Barı
        GameObject hpBarBgObj = new GameObject("HpBarBg");
        hpBarBgObj.transform.SetParent(cardObj.transform, false);
        RectTransform hpBgRt = GetRect(hpBarBgObj);
        hpBgRt.anchorMin = new Vector2(0.06f, 0.53f);
        hpBgRt.anchorMax = new Vector2(0.94f, 0.61f);
        hpBgRt.offsetMin = Vector2.zero;
        hpBgRt.offsetMax = Vector2.zero;

        Image hpBgImg = hpBarBgObj.AddComponent<Image>();
        hpBgImg.color = new Color(0.03f, 0.05f, 0.08f, 1f);

        GameObject hpFillObj = new GameObject("HpFill");
        hpFillObj.transform.SetParent(hpBarBgObj.transform, false);
        RectTransform hpFillRt = GetRect(hpFillObj);
        hpFillRt.anchorMin = Vector2.zero;
        hpFillRt.anchorMax = Vector2.one;
        hpFillRt.offsetMin = new Vector2(2f, 2f);
        hpFillRt.offsetMax = new Vector2(-2f, -2f);

        Image hpFill = hpFillObj.AddComponent<Image>();
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpFill.color = new Color(0.2f, 0.9f, 0.4f);

        GameObject hpTxtObj = new GameObject("HpText");
        hpTxtObj.transform.SetParent(hpBarBgObj.transform, false);
        RectTransform htRt = GetRect(hpTxtObj);
        htRt.anchorMin = Vector2.zero;
        htRt.anchorMax = Vector2.one;
        htRt.offsetMin = Vector2.zero;
        htRt.offsetMax = Vector2.zero;

        TextMeshProUGUI hpTMP = hpTxtObj.AddComponent<TextMeshProUGUI>();
        hpTMP.fontSize = 12f;
        hpTMP.fontStyle = FontStyles.Bold;
        hpTMP.alignment = TextAlignmentOptions.Center;
        hpTMP.color = Color.white;

        // 4. Açıklama & Savaş Nitelikleri
        GameObject dObj = new GameObject("Desc");
        dObj.transform.SetParent(cardObj.transform, false);
        RectTransform dRt = GetRect(dObj);
        dRt.anchorMin = new Vector2(0.06f, 0.32f);
        dRt.anchorMax = new Vector2(0.94f, 0.51f);
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;

        TextMeshProUGUI dTMP = dObj.AddComponent<TextMeshProUGUI>();
        dTMP.text = description;
        dTMP.fontSize = 14f;
        dTMP.lineSpacing = 3f;
        dTMP.alignment = TextAlignmentOptions.Center;
        dTMP.color = new Color(0.9f, 0.95f, 1f);

        // 5. Satın Alma / Tekrar Al Butonu (Kilitli veya Canı bitince)
        GameObject unlObj = new GameObject("BuyBtn");
        unlObj.transform.SetParent(cardObj.transform, false);
        RectTransform unlRt = GetRect(unlObj);
        unlRt.anchorMin = new Vector2(0.06f, 0.04f);
        unlRt.anchorMax = new Vector2(0.94f, 0.28f);
        unlRt.offsetMin = Vector2.zero;
        unlRt.offsetMax = Vector2.zero;

        unlObj.AddComponent<Image>();
        Button buyBtn = unlObj.AddComponent<Button>();
        buyBtn.onClick.AddListener(() =>
        {
            if (HangarManager.Instance.TryBuyDrone(type))
            {
                RefreshAllVisuals();
            }
        });
        TextMeshProUGUI buyTMP = CreateButtonTMP(unlObj, "SATIN AL");

        // 6. Kuşanma Butonları (Sol Kanat & Sağ Kanat)
        GameObject eqLObj = new GameObject("EquipLeftBtn");
        eqLObj.transform.SetParent(cardObj.transform, false);
        RectTransform elRt = GetRect(eqLObj);
        elRt.anchorMin = new Vector2(0.06f, 0.16f);
        elRt.anchorMax = new Vector2(0.48f, 0.28f);
        elRt.offsetMin = Vector2.zero;
        elRt.offsetMax = Vector2.zero;

        eqLObj.AddComponent<Image>();
        Button equipLBtn = eqLObj.AddComponent<Button>();
        equipLBtn.onClick.AddListener(() =>
        {
            HangarManager.Instance.EquipDrone(type, isLeft: true);
            RefreshAllVisuals();
        });
        TextMeshProUGUI eqLTMP = CreateButtonTMP(eqLObj, "SOL TAK");

        GameObject eqRObj = new GameObject("EquipRightBtn");
        eqRObj.transform.SetParent(cardObj.transform, false);
        RectTransform erRt = GetRect(eqRObj);
        erRt.anchorMin = new Vector2(0.52f, 0.16f);
        erRt.anchorMax = new Vector2(0.94f, 0.28f);
        erRt.offsetMin = Vector2.zero;
        erRt.offsetMax = Vector2.zero;

        eqRObj.AddComponent<Image>();
        Button equipRBtn = eqRObj.AddComponent<Button>();
        equipRBtn.onClick.AddListener(() =>
        {
            HangarManager.Instance.EquipDrone(type, isLeft: false);
            RefreshAllVisuals();
        });
        TextMeshProUGUI eqRTMP = CreateButtonTMP(eqRObj, "SAĞ TAK");

        // 7. Onarım Butonu
        GameObject repObj = new GameObject("RepairBtn");
        repObj.transform.SetParent(cardObj.transform, false);
        RectTransform repRt = GetRect(repObj);
        repRt.anchorMin = new Vector2(0.06f, 0.03f);
        repRt.anchorMax = new Vector2(0.94f, 0.14f);
        repRt.offsetMin = Vector2.zero;
        repRt.offsetMax = Vector2.zero;

        repObj.AddComponent<Image>();
        Button repBtn = repObj.AddComponent<Button>();
        repBtn.onClick.AddListener(() =>
        {
            if (HangarManager.Instance.TryRepairDrone(type))
            {
                RefreshAllVisuals();
            }
        });
        TextMeshProUGUI repTMP = CreateButtonTMP(repObj, "TAMİR ET");

        _droneCards.Add(new DroneCardRef
        {
            type = type,
            buyBtn = buyBtn,
            buyTMP = buyTMP,
            equipLBtn = equipLBtn,
            equipLTMP = eqLTMP,
            equipRBtn = equipRBtn,
            equipRTMP = eqRTMP,
            repairBtn = repBtn,
            repairTMP = repTMP,
            hpFillImg = hpFill,
            hpTextTMP = hpTMP,
            tagTMP = tagTMP,
            defaultTag = tierBadge
        });
    }

    private TextMeshProUGUI CreateButtonTMP(GameObject parent, string text)
    {
        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(parent.transform, false);
        RectTransform rt = GetRect(tObj);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = tObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    private void BuildBottomBar()
    {
        // 1. GERİ DÖN BUTONU (EKRANIN SOL EN ALTINDA)
        GameObject backBtnObj = new GameObject("BackFromHangarButton");
        backBtnObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform bRt = GetRect(backBtnObj);
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.zero;
        bRt.pivot = Vector2.zero;
        bRt.anchoredPosition = new Vector2(40f, 25f);
        bRt.sizeDelta = new Vector2(260f, 54f);

        Image bImg = backBtnObj.AddComponent<Image>();
        bImg.color = new Color(0.20f, 0.30f, 0.44f);

        Outline bOutline = backBtnObj.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.35f, 0.75f, 1f);
        bOutline.effectDistance = new Vector2(2f, -2f);

        Button bBtn = backBtnObj.AddComponent<Button>();
        bBtn.onClick.AddListener(CloseHangar);

        GameObject bTxtObj = new GameObject("Text");
        bTxtObj.transform.SetParent(backBtnObj.transform, false);
        RectTransform bTxtRt = GetRect(bTxtObj);
        bTxtRt.anchorMin = Vector2.zero;
        bTxtRt.anchorMax = Vector2.one;
        bTxtRt.offsetMin = Vector2.zero;
        bTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI bTMP = bTxtObj.AddComponent<TextMeshProUGUI>();
        bTMP.text = "< GERİ DÖN";
        bTMP.fontSize = 22f;
        bTMP.fontStyle = FontStyles.Bold;
        bTMP.alignment = TextAlignmentOptions.Center;
        bTMP.color = Color.white;

        // 2. ASKERİ ENVANTERİ AÇ BUTONU (ÖNCEKİ MENÜ KAPANIR, YENİ SAYFADA AÇILIR)
        GameObject invBtnObj = new GameObject("OpenInventoryButton");
        invBtnObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform iRt = GetRect(invBtnObj);
        iRt.anchorMin = new Vector2(1f, 0f);
        iRt.anchorMax = new Vector2(1f, 0f);
        iRt.pivot = new Vector2(1f, 0f);
        iRt.anchoredPosition = new Vector2(-360f, 25f);
        iRt.sizeDelta = new Vector2(300f, 54f);

        Image iImg = invBtnObj.AddComponent<Image>();
        iImg.color = new Color(0.18f, 0.48f, 0.72f);

        Button iBtn = invBtnObj.AddComponent<Button>();
        iBtn.onClick.AddListener(() =>
        {
            CloseDirectly();
            InventoryUI.EnsureUI()?.OpenInventory(() => OpenHangar());
        });

        GameObject iTxtObj = new GameObject("Text");
        iTxtObj.transform.SetParent(invBtnObj.transform, false);
        RectTransform iTxtRt = GetRect(iTxtObj);
        iTxtRt.anchorMin = Vector2.zero;
        iTxtRt.anchorMax = Vector2.one;
        iTxtRt.offsetMin = Vector2.zero;
        iTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI iTMP = iTxtObj.AddComponent<TextMeshProUGUI>();
        iTMP.text = "🎒 ASKERİ ENVANTER";
        iTMP.fontSize = 22f;
        iTMP.fontStyle = FontStyles.Bold;
        iTMP.alignment = TextAlignmentOptions.Center;
        iTMP.color = Color.white;

        // 3. İKMAL SANDIKLARI MERKEZİ BUTONU (SANDIKLAR AYRI MENÜDE)
        GameObject crateBtnObj = new GameObject("OpenCratesMenuButton");
        crateBtnObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform cRt = GetRect(crateBtnObj);
        cRt.anchorMin = new Vector2(1f, 0f);
        cRt.anchorMax = new Vector2(1f, 0f);
        cRt.pivot = new Vector2(1f, 0f);
        cRt.anchoredPosition = new Vector2(-40f, 25f);
        cRt.sizeDelta = new Vector2(300f, 54f);

        Image cImg = crateBtnObj.AddComponent<Image>();
        cImg.color = new Color(0.72f, 0.48f, 0.12f);

        Button cBtn = crateBtnObj.AddComponent<Button>();
        cBtn.onClick.AddListener(() =>
        {
            CloseDirectly();
            CratesUI.EnsureUI()?.OpenCratesPanel(() => OpenHangar());
        });

        GameObject cTxtObj = new GameObject("Text");
        cTxtObj.transform.SetParent(crateBtnObj.transform, false);
        RectTransform cTxtRt = GetRect(cTxtObj);
        cTxtRt.anchorMin = Vector2.zero;
        cTxtRt.anchorMax = Vector2.one;
        cTxtRt.offsetMin = Vector2.zero;
        cTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI cTMP = cTxtObj.AddComponent<TextMeshProUGUI>();
        cTMP.text = "🧰 İKMAL SANDIKLARI";
        cTMP.fontSize = 22f;
        cTMP.fontStyle = FontStyles.Bold;
        cTMP.alignment = TextAlignmentOptions.Center;
        cTMP.color = Color.white;

        // Geri Dön Butonunu hiyerarşide en öne al
        backBtnObj.transform.SetAsLastSibling();
    }
}
