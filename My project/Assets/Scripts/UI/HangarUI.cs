using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Askeri Hangar ve Atölye Arayüzü (Hangar UI):
/// - Uçak Filosu Seçimi ve Satın Alımı (F-16 Viper, A-10 Tank Buster, F-22 Raptor)
/// - Kalıcı Atölye Geliştirmeleri (Zırh, Hasar, Hız, Kritik)
/// - Altın Kasası ve Dinamik Kart Güncellemesi
/// </summary>
public class HangarUI : MonoBehaviour
{
    public static HangarUI Instance { get; private set; }

    private GameObject _panelRoot;
    private TextMeshProUGUI _goldTMP;

    // Uçak Kart Butonları & Metinleri
    private Button _f16Btn, _a10Btn, _f22Btn;
    private TextMeshProUGUI _f16BtnTMP, _a10BtnTMP, _f22BtnTMP;

    // Geliştirme Satır Butonları & Seviye Metinleri
    private TextMeshProUGUI _armorLvlTMP, _damageLvlTMP, _speedLvlTMP, _critLvlTMP;
    private Button _armorUpgBtn, _damageUpgBtn, _speedUpgBtn, _critUpgBtn;
    private TextMeshProUGUI _armorCostTMP, _damageCostTMP, _speedCostTMP, _critCostTMP;

    private void Awake()
    {
        Instance = this;
    }

    public static HangarUI EnsureUI()
    {
        if (Instance != null) return Instance;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        GameObject go = new GameObject("HangarUI");
        go.transform.SetParent(canvas.transform, false);
        Instance = go.AddComponent<HangarUI>();
        Instance.BuildUI();
        return Instance;
    }

    public void OpenHangar()
    {
        if (_panelRoot == null)
            BuildUI();

        _panelRoot.SetActive(true);
        RefreshAllVisuals();
    }

