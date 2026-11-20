using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Askeri Havacılık Telsiz & AWACS Seslendirme Yöneticisi (Military Radio Chatter & Tactical Voiceover).
/// - AWACS Kule (Hava Erken İhbar Merkezi) ve Kartal-1 (Avcı Pilotu) telsiz anonsları.
/// - Gerçekçi VHF/UHF telsiz filtreleri, PTT mandal klikleri, roger bipleri ve arka plan paraziti.
/// - Telsiz devreye girdiğinde savaş müziğini otomatik kısan Audio Ducking sistemi.
/// - Ekranda şık taktiksel askeri telsiz kutusu, yanıp sönen [● TX] sinyali ve dalga formu animasyonu.
/// </summary>
public class RadioChatterManager : MonoBehaviour
{
    public static RadioChatterManager Instance { get; private set; }

    public enum Priority
    {
        Normal = 0,
        High = 1,
        Critical = 2
    }

    [System.Serializable]
    public class RadioTransmission
    {
        public string id;
        public string speaker;
        public string subtitle;
        public Color speakerColor;
        public Priority priority;
        public AudioClip clip;
    }

    [Header("Ses Ayarları")]
    public float radioVolume = 0.95f;
    public bool isEnabled = true;

    private const string PREF_RADIO_VOL = "AirBattle_RadioVol";
    private const string PREF_RADIO_ENABLED = "AirBattle_RadioEnabled";

    private AudioSource _audioSource;
    private readonly Dictionary<string, RadioTransmission> _transmissionMap = new Dictionary<string, RadioTransmission>();

    // Cooldown sayaçları (Spam engelleme)
    private float _lastKillVoiceTime = -999f;
    private float _lastHullVoiceTime = -999f;
    private float _lastStreakVoiceTime = -999f;
    private float _lastBossVoiceTime = -999f;
    private int _lastKillIndex = -1;

    // HUD UI Elemanları
    private GameObject _radioPanelObj;
    private CanvasGroup _radioCanvasGroup;
    private TextMeshProUGUI _headerTMP;
    private TextMeshProUGUI _waveformTMP;
    private TextMeshProUGUI _subtitleTMP;
    private Image _borderImage;
    private Image _bgImage;
    private Coroutine _activeRadioRoutine;

    private static readonly string[] WaveformFrames = new string[]
    {
        " ▃▅", "▂▅▇", "▃▇▅", "▅▃▂", "▇▅▃", "▅▇▅", "▃▅▇"
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        InitializeAudioSource();
        RegisterRadioTransmissions();
    }

    public static RadioChatterManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("RadioChatterManager");
        if (go == null)
        {
            go = new GameObject("RadioChatterManager");
        }

