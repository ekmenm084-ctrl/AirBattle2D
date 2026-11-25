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

    private static readonly Dictionary<string, Sprite> _roundedBoxCache = new Dictionary<string, Sprite>();

    public static Sprite GetRoundedBoxSprite(string key, Color fillColor, Color borderColor, int cornerRadius = 12, int borderWidth = 2)
    {
        if (!string.IsNullOrEmpty(key) && _roundedBoxCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color clear = new Color(0, 0, 0, 0);

        float r = cornerRadius;
        float bw = borderWidth;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = (x < r) ? r : (x > size - 1 - r ? size - 1 - r : x);
                float cy = (y < r) ? r : (y > size - 1 - r ? size - 1 - r : y);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > r)
                {
                    tex.SetPixel(x, y, clear);
                }
                else if (dist > r - bw || x < bw || x >= size - bw || y < bw || y >= size - bw)
                {
                    tex.SetPixel(x, y, borderColor);
                }
                else
                {
                    tex.SetPixel(x, y, fillColor);
                }
            }
        }
        tex.Apply();
        int slice = Mathf.Clamp(cornerRadius + 4, 8, 24);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
        if (!string.IsNullOrEmpty(key)) _roundedBoxCache[key] = sp;
        return sp;
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
    private TextMeshProUGUI _modesCardTitleTMP;
    private TextMeshProUGUI _modesCardRecordTMP;
    private TextMeshProUGUI _modesCardDescTMP;
    private Button _quitFromModesButton;
    private Button _missionsFromModesButton;

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
    private GameObject _dailyOpsBadgeObj;
    private TextMeshProUGUI _dailyOpsBadgeTMP;
    private TextMeshProUGUI _dailyOpsRewardTextTMP;
    private Button _menuControlSchemeButton;

    private void OnDestroy()
    {
        if (DailyOpsManager.Instance != null)
            DailyOpsManager.Instance.OnDailyOpsChanged -= RefreshDailyOpsButtonVisual;
    }

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
    public static bool IsPortraitDuelModeActive()
    {
        if (AutoPlayOnSceneLoad && GameSettings.SelectedMode == GameSettings.GameMode.Online)
            return true;

        bool menuOpen = (Instance != null && Instance.gameObject.activeInHierarchy);
        if (menuOpen) return false;

        if (GameManager.Instance != null)
            return GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer;

        return GameSettings.SelectedMode == GameSettings.GameMode.Online;
    }

    /// <summary>
    /// Normal oyun ve menülerde Yatay (Landscape 1920x1080),
    /// 2 Kişilik VS (Düello) modunda ise Dikey (Portrait 1080x1920) ekran yönünü ve Canvas ölçeğini uygular.
    /// </summary>
    public static void OptimizeCanvasForTablet()
    {
        ApplyScreenOrientationAndCanvas(IsPortraitDuelModeActive());
    }

    public static void ApplyScreenOrientationAndCanvas(bool portraitDuel)
    {
        if (portraitDuel)
        {
            // 2 Kişilik VS Modu: Telefon ekranını Dikey (Portrait) moda geçir
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            if (Screen.orientation != ScreenOrientation.Portrait && Screen.orientation != ScreenOrientation.PortraitUpsideDown)
            {
                Screen.orientation = ScreenOrientation.Portrait;
            }
        }
        else
        {
            // Menüler ve Tek Kişilik Modlar: Ekranı kesin olarak Yatay (Landscape) moda geçir
            // Not: Ekran henüz dikeydeyse hemen ardından AutoRotation atanmaz; aksi halde dikey tutulan telefonda dönüş iptal olur!
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            if (Screen.width < Screen.height ||
                (Screen.orientation != ScreenOrientation.LandscapeLeft && Screen.orientation != ScreenOrientation.LandscapeRight))
            {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
            }
            else
            {
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
        }

#if UNITY_EDITOR
        TrySwitchEditorGameViewAspect(portraitDuel);
#endif

        // Kamera dikey/yatay saha yüksekliği ve merkez sıfırlaması (sarsıntı/yön değişimi kaymasını kesin önler)
        CameraShake.StopAllAndReset();
        Camera cam = Camera.main;
        if (cam != null)
        {
            if (cam.orthographic)
                cam.orthographicSize = portraitDuel ? 8.2f : 5.0f;
            cam.transform.position = new Vector3(0f, 0f, cam.transform.position.z);
        }

        // Sahnedeki tüm kök Canvas nesnelerine yönle uyumlu ölçekleme uygula
        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
        if (allCanvases == null || allCanvases.Length == 0) return;

        foreach (var canvas in allCanvases)
        {
            if (canvas == null || !canvas.isRootCanvas) continue;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            if (portraitDuel)
            {
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 0f; // 1080p genişliği sabitle, tüm dikey telefon oranlarına (9:16, 9:19.5, 9:20) tam oturt
            }
            else
            {
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                // Eğer ekran henüz dikeyden yataya dönme aşamasındaysa (Screen.width < Screen.height), yanlışlıkla 0f kilitleme!
                bool alreadyLandscape = (Screen.width >= Screen.height) && (Screen.height > 0);
                float currentAspect = alreadyLandscape ? ((float)Screen.width / Screen.height) : (16f / 9f);
                scaler.matchWidthOrHeight = (alreadyLandscape && currentAspect < (1920f / 1080f)) ? 0f : 1f;
            }
            scaler.dynamicPixelsPerUnit = 3f;
            scaler.referencePixelsPerUnit = 100f;
        }
    }

#if UNITY_EDITOR
    private static bool? _lastEditorPortraitState = null;

    [UnityEditor.InitializeOnLoadMethod]
    private static void RestoreEditorGameViewOnReload()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!UnityEditor.EditorApplication.isPlaying || !IsPortraitDuelModeActive())
            {
                _lastEditorPortraitState = null;
                TrySwitchEditorGameViewAspect(false);
            }
        };

        UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
    }

    private static void OnEditorPlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.EnteredEditMode ||
            state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
        {
            _lastEditorPortraitState = null;
            TrySwitchEditorGameViewAspect(false);
        }
    }

    private static void TrySwitchEditorGameViewAspect(bool portrait)
    {
        if (_lastEditorPortraitState.HasValue && _lastEditorPortraitState.Value == portrait)
            return;
        _lastEditorPortraitState = portrait;

        try
        {
            System.Reflection.Assembly editorAsm = typeof(UnityEditor.Editor).Assembly;
            if (editorAsm == null) return;

            System.Type gvSizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
            System.Type singleOpenType = editorAsm.GetType("UnityEditor.ScriptableSingleton`1");
            if (gvSizesType == null || singleOpenType == null) return;
            System.Type singleType = singleOpenType.MakeGenericType(gvSizesType);
            object sizesInst = singleType.GetProperty("instance")?.GetValue(null, null);
            if (sizesInst == null) return;

            object currentGroup = gvSizesType.GetProperty("currentGroup")?.GetValue(sizesInst, null);
            if (currentGroup == null) return;

            System.Type groupType = currentGroup.GetType();
            int totalCount = (int)groupType.GetMethod("GetTotalCount").Invoke(currentGroup, null);
            System.Reflection.MethodInfo getGameViewSize = groupType.GetMethod("GetGameViewSize");
            System.Type gvsEnumType = editorAsm.GetType("UnityEditor.GameViewSizeType");
            object aspectRatioEnumVal = System.Enum.Parse(gvsEnumType, "AspectRatio");

            int targetIdx = 0; // 0 = Free Aspect (Unity'nin orijinal pencereye tam sığan varsayılan görünümü; asla taşma yapmaz)

            if (portrait)
            {
                // 2 Kişilik Dikey Modda FixedResolution (1080x1920) yerine pencereye tam oturan 9:16 AspectRatio seç
                int foundPortraitAspect = -1;
                for (int i = 0; i < totalCount; i++)
                {
                    object gvs = getGameViewSize.Invoke(currentGroup, new object[] { i });
                    if (gvs == null) continue;
                    object st = gvs.GetType().GetProperty("sizeType")?.GetValue(gvs, null);
                    int w = (int)gvs.GetType().GetProperty("width").GetValue(gvs, null);
                    int h = (int)gvs.GetType().GetProperty("height").GetValue(gvs, null);
                    if (st != null && st.Equals(aspectRatioEnumVal) && w == 9 && h == 16)
                    {
                        foundPortraitAspect = i;
                        break;
                    }
                }

                if (foundPortraitAspect < 0)
                {
                    System.Type gvsType = editorAsm.GetType("UnityEditor.GameViewSize");
                    object newSize = System.Activator.CreateInstance(gvsType, new object[] { aspectRatioEnumVal, 9, 16, "9:16 Portrait" });
                    groupType.GetMethod("AddCustomSize").Invoke(currentGroup, new object[] { newSize });
                    foundPortraitAspect = (int)groupType.GetMethod("GetTotalCount").Invoke(currentGroup, null) - 1;
                }

                if (foundPortraitAspect >= 0)
                    targetIdx = foundPortraitAspect;
            }

            System.Type gvType = editorAsm.GetType("UnityEditor.GameView");
            UnityEngine.Object[] gvsList = Resources.FindObjectsOfTypeAll(gvType);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.MethodInfo sizeCb = gvType.GetMethod("SizeSelectionCallback", flags);
            System.Reflection.PropertyInfo selProp = gvType.GetProperty("selectedSizeIndex", flags);
            System.Reflection.PropertyInfo minScaleProp = gvType.GetProperty("minScale", flags);
            System.Reflection.MethodInfo snapZoomMethod = gvType.GetMethod("SnapZoom", flags);
            System.Reflection.MethodInfo repaintMethod = gvType.GetMethod("Repaint", flags);

            foreach (var gv in gvsList)
            {
                if (gv == null) continue;

                if (sizeCb != null)
                {
                    sizeCb.Invoke(gv, new object[] { targetIdx, null });
                }
                else if (selProp != null)
                {
                    selProp.SetValue(gv, targetIdx, null);
                }

                // Yakınlaştırma (Scale) kaydırıcısını pencereye tam sığacak minScale değerine sıfırla (1x taşmasını kesin önler)
                if (minScaleProp != null && snapZoomMethod != null)
                {
                    float minScale = (float)minScaleProp.GetValue(gv, null);
                    snapZoomMethod.Invoke(gv, new object[] { minScale });
                }

                repaintMethod?.Invoke(gv, null);
            }
        }
        catch { }
    }
