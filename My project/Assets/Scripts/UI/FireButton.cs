using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Profesyonel Taktik Ateş Butonu (Dokunmatik, Basılı Tutma ve Fare desteği):
/// - Tek dokunuş ve basılı tutarak seri ateş desteği (Continuous Rapid Fire).
/// - P1 için: Sağ Alt (X: -160, Y: 160)
/// - P2 için: Sol Üst (X: 180, Y: -180, 180° ters çevrilmiş)
/// - Neon kızıl askeri kokpit tasarımı.
/// </summary>
public class FireButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Yapılandırma")]
    public bool isPlayer1 = true;
    public bool autoPosition = true;

    [Header("Referans")]
    public PlaneShooter targetShooter;

    private Button _button;
    private Image _image;
    private bool _isPressed = false;

    /// <summary>
    /// Sahnede P1 Ateş Butonu'nun var olduğunu ve aktif olduğunu kesinleştirir.
    /// </summary>
    public static FireButton EnsureP1FireButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("FireButton");
        GameObject go = existing != null ? existing.gameObject : null;

        if (go == null)
        {
            var allFb = Resources.FindObjectsOfTypeAll<FireButton>();
            foreach (var fb in allFb)
            {
                if (fb != null && fb.isPlayer1 && fb.gameObject.scene.isLoaded)
                {
                    go = fb.gameObject;
                    break;
                }
            }
        }

        if (go == null)
        {
            go = new GameObject("FireButton", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
        }

        go.SetActive(true);
        go.transform.SetAsLastSibling();

        FireButton btn = go.GetComponent<FireButton>();
        if (btn == null) btn = go.AddComponent<FireButton>();
        btn.isPlayer1 = true;
        btn.autoPosition = true;
        btn.ApplyMilitaryFireStyling();
        btn.FindTargetShooterIfNull();

        return btn;
    }

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button == null) _button = gameObject.AddComponent<Button>();

        _image = GetComponent<Image>();
        if (_image == null) _image = gameObject.AddComponent<Image>();

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(OnFirePressed);
        ApplyMilitaryFireStyling();
    }

    private void OnEnable()
    {
        ApplyMilitaryFireStyling();
    }

    private void Start()
    {
        ApplyMilitaryFireStyling();
        FindTargetShooterIfNull();
    }

    private void Update()
    {
        if (_isPressed)
        {
            if (targetShooter == null) FindTargetShooterIfNull();
            targetShooter?.RequestFire();
        }
    }

    public void FindTargetShooterIfNull()
    {
        if (targetShooter != null) return;

        var shooters = FindObjectsOfType<PlaneShooter>();
        foreach (var s in shooters)
        {
            var pc = s.GetComponent<PlaneController>();
            if (pc != null && pc.isPlayer1 == isPlayer1)
            {
                targetShooter = s;
                break;
            }
        }
    }

    private static Sprite _cachedFireSprite;

    private static Sprite LoadFire1To1Sprite()
    {
        if (_cachedFireSprite != null) return _cachedFireSprite;

        string path = System.IO.Path.Combine(Application.dataPath, "Sprites/hud_btn_fire_1to1.png");
        if (System.IO.File.Exists(path))
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                _cachedFireSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return _cachedFireSprite;
            }
        }

        Sprite res = Resources.Load<Sprite>("hud_btn_fire_1to1");
        if (res != null)
        {
            _cachedFireSprite = res;
            return res;
        }

        Texture2D resTex = Resources.Load<Texture2D>("hud_btn_fire_1to1");
        if (resTex != null)
        {
            _cachedFireSprite = Sprite.Create(resTex, new Rect(0, 0, resTex.width, resTex.height), new Vector2(0.5f, 0.5f), 100f);
            return _cachedFireSprite;
        }
        return null;
    }

    public void ApplyMilitaryFireStyling()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) return;

        if (autoPosition)
        {
            if (isPlayer1)
            {
                // P1: Sağ Alt Köşe 1:1 referans yerleşimi (-180, 160), boyut (260, 260)
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-180f, 160f);
                rt.sizeDelta = new Vector2(260f, 260f);
                rt.localEulerAngles = Vector3.zero;
            }
            else
            {
                // P2: Sol Üst Köşe (170, -155), Karşı oyuncu için 180° ters açılı
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(170f, -155f);
                rt.sizeDelta = new Vector2(180f, 180f);
                rt.localEulerAngles = new Vector3(0f, 0f, 180f);
            }
        }

        if (_image == null) _image = GetComponent<Image>();
        if (_image == null) _image = gameObject.AddComponent<Image>();

        Outline outline = GetComponent<Outline>();
        if (outline != null) Destroy(outline);

        Sprite fireSpr = LoadFire1To1Sprite();
        Transform textChild = transform.Find("Text") ?? transform.Find("Ateş") ?? transform.Find("Text (TMP)");

        if (fireSpr != null)
        {
            _image.sprite = fireSpr;
            _image.type = Image.Type.Simple;
            _image.preserveAspect = true;
            _image.color = Color.white;
            if (textChild != null) textChild.gameObject.SetActive(false);
        }
        else
        {
            _image.color = new Color(0.92f, 0.18f, 0.22f, 0.90f);
            GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(transform, false);
            textObj.SetActive(true);
            RectTransform trt = textObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>() ?? textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "<b>ATEŞ</b>\n<size=55%>[ SPACE ]</size>";
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        transform.SetAsLastSibling();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        if (_image != null) _image.color = new Color(1f, 0.78f, 0.78f, 1f);
        transform.localScale = Vector3.one * 0.95f;
        OnFirePressed();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        if (_image != null) _image.color = Color.white;
        transform.localScale = Vector3.one;
    }

    private void OnFirePressed()
    {
        if (targetShooter == null) FindTargetShooterIfNull();
        targetShooter?.RequestFire();
    }
}
