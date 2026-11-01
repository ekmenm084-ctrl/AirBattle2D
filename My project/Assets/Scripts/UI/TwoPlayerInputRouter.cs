using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Karşı taraf (Düşman / 2. Oyuncu) mobil kontrolleri (Joystick & Ateş Butonu):
/// - Ekranın üst tarafında her zaman görünür ve aktiftir.
/// - Karşıdaki oyuncu veya deneme için 180° ters çevrilmiştir.
/// - Hem Solo hem Online modda ekranda yer alır ve dokunmaya duyarlıdır.
/// </summary>
public class TwoPlayerInputRouter : MonoBehaviour
{
    public static TwoPlayerInputRouter Instance { get; private set; }

    [Header("Görünürlük")]
    public bool alwaysShowEnemyControls = false; // Sadece 2 oyunculu online modda görünür

    [Header("P2 UI Kökü")]
    public GameObject p2ControlsRoot;

    [Header("P2 Joystick")]
    public VirtualJoystick p2Joystick;

    [Header("P2 Ateş Butonu")]
    public FireButton p2FireButton;

    [Header("Referanslar")]
    public PlaneShooter plane2Shooter;

    [Header("Giriş Ayarları")]
    [Tooltip("P2 uçağı aşağı baktığı ve joystick 180° ters durduğu için P2'nin sağı ekranın soluna denk gelir.")]
    public bool invertP2Joystick = true;

