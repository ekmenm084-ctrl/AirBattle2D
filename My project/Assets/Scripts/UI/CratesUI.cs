using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Askeri Hava İkmal ve Ganimet Sandıkları Arayüzü:
/// Gerçekçi 2D Askeri Sandık Görselleri (Bronz, Gümüş, Altın As),
/// Sinematik Sandık Sallanma, Açılma ve Işık Patlaması Animasyonu,
/// Kalıcı As Çipleri, Uçak Kilitleri ve Altın İkramiyeleri.
/// </summary>
public class CratesUI : MonoBehaviour
{
    public static CratesUI Instance { get; private set; }

    private GameObject _panelRoot;
    private TextMeshProUGUI _goldTMP;
    private Button _bronzeBtn, _silverBtn, _goldBtn;
    private TextMeshProUGUI _bronzeBtnTMP, _silverBtnTMP, _goldBtnTMP;

    // Kartlardaki Sandık Görselleri
    private Image _bronzeCardImg, _silverCardImg, _goldCardImg;

    // Sinematik Sandık Açma Sahnesi
    private GameObject _cinematicOverlayObj;
    private Image _cinematicSunburstImg;
    private Image _cinematicCrateImg;
    private RectTransform _cinematicCrateRt;
    private GameObject _cinematicRewardCardObj;
    private RectTransform _cinematicRewardCardRt;
    private TextMeshProUGUI _rewardTitleTMP, _rewardDescTMP;
    private Outline _rewardCardBorder;
    private Button _claimRewardBtn;
    private bool _isOpeningCrate = false;

    private Action _onBackCallback;

    // Önbelleğe alınmış Sandık Dokuları
    private static Sprite[] _cachedClosedSprites;
    private static Sprite[] _cachedOpenSprites;
    private static Sprite _cachedSunburstSprite;

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

    public static CratesUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("CratesUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("CratesUI");
        go.transform.SetParent(canvas.transform, false);
        MakeFullScreenRect(GetRect(go));
        Instance = go.GetComponent<CratesUI>();
        if (Instance == null) Instance = go.AddComponent<CratesUI>();
        if (Instance._panelRoot == null) Instance.BuildUI();
        return Instance;
    }

    public void OpenCratesPanel(Action onBack = null)
    {
        if (onBack != null) _onBackCallback = onBack;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        MakeFullScreenRect(GetRect(gameObject));

        if (_panelRoot == null)
            BuildUI();

        MakeFullScreenRect(GetRect(_panelRoot));
        _panelRoot.SetActive(true);
        PlayerProfileUI.EnsureUI()?.ShowMenuStatusBar();
        if (_cinematicOverlayObj != null) _cinematicOverlayObj.SetActive(false);
        _isOpeningCrate = false;
        RefreshVisuals();
    }

