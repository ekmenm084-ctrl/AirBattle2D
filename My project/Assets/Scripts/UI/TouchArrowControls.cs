using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Dokunmatik Sol / Sağ Taktik Uçuş Ok Butonları (D-Pad Touch Controls):
/// - Joystick yerine alternatif hassas yönlendirme sistemi.
/// - Sol alt köşede ergonomik iki adet geniş dokunmatik taktik buton: [ ◀ SOL ] ve [ SAĞ ▶ ].
/// - Basılı tutulduğunda sürekli girdi üretir, bırakıldığında uçağı dengeler.
/// - GameSettings.SelectedControl seçimine göre Joystick ile dinamik olarak takas edilir.
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
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy) ||
                          (HangarUI.Instance != null && HangarUI.Instance.gameObject.activeInHierarchy) ||
                          (CratesUI.Instance != null && CratesUI.Instance.gameObject.activeInHierarchy) ||
                          (InventoryUI.Instance != null && InventoryUI.Instance.gameObject.activeInHierarchy) ||
                          (DailyOpsUI.Instance != null && DailyOpsUI.Instance.gameObject.activeInHierarchy);

        bool isArrows = (GameSettings.SelectedControl == GameSettings.ControlType.Arrows);
        bool shouldShowArrows = isArrows && !isMenuOpen;

        if (_rootContainer != null && _rootContainer.activeSelf != shouldShowArrows)
        {
            _rootContainer.SetActive(shouldShowArrows);
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

        // 1. SOL OK BUTONU (◀ SOL)
        leftButtonObj = CreateArrowButton(_rootContainer.transform, "Btn_ArrowLeft", 
            new Vector2(95f, 130f), "◀", "SOL", isLeft: true);

        // 2. SAĞ OK BUTONU (SAĞ ▶)
        rightButtonObj = CreateArrowButton(_rootContainer.transform, "Btn_ArrowRight", 
            new Vector2(235f, 130f), "▶", "SAĞ", isLeft: false);
    }

    private GameObject CreateArrowButton(Transform parent, string name, Vector2 pos, string arrowIcon, string label, bool isLeft)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(125f, 105f); // Geniş, dokunması çok konforlu alan

        Image bg = btnObj.AddComponent<Image>();
        bg.sprite = MainMenuManager.GetHudSprite("hud_btn_cyan", new Color(0f, 0.85f, 1f, 0.95f), new Color(0.04f, 0.10f, 0.18f, 0.92f), true);
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Dokunmatik Girdi Dinleyicisi
        EventTrigger trigger = btnObj.AddComponent<EventTrigger>();

        // PointerDown
        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener((data) =>
        {
            if (isLeft) _isLeftPressed = true;
            else _isRightPressed = true;
            bg.color = new Color(1f, 1f, 1f, 1f);
            rt.localScale = Vector3.one * 1.08f;
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

        // PointerExit (Parmağı buton dışına kaydırdığında basılı kalmasın)
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) =>
        {
            if (isLeft) _isLeftPressed = false;
            else _isRightPressed = false;
            bg.color = Color.white;
            rt.localScale = Vector3.one;
        });
        trigger.triggers.Add(exitEntry);

        // İçerik Yazısı: [ ◀ ] ve [ SOL ]
        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = $"<size=150%><b>{arrowIcon}</b></size>\n<size=75%><color=#A0E0FF>{label}</color></size>";
        tmp.fontSize = 26f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.6f;
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
