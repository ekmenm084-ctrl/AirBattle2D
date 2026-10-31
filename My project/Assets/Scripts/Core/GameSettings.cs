using UnityEngine;

/// <summary>
/// Oyunun tüm genel ayarlarını (Zorluk, Mod) saklayan merkezi sınıf.
/// Sahneler arasında kaybolmaz ve PlayerPrefs ile kalıcıdır.
/// </summary>
public static class GameSettings
{
    public enum GameMode { Solo, Online }
    public enum Difficulty { Kolay, Normal, Zor }

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
    }

    public static void LoadSettings()
    {
        SelectedMode = (GameMode)PlayerPrefs.GetInt("GameSettings_Mode", (int)GameMode.Solo);
        SelectedDifficulty = (Difficulty)PlayerPrefs.GetInt("GameSettings_Difficulty", (int)Difficulty.Normal);
    }
}

