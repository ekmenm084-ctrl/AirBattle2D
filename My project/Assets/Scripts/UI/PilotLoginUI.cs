using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Bağımsız Pilot Profili ve Çağrı Kodu Giriş / Geçiş Ekranı (Multi-Profile Pilot Login):
/// - Her pilot kendi bağımsız Seviye (Level), Altın, Uçak Envanteri, İHA/Dronları ve Rekorlarına sahiptir.
/// - Aynı telefonda kayıtlı pilotlar arasında tek tıkla anında profil geçişi yapılabilir.
/// - Farklı bir çağrı kodu girildiğinde o pilota ait bağımsız profile geçiş yapılır veya yeni profil oluşturulur.
/// - Belirlenen isim Küresel Liderlik Tablosunda (Leaderboard), Ana Menü profil rozetinde ve telsiz anonslarında görünür.
/// </summary>
public class PilotLoginUI : MonoBehaviour
{
    public static PilotLoginUI Instance { get; private set; }

    private GameObject _modalRoot;
    private TMP_InputField _inputField;
    private TextMeshProUGUI _validationTMP;
    private TextMeshProUGUI _charCountTMP;
    private TextMeshProUGUI _activePilotBannerTMP;
    private Transform _knownProfilesContainer;
    private List<GameObject> _spawnedProfileBtns = new List<GameObject>();

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