    public void CloseCratesPanel()
    {
        if (_isOpeningCrate) return; // Açılış animasyonundayken kapatma engellenir

        CloseDirectly();

        if (_onBackCallback != null)
        {
            Action callback = _onBackCallback;
            _onBackCallback = null;
            callback.Invoke();
        }
        else
        {
            MainMenuManager.Instance?.OpenMainPanel();
            if (MainMenuManager.Instance == null)
            {
                MainMenuManager menu = FindObjectOfType<MainMenuManager>();
                menu?.OpenMainPanel();
            }
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

    public void RefreshVisuals()
    {
        HangarManager hm = HangarManager.EnsureInstance();
        hm.LoadHangarData();

        if (_goldTMP != null)
            _goldTMP.text = $"ALTIN: {hm.gold:N0} G";

        UpdateCrateButton(_bronzeBtn, _bronzeBtnTMP, HangarManager.CRATE_BRONZE_COST, hm.gold);
        UpdateCrateButton(_silverBtn, _silverBtnTMP, HangarManager.CRATE_SILVER_COST, hm.gold);
        UpdateCrateButton(_goldBtn, _goldBtnTMP, HangarManager.CRATE_GOLD_COST, hm.gold);
    }

    private void UpdateCrateButton(Button btn, TextMeshProUGUI tmp, int cost, int currentGold)
    {
        if (btn == null || tmp == null) return;
        bool canAfford = (currentGold >= cost) && !_isOpeningCrate;
        btn.interactable = canAfford;
        tmp.text = canAfford ? $"SANDIĞI AÇ ({cost:N0}G)" : $"YETERSİZ ALTIN ({cost:N0}G)";
        tmp.color = canAfford ? Color.white : new Color(0.9f, 0.6f, 0.6f);

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = canAfford ? new Color(0.18f, 0.60f, 0.35f) : new Color(0.35f, 0.25f, 0.22f);
        }
    }

    // ==================== UI YAPISI ====================
    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        _panelRoot = new GameObject("CratesPanelRoot");
        _panelRoot.transform.SetParent(transform, false);

        RectTransform rootRt = GetRect(_panelRoot);
        MakeFullScreenRect(rootRt);

        Image panelBg = _panelRoot.AddComponent<Image>();
        panelBg.color = new Color(0.02f, 0.05f, 0.09f, 1.0f);

        // 1. ÜST PANEL & ALTIN GÖSTERGESİ
        BuildHeader();

        // 2. GERÇEK 3 SANDIK KARTI (GÖRSEL SANDIK + DETAYLAR)
        BuildCrateCards();

        // 3. ALT NAVİGASYON (GERİ DÖN & HANGAR ENVANTERİ BUTONLARI)
        BuildBottomBar();

        // 4. SİNEMATİK SANDIK AÇILMA SAHNESİ
        BuildCinematicOpeningStage();

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
        hRt.sizeDelta = new Vector2(1100f, 75f);

        TextMeshProUGUI titleTMP = headerObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "<b>ASKERİ HAVA İKMAL VE GANİMET SANDIKLARI</b>\n<size=58%><color=#8AB4F8>Sandıkları açarak kalıcı As çipleri ve ikramiyeler kazanın. Tüm ödüller doğrudan Hangar Envanterine aktarılır!</color></size>";
        titleTMP.fontSize = 28f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;

        // Üst Sağda Belirgin Altın Gösterge Rozeti
        GameObject goldBadgeObj = new GameObject("GoldBadge");
        goldBadgeObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform gRt = GetRect(goldBadgeObj);
        gRt.anchorMin = new Vector2(1f, 1f);
        gRt.anchorMax = new Vector2(1f, 1f);
        gRt.pivot = new Vector2(1f, 1f);
        gRt.anchoredPosition = new Vector2(-40f, -22f);
        gRt.sizeDelta = new Vector2(320f, 54f);

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

    private void BuildCrateCards()
    {
        float cardY = 15f;
        float spacingX = 490f;

        CreateCrateCard("BronzeCard", -spacingX, cardY,
            "BRONZ İKMAL KUTUSU",
            $"{HangarManager.CRATE_BRONZE_COST} ALTIN",
            "• 100 - 250 Altın İkramiyesi\n• Taktik Mermi Çipi (+1.0 DMG)\n• Çelik Gövde Çipi (+10 HP)\n• Dayanıklı Askeri Mühimmat Sandığı",
            new Color(0.2f, 0.85f, 0.45f),
            HangarManager.CrateType.Bronze,
            out _bronzeBtn, out _bronzeBtnTMP, out _bronzeCardImg);

        CreateCrateCard("SilverCard", 0f, cardY,
            "GÜMÜŞ CEPHANE KASASI",
            $"{HangarManager.CRATE_SILVER_COST} ALTIN",
            "• 400 - 900 Altın İkramiyesi\n• Drone Dayanıklılık & Bakım Kiti\n• Gelişmiş As Çipi (+2.5 DMG / +25 HP)\n• Ağır Titanyum Siber Güvenlik Kasası",
            new Color(0.25f, 0.75f, 1f),
            HangarManager.CrateType.Silver,
            out _silverBtn, out _silverBtnTMP, out _silverCardImg);

        CreateCrateCard("GoldCard", spacingX, cardY,
            "ALTIN AS SANDIĞI",
            $"{HangarManager.CRATE_GOLD_COST} ALTIN",
            "• 1.200 - 2.500 Altın Dev İkramiye\n• Bedava A-10 / F-22 Uçak Kilidi İhtimali!\n• Efsanevi As Protokolü (+5 DMG / +35 HP)\n• 24K Altın Kaplama Lüks Apex Sandık",
            new Color(1f, 0.85f, 0.2f),
            HangarManager.CrateType.GoldAce,
            out _goldBtn, out _goldBtnTMP, out _goldCardImg);
    }

    private void CreateCrateCard(string objName, float posX, float posY, string title, string costSub, string lootDetails,
        Color accentColor, HangarManager.CrateType type, out Button openBtn, out TextMeshProUGUI btnTMP, out Image crateImgRef)
    {
        GameObject cardObj = new GameObject(objName);
        cardObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform cRt = GetRect(cardObj);
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(posX, posY);
        cRt.sizeDelta = new Vector2(440f, 545f);

        Image cBg = cardObj.AddComponent<Image>();
        cBg.color = new Color(0.06f, 0.10f, 0.17f, 0.95f);

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(3f, -3f);

        // 1. Başlık ve Fiyat Kutusu
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = GetRect(tObj);
        tRt.anchorMin = new Vector2(0f, 0.84f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tTMP = tObj.AddComponent<TextMeshProUGUI>();
        tTMP.text = $"<b>{title}</b>\n<size=75%><color=#FFE082>{costSub}</color></size>";
        tTMP.fontSize = 22f;
        tTMP.alignment = TextAlignmentOptions.Center;
        tTMP.color = Color.white;

        // 2. GERÇEK ASKERİ SANDIK GÖRSELİ (Büyük ve Detaylı)
        GameObject crateObj = new GameObject("CrateGraphic");
        crateObj.transform.SetParent(cardObj.transform, false);
        RectTransform crtRt = GetRect(crateObj);
        crtRt.anchorMin = new Vector2(0.5f, 0.65f);
        crtRt.anchorMax = new Vector2(0.5f, 0.65f);
        crtRt.pivot = new Vector2(0.5f, 0.5f);
        crtRt.anchoredPosition = Vector2.zero;
        crtRt.sizeDelta = new Vector2(210f, 210f);

        crateImgRef = crateObj.AddComponent<Image>();
        crateImgRef.sprite = GetCrateSprite(type, isOpen: false);
        crateImgRef.preserveAspect = true;

        // Sandık arkasında hafif atmosferik parlama efekti
        GameObject glowObj = new GameObject("GlowBackdrop");
        glowObj.transform.SetParent(crateObj.transform, false);
        glowObj.transform.SetAsFirstSibling();
        RectTransform gRt = GetRect(glowObj);
        gRt.anchorMin = new Vector2(0.5f, 0.5f);
        gRt.anchorMax = new Vector2(0.5f, 0.5f);
        gRt.sizeDelta = new Vector2(230f, 230f);
        Image glowImg = glowObj.AddComponent<Image>();
        glowImg.sprite = GetSunburstSprite();
        glowImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.22f);

        // 3. Sandık İçerik Açıklaması
        GameObject dObj = new GameObject("LootDetails");
        dObj.transform.SetParent(cardObj.transform, false);
        RectTransform dRt = GetRect(dObj);
        dRt.anchorMin = new Vector2(0.06f, 0.16f);
        dRt.anchorMax = new Vector2(0.94f, 0.44f);
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;

        TextMeshProUGUI dTMP = dObj.AddComponent<TextMeshProUGUI>();
        dTMP.text = lootDetails;
        dTMP.fontSize = 18f;
        dTMP.lineSpacing = 5f;
        dTMP.alignment = TextAlignmentOptions.Center;
        dTMP.color = new Color(0.9f, 0.95f, 1f);

        // 4. Sandığı Aç Butonu
        GameObject bObj = new GameObject("OpenBtn");
        bObj.transform.SetParent(cardObj.transform, false);
        RectTransform bRt = GetRect(bObj);
        bRt.anchorMin = new Vector2(0.08f, 0.03f);
        bRt.anchorMax = new Vector2(0.92f, 0.14f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        Image bImg = bObj.AddComponent<Image>();
        bImg.color = new Color(0.18f, 0.60f, 0.35f);

        openBtn = bObj.AddComponent<Button>();
        HangarManager.CrateType capturedType = type;
        openBtn.onClick.AddListener(() =>
        {
            OpenCrate(capturedType);
        });

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(bObj.transform, false);
        RectTransform trRt = GetRect(txtObj);
        trRt.anchorMin = Vector2.zero;
        trRt.anchorMax = Vector2.one;
        trRt.offsetMin = Vector2.zero;
        trRt.offsetMax = Vector2.zero;

        btnTMP = txtObj.AddComponent<TextMeshProUGUI>();
        btnTMP.text = "SANDIĞI AÇ";
        btnTMP.fontSize = 20f;
        btnTMP.fontStyle = FontStyles.Bold;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = Color.white;
    }

    private void BuildBottomBar()
    {
        // 1. GERİ DÖN BUTONU
        GameObject backBtnObj = new GameObject("BackFromCratesButton");
        backBtnObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform bRt = GetRect(backBtnObj);
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.zero;
        bRt.pivot = Vector2.zero;
        bRt.anchoredPosition = new Vector2(40f, 22f);
        bRt.sizeDelta = new Vector2(260f, 54f);

        Image bImg = backBtnObj.AddComponent<Image>();
        bImg.color = new Color(0.20f, 0.30f, 0.44f);

        Outline bOutline = backBtnObj.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.35f, 0.75f, 1f);
        bOutline.effectDistance = new Vector2(2f, -2f);

        Button bBtn = backBtnObj.AddComponent<Button>();
        bBtn.onClick.AddListener(CloseCratesPanel);

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

        // 2. HANGAR ENVANTERİNİ GÖR BUTONU
        GameObject invBtnObj = new GameObject("GoToInventoryButton");
        invBtnObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform iRt = GetRect(invBtnObj);
        iRt.anchorMin = new Vector2(1f, 0f);
        iRt.anchorMax = new Vector2(1f, 0f);
        iRt.pivot = new Vector2(1f, 0f);
        iRt.anchoredPosition = new Vector2(-40f, 22f);
        iRt.sizeDelta = new Vector2(300f, 54f);

        Image iImg = invBtnObj.AddComponent<Image>();
        iImg.color = new Color(0.18f, 0.48f, 0.72f);

        Button iBtn = invBtnObj.AddComponent<Button>();
        iBtn.onClick.AddListener(() =>
        {
            Action savedBack = _onBackCallback;
            CloseDirectly();
            InventoryUI.EnsureUI()?.OpenInventory(() => OpenCratesPanel(savedBack));
        });

        GameObject iTxtObj = new GameObject("Text");
        iTxtObj.transform.SetParent(invBtnObj.transform, false);
        RectTransform iTxtRt = GetRect(iTxtObj);
        iTxtRt.anchorMin = Vector2.zero;
        iTxtRt.anchorMax = Vector2.one;
        iTxtRt.offsetMin = Vector2.zero;
        iTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI iTMP = iTxtObj.AddComponent<TextMeshProUGUI>();
        iTMP.text = "[ENVANTER] HANGAR ENVANTERİ";
        iTMP.fontSize = 20f;
        iTMP.fontStyle = FontStyles.Bold;
        iTMP.alignment = TextAlignmentOptions.Center;
        iTMP.color = Color.white;

        backBtnObj.transform.SetAsLastSibling();
    }

    // ==================== SİNEMATİK SANDIK AÇILMA SAHNESİ ====================
    private void BuildCinematicOpeningStage()
    {
        _cinematicOverlayObj = new GameObject("CinematicCrateOpeningOverlay");
        _cinematicOverlayObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform oRt = GetRect(_cinematicOverlayObj);
        MakeFullScreenRect(oRt);

        Image overlayBg = _cinematicOverlayObj.AddComponent<Image>();
        overlayBg.color = new Color(0f, 0.02f, 0.05f, 0.94f); // Karartılmış odak arka planı

        // 1. Dönen Işık Huzmesi / Sunburst Efekti
        GameObject rayObj = new GameObject("CinematicSunburst");
        rayObj.transform.SetParent(_cinematicOverlayObj.transform, false);
        RectTransform rayRt = GetRect(rayObj);
        rayRt.anchorMin = new Vector2(0.5f, 0.52f);
        rayRt.anchorMax = new Vector2(0.5f, 0.52f);
        rayRt.sizeDelta = new Vector2(750f, 750f);

        _cinematicSunburstImg = rayObj.AddComponent<Image>();
        _cinematicSunburstImg.sprite = GetSunburstSprite();
        _cinematicSunburstImg.color = new Color(1f, 0.88f, 0.35f, 0.35f);

        // 2. Büyük Merkez Sandık Görseli
        GameObject crateCenterObj = new GameObject("CinematicCrateImage");
        crateCenterObj.transform.SetParent(_cinematicOverlayObj.transform, false);
        _cinematicCrateRt = GetRect(crateCenterObj);
        _cinematicCrateRt.anchorMin = new Vector2(0.5f, 0.44f);
        _cinematicCrateRt.anchorMax = new Vector2(0.5f, 0.44f);
        _cinematicCrateRt.pivot = new Vector2(0.5f, 0.5f);
        _cinematicCrateRt.anchoredPosition = Vector2.zero;
        _cinematicCrateRt.sizeDelta = new Vector2(300f, 300f);

        _cinematicCrateImg = crateCenterObj.AddComponent<Image>();
        _cinematicCrateImg.preserveAspect = true;

        // 3. Sandıktan Havaya Yükselen Ödül Kartı
        _cinematicRewardCardObj = new GameObject("RewardFloatingCard");
        _cinematicRewardCardObj.transform.SetParent(_cinematicOverlayObj.transform, false);
        _cinematicRewardCardRt = GetRect(_cinematicRewardCardObj);
        _cinematicRewardCardRt.anchorMin = new Vector2(0.5f, 0.5f);
        _cinematicRewardCardRt.anchorMax = new Vector2(0.5f, 0.5f);
        _cinematicRewardCardRt.pivot = new Vector2(0.5f, 0.5f);
        _cinematicRewardCardRt.anchoredPosition = new Vector2(0f, 150f);
        _cinematicRewardCardRt.sizeDelta = new Vector2(660f, 330f);

        Image cardBg = _cinematicRewardCardObj.AddComponent<Image>();
        cardBg.color = new Color(0.04f, 0.07f, 0.14f, 0.98f);

        _rewardCardBorder = _cinematicRewardCardObj.AddComponent<Outline>();
        _rewardCardBorder.effectColor = Color.yellow;
        _rewardCardBorder.effectDistance = new Vector2(3.5f, -3.5f);

        // Ödül Başlığı
        GameObject tObj = new GameObject("RewardTitle");
        tObj.transform.SetParent(_cinematicRewardCardObj.transform, false);
        RectTransform tRt = GetRect(tObj);
        tRt.anchorMin = new Vector2(0f, 0.65f);
        tRt.anchorMax = new Vector2(1f, 0.96f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        _rewardTitleTMP = tObj.AddComponent<TextMeshProUGUI>();
        _rewardTitleTMP.fontSize = 28f;
        _rewardTitleTMP.fontStyle = FontStyles.Bold;
        _rewardTitleTMP.alignment = TextAlignmentOptions.Center;
        _rewardTitleTMP.color = Color.yellow;

        // Ödül Açıklaması
        GameObject dObj = new GameObject("RewardDesc");
        dObj.transform.SetParent(_cinematicRewardCardObj.transform, false);
        RectTransform dRt = GetRect(dObj);
        dRt.anchorMin = new Vector2(0.05f, 0.28f);
        dRt.anchorMax = new Vector2(0.95f, 0.65f);
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;

        _rewardDescTMP = dObj.AddComponent<TextMeshProUGUI>();
        _rewardDescTMP.fontSize = 22f;
        _rewardDescTMP.lineSpacing = 5f;
        _rewardDescTMP.alignment = TextAlignmentOptions.Center;
        _rewardDescTMP.color = Color.white;

        // Ödülü Al ve Envantere Ekle Butonu
        GameObject okObj = new GameObject("ClaimRewardBtn");
        okObj.transform.SetParent(_cinematicRewardCardObj.transform, false);
        RectTransform okRt = GetRect(okObj);
        okRt.anchorMin = new Vector2(0.22f, 0.06f);
        okRt.anchorMax = new Vector2(0.78f, 0.24f);
        okRt.offsetMin = Vector2.zero;
        okRt.offsetMax = Vector2.zero;

        Image okImg = okObj.AddComponent<Image>();
        okImg.color = new Color(0.18f, 0.65f, 0.35f);

        _claimRewardBtn = okObj.AddComponent<Button>();
        _claimRewardBtn.onClick.AddListener(() =>
        {
            _cinematicOverlayObj.SetActive(false);
            _isOpeningCrate = false;
            RefreshVisuals();
        });

        GameObject okTxt = new GameObject("Text");
        okTxt.transform.SetParent(okObj.transform, false);
        RectTransform otRt = GetRect(okTxt);
        otRt.anchorMin = Vector2.zero;
        otRt.anchorMax = Vector2.one;
        otRt.offsetMin = Vector2.zero;
        otRt.offsetMax = Vector2.zero;

        TextMeshProUGUI okTMP = okTxt.AddComponent<TextMeshProUGUI>();
        okTMP.text = "<b>ÖDÜLÜ AL VE DEVAM ET</b>";
        okTMP.fontSize = 22f;
        okTMP.fontStyle = FontStyles.Bold;
        okTMP.alignment = TextAlignmentOptions.Center;
        okTMP.color = Color.white;

        _cinematicOverlayObj.SetActive(false);
    }

    private void OpenCrate(HangarManager.CrateType type)
    {
        if (_isOpeningCrate) return;

        var reward = HangarManager.Instance.OpenCrate(type);
        if (reward != null)
        {
            StartCoroutine(PlayCrateOpeningSequence(type, reward));
        }
        else
        {
            RefreshVisuals();
        }
    }

    private IEnumerator PlayCrateOpeningSequence(HangarManager.CrateType type, HangarManager.CrateReward reward)
    {
        _isOpeningCrate = true;
        RefreshVisuals();

        ColorUtility.TryParseHtmlString(reward.rarityColorHex, out Color rarityCol);

        // Sahneyi aç
        _cinematicOverlayObj.SetActive(true);
        _cinematicOverlayObj.transform.SetAsLastSibling();

        // 1. Kapalı Sandık Sahnesi
        _cinematicCrateImg.sprite = GetCrateSprite(type, isOpen: false);
        _cinematicCrateRt.anchoredPosition = new Vector2(0f, -40f);
        _cinematicCrateRt.localScale = Vector3.one;
        _cinematicCrateRt.localEulerAngles = Vector3.zero;

        _cinematicSunburstImg.gameObject.SetActive(false);
        _cinematicRewardCardObj.SetActive(false);

        // AŞAMA 1: Heyecan ve Titreme / Sallanma Efekti (0.75 saniye)
        float shakeDuration = 0.75f;
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / shakeDuration;
            float shakeAmp = Mathf.Lerp(2f, 10f, t);
            float angle = Mathf.Sin(elapsed * 45f) * shakeAmp;
            float scaleBob = 1.0f + Mathf.Sin(elapsed * 25f) * 0.06f * t;

            _cinematicCrateRt.localEulerAngles = new Vector3(0f, 0f, angle);
            _cinematicCrateRt.localScale = Vector3.one * scaleBob;

            yield return null;
        }

        // AŞAMA 2: Kapağın Havaya Açılması & Işık Patlaması!
        _cinematicCrateImg.sprite = GetCrateSprite(type, isOpen: true);
        _cinematicCrateRt.localEulerAngles = Vector3.zero;
        _cinematicCrateRt.localScale = Vector3.one * 1.25f; // Pop-up zıplaması

        CameraShake.Shake(0.22f, 0.08f);
        AudioManager.Instance?.PlayRewardFanfare();

        // Işık Huzmesi Parlaması
        _cinematicSunburstImg.gameObject.SetActive(true);
        _cinematicSunburstImg.color = new Color(rarityCol.r, rarityCol.g, rarityCol.b, 0.45f);

        // Hafifçe yerine oturma
        float settleDuration = 0.20f;
        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / settleDuration;
            _cinematicCrateRt.localScale = Vector3.Lerp(Vector3.one * 1.25f, Vector3.one, t);
            _cinematicSunburstImg.transform.Rotate(0f, 0f, 90f * Time.unscaledDeltaTime);
            yield return null;
        }
        _cinematicCrateRt.localScale = Vector3.one;

        // AŞAMA 3: Ödülün Sandıktan Süzülerek Yükselmesi (0.55 saniye)
        _rewardTitleTMP.text = reward.title;
        _rewardTitleTMP.color = rarityCol;
        _rewardDescTMP.text = reward.description;
        if (_rewardCardBorder != null) _rewardCardBorder.effectColor = rarityCol;

        _cinematicRewardCardObj.SetActive(true);

        Vector2 startPos = new Vector2(0f, -30f);
        Vector2 targetPos = new Vector2(0f, 175f);

        float floatDuration = 0.55f;
        elapsed = 0f;
        while (elapsed < floatDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / floatDuration;
            // Smooth ease out bounce
            float smooth = Mathf.Sin(t * Mathf.PI * 0.5f);
            _cinematicRewardCardRt.anchoredPosition = Vector2.Lerp(startPos, targetPos, smooth);
            _cinematicRewardCardRt.localScale = Vector3.Lerp(Vector3.one * 0.3f, Vector3.one, smooth);

            _cinematicSunburstImg.transform.Rotate(0f, 0f, 60f * Time.unscaledDeltaTime);
            yield return null;
        }

        _cinematicRewardCardRt.anchoredPosition = targetPos;
        _cinematicRewardCardRt.localScale = Vector3.one;

        // Arka plandaki ışık huzmesi oyuncu ödülü alana kadar dönmeye devam eder
        while (_cinematicOverlayObj.activeSelf)
        {
            _cinematicSunburstImg.transform.Rotate(0f, 0f, 35f * Time.unscaledDeltaTime);
            yield return null;
        }
    }

