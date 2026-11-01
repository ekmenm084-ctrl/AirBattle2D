using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Oyun içi ses ve müzik ayar yöneticisi.
/// Savaş müziği ve mermi seslerinin düzeyini slider ve [+] [-] butonları ile
/// kademeli veya sürekli olarak kısıp yükseltmeyi sağlar.
/// </summary>
public class AudioSettingsUI : MonoBehaviour
{
    public static AudioSettingsUI Instance { get; private set; }

    [Header("UI Elemanları")]
    public GameObject soundButtonObj;
    public GameObject settingsPanelObj;

    private Slider _musicSlider;
    private Slider _sfxSlider;
    private TextMeshProUGUI _musicValTMP;
    private TextMeshProUGUI _sfxValTMP;
    private TextMeshProUGUI _muteBtnTMP;

    private bool _isPanelOpen = false;

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
        // Ana menü açıkken oyun içi ses butonunu gizle, sadece savaşta sağ üstte göster
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

        // 1. Sağ Üst Köşe Taktik Ses Ayar Butonu ([SES])
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

                RectTransform rt = soundButtonObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f); // Sağ Üst Köşe
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-20f, -20f);
                rt.sizeDelta = new Vector2(65f, 36f);

                Image btnBg = soundButtonObj.AddComponent<Image>();
                btnBg.color = new Color(0.08f, 0.16f, 0.25f, 0.90f);

                Outline outline = soundButtonObj.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0.85f, 1f, 0.7f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                Button btn = soundButtonObj.AddComponent<Button>();
                btn.targetGraphic = btnBg;
                btn.onClick.AddListener(ToggleSettingsPanel);

                // Buton Yazısı: [SES]
                GameObject iconObj = new GameObject("IconText");
                iconObj.transform.SetParent(soundButtonObj.transform, false);
                RectTransform iconRt = iconObj.AddComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;

                TextMeshProUGUI btnTMP = iconObj.AddComponent<TextMeshProUGUI>();
                btnTMP.text = "SES";
                btnTMP.fontSize = 15f;
                btnTMP.fontStyle = FontStyles.Bold;
                btnTMP.alignment = TextAlignmentOptions.Center;
                btnTMP.color = new Color(0.2f, 0.95f, 1f);
                btnTMP.raycastTarget = false;
            }
        }

        // 2. Ses Ayarları Açılır Paneli
        if (settingsPanelObj == null)
        {
            Transform existingPanel = transform.Find("AudioSettingsPanel");
            if (existingPanel != null)
            {
                settingsPanelObj = existingPanel.gameObject;
            }
            else
            {
                BuildSettingsPanel();
            }
        }
    }

    private void BuildSettingsPanel()
    {
        settingsPanelObj = new GameObject("AudioSettingsPanel");
        settingsPanelObj.transform.SetParent(transform, false);

        RectTransform panelRt = settingsPanelObj.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(460f, 340f);

        Image bg = settingsPanelObj.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.14f, 0.97f);

        Outline outline = settingsPanelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Başlık
        GameObject titleObj = new GameObject("PanelTitle");
        titleObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.90f);
        titleRt.anchorMax = new Vector2(0.5f, 0.90f);
        titleRt.sizeDelta = new Vector2(420f, 40f);

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "SES VE MUZIK AYARLARI";
        titleTMP.fontSize = 20f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(1f, 0.85f, 0.2f); // Altın Sarısı

        // Kapatma Butonu (Sağ Üst X)
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform closeRt = closeBtnObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-12f, -12f);
        closeRt.sizeDelta = new Vector2(35f, 35f);

        Image closeBg = closeBtnObj.AddComponent<Image>();
        closeBg.color = new Color(0.7f, 0.2f, 0.2f, 0.9f);

        Button closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.targetGraphic = closeBg;
        closeBtn.onClick.AddListener(() => SetPanelOpen(false));

        GameObject closeTxtObj = new GameObject("CloseText");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform closeTxtRt = closeTxtObj.AddComponent<RectTransform>();
        closeTxtRt.anchorMin = Vector2.zero;
        closeTxtRt.anchorMax = Vector2.one;
        closeTxtRt.offsetMin = Vector2.zero;
        closeTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI closeTMP = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTMP.text = "X";
        closeTMP.fontSize = 18f;
        closeTMP.fontStyle = FontStyles.Bold;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeTMP.color = Color.white;
        closeTMP.raycastTarget = false;

        // 1. Savaş Müziği Satırı: [-]  Slider  [+]  ve Yüzde Metni
        _musicSlider = CreateVolumeRow("SAVAS MUZIGI", 0.68f, isMusic: true, out _musicValTMP);

        // 2. Mermi / Efekt Sesi Satırı: [-]  Slider  [+]  ve Yüzde Metni
        _sfxSlider = CreateVolumeRow("MERMI VE PATLAMA", 0.44f, isMusic: false, out _sfxValTMP);

        // 3. Mute & Tamam Butonları
        GameObject muteBtnObj = new GameObject("MuteButton");
        muteBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform muteRt = muteBtnObj.AddComponent<RectTransform>();
        muteRt.anchorMin = new Vector2(0.5f, 0.16f);
        muteRt.anchorMax = new Vector2(0.5f, 0.16f);
        muteRt.sizeDelta = new Vector2(240f, 38f);

        Image muteBg = muteBtnObj.AddComponent<Image>();
        muteBg.color = new Color(0.12f, 0.25f, 0.38f, 0.95f);

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
        _muteBtnTMP.fontSize = 13f;
        _muteBtnTMP.fontStyle = FontStyles.Bold;
        _muteBtnTMP.alignment = TextAlignmentOptions.Center;
        _muteBtnTMP.color = Color.white;
        _muteBtnTMP.raycastTarget = false;

        settingsPanelObj.SetActive(false);
    }

    private Slider CreateVolumeRow(string label, float yAnchor, bool isMusic, out TextMeshProUGUI valueTMP)
    {
        // Satır Başlığı & Yüzde Göstergesi
        GameObject headerObj = new GameObject(label + "_Header");
        headerObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform hRt = headerObj.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.10f, yAnchor + 0.08f);
        hRt.anchorMax = new Vector2(0.90f, yAnchor + 0.08f);
        hRt.sizeDelta = new Vector2(0f, 25f);

        TextMeshProUGUI titleTMP = headerObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = label;
        titleTMP.fontSize = 14f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(0.2f, 0.9f, 1f);
        titleTMP.alignment = TextAlignmentOptions.Left;

        // Yüzde Metni (%70)
        GameObject valObj = new GameObject(label + "_ValText");
        valObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform valRt = valObj.AddComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.65f, yAnchor + 0.08f);
        valRt.anchorMax = new Vector2(0.90f, yAnchor + 0.08f);
        valRt.sizeDelta = new Vector2(0f, 25f);

        valueTMP = valObj.AddComponent<TextMeshProUGUI>();
        valueTMP.fontSize = 14f;
        valueTMP.fontStyle = FontStyles.Bold;
        valueTMP.color = Color.white;
        valueTMP.alignment = TextAlignmentOptions.Right;

        // [-] Butonu (Sesi Kıs)
        GameObject minusBtnObj = new GameObject(label + "_MinusBtn");
        minusBtnObj.transform.SetParent(settingsPanelObj.transform, false);
        RectTransform minusRt = minusBtnObj.AddComponent<RectTransform>();
        minusRt.anchorMin = new Vector2(0.10f, yAnchor);
        minusRt.anchorMax = new Vector2(0.10f, yAnchor);
        minusRt.pivot = new Vector2(0f, 0.5f);
        minusRt.sizeDelta = new Vector2(38f, 32f);

        Image minusBg = minusBtnObj.AddComponent<Image>();
        minusBg.color = new Color(0.15f, 0.22f, 0.32f);

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
        plusRt.anchorMin = new Vector2(0.90f, yAnchor);
        plusRt.anchorMax = new Vector2(0.90f, yAnchor);
        plusRt.pivot = new Vector2(1f, 0.5f);
        plusRt.sizeDelta = new Vector2(38f, 32f);

        Image plusBg = plusBtnObj.AddComponent<Image>();
        plusBg.color = new Color(0.15f, 0.22f, 0.32f);

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
        sRt.sizeDelta = new Vector2(250f, 22f);

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

    public void ToggleSettingsPanel()
    {
        SetPanelOpen(!_isPanelOpen);
    }

    public void SetPanelOpen(bool open)
    {
        _isPanelOpen = open;
        if (settingsPanelObj != null)
        {
            settingsPanelObj.SetActive(_isPanelOpen);
        }
        if (_isPanelOpen)
        {
            UpdateUIValues();
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
