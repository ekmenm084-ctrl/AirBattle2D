using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Boss zaferinden sonra açılan interaktif Hangar Güçlendirme Arayüzü (Upgrade UI).
/// 3 farklı seçenek sunar, oyunu duraklatır (Time.timeScale = 0), seçim yapıldığında
/// uçağa yükseltmeyi entegre eder ve savaşa devam ettirir.
/// </summary>
public class UpgradeSelectionUI : MonoBehaviour
{
    public static UpgradeSelectionUI Instance { get; private set; }

    [Header("UI Kökü")]
    public GameObject modalRoot;
    public Transform cardsContainer;

    private Action _onSelectedCallback;
    private bool _isSelecting = false;
    public bool IsSelecting => _isSelecting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public static UpgradeSelectionUI EnsureInstance()
    {
        if (Instance != null) return Instance;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("CombatCanvas", typeof(RectTransform));
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Transform existing = canvas.transform.Find("UpgradeSelectionUI");
        GameObject modalObj = existing != null ? existing.gameObject : null;

        if (modalObj == null)
        {
            modalObj = new GameObject("UpgradeSelectionUI", typeof(RectTransform));
            modalObj.transform.SetParent(canvas.transform, false);
        }

        UpgradeSelectionUI ui = modalObj.GetComponent<UpgradeSelectionUI>();
        if (ui == null)
        {
            ui = modalObj.AddComponent<UpgradeSelectionUI>();
        }

        Instance = ui;
        return ui;
    }

    /// <summary>
    /// Boss yenilince çağrılır: Oyunu duraklatır ve 3 seçenek sunar.
    /// </summary>
    public void ShowUpgradeSelection(Action onChosen)
    {
        _onSelectedCallback = onChosen;
        _isSelecting = true;

        // Oyunu dondur (Time.timeScale = 0)
        Time.timeScale = 0f;

        PlayerUpgradeManager upgradeManager = PlayerUpgradeManager.EnsureInstance();
        List<UpgradeOption> options = upgradeManager.GenerateThreeRandomOptions();

        BuildUIHierarchy(options);

        if (modalRoot != null)
            modalRoot.SetActive(true);
    }

    private void BuildUIHierarchy(List<UpgradeOption> options)
    {
        if (modalRoot == null)
        {
            modalRoot = new GameObject("ModalRoot", typeof(RectTransform));
            modalRoot.transform.SetParent(transform, false);

            RectTransform rootRt = modalRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Karartma arka planı (arkaya tıklamaları engeller)
            Image bgOverlay = modalRoot.AddComponent<Image>();
            bgOverlay.color = new Color(0.03f, 0.06f, 0.12f, 0.93f);
            bgOverlay.raycastTarget = true;
        }

        // Varsa eski çocukları temizle
        foreach (Transform child in modalRoot.transform)
        {
            Destroy(child.gameObject);
        }

        // 1. Üst Başlık ve Bilgilendirme
        GameObject headerObj = new GameObject("HeaderContainer", typeof(RectTransform));
        headerObj.transform.SetParent(modalRoot.transform, false);
        RectTransform headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0.5f, 0.89f);
        headerRt.anchorMax = new Vector2(0.5f, 0.89f);
        headerRt.pivot = new Vector2(0.5f, 0.5f);
        headerRt.sizeDelta = new Vector2(1050f, 130f);

