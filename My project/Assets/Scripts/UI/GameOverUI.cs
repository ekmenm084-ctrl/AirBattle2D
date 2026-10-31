using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun sonu ekranı: kazanan metni, yeniden oyna ve ana menü butonları.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("UI Elemanları")]
    public GameObject panel;          // Ana panel (başlangıçta kapalı)
    public Text winnerText;           // "OYUNCU 1 KAZANDI!" gibi
    public Button restartButton;
    public Button mainMenuButton;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);

        if (restartButton != null)
            restartButton.onClick.AddListener(() => GameManager.Instance?.RestartGame());

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(() => GameManager.Instance?.GoToMainMenu());
    }

    /// <summary>GameManager tarafından çağrılır.</summary>
    public void Show(string message)
    {
        if (panel != null) panel.SetActive(true);
        if (winnerText != null) winnerText.text = message;
    }
}

