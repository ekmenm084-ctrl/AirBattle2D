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

    [Header("P2 Manevra Butonu")]
    public Button p2DodgeButton;

    [Header("P2 Füze Butonu")]
    public Button p2MissileButton;

    [Header("Referanslar")]
    public PlaneShooter plane2Shooter;
    public PlayerCombatManeuvers plane2Maneuvers;

    [Header("Giriş Ayarları")]
    [Tooltip("P2 uçağı aşağı baktığı ve joystick 180° ters durduğu için P2'nin sağı ekranın soluna denk gelir.")]
    public bool invertP2Joystick = true;

    private bool _twoPlayerMode = false;
    private GameObject _p2ArrowsRoot;
    private float _p2ArrowHorizInput = 0f;

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

        bool isPaused = AudioSettingsUI.IsGamePaused || (GameOverUI.Instance != null && GameOverUI.Instance.IsShowing);
        bool shouldShow = isOnline && !isMenuOpen && !isPaused;
        if (p2ControlsRoot != null && p2ControlsRoot.activeSelf != shouldShow)
        {
            p2ControlsRoot.SetActive(shouldShow);
        }

        if (shouldShow && p2Joystick != null)
        {
            bool isArrows = (GameSettings.SelectedControl == GameSettings.ControlType.Arrows);
            if (p2Joystick.gameObject.activeSelf == isArrows)
                p2Joystick.gameObject.SetActive(!isArrows);
            if (_p2ArrowsRoot != null && _p2ArrowsRoot.activeSelf != isArrows)
                _p2ArrowsRoot.SetActive(isArrows);
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
            p2ControlsRoot = new GameObject("P2_ControlsRoot", typeof(RectTransform));
            p2ControlsRoot.transform.SetParent(canvas.transform, false);

            RectTransform rootRt = p2ControlsRoot.GetComponent<RectTransform>();
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
            joyObj = new GameObject("P2_Joystick", typeof(RectTransform));
            joyObj.transform.SetParent(p2ControlsRoot.transform, false);

            RectTransform joyRt = joyObj.GetComponent<RectTransform>();
            joyRt.anchorMin = new Vector2(1f, 1f); // Dikey 2P VS Ekran Sağ Üst (P2'nin Sol Eli)
            joyRt.anchorMax = new Vector2(1f, 1f);
            joyRt.pivot = new Vector2(0.5f, 0.5f);
            joyRt.anchoredPosition = new Vector2(-148f, -148f);
            joyRt.sizeDelta = new Vector2(246f, 246f);
            joyRt.localEulerAngles = new Vector3(0f, 0f, 180f); // P2 için 180° ters

            Image joyBgImg = joyObj.AddComponent<Image>();
            joyBgImg.color = Color.white;

            // Handle (Dokunmatik top)
            GameObject handleObj = new GameObject("P2_JoystickHandle", typeof(RectTransform));
            handleObj.transform.SetParent(joyObj.transform, false);

            RectTransform handleRt = handleObj.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(104f, 104f);
            handleRt.anchoredPosition = Vector2.zero;

            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = Color.white;

            p2Joystick = joyObj.AddComponent<VirtualJoystick>();
            p2Joystick.background = joyRt;
            p2Joystick.handle = handleRt;
            p2Joystick.autoPosition = true;
            p2Joystick.isPlayer1 = false;
            p2Joystick.themeColor = new Color(1f, 0.4f, 0.15f, 1f);
            p2Joystick.ApplyProfessionalStyling();
        }
        else
        {
            p2Joystick = joyObj.GetComponent<VirtualJoystick>();
            if (p2Joystick != null)
            {
                p2Joystick.autoPosition = true;
                p2Joystick.isPlayer1 = false;
                p2Joystick.ApplyProfessionalStyling();
            }
        }

        // 2B. P2_TouchArrows (Ok kontrolü seçildiyse P2'nin sol eli için Sağ Üstte simetrik 180° yön tuşları)
        EnsureP2TouchArrows();

        // 3. P2_FireButton Oluştur/Bul (Dikey 1080p genişliğe tam simetrik: 132, -128)
        Transform existingFire = p2ControlsRoot.transform.Find("P2_FireButton");
        GameObject fireObj = existingFire != null ? existingFire.gameObject : null;

        if (fireObj == null)
        {
            fireObj = new GameObject("P2_FireButton", typeof(RectTransform));
            fireObj.transform.SetParent(p2ControlsRoot.transform, false);

            RectTransform fireRt = fireObj.GetComponent<RectTransform>();
            fireRt.anchorMin = new Vector2(0f, 1f); // Ekran Sol Üst (P2'nin Sağ Eli)
            fireRt.anchorMax = new Vector2(0f, 1f);
            fireRt.pivot = new Vector2(0.5f, 0.5f);
            fireRt.anchoredPosition = new Vector2(132f, -128f);
            fireRt.sizeDelta = new Vector2(204f, 204f);
            fireRt.localEulerAngles = new Vector3(0f, 0f, 180f); // P2 için 180° ters

            Image fireImg = fireObj.AddComponent<Image>();
            fireImg.color = new Color(0.9f, 0.15f, 0.2f, 0.85f);

            fireObj.AddComponent<Button>();

            // Buton İçi Metin
            GameObject textObj = new GameObject("Text (TMP)", typeof(RectTransform));
            textObj.transform.SetParent(fireObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "<b>ATEŞ</b>";
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            p2FireButton = fireObj.AddComponent<FireButton>();
            p2FireButton.isPlayer1 = false;
            p2FireButton.autoPosition = true;
            p2FireButton.ApplyMilitaryFireStyling();
        }
        else
        {
            p2FireButton = fireObj.GetComponent<FireButton>();
            if (p2FireButton != null)
            {
                p2FireButton.isPlayer1 = false;
                p2FireButton.autoPosition = true;
                p2FireButton.ApplyMilitaryFireStyling();
            }
        }

        // 4. P2_DodgeButton (Manevra Butonu: 318, -72)
        Transform existingDodge = p2ControlsRoot.transform.Find("P2_DodgeButton");
        GameObject dodgeObj = existingDodge != null ? existingDodge.gameObject : null;

        if (dodgeObj == null)
        {
            dodgeObj = new GameObject("P2_DodgeButton", typeof(RectTransform));
            dodgeObj.transform.SetParent(p2ControlsRoot.transform, false);

            RectTransform dodgeRt = dodgeObj.GetComponent<RectTransform>();
            dodgeRt.anchorMin = new Vector2(0f, 1f);
            dodgeRt.anchorMax = new Vector2(0f, 1f);
            dodgeRt.pivot = new Vector2(0.5f, 0.5f);
            dodgeRt.anchoredPosition = new Vector2(318f, -72f);
            dodgeRt.sizeDelta = new Vector2(124f, 84f);
            dodgeRt.localEulerAngles = new Vector3(0f, 0f, 180f);

            Image dodgeImg = dodgeObj.AddComponent<Image>();
            dodgeImg.color = new Color(0.08f, 0.16f, 0.25f, 0.92f);

            Outline dodgeOutline = dodgeObj.AddComponent<Outline>();
            dodgeOutline.effectColor = new Color(1f, 0.5f, 0.2f, 0.85f);
            dodgeOutline.effectDistance = new Vector2(2f, -2f);

            p2DodgeButton = dodgeObj.AddComponent<Button>();
            p2DodgeButton.targetGraphic = dodgeImg;
            p2DodgeButton.onClick.RemoveAllListeners();
            p2DodgeButton.onClick.AddListener(() =>
            {
                if (plane2Maneuvers == null) FindPlane2Shooter();
                plane2Maneuvers?.TryDodge();
            });

            GameObject dTextObj = new GameObject("Text (TMP)", typeof(RectTransform));
            dTextObj.transform.SetParent(dodgeObj.transform, false);
            RectTransform dtRt = dTextObj.GetComponent<RectTransform>();
            dtRt.anchorMin = Vector2.zero;
            dtRt.anchorMax = Vector2.one;
            dtRt.offsetMin = Vector2.zero;
            dtRt.offsetMax = Vector2.zero;

            TextMeshProUGUI dTmp = dTextObj.AddComponent<TextMeshProUGUI>();
            dTmp.text = "KAÇIŞ";
            dTmp.fontSize = 20f;
            dTmp.fontStyle = FontStyles.Bold;
            dTmp.alignment = TextAlignmentOptions.Center;
            dTmp.color = new Color(1f, 0.75f, 0.4f);
            dTmp.raycastTarget = false;
        }
        else
        {
            p2DodgeButton = dodgeObj.GetComponent<Button>();
            if (p2DodgeButton != null)
            {
                p2DodgeButton.onClick.RemoveAllListeners();
                p2DodgeButton.onClick.AddListener(() =>
                {
                    if (plane2Maneuvers == null) FindPlane2Shooter();
                    plane2Maneuvers?.TryDodge();
                });
            }
        }

        // 5. P2_MissileButton (Füze Butonu: 318, -180)
        Transform existingMissile = p2ControlsRoot.transform.Find("P2_MissileButton");
        GameObject missileObj = existingMissile != null ? existingMissile.gameObject : null;

        if (missileObj == null)
        {
            missileObj = new GameObject("P2_MissileButton", typeof(RectTransform));
            missileObj.transform.SetParent(p2ControlsRoot.transform, false);

            RectTransform missileRt = missileObj.GetComponent<RectTransform>();
            missileRt.anchorMin = new Vector2(0f, 1f);
            missileRt.anchorMax = new Vector2(0f, 1f);
            missileRt.pivot = new Vector2(0.5f, 0.5f);
            missileRt.anchoredPosition = new Vector2(318f, -180f);
            missileRt.sizeDelta = new Vector2(124f, 88f);
            missileRt.localEulerAngles = new Vector3(0f, 0f, 180f);

            Image missileImg = missileObj.AddComponent<Image>();
            missileImg.color = new Color(0.12f, 0.16f, 0.28f, 0.92f);

            Outline missileOutline = missileObj.AddComponent<Outline>();
            missileOutline.effectColor = new Color(0.2f, 0.85f, 1f, 0.85f);
            missileOutline.effectDistance = new Vector2(2f, -2f);

            p2MissileButton = missileObj.AddComponent<Button>();
            p2MissileButton.targetGraphic = missileImg;
            p2MissileButton.onClick.RemoveAllListeners();
            p2MissileButton.onClick.AddListener(() =>
            {
                MissileLauncherButton.Instance?.TryLaunchMissilesForP2();
            });

            GameObject mTextObj = new GameObject("Text (TMP)", typeof(RectTransform));
            mTextObj.transform.SetParent(missileObj.transform, false);
            RectTransform mtRt = mTextObj.GetComponent<RectTransform>();
            mtRt.anchorMin = Vector2.zero;
            mtRt.anchorMax = Vector2.one;
            mtRt.offsetMin = Vector2.zero;
            mtRt.offsetMax = Vector2.zero;

            TextMeshProUGUI mTmp = mTextObj.AddComponent<TextMeshProUGUI>();
            mTmp.text = "FÜZE\n<size=65%>[P]</size>";
            mTmp.fontSize = 18f;
            mTmp.fontStyle = FontStyles.Bold;
            mTmp.alignment = TextAlignmentOptions.Center;
            mTmp.color = Color.white;
            mTmp.raycastTarget = false;
        }
        else
        {
            p2MissileButton = missileObj.GetComponent<Button>();
            if (p2MissileButton != null)
            {
                p2MissileButton.onClick.RemoveAllListeners();
                p2MissileButton.onClick.AddListener(() =>
                {
                    MissileLauncherButton.Instance?.TryLaunchMissilesForP2();
                });
            }
        }

        // Menü arkasında kalmaması için en öne getir
        p2ControlsRoot.transform.SetAsLastSibling();
        p2ControlsRoot.SetActive(isOnline && !isMenuOpen);
    }

    private void FindPlane2Shooter()
    {
        if (plane2Shooter != null && plane2Maneuvers != null) return;

        var planes = FindObjectsOfType<PlaneController>();
        foreach (var pc in planes)
        {
            if (pc != null && !pc.isPlayer1)
            {
                plane2Shooter = pc.GetComponent<PlaneShooter>();
                plane2Maneuvers = pc.GetComponent<PlayerCombatManeuvers>();

                if (p2FireButton != null && plane2Shooter != null)
                {
                    p2FireButton.targetShooter = plane2Shooter;
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

    private void EnsureP2TouchArrows()
    {
        if (p2ControlsRoot == null) return;
        Transform existing = p2ControlsRoot.transform.Find("P2_TouchArrows");
        if (existing != null)
        {
            _p2ArrowsRoot = existing.gameObject;
            return;
        }

        _p2ArrowsRoot = new GameObject("P2_TouchArrows", typeof(RectTransform));
        _p2ArrowsRoot.transform.SetParent(p2ControlsRoot.transform, false);
        RectTransform rootRt = _p2ArrowsRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 1f); // Ekran Sağ Üst (P2'nin Sol Eli)
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 1f);
        rootRt.anchoredPosition = new Vector2(-18f, -18f);
        rootRt.sizeDelta = new Vector2(320f, 150f);

        Sprite sprL = Resources.Load<Sprite>("btn_arrow_left");
        Sprite sprR = Resources.Load<Sprite>("btn_arrow_right");

        // Ekranın soluna (merkeze doğru: -X) götüren ok butonu
        CreateP2ArrowButton(_p2ArrowsRoot.transform, "P2_BtnScreenLeft", new Vector2(-240f, -72f), sprL, dirX: -1f);
        // Ekranın sağına (sağ kenara doğru: +X) götüren ok butonu
        CreateP2ArrowButton(_p2ArrowsRoot.transform, "P2_BtnScreenRight", new Vector2(-78f, -72f), sprR, dirX: 1f);

        _p2ArrowsRoot.SetActive(GameSettings.SelectedControl == GameSettings.ControlType.Arrows);
    }

    private void CreateP2ArrowButton(Transform parent, string name, Vector2 pos, Sprite spr, float dirX)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(148f, 136f);

        Image img = btnObj.AddComponent<Image>();
        if (spr != null)
        {
            img.sprite = spr;
            img.preserveAspect = true;
            img.color = new Color(1f, 0.58f, 0.48f, 1f); // P2 Kızıl-Mercan tonu
        }
        else
        {
            img.color = new Color(0.85f, 0.25f, 0.20f, 0.90f);
        }

        var trigger = btnObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var down = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
        down.callback.AddListener((_) => { _p2ArrowHorizInput = dirX; btnObj.transform.localScale = Vector3.one * 1.08f; });
        trigger.triggers.Add(down);

        var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
        up.callback.AddListener((_) => { if (Mathf.Approximately(_p2ArrowHorizInput, dirX)) _p2ArrowHorizInput = 0f; btnObj.transform.localScale = Vector3.one; });
        trigger.triggers.Add(up);
    }

    /// <summary>
    /// P2'nin uçağına iletilecek yatay yön verisi (-1 sol, +1 sağ).
    /// </summary>
    public Vector2 GetP2Input()
    {
        if (GameSettings.SelectedControl == GameSettings.ControlType.Arrows && Mathf.Abs(_p2ArrowHorizInput) > 0.01f)
            return new Vector2(_p2ArrowHorizInput, 0f);

        if (p2Joystick == null) return Vector2.zero;

        Vector2 rawDir = p2Joystick.InputDirection;
        return invertP2Joystick ? new Vector2(-rawDir.x, -rawDir.y) : rawDir;
    }
}
