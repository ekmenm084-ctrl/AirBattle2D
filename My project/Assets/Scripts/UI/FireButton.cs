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

    public void ApplyMilitaryFireStyling()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) return;

        if (autoPosition)
        {
            if (isPlayer1)
            {
                // P1: Sağ Alt Köşe (-160, 160)
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-160f, 160f);
                rt.localEulerAngles = Vector3.zero;
            }
            else
            {
                // P2: Sol Üst Köşe (180, -180), Karşı oyuncu için 180° ters açılı
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(180f, -180f);
                rt.localEulerAngles = new Vector3(0f, 0f, 180f);
            }
            rt.sizeDelta = new Vector2(150f, 150f);
        }

        if (_image == null) _image = GetComponent<Image>();
        if (_image == null) _image = gameObject.AddComponent<Image>();
        _image.color = new Color(0.92f, 0.18f, 0.22f, 0.90f);

        // Kırmızı neon parlama efekti
        Outline outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        Transform textChild = transform.Find("Text");
        GameObject textObj = textChild != null ? textChild.gameObject : null;
        if (textObj == null)
        {
            textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(transform, false);
            RectTransform trt = textObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = textObj.AddComponent<TextMeshProUGUI>();

        tmp.text = "<b>ATEŞ</b>";
        tmp.fontSize = 28f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        transform.SetAsLastSibling();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        if (_image != null) _image.color = new Color(1f, 0.35f, 0.35f, 1f);
        OnFirePressed();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        if (_image != null) _image.color = new Color(0.92f, 0.18f, 0.22f, 0.90f);
    }

    private void OnFirePressed()
    {
        if (targetShooter == null) FindTargetShooterIfNull();
        targetShooter?.RequestFire();
    }
}
