using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Pilot Çağrı Kodu / Kullanıcı Adı Giriş Ekranı (Pilot Login & Callsign Modal):
/// - Oyuna ilk girişte otomatik olarak açılır ve oyuncudan takma ad / çağrı kodu girmesini ister.
/// - Belirlenen isim Küresel Liderlik Tablosunda (Leaderboard), Ana Menü profil rozetinde ve telsiz anonslarında görünür.
/// - 🎲 Rastgele havacılık çağrı kodu üreteci (Mustafa_Ace, KaanLeader, GhostRider, SkyReaper vb.) içerir.
/// - Minimum 3, maksimum 14 karakter doğrulaması ve URL-güvenli temizleme uygular.
/// - İstenildiğinde Ana Menüden veya Skor Tablosundan tekrar açılarak isim güncellenebilir.
/// </summary>
public class PilotLoginUI : MonoBehaviour
{
    public static PilotLoginUI Instance { get; private set; }

    private GameObject _modalRoot;
    private TMP_InputField _inputField;
    private TextMeshProUGUI _validationTMP;
    private TextMeshProUGUI _charCountTMP;
    private Button _saveButton;
    private Button _cancelButton;
    private Button _randomNameButton;

    private Action _onSaveCallback;
    private bool _isFirstTimeLogin = false;

