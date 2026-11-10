using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dinamik Canlı Taktik Fırtına Arka Plan Sistemi (UI Canvas):
/// 1. Kesintisiz çift katmanlı sonsuz konveyör akışı (Sıfır dikiş izi, sıfır şerit, sıfır boşluk).
/// 2. Dikey olarak kusursuz harmanlanmış görsel sayesinde geçiş çizgisi tamamen görünmezdir.
/// 3. Doğal çoklu darbe atmosferik şimşek çakmaları.
/// 4. Daima UI panellerinin arkasında (SetAsFirstSibling) çalışır; butonları asla kapatmaz.
/// 5. Menü duraklatılmışken bile (Time.timeScale = 0) unscaledDeltaTime ile 60 FPS canlı akar.
/// </summary>
public class DynamicStormBackground : MonoBehaviour
{
    public static DynamicStormBackground Instance { get; private set; }

    [Header("Akış Hızı & Hareket")]
    public float scrollSpeed = 40f;             // Gökyüzü akış hızı (piksel/sn)
    public bool enableSway = true;              // Hafif yatay sinematik süzülme
    public float swayAmount = 8f;               // Süzülme genliği
    public float swayFrequency = 0.50f;         // Süzülme sıklığı

    [Header("Şimşek Ayarları")]
    public bool enableLightning = true;
    public float minLightningInterval = 4.5f;   // İki şimşek arası min bekleme
    public float maxLightningInterval = 8.5f;   // İki şimşek arası maks bekleme
    public Color lightningTint = new Color(0.70f, 0.88f, 1.0f, 1f);

    private RectTransform _bg1Rt;
    private RectTransform _bg2Rt;
    private Image _bg1Img;
    private Image _bg2Img;

    private float _layerHeight = 1150f;
    private float _layerWidth = 2100f;
    private float _pos1Y = 0f;
    private float _pos2Y = 0f;

