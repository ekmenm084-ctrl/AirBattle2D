using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Uçak Savaşı Temalı Giriş Ekranı Yöneticisi:
/// - Askeri havacılık temalı başlık (AIR BATTLE 2D) ve rütbeler.
/// - Menü açıkken oyunu duraklatır (Time.timeScale = 0).
/// - Solo veya Online/2 Oyunculu modunu kusursuzca sahneye aktarır.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance { get; private set; }
    public static bool AutoPlayOnSceneLoad = false;
    public static bool OpenHangarOnSceneLoad = false;

    // ==========================================
    // ASKERİ TAKTİK HUD SPRITE & FONT YÖNETİMİ
    // ==========================================
    private static TMP_FontAsset _cachedTacticalFont;
    public static TMP_FontAsset GetTacticalFont()
    {
        if (_cachedTacticalFont != null) return _cachedTacticalFont;
        _cachedTacticalFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF")
            ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF")
            ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF")
            ?? TMP_Settings.defaultFontAsset;
        return _cachedTacticalFont;
    }

    private static readonly Dictionary<string, Sprite> _hudSpriteCache = new Dictionary<string, Sprite>();

    public static Sprite GetHudSprite(string spriteName, Color borderColor, Color glassColor, bool hasLeftBar = true)
    {
        if (!string.IsNullOrEmpty(spriteName) && _hudSpriteCache.TryGetValue(spriteName, out var cached) && cached != null)
            return cached;

        if (!string.IsNullOrEmpty(spriteName))
        {
            Sprite loaded = Resources.Load<Sprite>("UI/TacticalHUD/" + spriteName);
            if (loaded != null)
            {
                _hudSpriteCache[spriteName] = loaded;
                return loaded;
            }
        }

        // Procedural 9-slice chamfered HUD sprite fallback
        string key = $"proc_{borderColor.r:F2}_{borderColor.g:F2}_{borderColor.b:F2}_{hasLeftBar}";
        if (_hudSpriteCache.TryGetValue(key, out var procCached) && procCached != null)
            return procCached;

        Sprite proc = CreateProceduralHudSprite(borderColor, glassColor, hasLeftBar);
        _hudSpriteCache[key] = proc;
        if (!string.IsNullOrEmpty(spriteName)) _hudSpriteCache[spriteName] = proc;
        return proc;
    }

    private static Sprite CreateProceduralHudSprite(Color borderColor, Color glassColor, bool hasLeftBar)
    {
        int size = 64;
        int cut = 12;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isCut = (x + y < cut)
                    || ((size - 1 - x) + y < cut)
                    || (x + (size - 1 - y) < cut)
                    || ((size - 1 - x) + (size - 1 - y) < cut);

                if (isCut)
                {
                    tex.SetPixel(x, y, clear);
                    continue;
                }

                bool isBorder = (x == 0 || x == size - 1 || y == 0 || y == size - 1)
                    || (x + y == cut) || ((size - 1 - x) + y == cut)
                    || (x + (size - 1 - y) == cut) || ((size - 1 - x) + (size - 1 - y) == cut);

                if (isBorder)
                {
                    tex.SetPixel(x, y, borderColor);
                }
                else
                {
                    if (hasLeftBar && x >= 4 && x <= 6 && y >= cut + 2 && y <= size - cut - 3)
                    {
                        tex.SetPixel(x, y, new Color(borderColor.r, borderColor.g, borderColor.b, 0.95f));
                    }
                    else if (y == 2 && x >= cut + 2 && x <= size - cut - 3)
                    {
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.45f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, glassColor);
                    }
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
    }

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
    private Button _bulletHellButton;
    private Button _timeAttackButton;
    private Button _hardcoreButton;
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
    private Button _militaryStoreButton;
    private Button _cratesButton;
    private Button _leaderboardButton;
    private Button _modesLeaderboardButton;
    private Button _quickDiffButton;
    private Button _dailyOpsButton;

    public static void MakeFullScreenRect(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    public static void SetOutlineSafe(TextMeshProUGUI tmp, float width, Color color)
    {
        if (tmp == null) return;
        // TextMeshProUGUI.SetOutlineThickness accesses m_canvasRenderer & m_sharedMaterial,
        // which remain null until Awake/OnEnable runs on an active hierarchy.
        if (!tmp.gameObject.activeInHierarchy || tmp.canvasRenderer == null || tmp.fontSharedMaterial == null)
            return;
        tmp.outlineWidth = width;
        tmp.outlineColor = color;
    }

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 0f;

        // Başlangıçta panellerin durumunu kesin olarak eşitle
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    private void Update()
    {
        // Menü aktifken oyun içi dokunmatik kontrollerin ve sinematik alt yazıların ekrana sızmasını kesin olarak engelle
        SetTouchControlsVisibility(false, false);
        StoryCampaignManager.EnsureInstance()?.HideCinematicIfMenuOpen();
    }

    private void Start()
    {
        try { GameSettings.LoadSettings(); } catch (System.Exception e) { Debug.LogError(e); }
        try { AudioManager.EnsureInstance(); } catch (System.Exception e) { Debug.LogError(e); }
        try { StoryCampaignManager.EnsureInstance()?.HideCinematicIfMenuOpen(); } catch (System.Exception e) { Debug.LogError(e); }

        MakeFullScreenRect(mainPanel?.GetComponent<RectTransform>());
        MakeFullScreenRect(modesPanel?.GetComponent<RectTransform>());
        MakeFullScreenRect(settingsPanel?.GetComponent<RectTransform>());

        // mainPanel arka plan görseli varsa şeffaf yap, böylece canlı fırtına arka planını örtmez
        if (mainPanel != null)
        {
            Image mpImg = mainPanel.GetComponent<Image>();
            if (mpImg != null)
            {
                mpImg.color = new Color(0f, 0f, 0f, 0f);
                mpImg.raycastTarget = false;
            }
        }

        // TMP bileşenlerinin Awake/CanvasRenderer kurulumunu tamamlaması için alt panelleri inşa süresince aktif tut
        bool hadModes = modesPanel != null && modesPanel.activeSelf;
        bool hadSettings = settingsPanel != null && settingsPanel.activeSelf;
        if (modesPanel != null) modesPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(true);

        try { TryLoadBackgroundArt(); } catch (System.Exception e) { Debug.LogError(e); }
        try { SetupAviationHeader(); } catch (System.Exception e) { Debug.LogError(e); }
        try { FormatMilitaryButtons(); } catch (System.Exception e) { Debug.LogError(e); }
        try { SetupMainPanelQuitButton(); } catch (System.Exception e) { Debug.LogError(e); }
        try { SetupMainPanelDailyOpsButton(); } catch (System.Exception e) { Debug.LogError(e); }
        try { SetupModesHeader(); } catch (System.Exception e) { Debug.LogError(e); }
        try { SetupSettingsHeader(); } catch (System.Exception e) { Debug.LogError(e); }
        try { SetupCampaignSelectionUI(); } catch (System.Exception e) { Debug.LogError(e); }
        try { SetupMenuSoundControls(); } catch (System.Exception e) { Debug.LogError(e); }

        try { PlayerProfileUI.EnsureUI()?.SetupMainMenuProfileBadge(mainPanel != null ? mainPanel.transform : transform); } catch (System.Exception e) { Debug.LogError(e); }

        if (playButton == null && mainPanel != null)
        {
            Transform pBtn = mainPanel.transform.Find("PlayButon") ?? mainPanel.transform.Find("playButton") ?? mainPanel.transform.Find("PlayButton");
            if (pBtn != null) playButton = pBtn.GetComponent<Button>();
        }

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

        UpdateUIFeedback();
        if (!hadModes && modesPanel != null) modesPanel.SetActive(false);
        if (!hadSettings && settingsPanel != null) settingsPanel.SetActive(false);
        OpenMainPanel();
        SetTouchControlsVisibility(false, false);

        // İlk kez giriş yapan oyuncuya pilot çağrı kodu / isim belirleme ekranını göster
        try { PilotLoginUI.EnsureUI()?.CheckFirstTimeLogin(); } catch (System.Exception e) { Debug.LogError(e); }

        // Oyun sonu ekranından "Hangar'a Git" veya "Yeniden Oyna" seçilmişse sahne yüklenince otomatik aç
        if (OpenHangarOnSceneLoad)
        {
            OpenHangarOnSceneLoad = false;
            OpenHangarPanel();
        }
        else if (AutoPlayOnSceneLoad)
        {
            AutoPlayOnSceneLoad = false;
            OnPlayClicked();
        }
    }

    private void TryLoadBackgroundArt()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        // Arka plan için MenuRoot altında izole ve daima en arkada (Index 0) duracak konteyner nesnesi
        Transform existingContainer = transform.Find("MenuDynamicBackground_Root");
        GameObject bgObj = existingContainer != null ? existingContainer.gameObject : new GameObject("MenuDynamicBackground_Root", typeof(RectTransform));
        bgObj.transform.SetParent(transform, false);
        bgObj.transform.SetAsFirstSibling(); // Kesinlikle en arkada kalsın

        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        if (bgRt != null) MakeFullScreenRect(bgRt);

        DynamicStormBackground.AttachTo(bgObj, backgroundImage != null ? backgroundImage.sprite : null);

        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0f, 0f, 0f, 0f);
            backgroundImage.raycastTarget = false;
        }

        // Ana Menü paneli ve Başlık nesnesinin arka planın önünde yer almasını mutlak sağla
        if (mainPanel != null) mainPanel.transform.SetAsLastSibling();
        if (_titleObject != null) _titleObject.transform.SetAsLastSibling();
    }

    private void SetupAviationHeader()
    {
        Transform existingTitle = transform.Find("AirBattleTitle");
        GameObject titleObj = existingTitle != null ? existingTitle.gameObject : new GameObject("AirBattleTitle", typeof(RectTransform));
        titleObj.transform.SetParent(transform, false);

        RectTransform rt = titleObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(880f, 130f);
            rt.anchoredPosition = new Vector2(0f, 275f);
        }

        Image bgImg = titleObj.GetComponent<Image>() ?? titleObj.AddComponent<Image>();
        bgImg.sprite = GetHudSprite("hud_header_visor", new Color(0f, 0.90f, 1f, 0.85f), new Color(0.02f, 0.05f, 0.10f, 0.88f), hasLeftBar: false);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        Transform textChild = titleObj.transform.Find("TitleText");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("TitleText", typeof(RectTransform));
        textObj.transform.SetParent(titleObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        if (textRt != null)
        {
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 4f);
            textRt.offsetMax = new Vector2(-10f, -4f);
        }

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>() ?? textObj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = "<size=36%><color=#80D8FF>« TACTICAL FLIGHT SIMULATION • DEFCON-1 »</color></size>\n<b><color=#00E5FF>AIR</color> BATTLE <color=#FFD700>2D</color></b>\n<size=38%><color=#90CAF9>« ASKERİ HAVA HARP HAREKÂTI // İT DALAŞI »</color></size>";
        tmp.fontSize = 52f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 3.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.extraPadding = true;
        SetOutlineSafe(tmp, 0.12f, new Color(0.01f, 0.03f, 0.06f, 0.95f));
        tmp.raycastTarget = false;

        _titleObject = titleObj;
        _titleObject.transform.SetAsLastSibling();
    }

    private void FormatMilitaryButtons()
    {
        if (playButton == null && mainPanel != null)
        {
            Transform pBtn = mainPanel.transform.Find("PlayButon") ?? mainPanel.transform.Find("playButton") ?? mainPanel.transform.Find("PlayButton");
            if (pBtn != null) playButton = pBtn.GetComponent<Button>();
        }

        // 1. Ana Harekât Butonu (Büyük, Taktik Cyan)
        FormatBtn(playButton, 160f, "SYS // OPS-01  » HAREKÂTI BAŞLAT", new Color(0f, 0.95f, 0.65f), 560f, 66f, 0f, "hud_btn_cyan");

        if (playButton != null)
        {
            playButton.interactable = true;
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
            playButton.transform.SetAsLastSibling();

            Image pImg = playButton.GetComponent<Image>();
            if (pImg != null) pImg.raycastTarget = true;
        }

        // 2. Askeri Hangar & Donatım
        _hangarButton = CreateOrFindMainButton("HangarButton", 100f, "FLEET // AIR-02  [+] ASKERİ HANGAR VE FİLO", new Color(0.28f, 0.62f, 1.0f), 560f, 50f, "hud_btn_blue");
        if (_hangarButton != null)
        {
            _hangarButton.onClick.RemoveAllListeners();
            _hangarButton.onClick.AddListener(OpenHangarPanel);
        }

        // 3. Askeri Mağaza & Donatım
        _militaryStoreButton = CreateOrFindMainButton("MilitaryStoreButton", 42f, "ARMORY // LOG-03  [+] ASKERİ MAĞAZA VE DONATIM", new Color(0.12f, 0.85f, 0.92f), 560f, 50f, "hud_btn_teal");
        if (_militaryStoreButton != null)
        {
            _militaryStoreButton.onClick.RemoveAllListeners();
            _militaryStoreButton.onClick.AddListener(OpenStorePanel);
        }

        // 4. İkmal ve Ganimet Sandıkları
        _cratesButton = CreateOrFindMainButton("CratesButton", -16f, "SUPPLY // CRATE-04  [+] ASKERİ İKMAL SANDIKLARI", new Color(1.0f, 0.80f, 0.22f), 560f, 50f, "hud_btn_amber");
        if (_cratesButton != null)
        {
            _cratesButton.onClick.RemoveAllListeners();
            _cratesButton.onClick.AddListener(OpenCratesPanel);
        }

        // 5. Küresel As Pilot Tablosu
        _leaderboardButton = CreateOrFindMainButton("LeaderboardButton", -74f, "INTEL // RANK-05  [#] KÜRESEL AS PİLOT TABLOSU", new Color(0.96f, 0.65f, 0.20f), 560f, 50f, "hud_btn_orange");
        if (_leaderboardButton != null)
        {
            _leaderboardButton.onClick.RemoveAllListeners();
            _leaderboardButton.onClick.AddListener(() => OpenLeaderboardPanel());
        }

        // 6. Hızlı Zorluk Seçimi
        _quickDiffButton = CreateOrFindMainButton("QuickDiffButton", -132f, "PROTOCOL // DIFF-06  ZORLUK: [ NORMAL ]", new Color(0.40f, 0.55f, 0.72f), 560f, 50f, "hud_btn_amber");
        if (_quickDiffButton != null)
        {
            _quickDiffButton.onClick.RemoveAllListeners();
            _quickDiffButton.onClick.AddListener(CycleDifficulty);
        }

        // 7. Oyun Modları ve Seferler
        FormatBtn(modesButton, -190f, "SECTOR // OPS-07  [»] OYUN MODLARI VE SEFERLER", new Color(0.30f, 0.58f, 0.88f), 560f, 50f, 0f, "hud_btn_blue");

        // 8. Sistem ve Kokpit Ayarları
        FormatBtn(settingsButton, -248f, "AVIONICS // CFG-08  [*] KOKPİT VE SİSTEM AYARLARI", new Color(0.50f, 0.60f, 0.72f), 560f, 50f, 0f, "hud_btn_slate");

        // Ayarlar Paneli Zorluk Butonları (Kolay, Normal, Zor)
        FormatBtn(easyButton, 135f, "ACEMİ PİLOT (Kolay)", new Color(0.18f, 0.85f, 0.45f), 500f, 50f, 0f, "hud_btn_emerald");
        FormatBtn(normalButton, 75f, "YÜZBAŞI (Normal)", new Color(1.0f, 0.75f, 0.20f), 500f, 50f, 0f, "hud_btn_amber");
        FormatBtn(hardButton, 15f, "AS PİLOT (Zor)", new Color(0.92f, 0.25f, 0.25f), 500f, 50f, 0f, "hud_btn_red");

        if (backFromSettingsButton == null && settingsPanel != null)
        {
            Transform existingBack = settingsPanel.transform.Find("BackFromSettingsButon") ?? settingsPanel.transform.Find("BackFromSettingsButton") ?? settingsPanel.transform.Find("BackButton");
            if (existingBack != null) backFromSettingsButton = existingBack.GetComponent<Button>();
            if (backFromSettingsButton == null)
            {
                GameObject bObj = new GameObject("BackFromSettingsButton", typeof(RectTransform));
                bObj.transform.SetParent(settingsPanel.transform, false);
                backFromSettingsButton = bObj.AddComponent<Button>();
            }
        }
        if (backFromSettingsButton != null)
        {
            FormatBottomLeftBackBtn(backFromSettingsButton, OpenMainPanel);
        }
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

    private Button CreateOrFindMainButton(string objName, float posY, string text, Color color, float width, float height, string spriteName = null)
    {
        if (mainPanel == null) return null;
        Transform existing = mainPanel.transform.Find(objName);
        GameObject btnObj = existing != null ? existing.gameObject : null;

        if (btnObj == null)
        {
            btnObj = new GameObject(objName, typeof(RectTransform));
            btnObj.transform.SetParent(mainPanel.transform, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            btnObj.AddComponent<Image>();
            btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            textObj.AddComponent<TextMeshProUGUI>();
        }

        Button btn = btnObj.GetComponent<Button>();
        FormatBtn(btn, posY, text, color, width, height, 0f, spriteName);
        return btn;
    }

    public void OpenHangarPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (_titleObject != null) _titleObject.SetActive(false);
        if (backgroundImage != null) backgroundImage.enabled = true;

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: false);

        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

        HangarUI.EnsureUI()?.OpenHangar();
    }

    public void OpenStorePanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (_titleObject != null) _titleObject.SetActive(false);
        if (backgroundImage != null) backgroundImage.enabled = true;

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: false);

        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

        MilitaryStoreUI.EnsureUI()?.OpenStore();
    }

    public void OpenCratesPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (_titleObject != null) _titleObject.SetActive(false);
        if (backgroundImage != null) backgroundImage.enabled = true;

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: false);

        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

        CratesUI.EnsureUI()?.OpenCratesPanel(() => OpenMainPanel());
    }

    public void OpenLeaderboardPanel(LeaderboardManager.BoardMode mode = LeaderboardManager.BoardMode.Endless)
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (_titleObject != null) _titleObject.SetActive(false);
        if (backgroundImage != null) backgroundImage.enabled = true;

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: false);

        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

        LeaderboardUI.EnsureUI()?.OpenLeaderboard(mode, () => OpenMainPanel());
    }

    public void OpenDailyOpsPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (_titleObject != null) _titleObject.SetActive(false);
        if (backgroundImage != null) backgroundImage.enabled = true;

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: true);

        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();

        DailyOpsUI.EnsureUI()?.OpenOpsPanel(() => OpenMainPanel());
    }

    public static void FormatBtn(Button btn, float posY, string text, Color accentBorderColor, float width, float height, float posX = 0f, string hudSpriteName = null)
    {
        if (btn == null) return;

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(posX, posY);
        }

        if (string.IsNullOrEmpty(hudSpriteName))
        {
            if (accentBorderColor.r > 0.7f && accentBorderColor.g < 0.35f) hudSpriteName = "hud_btn_red";
            else if (accentBorderColor.g > 0.7f && accentBorderColor.r < 0.35f) hudSpriteName = "hud_btn_emerald";
            else if (accentBorderColor.r > 0.7f && accentBorderColor.g > 0.55f && accentBorderColor.b < 0.35f) hudSpriteName = "hud_btn_amber";
            else if (accentBorderColor.b > 0.7f && accentBorderColor.r < 0.35f) hudSpriteName = "hud_btn_blue";
            else hudSpriteName = "hud_btn_cyan";
        }

        Image img = btn.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
        img.sprite = GetHudSprite(hudSpriteName, accentBorderColor, new Color(0.03f, 0.06f, 0.11f, 0.92f), hasLeftBar: true);
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;
        btn.targetGraphic = img;
        btn.interactable = true;

        // Eski Unity Outline bileşenini kaldır (45° açılı kesik köşeleri dikdörtgenle kapatmasın)
        Outline outline = btn.GetComponent<Outline>();
        if (outline != null) Destroy(outline);

        // Aviyonik buton etkileşim renkleri (Hover ve basışta aviyonik parıltı)
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        cb.pressedColor = new Color(0.70f, 0.85f, 1.0f, 1f);
        cb.selectedColor = Color.white;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.06f;
        btn.colors = cb;

        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null)
        {
            GameObject txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btn.transform, false);
            RectTransform trt = txtObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            tmp = txtObj.AddComponent<TextMeshProUGUI>();
        }

        tmp.font = GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = (height >= 58f) ? 22f : 18f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 13f;
        tmp.fontSizeMax = (height >= 58f) ? 22f : 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 2.0f; // Askeri HUD havacılık yazı aralığı
        tmp.color = new Color(0.95f, 0.98f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.extraPadding = true;
        SetOutlineSafe(tmp, 0.08f, new Color(0.01f, 0.03f, 0.06f, 0.95f));
        tmp.raycastTarget = false;
    }

    public static void FormatBottomLeftBackBtn(Button btn, UnityEngine.Events.UnityAction onClick = null)
    {
        if (btn == null) return;
        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(40f, 25f);
            rt.sizeDelta = new Vector2(260f, 50f);
        }

        Image img = btn.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
        btn.targetGraphic = img;
        img.raycastTarget = true;
        btn.interactable = true;
        btn.transform.SetAsLastSibling();

        FormatBtn(btn, 25f, "« TACTICAL // GERİ DÖN", new Color(0.35f, 0.65f, 0.95f), 260f, 50f, 40f, "hud_btn_blue");

        if (onClick != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(onClick);
        }
        btn.transform.SetAsLastSibling();
    }

    private void SetupMainPanelQuitButton()
    {
        if (mainPanel == null) return;
        Transform existing = mainPanel.transform.Find("QuitButton");
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject("QuitButton", typeof(RectTransform));
        btnObj.transform.SetParent(mainPanel.transform, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(40f, 25f);
            rt.sizeDelta = new Vector2(260f, 50f);
        }
        btnObj.transform.SetAsLastSibling();

        Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
        FormatBtn(btn, 25f, "[X] ABORT // ÜSSE DÖN", new Color(0.95f, 0.20f, 0.25f), 260f, 50f, 40f, "hud_btn_red");

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        });
    }

    private void SetupMainPanelDailyOpsButton()
    {
        if (mainPanel == null) return;
        Transform existing = mainPanel.transform.Find("DailyOpsButton");
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject("DailyOpsButton", typeof(RectTransform));
        btnObj.transform.SetParent(mainPanel.transform, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-40f, 25f);
            rt.sizeDelta = new Vector2(390f, 54f);
        }
        btnObj.transform.SetAsLastSibling();

        _dailyOpsButton = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
        _dailyOpsButton.onClick.RemoveAllListeners();
        _dailyOpsButton.onClick.AddListener(OpenDailyOpsPanel);

        RefreshDailyOpsButtonVisual();
    }

    private void RefreshDailyOpsButtonVisual()
    {
        if (_dailyOpsButton == null) return;

        DailyOpsManager ops = DailyOpsManager.EnsureInstance();
        int claimable = ops != null ? ops.GetTotalClaimableCount() : 0;

        string label = claimable > 0
            ? $"OPS // GÖREV & MADALYA [{claimable} ÖDÜL!]"
            : "OPS // GÖREV & MADALYA";
        Color accent = claimable > 0
            ? new Color(1.0f, 0.78f, 0.18f)
            : new Color(0.14f, 0.85f, 0.80f);
        string spriteKey = claimable > 0 ? "hud_btn_ops_ready" : "hud_btn_ops_idle";

        FormatBtn(_dailyOpsButton, 25f, label, accent, 390f, 54f, -40f, spriteKey);
    }

    private void SetupModesHeader()
    {
        if (modesPanel == null) return;
        Transform existing = modesPanel.transform.Find("ModesHeaderTitle");
        GameObject titleObj = existing != null ? existing.gameObject : new GameObject("ModesHeaderTitle", typeof(RectTransform));
        titleObj.transform.SetParent(modesPanel.transform, false);

        RectTransform rt = titleObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -22f);
            rt.sizeDelta = new Vector2(880f, 74f);
        }

        Image bgImg = titleObj.GetComponent<Image>() ?? titleObj.AddComponent<Image>();
        bgImg.sprite = GetHudSprite("hud_header_visor", new Color(0f, 0.90f, 1f, 0.85f), new Color(0.02f, 0.05f, 0.10f, 0.88f), hasLeftBar: false);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        Transform textChild = titleObj.transform.Find("TitleText");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("TitleText", typeof(RectTransform));
        textObj.transform.SetParent(titleObj.transform, false);
        RectTransform tRt = textObj.GetComponent<RectTransform>();
        if (tRt != null)
        {
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(10f, 4f);
            tRt.offsetMax = new Vector2(-10f, -4f);
        }

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>() ?? textObj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = "<b><color=#00E5FF>HAVA HAREKÂTI</color> VE GÖREV SEFERLERİ</b>\n<size=52%><color=#94A3B8>Taktik senaryo operasyonları veya sınırsız it dalaşı seçin</color></size>";
        tmp.fontSize = 26f;
        tmp.characterSpacing = 2.4f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.extraPadding = true;
        tmp.raycastTarget = false;
        SetOutlineSafe(tmp, 0.10f, new Color(0.02f, 0.04f, 0.08f, 0.95f));
        titleObj.transform.SetAsFirstSibling();
    }

    private void SetupSettingsHeader()
    {
        if (settingsPanel == null) return;
        Transform existing = settingsPanel.transform.Find("SettingsHeaderTitle");
        GameObject titleObj = existing != null ? existing.gameObject : new GameObject("SettingsHeaderTitle", typeof(RectTransform));
        titleObj.transform.SetParent(settingsPanel.transform, false);

        RectTransform rt = titleObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -22f);
            rt.sizeDelta = new Vector2(880f, 74f);
        }

        Image bgImg = titleObj.GetComponent<Image>() ?? titleObj.AddComponent<Image>();
        bgImg.sprite = GetHudSprite("hud_header_visor", new Color(0f, 0.90f, 1f, 0.85f), new Color(0.02f, 0.05f, 0.10f, 0.88f), hasLeftBar: false);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        Transform textChild = titleObj.transform.Find("TitleText");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("TitleText", typeof(RectTransform));
        textObj.transform.SetParent(titleObj.transform, false);
        RectTransform tRt = textObj.GetComponent<RectTransform>();
        if (tRt != null)
        {
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(10f, 4f);
            tRt.offsetMax = new Vector2(-10f, -4f);
        }

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>() ?? textObj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = "<b><color=#00E5FF>KOKPİT SİSTEMLERİ</color> VE SES AYARLARI</b>\n<size=52%><color=#94A3B8>Zorluk kalibrasyonu ve aviyonik ses seviyeleri</color></size>";
        tmp.fontSize = 26f;
        tmp.characterSpacing = 2.4f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.extraPadding = true;
        tmp.raycastTarget = false;
        SetOutlineSafe(tmp, 0.10f, new Color(0.02f, 0.04f, 0.08f, 0.95f));
        titleObj.transform.SetAsFirstSibling();
    }

    public void OpenMainPanel()
    {
        if (backgroundImage != null) backgroundImage.enabled = true;
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
            mainPanel.transform.SetAsLastSibling(); // MainPanel ve butonları daima en önde
        }

        if (_titleObject != null)
        {
            _titleObject.SetActive(true);
            _titleObject.transform.SetAsLastSibling(); // Başlık asla panelin arkasında kalmasın
        }

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: true);

        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

        RefreshDailyOpsButtonVisual();

        // Menüdeyken oyun içi tüm HUD ve dokunmatik kontrollerin gizli kalmasını sağla
        SetTouchControlsVisibility(false, false);
        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.SetHUDVisibility(false);
    }

    public void OpenModesPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (modesPanel != null)
        {
            modesPanel.SetActive(true);
            modesPanel.transform.SetAsLastSibling();
            Image mImg = modesPanel.GetComponent<Image>();
            if (mImg != null)
            {
                mImg.sprite = GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.85f), new Color(0.02f, 0.05f, 0.10f, 0.90f), false);
                mImg.type = Image.Type.Sliced;
                mImg.color = Color.white;
            }
            SetupModesHeader();
            if (_campaign1Button == null || _campaign2Button == null || _campaign3Button == null
                || _endlessButton == null || _bossRushButton == null || _bulletHellButton == null
                || _timeAttackButton == null || _hardcoreButton == null || _modesDiffEasyBtn == null
                || _briefingTMP == null || _modesLeaderboardButton == null || backFromModesButton == null)
            {
                SetupCampaignSelectionUI();
            }
            if (backFromModesButton != null)
            {
                FormatBottomLeftBackBtn(backFromModesButton, OpenMainPanel);
                backFromModesButton.transform.SetAsLastSibling();
            }
        }
        if (_titleObject != null) _titleObject.SetActive(false);

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: true);

        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

        UpdateUIFeedback();
    }

    public void OpenSettingsPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling();
            Image sImg = settingsPanel.GetComponent<Image>();
            if (sImg != null)
            {
                sImg.sprite = GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.85f), new Color(0.02f, 0.05f, 0.10f, 0.90f), false);
                sImg.type = Image.Type.Sliced;
                sImg.color = Color.white;
            }
            SetupSettingsHeader();
            SetupMenuSoundControls();
            if (backFromSettingsButton != null)
            {
                FormatBottomLeftBackBtn(backFromSettingsButton, OpenMainPanel);
                backFromSettingsButton.transform.SetAsLastSibling();
            }
        }
        if (_titleObject != null) _titleObject.SetActive(false);

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: true);

        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

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

        _soundControlsContainer = new GameObject("MenuSoundControls", typeof(RectTransform));
        _soundControlsContainer.transform.SetParent(settingsPanel.transform, false);

        RectTransform rootRt = _soundControlsContainer.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(540f, 175f);
            rootRt.anchoredPosition = new Vector2(0f, -75f);
        }

        CreateMenuVolumeRow(_soundControlsContainer.transform, "SAVAŞ MÜZİĞİ", 34f, isMusic: true);
        CreateMenuVolumeRow(_soundControlsContainer.transform, "MERMİ & EFEKT", -44f, isMusic: false);
    }

    private void SetupCampaignSelectionUI()
    {
        if (modesPanel == null) return;

        // 1. Kampanya 1 (Operasyon Kara Şafak)
        if (_campaign1Button == null)
        {
            if (soloButton != null)
            {
                _campaign1Button = soloButton;
            }
            else
            {
                Transform existingSolo = modesPanel.transform.Find("SoloButon") ?? modesPanel.transform.Find("SoloButton");
                if (existingSolo != null) _campaign1Button = existingSolo.GetComponent<Button>();
                if (_campaign1Button == null)
                    _campaign1Button = CreateOrFindModesButton("SoloButton", 180f, "BÖLÜM 1: OPERASYON KARA ŞAFAK", new Color(0.12f, 0.85f, 0.45f), 560f, 48f, 0f, "hud_btn_emerald");
                soloButton = _campaign1Button;
            }
        }
        _campaign1Button.gameObject.SetActive(true);
        FormatBtn(_campaign1Button, 180f, "BÖLÜM 1: OPERASYON KARA ŞAFAK", new Color(0.12f, 0.85f, 0.45f), 560f, 48f, 0f, "hud_btn_emerald");
        _campaign1Button.onClick.RemoveAllListeners();
        _campaign1Button.onClick.AddListener(() => SetCampaign(StoryCampaignManager.CampaignType.OperationBlackDawn));

        // 2. Kampanya 2: Proje Kimeras
        _campaign2Button = CreateOrFindModesButton("Campaign2Btn", 128f, "BÖLÜM 2: PROJE KİMERAS", new Color(0.28f, 0.62f, 1.0f), 560f, 48f, 0f, "hud_btn_blue");
        _campaign2Button.onClick.RemoveAllListeners();
        _campaign2Button.onClick.AddListener(() => SetCampaign(StoryCampaignManager.CampaignType.ProjectChimera));

        // 3. Kampanya 3: Gökyüzü Paralı Askeri
        _campaign3Button = CreateOrFindModesButton("Campaign3Btn", 76f, "BÖLÜM 3: GÖKYÜZÜ PARALI ASKERİ", new Color(1.0f, 0.80f, 0.22f), 560f, 48f, 0f, "hud_btn_amber");
        _campaign3Button.onClick.RemoveAllListeners();
        _campaign3Button.onClick.AddListener(() => SetCampaign(StoryCampaignManager.CampaignType.SkyMercenary));

        // 4. Sonsuz İt Dalaşı (Endless Survival Mode)
        _endlessButton = CreateOrFindModesButton("EndlessModeBtn", 26f, "[SONSUZ] İT DALAŞI", new Color(0f, 0.90f, 1f), 274f, 44f, posX: -142f, spriteName: "hud_btn_cyan");
        _endlessButton.onClick.RemoveAllListeners();
        _endlessButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.Endless));

        // 5. Boss Rush (4 Boss Avı)
        _bossRushButton = CreateOrFindModesButton("BossRushBtn", 26f, "[BOSS RUSH] 4 AS", new Color(0.95f, 0.25f, 0.25f), 274f, 44f, posX: 142f, spriteName: "hud_btn_red");
        _bossRushButton.onClick.RemoveAllListeners();
        _bossRushButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.BossRush));

        // 6. Mermi Cehennemi (Bullet Hell)
        _bulletHellButton = CreateOrFindModesButton("BulletHellBtn", -24f, "[CEHENNEM] MERMİ FIRTINASI", new Color(0.95f, 0.20f, 0.50f), 274f, 44f, posX: -142f, spriteName: "hud_btn_red");
        _bulletHellButton.onClick.RemoveAllListeners();
        _bulletHellButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.BulletHell));

        // 7. Zaman Taarruzu (Time Attack)
        _timeAttackButton = CreateOrFindModesButton("TimeAttackBtn", -24f, "[HIZ] ZAMAN TAARRUZU", new Color(1.0f, 0.75f, 0.20f), 274f, 44f, posX: 142f, spriteName: "hud_btn_amber");
        _timeAttackButton.onClick.RemoveAllListeners();
        _timeAttackButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.TimeAttack));

        // 8. Hardcore Modu (Tek Can, 200 HP, Her Uçak Giderek Güçlenen Boss)
        _hardcoreButton = CreateOrFindModesButton("HardcoreModeBtn", -74f, "[!] [HARDCORE] 1 CAN BOSS", new Color(1.0f, 0.15f, 0.15f), 274f, 44f, posX: -142f, spriteName: "hud_btn_red");
        _hardcoreButton.onClick.RemoveAllListeners();
        _hardcoreButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.Hardcore));

        // 9. Online 2 Oyunculu Düello
        if (onlineButton == null)
        {
            Transform existingOnline = modesPanel.transform.Find("OnlineButon") ?? modesPanel.transform.Find("OnlineButton");
            if (existingOnline != null) onlineButton = existingOnline.GetComponent<Button>();
            if (onlineButton == null)
                onlineButton = CreateOrFindModesButton("OnlineButton", -74f, "ONLINE: 2P DUELLO", new Color(0.40f, 0.35f, 0.75f), 274f, 44f, posX: 142f, spriteName: "hud_btn_blue");
        }
        if (onlineButton != null)
        {
            onlineButton.gameObject.SetActive(true);
            FormatBtn(onlineButton, -74f, "ONLINE: 2P DUELLO", new Color(0.40f, 0.35f, 0.75f), 274f, 44f, posX: 142f, hudSpriteName: "hud_btn_blue");
            onlineButton.onClick.RemoveAllListeners();
            onlineButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.Online));
        }

        // 10. Modlar Panelinde Doğrudan Zorluk Seçim Butonları
        SetupModesDifficultyRow();

        // 11. Taktik Sefer Brifing Kartı
        SetupCampaignBriefingBox();

        // 12. Geri Dön Butonu
        if (backFromModesButton == null)
        {
            Transform existingBack = modesPanel.transform.Find("BackFromModesButon") ?? modesPanel.transform.Find("BackFromModesButton") ?? modesPanel.transform.Find("BackButton");
            if (existingBack != null) backFromModesButton = existingBack.GetComponent<Button>();
            if (backFromModesButton == null)
            {
                GameObject bObj = new GameObject("BackFromModesButton", typeof(RectTransform));
                bObj.transform.SetParent(modesPanel.transform, false);
                backFromModesButton = bObj.AddComponent<Button>();
            }
        }
        if (backFromModesButton != null)
        {
            backFromModesButton.gameObject.SetActive(true);
            FormatBottomLeftBackBtn(backFromModesButton, OpenMainPanel);
        }

        // 13. Modlar Paneli Rekor Tablosu Butonu (Sağ Alt Köşe)
        _modesLeaderboardButton = CreateOrFindModesButton("ModesLeaderboardBtn", 25f, "[#] REKOR TABLOSU", new Color(1.0f, 0.80f, 0.22f), 260f, 50f, posX: 0f, spriteName: "hud_btn_amber");
        if (_modesLeaderboardButton != null)
        {
            RectTransform lbRt = _modesLeaderboardButton.GetComponent<RectTransform>();
            if (lbRt != null)
            {
                lbRt.anchorMin = new Vector2(1f, 0f);
                lbRt.anchorMax = new Vector2(1f, 0f);
                lbRt.pivot = new Vector2(1f, 0f);
                lbRt.anchoredPosition = new Vector2(-40f, 25f);
                lbRt.sizeDelta = new Vector2(260f, 50f);
            }
            _modesLeaderboardButton.onClick.RemoveAllListeners();
            _modesLeaderboardButton.onClick.AddListener(() =>
            {
                LeaderboardManager.BoardMode targetBoard = GameSettings.SelectedMode switch
                {
                    GameSettings.GameMode.Hardcore => LeaderboardManager.BoardMode.Hardcore,
                    GameSettings.GameMode.TimeAttack => LeaderboardManager.BoardMode.TimeAttack,
                    GameSettings.GameMode.BulletHell => LeaderboardManager.BoardMode.BulletHell,
                    GameSettings.GameMode.BossRush => LeaderboardManager.BoardMode.BossRush,
                    _ => LeaderboardManager.BoardMode.Endless
                };
                OpenLeaderboardPanel(targetBoard);
            });
        }
    }

    private void SetupModesDifficultyRow()
    {
        _modesDiffEasyBtn = CreateOrFindModesButton("ModesDiffEasyBtn", -122f, "KOLAY", new Color(0.18f, 0.85f, 0.45f), 180f, 40f, posX: -186f, spriteName: "hud_btn_emerald");
        _modesDiffEasyBtn.onClick.RemoveAllListeners();
        _modesDiffEasyBtn.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Kolay));

        _modesDiffNormalBtn = CreateOrFindModesButton("ModesDiffNormalBtn", -122f, "NORMAL", new Color(1.0f, 0.80f, 0.22f), 180f, 40f, posX: 0f, spriteName: "hud_btn_amber");
        _modesDiffNormalBtn.onClick.RemoveAllListeners();
        _modesDiffNormalBtn.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Normal));

        _modesDiffHardBtn = CreateOrFindModesButton("ModesDiffHardBtn", -122f, "ZOR", new Color(0.95f, 0.25f, 0.25f), 180f, 40f, posX: 186f, spriteName: "hud_btn_red");
        _modesDiffHardBtn.onClick.RemoveAllListeners();
        _modesDiffHardBtn.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Zor));
    }

    private Button CreateOrFindModesButton(string objName, float posY, string text, Color color, float width, float height, float posX = 0f, string spriteName = null)
    {
        Transform existing = modesPanel.transform.Find(objName);
        GameObject btnObj = existing != null ? existing.gameObject : null;

        if (btnObj == null)
        {
            btnObj = new GameObject(objName, typeof(RectTransform));
            btnObj.transform.SetParent(modesPanel.transform, false);

            btnObj.AddComponent<Image>();
            btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            textObj.AddComponent<TextMeshProUGUI>();
        }

        btnObj.SetActive(true);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        Button btn = btnObj.GetComponent<Button>();
        FormatBtn(btn, posY, text, color, width, height, posX, spriteName);
        return btn;
    }

    private void SetupCampaignBriefingBox()
    {
        Transform existing = modesPanel.transform.Find("CampaignBriefingBox");
        GameObject boxObj = existing != null ? existing.gameObject : new GameObject("CampaignBriefingBox", typeof(RectTransform));
        boxObj.transform.SetParent(modesPanel.transform, false);

        RectTransform rt = boxObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -180f);
            rt.sizeDelta = new Vector2(560f, 76f);
        }

        Image bg = boxObj.GetComponent<Image>() ?? boxObj.AddComponent<Image>();
        bg.sprite = GetHudSprite("hud_panel_frame", new Color(0f, 0.85f, 1f, 0.85f), new Color(0.03f, 0.07f, 0.12f, 0.94f), false);
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;
        bg.raycastTarget = false;

        Outline outline = boxObj.GetComponent<Outline>();
        if (outline != null) Destroy(outline);

        Transform textChild = boxObj.transform.Find("BriefingText");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("BriefingText", typeof(RectTransform));
        textObj.transform.SetParent(boxObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        if (textRt != null)
        {
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 6f);
            textRt.offsetMax = new Vector2(-16f, -6f);
        }

        _briefingTMP = textObj.GetComponent<TextMeshProUGUI>() ?? textObj.AddComponent<TextMeshProUGUI>();
        _briefingTMP.font = GetTacticalFont();
        _briefingTMP.enableAutoSizing = true;
        _briefingTMP.fontSizeMin = 13f;
        _briefingTMP.fontSizeMax = 20f;
        _briefingTMP.fontSize = 19f;
        _briefingTMP.fontStyle = FontStyles.Bold;
        _briefingTMP.characterSpacing = 1.8f;
        _briefingTMP.alignment = TextAlignmentOptions.Center;
        _briefingTMP.color = Color.white;
        _briefingTMP.lineSpacing = 2.5f;
        _briefingTMP.textWrappingMode = TextWrappingModes.Normal;
        _briefingTMP.overflowMode = TextOverflowModes.Truncate;
        _briefingTMP.extraPadding = true;
        SetOutlineSafe(_briefingTMP, 0.12f, Color.black);
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
        GameObject row = new GameObject("Row_" + (isMusic ? "Music" : "SFX"), typeof(RectTransform));
        row.transform.SetParent(parent, false);

        RectTransform rRt = row.GetComponent<RectTransform>();
        if (rRt != null)
        {
            rRt.anchorMin = new Vector2(0.5f, 0.5f);
            rRt.anchorMax = new Vector2(0.5f, 0.5f);
            rRt.sizeDelta = new Vector2(500f, 64f);
            rRt.anchoredPosition = new Vector2(0f, localY);
        }

        // Başlık TMP
        GameObject lblObj = new GameObject("Label", typeof(RectTransform));
        lblObj.transform.SetParent(row.transform, false);
        RectTransform lblRt = lblObj.GetComponent<RectTransform>();
        if (lblRt != null)
        {
            lblRt.anchorMin = new Vector2(0f, 0.55f);
            lblRt.anchorMax = new Vector2(0.6f, 0.55f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.sizeDelta = new Vector2(0f, 30f);
        }

        TextMeshProUGUI lblTMP = lblObj.AddComponent<TextMeshProUGUI>();
        lblTMP.font = GetTacticalFont();
        lblTMP.text = title;
        lblTMP.fontSize = 20f;
        lblTMP.fontStyle = FontStyles.Bold;
        lblTMP.characterSpacing = 2.0f;
        lblTMP.color = new Color(0.2f, 0.9f, 1f);
        lblTMP.extraPadding = true;

        // Yüzde Metni
        GameObject valObj = new GameObject("ValText", typeof(RectTransform));
        valObj.transform.SetParent(row.transform, false);
        RectTransform valRt = valObj.GetComponent<RectTransform>();
        if (valRt != null)
        {
            valRt.anchorMin = new Vector2(0.6f, 0.55f);
            valRt.anchorMax = new Vector2(1f, 0.55f);
            valRt.pivot = new Vector2(1f, 0.5f);
            valRt.sizeDelta = new Vector2(0f, 30f);
        }

        TextMeshProUGUI valTMP = valObj.AddComponent<TextMeshProUGUI>();
        valTMP.font = GetTacticalFont();
        valTMP.fontSize = 20f;
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.characterSpacing = 2.0f;
        valTMP.alignment = TextAlignmentOptions.Right;
        valTMP.color = Color.white;
        valTMP.extraPadding = true;

        // [-] Butonu
        GameObject minusObj = new GameObject("MinusBtn", typeof(RectTransform));
        minusObj.transform.SetParent(row.transform, false);
        RectTransform mRt = minusObj.GetComponent<RectTransform>();
        if (mRt != null)
        {
            mRt.anchorMin = new Vector2(0f, 0.1f);
            mRt.anchorMax = new Vector2(0f, 0.1f);
            mRt.pivot = new Vector2(0f, 0.5f);
            mRt.sizeDelta = new Vector2(52f, 38f);
        }

        Image mImg = minusObj.AddComponent<Image>();
        mImg.sprite = GetHudSprite("hud_btn_slate", new Color(0.5f, 0.6f, 0.7f), new Color(0.1f, 0.15f, 0.22f), false);
        mImg.type = Image.Type.Sliced;
        mImg.color = Color.white;
        Button mBtn = minusObj.AddComponent<Button>();

        GameObject mTxtObj = new GameObject("Text", typeof(RectTransform));
        mTxtObj.transform.SetParent(minusObj.transform, false);
        RectTransform mTxtRt = mTxtObj.GetComponent<RectTransform>();
        if (mTxtRt != null)
        {
            mTxtRt.anchorMin = Vector2.zero;
            mTxtRt.anchorMax = Vector2.one;
            mTxtRt.offsetMin = Vector2.zero;
            mTxtRt.offsetMax = Vector2.zero;
        }
        TextMeshProUGUI mTMP = mTxtObj.AddComponent<TextMeshProUGUI>();
        mTMP.font = GetTacticalFont();
        mTMP.text = "-";
        mTMP.fontSize = 26f;
        mTMP.fontStyle = FontStyles.Bold;
        mTMP.alignment = TextAlignmentOptions.Center;
        mTMP.color = Color.white;
        mTMP.extraPadding = true;

        // [+] Butonu
        GameObject plusObj = new GameObject("PlusBtn", typeof(RectTransform));
        plusObj.transform.SetParent(row.transform, false);
        RectTransform pRt = plusObj.GetComponent<RectTransform>();
        if (pRt != null)
        {
            pRt.anchorMin = new Vector2(1f, 0.1f);
            pRt.anchorMax = new Vector2(1f, 0.1f);
            pRt.pivot = new Vector2(1f, 0.5f);
            pRt.sizeDelta = new Vector2(52f, 38f);
        }

        Image pImg = plusObj.AddComponent<Image>();
        pImg.sprite = GetHudSprite("hud_btn_slate", new Color(0.5f, 0.6f, 0.7f), new Color(0.1f, 0.15f, 0.22f), false);
        pImg.type = Image.Type.Sliced;
        pImg.color = Color.white;
        Button pBtn = plusObj.AddComponent<Button>();

        GameObject pTxtObj = new GameObject("Text", typeof(RectTransform));
        pTxtObj.transform.SetParent(plusObj.transform, false);
        RectTransform pTxtRt = pTxtObj.GetComponent<RectTransform>();
        if (pTxtRt != null)
        {
            pTxtRt.anchorMin = Vector2.zero;
            pTxtRt.anchorMax = Vector2.one;
            pTxtRt.offsetMin = Vector2.zero;
            pTxtRt.offsetMax = Vector2.zero;
        }
        TextMeshProUGUI pTMP = pTxtObj.AddComponent<TextMeshProUGUI>();
        pTMP.font = GetTacticalFont();
        pTMP.text = "+";
        pTMP.fontSize = 24f;
        pTMP.fontStyle = FontStyles.Bold;
        pTMP.alignment = TextAlignmentOptions.Center;
        pTMP.color = Color.white;
        pTMP.extraPadding = true;

        // Slider
        GameObject sObj = new GameObject("Slider", typeof(RectTransform));
        sObj.transform.SetParent(row.transform, false);
        RectTransform sRt = sObj.GetComponent<RectTransform>();
        if (sRt != null)
        {
            sRt.anchorMin = new Vector2(0.5f, 0.1f);
            sRt.anchorMax = new Vector2(0.5f, 0.1f);
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.sizeDelta = new Vector2(330f, 26f);
        }

        Slider s = sObj.AddComponent<Slider>();
        s.minValue = 0f;
        s.maxValue = 1f;

        GameObject bgObj = new GameObject("Background", typeof(RectTransform));
        bgObj.transform.SetParent(sObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        if (bgRt != null)
        {
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
        }
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.08f, 0.14f, 0.95f);

        GameObject faObj = new GameObject("Fill Area", typeof(RectTransform));
        faObj.transform.SetParent(sObj.transform, false);
        RectTransform faRt = faObj.GetComponent<RectTransform>();
        if (faRt != null)
        {
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.offsetMin = Vector2.zero;
            faRt.offsetMax = Vector2.zero;
        }

        GameObject fObj = new GameObject("Fill", typeof(RectTransform));
        fObj.transform.SetParent(faObj.transform, false);
        RectTransform fRt = fObj.GetComponent<RectTransform>();
        if (fRt != null)
        {
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
        }
        Image fImg = fObj.AddComponent<Image>();
        fImg.color = new Color(0f, 0.90f, 1f, 0.95f);

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
        if (gameUI != null)
        {
            if (!gameObject.activeInHierarchy) gameUI.SetHUDVisibility(true);
            gameUI.UpdateHealthBars();
        }
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
        bool isBulletHell = (GameSettings.SelectedMode == GameSettings.GameMode.BulletHell);
        bool isTimeAttack = (GameSettings.SelectedMode == GameSettings.GameMode.TimeAttack);
        bool isHardcore = (GameSettings.SelectedMode == GameSettings.GameMode.Hardcore);
        bool isSolo = (GameSettings.SelectedMode == GameSettings.GameMode.Solo);
        var activeCamp = StoryCampaignManager.CurrentCampaign;

        UpdateModeButtonVisual(_campaign1Button, "BÖLÜM 1: OPERASYON KARA ŞAFAK", "hud_btn_emerald", new Color(0.12f, 0.85f, 0.45f), isSolo && activeCamp == StoryCampaignManager.CampaignType.OperationBlackDawn);
        UpdateModeButtonVisual(_campaign2Button, "BÖLÜM 2: PROJE KİMERAS", "hud_btn_blue", new Color(0.28f, 0.62f, 1.0f), isSolo && activeCamp == StoryCampaignManager.CampaignType.ProjectChimera);
        UpdateModeButtonVisual(_campaign3Button, "BÖLÜM 3: GÖKYÜZÜ PARALI ASKERİ", "hud_btn_amber", new Color(1.0f, 0.80f, 0.22f), isSolo && activeCamp == StoryCampaignManager.CampaignType.SkyMercenary);
        UpdateModeButtonVisual(_endlessButton, "[SONSUZ] İT DALAŞI", "hud_btn_cyan", new Color(0f, 0.90f, 1f), isEndless);
        UpdateModeButtonVisual(_bossRushButton, "[BOSS RUSH] 4 AS", "hud_btn_red", new Color(0.95f, 0.25f, 0.25f), isBossRush);
        UpdateModeButtonVisual(_bulletHellButton, "[CEHENNEM] MERMİ FIRTINASI", "hud_btn_red", new Color(0.95f, 0.20f, 0.50f), isBulletHell);
        UpdateModeButtonVisual(_timeAttackButton, "[HIZ] ZAMAN TAARRUZU", "hud_btn_amber", new Color(1.0f, 0.75f, 0.20f), isTimeAttack);
        UpdateModeButtonVisual(_hardcoreButton, "[!] [HARDCORE] 1 CAN BOSS", "hud_btn_red", new Color(1.0f, 0.15f, 0.15f), isHardcore);
        UpdateModeButtonVisual(onlineButton, "ONLINE: 2P DUELLO", "hud_btn_blue", new Color(0.40f, 0.35f, 0.75f), isOnline);

        // 4. Modlar Paneli Brifing Metni
        UpdateBriefingText();

        // 5. Ana Menü Hızlı Zorluk Değiştirme Butonu
        if (_quickDiffButton != null)
        {
            TextMeshProUGUI qTmp = _quickDiffButton.GetComponentInChildren<TextMeshProUGUI>();
            if (qTmp != null)
            {
                qTmp.font = GetTacticalFont();
                qTmp.text = $"<b>PROTOCOL // DIFF-06</b>  <color={diffHex}><b>[ {diffName.ToUpper()} ]</b></color> <size=68%><color=#90A4AE>(Dokun Değiştir)</color></size>";
                qTmp.characterSpacing = 2.0f;
            }
            Image qImg = _quickDiffButton.GetComponent<Image>();
            if (qImg != null)
            {
                string diffSp = diff switch
                {
                    GameSettings.Difficulty.Kolay => "hud_btn_emerald",
                    GameSettings.Difficulty.Zor   => "hud_btn_red",
                    _                             => "hud_btn_amber"
                };
                qImg.sprite = GetHudSprite(diffSp, Color.white, new Color(0.04f, 0.08f, 0.12f, 0.94f), true);
                qImg.type = Image.Type.Sliced;
                qImg.color = Color.white;
            }
            Outline qOut = _quickDiffButton.GetComponent<Outline>();
            if (qOut != null) Destroy(qOut);
        }

        // 6. Ana Menüdeki "HAREKÂTI BAŞLAT" Butonu Başlığı
        if (playButton != null)
        {
            TextMeshProUGUI playTMP = playButton.GetComponentInChildren<TextMeshProUGUI>();
            if (playTMP != null)
            {
                playTMP.font = GetTacticalFont();
                playTMP.raycastTarget = false;
                playTMP.characterSpacing = 2.5f;

                string modeBadge = isOnline ? "<color=#D1C4E9>[ONLINE: 2P DÜELLO]</color>"
                    : isEndless ? $"<color=#4FC3F7>[SONSUZ İT DALAŞI (Rekor: {PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 1)})]</color>"
                    : isBossRush ? "<color=#FF8A80>[BOSS RUSH: 4 AS]</color>"
                    : isBulletHell ? $"<color=#FF4081>[MERMİ FIRTINASI]</color>"
                    : isTimeAttack ? $"<color=#FFD54F>[ZAMAN TAARRUZU]</color>"
                    : isHardcore ? $"<color=#FF2A2A>[HARDCORE (Tek Can)]</color>"
                    : $"<color=#FFE082>[{StoryCampaignManager.GetCampaignName(activeCamp)}]</color>";

                playTMP.text = $"<b><color=#00FFA3>SYS // OPS-01  » HAREKÂTI BAŞLAT</color></b>\n<size=54%>{modeBadge} • <color={diffHex}><b>[{diffName.ToUpper()}]</b></color></size>";
            }
        }
    }

    private void UpdateDifficultyButtonVisual(Button btn, string baseTitle, Color activeColor, bool isSelected)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        var outline = btn.GetComponent<Outline>();
        if (outline != null) Destroy(outline);

        string spriteKey = isSelected
            ? (activeColor.r > 0.7f && activeColor.g < 0.35f ? "hud_btn_red"
               : activeColor.g > 0.7f && activeColor.r < 0.35f ? "hud_btn_emerald"
               : "hud_btn_amber")
            : "hud_btn_slate";

        if (img != null)
        {
            img.sprite = GetHudSprite(spriteKey, activeColor, new Color(0.03f, 0.05f, 0.08f, 0.88f), hasLeftBar: isSelected);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }

        if (tmp != null)
        {
            tmp.font = GetTacticalFont();
            tmp.raycastTarget = false;
            if (isSelected)
            {
                tmp.text = $"<b>[» AKTİF] {baseTitle}</b>";
                tmp.color = Color.white;
                tmp.characterSpacing = 2.0f;
                SetOutlineSafe(tmp, 0.10f, new Color(0.02f, 0.04f, 0.08f, 0.95f));
            }
            else
            {
                tmp.text = baseTitle;
                tmp.color = new Color(0.72f, 0.78f, 0.86f, 0.85f);
                tmp.characterSpacing = 1.5f;
                SetOutlineSafe(tmp, 0.08f, new Color(0.01f, 0.02f, 0.04f, 0.90f));
            }
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
        else if (GameSettings.SelectedMode == GameSettings.GameMode.BulletHell)
        {
            float bestTime = PlayerPrefs.GetFloat("AirBattle_BulletHellBestTime", 0f);
            int m = (int)(bestTime / 60);
            int s = (int)(bestTime % 60);
            _briefingTMP.text = $"<color=#FF4081><b>[MERMİ CEHENNEMİ - HAYATTA KALMA]</b></color> • En İyi: <b>{m:00}:{s:00}</b>\n" +
                                "Spiral mermi fırtınaları, lazer tuzakları ve aşırı cehennem salvosu! Ne kadar dayanabilirsin?\n" +
                                $"{diffStats}";
        }
        else if (GameSettings.SelectedMode == GameSettings.GameMode.TimeAttack)
        {
            int bestKills = PlayerPrefs.GetInt("AirBattle_TimeAttackHighScore", 0);
            _briefingTMP.text = $"<color=#FFD54F><b>[ZAMAN TAARRUZU - HIZ HAREKATI]</b></color> • Rekor: <b>{bestKills} Uçak</b>\n" +
                                "60 saniyeyle başla! Her uçak <b>+4 sn</b>, Bosslar <b>+8 sn</b> ekler! Durmaksızın avlan!\n" +
                                $"{diffStats}";
        }
        else if (GameSettings.SelectedMode == GameSettings.GameMode.Hardcore)
        {
            int bestHc = PlayerPrefs.GetInt("AirBattle_HardcoreHighScore", 0);
            _briefingTMP.text = $"<color=#FF2A2A><b>[HARDCORE MODU: TEK CAN]</b></color> • En Yüksek: <b>{bestHc} Boss</b>\n" +
                                "• Sadece <b>1 Can Hakkı</b> (Yedek can yok, permadeath!).\n" +
                                "• Uçağın <b>200 HP</b> takviyeli gövdeye sahip.\n" +
                                "• <b>HER GELEN DÜŞMAN UÇAĞI BİR ÖNCEKİNDEN DAHA GÜÇLÜ BİR BOSS!</b>\n" +
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

    private void UpdateModeButtonVisual(Button btn, string baseTitle, string activeSpriteKey, Color activeAccentColor, bool isSelected)
    {
        if (btn == null) return;

        Outline outline = btn.GetComponent<Outline>();
        if (outline != null) Destroy(outline);

        Image img = btn.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
        btn.targetGraphic = img;
        img.raycastTarget = true;
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        if (isSelected)
        {
            img.sprite = GetHudSprite(activeSpriteKey, activeAccentColor, new Color(0.04f, 0.10f, 0.18f, 0.96f), hasLeftBar: true);
        }
        else
        {
            img.sprite = GetHudSprite("hud_btn_slate", new Color(0.38f, 0.48f, 0.60f, 0.75f), new Color(0.04f, 0.07f, 0.11f, 0.88f), hasLeftBar: false);
        }

        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null)
        {
            GameObject txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btn.transform, false);
            RectTransform trt = txtObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            tmp = txtObj.AddComponent<TextMeshProUGUI>();
        }

        tmp.font = GetTacticalFont();
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 13f;
        tmp.fontSizeMax = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.extraPadding = true;
        SetOutlineSafe(tmp, 0.10f, new Color(0.01f, 0.02f, 0.05f, 0.95f));

        if (isSelected)
        {
            tmp.text = $"<b>[» AKTİF] {baseTitle}</b>";
            tmp.color = Color.white;
            tmp.characterSpacing = 2.0f;
        }
        else
        {
            tmp.text = baseTitle;
            tmp.color = new Color(0.82f, 0.88f, 0.96f, 0.90f);
            tmp.characterSpacing = 1.5f;
        }
    }

    private void Highlight(Button btn, bool isSelected)
    {
        if (btn == null) return;
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        string title = tmp != null ? tmp.text.Replace("<b>[» AKTİF] ", "").Replace("<b>[► AKTİF] ", "").Replace("</b>", "") : "MOD";
        UpdateModeButtonVisual(btn, title, "hud_btn_cyan", new Color(0f, 0.85f, 1f), isSelected);
    }

    public void OnPlayClicked()
    {
        Time.timeScale = 1f;

        // Menüyü kapat ve savaşı başlat
        gameObject.SetActive(false);
        PlayerProfileUI.Instance?.HideMenuStatusBar();
        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

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
        else
        {
            var gm = FindObjectOfType<GameManager>();
            gm?.ApplyGameMode();
        }

        // Kendi uçağımızın joystick & ateş butonu açık, düşman uçağının kontrolleri sadece İkili Online modda açık
        SetTouchControlsVisibility(showP1Controls: true, showEnemyControls: isOnline);

        // Pause / Durdur Butonunu Kesin Olarak Başlat ve En Öne Al
        var audioUI = AudioSettingsUI.EnsureInstance();
        if (audioUI != null)
        {
            audioUI.EnsureUI();
            if (audioUI.soundButtonObj != null)
            {
                audioUI.soundButtonObj.SetActive(true);
                audioUI.soundButtonObj.transform.SetAsLastSibling();
            }
        }

        // Ateş Butonunu Kesinleştir ve En Öne Al
        FireButton.EnsureP1FireButton();

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
                {
                    j.gameObject.SetActive(showP1Controls);
                    if (showP1Controls) j.transform.SetAsLastSibling();
                }
                else
                {
                    j.gameObject.SetActive(showEnemyControls);
                }
            }
        }

        // 3. Ateş Butonları (Kendi uçağımız P1, düşman P2)
        if (showP1Controls)
        {
            FireButton.EnsureP1FireButton();
        }
        else
        {
            var fireButtons = FindObjectsOfType<FireButton>(true);
            foreach (var fb in fireButtons)
            {
                if (fb != null && fb.isPlayer1)
                    fb.gameObject.SetActive(false);
            }
        }

        var p2FireButtons = FindObjectsOfType<FireButton>(true);
        foreach (var fb in p2FireButtons)
        {
            if (fb != null && !fb.isPlayer1)
                fb.gameObject.SetActive(showEnemyControls);
        }

        // 4. Füze Butonu (P1 Özel Yeteneği)
        var missileBtns = FindObjectsOfType<MissileLauncherButton>(true);
        foreach (var mb in missileBtns)
        {
            if (mb != null) mb.gameObject.SetActive(showP1Controls);
        }
    }
}
