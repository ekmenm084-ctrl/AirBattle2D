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
        tmp.text = "✈️ AIR BATTLE 2D ✈️\n<size=40%><color=#F1C40F>★ İT DALAŞI HAREKATI ★</color></size>";
        tmp.fontSize = 46f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.95f, 1f);
    }

    private void FormatMilitaryButtons()
    {
        FormatBtn(playButton, 60f, "✈️ HAREKATA BAŞLA", new Color(0.18f, 0.55f, 0.34f), 320f, 65f);
        FormatBtn(modesButton, -20f, "🎯 OYUN MODLARI", new Color(0.15f, 0.40f, 0.65f), 320f, 60f);
        FormatBtn(settingsButton, -95f, "⚙️ UÇUŞ AYARLARI", new Color(0.30f, 0.35f, 0.42f), 320f, 60f);

        FormatBtn(soloButton, 55f, "👤 SOLO: 20 UÇAKLIK SEFER (BOSS)", new Color(0.15f, 0.50f, 0.70f), 390f, 60f);
        FormatBtn(onlineButton, -25f, "🌐 ONLINE: 2 OYUNCULU DÜELLO", new Color(0.20f, 0.30f, 0.45f), 390f, 60f);
        FormatBtn(backFromModesButton, -110f, "← GERİ DÖN", new Color(0.35f, 0.35f, 0.35f), 260f, 50f);

        FormatBtn(easyButton, 85f, "🟢 ACEMİ PİLOT (Kolay)", new Color(0.18f, 0.55f, 0.34f), 360f, 55f);
        FormatBtn(normalButton, 20f, "🟡 YÜZBAŞI (Normal)", new Color(0.70f, 0.55f, 0.15f), 360f, 55f);
        FormatBtn(hardButton, -45f, "🔴 AS PİLOT (Zor)", new Color(0.75f, 0.20f, 0.20f), 360f, 55f);
        FormatBtn(backFromSettingsButton, -125f, "← GERİ DÖN", new Color(0.35f, 0.35f, 0.35f), 260f, 50f);
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
        if (settingsPanel != null) settingsPanel.SetActive(true);
        UpdateUIFeedback();
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

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.UpdateHealthBars();
    }
}