        // Başlık TMP
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform));
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.65f);
        titleRt.anchorMax = new Vector2(0.5f, 0.65f);
        titleRt.sizeDelta = new Vector2(1050f, 60f);

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "HANGAR GÜÇLENDİRME SEÇİMİ";
        titleTMP.fontSize = 40f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(1f, 0.85f, 0.2f); // Parlak Altın Sarısı

        // Alt Başlık TMP
        GameObject subObj = new GameObject("SubText", typeof(RectTransform));
        subObj.transform.SetParent(headerObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 0.18f);
        subRt.anchorMax = new Vector2(0.5f, 0.18f);
        subRt.sizeDelta = new Vector2(1050f, 46f);

        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.text = "BOSS İMHA EDİLDİ! BİR YÜKSELTMEYLE UÇAĞINI DONAT VE SAVAŞA DEVAM ET:";
        subTMP.fontSize = 22f;
        subTMP.fontStyle = FontStyles.Bold;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = new Color(0.25f, 0.92f, 1f); // Neon Cyan

        // 2. Kartlar Konteynırı (Genişletilmiş ve Ferah)
        GameObject cardsObj = new GameObject("CardsContainer", typeof(RectTransform));
        cardsObj.transform.SetParent(modalRoot.transform, false);
        RectTransform cardsRt = cardsObj.GetComponent<RectTransform>();
        cardsRt.anchorMin = new Vector2(0.5f, 0.44f);
        cardsRt.anchorMax = new Vector2(0.5f, 0.44f);
        cardsRt.pivot = new Vector2(0.5f, 0.5f);
        cardsRt.sizeDelta = new Vector2(1180f, 560f);

        // 3 Kartı Yan Yana Yerleştir (-390, 0, +390)
        float[] xPositions = new float[] { -390f, 0f, 390f };

        for (int i = 0; i < options.Count && i < 3; i++)
        {
            UpgradeOption opt = options[i];
            CreateCardUI(cardsObj.transform, opt, xPositions[i]);
        }
    }

    private void CreateCardUI(Transform parent, UpgradeOption opt, float xPos)
    {
        GameObject card = new GameObject("Card_" + opt.type, typeof(RectTransform));
        card.transform.SetParent(parent, false);

        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = new Vector2(xPos, 0f);
        cardRt.sizeDelta = new Vector2(360f, 530f);

        // Kart Arka Planı (Koyu Şık Taktik Çerçeve)
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.06f, 0.10f, 0.17f, 0.98f);

        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(opt.themeColor.r, opt.themeColor.g, opt.themeColor.b, 0.85f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        // Üst Renkli Aksan Şeridi
        GameObject accentObj = new GameObject("AccentBar", typeof(RectTransform));
        accentObj.transform.SetParent(card.transform, false);
        RectTransform accentRt = accentObj.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.sizeDelta = new Vector2(0f, 10f);
        accentRt.anchoredPosition = Vector2.zero;

        Image accentImg = accentObj.AddComponent<Image>();
        accentImg.color = opt.themeColor;

        // Rozet / Kategori TMP
        GameObject badgeObj = new GameObject("Badge", typeof(RectTransform));
        badgeObj.transform.SetParent(card.transform, false);
        RectTransform badgeRt = badgeObj.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0.5f, 0.93f);
        badgeRt.anchorMax = new Vector2(0.5f, 0.93f);
        badgeRt.sizeDelta = new Vector2(330f, 34f);

        TextMeshProUGUI badgeTMP = badgeObj.AddComponent<TextMeshProUGUI>();
        badgeTMP.text = opt.badge;
        badgeTMP.fontSize = 20f;
        badgeTMP.fontStyle = FontStyles.Bold;
        badgeTMP.alignment = TextAlignmentOptions.Center;
        badgeTMP.color = opt.themeColor;

        // İkon / Emoji
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(card.transform, false);
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.80f);
        iconRt.anchorMax = new Vector2(0.5f, 0.80f);
        iconRt.sizeDelta = new Vector2(330f, 54f);

        TextMeshProUGUI iconTMP = iconObj.AddComponent<TextMeshProUGUI>();
        iconTMP.text = opt.icon;
        iconTMP.fontSize = 36f;
        iconTMP.fontStyle = FontStyles.Bold;
        iconTMP.color = opt.themeColor;
        iconTMP.alignment = TextAlignmentOptions.Center;

        // Başlık (Büyük ve Çok Okunabilir)
        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(card.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.65f);
        titleRt.anchorMax = new Vector2(0.5f, 0.65f);
        titleRt.sizeDelta = new Vector2(340f, 60f);

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = opt.title;
        titleTMP.fontSize = 25f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;

        // Açıklama (Net, Büyük ve Ferah Satır Aralığı)
        GameObject descObj = new GameObject("Desc", typeof(RectTransform));
        descObj.transform.SetParent(card.transform, false);
        RectTransform descRt = descObj.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0.5f, 0.44f);
        descRt.anchorMax = new Vector2(0.5f, 0.44f);
        descRt.sizeDelta = new Vector2(330f, 105f);

        TextMeshProUGUI descTMP = descObj.AddComponent<TextMeshProUGUI>();
        descTMP.text = opt.description;
        descTMP.fontSize = 20f;
        descTMP.alignment = TextAlignmentOptions.Center;
        descTMP.color = new Color(0.92f, 0.96f, 1f);
        descTMP.lineSpacing = 6f;
        descTMP.textWrappingMode = TextWrappingModes.Normal;

        // Avantaj Kutusu (Stat Box)
        GameObject statBox = new GameObject("StatBox", typeof(RectTransform));
        statBox.transform.SetParent(card.transform, false);
        RectTransform statRt = statBox.GetComponent<RectTransform>();
        statRt.anchorMin = new Vector2(0.5f, 0.23f);
        statRt.anchorMax = new Vector2(0.5f, 0.23f);
        statRt.sizeDelta = new Vector2(330f, 48f);

        Image statBg = statBox.AddComponent<Image>();
        statBg.color = new Color(0.04f, 0.08f, 0.14f, 0.95f);

        GameObject statTxtObj = new GameObject("StatText", typeof(RectTransform));
        statTxtObj.transform.SetParent(statBox.transform, false);
        RectTransform statTxtRt = statTxtObj.GetComponent<RectTransform>();
        statTxtRt.anchorMin = Vector2.zero;
        statTxtRt.anchorMax = Vector2.one;
        statTxtRt.offsetMin = Vector2.zero;
        statTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI statTMP = statTxtObj.AddComponent<TextMeshProUGUI>();
        statTMP.text = opt.statBenefit;
        statTMP.fontSize = 18.5f;
        statTMP.fontStyle = FontStyles.Bold;
        statTMP.alignment = TextAlignmentOptions.Center;
        statTMP.color = opt.themeColor;

        // SEÇ & DONAT BUTONU (Geniş, Yüksek ve Okunabilir)
        GameObject btnObj = new GameObject("SelectButton", typeof(RectTransform));
        btnObj.transform.SetParent(card.transform, false);
        RectTransform btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.08f);
        btnRt.anchorMax = new Vector2(0.5f, 0.08f);
        btnRt.sizeDelta = new Vector2(320f, 60f);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = opt.themeColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        ColorBlock cb = btn.colors;
        cb.normalColor = opt.themeColor;
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(opt.themeColor.r * 0.7f, opt.themeColor.g * 0.7f, opt.themeColor.b * 0.7f);
        btn.colors = cb;

        GameObject btnTxtObj = new GameObject("BtnText", typeof(RectTransform));
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        RectTransform btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.offsetMin = Vector2.zero;
        btnTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI btnTMP = btnTxtObj.AddComponent<TextMeshProUGUI>();
        btnTMP.text = "SEÇ VE DONAT";
        btnTMP.fontSize = 24f;
        btnTMP.fontStyle = FontStyles.Bold;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = new Color(0.04f, 0.06f, 0.09f); // Koyu çok net okunabilir yazı

        // Tıklama Olayı
        UpgradeOption capturedOpt = opt;
        btn.onClick.AddListener(() =>
        {
            OnCardChosen(capturedOpt);
        });
    }

    private void OnCardChosen(UpgradeOption chosen)
    {
        if (!_isSelecting) return;
        _isSelecting = false;

        // Yükseltmeyi uygula
        PlayerUpgradeManager.EnsureInstance().ApplyUpgrade(chosen.type);

        // Ekranı kapat ve oyunu devam ettir
        if (modalRoot != null)
            modalRoot.SetActive(false);

        Time.timeScale = 1f;

        // Callback çağır (Sonraki dalgayı başlat)
        _onSelectedCallback?.Invoke();
        _onSelectedCallback = null;
    }
}
