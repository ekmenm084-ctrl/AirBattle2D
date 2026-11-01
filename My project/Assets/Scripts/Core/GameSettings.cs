using UnityEngine;

/// <summary>
/// Oyunun tüm genel ayarlarını (Zorluk, Mod) saklayan merkezi sınıf.
/// Sahneler arasında kaybolmaz ve PlayerPrefs ile kalıcıdır.
/// </summary>
public static class GameSettings
{
    public enum GameMode 
    { 
        Solo,       // 20 Uçaklık Hikaye Seferi (Bölüm 1, 2, 3)
        Online,     // 2 Oyunculu Düello
        Endless,    // Sonsuz İt Dalaşı (High Score)
        BossRush    // Sadece Boss Filosu (4 Boss Peş Peşe)
    }

    public enum Difficulty 
    { 
        Kolay,  // Acemi Pilot (Düşmanlar yavaş, seyrek ateş, az can)
        Normal, // Yüzbaşı (Dengeli standart deneyim)
        Zor     // As Pilot (Düşmanlar çevik, seri ateş, yüksek can ve hasar)
    }

    public static GameMode SelectedMode = GameMode.Solo;
    public static Difficulty SelectedDifficulty = Difficulty.Normal;

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

    public static void LoadSettings()
    {
        SelectedMode = (GameMode)PlayerPrefs.GetInt("GameSettings_Mode", (int)GameMode.Solo);
        SelectedDifficulty = (Difficulty)PlayerPrefs.GetInt("GameSettings_Difficulty", (int)Difficulty.Normal);
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
}

