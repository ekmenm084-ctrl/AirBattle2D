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

    // 2 Ferah Sekme (Uçak Filosu & Destek Droneları)
    public enum HangarTab { Planes, Drones }
    private HangarTab _currentTab = HangarTab.Planes;

    private Button _tabPlanesBtn, _tabDronesBtn;
    private GameObject _planesContainer, _dronesContainer;

    // Tab 1: Uçak Filosu (6 İleri Düzey Askeri Jet)
    private class PlaneCardRef
    {
        public HangarManager.AircraftType type;
        public Button actionBtn;
        public TextMeshProUGUI actionBtnTMP;
    }
    private readonly System.Collections.Generic.List<PlaneCardRef> _planeCards = new System.Collections.Generic.List<PlaneCardRef>();
    private TextMeshProUGUI _fleetStatusTMP;
    private ScrollRect _planesScrollRect;

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
        PlayerProfileUI.EnsureUI()?.ShowMenuStatusBar();
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
        if (_dronesContainer != null) _dronesContainer.SetActive(tab == HangarTab.Drones);

        UpdateTabButtonVisual(_tabPlanesBtn, "UÇAK FİLOSU", tab == HangarTab.Planes);
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
        RefreshPlaneTabVisuals(hm);

        // 3. Tab 2: Destek Droneları
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
            bool isOwned = hm.IsDroneOwned(card.type);
            bool isUnlocked = hm.IsDroneUnlocked(card.type);
            int deaths = hm.GetDroneDeaths(card.type);
            float currentHp = hm.GetDroneHealth(card.type);
            float maxHp = HangarManager.GetDroneMaxHealth(card.type);
            int cost = HangarManager.GetDroneCost(card.type);
            int repCost = HangarManager.GetDroneRepairCost(card.type);
            int reqLvl = HangarManager.GetDroneRequiredLevel(card.type);
            bool isLevelLocked = (playerLevel < reqLvl);

            bool isLeft = (hm.leftDroneType == card.type);
            bool isRight = (hm.rightDroneType == card.type);

            bool isWreck = isOwned && currentHp <= 0f;

            // Dayanıklılık Barı & Metni
            if (card.hpFillImg != null)
            {
                float pct = Mathf.Clamp01(currentHp / Mathf.Max(1f, maxHp));
                card.hpFillImg.fillAmount = (isUnlocked) ? pct : 0f;
                card.hpFillImg.color = pct > 0.5f ? Color.Lerp(Color.yellow, new Color(0.15f, 0.95f, 0.35f), (pct - 0.5f) * 2f)
                                                  : Color.Lerp(Color.red, Color.yellow, pct * 2f);
            }
            if (card.hpTextTMP != null)
            {
                if (isLevelLocked)
                {
                    card.hpTextTMP.text = $"<color=#FF5252>SEVİYE {reqLvl} KİLİDİ</color>";
                }
                else if (isWreck)
                {
                    card.hpTextTMP.text = "<color=#FFA000>AĞIR HASARLI / ENKAZ (1/2 HAK)</color>";
                }
                else if (!isOwned)
                {
                    card.hpTextTMP.text = "<color=#FF5252>TAMAMEN İMHA OLDU (0/2 HAK)</color>";
                }
                else
                {
                    card.hpTextTMP.text = $"DAYANIKLILIK: {currentHp:0} / {maxHp:0} HP";
                }
            }

            // Rozet Başlığı
            if (card.tagTMP != null)
            {
                if (isLevelLocked)
                {
                    card.tagTMP.text = $"[ KİLİTLİ: SEV. {reqLvl} ]";
                }
                else if (isWreck)
                {
                    card.tagTMP.text = "[ ONARILABİLİR: 1/2 HAK ]";
                }
                else if (!isOwned)
                {
                    card.tagTMP.text = "[ 0/2 HAK - TEKRAR AL ]";
                }
                else
                {
                    int livesLeft = Mathf.Max(1, 2 - deaths);
                    card.tagTMP.text = $"[ {card.defaultTag} ({livesLeft}/2 HAK) ]";
                }
            }

            // Buton Görünürlükleri ve Konumlandırmaları
            if (isLevelLocked)
            {
                if (card.buyBtn != null && card.buyTMP != null)
                {
                    card.buyBtn.gameObject.SetActive(true);
                    card.buyTMP.text = $"SEV. {reqLvl} KİLİDİ ({cost:N0}G)";
                    card.buyBtn.interactable = false;
                    card.buyBtn.GetComponent<Image>().color = new Color(0.24f, 0.18f, 0.20f);
                }
                if (card.equipLBtn != null) card.equipLBtn.gameObject.SetActive(false);
                if (card.equipRBtn != null) card.equipRBtn.gameObject.SetActive(false);
                if (card.repairBtn != null) card.repairBtn.gameObject.SetActive(false);
            }
            else if (isWreck)
            {
                // 1. Ölüm sonrası: Satın alma butonu kapalı, Ağır Hasarı Onar butonu açık
                if (card.buyBtn != null) card.buyBtn.gameObject.SetActive(false);
                if (card.equipLBtn != null) card.equipLBtn.gameObject.SetActive(false);
                if (card.equipRBtn != null) card.equipRBtn.gameObject.SetActive(false);

                if (card.repairBtn != null && card.repairTMP != null)
                {
                    card.repairBtn.gameObject.SetActive(true);
                    RectTransform repRt = card.repairBtn.GetComponent<RectTransform>();
                    repRt.anchorMin = new Vector2(0.06f, 0.04f);
                    repRt.anchorMax = new Vector2(0.94f, 0.28f);

                    card.repairTMP.text = $"AĞIR HASARI ONAR\n<size=85%><color=#FFE082>({repCost:N0} ALTIN)</color></size>";
                    bool canAfford = (hm.gold >= repCost);
                    card.repairBtn.interactable = canAfford;
                    card.repairBtn.GetComponent<Image>().color = canAfford ? new Color(0.85f, 0.50f, 0.10f) : new Color(0.35f, 0.25f, 0.20f);
                }
            }
            else if (!isOwned)
            {
                // 2. Ölüm sonrası (Sıfırlanmış): Tekrar Satın Al butonu açık
                if (card.buyBtn != null && card.buyTMP != null)
                {
                    card.buyBtn.gameObject.SetActive(true);
                    card.buyTMP.text = $"TEKRAR SATIN AL\n<size=85%><color=#FFE082>({cost:N0} ALTIN)</color></size>";
                    bool canAfford = (hm.gold >= cost);
                    card.buyBtn.interactable = canAfford;
                    card.buyBtn.GetComponent<Image>().color = canAfford ? new Color(0.15f, 0.65f, 0.32f) : new Color(0.35f, 0.25f, 0.22f);
                }
                if (card.equipLBtn != null) card.equipLBtn.gameObject.SetActive(false);
                if (card.equipRBtn != null) card.equipRBtn.gameObject.SetActive(false);
                if (card.repairBtn != null) card.repairBtn.gameObject.SetActive(false);
            }
            else // isUnlocked (Operasyonel)
            {
                if (card.buyBtn != null) card.buyBtn.gameObject.SetActive(false);

                if (card.equipLBtn != null)
                {
                    card.equipLBtn.gameObject.SetActive(true);
                    if (card.equipLTMP != null) card.equipLTMP.text = isLeft ? "[AKTİF] SOL" : "SOL TAK";
                    card.equipLBtn.GetComponent<Image>().color = isLeft ? new Color(0.18f, 0.65f, 0.35f) : new Color(0.20f, 0.35f, 0.50f);
                }

                if (card.equipRBtn != null)
                {
                    card.equipRBtn.gameObject.SetActive(true);
                    if (card.equipRTMP != null) card.equipRTMP.text = isRight ? "[AKTİF] SAĞ" : "SAĞ TAK";
                    card.equipRBtn.GetComponent<Image>().color = isRight ? new Color(0.18f, 0.65f, 0.35f) : new Color(0.20f, 0.35f, 0.50f);
                }

                if (card.repairBtn != null)
                {
                    bool needsRepair = (currentHp < maxHp);
                    card.repairBtn.gameObject.SetActive(needsRepair);

                    if (needsRepair && card.repairTMP != null)
                    {
                        RectTransform repRt = card.repairBtn.GetComponent<RectTransform>();
                        repRt.anchorMin = new Vector2(0.06f, 0.03f);
                        repRt.anchorMax = new Vector2(0.94f, 0.14f);

                        card.repairTMP.text = $"ONAR ({repCost:N0}G)";
                        bool canAfford = (hm.gold >= repCost);
                        card.repairBtn.interactable = canAfford;
                        card.repairBtn.GetComponent<Image>().color = canAfford ? new Color(0.18f, 0.52f, 0.70f) : new Color(0.3f, 0.3f, 0.3f);
                    }
                }
            }
        }
    }
    private void RefreshPlaneTabVisuals(HangarManager hm)
    {
        if (_fleetStatusTMP != null)
        {
            string activeName = HangarManager.GetAircraftDisplayName(hm.activeAircraft);
            _fleetStatusTMP.text = $"<b>[HAVA KUVVETLERİ FİLOSU]</b> Seçili Görev Jeti: <color=#00E5FF>{activeName.ToUpper()}</color>  |  Filo Envanteri: 6 Muharip Jet";
        }

        int playerLevel = PlayerProfileManager.EnsureInstance()?.level ?? 1;

        foreach (var card in _planeCards)
        {
            if (card.actionBtn == null || card.actionBtnTMP == null) continue;

            bool isUnlocked = hm.IsAircraftUnlocked(card.type);
            bool isSelected = (hm.activeAircraft == card.type);
            int reqLvl = HangarManager.GetAircraftRequiredLevel(card.type);
            int unlockCost = HangarManager.GetAircraftUnlockCost(card.type);
            bool isLevelLocked = (playerLevel < reqLvl);

            Image bg = card.actionBtn.GetComponent<Image>();

            if (isSelected)
            {
                card.actionBtnTMP.text = "[AKTİF] GÖREVDE";
                card.actionBtnTMP.color = Color.white;
                card.actionBtn.interactable = true;
                if (bg != null) bg.color = new Color(0.18f, 0.55f, 0.32f);
            }
            else if (isUnlocked)
            {
                card.actionBtnTMP.text = "GÖREVE SEÇ";
                card.actionBtnTMP.color = Color.white;
                card.actionBtn.interactable = true;
                if (bg != null) bg.color = new Color(0.18f, 0.45f, 0.72f);
            }
            else
            {
                if (isLevelLocked)
                {
                    card.actionBtnTMP.text = $"SEV. {reqLvl} KİLİDİ ({unlockCost:N0}G)";
                    card.actionBtnTMP.color = new Color(0.85f, 0.5f, 0.5f);
                    card.actionBtn.interactable = false;
                    if (bg != null) bg.color = new Color(0.24f, 0.18f, 0.20f);
                }
                else
                {
                    card.actionBtnTMP.text = $"KİLİDİ AÇ ({unlockCost:N0}G)";
                    bool canAfford = (hm.gold >= unlockCost);
                    card.actionBtnTMP.color = canAfford ? Color.yellow : new Color(0.85f, 0.5f, 0.5f);
                    card.actionBtn.interactable = canAfford;
                    if (bg != null) bg.color = canAfford ? new Color(0.38f, 0.25f, 0.15f) : new Color(0.25f, 0.20f, 0.20f);
                }
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

        // 2. SEKME BUTONLARI (UÇAKLAR & DRONELAR)
        BuildTabsHeader();

        // 3. Tab 1 Container (Uçak Filosu)
        BuildPlanesContainer();

        // 4. Tab 2 Container (Destek Droneları)
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
        titleTMP.text = "<b>ASKERİ HANGAR</b>";
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
        tRt.sizeDelta = new Vector2(1200f, 50f);

        float tabWidth = 520f;
        float spacing = 280f;

        _tabPlanesBtn = CreateTabButton(tabsObj.transform, -spacing, "UÇAK FİLOSU", () => SwitchTab(HangarTab.Planes), tabWidth);
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

    // ==================== TAB 1: UÇAK FİLOSU (6 İLERİ DÜZEY ASKERİ JET) ====================
    private void BuildPlanesContainer()
    {
        _planesContainer = new GameObject("PlanesContainer");
        _planesContainer.transform.SetParent(_panelRoot.transform, false);

        RectTransform rt = GetRect(_planesContainer);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 1. Filo Durum Başlığı
        GameObject statusObj = new GameObject("FleetStatus");
        statusObj.transform.SetParent(_planesContainer.transform, false);
        RectTransform sRt = GetRect(statusObj);
        sRt.anchorMin = new Vector2(0.5f, 1f);
        sRt.anchorMax = new Vector2(0.5f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, -145f);
        sRt.sizeDelta = new Vector2(1440f, 45f);

        _fleetStatusTMP = statusObj.AddComponent<TextMeshProUGUI>();
        _fleetStatusTMP.fontSize = 22f;
        _fleetStatusTMP.alignment = TextAlignmentOptions.Center;
        _fleetStatusTMP.color = Color.white;

        // 2. Yatay ScrollView (6 Jet Vitrini)
        GameObject scrollObj = new GameObject("PlanesScrollView");
        scrollObj.transform.SetParent(_planesContainer.transform, false);
        RectTransform scRt = GetRect(scrollObj);
        scRt.anchorMin = new Vector2(0.5f, 0.5f);
        scRt.anchorMax = new Vector2(0.5f, 0.5f);
        scRt.pivot = new Vector2(0.5f, 0.5f);
        scRt.anchoredPosition = new Vector2(0f, 15f);
        scRt.sizeDelta = new Vector2(1500f, 580f);

        scrollObj.AddComponent<RectMask2D>();
        _planesScrollRect = scrollObj.AddComponent<ScrollRect>();
        _planesScrollRect.horizontal = true;
        _planesScrollRect.vertical = false;
        _planesScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _planesScrollRect.scrollSensitivity = 35f;

        // Scroll Content
        GameObject contentObj = new GameObject("PlanesContent");
        contentObj.transform.SetParent(scrollObj.transform, false);
        RectTransform cRt = GetRect(contentObj);
        cRt.anchorMin = new Vector2(0f, 0.5f);
        cRt.anchorMax = new Vector2(0f, 0.5f);
        cRt.pivot = new Vector2(0f, 0.5f);
        cRt.anchoredPosition = Vector2.zero;

        float cardW = 370f;
        float cardH = 560f;
        float spacing = 22f;
        float startX = 15f;
        int totalPlanes = 6;
        float totalWidth = startX + totalPlanes * (cardW + spacing) + 20f;
        cRt.sizeDelta = new Vector2(totalWidth, cardH + 20f);
        _planesScrollRect.content = cRt;

        _planeCards.Clear();

        // 6 Kartın Oluşturulması
        float curX = startX + cardW * 0.5f;

        CreatePlaneCard("F16_Card", contentObj.transform, curX, 0f, cardW, cardH,
            "F-16 VIPER",
            "DENGELİ ÇEVİK AVCI",
            "KADEME 1 (SEV. 1+)",
            "<b>Gövde Zırhı:</b> <color=#2ECC71>100 HP</color>  |  <b>Hız:</b> <color=#00E5FF>8.0 Mach</color>\n" +
            "<b>Ana Silah:</b> <color=#FFE082>20mm Taktik Top (20 DMG)</color>\n" +
            "<b>Mühimmat:</b> Tekli Hassas Mermi Salvosu\n" +
            "<b>Taktik Üstünlük:</b> Çevik İt Dalaşı & Standart Manevra",
            new Color(0.25f, 0.55f, 0.85f),
            HangarManager.AircraftType.F16Viper);
        curX += cardW + spacing;

        CreatePlaneCard("A10_Card", contentObj.transform, curX, 0f, cardW, cardH,
            "A-10 TANK BUSTER",
            "AĞIR ZIRHLI TAARRUZ JETİ",
            "KADEME 2 (SEV. 15+)",
            "<b>Gövde Zırhı:</b> <color=#2ECC71>180 HP (Titanyum)</color>  |  <b>Hız:</b> <color=#00E5FF>6.2 Mach</color>\n" +
            "<b>Ana Silah:</b> <color=#FF5252>30mm GAU-8 Avenger (32 DMG)</color>\n" +
            "<b>Mühimmat:</b> Ağır Kinetik Zırh Delici Mermi\n" +
            "<b>Taktik Üstünlük:</b> Yüksek Zırh & Yıkıcı Ateş Gücü",
            new Color(0.25f, 0.65f, 0.40f),
            HangarManager.AircraftType.A10TankBuster);
        curX += cardW + spacing;

        CreatePlaneCard("F22_Card", contentObj.transform, curX, 0f, cardW, cardH,
            "F-22 RAPTOR",
            "5. NESİL HAYALET JET",
            "KADEME 3 (SEV. 30+)",
            "<b>Gövde Zırhı:</b> <color=#E74C3C>120 HP (Karbon)</color>  |  <b>Hız:</b> <color=#00E5FF>10.5 Mach</color>\n" +
            "<b>Ana Silah:</b> <color=#BD10E0>Çift Vulcan (24 x 2 DMG)</color>\n" +
            "<b>Mühimmat:</b> Çift Paralel Salvo ile Yüksek DPS\n" +
            "<b>Taktik Üstünlük:</b> Radar Görünmezliği & Süperseyir",
            new Color(0.60f, 0.35f, 0.75f),
            HangarManager.AircraftType.F22Raptor);
        curX += cardW + spacing;

        CreatePlaneCard("Su57_Card", contentObj.transform, curX, 0f, cardW, cardH,
            "SU-57 FELON",
            "5. NESİL SÜPER-MANEVRA",
            "KADEME 4 (SEV. 50+)",
            "<b>Gövde Zırhı:</b> <color=#2ECC71>160 HP (Plazma)</color>  |  <b>Hız:</b> <color=#00E5FF>9.8 Mach</color>\n" +
            "<b>Ana Silah:</b> <color=#00E5FF>Çift 30mm Plazma Top (28 x 2 DMG)</color>\n" +
            "<b>Mühimmat:</b> Çift Salvo & <color=#F1C40F>%15 Kritik Şansı</color>\n" +
            "<b>Taktik Üstünlük:</b> 3D Vektör İtme & Kritik Darbe",
            new Color(0.15f, 0.75f, 0.85f),
            HangarManager.AircraftType.Su57Felon);
        curX += cardW + spacing;

        CreatePlaneCard("F35_Card", contentObj.transform, curX, 0f, cardW, cardH,
            "F-35 LIGHTNING II",
            "5. NESİL ÇOK AMAÇLI HAYALET",
            "KADEME 5 (SEV. 70+)",
            "<b>Gövde Zırhı:</b> <color=#2ECC71>150 HP (Grafen)</color>  |  <b>Hız:</b> <color=#00E5FF>11.2 Mach</color>\n" +
            "<b>Ana Silah:</b> <color=#FFA000>Üçlü Akıllı Salvo (22 x 3 DMG)</color>\n" +
            "<b>Mühimmat:</b> 3 Namlulu Yaylım & <color=#F1C40F>%10 Kritik</color>\n" +
            "<b>Taktik Üstünlük:</b> Elektro-Optik Hedefleme & Geniş Alan",
            new Color(1.0f, 0.65f, 0.15f),
            HangarManager.AircraftType.F35Lightning);
        curX += cardW + spacing;

        CreatePlaneCard("Kaan_Card", contentObj.transform, curX, 0f, cardW, cardH,
            "KAAN APEX FIGHTER",
            "5.+ NESİL HAVA HÂKİMİYETİ",
            "KADEME 6 (SEV. 90+)",
            "<b>Gövde Zırhı:</b> <color=#2ECC71>220 HP (Nano-Seramik)</color>  |  <b>Hız:</b> <color=#00E5FF>12.0 Mach</color>\n" +
            "<b>Ana Silah:</b> <color=#FF1744>3'lü Termal Batarya (34 x 3 DMG)</color>\n" +
            "<b>Mühimmat:</b> 3 Namlu Ağır Darbe & <color=#F1C40F>%25 Kritik</color>\n" +
            "<b>Taktik Üstünlük:</b> Mutlak Hava Hâkimiyeti & Zirve DPS",
            new Color(0.95f, 0.20f, 0.25f),
            HangarManager.AircraftType.KaanApex);

        // 3. Sol ve Sağ Kaydırma Butonları (Navigasyon Okları)
        CreateNavButton("NavLeftBtn", -795f, 15f, "<", () =>
        {
            if (_planesScrollRect != null)
                _planesScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(_planesScrollRect.horizontalNormalizedPosition - 0.35f);
        });

        CreateNavButton("NavRightBtn", 795f, 15f, ">", () =>
        {
            if (_planesScrollRect != null)
                _planesScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(_planesScrollRect.horizontalNormalizedPosition + 0.35f);
        });
    }

    private void CreateNavButton(string objName, float posX, float posY, string arrowText, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(objName);
        btnObj.transform.SetParent(_planesContainer.transform, false);

        RectTransform rt = GetRect(btnObj);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(posX, posY);
        rt.sizeDelta = new Vector2(55f, 110f);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.08f, 0.15f, 0.26f, 0.90f);

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.0f, 0.85f, 1f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = GetRect(txtObj);
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = arrowText;
        tmp.fontSize = 32f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.0f, 0.9f, 1.0f);
    }

    private void CreatePlaneCard(string objName, Transform parent, float posX, float posY, float width, float height, string name, string subtitle, string badgeTag, string stats, Color borderColor, HangarManager.AircraftType type)
    {
        GameObject cardObj = new GameObject(objName);
        cardObj.transform.SetParent(parent, false);

        RectTransform cRt = GetRect(cardObj);
        cRt.anchorMin = new Vector2(0f, 0.5f);
        cRt.anchorMax = new Vector2(0f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(posX, posY);
        cRt.sizeDelta = new Vector2(width, height);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.06f, 0.10f, 0.17f, 0.95f);

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        // 1. Başlık & Model Tanımı
        GameObject titleObj = new GameObject("CardTitle");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = GetRect(titleObj);
        tRt.anchorMin = new Vector2(0f, 0.87f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tTMP = titleObj.AddComponent<TextMeshProUGUI>();
        tTMP.text = $"<b>{name}</b>\n<size=70%><color=#8AB4F8>{subtitle}</color></size>";
        tTMP.fontSize = 20f;
        tTMP.alignment = TextAlignmentOptions.Center;
        tTMP.color = Color.white;

        // 2. UÇAK GÖRSEL VİTRİNİ
        GameObject showcaseObj = new GameObject("PlaneVisualShowcase");
        showcaseObj.transform.SetParent(cardObj.transform, false);
        RectTransform scRt = GetRect(showcaseObj);
        scRt.anchorMin = new Vector2(0.06f, 0.52f);
        scRt.anchorMax = new Vector2(0.94f, 0.86f);
        scRt.offsetMin = Vector2.zero;
        scRt.offsetMax = Vector2.zero;

        Image scBg = showcaseObj.AddComponent<Image>();
        scBg.color = new Color(0.02f, 0.05f, 0.10f, 0.95f);

        Outline scOutline = showcaseObj.AddComponent<Outline>();
        scOutline.effectColor = new Color(borderColor.r, borderColor.g, borderColor.b, 0.65f);
        scOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Uçak Resmi
        GameObject planeImgObj = new GameObject("PlaneSpriteImage");
        planeImgObj.transform.SetParent(showcaseObj.transform, false);
        RectTransform imgRt = GetRect(planeImgObj);
        imgRt.anchorMin = new Vector2(0.5f, 0.5f);
        imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        imgRt.anchoredPosition = new Vector2(0f, 10f);
        imgRt.sizeDelta = new Vector2(130f, 130f);

        Image planeImg = planeImgObj.AddComponent<Image>();
        planeImg.sprite = HangarManager.GetAircraftSprite(type);
        planeImg.preserveAspect = true;

        // Model Rol Rozeti
        GameObject tagObj = new GameObject("PlaneTag");
        tagObj.transform.SetParent(showcaseObj.transform, false);
        RectTransform tagRt = GetRect(tagObj);
        tagRt.anchorMin = new Vector2(0f, 0f);
        tagRt.anchorMax = new Vector2(1f, 0.22f);
        tagRt.offsetMin = Vector2.zero;
        tagRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tagTMP = tagObj.AddComponent<TextMeshProUGUI>();
        tagTMP.text = $"[ {badgeTag} ]";
        tagTMP.fontSize = 12f;
        tagTMP.fontStyle = FontStyles.Bold;
        tagTMP.alignment = TextAlignmentOptions.Center;
        tagTMP.color = borderColor;

        // 3. Teknik Özellikler
        GameObject statObj = new GameObject("Stats");
        statObj.transform.SetParent(cardObj.transform, false);
        RectTransform sRt = GetRect(statObj);
        sRt.anchorMin = new Vector2(0.06f, 0.16f);
        sRt.anchorMax = new Vector2(0.94f, 0.50f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        TextMeshProUGUI sTMP = statObj.AddComponent<TextMeshProUGUI>();
        sTMP.text = stats;
        sTMP.fontSize = 15f;
        sTMP.lineSpacing = 2.5f;
        sTMP.alignment = TextAlignmentOptions.Center;
        sTMP.color = new Color(0.9f, 0.95f, 1f);

        // 4. Göreve Seç / Kilidi Aç Butonu
        GameObject btnObj = new GameObject("ActionButton");
        btnObj.transform.SetParent(cardObj.transform, false);
        RectTransform bRt = GetRect(btnObj);
        bRt.anchorMin = new Vector2(0.08f, 0.035f);
        bRt.anchorMax = new Vector2(0.92f, 0.145f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.55f, 0.32f);

        Button actionBtn = btnObj.AddComponent<Button>();
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

        TextMeshProUGUI actionBtnTMP = btnTxtObj.AddComponent<TextMeshProUGUI>();
        actionBtnTMP.text = "GÖREVE SEÇ";
        actionBtnTMP.fontSize = 18f;
        actionBtnTMP.fontStyle = FontStyles.Bold;
        actionBtnTMP.alignment = TextAlignmentOptions.Center;
        actionBtnTMP.color = Color.white;

        _planeCards.Add(new PlaneCardRef
        {
            type = type,
            actionBtn = actionBtn,
            actionBtnTMP = actionBtnTMP
        });
    }

    // ==================== TAB 2: DESTEK DRONELARI (5 AŞAMALI GÜÇ DÜZEYİ & DAYANIKLILIK) ====================
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
            "KADEME 1 (SEV. 1+)");

        CreateDroneCard("MK2InterceptorCard", -1f * spacingX, cardY,
            "MK-2 AVCI",
            "SİBER LAZER SALVOSU",
            "Hasar: 8 DMG\nAtış: Çift Namlu 0.45s\nÖzellik: Keskin Siber Lazer",
            new Color(0.0f, 0.85f, 1f),
            DroneCompanion.DroneType.MK2_Interceptor,
            "KADEME 2 (SEV. 20+)");

        CreateDroneCard("MK3StrikerCard", 0f, cardY,
            "MK-3 TAARRUZ",
            "AĞIR KİNETİK TOP",
            "Hasar: 14 DMG\nAtış Aralığı: 0.70s\nÖzellik: Zırh Delici Mermi",
            new Color(0.95f, 0.25f, 0.2f),
            DroneCompanion.DroneType.MK3_Striker,
            "KADEME 3 (SEV. 30+)");

        CreateDroneCard("MK4MissileCard", 1f * spacingX, cardY,
            "MK-4 VALKYRIE",
            "GÜDÜMLÜ MİKRO ROKET",
            "Hasar: 26 DMG\nAtış: Isı Güdümlü 2.2s\nÖzellik: Düşmanı Takip Eder",
            new Color(1f, 0.55f, 0.12f),
            DroneCompanion.DroneType.MK4_Missile,
            "KADEME 4 (SEV. 50+)");

        CreateDroneCard("MK5TitanCard", 2f * spacingX, cardY,
            "MK-5 TİTAN AEGIS",
            "APEX KALKAN & PLAZMA",
            "Hasar: 18 DMG Plazma\nBariyer: 1.55m Savunma\nÖzellik: Mermileri Yutar",
            new Color(0.75f, 0.35f, 1f),
            DroneCompanion.DroneType.MK5_TitanAegis,
            "KADEME 5 (SEV. 75+)");
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
        iconRt.sizeDelta = new Vector2(105f, 105f);

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

        // 2. ASKERİ MAĞAZA VE DONATIM BUTONU
        GameObject storeBtnObj = new GameObject("OpenMilitaryStoreButton");
        storeBtnObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform stRt = GetRect(storeBtnObj);
        stRt.anchorMin = new Vector2(1f, 0f);
        stRt.anchorMax = new Vector2(1f, 0f);
        stRt.pivot = new Vector2(1f, 0f);
        stRt.anchoredPosition = new Vector2(-640f, 25f);
        stRt.sizeDelta = new Vector2(280f, 54f);

        Image stImg = storeBtnObj.AddComponent<Image>();
        stImg.color = new Color(0.15f, 0.58f, 0.65f);

        Outline stOutline = storeBtnObj.AddComponent<Outline>();
        stOutline.effectColor = new Color(0.38f, 0.85f, 0.95f);
        stOutline.effectDistance = new Vector2(2f, -2f);

        Button stBtn = storeBtnObj.AddComponent<Button>();
        stBtn.onClick.AddListener(() =>
        {
            CloseDirectly();
            MilitaryStoreUI.EnsureUI()?.OpenStore();
        });

        GameObject stTxtObj = new GameObject("Text");
        stTxtObj.transform.SetParent(storeBtnObj.transform, false);
        MakeFullScreenRect(GetRect(stTxtObj));

        TextMeshProUGUI stTMP = stTxtObj.AddComponent<TextMeshProUGUI>();
        stTMP.text = "🛒 ASKERİ MAĞAZA";
        stTMP.fontSize = 20f;
        stTMP.fontStyle = FontStyles.Bold;
        stTMP.alignment = TextAlignmentOptions.Center;
        stTMP.color = Color.white;

        // 3. ASKERİ ENVANTERİ AÇ BUTONU (ÖNCEKİ MENÜ KAPANIR, YENİ SAYFADA AÇILIR)
        GameObject invBtnObj = new GameObject("OpenInventoryButton");
        invBtnObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform iRt = GetRect(invBtnObj);
        iRt.anchorMin = new Vector2(1f, 0f);
        iRt.anchorMax = new Vector2(1f, 0f);
        iRt.pivot = new Vector2(1f, 0f);
        iRt.anchoredPosition = new Vector2(-340f, 25f);
        iRt.sizeDelta = new Vector2(280f, 54f);

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
        iTMP.text = "ASKERİ ENVANTER";
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
        cTMP.text = "İKMAL SANDIKLARI";
        cTMP.fontSize = 22f;
        cTMP.fontStyle = FontStyles.Bold;
        cTMP.alignment = TextAlignmentOptions.Center;
        cTMP.color = Color.white;

        // Geri Dön Butonunu hiyerarşide en öne al
        backBtnObj.transform.SetAsLastSibling();
    }
}
