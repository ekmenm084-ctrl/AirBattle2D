using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Isı Güdümlü Mikro Füze Salvo Butonu (Ultimate Ability UI):
/// - Dokunmatik buton ve klavye kısayolu (Q veya X tuşu).
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
        SetupUI();
    }

    private void OnEnable()
    {
        SetupUI();
    }

    private void Start()
    {
        SetupUI();
        FindPlayerPlane();
    }

    private float _lastP2FiredTime = -999f;
    public bool IsP2Ready => (Time.time >= _lastP2FiredTime + cooldownDuration);

    private void Update()
    {
        UpdateCooldownVisuals();

        if (Time.timeScale <= 0.01f) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameEnded) return;

        bool isTwoPlayer = (GameSettings.SelectedMode == GameSettings.GameMode.Online) ||
                           (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);

        // P1 Klavye Kısayolları (Q veya X)
        try
        {
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.X))
            {
                TryLaunchMissiles();
            }

            // P2 Klavye Kısayolları (Keypad 3, P, veya /)
            if (isTwoPlayer)
            {
                if (Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Slash))
                {
                    TryLaunchMissilesForP2();
                }
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
        Vector3 leftWing = _playerPlane.transform.position + new Vector3(-0.45f, 0.18f, 0f);
        Vector3 rightWing = _playerPlane.transform.position + new Vector3(0.45f, 0.18f, 0f);

        SpawnMissile(leftWing, Quaternion.Euler(0f, 0f, 15f), _playerPlane.gameObject);
        SpawnMissile(rightWing, Quaternion.Euler(0f, 0f, -15f), _playerPlane.gameObject);

        // Ses ve Hafif İtiş Titremesi
        AudioManager.Instance?.PlayMissileLaunch();
        CameraShake.Shake(0.18f, 0.15f);

        // Ekran Uyarısı
        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert("<color=#00E5FF>[P1] MİKRO FÜZE SALVOSU ATEŞLENDİ!</color>", new Color(0f, 0.9f, 1f), 1.2f);
    }

    public void TryLaunchMissilesForP2()
    {
        if (!IsP2Ready) return;

        PlaneController p2Plane = null;
        var planes = FindObjectsOfType<PlaneController>();
        foreach (var p in planes)
        {
            if (!p.isPlayer1)
            {
                p2Plane = p;
                break;
            }
        }
        if (p2Plane == null) return;

        PlaneHealth health = p2Plane.GetComponent<PlaneHealth>();
        if (health != null && health.IsDead) return;

        _lastP2FiredTime = Time.time;

        // P2 aşağı baktığı için füzeler aşağı doğru yayılır
        Vector3 leftWing = p2Plane.transform.position + new Vector3(-0.45f, -0.18f, 0f);
        Vector3 rightWing = p2Plane.transform.position + new Vector3(0.45f, -0.18f, 0f);

        SpawnMissile(leftWing, Quaternion.Euler(0f, 0f, 195f), p2Plane.gameObject);
        SpawnMissile(rightWing, Quaternion.Euler(0f, 0f, 165f), p2Plane.gameObject);

        AudioManager.Instance?.PlayMissileLaunch();
        CameraShake.Shake(0.18f, 0.15f);

        var gameUI = FindObjectOfType<GameUI>();
        gameUI?.ShowWaveAlert("<color=#FF5252>[P2] MİKRO FÜZE SALVOSU ATEŞLENDİ!</color>", new Color(1f, 0.35f, 0.35f), 1.2f);
    }

    public static HomingMissile SpawnMissile(Vector3 pos, Quaternion rot, GameObject ownerGo)
    {
        GameObject mObj = new GameObject("HomingMissile");
        mObj.transform.position = pos;
        mObj.transform.rotation = rot;

        HomingMissile missile = mObj.AddComponent<HomingMissile>();
        missile.owner = ownerGo;
        return missile;
    }

    private static Sprite _cachedMissileSprite;

    private static Sprite LoadMissile1To1Sprite()
    {
        if (_cachedMissileSprite != null) return _cachedMissileSprite;

        string path = System.IO.Path.Combine(Application.dataPath, "Sprites/hud_btn_missile_1to1.png");
        if (System.IO.File.Exists(path))
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                _cachedMissileSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return _cachedMissileSprite;
            }
        }

        Sprite res = Resources.Load<Sprite>("hud_btn_missile_1to1");
        if (res != null)
        {
            _cachedMissileSprite = res;
            return res;
        }

        Texture2D resTex = Resources.Load<Texture2D>("hud_btn_missile_1to1");
        if (resTex != null)
        {
            _cachedMissileSprite = Sprite.Create(resTex, new Rect(0, 0, resTex.width, resTex.height), new Vector2(0.5f, 0.5f), 100f);
            return _cachedMissileSprite;
        }
        return null;
    }

    private void SetupUI()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // 1:1 Referans yerleşim: Ateş butonunun sol üst çaprazında (-395, 205), roket ikonu + kare kutu (154 x 110)
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-395f, 205f);
        rt.sizeDelta = new Vector2(154f, 110f);

        _bgImage = GetComponent<Image>();
        if (_bgImage == null) _bgImage = gameObject.AddComponent<Image>();

        _outline = GetComponent<Outline>();
        if (_outline != null) Destroy(_outline);

        Sprite mSpr = LoadMissile1To1Sprite();
        if (mSpr != null)
        {
            _bgImage.sprite = mSpr;
            _bgImage.type = Image.Type.Simple;
            _bgImage.preserveAspect = true;
            _bgImage.color = Color.white;
        }
        else
        {
            _bgImage.color = new Color(0.10f, 0.18f, 0.28f, 0.92f);
        }

        _button = GetComponent<Button>();
        if (_button == null) _button = gameObject.AddComponent<Button>();
        _button.targetGraphic = _bgImage;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(TryLaunchMissiles);

        // Dolum Karartma Göstergesi (Sadece sağdaki kare kutu alanı: x = 0.31..0.99)
        Transform existingFill = transform.Find("CooldownFill");
        GameObject fillObj = existingFill != null ? existingFill.gameObject : new GameObject("CooldownFill", typeof(RectTransform));
        fillObj.transform.SetParent(transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0.31f, 0.02f);
        fillRt.anchorMax = new Vector2(0.99f, 0.98f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        _cooldownFillImg = fillObj.GetComponent<Image>() ?? fillObj.AddComponent<Image>();
        _cooldownFillImg.color = new Color(0f, 0f, 0f, 0.68f);
        _cooldownFillImg.type = Image.Type.Filled;
        _cooldownFillImg.fillMethod = Image.FillMethod.Radial360;
        _cooldownFillImg.fillOrigin = (int)Image.Origin360.Top;
        _cooldownFillImg.fillClockwise = false;
        _cooldownFillImg.raycastTarget = false;

        // Metin (Sağdaki kare kutunun tam ortasında: x = 0.31..0.99)
        Transform existingTxt = transform.Find("Text");
        GameObject txtObj = existingTxt != null ? existingTxt.gameObject : new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(transform, false);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = new Vector2(0.31f, 0f);
        txtRt.anchorMax = new Vector2(0.99f, 1f);
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        _btnTMP = txtObj.GetComponent<TextMeshProUGUI>() ?? txtObj.AddComponent<TextMeshProUGUI>();
        _btnTMP.font = MainMenuManager.GetTacticalFont();
        _btnTMP.text = "FÜZE\n<size=62%><color=#60A5FA>[ Q ]</color></size>";
        _btnTMP.fontSize = 23f;
        _btnTMP.fontStyle = FontStyles.Normal;
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
            _btnTMP.color = new Color(0.75f, 0.82f, 0.90f);
        }
        else
        {
            _cooldownFillImg.fillAmount = 0f;
            _btnTMP.text = "FÜZE\n<size=62%><color=#60A5FA>[ Q ]</color></size>";
            _btnTMP.color = Color.white;
        }
    }
}

