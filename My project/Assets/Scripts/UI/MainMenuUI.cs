using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Ana menü UI'ı. Mod seçimi ve oyun başlatma.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Butonlar")]
    public Button soloVsAIButton;
    public Button twoPlayerButton;
    public Button quitButton;

    [Header("Seçim Göstergesi")]
    public Image soloHighlight;
    public Image twoPlayerHighlight;

    private void Start()
    {
        if (FindObjectOfType<ModeSelector>() == null)
            new GameObject("ModeSelector").AddComponent<ModeSelector>();

        if (soloVsAIButton != null)
            soloVsAIButton.onClick.AddListener(OnSoloSelected);

        if (twoPlayerButton != null)
            twoPlayerButton.onClick.AddListener(OnTwoPlayerSelected);

        if (quitButton != null)
            quitButton.onClick.AddListener(Application.Quit);

        SelectSolo();
    }

    private void OnSoloSelected()
    {
        SelectSolo();
        StartGame();
    }

    private void OnTwoPlayerSelected()
    {
        ModeSelector.SetMode(ModeSelector.Mode.TwoPlayer);
        if (soloHighlight != null) soloHighlight.enabled = false;
        if (twoPlayerHighlight != null) twoPlayerHighlight.enabled = true;
        StartGame();
    }

    private void SelectSolo()
    {
        ModeSelector.SetMode(ModeSelector.Mode.SoloVsAI);
        if (soloHighlight != null) soloHighlight.enabled = true;
        if (twoPlayerHighlight != null) twoPlayerHighlight.enabled = false;
    }

    private void StartGame()
    {
        SceneManager.LoadScene("GameScene");
    }
}
