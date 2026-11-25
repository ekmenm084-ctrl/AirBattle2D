using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Savaş esnasında çalışan Durdurma Menüsü (Pause Menu) ve Ses Ayar Yöneticisi:
/// - Sağ üst köşedeki [|| DURDUR] butonu veya ESC / P tuşları ile oyunu duraklatır (Time.timeScale = 0).
/// - Açılan panelde:
///   1. DEVAM ET: Savaşa kaldığı yerden devam eder.
///   2. ANA SAYFAYA DÖN: Oyunu sıfırlayıp ana menüye döner.
///   3. SES DÜZEY AYARLARI: Savaş müziği ve mermi seslerini slider ve [+] [-] butonlarıyla ayarlar.
/// </summary>
public class AudioSettingsUI : MonoBehaviour
{
    public static AudioSettingsUI Instance { get; private set; }

    [Header("UI Elemanları")]
    public GameObject soundButtonObj;       // [|| DURDUR] Butonu
    public GameObject settingsPanelObj;     // Durdurma ve Ses Paneli

    private GameObject _landscapePauseRoot;
    private GameObject _portraitPauseRoot;

    // Yatay (Landscape) Kontroller
    private Slider _musicSlider;
    private Slider _sfxSlider;
    private TextMeshProUGUI _musicValTMP;
    private TextMeshProUGUI _sfxValTMP;
    private TextMeshProUGUI _muteBtnTMP;
    private TextMeshProUGUI _controlBtnTMP;
    private Image _controlBtnBg;
    private GameObject _pauseJoystickPreviewObj;
    private GameObject _pauseArrowsPreviewObj;

    // Dikey 1v1 (Portrait) Kontroller
    private Slider _pMusicSlider;
    private Slider _pSfxSlider;
    private TextMeshProUGUI _pMusicValTMP;
    private TextMeshProUGUI _pSfxValTMP;
    private TextMeshProUGUI _pMuteBtnTMP;
    private TextMeshProUGUI _pControlBtnTMP;

