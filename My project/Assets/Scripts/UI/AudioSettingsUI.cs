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
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void Start()
    {
        EnsureUI();
        UpdateUIValues();
    }

    private void Update()
    {
        // Ana menü açıkken durdurma butonunu gizle, sadece savaşta sağ üstte göster
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy);

        if (soundButtonObj != null)
        {
            bool shouldShow = !isMenuOpen;
            if (soundButtonObj.activeSelf != shouldShow)
            {
                soundButtonObj.SetActive(shouldShow);
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
            go = new GameObject("AudioSettingsUI");
            go.transform.SetParent(canvas.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
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

        // 1. Sağ Üst Köşe Şeffaf Pause İkon Butonu
        if (soundButtonObj == null)
        {
            Transform existingBtn = transform.Find("SoundToggleButton");
            if (existingBtn != null)
            {
                soundButtonObj = existingBtn.gameObject;
            }
            else
            {
                soundButtonObj = new GameObject("SoundToggleButton");
                soundButtonObj.transform.SetParent(transform, false);
            }
        }

        // Buton boyutunu ve konumunu şeffaf pause ikonuna göre ayarla (46x46 piksel)
        RectTransform rt = soundButtonObj.GetComponent<RectTransform>();
        if (rt == null) rt = soundButtonObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f); // Sağ Üst Köşe
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-22f, -22f);
        rt.sizeDelta = new Vector2(46f, 46f);

        Image btnBg = soundButtonObj.GetComponent<Image>();
        if (btnBg == null) btnBg = soundButtonObj.AddComponent<Image>();

        Sprite pauseSprite = Resources.Load<Sprite>("pause_icon");
        if (pauseSprite != null)
        {
            btnBg.sprite = pauseSprite;
            btnBg.color = Color.white;
        }
        else
        {
            btnBg.color = new Color(0f, 0.85f, 1f, 0.85f);
        }

        // Varsa eski kutu outline'ını kaldır (ikonun kendi neon ışıması var)
        Outline outline = soundButtonObj.GetComponent<Outline>();
        if (outline != null)
        {
            Destroy(outline);
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

        // Varsa eski "|| DURDUR" yazı objesini kaldır
        Transform iconChild = soundButtonObj.transform.Find("IconText");
        if (iconChild != null)
        {
            Destroy(iconChild.gameObject);
        }

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
        settingsPanelObj = new GameObject("AudioSettingsPanel");
        settingsPanelObj.transform.SetParent(transform, false);

        RectTransform panelRt = settingsPanelObj.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(530f, 540f);

        Image bg = settingsPanelObj.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.07f, 0.13f, 0.98f);

        Outline outline = settingsPanelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        // Başlık: OYUN DURDURULDU
        GameObject titleObj = new GameObject("PanelTitle");
        titleObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.91f);
        titleRt.anchorMax = new Vector2(0.5f, 0.91f);
        titleRt.sizeDelta = new Vector2(500f, 40f);

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "OYUN DURDURULDU";
        titleTMP.fontSize = 28f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(1f, 0.85f, 0.2f); // Parlak Altın Sarısı

        // Alt Başlık: Hava sahası duraklatıldı
        GameObject subObj = new GameObject("PanelSubtitle");
        subObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 0.84f);
        subRt.anchorMax = new Vector2(0.5f, 0.84f);
        subRt.sizeDelta = new Vector2(500f, 28f);

        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.text = "Hava Sahasi ve Operasyon Duraklatildi";
        subTMP.fontSize = 15.5f;
        subTMP.fontStyle = FontStyles.Bold;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = new Color(0.35f, 0.9f, 1f);

        // 1. Savaş Müziği Satırı: [-]  Slider  [+]
        _musicSlider = CreateVolumeRow("SAVAS MUZIGI", 0.70f, isMusic: true, out _musicValTMP);

        // 2. Mermi / Efekt Sesi Satırı: [-]  Slider  [+]
        _sfxSlider = CreateVolumeRow("MERMI VE PATLAMA", 0.54f, isMusic: false, out _sfxValTMP);

        // 3. Mute Butonu
        GameObject muteBtnObj = new GameObject("MuteButton");
        muteBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform muteRt = muteBtnObj.AddComponent<RectTransform>();
        muteRt.anchorMin = new Vector2(0.5f, 0.38f);
        muteRt.anchorMax = new Vector2(0.5f, 0.38f);
        muteRt.sizeDelta = new Vector2(300f, 42f);

        Image muteBg = muteBtnObj.AddComponent<Image>();
        muteBg.color = new Color(0.12f, 0.20f, 0.30f, 0.95f);

        Button muteBtn = muteBtnObj.AddComponent<Button>();
        muteBtn.targetGraphic = muteBg;
        muteBtn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.ToggleMute();
            UpdateMuteVisuals();
        });

        GameObject muteTxtObj = new GameObject("MuteText");
        muteTxtObj.transform.SetParent(muteBtnObj.transform, false);
        RectTransform muteTxtRt = muteTxtObj.AddComponent<RectTransform>();
        muteTxtRt.anchorMin = Vector2.zero;
        muteTxtRt.anchorMax = Vector2.one;
        muteTxtRt.offsetMin = Vector2.zero;
        muteTxtRt.offsetMax = Vector2.zero;

        _muteBtnTMP = muteTxtObj.AddComponent<TextMeshProUGUI>();
        _muteBtnTMP.fontSize = 16f;
        _muteBtnTMP.fontStyle = FontStyles.Bold;
        _muteBtnTMP.alignment = TextAlignmentOptions.Center;
        _muteBtnTMP.color = Color.white;
        _muteBtnTMP.raycastTarget = false;

        // 4. DEVAM ET BUTONU (Büyük Yeşil Buton)
        GameObject resumeBtnObj = new GameObject("ResumeButton");
        resumeBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform resumeRt = resumeBtnObj.AddComponent<RectTransform>();
        resumeRt.anchorMin = new Vector2(0.5f, 0.23f);
        resumeRt.anchorMax = new Vector2(0.5f, 0.23f);
        resumeRt.sizeDelta = new Vector2(380f, 56f);

        Image resumeBg = resumeBtnObj.AddComponent<Image>();
        resumeBg.color = new Color(0.12f, 0.58f, 0.32f, 0.98f);

        Outline resumeOutline = resumeBtnObj.AddComponent<Outline>();
        resumeOutline.effectColor = new Color(0.2f, 1f, 0.5f, 0.7f);
        resumeOutline.effectDistance = new Vector2(2f, -2f);

        Button resumeBtn = resumeBtnObj.AddComponent<Button>();
        resumeBtn.targetGraphic = resumeBg;
        resumeBtn.onClick.AddListener(ResumeGame);

        GameObject resumeTxtObj = new GameObject("ResumeText");
        resumeTxtObj.transform.SetParent(resumeBtnObj.transform, false);
        RectTransform resumeTxtRt = resumeTxtObj.AddComponent<RectTransform>();
        resumeTxtRt.anchorMin = Vector2.zero;
        resumeTxtRt.anchorMax = Vector2.one;
        resumeTxtRt.offsetMin = Vector2.zero;
        resumeTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI resumeTMP = resumeTxtObj.AddComponent<TextMeshProUGUI>();
        resumeTMP.text = "DEVAM ET";
        resumeTMP.fontSize = 22f;
        resumeTMP.fontStyle = FontStyles.Bold;
        resumeTMP.alignment = TextAlignmentOptions.Center;
        resumeTMP.color = Color.white;
        resumeTMP.raycastTarget = false;

        // 5. ANA SAYFAYA DÖN BUTONU (Kırmızı / Lacivert Buton)
        GameObject homeBtnObj = new GameObject("HomeButton");
        homeBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform homeRt = homeBtnObj.AddComponent<RectTransform>();
        homeRt.anchorMin = new Vector2(0.5f, 0.09f);
        homeRt.anchorMax = new Vector2(0.5f, 0.09f);
        homeRt.sizeDelta = new Vector2(380f, 48f);

        Image homeBg = homeBtnObj.AddComponent<Image>();
        homeBg.color = new Color(0.68f, 0.18f, 0.18f, 0.98f);

        Outline homeOutline = homeBtnObj.AddComponent<Outline>();
        homeOutline.effectColor = new Color(1f, 0.3f, 0.3f, 0.7f);
        homeOutline.effectDistance = new Vector2(2f, -2f);

        Button homeBtn = homeBtnObj.AddComponent<Button>();
        homeBtn.targetGraphic = homeBg;
        homeBtn.onClick.AddListener(ReturnToHome);

        GameObject homeTxtObj = new GameObject("HomeText");
        homeTxtObj.transform.SetParent(homeBtnObj.transform, false);
        RectTransform homeTxtRt = homeTxtObj.AddComponent<RectTransform>();
        homeTxtRt.anchorMin = Vector2.zero;
        homeTxtRt.anchorMax = Vector2.one;
        homeTxtRt.offsetMin = Vector2.zero;
        homeTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI homeTMP = homeTxtObj.AddComponent<TextMeshProUGUI>();
        homeTMP.text = "ANA SAYFAYA DON";
        homeTMP.fontSize = 18f;
        homeTMP.fontStyle = FontStyles.Bold;
        homeTMP.alignment = TextAlignmentOptions.Center;
        homeTMP.color = Color.white;
        homeTMP.raycastTarget = false;

        settingsPanelObj.SetActive(false);
    }

    private Slider CreateVolumeRow(string label, float yAnchor, bool isMusic, out TextMeshProUGUI valueTMP)
    {
        // Satır Başlığı
        GameObject headerObj = new GameObject(label + "_Header");
        headerObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform hRt = headerObj.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.08f, yAnchor + 0.065f);
        hRt.anchorMax = new Vector2(0.92f, yAnchor + 0.065f);
        hRt.sizeDelta = new Vector2(0f, 26f);

        TextMeshProUGUI titleTMP = headerObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = label;
        titleTMP.fontSize = 17f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(0.2f, 0.9f, 1f);
        titleTMP.alignment = TextAlignmentOptions.Left;

        // Yüzde Metni (%70)
        GameObject valObj = new GameObject(label + "_ValText");
        valObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform valRt = valObj.AddComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.65f, yAnchor + 0.065f);
        valRt.anchorMax = new Vector2(0.92f, yAnchor + 0.065f);
        valRt.sizeDelta = new Vector2(0f, 26f);

        valueTMP = valObj.AddComponent<TextMeshProUGUI>();
        valueTMP.fontSize = 17f;
        valueTMP.fontStyle = FontStyles.Bold;
        valueTMP.color = Color.white;
        valueTMP.alignment = TextAlignmentOptions.Right;

        // [-] Butonu (Sesi Kıs)
        GameObject minusBtnObj = new GameObject(label + "_MinusBtn");
        minusBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform minusRt = minusBtnObj.AddComponent<RectTransform>();
        minusRt.anchorMin = new Vector2(0.08f, yAnchor);
        minusRt.anchorMax = new Vector2(0.08f, yAnchor);
        minusRt.pivot = new Vector2(0f, 0.5f);
        minusRt.sizeDelta = new Vector2(44f, 36f);

        Image minusBg = minusBtnObj.AddComponent<Image>();
        minusBg.color = new Color(0.15f, 0.24f, 0.36f);

        Button minusBtn = minusBtnObj.AddComponent<Button>();
        minusBtn.targetGraphic = minusBg;

        GameObject minusTxtObj = new GameObject("Text");
        minusTxtObj.transform.SetParent(minusBtnObj.transform, false);
        RectTransform mTxtRt = minusTxtObj.AddComponent<RectTransform>();
        mTxtRt.anchorMin = Vector2.zero;
        mTxtRt.anchorMax = Vector2.one;
        mTxtRt.offsetMin = Vector2.zero;
        mTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI minusTMP = minusTxtObj.AddComponent<TextMeshProUGUI>();
        minusTMP.text = "-";
        minusTMP.fontSize = 24f;
        minusTMP.fontStyle = FontStyles.Bold;
        minusTMP.alignment = TextAlignmentOptions.Center;
        minusTMP.color = Color.white;
        minusTMP.raycastTarget = false;

        // [+] Butonu (Sesi Yükselt)
        GameObject plusBtnObj = new GameObject(label + "_PlusBtn");
        plusBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform plusRt = plusBtnObj.AddComponent<RectTransform>();
        plusRt.anchorMin = new Vector2(0.92f, yAnchor);
        plusRt.anchorMax = new Vector2(0.92f, yAnchor);
        plusRt.pivot = new Vector2(1f, 0.5f);
        plusRt.sizeDelta = new Vector2(44f, 36f);

        Image plusBg = plusBtnObj.AddComponent<Image>();
        plusBg.color = new Color(0.15f, 0.24f, 0.36f);

        Button plusBtn = plusBtnObj.AddComponent<Button>();
        plusBtn.targetGraphic = plusBg;

        GameObject plusTxtObj = new GameObject("Text");
        plusTxtObj.transform.SetParent(plusBtnObj.transform, false);
        RectTransform pTxtRt = plusTxtObj.AddComponent<RectTransform>();
        pTxtRt.anchorMin = Vector2.zero;
        pTxtRt.anchorMax = Vector2.one;
        pTxtRt.offsetMin = Vector2.zero;
        pTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI plusTMP = plusTxtObj.AddComponent<TextMeshProUGUI>();
        plusTMP.text = "+";
        plusTMP.fontSize = 22f;
        plusTMP.fontStyle = FontStyles.Bold;
        plusTMP.alignment = TextAlignmentOptions.Center;
        plusTMP.color = Color.white;
        plusTMP.raycastTarget = false;

        // Ortadaki Kaydırıcı (Slider)
        GameObject sliderObj = new GameObject(label + "_Slider");
        sliderObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform sRt = sliderObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, yAnchor);
        sRt.anchorMax = new Vector2(0.5f, yAnchor);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.sizeDelta = new Vector2(280f, 22f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Slider Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.15f, 0.22f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero;
        faRt.offsetMax = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fillObj.AddComponent<RectTransform>();
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
