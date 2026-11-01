using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Uçak Savaşı Temalı Giriş Ekranı Yöneticisi:
/// - Askeri havacılık temalı başlık (AIR BATTLE 2D) ve rütbeler.
/// - Menü açıkken oyunu duraklatır (Time.timeScale = 0).
/// - Solo veya Online/2 Oyunculu modunu kusursuzca sahneye aktarır.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject mainPanel;        // Ana Menü
    public GameObject modesPanel;       // Mod Seçimi
    public GameObject settingsPanel;    // Ayarlar (Zorluk)

    [Header("Arka Plan")]
    public Image backgroundImage;       // Savaş uçağı temalı arka plan

    [Header("Ana Menü Butonları")]
    public Button playButton;           // HAREKATA BAŞLA
    public Button modesButton;          // MODLAR
    public Button settingsButton;       // AYARLAR

    [Header("Modlar Paneli")]
    public Button soloButton;
    public Button onlineButton;
    public Button backFromModesButton;
    public TextMeshProUGUI selectedModeTMP;

    [Header("Ayarlar Paneli (Rütbeler)")]
    public Button easyButton;           // Acemi Pilot
    public Button normalButton;         // Yüzbaşı
    public Button hardButton;           // As Pilot
    public Button backFromSettingsButton;
    public TextMeshProUGUI selectedDifficultyTMP;

    [Header("Sahne")]
    public bool loadSceneOnPlay = false;
    public string gameSceneName = "SampleScene";
    public GameObject inGameUI;

    private GameObject _titleObject;

    private void Awake()
    {
        Time.timeScale = 0f;
    }

    private void Start()
    {
        GameSettings.LoadSettings();
        AudioManager.EnsureInstance();

        TryLoadBackgroundArt();
        SetupAviationHeader();
        FormatMilitaryButtons();

        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (modesButton != null) modesButton.onClick.AddListener(OpenModesPanel);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettingsPanel);

        if (soloButton != null) soloButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.Solo));
        if (onlineButton != null) onlineButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.Online));
        if (backFromModesButton != null) backFromModesButton.onClick.AddListener(OpenMainPanel);

        if (easyButton != null) easyButton.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Kolay));
        if (normalButton != null) normalButton.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Normal));
        if (hardButton != null) hardButton.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Zor));
        if (backFromSettingsButton != null) backFromSettingsButton.onClick.AddListener(OpenMainPanel);

        OpenMainPanel();
        UpdateUIFeedback();
        SetTouchControlsVisibility(false, false);
    }

    private void TryLoadBackgroundArt()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (backgroundImage != null)
        {
            if (backgroundImage.sprite != null && backgroundImage.sprite.name != "Background")
            {
                backgroundImage.color = Color.white;
                return;
            }
        }
    }

    private void SetupAviationHeader()
    {
        Transform existingTitle = transform.Find("AirBattleTitle");
        if (existingTitle != null)
        {
            _titleObject = existingTitle.gameObject;
            return;
        }

        _titleObject = new GameObject("AirBattleTitle");
        _titleObject.transform.SetParent(transform, false);

        RectTransform rt = _titleObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 130f);
        rt.anchoredPosition = new Vector2(0f, 210f);

        TextMeshProUGUI tmp = _titleObject.AddComponent<TextMeshProUGUI>();
        tmp.text = "AIR BATTLE 2D\n<size=40%><color=#F1C40F>- IT DALASI HAREKATI -</color></size>";
        tmp.fontSize = 46f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.95f, 1f);
    }

    private void FormatMilitaryButtons()
    {
        FormatBtn(playButton, 60f, "HAREKATA BAŞLA", new Color(0.18f, 0.55f, 0.34f), 320f, 65f);
        FormatBtn(modesButton, -20f, "OYUN MODLARI", new Color(0.15f, 0.40f, 0.65f), 320f, 60f);
        FormatBtn(settingsButton, -95f, "UÇUŞ VE SES AYARLARI", new Color(0.30f, 0.35f, 0.42f), 320f, 60f);

        FormatBtn(soloButton, 55f, "SOLO: 20 UÇAKLIK SEFER", new Color(0.15f, 0.50f, 0.70f), 390f, 60f);
        FormatBtn(onlineButton, -25f, "ONLINE: 2 OYUNCULU DÜELLO", new Color(0.20f, 0.30f, 0.45f), 390f, 60f);
        FormatBtn(backFromModesButton, -110f, "GERİ DÖN", new Color(0.35f, 0.35f, 0.35f), 260f, 50f);

        FormatBtn(easyButton, 120f, "ACEMİ PİLOT (Kolay)", new Color(0.18f, 0.55f, 0.34f), 350f, 44f);
        FormatBtn(normalButton, 70f, "YÜZBAŞI (Normal)", new Color(0.70f, 0.55f, 0.15f), 350f, 44f);
        FormatBtn(hardButton, 20f, "AS PİLOT (Zor)", new Color(0.75f, 0.20f, 0.20f), 350f, 44f);
        FormatBtn(backFromSettingsButton, -140f, "GERİ DÖN", new Color(0.35f, 0.35f, 0.35f), 260f, 44f);
    }

    private void FormatBtn(Button btn, float posY, string text, Color color, float width, float height)
    {
        if (btn == null) return;

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, posY);
        }

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = color;
        }

        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
            tmp.fontSize = 20f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
        }
    }

    public void OpenMainPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (_titleObject != null) _titleObject.SetActive(true);
    }

    public void OpenModesPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        UpdateUIFeedback();
    }

    public void OpenSettingsPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            SetupMenuSoundControls();
        }
        UpdateUIFeedback();
    }

    private GameObject _soundControlsContainer;

    private void SetupMenuSoundControls()
    {
        if (settingsPanel == null) return;
        if (_soundControlsContainer != null) return;

        Transform existing = settingsPanel.transform.Find("MenuSoundControls");
        if (existing != null)
        {
            _soundControlsContainer = existing.gameObject;
            return;
        }

        _soundControlsContainer = new GameObject("MenuSoundControls");
        _soundControlsContainer.transform.SetParent(settingsPanel.transform, false);

        RectTransform rootRt = _soundControlsContainer.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(380f, 120f);
        rootRt.anchoredPosition = new Vector2(0f, -55f);

        // Müzik Satırı (Y: 20f)
        CreateMenuVolumeRow(_soundControlsContainer.transform, "SAVAŞ MÜZİĞİ", 20f, isMusic: true);

        // Efekt Satırı (Y: -35f)
        CreateMenuVolumeRow(_soundControlsContainer.transform, "MERMİ & EFEKT", -35f, isMusic: false);
    }

    private void CreateMenuVolumeRow(Transform parent, string title, float localY, bool isMusic)
    {
        GameObject row = new GameObject("Row_" + (isMusic ? "Music" : "SFX"));
        row.transform.SetParent(parent, false);

        RectTransform rRt = row.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.5f, 0.5f);
        rRt.anchorMax = new Vector2(0.5f, 0.5f);
        rRt.sizeDelta = new Vector2(360f, 45f);
        rRt.anchoredPosition = new Vector2(0f, localY);

        // Başlık TMP
        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(row.transform, false);
        RectTransform lblRt = lblObj.AddComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0f, 0.55f);
        lblRt.anchorMax = new Vector2(0.6f, 0.55f);
        lblRt.pivot = new Vector2(0f, 0.5f);
        lblRt.sizeDelta = new Vector2(0f, 20f);

        TextMeshProUGUI lblTMP = lblObj.AddComponent<TextMeshProUGUI>();
        lblTMP.text = title;
        lblTMP.fontSize = 12f;
        lblTMP.fontStyle = FontStyles.Bold;
        lblTMP.color = new Color(0.2f, 0.9f, 1f);

        // Yüzde Metni
        GameObject valObj = new GameObject("ValText");
        valObj.transform.SetParent(row.transform, false);
        RectTransform valRt = valObj.AddComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.6f, 0.55f);
        valRt.anchorMax = new Vector2(1f, 0.55f);
        valRt.pivot = new Vector2(1f, 0.5f);
        valRt.sizeDelta = new Vector2(0f, 20f);

        TextMeshProUGUI valTMP = valObj.AddComponent<TextMeshProUGUI>();
        valTMP.fontSize = 12f;
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.alignment = TextAlignmentOptions.Right;
        valTMP.color = Color.white;

        // [-] Butonu
        GameObject minusObj = new GameObject("MinusBtn");
        minusObj.transform.SetParent(row.transform, false);
        RectTransform mRt = minusObj.AddComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0f, 0.1f);
        mRt.anchorMax = new Vector2(0f, 0.1f);
        mRt.pivot = new Vector2(0f, 0.5f);
        mRt.sizeDelta = new Vector2(32f, 24f);

        Image mImg = minusObj.AddComponent<Image>();
        mImg.color = new Color(0.18f, 0.25f, 0.35f);
        Button mBtn = minusObj.AddComponent<Button>();

        GameObject mTxtObj = new GameObject("Text");
        mTxtObj.transform.SetParent(minusObj.transform, false);
        RectTransform mTxtRt = mTxtObj.AddComponent<RectTransform>();
        mTxtRt.anchorMin = Vector2.zero;
        mTxtRt.anchorMax = Vector2.one;
        mTxtRt.offsetMin = Vector2.zero;
        mTxtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI mTMP = mTxtObj.AddComponent<TextMeshProUGUI>();
        mTMP.text = "-";
        mTMP.fontSize = 20f;
        mTMP.fontStyle = FontStyles.Bold;
        mTMP.alignment = TextAlignmentOptions.Center;
        mTMP.color = Color.white;

        // [+] Butonu
        GameObject plusObj = new GameObject("PlusBtn");
        plusObj.transform.SetParent(row.transform, false);
        RectTransform pRt = plusObj.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(1f, 0.1f);
        pRt.anchorMax = new Vector2(1f, 0.1f);
        pRt.pivot = new Vector2(1f, 0.5f);
        pRt.sizeDelta = new Vector2(32f, 24f);

        Image pImg = plusObj.AddComponent<Image>();
        pImg.color = new Color(0.18f, 0.25f, 0.35f);
        Button pBtn = plusObj.AddComponent<Button>();

        GameObject pTxtObj = new GameObject("Text");
        pTxtObj.transform.SetParent(plusObj.transform, false);
        RectTransform pTxtRt = pTxtObj.AddComponent<RectTransform>();
        pTxtRt.anchorMin = Vector2.zero;
        pTxtRt.anchorMax = Vector2.one;
        pTxtRt.offsetMin = Vector2.zero;
        pTxtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI pTMP = pTxtObj.AddComponent<TextMeshProUGUI>();
        pTMP.text = "+";
        pTMP.fontSize = 18f;
        pTMP.fontStyle = FontStyles.Bold;
        pTMP.alignment = TextAlignmentOptions.Center;
        pTMP.color = Color.white;

        // Slider
        GameObject sObj = new GameObject("Slider");
        sObj.transform.SetParent(row.transform, false);
        RectTransform sRt = sObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0.1f);
        sRt.anchorMax = new Vector2(0.5f, 0.1f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.sizeDelta = new Vector2(240f, 18f);

        Slider s = sObj.AddComponent<Slider>();
        s.minValue = 0f;
        s.maxValue = 1f;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sObj.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.15f, 0.22f);

        GameObject faObj = new GameObject("Fill Area");
        faObj.transform.SetParent(sObj.transform, false);
        RectTransform faRt = faObj.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero;
        faRt.offsetMax = Vector2.zero;

        GameObject fObj = new GameObject("Fill");
        fObj.transform.SetParent(faObj.transform, false);
        RectTransform fRt = fObj.AddComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        Image fImg = fObj.AddComponent<Image>();
        fImg.color = new Color(0f, 0.85f, 1f);

        s.fillRect = fRt;
        s.targetGraphic = fImg;

        float initVal = isMusic
            ? (AudioManager.Instance != null ? AudioManager.Instance.musicVolume : 0.70f)
            : (AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0.85f);

        s.value = initVal;
        valTMP.text = $"%{(int)(initVal * 100)}";

        s.onValueChanged.AddListener((val) =>
        {
            if (isMusic)
                AudioManager.Instance?.SetMusicVolume(val);
            else
                AudioManager.Instance?.SetSFXVolume(val);

            valTMP.text = $"%{(int)(val * 100)}";
        });

        mBtn.onClick.AddListener(() =>
        {
            s.value = Mathf.Clamp01(s.value - 0.10f);
            if (!isMusic) AudioManager.Instance?.PlayGunfire(false);
        });

        pBtn.onClick.AddListener(() =>
        {
            s.value = Mathf.Clamp01(s.value + 0.10f);
            if (!isMusic) AudioManager.Instance?.PlayGunfire(false);
        });
    }

    public void SetMode(GameSettings.GameMode mode)
    {
        GameSettings.SetMode(mode);
        UpdateUIFeedback();
    }

    public void SetDifficulty(GameSettings.Difficulty diff)
    {
        GameSettings.SetDifficulty(diff);
        UpdateUIFeedback();

        var ai = FindObjectOfType<AIController>();
        ai?.ApplyDifficultySettings();
    }

    private void UpdateUIFeedback()
    {
        Highlight(easyButton, GameSettings.SelectedDifficulty == GameSettings.Difficulty.Kolay);
        Highlight(normalButton, GameSettings.SelectedDifficulty == GameSettings.Difficulty.Normal);
        Highlight(hardButton, GameSettings.SelectedDifficulty == GameSettings.Difficulty.Zor);

        Highlight(soloButton, GameSettings.SelectedMode == GameSettings.GameMode.Solo);
        Highlight(onlineButton, GameSettings.SelectedMode == GameSettings.GameMode.Online);
    }

    private void Highlight(Button btn, bool isSelected)
    {
        if (btn == null) return;
        var outline = btn.GetComponent<Outline>();
        if (isSelected)
        {
            if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.yellow;
            outline.effectDistance = new Vector2(3f, -3f);
        }
        else if (outline != null)
        {
            Destroy(outline);
        }
    }

    public void OnPlayClicked()
    {
        Time.timeScale = 1f;

        // Menüyü kapat ve savaşı başlat
        gameObject.SetActive(false);

        if (inGameUI != null) inGameUI.SetActive(true);

        // MODU KESİN OLARAK UYGULA:
        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);

        var planes = FindObjectsOfType<PlaneController>();
        foreach (var p in planes)
        {
            if (!p.isPlayer1)
            {
                var ai = p.GetComponent<AIController>();
                if (ai != null)
                {
                    // Online seçildiyse AI KESİNLİKLE KAPALI, Solo ise açık!
                    ai.enabled = !isOnline;
                    if (!isOnline) ai.ApplyDifficultySettings();
                }
            }
        }

        if (GameManager.Instance != null)
            GameManager.Instance.ApplyGameMode();

        // Kendi uçağımızın joystick & ateş butonu açık, düşman uçağının kontrolleri sadece İkili Online modda açık
        SetTouchControlsVisibility(showP1Controls: true, showEnemyControls: isOnline);

        var audioUI = FindObjectOfType<AudioSettingsUI>();
        if (audioUI != null && audioUI.soundButtonObj != null)
        {
            audioUI.soundButtonObj.SetActive(true);
        }

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateHealthBars();
    }

    /// <summary>
    /// Ekrandaki dokunmatik kontrollerin (Joystick ve Ateş Butonları) görünürlüğünü yönetir:
    /// - 1. Oyuncunun (Kendi uçağımızın) Joystick ve Ateş Butonu her zaman aktiftir.
    /// - Düşman uçağının (2. Oyuncunun) kontrolleri YALNIZCA İkili Online modda açılır; Yapay Zeka (Solo) modunda kaldırılır.
    /// </summary>
    public static void SetTouchControlsVisibility(bool showP1Controls, bool showEnemyControls)
    {
        // 1. Düşman Kontrolleri (P2 Joystick & P2 Ateş Butonu)
        var p2Router = FindObjectOfType<TwoPlayerInputRouter>();
        if (p2Router != null && p2Router.p2ControlsRoot != null)
        {
            p2Router.p2ControlsRoot.SetActive(showEnemyControls);
        }

        // 2. Sanal Joystick'ler (Kendi uçağımız P1, düşman P2)
        var joysticks = FindObjectsOfType<VirtualJoystick>(true);
        foreach (var j in joysticks)
        {
            if (j != null)
            {
                if (j.isPlayer1)
                    j.gameObject.SetActive(showP1Controls);
                else
                    j.gameObject.SetActive(showEnemyControls);
            }
        }

        // 3. Ateş Butonları (Kendi uçağımız P1, düşman P2)
        var fireButtons = FindObjectsOfType<FireButton>(true);
        foreach (var fb in fireButtons)
        {
            if (fb != null)
            {
                if (fb.isPlayer1)
                    fb.gameObject.SetActive(showP1Controls);
                else
                    fb.gameObject.SetActive(showEnemyControls);
            }
        }
    }
}
