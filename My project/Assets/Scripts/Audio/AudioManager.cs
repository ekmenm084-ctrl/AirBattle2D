using UnityEngine;

/// <summary>
/// Savaş müziği ve mermi ses efektlerini yöneten merkezi ses yöneticisi (Audio Manager).
/// Savaş müziği döngüsel çalar, ses seviyesi ayarlanabilir ve PlayerPrefs ile kaydedilir.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Ses Kaynakları")]
    private AudioSource _musicSource;
    private AudioSource _sfxSource;

    [Header("Ses Klipleri")]
    public AudioClip battleThemeClip;
    public AudioClip gunfireClip;

    [Header("Ayarlar")]
    public float musicVolume = 0.70f;
    public float sfxVolume = 0.85f;
    public bool isMuted = false;

    private const string PREF_MUSIC_VOL = "AirBattle_MusicVol";
    private const string PREF_SFX_VOL = "AirBattle_SFXVol";
    private const string PREF_MUTED = "AirBattle_IsMuted";

    // Prosedürel sentezlenen ek SFX klipleri (Resources klibi yoksa veya özel efektler için)
    private AudioClip _shieldHitClip;
    private AudioClip _lootPickupClip;
    private AudioClip _keyPickupClip;
    private AudioClip _warningAlarmClip;
    private AudioClip _missileLaunchClip;
    private AudioClip _synthExplosionClip;
    private AudioClip _synthFanfareClip;
    private AudioClip _grazeClip;
    private AudioClip _comboTierClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSavedSettings();
        InitializeAudioSources();
        LoadClipsIfNull();
    }

    private void Start()
    {
        PlayBattleTheme();
    }

    public static AudioManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("AudioManager");
        if (go == null)
        {
            go = new GameObject("AudioManager");
        }

        Instance = go.GetComponent<AudioManager>();
        if (Instance == null)
        {
            Instance = go.AddComponent<AudioManager>();
        }
        return Instance;
    }

    private void LoadSavedSettings()
    {
        musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 0.70f);
        sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.85f);
        isMuted = PlayerPrefs.GetInt(PREF_MUTED, 0) == 1;
    }

    private void InitializeAudioSources()
    {
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.volume = isMuted ? 0f : musicVolume;
            _musicSource.spatialBlend = 0f; // 2D Ses
        }

        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = isMuted ? 0f : sfxVolume;
            _sfxSource.spatialBlend = 0f; // 2D Ses
        }
    }

    private void LoadClipsIfNull()
    {
        if (battleThemeClip == null)
        {
            battleThemeClip = Resources.Load<AudioClip>("battle_theme");
        }

        if (gunfireClip == null)
        {
            gunfireClip = Resources.Load<AudioClip>("gunfire");
        }
    }

    /// <summary>
    /// Savaş arka plan müziğini kesintisiz döngü olarak başlatır.
    /// </summary>
    public void PlayBattleTheme()
    {
        LoadClipsIfNull();

        if (battleThemeClip != null && _musicSource != null)
        {
            if (_musicSource.clip != battleThemeClip || !_musicSource.isPlaying)
            {
                _musicSource.clip = battleThemeClip;
                _musicSource.volume = isMuted ? 0f : musicVolume;
                _musicSource.Play();
            }
        }
    }

    /// <summary>
    /// Uçak ateş ettiğinde dinamik mermi sesini oynatır.
    /// Hafif perde (pitch) varyasyonu ile makineli top hissi verir.
    /// </summary>
    public void PlayGunfire(bool isHeavy = false)
    {
        if (isMuted || gunfireClip == null || _sfxSource == null) return;

        // Gerçekçi makineli tüfek hissi için ses perdesini hafif rastgeleştir
        _sfxSource.pitch = Random.Range(0.92f, 1.08f);

        float vol = sfxVolume * (isHeavy ? 1.0f : 0.82f);
        _sfxSource.PlayOneShot(gunfireClip, vol);
    }

    /// <summary>
    /// Uçak veya Boss patladığında tok bir patlama sesi oynatır.
    /// </summary>
    public void PlayExplosion(bool isBoss = false)
    {
        if (isMuted || _sfxSource == null) return;

        AudioClip clip = Resources.Load<AudioClip>("explosion");
        if (clip == null)
        {
            EnsureSynthClips();
            clip = _synthExplosionClip != null ? _synthExplosionClip : gunfireClip;
        }

        if (clip != null)
        {
            _sfxSource.pitch = isBoss ? Random.Range(0.55f, 0.70f) : Random.Range(0.85f, 1.05f);
            float vol = sfxVolume * (isBoss ? 1.0f : 0.88f);
            _sfxSource.PlayOneShot(clip, vol);
        }
    }

    /// <summary>
    /// Seviye atlandığında veya büyük ödül kazanıldığında zafer fanfarı sesi oynatır.
    /// </summary>
    public void PlayRewardFanfare()
    {
        if (isMuted || _sfxSource == null) return;

        AudioClip clip = Resources.Load<AudioClip>("reward_fanfare");
        if (clip == null) clip = Resources.Load<AudioClip>("fanfare");
        if (clip == null)
        {
            EnsureSynthClips();
            clip = _synthFanfareClip;
        }

        if (clip != null)
        {
            _sfxSource.pitch = 1.0f;
            _sfxSource.PlayOneShot(clip, sfxVolume);
        }
        else if (gunfireClip != null)
        {
            _sfxSource.pitch = 1.85f;
            _sfxSource.PlayOneShot(gunfireClip, sfxVolume * 0.75f);
        }
    }

    public void PlayShieldHit()
    {
        PlaySynthEffect(ref _shieldHitClip, 0.92f, 1.08f, 0.65f);
    }

    public void PlayLootPickup(bool isKey = false)
    {
        EnsureSynthClips();
        AudioClip clip = isKey ? _keyPickupClip : _lootPickupClip;
        if (isMuted || _sfxSource == null || clip == null) return;
        _sfxSource.pitch = 1.0f;
        _sfxSource.PlayOneShot(clip, sfxVolume * 0.85f);
    }

    public void PlayWarningAlarm()
    {
        PlaySynthEffect(ref _warningAlarmClip, 0.98f, 1.02f, 0.78f);
    }

    public void PlayMissileLaunch()
    {
        PlaySynthEffect(ref _missileLaunchClip, 0.92f, 1.08f, 0.75f);
    }

    public void PlayGraze(int comboCount = 1)
    {
        float pitchStep = Mathf.Clamp(1.0f + (comboCount * 0.02f), 1.0f, 1.35f);
        PlaySynthEffect(ref _grazeClip, pitchStep - 0.03f, pitchStep + 0.03f, 0.48f);
    }

    public void PlayComboTierUp(float multiplier)
    {
        float pitch = Mathf.Clamp(0.95f + (multiplier - 1f) * 0.12f, 0.95f, 1.28f);
        PlaySynthEffect(ref _comboTierClip, pitch, pitch + 0.02f, 0.62f);
    }

    private void PlaySynthEffect(ref AudioClip clipField, float minPitch, float maxPitch, float volumeScale)
    {
        if (isMuted || _sfxSource == null) return;
        EnsureSynthClips();
        if (clipField == null) return;
        _sfxSource.pitch = Random.Range(minPitch, maxPitch);
        _sfxSource.PlayOneShot(clipField, sfxVolume * volumeScale);
    }

    private void EnsureSynthClips()
    {
        if (_shieldHitClip != null) return;

        const int sr = 22050;
        _shieldHitClip = BuildSynthClip("SynthShieldHit", sr, 0.14f, (t, n) =>
        {
            float freq = Mathf.Lerp(1150f, 420f, n);
            float env = (1f - n) * (1f - n);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
        });

        _lootPickupClip = BuildSynthClip("SynthLoot", sr, 0.16f, (t, n) =>
        {
            float freq = n < 0.5f ? 660f : 990f;
            float env = Mathf.Sin(Mathf.PI * n);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
        });

        _keyPickupClip = BuildSynthClip("SynthKeyLoot", sr, 0.28f, (t, n) =>
        {
            float freq = (n < 0.33f) ? 587f : (n < 0.66f ? 880f : 1175f);
            float env = Mathf.Clamp01(1f - n * 0.8f);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
        });

        _warningAlarmClip = BuildSynthClip("SynthWarning", sr, 0.32f, (t, n) =>
        {
            float half = (n < 0.5f) ? n * 2f : (n - 0.5f) * 2f;
            float freq = Mathf.Lerp(520f, 840f, half);
            float env = Mathf.Sin(Mathf.PI * half);
            return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * freq * t)) * env * 0.45f;
        });

        _missileLaunchClip = BuildSynthClip("SynthMissile", sr, 0.22f, (t, n) =>
        {
            float freq = Mathf.Lerp(260f, 95f, n);
            float noise = ((Mathf.Sin(t * 9876.5f) * 43758.54f) % 1f) * 2f - 1f;
            float env = 1f - n;
            return (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f + noise * 0.35f) * env * 0.6f;
        });

        _synthExplosionClip = BuildSynthClip("SynthExplosion", sr, 0.34f, (t, n) =>
        {
            float noise = ((Mathf.Sin(t * 14231.7f) * 29876.1f) % 1f) * 2f - 1f;
            float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 38f, n) * t);
            float env = Mathf.Pow(1f - n, 1.7f);
            return (noise * 0.55f + thump * 0.65f) * env * 0.7f;
        });

        _synthFanfareClip = BuildSynthClip("SynthFanfare", sr, 0.36f, (t, n) =>
        {
            float freq = (n < 0.33f) ? 523.25f : (n < 0.66f ? 659.25f : 783.99f);
            float env = Mathf.Clamp01(1f - n * 0.75f);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
        });

        _grazeClip = BuildSynthClip("SynthGraze", sr, 0.09f, (t, n) =>
        {
            float freq = Mathf.Lerp(1350f, 2250f, n);
            float env = Mathf.Sin(Mathf.PI * n);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.42f;
        });

        _comboTierClip = BuildSynthClip("SynthComboTier", sr, 0.20f, (t, n) =>
        {
            float freq = n < 0.48f ? 740f : 1108.73f;
            float env = Mathf.Clamp01(1f - n * 0.7f);
            return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * freq * t)) * env * 0.45f;
        });
    }

    private AudioClip BuildSynthClip(string clipName, int sampleRate, float duration, System.Func<float, float, float> sampleFunc)
    {
        int totalSamples = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
        float[] data = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            float n = (float)i / totalSamples;
            float t = (float)i / sampleRate;
            data[i] = Mathf.Clamp(sampleFunc(t, n), -0.95f, 0.95f);
        }
        AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Arka plan savaş müziğinin ses düzeyini ayarlar (0.0 - 1.0).
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicVolume > 0f && isMuted)
        {
            isMuted = false;
            PlayerPrefs.SetInt(PREF_MUTED, 0);
        }
        PlayerPrefs.SetFloat(PREF_MUSIC_VOL, musicVolume);
        PlayerPrefs.Save();

        if (_musicSource != null)
        {
            _musicSource.volume = isMuted ? 0f : musicVolume;
            if (musicVolume > 0f && !_musicSource.isPlaying && !isMuted)
            {
                _musicSource.Play();
            }
        }
    }

    /// <summary>
    /// Mermi ve patlama ses efektlerinin düzeyini ayarlar (0.0 - 1.0).
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxVolume > 0f && isMuted)
        {
            isMuted = false;
            PlayerPrefs.SetInt(PREF_MUTED, 0);
        }
        PlayerPrefs.SetFloat(PREF_SFX_VOL, sfxVolume);
        PlayerPrefs.Save();

        if (_sfxSource != null)
        {
            _sfxSource.volume = isMuted ? 0f : sfxVolume;
        }
    }

    /// <summary>
    /// Tüm sesleri tek tuşla açar/kapatır (Mute).
    /// </summary>
    public void ToggleMute()
    {
        isMuted = !isMuted;
        PlayerPrefs.SetInt(PREF_MUTED, isMuted ? 1 : 0);
        PlayerPrefs.Save();

        if (_musicSource != null)
            _musicSource.volume = isMuted ? 0f : musicVolume;

        if (_sfxSource != null)
            _sfxSource.volume = isMuted ? 0f : sfxVolume;
    }
}

