using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Yüksek Çözünürlüklü Dokunmatik Sol / Sağ Taktik Uçuş Ok Butonları (D-Pad Touch Controls):
/// - Oyuncu Ok Tuşlarını seçtiğinde sanal joystick otomatik olarak gizlenir.
/// - Sol alt köşede iki adet geniş taktik buton: [ ◀ SOL ] ve [ SAĞ ▶ ].
/// - Askeri pahlı neon cyan çerçeve ve 3D taktik ok simgeleri.
/// - Basılı tutulduğunda sürekli girdi üretir, bırakıldığında uçağı dengeler.
/// </summary>
public class TouchArrowControls : MonoBehaviour
{
    public static TouchArrowControls Instance { get; private set; }

    public static float HorizontalInput { get; private set; } = 0f;

    [Header("Buton Nesneleri")]
    public GameObject leftButtonObj;
    public GameObject rightButtonObj;

    private bool _isLeftPressed = false;
    private bool _isRightPressed = false;
    private GameObject _rootContainer;
    private GameObject _p1JoystickObj;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        GameSettings.LoadSettings();
        GameSettings.OnControlSchemeChanged += HandleControlSchemeChanged;
    }

    private void OnDestroy()
    {
        GameSettings.OnControlSchemeChanged -= HandleControlSchemeChanged;
    }

    private void Start()
    {
        EnsureArrowUI();
        FindP1Joystick();
        UpdateVisibility();
    }

    private void Update()
    {
        // Girdi hesaplaması
        float input = 0f;
        if (_isLeftPressed) input -= 1f;
        if (_isRightPressed) input += 1f;
        HorizontalInput = input;

        UpdateVisibility();
    }

    private void FindP1Joystick()
    {
        if (_p1JoystickObj != null) return;

        var joysticks = FindObjectsOfType<VirtualJoystick>(true);
        foreach (var j in joysticks)
        {
            if (j.isPlayer1)
            {
                _p1JoystickObj = j.gameObject;
                break;
            }
        }

        if (_p1JoystickObj == null)
        {
            var go = GameObject.Find("JoyStickBackRound");
            if (go != null) _p1JoystickObj = go;
        }
    }

    private void HandleControlSchemeChanged(GameSettings.ControlType control)
    {
        UpdateVisibility();
    }

    public void UpdateVisibility()
    {
        if (_rootContainer == null)
        {
            EnsureArrowUI();
        }

        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy) ||
                          (HangarUI.Instance != null && HangarUI.Instance.gameObject.activeInHierarchy) ||
                          (CratesUI.Instance != null && CratesUI.Instance.gameObject.activeInHierarchy) ||
                          (InventoryUI.Instance != null && InventoryUI.Instance.gameObject.activeInHierarchy) ||
                          (DailyOpsUI.Instance != null && DailyOpsUI.Instance.gameObject.activeInHierarchy);

        bool isArrows = (GameSettings.SelectedControl == GameSettings.ControlType.Arrows);
        bool shouldShowArrows = isArrows && !isMenuOpen;

        if (_rootContainer != null)
        {
            if (_rootContainer.activeSelf != shouldShowArrows)
            {
                _rootContainer.SetActive(shouldShowArrows);
            }
            if (shouldShowArrows)
            {
                _rootContainer.transform.SetAsLastSibling();
            }
        }

        if (_p1JoystickObj == null) FindP1Joystick();

        if (_p1JoystickObj != null)
        {
            bool shouldShowJoystick = (!isArrows) && !isMenuOpen;
            if (_p1JoystickObj.activeSelf != shouldShowJoystick)
            {
                _p1JoystickObj.SetActive(shouldShowJoystick);
            }
        }
    }

    public void EnsureArrowUI()
    {
        if (_rootContainer != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("HUD_TouchArrowControls");
        if (existing != null)
        {
            _rootContainer = existing.gameObject;
            return;
        }

        _rootContainer = new GameObject("HUD_TouchArrowControls", typeof(RectTransform));
        _rootContainer.transform.SetParent(canvas.transform, false);

        RectTransform rRt = _rootContainer.GetComponent<RectTransform>();
        rRt.anchorMin = new Vector2(0f, 0f); // Sol Alt Köşe
        rRt.anchorMax = new Vector2(0f, 0f);
        rRt.pivot = new Vector2(0f, 0f);
        rRt.anchoredPosition = Vector2.zero;
        rRt.sizeDelta = new Vector2(400f, 260f);

        // 1. SOL OK BUTONU (◀ SOL) - Boyut: 140x125
        leftButtonObj = CreateArrowButton(_rootContainer.transform, "Btn_ArrowLeft", 
            new Vector2(105f, 150f), "btn_arrow_left", "SOL", isLeft: true);

        // 2. SAĞ OK BUTONU (SAĞ ▶) - Boyut: 140x125
        rightButtonObj = CreateArrowButton(_rootContainer.transform, "Btn_ArrowRight", 
            new Vector2(255f, 150f), "btn_arrow_right", "SAĞ", isLeft: false);

        _rootContainer.SetActive(GameSettings.SelectedControl == GameSettings.ControlType.Arrows);
    }

    private GameObject CreateArrowButton(Transform parent, string name, Vector2 pos, string spriteResName, string label, bool isLeft)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(140f, 125f); // Geniş, dokunması çok konforlu alan

        Image bg = btnObj.AddComponent<Image>();
        Sprite arrowSpr = Resources.Load<Sprite>(spriteResName);
        if (arrowSpr != null)
        {
            bg.sprite = arrowSpr;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = true;
            bg.color = Color.white;
        }
        else
        {
            bg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f, 0.95f), new Color(0.04f, 0.10f, 0.18f, 0.92f), true);
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }

        // Dokunmatik Girdi Dinleyicisi
        EventTrigger trigger = btnObj.AddComponent<EventTrigger>();

        // PointerDown
        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener((data) =>
        {
            if (isLeft) _isLeftPressed = true;
            else _isRightPressed = true;
            bg.color = new Color(0.85f, 1f, 1f, 1f);
            rt.localScale = Vector3.one * 1.10f;
        });
        trigger.triggers.Add(downEntry);

        // PointerUp
        var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener((data) =>
        {
            if (isLeft) _isLeftPressed = false;
            else _isRightPressed = false;
            bg.color = Color.white;
            rt.localScale = Vector3.one;
        });
        trigger.triggers.Add(upEntry);

        // PointerExit
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) =>
        {
            if (isLeft) _isLeftPressed = false;
            else _isRightPressed = false;
            bg.color = Color.white;
            rt.localScale = Vector3.one;
        });
        trigger.triggers.Add(exitEntry);

        // Alt Taktik Yazı: [ SOL ] veya [ SAĞ ]
        GameObject txtObj = new GameObject("Label", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0f);
        tRt.anchorMax = new Vector2(1f, 0.35f);
        tRt.pivot = new Vector2(0.5f, 0f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = $"<color=#00E5FF><b>[ {label} ]</b></color>";
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 2.0f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.extraPadding = true;

        return btnObj;
    }

    public static TouchArrowControls EnsureInstance()
    {
        if (Instance != null) return Instance;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("HUD_TouchArrowControls");
        GameObject go = existing != null ? existing.gameObject : new GameObject("HUD_TouchArrowControls", typeof(RectTransform));
        if (existing == null) go.transform.SetParent(canvas.transform, false);

        TouchArrowControls ctrl = go.GetComponent<TouchArrowControls>() ?? go.AddComponent<TouchArrowControls>();
        Instance = ctrl;
        ctrl.EnsureArrowUI();
        return ctrl;
    }
}
