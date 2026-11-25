using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Yeni Nesil Profesyonel Taktik Uçuş Joystick'i:
/// - P1: Cyber Cyan & Titanyum Gimbal (Uçak 1 Stealth teması).
/// - P2: Crimson Magma & Tungsten Gimbal (Uçak 2 Interceptor teması).
/// - 3D metalik tutma başlığı (knurled thumb grip) ve ortasında jet amblemi.
/// - Pusula azimut çentikleri, radar halkaları ve taktik kanatçık yön okları.
/// - Dokunulduğunda parlayan ve büyüyen dokunsal (tactile) tepki.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick Parçaları")]
    public RectTransform background;  // Dış taban çemberi
    public RectTransform handle;      // İç dokunma topu

    [Header("Görsel Ayarları")]
    public Sprite baseSprite;
    public Sprite handleSprite;

    [Header("Yapılandırma")]
    public bool autoPosition = true;
    public bool isPlayer1 = true;
    public Color themeColor = new Color(0f, 0.85f, 1f, 1f);

    [Range(0f, 1f)]
    public float deadZone = 0.08f;

    public Vector2 InputDirection { get; private set; }

    private float _radius = 150f;
    private Image _bgImage;
    private Image _handleImage;
    private CanvasGroup _canvasGroup;
    private PlaneController _trackedPlane;
    private Canvas _parentCanvas;
    private RectTransform _canvasRt;
    private Vector3 _targetHandleScale = Vector3.one;

    private void Awake()
    {
        if (background == null)
            background = GetComponent<RectTransform>();

        _radius = 150f;
        EnsureCanvasGroup();
    }

    private void OnEnable()
    {
        ApplyProfessionalStyling();
    }

    private void Start()
    {
        ApplyProfessionalStyling();
        FindTrackedPlane();
    }

    private void Update()
    {
        // Tutma topunun yumuşak ölçeklenme animasyonu
        if (handle != null)
        {
            handle.localScale = Vector3.Lerp(handle.localScale, _targetHandleScale, Time.unscaledDeltaTime * 15f);
        }

        UpdatePlaneAlignmentTransparency();
    }

    private void EnsureCanvasGroup()
    {
        if (background == null) return;
        if (_canvasGroup == null)
        {
            _canvasGroup = background.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = background.gameObject.AddComponent<CanvasGroup>();
        }
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
    }

    private void FindTrackedPlane()
    {
        var planes = FindObjectsOfType<PlaneController>();
        foreach (var pc in planes)
        {
            if (pc != null && pc.isPlayer1 == isPlayer1)
            {
                _trackedPlane = pc;
                break;
            }
        }
    }

    /// <summary>
    /// Uçak joystick hizasına geldiğinde joystick'i şeffaflaştırarak uçağın kapanmasını önler.
    /// </summary>
    private void UpdatePlaneAlignmentTransparency()
    {
        EnsureCanvasGroup();
        if (_canvasGroup == null || background == null) return;

        if (_trackedPlane == null || !_trackedPlane.gameObject.activeInHierarchy)
            FindTrackedPlane();

        if (_parentCanvas == null)
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
                _canvasRt = _parentCanvas.GetComponent<RectTransform>();
        }

        float targetAlpha = 1f;
        Camera cam = Camera.main;

        if (_trackedPlane != null && cam != null && _canvasRt != null)
        {
            Vector3 screenPt = cam.WorldToScreenPoint(_trackedPlane.transform.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, screenPt, null, out Vector2 planeCanvasLocal))
            {
                Vector2 canvasSize = _canvasRt.rect.size;
                float planeXFromLeft = planeCanvasLocal.x + (canvasSize.x * 0.5f);

                // Uçağın kanat açıklığı + kanat dronu payı (~145px)
                float wingReachPx = 145f;
                float fadeDistancePx = 140f;

                if (isPlayer1)
                {
                    // P1 Sol Alt Joystick X aralığı: [17 .. 347]
                    float joyLeft = background.anchoredPosition.x - (background.sizeDelta.x * 0.5f);
                    float joyRight = background.anchoredPosition.x + (background.sizeDelta.x * 0.5f);
                    float planeMinX = planeXFromLeft - wingReachPx;
                    float planeMaxX = planeXFromLeft + wingReachPx;

                    float gap = 0f;
                    if (planeMinX > joyRight) gap = planeMinX - joyRight;
                    else if (planeMaxX < joyLeft) gap = joyLeft - planeMaxX;

                    float t = Mathf.Clamp01(gap / fadeDistancePx);
                    targetAlpha = Mathf.Lerp(0.18f, 1.0f, t);
                }
                else
                {
                    // P2 Sağ Üst Joystick X aralığı
                    float joyCenterFromLeft = canvasSize.x + background.anchoredPosition.x;
                    float joyLeft = joyCenterFromLeft - (background.sizeDelta.x * 0.5f);
                    float joyRight = joyCenterFromLeft + (background.sizeDelta.x * 0.5f);
                    float planeMinX = planeXFromLeft - wingReachPx;
                    float planeMaxX = planeXFromLeft + wingReachPx;

                    float gap = 0f;
                    if (planeMaxX < joyLeft) gap = joyLeft - planeMaxX;
                    else if (planeMinX > joyRight) gap = planeMinX - joyRight;

                    float t = Mathf.Clamp01(gap / fadeDistancePx);
                    targetAlpha = Mathf.Lerp(0.18f, 1.0f, t);
                }
            }
        }

        _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 14f);
    }

    /// <summary>
    /// Joystick'e özel yüksek çözünürlüklü askeri gimbal tasarımını giydirir.
    /// </summary>
    public void ApplyProfessionalStyling()
    {
        TryLoadCustomSprites();
        EnsureCanvasGroup();

        if (background != null)
        {
            if (autoPosition)
            {
                if (isPlayer1)
                {
                    // P1: Sol Alt Köşe (182, 180) - 2 kademe büyütülmüş ergonomik yerleşim
                    background.anchorMin = new Vector2(0f, 0f);
                    background.anchorMax = new Vector2(0f, 0f);
                    background.pivot = new Vector2(0.5f, 0.5f);
                    background.anchoredPosition = new Vector2(182f, 180f);
                    background.localEulerAngles = Vector3.zero;
                }
                else
                {
                    // P2: Sağ Üst Köşe (-182, -210), Karşı oyuncu için 180° ters açılı
                    background.anchorMin = new Vector2(1f, 1f);
                    background.anchorMax = new Vector2(1f, 1f);
                    background.pivot = new Vector2(0.5f, 0.5f);
                    background.anchoredPosition = new Vector2(-182f, -210f);
                    background.localEulerAngles = new Vector3(0f, 0f, 180f);
                }
            }

            background.sizeDelta = new Vector2(330f, 330f);
            _radius = 150f;

            _bgImage = background.GetComponent<Image>();
            if (_bgImage != null)
            {
                if (baseSprite != null)
                {
                    _bgImage.sprite = baseSprite;
                    _bgImage.color = Color.white;
                }
                else
                {
                    _bgImage.color = isPlayer1
                        ? new Color(0.06f, 0.12f, 0.18f, 0.85f)
                        : new Color(0.18f, 0.06f, 0.06f, 0.85f);
                }
            }

            // Eski basit metin okları varsa kaldır (Grafiğin kendisinde yüksek kaliteli oklar var)
            Transform oldGuide = background.transform.Find("HUD_ArrowGuide");
            if (oldGuide != null)
            {
                Destroy(oldGuide.gameObject);
            }
        }

        if (handle != null)
        {
            handle.sizeDelta = new Vector2(138f, 138f);
            handle.anchoredPosition = Vector2.zero;

            _handleImage = handle.GetComponent<Image>();
            if (_handleImage != null)
            {
                if (handleSprite != null)
                {
                    _handleImage.sprite = handleSprite;
                    _handleImage.color = Color.white;
                }
                else
                {
                    _handleImage.color = isPlayer1
                        ? new Color(0.15f, 0.90f, 1f, 0.95f)
                        : new Color(1.00f, 0.45f, 0.20f, 0.95f);
                }
            }
        }
    }

    private void TryLoadCustomSprites()
    {
        string baseName = isPlayer1 ? "joystick_base_p1.png" : "joystick_base_p2.png";
        string handleName = isPlayer1 ? "joystick_handle_p1.png" : "joystick_handle_p2.png";

        if (baseSprite == null)
            baseSprite = LoadSpriteFromDisk(baseName);

        if (handleSprite == null)
            handleSprite = LoadSpriteFromDisk(handleName);
    }

    private Sprite LoadSpriteFromDisk(string fileName)
    {
        string resName = Path.GetFileNameWithoutExtension(fileName);
        Sprite resSpr = Resources.Load<Sprite>(resName);
        if (resSpr != null) return resSpr;

        try
        {
            string path = Path.Combine(Application.dataPath, "Sprites", fileName);
            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }
        catch { }
        return null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _targetHandleScale = Vector3.one * 1.15f; // Dokunulduğunda hafifçe büyür
        OnDrag(eventData);

        if (_handleImage != null && handleSprite == null)
            _handleImage.color = Color.white;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, eventData.position, eventData.pressEventCamera, out localPos))
        {
            Vector2 clamped = Vector2.ClampMagnitude(localPos, _radius);
            if (handle != null)
                handle.anchoredPosition = clamped;

            Vector2 normalized = clamped / _radius;
            InputDirection = normalized.magnitude < deadZone ? Vector2.zero : normalized;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _targetHandleScale = Vector3.one; // Bırakılınca normal boyuta döner
        InputDirection = Vector2.zero;

        if (handle != null)
            handle.anchoredPosition = Vector2.zero;

        if (_handleImage != null && handleSprite == null)
            _handleImage.color = isPlayer1 ? new Color(0.15f, 0.90f, 1f, 0.95f) : new Color(1.00f, 0.45f, 0.20f, 0.95f);
    }
}
