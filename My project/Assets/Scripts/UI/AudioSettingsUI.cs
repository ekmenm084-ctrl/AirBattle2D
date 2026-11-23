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

    private Slider _musicSlider;
    private Slider _sfxSlider;
    private TextMeshProUGUI _musicValTMP;
    private TextMeshProUGUI _sfxValTMP;
    private TextMeshProUGUI _muteBtnTMP;
    private TextMeshProUGUI _controlBtnTMP;
    private Image _controlBtnBg;

    private bool _isPaused = false;

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
            bool shouldShow = !isMenuOpen && !_isPaused;
            if (soundButtonObj.activeSelf != shouldShow)
            {
                soundButtonObj.SetActive(shouldShow);
                if (shouldShow)
                {
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
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
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

        // İkon boyutunu ve konumunu en köşeye şık ve kompakt olarak ayarla
        RectTransform rt = soundButtonObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f); // Sağ Üst Köşe
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-12f, -12f);
        rt.sizeDelta = new Vector2(44f, 44f);

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

        Image bg = settingsPanelObj.AddComponent<Image>();
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
        _musicSlider = CreateVolumeRow("SAVAS MUZIGI", 200.5f, 264.0f, isMusic: true, out _musicValTMP);

        // 2. Mermi / Efekt Sesi Satırı: [-]  Slider  [+]
        _sfxSlider = CreateVolumeRow("MERMI VE PATLAMA", 76.5f, 140.0f, isMusic: false, out _sfxValTMP);

        // 3. Uçuş Kontrol Tipi Butonu (Eşit çerçeveye tam hizalı: centerX = 20.5f, y = -28.0f)
        GameObject ctrlBtnObj = new GameObject("ControlSchemeButton", typeof(RectTransform));
        ctrlBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform ctrlRt = ctrlBtnObj.GetComponent<RectTransform>();
        ctrlRt.anchorMin = new Vector2(0.5f, 0.5f);
        ctrlRt.anchorMax = new Vector2(0.5f, 0.5f);
        ctrlRt.pivot = new Vector2(0.5f, 0.5f);
        ctrlRt.anchoredPosition = new Vector2(20.5f, -28.0f);
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

        // 4. Mute Butonu (Eşit çerçeveye tam hizalı: centerX = 18.5f, y = -142.0f)
        GameObject muteBtnObj = new GameObject("MuteButton", typeof(RectTransform));
        muteBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform muteRt = muteBtnObj.GetComponent<RectTransform>();
        muteRt.anchorMin = new Vector2(0.5f, 0.5f);
        muteRt.anchorMax = new Vector2(0.5f, 0.5f);
        muteRt.pivot = new Vector2(0.5f, 0.5f);
        muteRt.anchoredPosition = new Vector2(18.5f, -142.0f);
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

        // 5. DEVAM ET BUTONU (Eşit çerçeveye tam hizalı: centerX = 18.5f, y = -252.0f)
        GameObject resumeBtnObj = new GameObject("ResumeButton", typeof(RectTransform));
        resumeBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        Image resumeBg = resumeBtnObj.AddComponent<Image>();
        Button resumeBtn = resumeBtnObj.AddComponent<Button>();
        resumeBtn.targetGraphic = resumeBg;
        resumeBtn.onClick.AddListener(ResumeGame);
        FormatPausePixelExactButton(resumeBtnObj, new Vector2(18.5f, -252.0f), new Vector2(671f, 72f));

        // 6. ANA SAYFAYA DÖN BUTONU (Eşit çerçeveye tam hizalı: centerX = 18.5f, y = -360.0f)
        GameObject homeBtnObj = new GameObject("HomeButton", typeof(RectTransform));
        homeBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        Image homeBg = homeBtnObj.AddComponent<Image>();
        Button homeBtn = homeBtnObj.AddComponent<Button>();
        homeBtn.targetGraphic = homeBg;
        homeBtn.onClick.AddListener(ReturnToHome);
        FormatPausePixelExactButton(homeBtnObj, new Vector2(18.5f, -360.0f), new Vector2(671f, 72f));

        settingsPanelObj.SetActive(false);
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

    private Slider CreateVolumeRow(string label, float barCenterY1080, float valCenterY1080, bool isMusic, out TextMeshProUGUI valueTMP)
    {
        // Yüzde Metni (%53 / %51) - Eşit çerçeveye tam hizalı: centerX = 359.0f
        GameObject valObj = new GameObject(label + "_ValText", typeof(RectTransform));
        valObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform valRt = valObj.GetComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.5f, 0.5f);
        valRt.anchorMax = new Vector2(0.5f, 0.5f);
        valRt.pivot = new Vector2(0.5f, 0.5f);
        valRt.anchoredPosition = new Vector2(359.0f, valCenterY1080);
        valRt.sizeDelta = new Vector2(120f, 50f);

        valueTMP = valObj.AddComponent<TextMeshProUGUI>();
        valueTMP.font = MainMenuManager.GetTacticalFont();
        valueTMP.fontSize = 30f;
        valueTMP.fontStyle = FontStyles.Bold;
        valueTMP.characterSpacing = 1.0f;
        valueTMP.color = Color.white;
        valueTMP.alignment = TextAlignmentOptions.Center;

        // [-] Butonu (Eşit çerçeveye tam hizalı: centerX = -347.0f)
        GameObject minusBtnObj = new GameObject(label + "_MinusBtn", typeof(RectTransform));
        minusBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        Image minusBg = minusBtnObj.AddComponent<Image>();
        Button minusBtn = minusBtnObj.AddComponent<Button>();
        minusBtn.targetGraphic = minusBg;
        FormatPausePixelExactButton(minusBtnObj, new Vector2(-347.0f, barCenterY1080), new Vector2(64f, 60f));

        // [+] Butonu (Eşit çerçeveye tam hizalı: centerX = +372.0f)
        GameObject plusBtnObj = new GameObject(label + "_PlusBtn", typeof(RectTransform));
        plusBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        Image plusBg = plusBtnObj.AddComponent<Image>();
        Button plusBtn = plusBtnObj.AddComponent<Button>();
        plusBtn.targetGraphic = plusBg;
        FormatPausePixelExactButton(plusBtnObj, new Vector2(372.0f, barCenterY1080), new Vector2(64f, 60f));

        // Ortadaki Kaydırıcı (Slider: Eşit çerçeveye tam hizalı: centerX = 12.0f)
        GameObject sliderObj = new GameObject(label + "_Slider", typeof(RectTransform));
        sliderObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform sRt = sliderObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0.5f);
        sRt.anchorMax = new Vector2(0.5f, 0.5f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.anchoredPosition = new Vector2(12.0f, barCenterY1080);
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

        // Oyunu duraklat veya devam ettir
        Time.timeScale = _isPaused ? 0f : 1f;

        if (settingsPanelObj != null)
        {
            settingsPanelObj.SetActive(_isPaused);
            if (_isPaused)
            {
                transform.SetAsLastSibling();
                settingsPanelObj.transform.SetAsLastSibling();
            }
        }

        if (_isPaused)
        {
            UpdateUIValues();
        }
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public void ReturnToHome()
    {
        Time.timeScale = 1f;
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
            if (_musicSlider != null)
                _musicSlider.value = AudioManager.Instance.musicVolume;

            if (_sfxSlider != null)
                _sfxSlider.value = AudioManager.Instance.sfxVolume;

            if (_musicValTMP != null)
                _musicValTMP.text = $"%{(int)(AudioManager.Instance.musicVolume * 100)}";

            if (_sfxValTMP != null)
                _sfxValTMP.text = $"%{(int)(AudioManager.Instance.sfxVolume * 100)}";
        }
        UpdateMuteVisuals();
        UpdateControlVisuals();
    }

    private void UpdateControlVisuals()
    {
        if (_controlBtnTMP == null) return;
        bool isArrows = (GameSettings.SelectedControl == GameSettings.ControlType.Arrows);
        if (isArrows)
        {
            _controlBtnTMP.text = "UÇUŞ KONTROLÜ: <color=#00E5FF>SOL / SAĞ OK TUŞLARI</color>";
        }
        else
        {
            _controlBtnTMP.text = "UÇUŞ KONTROLÜ: <color=#D99B38>SANAL JOYSTICK</color>";
        }
        if (_controlBtnBg != null)
        {
            _controlBtnBg.sprite = null;
            _controlBtnBg.color = new Color(1f, 1f, 1f, 0.01f);
        }
    }

    private void UpdateMuteVisuals()
    {
        bool muted = AudioManager.Instance != null && AudioManager.Instance.isMuted;

        if (_muteBtnTMP != null)
        {
            _muteBtnTMP.text = muted ? "TÜM SESLERİ AÇ" : "TÜM SESLERİ KAPAT";
            _muteBtnTMP.color = muted ? new Color(0.20f, 0.90f, 1.00f, 1f) : new Color(0.56f, 0.65f, 0.74f, 1f);
        }
    }
}
