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
    private AudioClip _thunderClip;
    private AudioClip _buttonClickClip;

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
    /// Her uçağın kalibresine ve karakterine özel makineli top ateş sesi oynatır.
    /// A-10: Tok ve gürleyen 30mm Avenger bası (BRRRTT)
    /// F-22: Yüksek frekanslı süpersonik kamçı sesi
    /// Su-57: Ağır Rus oto-top gümbürtüsü
    /// F-35: Seri ve keskin modern gatling vuruşları
    /// KAAN: Zirve rezonanslı hipersonik gürleme
    /// F-16: Klasik dengeli 20mm Vulcan
    /// </summary>
    public void PlayGunfireForAircraft(HangarManager.AircraftType type, bool isSalvo = false)
    {
        if (isMuted || gunfireClip == null || _sfxSource == null) return;

        float targetPitch = type switch
        {
            HangarManager.AircraftType.A10TankBuster => Random.Range(0.64f, 0.72f),
            HangarManager.AircraftType.F22Raptor     => Random.Range(0.92f, 1.00f),
            HangarManager.AircraftType.Su57Felon     => Random.Range(0.74f, 0.82f),
            HangarManager.AircraftType.F35Lightning  => Random.Range(0.86f, 0.94f),
            HangarManager.AircraftType.KaanApex      => Random.Range(0.80f, 0.88f),
            _                                        => Random.Range(0.82f, 0.90f)
        };

        float vol = sfxVolume * (type == HangarManager.AircraftType.A10TankBuster ? 1.05f : (isSalvo ? 0.95f : 0.85f));
        _sfxSource.pitch = targetPitch;
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
        float pitchStep = Mathf.Clamp(0.85f + (comboCount * 0.015f), 0.85f, 1.08f);
        PlaySynthEffect(ref _grazeClip, pitchStep - 0.02f, pitchStep + 0.02f, 0.48f);
    }

    public void PlayComboTierUp(float multiplier)
    {
        float pitch = Mathf.Clamp(0.85f + (multiplier - 1f) * 0.08f, 0.85f, 1.08f);
        PlaySynthEffect(ref _comboTierClip, pitch, pitch + 0.02f, 0.65f);
    }

    public void PlayThunder()
    {
        PlaySynthEffect(ref _thunderClip, 0.82f, 0.96f, 0.78f);
    }

    public void PlayButtonClick()
    {
        PlaySynthEffect(ref _buttonClickClip, 0.94f, 1.04f, 0.55f);
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

        // 1. Tok Mekanik Kokpit Anahtarı Sesi (Buton Tıklaması - Kesinlikle tiz/cızırtısız)
        _buttonClickClip = BuildSynthClip("SynthButtonClick", sr, 0.055f, (t, n) =>
        {
            float freq = Mathf.Lerp(175f, 58f, n);
            float env = Mathf.Exp(-n * 7.5f) * (1f - Mathf.Exp(-n * 90f));
            return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.45f * Mathf.Sin(4f * Mathf.PI * freq * t)) * env * 0.65f;
        }, cutoffHz: 420f);

        // 2. Ağır Zırh / Kalkan Darbe Sesi (Derin metalik bas vuruşu)
        _shieldHitClip = BuildSynthClip("SynthShieldHit", sr, 0.16f, (t, n) =>
        {
            float freq = Mathf.Lerp(210f, 64f, n);
            float env = (1f - n) * (1f - n);
            return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.4f * Mathf.Sin(3f * Mathf.PI * freq * t)) * env * 0.70f;
        }, cutoffHz: 380f);

        // 3. İkmal / Ganimet Toplama (Tok hidrolik kilit sesi)
        _lootPickupClip = BuildSynthClip("SynthLoot", sr, 0.16f, (t, n) =>
        {
            float freq = n < 0.45f ? 146.83f : 220.0f; // D3 -> A3 derin bas aralığı
            float env = Mathf.Sin(Mathf.PI * n);
            return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * freq * t)) * env * 0.60f;
        }, cutoffHz: 460f);

        // 4. Sandık Anahtarı Toplama (Derin askeri akor kilidi)
        _keyPickupClip = BuildSynthClip("SynthKeyLoot", sr, 0.28f, (t, n) =>
        {
            float freq = (n < 0.33f) ? 130.81f : (n < 0.66f ? 164.81f : 196.00f); // C3 -> E3 -> G3 tok bas akor
            float env = Mathf.Clamp01(1f - n * 0.75f);
            return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t)) * env * 0.65f;
        }, cutoffHz: 480f);

        // 5. Boss / Tehlike Alarmı (Kaba, sert denizaltı / savaş jeti bas kornası - 135Hz -> 92Hz)
        _warningAlarmClip = BuildSynthClip("SynthWarning", sr, 0.42f, (t, n) =>
        {
            float half = (n < 0.5f) ? n * 2f : (n - 0.5f) * 2f;
            float freq = Mathf.Lerp(148f, 96f, half);
            float env = Mathf.Sin(Mathf.PI * half);
            float raw = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.55f * Mathf.Sin(4f * Mathf.PI * freq * t) + 0.30f * Mathf.Sin(6f * Mathf.PI * freq * t);
            return (float)System.Math.Tanh(raw * 1.6f) * env * 0.72f;
        }, cutoffHz: 360f);

        // 6. Füze Ateşleme (Derin roket motoru ateşleme gümbürtüsü)
        _missileLaunchClip = BuildSynthClip("SynthMissile", sr, 0.26f, (t, n) =>
        {
            float freq = Mathf.Lerp(145f, 48f, n);
            float rumble = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * (freq * 1.45f) * t);
            float env = Mathf.Pow(1f - n, 1.3f);
            return (float)System.Math.Tanh(rumble * 1.8f) * env * 0.75f;
        }, cutoffHz: 340f);

        // 7. Patlama Sesi (Derin sub-bass şok dalgası)
        _synthExplosionClip = BuildSynthClip("SynthExplosion", sr, 0.40f, (t, n) =>
        {
            float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(105f, 30f, n) * t);
            float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(65f, 24f, n) * t);
            float env = Mathf.Pow(1f - n, 1.6f);
            return (float)System.Math.Tanh((thump * 0.85f + sub * 0.75f) * 2.0f) * env * 0.82f;
        }, cutoffHz: 280f);

        // 8. Zafer / Ödül Sesi (Derin sinematik pirinç/bas güç akoru)
        _synthFanfareClip = BuildSynthClip("SynthFanfare", sr, 0.44f, (t, n) =>
        {
            float freq = (n < 0.30f) ? 110.0f : 146.83f; // A2 -> D3 ağır bas fanfar
            float env = Mathf.Clamp01(1f - n * 0.7f);
            float chord = Mathf.Sin(2f * Mathf.PI * freq * t)
                        + 0.65f * Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t)
                        + 0.45f * Mathf.Sin(2f * Mathf.PI * (freq * 1.5f) * t);
            return (float)System.Math.Tanh(chord * 1.3f) * env * 0.68f;
        }, cutoffHz: 440f);

        // 9. Mermi Sıyrılma / Graze (Tok rüzgar/türbin bas geçişi)
        _grazeClip = BuildSynthClip("SynthGraze", sr, 0.10f, (t, n) =>
        {
            float freq = Mathf.Lerp(180f, 110f, n);
            float env = Mathf.Sin(Mathf.PI * n);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.52f;
        }, cutoffHz: 360f);

        // 10. Kombo Kademe Yükselişi (Ağır türbin güçlenme sesi)
        _comboTierClip = BuildSynthClip("SynthComboTier", sr, 0.22f, (t, n) =>
        {
            float freq = n < 0.45f ? 130.81f : 174.61f; // C3 -> F3
            float env = Mathf.Clamp01(1f - n * 0.65f);
            return (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t)) * env * 0.62f;
        }, cutoffHz: 420f);

        // 11. Gök Gürültüsü (Derin atmosferik sub-bass gürleme)
        _thunderClip = BuildSynthClip("SynthThunder", sr, 0.50f, (t, n) =>
        {
            float rumble = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(82f, 28f, n) * t)
                         + 0.6f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(54f, 22f, n) * t);
            float env = Mathf.Pow(1f - n, 1.4f);
            return (float)System.Math.Tanh(rumble * 1.7f) * env * 0.80f;
        }, cutoffHz: 240f);
    }

    private AudioClip BuildSynthClip(string clipName, int sampleRate, float duration, System.Func<float, float, float> sampleFunc, float cutoffHz = 450f)
    {
        int totalSamples = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
        float[] data = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            float n = (float)i / totalSamples;
            float t = (float)i / sampleRate;
            // Yumuşak fade-in / fade-out zarfı ile tıklama/cızırtı artefaktlarını tamamen önle
            float edgeFade = Mathf.Clamp01(i / 48f) * Mathf.Clamp01((totalSamples - 1 - i) / 96f);
            data[i] = Mathf.Clamp(sampleFunc(t, n) * edgeFade, -0.92f, 0.92f);
        }

        // 3 kademeli Low-Pass filtre ile tüm tiz cızırtıları sil, tok ve sert bas karakteri koru
        float rc = 1.0f / (2.0f * Mathf.PI * Mathf.Max(80f, cutoffHz));
        float dt = 1.0f / sampleRate;
        float alpha = dt / (rc + dt);
        for (int pass = 0; pass < 3; pass++)
        {
            float val = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                val += alpha * (data[i] - val);
                data[i] = val;
            }
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

    private Coroutine _duckCoroutine;

    /// <summary>
    /// Askeri telsiz konuşması başladığında arka plan savaş müziğini yumuşakça kısar (Audio Ducking).
    /// </summary>
    public void DuckMusic(float targetFactor = 0.35f, float duration = 0.18f)
    {
        if (isMuted || _musicSource == null) return;
        if (_duckCoroutine != null) StopCoroutine(_duckCoroutine);
        _duckCoroutine = StartCoroutine(FadeMusicVolumeRoutine(musicVolume * targetFactor, duration));
    }

    /// <summary>
    /// Telsiz konuşması bittiğinde savaş müziğini eski seviyesine geri getirir.
    /// </summary>
    public void RestoreMusic(float duration = 0.35f)
    {
        if (isMuted || _musicSource == null) return;
        if (_duckCoroutine != null) StopCoroutine(_duckCoroutine);
        _duckCoroutine = StartCoroutine(FadeMusicVolumeRoutine(musicVolume, duration));
    }

    private System.Collections.IEnumerator FadeMusicVolumeRoutine(float targetVol, float duration)
    {
        if (_musicSource == null) yield break;
        float startVol = _musicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(startVol, targetVol, elapsed / duration);
            yield return null;
        }
        _musicSource.volume = targetVol;
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

