using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun sonu ekranı: kazanan metni, yeniden oyna, hangar ve ana menü butonları.
/// Sahnede panel atanmamışsa dahi otomatik olarak Canvas üzerinde kusursuz panel inşa eder.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("UI Elemanları")]
    public GameObject panel;          // Ana panel (başlangıçta kapalı)
    public Text winnerText;           // "OYUNCU 1 KAZANDI!" gibi
    public Button restartButton;
    public Button mainMenuButton;

    private Button _hangarDirectBtn;
    private Button _leaderboardBtn;

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
        EnsurePanel();

        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

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

    private void EnsurePanel()
    {
        if (panel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (canvases.Length > 0) canvas = canvases[0];
        }
        if (canvas == null) return;

        panel = new GameObject("GameOverPanel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = Vector2.zero;
        pRt.anchorMax = Vector2.one;
        pRt.offsetMin = Vector2.zero;
        pRt.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.14f, 0.94f);

        // Winner / Message Text
        GameObject tObj = new GameObject("WinnerText", typeof(RectTransform));
        tObj.transform.SetParent(panel.transform, false);
        RectTransform tRt = tObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.1f, 0.42f);
        tRt.anchorMax = new Vector2(0.9f, 0.88f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        winnerText = tObj.AddComponent<Text>();
        winnerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        winnerText.fontSize = 24;
        winnerText.fontStyle = FontStyle.Bold;
        winnerText.alignment = TextAnchor.MiddleCenter;
        winnerText.color = Color.white;

        // Restart Button
        restartButton = CreateBtn(panel.transform, "RestartBtn", "YENİDEN OYNA", new Vector2(0f, -35f), new Color(0.16f, 0.55f, 0.32f), () => GameManager.Instance?.RestartGame());

        // Leaderboard Button
        _leaderboardBtn = CreateBtn(panel.transform, "LeaderboardBtn", "🏆 KÜRESEL REKOR TABLOSU", new Vector2(0f, -95f), new Color(0.72f, 0.48f, 0.12f), OpenLeaderboardFromGameOver);

        // Main Menu Button
        mainMenuButton = CreateBtn(panel.transform, "MainMenuBtn", "ANA MENÜYE DÖN", new Vector2(0f, -215f), new Color(0.25f, 0.30f, 0.38f), () => GameManager.Instance?.GoToMainMenu());
    }

    private void OpenLeaderboardFromGameOver()
    {
        LeaderboardManager.BoardMode bMode = LeaderboardManager.BoardMode.Endless;
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentMode == GameManager.GameMode.TimeAttack)
                bMode = LeaderboardManager.BoardMode.TimeAttack;
            else if (GameManager.Instance.CurrentMode == GameManager.GameMode.BulletHell)
                bMode = LeaderboardManager.BoardMode.BulletHell;
            else if (GameManager.Instance.CurrentMode == GameManager.GameMode.BossRush)
                bMode = LeaderboardManager.BoardMode.BossRush;
        }

        LeaderboardUI.EnsureUI()?.OpenLeaderboard(bMode, null);
    }

    private Button CreateBtn(Transform parent, string name, string text, Vector2 pos, Color col, UnityEngine.Events.UnityAction action)
    {
        GameObject bObj = new GameObject(name, typeof(RectTransform));
        bObj.transform.SetParent(parent, false);
        RectTransform rt = bObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(340f, 50f);
        rt.anchoredPosition = pos;

        Image img = bObj.AddComponent<Image>();
        img.color = col;

        Button btn = bObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        GameObject tObj = new GameObject("Text", typeof(RectTransform));
        tObj.transform.SetParent(bObj.transform, false);
        RectTransform tRt = tObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        Text t = tObj.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 18;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;

        return btn;
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

        GameObject hObj = new GameObject("DirectHangarButton", typeof(RectTransform));
        hObj.transform.SetParent(panel.transform, false);

        RectTransform rt = hObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(340f, 50f);
        rt.anchoredPosition = new Vector2(0f, -155f);

        Image bg = hObj.AddComponent<Image>();
        bg.color = new Color(0.72f, 0.48f, 0.12f, 0.95f);

        _hangarDirectBtn = hObj.AddComponent<Button>();
        _hangarDirectBtn.targetGraphic = bg;
        _hangarDirectBtn.onClick.AddListener(() =>
        {
            GameManager.Instance?.GoToMainMenu();
            MainMenuManager menu = FindAnyObjectByType<MainMenuManager>();
            menu?.OpenHangarPanel();
        });

        GameObject txtObj = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(hObj.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
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
