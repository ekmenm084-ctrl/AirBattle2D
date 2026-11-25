using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Bu bileşene sahip UI nesneleri (ve alt nesneleri), genel kokpit teması renk dönüşümünden muaf tutulur
/// (ör. Ayarlar ekranındaki tema önizleme butonlarının her birinin kendi gerçek rengini göstermesi için).
/// </summary>
public class CockpitThemeIgnore : MonoBehaviour
{
}

/// <summary>
/// Tüm Oyun Genelinde Seçilebilir Kokpit Teması Yöneticisi:
/// - Siber Mavi (CyberBlue): Tüm materyaller, ışıklar, metinler ve dokular %100 orijinal halinde kalır.
/// - Radar Yeşili (RadarGreen) / Altın Karbon (StealthGold):
///   1) Tüm menülerdeki (Ana Menü, Ayarlar, Modlar, Hangar, Mağaza, Sandıklar, Envanter, Rekorlar, Görevler,
///      Pilot Dosyası, Pause, GameOver, Upgrade) tüm Image & RawImage bileşenlerine özel tema materyali uygulanır.
///   2) Savaş sahnesindeki fırtına bulutları (BackgroundScroller) ve URP Global Light 2D atmosfer ışığı temaya uyarlanır.
///   3) Tüm TextMeshProUGUI mavi/cyan renkleri ve RichText vurgu etiketleri geri dönüşümlü olarak temaya uyarlanır.
/// </summary>
public class CockpitThemeDirector : MonoBehaviour
{
    public static CockpitThemeDirector Instance { get; private set; }

    private static Shader _cachedUiShader;
    private static Material _radarGreenUiMat;
    private static Material _stealthGoldUiMat;
    private static readonly int ThemeModeProp = Shader.PropertyToID("_ThemeMode");
    private static readonly int MainTexProp = Shader.PropertyToID("_MainTex");

    private readonly HashSet<Graphic> _themedGraphics = new HashSet<Graphic>();
    private readonly Dictionary<TextMeshProUGUI, Color> _originalTmpVertexColors = new Dictionary<TextMeshProUGUI, Color>();
    private static readonly Dictionary<string, Sprite> _themedWorldSpriteCache = new Dictionary<string, Sprite>();

    private Color? _originalCameraBgColor;
    private readonly Dictionary<Component, Color> _originalLight2DColors = new Dictionary<Component, Color>();
    private static System.Type _light2DType;
    private static PropertyInfo _light2DColorProp;
    private static PropertyInfo _light2DTypeProp;
    private static bool _light2DReflectionResolved;

    private float _nextScanTime = 0f;

    // Geri dönüşümlü benzersiz zengin metin (RichText) renk kodları (zorluk/altın kodlarıyla çakışmaz)
    private const string GREEN_BRIGHT_HEX = "#23E66F";
    private const string GREEN_SOFT_HEX   = "#7CE8A3";
    private const string GOLD_BRIGHT_HEX  = "#F7B733";
    private const string GOLD_SOFT_HEX    = "#FAD375";

    private static readonly string[] CyanBrightHexes = { "#00E5FF", "#00F0FF", "#22D3EE", "#38BDF8", "#08D1EC", "#00FFAA" };
    private static readonly string[] CyanSoftHexes   = { "#7DD3FC", "#60A5FA", "#93C5FD", "#82AABF", "#5EEAD4", "#E0F2FE" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static CockpitThemeDirector EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("CockpitThemeDirector");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<CockpitThemeDirector>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        GameSettings.LoadSettings();
        GameSettings.OnCockpitThemeChanged += HandleThemeChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        ApplyThemeNow();
    }

    private void OnDestroy()
    {
        GameSettings.OnCockpitThemeChanged -= HandleThemeChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _originalCameraBgColor = null;
        _originalLight2DColors.Clear();
        ApplyThemeNow();
    }