    public void CloseHangar()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        menu?.OpenMainPanel();
    }

    public void RefreshAllVisuals()
    {
        HangarManager hm = HangarManager.EnsureInstance();
        hm.LoadHangarData();

        // 1. Altın Kasası
        if (_goldTMP != null)
            _goldTMP.text = $"ALTIN: {hm.gold:N0} G";

        // 2. Uçak Kartları
        UpdatePlaneCard(HangarManager.AircraftType.F16Viper, _f16Btn, _f16BtnTMP, 0);
        UpdatePlaneCard(HangarManager.AircraftType.A10TankBuster, _a10Btn, _a10BtnTMP, HangarManager.A10_UNLOCK_COST);
        UpdatePlaneCard(HangarManager.AircraftType.F22Raptor, _f22Btn, _f22BtnTMP, HangarManager.F22_UNLOCK_COST);

        // 3. Atölye Geliştirme Seviyeleri & Butonları
        UpdateUpgradeRow("ZIRH", hm.armorLevel, HangarManager.ArmorCosts, _armorLvlTMP, _armorUpgBtn, _armorCostTMP, hm.gold);
        UpdateUpgradeRow("HASAR", hm.damageLevel, HangarManager.DamageCosts, _damageLvlTMP, _damageUpgBtn, _damageCostTMP, hm.gold);
        UpdateUpgradeRow("HIZ", hm.speedLevel, HangarManager.SpeedCosts, _speedLvlTMP, _speedUpgBtn, _speedCostTMP, hm.gold);
        UpdateUpgradeRow("KRİTİK", hm.criticalLevel, HangarManager.CritCosts, _critLvlTMP, _critUpgBtn, _critCostTMP, hm.gold);
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
            btnTMP.text = "✓ SEÇİLDİ";
            btnTMP.color = Color.white;
            if (bg != null) bg.color = new Color(0.18f, 0.55f, 0.32f);
        }
        else if (isUnlocked)
        {
            btnTMP.text = "GÖREVE SEÇ";
            btnTMP.color = Color.white;
            if (bg != null) bg.color = new Color(0.15f, 0.40f, 0.65f);
        }
        else
        {
            btnTMP.text = $"KİLİDİ AÇ ({unlockCost}G)";
            btnTMP.color = (hm.gold >= unlockCost) ? Color.yellow : new Color(0.8f, 0.5f, 0.5f);
            if (bg != null) bg.color = new Color(0.35f, 0.22f, 0.15f);
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
                costTMP.text = "MAKS";
                upgBtn.interactable = false;
                upgBtn.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
            }
            else
            {
                int cost = costs[level];
                costTMP.text = $"GELİŞTİR ({cost}G)";
                bool canAfford = (currentGold >= cost);
                upgBtn.interactable = canAfford;
                upgBtn.GetComponent<Image>().color = canAfford ? new Color(0.18f, 0.52f, 0.30f) : new Color(0.32f, 0.25f, 0.25f);
            }
        }
    }

    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        _panelRoot = new GameObject("HangarPanelRoot");
        _panelRoot.transform.SetParent(transform, false);

        RectTransform rootRt = _panelRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image panelBg = _panelRoot.AddComponent<Image>();
        panelBg.color = new Color(0.03f, 0.06f, 0.11f, 0.98f);

        // 1. Üst Başlık & Kasa Rozeti
        GameObject headerObj = new GameObject("HeaderTitle");
        headerObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform hRt = headerObj.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.5f, 1f);
        hRt.anchorMax = new Vector2(0.5f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -25f);
        hRt.sizeDelta = new Vector2(900f, 60f);

        TextMeshProUGUI titleTMP = headerObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "ASKERİ HANGAR VE ATÖLYE";
        titleTMP.fontSize = 36f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0.95f, 0.95f, 1f);

        // Kasa Altın Metni
        GameObject goldObj = new GameObject("GoldText");
        goldObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform gRt = goldObj.AddComponent<RectTransform>();
        gRt.anchorMin = new Vector2(1f, 1f);
        gRt.anchorMax = new Vector2(1f, 1f);
        gRt.pivot = new Vector2(1f, 1f);
        gRt.anchoredPosition = new Vector2(-40f, -30f);
        gRt.sizeDelta = new Vector2(300f, 50f);

        _goldTMP = goldObj.AddComponent<TextMeshProUGUI>();
        _goldTMP.fontSize = 26f;
        _goldTMP.fontStyle = FontStyles.Bold;
        _goldTMP.alignment = TextAlignmentOptions.Right;
        _goldTMP.color = new Color(1f, 0.85f, 0.2f);

        // 2. 3 Uçak Kartı (Yan Yana)
        float cardY = 110f;
        CreatePlaneCard("F16_Card", -340f, cardY, "F-16 VIPER", "[DENGELİ AVCI]", "Can: 100\nHız: 8.0\nHasar: 20", new Color(0.2f, 0.4f, 0.6f), HangarManager.AircraftType.F16Viper, out _f16Btn, out _f16BtnTMP);
        CreatePlaneCard("A10_Card", 0f, cardY, "A-10 TANK BUSTER", "[ZIRHLI TANK]", "Can: 160\nHız: 6.2\nHasar: 28", new Color(0.2f, 0.5f, 0.3f), HangarManager.AircraftType.A10TankBuster, out _a10Btn, out _a10BtnTMP);
        CreatePlaneCard("F22_Card", 340f, cardY, "F-22 RAPTOR", "[HAYALET JET]", "Can: 80\nHız: 10.5\nHasar: 22 (Çift)", new Color(0.45f, 0.25f, 0.55f), HangarManager.AircraftType.F22Raptor, out _f22Btn, out _f22BtnTMP);

        // 3. Kalıcı Atölye Satırları (Altta)
        float upgStartY = -110f;
        float rowSpacing = 52f;
        CreateUpgradeRow("[ZIRH] TİTANYUM GÖVDE (+15 HP)", upgStartY, out _armorLvlTMP, out _armorUpgBtn, out _armorCostTMP, () => { if (HangarManager.Instance.TryUpgradeArmor()) RefreshAllVisuals(); });
        CreateUpgradeRow("[HASAR] PLAZMA NAMLULAR (+3 DMG)", upgStartY - rowSpacing, out _damageLvlTMP, out _damageUpgBtn, out _damageCostTMP, () => { if (HangarManager.Instance.TryUpgradeDamage()) RefreshAllVisuals(); });
        CreateUpgradeRow("[HIZ] TURBO İTİCİ (+0.6 HIZ)", upgStartY - rowSpacing * 2, out _speedLvlTMP, out _speedUpgBtn, out _speedCostTMP, () => { if (HangarManager.Instance.TryUpgradeSpeed()) RefreshAllVisuals(); });
        CreateUpgradeRow("[KRİTİK] HEDEFLEME NİŞANGAHI (+%6)", upgStartY - rowSpacing * 3, out _critLvlTMP, out _critUpgBtn, out _critCostTMP, () => { if (HangarManager.Instance.TryUpgradeCritical()) RefreshAllVisuals(); });

        // 4. Geri Dön Butonu
        GameObject backBtnObj = new GameObject("BackFromHangarButton");
        backBtnObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform bRt = backBtnObj.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 0f);
        bRt.anchorMax = new Vector2(0.5f, 0f);
        bRt.pivot = new Vector2(0.5f, 0f);
        bRt.anchoredPosition = new Vector2(0f, 35f);
        bRt.sizeDelta = new Vector2(320f, 52f);

        Image bImg = backBtnObj.AddComponent<Image>();
        bImg.color = new Color(0.35f, 0.35f, 0.35f);

        Button bBtn = backBtnObj.AddComponent<Button>();
        bBtn.onClick.AddListener(CloseHangar);

        GameObject bTxtObj = new GameObject("Text");
        bTxtObj.transform.SetParent(backBtnObj.transform, false);
        RectTransform bTxtRt = bTxtObj.AddComponent<RectTransform>();
        bTxtRt.anchorMin = Vector2.zero;
        bTxtRt.anchorMax = Vector2.one;
        bTxtRt.offsetMin = Vector2.zero;
        bTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI bTMP = bTxtObj.AddComponent<TextMeshProUGUI>();
        bTMP.text = "GERİ DÖN";
        bTMP.fontSize = 24f;
        bTMP.fontStyle = FontStyles.Bold;
        bTMP.alignment = TextAlignmentOptions.Center;
        bTMP.color = Color.white;

        _panelRoot.SetActive(false);
    }

    private void CreatePlaneCard(string objName, float posX, float posY, string name, string subtitle, string stats, Color borderColor, HangarManager.AircraftType type, out Button actionBtn, out TextMeshProUGUI actionBtnTMP)
    {
        GameObject cardObj = new GameObject(objName);
        cardObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform cRt = cardObj.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(posX, posY);
        cRt.sizeDelta = new Vector2(300f, 215f);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.08f, 0.12f, 0.20f, 0.95f);

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        // Kart Başlığı
        GameObject titleObj = new GameObject("CardTitle");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = titleObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0.65f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tTMP = titleObj.AddComponent<TextMeshProUGUI>();
        tTMP.text = $"<b>{name}</b>\n<size=70%><color=#8AB4F8>{subtitle}</color></size>";
        tTMP.fontSize = 20f;
        tTMP.alignment = TextAlignmentOptions.Center;
        tTMP.color = Color.white;

        // Statlar
        GameObject statObj = new GameObject("Stats");
        statObj.transform.SetParent(cardObj.transform, false);
        RectTransform sRt = statObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0.25f);
        sRt.anchorMax = new Vector2(1f, 0.68f);
        sRt.offsetMin = new Vector2(12f, 0f);
        sRt.offsetMax = new Vector2(-12f, 0f);

        TextMeshProUGUI sTMP = statObj.AddComponent<TextMeshProUGUI>();
        sTMP.text = stats;
        sTMP.fontSize = 18f;
        sTMP.fontStyle = FontStyles.Bold;
        sTMP.alignment = TextAlignmentOptions.Center;
        sTMP.color = new Color(0.9f, 0.95f, 1f);

        // Seçim / Satın Alma Butonu
        GameObject btnObj = new GameObject("ActionButton");
        btnObj.transform.SetParent(cardObj.transform, false);
        RectTransform bRt = btnObj.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.08f, 0.06f);
        bRt.anchorMax = new Vector2(0.92f, 0.26f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.55f, 0.32f);

        actionBtn = btnObj.AddComponent<Button>();
        actionBtn.targetGraphic = btnImg;

        GameObject btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        RectTransform btRt = btnTxtObj.AddComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        actionBtnTMP = btnTxtObj.AddComponent<TextMeshProUGUI>();
        actionBtnTMP.text = "SEÇ";
        actionBtnTMP.fontSize = 17f;
        actionBtnTMP.fontStyle = FontStyles.Bold;
        actionBtnTMP.alignment = TextAlignmentOptions.Center;
        actionBtnTMP.color = Color.white;

        HangarManager.AircraftType capturedType = type;
        actionBtn.onClick.AddListener(() =>
        {
            HangarManager hm = HangarManager.EnsureInstance();
            if (hm.IsAircraftUnlocked(capturedType))
            {
                hm.SelectAircraft(capturedType);
                RefreshAllVisuals();
            }
            else
            {
                if (hm.TryUnlockAircraft(capturedType))
                {
                    RefreshAllVisuals();
                }
            }
        });
    }

    private void CreateUpgradeRow(string title, float posY, out TextMeshProUGUI lvlTMP, out Button upgBtn, out TextMeshProUGUI costTMP, UnityEngine.Events.UnityAction onUpgrade)
    {
        GameObject rowObj = new GameObject("Row_" + title);
        rowObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform rRt = rowObj.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.5f, 0.5f);
        rRt.anchorMax = new Vector2(0.5f, 0.5f);
        rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.anchoredPosition = new Vector2(0f, posY);
        rRt.sizeDelta = new Vector2(850f, 44f);

        Image bg = rowObj.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.10f, 0.16f, 0.85f);

        // Başlık
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(rowObj.transform, false);
        RectTransform tRt = tObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.02f, 0f);
        tRt.anchorMax = new Vector2(0.48f, 1f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tTMP = tObj.AddComponent<TextMeshProUGUI>();
        tTMP.text = title;
        tTMP.fontSize = 20f;
        tTMP.fontStyle = FontStyles.Bold;
        tTMP.alignment = TextAlignmentOptions.Left;
        tTMP.color = Color.white;

        // Seviye Göstergesi
        GameObject lObj = new GameObject("LevelText");
        lObj.transform.SetParent(rowObj.transform, false);
        RectTransform lRt = lObj.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0.48f, 0f);
        lRt.anchorMax = new Vector2(0.74f, 1f);
        lRt.offsetMin = Vector2.zero;
        lRt.offsetMax = Vector2.zero;

        lvlTMP = lObj.AddComponent<TextMeshProUGUI>();
        lvlTMP.fontSize = 19f;
        lvlTMP.fontStyle = FontStyles.Bold;
        lvlTMP.alignment = TextAlignmentOptions.Center;
        lvlTMP.color = new Color(0.2f, 0.9f, 1f);

        // Yükselt Butonu
        GameObject bObj = new GameObject("UpgradeButton");
        bObj.transform.SetParent(rowObj.transform, false);
        RectTransform bRt = bObj.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.76f, 0.1f);
        bRt.anchorMax = new Vector2(0.98f, 0.9f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        Image bImg = bObj.AddComponent<Image>();
        bImg.color = new Color(0.18f, 0.52f, 0.30f);

        upgBtn = bObj.AddComponent<Button>();
        upgBtn.targetGraphic = bImg;
        upgBtn.onClick.AddListener(onUpgrade);

        GameObject cObj = new GameObject("Text");
        cObj.transform.SetParent(bObj.transform, false);
        RectTransform cRt = cObj.AddComponent<RectTransform>();
        cRt.anchorMin = Vector2.zero;
        cRt.anchorMax = Vector2.one;
        cRt.offsetMin = Vector2.zero;
        cRt.offsetMax = Vector2.zero;

        costTMP = cObj.AddComponent<TextMeshProUGUI>();
        costTMP.fontSize = 17f;
        costTMP.fontStyle = FontStyles.Bold;
        costTMP.alignment = TextAlignmentOptions.Center;
        costTMP.color = Color.white;
    }
}
