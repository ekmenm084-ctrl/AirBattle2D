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

    private void Start()
    {
        FindPlanesIfNull();

        SetupHealthBar(plane1HealthBar, isPlayer: true);
        SetupHealthBar(plane2HealthBar, isPlayer: false);

        SetupWaveBadge();
        SetupWaveAlertObject();
        SetupRedZoneOverlay();
        SetupModeIntroBanner();
        SetupMissileButton();
        SetupGoldHUD();

        UpdateHealthBars();

        MainMenuManager menu = FindObjectOfType<MainMenuManager>();
        bool isMenuOpen = (menu != null && menu.gameObject.activeInHierarchy);
        if (isMenuOpen)
        {
            SetHUDVisibility(false);
        }
    }

    public void SetHUDVisibility(bool visible)
    {
        if (_waveBadgeObj != null) _waveBadgeObj.SetActive(visible);
        if (plane1HealthBar != null) plane1HealthBar.gameObject.SetActive(visible);
        if (plane2HealthBar != null) plane2HealthBar.gameObject.SetActive(visible);
        if (_missileBtnObj != null) _missileBtnObj.SetActive(visible);
        if (_goldHUDObj != null) _goldHUDObj.SetActive(visible);
        if (!visible && _redZoneOverlayObj != null) _redZoneOverlayObj.SetActive(false);
        if (!visible && _modeIntroObj != null) _modeIntroObj.SetActive(false);
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
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(60f, 310f);
                rt.sizeDelta = new Vector2(300f, 26f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(60f, -40f);
                rt.sizeDelta = new Vector2(320f, 26f);
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
        Transform existing = slider.transform.Find("HUD_Label");
        GameObject labelObj = existing != null ? existing.gameObject : new GameObject("HUD_Label");
        labelObj.transform.SetParent(slider.transform, false);

        RectTransform rt = labelObj.GetComponent<RectTransform>();
        if (rt == null) rt = labelObj.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 4f);
        rt.sizeDelta = new Vector2(0f, 26f);

        TextMeshProUGUI tmp = labelObj.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = labelObj.AddComponent<TextMeshProUGUI>();

        tmp.fontSize = 18f;
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

        Transform existing = canvas.transform.Find("HUD_WaveBadge");
        GameObject badgeObj = existing != null ? existing.gameObject : new GameObject("HUD_WaveBadge");
        badgeObj.transform.SetParent(canvas.transform, false);
        _waveBadgeObj = badgeObj;

        RectTransform rt = badgeObj.GetComponent<RectTransform>();
        if (rt == null) rt = badgeObj.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 1f); // Üst Orta
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -25f);
        rt.sizeDelta = new Vector2(440f, 48f);

        Image bg = badgeObj.GetComponent<Image>();
        if (bg == null) bg = badgeObj.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.09f, 0.14f, 0.85f);

        Outline outline = badgeObj.GetComponent<Outline>();
        if (outline == null) outline = badgeObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        Transform textChild = badgeObj.transform.Find("Text");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("Text");
        textObj.transform.SetParent(badgeObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        if (textRt == null) textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        _waveBadgeTMP = textObj.GetComponent<TextMeshProUGUI>();
        if (_waveBadgeTMP == null) _waveBadgeTMP = textObj.AddComponent<TextMeshProUGUI>();

        _waveBadgeTMP.fontSize = 22f;
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

        Transform existing = canvas.transform.Find("HUD_WaveAlert");
        _waveAlertObj = existing != null ? existing.gameObject : new GameObject("HUD_WaveAlert");
        _waveAlertObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = _waveAlertObj.GetComponent<RectTransform>();
        if (rt == null) rt = _waveAlertObj.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(850f, 120f);

        _waveAlertTMP = _waveAlertObj.GetComponent<TextMeshProUGUI>();
        if (_waveAlertTMP == null) _waveAlertTMP = _waveAlertObj.AddComponent<TextMeshProUGUI>();

        _waveAlertTMP.fontSize = 36f;
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

        Transform existing = canvas.transform.Find("HUD_RedZoneOverlay");
        _redZoneOverlayObj = existing != null ? existing.gameObject : new GameObject("HUD_RedZoneOverlay");
        _redZoneOverlayObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = _redZoneOverlayObj.GetComponent<RectTransform>();
        if (rt == null) rt = _redZoneOverlayObj.AddComponent<RectTransform>();

        // Tam ekran kaplama
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _redZoneBgImage = _redZoneOverlayObj.GetComponent<Image>();
        if (_redZoneBgImage == null) _redZoneBgImage = _redZoneOverlayObj.AddComponent<Image>();
        _redZoneBgImage.color = new Color(0.9f, 0.05f, 0.05f, 0f);
        _redZoneBgImage.raycastTarget = false;

        // Uyarı Başlık Metni
        Transform bannerTr = _redZoneOverlayObj.transform.Find("BannerText");
        GameObject bannerObj = (bannerTr != null) ? bannerTr.gameObject : new GameObject("BannerText");
        bannerObj.transform.SetParent(_redZoneOverlayObj.transform, false);

        RectTransform bannerRt = bannerObj.GetComponent<RectTransform>();
        if (bannerRt == null) bannerRt = bannerObj.AddComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0f, 0.5f);
        bannerRt.anchorMax = new Vector2(1f, 0.5f);
        bannerRt.pivot = new Vector2(0.5f, 0.5f);
        bannerRt.anchoredPosition = new Vector2(0f, 85f);
        bannerRt.sizeDelta = new Vector2(0f, 180f);

        _redZoneAlertTMP = bannerObj.GetComponent<TextMeshProUGUI>();
        if (_redZoneAlertTMP == null) _redZoneAlertTMP = bannerObj.AddComponent<TextMeshProUGUI>();
        _redZoneAlertTMP.fontSize = 38f;
        _redZoneAlertTMP.fontStyle = FontStyles.Bold;
        _redZoneAlertTMP.alignment = TextAlignmentOptions.Center;
        _redZoneAlertTMP.raycastTarget = false;

        var outline = bannerObj.GetComponent<Outline>();
        if (outline == null) outline = bannerObj.AddComponent<Outline>();
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

    private void SetupModeIntroBanner()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("HUD_ModeIntroBanner");
        _modeIntroObj = existing != null ? existing.gameObject : new GameObject("HUD_ModeIntroBanner");
        _modeIntroObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = _modeIntroObj.GetComponent<RectTransform>() ?? _modeIntroObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 75f);
        rt.sizeDelta = new Vector2(760f, 180f);

        Image bg = _modeIntroObj.GetComponent<Image>() ?? _modeIntroObj.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.07f, 0.13f, 0.94f);
        bg.raycastTarget = false;

        _modeIntroOutline = _modeIntroObj.GetComponent<Outline>() ?? _modeIntroObj.AddComponent<Outline>();
        _modeIntroOutline.effectColor = new Color(0f, 0.85f, 1f, 0.9f);
        _modeIntroOutline.effectDistance = new Vector2(2.5f, -2.5f);

        _modeIntroCanvasGroup = _modeIntroObj.GetComponent<CanvasGroup>() ?? _modeIntroObj.AddComponent<CanvasGroup>();

        // 1. Başlık
        Transform titleTr = _modeIntroObj.transform.Find("TitleText");
        GameObject titleObj = (titleTr != null) ? titleTr.gameObject : new GameObject("TitleText");
        titleObj.transform.SetParent(_modeIntroObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>() ?? titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.60f);
        titleRt.anchorMax = new Vector2(1f, 0.98f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        _modeIntroTitleTMP = titleObj.GetComponent<TextMeshProUGUI>() ?? titleObj.AddComponent<TextMeshProUGUI>();
        _modeIntroTitleTMP.fontSize = 28f;
        _modeIntroTitleTMP.fontStyle = FontStyles.Bold;
        _modeIntroTitleTMP.alignment = TextAlignmentOptions.Center;
        _modeIntroTitleTMP.raycastTarget = false;

        // 2. Alt Başlık
        Transform subTr = _modeIntroObj.transform.Find("SubtitleText");
        GameObject subObj = (subTr != null) ? subTr.gameObject : new GameObject("SubtitleText");
        subObj.transform.SetParent(_modeIntroObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>() ?? subObj.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 0.30f);
        subRt.anchorMax = new Vector2(1f, 0.62f);
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;
        _modeIntroSubtitleTMP = subObj.GetComponent<TextMeshProUGUI>() ?? subObj.AddComponent<TextMeshProUGUI>();
        _modeIntroSubtitleTMP.fontSize = 18f;
        _modeIntroSubtitleTMP.fontStyle = FontStyles.Bold;
        _modeIntroSubtitleTMP.alignment = TextAlignmentOptions.Center;
        _modeIntroSubtitleTMP.raycastTarget = false;

        // 3. Açıklama & Hazır Mesajı
        Transform descTr = _modeIntroObj.transform.Find("DescText");
        GameObject descObj = (descTr != null) ? descTr.gameObject : new GameObject("DescText");
        descObj.transform.SetParent(_modeIntroObj.transform, false);
        RectTransform descRt = descObj.GetComponent<RectTransform>() ?? descObj.AddComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 0.04f);
        descRt.anchorMax = new Vector2(1f, 0.32f);
        descRt.offsetMin = Vector2.zero;
        descRt.offsetMax = Vector2.zero;
        _modeIntroDescTMP = descObj.GetComponent<TextMeshProUGUI>() ?? descObj.AddComponent<TextMeshProUGUI>();
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

        Transform existing = canvas.transform.Find("HUD_GoldBadge");
        _goldHUDObj = existing != null ? existing.gameObject : new GameObject("HUD_GoldBadge");
        _goldHUDObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = _goldHUDObj.GetComponent<RectTransform>();
        if (rt == null) rt = _goldHUDObj.AddComponent<RectTransform>();

        // Sağ üst köşe, Pause butonunun hemen solu
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-96f, -28f);
        rt.sizeDelta = new Vector2(210f, 44f);

        Image bg = _goldHUDObj.GetComponent<Image>();
        if (bg == null) bg = _goldHUDObj.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.09f, 0.15f, 0.90f);

        _goldHUDOutline = _goldHUDObj.GetComponent<Outline>();
        if (_goldHUDOutline == null) _goldHUDOutline = _goldHUDObj.AddComponent<Outline>();
        _goldHUDOutline.effectColor = new Color(1f, 0.85f, 0.2f, 0.85f);
        _goldHUDOutline.effectDistance = new Vector2(2f, -2f);

        Transform textChild = _goldHUDObj.transform.Find("Text");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("Text");
        textObj.transform.SetParent(_goldHUDObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        if (textRt == null) textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        _goldHUDTMP = textObj.GetComponent<TextMeshProUGUI>();
        if (_goldHUDTMP == null) _goldHUDTMP = textObj.AddComponent<TextMeshProUGUI>();

        _goldHUDTMP.fontSize = 20f;
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
        if (_goldHUDTMP == null || _goldHUDObj == null) yield break;

        _goldHUDTMP.color = Color.white;
        _goldHUDObj.transform.localScale = Vector3.one * 1.15f;

        yield return new WaitForSecondsRealtime(0.18f);

        _goldHUDTMP.color = new Color(1f, 0.88f, 0.25f);
        _goldHUDObj.transform.localScale = Vector3.one;
        _goldPulseCoroutine = null;
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
            _missileBtnObj = new GameObject("MissileLauncherButton");
            _missileBtnObj.transform.SetParent(canvas.transform, false);
            _missileBtnObj.AddComponent<MissileLauncherButton>();
        }
    }

    public void ShowWaveAlert(string message, Color color, float duration = 2.0f)
    {
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
            elapsed += Time.deltaTime;
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
            _redZoneAlertTMP.text = "🚨 <color=#FFE600><b>[ RED ZONE: KRİTİK TEHLİKE ]</b></color> 🚨\n<size=75%><color=#FF2222>FİNAL BOSS HAVA SAHASINA GİRİYOR!</color></size>\n<size=52%><color=#FFFFFF>NÜKLEER AS TESPİT EDİLDİ - DİKKAT!</color></size>";
        }

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
            _modeIntroDescTMP.text = $"{description}\n<size=80%><color=#2ECC71><b>► HAVA SAHASINA GİRİLDİ • MOTORLAR ÇALIŞTI ◄</b></color></size>";
        }

        if (_modeIntroOutline != null)
        {
            _modeIntroOutline.effectColor = themeColor;
        }

        // Telsiz atış sesi ile bildirim
        AudioManager.Instance?.PlayGunfire(false);

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

        if (_waveBadgeObj != null && _waveBadgeObj.activeSelf == isMenuOpen)
        {
            _waveBadgeObj.SetActive(!isMenuOpen);
        }

        if (plane1HealthBar != null && plane1HealthBar.gameObject.activeSelf == isMenuOpen)
        {
            plane1HealthBar.gameObject.SetActive(!isMenuOpen);
        }

        if (plane2HealthBar != null && plane2HealthBar.gameObject.activeSelf == isMenuOpen)
        {
            plane2HealthBar.gameObject.SetActive(!isMenuOpen);
        }

        if (isMenuOpen) return;
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
                    string shieldTxt = (PlayerUpgradeManager.Instance != null && PlayerUpgradeManager.Instance.currentShield > 0f)
                        ? $" <color=#00E5FF>[KALKAN: {Mathf.CeilToInt(PlayerUpgradeManager.Instance.currentShield)}]</color>"
                        : "";
                    _p1TmpLabel.text = $"CAN: %{hpInt} ({rawHp} HP){shieldTxt} | {hearts}";
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

                if (_p2TmpLabel != null)
                {
                    if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.BulletHell)
                    {
                        _p2TmpLabel.text = $"CEHENNEM KALESİ: %{hpInt} ({rawHp} HP)";
                        _p2TmpLabel.color = new Color(1f, 0.25f, 0.55f);
                    }
                    else if (isBoss)
                    {
                        _p2TmpLabel.text = $"{title}: %{hpInt} ({rawHp} HP)";
                        _p2TmpLabel.color = new Color(1f, 0.85f, 0.2f); // Altın / Akkor Boss rengi
                    }
                    else
                    {
                        string waveLabel = (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Endless)
                            ? $"DALGA {wave}"
                            : $"[{wave}/{totalPlanes}]";
                        _p2TmpLabel.text = $"{waveLabel} {title}: %{hpInt}";
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
