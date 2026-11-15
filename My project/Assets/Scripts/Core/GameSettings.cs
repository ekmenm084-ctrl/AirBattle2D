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

    public static GameMode SelectedMode = GameMode.Solo;
    public static Difficulty SelectedDifficulty = Difficulty.Normal;
    public static ControlType SelectedControl = ControlType.Arrows;

    public static event Action<ControlType> OnControlSchemeChanged;

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

    public static void LoadSettings()
    {
        SelectedMode = (GameMode)PlayerPrefs.GetInt("GameSettings_Mode", (int)GameMode.Solo);
        SelectedDifficulty = (Difficulty)PlayerPrefs.GetInt("GameSettings_Difficulty", (int)Difficulty.Normal);
        SelectedControl = (ControlType)PlayerPrefs.GetInt("GameSettings_ControlType", (int)ControlType.Arrows);
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
}