    public static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }
    }

    public void CheckFirstTimeLogin()
    {
        if (!LeaderboardManager.HasSetCustomCallsign)
        {
            OpenLoginModal(null, isMandatoryFirstTime: true);
        }
    }

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
            _cancelButton.gameObject.SetActive(true);
        }

        string currentName = ProfileManager.ActiveProfile;
        if (_inputField != null)
        {
            _inputField.text = currentName;
            _inputField.ActivateInputField();
            _inputField.Select();
        }

        if (_validationTMP != null)
        {
            _validationTMP.text = _isFirstTimeLogin
                ? "<color=#8AB4F8>Lütfen bu cihazda kullanılacak pilot çağrı kodunuzu belirleyin.</color>"
                : "<color=#8AB4F8>Farklı bir isim girdiğinizde o pilota ait bağımsız profile anında geçiş yapılır.</color>";
        }

        UpdateCharCount(currentName);
        RefreshModalProfileView();
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

        // 1. Karartma Arka Planı
        _modalRoot = new GameObject("PilotLoginModalRoot", typeof(RectTransform));
        _modalRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = _modalRoot.GetComponent<RectTransform>();
        MainMenuManager.MakeFullScreenRect(rootRt);

        Image bg = _modalRoot.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.04f, 0.08f, 0.94f);

        // 2. Ana Askeri Kart (Modal Box)
        GameObject boxObj = new GameObject("PilotCardBox", typeof(RectTransform));
        boxObj.transform.SetParent(_modalRoot.transform, false);
        RectTransform bRt = boxObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 0.5f);
        bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.anchoredPosition = Vector2.zero;
        bRt.sizeDelta = new Vector2(700f, 540f);

        Image bBg = boxObj.AddComponent<Image>();
        bBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.03f, 0.07f, 0.14f, 0.98f), false);
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
        hsRt.sizeDelta = new Vector2(660f, 50f);

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
        titleTMP.text = "DEFCON // PİLOT HESAP & PROFİL SEÇİMİ";
        titleTMP.fontSize = 20f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 1.8f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0f, 0.95f, 1f);

        // Açıklama
        GameObject subObj = new GameObject("SubTitleText", typeof(RectTransform));
        subObj.transform.SetParent(boxObj.transform, false);
        RectTransform sRt = subObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0f, -66f);
        sRt.sizeDelta = new Vector2(640f, 26f);

        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.font = MainMenuManager.GetTacticalFont();
        subTMP.text = "Her pilot kendi bağımsız seviyesine, altınlarına, hangarına ve rekorlarına sahiptir.";
        subTMP.fontSize = 18f;
        subTMP.characterSpacing = 1.1f;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = new Color(0.80f, 0.88f, 0.95f);

        // 1. Mevcut Aktif Pilot Kartı
        GameObject activeCardObj = new GameObject("ActivePilotCard", typeof(RectTransform));
        activeCardObj.transform.SetParent(boxObj.transform, false);
        RectTransform acRt = activeCardObj.GetComponent<RectTransform>();
        acRt.anchorMin = new Vector2(0.5f, 1f);
        acRt.anchorMax = new Vector2(0.5f, 1f);
        acRt.pivot = new Vector2(0.5f, 1f);
        acRt.anchoredPosition = new Vector2(0f, -96f);
        acRt.sizeDelta = new Vector2(640f, 42f);

        Image acBg = activeCardObj.AddComponent<Image>();
        acBg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0.2f, 0.95f, 0.45f, 0.85f), new Color(0.02f, 0.12f, 0.08f, 0.95f), false);
        acBg.type = Image.Type.Sliced;
        acBg.color = Color.white;

        GameObject acTxtObj = new GameObject("Text", typeof(RectTransform));
        acTxtObj.transform.SetParent(activeCardObj.transform, false);
        RectTransform actRt = acTxtObj.GetComponent<RectTransform>();
        actRt.anchorMin = Vector2.zero;
        actRt.anchorMax = Vector2.one;
        actRt.offsetMin = new Vector2(15f, 0f);
        actRt.offsetMax = new Vector2(-15f, 0f);

        _activePilotBannerTMP = acTxtObj.AddComponent<TextMeshProUGUI>();
        _activePilotBannerTMP.font = MainMenuManager.GetTacticalFont();
        _activePilotBannerTMP.fontSize = 20f;
        _activePilotBannerTMP.fontStyle = FontStyles.Bold;
        _activePilotBannerTMP.characterSpacing = 1.2f;
        _activePilotBannerTMP.alignment = TextAlignmentOptions.Center;
        _activePilotBannerTMP.color = Color.white;

        // 2. Kayıtlı Pilotlar Bölümü Etiketi
        GameObject kpLblObj = new GameObject("KnownProfilesLabel", typeof(RectTransform));
        kpLblObj.transform.SetParent(boxObj.transform, false);
        RectTransform kplRt = kpLblObj.GetComponent<RectTransform>();
        kplRt.anchorMin = new Vector2(0.5f, 1f);
        kplRt.anchorMax = new Vector2(0.5f, 1f);
        kplRt.pivot = new Vector2(0.5f, 1f);
        kplRt.anchoredPosition = new Vector2(0f, -145f);
        kplRt.sizeDelta = new Vector2(640f, 24f);

        TextMeshProUGUI kplTMP = kpLblObj.AddComponent<TextMeshProUGUI>();
        kplTMP.font = MainMenuManager.GetTacticalFont();
        kplTMP.text = "CİHAZDAKİ KAYITLI PİLOTLAR (HIZLI GEÇİŞ):";
        kplTMP.fontSize = 18f;
        kplTMP.fontStyle = FontStyles.Bold;
        kplTMP.characterSpacing = 1.2f;
        kplTMP.alignment = TextAlignmentOptions.Left;
        kplTMP.color = new Color(0.3f, 0.85f, 1f);

        // Kayıtlı Pilotlar Buton Konteyneri
        GameObject kpContObj = new GameObject("KnownProfilesContainer", typeof(RectTransform));
        kpContObj.transform.SetParent(boxObj.transform, false);
        RectTransform kpcRt = kpContObj.GetComponent<RectTransform>();
        kpcRt.anchorMin = new Vector2(0.5f, 1f);
        kpcRt.anchorMax = new Vector2(0.5f, 1f);
        kpcRt.pivot = new Vector2(0.5f, 1f);
        kpcRt.anchoredPosition = new Vector2(0f, -172f);
        kpcRt.sizeDelta = new Vector2(640f, 50f);

        _knownProfilesContainer = kpContObj.transform;

        // 3. Yeni / Farklı Çağrı Kodu Giriş Alanı Etiketi
        GameObject inpLblObj = new GameObject("InputLabel", typeof(RectTransform));
        inpLblObj.transform.SetParent(boxObj.transform, false);
        RectTransform inplRt = inpLblObj.GetComponent<RectTransform>();
        inplRt.anchorMin = new Vector2(0.5f, 1f);
        inplRt.anchorMax = new Vector2(0.5f, 1f);
        inplRt.pivot = new Vector2(0.5f, 1f);
        inplRt.anchoredPosition = new Vector2(0f, -226f);
        inplRt.sizeDelta = new Vector2(640f, 24f);

        TextMeshProUGUI inplTMP = inpLblObj.AddComponent<TextMeshProUGUI>();
        inplTMP.font = MainMenuManager.GetTacticalFont();
        inplTMP.text = "VEYA FARKLI / YENİ PİLOT ÇAĞRI KODU GİRİN:";
        inplTMP.fontSize = 18f;
        inplTMP.fontStyle = FontStyles.Bold;
        inplTMP.characterSpacing = 1.2f;
        inplTMP.alignment = TextAlignmentOptions.Left;
        inplTMP.color = new Color(0.3f, 0.85f, 1f);

        // Input Field
        GameObject inpObj = new GameObject("NameInputField", typeof(RectTransform));
        inpObj.transform.SetParent(boxObj.transform, false);
        RectTransform inRt = inpObj.GetComponent<RectTransform>();
        inRt.anchorMin = new Vector2(0.5f, 1f);
        inRt.anchorMax = new Vector2(0.5f, 1f);
        inRt.pivot = new Vector2(0.5f, 1f);
        inRt.anchoredPosition = new Vector2(0f, -250f);
        inRt.sizeDelta = new Vector2(500f, 48f);

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
        phText.text = "Pilot Çağrı Kodu (Örn: Mustafa, Kaan)...";
        phText.fontSize = 18f;
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
        cntRt.anchorMin = new Vector2(0.5f, 1f);
        cntRt.anchorMax = new Vector2(0.5f, 1f);
        cntRt.pivot = new Vector2(1f, 1f);
        cntRt.anchoredPosition = new Vector2(320f, -226f);
        cntRt.sizeDelta = new Vector2(120f, 20f);

        _charCountTMP = countObj.AddComponent<TextMeshProUGUI>();
        _charCountTMP.font = MainMenuManager.GetTacticalFont();
        _charCountTMP.fontSize = 16f;
        _charCountTMP.alignment = TextAlignmentOptions.Right;
        _charCountTMP.color = new Color(0.6f, 0.75f, 0.9f);

        // Doğrulama / Uyarı Mesajı
        GameObject valObj = new GameObject("ValidationMessage", typeof(RectTransform));
        valObj.transform.SetParent(boxObj.transform, false);
        RectTransform vRt = valObj.GetComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0.5f, 1f);
        vRt.anchorMax = new Vector2(0.5f, 1f);
        vRt.pivot = new Vector2(0.5f, 1f);
        vRt.anchoredPosition = new Vector2(0f, -305f);
        vRt.sizeDelta = new Vector2(580f, 26f);

        _validationTMP = valObj.AddComponent<TextMeshProUGUI>();
        _validationTMP.font = MainMenuManager.GetTacticalFont();
        _validationTMP.fontSize = 17f;
        _validationTMP.characterSpacing = 1.1f;
        _validationTMP.alignment = TextAlignmentOptions.Center;
        _validationTMP.color = new Color(0.9f, 0.9f, 0.9f);

        // Rastgele İsim Üret Butonu
        GameObject rndBtnObj = CreateButton(boxObj.transform, "RandomNameBtn", "🎲 RASTGELE ÇAĞRI KODU SEÇ",
            new Vector2(0f, -345f), new Vector2(300f, 38f),
            "hud_btn_cyan", new Color(0f, 0.85f, 1f, 1f), new Color(0.04f, 0.12f, 0.20f, 1f), GenerateRandomCallsign);
        _randomNameButton = rndBtnObj.GetComponent<Button>();

        // 4. Alt Aksiyon Butonları
        // Onayla ve Savaşa Katıl Butonu
        GameObject saveBtnObj = CreateButton(boxObj.transform, "SaveCallsignBtn", "ONAYLA & PROFİLE GEÇ",
            new Vector2(0f, -408f), new Vector2(340f, 52f),
            "hud_btn_emerald", new Color(0.2f, 0.95f, 0.4f, 1f), new Color(0.04f, 0.16f, 0.08f, 1f), OnSaveClicked);
        _saveButton = saveBtnObj.GetComponent<Button>();

        // İptal / Kapat Butonu
        GameObject cancelBtnObj = CreateButton(boxObj.transform, "CancelBtn", "[<<] KAPAT",
            new Vector2(0f, -472f), new Vector2(170f, 38f),
            "hud_btn_red", new Color(1f, 0.35f, 0.35f, 1f), new Color(0.24f, 0.05f, 0.05f, 1f), CloseModal);
        _cancelButton = cancelBtnObj.GetComponent<Button>();

        _modalRoot.SetActive(false);
    }

    private void RefreshModalProfileView()
    {
        string currentActive = ProfileManager.ActiveProfile;
        int lvl = ProfileManager.GetInt("AirBattle_Pilot_Level", 1);
        int gold = ProfileManager.GetInt("AirBattle_Gold", 250);

        if (_activePilotBannerTMP != null)
        {
            _activePilotBannerTMP.text = $"AKTİF PİLOT: <color=#00E5FF><b>[{currentActive}]</b></color> • Seviye <b>{lvl}</b> • Altın: <b>{gold:N0} G</b>";
        }

        // Kayıtlı pilot butonlarını temizle ve yeniden oluştur
        foreach (var b in _spawnedProfileBtns)
        {
            if (b != null) Destroy(b);
        }
        _spawnedProfileBtns.Clear();

        if (_knownProfilesContainer == null) return;

        var knownProfiles = ProfileManager.GetKnownProfiles();
        int count = Mathf.Min(knownProfiles.Count, 4); // En fazla ilk 4 profili yan yana göster
        float btnWidth = 145f;
        float spacing = 12f;
        float totalW = (count * btnWidth) + ((count - 1) * spacing);
        float startX = -totalW / 2f + (btnWidth / 2f);

        for (int i = 0; i < count; i++)
        {
            string pName = knownProfiles[i];
            bool isActive = pName.Equals(currentActive, StringComparison.OrdinalIgnoreCase);
            int pLvl = PlayerPrefs.GetInt(ProfileManager.GetPrefKeyFor(pName, "AirBattle_Pilot_Level"), 1);

            float posX = startX + i * (btnWidth + spacing);
            string label = isActive ? $"<b>[{pName}]</b>\n<size=75%>AKTİF</size>" : $"{pName}\n<size=75%>Sv.{pLvl}</size>";
            string spriteKey = isActive ? "hud_btn_cyan" : "hud_btn_blue";
            Color border = isActive ? new Color(0f, 1f, 0.85f, 1f) : new Color(0.25f, 0.6f, 0.9f, 0.9f);
            Color fill = isActive ? new Color(0.04f, 0.16f, 0.22f, 1f) : new Color(0.05f, 0.10f, 0.18f, 0.95f);

            GameObject pBtnObj = CreateButton(_knownProfilesContainer, $"ProfileBtn_{pName}", label,
                new Vector2(posX, 0f), new Vector2(btnWidth, 42f),
                spriteKey, border, fill, () =>
                {
                    ProfileManager.SwitchProfile(pName);
                    if (_inputField != null) _inputField.text = pName;
                    RefreshModalProfileView();
                    if (_validationTMP != null)
                    {
                        _validationTMP.text = $"<color=#00FFAA>[✓] <b>{pName}</b> profiline başarıyla geçildi!</color>";
                    }
                });

            // Anchor'ı üst ortaya sabitle
            RectTransform pRt = pBtnObj.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(0.5f, 0.5f);

            _spawnedProfileBtns.Add(pBtnObj);
        }
    }

    private GameObject CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, string spriteKey, Color borderColor, Color fillColor, Action onClick)
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
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.2f;
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
                _validationTMP.text = "<color=#FF5252>[!] Çağrı kodu en az 3 karakter uzunluğunda olmalıdır!</color>";
            }
            return;
        }

        // Profil Değişimi & Bağımsız Hesap Yükleme
        ProfileManager.SwitchProfile(input);

        CloseModal();

        _onSaveCallback?.Invoke();
        _onSaveCallback = null;
    }
}
