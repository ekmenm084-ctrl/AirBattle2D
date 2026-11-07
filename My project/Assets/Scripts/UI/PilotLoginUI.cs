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
        GameObject go = existing != null ? existing.gameObject : new GameObject("PilotLoginUI");
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
            // İlk girişte iptal butonu gizlenir veya vazgeçme yerine varsayılanla devam ettirilir
            _cancelButton.gameObject.SetActive(!_isFirstTimeLogin);
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
        _modalRoot = new GameObject("PilotLoginModalRoot");
        _modalRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = _modalRoot.AddComponent<RectTransform>();
        MainMenuManager.MakeFullScreenRect(rootRt);

        Image bg = _modalRoot.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.05f, 0.09f, 0.94f); // Derin askeri kokpit gecesi

        // 2. Ana Askeri Kart / İletişim Kutusu (Modal Box)
        GameObject boxObj = new GameObject("PilotCardBox");
        boxObj.transform.SetParent(_modalRoot.transform, false);
        RectTransform bRt = boxObj.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 0.5f);
        bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.anchoredPosition = Vector2.zero;
        bRt.sizeDelta = new Vector2(620f, 440f);

        Image bBg = boxObj.AddComponent<Image>();
        bBg.color = new Color(0.06f, 0.11f, 0.18f, 0.98f);

        Outline bOutline = boxObj.AddComponent<Outline>();
        bOutline.effectColor = new Color(0f, 0.88f, 1f, 0.9f); // Neon Cyan
        bOutline.effectDistance = new Vector2(3f, -3f);

        // Şık Üst Başlık Şeridi
        GameObject headerStrip = new GameObject("HeaderStrip");
        headerStrip.transform.SetParent(boxObj.transform, false);
        RectTransform hsRt = headerStrip.AddComponent<RectTransform>();
        hsRt.anchorMin = new Vector2(0f, 1f);
        hsRt.anchorMax = new Vector2(1f, 1f);
        hsRt.pivot = new Vector2(0.5f, 1f);
        hsRt.anchoredPosition = Vector2.zero;
        hsRt.sizeDelta = new Vector2(0f, 60f);

        Image hsImg = headerStrip.AddComponent<Image>();
        hsImg.color = new Color(0.09f, 0.18f, 0.28f, 1f);

        // Başlık Metni
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(headerStrip.transform, false);
        RectTransform tRt = titleObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "✈️ ASKERİ UÇUŞ KARTI & PİLOT GİRİŞİ ✈️";
        titleTMP.fontSize = 20f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0f, 0.95f, 1f);

        // Alt Bilgi / Açıklama
        GameObject subObj = new GameObject("SubTitleText");
        subObj.transform.SetParent(boxObj.transform, false);
        RectTransform sRt = subObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, -75f);
        sRt.sizeDelta = new Vector2(580f, 40f);

        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.text = "Liderlik tablosunda ve telsiz konuşmalarında diğer pilotların sizi göreceği <b>Çağrı Kodunuzu (Takma Ad)</b> belirleyin:";
        subTMP.fontSize = 13.5f;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = new Color(0.85f, 0.9f, 0.95f);

        // Input Field Arka Planı
        GameObject inpObj = new GameObject("NameInputField");
        inpObj.transform.SetParent(boxObj.transform, false);
        RectTransform inRt = inpObj.AddComponent<RectTransform>();
        inRt.anchorMin = new Vector2(0.5f, 0.5f);
        inRt.anchorMax = new Vector2(0.5f, 0.5f);
        inRt.pivot = new Vector2(0.5f, 0.5f);
        inRt.anchoredPosition = new Vector2(0f, 45f);
        inRt.sizeDelta = new Vector2(440f, 52f);

        Image inBg = inpObj.AddComponent<Image>();
        inBg.color = new Color(0.03f, 0.07f, 0.12f, 1f);

        Outline inOutline = inpObj.AddComponent<Outline>();
        inOutline.effectColor = new Color(0f, 0.85f, 1f, 0.7f);
        inOutline.effectDistance = new Vector2(2f, -2f);

        // Text Component
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inpObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(16f, 4f);
        textRt.offsetMax = new Vector2(-16f, -4f);

        TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 19f;
        inputText.fontStyle = FontStyles.Bold;
        inputText.alignment = TextAlignmentOptions.Center;
        inputText.color = Color.white;

        // Placeholder Text
        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(inpObj.transform, false);
        RectTransform phRt = phObj.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(16f, 4f);
        phRt.offsetMax = new Vector2(-16f, -4f);

        TextMeshProUGUI phText = phObj.AddComponent<TextMeshProUGUI>();
        phText.text = "Çağrı Kodu Girin (Örn: Mustafa)...";
        phText.fontSize = 17f;
        phText.fontStyle = FontStyles.Italic;
        phText.alignment = TextAlignmentOptions.Center;
        phText.color = new Color(0.5f, 0.6f, 0.7f, 0.6f);

        _inputField = inpObj.AddComponent<TMP_InputField>();
        _inputField.textComponent = inputText;
        _inputField.placeholder = phText;
        _inputField.characterLimit = 14;
        _inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
        _inputField.onValueChanged.AddListener(OnInputChanged);

        // Karakter Sayacı
        GameObject countObj = new GameObject("CharCounter");
        countObj.transform.SetParent(boxObj.transform, false);
        RectTransform cntRt = countObj.AddComponent<RectTransform>();
        cntRt.anchorMin = new Vector2(0.5f, 0.5f);
        cntRt.anchorMax = new Vector2(0.5f, 0.5f);
        cntRt.pivot = new Vector2(1f, 0.5f);
        cntRt.anchoredPosition = new Vector2(220f, 10f);
        cntRt.sizeDelta = new Vector2(150f, 20f);

        _charCountTMP = countObj.AddComponent<TextMeshProUGUI>();
        _charCountTMP.fontSize = 11.5f;
        _charCountTMP.alignment = TextAlignmentOptions.Right;
        _charCountTMP.color = new Color(0.6f, 0.75f, 0.9f);

        // Doğrulama / Uyarı Mesajı
        GameObject valObj = new GameObject("ValidationMessage");
        valObj.transform.SetParent(boxObj.transform, false);
        RectTransform vRt = valObj.AddComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0.5f, 0.5f);
        vRt.anchorMax = new Vector2(0.5f, 0.5f);
        vRt.pivot = new Vector2(0.5f, 0.5f);
        vRt.anchoredPosition = new Vector2(0f, -12f);
        vRt.sizeDelta = new Vector2(500f, 26f);

        _validationTMP = valObj.AddComponent<TextMeshProUGUI>();
        _validationTMP.fontSize = 12.5f;
        _validationTMP.alignment = TextAlignmentOptions.Center;
        _validationTMP.color = new Color(0.9f, 0.9f, 0.9f);

        // 🎲 Rastgele İsim Üret Butonu
        GameObject rndBtnObj = CreateButton(boxObj.transform, "RandomNameBtn", "🎲 RASTGELE ÇAĞRI KODU",
            new Vector2(0f, -55f), new Vector2(260f, 38f),
            new Color(0.12f, 0.22f, 0.35f), GenerateRandomCallsign);
        _randomNameButton = rndBtnObj.GetComponent<Button>();

        // 3. Alt Aksiyon Butonları
        // Onayla ve Savaşa Katıl Butonu
        GameObject saveBtnObj = CreateButton(boxObj.transform, "SaveCallsignBtn", "✓ ONAYLA VE GÖREVE BAŞLA",
            new Vector2(0f, -125f), new Vector2(300f, 50f),
            new Color(0.12f, 0.55f, 0.32f), OnSaveClicked);
        _saveButton = saveBtnObj.GetComponent<Button>();

        // İptal Butonu
        GameObject cancelBtnObj = CreateButton(boxObj.transform, "CancelBtn", "KAPAT",
            new Vector2(0f, -182f), new Vector2(160f, 34f),
            new Color(0.40f, 0.20f, 0.20f), CloseModal);
        _cancelButton = cancelBtnObj.GetComponent<Button>();

        _modalRoot.SetActive(false);
    }

    private GameObject CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor, Action onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
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

