using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Profesyonel Taktik Ateş Butonu (Dokunmatik, Basılı Tutma ve Fare desteği):
/// - Tek dokunuş ve basılı tutarak seri ateş desteği (Continuous Rapid Fire).
/// - P1 için: Sağ Alt (X: -180, Y: 180)
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

    private void Awake()
    {
        _button = GetComponent<Button>();
        _image = GetComponent<Image>();

        if (_button != null)
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
        if (rt != null && autoPosition)
        {
            if (isPlayer1)
            {
                // P1: Sağ Alt Köşe (-180, 180)
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-180f, 180f);
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
            rt.sizeDelta = new Vector2(140f, 140f);
        }

        // Kırmızı neon parlama efekti
        Outline outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.25f, 0.25f, 0.85f);
        outline.effectDistance = new Vector2(3f, -3f);

        if (_image != null)
        {
            _image.color = new Color(0.9f, 0.15f, 0.2f, 0.85f);
        }

        TextMeshProUGUI tmp = GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = "🔥\n<size=50%>ATEŞ</size>";
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }
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
        if (_image != null) _image.color = new Color(0.9f, 0.15f, 0.2f, 0.85f);
    }

    private void OnFirePressed()
    {
        if (targetShooter == null) FindTargetShooterIfNull();
        targetShooter?.RequestFire();
    }
}
