using System;
using UnityEngine;

/// <summary>
/// Oyunun tüm genel ayarlarını (Zorluk, Mod, Kontrol Şeması) saklayan merkezi sınıf.
/// Sahneler arasında kaybolmaz ve PlayerPrefs ile kalıcıdır.
/// </summary>
public static class GameSettings
{
    public enum GameMode 
    { 
        Solo,       // 20 Uçaklık Hikaye Seferi (Bölüm 1, 2, 3)
        Online,     // 2 Oyunculu Düello
        Endless,    // Sonsuz İt Dalaşı (High Score)
        BossRush,   // Sadece Boss Filosu (4 Boss Peş Peşe)
        BulletHell, // Mermi Cehennemi (Aşırı Mermi Fırtınası & Hayatta Kalma)
        TimeAttack, // Zaman Taarruzu (Zamana Karşı Hızlı Av Modu)
        Hardcore    // Hardcore Modu (Tek Can, 200 HP, Her Gelen Düşman Güçlenen Boss)
    }

    public enum Difficulty 
    { 
        Kolay,  // Acemi Pilot (Düşmanlar yavaş, seyrek ateş, az can)
        Normal, // Yüzbaşı (Dengeli standart deneyim)
        Zor     // As Pilot (Düşmanlar çevik, seri ateş, yüksek can ve hasar)
    }

    public enum ControlType
    {
        Joystick,   // Sanal Taktik Uçuş Joystick'i
        Arrows      // Sol / Sağ Dokunmatik Taktik Ok Butonları
    }

    public enum CockpitTheme
    {
        CyberBlue = 0,   // Varsayılan Siber Mavi (Mevcut 1:1 görünüm)
        RadarGreen = 1,  // F-16 Taktik Radar Yeşili & Gece Görüş
        StealthGold = 2  // Black Ops Karbon & Altın Kehribar
    }

    public static GameMode SelectedMode = GameMode.Solo;
    public static Difficulty SelectedDifficulty = Difficulty.Normal;
    public static ControlType SelectedControl = ControlType.Arrows;
    public static CockpitTheme SelectedCockpitTheme = CockpitTheme.CyberBlue;

    public static event Action<ControlType> OnControlSchemeChanged;
    public static event Action<CockpitTheme> OnCockpitThemeChanged;

    public static void SetMode(GameMode mode)
    {
        SelectedMode = mode;
        PlayerPrefs.SetInt("GameSettings_Mode", (int)mode);
        PlayerPrefs.Save();
    }

    public static void SetDifficulty(Difficulty difficulty)
    {
        SelectedDifficulty = difficulty;
        PlayerPrefs.SetInt("GameSettings_Difficulty", (int)difficulty);
        PlayerPrefs.Save();
        Debug.Log($"[GameSettings] Zorluk Kaydedildi: {difficulty}");
    }

    public static void SetControlType(ControlType control)
    {
        SelectedControl = control;
        PlayerPrefs.SetInt("GameSettings_ControlType", (int)control);
        PlayerPrefs.Save();
        Debug.Log($"[GameSettings] Kontrol Tipi Kaydedildi: {control}");
        OnControlSchemeChanged?.Invoke(control);
    }

    public static void ToggleControlType()
    {
        ControlType next = (SelectedControl == ControlType.Joystick) ? ControlType.Arrows : ControlType.Joystick;
        SetControlType(next);
    }

    public static void SetCockpitTheme(CockpitTheme theme)
    {
        SelectedCockpitTheme = theme;
        PlayerPrefs.SetInt("GameSettings_CockpitTheme", (int)theme);
        PlayerPrefs.Save();
        Debug.Log($"[GameSettings] Kokpit Teması Kaydedildi: {theme}");
        OnCockpitThemeChanged?.Invoke(theme);
    }

    public static void CycleCockpitTheme()
    {
        int next = ((int)SelectedCockpitTheme + 1) % 3;
        SetCockpitTheme((CockpitTheme)next);
    }

    public static void LoadSettings()
    {
        SelectedMode = (GameMode)PlayerPrefs.GetInt("GameSettings_Mode", (int)GameMode.Solo);
        SelectedDifficulty = (Difficulty)PlayerPrefs.GetInt("GameSettings_Difficulty", (int)Difficulty.Normal);
        SelectedControl = (ControlType)PlayerPrefs.GetInt("GameSettings_ControlType", (int)ControlType.Arrows);

        int rawTheme = PlayerPrefs.GetInt("GameSettings_CockpitTheme", (int)CockpitTheme.CyberBlue);
        if (rawTheme < 0 || rawTheme > 2) rawTheme = (int)CockpitTheme.CyberBlue;
        SelectedCockpitTheme = (CockpitTheme)rawTheme;
    }