    private bool _twoPlayerMode = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureP2UIHierarchy();
    }

    private void Start()
    {
        EnsureP2UIHierarchy();
        FindPlane2Shooter();

        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);
        SetTwoPlayerMode(isOnline);
    }

    private void Update()
    {
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy);
        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online) ||
                        (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);

        bool shouldShow = isOnline && !isMenuOpen;
        if (p2ControlsRoot != null && p2ControlsRoot.activeSelf != shouldShow)
        {
            p2ControlsRoot.SetActive(shouldShow);
        }
    }

    /// <summary>
    /// Eğer sahnede P2 kontrol elemanları henüz yoksa Canvas altında otomatik inşa eder.
    /// </summary>
    public void EnsureP2UIHierarchy()
    {
        if (p2ControlsRoot != null && p2Joystick != null && p2FireButton != null)
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. P2_ControlsRoot Kontrolü
        Transform existingRoot = canvas.transform.Find("P2_ControlsRoot");
        if (existingRoot != null)
        {
            p2ControlsRoot = existingRoot.gameObject;
        }
        else
        {
            p2ControlsRoot = new GameObject("P2_ControlsRoot");
            p2ControlsRoot.transform.SetParent(canvas.transform, false);

            RectTransform rootRt = p2ControlsRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
        }

        bool isOnline = (GameSettings.SelectedMode == GameSettings.GameMode.Online);
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy);
        p2ControlsRoot.SetActive(isOnline && !isMenuOpen);

        // 2. P2_Joystick Oluştur/Bul
        Transform existingJoy = p2ControlsRoot.transform.Find("P2_Joystick");
        GameObject joyObj = existingJoy != null ? existingJoy.gameObject : null;

        if (joyObj == null)
        {
            joyObj = new GameObject("P2_Joystick");
            joyObj.transform.SetParent(p2ControlsRoot.transform, false);

            RectTransform joyRt = joyObj.AddComponent<RectTransform>();
            joyRt.anchorMin = new Vector2(1f, 1f); // Ekran Sağ Üst (P2'nin Sol Eli)
            joyRt.anchorMax = new Vector2(1f, 1f);
            joyRt.pivot = new Vector2(0.5f, 0.5f);
            joyRt.anchoredPosition = new Vector2(-180f, -180f);
            joyRt.sizeDelta = new Vector2(200f, 200f);
            joyRt.localEulerAngles = new Vector3(0f, 0f, 180f); // P2 için 180° ters

            Image joyBgImg = joyObj.AddComponent<Image>();
            joyBgImg.color = Color.white;

            // Handle (Dokunmatik top)
            GameObject handleObj = new GameObject("P2_JoystickHandle");
            handleObj.transform.SetParent(joyObj.transform, false);

            RectTransform handleRt = handleObj.AddComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(85f, 85f);
            handleRt.anchoredPosition = Vector2.zero;

            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = Color.white;

            p2Joystick = joyObj.AddComponent<VirtualJoystick>();
            p2Joystick.background = joyRt;
            p2Joystick.handle = handleRt;
            p2Joystick.autoPosition = false;
            p2Joystick.isPlayer1 = false;
            p2Joystick.themeColor = new Color(1f, 0.4f, 0.15f, 1f);
            p2Joystick.ApplyProfessionalStyling();
        }
        else
        {
            p2Joystick = joyObj.GetComponent<VirtualJoystick>();
            if (p2Joystick != null)
            {
                p2Joystick.autoPosition = false;
                p2Joystick.isPlayer1 = false;
                p2Joystick.ApplyProfessionalStyling();
            }
        }

        // 3. P2_FireButton Oluştur/Bul
        Transform existingFire = p2ControlsRoot.transform.Find("P2_FireButton");
        GameObject fireObj = existingFire != null ? existingFire.gameObject : null;

        if (fireObj == null)
        {
            fireObj = new GameObject("P2_FireButton");
            fireObj.transform.SetParent(p2ControlsRoot.transform, false);

            RectTransform fireRt = fireObj.AddComponent<RectTransform>();
            fireRt.anchorMin = new Vector2(0f, 1f); // Ekran Sol Üst (P2'nin Sağ Eli)
            fireRt.anchorMax = new Vector2(0f, 1f);
            fireRt.pivot = new Vector2(0.5f, 0.5f);
            fireRt.anchoredPosition = new Vector2(180f, -180f);
            fireRt.sizeDelta = new Vector2(140f, 140f);
            fireRt.localEulerAngles = new Vector3(0f, 0f, 180f); // P2 için 180° ters

            Image fireImg = fireObj.AddComponent<Image>();
            fireImg.color = new Color(0.9f, 0.15f, 0.2f, 0.85f);

            fireObj.AddComponent<Button>();

            // Buton İçi Metin
            GameObject textObj = new GameObject("Text (TMP)");
            textObj.transform.SetParent(fireObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "ATEŞ";
            tmp.fontSize = 22f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            p2FireButton = fireObj.AddComponent<FireButton>();
            p2FireButton.isPlayer1 = false;
            p2FireButton.autoPosition = false;
            p2FireButton.ApplyMilitaryFireStyling();
        }
        else
        {
            p2FireButton = fireObj.GetComponent<FireButton>();
            if (p2FireButton != null)
            {
                p2FireButton.isPlayer1 = false;
                p2FireButton.autoPosition = false;
                p2FireButton.ApplyMilitaryFireStyling();
            }
        }

        // Menü arkasında kalmaması için en öne getir
        p2ControlsRoot.transform.SetAsLastSibling();
        p2ControlsRoot.SetActive(true);
    }

    private void FindPlane2Shooter()
    {
        if (plane2Shooter != null) return;

        var shooters = FindObjectsOfType<PlaneShooter>();
        foreach (var s in shooters)
        {
            var pc = s.GetComponent<PlaneController>();
            if (pc != null && !pc.isPlayer1)
            {
                plane2Shooter = s;
                if (p2FireButton != null)
                {
                    p2FireButton.targetShooter = s;
                }
                break;
            }
        }
    }

    public void SetTwoPlayerMode(bool enabled)
    {
        _twoPlayerMode = enabled;
        EnsureP2UIHierarchy();
        FindPlane2Shooter();

        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy);

        if (p2ControlsRoot != null)
        {
            // Yalnızca 2 oyunculu / online modda VE menü kapalıyken göster
            p2ControlsRoot.SetActive(enabled && !isMenuOpen);
        }
    }

    /// <summary>
    /// P2'nin uçağına iletilecek yatay yön verisi (-1 sol, +1 sağ).
    /// </summary>
    public Vector2 GetP2Input()
    {
        if (p2Joystick == null) return Vector2.zero;

        Vector2 rawDir = p2Joystick.InputDirection;
        return invertP2Joystick ? new Vector2(-rawDir.x, -rawDir.y) : rawDir;
    }
}
