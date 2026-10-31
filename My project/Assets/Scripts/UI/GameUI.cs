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

        UpdateHealthBars();
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
                rt.sizeDelta = new Vector2(260f, 22f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(60f, -40f);
                rt.sizeDelta = new Vector2(280f, 22f);
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
        rt.sizeDelta = new Vector2(0f, 20f);

        TextMeshProUGUI tmp = labelObj.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = labelObj.AddComponent<TextMeshProUGUI>();

        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;

        if (isPlayer)
        {
            tmp.color = new Color(0.4f, 0.9f, 1f);
            tmp.text = "🛡️ OYUNCU 1: %100";
            _p1TmpLabel = tmp;
        }
        else
        {
            tmp.color = new Color(1f, 0.4f, 0.4f);
            tmp.text = "⚠️ DÜŞMAN: %100";
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

        RectTransform rt = badgeObj.GetComponent<RectTransform>();
        if (rt == null) rt = badgeObj.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 1f); // Üst Orta
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -25f);
        rt.sizeDelta = new Vector2(360f, 38f);

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

        _waveBadgeTMP.fontSize = 17f;
        _waveBadgeTMP.fontStyle = FontStyles.Bold;
        _waveBadgeTMP.alignment = TextAlignmentOptions.Center;
        _waveBadgeTMP.color = new Color(0.95f, 0.95f, 1f);
        _waveBadgeTMP.text = "🎯 HAREKAT İLERLEMESİ: 1 / 20";
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
        rt.sizeDelta = new Vector2(750f, 100f);

        _waveAlertTMP = _waveAlertObj.GetComponent<TextMeshProUGUI>();
        if (_waveAlertTMP == null) _waveAlertTMP = _waveAlertObj.AddComponent<TextMeshProUGUI>();

        _waveAlertTMP.fontSize = 28f;
        _waveAlertTMP.fontStyle = FontStyles.Bold;
        _waveAlertTMP.alignment = TextAlignmentOptions.Center;
        _waveAlertTMP.raycastTarget = false;

        _waveAlertObj.SetActive(false);
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

    private void Update()
    {
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
                    _p1TmpLabel.text = $"🛡️ OYUNCU 1: %{hpInt} ({rawHp} HP)";
                }
                else
                {
                    int lives = GameManager.Instance != null ? GameManager.Instance.player1Lives : 3;
                    string hearts = GetHeartsDisplay(lives);
                    _p1TmpLabel.text = $"🛡️ CAN: %{hpInt} | {hearts}";
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
                    _p2TmpLabel.text = $"👤 OYUNCU 2: %{hpInt} ({rawHp} HP)";
                    _p2TmpLabel.color = new Color(1f, 0.6f, 0.2f);
                }
                if (_waveBadgeTMP != null)
                {
                    _waveBadgeTMP.text = "⚔️ 2 OYUNCULU DÜELLO ⚔️";
                }
            }
            else
            {
                // Solo Mod: 20 Uçaklık İlerleme
                int wave = GameManager.Instance != null ? GameManager.Instance.currentPlaneIndex : 1;
                bool isBoss = GameManager.IsBossPlane(wave);
                string title = GameManager.GetPlaneTitle(wave);

                if (_p2TmpLabel != null)
                {
                    if (isBoss)
                    {
                        _p2TmpLabel.text = $"{title}: %{hpInt} ({rawHp} HP)";
                        _p2TmpLabel.color = new Color(1f, 0.85f, 0.2f); // Altın / Akkor Boss rengi
                    }
                    else
                    {
                        _p2TmpLabel.text = $"⚠️ [{wave}/{GameManager.MaxPlanes}] {title}: %{hpInt}";
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
                    case GameSettings.Difficulty.Zor:   diffTag = "<size=80%><color=#E74C3C>[ZOR]</color></size>"; break;
                    default:                            diffTag = "<size=80%><color=#F39C12>[NORMAL]</color></size>"; break;
                }

                if (_waveBadgeTMP != null)
                {
                    if (isBoss)
                    {
                        _waveBadgeTMP.text = $"👑 <color=#F1C40F>BOSS: {wave} / {GameManager.MaxPlanes}</color> {diffTag}";
                    }
                    else
                    {
                        _waveBadgeTMP.text = $"🎯 HAREKAT: {wave} / {GameManager.MaxPlanes} {diffTag}";
                    }
                }
            }
        }
    }

    private string GetHeartsDisplay(int lives)
    {
        switch (lives)
        {
            case 3: return "<color=#FF3366>❤️ ❤️ ❤️</color>";
            case 2: return "<color=#FF3366>❤️ ❤️</color> <color=#444444>🖤</color>";
            case 1: return "<color=#FF3366>❤️</color> <color=#444444>🖤 🖤</color>";
            default: return "<color=#444444>🖤 🖤 🖤</color>";
        }
    }
}
