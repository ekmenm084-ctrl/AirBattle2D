using UnityEngine;

/// <summary>
/// Başlangıç sahnesinde seçilen oyun modunu taşır (DontDestroyOnLoad).
/// </summary>
public class ModeSelector : MonoBehaviour
{
    public enum Mode { SoloVsAI, TwoPlayer }
    public static Mode SelectedMode { get; private set; } = Mode.SoloVsAI;

    private static ModeSelector _instance;

    private void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>MainMenuUI tarafından çağrılır.</summary>
    public static void SetMode(Mode mode)
    {
        SelectedMode = mode;
        if (mode == Mode.SoloVsAI)
            GameSettings.SelectedMode = GameSettings.GameMode.Solo;
        else
            GameSettings.SelectedMode = GameSettings.GameMode.Online;
    }
}