    // Havacılık temalı elit çağrı kodu önerileri
    private static readonly string[] RANDOM_CALLSIGNS = new string[]
    {
        "Mustafa_Ace", "KaanLeader", "GhostRider", "Viper_99", "SkyReaper",
        "Phoenix_TR", "ShadowJet", "EagleEye", "Thunder_01", "IronWing",
        "Korkusuz", "GokAsi", "CelikYigit", "BordoBere", "KurtSurusu",
        "Firtina_Air", "Atmaca_Ace", "Pars_06", "Hancer_Air", "Yagiz_Pilot",
        "Apex_Striker", "StormHunter", "Titan_Lead", "NightHawk", "Delta_One"
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static PilotLoginUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        EnsureEventSystem();

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("PilotLoginUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("PilotLoginUI", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        Instance = go.GetComponent<PilotLoginUI>();
        if (Instance == null) Instance = go.AddComponent<PilotLoginUI>();
        return Instance;
    }

    /// <summary>
    /// TMP_InputField klavye ve fare tıklamalarını yakalayabilmesi için sahnede EventSystem bulunmasını garanti eder.
    /// </summary>
    public static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }
    }

    /// <summary>
    /// Oyuncu daha önce kendi özel çağrı kodunu belirlemediyse ilk açılış ekranını gösterir.
    /// </summary>
    public void CheckFirstTimeLogin()
    {
        if (!LeaderboardManager.HasSetCustomCallsign)
        {
            OpenLoginModal(null, isMandatoryFirstTime: true);
        }
    }

    /// <summary>
    /// Giriş / Çağrı Kodu belirleme penceresini açar.
    /// </summary>
    public void OpenLoginModal(Action onSaved = null, bool isMandatoryFirstTime = false)
    {
        EnsureEventSystem();
        _onSaveCallback = onSaved;
        _isFirstTimeLogin = isMandatoryFirstTime;

        EnsureModalStructure();

        if (_modalRoot != null)
        {
            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
        }

        if (_cancelButton != null)
        {
            // Kullanıcı dilediğinde kapatıp ana menüye dönebilir veya varsayılan çağrı koduyla devam edebilir
            _cancelButton.gameObject.SetActive(true);
        }

        string currentName = LeaderboardManager.PlayerCallsign;
        if (_inputField != null)
        {
            _inputField.text = currentName;
            _inputField.ActivateInputField();
            _inputField.Select();
        }

        if (_validationTMP != null)
        {
            _validationTMP.text = _isFirstTimeLogin
                ? "<color=#8AB4F8>Lütfen hava harekatında kullanılacak pilot çağrı kodunuzu belirleyin.</color>"
                : "<color=#8AB4F8>Yeni çağrı kodunuz skor tablosunda anında güncellenecektir.</color>";
        }

        UpdateCharCount(currentName);
    }

    public void CloseModal()
    {
        if (_modalRoot != null)
            _modalRoot.SetActive(false);
    }

    private void EnsureModalStructure()
    {
        if (_modalRoot != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. Karartma Arka Planı (Full Screen Overlay)
        _modalRoot = new GameObject("PilotLoginModalRoot", typeof(RectTransform));
        _modalRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = _modalRoot.GetComponent<RectTransform>();
        MainMenuManager.MakeFullScreenRect(rootRt);

        Image bg = _modalRoot.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.04f, 0.08f, 0.92f); // Derin aero-glass askeri kokpit gecesi

        // 2. Ana Askeri Kart / İletişim Kutusu (Modal Box)
        GameObject boxObj = new GameObject("PilotCardBox", typeof(RectTransform));
        boxObj.transform.SetParent(_modalRoot.transform, false);
        RectTransform bRt = boxObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 0.5f);
        bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.anchoredPosition = Vector2.zero;
        bRt.sizeDelta = new Vector2(640f, 450f);

        Image bBg = boxObj.AddComponent<Image>();
        bBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.03f, 0.07f, 0.14f, 0.96f), false);
        bBg.type = Image.Type.Sliced;
        bBg.color = Color.white;

        // Şık Üst Başlık Vizörü
        GameObject headerStrip = new GameObject("HeaderStrip", typeof(RectTransform));
        headerStrip.transform.SetParent(boxObj.transform, false);
        RectTransform hsRt = headerStrip.GetComponent<RectTransform>();
        hsRt.anchorMin = new Vector2(0f, 1f);
        hsRt.anchorMax = new Vector2(1f, 1f);
        hsRt.pivot = new Vector2(0.5f, 1f);
        hsRt.anchoredPosition = new Vector2(0f, -10f);
        hsRt.sizeDelta = new Vector2(600f, 54f);

        Image hsImg = headerStrip.AddComponent<Image>();
        hsImg.sprite = MainMenuManager.GetHudSprite("hud_header_visor", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.04f, 0.11f, 0.20f, 1f), true);
        hsImg.type = Image.Type.Sliced;
        hsImg.color = Color.white;

        // Başlık Metni
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform));
        titleObj.transform.SetParent(headerStrip.transform, false);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.font = MainMenuManager.GetTacticalFont();
        titleTMP.text = "DEFCON // PİLOT UÇUŞ KARTI & GİRİŞ";
        titleTMP.fontSize = 19f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 2.0f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0f, 0.95f, 1f);

        // Alt Bilgi / Açıklama
        GameObject subObj = new GameObject("SubTitleText", typeof(RectTransform));
        subObj.transform.SetParent(boxObj.transform, false);
        RectTransform sRt = subObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, -75f);
        sRt.sizeDelta = new Vector2(580f, 40f);

        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.font = MainMenuManager.GetTacticalFont();
        subTMP.text = "Liderlik tablosunda ve telsiz anonslarında görünecek <b>Çağrı Kodunuzu</b> belirleyin:";
        subTMP.fontSize = 13f;
        subTMP.characterSpacing = 1.2f;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = new Color(0.85f, 0.9f, 0.95f);

        // Input Field Arka Planı
        GameObject inpObj = new GameObject("NameInputField", typeof(RectTransform));
        inpObj.transform.SetParent(boxObj.transform, false);
        RectTransform inRt = inpObj.GetComponent<RectTransform>();
        inRt.anchorMin = new Vector2(0.5f, 0.5f);
        inRt.anchorMax = new Vector2(0.5f, 0.5f);
        inRt.pivot = new Vector2(0.5f, 0.5f);
        inRt.anchoredPosition = new Vector2(0f, 48f);
        inRt.sizeDelta = new Vector2(460f, 50f);

        Image inBg = inpObj.AddComponent<Image>();
        inBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.7f), new Color(0.02f, 0.05f, 0.09f, 1f), false);
        inBg.type = Image.Type.Sliced;
        inBg.color = Color.white;

        // Text Component
        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(inpObj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(16f, 4f);
        textRt.offsetMax = new Vector2(-16f, -4f);

        TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
        inputText.font = MainMenuManager.GetTacticalFont();
        inputText.fontSize = 18f;
        inputText.fontStyle = FontStyles.Bold;
        inputText.characterSpacing = 1.6f;
        inputText.alignment = TextAlignmentOptions.Center;
        inputText.color = Color.white;

        // Placeholder Text
        GameObject phObj = new GameObject("Placeholder", typeof(RectTransform));
        phObj.transform.SetParent(inpObj.transform, false);
        RectTransform phRt = phObj.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(16f, 4f);
        phRt.offsetMax = new Vector2(-16f, -4f);

        TextMeshProUGUI phText = phObj.AddComponent<TextMeshProUGUI>();
        phText.font = MainMenuManager.GetTacticalFont();
        phText.text = "Çağrı Kodu Girin (Örn: Mustafa)...";
        phText.fontSize = 15f;
        phText.fontStyle = FontStyles.Italic;
        phText.characterSpacing = 1.2f;
        phText.alignment = TextAlignmentOptions.Center;
        phText.color = new Color(0.5f, 0.6f, 0.7f, 0.6f);

        _inputField = inpObj.AddComponent<TMP_InputField>();
        _inputField.textComponent = inputText;
        _inputField.placeholder = phText;
        _inputField.characterLimit = 14;
        _inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
        _inputField.onValueChanged.AddListener(OnInputChanged);

        // Karakter Sayacı
        GameObject countObj = new GameObject("CharCounter", typeof(RectTransform));
        countObj.transform.SetParent(boxObj.transform, false);
        RectTransform cntRt = countObj.GetComponent<RectTransform>();
        cntRt.anchorMin = new Vector2(0.5f, 0.5f);
        cntRt.anchorMax = new Vector2(0.5f, 0.5f);
        cntRt.pivot = new Vector2(1f, 0.5f);
        cntRt.anchoredPosition = new Vector2(230f, 12f);
        cntRt.sizeDelta = new Vector2(150f, 20f);

        _charCountTMP = countObj.AddComponent<TextMeshProUGUI>();
        _charCountTMP.font = MainMenuManager.GetTacticalFont();
        _charCountTMP.fontSize = 12f;
        _charCountTMP.alignment = TextAlignmentOptions.Right;
        _charCountTMP.color = new Color(0.6f, 0.75f, 0.9f);

        // Doğrulama / Uyarı Mesajı
        GameObject valObj = new GameObject("ValidationMessage", typeof(RectTransform));
        valObj.transform.SetParent(boxObj.transform, false);
        RectTransform vRt = valObj.GetComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0.5f, 0.5f);
        vRt.anchorMax = new Vector2(0.5f, 0.5f);
        vRt.pivot = new Vector2(0.5f, 0.5f);
        vRt.anchoredPosition = new Vector2(0f, -12f);
        vRt.sizeDelta = new Vector2(500f, 26f);

        _validationTMP = valObj.AddComponent<TextMeshProUGUI>();
        _validationTMP.font = MainMenuManager.GetTacticalFont();
        _validationTMP.fontSize = 12.5f;
        _validationTMP.characterSpacing = 1.1f;
        _validationTMP.alignment = TextAlignmentOptions.Center;
        _validationTMP.color = new Color(0.9f, 0.9f, 0.9f);

        // Rastgele İsim Üret Butonu
        GameObject rndBtnObj = CreateButton(boxObj.transform, "RandomNameBtn", "🎲 RASTGELE ÇAĞRI KODU",
            new Vector2(0f, -55f), new Vector2(270f, 38f),
            "hud_btn_cyan", new Color(0f, 0.85f, 1f, 1f), new Color(0.04f, 0.12f, 0.20f, 1f), GenerateRandomCallsign);
        _randomNameButton = rndBtnObj.GetComponent<Button>();

        // 3. Alt Aksiyon Butonları
        // Onayla ve Savaşa Katıl Butonu
        GameObject saveBtnObj = CreateButton(boxObj.transform, "SaveCallsignBtn", "✓ ONAYLA VE GÖREVE BAŞLA",
            new Vector2(0f, -125f), new Vector2(320f, 50f),
            "hud_btn_emerald", new Color(0.2f, 0.95f, 0.4f, 1f), new Color(0.04f, 0.16f, 0.08f, 1f), OnSaveClicked);
        _saveButton = saveBtnObj.GetComponent<Button>();

        // İptal Butonu
        GameObject cancelBtnObj = CreateButton(boxObj.transform, "CancelBtn", "◀ KAPAT",
            new Vector2(0f, -182f), new Vector2(170f, 36f),
            "hud_btn_red", new Color(1f, 0.35f, 0.35f, 1f), new Color(0.24f, 0.05f, 0.05f, 1f), CloseModal);
        _cancelButton = cancelBtnObj.GetComponent<Button>();

        _modalRoot.SetActive(false);
    }

    private GameObject CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, string spriteKey, Color borderColor, Color fillColor, Action onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.sprite = MainMenuManager.GetHudSprite(spriteKey, borderColor, fillColor, true);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = label;
        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btnObj;
    }

    private void OnInputChanged(string text)
    {
        UpdateCharCount(text);
    }

    private void UpdateCharCount(string text)
    {
        int len = string.IsNullOrEmpty(text) ? 0 : text.Length;
        if (_charCountTMP != null)
        {
            _charCountTMP.text = $"{len} / 14";
        }
    }

    private void GenerateRandomCallsign()
    {
        string rnd = RANDOM_CALLSIGNS[UnityEngine.Random.Range(0, RANDOM_CALLSIGNS.Length)];
        if (_inputField != null)
        {
            _inputField.text = rnd;
            _inputField.ActivateInputField();
        }
        UpdateCharCount(rnd);
        if (_validationTMP != null)
        {
            _validationTMP.text = $"<color=#00E5FF>Rastgele çağrı kodu seçildi: <b>{rnd}</b></color>";
        }
    }

    private void OnSaveClicked()
    {
        string input = _inputField != null ? _inputField.text.Trim() : "";

        // Minimum 3 karakter denetimi
        if (string.IsNullOrEmpty(input) || input.Length < 3)
        {
            if (_validationTMP != null)
            {
                _validationTMP.text = "<color=#FF5252>⚠️ Çağrı kodu en az 3 karakter uzunluğunda olmalıdır!</color>";
            }
            return;
        }

        // İsim kaydı
        LeaderboardManager.PlayerCallsign = input;

        // UI Güncellemeleri
        PlayerProfileUI.Instance?.RefreshBadgeVisuals();
        LeaderboardUI.Instance?.RefreshScores();

        CloseModal();

        _onSaveCallback?.Invoke();
        _onSaveCallback = null;
    }
}

