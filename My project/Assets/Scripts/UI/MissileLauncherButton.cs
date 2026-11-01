using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Isı Güdümlü Mikro Füze Salvo Butonu (Ultimate Ability UI):
/// - Dokunmatik buton ve klavye kısayolu (Q, E veya X tuşu).
/// - Sağ alt köşede Ateş butonunun hemen üzerinde ergonomik yerleşim.
/// - 12 saniye bekleme süresi (cooldown) ve dinamik dolum göstergesi.
/// - Aynı anda çift kanattan iki güdümlü roket ateşler.
/// </summary>
public class MissileLauncherButton : MonoBehaviour
{
    public static MissileLauncherButton Instance { get; private set; }

    [Header("Bekleme Süresi (Cooldown)")]
    public float cooldownDuration = 12f;
    private float _lastFiredTime = -999f;

    [Header("Referanslar")]
    private PlaneController _playerPlane;
    private Button _button;
    private Image _bgImage;
    private Image _cooldownFillImg;
    private TextMeshProUGUI _btnTMP;
    private Outline _outline;

    public bool IsReady => (Time.time >= _lastFiredTime + cooldownDuration);

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetupUI();
        FindPlayerPlane();
    }

    private void Update()
    {
        UpdateCooldownVisuals();

        // Klavye Kısayolları (Q, E veya X)
        try
        {
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.X))
            {
                TryLaunchMissiles();
            }
        }
        catch { }
    }

    private void FindPlayerPlane()
    {
        var planes = FindObjectsOfType<PlaneController>();
        foreach (var p in planes)
        {
            if (p.isPlayer1)
            {
                _playerPlane = p;
                break;
            }
        }
    }

    public void TryLaunchMissiles()
    {
        if (!IsReady) return;

        if (_playerPlane == null || !_playerPlane.gameObject.activeInHierarchy)
            FindPlayerPlane();

        if (_playerPlane == null) return;

        PlaneHealth health = _playerPlane.GetComponent<PlaneHealth>();
        if (health != null && health.IsDead) return;

        _lastFiredTime = Time.time;

        // Çift Kanattan İki Güdümlü Füze Fırlat
        Vector3 leftWing = _playerPlane.transform.position + new Vector3(-0.6f, 0.25f, 0f);
        Vector3 rightWing = _playerPlane.transform.position + new Vector3(0.6f, 0.25f, 0f);

        SpawnMissile(leftWing, Quaternion.Euler(0f, 0f, 15f));
        SpawnMissile(rightWing, Quaternion.Euler(0f, 0f, -15f));

        // Ses ve Hafif İtiş Titremesi
        AudioManager.Instance?.PlayGunfire(isHeavy: true);
        CameraShake.Shake(0.18f, 0.15f);

        // Ekran Uyarısı
        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert("MİKRO FÜZE SALVOSU ATEŞLENDİ!", new Color(0f, 0.9f, 1f), 1.2f);
    }

    private void SpawnMissile(Vector3 pos, Quaternion rot)
    {
        GameObject mObj = new GameObject("HomingMissile");
        mObj.transform.position = pos;
        mObj.transform.rotation = rot;

        HomingMissile missile = mObj.AddComponent<HomingMissile>();
        missile.owner = _playerPlane.gameObject;
    }

    private void SetupUI()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        // Ateş Butonunun (-180, 180) hemen üzerinde (-180, 310) konumlanma
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-180f, 310f);
        rt.sizeDelta = new Vector2(100f, 100f);

        _bgImage = GetComponent<Image>();
        if (_bgImage == null) _bgImage = gameObject.AddComponent<Image>();
        _bgImage.color = new Color(0.10f, 0.18f, 0.28f, 0.92f);

        _outline = GetComponent<Outline>();
        if (_outline == null) _outline = gameObject.AddComponent<Outline>();
        _outline.effectColor = new Color(0f, 0.85f, 1f, 0.9f);
        _outline.effectDistance = new Vector2(2.5f, -2.5f);

        _button = GetComponent<Button>();
        if (_button == null) _button = gameObject.AddComponent<Button>();
        _button.targetGraphic = _bgImage;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(TryLaunchMissiles);

        // Dolum Karartma Göstergesi
        GameObject fillObj = new GameObject("CooldownFill");
        fillObj.transform.SetParent(transform, false);
        RectTransform fillRt = fillObj.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        _cooldownFillImg = fillObj.AddComponent<Image>();
        _cooldownFillImg.color = new Color(0f, 0f, 0f, 0.70f);
        _cooldownFillImg.type = Image.Type.Filled;
        _cooldownFillImg.fillMethod = Image.FillMethod.Radial360;
        _cooldownFillImg.fillOrigin = (int)Image.Origin360.Top;
        _cooldownFillImg.fillClockwise = false;
        _cooldownFillImg.raycastTarget = false;

        // Metin
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        _btnTMP = txtObj.AddComponent<TextMeshProUGUI>();
        _btnTMP.text = "FÜZE\n<size=75%>SALVO</size>";
        _btnTMP.fontSize = 20f;
        _btnTMP.fontStyle = FontStyles.Bold;
        _btnTMP.alignment = TextAlignmentOptions.Center;
        _btnTMP.color = Color.white;
        _btnTMP.raycastTarget = false;
    }

    private void UpdateCooldownVisuals()
    {
        if (_cooldownFillImg == null || _btnTMP == null) return;

        float elapsed = Time.time - _lastFiredTime;

        if (elapsed < cooldownDuration)
        {
            float remaining = cooldownDuration - elapsed;
            _cooldownFillImg.fillAmount = (remaining / cooldownDuration);
            _btnTMP.text = $"{remaining:F1}s";
            _btnTMP.color = new Color(0.7f, 0.7f, 0.7f);
            if (_outline != null) _outline.effectColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        }
        else
        {
            _cooldownFillImg.fillAmount = 0f;
            _btnTMP.text = "FÜZE\n<size=75%>SALVO</size>";
            _btnTMP.color = Color.white;
            if (_outline != null) _outline.effectColor = new Color(0f, 0.85f, 1f, 0.95f);
        }
    }
}