    private Image _lightningOverlay;
    private Coroutine _lightningCoroutine;
    private Sprite _stormSprite;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_lightningCoroutine != null)
        {
            StopCoroutine(_lightningCoroutine);
            _lightningCoroutine = null;
        }
    }

    public static DynamicStormBackground AttachTo(GameObject target, Sprite stormSprite = null)
    {
        if (target == null) return null;
        DynamicStormBackground dsb = target.GetComponent<DynamicStormBackground>();
        if (dsb == null) dsb = target.AddComponent<DynamicStormBackground>();
        if (stormSprite != null) dsb._stormSprite = stormSprite;
        dsb.Initialize();
        return dsb;
    }

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (_bg1Rt != null) return;

        // Görseli al veya diskten yükle
        Image baseImg = GetComponent<Image>();
        if (baseImg != null && _stormSprite == null)
        {
            _stormSprite = baseImg.sprite;
        }

        if (_stormSprite == null)
        {
            _stormSprite = Resources.Load<Sprite>("menu_background") ?? LoadSpriteFromDisk("My project/Assets/Sprites/menu_background.jpg");
        }

        // Taşıyıcı Image bileşeni varsa şeffaf yap, alt katmanlar görüntüyü yönetecek
        if (baseImg != null)
        {
            baseImg.color = new Color(0f, 0f, 0f, 0f);
            baseImg.raycastTarget = false;
        }

        CalculateDimensions();
        SetupContinuousScrollingLayers();
        SetupLightningOverlay();

        if (enableLightning && gameObject.activeInHierarchy && _lightningCoroutine == null)
        {
            _lightningCoroutine = StartCoroutine(LightningRoutine());
        }
    }

    private void CalculateDimensions()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRt = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        float canvasW = (canvasRt != null && canvasRt.rect.width > 50f) ? canvasRt.rect.width : Screen.width;
        float canvasH = (canvasRt != null && canvasRt.rect.height > 50f) ? canvasRt.rect.height : Screen.height;

        if (canvasW < 100f) canvasW = 1920f;
        if (canvasH < 100f) canvasH = 1080f;

        // Ekranı tam kaplayacak şekilde boyutlandır
        _layerWidth = canvasW * 1.25f;
        _layerHeight = Mathf.Max(canvasH, canvasW * 0.60f) * 1.10f;
    }

    private void SetupContinuousScrollingLayers()
    {
        // Katman 1
        GameObject bg1Obj = new GameObject("DynamicStorm_Layer1", typeof(RectTransform));
        bg1Obj.transform.SetParent(transform, false);
        bg1Obj.transform.SetAsFirstSibling();
        _bg1Rt = bg1Obj.GetComponent<RectTransform>();
        ConfigureLayerRect(_bg1Rt, 0f);
        _bg1Img = bg1Obj.AddComponent<Image>();
        _bg1Img.sprite = _stormSprite;
        _bg1Img.color = Color.white;
        _bg1Img.raycastTarget = false;

        // Katman 2 (Üstüne eklenen ikinci görsel)
        GameObject bg2Obj = new GameObject("DynamicStorm_Layer2", typeof(RectTransform));
        bg2Obj.transform.SetParent(transform, false);
        bg2Obj.transform.SetAsFirstSibling();
        _bg2Rt = bg2Obj.GetComponent<RectTransform>();
        ConfigureLayerRect(_bg2Rt, _layerHeight);
        _bg2Img = bg2Obj.AddComponent<Image>();
        _bg2Img.sprite = _stormSprite;
        _bg2Img.color = Color.white;
        _bg2Img.raycastTarget = false;

        _pos1Y = 0f;
        _pos2Y = _layerHeight;
    }

    private void ConfigureLayerRect(RectTransform rt, float startY)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, startY);
        rt.sizeDelta = new Vector2(_layerWidth, _layerHeight + 2f);
    }

    private void SetupLightningOverlay()
    {
        GameObject overlayObj = new GameObject("DynamicStorm_LightningOverlay", typeof(RectTransform));
        overlayObj.transform.SetParent(transform, false);
        overlayObj.transform.SetAsLastSibling();

        RectTransform oRt = overlayObj.GetComponent<RectTransform>();
        oRt.anchorMin = Vector2.zero;
        oRt.anchorMax = Vector2.one;
        oRt.offsetMin = Vector2.zero;
        oRt.offsetMax = Vector2.zero;

        _lightningOverlay = overlayObj.AddComponent<Image>();
        _lightningOverlay.color = new Color(0f, 0f, 0f, 0f);
        _lightningOverlay.raycastTarget = false;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        // 1. Kesintisiz Çift Katmanlı Sonsuz Konveyör Hareketi (Sıfır Şerit, Sıfır Boşluk)
        if (_bg1Rt != null && _bg2Rt != null)
        {
            _pos1Y -= scrollSpeed * dt;
            _pos2Y -= scrollSpeed * dt;

            // Katman 1 alt sınıra indiğinde Katman 2'nin üst ucuna kilitlenir
            if (_pos1Y <= -_layerHeight)
            {
                _pos1Y = _pos2Y + _layerHeight;
            }

            // Katman 2 alt sınıra indiğinde Katman 1'in üst ucuna kilitlenir
            if (_pos2Y <= -_layerHeight)
            {
                _pos2Y = _pos1Y + _layerHeight;
            }

            // Sinematik yatay süzülme
            float swayX = 0f;
            if (enableSway)
            {
                swayX = Mathf.Sin(Time.unscaledTime * swayFrequency) * swayAmount;
            }

            _bg1Rt.anchoredPosition = new Vector2(swayX, _pos1Y);
            _bg2Rt.anchoredPosition = new Vector2(swayX, _pos2Y);
        }
    }

    private IEnumerator LightningRoutine()
    {
        while (true)
        {
            float waitSec = Random.Range(minLightningInterval, maxLightningInterval);
            yield return new WaitForSecondsRealtime(waitSec);

            if (_lightningOverlay == null) yield break;

            // 1. Öncü Darbe (Pre-Strike)
            SetOverlay(new Color(lightningTint.r, lightningTint.g, lightningTint.b, 0.28f));
            yield return new WaitForSecondsRealtime(0.04f);
            SetOverlay(new Color(0f, 0f, 0f, 0f));
            yield return new WaitForSecondsRealtime(0.06f);

            // 2. Ana Parlama (Bright Flash)
            SetOverlay(new Color(1f, 1f, 1f, 0.55f));
            yield return new WaitForSecondsRealtime(0.09f);

            // 3. Art Parıltı Titremesi (Afterglow Flicker)
            SetOverlay(new Color(lightningTint.r, lightningTint.g, lightningTint.b, 0.32f));
            yield return new WaitForSecondsRealtime(0.05f);
            SetOverlay(new Color(lightningTint.r, lightningTint.g, lightningTint.b, 0.15f));
            yield return new WaitForSecondsRealtime(0.07f);

            // 4. Doğal Sönümlenme (Smooth Fade)
            float elapsed = 0f;
            float fadeDur = 0.28f;
            Color startColor = (_lightningOverlay != null) ? _lightningOverlay.color : Color.clear;
            while (elapsed < fadeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / fadeDur;
                if (_lightningOverlay != null)
                {
                    _lightningOverlay.color = Color.Lerp(startColor, new Color(0f, 0f, 0f, 0f), t);
                }
                yield return null;
            }
            SetOverlay(new Color(0f, 0f, 0f, 0f));
        }
    }

    private void SetOverlay(Color col)
    {
        if (_lightningOverlay != null)
        {
            _lightningOverlay.color = col;
        }
    }

    private static Sprite LoadSpriteFromDisk(string relativePath)
    {
        string fullPath = System.IO.Path.Combine(Application.dataPath, "../", relativePath);
        if (System.IO.File.Exists(fullPath))
        {
            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (tex.LoadImage(bytes))
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        return null;
    }
}