    private void HandleThemeChanged(GameSettings.CockpitTheme _)
    {
        ApplyThemeNow();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextScanTime)
        {
            _nextScanTime = Time.unscaledTime + 0.10f;
            SyncActiveSceneVisuals(includeInactive: false);
        }
    }

    public void ApplyThemeNow()
    {
        _nextScanTime = Time.unscaledTime + 0.10f;
        SyncActiveSceneVisuals(includeInactive: true);

        // Savaş arka planı bulut akışını ve oyuncu kalkanını anında güncelle
        var scrollers = FindObjectsByType<BackgroundScroller>(FindObjectsInactive.Include);
        for (int i = 0; i < scrollers.Length; i++)
        {
            if (scrollers[i] != null) scrollers[i].RefreshThemeSprite();
        }

        if (PlayerUpgradeManager.Instance != null)
        {
            PlayerUpgradeManager.Instance.RefreshShieldThemeVisual();
        }
    }

    private static Material GetThemeMaterial(GameSettings.CockpitTheme theme)
    {
        if (theme == GameSettings.CockpitTheme.CyberBlue)
            return null;

        if (_cachedUiShader == null)
        {
            _cachedUiShader = Resources.Load<Shader>("Shaders/CockpitThemeUI");
            if (_cachedUiShader == null)
                _cachedUiShader = Shader.Find("UI/CockpitThemeRemap");
        }

        if (_cachedUiShader == null)
            return null;

        if (theme == GameSettings.CockpitTheme.RadarGreen)
        {
            if (_radarGreenUiMat == null)
            {
                _radarGreenUiMat = new Material(_cachedUiShader)
                {
                    name = "CockpitThemeUI_RadarGreen",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            _radarGreenUiMat.SetFloat(ThemeModeProp, 1f);
            return _radarGreenUiMat;
        }

        if (_stealthGoldUiMat == null)
        {
            _stealthGoldUiMat = new Material(_cachedUiShader)
            {
                name = "CockpitThemeUI_StealthGold",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        _stealthGoldUiMat.SetFloat(ThemeModeProp, 2f);
        return _stealthGoldUiMat;
    }

    private static bool IsOurThemeMaterial(Material m)
    {
        if (m == null) return false;
        return m == _radarGreenUiMat || m == _stealthGoldUiMat ||
               m.name == "CockpitThemeUI_RadarGreen" || m.name == "CockpitThemeUI_StealthGold";
    }

    private static bool CanOverrideGraphicMaterial(Graphic g)
    {
        if (g == null) return false;
        Material cur = g.material;
        if (cur == null) return true;
        if (cur == g.defaultMaterial || cur == Graphic.defaultGraphicMaterial) return true;
        if (IsOurThemeMaterial(cur)) return true;
        return false;
    }

    private static bool IsIgnored(Transform t)
    {
        Transform cur = t;
        while (cur != null)
        {
            if (cur.GetComponent<CockpitThemeIgnore>() != null)
                return true;
            cur = cur.parent;
        }
        return false;
    }

    private void SyncActiveSceneVisuals(bool includeInactive)
    {
        var theme = GameSettings.SelectedCockpitTheme;
        Material targetMat = GetThemeMaterial(theme);

        // 1. Kamera arka plan rengi ve URP Global Light 2D savaş sahnesi aydınlatması
        Camera cam = Camera.main;
        if (cam != null)
        {
            if (!_originalCameraBgColor.HasValue)
                _originalCameraBgColor = cam.backgroundColor;

            cam.backgroundColor = theme switch
            {
                GameSettings.CockpitTheme.RadarGreen  => new Color(0.02f, 0.09f, 0.05f, 1f),
                GameSettings.CockpitTheme.StealthGold => new Color(0.08f, 0.06f, 0.03f, 1f),
                _                                     => _originalCameraBgColor.Value
            };
        }

        SyncGlobalLight2D(theme);

        // 2. Tüm UI Image ve RawImage bileşenleri (Menü arka planları, kartlar, butonlar, HUD, kontroller)
        var findMode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;

        if (theme == GameSettings.CockpitTheme.CyberBlue || targetMat == null)
        {
            if (_themedGraphics.Count > 0)
            {
                foreach (var g in _themedGraphics)
                {
                    if (g != null && IsOurThemeMaterial(g.material))
                    {
                        g.material = null;
                        g.SetMaterialDirty();
                    }
                }
                _themedGraphics.Clear();
            }

            // İhtiyat taraması: daha önce tema materyali almış tüm aktif/inaktif Image'ları temizle
            if (includeInactive)
            {
                Image[] allImgs = FindObjectsByType<Image>(FindObjectsInactive.Include);
                for (int i = 0; i < allImgs.Length; i++)
                {
                    if (allImgs[i] != null && IsOurThemeMaterial(allImgs[i].material))
                    {
                        allImgs[i].material = null;
                        allImgs[i].SetMaterialDirty();
                    }
                }
            }
        }
        else
        {
            Image[] images = FindObjectsByType<Image>(findMode);
            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img == null) continue;
                if (IsIgnored(img.transform))
                {
                    if (IsOurThemeMaterial(img.material))
                    {
                        img.material = null;
                        img.SetMaterialDirty();
                    }
                    continue;
                }
                if (CanOverrideGraphicMaterial(img))
                {
                    if (img.material != targetMat)
                    {
                        img.material = targetMat;
                        img.SetMaterialDirty();
                    }
                    _themedGraphics.Add(img);
                }
            }

            RawImage[] rawImages = FindObjectsByType<RawImage>(findMode);
            for (int i = 0; i < rawImages.Length; i++)
            {
                RawImage rImg = rawImages[i];
                if (rImg == null) continue;
                if (IsIgnored(rImg.transform))
                {
                    if (IsOurThemeMaterial(rImg.material))
                    {
                        rImg.material = null;
                        rImg.SetMaterialDirty();
                    }
                    continue;
                }
                if (CanOverrideGraphicMaterial(rImg))
                {
                    if (rImg.material != targetMat)
                    {
                        rImg.material = targetMat;
                        rImg.SetMaterialDirty();
                    }
                    _themedGraphics.Add(rImg);
                }
            }
        }

        // 3. Tüm TextMeshProUGUI mavi/cyan başlık, alt yazı ve RichText <color=#...> etiketleri
        TextMeshProUGUI[] tmps = FindObjectsByType<TextMeshProUGUI>(findMode);
        for (int i = 0; i < tmps.Length; i++)
        {
            TextMeshProUGUI tmp = tmps[i];
            if (tmp == null || IsIgnored(tmp.transform)) continue;
            SyncTmpTheme(tmp, theme);
        }
    }

    private void SyncGlobalLight2D(GameSettings.CockpitTheme theme)
    {
        if (!_light2DReflectionResolved)
        {
            _light2DReflectionResolved = true;
            _light2DType = System.Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
            if (_light2DType != null)
            {
                _light2DColorProp = _light2DType.GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
                _light2DTypeProp  = _light2DType.GetProperty("lightType", BindingFlags.Instance | BindingFlags.Public);
            }
        }

        if (_light2DType == null || _light2DColorProp == null) return;

        Object[] lights = FindObjectsByType(_light2DType, FindObjectsInactive.Include);
        for (int i = 0; i < lights.Length; i++)
        {
            Component lightComp = lights[i] as Component;
            if (lightComp == null) continue;

            // Global Light 2D kontrolü (lightType == Global (4) veya isimde Global)
            bool isGlobal = lightComp.gameObject.name.IndexOf("Global", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isGlobal && _light2DTypeProp != null)
            {
                try
                {
                    int lt = System.Convert.ToInt32(_light2DTypeProp.GetValue(lightComp, null));
                    if (lt == 4) isGlobal = true;
                }
                catch { }
            }
            if (!isGlobal) continue;

            if (!_originalLight2DColors.TryGetValue(lightComp, out Color baseLightCol))
            {
                baseLightCol = (Color)_light2DColorProp.GetValue(lightComp, null);
                _originalLight2DColors[lightComp] = baseLightCol;
            }

            Color targetLightCol = theme switch
            {
                GameSettings.CockpitTheme.RadarGreen  => new Color(0.82f, 1.00f, 0.88f, 1f),
                GameSettings.CockpitTheme.StealthGold => new Color(1.00f, 0.91f, 0.78f, 1f),
                _                                     => baseLightCol
            };

            _light2DColorProp.SetValue(lightComp, targetLightCol, null);
        }
    }

    private void SyncTmpTheme(TextMeshProUGUI tmp, GameSettings.CockpitTheme theme)
    {
        // A) Vertex rengi cyan/mavi bandındaysa temaya uyarla, Siber Mavi'ye dönünce orijinali geri yükle
        if (theme == GameSettings.CockpitTheme.CyberBlue)
        {
            if (_originalTmpVertexColors.TryGetValue(tmp, out Color origCol))
            {
                tmp.color = origCol;
                _originalTmpVertexColors.Remove(tmp);
            }
        }
        else
        {
            Color checkCol = _originalTmpVertexColors.TryGetValue(tmp, out Color savedCol) ? savedCol : tmp.color;
            Color.RGBToHSV(checkCol, out float h, out float s, out float v);
            if (h >= 0.40f && h <= 0.72f && s >= 0.12f)
            {
                if (!_originalTmpVertexColors.ContainsKey(tmp))
                    _originalTmpVertexColors[tmp] = checkCol;

                float targetH = (theme == GameSettings.CockpitTheme.RadarGreen) ? 0.395f : 0.105f;
                float targetS = Mathf.Clamp01(Mathf.Max(s * 0.92f, 0.55f));
                Color themed = Color.HSVToRGB(targetH, targetS, v);
                themed.a = checkCol.a;
                if (tmp.color != themed)
                    tmp.color = themed;
            }
        }

        // B) Metin içi <color=#...> cyan vurgu etiketleri
        string txt = tmp.text;
        if (string.IsNullOrEmpty(txt) || txt.IndexOf('#') < 0)
            return;

        string updated = RewriteRichTextHexes(txt, theme);
        if (!ReferenceEquals(updated, txt) && updated != txt)
        {
            tmp.text = updated;
        }
    }

    private static string RewriteRichTextHexes(string input, GameSettings.CockpitTheme theme)
    {
        string s = input;

        if (theme == GameSettings.CockpitTheme.CyberBlue)
        {
            if (s.IndexOf(GREEN_BRIGHT_HEX, System.StringComparison.OrdinalIgnoreCase) >= 0)
                s = ReplaceIgnoreCase(s, GREEN_BRIGHT_HEX, "#00E5FF");
            if (s.IndexOf(GOLD_BRIGHT_HEX, System.StringComparison.OrdinalIgnoreCase) >= 0)
                s = ReplaceIgnoreCase(s, GOLD_BRIGHT_HEX, "#00E5FF");
            if (s.IndexOf(GREEN_SOFT_HEX, System.StringComparison.OrdinalIgnoreCase) >= 0)
                s = ReplaceIgnoreCase(s, GREEN_SOFT_HEX, "#7DD3FC");
            if (s.IndexOf(GOLD_SOFT_HEX, System.StringComparison.OrdinalIgnoreCase) >= 0)
                s = ReplaceIgnoreCase(s, GOLD_SOFT_HEX, "#7DD3FC");
            return s;
        }

        string targetBright = (theme == GameSettings.CockpitTheme.RadarGreen) ? GREEN_BRIGHT_HEX : GOLD_BRIGHT_HEX;
        string otherBright  = (theme == GameSettings.CockpitTheme.RadarGreen) ? GOLD_BRIGHT_HEX  : GREEN_BRIGHT_HEX;
        string targetSoft   = (theme == GameSettings.CockpitTheme.RadarGreen) ? GREEN_SOFT_HEX   : GOLD_SOFT_HEX;
        string otherSoft    = (theme == GameSettings.CockpitTheme.RadarGreen) ? GOLD_SOFT_HEX    : GREEN_SOFT_HEX;

        for (int i = 0; i < CyanBrightHexes.Length; i++)
        {
            if (s.IndexOf(CyanBrightHexes[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                s = ReplaceIgnoreCase(s, CyanBrightHexes[i], targetBright);
        }
        if (s.IndexOf(otherBright, System.StringComparison.OrdinalIgnoreCase) >= 0)
            s = ReplaceIgnoreCase(s, otherBright, targetBright);

        for (int i = 0; i < CyanSoftHexes.Length; i++)
        {
            if (s.IndexOf(CyanSoftHexes[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                s = ReplaceIgnoreCase(s, CyanSoftHexes[i], targetSoft);
        }
        if (s.IndexOf(otherSoft, System.StringComparison.OrdinalIgnoreCase) >= 0)
            s = ReplaceIgnoreCase(s, otherSoft, targetSoft);

        return s;
    }

    private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
    {
        int idx = source.IndexOf(oldValue, System.StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return source;

        System.Text.StringBuilder sb = new System.Text.StringBuilder(source.Length);
        int prev = 0;
        while (idx >= 0)
        {
            sb.Append(source, prev, idx - prev);
            sb.Append(newValue);
            prev = idx + oldValue.Length;
            idx = source.IndexOf(oldValue, prev, System.StringComparison.OrdinalIgnoreCase);
        }
        sb.Append(source, prev, source.Length - prev);
        return sb.ToString();
    }

    /// <summary>
    /// URP 2D SpriteRenderer materyalini (Sprite-Lit-Default) hiç değiştirmeden dünya arka plan görselini
    /// (ör. savaş fırtına bulutları game_background.jpg) seçilen kokpit temasına önbellekli olarak dönüştürür.
    /// </summary>
    public static Sprite GetThemedWorldSprite(Sprite originalSprite, string diskFileBase, GameSettings.CockpitTheme theme)
    {
        if (originalSprite == null || theme == GameSettings.CockpitTheme.CyberBlue)
            return originalSprite;

        int objHash = RuntimeHelpers.GetHashCode(originalSprite);
        string key = $"{objHash}_{originalSprite.name}_{(int)theme}";
        if (_themedWorldSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Texture2D srcTex = originalSprite.texture;
        float ppu = originalSprite.pixelsPerUnit > 0f ? originalSprite.pixelsPerUnit : 100f;

        // 1. Öncelik: GPU Blit (CockpitThemeUI.shader Pass 1 ile birebir aynı renk eğrisi)
        Material themeMat = GetThemeMaterial(theme);
        if (srcTex != null && themeMat != null)
        {
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                themeMat.SetTexture(MainTexProp, srcTex);
                int blitPass = themeMat.passCount > 1 ? 1 : 0;
                Graphics.Blit(srcTex, rt, themeMat, blitPass);
                RenderTexture.active = rt;

                Texture2D outTex = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false, false);
                outTex.ReadPixels(new Rect(0, 0, srcTex.width, srcTex.height), 0, 0, false);
                outTex.Apply(false, false);
                outTex.wrapMode = srcTex.wrapMode;
                outTex.filterMode = srcTex.filterMode;

                Rect sprRect = originalSprite.rect;
                Vector2 pivot = new Vector2(
                    sprRect.width > 0f ? originalSprite.pivot.x / sprRect.width : 0.5f,
                    sprRect.height > 0f ? originalSprite.pivot.y / sprRect.height : 0.5f);

                Sprite built = Sprite.Create(outTex, sprRect, pivot, ppu);
                _themedWorldSpriteCache[key] = built;
                return built;
            }
            catch { }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // 2. Yedek (Fallback): Diskten doğrudan yükleyip CPU üzerinde aynı HSV dönüşümünü uygula
        if (!string.IsNullOrEmpty(diskFileBase))
        {
            string[] candidates =
            {
                Path.Combine(Application.dataPath, "Sprites/" + diskFileBase + ".jpg"),
                Path.Combine(Application.dataPath, "Sprites/" + diskFileBase + ".png"),
                Path.Combine(Application.dataPath, "Resources/" + diskFileBase + ".jpg"),
                Path.Combine(Application.dataPath, "Resources/" + diskFileBase + ".png")
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(p);
                        Texture2D cpuTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (cpuTex.LoadImage(bytes))
                        {
                            RemapTextureCpu(cpuTex, theme);
                            cpuTex.wrapMode = TextureWrapMode.Clamp;
                            cpuTex.filterMode = FilterMode.Bilinear;
                            Sprite cpuSpr = Sprite.Create(cpuTex, new Rect(0, 0, cpuTex.width, cpuTex.height), new Vector2(0.5f, 0.5f), ppu);
                            _themedWorldSpriteCache[key] = cpuSpr;
                            return cpuSpr;
                        }
                    }
                    catch { }
                }
            }
        }

        return originalSprite;
    }

    private static void RemapTextureCpu(Texture2D tex, GameSettings.CockpitTheme theme)
    {
        Color[] cols = tex.GetPixels();
        for (int i = 0; i < cols.Length; i++)
        {
            Color c = cols[i];
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float isRed = Mathf.Max(SmoothStep(0.915f, 0.955f, h), 1f - SmoothStep(0.035f, 0.068f, h)) * SmoothStep(0.32f, 0.52f, s);
            float isWhite = SmoothStep(0.86f, 0.97f, v) * (1f - SmoothStep(0.04f, 0.12f, s));
            float protect = Mathf.Clamp01(Mathf.Max(isRed, isWhite));
            float blueBand = SmoothStep(0.375f, 0.435f, h) * (1f - SmoothStep(0.715f, 0.785f, h));
            float weight = Mathf.Lerp(0.68f, 1f, blueBand) * (1f - protect);
            if (weight <= 0.002f) continue;

            float bright = SmoothStep(0.12f, 0.74f, v);
            float th, ts, tv;
            if (theme == GameSettings.CockpitTheme.RadarGreen)
            {
                th = Mathf.Lerp(0.415f, 0.390f, bright);
                ts = Mathf.Clamp01(Mathf.Max(s * 0.92f, 0.28f * SmoothStep(0.02f, 0.18f, v)));
                tv = Mathf.Clamp01(v * Mathf.Lerp(0.95f, 1.04f, bright));
            }
            else
            {
                th = Mathf.Lerp(0.088f, 0.106f, bright);
                ts = Mathf.Clamp01(Mathf.Max(s * Mathf.Lerp(0.45f, 1.0f, bright), 0.24f * SmoothStep(0.03f, 0.22f, v)));
                tv = Mathf.Clamp01(v * Mathf.Lerp(0.92f, 1.06f, bright));
            }

            Color tc = Color.HSVToRGB(th, ts, tv);
            tc.a = c.a;
            cols[i] = Color.Lerp(c, tc, weight);
        }
        tex.SetPixels(cols);
        tex.Apply();
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