        Instance = go.GetComponent<RadioChatterManager>();
        if (Instance == null)
        {
            Instance = go.AddComponent<RadioChatterManager>();
        }
        return Instance;
    }

    private void LoadSettings()
    {
        radioVolume = PlayerPrefs.GetFloat(PREF_RADIO_VOL, 0.95f);
        isEnabled = PlayerPrefs.GetInt(PREF_RADIO_ENABLED, 1) == 1;
    }

    private void InitializeAudioSource()
    {
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D Ses
            _audioSource.volume = radioVolume;
        }
    }

    private void RegisterRadioTransmissions()
    {
        // 1. AWACS & KULE KONTROL (Tok & Otoriter Askeri Komutan - VHF 121.5 MHz)
        AddLine("radio_takeoff", "KULE KONTROL", "Kartal-1, kalkış serbest. Hava sahasına intikal edin, av serbest!",
            new Color(0f, 0.92f, 1f), Priority.High);

        AddLine("radio_boss_warning", "AWACS KONTROL", "DİKKAT! Radarda devasa düşman imzası! Ağır bombardıman yaklaşıyor!",
            new Color(1f, 0.22f, 0.22f), Priority.Critical);

        AddLine("radio_missile_warning", "AWACS KONTROL", "FÜZE KİLİDİ! Üzerine füze geliyor, kaçış manevrası yap!",
            new Color(1f, 0.28f, 0.28f), Priority.Critical);

        AddLine("radio_squadron_clear", "AWACS KONTROL", "Düşman filosu tamamen imha edildi! Hava sahası temizlendi!",
            new Color(0.2f, 1f, 0.55f), Priority.High);

        AddLine("radio_hull_warning", "AWACS KONTROL", "DİKKAT! Kritik gövde hasarı! Uçak parçalanıyor, irtifayı koru!",
            new Color(1f, 0.32f, 0.32f), Priority.Critical);

        AddLine("radio_hazard_storm", "AWACS KONTROL", "DİKKAT! Ağır manyetik fırtına! Radar kilitlendi, gözle temas kur!",
            new Color(1f, 0.72f, 0.15f), Priority.High);

        // 2. KARTAL-1 AVCI PİLOTU (Sert & Agresif Muharebe Pilotu - UHF 243.0 MHz)
        AddLine("radio_kill_1", "KARTAL-1", "Hedef vuruldu! Düşman jeti imha edildi!",
            new Color(1f, 0.82f, 0.2f), Priority.Normal);

        AddLine("radio_kill_2", "KARTAL-1", "Fox Two! Düşman havada parçalandı!",
            new Color(1f, 0.82f, 0.2f), Priority.Normal);

        AddLine("radio_kill_3", "KARTAL-1", "Splash One! Düşman düştü, sonraki hedefe geçiyorum!",
            new Color(1f, 0.82f, 0.2f), Priority.Normal);

        AddLine("radio_streak_triple", "KARTAL-1", "Üçte üç! Hepsini tek tek indireceğim!",
            new Color(1f, 0.65f, 0.15f), Priority.Normal);

        AddLine("radio_streak_ace", "KARTAL-1", "Gökyüzünün ası! Kimse karşımda duramaz!",
            new Color(1f, 0.85f, 0.05f), Priority.High);

        AddLine("radio_overdrive", "KARTAL-1", "Tam itki devrede! Son sürat hücum!",
            new Color(0.2f, 0.85f, 1f), Priority.High);

        AddLine("radio_shield_down", "KARTAL-1", "Kalkan parçalandı! İt dalaşına giriyorum!",
            new Color(1f, 0.45f, 0.45f), Priority.High);

        AddLine("radio_crate_loot", "KARTAL-1", "Mühimmat sandığı alındı, silahlar dolu!",
            new Color(0.35f, 0.95f, 0.45f), Priority.Normal);

        AddLine("radio_boss_down", "KARTAL-1", "Amiral gemisi alevler içinde düşüyor! Zafer bizim!",
            new Color(1f, 0.90f, 0.15f), Priority.High);
    }

    private void AddLine(string id, string speaker, string subtitle, Color color, Priority prio)
    {
        AudioClip clip = Resources.Load<AudioClip>($"Radio/{id}");
        _transmissionMap[id] = new RadioTransmission
        {
            id = id,
            speaker = speaker,
            subtitle = subtitle,
            speakerColor = color,
            priority = prio,
            clip = clip
        };
    }

    /// <summary>
    /// ID ile belirtilen telsiz konuşmasını tetikler.
    /// </summary>
    public bool PlayTransmission(string id, bool force = false)
    {
        if (!isEnabled || (AudioManager.Instance != null && AudioManager.Instance.isMuted)) return false;
        if (!_transmissionMap.TryGetValue(id, out RadioTransmission tx)) return false;

        // Halihazırda oynayan telsiz varsa öncelik kontrolü yap
        if (_activeRadioRoutine != null && !force)
        {
            if (tx.priority < Priority.Critical)
            {
                // Kritik olmayan mesajlar çalmakta olan bir yayını kesmez
                return false;
            }
        }

        if (_activeRadioRoutine != null)
        {
            StopCoroutine(_activeRadioRoutine);
            _activeRadioRoutine = null;
        }

        _activeRadioRoutine = StartCoroutine(RadioTransmissionRoutine(tx));
        return true;
    }

    // ==========================================
    // ÖZEL TETİKLEYİCİ METOTLAR (Oynanış Kancaları)
    // ==========================================

    public void PlayTakeoff()
    {
        PlayTransmission("radio_takeoff", force: true);
    }

    public void PlayBossWarning()
    {
        if (Time.time - _lastBossVoiceTime < 20.0f) return;
        _lastBossVoiceTime = Time.time;
        PlayTransmission("radio_boss_warning", force: true);
    }

    public void PlayBossDown()
    {
        PlayTransmission("radio_boss_down", force: true);
    }

    public void PlayMissileWarning()
    {
        PlayTransmission("radio_missile_warning", force: true);
    }

    public void PlaySquadronClear()
    {
        PlayTransmission("radio_squadron_clear", force: true);
    }

    public void PlayHullWarning()
    {
        if (Time.time - _lastHullVoiceTime < 16.0f) return;
        _lastHullVoiceTime = Time.time;
        PlayTransmission("radio_hull_warning", force: true);
    }

    public void PlayAirKill()
    {
        if (Time.time - _lastKillVoiceTime < 4.2f) return;
        _lastKillVoiceTime = Time.time;

        // 3 farklı vuruş cümlesinden birini sırayla seç
        int nextIdx = Random.Range(1, 4);
        if (nextIdx == _lastKillIndex) nextIdx = (nextIdx % 3) + 1;
        _lastKillIndex = nextIdx;

        PlayTransmission($"radio_kill_{nextIdx}");
    }

    public void PlayStreak(int streakCount)
    {
        if (Time.time - _lastStreakVoiceTime < 5.0f) return;
        _lastStreakVoiceTime = Time.time;

        if (streakCount >= 5)
        {
            PlayTransmission("radio_streak_ace", force: true);
        }
        else if (streakCount >= 3)
        {
            PlayTransmission("radio_streak_triple");
        }
    }

    public void PlayOverdrive()
    {
        PlayTransmission("radio_overdrive", force: true);
    }

    public void PlayShieldDown()
    {
        PlayTransmission("radio_shield_down", force: true);
    }

    public void PlayCrateLoot()
    {
        PlayTransmission("radio_crate_loot");
    }

    public void PlayHazardStorm()
    {
        PlayTransmission("radio_hazard_storm", force: true);
    }

    // ==========================================
    // TELSİZ SEKANS KORUTİNİ & HUD GÖRSELİ
    // ==========================================

    private IEnumerator RadioTransmissionRoutine(RadioTransmission tx)
    {
        EnsureHUDPanel();

        // 1. Audio Ducking (Arka plan savaş müziğini %35 seviyesine kıs)
        AudioManager.Instance?.DuckMusic(0.35f, 0.15f);

        // 2. Ses Dosyasını Başlat
        if (tx.clip != null && _audioSource != null)
        {
            float targetVol = radioVolume * (AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0.85f);
            _audioSource.volume = targetVol;
            _audioSource.clip = tx.clip;
            _audioSource.Play();
        }

        // 3. HUD Kutusunu Doldur
        string hex = ColorUtility.ToHtmlStringRGB(tx.speakerColor);
        string channel = tx.speaker.Contains("AWACS") ? "VHF 121.5 MHz" : "UHF 243.0 MHz";
        
        if (_headerTMP != null)
        {
            _headerTMP.text = $"<color=#{hex}><b>[● TX] {tx.speaker}</b></color> <size=75%><color=#78909C>• {channel}</color></size>";
        }

        if (_subtitleTMP != null)
        {
            _subtitleTMP.text = $"\"{tx.subtitle}\"";
        }

        if (_borderImage != null)
        {
            _borderImage.color = new Color(tx.speakerColor.r, tx.speakerColor.g, tx.speakerColor.b, 0.85f);
        }

        // 4. Panel Açılış Animasyonu (Yumuşak Fade-In ve Hafif Pop Efekti)
        if (_radioPanelObj != null) _radioPanelObj.SetActive(true);
        float animIn = 0.15f;
        float elapsed = 0f;
        RectTransform rt = _radioPanelObj != null ? _radioPanelObj.GetComponent<RectTransform>() : null;
        Vector2 basePos = new Vector2(0f, -145f);

        while (elapsed < animIn)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animIn);
            if (_radioCanvasGroup != null) _radioCanvasGroup.alpha = t;
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.Lerp(basePos + new Vector2(0f, 12f), basePos, t);
                rt.localScale = Vector3.Lerp(new Vector3(0.92f, 0.92f, 1f), Vector3.one, t);
            }
            yield return null;
        }

        if (_radioCanvasGroup != null) _radioCanvasGroup.alpha = 1f;
        if (rt != null)
        {
            rt.anchoredPosition = basePos;
            rt.localScale = Vector3.one;
        }

        // 5. Ses Çalarken Dalga Formu ve TX Yanıp Sönme Animasyonu
        float clipDuration = (tx.clip != null) ? tx.clip.length : 3.5f;
        float playElapsed = 0f;
        int frameIdx = 0;
        float waveTimer = 0f;

        while (playElapsed < clipDuration)
        {
            playElapsed += Time.unscaledDeltaTime;
            waveTimer += Time.unscaledDeltaTime;

            if (waveTimer >= 0.08f)
            {
                waveTimer = 0f;
                frameIdx = (frameIdx + 1) % WaveformFrames.Length;
                if (_waveformTMP != null)
                {
                    string bars = WaveformFrames[frameIdx] + WaveformFrames[(frameIdx + 2) % WaveformFrames.Length];
                    _waveformTMP.text = $"<color=#{hex}>{bars}</color>";
                }
            }

            yield return null;
        }

        // 6. Cümle bittikten sonra oyuncunun okuması için kısa bekleme (0.45s)
        if (_waveformTMP != null) _waveformTMP.text = "<color=#546E7A>---</color>";
        yield return new WaitForSecondsRealtime(0.45f);

        // 7. Panel Kapanış Animasyonu (Fade-Out)
        float animOut = 0.22f;
        elapsed = 0f;
        while (elapsed < animOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / animOut);
            if (_radioCanvasGroup != null) _radioCanvasGroup.alpha = t;
            yield return null;
        }

        if (_radioCanvasGroup != null) _radioCanvasGroup.alpha = 0f;
        if (_radioPanelObj != null) _radioPanelObj.SetActive(false);

        // 8. Savaş Müziğini Eski Seviyesine Döndür
        AudioManager.Instance?.RestoreMusic(0.35f);

        _activeRadioRoutine = null;
    }

    private void EnsureHUDPanel()
    {
        if (_radioPanelObj != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _radioPanelObj = new GameObject("RadioChatterHUD");
        _radioPanelObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = _radioPanelObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -145f);
        rt.sizeDelta = new Vector2(480f, 66f);

        _radioCanvasGroup = _radioPanelObj.AddComponent<CanvasGroup>();
        _radioCanvasGroup.alpha = 0f;
        _radioCanvasGroup.blocksRaycasts = false;
        _radioCanvasGroup.interactable = false;

        // Dış Işıltılı Çerçeve (Border)
        _borderImage = _radioPanelObj.AddComponent<Image>();
        _borderImage.color = new Color(0f, 0.85f, 1f, 0.85f);

        // İç Koyu Slate Panel (Background)
        GameObject innerObj = new GameObject("InnerBg");
        innerObj.transform.SetParent(_radioPanelObj.transform, false);
        RectTransform innerRt = innerObj.AddComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(2f, 2f);
        innerRt.offsetMax = new Vector2(-2f, -2f);

        _bgImage = innerObj.AddComponent<Image>();
        _bgImage.color = new Color(0.04f, 0.08f, 0.12f, 0.94f);

        // Üst Başlık (Header: Speaker & Channel)
        GameObject headerObj = new GameObject("HeaderTMP");
        headerObj.transform.SetParent(innerObj.transform, false);
        RectTransform headerRt = headerObj.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0f, 1f);
        headerRt.anchoredPosition = new Vector2(12f, -4f);
        headerRt.sizeDelta = new Vector2(-70f, 20f);

        _headerTMP = headerObj.AddComponent<TextMeshProUGUI>();
        _headerTMP.fontSize = 13.5f;
        _headerTMP.alignment = TextAlignmentOptions.Left;
        _headerTMP.raycastTarget = false;

        // Sağ Üst Dalga Formu / Equalizer (Waveform)
        GameObject waveObj = new GameObject("WaveTMP");
        waveObj.transform.SetParent(innerObj.transform, false);
        RectTransform waveRt = waveObj.AddComponent<RectTransform>();
        waveRt.anchorMin = new Vector2(1f, 1f);
        waveRt.anchorMax = new Vector2(1f, 1f);
        waveRt.pivot = new Vector2(1f, 1f);
        waveRt.anchoredPosition = new Vector2(-12f, -4f);
        waveRt.sizeDelta = new Vector2(60f, 20f);

        _waveformTMP = waveObj.AddComponent<TextMeshProUGUI>();
        _waveformTMP.fontSize = 13f;
        _waveformTMP.alignment = TextAlignmentOptions.Right;
        _waveformTMP.raycastTarget = false;

        // Alt Altyazı Metni (Subtitle)
        GameObject subObj = new GameObject("SubtitleTMP");
        subObj.transform.SetParent(innerObj.transform, false);
        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = Vector2.zero;
        subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 0f);
        subRt.offsetMin = new Vector2(12f, 4f);
        subRt.offsetMax = new Vector2(-12f, -24f);

        _subtitleTMP = subObj.AddComponent<TextMeshProUGUI>();
        _subtitleTMP.fontSize = 14f;
        _subtitleTMP.alignment = TextAlignmentOptions.Left;
        _subtitleTMP.color = new Color(0.92f, 0.96f, 1f);
        _subtitleTMP.textWrappingMode = TextWrappingModes.Normal;
        _subtitleTMP.overflowMode = TextOverflowModes.Ellipsis;
        _subtitleTMP.raycastTarget = false;

        _radioPanelObj.SetActive(false);
    }

    public void SetRadioVolume(float volume)
    {
        radioVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREF_RADIO_VOL, radioVolume);
        PlayerPrefs.Save();
        if (_audioSource != null) _audioSource.volume = radioVolume;
    }

    public void ToggleRadioEnabled()
    {
        isEnabled = !isEnabled;
        PlayerPrefs.SetInt(PREF_RADIO_ENABLED, isEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetHUDVisible(bool visible)
    {
        if (_radioPanelObj != null)
        {
            _radioPanelObj.SetActive(visible && _activeRadioRoutine != null);
        }
    }
}
