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
            bool shouldShow = !isMenuOpen;
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
        if (!isMenuOpen)
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

        // İkon boyutunu ve konumunu şık ve orijinal pause ikonuna göre ayarla
        RectTransform rt = soundButtonObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f); // Sağ Üst Köşe
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-26f, -26f);
        rt.sizeDelta = new Vector2(56f, 56f);

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

    private void BuildPauseAndSettingsPanel()
    {
        settingsPanelObj = new GameObject("AudioSettingsPanel", typeof(RectTransform));
        settingsPanelObj.transform.SetParent(transform, false);

        RectTransform panelRt = settingsPanelObj.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(700f, 680f);

        Image bg = settingsPanelObj.AddComponent<Image>();
        bg.sprite = MainMenuManager.GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.03f, 0.06f, 0.12f, 0.96f), false);
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        // Üst Başlık Visor Plate
        GameObject visorObj = new GameObject("HeaderVisor", typeof(RectTransform));
        visorObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform vRt = visorObj.GetComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0.5f, 0.94f);
        vRt.anchorMax = new Vector2(0.5f, 0.94f);
        vRt.pivot = new Vector2(0.5f, 1f);
        vRt.sizeDelta = new Vector2(660f, 64f);

        Image vImg = visorObj.AddComponent<Image>();
        vImg.sprite = MainMenuManager.GetHudSprite("hud_header_visor", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.04f, 0.10f, 0.18f, 1f), true);
        vImg.type = Image.Type.Sliced;
        vImg.color = Color.white;

        // Başlık: DEFCON // HAVA SAHASI DURAKLATILDI
        GameObject titleObj = new GameObject("PanelTitle", typeof(RectTransform));
        titleObj.transform.SetParent(visorObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.font = MainMenuManager.GetTacticalFont();
        titleTMP.text = "DEFCON // SAVAŞ HAREKATI DURAKLATILDI\n<size=52%><color=#8AB4F8>AVIONICS KOKPİT & SES KONTROL PANELİ</color></size>";
        titleTMP.fontSize = 20f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 2.0f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;
        titleTMP.extraPadding = true;

        // 1. Savaş Müziği Satırı: [-]  Slider  [+]
        _musicSlider = CreateVolumeRow("SAVAS MUZIGI", 0.68f, isMusic: true, out _musicValTMP);

        // 2. Mermi / Efekt Sesi Satırı: [-]  Slider  [+]
        _sfxSlider = CreateVolumeRow("MERMI VE PATLAMA", 0.50f, isMusic: false, out _sfxValTMP);

        // 3. Mute Butonu
        GameObject muteBtnObj = new GameObject("MuteButton", typeof(RectTransform));
        muteBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform muteRt = muteBtnObj.GetComponent<RectTransform>();
        muteRt.anchorMin = new Vector2(0.5f, 0.35f);
        muteRt.anchorMax = new Vector2(0.5f, 0.35f);
        muteRt.sizeDelta = new Vector2(460f, 54f);

        Image muteBg = muteBtnObj.AddComponent<Image>();
        muteBg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.04f, 0.11f, 0.18f, 0.95f), true);
        muteBg.type = Image.Type.Sliced;
        muteBg.color = Color.white;

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
        _muteBtnTMP.fontSize = 17f;
        _muteBtnTMP.fontStyle = FontStyles.Bold;
        _muteBtnTMP.characterSpacing = 1.6f;
        _muteBtnTMP.alignment = TextAlignmentOptions.Center;
        _muteBtnTMP.color = Color.white;
        _muteBtnTMP.extraPadding = true;
        _muteBtnTMP.raycastTarget = false;

        // 4. DEVAM ET BUTONU (Büyük Zümrüt Yeşili HUD Buton)
        GameObject resumeBtnObj = new GameObject("ResumeButton", typeof(RectTransform));
        resumeBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform resumeRt = resumeBtnObj.GetComponent<RectTransform>();
        resumeRt.anchorMin = new Vector2(0.5f, 0.20f);
        resumeRt.anchorMax = new Vector2(0.5f, 0.20f);
        resumeRt.sizeDelta = new Vector2(520f, 62f);

        Image resumeBg = resumeBtnObj.AddComponent<Image>();
        resumeBg.sprite = MainMenuManager.GetHudSprite("hud_btn_emerald", new Color(0.2f, 0.95f, 0.4f, 1f), new Color(0.04f, 0.18f, 0.08f, 1f), true);
        resumeBg.type = Image.Type.Sliced;
        resumeBg.color = Color.white;

        Button resumeBtn = resumeBtnObj.AddComponent<Button>();
        resumeBtn.targetGraphic = resumeBg;
        resumeBtn.onClick.AddListener(ResumeGame);

        GameObject resumeTxtObj = new GameObject("ResumeText", typeof(RectTransform));
        resumeTxtObj.transform.SetParent(resumeBtnObj.transform, false);
        RectTransform resumeTxtRt = resumeTxtObj.GetComponent<RectTransform>();
        resumeTxtRt.anchorMin = Vector2.zero;
        resumeTxtRt.anchorMax = Vector2.one;
        resumeTxtRt.offsetMin = Vector2.zero;
        resumeTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI resumeTMP = resumeTxtObj.AddComponent<TextMeshProUGUI>();
        resumeTMP.font = MainMenuManager.GetTacticalFont();
        resumeTMP.text = "▶ HAREKATA DEVAM ET";
        resumeTMP.fontSize = 21f;
        resumeTMP.fontStyle = FontStyles.Bold;
        resumeTMP.characterSpacing = 2.0f;
        resumeTMP.alignment = TextAlignmentOptions.Center;
        resumeTMP.color = Color.white;
        resumeTMP.extraPadding = true;
        resumeTMP.raycastTarget = false;

        // 5. ANA SAYFAYA DÖN BUTONU (Kırmızı HUD Buton)
        GameObject homeBtnObj = new GameObject("HomeButton", typeof(RectTransform));
        homeBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform homeRt = homeBtnObj.GetComponent<RectTransform>();
        homeRt.anchorMin = new Vector2(0.5f, 0.08f);
        homeRt.anchorMax = new Vector2(0.5f, 0.08f);
        homeRt.sizeDelta = new Vector2(520f, 56f);

        Image homeBg = homeBtnObj.AddComponent<Image>();
        homeBg.sprite = MainMenuManager.GetHudSprite("hud_btn_red", new Color(1f, 0.35f, 0.35f, 1f), new Color(0.24f, 0.05f, 0.05f, 1f), true);
        homeBg.type = Image.Type.Sliced;
        homeBg.color = Color.white;

        Button homeBtn = homeBtnObj.AddComponent<Button>();
        homeBtn.targetGraphic = homeBg;
        homeBtn.onClick.AddListener(ReturnToHome);

        GameObject homeTxtObj = new GameObject("HomeText", typeof(RectTransform));
        homeTxtObj.transform.SetParent(homeBtnObj.transform, false);
        RectTransform homeTxtRt = homeTxtObj.GetComponent<RectTransform>();
        homeTxtRt.anchorMin = Vector2.zero;
        homeTxtRt.anchorMax = Vector2.one;
        homeTxtRt.offsetMin = Vector2.zero;
        homeTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI homeTMP = homeTxtObj.AddComponent<TextMeshProUGUI>();
        homeTMP.font = MainMenuManager.GetTacticalFont();
        homeTMP.text = "◀ ANA ÜSSE GERİ DÖN";
        homeTMP.fontSize = 19f;
        homeTMP.fontStyle = FontStyles.Bold;
        homeTMP.characterSpacing = 1.8f;
        homeTMP.alignment = TextAlignmentOptions.Center;
        homeTMP.color = Color.white;
        homeTMP.extraPadding = true;
        homeTMP.raycastTarget = false;

        settingsPanelObj.SetActive(false);
    }

    private Slider CreateVolumeRow(string label, float yAnchor, bool isMusic, out TextMeshProUGUI valueTMP)
    {
        // Satır Başlığı
        GameObject headerObj = new GameObject(label + "_Header", typeof(RectTransform));
        headerObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform hRt = headerObj.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.08f, yAnchor + 0.075f);
        hRt.anchorMax = new Vector2(0.92f, yAnchor + 0.075f);
        hRt.sizeDelta = new Vector2(0f, 36f);

        TextMeshProUGUI titleTMP = headerObj.AddComponent<TextMeshProUGUI>();
        titleTMP.font = MainMenuManager.GetTacticalFont();
        titleTMP.text = label;
        titleTMP.fontSize = 20f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.characterSpacing = 1.6f;
        titleTMP.color = new Color(0.2f, 0.9f, 1f);
        titleTMP.alignment = TextAlignmentOptions.Left;

        // Yüzde Metni (%70)
        GameObject valObj = new GameObject(label + "_ValText", typeof(RectTransform));
        valObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform valRt = valObj.GetComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.65f, yAnchor + 0.075f);
        valRt.anchorMax = new Vector2(0.92f, yAnchor + 0.075f);
        valRt.sizeDelta = new Vector2(0f, 36f);

        valueTMP = valObj.AddComponent<TextMeshProUGUI>();
        valueTMP.font = MainMenuManager.GetTacticalFont();
        valueTMP.fontSize = 20f;
        valueTMP.fontStyle = FontStyles.Bold;
        valueTMP.characterSpacing = 1.6f;
        valueTMP.color = Color.white;
        valueTMP.alignment = TextAlignmentOptions.Right;

        // [-] Butonu (Sesi Kıs)
        GameObject minusBtnObj = new GameObject(label + "_MinusBtn", typeof(RectTransform));
        minusBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform minusRt = minusBtnObj.GetComponent<RectTransform>();
        minusRt.anchorMin = new Vector2(0.08f, yAnchor);
        minusRt.anchorMax = new Vector2(0.08f, yAnchor);
        minusRt.pivot = new Vector2(0f, 0.5f);
        minusRt.sizeDelta = new Vector2(58f, 46f);

        Image minusBg = minusBtnObj.AddComponent<Image>();
        minusBg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.04f, 0.11f, 0.18f, 1f), true);
        minusBg.type = Image.Type.Sliced;
        minusBg.color = Color.white;

        Button minusBtn = minusBtnObj.AddComponent<Button>();
        minusBtn.targetGraphic = minusBg;

        GameObject minusTxtObj = new GameObject("Text", typeof(RectTransform));
        minusTxtObj.transform.SetParent(minusBtnObj.transform, false);
        RectTransform mTxtRt = minusTxtObj.GetComponent<RectTransform>();
        mTxtRt.anchorMin = Vector2.zero;
        mTxtRt.anchorMax = Vector2.one;
        mTxtRt.offsetMin = Vector2.zero;
        mTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI minusTMP = minusTxtObj.AddComponent<TextMeshProUGUI>();
        minusTMP.font = MainMenuManager.GetTacticalFont();
        minusTMP.text = "-";
        minusTMP.fontSize = 28f;
        minusTMP.fontStyle = FontStyles.Bold;
        minusTMP.alignment = TextAlignmentOptions.Center;
        minusTMP.color = Color.white;
        minusTMP.raycastTarget = false;

        // [+] Butonu (Sesi Yükselt)
        GameObject plusBtnObj = new GameObject(label + "_PlusBtn", typeof(RectTransform));
        plusBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform plusRt = plusBtnObj.GetComponent<RectTransform>();
        plusRt.anchorMin = new Vector2(0.92f, yAnchor);
        plusRt.anchorMax = new Vector2(0.92f, yAnchor);
        plusRt.pivot = new Vector2(1f, 0.5f);
        plusRt.sizeDelta = new Vector2(58f, 46f);

        Image plusBg = plusBtnObj.AddComponent<Image>();
        plusBg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f, 0.9f), new Color(0.04f, 0.11f, 0.18f, 1f), true);
        plusBg.type = Image.Type.Sliced;
        plusBg.color = Color.white;

        Button plusBtn = plusBtnObj.AddComponent<Button>();
        plusBtn.targetGraphic = plusBg;

        GameObject plusTxtObj = new GameObject("Text", typeof(RectTransform));
        plusTxtObj.transform.SetParent(plusBtnObj.transform, false);
        RectTransform pTxtRt = plusTxtObj.GetComponent<RectTransform>();
        pTxtRt.anchorMin = Vector2.zero;
        pTxtRt.anchorMax = Vector2.one;
        pTxtRt.offsetMin = Vector2.zero;
        pTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI plusTMP = plusTxtObj.AddComponent<TextMeshProUGUI>();
        plusTMP.font = MainMenuManager.GetTacticalFont();
        plusTMP.text = "+";
        plusTMP.fontSize = 26f;
        plusTMP.fontStyle = FontStyles.Bold;
        plusTMP.alignment = TextAlignmentOptions.Center;
        plusTMP.color = Color.white;
        plusTMP.raycastTarget = false;

        // Ortadaki Kaydırıcı (Slider)
        GameObject sliderObj = new GameObject(label + "_Slider", typeof(RectTransform));
        sliderObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform sRt = sliderObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, yAnchor);
        sRt.anchorMax = new Vector2(0.5f, yAnchor);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.sizeDelta = new Vector2(360f, 30f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Slider Background
        GameObject bgObj = new GameObject("Background", typeof(RectTransform));
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.15f, 0.22f);

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
        fillImg.color = new Color(0f, 0.85f, 1f);

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
    }

    private void UpdateMuteVisuals()
    {
        bool muted = AudioManager.Instance != null && AudioManager.Instance.isMuted;

        if (_muteBtnTMP != null)
            _muteBtnTMP.text = muted ? "SESLERI AC" : "TUM SESLERI KAPAT";
    }
}
