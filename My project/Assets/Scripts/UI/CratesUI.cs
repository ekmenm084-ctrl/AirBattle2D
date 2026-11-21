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
    private TextMeshProUGUI _keysTMP;

    // Anahtar Butonları ve Rozetleri (Boss Anahtarları ile Açılış)
    private Button _bronzeKeyBtn, _silverKeyBtn, _goldKeyBtn;
    private TextMeshProUGUI _bronzeKeyBtnTMP, _silverKeyBtnTMP, _goldKeyBtnTMP;
    private TextMeshProUGUI _bronzeKeyBadgeTMP, _silverKeyBadgeTMP, _goldKeyBadgeTMP;

    // Anahtar Eksik İkaz Bildirimi (Toast)
    private GameObject _keyAlertToastObj;
    private TextMeshProUGUI _keyAlertTMP;
    private Coroutine _keyAlertCoroutine;

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
    private static Sprite _cratesBgSprite;
    private static Sprite[] _cachedClosedSprites;
    private static Sprite[] _cachedOpenSprites;
    private static Sprite _cachedSunburstSprite;

    public static Sprite GetCratesBgSprite()
    {
        if (_cratesBgSprite != null) return _cratesBgSprite;

        string[] paths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, "Sprites/crates_background.jpg"),
            System.IO.Path.Combine(Application.dataPath, "Resources/crates_background.jpg")
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
                    _cratesBgSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    break;
                }
            }
        }

        if (_cratesBgSprite == null)
        {
            _cratesBgSprite = Resources.Load<Sprite>("crates_background");
        }
        return _cratesBgSprite;
    }

    private static RectTransform GetRect(GameObject go)
    {
        if (go == null) return null;
        return go.GetComponent<RectTransform>();
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
        GameObject go = existing != null ? existing.gameObject : new GameObject("CratesUI", typeof(RectTransform));
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
        PlayerProfileUI.EnsureUI()?.ShowMenuStatusBar(showGoldBadge: false);
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

        if (_keysTMP != null)
        {
            string superJetStr = (hm.singleUseSuperPlanes > 0) ? $"  |  <color=#00FFFF>[+] 6. Nesil Jet Sortisi: {hm.singleUseSuperPlanes}</color>" : "";
            _keysTMP.text = $"<b>ANAHTARLARINIZ:</b> <color=#CD7F32>Bronz: {hm.bronzeKeys}</color>  |  <color=#C0C0C0>Gümüş: {hm.silverKeys}</color>  |  <color=#FFD700>Altın As: {hm.goldKeys}</color>{superJetStr}";
        }

        UpdateKeyButton(_bronzeKeyBtn, _bronzeKeyBtnTMP, _bronzeKeyBadgeTMP, "BRONZ", hm.bronzeKeys, new Color(0.85f, 0.50f, 0.20f));
        UpdateKeyButton(_silverKeyBtn, _silverKeyBtnTMP, _silverKeyBadgeTMP, "GÜMÜŞ", hm.silverKeys, new Color(0.25f, 0.75f, 1f));
        UpdateKeyButton(_goldKeyBtn, _goldKeyBtnTMP, _goldKeyBadgeTMP, "ALTIN AS", hm.goldKeys, new Color(1f, 0.85f, 0.2f));
    }

    private void UpdateKeyButton(Button btn, TextMeshProUGUI tmp, TextMeshProUGUI badgeTMP, string keyName, int keyCount, Color keyCol)
    {
        if (badgeTMP != null)
        {
            badgeTMP.font = MainMenuManager.GetTacticalFont();
            badgeTMP.text = (keyCount > 0)
                ? $"<b><color=#{ColorUtility.ToHtmlStringRGB(keyCol)}>{keyName} ANAHTARI: {keyCount} ADET</color></b>"
                : $"<color=#94A3B8>{keyName} ANAHTARI: 0 ADET</color>";
        }

        if (btn == null || tmp == null) return;
        bool hasKey = (keyCount > 0) && !_isOpeningCrate;
        btn.interactable = !_isOpeningCrate;

        tmp.font = MainMenuManager.GetTacticalFont();
        if (hasKey)
        {
            tmp.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(keyCol)}>{keyName} ANAHTARLA AÇ (1x)</color></b>\n<size=82%><color=#4ADE80>» {keyCount} ADET MEVCUT - DOKUN VE AÇ «</color></size>";
            tmp.color = Color.white;
        }
        else
        {
            string topCol = keyName == "ALTIN AS" ? "#FDE047" : (keyName == "GÜMÜŞ" ? "#93C5FD" : "#FCA5A5");
            tmp.text = $"<b><color={topCol}>{keyName} ANAHTARI GEREKLİ (0/1)</color></b>\n<size=80%><color=#F87171>Anahtar Yok - Bosslardan veya\nGörevlerden Düşer</color></size>";
            tmp.color = Color.white;
        }
    }

    // ==================== UI YAPISI (1:1 3D SANDIK MENÜSÜ) ====================
    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        _panelRoot = new GameObject("CratesPanelRoot", typeof(RectTransform));
        _panelRoot.transform.SetParent(transform, false);

        RectTransform rootRt = GetRect(_panelRoot);
        MakeFullScreenRect(rootRt);

        Image panelBg = _panelRoot.AddComponent<Image>();
        panelBg.sprite = GetCratesBgSprite();
        panelBg.color = Color.white;
        panelBg.raycastTarget = false;

        // 1. ÜST PANEL & ALTIN GÖSTERGESİ
        BuildHeader();

        // 2. 1:1 3 SANDIK KARTI DİNAMİK ANAHTAR & AÇMA BUTONLARI
        BuildCrateCards();

        // 2.1 ANAHTAR EKSİK İKAZ BİLDİRİMİ (TOAST)
        BuildAlertToast();

        // 3. ALT NAVİGASYON (« GERİ DÖN - Askeri Envanter kaldırıldı)
        BuildBottomBar();

        // 4. SİNEMATİK SANDIK AÇILMA SAHNESİ
        BuildCinematicOpeningStage();

        _panelRoot.SetActive(false);
    }

    private void BuildHeader()
    {
        // 1. Üstতে Anahtar Rozeti (Arka plan görselindeki y=122..162 satırına tam oturur)
        GameObject keysBadgeObj = new GameObject("KeysBadge", typeof(RectTransform));
        keysBadgeObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform kRt = GetRect(keysBadgeObj);
        HangarUI.SetNormalizedRect1080p(kRt, new Vector2(0f, 396.0f), new Vector2(960f, 38f));

        _keysTMP = keysBadgeObj.AddComponent<TextMeshProUGUI>();
        _keysTMP.font = MainMenuManager.GetTacticalFont();
        _keysTMP.text = "<b>ANAHTARLARINIZ:</b> <color=#CD7F32>Bronz: 0</color>  |  <color=#C0C0C0>Gümüş: 0</color>  |  <color=#FFD700>Altın As: 0</color>";
        _keysTMP.fontSize = 20f;
        _keysTMP.characterSpacing = 1.4f;
        _keysTMP.fontStyle = FontStyles.Bold;
        _keysTMP.alignment = TextAlignmentOptions.Center;
        _keysTMP.color = new Color(0.92f, 0.96f, 1f);
        _keysTMP.raycastTarget = false;

        // 2. Üst Sağ Canlı Altın Göstergesi (Hangar ve Drone menüsüyle birebir aynı konum ve boyutta)
        GameObject goldBadgeObj = new GameObject("LiveGoldBadge", typeof(RectTransform));
        goldBadgeObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform gRt = GetRect(goldBadgeObj);
        HangarUI.SetNormalizedRect1080p(gRt, new Vector2(685.0f, 526.0f), new Vector2(215.0f, 30.0f));

        GameObject goldTxtObj = new GameObject("GoldText", typeof(RectTransform));
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

        // 3. Üst Sol Profil Tetikleyicisi
        HangarUI.FormatPixelExactButton(
            "ProfileBadgeTrigger",
            _panelRoot.transform,
            new Vector2(-565.6f, 513.7f),
            new Vector2(361.0f, 52.6f),
            new Color(0f, 0.85f, 1f, 0.10f),
            new Color(0f, 0.7f, 0.95f, 0.18f),
            () => PlayerProfileUI.EnsureUI()?.OpenDossierModal());
    }

    private void BuildCrateCards()
    {
        // Arka plandaki 3 adet 3D sandık kartının (x=126..652, 700..1226, 1280..1806) merkez X koordinatları:
        CreateCrateCardOverlay("BronzeCardOverlay", -571.0f,
            new Color(0.85f, 0.50f, 0.20f),
            HangarManager.CrateType.Bronze,
            out _bronzeKeyBtn, out _bronzeKeyBtnTMP, out _bronzeKeyBadgeTMP);

        CreateCrateCardOverlay("SilverCardOverlay", 3.0f,
            new Color(0.35f, 0.75f, 1f),
            HangarManager.CrateType.Silver,
            out _silverKeyBtn, out _silverKeyBtnTMP, out _silverKeyBadgeTMP);

        CreateCrateCardOverlay("GoldCardOverlay", 583.0f,
            new Color(1f, 0.85f, 0.2f),
            HangarManager.CrateType.GoldAce,
            out _goldKeyBtn, out _goldKeyBtnTMP, out _goldKeyBadgeTMP);
    }

    private void CreateCrateCardOverlay(string objName, float centerX1080,
        Color accentColor, HangarManager.CrateType type,
        out Button keyBtn, out TextMeshProUGUI keyBtnTMP, out TextMeshProUGUI keyBadgeTMP)
    {
        // 1. Kart İçi Dinamik Anahtar Sayısı Satırı (y0 = 342..386 -> centerY = 540 - 364 = +176.0f)
        GameObject kbObj = new GameObject(objName + "_KeyBadge", typeof(RectTransform));
        kbObj.transform.SetParent(_panelRoot.transform, false);
        HangarUI.SetNormalizedRect1080p(GetRect(kbObj), new Vector2(centerX1080, 176.0f), new Vector2(460.0f, 36.0f));

        keyBadgeTMP = kbObj.AddComponent<TextMeshProUGUI>();
        keyBadgeTMP.font = MainMenuManager.GetTacticalFont();
        keyBadgeTMP.text = "ANAHTARI: 0 ADET";
        keyBadgeTMP.fontSize = 18.5f;
        keyBadgeTMP.characterSpacing = 1.2f;
        keyBadgeTMP.fontStyle = FontStyles.Bold;
        keyBadgeTMP.alignment = TextAlignmentOptions.Center;
        keyBadgeTMP.color = new Color(0.58f, 0.64f, 0.72f);
        keyBadgeTMP.raycastTarget = false;

        // 2. Kartın Kendisine veya Alt Çerçeveli Butona Tıklanarak Sandık Açma Butonu (y0 = 814..902 -> centerY = 540 - 858 = -318.0f)
        HangarManager.CrateType capturedType = type;
        keyBtn = HangarUI.FormatPixelExactButton(
            objName + "_OpenBtn",
            _panelRoot.transform,
            new Vector2(centerX1080, -318.0f),
            new Vector2(458.0f, 88.0f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.14f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.26f),
            () => OpenCrateWithKey(capturedType));

        GameObject ktTxtObj = new GameObject("Text", typeof(RectTransform));
        ktTxtObj.transform.SetParent(keyBtn.transform, false);
        RectTransform ktRt = GetRect(ktTxtObj);
        ktRt.anchorMin = new Vector2(0.03f, 0.06f);
        ktRt.anchorMax = new Vector2(0.97f, 0.94f);
        ktRt.offsetMin = Vector2.zero;
        ktRt.offsetMax = Vector2.zero;

        keyBtnTMP = ktTxtObj.AddComponent<TextMeshProUGUI>();
        keyBtnTMP.font = MainMenuManager.GetTacticalFont();
        keyBtnTMP.text = "ANAHTAR İLE AÇ";
        keyBtnTMP.fontSize = 15.5f;
        keyBtnTMP.characterSpacing = 1.0f;
        keyBtnTMP.lineSpacing = 1.0f;
        keyBtnTMP.fontStyle = FontStyles.Bold;
        keyBtnTMP.alignment = TextAlignmentOptions.Center;
        keyBtnTMP.color = Color.white;
        keyBtnTMP.raycastTarget = false;
    }

    private void BuildBottomBar()
    {
        // Sol Alt « GERİ DÖN Butonu (Arka plan görselindeki x=118..434, y=982..1046 çerçevesine birebir kilitli)
        HangarUI.FormatPixelExactButton(
            "BackFromCratesButton",
            _panelRoot.transform,
            new Vector2(-684.0f, -474.0f),
            new Vector2(316.0f, 64.0f),
            new Color(0f, 0.85f, 1f, 0.18f),
            new Color(0f, 0.7f, 0.95f, 0.35f),
            CloseCratesPanel);
    }

    // ==================== SİNEMATİK SANDIK AÇILMA SAHNESİ ====================
    private void BuildCinematicOpeningStage()
    {
        _cinematicOverlayObj = new GameObject("CinematicCrateOpeningOverlay", typeof(RectTransform));
        _cinematicOverlayObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform oRt = GetRect(_cinematicOverlayObj);
        MakeFullScreenRect(oRt);

        Image overlayBg = _cinematicOverlayObj.AddComponent<Image>();
        overlayBg.color = new Color(0f, 0.02f, 0.05f, 0.94f); // Karartılmış odak arka planı

        // 1. Dönen Işık Huzmesi / Sunburst Efekti
        GameObject rayObj = new GameObject("CinematicSunburst", typeof(RectTransform));
        rayObj.transform.SetParent(_cinematicOverlayObj.transform, false);
        RectTransform rayRt = GetRect(rayObj);
        rayRt.anchorMin = new Vector2(0.5f, 0.52f);
        rayRt.anchorMax = new Vector2(0.5f, 0.52f);
        rayRt.sizeDelta = new Vector2(750f, 750f);

        _cinematicSunburstImg = rayObj.AddComponent<Image>();
        _cinematicSunburstImg.sprite = GetSunburstSprite();
        _cinematicSunburstImg.color = new Color(1f, 0.88f, 0.35f, 0.35f);

        // 2. Büyük Merkez Sandık Görseli
        GameObject crateCenterObj = new GameObject("CinematicCrateImage", typeof(RectTransform));
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
        _cinematicRewardCardObj = new GameObject("RewardFloatingCard", typeof(RectTransform));
        _cinematicRewardCardObj.transform.SetParent(_cinematicOverlayObj.transform, false);
        _cinematicRewardCardRt = GetRect(_cinematicRewardCardObj);
        _cinematicRewardCardRt.anchorMin = new Vector2(0.5f, 0.5f);
        _cinematicRewardCardRt.anchorMax = new Vector2(0.5f, 0.5f);
        _cinematicRewardCardRt.pivot = new Vector2(0.5f, 0.5f);
        _cinematicRewardCardRt.anchoredPosition = new Vector2(0f, 150f);
        _cinematicRewardCardRt.sizeDelta = new Vector2(660f, 330f);

        Image cardBg = _cinematicRewardCardObj.AddComponent<Image>();
        cardBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(1f, 0.82f, 0.2f), new Color(0.04f, 0.07f, 0.14f, 0.98f), false);
        cardBg.type = Image.Type.Sliced;
        cardBg.color = Color.white;

        // Ödül Başlığı
        GameObject tObj = new GameObject("RewardTitle", typeof(RectTransform));
        tObj.transform.SetParent(_cinematicRewardCardObj.transform, false);
        RectTransform tRt = GetRect(tObj);
        tRt.anchorMin = new Vector2(0f, 0.65f);
        tRt.anchorMax = new Vector2(1f, 0.96f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        _rewardTitleTMP = tObj.AddComponent<TextMeshProUGUI>();
        _rewardTitleTMP.font = MainMenuManager.GetTacticalFont();
        _rewardTitleTMP.fontSize = 28f;
        _rewardTitleTMP.characterSpacing = 2.0f;
        _rewardTitleTMP.fontStyle = FontStyles.Bold;
        _rewardTitleTMP.alignment = TextAlignmentOptions.Center;
        _rewardTitleTMP.color = new Color(1f, 0.88f, 0.25f);

        // Ödül Açıklaması
        GameObject dObj = new GameObject("RewardDesc", typeof(RectTransform));
        dObj.transform.SetParent(_cinematicRewardCardObj.transform, false);
        RectTransform dRt = GetRect(dObj);
        dRt.anchorMin = new Vector2(0.05f, 0.28f);
        dRt.anchorMax = new Vector2(0.95f, 0.65f);
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;

        _rewardDescTMP = dObj.AddComponent<TextMeshProUGUI>();
        _rewardDescTMP.font = MainMenuManager.GetTacticalFont();
        _rewardDescTMP.fontSize = 22f;
        _rewardDescTMP.characterSpacing = 1.4f;
        _rewardDescTMP.lineSpacing = 4f;
        _rewardDescTMP.alignment = TextAlignmentOptions.Center;
        _rewardDescTMP.color = Color.white;

        // Ödülü Al ve Envantere Ekle Butonu
        GameObject okObj = new GameObject("ClaimRewardBtn", typeof(RectTransform));
        okObj.transform.SetParent(_cinematicRewardCardObj.transform, false);
        RectTransform okRt = GetRect(okObj);
        okRt.anchorMin = new Vector2(0.22f, 0.06f);
        okRt.anchorMax = new Vector2(0.78f, 0.24f);
        okRt.offsetMin = Vector2.zero;
        okRt.offsetMax = Vector2.zero;

        Image okImg = okObj.AddComponent<Image>();
        okImg.sprite = MainMenuManager.GetHudSprite("hud_btn_emerald", new Color(0.18f, 0.85f, 0.45f), new Color(0.04f, 0.12f, 0.08f, 0.95f), true);
        okImg.type = Image.Type.Sliced;
        okImg.color = Color.white;

        _claimRewardBtn = okObj.AddComponent<Button>();
        _claimRewardBtn.onClick.AddListener(() =>
        {
            _cinematicOverlayObj.SetActive(false);
            _isOpeningCrate = false;
            RefreshVisuals();
        });

        GameObject okTxt = new GameObject("Text", typeof(RectTransform));
        okTxt.transform.SetParent(okObj.transform, false);
        RectTransform otRt = GetRect(okTxt);
        otRt.anchorMin = Vector2.zero;
        otRt.anchorMax = Vector2.one;
        otRt.offsetMin = Vector2.zero;
        otRt.offsetMax = Vector2.zero;

        TextMeshProUGUI okTMP = okTxt.AddComponent<TextMeshProUGUI>();
        okTMP.font = MainMenuManager.GetTacticalFont();
        okTMP.text = "<b>ÖDÜLÜ AL VE DEVAM ET</b>";
        okTMP.fontSize = 22f;
        okTMP.characterSpacing = 2.0f;
        okTMP.fontStyle = FontStyles.Bold;
        okTMP.alignment = TextAlignmentOptions.Center;
        okTMP.color = Color.white;

        _cinematicOverlayObj.SetActive(false);
    }

    private void BuildAlertToast()
    {
        _keyAlertToastObj = new GameObject("KeyAlertToast", typeof(RectTransform));
        _keyAlertToastObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform tRt = GetRect(_keyAlertToastObj);
        tRt.anchorMin = new Vector2(0.5f, 0.18f);
        tRt.anchorMax = new Vector2(0.5f, 0.18f);
        tRt.pivot = new Vector2(0.5f, 0.5f);
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta = new Vector2(850f, 68f);

        Image bg = _keyAlertToastObj.AddComponent<Image>();
        bg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(1f, 0.25f, 0.25f, 0.95f), new Color(0.12f, 0.02f, 0.03f, 0.96f), true);
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;

        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(_keyAlertToastObj.transform, false);
        MakeFullScreenRect(GetRect(txtObj));

        _keyAlertTMP = txtObj.AddComponent<TextMeshProUGUI>();
        _keyAlertTMP.font = MainMenuManager.GetTacticalFont();
        _keyAlertTMP.fontSize = 21f;
        _keyAlertTMP.characterSpacing = 1.2f;
        _keyAlertTMP.alignment = TextAlignmentOptions.Center;
        _keyAlertTMP.color = Color.white;
        _keyAlertTMP.raycastTarget = false;

        _keyAlertToastObj.SetActive(false);
    }

    public void ShowKeyAlert(string message)
    {
        if (_keyAlertToastObj == null || _keyAlertTMP == null) return;
        if (_keyAlertCoroutine != null) StopCoroutine(_keyAlertCoroutine);
        _keyAlertCoroutine = StartCoroutine(KeyAlertRoutine(message));
    }

    private IEnumerator KeyAlertRoutine(string message)
    {
        _keyAlertTMP.text = message;
        _keyAlertToastObj.SetActive(true);
        _keyAlertToastObj.transform.SetAsLastSibling();

        CanvasGroup cg = _keyAlertToastObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = _keyAlertToastObj.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        yield return new WaitForSecondsRealtime(2.8f);

        float fade = 0.4f;
        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / fade);
            yield return null;
        }
        _keyAlertToastObj.SetActive(false);
        _keyAlertCoroutine = null;
    }

    private void OpenCrate(HangarManager.CrateType type)
    {
        OpenCrateWithKey(type);
    }

    private void OpenCrateWithKey(HangarManager.CrateType type)
    {
        if (_isOpeningCrate) return;

        HangarManager hm = HangarManager.EnsureInstance();
        HangarManager.KeyType keyNeeded = type switch
        {
            HangarManager.CrateType.Bronze  => HangarManager.KeyType.Bronze,
            HangarManager.CrateType.Silver  => HangarManager.KeyType.Silver,
            HangarManager.CrateType.GoldAce => HangarManager.KeyType.Gold,
            _                               => HangarManager.KeyType.Bronze
        };

        int currentKeys = hm.GetKeyCount(keyNeeded);
        if (currentKeys <= 0)
        {
            string keyName = keyNeeded switch
            {
                HangarManager.KeyType.Bronze => "BRONZ",
                HangarManager.KeyType.Silver => "GÜMÜŞ",
                HangarManager.KeyType.Gold   => "ALTIN AS",
                _                            => "ÖZEL"
            };
            ShowKeyAlert($"<color=#EF4444>[KİLİTLİ]</color> <b>{keyName} ANAHTARI GEREKLİ!</b>\n<size=80%><color=#CBD5E1>Envanterde anahtar bulunmuyor. Seviye 20+ Bossları yok ederek veya Günlük Görevlerden anahtar kazanın!</color></size>");
            AudioManager.Instance?.PlayWarningAlarm();
            RefreshVisuals();
            return;
        }

        var reward = hm.OpenCrateWithKey(type);
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

    // ==================== 3D GERÇEK SANDIK DOKULARI ====================
    public static Sprite GetCrateSprite(HangarManager.CrateType type, bool isOpen)
    {
        if (_cachedClosedSprites == null) _cachedClosedSprites = new Sprite[3];
        if (_cachedOpenSprites == null) _cachedOpenSprites = new Sprite[3];

        int idx = Mathf.Clamp((int)type, 0, 2);
        if (!isOpen && _cachedClosedSprites[idx] != null) return _cachedClosedSprites[idx];
        if (isOpen && _cachedOpenSprites[idx] != null) return _cachedOpenSprites[idx];

        string[] paths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, $"Sprites/crate_3d_{idx}.png"),
            System.IO.Path.Combine(Application.dataPath, $"Resources/crate_3d_{idx}.png")
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
                    Sprite spr3d = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    _cachedClosedSprites[idx] = spr3d;
                    _cachedOpenSprites[idx] = spr3d;
                    return spr3d;
                }
            }
        }

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
