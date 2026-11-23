using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Supabase Veritabanı Destekli Pilot Kullanıcı Adı & Şifre Giriş / Kayıt Ekranı:
/// - Her oyun açılışında zorunlu olarak açılır (PilotAuthManager.IsAuthenticatedInSession == false).
/// - Kullanıcılar [ GİRİŞ YAP ] veya [ KAYIT OL ] sekmeleriyle kullanıcı adı ve şifre kullanarak giriş yapar.
/// - Şifreler gizli (Password) olarak girilir ve Supabase PostgreSQL (pgcrypto/bcrypt) üzerinde doğrulanır.
/// - Giriş yapan pilotun kendi bağımsız Seviye, Altın, Hangar, İHA ve Rekor profili anında yüklenir.
/// </summary>
public class PilotLoginUI : MonoBehaviour
{
    public static PilotLoginUI Instance { get; private set; }

    private GameObject _modalRoot;
    private RectTransform _cardRt;

    private Button _loginTabBtn;
    private Button _registerTabBtn;

    private TMP_InputField _usernameInput;
    private TMP_InputField _passwordInput;
    private GameObject _confirmPasswordGroup;
    private TMP_InputField _confirmPasswordInput;

    private TextMeshProUGUI _userLabelTMP;
    private TextMeshProUGUI _passLabelTMP;
    private TextMeshProUGUI _confirmPassLabelTMP;

    private RectTransform _userCaretRt;
    private RectTransform _passCaretRt;
    private RectTransform _confirmCaretRt;

    private Button _togglePwdVisBtn;
    private TextMeshProUGUI _togglePwdVisTMP;
    private bool _showPassword = false;

    private TextMeshProUGUI _validationTMP;
    private Button _submitButton;
    private TextMeshProUGUI _submitBtnTMP;
    private Button _cancelButton;
    private Button _logoutButton;

