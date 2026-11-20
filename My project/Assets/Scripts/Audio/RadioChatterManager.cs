using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Askeri Havacılık Telsiz & AWACS Seslendirme Yöneticisi (Military Radio Chatter & Tactical Voiceover).
/// - Yalnızca 2 kritik durumda devreye girer:
///   1. İlk görev girişi / kalkışta (Takeoff clearance).
///   2. İt dalaşı / dalga modunda her 5 düşmanda bir gelen Boss savaş jeti tanıtımında.
/// - Kısa, tok, net, erkeksi komutan/pilot ses tonu ve gerçekçi RF paraziti.
/// - Audio Ducking: Telsiz konuşurken arka plan müziğini yumuşakça kısar ve hemen geri yükler.
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

    private float _lastBossVoiceTime = -999f;
    private bool _hasPlayedTakeoffThisMatch = false;

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
        "|||", "|:|", "::|", "|::", "||:", ":||", ":::"
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
        _transmissionMap.Clear();

        // 1. İLK GİRİŞ / KALKIŞ (Kısa & Net)
        AddLine("radio_takeoff", "KULE KONTROL", "Kartal-1, kalkış serbest. Av serbest!",
            new Color(0f, 0.92f, 1f), Priority.High);

        // 2. HER 5 DÜŞMANDAN SONRA GELEN BOSS JETLERİ TANITIMI
        // Tu-22M Stratejik Bombardıman (Dalga 5 / 25 / 45...)
        AddLine("radio_boss_tu22", "AWACS KONTROL", "DİKKAT! Ağır Tu-22M Bombardıman sahada, savunma pozisyonu al!",
            new Color(1f, 0.35f, 0.15f), Priority.Critical);

        // Su-35 Flanker-E Elit Lider (Dalga 10 / 30 / 50...)
        AddLine("radio_boss_su35", "AWACS KONTROL", "DİKKAT! Elit Su-35 Flanker sahada, manevraya geç!",
            new Color(1f, 0.45f, 0.10f), Priority.Critical);

        // S-37 Berkut Süpersonik Nişancı (Dalga 15 / 35 / 55...)
        AddLine("radio_boss_s37", "AWACS KONTROL", "DİKKAT! Süpersonik Berkut yaklaşıyor, füzelerine dikkat et!",
            new Color(0.95f, 0.20f, 0.40f), Priority.Critical);

        // J-20 Hayalet Amiral Gemisi (Dalga 20 / 40 / 60...)
        AddLine("radio_boss_j20", "AWACS KONTROL", "DİKKAT! Düşman J-20 Amiral Gemisi sahada! Tam güçle vur!",
            new Color(1f, 0.18f, 0.18f), Priority.Critical);

        // Genel / Yedek Boss Çağrısı
        AddLine("radio_boss_warning", "AWACS KONTROL", "DİKKAT! Ağır Boss jeti yaklaşıyor, savunma pozisyonu al!",
            new Color(1f, 0.25f, 0.25f), Priority.Critical);
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

        if (_activeRadioRoutine != null)
        {
            StopCoroutine(_activeRadioRoutine);
            _activeRadioRoutine = null;
        }

        _activeRadioRoutine = StartCoroutine(RadioTransmissionRoutine(tx));
        return true;
    }

    // ========================================================
    // SADECE GİRİŞ VE HER 5 DÜŞMANDAN SONRA GELEN BOSS TETİKLEYİCİLERİ
    // ========================================================

    /// <summary>
    /// Yalnızca oyun ilk açıldığında / kalkış anında 1 kez çalar.
    /// </summary>
    public void PlayTakeoff()
    {
        if (_hasPlayedTakeoffThisMatch) return;
        _hasPlayedTakeoffThisMatch = true;
        PlayTransmission("radio_takeoff", force: true);
    }

    /// <summary>
    /// İt dalaşında her 5 düşmanda bir gelen Boss'u modeline göre tanıtır.
    /// </summary>
    public void PlayBossIntroduction(AIController.EnemyJetModel model, int waveIndex)
    {
        if (Time.time - _lastBossVoiceTime < 14.0f) return;
        _lastBossVoiceTime = Time.time;

        string targetId = model switch
        {
            AIController.EnemyJetModel.Tu22_Bomber     => "radio_boss_tu22",
            AIController.EnemyJetModel.Su35_Flanker    => "radio_boss_su35",
            AIController.EnemyJetModel.S37_Berkut      => "radio_boss_s37",
            AIController.EnemyJetModel.J20_Dreadnought => "radio_boss_j20",
            _                                          => "radio_boss_warning"
        };

        PlayTransmission(targetId, force: true);
    }

    public void PlayBossWarning()
    {
        if (Time.time - _lastBossVoiceTime < 14.0f) return;
        _lastBossVoiceTime = Time.time;
        PlayTransmission("radio_boss_warning", force: true);
    }

    /// <summary>
    /// Yeni maç başladığında kalkış bayrağını sıfırlar.
    /// </summary>
    public void ResetMatchState()
    {
        _hasPlayedTakeoffThisMatch = false;
        _lastBossVoiceTime = -999f;
        if (_activeRadioRoutine != null)
        {
            StopCoroutine(_activeRadioRoutine);
            _activeRadioRoutine = null;
        }
        if (_radioPanelObj != null)
        {
            _radioPanelObj.SetActive(false);
        }
        AudioManager.Instance?.RestoreMusic(0.1f);
    }

    // ========================================================
    // DİĞER DURUMLARDA TELSİZ DEVREYE GİRMEZ (SESSİZ - NO-OP)
    // ========================================================
    public void PlayAirKill() { }
    public void PlayStreak(int streakCount) { }
    public void PlayBossDown() { }
    public void PlayMissileWarning() { }
    public void PlaySquadronClear() { }
    public void PlayHullWarning() { }
    public void PlayOverdrive() { }
    public void PlayShieldDown() { }
    public void PlayCrateLoot() { }
    public void PlayHazardStorm() { }

    // ==========================================
    // TELSİZ SEKANS KORUTİNİ & HUD GÖRSELİ
    // ==========================================

    private IEnumerator RadioTransmissionRoutine(RadioTransmission tx)
    {
        EnsureHUDPanel();

        // 1. Audio Ducking (Arka plan savaş müziğini %35 seviyesine kıs)
        AudioManager.Instance?.DuckMusic(0.35f, 0.12f);

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
        string channel = tx.speaker.Contains("AWACS") ? "VHF 121.5 MHz" : "VHF 118.1 MHz";
        
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
        float animIn = 0.12f;
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
                rt.anchoredPosition = Vector2.Lerp(basePos + new Vector2(0f, 10f), basePos, t);
                rt.localScale = Vector3.Lerp(new Vector3(0.94f, 0.94f, 1f), Vector3.one, t);
            }
            yield return null;
        }

        if (_radioCanvasGroup != null) _radioCanvasGroup.alpha = 1f;
        if (rt != null)
        {
            rt.anchoredPosition = basePos;
            rt.localScale = Vector3.one;
        }

        // 5. Ses Çalarken Dalga Formu ve TX Yanıp Sönme Animasyonu (Kısa & Öz: maks 2 sn)
        float clipDuration = (tx.clip != null) ? Mathf.Min(tx.clip.length, 2.0f) : 1.8f;
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

        // 6. Çok uzatmadan hızlıca kapat (yalnızca 0.12s bekleme)
        if (_waveformTMP != null) _waveformTMP.text = "<color=#546E7A>---</color>";
        yield return new WaitForSecondsRealtime(0.12f);

        // 7. Panel Kapanış Animasyonu (Hızlı Fade-Out)
        float animOut = 0.12f;
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
        AudioManager.Instance?.RestoreMusic(0.25f);

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
        rt.anchoredPosition = new Vector2(0f, -140f);
        rt.sizeDelta = new Vector2(640f, 84f);

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
        headerRt.anchoredPosition = new Vector2(16f, -6f);
        headerRt.sizeDelta = new Vector2(-100f, 26f);

        _headerTMP = headerObj.AddComponent<TextMeshProUGUI>();
        _headerTMP.fontSize = 18f;
        _headerTMP.fontStyle = FontStyles.Bold;
        _headerTMP.alignment = TextAlignmentOptions.Left;
        _headerTMP.raycastTarget = false;

        // Sağ Üst Dalga Formu / Equalizer (Waveform)
        GameObject waveObj = new GameObject("WaveTMP");
        waveObj.transform.SetParent(innerObj.transform, false);
        RectTransform waveRt = waveObj.AddComponent<RectTransform>();
        waveRt.anchorMin = new Vector2(1f, 1f);
        waveRt.anchorMax = new Vector2(1f, 1f);
        waveRt.pivot = new Vector2(1f, 1f);
        waveRt.anchoredPosition = new Vector2(-16f, -6f);
        waveRt.sizeDelta = new Vector2(80f, 26f);

        _waveformTMP = waveObj.AddComponent<TextMeshProUGUI>();
        _waveformTMP.fontSize = 16f;
        _waveformTMP.fontStyle = FontStyles.Bold;
        _waveformTMP.alignment = TextAlignmentOptions.Right;
        _waveformTMP.raycastTarget = false;

        // Alt Altyazı Metni (Subtitle)
        GameObject subObj = new GameObject("SubtitleTMP");
        subObj.transform.SetParent(innerObj.transform, false);
        RectTransform subRt = subObj.AddComponent<RectTransform>();
        subRt.anchorMin = Vector2.zero;
        subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 0f);
        subRt.offsetMin = new Vector2(16f, 8f);
        subRt.offsetMax = new Vector2(-16f, -32f);

        _subtitleTMP = subObj.AddComponent<TextMeshProUGUI>();
        _subtitleTMP.fontSize = 20f;
        _subtitleTMP.fontStyle = FontStyles.Bold;
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