    // ==================== PROCEDURAL GERÇEK SANDIK DOKULARI ====================
    public static Sprite GetCrateSprite(HangarManager.CrateType type, bool isOpen)
    {
        if (_cachedClosedSprites == null) _cachedClosedSprites = new Sprite[3];
        if (_cachedOpenSprites == null) _cachedOpenSprites = new Sprite[3];

        int idx = Mathf.Clamp((int)type, 0, 2);
        if (isOpen && _cachedOpenSprites[idx] != null) return _cachedOpenSprites[idx];
        if (!isOpen && _cachedClosedSprites[idx] != null) return _cachedClosedSprites[idx];

        Sprite gen = GenerateCrateSprite(type, isOpen);
        if (isOpen) _cachedOpenSprites[idx] = gen;
        else _cachedClosedSprites[idx] = gen;
        return gen;
    }

    public static Sprite GetSunburstSprite()
    {
        if (_cachedSunburstSprite != null) return _cachedSunburstSprite;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / center;
                if (dist > 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                float angle = Mathf.Atan2(dy, dx);
                // 14 Işık Huzmesi
                float ray = Mathf.Sin(angle * 14f);
                float alpha = Mathf.Clamp01(ray * 0.6f + 0.4f) * (1f - dist * 0.65f) * 0.85f;
                tex.SetPixel(x, y, new Color(1f, 0.95f, 0.65f, alpha));
            }
        }
        tex.Apply();
        _cachedSunburstSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _cachedSunburstSprite;
    }

    private static Sprite GenerateCrateSprite(HangarManager.CrateType type, bool isOpen)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color clear = Color.clear;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

        void BlendPx(int px, int py, Color c)
        {
            if (px < 0 || px >= size || py < 0 || py >= size) return;
            Color bg = tex.GetPixel(px, py);
            float outA = c.a + bg.a * (1f - c.a);
            if (outA > 0.001f)
            {
                float r = (c.r * c.a + bg.r * bg.a * (1f - c.a)) / outA;
                float g = (c.g * c.a + bg.g * bg.a * (1f - c.a)) / outA;
                float b = (c.b * c.a + bg.b * bg.a * (1f - c.a)) / outA;
                tex.SetPixel(px, py, new Color(r, g, b, outA));
            }
        }

        // Tema Renk Paletleri
        Color bodyCol, bodyDark, bodyLight;
        Color metalCol, metalLight, metalDark;
        Color glowCol, lockCore;

        if (type == HangarManager.CrateType.Bronze)
        {
            // Askeri Bronz & Zeytin Yeşili Mühimmat Sandığı
            bodyCol = new Color(0.24f, 0.32f, 0.20f, 1f);
            bodyDark = new Color(0.14f, 0.20f, 0.12f, 1f);
            bodyLight = new Color(0.34f, 0.44f, 0.28f, 1f);

            metalCol = new Color(0.78f, 0.54f, 0.24f, 1f);
            metalLight = new Color(0.95f, 0.74f, 0.40f, 1f);
            metalDark = new Color(0.44f, 0.28f, 0.10f, 1f);

            glowCol = new Color(0.30f, 0.95f, 0.45f, 1f);
            lockCore = new Color(0.20f, 0.85f, 0.35f, 1f);
        }
        else if (type == HangarManager.CrateType.Silver)
        {
            // Siber Titanyum & Kobalt Çelik Ağır Cephane Kasası
            bodyCol = new Color(0.14f, 0.22f, 0.32f, 1f);
            bodyDark = new Color(0.08f, 0.13f, 0.20f, 1f);
            bodyLight = new Color(0.22f, 0.34f, 0.48f, 1f);

            metalCol = new Color(0.45f, 0.75f, 0.95f, 1f);
            metalLight = new Color(0.78f, 0.92f, 1.0f, 1f);
            metalDark = new Color(0.20f, 0.40f, 0.60f, 1f);

            glowCol = new Color(0.10f, 0.85f, 1.0f, 1f);
            lockCore = new Color(0.0f, 0.95f, 1.0f, 1f);
        }
        else
        {
            // Efsanevi Obsidyen Karbon & 24K Altın Kaplama Lüks Sandık
            bodyCol = new Color(0.11f, 0.12f, 0.16f, 1f);
            bodyDark = new Color(0.05f, 0.06f, 0.09f, 1f);
            bodyLight = new Color(0.20f, 0.22f, 0.28f, 1f);

            metalCol = new Color(0.95f, 0.78f, 0.18f, 1f);
            metalLight = new Color(1.0f, 0.92f, 0.52f, 1f);
            metalDark = new Color(0.60f, 0.42f, 0.05f, 1f);

            glowCol = new Color(1.0f, 0.85f, 0.25f, 1f);
            lockCore = new Color(1.0f, 0.95f, 0.60f, 1f);
        }

        // 1. Zemin Gölgesi (Alt yumuşak elips)
        for (int y = 8; y <= 22; y++)
        {
            for (int x = 16; x <= 112; x++)
            {
                float dx = (x - 64) / 46f;
                float dy = (y - 15) / 6f;
                float distSq = dx * dx + dy * dy;
                if (distSq <= 1f)
                {
                    float a = (1f - distSq) * 0.55f;
                    BlendPx(x, y, new Color(0f, 0f, 0f, a));
                }
            }
        }

        // 2. Sandık Alt Gövdesi (x: 22..106, y: 20..68)
        int bx0 = 22, bx1 = 106;
        int by0 = 20, by1 = 68;

        for (int y = by0; y <= by1; y++)
        {
            float tVert = (y - by0) / (float)(by1 - by0);
            for (int x = bx0; x <= bx1; x++)
            {
                float tHoriz = (x - bx0) / (float)(bx1 - bx0);
                float shade = 0.85f + 0.3f * tVert - 0.15f * tHoriz;
                Color c = Color.Lerp(bodyDark, bodyLight, tVert) * shade;
                c.a = 1f;

                // Panel dikiş hatları
                if (x == 44 || x == 84) c *= 0.78f;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }

        // Gövde Dikey Çelik Şeritleri (Sol, Orta, Sağ)
        int[][] bands = new int[][] { new int[] { 22, 30 }, new int[] { 60, 68 }, new int[] { 98, 106 } };
        foreach (var b in bands)
        {
            int x0 = b[0], x1 = b[1];
            for (int y = by0; y <= by1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    bool isEdge = (x == x0 || x == x1 || y == by0 || y == by1);
                    bool isHi = (x == x0 + 1 && y > by0 && y < by1);
                    tex.SetPixel(x, y, isHi ? metalLight : (isEdge ? metalDark : metalCol));
                }
            }
        }

        // Gövde Alt ve Üst Yatay Çerçevesi
        for (int y = by0; y <= by0 + 6; y++)
        {
            for (int x = bx0; x <= bx1; x++)
            {
                bool isEdge = (y == by0 || y == by0 + 6 || x == bx0 || x == bx1);
                bool isHi = (y == by0 + 5);
                tex.SetPixel(x, y, isHi ? metalLight : (isEdge ? metalDark : metalCol));
            }
        }
        for (int y = by1 - 5; y <= by1; y++)
        {
            for (int x = bx0; x <= bx1; x++)
            {
                bool isEdge = (y == by1 || y == by1 - 5 || x == bx0 || x == bx1);
                bool isHi = (y == by1 - 1);
                tex.SetPixel(x, y, isHi ? metalLight : (isEdge ? metalDark : metalCol));
            }
        }

        // Gövde Köşe Perçinleri
        int[][] rivets = new int[][] {
            new int[]{26, 23}, new int[]{26, 44}, new int[]{26, 65},
            new int[]{64, 23}, new int[]{64, 44},
            new int[]{102, 23}, new int[]{102, 44}, new int[]{102, 65}
        };
        foreach (var r in rivets)
        {
            int rx = r[0], ry = r[1];
            tex.SetPixel(rx, ry + 1, metalLight);
            tex.SetPixel(rx, ry, metalCol * 0.9f);
            tex.SetPixel(rx + 1, ry, metalDark);
            tex.SetPixel(rx, ry - 1, metalDark);
        }

        // Yan Taşıma Kolları
        for (int y = 36; y <= 50; y++)
        {
            for (int x = 14; x <= 22; x++)
            {
                if (x == 14 || x == 15 || y == 36 || y == 50)
                    tex.SetPixel(x, y, metalCol);
            }
            for (int x = 106; x <= 114; x++)
            {
                if (x == 113 || x == 114 || y == 36 || y == 50)
                    tex.SetPixel(x, y, metalCol);
            }
        }

        if (!isOpen)
        {
            // ==================== KAPALI SANDIK KAPAĞI ====================
            int lx0 = 18, lx1 = 110;
            int ly0 = 68, ly1 = 102;

            for (int y = ly0; y <= ly1; y++)
            {
                float tY = (y - ly0) / (float)(ly1 - ly0);
                int inset = (int)(tY * tY * 9);
                int curX0 = lx0 + inset;
                int curX1 = lx1 - inset;

                for (int x = curX0; x <= curX1; x++)
                {
                    float tX = (x - curX0) / (float)Mathf.Max(1, curX1 - curX0);
                    float highlight = Mathf.Sin(tY * Mathf.PI) * 0.35f;
                    Color c = bodyLight * (0.80f + highlight - 0.20f * tX);
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }

            // Kapak Alt Çıkıntı Dudağı (Lip Rim)
            for (int y = ly0; y <= ly0 + 6; y++)
            {
                for (int x = lx0; x <= lx1; x++)
                {
                    bool isEdge = (y == ly0 || y == ly0 + 6 || x == lx0 || x == lx1);
                    bool isHi = (y == ly0 + 5);
                    tex.SetPixel(x, y, isHi ? metalLight : (isEdge ? metalDark : metalCol));
                }
            }

            // Kapak Metal Şeritleri (Dikey)
            foreach (var b in bands)
            {
                int bx0B = b[0], bx1B = b[1];
                for (int y = ly0; y <= ly1; y++)
                {
                    float tY = (y - ly0) / (float)(ly1 - ly0);
                    int inset = (int)(tY * tY * 9 * 0.3f);
                    int curLx0 = bx0B - inset;
                    int curLx1 = bx1B - inset;
                    for (int x = curLx0; x <= curLx1; x++)
                    {
                        bool isHi = (x == curLx0 + 1);
                        tex.SetPixel(x, y, isHi ? metalLight : metalCol);
                    }
                }
            }

            // Kapak Tepe Kenar Trimi
            for (int y = ly1 - 3; y <= ly1; y++)
            {
                float tY = (y - ly0) / (float)(ly1 - ly0);
                int inset = (int)(tY * tY * 9);
                for (int x = lx0 + inset; x <= lx1 - inset; x++)
                {
                    tex.SetPixel(x, y, metalLight);
                }
            }

            // Orta Kilit Mekanizması (y: 58..76, x: 57..71)
            int kx0 = 57, kx1 = 71;
            int ky0 = 58, ky1 = 76;

            // Kilit arkası gölge
            for (int y = ky0 - 2; y <= ky1 + 2; y++)
                for (int x = kx0 - 2; x <= kx1 + 2; x++)
                    BlendPx(x, y, new Color(0f, 0f, 0f, 0.45f));

            for (int y = ky0; y <= ky1; y++)
            {
                for (int x = kx0; x <= kx1; x++)
                {
                    bool isEdge = (x == kx0 || x == kx1 || y == ky0 || y == ky1);
                    bool isHi = (x == kx0 + 1 || y == ky1 - 1);
                    tex.SetPixel(x, y, isHi ? metalLight : (isEdge ? metalDark : metalCol));
                }
            }

            // Kilit Yuvası & Işıyan Kristal Çekirdek
            for (int y = 63; y <= 69; y++)
            {
                for (int x = 62; x <= 66; x++)
                {
                    tex.SetPixel(x, y, lockCore);
                }
            }
            tex.SetPixel(64, 72, glowCol);
            tex.SetPixel(65, 72, glowCol);
        }
        else
        {
            // ==================== AÇIK SANDIK KAPAĞI ====================
            // İç Boşluk (Derin karanlık zemin)
            for (int y = 54; y <= by1; y++)
            {
                for (int x = bx0 + 4; x <= bx1 - 4; x++)
                {
                    tex.SetPixel(x, y, new Color(0.05f, 0.04f, 0.07f, 1f));
                }
            }

            // Sandıktan Fışkıran Işık Huzmeleri
            for (int y = 56; y <= 124; y++)
            {
                float tRay = (y - 56) / 68f;
                int rayW = (int)(22 + tRay * 38);
                for (int x = 64 - rayW; x <= 64 + rayW; x++)
                {
                    float dist = Mathf.Abs(x - 64) / (float)rayW;
                    if (dist <= 1f)
                    {
                        float a = (1f - dist) * (1f - tRay * 0.65f) * 0.90f;
                        if ((x + y) % 6 == 0 || (x - y) % 8 == 0) a = Mathf.Min(1f, a * 1.5f);
                        BlendPx(x, y, new Color(glowCol.r, glowCol.g, glowCol.b, a));
                    }
                }
            }

            // Açılmış ve Arkaya Yaslanmış Kapak (y: 70..112)
            int lx0 = 18, lx1 = 110;
            for (int y = 70; y <= 112; y++)
            {
                float tY = (y - 70) / 42f;
                int inset = (int)(tY * 13);
                int curX0 = lx0 + inset;
                int curX1 = lx1 - inset;

                for (int x = curX0; x <= curX1; x++)
                {
                    bool isRim = (x == curX0 || x == curX1 || y == 112 || y == 70);
                    tex.SetPixel(x, y, isRim ? metalCol : bodyDark);
                }
            }

            // Sandık Yan Menteşeleri
            for (int y = 66; y <= 74; y++)
            {
                for (int x = 24; x <= 31; x++) tex.SetPixel(x, y, metalLight);
                for (int x = 97; x <= 104; x++) tex.SetPixel(x, y, metalLight);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