    public static string GetDifficultyName(Difficulty diff)
    {
        switch (diff)
        {
            case Difficulty.Kolay: return "KOLAY (Acemi)";
            case Difficulty.Zor:   return "ZOR (As Pilot)";
            default:               return "NORMAL (Yüzbaşı)";
        }
    }

    public static string GetDifficultyColorHex(Difficulty diff)
    {
        switch (diff)
        {
            case Difficulty.Kolay: return "#2ECC71"; // Yeşil
            case Difficulty.Zor:   return "#E74C3C"; // Kırmızı
            default:               return "#F1C40F"; // Altın Sarı
        }
    }

    public static string GetControlTypeName(ControlType control)
    {
        switch (control)
        {
            case ControlType.Arrows: return "SOL / SAĞ OK TUŞLARI";
            default:                 return "SANAL JOYSTICK";
        }
    }

    public static string GetCockpitThemeName(CockpitTheme theme)
    {
        switch (theme)
        {
            case CockpitTheme.RadarGreen:  return "RADAR YEŞİLİ";
            case CockpitTheme.StealthGold: return "ALTIN KARBON";
            default:                       return "SİBER MAVİ";
        }
    }

    public static string GetCockpitThemeColorHex(CockpitTheme theme)
    {
        switch (theme)
        {
            case CockpitTheme.RadarGreen:  return "#22C55E";
            case CockpitTheme.StealthGold: return "#F59E0B";
            default:                       return "#00E5FF";
        }
    }

    public static Color GetCockpitHpBarColor()
    {
        switch (SelectedCockpitTheme)
        {
            case CockpitTheme.RadarGreen:  return new Color(0.13f, 0.84f, 0.42f, 1f); // F-16 Radar Zümrüt Yeşili
            case CockpitTheme.StealthGold: return new Color(0.96f, 0.65f, 0.14f, 1f); // Black Ops Kehribar Altın
            default:                       return new Color(0.03f, 0.82f, 0.93f, 1f); // Mevcut 1:1 #08D1EC Parlak Cyan
        }
    }

    public static Color GetCockpitUltiBarColor()
    {
        switch (SelectedCockpitTheme)
        {
            case CockpitTheme.RadarGreen:  return new Color(0.42f, 0.96f, 0.62f, 1f); // Fosfor Nane Yeşili
            case CockpitTheme.StealthGold: return new Color(1.00f, 0.82f, 0.34f, 1f); // Aviyonik Açık Altın
            default:                       return new Color(0.49f, 0.95f, 1f, 1f);    // Mevcut 1:1 #7EF2FF Buz Mavisi
        }
    }

    public static Color GetCockpitAccentBorderColor(float alpha = 0.85f)
    {
        switch (SelectedCockpitTheme)
        {
            case CockpitTheme.RadarGreen:  return new Color(0.14f, 0.86f, 0.44f, alpha);
            case CockpitTheme.StealthGold: return new Color(0.96f, 0.66f, 0.14f, alpha);
            default:                       return new Color(0f, 0.85f, 1f, alpha);    // Mevcut 1:1 Cyan Çerçeve
        }
    }

    public static Color GetCockpitSkyTint()
    {
        switch (SelectedCockpitTheme)
        {
            case CockpitTheme.RadarGreen:  return new Color(0.76f, 0.97f, 0.84f, 1f); // Gece Görüş / Radar Bulut Tonu
            case CockpitTheme.StealthGold: return new Color(1.00f, 0.89f, 0.76f, 1f); // Alacakaranlık Kehribar Bulut Tonu
            default:                       return Color.white;                        // Mevcut 1:1 Orijinal Gökyüzü
        }
    }

    public static Color GetCockpitLightningTint()
    {
        switch (SelectedCockpitTheme)
        {
            case CockpitTheme.RadarGreen:  return new Color(0.68f, 1.00f, 0.78f, 1f);
            case CockpitTheme.StealthGold: return new Color(1.00f, 0.88f, 0.66f, 1f);
            default:                       return new Color(0.72f, 0.90f, 1.0f, 1f);  // Mevcut 1:1 Şimşek Tonu
        }
    }
}
