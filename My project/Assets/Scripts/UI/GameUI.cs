using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gösterişli Taktik Askeri Kokpit Can Barları ve Sefer HUD'ı:
/// - Oyuncu 1 Can Barı: Sol Alt Köşe (Joystick'in üstünde, Cyan neon)
/// - Oyuncu 2 / AI Can Barı: Sol Üst Köşe (Solo ise Boss / Düşman ilerlemesi)
/// - Ekran Üstü Sefer İlerleme Rozeti: 🎯 DÜŞMAN: 1 / 20
/// - Boss & Düşman İlerleme Uyarı Banner'ları.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Can Çubukları (Sliders)")]
    public Slider plane1HealthBar;
    public Slider plane2HealthBar;

    [Header("İsim & Yüzde Etiketleri")]
    public Text plane1Label;
    public Text plane2Label;

    [Header("Uçak Referansları")]
    public PlaneHealth plane1Health;
    public PlaneHealth plane2Health;

    private float _targetP1Health = 1f;
    private float _targetP2Health = 1f;
    private Image _p1FillImage;
    private Image _p2FillImage;
    private TextMeshProUGUI _p1TmpLabel;
    private TextMeshProUGUI _p2TmpLabel;

    private TextMeshProUGUI _waveBadgeTMP;
    private GameObject _waveBadgeObj;
    private GameObject _waveAlertObj;
    private TextMeshProUGUI _waveAlertTMP;
    private Coroutine _alertCoroutine;

    private void Awake()
    {
        MainMenuManager.OptimizeCanvasForTablet();
    }

    private void Start()
    {
        MainMenuManager.OptimizeCanvasForTablet();
        FindPlanesIfNull();

        SetupHealthBar(plane1HealthBar, isPlayer: true);
        SetupHealthBar(plane2HealthBar, isPlayer: false);

        SetupWaveBadge();
        SetupWaveAlertObject();
        SetupRedZoneOverlay();
        SetupModeIntroBanner();
        SetupMissileButton();
        SetupGoldHUD();
        SetupComboHUD();
        SetupAirspaceBanner();
        SetupUltiBar();
        SetupCombatManeuverButtons();

        UpdateHealthBars();

        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy);
        if (isMenuOpen)
        {
            SetHUDVisibility(false);
        }
    }

    private bool _isHUDHiddenByCinematic = false;

    public void SetHUDVisibility(bool visible)
    {
        _isHUDHiddenByCinematic = !visible;
        if (_waveBadgeObj != null) _waveBadgeObj.SetActive(visible);
        if (plane1HealthBar != null) plane1HealthBar.gameObject.SetActive(visible);
        if (plane2HealthBar != null) plane2HealthBar.gameObject.SetActive(visible);
        if (_missileBtnObj != null) _missileBtnObj.SetActive(visible);
        if (_goldHUDObj != null) _goldHUDObj.SetActive(visible);
        if (_comboHUDObj != null) _comboHUDObj.SetActive(visible);
        if (!visible && _airspaceBannerObj != null) _airspaceBannerObj.SetActive(false);
        if (_ultiBarObj != null) _ultiBarObj.SetActive(visible);
        if (_dodgeBtnObj != null) _dodgeBtnObj.SetActive(visible);
        if (_ultiBtnObj != null) _ultiBtnObj.SetActive(visible);
        if (!visible && _redZoneOverlayObj != null) _redZoneOverlayObj.SetActive(false);
        if (!visible && _modeIntroObj != null) _modeIntroObj.SetActive(false);
        if (!visible && _waveAlertObj != null) _waveAlertObj.SetActive(false);
    }

    private void FindPlanesIfNull()
    {
        if (plane1Health == null || plane2Health == null)
        {
            var planes = FindObjectsOfType<PlaneHealth>();
            foreach (var p in planes)
            {
                var pc = p.GetComponent<PlaneController>();
                if (pc != null)
                {
                    if (pc.isPlayer1 && plane1Health == null) plane1Health = p;
                    else if (!pc.isPlayer1 && plane2Health == null) plane2Health = p;
                }
            }
        }
    }

    private void SetupHealthBar(Slider slider, bool isPlayer)
    {
        if (slider == null) return;

        RectTransform rt = slider.GetComponent<RectTransform>();
        if (rt != null)
        {
            if (isPlayer)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(35f, -25f);
                rt.sizeDelta = new Vector2(320f, 28f);
            }
            else
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -25f);
                rt.sizeDelta = new Vector2(520f, 30f);
            }
        }

        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        Transform handleArea = slider.transform.Find("Handle Slide Area");
        if (handleArea != null) handleArea.gameObject.SetActive(false);

        Transform bg = slider.transform.Find("Background");
        if (bg != null)
        {
            Image bgImg = bg.GetComponent<Image>();
            if (bgImg != null)
                bgImg.color = new Color(0.08f, 0.11f, 0.16f, 0.85f);
        }

        Transform fillArea = slider.transform.Find("Fill Area");
        if (fillArea != null)
        {
            RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
            if (fillAreaRt != null)
            {
                fillAreaRt.anchorMin = Vector2.zero;
                fillAreaRt.anchorMax = Vector2.one;
                fillAreaRt.offsetMin = Vector2.zero;
                fillAreaRt.offsetMax = Vector2.zero;
            }

            Transform fill = fillArea.Find("Fill");
            if (fill != null)
            {
                Image fillImg = fill.GetComponent<Image>();
                if (fillImg != null)
                {
                    if (isPlayer)
                    {
                        fillImg.color = new Color(0f, 0.9f, 1f);
                        _p1FillImage = fillImg;
                    }
                    else
                    {
                        fillImg.color = new Color(1f, 0.2f, 0.25f);
                        _p2FillImage = fillImg;
                    }
                }
            }
        }

        CreateHUDLabel(slider, isPlayer);
    }

    private void CreateHUDLabel(Slider slider, bool isPlayer)
    {
        GameObject labelObj = EnsureUIGameObject(slider.transform, "HUD_Label");
        RectTransform rt = EnsureComponent<RectTransform>(labelObj);

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 2f);
        rt.sizeDelta = new Vector2(0f, 24f);

        TextMeshProUGUI tmp = EnsureComponent<TextMeshProUGUI>(labelObj);

        tmp.fontSize = isPlayer ? 16f : 17f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;

        if (isPlayer)
        {
            tmp.color = new Color(0.4f, 0.9f, 1f);
            tmp.text = "[P1] OYUNCU 1: %100";
            _p1TmpLabel = tmp;
        }
        else
        {
            tmp.color = new Color(1f, 0.4f, 0.4f);
            tmp.text = "[DÜŞMAN] %100";
            _p2TmpLabel = tmp;
        }
    }

    private void SetupWaveBadge()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject badgeObj = EnsureUIGameObject(canvas.transform, "HUD_WaveBadge");
        _waveBadgeObj = badgeObj;

        RectTransform rt = EnsureComponent<RectTransform>(badgeObj);
        rt.anchorMin = new Vector2(0.5f, 1f); // Üst Orta
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -62f);
        rt.sizeDelta = new Vector2(380f, 32f);

        Image bg = EnsureComponent<Image>(badgeObj);
        bg.color = new Color(0.06f, 0.09f, 0.14f, 0.85f);

        Outline outline = EnsureComponent<Outline>(badgeObj);
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textObj = EnsureUIGameObject(badgeObj.transform, "Text");
        RectTransform textRt = EnsureComponent<RectTransform>(textObj);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        _waveBadgeTMP = EnsureComponent<TextMeshProUGUI>(textObj);

        _waveBadgeTMP.fontSize = 18f;
        _waveBadgeTMP.fontStyle = FontStyles.Bold;
        _waveBadgeTMP.alignment = TextAlignmentOptions.Center;
        _waveBadgeTMP.color = new Color(0.95f, 0.95f, 1f);
        
        string initialDiffTag = GameSettings.SelectedDifficulty switch
        {
            GameSettings.Difficulty.Kolay => "<size=80%><color=#2ECC71>[KOLAY]</color></size>",
            GameSettings.Difficulty.Zor   => "<size=80%><color=#E74C3C><b>[ZOR]</b></color></size>",
            _                             => "<size=80%><color=#F39C12>[NORMAL]</color></size>"
        };
        _waveBadgeTMP.text = $"HAREKAT: 1 / 20 {initialDiffTag}";
    }

    private void SetupWaveAlertObject()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _waveAlertObj = EnsureUIGameObject(canvas.transform, "HUD_WaveAlert");

        RectTransform rt = EnsureComponent<RectTransform>(_waveAlertObj);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(800f, 100f);

        _waveAlertTMP = EnsureComponent<TextMeshProUGUI>(_waveAlertObj);
        _waveAlertTMP.fontSize = 32f;
        _waveAlertTMP.fontStyle = FontStyles.Bold;
        _waveAlertTMP.alignment = TextAlignmentOptions.Center;
        _waveAlertTMP.raycastTarget = false;

        _waveAlertObj.SetActive(false);
    }

    private GameObject _redZoneOverlayObj;
    private Image _redZoneBgImage;
    private TextMeshProUGUI _redZoneAlertTMP;
    private Coroutine _redZoneCoroutine;

    private void SetupRedZoneOverlay()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _redZoneOverlayObj = EnsureUIGameObject(canvas.transform, "HUD_RedZoneOverlay");

        RectTransform rt = EnsureComponent<RectTransform>(_redZoneOverlayObj);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _redZoneBgImage = EnsureComponent<Image>(_redZoneOverlayObj);
        _redZoneBgImage.color = new Color(0.9f, 0.05f, 0.05f, 0f);
        _redZoneBgImage.raycastTarget = false;

        // Uyarı Başlık Metni
        GameObject bannerObj = EnsureUIGameObject(_redZoneOverlayObj.transform, "BannerText");
        RectTransform bannerRt = EnsureComponent<RectTransform>(bannerObj);
        bannerRt.anchorMin = new Vector2(0f, 0.5f);
        bannerRt.anchorMax = new Vector2(1f, 0.5f);
        bannerRt.pivot = new Vector2(0.5f, 0.5f);
        bannerRt.anchoredPosition = new Vector2(0f, 85f);
        bannerRt.sizeDelta = new Vector2(0f, 180f);

        _redZoneAlertTMP = EnsureComponent<TextMeshProUGUI>(bannerObj);
        _redZoneAlertTMP.fontSize = 38f;
        _redZoneAlertTMP.fontStyle = FontStyles.Bold;
        _redZoneAlertTMP.alignment = TextAlignmentOptions.Center;
        _redZoneAlertTMP.raycastTarget = false;

        var outline = EnsureComponent<Outline>(bannerObj);
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        _redZoneOverlayObj.SetActive(false);
    }

    private GameObject _modeIntroObj;
    private TextMeshProUGUI _modeIntroTitleTMP;
    private TextMeshProUGUI _modeIntroSubtitleTMP;
    private TextMeshProUGUI _modeIntroDescTMP;
    private CanvasGroup _modeIntroCanvasGroup;
    private Outline _modeIntroOutline;
    private Coroutine _modeIntroCoroutine;

    private static GameObject EnsureUIGameObject(Transform parent, string name)
    {
        Transform child = parent != null ? parent.Find(name) : null;
        if (child != null) return child.gameObject;
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private void SetupModeIntroBanner()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _modeIntroObj = EnsureUIGameObject(canvas.transform, "HUD_ModeIntroBanner");

        RectTransform rt = EnsureComponent<RectTransform>(_modeIntroObj);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 75f);
        rt.sizeDelta = new Vector2(760f, 180f);

        Image bg = EnsureComponent<Image>(_modeIntroObj);
        bg.color = new Color(0.03f, 0.07f, 0.13f, 0.94f);
        bg.raycastTarget = false;

        _modeIntroOutline = EnsureComponent<Outline>(_modeIntroObj);
        _modeIntroOutline.effectColor = new Color(0f, 0.85f, 1f, 0.9f);
        _modeIntroOutline.effectDistance = new Vector2(2.5f, -2.5f);

        _modeIntroCanvasGroup = EnsureComponent<CanvasGroup>(_modeIntroObj);

        // 1. Başlık
        GameObject titleObj = EnsureUIGameObject(_modeIntroObj.transform, "TitleText");
        RectTransform titleRt = EnsureComponent<RectTransform>(titleObj);
        titleRt.anchorMin = new Vector2(0f, 0.60f);
        titleRt.anchorMax = new Vector2(1f, 0.98f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        _modeIntroTitleTMP = EnsureComponent<TextMeshProUGUI>(titleObj);
        _modeIntroTitleTMP.fontSize = 28f;
        _modeIntroTitleTMP.fontStyle = FontStyles.Bold;
        _modeIntroTitleTMP.alignment = TextAlignmentOptions.Center;
        _modeIntroTitleTMP.raycastTarget = false;

        // 2. Alt Başlık
        GameObject subObj = EnsureUIGameObject(_modeIntroObj.transform, "SubtitleText");
        RectTransform subRt = EnsureComponent<RectTransform>(subObj);
        subRt.anchorMin = new Vector2(0f, 0.30f);
        subRt.anchorMax = new Vector2(1f, 0.62f);
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;
        _modeIntroSubtitleTMP = EnsureComponent<TextMeshProUGUI>(subObj);
        _modeIntroSubtitleTMP.fontSize = 18f;
        _modeIntroSubtitleTMP.fontStyle = FontStyles.Bold;
        _modeIntroSubtitleTMP.alignment = TextAlignmentOptions.Center;
        _modeIntroSubtitleTMP.raycastTarget = false;

        // 3. Açıklama & Hazır Mesajı
        GameObject descObj = EnsureUIGameObject(_modeIntroObj.transform, "DescText");
        RectTransform descRt = EnsureComponent<RectTransform>(descObj);
        descRt.anchorMin = new Vector2(0f, 0.04f);
        descRt.anchorMax = new Vector2(1f, 0.32f);
        descRt.offsetMin = Vector2.zero;
        descRt.offsetMax = Vector2.zero;
        _modeIntroDescTMP = EnsureComponent<TextMeshProUGUI>(descObj);
        _modeIntroDescTMP.fontSize = 15f;
        _modeIntroDescTMP.fontStyle = FontStyles.Normal;
        _modeIntroDescTMP.alignment = TextAlignmentOptions.Center;
        _modeIntroDescTMP.raycastTarget = false;

        _modeIntroObj.SetActive(false);
    }

    private GameObject _goldHUDObj;
    private TextMeshProUGUI _goldHUDTMP;
    private Outline _goldHUDOutline;
    private Coroutine _goldPulseCoroutine;

    private void SetupGoldHUD()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _goldHUDObj = EnsureUIGameObject(canvas.transform, "HUD_GoldBadge");

        RectTransform rt = EnsureComponent<RectTransform>(_goldHUDObj);
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-35f, -25f);
        rt.sizeDelta = new Vector2(200f, 36f);

        Image bg = EnsureComponent<Image>(_goldHUDObj);
        bg.color = new Color(0.06f, 0.09f, 0.15f, 0.90f);

        _goldHUDOutline = EnsureComponent<Outline>(_goldHUDObj);
        _goldHUDOutline.effectColor = new Color(1f, 0.85f, 0.2f, 0.85f);
        _goldHUDOutline.effectDistance = new Vector2(2f, -2f);

        GameObject textObj = EnsureUIGameObject(_goldHUDObj.transform, "Text");
        RectTransform textRt = EnsureComponent<RectTransform>(textObj);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        _goldHUDTMP = EnsureComponent<TextMeshProUGUI>(textObj);

        _goldHUDTMP.fontSize = 18f;
        _goldHUDTMP.fontStyle = FontStyles.Bold;
        _goldHUDTMP.alignment = TextAlignmentOptions.Center;
        _goldHUDTMP.color = new Color(1f, 0.88f, 0.25f);

        int currentGold = HangarManager.EnsureInstance().gold;
        _goldHUDTMP.text = $"ALTIN: {currentGold:N0} G";
    }

    public void UpdateGoldHUD(int totalGold, int addedAmount = 0)
    {
        if (_goldHUDObj == null || _goldHUDTMP == null)
            SetupGoldHUD();

        if (_goldHUDObj != null)
        {
            RectTransform rt = _goldHUDObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-35f, -25f);
                rt.sizeDelta = new Vector2(200f, 36f);
            }
        }

        if (_goldHUDTMP != null)
        {
            _goldHUDTMP.text = $"ALTIN: {totalGold:N0} G";

            if (addedAmount > 0)
            {
                if (_goldPulseCoroutine != null)
                    StopCoroutine(_goldPulseCoroutine);
                _goldPulseCoroutine = StartCoroutine(GoldPulseRoutine());
            }
        }
    }

    private IEnumerator GoldPulseRoutine()
    {
        if (_goldHUDTMP == null) yield break;

        _goldHUDTMP.color = Color.white;
        _goldHUDTMP.transform.localScale = Vector3.one * 1.15f;

        yield return new WaitForSecondsRealtime(0.18f);

        if (_goldHUDTMP != null)
        {
            _goldHUDTMP.color = new Color(1f, 0.88f, 0.25f);
            _goldHUDTMP.transform.localScale = Vector3.one;
        }
        _goldPulseCoroutine = null;
    }

    private GameObject _comboHUDObj;
    private TextMeshProUGUI _comboMainTMP;
    private TextMeshProUGUI _comboSubTMP;
    private Image _comboFillImg;
    private Outline _comboOutline;
    private Coroutine _comboPulseCoroutine;

    private void SetupComboHUD()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _comboHUDObj = EnsureUIGameObject(canvas.transform, "HUD_ComboBadge");

        RectTransform rt = EnsureComponent<RectTransform>(_comboHUDObj);
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-35f, -68f);
        rt.sizeDelta = new Vector2(200f, 44f);

        Image bg = EnsureComponent<Image>(_comboHUDObj);
        bg.color = new Color(0.05f, 0.08f, 0.14f, 0.88f);

        _comboOutline = EnsureComponent<Outline>(_comboHUDObj);
        _comboOutline.effectColor = new Color(0.18f, 0.85f, 1f, 0.70f);
        _comboOutline.effectDistance = new Vector2(1.8f, -1.8f);

        GameObject mainObj = EnsureUIGameObject(_comboHUDObj.transform, "MainText");
        RectTransform mainRt = EnsureComponent<RectTransform>(mainObj);
        mainRt.anchorMin = new Vector2(0f, 0.48f);
        mainRt.anchorMax = new Vector2(1f, 0.96f);
        mainRt.offsetMin = new Vector2(8f, 0f);
        mainRt.offsetMax = new Vector2(-8f, -2f);

        _comboMainTMP = EnsureComponent<TextMeshProUGUI>(mainObj);
        _comboMainTMP.fontSize = 18f;
        _comboMainTMP.fontStyle = FontStyles.Bold;
        _comboMainTMP.alignment = TextAlignmentOptions.Center;
        _comboMainTMP.color = new Color(0.75f, 0.88f, 1f);
        _comboMainTMP.raycastTarget = false;
        _comboMainTMP.text = "COMBO x1.0";

        GameObject subObj = EnsureUIGameObject(_comboHUDObj.transform, "SubText");
        RectTransform subRt = EnsureComponent<RectTransform>(subObj);
        subRt.anchorMin = new Vector2(0f, 0.14f);
        subRt.anchorMax = new Vector2(1f, 0.48f);
        subRt.offsetMin = new Vector2(6f, 0f);
        subRt.offsetMax = new Vector2(-6f, 0f);

        _comboSubTMP = EnsureComponent<TextMeshProUGUI>(subObj);
        _comboSubTMP.fontSize = 11f;
        _comboSubTMP.fontStyle = FontStyles.Bold;
        _comboSubTMP.alignment = TextAlignmentOptions.Center;
        _comboSubTMP.color = new Color(0.85f, 0.92f, 0.98f);
        _comboSubTMP.raycastTarget = false;
        _comboSubTMP.text = "SIYIRMA: 0  |  <color=#00FFAA>HASARSIZ</color>";

        GameObject barBgObj = EnsureUIGameObject(_comboHUDObj.transform, "DecayBarBg");
        RectTransform barBgRt = EnsureComponent<RectTransform>(barBgObj);
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot = new Vector2(0.5f, 0f);
        barBgRt.anchoredPosition = new Vector2(0f, 2f);
        barBgRt.sizeDelta = new Vector2(-10f, 4f);

        Image barBgImg = EnsureComponent<Image>(barBgObj);
        barBgImg.color = new Color(0.12f, 0.16f, 0.24f, 0.9f);
        barBgImg.raycastTarget = false;

        GameObject barFillObj = EnsureUIGameObject(barBgObj.transform, "DecayBarFill");
        RectTransform barFillRt = EnsureComponent<RectTransform>(barFillObj);
        barFillRt.anchorMin = Vector2.zero;
        barFillRt.anchorMax = Vector2.one;
        barFillRt.offsetMin = Vector2.zero;
        barFillRt.offsetMax = Vector2.zero;

        _comboFillImg = EnsureComponent<Image>(barFillObj);
        _comboFillImg.type = Image.Type.Filled;
        _comboFillImg.fillMethod = Image.FillMethod.Horizontal;
        _comboFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        _comboFillImg.color = new Color(0.18f, 0.90f, 1f, 0.95f);
        _comboFillImg.fillAmount = 0f;
        _comboFillImg.raycastTarget = false;
    }

    public void PulseComboHUD()
    {
        if (_comboHUDObj == null || !_comboHUDObj.activeInHierarchy) return;
        if (_comboPulseCoroutine != null) StopCoroutine(_comboPulseCoroutine);
        _comboPulseCoroutine = StartCoroutine(ComboPulseRoutine());
    }

    private IEnumerator ComboPulseRoutine()
    {
        if (_comboHUDObj == null) yield break;
        _comboHUDObj.transform.localScale = Vector3.one * 1.12f;
        yield return new WaitForSecondsRealtime(0.14f);
        if (_comboHUDObj != null)
            _comboHUDObj.transform.localScale = Vector3.one;
        _comboPulseCoroutine = null;
    }

    private void UpdateComboHUD()
    {
        if (_comboHUDObj == null) return;

        bool hideForMode = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);
        if (hideForMode)
        {
            if (_comboHUDObj.activeSelf) _comboHUDObj.SetActive(false);
            return;
        }

        CombatComboManager combo = CombatComboManager.Instance;
        if (combo == null)
        {
            if (_comboMainTMP != null) _comboMainTMP.text = "COMBO x1.0";
            if (_comboSubTMP != null) _comboSubTMP.text = "SIYIRMA: 0  |  <color=#00FFAA>HASARSIZ</color>";
            if (_comboFillImg != null) _comboFillImg.fillAmount = 0f;
            return;
        }

        Color tierCol = combo.GetTierColor();
        float mult = combo.GetMultiplier();

        if (_comboMainTMP != null)
        {
            _comboMainTMP.color = tierCol;
            _comboMainTMP.text = (combo.currentCombo > 0)
                ? $"COMBO x{mult:0.0}  [{combo.currentCombo} {combo.GetTierLabel()}]"
                : "COMBO x1.0";
        }

        if (_comboSubTMP != null)
        {
            string flawlessTxt = combo.waveTookDamage
                ? "<color=#FF7043>HASAR ALINDI</color>"
                : (combo.noDamageWavesStreak > 0
                    ? $"<color=#00FFAA>HASARSIZ x{combo.noDamageWavesStreak}</color>"
                    : "<color=#00FFAA>HASARSIZ</color>");
            _comboSubTMP.text = $"SIYIRMA: {combo.totalGrazes}  |  {flawlessTxt}";
        }

        if (_comboFillImg != null)
        {
            _comboFillImg.fillAmount = combo.ComboTimerNormalized;
            _comboFillImg.color = tierCol;
        }

        if (_comboOutline != null)
        {
            _comboOutline.effectColor = new Color(tierCol.r, tierCol.g, tierCol.b, combo.currentCombo > 0 ? 0.90f : 0.45f);
        }
    }

    private GameObject _airspaceBannerObj;
    private TextMeshProUGUI _airspaceBannerTMP;
    private Outline _airspaceBannerOutline;

    private void SetupAirspaceBanner()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _airspaceBannerObj = EnsureUIGameObject(canvas.transform, "HUD_AirspaceBanner");

        RectTransform rt = EnsureComponent<RectTransform>(_airspaceBannerObj);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -100f);
        rt.sizeDelta = new Vector2(480f, 34f);

        Image bg = EnsureComponent<Image>(_airspaceBannerObj);
        bg.color = new Color(0.05f, 0.08f, 0.14f, 0.88f);
        bg.raycastTarget = false;

        _airspaceBannerOutline = EnsureComponent<Outline>(_airspaceBannerObj);
        _airspaceBannerOutline.effectColor = new Color(0.3f, 0.9f, 1f, 0.8f);
        _airspaceBannerOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject txtObj = EnsureUIGameObject(_airspaceBannerObj.transform, "Text");
        RectTransform txtRt = EnsureComponent<RectTransform>(txtObj);
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(8f, 0f);
        txtRt.offsetMax = new Vector2(-8f, 0f);

        _airspaceBannerTMP = EnsureComponent<TextMeshProUGUI>(txtObj);
        _airspaceBannerTMP.fontSize = 16f;
        _airspaceBannerTMP.fontStyle = FontStyles.Bold;
        _airspaceBannerTMP.alignment = TextAlignmentOptions.Center;
        _airspaceBannerTMP.raycastTarget = false;

        _airspaceBannerObj.SetActive(false);
    }

    private void UpdateAirspaceBanner()
    {
        if (_airspaceBannerObj == null) return;

        AirspaceHazardDirector dir = AirspaceHazardDirector.Instance;
        bool shouldShow = (dir != null && dir.HasActiveBanner &&
            (GameManager.Instance == null || (!GameManager.Instance.IsGameEnded && GameManager.Instance.CurrentMode != GameManager.GameMode.TwoPlayer)));

        if (_airspaceBannerObj.activeSelf != shouldShow)
            _airspaceBannerObj.SetActive(shouldShow);

        if (!shouldShow) return;

        if (_airspaceBannerTMP != null)
        {
            _airspaceBannerTMP.text = dir.ActiveBannerText;
            _airspaceBannerTMP.color = dir.ActiveBannerColor;
        }
        if (_airspaceBannerOutline != null)
        {
            Color c = dir.ActiveBannerColor;
            _airspaceBannerOutline.effectColor = new Color(c.r, c.g, c.b, 0.85f);
        }
    }

    private GameObject _missileBtnObj;

    private void SetupMissileButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("MissileLauncherButton");
        if (existing != null)
        {
            _missileBtnObj = existing.gameObject;
        }
        else
        {
            _missileBtnObj = new GameObject("MissileLauncherButton", typeof(RectTransform));
            _missileBtnObj.transform.SetParent(canvas.transform, false);
            _missileBtnObj.AddComponent<MissileLauncherButton>();
        }
    }

    [Header("Ulti Barı ve Savaş Manevraları UI")]
    private GameObject _ultiBarObj;
    private Image _ultiBarFillImg;
    private TextMeshProUGUI _ultiBarTMP;
    private Outline _ultiBarOutline;

    private GameObject _dodgeBtnObj;
    private Image _dodgeFillImg;
    private TextMeshProUGUI _dodgeBtnTMP;
    private Outline _dodgeBtnOutline;

    private GameObject _ultiBtnObj;
    private Image _ultiFillImg;
    private TextMeshProUGUI _ultiBtnTMP;
    private Outline _ultiBtnOutline;
    private Vector3 _ultiBtnBaseScale = Vector3.one;

    private void SetupUltiBar()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _ultiBarObj = EnsureUIGameObject(canvas.transform, "HUD_UltiBar");

        RectTransform rt = EnsureComponent<RectTransform>(_ultiBarObj);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(35f, -60f);
        rt.sizeDelta = new Vector2(320f, 20f);

        Image bg = EnsureComponent<Image>(_ultiBarObj);
        bg.color = new Color(0.06f, 0.10f, 0.16f, 0.90f);

        _ultiBarOutline = EnsureComponent<Outline>(_ultiBarObj);
        _ultiBarOutline.effectColor = new Color(0f, 0.85f, 1f, 0.85f);
        _ultiBarOutline.effectDistance = new Vector2(2f, -2f);

        GameObject fillObj = EnsureUIGameObject(_ultiBarObj.transform, "Fill");
        RectTransform fillRt = EnsureComponent<RectTransform>(fillObj);
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);

        _ultiBarFillImg = EnsureComponent<Image>(fillObj);
        _ultiBarFillImg.type = Image.Type.Filled;
        _ultiBarFillImg.fillMethod = Image.FillMethod.Horizontal;
        _ultiBarFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        _ultiBarFillImg.color = new Color(0f, 0.95f, 1f, 0.95f);
        _ultiBarFillImg.fillAmount = 0f;

        GameObject txtObj = EnsureUIGameObject(_ultiBarObj.transform, "Text");
        RectTransform txtRt = EnsureComponent<RectTransform>(txtObj);
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        _ultiBarTMP = EnsureComponent<TextMeshProUGUI>(txtObj);
        _ultiBarTMP.fontSize = 14f;
        _ultiBarTMP.fontStyle = FontStyles.Bold;
        _ultiBarTMP.alignment = TextAlignmentOptions.Center;
        _ultiBarTMP.color = Color.white;
        _ultiBarTMP.text = "[ULTİ] SÜPER GÜÇ: %0";
        _ultiBarTMP.raycastTarget = false;
    }

    private void SetupCombatManeuverButtons()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. KAÇIŞ (DODGE ROLL) BUTONU
        _dodgeBtnObj = EnsureUIGameObject(canvas.transform, "HUD_DodgeButton");

        RectTransform dRt = EnsureComponent<RectTransform>(_dodgeBtnObj);
        dRt.anchorMin = new Vector2(1f, 0f);
        dRt.anchorMax = new Vector2(1f, 0f);
        dRt.pivot = new Vector2(0.5f, 0.5f);
        dRt.anchoredPosition = new Vector2(-285f, 105f);
        dRt.sizeDelta = new Vector2(90f, 90f);

        Image dImg = EnsureComponent<Image>(_dodgeBtnObj);
        dImg.color = new Color(0.08f, 0.16f, 0.25f, 0.94f);

        _dodgeBtnOutline = EnsureComponent<Outline>(_dodgeBtnObj);
        _dodgeBtnOutline.effectColor = new Color(0f, 0.85f, 1f, 0.9f);
        _dodgeBtnOutline.effectDistance = new Vector2(2f, -2f);

        Button dBtn = EnsureComponent<Button>(_dodgeBtnObj);
        dBtn.targetGraphic = dImg;
        dBtn.onClick.RemoveAllListeners();
        dBtn.onClick.AddListener(() =>
        {
            PlayerCombatManeuvers.Instance?.TryDodge();
        });

        GameObject dFillObj = EnsureUIGameObject(_dodgeBtnObj.transform, "CooldownFill");
        RectTransform dFillRt = EnsureComponent<RectTransform>(dFillObj);
        dFillRt.anchorMin = Vector2.zero;
        dFillRt.anchorMax = Vector2.one;
        dFillRt.offsetMin = Vector2.zero;
        dFillRt.offsetMax = Vector2.zero;

        _dodgeFillImg = EnsureComponent<Image>(dFillObj);
        _dodgeFillImg.color = new Color(0f, 0f, 0f, 0.72f);
        _dodgeFillImg.type = Image.Type.Filled;
        _dodgeFillImg.fillMethod = Image.FillMethod.Radial360;
        _dodgeFillImg.fillOrigin = (int)Image.Origin360.Top;
        _dodgeFillImg.fillClockwise = false;
        _dodgeFillImg.raycastTarget = false;
        _dodgeFillImg.fillAmount = 0f;

        GameObject dTxtObj = EnsureUIGameObject(_dodgeBtnObj.transform, "Text");
        RectTransform dTxtRt = EnsureComponent<RectTransform>(dTxtObj);
        dTxtRt.anchorMin = Vector2.zero;
        dTxtRt.anchorMax = Vector2.one;
        dTxtRt.offsetMin = Vector2.zero;
        dTxtRt.offsetMax = Vector2.zero;

        _dodgeBtnTMP = EnsureComponent<TextMeshProUGUI>(dTxtObj);
        _dodgeBtnTMP.text = "KAÇIŞ\n<size=65%>[SHIFT]</size>";
        _dodgeBtnTMP.fontSize = 17f;
        _dodgeBtnTMP.fontStyle = FontStyles.Bold;
        _dodgeBtnTMP.alignment = TextAlignmentOptions.Center;
        _dodgeBtnTMP.color = Color.white;
        _dodgeBtnTMP.raycastTarget = false;

        // 2. ULTİ (SÜPER YETENEK) BUTONU
        _ultiBtnObj = EnsureUIGameObject(canvas.transform, "HUD_UltiButton");

        RectTransform uRt = EnsureComponent<RectTransform>(_ultiBtnObj);
        uRt.anchorMin = new Vector2(1f, 0f);
        uRt.anchorMax = new Vector2(1f, 0f);
        uRt.pivot = new Vector2(0.5f, 0.5f);
        uRt.anchoredPosition = new Vector2(-130f, 280f);
        uRt.sizeDelta = new Vector2(95f, 95f);
        _ultiBtnBaseScale = Vector3.one;

        Image uImg = EnsureComponent<Image>(_ultiBtnObj);
        uImg.color = new Color(0.18f, 0.12f, 0.04f, 0.94f);

        _ultiBtnOutline = EnsureComponent<Outline>(_ultiBtnObj);
        _ultiBtnOutline.effectColor = new Color(1f, 0.85f, 0.2f, 0.85f);
        _ultiBtnOutline.effectDistance = new Vector2(2f, -2f);

        Button uBtn = EnsureComponent<Button>(_ultiBtnObj);
        uBtn.targetGraphic = uImg;
        uBtn.onClick.RemoveAllListeners();
        uBtn.onClick.AddListener(() =>
        {
            PlayerCombatManeuvers.Instance?.TryActivateUltimate();
        });

        GameObject uFillObj = EnsureUIGameObject(_ultiBtnObj.transform, "ChargeFill");
        RectTransform uFillRt = EnsureComponent<RectTransform>(uFillObj);
        uFillRt.anchorMin = Vector2.zero;
        uFillRt.anchorMax = Vector2.one;
        uFillRt.offsetMin = Vector2.zero;
        uFillRt.offsetMax = Vector2.zero;

        _ultiFillImg = EnsureComponent<Image>(uFillObj);
        _ultiFillImg.color = new Color(0f, 0f, 0f, 0.70f);
        _ultiFillImg.type = Image.Type.Filled;
        _ultiFillImg.fillMethod = Image.FillMethod.Radial360;
        _ultiFillImg.fillOrigin = (int)Image.Origin360.Top;
        _ultiFillImg.fillClockwise = false;
        _ultiFillImg.raycastTarget = false;
        _ultiFillImg.fillAmount = 1f;

        GameObject uTxtObj = EnsureUIGameObject(_ultiBtnObj.transform, "Text");
        RectTransform uTxtRt = EnsureComponent<RectTransform>(uTxtObj);
        uTxtRt.anchorMin = Vector2.zero;
        uTxtRt.anchorMax = Vector2.one;
        uTxtRt.offsetMin = Vector2.zero;
        uTxtRt.offsetMax = Vector2.zero;

        _ultiBtnTMP = EnsureComponent<TextMeshProUGUI>(uTxtObj);
        _ultiBtnTMP.text = "ULTİ\n<size=65%>[E]</size>";
        _ultiBtnTMP.fontSize = 18f;
        _ultiBtnTMP.fontStyle = FontStyles.Bold;
        _ultiBtnTMP.alignment = TextAlignmentOptions.Center;
        _ultiBtnTMP.color = Color.white;
        _ultiBtnTMP.raycastTarget = false;
    }

    private void UpdateUltiAndManeuverVisuals()
    {
        var pcm = PlayerCombatManeuvers.Instance;
        if (pcm == null) return;

        // 1. ULTİ BARI
        if (_ultiBarFillImg != null && _ultiBarTMP != null)
        {
            if (pcm.isOverdriveActive)
            {
                float fill = Mathf.Clamp01(pcm.overdriveRemaining / pcm.overdriveDuration);
                _ultiBarFillImg.fillAmount = fill;
                _ultiBarFillImg.color = new Color(1f, 0.6f, 0.1f);
                _ultiBarTMP.text = $"[AŞIRI YÜK] {pcm.overdriveRemaining:F1}s";
                if (_ultiBarOutline != null) _ultiBarOutline.effectColor = new Color(1f, 0.85f, 0.2f);
            }
            else
            {
                float fill = Mathf.Clamp01(pcm.currentUlti / pcm.maxUlti);
                _ultiBarFillImg.fillAmount = fill;

                if (pcm.IsUltiReady)
                {
                    float pulse = Mathf.PingPong(Time.time * 4f, 1f);
                    _ultiBarFillImg.color = Color.Lerp(new Color(0f, 0.95f, 1f), new Color(1f, 0.85f, 0.2f), pulse);
                    _ultiBarTMP.text = "[ULTİ] SÜPER GÜÇ: %100 [HAZIR! • E]";
                    if (_ultiBarOutline != null) _ultiBarOutline.effectColor = Color.yellow;
                }
                else
                {
                    _ultiBarFillImg.color = new Color(0f, 0.85f, 1f);
                    _ultiBarTMP.text = $"[ULTİ] SÜPER GÜÇ: %{(int)pcm.currentUlti}";
                    if (_ultiBarOutline != null) _ultiBarOutline.effectColor = new Color(0f, 0.85f, 1f, 0.85f);
                }
            }
        }

        // 2. KAÇIŞ BUTONU
        if (_dodgeFillImg != null && _dodgeBtnTMP != null)
        {
            float rem = pcm.DodgeCooldownRemaining;
            if (rem > 0.05f)
            {
                _dodgeFillImg.fillAmount = (rem / pcm.dodgeCooldown);
                _dodgeBtnTMP.text = $"{rem:F1}s";
                _dodgeBtnTMP.color = new Color(0.7f, 0.7f, 0.7f);
                if (_dodgeBtnOutline != null) _dodgeBtnOutline.effectColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            }
            else
            {
                _dodgeFillImg.fillAmount = 0f;
                _dodgeBtnTMP.text = "KAÇIŞ\n<size=65%>[SHIFT]</size>";
                _dodgeBtnTMP.color = Color.white;
                if (_dodgeBtnOutline != null) _dodgeBtnOutline.effectColor = new Color(0f, 0.85f, 1f, 0.95f);
            }
        }

        // 3. ULTİ BUTONU
        if (_ultiFillImg != null && _ultiBtnTMP != null && _ultiBtnObj != null)
        {
            if (pcm.isOverdriveActive)
            {
                _ultiFillImg.fillAmount = 0f;
                _ultiBtnTMP.text = $"[AKTİF]\n<size=65%>{pcm.overdriveRemaining:F1}s</size>";
                _ultiBtnTMP.color = new Color(1f, 0.9f, 0.3f);
                if (_ultiBtnOutline != null) _ultiBtnOutline.effectColor = new Color(1f, 0.5f, 0.1f);
                _ultiBtnObj.transform.localScale = _ultiBtnBaseScale;
            }
            else if (pcm.IsUltiReady)
            {
                _ultiFillImg.fillAmount = 0f;
                _ultiBtnTMP.text = "[ULTİ]\n<size=65%>HAZIR! [E]</size>";
                _ultiBtnTMP.color = Color.white;
                if (_ultiBtnOutline != null) _ultiBtnOutline.effectColor = Color.yellow;

                float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.08f;
                _ultiBtnObj.transform.localScale = _ultiBtnBaseScale * pulse;
            }
            else
            {
                float uncharged = 1f - (pcm.currentUlti / pcm.maxUlti);
                _ultiFillImg.fillAmount = uncharged;
                _ultiBtnTMP.text = $"ULTİ\n<size=65%>%{(int)pcm.currentUlti} [E]</size>";
                _ultiBtnTMP.color = new Color(0.75f, 0.75f, 0.75f);
                if (_ultiBtnOutline != null) _ultiBtnOutline.effectColor = new Color(0.5f, 0.45f, 0.2f, 0.7f);
                _ultiBtnObj.transform.localScale = _ultiBtnBaseScale;
            }
        }
    }

    public void ShowWaveAlert(string message, Color color, float duration = 2.0f)
    {
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        if (menu != null && menu.mainPanel != null && menu.mainPanel.activeInHierarchy)
        {
            if (_waveAlertObj != null) _waveAlertObj.SetActive(false);
            return;
        }

        if (_waveAlertObj == null || _waveAlertTMP == null)
            SetupWaveAlertObject();

        if (_alertCoroutine != null)
            StopCoroutine(_alertCoroutine);

        _alertCoroutine = StartCoroutine(WaveAlertRoutine(message, color, duration));
    }

    private IEnumerator WaveAlertRoutine(string message, Color color, float duration)
    {
        _waveAlertObj.SetActive(true);
        _waveAlertTMP.text = message;
        _waveAlertTMP.color = color;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            MainMenuManager menu = FindObjectOfType<MainMenuManager>();
            if (menu != null && menu.mainPanel != null && menu.mainPanel.activeInHierarchy)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _waveAlertObj.SetActive(false);
        _alertCoroutine = null;
    }

    /// <summary>
    /// Boss Rush modunda veya Final Boss yaklaşırken ekranı kırmızı alarm (Red Zone) ve sarsıntı ile kaplar.
    /// </summary>
    public void TriggerRedZoneWarning(float duration = 2.8f, System.Action onComplete = null)
    {
        if (_redZoneOverlayObj == null || _redZoneAlertTMP == null)
            SetupRedZoneOverlay();

        if (_redZoneCoroutine != null)
            StopCoroutine(_redZoneCoroutine);

        _redZoneCoroutine = StartCoroutine(RedZoneRoutine(duration, onComplete));
    }

    private IEnumerator RedZoneRoutine(float duration, System.Action onComplete)
    {
        if (_redZoneOverlayObj == null) yield break;

        _redZoneOverlayObj.SetActive(true);
        _redZoneOverlayObj.transform.SetAsLastSibling();

        if (_redZoneAlertTMP != null)
        {
            _redZoneAlertTMP.text = "[!] <color=#FFE600><b>[ RED ZONE: KRİTİK TEHLİKE ]</b></color> [!]\n<size=75%><color=#FF2222>FİNAL BOSS HAVA SAHASINA GİRİYOR!</color></size>\n<size=52%><color=#FFFFFF>NÜKLEER AS TESPİT EDİLDİ - DİKKAT!</color></size>";
        }

        AudioManager.Instance?.PlayWarningAlarm();
        // Alarm süresince ekrana sürekli tok sarsıntı ver
        CameraShake.Shake(0.30f, duration);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Nabız atan derin kırmızı ekran efekti
            float pulse = Mathf.PingPong(elapsed * 4.5f, 1f);
            float alpha = Mathf.Lerp(0.12f, 0.44f, pulse);
            if (_redZoneBgImage != null)
                _redZoneBgImage.color = new Color(0.95f, 0.05f, 0.05f, alpha);

            // Uyarı metni nabız parlaklığı
            if (_redZoneAlertTMP != null)
            {
                float textAlpha = Mathf.Lerp(0.65f, 1f, pulse);
                _redZoneAlertTMP.alpha = textAlpha;
            }

            yield return null;
        }

        // Yumuşak kayboluş (Fade out)
        float fadeOutTime = 0.40f;
        float fadeElapsed = 0f;
        Color initialColor = (_redZoneBgImage != null) ? _redZoneBgImage.color : Color.clear;
        while (fadeElapsed < fadeOutTime)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            float t = fadeElapsed / fadeOutTime;
            if (_redZoneBgImage != null)
                _redZoneBgImage.color = new Color(initialColor.r, initialColor.g, initialColor.b, Mathf.Lerp(initialColor.a, 0f, t));
            if (_redZoneAlertTMP != null)
                _redZoneAlertTMP.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        _redZoneOverlayObj.SetActive(false);
        _redZoneCoroutine = null;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Savaşa başlamadan önce seçilen oyun modunu ve hedeflerini tanıtan askeri brifing kartı gösterir.
    /// </summary>
    public void ShowModeIntroBanner(string title, string subtitle, string description, Color themeColor, float duration = 2.8f)
    {
        if (_modeIntroObj == null)
            SetupModeIntroBanner();

        if (_modeIntroCoroutine != null)
            StopCoroutine(_modeIntroCoroutine);

        _modeIntroCoroutine = StartCoroutine(ModeIntroBannerRoutine(title, subtitle, description, themeColor, duration));
    }

    private IEnumerator ModeIntroBannerRoutine(string title, string subtitle, string description, Color themeColor, float duration)
    {
        if (_modeIntroObj == null) yield break;

        _modeIntroObj.SetActive(true);
        _modeIntroObj.transform.SetAsLastSibling();

        if (_modeIntroTitleTMP != null)
        {
            _modeIntroTitleTMP.text = title;
            _modeIntroTitleTMP.color = themeColor;
        }

        if (_modeIntroSubtitleTMP != null)
        {
            _modeIntroSubtitleTMP.text = subtitle;
            _modeIntroSubtitleTMP.color = Color.white;
        }

        if (_modeIntroDescTMP != null)
        {
            _modeIntroDescTMP.text = $"{description}\n<size=80%><color=#2ECC71><b>» HAVA SAHASINA GİRİLDİ • MOTORLAR ÇALIŞTI «</b></color></size>";
        }

        if (_modeIntroOutline != null)
        {
            _modeIntroOutline.effectColor = themeColor;
        }

        // Telsiz / uyarı sinyali ile bildirim
        AudioManager.Instance?.PlayWarningAlarm();

        // Yumuşak Beliriş (Fade In)
        float fadeIn = 0.28f;
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_modeIntroCanvasGroup != null)
                _modeIntroCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeIn);
            yield return null;
        }
        if (_modeIntroCanvasGroup != null) _modeIntroCanvasGroup.alpha = 1f;

        // Ekranda Kalma Süresi
        float stayTime = duration - 0.6f;
        elapsed = 0f;
        while (elapsed < stayTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Yumuşak Kayboluş (Fade Out)
        float fadeOut = 0.32f;
        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_modeIntroCanvasGroup != null)
                _modeIntroCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeOut));
            yield return null;
        }

        _modeIntroObj.SetActive(false);
        _modeIntroCoroutine = null;
    }



    private void Update()
    {
        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy);
        bool shouldHideAll = isMenuOpen || _isHUDHiddenByCinematic;

        if (shouldHideAll)
        {
            if (plane1HealthBar != null && plane1HealthBar.gameObject.activeSelf) plane1HealthBar.gameObject.SetActive(false);
            if (plane2HealthBar != null && plane2HealthBar.gameObject.activeSelf) plane2HealthBar.gameObject.SetActive(false);
            if (_ultiBarObj != null && _ultiBarObj.activeSelf) _ultiBarObj.SetActive(false);
            if (_dodgeBtnObj != null && _dodgeBtnObj.activeSelf) _dodgeBtnObj.SetActive(false);
            if (_ultiBtnObj != null && _ultiBtnObj.activeSelf) _ultiBtnObj.SetActive(false);
            if (_comboHUDObj != null && _comboHUDObj.activeSelf) _comboHUDObj.SetActive(false);
            if (_goldHUDObj != null && _goldHUDObj.activeSelf) _goldHUDObj.SetActive(false);
            if (_waveBadgeObj != null && _waveBadgeObj.activeSelf) _waveBadgeObj.SetActive(false);
            if (_airspaceBannerObj != null && _airspaceBannerObj.activeSelf) _airspaceBannerObj.SetActive(false);
            if (_waveAlertObj != null && _waveAlertObj.activeSelf) _waveAlertObj.SetActive(false);
            return;
        }

        if (_waveBadgeObj != null)
        {
            bool isTwoPlayer = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);
            bool shouldShow = !isTwoPlayer;
            if (_waveBadgeObj.activeSelf != shouldShow)
            {
                _waveBadgeObj.SetActive(shouldShow);
            }
        }

        if (plane1HealthBar != null && !plane1HealthBar.gameObject.activeSelf)
        {
            plane1HealthBar.gameObject.SetActive(true);
        }

        if (plane2HealthBar != null && !plane2HealthBar.gameObject.activeSelf)
        {
            plane2HealthBar.gameObject.SetActive(true);
        }

        if (_ultiBarObj != null && !_ultiBarObj.activeSelf)
        {
            _ultiBarObj.SetActive(true);
        }

        if (_dodgeBtnObj != null && !_dodgeBtnObj.activeSelf)
        {
            _dodgeBtnObj.SetActive(true);
        }

        if (_ultiBtnObj != null && !_ultiBtnObj.activeSelf)
        {
            _ultiBtnObj.SetActive(true);
        }

        bool showComboHud = (GameManager.Instance == null || GameManager.Instance.CurrentMode != GameManager.GameMode.TwoPlayer);
        if (_comboHUDObj != null && _comboHUDObj.activeSelf != showComboHud)
        {
            _comboHUDObj.SetActive(showComboHud);
        }

        if (_goldHUDObj != null && !_goldHUDObj.activeSelf)
        {
            _goldHUDObj.SetActive(true);
        }

        UpdateUltiAndManeuverVisuals();
        UpdateComboHUD();
        UpdateAirspaceBanner();
        if (plane1HealthBar != null)
        {
            plane1HealthBar.value = Mathf.MoveTowards(plane1HealthBar.value, _targetP1Health, Time.deltaTime * 2.5f);

            if (_p1FillImage != null && _targetP1Health < 0.3f)
            {
                float pulse = Mathf.PingPong(Time.time * 4f, 1f);
                _p1FillImage.color = Color.Lerp(Color.red, Color.yellow, pulse);
            }
        }

        if (plane2HealthBar != null)
        {
            plane2HealthBar.value = Mathf.MoveTowards(plane2HealthBar.value, _targetP2Health, Time.deltaTime * 2.5f);
        }

        if (GameManager.Instance != null && _waveBadgeTMP != null)
        {
            if (GameManager.Instance.CurrentMode == GameManager.GameMode.BulletHell)
            {
                float t = GameManager.Instance.bulletHellTime;
                int m = (int)(t / 60f);
                int s = (int)(t % 60f);
                int ms = (int)((t * 10f) % 10f);
                _waveBadgeTMP.text = $"MERMİ CEHENNEMİ: <color=#FF4081><b>{m:00}:{s:00}.{ms}</b></color> (KADEME {GameManager.Instance.bulletHellTier})";
            }
            else if (GameManager.Instance.CurrentMode == GameManager.GameMode.TimeAttack)
            {
                float rem = GameManager.Instance.timeAttackRemaining;
                int kills = GameManager.Instance.timeAttackKills;
                string timerColor = (rem <= 10f) ? "#FF1744" : (rem <= 20f ? "#FFA000" : "#00E5FF");
                _waveBadgeTMP.text = $"ZAMAN TAARRUZU: <color={timerColor}><b>{rem:0.0}s</b></color> • İMHA: <b>{kills}</b>";
            }
        }
    }

    public void UpdateHealthBars()
    {
        FindPlanesIfNull();

        // 1. OYUNCU 1 CAN BARI
        if (plane1Health != null)
        {
            _targetP1Health = plane1Health.HealthPercent;
            int hpInt = Mathf.CeilToInt(plane1Health.HealthPercent * 100f);
            int rawHp = Mathf.CeilToInt(plane1Health.currentHealth);

            bool isTwoPlayer = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);

            if (_p1TmpLabel != null)
            {
                if (isTwoPlayer)
                {
                    _p1TmpLabel.text = $"OYUNCU 1: %{hpInt} ({rawHp} HP)";
                }
                else
                {
                    int lives = GameManager.Instance != null ? GameManager.Instance.player1Lives : 3;
                    string hearts = GetHeartsDisplay(lives);
                    string livesDisplay = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Hardcore)
                        ? "<color=#FF2222><b>[TEK CAN!]</b></color>"
                        : hearts;
                    string shieldTxt = (PlayerUpgradeManager.Instance != null && PlayerUpgradeManager.Instance.currentShield > 0f)
                        ? $" <color=#00E5FF>[KALKAN: {Mathf.CeilToInt(PlayerUpgradeManager.Instance.currentShield)}]</color>"
                        : "";
                    _p1TmpLabel.text = $"CAN: %{hpInt} ({rawHp} HP){shieldTxt} | {livesDisplay}";
                }
            }
        }

        // 2. DÜŞMAN / OYUNCU 2 CAN BARI & DALGA BİLGİSİ
        if (plane2Health != null)
        {
            _targetP2Health = plane2Health.HealthPercent;
            int hpInt = Mathf.CeilToInt(plane2Health.HealthPercent * 100f);
            int rawHp = Mathf.CeilToInt(plane2Health.currentHealth);

            bool isTwoPlayer = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);

            if (isTwoPlayer)
            {
                if (_p2TmpLabel != null)
                {
                    _p2TmpLabel.text = $"OYUNCU 2: %{hpInt} ({rawHp} HP)";
                    _p2TmpLabel.color = new Color(1f, 0.6f, 0.2f);
                }
                if (_waveBadgeTMP != null)
                {
                    _waveBadgeTMP.text = "2 OYUNCULU DUELLO";
                }
            }
            else
            {
                // Solo / Sonsuz / Boss Rush İlerleme Modu
                int wave = GameManager.Instance != null ? GameManager.Instance.currentPlaneIndex : 1;
                int totalPlanes = GameManager.Instance != null ? GameManager.Instance.GetTotalPlanesForMode() : 20;
                bool isBoss = GameManager.Instance != null && GameManager.Instance.IsBossPlane(wave);
                string title = GameManager.GetPlaneTitle(wave);

                string enemyShieldTxt = plane2Health.HasEnemyShield
                    ? $" <color=#00E5FF>[KALKAN: {Mathf.CeilToInt(plane2Health.currentEnemyShield)}]</color>"
                    : "";
                string archTag = isBoss
                    ? BossPatternController.GetArchetypeLabel(BossPatternController.GetBossArchetype(wave))
                    : AIController.GetArchetypeTag(AIController.GetStandardArchetype(wave));

                if (_p2TmpLabel != null)
                {
                    if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.BulletHell)
                    {
                        _p2TmpLabel.text = $"CEHENNEM KALESİ: %{hpInt} ({rawHp} HP){enemyShieldTxt}";
                        _p2TmpLabel.color = new Color(1f, 0.25f, 0.55f);
                    }
                    else if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Hardcore)
                    {
                        _p2TmpLabel.text = $"HARDCORE BOSS #{wave} [{archTag}]: %{hpInt} ({rawHp} HP){enemyShieldTxt}";
                        _p2TmpLabel.color = new Color(1f, 0.25f, 0.25f);
                    }
                    else if (isBoss)
                    {
                        _p2TmpLabel.text = $"{title} [{archTag}]: %{hpInt} ({rawHp} HP){enemyShieldTxt}";
                        _p2TmpLabel.color = new Color(1f, 0.85f, 0.2f); // Altın / Akkor Boss rengi
                    }
                    else
                    {
                        string waveLabel = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Endless)
                            ? $"DALGA {wave}"
                            : $"[{wave}/{totalPlanes}]";
                        _p2TmpLabel.text = $"{waveLabel} {title} [{archTag}]: %{hpInt}{enemyShieldTxt}";
                        _p2TmpLabel.color = new Color(1f, 0.45f, 0.45f);
                    }
                }

                if (_p2FillImage != null)
                {
                    _p2FillImage.color = isBoss
                        ? new Color(1f, 0.55f, 0.1f) // Boss için Lav Turuncusu/Altın
                        : new Color(1f, 0.2f, 0.25f);
                }

                string diffTag = "";
                switch (GameSettings.SelectedDifficulty)
                {
                    case GameSettings.Difficulty.Kolay: diffTag = "<size=80%><color=#2ECC71>[KOLAY]</color></size>"; break;
                    case GameSettings.Difficulty.Zor:   diffTag = "<size=80%><color=#E74C3C><b>[ZOR]</b></color></size>"; break;
                    default:                            diffTag = "<size=80%><color=#F39C12>[NORMAL]</color></size>"; break;
                }

                if (_waveBadgeTMP != null)
                {
                    if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Endless)
                    {
                        int best = PlayerPrefs.GetInt("AirBattle_EndlessHighScore", 1);
                        _waveBadgeTMP.text = isBoss
                            ? $"<color=#F1C40F>SONSUZ BOSS: DALGA {wave}</color> {diffTag}"
                            : $"SONSUZ İT DALAŞI: {wave} (REKOR: {best}) {diffTag}";
                    }
                    else if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.BossRush)
                    {
                        _waveBadgeTMP.text = $"<color=#E74C3C>BOSS RUSH: {wave} / 4</color> {diffTag}";
                    }
                    else if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.BulletHell)
                    {
                        _waveBadgeTMP.text = $"MERMİ CEHENNEMİ: 00:00.0 (KADEME 1) {diffTag}";
                    }
                    else if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TimeAttack)
                    {
                        _waveBadgeTMP.text = $"ZAMAN TAARRUZU: 60.0s • İMHA: 0 {diffTag}";
                    }
                    else if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Hardcore)
                    {
                        int best = PlayerPrefs.GetInt("AirBattle_HardcoreHighScore", 0);
                        _waveBadgeTMP.text = $"<color=#FF2222>[HC] HARDCORE BOSS #{wave}</color> (REKOR: {best}) {diffTag}";
                    }
                    else
                    {
                        if (isBoss)
                        {
                            _waveBadgeTMP.text = $"<color=#F1C40F>BOSS: {wave} / {totalPlanes}</color> {diffTag}";
                        }
                        else
                        {
                            _waveBadgeTMP.text = $"HAREKAT: {wave} / {totalPlanes} {diffTag}";
                        }
                    }
                }
            }
        }
    }

    private string GetHeartsDisplay(int lives)
    {
        if (lives <= 0) return "<color=#888888>[CAN YOK]</color>";
        if (lives == 1) return "<color=#E74C3C>[SON CAN]</color>";
        if (lives == 2) return "<color=#F39C12>[2 CAN]</color>";
        return $"<color=#FF3366>[{lives} CAN]</color>";
    }
}