    private bool _isPaused = false;
    public bool IsPaused => _isPaused;
    public static bool IsGamePaused => (Instance != null && Instance._isPaused);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Kendi RectTransform'unu Canvas'a tam oturt
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            _isPaused = false;
            Instance = null;
        }
    }

    private void Start()
    {
        EnsureUI();
        UpdateUIValues();
    }

    private void Update()
    {
        // Ana menü veya alt menüler açıkken durdurma butonunu gizle, sadece savaşta sağ üstte göster
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy) ||
                          (HangarUI.Instance != null && HangarUI.Instance.gameObject.activeInHierarchy) ||
                          (CratesUI.Instance != null && CratesUI.Instance.gameObject.activeInHierarchy) ||
                          (InventoryUI.Instance != null && InventoryUI.Instance.gameObject.activeInHierarchy) ||
                          (DailyOpsUI.Instance != null && DailyOpsUI.Instance.gameObject.activeInHierarchy);

        if (soundButtonObj != null)
        {
            bool isGameOverOpen = (GameOverUI.Instance != null && GameOverUI.Instance.IsShowing);
            bool shouldShow = !isMenuOpen && !_isPaused && !isGameOverOpen;
            if (soundButtonObj.activeSelf != shouldShow)
            {
                soundButtonObj.SetActive(shouldShow);
                if (shouldShow)
                {
                    RefreshPauseButtonPosition();
                    soundButtonObj.transform.SetAsLastSibling();
                }
            }
        }

        // Klavye kısayolu ile duraklatma (ESC veya P tuşu)
        bool gameEnded = (GameManager.Instance != null && GameManager.Instance.IsGameEnded);
        bool upgrading = (UpgradeSelectionUI.Instance != null && UpgradeSelectionUI.Instance.IsSelecting);
        bool modalPausedElsewhere = (!_isPaused && Time.timeScale <= 0.01f);

        if (!isMenuOpen && !gameEnded && !upgrading && !modalPausedElsewhere)
        {
            try
            {
                bool isTwoPlayer = (GameSettings.SelectedMode == GameSettings.GameMode.Online) ||
                                   (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);
                // 2 Oyunculu modda 'P' tuşu P2'nin Füze tuşu olduğu için yalnızca ESC duraklatır; tek kişilik modda ESC veya P çalışır
                if (Input.GetKeyDown(KeyCode.Escape) || (!isTwoPlayer && Input.GetKeyDown(KeyCode.P)))
                {
                    TogglePauseMenu();
                }
            }
            catch { }
        }
    }

    public static AudioSettingsUI EnsureInstance()
    {
        if (Instance != null) return Instance;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("AudioSettingsUI");
        GameObject go = existing != null ? existing.gameObject : null;

        if (go == null)
        {
            go = new GameObject("AudioSettingsUI", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        AudioSettingsUI ui = go.GetComponent<AudioSettingsUI>();
        if (ui == null)
        {
            ui = go.AddComponent<AudioSettingsUI>();
        }

        Instance = ui;
        ui.EnsureUI();
        return ui;
    }

    public void EnsureUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. Sağ Üst Köşe Orijinal Şeffaf Pause İkon Butonu
        if (soundButtonObj == null)
        {
            Transform existingBtn = transform.Find("SoundToggleButton");
            if (existingBtn != null)
            {
                soundButtonObj = existingBtn.gameObject;
            }
            else
            {
                soundButtonObj = new GameObject("SoundToggleButton", typeof(RectTransform));
                soundButtonObj.transform.SetParent(transform, false);
            }
        }

        soundButtonObj.SetActive(true);
        RefreshPauseButtonPosition();

        Image btnBg = soundButtonObj.GetComponent<Image>();
        if (btnBg == null) btnBg = soundButtonObj.AddComponent<Image>();

        Sprite pauseSprite = Resources.Load<Sprite>("pause_icon");
        if (pauseSprite != null)
        {
            btnBg.sprite = pauseSprite;
            btnBg.color = Color.white;
            btnBg.preserveAspect = true;
        }

        // Varsa kutu outline'ını kaldır (ikonun kendi şık neon ışıması vardır)
        Outline outline = soundButtonObj.GetComponent<Outline>();
        if (outline != null)
        {
            Destroy(outline);
        }

        // Varsa yazı objesini kaldır (sadece profesyonel grafik ikon gözüksün)
        Transform iconChild = soundButtonObj.transform.Find("IconText");
        if (iconChild != null)
        {
            Destroy(iconChild.gameObject);
        }

        Button btn = soundButtonObj.GetComponent<Button>();
        if (btn == null) btn = soundButtonObj.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(TogglePauseMenu);

        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.80f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.50f, 0.85f, 1f, 0.75f);
        colors.selectedColor = Color.white;
        btn.colors = colors;

        soundButtonObj.transform.SetAsLastSibling();

        // 2. Durdurma & Ses Paneli
        if (settingsPanelObj == null)
        {
            Transform existingPanel = transform.Find("AudioSettingsPanel");
            if (existingPanel != null)
            {
                settingsPanelObj = existingPanel.gameObject;
            }
            else
            {
                BuildPauseAndSettingsPanel();
            }
        }
    }

    public void RefreshPauseButtonPosition()
    {
        if (soundButtonObj == null) return;
        RectTransform rt = soundButtonObj.GetComponent<RectTransform>();
        if (rt == null) return;

        if (MainMenuManager.IsPortraitDuelModeActive())
        {
            // Dikey 2P VS modunda Pause ikonu sağ orta ekvatorda durur (P2'nin üst köşedeki eliyle çakışmaz)
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-16f, 0f);
            rt.sizeDelta = new Vector2(52f, 52f);
        }
        else
        {
            // Normal Yatay Mod: Sağ üst köşe
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-42f, -12f);
            rt.sizeDelta = new Vector2(54f, 54f);
        }
    }

    private static Sprite _cachedPauseBgSprite;

    private static Sprite LoadPauseBgSprite()
    {
        if (_cachedPauseBgSprite != null) return _cachedPauseBgSprite;

        string path = System.IO.Path.Combine(Application.dataPath, "Sprites/pause_background.jpg");
        if (System.IO.File.Exists(path))
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (tex.LoadImage(bytes))
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                _cachedPauseBgSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return _cachedPauseBgSprite;
            }
        }

        Sprite res = Resources.Load<Sprite>("pause_background");
        if (res != null)
        {
            _cachedPauseBgSprite = res;
            return res;
        }

        Texture2D resTex = Resources.Load<Texture2D>("pause_background");
        if (resTex != null)
        {
            _cachedPauseBgSprite = Sprite.Create(resTex, new Rect(0, 0, resTex.width, resTex.height), new Vector2(0.5f, 0.5f), 100f);
            return _cachedPauseBgSprite;
        }
        return null;
    }

    private void BuildPauseAndSettingsPanel()
    {
        settingsPanelObj = new GameObject("AudioSettingsPanel", typeof(RectTransform));
        settingsPanelObj.transform.SetParent(transform, false);

        RectTransform panelRt = settingsPanelObj.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        // A) YATAY (LANDSCAPE 1920x1080) DURDURMA PANELİ KÖKÜ
        _landscapePauseRoot = new GameObject("LandscapePauseRoot", typeof(RectTransform));
        _landscapePauseRoot.transform.SetParent(settingsPanelObj.transform, false);
        MainMenuManager.MakeFullScreenRect(_landscapePauseRoot.GetComponent<RectTransform>());

        Image bg = _landscapePauseRoot.AddComponent<Image>();
        Sprite pauseBg = LoadPauseBgSprite();
        if (pauseBg != null)
        {
            bg.sprite = pauseBg;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.color = Color.white;
        }
        else
        {
            bg.color = new Color(0.03f, 0.06f, 0.12f, 0.96f);
        }

        // 1. Savaş Müziği Satırı: [-]  Slider  [+]
        _musicSlider = CreateVolumeRow(_landscapePauseRoot.transform, "SAVAS MUZIGI", 200.5f, 264.0f, isMusic: true, out _musicValTMP);

        // 2. Mermi / Efekt Sesi Satırı: [-]  Slider  [+]
        _sfxSlider = CreateVolumeRow(_landscapePauseRoot.transform, "MERMI VE PATLAMA", 76.5f, 140.0f, isMusic: false, out _sfxValTMP);

        // 3. Uçuş Kontrol Tipi Butonu (Tam ortalanmış: centerX = 0f, y = -28.0f)
        GameObject ctrlBtnObj = new GameObject("ControlSchemeButton", typeof(RectTransform));
        ctrlBtnObj.transform.SetParent(_landscapePauseRoot.transform, false);
        RectTransform ctrlRt = ctrlBtnObj.GetComponent<RectTransform>();
        ctrlRt.anchorMin = new Vector2(0.5f, 0.5f);
        ctrlRt.anchorMax = new Vector2(0.5f, 0.5f);
        ctrlRt.pivot = new Vector2(0.5f, 0.5f);
        ctrlRt.anchoredPosition = new Vector2(0f, -28.0f);
        ctrlRt.sizeDelta = new Vector2(825f, 70f);

        _controlBtnBg = ctrlBtnObj.AddComponent<Image>();
        _controlBtnBg.sprite = null;
        _controlBtnBg.color = new Color(1f, 1f, 1f, 0.01f);

        Button ctrlBtn = ctrlBtnObj.AddComponent<Button>();
        ctrlBtn.targetGraphic = _controlBtnBg;
        ctrlBtn.onClick.AddListener(() =>
        {
            GameSettings.ToggleControlType();
            UpdateControlVisuals();
            TouchArrowControls.Instance?.EnsureArrowUI();
            TouchArrowControls.Instance?.UpdateVisibility();
            AudioManager.Instance?.PlayButtonClick();
        });

        GameObject ctrlTxtObj = new GameObject("ControlText", typeof(RectTransform));
        ctrlTxtObj.transform.SetParent(ctrlBtnObj.transform, false);
        RectTransform ctrlTxtRt = ctrlTxtObj.GetComponent<RectTransform>();
        ctrlTxtRt.anchorMin = Vector2.zero;
        ctrlTxtRt.anchorMax = Vector2.one;
        ctrlTxtRt.offsetMin = Vector2.zero;
        ctrlTxtRt.offsetMax = Vector2.zero;

        _controlBtnTMP = ctrlTxtObj.AddComponent<TextMeshProUGUI>();
        _controlBtnTMP.font = MainMenuManager.GetTacticalFont();
        _controlBtnTMP.fontSize = 27f;
        _controlBtnTMP.fontStyle = FontStyles.Bold;
        _controlBtnTMP.characterSpacing = 1.4f;
        _controlBtnTMP.alignment = TextAlignmentOptions.Center;
        _controlBtnTMP.color = Color.white;
        _controlBtnTMP.extraPadding = true;
        _controlBtnTMP.raycastTarget = false;

        // 4. Mute Butonu (Tam ortalanmış: centerX = 0f, y = -142.0f)
        GameObject muteBtnObj = new GameObject("MuteButton", typeof(RectTransform));
        muteBtnObj.transform.SetParent(_landscapePauseRoot.transform, false);
        RectTransform muteRt = muteBtnObj.GetComponent<RectTransform>();
        muteRt.anchorMin = new Vector2(0.5f, 0.5f);
        muteRt.anchorMax = new Vector2(0.5f, 0.5f);
        muteRt.pivot = new Vector2(0.5f, 0.5f);
        muteRt.anchoredPosition = new Vector2(0f, -142.0f);
        muteRt.sizeDelta = new Vector2(671f, 72f);

        Image muteBg = muteBtnObj.AddComponent<Image>();
        muteBg.sprite = null;
        muteBg.color = new Color(1f, 1f, 1f, 0.01f);

        Button muteBtn = muteBtnObj.AddComponent<Button>();
        muteBtn.targetGraphic = muteBg;
        muteBtn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.ToggleMute();
            UpdateMuteVisuals();
        });

        GameObject muteTxtObj = new GameObject("MuteText", typeof(RectTransform));
        muteTxtObj.transform.SetParent(muteBtnObj.transform, false);
        RectTransform muteTxtRt = muteTxtObj.GetComponent<RectTransform>();
        muteTxtRt.anchorMin = Vector2.zero;
        muteTxtRt.anchorMax = Vector2.one;
        muteTxtRt.offsetMin = Vector2.zero;
        muteTxtRt.offsetMax = Vector2.zero;

        _muteBtnTMP = muteTxtObj.AddComponent<TextMeshProUGUI>();
        _muteBtnTMP.font = MainMenuManager.GetTacticalFont();
        _muteBtnTMP.fontSize = 26f;
        _muteBtnTMP.fontStyle = FontStyles.Bold;
        _muteBtnTMP.characterSpacing = 1.6f;
        _muteBtnTMP.alignment = TextAlignmentOptions.Center;
        _muteBtnTMP.color = new Color(0.56f, 0.65f, 0.74f, 1f);
        _muteBtnTMP.extraPadding = true;
        _muteBtnTMP.raycastTarget = false;

        // 5. DEVAM ET BUTONU (Tam ortalanmış: centerX = 0f, y = -252.0f)
        GameObject resumeBtnObj = new GameObject("ResumeButton", typeof(RectTransform));
        resumeBtnObj.transform.SetParent(_landscapePauseRoot.transform, false);
        Image resumeBg = resumeBtnObj.AddComponent<Image>();
        Button resumeBtn = resumeBtnObj.AddComponent<Button>();
        resumeBtn.targetGraphic = resumeBg;
        resumeBtn.onClick.AddListener(ResumeGame);
        FormatPausePixelExactButton(resumeBtnObj, new Vector2(0f, -252.0f), new Vector2(671f, 72f));

        // 6. ANA SAYFAYA DÖN BUTONU (Tam ortalanmış: centerX = 0f, y = -360.0f)
        GameObject homeBtnObj = new GameObject("HomeButton", typeof(RectTransform));
        homeBtnObj.transform.SetParent(_landscapePauseRoot.transform, false);
        Image homeBg = homeBtnObj.AddComponent<Image>();
        Button homeBtn = homeBtnObj.AddComponent<Button>();
        homeBtn.targetGraphic = homeBg;
        homeBtn.onClick.AddListener(ReturnToHome);
        FormatPausePixelExactButton(homeBtnObj, new Vector2(0f, -360.0f), new Vector2(671f, 72f));

        // 7. Sol Alt Uçuş Kontrolü Canlı Önizlemesi (Seçilen kontrol şemasıyla birebir senkronize)
        BuildControlSchemePreview(_landscapePauseRoot.transform);

        // B) DİKEY 1v1 DÜELLO (PORTRAIT 1080x1920) DURDURMA PANELİ KÖKÜ
        BuildPortraitDuelPausePanel();

        settingsPanelObj.SetActive(false);
    }

    private void BuildPortraitDuelPausePanel()
    {
        _portraitPauseRoot = new GameObject("PortraitDuelPauseRoot", typeof(RectTransform));
        _portraitPauseRoot.transform.SetParent(settingsPanelObj.transform, false);
        MainMenuManager.MakeFullScreenRect(_portraitPauseRoot.GetComponent<RectTransform>());

        Image dimmer = _portraitPauseRoot.AddComponent<Image>();
        dimmer.color = new Color(0.02f, 0.05f, 0.10f, 0.90f);
        dimmer.raycastTarget = true;

        GameObject cardObj = new GameObject("PortraitPauseCard", typeof(RectTransform));
        cardObj.transform.SetParent(_portraitPauseRoot.transform, false);
        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(880f, 996f);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.sprite = MainMenuManager.GetRoundedBoxSprite(
            "portrait_pause_card_bg",
            new Color(0.06f, 0.10f, 0.15f, 0.99f),
            new Color(0.28f, 0.50f, 0.68f, 0.94f),
            18, 2);
        cardBg.type = Image.Type.Sliced;
        cardBg.color = Color.white;
        cardBg.raycastTarget = true;

        // Üst Başlık
        CreatePortraitLabel(cardObj.transform, "HeaderTitle", "<b>OYUN DURAKLATILDI</b>", new Vector2(0f, -32f), new Vector2(780f, 46f), 34f, Color.white, TextAlignmentOptions.Center, 2.2f);
        CreatePortraitLabel(cardObj.transform, "HeaderSub", "2 OYUNCULU DÜELLO  •  SİSTEM AYARLARI", new Vector2(0f, -80f), new Vector2(780f, 26f), 16f, new Color(0.28f, 0.85f, 1f, 0.95f), TextAlignmentOptions.Center, 1.8f);

        // 1. Savaş Müziği Kutusu
        _pMusicSlider = CreatePortraitVolumeBox(cardObj.transform, "PortraitMusicBox", "SAVAŞ MÜZİĞİ", -128f, isMusic: true, out _pMusicValTMP);

        // 2. Mermi ve Patlama Kutusu
        _pSfxSlider = CreatePortraitVolumeBox(cardObj.transform, "PortraitSfxBox", "MERMİ VE PATLAMA SESİ", -286f, isMusic: false, out _pSfxValTMP);

        // 3. Uçuş Kontrol Şeması Butonu
        Button pCtrlBtn = CreatePortraitButton(
            cardObj.transform, "PortraitControlBtn", "UÇUŞ KONTROLÜ: SANAL JOYSTICK",
            new Vector2(0f, -450f), new Vector2(788f, 80f),
            new Color(0.09f, 0.16f, 0.24f, 1f), new Color(0.24f, 0.52f, 0.72f, 0.95f), Color.white, 22f,
            () =>
            {
                GameSettings.ToggleControlType();
                UpdateControlVisuals();
                TouchArrowControls.Instance?.EnsureArrowUI();
                TouchArrowControls.Instance?.UpdateVisibility();
                AudioManager.Instance?.PlayButtonClick();
            });
        _pControlBtnTMP = pCtrlBtn.GetComponentInChildren<TextMeshProUGUI>();

        // 4. Sesleri Kapat / Aç Butonu
        Button pMuteBtn = CreatePortraitButton(
            cardObj.transform, "PortraitMuteBtn", "TÜM SESLERİ KAPAT",
            new Vector2(0f, -548f), new Vector2(788f, 74f),
            new Color(0.08f, 0.13f, 0.20f, 1f), new Color(0.22f, 0.34f, 0.46f, 0.95f), new Color(0.75f, 0.84f, 0.92f), 21f,
            () =>
            {
                AudioManager.Instance?.ToggleMute();
                UpdateMuteVisuals();
                AudioManager.Instance?.PlayButtonClick();
            });
        _pMuteBtnTMP = pMuteBtn.GetComponentInChildren<TextMeshProUGUI>();

        // 5. DEVAM ET (Yeşil / Ana Aksiyon)
        CreatePortraitButton(
            cardObj.transform, "PortraitResumeBtn", "<b>DEVAM ET</b>",
            new Vector2(0f, -650f), new Vector2(788f, 92f),
            new Color(0.11f, 0.68f, 0.44f, 1f), new Color(0.28f, 0.94f, 0.64f, 1f), Color.white, 28f,
            () =>
            {
                AudioManager.Instance?.PlayButtonClick();
                ResumeGame();
            });

        // 6. YENİDEN BAŞLAT
        CreatePortraitButton(
            cardObj.transform, "PortraitRestartBtn", "MAÇI YENİDEN BAŞLAT",
            new Vector2(0f, -762f), new Vector2(788f, 80f),
            new Color(0.11f, 0.20f, 0.30f, 1f), new Color(0.32f, 0.56f, 0.76f, 0.95f), new Color(0.84f, 0.92f, 0.98f), 22f,
            () =>
            {
                AudioManager.Instance?.PlayButtonClick();
                Time.timeScale = 1f;
                _isPaused = false;
                if (settingsPanelObj != null) settingsPanelObj.SetActive(false);
                GameManager.Instance?.RestartGame();
            });

        // 7. ANA SAYFAYA DÖN
        CreatePortraitButton(
            cardObj.transform, "PortraitHomeBtn", "ANA SAYFAYA DÖN",
            new Vector2(0f, -862f), new Vector2(788f, 80f),
            new Color(0.24f, 0.09f, 0.12f, 1f), new Color(0.78f, 0.24f, 0.28f, 0.95f), new Color(0.98f, 0.72f, 0.74f), 22f,
            () =>
            {
                AudioManager.Instance?.PlayButtonClick();
                ReturnToHome();
            });
    }

    private TextMeshProUGUI CreatePortraitLabel(Transform parent, string name, string text, Vector2 topPos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align, float spacing)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = topPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = spacing;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button CreatePortraitButton(Transform parent, string name, string label, Vector2 topPos, Vector2 size, Color fill, Color border, Color textColor, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = topPos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.sprite = MainMenuManager.GetRoundedBoxSprite($"p_pause_{name}", fill, border, 12, 2);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick);

        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(16f, 4f);
        tRt.offsetMax = new Vector2(-16f, -4f);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        return btn;
    }

    private Slider CreatePortraitVolumeBox(Transform parent, string boxName, string title, float topY, bool isMusic, out TextMeshProUGUI valueTMP)
    {
        GameObject boxObj = new GameObject(boxName, typeof(RectTransform));
        boxObj.transform.SetParent(parent, false);
        RectTransform boxRt = boxObj.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 1f);
        boxRt.anchorMax = new Vector2(0.5f, 1f);
        boxRt.pivot = new Vector2(0.5f, 1f);
        boxRt.anchoredPosition = new Vector2(0f, topY);
        boxRt.sizeDelta = new Vector2(788f, 140f);

        Image boxImg = boxObj.AddComponent<Image>();
        boxImg.sprite = MainMenuManager.GetRoundedBoxSprite("p_pause_vol_box", new Color(0.09f, 0.14f, 0.20f, 0.98f), new Color(0.18f, 0.28f, 0.38f, 0.85f), 12, 1);
        boxImg.type = Image.Type.Sliced;
        boxImg.color = Color.white;

        CreatePortraitLabel(boxObj.transform, "Label", title, new Vector2(-20f, -16f), new Vector2(680f, 34f), 20f, new Color(0.82f, 0.89f, 0.96f), TextAlignmentOptions.MidlineLeft, 1.4f);
        valueTMP = CreatePortraitLabel(boxObj.transform, "Value", "%50", new Vector2(0f, -16f), new Vector2(724f, 34f), 22f, new Color(0.15f, 0.88f, 1f), TextAlignmentOptions.MidlineRight, 1.0f);

        GameObject sliderObj = new GameObject("Slider", typeof(RectTransform));
        sliderObj.transform.SetParent(boxObj.transform, false);
        RectTransform sRt = sliderObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 1f);
        sRt.anchorMax = new Vector2(0.5f, 1f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.anchoredPosition = new Vector2(0f, -92f);
        sRt.sizeDelta = new Vector2(536f, 38f);

        Image trackBg = sliderObj.AddComponent<Image>();
        trackBg.sprite = MainMenuManager.GetRoundedBoxSprite("p_pause_slider_track", new Color(0.03f, 0.06f, 0.10f, 1f), new Color(0.18f, 0.30f, 0.42f, 1f), 8, 1);
        trackBg.type = Image.Type.Sliced;
        trackBg.color = Color.white;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(4f, 4f);
        faRt.offsetMax = new Vector2(-4f, -4f);

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.07f, 0.82f, 0.96f, 0.96f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = fillRt;
        slider.targetGraphic = fillImg;

        TextMeshProUGUI capturedVal = valueTMP;
        slider.onValueChanged.AddListener((val) =>
        {
            if (isMusic) AudioManager.Instance?.SetMusicVolume(val);
            else AudioManager.Instance?.SetSFXVolume(val);
            if (capturedVal != null) capturedVal.text = $"%{(int)(val * 100)}";
            UpdateMuteVisuals();
        });

        CreatePortraitButton(
            boxObj.transform, "MinusBtn", "<b>-</b>",
            new Vector2(-330f, -64f), new Vector2(86f, 56f),
            new Color(0.06f, 0.11f, 0.18f, 1f), new Color(0.24f, 0.44f, 0.62f, 1f), Color.white, 30f,
            () =>
            {
                slider.value = Mathf.Clamp01(slider.value - 0.10f);
                if (!isMusic) AudioManager.Instance?.PlayGunfire(false);
            });

        CreatePortraitButton(
            boxObj.transform, "PlusBtn", "<b>+</b>",
            new Vector2(330f, -64f), new Vector2(86f, 56f),
            new Color(0.06f, 0.11f, 0.18f, 1f), new Color(0.24f, 0.44f, 0.62f, 1f), Color.white, 30f,
            () =>
            {
                slider.value = Mathf.Clamp01(slider.value + 0.10f);
                if (!isMusic) AudioManager.Instance?.PlayGunfire(false);
            });

        return slider;
    }

    private static void FormatPausePixelExactButton(GameObject btnObj, Vector2 centerPos1080, Vector2 size1080)
    {
        if (btnObj == null) return;
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centerPos1080;
            rt.sizeDelta = size1080;
        }
        Image img = btnObj.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = null;
            img.color = new Color(1f, 1f, 1f, 0.01f);
        }
        Button btn = btnObj.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0.01f);
            cb.highlightedColor = new Color(0.35f, 0.85f, 1f, 0.14f);
            cb.pressedColor = new Color(0.20f, 0.70f, 1f, 0.25f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0.01f);
            btn.colors = cb;
        }
    }

    private Slider CreateVolumeRow(Transform parentRoot, string label, float barCenterY1080, float valCenterY1080, bool isMusic, out TextMeshProUGUI valueTMP)
    {
        // Yüzde Metni (%53 / %51) - Eşit çerçeveye tam hizalı: centerX = 415.0f
        GameObject valObj = new GameObject(label + "_ValText", typeof(RectTransform));
        valObj.transform.SetParent(parentRoot, false);
        RectTransform valRt = valObj.GetComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.5f, 0.5f);
        valRt.anchorMax = new Vector2(0.5f, 0.5f);
        valRt.pivot = new Vector2(0.5f, 0.5f);
        valRt.anchoredPosition = new Vector2(415.0f, valCenterY1080);
        valRt.sizeDelta = new Vector2(100f, 50f);

        valueTMP = valObj.AddComponent<TextMeshProUGUI>();
        valueTMP.font = MainMenuManager.GetTacticalFont();
        valueTMP.fontSize = 30f;
        valueTMP.fontStyle = FontStyles.Bold;
        valueTMP.characterSpacing = 1.0f;
        valueTMP.color = Color.white;
        valueTMP.alignment = TextAlignmentOptions.Center;

        // [-] Butonu (Tam ortalanmış simetrik: centerX = -360.0f)
        GameObject minusBtnObj = new GameObject(label + "_MinusBtn", typeof(RectTransform));
        minusBtnObj.transform.SetParent(parentRoot, false);
        Image minusBg = minusBtnObj.AddComponent<Image>();
        Button minusBtn = minusBtnObj.AddComponent<Button>();
        minusBtn.targetGraphic = minusBg;
        FormatPausePixelExactButton(minusBtnObj, new Vector2(-360.0f, barCenterY1080), new Vector2(64f, 60f));

        // [+] Butonu (Tam ortalanmış simetrik: centerX = +360.0f)
        GameObject plusBtnObj = new GameObject(label + "_PlusBtn", typeof(RectTransform));
        plusBtnObj.transform.SetParent(parentRoot, false);
        Image plusBg = plusBtnObj.AddComponent<Image>();
        Button plusBtn = plusBtnObj.AddComponent<Button>();
        plusBtn.targetGraphic = plusBg;
        FormatPausePixelExactButton(plusBtnObj, new Vector2(360.0f, barCenterY1080), new Vector2(64f, 60f));

        // Ortadaki Kaydırıcı (Slider: Tam ortalanmış: centerX = 0f)
        GameObject sliderObj = new GameObject(label + "_Slider", typeof(RectTransform));
        sliderObj.transform.SetParent(parentRoot, false);
        RectTransform sRt = sliderObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0.5f);
        sRt.anchorMax = new Vector2(0.5f, 0.5f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.anchoredPosition = new Vector2(0f, barCenterY1080);
        sRt.sizeDelta = new Vector2(498f, 41f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Slider Background (şeffaf - arka plandaki çizgili koyu kanal görünür)
        GameObject bgObj = new GameObject("Background", typeof(RectTransform));
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero;
        faRt.offsetMax = Vector2.zero;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.07f, 0.82f, 0.96f, 0.96f);

        // Cyan barın ortasındaki ince yatay çizgi (referans görseldekiyle birebir aynı)
        GameObject lineObj = new GameObject("CenterLine", typeof(RectTransform));
        lineObj.transform.SetParent(fillObj.transform, false);
        RectTransform lineRt = lineObj.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0.5f);
        lineRt.anchorMax = new Vector2(1f, 0.5f);
        lineRt.pivot = new Vector2(0.5f, 0.5f);
        lineRt.anchoredPosition = Vector2.zero;
        lineRt.sizeDelta = new Vector2(0f, 2f);
        Image lineImg = lineObj.AddComponent<Image>();
        lineImg.color = new Color(0.65f, 0.96f, 1f, 0.65f);
        lineImg.raycastTarget = false;

        slider.fillRect = fillRt;
        slider.targetGraphic = fillImg;

        TextMeshProUGUI capturedValTMP = valueTMP;

        // Slider Değişimi Dinleyicisi
        slider.onValueChanged.AddListener((val) =>
        {
            if (isMusic)
            {
                AudioManager.Instance?.SetMusicVolume(val);
            }
            else
            {
                AudioManager.Instance?.SetSFXVolume(val);
            }

            if (capturedValTMP != null)
                capturedValTMP.text = $"%{(int)(val * 100)}";

            UpdateMuteVisuals();
        });

        // [-] Butonu ile 0.10 Kısma
        minusBtn.onClick.AddListener(() =>
        {
            float current = slider.value;
            slider.value = Mathf.Clamp01(current - 0.10f);
            if (!isMusic) AudioManager.Instance?.PlayGunfire(false);
        });

        // [+] Butonu ile 0.10 Yükseltme
        plusBtn.onClick.AddListener(() =>
        {
            float current = slider.value;
            slider.value = Mathf.Clamp01(current + 0.10f);
            if (!isMusic) AudioManager.Instance?.PlayGunfire(false);
        });

        return slider;
    }

    public void TogglePauseMenu()
    {
        SetPaused(!_isPaused);
    }

    public void SetPaused(bool pause)
    {
        _isPaused = pause;

        // Kamera sarsıntısı ortasında duraklatıldıysa ekranın kayık kalmasını kesin önle
        CameraShake.StopAllAndReset();

        // Oyunu duraklat veya devam ettir
        Time.timeScale = _isPaused ? 0f : 1f;
        if (!_isPaused)
            Time.fixedDeltaTime = 0.02f;

        bool portraitDuel = MainMenuManager.IsPortraitDuelModeActive();

        if (settingsPanelObj != null)
        {
            if (_landscapePauseRoot != null) _landscapePauseRoot.SetActive(!portraitDuel);
            if (_portraitPauseRoot != null) _portraitPauseRoot.SetActive(portraitDuel);

            settingsPanelObj.SetActive(_isPaused);
            if (_isPaused)
            {
                transform.SetAsLastSibling();
                settingsPanelObj.transform.SetAsLastSibling();
            }
        }

        if (_isPaused)
        {
            if (soundButtonObj != null) soundButtonObj.SetActive(false);
            MainMenuManager.SetTouchControlsVisibility(false, false);
            UpdateUIValues();
        }
        else
        {
            MainMenuManager.ApplyScreenOrientationAndCanvas(portraitDuel);
            MainMenuManager.SetTouchControlsVisibility(showP1Controls: true, showEnemyControls: portraitDuel);
            TouchArrowControls.Instance?.EnsureArrowUI();
            RefreshPauseButtonPosition();
            if (soundButtonObj != null)
            {
                soundButtonObj.SetActive(true);
                soundButtonObj.transform.SetAsLastSibling();
            }
        }

        TouchArrowControls.Instance?.UpdateVisibility();
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public void ReturnToHome()
    {
        CameraShake.StopAllAndReset();
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        _isPaused = false;

        if (settingsPanelObj != null)
            settingsPanelObj.SetActive(false);

        // GameManager üzerinden ana menüye dön
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GoToMainMenu();
        }
    }

    public void UpdateUIValues()
    {
        if (AudioManager.Instance != null)
        {
            float mVol = AudioManager.Instance.musicVolume;
            float sVol = AudioManager.Instance.sfxVolume;

            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(mVol);
            if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(sVol);
            if (_pMusicSlider != null) _pMusicSlider.SetValueWithoutNotify(mVol);
            if (_pSfxSlider != null) _pSfxSlider.SetValueWithoutNotify(sVol);

            string mPct = $"%{(int)(mVol * 100)}";
            string sPct = $"%{(int)(sVol * 100)}";
            if (_musicValTMP != null) _musicValTMP.text = mPct;
            if (_sfxValTMP != null) _sfxValTMP.text = sPct;
            if (_pMusicValTMP != null) _pMusicValTMP.text = mPct;
            if (_pSfxValTMP != null) _pSfxValTMP.text = sPct;
        }
        UpdateMuteVisuals();
        UpdateControlVisuals();
    }

    private void UpdateControlVisuals()
    {
        bool isArrows = (GameSettings.SelectedControl == GameSettings.ControlType.Arrows);
        string labelText = isArrows
            ? "UÇUŞ KONTROLÜ: <color=#00E5FF>SOL / SAĞ OK TUŞLARI</color>"
            : "UÇUŞ KONTROLÜ: <color=#D99B38>SANAL JOYSTICK</color>";

        if (_controlBtnTMP != null) _controlBtnTMP.text = labelText;
        if (_pControlBtnTMP != null) _pControlBtnTMP.text = labelText;

        if (_controlBtnBg != null)
        {
            _controlBtnBg.sprite = null;
            _controlBtnBg.color = new Color(1f, 1f, 1f, 0.01f);
        }

        if (_pauseArrowsPreviewObj != null)
            _pauseArrowsPreviewObj.SetActive(isArrows);
        if (_pauseJoystickPreviewObj != null)
            _pauseJoystickPreviewObj.SetActive(!isArrows);
    }

    private void BuildControlSchemePreview(Transform parentRoot)
    {
        // 1. Sanal Joystick Önizlemesi (Gimbal Çemberi + Başlık - 2 kademe büyütülmüş gerçek boyut)
        _pauseJoystickPreviewObj = new GameObject("PauseJoystickPreview", typeof(RectTransform));
        _pauseJoystickPreviewObj.transform.SetParent(parentRoot, false);
        RectTransform jRt = _pauseJoystickPreviewObj.GetComponent<RectTransform>();
        jRt.anchorMin = new Vector2(0f, 0f);
        jRt.anchorMax = new Vector2(0f, 0f);
        jRt.pivot = new Vector2(0.5f, 0.5f);
        jRt.anchoredPosition = new Vector2(182f, 180f);
        jRt.sizeDelta = new Vector2(330f, 330f);

        Image jBg = _pauseJoystickPreviewObj.AddComponent<Image>();
        Sprite jBase = Resources.Load<Sprite>("joystick_base_p1") ?? LoadLocalSprite("joystick_base_p1.png");
        if (jBase != null)
        {
            jBg.sprite = jBase;
            jBg.preserveAspect = true;
            jBg.color = Color.white;
        }

        GameObject jHandle = new GameObject("Handle", typeof(RectTransform));
        jHandle.transform.SetParent(_pauseJoystickPreviewObj.transform, false);
        RectTransform hRt = jHandle.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.5f, 0.5f);
        hRt.anchorMax = new Vector2(0.5f, 0.5f);
        hRt.pivot = new Vector2(0.5f, 0.5f);
        hRt.anchoredPosition = Vector2.zero;
        hRt.sizeDelta = new Vector2(138f, 138f);

        Image hImg = jHandle.AddComponent<Image>();
        Sprite jHnd = Resources.Load<Sprite>("joystick_handle_p1") ?? LoadLocalSprite("joystick_handle_p1.png");
        if (jHnd != null)
        {
            hImg.sprite = jHnd;
            hImg.preserveAspect = true;
            hImg.color = Color.white;
        }

        // 2. Sol / Sağ Ok Tuşları Önizlemesi ([ ◀ ] ve [ ▶ ] - 1 kademe ayrılmış güncel hiza)
        _pauseArrowsPreviewObj = new GameObject("PauseArrowsPreview", typeof(RectTransform));
        _pauseArrowsPreviewObj.transform.SetParent(parentRoot, false);
        RectTransform aRt = _pauseArrowsPreviewObj.GetComponent<RectTransform>();
        aRt.anchorMin = new Vector2(0f, 0f);
        aRt.anchorMax = new Vector2(0f, 0f);
        aRt.pivot = new Vector2(0f, 0f);
        aRt.anchoredPosition = new Vector2(28f, 22f);
        aRt.sizeDelta = new Vector2(430f, 184f);

        // Sol Ok Butonu
        GameObject leftArrow = new GameObject("LeftArrow", typeof(RectTransform));
        leftArrow.transform.SetParent(_pauseArrowsPreviewObj.transform, false);
        RectTransform lRt = leftArrow.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(0f, 0f);
        lRt.pivot = new Vector2(0.5f, 0.5f);
        lRt.anchoredPosition = new Vector2(94f, 88f);
        lRt.sizeDelta = new Vector2(184f, 168f);
        Image lImg = leftArrow.AddComponent<Image>();
        Sprite lSpr = Resources.Load<Sprite>("btn_arrow_left") ?? LoadLocalSprite("btn_arrow_left.png");
        if (lSpr != null)
        {
            lImg.sprite = lSpr;
            lImg.preserveAspect = true;
            lImg.color = Color.white;
        }

        // Sağ Ok Butonu
        GameObject rightArrow = new GameObject("RightArrow", typeof(RectTransform));
        rightArrow.transform.SetParent(_pauseArrowsPreviewObj.transform, false);
        RectTransform rRt = rightArrow.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 0f);
        rRt.anchorMax = new Vector2(0f, 0f);
        rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.anchoredPosition = new Vector2(330f, 88f);
        rRt.sizeDelta = new Vector2(184f, 168f);
        Image rImg = rightArrow.AddComponent<Image>();
        Sprite rSpr = Resources.Load<Sprite>("btn_arrow_right") ?? LoadLocalSprite("btn_arrow_right.png");
        if (rSpr != null)
        {
            rImg.sprite = rSpr;
            rImg.preserveAspect = true;
            rImg.color = Color.white;
        }
    }

    private static Sprite LoadLocalSprite(string fileName)
    {
        try
        {
            string path = System.IO.Path.Combine(Application.dataPath, "Sprites", fileName);
            if (System.IO.File.Exists(path))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }
        catch { }
        return null;
    }

    private void UpdateMuteVisuals()
    {
        bool muted = AudioManager.Instance != null && AudioManager.Instance.isMuted;
        string txt = muted ? "TÜM SESLERİ AÇ" : "TÜM SESLERİ KAPAT";
        Color col = muted ? new Color(0.20f, 0.90f, 1.00f, 1f) : new Color(0.56f, 0.65f, 0.74f, 1f);

        if (_muteBtnTMP != null)
        {
            _muteBtnTMP.text = txt;
            _muteBtnTMP.color = col;
        }
        if (_pMuteBtnTMP != null)
        {
            _pMuteBtnTMP.text = txt;
            _pMuteBtnTMP.color = muted ? new Color(0.20f, 0.90f, 1.00f, 1f) : new Color(0.75f, 0.84f, 0.92f, 1f);
        }
    }
}