    private Action _onSaveCallback;
    private bool _isMandatory = false;
    private bool _isRegisterMode = false;
    private bool _isBusy = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_modalRoot == null || !_modalRoot.activeInHierarchy) return;

        // Aktif kutu vurgusu ve Time.timeScale == 0 iken bile çalışan yanıp sönen imleç
        UpdateInputFieldFocusAndCaret(_usernameInput, _userLabelTMP, _userCaretRt);
        UpdateInputFieldFocusAndCaret(_passwordInput, _passLabelTMP, _passCaretRt);
        if (_isRegisterMode)
            UpdateInputFieldFocusAndCaret(_confirmPasswordInput, _confirmPassLabelTMP, _confirmCaretRt);

        if (_isBusy) return;

        // Tab tuşu ile alanlar arası hızlı geçiş
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (_usernameInput != null && _usernameInput.isFocused)
            {
                _passwordInput?.ActivateInputField();
                _passwordInput?.Select();
            }
            else if (_passwordInput != null && _passwordInput.isFocused)
            {
                if (_isRegisterMode && _confirmPasswordInput != null)
                {
                    _confirmPasswordInput.ActivateInputField();
                    _confirmPasswordInput.Select();
                }
                else
                {
                    _usernameInput?.ActivateInputField();
                    _usernameInput?.Select();
                }
            }
            else if (_confirmPasswordInput != null && _confirmPasswordInput.isFocused)
            {
                _usernameInput?.ActivateInputField();
                _usernameInput?.Select();
            }
        }

        // Enter tuşu ile doğrudan gönder
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnSubmitClicked();
        }
    }

    public static PilotLoginUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        EnsureEventSystem();

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("PilotLoginUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("PilotLoginUI", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        Instance = go.GetComponent<PilotLoginUI>();
        if (Instance == null) Instance = go.AddComponent<PilotLoginUI>();
        return Instance;
    }

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
    /// Her oyun açılışında çağrılır: oturumda henüz şifreyle giriş yapılmadıysa zorunlu giriş ekranını açar.
    /// </summary>
    public void CheckFirstTimeLogin()
    {
        PilotAuthManager.EnsureInstance();
        if (!PilotAuthManager.IsAuthenticatedInSession)
        {
            OpenLoginModal(null, isMandatoryFirstTime: true);
        }
    }

    public void OpenLoginModal(Action onSaved = null, bool isMandatoryFirstTime = false)
    {
        EnsureEventSystem();
        PilotAuthManager.EnsureInstance();

        _onSaveCallback = onSaved;
        _isMandatory = isMandatoryFirstTime;
        _isBusy = false;
        _showPassword = false;

        EnsureModalStructure();

        if (_modalRoot != null)
        {
            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
        }

        // Zorunlu açılış girişinde Kapat ve Çıkış butonları gizlenir
        if (_cancelButton != null)
            _cancelButton.gameObject.SetActive(!_isMandatory);
        if (_logoutButton != null)
            _logoutButton.gameObject.SetActive(!_isMandatory && PilotAuthManager.IsAuthenticatedInSession);

        // Daha önce kayıtlı bir kullanıcı adı varsa Giriş sekmesini, yoksa Kayıt sekmesini aç
        string lastUser = PilotAuthManager.LastLoggedInUsername;
        SetAuthMode(isRegister: false);

        if (_usernameInput != null)
        {
            _usernameInput.text = !string.IsNullOrEmpty(lastUser) ? lastUser : "";
        }
        if (_passwordInput != null) _passwordInput.text = "";
        if (_confirmPasswordInput != null) _confirmPasswordInput.text = "";

        ApplyPasswordMasking();

        if (_usernameInput != null && string.IsNullOrEmpty(_usernameInput.text))
        {
            _usernameInput.ActivateInputField();
        }
        else if (_passwordInput != null)
        {
            _passwordInput.ActivateInputField();
        }
    }

    public void CloseModal()
    {
        if (_isMandatory && !PilotAuthManager.IsAuthenticatedInSession) return;
        if (_modalRoot != null)
            _modalRoot.SetActive(false);
    }

    private void SetAuthMode(bool isRegister)
    {
        _isRegisterMode = isRegister;

        if (_confirmPasswordGroup != null)
            _confirmPasswordGroup.SetActive(_isRegisterMode);

        // Sekme görselleri
        UpdateTabVisual(_loginTabBtn, !_isRegisterMode);
        UpdateTabVisual(_registerTabBtn, _isRegisterMode);

        // Kart yüksekliği ve aksiyon buton metni
        if (_cardRt != null)
            _cardRt.sizeDelta = new Vector2(620f, _isRegisterMode ? 650f : 560f);

        if (_submitBtnTMP != null)
            _submitBtnTMP.text = _isRegisterMode ? "<b>HESAP OLUŞTUR VE GİRİŞ YAP</b>" : "<b>GİRİŞ YAP</b>";

        if (_submitButton != null)
        {
            Image sImg = _submitButton.GetComponent<Image>();
            if (sImg != null)
            {
                sImg.sprite = _isRegisterMode
                    ? MainMenuManager.GetRoundedBoxSprite("auth_btn_register", new Color(0.14f, 0.78f, 0.48f, 1f), new Color(0.32f, 0.96f, 0.65f, 1f), 10, 1)
                    : MainMenuManager.GetRoundedBoxSprite("auth_btn_login", new Color(0.24f, 0.86f, 0.96f, 1f), new Color(0.48f, 0.95f, 1f, 1f), 10, 1);
            }
        }

        if (_validationTMP != null)
        {
            _validationTMP.text = _isRegisterMode
                ? "<color=#8FA8BD>Yeni pilot hesabı oluşturun (Kullanıcı adı: 3-14, Şifre: min 4 karakter).</color>"
                : "<color=#8FA8BD>Devam etmek için pilot kullanıcı adınızı ve şifrenizi girin.</color>";
        }

        RepositionBottomElements();
    }

    private void RepositionBottomElements()
    {
        float yVal = _isRegisterMode ? -425f : -335f;
        if (_validationTMP != null)
        {
            RectTransform vRt = _validationTMP.GetComponent<RectTransform>();
            if (vRt != null) vRt.anchoredPosition = new Vector2(0f, yVal);
        }

        if (_submitButton != null)
        {
            RectTransform sRt = _submitButton.GetComponent<RectTransform>();
            if (sRt != null) sRt.anchoredPosition = new Vector2(0f, yVal - 44f);
        }

        float bottomRowY = yVal - 118f;
        if (_cancelButton != null)
        {
            RectTransform cRt = _cancelButton.GetComponent<RectTransform>();
            if (cRt != null)
                cRt.anchoredPosition = (_logoutButton != null && _logoutButton.gameObject.activeSelf)
                    ? new Vector2(-135f, bottomRowY)
                    : new Vector2(0f, bottomRowY);
        }
        if (_logoutButton != null)
        {
            RectTransform lRt = _logoutButton.GetComponent<RectTransform>();
            if (lRt != null) lRt.anchoredPosition = new Vector2(135f, bottomRowY);
        }
    }

    private void UpdateTabVisual(Button tabBtn, bool isActive)
    {
        if (tabBtn == null) return;
        Image img = tabBtn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = isActive
                ? MainMenuManager.GetRoundedBoxSprite("auth_tab_active", new Color(0.08f, 0.18f, 0.27f, 1f), new Color(0f, 0.90f, 1f, 1f), 8, 2)
                : MainMenuManager.GetRoundedBoxSprite("auth_tab_inactive", new Color(0.05f, 0.09f, 0.14f, 0.9f), new Color(0.16f, 0.24f, 0.33f, 0.85f), 8, 1);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        TextMeshProUGUI tmp = tabBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.color = isActive ? Color.white : new Color(0.58f, 0.68f, 0.78f, 0.9f);
        }
    }

    private void EnsureModalStructure()
    {
        if (_modalRoot != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // 1. Tam Ekran Karartma Arka Planı (Tıklamaları bloke eder)
        _modalRoot = new GameObject("PilotLoginModalRoot", typeof(RectTransform));
        _modalRoot.transform.SetParent(canvas.transform, false);
        MainMenuManager.MakeFullScreenRect(_modalRoot.GetComponent<RectTransform>());

        Image bg = _modalRoot.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.04f, 0.08f, 0.94f);
        bg.raycastTarget = true;

        // 2. Ana Taktik Giriş Kartı
        GameObject boxObj = new GameObject("PilotAuthCardBox", typeof(RectTransform));
        boxObj.transform.SetParent(_modalRoot.transform, false);
        _cardRt = boxObj.GetComponent<RectTransform>();
        _cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        _cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        _cardRt.pivot = new Vector2(0.5f, 0.5f);
        _cardRt.anchoredPosition = Vector2.zero;
        _cardRt.sizeDelta = new Vector2(620f, 560f);

        Image bBg = boxObj.AddComponent<Image>();
        bBg.sprite = MainMenuManager.GetRoundedBoxSprite("auth_card_bg", new Color(0.05f, 0.09f, 0.14f, 0.98f), new Color(0.14f, 0.26f, 0.38f, 0.98f), 16, 2);
        bBg.type = Image.Type.Sliced;
        bBg.color = Color.white;

        // Üst Başlık: AIR BATTLE
        CreateLabel(boxObj.transform, "BrandTitle", "<b>AIR BATTLE</b>", new Vector2(0f, -26f), new Vector2(540f, 44f), 34f, Color.white, TextAlignmentOptions.Center, 3.0f);
        CreateLabel(boxObj.transform, "SubHeader", "PİLOT KİMLİK DOĞRULAMA SİSTEMİ", new Vector2(0f, -72f), new Vector2(540f, 22f), 14f, new Color(0f, 0.85f, 1f), TextAlignmentOptions.Center, 2.0f);

        // 3. Sekme Butonları: [ GİRİŞ YAP ] | [ KAYIT OL ]
        _loginTabBtn = CreateActionButton(boxObj.transform, "TabLoginBtn", "<b>GİRİŞ YAP</b>", new Vector2(-135f, -110f), new Vector2(256f, 46f),
            new Color(0.08f, 0.18f, 0.27f, 1f), new Color(0f, 0.90f, 1f, 1f), Color.white, 18f, () => SetAuthMode(false));

        _registerTabBtn = CreateActionButton(boxObj.transform, "TabRegisterBtn", "<b>KAYIT OL</b>", new Vector2(135f, -110f), new Vector2(256f, 46f),
            new Color(0.05f, 0.09f, 0.14f, 0.9f), new Color(0.16f, 0.24f, 0.33f, 0.85f), new Color(0.6f, 0.7f, 0.8f), 18f, () => SetAuthMode(true));

        // 4. KULLANICI ADI Alanı
        _userLabelTMP = CreateLabel(boxObj.transform, "UserLabel", "KULLANICI ADI", new Vector2(0f, -172f), new Vector2(526f, 22f), 14f, new Color(0.52f, 0.64f, 0.74f), TextAlignmentOptions.MidlineLeft, 1.4f);
        _usernameInput = CreateInputField(boxObj.transform, "UsernameInput", new Vector2(0f, -196f), new Vector2(526f, 50f), "Pilot kullanıcı adınızı girin...", 14, TMP_InputField.ContentType.Alphanumeric, out _userCaretRt);

        // 5. ŞİFRE Alanı + Göster/Gizle Butonu
        _passLabelTMP = CreateLabel(boxObj.transform, "PassLabel", "ŞİFRE", new Vector2(0f, -258f), new Vector2(526f, 22f), 14f, new Color(0.52f, 0.64f, 0.74f), TextAlignmentOptions.MidlineLeft, 1.4f);
        _passwordInput = CreateInputField(boxObj.transform, "PasswordInput", new Vector2(-48f, -282f), new Vector2(430f, 50f), "Şifrenizi girin...", 32, TMP_InputField.ContentType.Password, out _passCaretRt);

        _togglePwdVisBtn = CreateActionButton(boxObj.transform, "TogglePwdVisBtn", "GÖSTER", new Vector2(219f, -282f), new Vector2(88f, 50f),
            new Color(0.08f, 0.13f, 0.20f, 1f), new Color(0.18f, 0.28f, 0.38f, 1f), new Color(0.70f, 0.82f, 0.92f), 13f, TogglePasswordVisibility);
        _togglePwdVisTMP = _togglePwdVisBtn.GetComponentInChildren<TextMeshProUGUI>();

        // 6. ŞİFRE TEKRAR Alanı (Sadece KAYIT OL sekmesinde görünür)
        _confirmPasswordGroup = new GameObject("ConfirmPasswordGroup", typeof(RectTransform));
        _confirmPasswordGroup.transform.SetParent(boxObj.transform, false);
        RectTransform cgRt = _confirmPasswordGroup.GetComponent<RectTransform>();
        cgRt.anchorMin = new Vector2(0.5f, 1f);
        cgRt.anchorMax = new Vector2(0.5f, 1f);
        cgRt.pivot = new Vector2(0.5f, 1f);
        cgRt.anchoredPosition = new Vector2(0f, -344f);
        cgRt.sizeDelta = new Vector2(526f, 76f);

        _confirmPassLabelTMP = CreateLabel(_confirmPasswordGroup.transform, "ConfirmPassLabel", "ŞİFRE TEKRAR", new Vector2(0f, 0f), new Vector2(526f, 22f), 14f, new Color(0.52f, 0.64f, 0.74f), TextAlignmentOptions.MidlineLeft, 1.4f);
        _confirmPasswordInput = CreateInputField(_confirmPasswordGroup.transform, "ConfirmPasswordInput", new Vector2(0f, -24f), new Vector2(526f, 50f), "Şifrenizi tekrar girin...", 32, TMP_InputField.ContentType.Password, out _confirmCaretRt);

        // 7. Durum / Hata Mesajı
        _validationTMP = CreateLabel(boxObj.transform, "ValidationMessage", "", new Vector2(0f, -335f), new Vector2(540f, 34f), 16f, new Color(0.85f, 0.90f, 0.95f), TextAlignmentOptions.Center, 0.8f);

        // 8. Ana Gönder Butonu (GİRİŞ YAP / HESAP OLUŞTUR)
        _submitButton = CreateActionButton(boxObj.transform, "SubmitAuthBtn", "<b>GİRİŞ YAP</b>", new Vector2(0f, -379f), new Vector2(526f, 58f),
            new Color(0.24f, 0.86f, 0.96f, 1f), new Color(0.48f, 0.95f, 1f, 1f), new Color(0.03f, 0.07f, 0.12f, 1f), 21f, OnSubmitClicked);
        _submitBtnTMP = _submitButton.GetComponentInChildren<TextMeshProUGUI>();

        // 9. Alt İkincil Butonlar (İsteğe Bağlı Kapat ve Oturumu Kapat)
        _cancelButton = CreateActionButton(boxObj.transform, "CancelBtn", "« KAPAT", new Vector2(-135f, -453f), new Vector2(246f, 44f),
            new Color(0.08f, 0.12f, 0.18f, 0.95f), new Color(0.22f, 0.30f, 0.40f, 0.95f), new Color(0.80f, 0.86f, 0.92f), 16f, CloseModal);

        _logoutButton = CreateActionButton(boxObj.transform, "LogoutBtn", "OTURUMU KAPAT", new Vector2(135f, -453f), new Vector2(246f, 44f),
            new Color(0.32f, 0.08f, 0.12f, 0.95f), new Color(0.82f, 0.22f, 0.28f, 0.95f), Color.white, 16f, OnLogoutClicked);

        _modalRoot.SetActive(false);
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment, float spacing)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = spacing;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private TMP_InputField CreateInputField(Transform parent, string name, Vector2 pos, Vector2 size, string placeholder, int charLimit, TMP_InputField.ContentType contentType, out RectTransform customCaretRt)
    {
        GameObject inpObj = new GameObject(name, typeof(RectTransform));
        inpObj.transform.SetParent(parent, false);
        RectTransform inRt = inpObj.GetComponent<RectTransform>();
        inRt.anchorMin = new Vector2(0.5f, 1f);
        inRt.anchorMax = new Vector2(0.5f, 1f);
        inRt.pivot = new Vector2(0.5f, 1f);
        inRt.anchoredPosition = pos;
        inRt.sizeDelta = size;

        Image inBg = inpObj.AddComponent<Image>();
        inBg.sprite = MainMenuManager.GetRoundedBoxSprite("auth_input_bg", new Color(0.03f, 0.06f, 0.10f, 1f), new Color(0.18f, 0.30f, 0.42f, 1f), 8, 1);
        inBg.type = Image.Type.Sliced;
        inBg.color = Color.white;

        GameObject viewport = new GameObject("TextArea", typeof(RectTransform));
        viewport.transform.SetParent(inpObj.transform, false);
        RectTransform vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(16f, 4f);
        vpRt.offsetMax = new Vector2(-16f, -4f);
        viewport.AddComponent<RectMask2D>();

        GameObject phObj = new GameObject("Placeholder", typeof(RectTransform));
        phObj.transform.SetParent(viewport.transform, false);
        RectTransform phRt = phObj.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = Vector2.zero;
        phRt.offsetMax = Vector2.zero;

        TextMeshProUGUI phText = phObj.AddComponent<TextMeshProUGUI>();
        phText.font = MainMenuManager.GetTacticalFont();
        phText.text = placeholder;
        phText.fontSize = 18f;
        phText.fontStyle = FontStyles.Italic;
        phText.alignment = TextAlignmentOptions.MidlineLeft;
        phText.color = new Color(0.45f, 0.56f, 0.68f, 0.65f);
        phText.raycastTarget = false;

        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(viewport.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI inputText = txtObj.AddComponent<TextMeshProUGUI>();
        inputText.font = MainMenuManager.GetTacticalFont();
        inputText.fontSize = 19f;
        inputText.fontStyle = FontStyles.Bold;
        inputText.characterSpacing = 1.1f;
        inputText.alignment = TextAlignmentOptions.MidlineLeft;
        inputText.color = Color.white;
        inputText.raycastTarget = false;

        // Time.timeScale == 0 iken bile net şekilde yanıp sönen özel camgöbeği imleç çubuğu
        GameObject caretObj = new GameObject("CustomCaretBar", typeof(RectTransform));
        caretObj.transform.SetParent(viewport.transform, false);
        customCaretRt = caretObj.GetComponent<RectTransform>();
        customCaretRt.anchorMin = new Vector2(0f, 0.5f);
        customCaretRt.anchorMax = new Vector2(0f, 0.5f);
        customCaretRt.pivot = new Vector2(0f, 0.5f);
        customCaretRt.sizeDelta = new Vector2(2.8f, 24f);
        customCaretRt.anchoredPosition = new Vector2(2f, 0f);
        Image caretImg = caretObj.AddComponent<Image>();
        caretImg.color = new Color(0f, 0.95f, 1f, 1f);
        caretImg.raycastTarget = false;
        caretObj.SetActive(false);

        TMP_InputField field = inpObj.AddComponent<TMP_InputField>();
        field.textViewport = vpRt;
        field.textComponent = inputText;
        field.placeholder = phText;
        field.characterLimit = charLimit;
        field.contentType = contentType;
        field.customCaretColor = true;
        field.caretWidth = 2;
        field.caretBlinkRate = 0.85f;
        field.caretColor = new Color(0f, 0.90f, 1f, 0f); // Çift imleç olmasın diye yerleşik caret şeffaf; özel unscaled imleç aktif
        field.selectionColor = new Color(0f, 0.65f, 0.85f, 0.35f);

        // Runtime'da eklenen TMP_InputField'ın OnEnable kurulumunu tazele
        field.enabled = false;
        field.enabled = true;
        return field;
    }

    private void UpdateInputFieldFocusAndCaret(TMP_InputField field, TextMeshProUGUI labelTMP, RectTransform caretRt)
    {
        if (field == null) return;
        bool focused = field.isFocused;

        // 1. Kutu çerçevesi ve arka plan odağı (Odaklı kutu parlak camgöbeği çerçeve alır)
        Image bg = field.GetComponent<Image>();
        if (bg != null)
        {
            Sprite targetSprite = focused
                ? MainMenuManager.GetRoundedBoxSprite("auth_input_focused", new Color(0.05f, 0.12f, 0.19f, 1f), new Color(0f, 0.92f, 1f, 1f), 8, 2)
                : MainMenuManager.GetRoundedBoxSprite("auth_input_bg", new Color(0.03f, 0.06f, 0.10f, 1f), new Color(0.18f, 0.30f, 0.42f, 1f), 8, 1);
            if (bg.sprite != targetSprite)
            {
                bg.sprite = targetSprite;
                bg.type = Image.Type.Sliced;
            }
        }

        // 2. Etiket rengi vurgusu
        if (labelTMP != null)
        {
            labelTMP.color = focused ? new Color(0f, 0.95f, 1f, 1f) : new Color(0.52f, 0.64f, 0.74f, 1f);
        }

        // 3. Yanıp sönen camgöbeği imleç çubuğu (Time.unscaledTime ile menü duraklatılmışken bile çalışır)
        if (caretRt != null)
        {
            if (!focused)
            {
                if (caretRt.gameObject.activeSelf) caretRt.gameObject.SetActive(false);
                return;
            }

            bool blinkOn = (Mathf.FloorToInt(Time.unscaledTime * 2.1f) % 2) == 0;
            if (caretRt.gameObject.activeSelf != blinkOn)
                caretRt.gameObject.SetActive(blinkOn);

            if (blinkOn && field.textComponent != null)
            {
                float caretX = 2f;
                var tc = field.textComponent;
                string rendered = tc.text;
                if (!string.IsNullOrEmpty(rendered))
                {
                    tc.ForceMeshUpdate();
                    var ti = tc.textInfo;
                    int pos = Mathf.Clamp(field.caretPosition, 0, ti.characterCount);
                    if (ti.characterCount > 0)
                    {
                        float halfW = tc.rectTransform.rect.width * 0.5f;
                        if (pos == 0)
                        {
                            caretX = ti.characterInfo[0].origin + halfW;
                        }
                        else if (pos >= ti.characterCount)
                        {
                            caretX = ti.characterInfo[ti.characterCount - 1].xAdvance + halfW;
                        }
                        else
                        {
                            caretX = ti.characterInfo[pos].origin + halfW;
                        }
                    }
                }
                float maxW = field.textViewport != null ? field.textViewport.rect.width - 4f : 400f;
                caretRt.anchoredPosition = new Vector2(Mathf.Clamp(caretX, 2f, maxW), 0f);
            }
        }
    }

    private Button CreateActionButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color fillColor, Color borderColor, Color textColor, float fontSize, Action onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        string key = $"auth_btn_{name}_{fillColor.r:F2}_{fillColor.g:F2}_{fillColor.b:F2}";
        img.sprite = MainMenuManager.GetRoundedBoxSprite(key, fillColor, borderColor, 10, 1);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayButtonClick();
            onClick?.Invoke();
        });

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
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;

        return btn;
    }

    private void TogglePasswordVisibility()
    {
        _showPassword = !_showPassword;
        ApplyPasswordMasking();
    }

    private void ApplyPasswordMasking()
    {
        var ct = _showPassword ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
        if (_passwordInput != null)
        {
            _passwordInput.contentType = ct;
            _passwordInput.ForceLabelUpdate();
        }
        if (_confirmPasswordInput != null)
        {
            _confirmPasswordInput.contentType = ct;
            _confirmPasswordInput.ForceLabelUpdate();
        }
        if (_togglePwdVisTMP != null)
            _togglePwdVisTMP.text = _showPassword ? "GİZLE" : "GÖSTER";
    }

    private void OnLogoutClicked()
    {
        PilotAuthManager.EnsureInstance().Logout();
        OpenLoginModal(_onSaveCallback, isMandatoryFirstTime: true);
    }

    private void OnSubmitClicked()
    {
        if (_isBusy) return;

        string username = _usernameInput != null ? _usernameInput.text.Trim() : "";
        string password = _passwordInput != null ? _passwordInput.text : "";

        if (username.Length < 3)
        {
            SetValidationError("Kullanıcı adı en az 3 karakter olmalıdır!");
            return;
        }
        if (password.Length < 4)
        {
            SetValidationError("Şifre en az 4 karakter olmalıdır!");
            return;
        }

        if (_isRegisterMode)
        {
            string confirm = _confirmPasswordInput != null ? _confirmPasswordInput.text : "";
            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                SetValidationError("Girilen şifreler birbiriyle eşleşmiyor!");
                return;
            }
        }

        SetBusyState(true);
        if (_validationTMP != null)
            _validationTMP.text = "<color=#00E5FF>Veritabanı ile doğrulanıyor...</color>";

        var auth = PilotAuthManager.EnsureInstance();
        Action<bool, string> callback = (success, message) =>
        {
            SetBusyState(false);
            if (!success)
            {
                SetValidationError(message);
                return;
            }

            if (_validationTMP != null)
                _validationTMP.text = $"<color=#2ECC71><b>{message}</b></color>";

            _isMandatory = false;
            CloseModal();
            _onSaveCallback?.Invoke();
            _onSaveCallback = null;
        };

        if (_isRegisterMode)
            auth.Register(username, password, callback);
        else
            auth.Login(username, password, callback);
    }

    private void SetBusyState(bool busy)
    {
        _isBusy = busy;
        if (_submitButton != null) _submitButton.interactable = !busy;
        if (_loginTabBtn != null) _loginTabBtn.interactable = !busy;
        if (_registerTabBtn != null) _registerTabBtn.interactable = !busy;
        if (_submitBtnTMP != null)
        {
            if (busy)
                _submitBtnTMP.text = "<b>DOĞRULANIYOR...</b>";
            else
                _submitBtnTMP.text = _isRegisterMode ? "<b>HESAP OLUŞTUR VE GİRİŞ YAP</b>" : "<b>GİRİŞ YAP</b>";
        }
    }

    private void SetValidationError(string msg)
    {
        if (_validationTMP != null)
            _validationTMP.text = $"<color=#FF5252>[!] {msg}</color>";
    }
}
