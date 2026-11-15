using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Yüksek Çözünürlüklü Dokunmatik Sol / Sağ Taktik Uçuş Ok Butonları (D-Pad Touch Controls):
/// - Oyuncu Ok Tuşlarını seçtiğinde sanal joystick otomatik olarak gizlenir.
/// - Sol alt köşede iki adet geniş taktik buton: [ ◀ SOL ] ve [ SAĞ ▶ ].
/// - Model 1 Cyber Cyan 256x256 askeri pahlı neon çerçeve ve 3D taktik ok simgeleri.
/// - Dokunulduğunda ve PC'de klavye (A/D/Oklar) ile basıldığında canlı görsel tepki.
/// - Kontrolcü bileşeni Canvas altında kalıcıdır; UI paneli ise dinamik açılıp kapanır.
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
        // Klavye Girdileri (PC editör testi ve klavye kullanan oyuncular için senkronizasyon)
        bool kbLeft = false;
        bool kbRight = false;
        try
        {
            kbLeft = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            kbRight = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        }
        catch { }

        // Görsel buton basılma animasyonu / parlaması
        bool activeLeft = _isLeftPressed || kbLeft;
        bool activeRight = _isRightPressed || kbRight;

        if (leftButtonObj != null)
        {
            leftButtonObj.transform.localScale = activeLeft ? Vector3.one * 1.08f : Vector3.one;
            var img = leftButtonObj.GetComponent<Image>();
            if (img != null) img.color = activeLeft ? new Color(0.80f, 1f, 1f, 1f) : Color.white;
        }

        if (rightButtonObj != null)
        {
            rightButtonObj.transform.localScale = activeRight ? Vector3.one * 1.08f : Vector3.one;
            var img = rightButtonObj.GetComponent<Image>();
            if (img != null) img.color = activeRight ? new Color(0.80f, 1f, 1f, 1f) : Color.white;
        }

        // Yatay girdi hesaplaması (-1f .. +1f)
        float input = 0f;
        if (activeLeft) input -= 1f;
        if (activeRight) input += 1f;
        HorizontalInput = Mathf.Clamp(input, -1f, 1f);

        // PC Hızlı Test Kısayolu: [C] tuşuyla Joystick / Ok Tuşları arasında anında geçiş
        try
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                GameSettings.ToggleControlType();
                UpdateVisibility();
                Debug.Log($"[TouchArrowControls] Kontrol Şeması Değiştirildi (C Kısayolu): {GameSettings.SelectedControl}");
            }
        }
        catch { }

        UpdateVisibility();
    }

    private void FindP1Joystick()
    {
        if (_p1JoystickObj != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            Transform jTrans = canvas.transform.Find("JoyStickBackRound");
            if (jTrans != null)
            {
                _p1JoystickObj = jTrans.gameObject;
                return;
            }
        }

        var joysticks = FindObjectsByType<VirtualJoystick>(FindObjectsInactive.Include);
        foreach (var j in joysticks)
        {
            if (j.isPlayer1)
            {
                _p1JoystickObj = j.gameObject;
                return;
            }
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

        MainMenuManager menu = FindAnyObjectByType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy) ||
                          (HangarUI.Instance != null && HangarUI.Instance.gameObject.activeInHierarchy) ||
                          (CratesUI.Instance != null && CratesUI.Instance.gameObject.activeInHierarchy) ||
                          (InventoryUI.Instance != null && InventoryUI.Instance.gameObject.activeInHierarchy) ||
                          (DailyOpsUI.Instance != null && DailyOpsUI.Instance.gameObject.activeInHierarchy);

        bool isArrows = (GameSettings.SelectedControl == GameSettings.ControlType.Arrows);
        bool shouldShowArrows = isArrows && !isMenuOpen;
        bool shouldShowJoystick = (!isArrows) && !isMenuOpen;

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
            if (_p1JoystickObj.activeSelf != shouldShowJoystick)
            {
                _p1JoystickObj.SetActive(shouldShowJoystick);
            }
        }
    }

    public void EnsureArrowUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("HUD_TouchArrowControls");
        if (existing != null)
        {
            _rootContainer = existing.gameObject;
        }
        else
        {
            _rootContainer = new GameObject("HUD_TouchArrowControls", typeof(RectTransform));
            _rootContainer.transform.SetParent(canvas.transform, false);

            RectTransform rRt = _rootContainer.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(0f, 0f); // Sol Alt Köşe
            rRt.anchorMax = new Vector2(0f, 0f);
            rRt.pivot = new Vector2(0f, 0f);
            rRt.anchoredPosition = new Vector2(15f, 15f);
            rRt.sizeDelta = new Vector2(380f, 220f);
        }

        // 1. SOL OK BUTONU (◀) - Boyut: 140x130
        Transform leftT = _rootContainer.transform.Find("Btn_ArrowLeft");
        if (leftT != null)
        {
            leftButtonObj = leftT.gameObject;
            Transform lbl = leftT.Find("Label");
            if (lbl != null) Destroy(lbl.gameObject);
        }
        else
        {
            leftButtonObj = CreateArrowButton(_rootContainer.transform, "Btn_ArrowLeft", 
                new Vector2(90f, 105f), "btn_arrow_left", isLeft: true);
        }

        // 2. SAĞ OK BUTONU (▶) - Boyut: 140x130
        Transform rightT = _rootContainer.transform.Find("Btn_ArrowRight");
        if (rightT != null)
        {
            rightButtonObj = rightT.gameObject;
            Transform lbl = rightT.Find("Label");
            if (lbl != null) Destroy(lbl.gameObject);
        }
        else
        {
            rightButtonObj = CreateArrowButton(_rootContainer.transform, "Btn_ArrowRight", 
                new Vector2(245f, 105f), "btn_arrow_right", isLeft: false);
        }

        _rootContainer.transform.SetAsLastSibling();
    }

    private GameObject CreateArrowButton(Transform parent, string name, Vector2 pos, string spriteResName, bool isLeft)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(140f, 130f); // Geniş, dokunması çok konforlu askeri alan

        Image bg = btnObj.AddComponent<Image>();
        Sprite arrowSpr = GetArrowSprite(spriteResName, isLeft);
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
            bg.color = new Color(0.80f, 1f, 1f, 1f);
            btnObj.transform.localScale = Vector3.one * 1.08f;
            AudioManager.Instance?.PlayButtonClick();
        });
        trigger.triggers.Add(downEntry);

        // PointerUp
        var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener((data) =>
        {
            if (isLeft) _isLeftPressed = false;
            else _isRightPressed = false;
            bg.color = Color.white;
            btnObj.transform.localScale = Vector3.one;
        });
        trigger.triggers.Add(upEntry);

        // PointerExit
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) =>
        {
            if (isLeft) _isLeftPressed = false;
            else _isRightPressed = false;
            bg.color = Color.white;
            btnObj.transform.localScale = Vector3.one;
        });
        trigger.triggers.Add(exitEntry);

        return btnObj;
    }

    /// <summary>
    /// Model 1 Cyber Cyan 256x256 taktik ok görselini Resources, Asset veya Runtime Fallback ile yükler.
    /// </summary>
    private Sprite GetArrowSprite(string resName, bool isLeft)
    {
        // 1. Resources üzerinden yükleme dene
        Sprite spr = Resources.Load<Sprite>(resName);
        if (spr != null) return spr;

        // 2. Doğrudan dosya yolundan Texture2D yükleme (Unity editor veya runtime güvenliği için)
        try
        {
            string[] candidatePaths = {
                Path.Combine(Application.dataPath, "Resources", resName + ".png"),
                Path.Combine(Application.dataPath, "Sprites", resName + ".png")
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
        }
        catch { }

        // 3. Runtime Yüksek Kaliteli Cyber Cyan Neon Ok Fallback Çizimi
        return GenerateProceduralArrowSprite(isLeft);
    }

    /// <summary>
    /// Eğer diskte dosya bulunamazsa koddan doğrudan 256x256 Model 1 Cyber Cyan ok oluşturur.
    /// </summary>
    private Sprite GenerateProceduralArrowSprite(bool isLeft)
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color[] cols = new Color[size * size];
        for (int i = 0; i < cols.Length; i++) cols[i] = clear;
        tex.SetPixels(cols);

        Color cyan = new Color(0f, 0.90f, 1f, 1f);
        Color darkBlue = new Color(0.04f, 0.12f, 0.22f, 0.92f);
        Color white = Color.white;

        // Pahlı sekizgen gövde
        for (int y = 10; y < size - 10; y++)
        {
            for (int x = 10; x < size - 10; x++)
            {
                int cornerCut = 35;
                if ((x < cornerCut && y < cornerCut - x) ||
                    (x > size - 1 - cornerCut && y < cornerCut - (size - 1 - x)) ||
                    (x < cornerCut && y > size - 1 - cornerCut + x) ||
                    (x > size - 1 - cornerCut && y > size - 1 - cornerCut + (size - 1 - x)))
                {
                    continue;
                }

                // Dış hat (Border)
                bool isBorder = (x <= 14 || x >= size - 15 || y <= 14 || y >= size - 15);
                tex.SetPixel(x, y, isBorder ? cyan : darkBlue);
            }
        }

        // Taktik Ok Üçgeni
        int cx = size / 2;
        int cy = size / 2 + 10;
        int arrowW = 60;
        int arrowH = 70;

        for (int y = cy - arrowH / 2; y <= cy + arrowH / 2; y++)
        {
            float t = (float)(y - (cy - arrowH / 2)) / arrowH;
            float span = (1f - Mathf.Abs(t - 0.5f) * 2f) * arrowW;

            int startX = isLeft ? (int)(cx + span / 2) : (int)(cx - span / 2);
            int endX = isLeft ? (int)(cx - span / 2) : (int)(cx + span / 2);
            int minX = Mathf.Min(startX, endX);
            int maxX = Mathf.Max(startX, endX);

            for (int x = minX; x <= maxX; x++)
            {
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    tex.SetPixel(x, y, (x == minX || x == maxX) ? white : cyan);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    public static TouchArrowControls EnsureInstance()
    {
        if (Instance != null) return Instance;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        // Yönetici obje Canvas altında kalıcı olarak yer alır
        Transform mgrT = canvas.transform.Find("TouchArrowControlsManager");
        GameObject mgrGo = mgrT != null ? mgrT.gameObject : new GameObject("TouchArrowControlsManager");
        if (mgrT == null) mgrGo.transform.SetParent(canvas.transform, false);

        TouchArrowControls ctrl = mgrGo.GetComponent<TouchArrowControls>() ?? mgrGo.AddComponent<TouchArrowControls>();
        Instance = ctrl;
        ctrl.EnsureArrowUI();
        return ctrl;
    }
}
