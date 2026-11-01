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
        if (clip == null) clip = gunfireClip;

        if (clip != null)
        {
            _sfxSource.pitch = isBoss ? Random.Range(0.50f, 0.65f) : Random.Range(0.70f, 0.85f);
            float vol = sfxVolume * (isBoss ? 1.0f : 0.88f);
            _sfxSource.PlayOneShot(clip, vol);
        }
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

