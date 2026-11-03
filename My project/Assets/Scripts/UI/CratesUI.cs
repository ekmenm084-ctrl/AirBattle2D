using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Askeri Hava İkmal ve Ganimet Sandıkları Arayüzü:
/// Sandık açma, büyük altın ikramiyeleri, kalıcı As çipleri ve uçak şemaları.
/// Tüm kazanılan ödüller doğrudan Hangar Askeri Envanteri'ne aktarılır.
/// </summary>
public class CratesUI : MonoBehaviour
{
    public static CratesUI Instance { get; private set; }

    private GameObject _panelRoot;
    private TextMeshProUGUI _goldTMP;
    private Button _bronzeBtn, _silverBtn, _goldBtn;
    private TextMeshProUGUI _bronzeBtnTMP, _silverBtnTMP, _goldBtnTMP;

    // Ödül Bildirim Modal Penceresi
    private GameObject _rewardModalObj;
    private TextMeshProUGUI _rewardTitleTMP, _rewardDescTMP;
    private Outline _rewardModalBorder;

    private Action _onBackCallback;

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
        RefreshVisuals();
    }

    public void CloseCratesPanel()
    {
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
        bool canAfford = (currentGold >= cost);
        btn.interactable = canAfford;
        tmp.text = canAfford ? $"SANDIĞI AÇ ({cost}G)" : $"YETERSİZ ALTIN ({cost}G)";
        tmp.color = canAfford ? Color.white : new Color(0.9f, 0.6f, 0.6f);

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = canAfford ? new Color(0.18f, 0.55f, 0.32f) : new Color(0.35f, 0.22f, 0.22f);
        }
    }

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

        // 2. FERAH 3 SANDIK KARTI (GENİŞ YERLEŞİM)
        BuildCrateCards();

        // 3. ALT NAVİGASYON (GERİ DÖN & HANGAR ENVANTERİ BUTONLARI)
        BuildBottomBar();

        // 4. ÖDÜL KAZANMA POP-UP MODAL PENCERESİ
        BuildRewardModal();

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
        hRt.anchoredPosition = new Vector2(0f, -25f);
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
        gRt.anchoredPosition = new Vector2(-40f, -25f);
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

        // Üst Sol Geri Dön Butonu (Her zaman görünür ve kolay erişilebilir)
        GameObject topBackBtnObj = new GameObject("TopBackFromCratesButton");
        topBackBtnObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform tbRt = GetRect(topBackBtnObj);
        tbRt.anchorMin = new Vector2(0f, 1f);
        tbRt.anchorMax = new Vector2(0f, 1f);
        tbRt.pivot = new Vector2(0f, 1f);
        tbRt.anchoredPosition = new Vector2(35f, -22f);
        tbRt.sizeDelta = new Vector2(200f, 52f);

        Image tbImg = topBackBtnObj.AddComponent<Image>();
        tbImg.color = new Color(0.18f, 0.28f, 0.40f);

        Outline tbOutline = topBackBtnObj.AddComponent<Outline>();
        tbOutline.effectColor = new Color(0.35f, 0.75f, 1f);
        tbOutline.effectDistance = new Vector2(2f, -2f);

        Button tbBtn = topBackBtnObj.AddComponent<Button>();
        tbBtn.onClick.AddListener(CloseCratesPanel);

        GameObject tbTxtObj = new GameObject("Text");
        tbTxtObj.transform.SetParent(topBackBtnObj.transform, false);
        RectTransform tbTxtRt = GetRect(tbTxtObj);
        tbTxtRt.anchorMin = Vector2.zero;
        tbTxtRt.anchorMax = Vector2.one;
        tbTxtRt.offsetMin = Vector2.zero;
        tbTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tbTMP = tbTxtObj.AddComponent<TextMeshProUGUI>();
        tbTMP.text = "< GERİ DÖN";
        tbTMP.fontSize = 21f;
        tbTMP.fontStyle = FontStyles.Bold;
        tbTMP.alignment = TextAlignmentOptions.Center;
        tbTMP.color = Color.white;
    }

    private void BuildCrateCards()
    {
        float cardY = 20f;
        float spacingX = 490f;

        CreateCrateCard("BronzeCard", -spacingX, cardY,
            "BRONZ İKMAL KUTUSU",
            "150 ALTIN",
            "• 180 - 320 Altın İkramiyesi\n\n• Taktik Mermi Çipi (+1.0 DMG)\n\n• Çelik Gövde Çipi (+10 HP)\n\n• Kalıcı bonuslar filonuza eklenir",
            new Color(0.2f, 0.8f, 0.45f),
            HangarManager.CrateType.Bronze,
            out _bronzeBtn, out _bronzeBtnTMP);

        CreateCrateCard("SilverCard", 0f, cardY,
            "GÜMÜŞ CEPHANE KASASI",
            "450 ALTIN",
            "• 500 - 1100 Altın Büyük Ödülü\n\n• Ücretsiz Drone Seviye Artışı\n\n• Gelişmiş As Çipi (+2.5 DMG / +25 HP)\n\n• Yüksek As Çipi çıkma ihtimali",
            new Color(0.3f, 0.65f, 1f),
            HangarManager.CrateType.Silver,
            out _silverBtn, out _silverBtnTMP);

        CreateCrateCard("GoldCard", spacingX, cardY,
            "ALTIN AS SANDIĞI",
            "1100 ALTIN",
            "• 1400 - 2800 Altın Dev İkramiye\n\n• Bedava A-10 veya F-22 Uçak Kilidi!\n\n• Efsanevi As Protokolü (+5 DMG / +35 HP)\n\n• En yüksek kalıcı filo güçlendirmesi",
            new Color(1f, 0.82f, 0.2f),
            HangarManager.CrateType.GoldAce,
            out _goldBtn, out _goldBtnTMP);
    }

    private void CreateCrateCard(string objName, float posX, float posY, string title, string costSub, string lootDetails, Color accentColor, HangarManager.CrateType type, out Button openBtn, out TextMeshProUGUI btnTMP)
    {
        GameObject cardObj = new GameObject(objName);
        cardObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform cRt = GetRect(cardObj);
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(posX, posY);
        cRt.sizeDelta = new Vector2(440f, 530f);

        Image cBg = cardObj.AddComponent<Image>();
        cBg.color = new Color(0.06f, 0.10f, 0.17f, 0.95f);

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(3f, -3f);

        // Başlık Kutusu
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = GetRect(tObj);
        tRt.anchorMin = new Vector2(0f, 0.80f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tTMP = tObj.AddComponent<TextMeshProUGUI>();
        tTMP.text = $"<b>{title}</b>\n<size=75%><color=#FFE082>{costSub}</color></size>";
        tTMP.fontSize = 23f;
        tTMP.alignment = TextAlignmentOptions.Center;
        tTMP.color = Color.white;

        // Sandık Detay Listesi (Ferah satır boşluklu)
        GameObject dObj = new GameObject("LootDetails");
        dObj.transform.SetParent(cardObj.transform, false);
        RectTransform dRt = GetRect(dObj);
        dRt.anchorMin = new Vector2(0.06f, 0.18f);
        dRt.anchorMax = new Vector2(0.94f, 0.78f);
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;

        TextMeshProUGUI dTMP = dObj.AddComponent<TextMeshProUGUI>();
        dTMP.text = lootDetails;
        dTMP.fontSize = 19f;
        dTMP.lineSpacing = 6f;
        dTMP.alignment = TextAlignmentOptions.Center;
        dTMP.color = new Color(0.9f, 0.95f, 1f);

        // Aç Butonu
        GameObject bObj = new GameObject("OpenBtn");
        bObj.transform.SetParent(cardObj.transform, false);
        RectTransform bRt = GetRect(bObj);
        bRt.anchorMin = new Vector2(0.08f, 0.04f);
        bRt.anchorMax = new Vector2(0.92f, 0.16f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        Image bImg = bObj.AddComponent<Image>();
        bImg.color = new Color(0.18f, 0.55f, 0.32f);

        openBtn = bObj.AddComponent<Button>();
        openBtn.onClick.AddListener(() =>
        {
            var reward = HangarManager.Instance.OpenCrate(type);
            if (reward != null)
            {
                ShowRewardModal(reward);
                RefreshVisuals();
            }
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
        // 1. GERİ DÖN BUTONU (EKRANIN SOL EN ALTINDA)
        GameObject backBtnObj = new GameObject("BackFromCratesButton");
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

        // 2. HANGAR ENVANTERİNİ GÖR BUTONU (ÖNCEKİ MENÜ KAPANIR, YENİ SAYFADA AÇILIR)
        GameObject invBtnObj = new GameObject("GoToInventoryButton");
        invBtnObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform iRt = GetRect(invBtnObj);
        iRt.anchorMin = new Vector2(1f, 0f);
        iRt.anchorMax = new Vector2(1f, 0f);
        iRt.pivot = new Vector2(1f, 0f);
        iRt.anchoredPosition = new Vector2(-40f, 25f);
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
        iTMP.text = "🎒 HANGAR ENVANTERİ";
        iTMP.fontSize = 22f;
        iTMP.fontStyle = FontStyles.Bold;
        iTMP.alignment = TextAlignmentOptions.Center;
        iTMP.color = Color.white;

        // Geri dön butonunu en üste al
        backBtnObj.transform.SetAsLastSibling();
    }

    private void BuildRewardModal()
    {
        _rewardModalObj = new GameObject("CrateRewardModal");
        _rewardModalObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform rt = GetRect(_rewardModalObj);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(650f, 380f);

        Image bg = _rewardModalObj.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.07f, 0.13f, 0.98f);

        _rewardModalBorder = _rewardModalObj.AddComponent<Outline>();
        _rewardModalBorder.effectColor = Color.yellow;
        _rewardModalBorder.effectDistance = new Vector2(3f, -3f);

        // Başlık
        GameObject tObj = new GameObject("RewardTitle");
        tObj.transform.SetParent(_rewardModalObj.transform, false);
        RectTransform tRt = GetRect(tObj);
        tRt.anchorMin = new Vector2(0f, 0.68f);
        tRt.anchorMax = new Vector2(1f, 0.96f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        _rewardTitleTMP = tObj.AddComponent<TextMeshProUGUI>();
        _rewardTitleTMP.fontSize = 28f;
        _rewardTitleTMP.fontStyle = FontStyles.Bold;
        _rewardTitleTMP.alignment = TextAlignmentOptions.Center;
        _rewardTitleTMP.color = Color.yellow;

        // Açıklama
        GameObject dObj = new GameObject("RewardDesc");
        dObj.transform.SetParent(_rewardModalObj.transform, false);
        RectTransform dRt = GetRect(dObj);
        dRt.anchorMin = new Vector2(0.06f, 0.28f);
        dRt.anchorMax = new Vector2(0.94f, 0.68f);
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;

        _rewardDescTMP = dObj.AddComponent<TextMeshProUGUI>();
        _rewardDescTMP.fontSize = 22f;
        _rewardDescTMP.lineSpacing = 5f;
        _rewardDescTMP.alignment = TextAlignmentOptions.Center;
        _rewardDescTMP.color = Color.white;

        // Onay Butonu
        GameObject okObj = new GameObject("OkBtn");
        okObj.transform.SetParent(_rewardModalObj.transform, false);
        RectTransform okRt = GetRect(okObj);
        okRt.anchorMin = new Vector2(0.3f, 0.07f);
        okRt.anchorMax = new Vector2(0.7f, 0.24f);
        okRt.offsetMin = Vector2.zero;
        okRt.offsetMax = Vector2.zero;

        Image okImg = okObj.AddComponent<Image>();
        okImg.color = new Color(0.18f, 0.55f, 0.34f);

        Button okBtn = okObj.AddComponent<Button>();
        okBtn.onClick.AddListener(() => _rewardModalObj.SetActive(false));

        GameObject okTxt = new GameObject("Text");
        okTxt.transform.SetParent(okObj.transform, false);
        RectTransform otRt = GetRect(okTxt);
        otRt.anchorMin = Vector2.zero;
        otRt.anchorMax = Vector2.one;
        otRt.offsetMin = Vector2.zero;
        otRt.offsetMax = Vector2.zero;

        TextMeshProUGUI okTMP = okTxt.AddComponent<TextMeshProUGUI>();
        okTMP.text = "HARİKA!";
        okTMP.fontSize = 23f;
        okTMP.fontStyle = FontStyles.Bold;
        okTMP.alignment = TextAlignmentOptions.Center;
        okTMP.color = Color.white;

        _rewardModalObj.SetActive(false);
    }

    private void ShowRewardModal(HangarManager.CrateReward reward)
    {
        if (_rewardModalObj == null || reward == null) return;

        ColorUtility.TryParseHtmlString(reward.rarityColorHex, out Color borderCol);
        if (_rewardModalBorder != null) _rewardModalBorder.effectColor = borderCol;

        if (_rewardTitleTMP != null)
        {
            _rewardTitleTMP.text = reward.title;
            _rewardTitleTMP.color = borderCol;
        }

        if (_rewardDescTMP != null)
        {
            _rewardDescTMP.text = reward.description;
        }

        _rewardModalObj.SetActive(true);
        AudioManager.Instance?.PlayGunfire(true);
        CameraShake.Shake(0.12f, 0.06f);
    }
}