#endif

    private int _lastMenuScreenW = -1;
    private int _lastMenuScreenH = -1;

    private void Update()
    {
        // Dikey 1v1 modundan ana menüye dönüldüğünde ekran çözünürlüğü 1-2 kare gecikmeli dönerse Canvas ölçeğini anında senkronize et
        if (Screen.width != _lastMenuScreenW || Screen.height != _lastMenuScreenH)
        {
            _lastMenuScreenW = Screen.width;
            _lastMenuScreenH = Screen.height;
            ApplyScreenOrientationAndCanvas(portraitDuel: false);
        }

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

        // 5. MODLAR (AYARLAR butonunun hemen altında, arka plan görselindeki MODLAR kartına piksel piksel oturan şeffaf etkileşimli buton)
        Transform mModesBtn = mainPanel?.transform.Find("ModesButton") ?? mainPanel?.transform.Find("ModesButon");
        GameObject mModesObj = mModesBtn != null ? mModesBtn.gameObject : new GameObject("ModesButton", typeof(RectTransform));
        mModesObj.transform.SetParent(mainPanel.transform, false);
        mModesObj.SetActive(true);
        modesButton = mModesObj.GetComponent<Button>() ?? mModesObj.AddComponent<Button>();
        FormatPixelExactButton(modesButton, new Vector2(-472.2f, -360.0f), new Vector2(487.4f, 97.8f), new Color(0f, 0.85f, 1f, 0.16f), new Color(0f, 0.85f, 1f, 0.35f), OpenModesPanel);

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

        PlayerProfileUI.Instance?.ShowMenuStatusBar(showGoldBadge: true);

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
        FormatPixelExactButton(_dailyOpsButton, new Vector2(542.5f, -452.8f), new Vector2(415.0f, 165.5f), new Color(1f, 0.78f, 0.18f, 0.16f), new Color(1f, 0.78f, 0.18f, 0.35f), OpenDailyOpsPanel);

        // GÖREVLER ve ETKİNLİKLER butonlarının yanındaki SKOR TABLOSU butonu (1920x1080 exact: x = 1718..1902, y = 936..987 -> center = (850.0, -421.0), size = (184.0, 56.0))
        Transform existingLbTab = mainPanel.transform.Find("LeaderboardTabButton");
        GameObject lbTabObj = existingLbTab != null ? existingLbTab.gameObject : new GameObject("LeaderboardTabButton", typeof(RectTransform));
        lbTabObj.transform.SetParent(mainPanel.transform, false);
        lbTabObj.SetActive(true);

        Button lbTabBtn = lbTabObj.GetComponent<Button>() ?? lbTabObj.AddComponent<Button>();
        FormatPixelExactButton(lbTabBtn, new Vector2(850.0f, -421.0f), new Vector2(184.0f, 56.0f), new Color(0f, 0.88f, 1f, 0.18f), new Color(0f, 0.88f, 1f, 0.38f), () => OpenLeaderboardPanel());
        lbTabObj.transform.SetAsLastSibling();

        // ETKİNLİKLER sekmesi butonu -> 7 Günlük Giriş Ödülü Takvimini açar (center = (668.0, -421.0), size = (115.0, 56.0))
        Transform existingEvTab = mainPanel.transform.Find("EventsTabButton");
        GameObject evTabObj = existingEvTab != null ? existingEvTab.gameObject : new GameObject("EventsTabButton", typeof(RectTransform));
        evTabObj.transform.SetParent(mainPanel.transform, false);
        evTabObj.SetActive(true);

        Button evTabBtn = evTabObj.GetComponent<Button>() ?? evTabObj.AddComponent<Button>();
        FormatPixelExactButton(evTabBtn, new Vector2(668.0f, -421.0f), new Vector2(115.0f, 56.0f), new Color(1f, 0.82f, 0.22f, 0.18f), new Color(1f, 0.82f, 0.22f, 0.38f), () => DailyLoginStreakUI.EnsureUI()?.OpenCalendarModal());
        evTabObj.transform.SetAsLastSibling();

        // Canlı kırmızı bildirim rozeti (altın madalya simgesinin sağ üstü: 1920x1080 -> (380.5, -450.5), 30x30)
        Transform existingBadge = mainPanel.transform.Find("DailyOpsDynamicBadge");
        _dailyOpsBadgeObj = existingBadge != null ? existingBadge.gameObject : new GameObject("DailyOpsDynamicBadge", typeof(RectTransform));
        _dailyOpsBadgeObj.transform.SetParent(mainPanel.transform, false);
        HangarUI.SetNormalizedRect1080p(_dailyOpsBadgeObj.GetComponent<RectTransform>(), new Vector2(380.5f, -450.5f), new Vector2(30f, 30f));

        Image badgeImg = _dailyOpsBadgeObj.GetComponent<Image>() ?? _dailyOpsBadgeObj.AddComponent<Image>();
        badgeImg.sprite = GetRoundedBoxSprite("daily_ops_red_badge", new Color(0.82f, 0.16f, 0.22f, 1f), new Color(1f, 0.38f, 0.42f, 0.92f), cornerRadius: 30, borderWidth: 1);
        badgeImg.type = Image.Type.Sliced;
        badgeImg.color = Color.white;
        badgeImg.raycastTarget = false;

        Transform badgeTxtChild = _dailyOpsBadgeObj.transform.Find("CountText");
        GameObject badgeTxtObj = badgeTxtChild != null ? badgeTxtChild.gameObject : new GameObject("CountText", typeof(RectTransform));
        badgeTxtObj.transform.SetParent(_dailyOpsBadgeObj.transform, false);
        RectTransform btRt = badgeTxtObj.GetComponent<RectTransform>() ?? badgeTxtObj.AddComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        _dailyOpsBadgeTMP = badgeTxtObj.GetComponent<TextMeshProUGUI>() ?? badgeTxtObj.AddComponent<TextMeshProUGUI>();
        _dailyOpsBadgeTMP.font = GetTacticalFont();
        _dailyOpsBadgeTMP.fontSize = 17f;
        _dailyOpsBadgeTMP.fontStyle = FontStyles.Bold;
        _dailyOpsBadgeTMP.alignment = TextAlignmentOptions.Center;
        _dailyOpsBadgeTMP.color = Color.white;
        _dailyOpsBadgeTMP.raycastTarget = false;

        // Dinamik "X YENİ ÖDÜL" alt yazısı (GÖREVLER & MADALYALAR'ın hemen sağ altı: (617, -499), 220x24)
        Transform existingRewardTxt = mainPanel.transform.Find("DailyOpsDynamicRewardText");
        GameObject rewardTxtObj = existingRewardTxt != null ? existingRewardTxt.gameObject : new GameObject("DailyOpsDynamicRewardText", typeof(RectTransform));
        rewardTxtObj.transform.SetParent(mainPanel.transform, false);
        HangarUI.SetNormalizedRect1080p(rewardTxtObj.GetComponent<RectTransform>(), new Vector2(617f, -499.0f), new Vector2(220f, 24f));

        _dailyOpsRewardTextTMP = rewardTxtObj.GetComponent<TextMeshProUGUI>() ?? rewardTxtObj.AddComponent<TextMeshProUGUI>();
        _dailyOpsRewardTextTMP.font = GetTacticalFont();
        _dailyOpsRewardTextTMP.fontSize = 16f;
        _dailyOpsRewardTextTMP.characterSpacing = 1.2f;
        _dailyOpsRewardTextTMP.fontStyle = FontStyles.Bold;
        _dailyOpsRewardTextTMP.alignment = TextAlignmentOptions.MidlineRight;
        _dailyOpsRewardTextTMP.color = new Color(0.75f, 0.82f, 0.88f, 0.95f);
        _dailyOpsRewardTextTMP.raycastTarget = false;

        try
        {
            var opsMgr = DailyOpsManager.EnsureInstance();
            if (opsMgr != null)
            {
                opsMgr.OnDailyOpsChanged -= RefreshDailyOpsButtonVisual;
                opsMgr.OnDailyOpsChanged += RefreshDailyOpsButtonVisual;
            }
        }
        catch { }

        RefreshDailyOpsButtonVisual();
    }

    public void RefreshDailyOpsButtonVisual()
    {
        int claimable = 0;
        try { claimable = DailyOpsManager.EnsureInstance()?.GetTotalClaimableCount() ?? 0; } catch { }
        try { if (DailyLoginStreakUI.CanClaimToday()) claimable += 1; } catch { }

        if (_dailyOpsBadgeObj != null)
        {
            _dailyOpsBadgeObj.SetActive(claimable > 0);
            if (claimable > 0)
            {
                _dailyOpsBadgeObj.transform.SetAsLastSibling();
                if (_dailyOpsBadgeTMP != null) _dailyOpsBadgeTMP.text = claimable.ToString();
            }
        }

        if (_dailyOpsRewardTextTMP != null)
        {
            _dailyOpsRewardTextTMP.gameObject.SetActive(claimable > 0);
            if (claimable > 0)
            {
                _dailyOpsRewardTextTMP.transform.SetAsLastSibling();
                _dailyOpsRewardTextTMP.text = $"{claimable} YENİ ÖDÜL";
            }
        }

        if (_missionsFromModesButton != null)
        {
            TextMeshProUGUI mTmp = _missionsFromModesButton.GetComponentInChildren<TextMeshProUGUI>();
            if (mTmp != null)
            {
                mTmp.text = claimable > 0
                    ? $"<b>GÖREVLER <color=#FF3B30>({claimable})</color></b>"
                    : "<b>GÖREVLER</b>";
            }
        }
    }

    public bool IsAnyMenuPanelOpen()
    {
        if (HangarUI.Instance != null && HangarUI.Instance.gameObject.activeInHierarchy) return true;
        if (MilitaryStoreUI.Instance != null && MilitaryStoreUI.Instance.gameObject.activeInHierarchy) return true;
        if (CratesUI.Instance != null && CratesUI.Instance.gameObject.activeInHierarchy) return true;
        if (InventoryUI.Instance != null && InventoryUI.Instance.gameObject.activeInHierarchy) return true;
        if (LeaderboardUI.Instance != null && LeaderboardUI.Instance.gameObject.activeInHierarchy) return true;
        if (DailyOpsUI.Instance != null && DailyOpsUI.Instance.gameObject.activeInHierarchy) return true;

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

        RectTransform rt = titleObj.GetComponent<RectTransform>() ?? titleObj.AddComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -28f);
            rt.sizeDelta = new Vector2(700f, 60f);
        }

        Image bgImg = titleObj.GetComponent<Image>() ?? titleObj.AddComponent<Image>();
        bgImg.sprite = null;
        bgImg.color = new Color(0, 0, 0, 0);
        bgImg.raycastTarget = false;

        Transform textChild = titleObj.transform.Find("TitleText");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("TitleText", typeof(RectTransform));
        textObj.transform.SetParent(titleObj.transform, false);
        RectTransform tRt = textObj.GetComponent<RectTransform>() ?? textObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>() ?? textObj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = "<b>AIR BATTLE</b>";
        tmp.fontSize = 46f;
        tmp.characterSpacing = 3.0f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.extraPadding = true;
        tmp.raycastTarget = false;
        SetOutlineSafe(tmp, 0.12f, new Color(0.01f, 0.02f, 0.04f, 0.95f));
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

        // Ana menüde de giriş yapan oyuncunun canlı Seviye, EXP Barı ve Altın rozetini büyük ve net olarak göster
        PlayerProfileUI.EnsureUI()?.ShowMenuStatusBar(showGoldBadge: true);

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
            MakeFullScreenRect(modesPanel.GetComponent<RectTransform>());
            Image mImg = modesPanel.GetComponent<Image>() ?? modesPanel.AddComponent<Image>();
            if (mImg != null)
            {
                mImg.sprite = null;
                mImg.color = new Color(0.03f, 0.06f, 0.10f, 0.90f);
                mImg.raycastTarget = true;
            }
            SetupModesHeader();
            SetupCampaignSelectionUI();
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

        // Sahnedeki tüm eski/kalıntı buton ve metinleri (SoloButon, OnlineButon, varsayılan beyaz Button, SelectedModeText vb.) kesin olarak gizle
        Transform keepBack = backFromModesButton != null ? backFromModesButton.transform : null;
        for (int i = 0; i < modesPanel.transform.childCount; i++)
        {
            Transform child = modesPanel.transform.GetChild(i);
            if (child == null) continue;
            string cname = child.name;
            if (cname != "ModesHeaderTitle"
                && cname != "ModesLeftColumn"
                && cname != "ModesDetailCard"
                && cname != "BackFromModesButton"
                && cname != "QuitFromModesButton"
                && cname != "MissionsFromModesButton"
                && child != keepBack)
            {
                child.gameObject.SetActive(false);
            }
        }
        if (selectedModeTMP != null) selectedModeTMP.gameObject.SetActive(false);

        // ==========================================
        // 1. SOL KOLON (HİKAYE BÖLÜMLERİ & ÖZEL MODLAR)
        // ==========================================
        Transform leftCol = modesPanel.transform.Find("ModesLeftColumn");
        GameObject leftColObj = leftCol != null ? leftCol.gameObject : new GameObject("ModesLeftColumn", typeof(RectTransform));
        leftColObj.transform.SetParent(modesPanel.transform, false);
        leftColObj.SetActive(true);

        RectTransform lRt = leftColObj.GetComponent<RectTransform>() ?? leftColObj.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0.5f, 0.5f);
        lRt.anchorMax = new Vector2(0.5f, 0.5f);
        lRt.pivot = new Vector2(0.5f, 0.5f);
        lRt.anchoredPosition = new Vector2(-340f, 15f);
        lRt.sizeDelta = new Vector2(1080f, 760f);

        // Başlık 1: HİKAYE BÖLÜMLERİ
        CreateModesSectionHeader(leftColObj.transform, "StorySectionHeader", new Vector2(0f, 340f), "HİKAYE BÖLÜMLERİ");

        // 3 Bölüm Kartı
        _campaign1Button = CreateOrSetupModeCard(leftColObj.transform, "Campaign1Btn", new Vector2(0f, 285f), new Vector2(1080f, 60f),
            "01   |   OPERASYON KARA ŞAFAK", isLeftAligned: true,
            () => SetCampaign(StoryCampaignManager.CampaignType.OperationBlackDawn));
        soloButton = _campaign1Button;

        _campaign2Button = CreateOrSetupModeCard(leftColObj.transform, "Campaign2Btn", new Vector2(0f, 215f), new Vector2(1080f, 60f),
            "02   |   PROJE KİMERAS", isLeftAligned: true,
            () => SetCampaign(StoryCampaignManager.CampaignType.ProjectChimera));

        _campaign3Button = CreateOrSetupModeCard(leftColObj.transform, "Campaign3Btn", new Vector2(0f, 145f), new Vector2(1080f, 60f),
            "03   |   GÖKYÜZÜ PARALI ASKERİ", isLeftAligned: true,
            () => SetCampaign(StoryCampaignManager.CampaignType.SkyMercenary));

        // Başlık 2: ÖZEL MODLAR
        CreateModesSectionHeader(leftColObj.transform, "SpecialSectionHeader", new Vector2(0f, 80f), "ÖZEL MODLAR");

        // 2x3 Grid (6 Özel Mod Kartı)
        // Satır 1
        _endlessButton = CreateOrSetupModeCard(leftColObj.transform, "EndlessModeBtn", new Vector2(-276f, 20f), new Vector2(528f, 64f),
            "SONSUZ İT DALAŞI", isLeftAligned: false,
            () => SetMode(GameSettings.GameMode.Endless));

        _bossRushButton = CreateOrSetupModeCard(leftColObj.transform, "BossRushBtn", new Vector2(276f, 20f), new Vector2(528f, 64f),
            "BOSS RUSH: 4 AS", isLeftAligned: false,
            () => SetMode(GameSettings.GameMode.BossRush));

        // Satır 2
        _bulletHellButton = CreateOrSetupModeCard(leftColObj.transform, "BulletHellBtn", new Vector2(-276f, -56f), new Vector2(528f, 64f),
            "MERMİ FIRTINASI", isLeftAligned: false,
            () => SetMode(GameSettings.GameMode.BulletHell));

        _timeAttackButton = CreateOrSetupModeCard(leftColObj.transform, "TimeAttackBtn", new Vector2(276f, -56f), new Vector2(528f, 64f),
            "ZAMAN TAARRUZU", isLeftAligned: false,
            () => SetMode(GameSettings.GameMode.TimeAttack));

        // Satır 3
        _hardcoreButton = CreateOrSetupModeCard(leftColObj.transform, "HardcoreModeBtn", new Vector2(-276f, -132f), new Vector2(528f, 64f),
            "HARDCORE: 1 CAN BOSS", isLeftAligned: false,
            () => SetMode(GameSettings.GameMode.Hardcore));

        onlineButton = CreateOrSetupModeCard(leftColObj.transform, "OnlineButton", new Vector2(276f, -132f), new Vector2(528f, 64f),
            "2 OYUNCULU İT DALAŞI", isLeftAligned: false,
            () => SetMode(GameSettings.GameMode.Online));

        // ==========================================
        // 2. SAĞ DETAY & HAREKÂT BAŞLAT KARTI
        // ==========================================
        Transform cardTrans = modesPanel.transform.Find("ModesDetailCard");
        GameObject cardObj = cardTrans != null ? cardTrans.gameObject : new GameObject("ModesDetailCard", typeof(RectTransform));
        cardObj.transform.SetParent(modesPanel.transform, false);
        cardObj.SetActive(true);

        RectTransform cRt = cardObj.GetComponent<RectTransform>() ?? cardObj.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(575f, 75f);
        cRt.sizeDelta = new Vector2(610f, 750f);

        Image cardBg = cardObj.GetComponent<Image>() ?? cardObj.AddComponent<Image>();
        cardBg.sprite = GetRoundedBoxSprite("modes_card_bg", new Color(0.05f, 0.09f, 0.13f, 0.96f), new Color(0.14f, 0.22f, 0.30f, 0.95f), cornerRadius: 16, borderWidth: 2);
        cardBg.type = Image.Type.Sliced;
        cardBg.color = Color.white;

        // Üst Başlık: SEÇİLİ MOD
        CreateCardSubLabel(cardObj.transform, "CardHeaderLabel", new Vector2(0f, 325f), new Vector2(530f, 24f), "SEÇİLİ MOD", 15f);

        // Mod Başlığı (Large bold white)
        _modesCardTitleTMP = CreateCardDynamicText(cardObj.transform, "CardTitleText", new Vector2(0f, 275f), new Vector2(530f, 50f), "SONSUZ İT DALAŞI", 30f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);

        // Rekor Rozeti (Pill)
        Transform pillTrans = cardObj.transform.Find("CardRecordPill");
        GameObject pillObj = pillTrans != null ? pillTrans.gameObject : new GameObject("CardRecordPill", typeof(RectTransform));
        pillObj.transform.SetParent(cardObj.transform, false);
        pillObj.SetActive(true);

        RectTransform pRt = pillObj.GetComponent<RectTransform>() ?? pillObj.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.anchoredPosition = new Vector2(-95f, 215f);
        pRt.sizeDelta = new Vector2(340f, 36f);

        Image pillBg = pillObj.GetComponent<Image>() ?? pillObj.AddComponent<Image>();
        pillBg.sprite = GetRoundedBoxSprite("modes_pill_bg", new Color(0.10f, 0.16f, 0.23f, 0.95f), new Color(0.16f, 0.26f, 0.36f, 0.90f), cornerRadius: 14, borderWidth: 1);
        pillBg.type = Image.Type.Sliced;
        pillBg.color = Color.white;

        _modesCardRecordTMP = CreateCardDynamicText(pillObj.transform, "RecordPillText", Vector2.zero, new Vector2(320f, 32f), "En Yüksek Rekor: Dalga 10", 16f, FontStyles.Bold, new Color(0.62f, 0.76f, 0.88f), TextAlignmentOptions.Center);

        // Açıklama Metni (Multi-line)
        _modesCardDescTMP = CreateCardDynamicText(cardObj.transform, "CardDescText", new Vector2(0f, 110f), new Vector2(530f, 130f), "Düşman filoları durmaksızın gelir!\nHer 5 dalgada bir Boss savaşı başlar.", 19f, FontStyles.Normal, new Color(0.56f, 0.66f, 0.76f), TextAlignmentOptions.TopLeft);
        _modesCardDescTMP.lineSpacing = 1.25f;
        _briefingTMP = _modesCardDescTMP;

        // Başlık: ZORLUK
        CreateCardSubLabel(cardObj.transform, "CardDiffLabel", new Vector2(0f, -25f), new Vector2(530f, 24f), "ZORLUK", 15f);

        // Zorluk Butonları (KOLAY, NORMAL, ZOR)
        _modesDiffEasyBtn = CreateOrSetupDiffButton(cardObj.transform, "ModesDiffEasyBtn", new Vector2(-180f, -75f), new Vector2(170f, 50f), "KOLAY", () => SetDifficulty(GameSettings.Difficulty.Kolay));
        _modesDiffNormalBtn = CreateOrSetupDiffButton(cardObj.transform, "ModesDiffNormalBtn", new Vector2(0f, -75f), new Vector2(170f, 50f), "NORMAL", () => SetDifficulty(GameSettings.Difficulty.Normal));
        _modesDiffHardBtn = CreateOrSetupDiffButton(cardObj.transform, "ModesDiffHardBtn", new Vector2(180f, -75f), new Vector2(170f, 50f), "ZOR", () => SetDifficulty(GameSettings.Difficulty.Zor));

        // HAREKÂTI BAŞLAT Butonu
        _modesLaunchCombatBtn = CreateOrSetupLaunchButton(cardObj.transform, "ModesLaunchCombatBtn", new Vector2(0f, -185f), new Vector2(530f, 72f), "HAREKÂTI BAŞLAT", OnPlayClicked);

        // ==========================================
        // 3. ALT BAR (GERİ DÖN, OYUNDAN ÇIK, GÖREVLER)
        // ==========================================
        if (backFromModesButton == null)
        {
            Transform existingBack = modesPanel.transform.Find("BackFromModesButton") ?? modesPanel.transform.Find("BackFromModesButon");
            if (existingBack != null) backFromModesButton = existingBack.GetComponent<Button>();
            if (backFromModesButton == null)
            {
                GameObject bObj = new GameObject("BackFromModesButton", typeof(RectTransform));
                bObj.transform.SetParent(modesPanel.transform, false);
                backFromModesButton = bObj.AddComponent<Button>();
            }
        }
        backFromModesButton.gameObject.SetActive(true);
        FormatBottomBarButton(backFromModesButton, new Vector2(-780f, -485f), new Vector2(240f, 52f), "« GERİ DÖN", OpenMainPanel);

        if (_quitFromModesButton == null)
        {
            Transform existingQuit = modesPanel.transform.Find("QuitFromModesButton");
            if (existingQuit != null) _quitFromModesButton = existingQuit.GetComponent<Button>();
            if (_quitFromModesButton == null)
            {
                GameObject qObj = new GameObject("QuitFromModesButton", typeof(RectTransform));
                qObj.transform.SetParent(modesPanel.transform, false);
                _quitFromModesButton = qObj.AddComponent<Button>();
            }
        }
        _quitFromModesButton.gameObject.SetActive(true);
        FormatBottomBarButton(_quitFromModesButton, new Vector2(-510f, -485f), new Vector2(240f, 52f), "OYUNDAN ÇIK", () =>
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        });

        if (_missionsFromModesButton == null)
        {
            Transform existingMissions = modesPanel.transform.Find("MissionsFromModesButton");
            if (existingMissions != null) _missionsFromModesButton = existingMissions.GetComponent<Button>();
            if (_missionsFromModesButton == null)
            {
                GameObject mObj = new GameObject("MissionsFromModesButton", typeof(RectTransform));
                mObj.transform.SetParent(modesPanel.transform, false);
                _missionsFromModesButton = mObj.AddComponent<Button>();
            }
        }
        _missionsFromModesButton.gameObject.SetActive(true);
        int claimableOps = 0;
        try { claimableOps = DailyOpsManager.EnsureInstance()?.GetTotalClaimableCount() ?? 0; } catch { }
        string missionsLabel = claimableOps > 0 ? $"GÖREVLER <color=#FF3B30>({claimableOps})</color>" : "GÖREVLER";
        FormatBottomBarButton(_missionsFromModesButton, new Vector2(760f, -485f), new Vector2(240f, 52f), missionsLabel, OpenDailyOpsPanel);
    }

    private void CreateModesSectionHeader(Transform parent, string name, Vector2 pos, string title)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1080f, 28f);

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>() ?? obj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = title;
        tmp.fontSize = 17f;
        tmp.characterSpacing = 1.4f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = new Color(0.44f, 0.54f, 0.62f);
        tmp.raycastTarget = false;
    }

    private void CreateCardSubLabel(Transform parent, string name, Vector2 pos, Vector2 size, string text, float fontSize)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>() ?? obj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.characterSpacing = 1.2f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = new Color(0.44f, 0.54f, 0.62f);
        tmp.raycastTarget = false;
    }

    private TextMeshProUGUI CreateCardDynamicText(Transform parent, string name, Vector2 pos, Vector2 size, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>() ?? obj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.characterSpacing = 1.0f;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.extraPadding = true;
        return tmp;
    }

    private Button CreateOrSetupModeCard(Transform parent, string objName, Vector2 pos, Vector2 size, string text, bool isLeftAligned, UnityEngine.Events.UnityAction onClick)
    {
        Transform existing = parent.Find(objName);
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject(objName, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>() ?? btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
        img.sprite = GetRoundedBoxSprite("modes_card_unselected", new Color(0.06f, 0.10f, 0.15f, 0.88f), new Color(0.16f, 0.25f, 0.33f, 0.90f), 10, 1);
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        cb.pressedColor = new Color(0.8f, 0.9f, 1f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.06f;
        btn.colors = cb;

        Transform tChild = btnObj.transform.Find("Text");
        GameObject tObj = tChild != null ? tChild.gameObject : new GameObject("Text", typeof(RectTransform));
        tObj.transform.SetParent(btnObj.transform, false);

        RectTransform trt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = isLeftAligned ? new Vector2(28f, 0f) : Vector2.zero;
        trt.offsetMax = isLeftAligned ? new Vector2(-28f, 0f) : Vector2.zero;

        TextMeshProUGUI tmp = tObj.GetComponent<TextMeshProUGUI>() ?? tObj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = 21f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = isLeftAligned ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
        tmp.color = new Color(0.82f, 0.88f, 0.96f, 0.90f);
        tmp.raycastTarget = false;

        if (onClick != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                onClick();
                AudioManager.Instance?.PlayButtonClick();
            });
        }
        return btn;
    }

    private Button CreateOrSetupDiffButton(Transform parent, string objName, Vector2 pos, Vector2 size, string text, UnityEngine.Events.UnityAction onClick)
    {
        Transform existing = parent.Find(objName);
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject(objName, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>() ?? btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
        img.sprite = GetRoundedBoxSprite("modes_diff_inactive", new Color(0.07f, 0.12f, 0.18f, 0.90f), new Color(0.15f, 0.24f, 0.33f, 0.85f), 8, 1);
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        Transform tChild = btnObj.transform.Find("Text");
        GameObject tObj = tChild != null ? tChild.gameObject : new GameObject("Text", typeof(RectTransform));
        tObj.transform.SetParent(btnObj.transform, false);

        RectTransform trt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = tObj.GetComponent<TextMeshProUGUI>() ?? tObj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.68f, 0.78f, 0.88f, 0.90f);
        tmp.raycastTarget = false;

        if (onClick != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                onClick();
                AudioManager.Instance?.PlayButtonClick();
            });
        }
        return btn;
    }

    private Button CreateOrSetupLaunchButton(Transform parent, string objName, Vector2 pos, Vector2 size, string text, UnityEngine.Events.UnityAction onClick)
    {
        Transform existing = parent.Find(objName);
        GameObject btnObj = existing != null ? existing.gameObject : new GameObject(objName, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>() ?? btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
        img.sprite = GetRoundedBoxSprite("modes_launch_btn", new Color(0.29f, 0.88f, 0.96f, 1.0f), new Color(0.45f, 0.95f, 1.00f, 1.0f), 10, 1);
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        Transform tChild = btnObj.transform.Find("Text");
        GameObject tObj = tChild != null ? tChild.gameObject : new GameObject("Text", typeof(RectTransform));
        tObj.transform.SetParent(btnObj.transform, false);

        RectTransform trt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = tObj.GetComponent<TextMeshProUGUI>() ?? tObj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = $"<b>{text}</b>";
        tmp.fontSize = 24f;
        tmp.characterSpacing = 1.6f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.04f, 0.08f, 0.12f, 1f);
        tmp.raycastTarget = false;

        if (onClick != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                onClick();
                AudioManager.Instance?.PlayButtonClick();
            });
        }
        return btn;
    }

    private void FormatBottomBarButton(Button btn, Vector2 pos, Vector2 size, string text, UnityEngine.Events.UnityAction onClick)
    {
        if (btn == null) return;
        RectTransform rt = btn.GetComponent<RectTransform>() ?? btn.gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btn.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
        img.sprite = GetRoundedBoxSprite("modes_bottom_btn", new Color(0.06f, 0.10f, 0.15f, 0.88f), new Color(0.16f, 0.25f, 0.33f, 0.90f), 10, 1);
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;
        btn.targetGraphic = img;

        // Sahnedeki varsayılan "Text (TMP)" veya eski "Button" metin çocuklarını tamamen gizle
        for (int i = 0; i < btn.transform.childCount; i++)
        {
            Transform child = btn.transform.GetChild(i);
            if (child != null && child.name != "Text")
                child.gameObject.SetActive(false);
        }

        Transform tChild = btn.transform.Find("Text");
        GameObject tObj = tChild != null ? tChild.gameObject : new GameObject("Text", typeof(RectTransform));
        tObj.transform.SetParent(btn.transform, false);
        tObj.SetActive(true);

        // Eski UnityEngine.UI.Text bileşeni varsa kaldır
        var legacyText = tObj.GetComponent<UnityEngine.UI.Text>();
        if (legacyText != null) Destroy(legacyText);

        RectTransform trt = tObj.GetComponent<RectTransform>() ?? tObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = tObj.GetComponent<TextMeshProUGUI>() ?? tObj.AddComponent<TextMeshProUGUI>();
        tmp.font = GetTacticalFont();
        tmp.text = $"<b>{text}</b>";
        tmp.fontSize = 20f;
        tmp.characterSpacing = 1.2f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.90f, 0.96f, 0.95f);
        tmp.raycastTarget = false;

        if (onClick != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                onClick();
                AudioManager.Instance?.PlayButtonClick();
            });
        }
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

        // 2. Modlar Panelindeki Zorluk Seçim Butonları (Sağ Detay Kartı)
        UpdateCardDiffButtonVisual(_modesDiffEasyBtn, "KOLAY", diff == GameSettings.Difficulty.Kolay, new Color(0.12f, 0.45f, 0.25f, 1f));
        UpdateCardDiffButtonVisual(_modesDiffNormalBtn, "NORMAL", diff == GameSettings.Difficulty.Normal, new Color(0.55f, 0.40f, 0.10f, 1f));
        UpdateCardDiffButtonVisual(_modesDiffHardBtn, "ZOR", diff == GameSettings.Difficulty.Zor, new Color(0.55f, 0.12f, 0.18f, 1f));

        // 3. Modlar Panelindeki 9 Mod Kartının Seçim Vurgusu (Neon Cyan Çerçeve)
        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);
        bool isEndless = (GameSettings.SelectedMode == GameSettings.GameMode.Endless);
        bool isBossRush = (GameSettings.SelectedMode == GameSettings.GameMode.BossRush);
        bool isBulletHell = (GameSettings.SelectedMode == GameSettings.GameMode.BulletHell);
        bool isTimeAttack = (GameSettings.SelectedMode == GameSettings.GameMode.TimeAttack);
        bool isHardcore = (GameSettings.SelectedMode == GameSettings.GameMode.Hardcore);
        bool isSolo = (GameSettings.SelectedMode == GameSettings.GameMode.Solo);
        var activeCamp = StoryCampaignManager.CurrentCampaign;

        UpdateModeCardVisual(_campaign1Button, isSolo && activeCamp == StoryCampaignManager.CampaignType.OperationBlackDawn, "01   |   OPERASYON KARA ŞAFAK", isLeftAligned: true);
        UpdateModeCardVisual(_campaign2Button, isSolo && activeCamp == StoryCampaignManager.CampaignType.ProjectChimera, "02   |   PROJE KİMERAS", isLeftAligned: true);
        UpdateModeCardVisual(_campaign3Button, isSolo && activeCamp == StoryCampaignManager.CampaignType.SkyMercenary, "03   |   GÖKYÜZÜ PARALI ASKERİ", isLeftAligned: true);
        UpdateModeCardVisual(_endlessButton, isEndless, "SONSUZ İT DALAŞI", isLeftAligned: false);
        UpdateModeCardVisual(_bossRushButton, isBossRush, "BOSS RUSH: 4 AS", isLeftAligned: false);
        UpdateModeCardVisual(_bulletHellButton, isBulletHell, "MERMİ FIRTINASI", isLeftAligned: false);
        UpdateModeCardVisual(_timeAttackButton, isTimeAttack, "ZAMAN TAARRUZU", isLeftAligned: false);
        UpdateModeCardVisual(_hardcoreButton, isHardcore, "HARDCORE: 1 CAN BOSS", isLeftAligned: false);
        UpdateModeCardVisual(onlineButton, isOnline, "2 OYUNCULU İT DALAŞI", isLeftAligned: false);

        // 4. Sağ Detay Kartı Dinamik Başlık, Rekor Rozeti ve Brifing Açıklaması
        string cardTitle = "";
        string cardRecord = "";
        string cardDesc = "";

        if (isSolo)
        {
            switch (activeCamp)
            {
                case StoryCampaignManager.CampaignType.OperationBlackDawn:
                    cardTitle = "OPERASYON KARA ŞAFAK";
                    cardRecord = "Bölüm 1 • 20 Dalga & 4 Boss";
                    cardDesc = "Vatan hava sahasını işgal eden düşman filolarına karşı şafak taarruzu. Müttefik üslerini savun ve düşman komuta zeplinini düşür!";
                    break;
                case StoryCampaignManager.CampaignType.ProjectChimera:
                    cardTitle = "PROJE KİMERAS";
                    cardRecord = "Bölüm 2 • 20 Dalga & 4 Boss";
                    cardDesc = "Yapay zeka kontrollü otonom avcı filoları isyan etti! Siber harp sinyallerini kır ve deneysel prototip jetleri düşür.";
                    break;
                case StoryCampaignManager.CampaignType.SkyMercenary:
                    cardTitle = "GÖKYÜZÜ PARALI ASKERİ";
                    cardRecord = "Bölüm 3 • 20 Dalga & 4 Boss";
                    cardDesc = "Hava korsanları ticaret koridorlarını ele geçirdi. Ağır silahlı korsan aslarını avla ve gökyüzü ödüllerini topla!";
                    break;
            }
        }
        else if (isEndless)
        {
            int bestWave = ProfileManager.GetInt("AirBattle_EndlessHighScore", 1);
            cardTitle = "SONSUZ İT DALAŞI";
            cardRecord = $"En Yüksek Rekor: Dalga {bestWave}";
            cardDesc = "Düşman filoları durmaksızın gelir!\nHer 5 dalgada bir Boss savaşı başlar.";
        }
        else if (isBossRush)
        {
            cardTitle = "BOSS RUSH: 4 AS";
            cardRecord = "Özel Ödül: +1500 Altın";
            cardDesc = "Sıradan uçaklar yok! En güçlü 4 as düşman ardı ardına saldırır. Uçuş manevralarını ve silahlarını en üst düzeye çıkar!";
        }
        else if (isBulletHell)
        {
            float bestTime = ProfileManager.GetFloat("AirBattle_BulletHellBestTime", 0f);
            int m = (int)(bestTime / 60);
            int s = (int)(bestTime % 60);
            cardTitle = "MERMİ FIRTINASI";
            cardRecord = $"En İyi Süre: {m:00}:{s:00}";
            cardDesc = "Spiral mermi fırtınaları, lazer tuzakları ve aşırı cehennem salvosu! Ne kadar dayanabilirsin?";
        }
        else if (isTimeAttack)
        {
            int bestKills = ProfileManager.GetInt("AirBattle_TimeAttackHighScore", 0);
            cardTitle = "ZAMAN TAARRUZU";
            cardRecord = $"En Çok Av: {bestKills} Uçak";
            cardDesc = "60 saniyeyle başla! Her uçak +4 sn, Bosslar +8 sn ekler! Durmaksızın avlan!";
        }
        else if (isHardcore)
        {
            int bestHc = ProfileManager.GetInt("AirBattle_HardcoreHighScore", 0);
            cardTitle = "HARDCORE: 1 CAN BOSS";
            cardRecord = $"Rekor: {bestHc} Boss Avı";
            cardDesc = "Sadece 1 Can Hakkı! Uçağın 200 HP takviyeli gövdeye sahip. Karşına çıkan her uçak giderek güçlenen bir Boss!";
        }
        else if (isOnline)
        {
            cardTitle = "2 OYUNCULU İT DALAŞI";
            cardRecord = "Yerel Çok Oyunculu Düello";
            cardDesc = "Aynı cihazda iki pilot karşı karşıya!\n1. Pilot alt ekranda, 2. Pilot 180° ters üst ekranda manevra yapar.";
        }

        if (_modesCardTitleTMP != null) _modesCardTitleTMP.text = cardTitle;
        if (_modesCardRecordTMP != null) _modesCardRecordTMP.text = cardRecord;
        if (_modesCardDescTMP != null) _modesCardDescTMP.text = cardDesc;
        if (_briefingTMP != null && _briefingTMP != _modesCardDescTMP) _briefingTMP.text = cardDesc;

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

    private void UpdateModeCardVisual(Button btn, bool isSelected, string text, bool isLeftAligned)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = isSelected
                ? GetRoundedBoxSprite("modes_card_selected", new Color(0.07f, 0.15f, 0.22f, 0.96f), new Color(0.00f, 0.90f, 1.00f, 1.00f), 10, 2)
                : GetRoundedBoxSprite("modes_card_unselected", new Color(0.06f, 0.10f, 0.15f, 0.88f), new Color(0.16f, 0.25f, 0.33f, 0.90f), 10, 1);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }

        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
            tmp.font = GetTacticalFont();
            tmp.alignment = isLeftAligned ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            tmp.color = isSelected ? Color.white : new Color(0.68f, 0.78f, 0.88f, 0.85f);
            tmp.characterSpacing = isSelected ? 1.2f : 0.6f;
        }
    }

    private void UpdateCardDiffButtonVisual(Button btn, string label, bool isSelected, Color activeColor)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (img != null)
        {
            string key = isSelected ? $"diff_act_{activeColor.r:F2}_{activeColor.g:F2}_{activeColor.b:F2}" : "modes_diff_inactive";
            img.sprite = isSelected
                ? GetRoundedBoxSprite(key, activeColor, new Color(Mathf.Min(1f, activeColor.r * 1.3f), Mathf.Min(1f, activeColor.g * 1.3f), Mathf.Min(1f, activeColor.b * 1.3f), 1f), 8, 1)
                : GetRoundedBoxSprite("modes_diff_inactive", new Color(0.07f, 0.12f, 0.18f, 0.90f), new Color(0.15f, 0.24f, 0.33f, 0.85f), 8, 1);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        if (tmp != null)
        {
            tmp.text = label;
            tmp.color = isSelected ? Color.white : new Color(0.55f, 0.65f, 0.75f, 0.85f);
            tmp.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
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
