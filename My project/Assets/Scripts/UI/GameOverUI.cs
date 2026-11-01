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

    private Button _hangarDirectBtn;

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

        int earned = (GameManager.Instance != null) ? GameManager.Instance.sessionGoldEarned : 0;
        int totalGold = HangarManager.EnsureInstance().gold;

        string fullMsg = message;
        if (earned > 0)
        {
            fullMsg += $"\n\nSEFER KAZANCI: +{earned} G\nKASADAKİ TOPLAM: {totalGold} G";
        }

        if (winnerText != null) winnerText.text = fullMsg;

        EnsureHangarButton();
    }

    private void EnsureHangarButton()
    {
        if (panel == null || _hangarDirectBtn != null) return;

        Transform existing = panel.transform.Find("DirectHangarButton");
        if (existing != null)
        {
            _hangarDirectBtn = existing.GetComponent<Button>();
            return;
        }

        GameObject hObj = new GameObject("DirectHangarButton");
        hObj.transform.SetParent(panel.transform, false);

        RectTransform rt = hObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(340f, 50f);
        rt.anchoredPosition = new Vector2(0f, -120f);

        Image bg = hObj.AddComponent<Image>();
        bg.color = new Color(0.72f, 0.48f, 0.12f, 0.95f);

        _hangarDirectBtn = hObj.AddComponent<Button>();
        _hangarDirectBtn.targetGraphic = bg;
        _hangarDirectBtn.onClick.AddListener(() =>
        {
            GameManager.Instance?.GoToMainMenu();
            MainMenuManager menu = FindObjectOfType<MainMenuManager>();
            menu?.OpenHangarPanel();
        });

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(hObj.transform, false);
        RectTransform tRt = txtObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        Text t = txtObj.AddComponent<Text>();
        t.text = "HANGAR'A GİT (UÇAK GELİŞTİR)";
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 17;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
    }
}

