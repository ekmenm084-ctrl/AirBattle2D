using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 1:1 Modlara Göre Rekorlar & Gerçek Veritabanı (Supabase) Liderlik Tablosu Arayüzü:
/// - Rastgele örnek profil içermez; yalnızca projeye entegre Supabase veritabanındaki (public.leaderboard)
///   gerçek kullanıcı adlarını ve kayıtlı gerçek oyuncu profillerini listeler.
/// - 4 Sütun: RÜTBE (01..05) | OYUNCU | AKTİF OYUN MODU | EN YÜKSEK AŞAMA
/// - Aktif oyuncunun satırı cyan çerçeve, askeri rütbe arması ve "[AKTİF] kullanıcı_adı (Sen)" ile vurgulanır.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    public static LeaderboardUI Instance { get; private set; }

    private GameObject _panelRoot;
    private LeaderboardManager.BoardMode _currentMode = LeaderboardManager.BoardMode.Endless;
    private Action _onCloseCallback;

    private Transform _rowsContainer;
    private readonly List<GameObject> _spawnedRows = new List<GameObject>();

    private static Sprite _cachedBgSprite;
    private static Sprite _cachedActiveRowSprite;
    private static Sprite _cachedChevronSprite;

    private static readonly float[] ROW_CENTER_Y_1080 = new float[]
    {
        161.0f,
        54.0f,
        -53.0f,
        -160.0f,
        -267.0f
    };

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static LeaderboardUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("LeaderboardUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("LeaderboardUI", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        Instance = go.GetComponent<LeaderboardUI>();
        if (Instance == null) Instance = go.AddComponent<LeaderboardUI>();
        return Instance;
    }

    private static Sprite LoadSpriteFromDiskOrResources(string fileName, string resourceName, ref Sprite cache)
    {
        if (cache != null) return cache;

        string path = Path.Combine(Application.dataPath, "Sprites/" + fileName);
        if (File.Exists(path))
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                cache = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return cache;
            }
        }

        Sprite res = Resources.Load<Sprite>(resourceName);
        if (res != null)
        {
            cache = res;
            return res;
        }

        Texture2D resTex = Resources.Load<Texture2D>(resourceName);
        if (resTex != null)
        {
            cache = Sprite.Create(resTex, new Rect(0, 0, resTex.width, resTex.height), new Vector2(0.5f, 0.5f), 100f);
            return cache;
        }

        return null;
    }

    public void OpenLeaderboard(LeaderboardManager.BoardMode mode = LeaderboardManager.BoardMode.Endless, Action onClose = null)
    {
        _currentMode = mode;
        _onCloseCallback = onClose;

        EnsurePanelStructure();

        if (_panelRoot != null)
        {
            _panelRoot.SetActive(true);
            _panelRoot.transform.SetAsLastSibling();
            PlayerProfileUI.EnsureUI()?.ShowMenuStatusBar(showGoldBadge: true);
        }

        RefreshScores();
    }

    public void CloseLeaderboard()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        var cb = _onCloseCallback;
        _onCloseCallback = null;
        cb?.Invoke();
    }

    public void CloseDirectly()
    {
        if (this == null) return;
        _onCloseCallback = null;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void EnsurePanelStructure()
    {
        if (_panelRoot != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        _panelRoot = new GameObject("LeaderboardPanelRoot", typeof(RectTransform));
        _panelRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = _panelRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image bg = _panelRoot.AddComponent<Image>();
        Sprite bgSpr = LoadSpriteFromDiskOrResources("leaderboard_background.jpg", "leaderboard_background", ref _cachedBgSprite);
        if (bgSpr != null)
        {
            bg.sprite = bgSpr;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.color = Color.white;
        }
        else
        {
            bg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);
        }

        // Satırlar Konteyneri (Tam ekran 1920x1080 merkez koordinat sistemi)
        GameObject rowsObj = new GameObject("RowsContainer", typeof(RectTransform));
        rowsObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform rowsRt = rowsObj.GetComponent<RectTransform>();
        rowsRt.anchorMin = Vector2.zero;
        rowsRt.anchorMax = Vector2.one;
        rowsRt.offsetMin = Vector2.zero;
        rowsRt.offsetMax = Vector2.zero;
        _rowsContainer = rowsObj.transform;

        // Sol Alt Buton: << ANA MENÜYE DÖN (1920x1080 exact: x = 54..412, y = 962..1034 -> center = (-727.0, -458.0), size = (358, 72))
        HangarUI.FormatPixelExactButton(
            "BackToMainMenuBtn",
            _panelRoot.transform,
            new Vector2(-727.0f, -458.0f),
            new Vector2(358.0f, 72.0f),
            new Color(0.25f, 0.65f, 1.0f, 0.18f),
            new Color(0.20f, 0.55f, 0.95f, 0.35f),
            CloseLeaderboard);

        // Sağ Alt Buton: [>>] SONRAKİ GÖREV (1920x1080 exact: x = 1510..1866, y = 962..1034 -> center = (728.0, -458.0), size = (356, 72))
        HangarUI.FormatPixelExactButton(
            "NextMissionBtn",
            _panelRoot.transform,
            new Vector2(728.0f, -458.0f),
            new Vector2(356.0f, 72.0f),
            new Color(0.20f, 0.90f, 0.45f, 0.18f),
            new Color(0.15f, 0.75f, 0.35f, 0.35f),
            OnNextMissionClicked);
    }

    private void OnNextMissionClicked()
    {
        CloseDirectly();
        MainMenuManager menu = FindAnyObjectByType<MainMenuManager>();
        if (menu != null)
        {
            menu.OpenModesPanel();
        }
    }

    public void RefreshScores()
    {
        LeaderboardManager lm = LeaderboardManager.EnsureInstance();
        if (lm == null) return;

        lm.FetchAllModesLeaderboard((entries, isOnline) =>
        {
            DisplayLeaderboardEntries(entries);
        });
    }

    private void DisplayLeaderboardEntries(List<LeaderboardManager.LeaderboardEntry> entries)
    {
        foreach (var r in _spawnedRows)
        {
            if (r != null) Destroy(r);
        }
        _spawnedRows.Clear();

        if (entries == null || entries.Count == 0)
            return;

        int maxRows = Mathf.Min(ROW_CENTER_Y_1080.Length, entries.Count);
        for (int i = 0; i < maxRows; i++)
        {
            GameObject rowObj = Create1To1RowElement(i, entries[i], ROW_CENTER_Y_1080[i]);
            _spawnedRows.Add(rowObj);
        }
    }

    private GameObject Create1To1RowElement(int slotIndex, LeaderboardManager.LeaderboardEntry entry, float centerY1080)
    {
        GameObject rowObj = new GameObject($"Row_{slotIndex + 1}", typeof(RectTransform));
        rowObj.transform.SetParent(_rowsContainer, false);

        RectTransform rt = rowObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, centerY1080);
        rt.sizeDelta = new Vector2(1416f, 92f);

        // Aktif oyuncunun satırıysa cyan parlak çerçeve ve askeri rütbe armasını göster
        if (entry.isPlayer)
        {
            Sprite activeSpr = LoadSpriteFromDiskOrResources("lb_row_active_frame.png", "lb_row_active_frame", ref _cachedActiveRowSprite);
            if (activeSpr != null)
            {
                Image activeBg = rowObj.AddComponent<Image>();
                activeBg.sprite = activeSpr;
                activeBg.color = Color.white;
                activeBg.raycastTarget = false;
            }

            Sprite chevSpr = LoadSpriteFromDiskOrResources("lb_chevron_icon.png", "lb_chevron_icon", ref _cachedChevronSprite);
            if (chevSpr != null)
            {
                GameObject chevObj = new GameObject("ChevronBadge", typeof(RectTransform));
                chevObj.transform.SetParent(rowObj.transform, false);
                RectTransform crt = chevObj.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.5f, 0.5f);
                crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2(-660.0f, 0f);
                crt.sizeDelta = new Vector2(52f, 58f);

                Image cImg = chevObj.AddComponent<Image>();
                cImg.sprite = chevSpr;
                cImg.preserveAspect = true;
                cImg.color = Color.white;
                cImg.raycastTarget = false;
            }
        }

        // 1. Sütun: RÜTBE (01, 02, 03, 04, 05) -> centerX = -590.0f
        string rankStr = $"{entry.rank:00}";
        CreateCellText(
            rowObj.transform,
            "RankCell",
            rankStr,
            new Vector2(-590.0f, 0f),
            new Vector2(90f, 70f),
            TextAlignmentOptions.Center,
            29f,
            entry.isPlayer ? new Color(0.78f, 0.88f, 0.96f) : new Color(0.60f, 0.68f, 0.76f));

        // 2. Sütun: OYUNCU (Veritabanından gelen gerçek kullanıcı adı) -> x = 478..980 -> centerX = -231.0f, width = 502
        string playerDisplay = entry.isPlayer
            ? $"<color=#4ADE80>[AKTİF]</color> <color=#FFFFFF>{entry.playerName} (Sen)</color>"
            : $"<color=#CBD5E1>{entry.playerName}</color>";

        CreateCellText(
            rowObj.transform,
            "PlayerCell",
            playerDisplay,
            new Vector2(-231.0f, 0f),
            new Vector2(502f, 70f),
            TextAlignmentOptions.Left,
            28f,
            Color.white);

        // 3. Sütun: AKTİF OYUN MODU -> x = 990..1330 -> centerX = +200.0f, width = 340
        string modeDisp = !string.IsNullOrEmpty(entry.modeDisplayName) ? entry.modeDisplayName : "HAREKAT MODU";
        CreateCellText(
            rowObj.transform,
            "ModeCell",
            modeDisp,
            new Vector2(200.0f, 0f),
            new Vector2(340f, 70f),
            TextAlignmentOptions.Center,
            27f,
            entry.isPlayer ? new Color(0.85f, 0.92f, 0.98f) : new Color(0.74f, 0.80f, 0.86f));

        // 4. Sütun: EN YÜKSEK AŞAMA -> x = 1355..1645 -> centerX = +540.0f, width = 290
        string stageDisp = !string.IsNullOrEmpty(entry.stageDisplayText) ? entry.stageDisplayText : entry.formattedScore;
        CreateCellText(
            rowObj.transform,
            "StageCell",
            stageDisp,
            new Vector2(540.0f, 0f),
            new Vector2(290f, 70f),
            TextAlignmentOptions.Center,
            28f,
            entry.isPlayer ? Color.white : new Color(0.84f, 0.89f, 0.94f));

        return rowObj;
    }

    private static void CreateCellText(
        Transform parent,
        string name,
        string text,
        Vector2 anchoredPos,
        Vector2 size,
        TextAlignmentOptions align,
        float fontSize,
        Color color)
    {
        GameObject cellObj = new GameObject(name, typeof(RectTransform));
        cellObj.transform.SetParent(parent, false);

        RectTransform rt = cellObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = cellObj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Normal;
        tmp.characterSpacing = 1.0f;
        tmp.alignment = align;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
    }
}
