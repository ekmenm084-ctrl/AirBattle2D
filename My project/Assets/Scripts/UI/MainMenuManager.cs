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

    private Button _campaign1Button;
    private Button _campaign2Button;
    private Button _campaign3Button;
    private Button _endlessButton;
    private Button _bossRushButton;
    private Button _modesDiffEasyBtn;
    private Button _modesDiffNormalBtn;
    private Button _modesDiffHardBtn;
    private TextMeshProUGUI _briefingTMP;

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
    private Button _hangarButton;
    private Button _quickDiffButton;

    private void Awake()
    {
        Time.timeScale = 0f;
    }

    private void Update()
    {
        // Menü aktifken oyun içi dokunmatik kontrollerin ve sinematik alt yazıların ekrana sızmasını kesin olarak engelle
        SetTouchControlsVisibility(false, false);
        StoryCampaignManager.EnsureInstance()?.HideCinematicIfMenuOpen();
    }

    private void Start()
    {
        GameSettings.LoadSettings();
        AudioManager.EnsureInstance();
        StoryCampaignManager.EnsureInstance()?.HideCinematicIfMenuOpen();

        TryLoadBackgroundArt();
        SetupAviationHeader();
        FormatMilitaryButtons();
        SetupCampaignSelectionUI();

        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }
        if (modesButton != null)
        {
            modesButton.onClick.RemoveAllListeners();
            modesButton.onClick.AddListener(OpenModesPanel);
        }
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettingsPanel);
        }

        if (backFromModesButton != null)
        {
            backFromModesButton.onClick.RemoveAllListeners();
            backFromModesButton.onClick.AddListener(OpenMainPanel);
        }

        if (easyButton != null)
        {
            easyButton.onClick.RemoveAllListeners();
            easyButton.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Kolay));
        }
        if (normalButton != null)
        {
            normalButton.onClick.RemoveAllListeners();
            normalButton.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Normal));
        }
        if (hardButton != null)
        {
            hardButton.onClick.RemoveAllListeners();
            hardButton.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Zor));
        }
        if (backFromSettingsButton != null)
        {
            backFromSettingsButton.onClick.RemoveAllListeners();
            backFromSettingsButton.onClick.AddListener(OpenMainPanel);
        }

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
        rt.sizeDelta = new Vector2(850f, 150f);
        rt.anchoredPosition = new Vector2(0f, 225f);

        TextMeshProUGUI tmp = _titleObject.AddComponent<TextMeshProUGUI>();
        tmp.text = "AIR BATTLE 2D\n<size=42%><color=#F1C40F>- İT DALAŞI HAREKATI -</color></size>";
        tmp.fontSize = 58f; // 46f -> 58f (+12f)
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.95f, 1f);
        tmp.extraPadding = true;
        tmp.outlineWidth = 0.22f;
        tmp.outlineColor = Color.black;
    }

    private void FormatMilitaryButtons()
    {
        FormatBtn(playButton, 115f, "HAREKATA BAŞLA", new Color(0.18f, 0.55f, 0.34f), 500f, 70f);

        _hangarButton = CreateOrFindMainButton("HangarButton", 46f, "🛩️ HANGAR & ATÖLYE", new Color(0.72f, 0.48f, 0.12f), 500f, 58f);
        if (_hangarButton != null)
        {
            _hangarButton.onClick.RemoveAllListeners();
            _hangarButton.onClick.AddListener(OpenHangarPanel);
        }

        _quickDiffButton = CreateOrFindMainButton("QuickDiffButton", -20f, "⚡ ZORLUK: [ NORMAL ]", new Color(0.20f, 0.28f, 0.38f), 500f, 58f);
        if (_quickDiffButton != null)
        {
            _quickDiffButton.onClick.RemoveAllListeners();
            _quickDiffButton.onClick.AddListener(CycleDifficulty);
        }

        FormatBtn(modesButton, -86f, "OYUN MODLARI & BÖLÜMLER", new Color(0.15f, 0.40f, 0.65f), 500f, 58f);
        FormatBtn(settingsButton, -152f, "UÇUŞ VE SES AYARLARI", new Color(0.30f, 0.35f, 0.42f), 500f, 58f);

        // Ayarlar Paneli Zorluk Butonları (Kolay, Normal, Zor)
        FormatBtn(easyButton, 135f, "ACEMİ PİLOT (Kolay)", new Color(0.18f, 0.55f, 0.34f), 480f, 52f);
        FormatBtn(normalButton, 75f, "YÜZBAŞI (Normal)", new Color(0.70f, 0.55f, 0.15f), 480f, 52f);
        FormatBtn(hardButton, 15f, "AS PİLOT (Zor)", new Color(0.75f, 0.20f, 0.20f), 480f, 52f);
        FormatBtn(backFromSettingsButton, -210f, "GERİ DÖN", new Color(0.35f, 0.35f, 0.35f), 300f, 46f);
    }

    private void CycleDifficulty()
    {
        GameSettings.Difficulty next = GameSettings.SelectedDifficulty switch
        {
            GameSettings.Difficulty.Kolay => GameSettings.Difficulty.Normal,
            GameSettings.Difficulty.Normal => GameSettings.Difficulty.Zor,
            _ => GameSettings.Difficulty.Kolay
        };
        SetDifficulty(next);
        AudioManager.Instance?.PlayGunfire(false);
    }

    private Button CreateOrFindMainButton(string objName, float posY, string text, Color color, float width, float height)
    {
        if (mainPanel == null) return null;
        Transform existing = mainPanel.transform.Find(objName);
        GameObject btnObj = existing != null ? existing.gameObject : null;

        if (btnObj == null)
        {
            btnObj = new GameObject(objName);
            btnObj.transform.SetParent(mainPanel.transform, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            btnObj.AddComponent<Image>();
            btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            textObj.AddComponent<TextMeshProUGUI>();
        }

        Button btn = btnObj.GetComponent<Button>();
        FormatBtn(btn, posY, text, color, width, height);
        return btn;
    }

    public void OpenHangarPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (_titleObject != null) _titleObject.SetActive(false);
        HangarUI.EnsureUI()?.OpenHangar();
    }

    private void FormatBtn(Button btn, float posY, string text, Color color, float width, float height, float posX = 0f)
    {
        if (btn == null) return;

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(posX, posY);
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
            tmp.fontSize = 25f; // 21f -> 25f (+4f orantılı büyüme)
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.extraPadding = true;
            tmp.outlineWidth = 0.20f;
            tmp.outlineColor = Color.black;
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
        rootRt.sizeDelta = new Vector2(540f, 175f); // Geniş ve ferah
        rootRt.anchoredPosition = new Vector2(0f, -75f);

        // Müzik Satırı (Y: 34f)
        CreateMenuVolumeRow(_soundControlsContainer.transform, "SAVAŞ MÜZİĞİ", 34f, isMusic: true);

        // Efekt Satırı (Y: -44f)
        CreateMenuVolumeRow(_soundControlsContainer.transform, "MERMİ & EFEKT", -44f, isMusic: false);
    }

    private void SetupCampaignSelectionUI()
    {
        if (modesPanel == null) return;

        // 1. Kampanya 1 (Operasyon Kara Şafak)
        if (soloButton != null)
        {
            _campaign1Button = soloButton;
            FormatBtn(_campaign1Button, 180f, "BÖLÜM 1: OPERASYON KARA ŞAFAK", new Color(0.12f, 0.45f, 0.28f), 540f, 48f);
            _campaign1Button.onClick.RemoveAllListeners();
            _campaign1Button.onClick.AddListener(() => SetCampaign(StoryCampaignManager.CampaignType.OperationBlackDawn));
        }

        // 2. Kampanya 2: Proje Kimeras
        _campaign2Button = CreateOrFindModesButton("Campaign2Btn", 128f, "BÖLÜM 2: PROJE KİMERAS", new Color(0.15f, 0.35f, 0.60f), 540f, 48f);
        _campaign2Button.onClick.RemoveAllListeners();
        _campaign2Button.onClick.AddListener(() => SetCampaign(StoryCampaignManager.CampaignType.ProjectChimera));

        // 3. Kampanya 3: Gökyüzü Paralı Askeri
        _campaign3Button = CreateOrFindModesButton("Campaign3Btn", 76f, "BÖLÜM 3: GÖKYÜZÜ PARALI ASKERİ", new Color(0.55f, 0.22f, 0.15f), 540f, 48f);
        _campaign3Button.onClick.RemoveAllListeners();
        _campaign3Button.onClick.AddListener(() => SetCampaign(StoryCampaignManager.CampaignType.SkyMercenary));

        // 4. Sonsuz İt Dalaşı (Endless Survival Mode)
        _endlessButton = CreateOrFindModesButton("EndlessModeBtn", 24f, "♾️ SONSUZ İT DALAŞI", new Color(0.16f, 0.44f, 0.52f), 264f, 46f, posX: -138f);
        _endlessButton.onClick.RemoveAllListeners();
        _endlessButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.Endless));

        // 5. Boss Rush (4 Boss Avı)
        _bossRushButton = CreateOrFindModesButton("BossRushBtn", 24f, "💀 BOSS RUSH (4 AS)", new Color(0.60f, 0.20f, 0.18f), 264f, 46f, posX: 138f);
        _bossRushButton.onClick.RemoveAllListeners();
        _bossRushButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.BossRush));

        // 6. Online 2 Oyunculu Düello
        if (onlineButton != null)
        {
            FormatBtn(onlineButton, -26f, "ONLINE: 2 OYUNCULU DÜELLO", new Color(0.28f, 0.20f, 0.45f), 540f, 44f);
            onlineButton.onClick.RemoveAllListeners();
            onlineButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.Online));
        }

        // 7. Modlar Panelinde Doğrudan Zorluk Seçim Butonları
        SetupModesDifficultyRow();

        // 8. Taktik Sefer Brifing Kartı
        SetupCampaignBriefingBox();

        // 9. Geri Dön Butonu
        if (backFromModesButton != null)
        {
            FormatBtn(backFromModesButton, -232f, "GERİ DÖN", new Color(0.35f, 0.35f, 0.35f), 300f, 44f);
        }
    }

    private void SetupModesDifficultyRow()
    {
        _modesDiffEasyBtn = CreateOrFindModesButton("ModesDiffEasyBtn", -74f, "KOLAY", new Color(0.18f, 0.55f, 0.34f), 174f, 42f, posX: -182f);
        _modesDiffEasyBtn.onClick.RemoveAllListeners();
        _modesDiffEasyBtn.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Kolay));

        _modesDiffNormalBtn = CreateOrFindModesButton("ModesDiffNormalBtn", -74f, "NORMAL", new Color(0.70f, 0.55f, 0.15f), 174f, 42f, posX: 0f);
        _modesDiffNormalBtn.onClick.RemoveAllListeners();
        _modesDiffNormalBtn.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Normal));

        _modesDiffHardBtn = CreateOrFindModesButton("ModesDiffHardBtn", -74f, "ZOR", new Color(0.75f, 0.20f, 0.20f), 174f, 42f, posX: 182f);
        _modesDiffHardBtn.onClick.RemoveAllListeners();
        _modesDiffHardBtn.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Zor));
    }

    private Button CreateOrFindModesButton(string objName, float posY, string text, Color color, float width, float height, float posX = 0f)
    {
        Transform existing = modesPanel.transform.Find(objName);
        GameObject btnObj = existing != null ? existing.gameObject : null;

        if (btnObj == null)
        {
            btnObj = new GameObject(objName);
            btnObj.transform.SetParent(modesPanel.transform, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            btnObj.AddComponent<Image>();
            btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            textObj.AddComponent<TextMeshProUGUI>();
        }

        Button btn = btnObj.GetComponent<Button>();
        FormatBtn(btn, posY, text, color, width, height, posX);
        return btn;
    }

    private void SetupCampaignBriefingBox()
    {
        Transform existing = modesPanel.transform.Find("CampaignBriefingBox");
        GameObject boxObj = existing != null ? existing.gameObject : new GameObject("CampaignBriefingBox");
        boxObj.transform.SetParent(modesPanel.transform, false);

        RectTransform rt = boxObj.GetComponent<RectTransform>();
        if (rt == null) rt = boxObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -145f);
        rt.sizeDelta = new Vector2(540f, 95f); // Butonlarla orantılı

        Image bg = boxObj.GetComponent<Image>();
        if (bg == null) bg = boxObj.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.07f, 0.12f, 0.96f);

        Outline outline = boxObj.GetComponent<Outline>();
        if (outline == null) outline = boxObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        Transform textChild = boxObj.transform.Find("BriefingText");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("BriefingText");
        textObj.transform.SetParent(boxObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        if (textRt == null) textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(14f, 6f);
        textRt.offsetMax = new Vector2(-14f, -6f);

        _briefingTMP = textObj.GetComponent<TextMeshProUGUI>();
        if (_briefingTMP == null) _briefingTMP = textObj.AddComponent<TextMeshProUGUI>();
        _briefingTMP.enableAutoSizing = true;
        _briefingTMP.fontSizeMin = 13f;
        _briefingTMP.fontSizeMax = 20f;
        _briefingTMP.fontSize = 19f;
        _briefingTMP.fontStyle = FontStyles.Bold;
        _briefingTMP.alignment = TextAlignmentOptions.Center;
        _briefingTMP.color = Color.white;
        _briefingTMP.lineSpacing = 2.5f;
        _briefingTMP.textWrappingMode = TextWrappingModes.Normal;
        _briefingTMP.overflowMode = TextOverflowModes.Truncate;
        _briefingTMP.extraPadding = true;
        _briefingTMP.outlineWidth = 0.20f;
        _briefingTMP.outlineColor = Color.black;
        _briefingTMP.raycastTarget = false;
    }

    public void SetCampaign(StoryCampaignManager.CampaignType campaign)
    {
        StoryCampaignManager.CurrentCampaign = campaign;
        GameSettings.SetMode(GameSettings.GameMode.Solo);
        UpdateUIFeedback();
    }

    private void CreateMenuVolumeRow(Transform parent, string title, float localY, bool isMusic)
    {
        GameObject row = new GameObject("Row_" + (isMusic ? "Music" : "SFX"));
        row.transform.SetParent(parent, false);

        RectTransform rRt = row.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.5f, 0.5f);
        rRt.anchorMax = new Vector2(0.5f, 0.5f);
        rRt.sizeDelta = new Vector2(500f, 64f);
        rRt.anchoredPosition = new Vector2(0f, localY);

        // Başlık TMP
        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(row.transform, false);
        RectTransform lblRt = lblObj.AddComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0f, 0.55f);
        lblRt.anchorMax = new Vector2(0.6f, 0.55f);
        lblRt.pivot = new Vector2(0f, 0.5f);
        lblRt.sizeDelta = new Vector2(0f, 30f);

        TextMeshProUGUI lblTMP = lblObj.AddComponent<TextMeshProUGUI>();
        lblTMP.text = title;
        lblTMP.fontSize = 22f; // Büyütüldü
        lblTMP.fontStyle = FontStyles.Bold;
        lblTMP.color = new Color(0.2f, 0.9f, 1f);
        lblTMP.extraPadding = true;

        // Yüzde Metni
        GameObject valObj = new GameObject("ValText");
        valObj.transform.SetParent(row.transform, false);
        RectTransform valRt = valObj.AddComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.6f, 0.55f);
        valRt.anchorMax = new Vector2(1f, 0.55f);
        valRt.pivot = new Vector2(1f, 0.5f);
        valRt.sizeDelta = new Vector2(0f, 30f);

        TextMeshProUGUI valTMP = valObj.AddComponent<TextMeshProUGUI>();
        valTMP.fontSize = 22f; // Büyütüldü
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.alignment = TextAlignmentOptions.Right;
        valTMP.color = Color.white;
        valTMP.extraPadding = true;

        // [-] Butonu
        GameObject minusObj = new GameObject("MinusBtn");
        minusObj.transform.SetParent(row.transform, false);
        RectTransform mRt = minusObj.AddComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0f, 0.1f);
        mRt.anchorMax = new Vector2(0f, 0.1f);
        mRt.pivot = new Vector2(0f, 0.5f);
        mRt.sizeDelta = new Vector2(52f, 38f);

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
        mTMP.fontSize = 28f;
        mTMP.fontStyle = FontStyles.Bold;
        mTMP.alignment = TextAlignmentOptions.Center;
        mTMP.color = Color.white;
        mTMP.extraPadding = true;

        // [+] Butonu
        GameObject plusObj = new GameObject("PlusBtn");
        plusObj.transform.SetParent(row.transform, false);
        RectTransform pRt = plusObj.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(1f, 0.1f);
        pRt.anchorMax = new Vector2(1f, 0.1f);
        pRt.pivot = new Vector2(1f, 0.5f);
        pRt.sizeDelta = new Vector2(52f, 38f);

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
        pTMP.fontSize = 26f;
        pTMP.fontStyle = FontStyles.Bold;
        pTMP.alignment = TextAlignmentOptions.Center;
        pTMP.color = Color.white;
        pTMP.extraPadding = true;

        // Slider
        GameObject sObj = new GameObject("Slider");
        sObj.transform.SetParent(row.transform, false);
        RectTransform sRt = sObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0.1f);
        sRt.anchorMax = new Vector2(0.5f, 0.1f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.sizeDelta = new Vector2(330f, 26f);

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

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.SetHUDVisibility(true);
        gameUI?.UpdateHealthBars();
    }

    private void UpdateUIFeedback()
    {
        var diff = GameSettings.SelectedDifficulty;
        string diffHex = GameSettings.GetDifficultyColorHex(diff);
        string diffName = GameSettings.GetDifficultyName(diff);

        // 1. Ayarlar Panelindeki Butonların Canlı ve Net Durumu
        UpdateDifficultyButtonVisual(easyButton, "ACEMİ PİLOT (Kolay)", new Color(0.18f, 0.55f, 0.34f), diff == GameSettings.Difficulty.Kolay);
        UpdateDifficultyButtonVisual(normalButton, "YÜZBAŞI (Normal)", new Color(0.70f, 0.55f, 0.15f), diff == GameSettings.Difficulty.Normal);
        UpdateDifficultyButtonVisual(hardButton, "AS PİLOT (Zor)", new Color(0.75f, 0.20f, 0.20f), diff == GameSettings.Difficulty.Zor);

        // 2. Modlar Panelindeki Zorluk Seçim Butonları
        UpdateDifficultyButtonVisual(_modesDiffEasyBtn, "KOLAY", new Color(0.18f, 0.55f, 0.34f), diff == GameSettings.Difficulty.Kolay);
        UpdateDifficultyButtonVisual(_modesDiffNormalBtn, "NORMAL", new Color(0.70f, 0.55f, 0.15f), diff == GameSettings.Difficulty.Normal);
        UpdateDifficultyButtonVisual(_modesDiffHardBtn, "ZOR", new Color(0.75f, 0.20f, 0.20f), diff == GameSettings.Difficulty.Zor);

        // 3. Modlar Panelindeki Mod Butonlarının Seçim Vurgusu
        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);
        bool isEndless = (GameSettings.SelectedMode == GameSettings.GameMode.Endless);
        bool isBossRush = (GameSettings.SelectedMode == GameSettings.GameMode.BossRush);
        bool isSolo = (GameSettings.SelectedMode == GameSettings.GameMode.Solo);
        var activeCamp = StoryCampaignManager.CurrentCampaign;

        Highlight(_campaign1Button, isSolo && activeCamp == StoryCampaignManager.CampaignType.OperationBlackDawn);
        Highlight(_campaign2Button, isSolo && activeCamp == StoryCampaignManager.CampaignType.ProjectChimera);
        Highlight(_campaign3Button, isSolo && activeCamp == StoryCampaignManager.CampaignType.SkyMercenary);
        Highlight(_endlessButton, isEndless);
        Highlight(_bossRushButton, isBossRush);
        Highlight(onlineButton, isOnline);

        // 4. Modlar Paneli Brifing Metni
        UpdateBriefingText();

        // 5. Ana Menü Hızlı Zorluk Değiştirme Butonu
        if (_quickDiffButton != null)
        {
            TextMeshProUGUI qTmp = _quickDiffButton.GetComponentInChildren<TextMeshProUGUI>();
            if (qTmp != null)
            {
                qTmp.text = $"⚡ ZORLUK: <color={diffHex}><b>[ {diffName.ToUpper()} ]</b></color> <size=68%>(Dokun Değiştir)</size>";
            }
            Image qImg = _quickDiffButton.GetComponent<Image>();
            if (qImg != null)
            {
                qImg.color = diff switch
                {
                    GameSettings.Difficulty.Kolay => new Color(0.12f, 0.35f, 0.22f),
                    GameSettings.Difficulty.Zor   => new Color(0.48f, 0.14f, 0.14f),
                    _                             => new Color(0.42f, 0.32f, 0.10f)
                };
            }
        }

        // 6. Ana Menüdeki "HAREKATA BAŞLA" Butonu Başlığı
        if (playButton != null)
        {
            TextMeshProUGUI playTMP = playButton.GetComponentInChildren<TextMeshProUGUI>();
            if (playTMP != null)
            {
                if (isOnline)
                {
                    playTMP.text = "HAREKATA BAŞLA\n<size=62%><color=#D1C4E9>[ONLINE: 2 OYUNCULU DÜELLO]</color></size>";
                }
                else if (isEndless)
                {
                    int best = PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 1);
                    playTMP.text = $"HAREKATA BAŞLA\n<size=62%><color=#4FC3F7>[SONSUZ İT DALAŞI (Rekor: {best})]</color> • <color={diffHex}><b>[ZORLUK: {diffName}]</b></color></size>";
                }
                else if (isBossRush)
                {
                    playTMP.text = $"HAREKATA BAŞLA\n<size=62%><color=#FF8A80>[BOSS RUSH: 4 AS]</color> • <color={diffHex}><b>[ZORLUK: {diffName}]</b></color></size>";
                }
                else
                {
                    playTMP.text = $"HAREKATA BAŞLA\n<size=62%><color=#FFE082>[{StoryCampaignManager.GetCampaignName(activeCamp)}]</color> • <color={diffHex}><b>[ZORLUK: {diffName}]</b></color></size>";
                }
            }
        }
    }

    private void UpdateDifficultyButtonVisual(Button btn, string baseTitle, Color activeColor, bool isSelected)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        var outline = btn.GetComponent<Outline>();

        if (isSelected)
        {
            if (img != null) img.color = activeColor;
            if (tmp != null)
            {
                tmp.text = $"✓ [SEÇİLİ] {baseTitle}";
                tmp.color = Color.white;
            }
            if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.yellow;
            outline.effectDistance = new Vector2(3f, -3f);
        }
        else
        {
            if (img != null) img.color = new Color(activeColor.r * 0.40f, activeColor.g * 0.40f, activeColor.b * 0.40f, 0.70f);
            if (tmp != null)
            {
                tmp.text = baseTitle;
                tmp.color = new Color(0.80f, 0.80f, 0.80f, 0.85f);
            }
            if (outline != null) Destroy(outline);
        }
    }

    private void UpdateBriefingText()
    {
        if (_briefingTMP == null) return;

        var diff = GameSettings.SelectedDifficulty;
        string diffStats = diff switch
        {
            GameSettings.Difficulty.Kolay => "<color=#2ECC71><b>Zorluk:</b> KOLAY (Düşman Canı: %70, Seyrek Ateş)</color>",
            GameSettings.Difficulty.Zor   => "<color=#E74C3C><b>Zorluk:</b> ZOR (Düşman Canı: %145, Seri Ateş, Çevik Takip)</color>",
            _                             => "<color=#F1C40F><b>Zorluk:</b> NORMAL (Dengeli Düşman Gücü)</color>"
        };

        if (GameSettings.SelectedMode == GameSettings.GameMode.Online)
        {
            _briefingTMP.text = "<color=#B388FF><b>[2 OYUNCULU TAKTİK DÜELLO]</b></color>\n" +
                                "Aynı cihazda iki pilot karşı karşıya!\n" +
                                "<size=90%><color=#A0C4DF>1. Pilot (Alt ekran) vs 2. Pilot (Üst ekran 180° ters)</color></size>";
        }
        else if (GameSettings.SelectedMode == GameSettings.GameMode.Endless)
        {
            int best = PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 1);
            _briefingTMP.text = $"<color=#4FC3F7><b>[SONSUZ İT DALAŞI]</b></color> • En Yüksek Rekor: <b>Dalga {best}</b>\n" +
                                "Düşman filoları durmaksızın gelir! Her 5 dalgada bir Boss savaşı!\n" +
                                $"{diffStats}";
        }
        else if (GameSettings.SelectedMode == GameSettings.GameMode.BossRush)
        {
            _briefingTMP.text = "<color=#FF8A80><b>[BOSS RUSH - 4 AS HAREKATI]</b></color> • Ödül: <b>+1500 Altın</b>\n" +
                                "Sıradan uçaklar yok! 4 dev Boss uçağı peş peşe saldırır!\n" +
                                $"{diffStats}";
        }
        else
        {
            var camp = StoryCampaignManager.CurrentCampaign;
            string slogan = StoryCampaignManager.GetCampaignSlogan(camp);
            string missionInfo = "";

            switch (camp)
            {
                case StoryCampaignManager.CampaignType.OperationBlackDawn:
                    missionInfo = "<color=#2ECC71><b>Hedef:</b> Vatan Savunması (20 Dalga & 4 Boss Filosu)</color>";
                    break;
                case StoryCampaignManager.CampaignType.ProjectChimera:
                    missionInfo = "<color=#3498DB><b>Hedef:</b> Siber Harp & AI İsyanı (20 Dalga & 4 Boss Filosu)</color>";
                    break;
                case StoryCampaignManager.CampaignType.SkyMercenary:
                    missionInfo = "<color=#E67E22><b>Hedef:</b> Korsan Kartel Avı (20 Dalga & 4 Boss Filosu)</color>";
                    break;
            }

            _briefingTMP.text = $"<color=#F1C40F><b>{slogan}</b></color> • {diffStats}\n{missionInfo}";
        }
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
            if (p.isPlayer1)
            {
                // Hangar ve Kalıcı Geliştirme Özelliklerini Oyuncuya Uygula
                HangarManager.EnsureInstance()?.ApplyActiveAircraftStats(p);
            }
            else
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
        gameUI?.SetHUDVisibility(true);
        gameUI?.UpdateHealthBars();

        // Başlangıçta aktif zorluğu ekranda büyük ve net bir duyuruyla bildir!
        if (!isOnline)
        {
            string diffAlert = GameSettings.SelectedDifficulty switch
            {
                GameSettings.Difficulty.Kolay => "<color=#2ECC71>KOLAY MOD DEVREDE</color>\nAcemi düşman filoları hafif zırhlı.",
                GameSettings.Difficulty.Zor   => "<color=#E74C3C>DİKKAT: ZOR MOD DEVREDE!</color>\nDüşman filoları maksimum can ve agresif saldırıda!",
                _                             => "<color=#F1C40F>NORMAL MOD DEVREDE</color>\nStandart dengeli hava muharebesi."
            };
            Color alertColor = GameSettings.SelectedDifficulty switch
            {
                GameSettings.Difficulty.Kolay => new Color(0.2f, 0.85f, 0.4f),
                GameSettings.Difficulty.Zor   => new Color(1f, 0.25f, 0.25f),
                _                             => new Color(0.95f, 0.8f, 0.2f)
            };
            gameUI?.ShowWaveAlert(diffAlert, alertColor, 2.2f);
        }
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

        // 4. Füze Butonu (P1 Özel Yeteneği)
        var missileBtns = FindObjectsOfType<MissileLauncherButton>(true);
        foreach (var mb in missileBtns)
        {
            if (mb != null) mb.gameObject.SetActive(showP1Controls);
        }
    }
}
