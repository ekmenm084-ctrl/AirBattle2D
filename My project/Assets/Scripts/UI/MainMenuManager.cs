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
        _cachedTacticalFont = TMP_Settings.defaultFontAsset
            ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
            ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
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
    private Button _modesLaunchCombatBtn;
    private Button _quickDiffButton;
    private Button _dailyOpsButton;
    private Button _menuControlSchemeButton;

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

        OptimizeCanvasForTablet();

        // Başlangıçta panellerin durumunu kesin olarak eşitle
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    /// <summary>
    /// Mobil, tablet ve tüm ekranlarda UI öğelerinin ve butonların taşmasını önleyen,
    /// yazıları kristal netliğinde (high-DPI) gösteren dinamik Canvas ölçekleyici.
    /// </summary>
    public static void OptimizeCanvasForTablet()
    {
        // 1. Ekranı kesin olarak Yatay (Landscape) olarak kilitle ve dikey dönüşü engelle
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        if (Screen.orientation != ScreenOrientation.LandscapeLeft && Screen.orientation != ScreenOrientation.LandscapeRight)
        {
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        // 2. Sahnedeki tüm Canvas nesnelerine taşmayı engelleyen Expand ölçekleme uygula
        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
        if (allCanvases == null || allCanvases.Length == 0) return;

        foreach (var canvas in allCanvases)
        {
            if (canvas == null) continue;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // 1080p yüksekliği koru, tüm en-boy oranlarına (16:9, 18:9, 20:9, 21:9, 4:3) uyum sağla
            scaler.dynamicPixelsPerUnit = 3f;
            scaler.referencePixelsPerUnit = 100f;
        }
    }

    private void Update()
    {
        // Menü aktifken oyun içi dokunmatik kontrollerin ve sinematik alt yazıların ekrana sızmasını kesin olarak engelle
        SetTouchControlsVisibility(false, false);
        StoryCampaignManager.EnsureInstance()?.HideCinematicIfMenuOpen();
    }

    private void Start()
    {
        OptimizeCanvasForTablet();
        try { GameSettings.LoadSettings(); } catch (System.Exception e) { Debug.LogError(e); }
        try { AudioManager.EnsureInstance(); } catch (System.Exception e) { Debug.LogError(e); }
        try { StoryCampaignManager.EnsureInstance()?.HideCinematicIfMenuOpen(); } catch (System.Exception e) { Debug.LogError(e); }

        MakeFullScreenRect(GetComponent<RectTransform>());
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

    private static Sprite _cachedReticleSprite;
    public static Sprite GetTacticalReticleSprite()
    {
        if (_cachedReticleSprite != null) return _cachedReticleSprite;
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color clear = new Color(0, 0, 0, 0);
        Color cyan = new Color(0f, 0.85f, 1f, 0.70f);
        Color brightCyan = new Color(0.40f, 0.95f, 1f, 0.95f);
        Color dimCyan = new Color(0f, 0.60f, 0.85f, 0.22f);
        float center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                bool isOuterRing = dist >= 111.5f && dist <= 114f;
                bool isInnerRing = dist >= 81.5f && dist <= 83.5f;
                bool isCenterRing = dist >= 39f && dist <= 41f;

                bool isCrosshairX = Mathf.Abs(x - center) <= 1.2f && ((dist >= 75f && dist <= 90f) || (dist >= 105f && dist <= 122f));
                bool isCrosshairY = Mathf.Abs(y - center) <= 1.2f && ((dist >= 75f && dist <= 90f) || (dist >= 105f && dist <= 122f));
                bool isTopArc = (dist >= 118f && dist <= 121f) && (y > center + 60f) && (Mathf.Abs(x - center) <= 45f);

                if (isTopArc)
                {
                    tex.SetPixel(x, y, brightCyan);
                }
                else if (isOuterRing || isInnerRing || isCenterRing || isCrosshairX || isCrosshairY)
                {
                    tex.SetPixel(x, y, cyan);
                }
                else if (dist <= 114f && dist >= 40f && ((x + y) % 24 == 0))
                {
                    tex.SetPixel(x, y, dimCyan);
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }
        }
        tex.Apply();
        _cachedReticleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _cachedReticleSprite;
    }

    private static readonly Dictionary<string, Sprite> _menuIconCache = new Dictionary<string, Sprite>();

    public static Sprite GetTacticalMenuIconSprite(string iconType, Color color)
    {
        string key = $"{iconType}_{color.r:F2}_{color.g:F2}_{color.b:F2}";
        if (_menuIconCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color clear = new Color(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool pixelOn = false;
                float cx = x - 32f;
                float cy = y - 32f;

                if (iconType == "gamepad")
                {
                    bool inBody = (x >= 12 && x <= 52 && y >= 18 && y <= 42);
                    bool inGripCut = (x >= 25 && x <= 39 && y >= 16 && y <= 26);
                    bool isBorder = inBody && !inGripCut && (x == 12 || x == 52 || y == 42 || (y == 18 && (x <= 25 || x >= 39)) || (y == 26 && x >= 25 && x <= 39) || (x == 25 && y <= 26) || (x == 39 && y <= 26));
                    bool dpadH = (x >= 18 && x <= 26 && y >= 28 && y <= 32);
                    bool dpadV = (x >= 20 && x <= 24 && y >= 26 && y <= 34);
                    bool b1 = (Mathf.Abs(x - 44f) <= 1.5f && Mathf.Abs(y - 34f) <= 1.5f);
                    bool b2 = (Mathf.Abs(x - 40f) <= 1.5f && Mathf.Abs(y - 30f) <= 1.5f);
                    bool b3 = (Mathf.Abs(x - 48f) <= 1.5f && Mathf.Abs(y - 30f) <= 1.5f);
                    bool b4 = (Mathf.Abs(x - 44f) <= 1.5f && Mathf.Abs(y - 26f) <= 1.5f);
                    bool shoulderL = (x >= 15 && x <= 25 && y >= 44 && y <= 46);
                    bool shoulderR = (x >= 39 && x <= 49 && y >= 44 && y <= 46);
                    pixelOn = isBorder || dpadH || dpadV || b1 || b2 || b3 || b4 || shoulderL || shoulderR;
                }
                else if (iconType == "plane")
                {
                    bool fuse = (Mathf.Abs(cx) <= 2.5f && cy >= -18f && cy <= 22f);
                    bool nose = (cy >= 18f && cy <= 26f && Mathf.Abs(cx) <= (26f - cy) * 0.6f);
                    bool wings = (cy >= -6f && cy <= 6f && Mathf.Abs(cx) <= (24f - Mathf.Abs(cy - 2f) * 2.5f));
                    bool tail = (cy >= -20f && cy <= -10f && Mathf.Abs(cx) <= (14f - Mathf.Abs(cy + 15f) * 1.5f));
                    pixelOn = fuse || nose || wings || tail;
                }
                else if (iconType == "cart")
                {
                    bool basketTop = (y == 42 && x >= 14 && x <= 50);
                    bool basketBottom = (y == 24 && x >= 20 && x <= 46);
                    bool basketLeft = (x >= 14 && x <= 20 && y >= 24 && y <= 42 && (x - 14) * 3 == (y - 24));
                    bool basketRight = (x >= 46 && x <= 50 && y >= 24 && y <= 42 && (50 - x) * 4 == (y - 24));
                    bool basketBody = (x >= 16 && x <= 48 && y >= 24 && y <= 40 && ((x + y) % 6 == 0));
                    bool handle = (x >= 10 && x <= 16 && y >= 40 && y <= 46);
                    bool wheel1 = (Vector2.Distance(new Vector2(x, y), new Vector2(24f, 18f)) <= 3.5f);
                    bool wheel2 = (Vector2.Distance(new Vector2(x, y), new Vector2(42f, 18f)) <= 3.5f);
                    pixelOn = basketTop || basketBottom || basketLeft || basketRight || basketBody || handle || wheel1 || wheel2;
                }
                else if (iconType == "gear")
                {
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);
                    float angle = Mathf.Atan2(cy, cx);
                    float teeth = Mathf.Cos(angle * 8f);
                    float outerR = 21f + teeth * 4f;
                    pixelOn = (dist >= 7.5f && dist <= outerR);
                }
                else if (iconType == "power")
                {
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);
                    float angle = Mathf.Atan2(cy, cx) * Mathf.Rad2Deg;
                    bool inCircle = (dist >= 13f && dist <= 17.5f && !(angle >= 65f && angle <= 115f));
                    bool inTick = (Mathf.Abs(cx) <= 2.2f && cy >= 2f && cy <= 20f);
                    pixelOn = inCircle || inTick;
                }

                tex.SetPixel(x, y, pixelOn ? color : clear);
            }
        }
        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _menuIconCache[key] = sp;
        return sp;
    }

    private void FormatPixelExactButton(
        Button btn,
        Vector2 pos,
        Vector2 size,
        Color hoverColor,
        Color pressColor,
        UnityEngine.Events.UnityAction onClick)
    {
        if (btn == null) return;
        GameObject btnObj = btn.gameObject;
        btnObj.SetActive(true);

        RectTransform rt = btnObj.GetComponent<RectTransform>() ?? btnObj.AddComponent<RectTransform>();
        HangarUI.SetNormalizedRect1080p(rt, pos, size);

        Image img = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
        img.sprite = null;
        img.color = Color.white;
        img.raycastTarget = true;
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        Outline outline = btnObj.GetComponent<Outline>();
        if (outline != null) Destroy(outline);

        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(hoverColor.r, hoverColor.g, hoverColor.b, Mathf.Min(0.10f, hoverColor.a));
        cb.pressedColor = new Color(pressColor.r, pressColor.g, pressColor.b, Mathf.Min(0.16f, pressColor.a));
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        cb.disabledColor = new Color(1f, 1f, 1f, 0f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.06f;
        btn.colors = cb;

        // Görsel üzerindeki orijinal aviyonik tipografi ve ikonların üzerine ek metin kutusu çizilmesini engelle
        for (int i = btnObj.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = btnObj.transform.GetChild(i);
            child.gameObject.SetActive(false);
        }

        if (onClick != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(onClick);
        }
        btnObj.transform.SetAsLastSibling();
    }

    private void SetupAviationHeader()
    {
        Transform existingTitle = transform.Find("AirBattleTitle");
        GameObject titleObj = existingTitle != null ? existingTitle.gameObject : new GameObject("AirBattleTitle", typeof(RectTransform));
        titleObj.transform.SetParent(transform, false);

        RectTransform rt = titleObj.GetComponent<RectTransform>() ?? titleObj.AddComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(680f, 85f);
            rt.anchoredPosition = new Vector2(0f, 255f);
        }

        // Taktik Hedefleme & Nişangah Dairesi (Reticle Visor - Başlığın arkasında)
        Transform existingReticle = titleObj.transform.Find("AirBattleReticleVisor");
        GameObject reticleObj = existingReticle != null ? existingReticle.gameObject : new GameObject("AirBattleReticleVisor", typeof(RectTransform));
        reticleObj.transform.SetParent(titleObj.transform, false);

        RectTransform rRt = reticleObj.GetComponent<RectTransform>() ?? reticleObj.AddComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0.5f, 0.5f);
        rRt.anchorMax = new Vector2(0.5f, 0.5f);
        rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.anchoredPosition = Vector2.zero;
        rRt.sizeDelta = new Vector2(340f, 340f);

        Image reticleImg = reticleObj.GetComponent<Image>() ?? reticleObj.AddComponent<Image>();
        reticleImg.sprite = GetTacticalReticleSprite();
        reticleImg.color = Color.white;
        reticleImg.raycastTarget = false;
        reticleObj.transform.SetAsFirstSibling();

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
        tmp.text = "<b><color=#00E5FF>AIR</color> BATTLE</b>";
        tmp.fontSize = 62f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 3.6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.extraPadding = true;
        SetOutlineSafe(tmp, 0.14f, new Color(0.01f, 0.03f, 0.06f, 0.95f));
        tmp.raycastTarget = false;

        // Üst Telemetri [ XA00 ]
        Transform topDecChild = titleObj.transform.Find("TopTelemetry");
        GameObject topDecObj = topDecChild != null ? topDecChild.gameObject : new GameObject("TopTelemetry", typeof(RectTransform));
        topDecObj.transform.SetParent(titleObj.transform, false);
        RectTransform tdRt = topDecObj.GetComponent<RectTransform>() ?? topDecObj.AddComponent<RectTransform>();
        tdRt.anchorMin = new Vector2(0.5f, 1f);
        tdRt.anchorMax = new Vector2(0.5f, 1f);
        tdRt.pivot = new Vector2(0.5f, 0f);
        tdRt.anchoredPosition = new Vector2(0f, 6f);
        tdRt.sizeDelta = new Vector2(200f, 16f);

        TextMeshProUGUI tdTMP = topDecObj.GetComponent<TextMeshProUGUI>() ?? topDecObj.AddComponent<TextMeshProUGUI>();
        tdTMP.font = GetTacticalFont();
        tdTMP.text = "<color=#38BDF8>[ XA00 ]</color>";
        tdTMP.fontSize = 11f;
        tdTMP.characterSpacing = 2.0f;
        tdTMP.alignment = TextAlignmentOptions.Center;
        tdTMP.raycastTarget = false;

        // Yan Köşe Telemetrisi [ 2640, JL0003 ]
        Transform leftDecChild = titleObj.transform.Find("RightTelemetry");
        GameObject leftDecObj = leftDecChild != null ? leftDecChild.gameObject : new GameObject("RightTelemetry", typeof(RectTransform));
        leftDecObj.transform.SetParent(titleObj.transform, false);
        RectTransform ldRt = leftDecObj.GetComponent<RectTransform>() ?? leftDecObj.AddComponent<RectTransform>();
        ldRt.anchorMin = new Vector2(1f, 1f);
        ldRt.anchorMax = new Vector2(1f, 1f);
        ldRt.pivot = new Vector2(0f, 0f);
        ldRt.anchoredPosition = new Vector2(10f, 2f);
        ldRt.sizeDelta = new Vector2(140f, 26f);

        TextMeshProUGUI ldTMP = leftDecObj.GetComponent<TextMeshProUGUI>() ?? leftDecObj.AddComponent<TextMeshProUGUI>();
        ldTMP.font = GetTacticalFont();
        ldTMP.text = "<size=9><color=#64748B>[ 0.015, 001007 ]\n[ 2640, JL0003 ]</color></size>";
        ldTMP.alignment = TextAlignmentOptions.Left;
        ldTMP.raycastTarget = false;

        _titleObject = titleObj;
        _titleObject.SetActive(false); // Arka plan görselindeki orijinal krom AIR BATTLE ve nişangah doğrudan görünsün
    }

    private void FormatLeftTacticalButtonExisting(
        Button btn,
        float posY,
        string title,
        string subtitle,
        string iconType,
        Color themeColor,
        float width = 470f,
        float height = 66f,
        string hudSprite = "hud_btn_cyan")
    {
        if (btn == null) return;
        GameObject btnObj = btn.gameObject;
        btnObj.SetActive(true);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(75f, posY);
            rt.sizeDelta = new Vector2(width, height);
        }

        Image img = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
        img.sprite = GetHudSprite(hudSprite, themeColor, new Color(0.02f, 0.05f, 0.10f, 0.90f), hasLeftBar: true);
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;
        btn.targetGraphic = img;

        Outline outline = btnObj.GetComponent<Outline>();
        if (outline != null) Destroy(outline);

        // Metin (Sol Hizada)
        Transform tChild = btnObj.transform.Find("Text");
        GameObject txtObj = tChild != null ? tChild.gameObject : new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>() ?? txtObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(26f, 4f);
        tRt.offsetMax = new Vector2(-68f, -4f);

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>() ?? txtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.extraPadding = true;
        tmp.raycastTarget = false;

        if (string.IsNullOrEmpty(subtitle))
        {
            tmp.text = $"<b><color=#E0F2FE>{title}</color></b>";
            tmp.fontSize = 24f;
            tmp.characterSpacing = 1.6f;
        }
        else
        {
            tmp.text = $"<b><size=26><color=#FFFFFF>{title}</color></size></b>\n<size=13><color=#7DD3FC>{subtitle}</color></size>";
            tmp.characterSpacing = 1.0f;
        }

        // İkon (Sağ Taraf)
        Transform iChild = btnObj.transform.Find("Icon");
        GameObject iconObj = iChild != null ? iChild.gameObject : new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(btnObj.transform, false);
        RectTransform iRt = iconObj.GetComponent<RectTransform>() ?? iconObj.AddComponent<RectTransform>();
        iRt.anchorMin = new Vector2(1f, 0.5f);
        iRt.anchorMax = new Vector2(1f, 0.5f);
        iRt.pivot = new Vector2(1f, 0.5f);
        iRt.anchoredPosition = new Vector2(-20f, 0f);
        iRt.sizeDelta = new Vector2(36f, 36f);

        Image iconImg = iconObj.GetComponent<Image>() ?? iconObj.AddComponent<Image>();
        iconImg.sprite = GetTacticalMenuIconSprite(iconType, themeColor);
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;
    }

    private Button FormatLeftTacticalButton(
        string objName,
        float posY,
        string title,
        string subtitle,
        string iconType,
        Color themeColor,
        float width = 470f,
        float height = 66f,
        string hudSprite = "hud_btn_cyan")
    {
        if (mainPanel == null) return null;
        Transform existing = mainPanel.transform.Find(objName);
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject(objName, typeof(RectTransform));
        btnObj.transform.SetParent(mainPanel.transform, false);

        Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
        FormatLeftTacticalButtonExisting(btn, posY, title, subtitle, iconType, themeColor, width, height, hudSprite);
        return btn;
    }

    private string GetActiveModeAndDiffSubtitle()
    {
        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);
        bool isEndless = (GameSettings.SelectedMode == GameSettings.GameMode.Endless);
        bool isBossRush = (GameSettings.SelectedMode == GameSettings.GameMode.BossRush);
        bool isBulletHell = (GameSettings.SelectedMode == GameSettings.GameMode.BulletHell);
        bool isTimeAttack = (GameSettings.SelectedMode == GameSettings.GameMode.TimeAttack);
        bool isHardcore = (GameSettings.SelectedMode == GameSettings.GameMode.Hardcore);
        var activeCamp = StoryCampaignManager.CurrentCampaign;

        string modeName = isOnline ? "2 OYUNCULU İT DALAŞI"
            : isEndless ? "SONSUZ İT DALAŞI (Rekor: S)"
            : isBossRush ? "BOSS RUSH (4 AS)"
            : isBulletHell ? "MERMİ FIRTINASI"
            : isTimeAttack ? "ZAMAN TAARRUZU"
            : isHardcore ? "HARDCORE (1 CAN)"
            : activeCamp switch
            {
                StoryCampaignManager.CampaignType.OperationBlackDawn => "BÖLÜM 1: KARA ŞAFAK",
                StoryCampaignManager.CampaignType.ProjectChimera => "BÖLÜM 2: PROJE KİMERAS",
                StoryCampaignManager.CampaignType.SkyMercenary => "BÖLÜM 3: GÖKYÜZÜ PARALISI",
                _ => "HİKÂYE SEFERİ"
            };

        string diffName = GameSettings.SelectedDifficulty switch
        {
            GameSettings.Difficulty.Kolay => "KOLAY (ACEMİ)",
            GameSettings.Difficulty.Zor => "ZOR (AS PİLOT)",
            _ => "NORMAL (YÜZBAŞI)"
        };

        return $"[{modeName}] • [{diffName}]";
    }

    private void FormatMilitaryButtons()
    {
        // 0. Eski dağınık butonları gizle
        Transform oldCrates = mainPanel?.transform.Find("CratesButton");
        if (oldCrates != null) oldCrates.gameObject.SetActive(false);
        Transform oldLb = mainPanel?.transform.Find("LeaderboardButton");
        if (oldLb != null) oldLb.gameObject.SetActive(false);
        Transform oldDiff = mainPanel?.transform.Find("QuickDiffButton");
        if (oldDiff != null) oldDiff.gameObject.SetActive(false);
        if (modesButton != null) modesButton.gameObject.SetActive(false);

        // 1. OYNA (Görsel üzerindeki OYNA kartının üzerine piksel piksel oturan şeffaf etkileşimli buton)
        if (playButton == null && mainPanel != null)
        {
            Transform pBtn = mainPanel.transform.Find("PlayButon") ?? mainPanel.transform.Find("playButton") ?? mainPanel.transform.Find("PlayButton");
            if (pBtn != null) playButton = pBtn.GetComponent<Button>();
        }
        if (playButton == null && mainPanel != null)
        {
            GameObject pObj = new GameObject("PlayButton", typeof(RectTransform));
            pObj.transform.SetParent(mainPanel.transform, false);
            playButton = pObj.AddComponent<Button>();
        }
        FormatPixelExactButton(playButton, new Vector2(-472.2f, 133.9f), new Vector2(487.4f, 120.3f), new Color(0f, 0.95f, 1f, 0.16f), new Color(0f, 0.95f, 1f, 0.35f), OpenModesPanel);

        // 2. HANGAR (Uçak filosu, destek droneları ve askeri envanter üssü)
        Transform hBtn = mainPanel?.transform.Find("HangarButton");
        GameObject hObj = hBtn != null ? hBtn.gameObject : new GameObject("HangarButton", typeof(RectTransform));
        hObj.transform.SetParent(mainPanel.transform, false);
        _hangarButton = hObj.GetComponent<Button>() ?? hObj.AddComponent<Button>();
        FormatPixelExactButton(_hangarButton, new Vector2(-472.2f, 4.5f), new Vector2(487.4f, 96.3f), new Color(0.28f, 0.62f, 1f, 0.16f), new Color(0.28f, 0.62f, 1f, 0.35f), OpenHangarPanel);

        // 3. MAĞAZA (Savaş jetleri, silahlar, donanım, kredi paketleri ve ikmal sandıkları)
        Transform mBtn = mainPanel?.transform.Find("MilitaryStoreButton");
        GameObject mObj = mBtn != null ? mBtn.gameObject : new GameObject("MilitaryStoreButton", typeof(RectTransform));
        mObj.transform.SetParent(mainPanel.transform, false);
        _militaryStoreButton = mObj.GetComponent<Button>() ?? mObj.AddComponent<Button>();
        FormatPixelExactButton(_militaryStoreButton, new Vector2(-472.2f, -118.1f), new Vector2(487.4f, 97.8f), new Color(0.12f, 0.85f, 0.92f, 0.16f), new Color(0.12f, 0.85f, 0.92f, 0.35f), OpenStorePanel);

        // 4. AYARLAR (Kokpit sistemleri, ses kalibrasyonu, zorluk dereceleri)
        if (settingsButton == null && mainPanel != null)
        {
            Transform sBtn = mainPanel.transform.Find("SettingsButton") ?? mainPanel.transform.Find("settingsButton");
            if (sBtn != null) settingsButton = sBtn.GetComponent<Button>();
        }
        if (settingsButton == null && mainPanel != null)
        {
            GameObject sObj = new GameObject("SettingsButton", typeof(RectTransform));
            sObj.transform.SetParent(mainPanel.transform, false);
            settingsButton = sObj.AddComponent<Button>();
        }
        FormatPixelExactButton(settingsButton, new Vector2(-472.2f, -238.4f), new Vector2(487.4f, 97.8f), new Color(0.50f, 0.60f, 0.72f, 0.16f), new Color(0.50f, 0.60f, 0.72f, 0.35f), OpenSettingsPanel);

        // 5. Profil Tetikleyici Buton (Sol Üst Köşe - Pilot Kimlik Kartı & Hizmet Belgesi Modalı)
        Transform profBtn = mainPanel?.transform.Find("ProfileClickTrigger");
        GameObject profObj = profBtn != null ? profBtn.gameObject : new GameObject("ProfileClickTrigger", typeof(RectTransform));
        profObj.transform.SetParent(mainPanel.transform, false);
        Button pClickBtn = profObj.GetComponent<Button>() ?? profObj.AddComponent<Button>();
        FormatPixelExactButton(pClickBtn, new Vector2(-566.9f, 509.9f), new Vector2(361f, 60.2f), new Color(0f, 0.85f, 1f, 0.12f), new Color(0f, 0.85f, 1f, 0.28f), () => PlayerProfileUI.EnsureUI()?.OpenDossierModal());

        // Ayarlar Paneli Zorluk Butonları (Kolay, Normal, Zor)
        FormatBtn(easyButton, 120f, "ACEMİ PİLOT (Kolay)", new Color(0.18f, 0.85f, 0.45f), 520f, 52f, 0f, "hud_btn_emerald");
        FormatBtn(normalButton, 55f, "YÜZBAŞI (Normal)", new Color(1.0f, 0.75f, 0.20f), 520f, 52f, 0f, "hud_btn_amber");
        FormatBtn(hardButton, -10f, "AS PİLOT (Zor)", new Color(0.92f, 0.25f, 0.25f), 520f, 52f, 0f, "hud_btn_red");

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
        tmp.fontSize = (height >= 58f) ? 23f : 20f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = (height >= 58f) ? 24f : 21f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 0.5f; // Temiz, keskin askeri yazı aralığı
        tmp.color = new Color(0.95f, 0.98f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.extraPadding = true;
        SetOutlineSafe(tmp, 0.0f, new Color(0.01f, 0.03f, 0.06f, 0.95f));
        tmp.raycastTarget = false;
    }

    public static void FormatBottomLeftBackBtn(Button btn, UnityEngine.Events.UnityAction onClick = null)
    {
        if (btn == null) return;

        FormatBtn(btn, 0f, "« GERİ DÖN", new Color(0.35f, 0.65f, 0.95f), 300f, 54f, 0f, "hud_btn_blue");

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(50f, 35f);
            rt.sizeDelta = new Vector2(300f, 54f);
        }

        Image img = btn.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
        btn.targetGraphic = img;
        img.raycastTarget = true;
        btn.interactable = true;

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

        Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
        FormatPixelExactButton(btn, new Vector2(-604.5f, -490.4f), new Vector2(255.7f, 60.2f), new Color(0.95f, 0.25f, 0.25f, 0.18f), new Color(0.95f, 0.25f, 0.25f, 0.38f), () =>
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

        // Eski alt sekmeler veya badge varsa gizle, görselin kendisi 1:1 orijinaldir
        Transform existingTabs = mainPanel.transform.Find("DailyOpsMiniTabs");
        if (existingTabs != null) existingTabs.gameObject.SetActive(false);

        Transform existing = mainPanel.transform.Find("DailyOpsButton");
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject("DailyOpsButton", typeof(RectTransform));
        btnObj.transform.SetParent(mainPanel.transform, false);

        _dailyOpsButton = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
        FormatPixelExactButton(_dailyOpsButton, new Vector2(549.9f, -452.8f), new Vector2(428.7f, 165.5f), new Color(1f, 0.78f, 0.18f, 0.16f), new Color(1f, 0.78f, 0.18f, 0.35f), OpenDailyOpsPanel);
    }

    private void RefreshDailyOpsButtonVisual()
    {
        // Arka plan görselinin kendi üzerinde yüksek çözünürlüklü 8 YENİ ÖDÜL ve rozetler doğrudan yer almaktadır
    }

    public bool IsAnyMenuPanelOpen()
    {
        if (!gameObject.activeInHierarchy) return false;
        if (mainPanel != null && mainPanel.activeInHierarchy) return true;
        if (modesPanel != null && modesPanel.activeInHierarchy) return true;
        if (settingsPanel != null && settingsPanel.activeInHierarchy) return true;
        return false;
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
        tmp.text = "<b><color=#00E5FF>HAVA HAREKÂTI</color> VE GÖREV SEFERLERİ</b>\n<size=75%><color=#94A3B8>Taktik senaryo operasyonları veya sınırsız it dalaşı seçin</color></size>";
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
        tmp.text = "<b><color=#00E5FF>KOKPİT SİSTEMLERİ</color> VE SES AYARLARI</b>\n<size=75%><color=#94A3B8>Zorluk kalibrasyonu ve aviyonik ses seviyeleri</color></size>";
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
            _titleObject.SetActive(false); // Arka plan görselindeki orijinal krom AIR BATTLE ve nişangah doğrudan görünsün
        }

        // Ana menüdeyken görsel üzerindeki orijinal pilot rütbesi ve altın barı net kalsın; alt sayfalarda dinamik çubuk açılır
        PlayerProfileUI.Instance?.HideMenuStatusBar();

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

    private GameObject _soundControlsContainer;
    private TextMeshProUGUI _settingsLiveGoldTMP;
    private static Sprite _settingsBgSprite;
    private static Sprite _settingsDiffInactSprite;
    private static Sprite _settingsDiffActSprite;
    private static Sprite _settingsCtrlGoldSprite;

    private static Sprite LoadSettingsSpriteAsset(ref Sprite cache, string fileBase, bool isJpg = false)
    {
        if (cache != null) return cache;
        string ext = isJpg ? ".jpg" : ".png";
        string[] paths = new string[]
        {
            System.IO.Path.Combine(Application.dataPath, "Sprites/" + fileBase + ext),
            System.IO.Path.Combine(Application.dataPath, "Resources/" + fileBase + ext)
        };
        foreach (var p in paths)
        {
            if (System.IO.File.Exists(p))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(p);
                Texture2D tex = new Texture2D(2, 2, isJpg ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    cache = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    return cache;
                }
            }
        }
        cache = Resources.Load<Sprite>(fileBase);
        return cache;
    }

    public static Sprite GetSettingsBgSprite() => LoadSettingsSpriteAsset(ref _settingsBgSprite, "settings_background", true);
    public static Sprite GetSettingsDiffInactiveSprite() => LoadSettingsSpriteAsset(ref _settingsDiffInactSprite, "settings_diff_inactive", false);
    public static Sprite GetSettingsDiffActiveSprite() => LoadSettingsSpriteAsset(ref _settingsDiffActSprite, "settings_diff_active", false);
    public static Sprite GetSettingsCtrlGoldSprite() => LoadSettingsSpriteAsset(ref _settingsCtrlGoldSprite, "settings_ctrl_gold", false);

    public void OpenSettingsPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling();
            RectTransform spRt = settingsPanel.GetComponent<RectTransform>();
            if (spRt != null)
            {
                spRt.anchorMin = Vector2.zero;
                spRt.anchorMax = Vector2.one;
                spRt.offsetMin = Vector2.zero;
                spRt.offsetMax = Vector2.zero;
            }
            Image sImg = settingsPanel.GetComponent<Image>() ?? settingsPanel.AddComponent<Image>();
            if (sImg != null)
            {
                sImg.sprite = GetSettingsBgSprite();
                sImg.type = Image.Type.Simple;
                sImg.color = Color.white;
            }
            Transform oldHdr = settingsPanel.transform.Find("SettingsHeaderTitle");
            if (oldHdr != null) oldHdr.gameObject.SetActive(false);

            SetupSettings1To1Layout();
            SetupMenuSoundControls();
        }
        if (_titleObject != null) _titleObject.SetActive(false);

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: false);
        if (_settingsLiveGoldTMP != null)
        {
            int goldVal = HangarManager.EnsureInstance()?.gold ?? 0;
            _settingsLiveGoldTMP.text = $"ALTIN: {goldVal:N0} G";
        }

        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

        UpdateUIFeedback();
    }

    private void SetupSettings1To1Layout()
    {
        if (settingsPanel == null) return;

        // 1. Üst Sağ Canlı Altın Göstergesi ve Üst Sol Profil Tetikleyicisi
        Transform existingGold = settingsPanel.transform.Find("SettingsLiveGoldBadge");
        GameObject goldObj = existingGold != null ? existingGold.gameObject : new GameObject("SettingsLiveGoldBadge", typeof(RectTransform));
        goldObj.transform.SetParent(settingsPanel.transform, false);
        HangarUI.SetNormalizedRect1080p(goldObj.GetComponent<RectTransform>(), new Vector2(685.0f, 526.0f), new Vector2(215.0f, 30.0f));
        _settingsLiveGoldTMP = goldObj.GetComponent<TextMeshProUGUI>() ?? goldObj.AddComponent<TextMeshProUGUI>();
        _settingsLiveGoldTMP.font = GetTacticalFont();
        _settingsLiveGoldTMP.fontSize = 18f;
        _settingsLiveGoldTMP.characterSpacing = 1.6f;
        _settingsLiveGoldTMP.fontStyle = FontStyles.Bold;
        _settingsLiveGoldTMP.alignment = TextAlignmentOptions.Left;
        _settingsLiveGoldTMP.color = new Color(1f, 0.88f, 0.25f);
        _settingsLiveGoldTMP.raycastTarget = false;

        HangarUI.FormatPixelExactButton(
            "SettingsProfileTrigger",
            settingsPanel.transform,
            new Vector2(-565.6f, 513.7f),
            new Vector2(361.0f, 52.6f),
            new Color(0f, 0.85f, 1f, 0.10f),
            new Color(0f, 0.7f, 0.95f, 0.18f),
            () => PlayerProfileUI.EnsureUI()?.OpenDossierModal());

        // 2. Merkezde Hizalanmış 3 Zorluk Butonu (ACEMİ PİLOT, YÜZBAŞI, AS PİLOT - Yanlardaki OYNA/HANGAR/MAĞAZA kaldırıldı)
        string[] oldSideBtns = { "SettingsNav_Play", "SettingsNav_Hangar", "SettingsNav_Store", "SettingsNav_Settings", "SettingsDailyOpsBtn" };
        foreach (string sbName in oldSideBtns)
        {
            Transform oldSb = settingsPanel.transform.Find(sbName);
            if (oldSb != null) oldSb.gameObject.SetActive(false);
        }

        if (easyButton != null)
        {
            HangarUI.SetNormalizedRect1080p(easyButton.GetComponent<RectTransform>(), new Vector2(0.0f, 257.0f), new Vector2(854.0f, 78.0f));
        }
        if (normalButton != null)
        {
            HangarUI.SetNormalizedRect1080p(normalButton.GetComponent<RectTransform>(), new Vector2(0.0f, 161.5f), new Vector2(854.0f, 77.0f));
        }
        if (hardButton != null)
        {
            HangarUI.SetNormalizedRect1080p(hardButton.GetComponent<RectTransform>(), new Vector2(0.0f, 65.0f), new Vector2(854.0f, 78.0f));
        }

        // 3. Sol Alt « GERİ DÖN Butonu
        if (backFromSettingsButton != null)
        {
            RectTransform bRt = backFromSettingsButton.GetComponent<RectTransform>();
            HangarUI.SetNormalizedRect1080p(bRt, new Vector2(-684.0f, -459.0f), new Vector2(316.0f, 70.0f));
            Image bImg = backFromSettingsButton.GetComponent<Image>();
            if (bImg != null)
            {
                bImg.sprite = null;
                bImg.color = new Color(1f, 1f, 1f, 0.01f);
            }
            TextMeshProUGUI bTmp = backFromSettingsButton.GetComponentInChildren<TextMeshProUGUI>();
            if (bTmp != null) bTmp.text = "";
            backFromSettingsButton.onClick.RemoveAllListeners();
            backFromSettingsButton.onClick.AddListener(OpenMainPanel);
        }
    }

    private void SetupSettingsControlSchemeButton()
    {
        if (settingsPanel == null) return;

        Transform existing = settingsPanel.transform.Find("MenuControlSchemeButton");
        GameObject btnObj = existing != null ? existing.gameObject : null;

        if (btnObj == null)
        {
            btnObj = new GameObject("MenuControlSchemeButton", typeof(RectTransform));
            btnObj.transform.SetParent(settingsPanel.transform, false);

            Image img = btnObj.AddComponent<Image>();
            img.sprite = GetSettingsCtrlGoldSprite();
            img.type = Image.Type.Simple;
            img.color = Color.white;

            _menuControlSchemeButton = btnObj.AddComponent<Button>();
            _menuControlSchemeButton.targetGraphic = img;

            GameObject txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform tRt = txtObj.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.font = GetTacticalFont();
            tmp.fontSize = 23f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 1.4f;
            tmp.lineSpacing = 1.0f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.extraPadding = true;
            tmp.raycastTarget = false;
        }
        else
        {
            _menuControlSchemeButton = btnObj.GetComponent<Button>();
        }

        HangarUI.SetNormalizedRect1080p(btnObj.GetComponent<RectTransform>(), new Vector2(0.0f, -39.5f), new Vector2(854.0f, 97.0f));

        if (_menuControlSchemeButton != null)
        {
            _menuControlSchemeButton.onClick.RemoveAllListeners();
            _menuControlSchemeButton.onClick.AddListener(() =>
            {
                GameSettings.ToggleControlType();
                UpdateUIFeedback();
                try { TouchArrowControls.EnsureInstance()?.UpdateVisibility(); } catch { }
                AudioManager.Instance?.PlayButtonClick();
            });
        }
    }

    private void SetupMenuSoundControls()
    {
        if (settingsPanel == null) return;
        SetupSettingsControlSchemeButton();
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
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
        }

        CreateMenuVolumeRow(_soundControlsContainer.transform, "SAVAŞ MÜZİĞİ", -196.5f, isMusic: true);
        CreateMenuVolumeRow(_soundControlsContainer.transform, "MERMİ & EFEKT", -352.5f, isMusic: false);
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
                    _campaign1Button = CreateOrFindModesButton("SoloButton", 160f, "BÖLÜM 1: OPERASYON KARA ŞAFAK", new Color(0.12f, 0.85f, 0.45f), 580f, 52f, 0f, "hud_btn_emerald");
                soloButton = _campaign1Button;
            }
        }
        _campaign1Button.gameObject.SetActive(true);
        FormatBtn(_campaign1Button, 160f, "BÖLÜM 1: OPERASYON KARA ŞAFAK", new Color(0.12f, 0.85f, 0.45f), 580f, 52f, 0f, "hud_btn_emerald");
        _campaign1Button.onClick.RemoveAllListeners();
        _campaign1Button.onClick.AddListener(() => SetCampaign(StoryCampaignManager.CampaignType.OperationBlackDawn));

        // 2. Kampanya 2: Proje Kimeras
        _campaign2Button = CreateOrFindModesButton("Campaign2Btn", 104f, "BÖLÜM 2: PROJE KİMERAS", new Color(0.28f, 0.62f, 1.0f), 580f, 52f, 0f, "hud_btn_blue");
        _campaign2Button.onClick.RemoveAllListeners();
        _campaign2Button.onClick.AddListener(() => SetCampaign(StoryCampaignManager.CampaignType.ProjectChimera));

        // 3. Kampanya 3: Gökyüzü Paralı Askeri
        _campaign3Button = CreateOrFindModesButton("Campaign3Btn", 48f, "BÖLÜM 3: GÖKYÜZÜ PARALI ASKERİ", new Color(1.0f, 0.80f, 0.22f), 580f, 52f, 0f, "hud_btn_amber");
        _campaign3Button.onClick.RemoveAllListeners();
        _campaign3Button.onClick.AddListener(() => SetCampaign(StoryCampaignManager.CampaignType.SkyMercenary));

        // 4. Sonsuz İt Dalaşı (Endless Survival Mode)
        _endlessButton = CreateOrFindModesButton("EndlessModeBtn", -8f, "[SONSUZ] İT DALAŞI", new Color(0f, 0.90f, 1f), 285f, 48f, posX: -145f, spriteName: "hud_btn_cyan");
        _endlessButton.onClick.RemoveAllListeners();
        _endlessButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.Endless));

        // 5. Boss Rush (4 Boss Avı)
        _bossRushButton = CreateOrFindModesButton("BossRushBtn", -8f, "[BOSS RUSH] 4 AS", new Color(0.95f, 0.25f, 0.25f), 285f, 48f, posX: 145f, spriteName: "hud_btn_red");
        _bossRushButton.onClick.RemoveAllListeners();
        _bossRushButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.BossRush));

        // 6. Mermi Cehennemi (Bullet Hell)
        _bulletHellButton = CreateOrFindModesButton("BulletHellBtn", -60f, "[CEHENNEM] MERMİ FIRTINASI", new Color(0.95f, 0.20f, 0.50f), 285f, 48f, posX: -145f, spriteName: "hud_btn_red");
        _bulletHellButton.onClick.RemoveAllListeners();
        _bulletHellButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.BulletHell));

        // 7. Zaman Taarruzu (Time Attack)
        _timeAttackButton = CreateOrFindModesButton("TimeAttackBtn", -60f, "[HIZ] ZAMAN TAARRUZU", new Color(1.0f, 0.75f, 0.20f), 285f, 48f, posX: 145f, spriteName: "hud_btn_amber");
        _timeAttackButton.onClick.RemoveAllListeners();
        _timeAttackButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.TimeAttack));

        // 8. Hardcore Modu (Tek Can, 200 HP, Her Uçak Giderek Güçlenen Boss)
        _hardcoreButton = CreateOrFindModesButton("HardcoreModeBtn", -112f, "[!] [HARDCORE] 1 CAN BOSS", new Color(1.0f, 0.15f, 0.15f), 285f, 48f, posX: -145f, spriteName: "hud_btn_red");
        _hardcoreButton.onClick.RemoveAllListeners();
        _hardcoreButton.onClick.AddListener(() => SetMode(GameSettings.GameMode.Hardcore));

        // 9. Online 2 Oyunculu Düello
        if (onlineButton == null)
        {
            Transform existingOnline = modesPanel.transform.Find("OnlineButon") ?? modesPanel.transform.Find("OnlineButton");
            if (existingOnline != null) onlineButton = existingOnline.GetComponent<Button>();
            if (onlineButton == null)
                onlineButton = CreateOrFindModesButton("OnlineButton", -112f, "2 OYUNCULU İT DALAŞI", new Color(0.40f, 0.35f, 0.75f), 285f, 48f, posX: 145f, spriteName: "hud_btn_blue");
        }
        if (onlineButton != null)
        {
            onlineButton.gameObject.SetActive(true);
            FormatBtn(onlineButton, -112f, "2 OYUNCULU İT DALAŞI", new Color(0.40f, 0.35f, 0.75f), 285f, 48f, posX: 145f, hudSpriteName: "hud_btn_blue");
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
            RectTransform bRt = backFromModesButton.GetComponent<RectTransform>();
            if (bRt != null)
            {
                bRt.anchorMin = new Vector2(0f, 0f);
                bRt.anchorMax = new Vector2(0f, 0f);
                bRt.pivot = new Vector2(0f, 0f);
                bRt.anchoredPosition = new Vector2(50f, 25f);
                bRt.sizeDelta = new Vector2(280f, 52f);
            }
        }

        // 13. Modlar Paneli Rekor Tablosu Butonu (Sağ Alt Köşe)
        _modesLeaderboardButton = CreateOrFindModesButton("ModesLeaderboardBtn", 25f, "[#] REKOR TABLOSU", new Color(1.0f, 0.80f, 0.22f), 280f, 52f, posX: 0f, spriteName: "hud_btn_amber");
        if (_modesLeaderboardButton != null)
        {
            RectTransform lbRt = _modesLeaderboardButton.GetComponent<RectTransform>();
            if (lbRt != null)
            {
                lbRt.anchorMin = new Vector2(1f, 0f);
                lbRt.anchorMax = new Vector2(1f, 0f);
                lbRt.pivot = new Vector2(1f, 0f);
                lbRt.anchoredPosition = new Vector2(-50f, 25f);
                lbRt.sizeDelta = new Vector2(280f, 52f);
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

        // 14. Modlar Paneli Doğrudan Savaşa Başlama Butonu (Orta Alt)
        _modesLaunchCombatBtn = CreateOrFindModesButton("ModesLaunchCombatBtn", 25f, "» HAREKÂTI BAŞLAT", new Color(0f, 0.95f, 0.65f), 440f, 56f, posX: 0f, spriteName: "hud_btn_cyan");
        if (_modesLaunchCombatBtn != null)
        {
            RectTransform lcRt = _modesLaunchCombatBtn.GetComponent<RectTransform>();
            if (lcRt != null)
            {
                lcRt.anchorMin = new Vector2(0.5f, 0f);
                lcRt.anchorMax = new Vector2(0.5f, 0f);
                lcRt.pivot = new Vector2(0.5f, 0f);
                lcRt.anchoredPosition = new Vector2(0f, 25f);
                lcRt.sizeDelta = new Vector2(440f, 56f);
            }
            _modesLaunchCombatBtn.onClick.RemoveAllListeners();
            _modesLaunchCombatBtn.onClick.AddListener(OnPlayClicked);
            _modesLaunchCombatBtn.transform.SetAsLastSibling();
        }
    }

    private void SetupModesDifficultyRow()
    {
        _modesDiffEasyBtn = CreateOrFindModesButton("ModesDiffEasyBtn", -164f, "KOLAY", new Color(0.18f, 0.85f, 0.45f), 180f, 42f, posX: -190f, spriteName: "hud_btn_emerald");
        _modesDiffEasyBtn.onClick.RemoveAllListeners();
        _modesDiffEasyBtn.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Kolay));

        _modesDiffNormalBtn = CreateOrFindModesButton("ModesDiffNormalBtn", -164f, "NORMAL", new Color(1.0f, 0.80f, 0.22f), 180f, 42f, posX: 0f, spriteName: "hud_btn_amber");
        _modesDiffNormalBtn.onClick.RemoveAllListeners();
        _modesDiffNormalBtn.onClick.AddListener(() => SetDifficulty(GameSettings.Difficulty.Normal));

        _modesDiffHardBtn = CreateOrFindModesButton("ModesDiffHardBtn", -164f, "ZOR", new Color(0.95f, 0.25f, 0.25f), 180f, 42f, posX: 190f, spriteName: "hud_btn_red");
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
            rt.anchoredPosition = new Vector2(0f, -230f);
            rt.sizeDelta = new Vector2(580f, 75f);
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
        _briefingTMP.fontSizeMin = 16f;
        _briefingTMP.fontSizeMax = 24f;
        _briefingTMP.fontSize = 23f;
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

    private void CreateMenuVolumeRow(Transform parent, string title, float barCenterY1080, bool isMusic)
    {
        GameObject row = new GameObject("Row_" + (isMusic ? "Music" : "SFX"), typeof(RectTransform));
        row.transform.SetParent(parent, false);

        RectTransform rRt = row.GetComponent<RectTransform>();
        if (rRt != null)
        {
            rRt.anchorMin = Vector2.zero;
            rRt.anchorMax = Vector2.one;
            rRt.offsetMin = Vector2.zero;
            rRt.offsetMax = Vector2.zero;
        }

        // Yüzde Metni (Merkezlenmiş arka plan kutusundaki sağ üst '%61' / '%51' konumuna birebir kilitli)
        GameObject valObj = new GameObject("ValText", typeof(RectTransform));
        valObj.transform.SetParent(row.transform, false);
        HangarUI.SetNormalizedRect1080p(valObj.GetComponent<RectTransform>(), new Vector2(348.0f, barCenterY1080 + 56.5f), new Vector2(105.0f, 44.0f));

        TextMeshProUGUI valTMP = valObj.AddComponent<TextMeshProUGUI>();
        valTMP.font = GetTacticalFont();
        valTMP.fontSize = 24f;
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.characterSpacing = 1.2f;
        valTMP.alignment = TextAlignmentOptions.Center;
        valTMP.color = Color.white;
        valTMP.raycastTarget = false;

        // [-] Butonu (Merkezlenmiş arka plan görselindeki sekizgen '-' butonunun tam üzeri)
        Button mBtn = HangarUI.FormatPixelExactButton(
            "MinusBtn",
            row.transform,
            new Vector2(-358.0f, barCenterY1080),
            new Vector2(78.0f, 57.0f),
            new Color(0f, 0.85f, 1f, 0.18f),
            new Color(0f, 0.7f, 0.95f, 0.35f),
            () => { });

        // [+] Butonu (Merkezlenmiş arka plan görselindeki sekizgen '+' butonunun tam üzeri)
        Button pBtn = HangarUI.FormatPixelExactButton(
            "PlusBtn",
            row.transform,
            new Vector2(348.0f, barCenterY1080),
            new Vector2(78.0f, 57.0f),
            new Color(0f, 0.85f, 1f, 0.18f),
            new Color(0f, 0.7f, 0.95f, 0.35f),
            () => { });

        // Slider (Merkezlenmiş arka plan görselindeki bar kanalının tam içi)
        GameObject sObj = new GameObject("Slider", typeof(RectTransform));
        sObj.transform.SetParent(row.transform, false);
        RectTransform sRt = sObj.GetComponent<RectTransform>();
        HangarUI.SetNormalizedRect1080p(sRt, new Vector2(-2.5f, barCenterY1080), new Vector2(555.0f, 43.0f));

        Slider s = sObj.AddComponent<Slider>();
        s.minValue = 0f;
        s.maxValue = 1f;

        GameObject bgObj = new GameObject("Background", typeof(RectTransform));
        bgObj.transform.SetParent(sObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.03f, 0.06f, 0.10f, 0.96f);

        GameObject faObj = new GameObject("Fill Area", typeof(RectTransform));
        faObj.transform.SetParent(sObj.transform, false);
        RectTransform faRt = faObj.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero;
        faRt.offsetMax = Vector2.zero;

        GameObject fObj = new GameObject("Fill", typeof(RectTransform));
        fObj.transform.SetParent(faObj.transform, false);
        RectTransform fRt = fObj.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        Image fImg = fObj.AddComponent<Image>();
        fImg.color = new Color(0.06f, 0.86f, 0.96f, 1f);

        s.fillRect = fRt;
        s.targetGraphic = fImg;

        float initVal = isMusic
            ? (AudioManager.Instance != null ? AudioManager.Instance.musicVolume : 0.61f)
            : (AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0.51f);

        s.value = initVal;
        valTMP.text = $"%{(int)Mathf.Round(initVal * 100f)}";

        s.onValueChanged.AddListener((val) =>
        {
            if (isMusic)
                AudioManager.Instance?.SetMusicVolume(val);
            else
                AudioManager.Instance?.SetSFXVolume(val);

            valTMP.text = $"%{(int)Mathf.Round(val * 100f)}";
        });

        mBtn.onClick.RemoveAllListeners();
        mBtn.onClick.AddListener(() =>
        {
            s.value = Mathf.Clamp01(s.value - 0.10f);
            if (!isMusic) AudioManager.Instance?.PlayGunfire(false);
        });

        pBtn.onClick.RemoveAllListeners();
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
        UpdateModeButtonVisual(onlineButton, "2 OYUNCULU İT DALAŞI", "hud_btn_blue", new Color(0.40f, 0.35f, 0.75f), isOnline);

        // 4. Modlar Paneli Brifing Metni
        UpdateBriefingText();

        // 5. Ana Menü Hızlı Zorluk Değiştirme Butonu
        if (_quickDiffButton != null)
        {
            TextMeshProUGUI qTmp = _quickDiffButton.GetComponentInChildren<TextMeshProUGUI>();
            if (qTmp != null)
            {
                qTmp.font = GetTacticalFont();
                qTmp.text = $"<b>ZORLUK DERECESİ:</b>  <color={diffHex}><b>[ {diffName.ToUpper()} ]</b></color> <size=85%><color=#90A4AE>(Dokun Değiştir)</color></size>";
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

        // 6. Ana Menüdeki "OYNA" Butonu Başlığı ve Taktik Alt Yazısı
        if (playButton != null)
        {
            Transform txtChild = playButton.transform.Find("Text");
            TextMeshProUGUI playTMP = txtChild != null ? txtChild.GetComponent<TextMeshProUGUI>() : playButton.GetComponentInChildren<TextMeshProUGUI>();
            if (playTMP != null)
            {
                playTMP.font = GetTacticalFont();
                playTMP.raycastTarget = false;
                playTMP.alignment = TextAlignmentOptions.MidlineLeft;
                playTMP.text = $"<b><size=26><color=#FFFFFF>OYNA</color></size></b>\n<size=13><color=#7DD3FC>{GetActiveModeAndDiffSubtitle()}</color></size>";
            }
        }

        // 7. Ayarlar Panelindeki Kontrol Şeması Butonu (1:1 altın çerçeveli iki satırlı görsel düzen)
        if (_menuControlSchemeButton != null)
        {
            TextMeshProUGUI cTmp = _menuControlSchemeButton.GetComponentInChildren<TextMeshProUGUI>();
            Image cImg = _menuControlSchemeButton.GetComponent<Image>();
            bool isArrows = (GameSettings.SelectedControl == GameSettings.ControlType.Arrows);

            if (cTmp != null)
            {
                cTmp.font = GetTacticalFont();
                cTmp.text = isArrows
                    ? "<b>UÇUŞ KONTROLÜ: <color=#FDE047>[ SOL / SAĞ OK TUŞLARI ]</color></b>\n<size=78%><color=#94A3B8>(Dokun Değiştir)</color></size>"
                    : "<b>UÇUŞ KONTROLÜ: <color=#FDE047>[ SANAL JOYSTICK ]</color></b>\n<size=78%><color=#94A3B8>(Dokun Değiştir)</color></size>";
                cTmp.characterSpacing = 1.4f;
            }
            if (cImg != null)
            {
                cImg.sprite = GetSettingsCtrlGoldSprite();
                cImg.type = Image.Type.Simple;
                cImg.color = Color.white;
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

        bool isSettingsDiffBtn = (btn == easyButton || btn == normalButton || btn == hardButton);

        if (isSettingsDiffBtn)
        {
            if (img != null)
            {
                img.sprite = isSelected ? GetSettingsDiffActiveSprite() : GetSettingsDiffInactiveSprite();
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }

            if (tmp != null)
            {
                tmp.font = GetTacticalFont();
                tmp.raycastTarget = false;
                tmp.fontSize = 24f;
                if (isSelected)
                {
                    tmp.text = $"<b>» <color=#EF4444>[AKTİF]</color> {baseTitle}</b>";
                    tmp.color = Color.white;
                    tmp.characterSpacing = 1.4f;
                }
                else
                {
                    tmp.text = baseTitle;
                    tmp.color = new Color(0.65f, 0.75f, 0.85f, 0.92f);
                    tmp.characterSpacing = 1.2f;
                }
            }
            return;
        }

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
            int best = ProfileManager.GetInt("AirBattle_EndlessHighScore", 1);
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
            float bestTime = ProfileManager.GetFloat("AirBattle_BulletHellBestTime", 0f);
            int m = (int)(bestTime / 60);
            int s = (int)(bestTime % 60);
            _briefingTMP.text = $"<color=#FF4081><b>[MERMİ CEHENNEMİ - HAYATTA KALMA]</b></color> • En İyi: <b>{m:00}:{s:00}</b>\n" +
                                "Spiral mermi fırtınaları, lazer tuzakları ve aşırı cehennem salvosu! Ne kadar dayanabilirsin?\n" +
                                $"{diffStats}";
        }
        else if (GameSettings.SelectedMode == GameSettings.GameMode.TimeAttack)
        {
            int bestKills = ProfileManager.GetInt("AirBattle_TimeAttackHighScore", 0);
            _briefingTMP.text = $"<color=#FFD54F><b>[ZAMAN TAARRUZU - HIZ HAREKATI]</b></color> • Rekor: <b>{bestKills} Uçak</b>\n" +
                                "60 saniyeyle başla! Her uçak <b>+4 sn</b>, Bosslar <b>+8 sn</b> ekler! Durmaksızın avlan!\n" +
                                $"{diffStats}";
        }
        else if (GameSettings.SelectedMode == GameSettings.GameMode.Hardcore)
        {
            int bestHc = ProfileManager.GetInt("AirBattle_HardcoreHighScore", 0);
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
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 22f;
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
        if (mainPanel != null) mainPanel.SetActive(false);
        if (modesPanel != null) modesPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        PlayerProfileUI.Instance?.HideMenuStatusBar();
        if (MilitaryStoreUI.Instance != null) MilitaryStoreUI.Instance.CloseDirectly();
        if (HangarUI.Instance != null) HangarUI.Instance.CloseDirectly();
        if (CratesUI.Instance != null) CratesUI.Instance.CloseDirectly();
        if (InventoryUI.Instance != null) InventoryUI.Instance.CloseDirectly();
        if (LeaderboardUI.Instance != null) LeaderboardUI.Instance.CloseDirectly();
        if (DailyOpsUI.Instance != null) DailyOpsUI.Instance.CloseDirectly();

        if (inGameUI != null) inGameUI.SetActive(true);
        try { TouchArrowControls.EnsureInstance()?.UpdateVisibility(); } catch { }

        // MODU KESİN OLARAK UYGULA:
        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);

        var planes = FindObjectsOfType<PlaneController>();
        foreach (var p in planes)
        {
            if (p.isPlayer1)
            {
                // Hangar ve Kalıcı Geliştirme Özelliklerini Oyuncuya Uygula (2 Oyunculu Düello modunda adil yarış için tek kullanımlık sorti tüketilmez)
                if (!isOnline)
                {
                    HangarManager.EnsureInstance()?.ApplyActiveAircraftStats(p, isActualCombatStart: true);
                }
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
