using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 7 Günlük Giriş Ödülü Takvimi (Daily Login Streak System & UI):
/// - Oyuncunun ardışık günlerde oyuna giriş serisini (1..7 Gün) takip eder ve Supabase bulutuyla senkronize saklar.
/// - Her gün artan Altın, EXP ve Sandık Anahtarı (Bronz / Gümüş / 7. Gün Altın Sandık Anahtarı) ödülleri verir.
/// - Giriş yapıldığında bugünün ödülü alınmadıysa otomatik açılır; ayrıca Ana Menü "ETKİNLİKLER" sekmesinden her zaman açılabilir.
/// </summary>
public class DailyLoginStreakUI : MonoBehaviour
{
    public static DailyLoginStreakUI Instance { get; private set; }

    public const string PREF_STREAK_DAY = "DailyLogin_StreakDay";
    public const string PREF_LAST_CLAIM_DATE = "DailyLogin_LastClaimDate";
    public const string PREF_TOTAL_LOGINS = "DailyLogin_TotalLogins";

    public struct DayRewardDef
    {
        public int day;
        public string title;
        public string subtitle;
        public int gold;
        public int exp;
        public bool hasKey;
        public HangarManager.KeyType keyType;
        public int keyCount;
        public Color accentColor;
    }

    public static readonly DayRewardDef[] Rewards = new DayRewardDef[]
    {
        new DayRewardDef { day = 1, title = "500 ALTIN",          subtitle = "+150 EXP İkmal",          gold = 500,  exp = 150,  hasKey = false, keyType = HangarManager.KeyType.Bronze, keyCount = 0, accentColor = new Color(0.25f, 0.85f, 1.00f) },
        new DayRewardDef { day = 2, title = "1x BRONZ ANAHTAR",   subtitle = "+750 ALTIN",              gold = 750,  exp = 200,  hasKey = true,  keyType = HangarManager.KeyType.Bronze, keyCount = 1, accentColor = new Color(0.88f, 0.56f, 0.28f) },
        new DayRewardDef { day = 3, title = "1.200 ALTIN",        subtitle = "+300 EXP Filo Fonu",      gold = 1200, exp = 300,  hasKey = false, keyType = HangarManager.KeyType.Bronze, keyCount = 0, accentColor = new Color(0.25f, 0.85f, 1.00f) },
        new DayRewardDef { day = 4, title = "1x GÜMÜŞ ANAHTAR",   subtitle = "+1.500 ALTIN",            gold = 1500, exp = 400,  hasKey = true,  keyType = HangarManager.KeyType.Silver, keyCount = 1, accentColor = new Color(0.75f, 0.84f, 0.96f) },
        new DayRewardDef { day = 5, title = "2.200 ALTIN",        subtitle = "+500 EXP Elit Prim",      gold = 2200, exp = 500,  hasKey = false, keyType = HangarManager.KeyType.Silver, keyCount = 0, accentColor = new Color(0.25f, 0.85f, 1.00f) },
        new DayRewardDef { day = 6, title = "2x GÜMÜŞ ANAHTAR",   subtitle = "+3.000 ALTIN",            gold = 3000, exp = 650,  hasKey = true,  keyType = HangarManager.KeyType.Silver, keyCount = 2, accentColor = new Color(0.75f, 0.84f, 0.96f) },
        new DayRewardDef { day = 7, title = "1x ALTIN ANAHTAR",   subtitle = "+5.000 ALTIN • +1.000 EXP", gold = 5000, exp = 1000, hasKey = true,  keyType = HangarManager.KeyType.Gold,   keyCount = 1, accentColor = new Color(1.00f, 0.82f, 0.20f) }
    };

    private GameObject _modalRoot;
    private TextMeshProUGUI _streakHeaderTMP;
    private TextMeshProUGUI _statusBannerTMP;
    private Button _claimButton;
    private TextMeshProUGUI _claimBtnTMP;

    private readonly List<Image> _cardBgs = new List<Image>();
    private readonly List<TextMeshProUGUI> _cardStatusTMPs = new List<TextMeshProUGUI>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static DailyLoginStreakUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("DailyLoginStreakUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("DailyLoginStreakUI", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        Instance = go.GetComponent<DailyLoginStreakUI>();
        if (Instance == null) Instance = go.AddComponent<DailyLoginStreakUI>();
        return Instance;
    }

    private static string GetTodayDateKey()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd");
    }

    private static string GetYesterdayDateKey()
    {
        return DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Bugün ödül alınabilir mi ve mevcut hedef gün (1..7) kaç?
    /// </summary>
    public static void EvaluateStreakState(out int activeDay1To7, out bool canClaimToday)
    {
        string today = GetTodayDateKey();
        string yesterday = GetYesterdayDateKey();
        string lastClaim = ProfileManager.GetString(PREF_LAST_CLAIM_DATE, "");
        int savedStreak = Mathf.Clamp(ProfileManager.GetInt(PREF_STREAK_DAY, 0), 0, 7);

        if (lastClaim == today)
        {
            // Bugün zaten alındı
            activeDay1To7 = Mathf.Max(1, savedStreak);
            canClaimToday = false;
            return;
        }

        if (lastClaim == yesterday && savedStreak > 0)
        {
            // Seri kesintisiz devam ediyor (7. günden sonra 1. güne döner)
            activeDay1To7 = (savedStreak % 7) + 1;
            canClaimToday = true;
            return;
        }

        // İlk kez giriş veya seri kırılmış -> 1. Gün
        activeDay1To7 = 1;
        canClaimToday = true;
    }

    public static bool CanClaimToday()
    {
        EvaluateStreakState(out _, out bool canClaim);
        return canClaim;
    }

    public void CheckAndShowAutoPopupOnLogin()
    {
        if (CanClaimToday())
        {
            OpenCalendarModal();
        }
    }

    public void OpenCalendarModal()
    {
        EnsureModalStructure();
        if (_modalRoot != null)
        {
            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
        }
        RefreshCalendarVisuals();
    }

    public void CloseModal()
    {
        if (_modalRoot != null)
            _modalRoot.SetActive(false);
    }

    private void OnClaimTodayClicked()
    {
        EvaluateStreakState(out int targetDay, out bool canClaim);
        if (!canClaim)
        {
            AudioManager.Instance?.PlayButtonClick();
            return;
        }

        DayRewardDef reward = Rewards[Mathf.Clamp(targetDay - 1, 0, Rewards.Length - 1)];

        // Ödülleri hesaba ekle
        HangarManager hm = HangarManager.EnsureInstance();
        if (hm != null)
        {
            if (reward.gold > 0) hm.AddGold(reward.gold);
            if (reward.hasKey && reward.keyCount > 0) hm.AddKey(reward.keyType, reward.keyCount);
        }

        if (reward.exp > 0)
        {
            PlayerProfileManager.EnsureInstance()?.AddExp(reward.exp, $"{targetDay}. GÜN GİRİŞ ÖDÜLÜ");
        }

        // Seri ve tarih bilgisini kaydet (buluta otomatik senkronize olur)
        ProfileManager.SetInt(PREF_STREAK_DAY, targetDay);
        ProfileManager.SetString(PREF_LAST_CLAIM_DATE, GetTodayDateKey());
        ProfileManager.SetInt(PREF_TOTAL_LOGINS, ProfileManager.GetInt(PREF_TOTAL_LOGINS, 0) + 1);
        ProfileManager.Save();

        AudioManager.Instance?.PlayRewardFanfare();
        PlayerProfileUI.Instance?.RefreshBadgeVisuals();
        DailyOpsManager.Instance?.CheckDailyRollover();
        MainMenuManager.Instance?.RefreshDailyOpsButtonVisual();

        RefreshCalendarVisuals();

        if (_statusBannerTMP != null)
        {
            _statusBannerTMP.text = $"<color=#2ECC71><b>[+] {targetDay}. GÜN ÖDÜLÜ ENVANTERE EKLENDİ: {reward.title} ({reward.subtitle})!</b></color>";
        }
    }

    private void RefreshCalendarVisuals()
    {
        EvaluateStreakState(out int activeDay, out bool canClaim);
        int totalLogins = ProfileManager.GetInt(PREF_TOTAL_LOGINS, 0);

        if (_streakHeaderTMP != null)
        {
            _streakHeaderTMP.text = $"AKTİF GİRİŞ SERİSİ: <color=#00E5FF><b>GÜN {activeDay} / 7</b></color>   •   TOPLAM GİRİŞ: <color=#FDE047><b>{totalLogins} GÜN</b></color>";
        }

        for (int i = 0; i < Rewards.Length && i < _cardBgs.Count; i++)
        {
            int dayNum = i + 1;
            Image cardBg = _cardBgs[i];
            TextMeshProUGUI statusTMP = _cardStatusTMPs[i];
            bool isDay7 = (dayNum == 7);

            bool isClaimed = (dayNum < activeDay) || (dayNum == activeDay && !canClaim);
            bool isTodayReady = (dayNum == activeDay && canClaim);

            if (cardBg != null)
            {
                if (isTodayReady)
                {
                    cardBg.sprite = MainMenuManager.GetRoundedBoxSprite(
                        isDay7 ? "streak_d7_ready" : "streak_card_ready",
                        isDay7 ? new Color(0.18f, 0.14f, 0.04f, 0.98f) : new Color(0.07f, 0.17f, 0.26f, 0.98f),
                        isDay7 ? new Color(1f, 0.85f, 0.20f, 1f) : new Color(0f, 0.92f, 1f, 1f),
                        12, 2);
                }
                else if (isClaimed)
                {
                    cardBg.sprite = MainMenuManager.GetRoundedBoxSprite(
                        "streak_card_claimed",
                        new Color(0.04f, 0.13f, 0.09f, 0.92f),
                        new Color(0.18f, 0.78f, 0.45f, 0.90f),
                        12, 2);
                }
                else
                {
                    cardBg.sprite = MainMenuManager.GetRoundedBoxSprite(
                        isDay7 ? "streak_d7_locked" : "streak_card_locked",
                        isDay7 ? new Color(0.11f, 0.09f, 0.04f, 0.92f) : new Color(0.05f, 0.09f, 0.14f, 0.90f),
                        isDay7 ? new Color(0.65f, 0.52f, 0.16f, 0.85f) : new Color(0.15f, 0.24f, 0.34f, 0.85f),
                        12, 1);
                }
                cardBg.type = Image.Type.Sliced;
            }

            if (statusTMP != null)
            {
                if (isClaimed)
                    statusTMP.text = "<color=#2ECC71><b>[ ALINDI ]</b></color>";
                else if (isTodayReady)
                    statusTMP.text = "<color=#00E5FF><b>[ BUGÜN HAZIR ]</b></color>";
                else
                    statusTMP.text = $"<color=#64748B><b>[ {dayNum}. GÜN ]</b></color>";
            }
        }

        if (_claimButton != null && _claimBtnTMP != null)
        {
            _claimButton.interactable = canClaim;
            Image cImg = _claimButton.GetComponent<Image>();
            if (canClaim)
            {
                DayRewardDef r = Rewards[Mathf.Clamp(activeDay - 1, 0, Rewards.Length - 1)];
                _claimBtnTMP.text = $"<b>{activeDay}. GÜN ÖDÜLÜNÜ TOPLA  ({r.title})</b>";
                _claimBtnTMP.color = new Color(0.03f, 0.08f, 0.12f, 1f);
                if (cImg != null)
                {
                    cImg.sprite = MainMenuManager.GetRoundedBoxSprite("streak_claim_active", new Color(0.24f, 0.88f, 0.96f, 1f), new Color(0.55f, 0.96f, 1f, 1f), 10, 1);
                }
                if (_statusBannerTMP != null)
                    _statusBannerTMP.text = $"<color=#8FA8BD>Seriyi bozmadan her gün giriş yaparak <b>7. Gün Altın Sandık Anahtarı + 5.000 Altın</b> büyük ödülüne ulaşın!</color>";
            }
            else
            {
                _claimBtnTMP.text = "<b>BUGÜNÜN ÖDÜLÜ ALINDI (YARIN TEKRAR GEL)</b>";
                _claimBtnTMP.color = new Color(0.75f, 0.85f, 0.92f, 0.9f);
                if (cImg != null)
                {
                    cImg.sprite = MainMenuManager.GetRoundedBoxSprite("streak_claim_done", new Color(0.08f, 0.14f, 0.20f, 0.95f), new Color(0.20f, 0.34f, 0.46f, 0.9f), 10, 1);
                }
                if (_statusBannerTMP != null)
                    _statusBannerTMP.text = "<color=#2ECC71>Bugünkü ikmal ödülünüzü aldınız. Serinin devamı için yarın tekrar giriş yapın!</color>";
            }
        }
    }

    private void EnsureModalStructure()
    {
        if (_modalRoot != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        _modalRoot = new GameObject("DailyLoginStreakModalRoot", typeof(RectTransform));
        _modalRoot.transform.SetParent(canvas.transform, false);
        MainMenuManager.MakeFullScreenRect(_modalRoot.GetComponent<RectTransform>());

        Image bg = _modalRoot.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.04f, 0.08f, 0.93f);
        bg.raycastTarget = true;

        // Ana Kart (1180 x 700)
        GameObject boxObj = new GameObject("StreakCalendarCard", typeof(RectTransform));
        boxObj.transform.SetParent(_modalRoot.transform, false);
        RectTransform bRt = boxObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.5f, 0.5f);
        bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.anchoredPosition = Vector2.zero;
        bRt.sizeDelta = new Vector2(1180f, 700f);

        Image bBg = boxObj.AddComponent<Image>();
        bBg.sprite = MainMenuManager.GetRoundedBoxSprite("streak_modal_bg", new Color(0.05f, 0.09f, 0.14f, 0.98f), new Color(0.16f, 0.28f, 0.40f, 0.98f), 16, 2);
        bBg.type = Image.Type.Sliced;
        bBg.color = Color.white;

        // Başlıklar
        CreateText(boxObj.transform, "HeaderTitle", "<b>7 GÜNLÜK GİRİŞ ÖDÜLÜ TAKVİMİ</b>", new Vector2(0f, 305f), new Vector2(1080f, 44f), 32f, Color.white, TextAlignmentOptions.Center, 2.4f);
        _streakHeaderTMP = CreateText(boxObj.transform, "StreakSubHeader", "", new Vector2(0f, 264f), new Vector2(1080f, 28f), 17f, new Color(0.75f, 0.85f, 0.95f), TextAlignmentOptions.Center, 1.4f);

        _cardBgs.Clear();
        _cardStatusTMPs.Clear();

        // Gün 1..6: Sol tarafta 3x2 Grid (her kart 238 x 194, aralık 18)
        float startX = -435f;
        float stepX = 254f;
        float row1Y = 122f;
        float row2Y = -88f;

        for (int i = 0; i < 6; i++)
        {
            int col = i % 3;
            int row = i / 3;
            Vector2 pos = new Vector2(startX + col * stepX, row == 0 ? row1Y : row2Y);
            BuildDayCard(boxObj.transform, Rewards[i], pos, new Vector2(238f, 194f), isGrandFinale: false);
        }

        // Gün 7: Sağ tarafta Büyük Final Kartı (340 x 404)
        BuildDayCard(boxObj.transform, Rewards[6], new Vector2(375f, 17f), new Vector2(340f, 404f), isGrandFinale: true);

        // Alt Bilgi Mesajı
        _statusBannerTMP = CreateText(boxObj.transform, "StatusBanner", "", new Vector2(0f, -222f), new Vector2(1080f, 28f), 16.5f, new Color(0.75f, 0.85f, 0.95f), TextAlignmentOptions.Center, 0.8f);

        // Ödülü Topla Butonu
        GameObject claimObj = new GameObject("ClaimTodayBtn", typeof(RectTransform));
        claimObj.transform.SetParent(boxObj.transform, false);
        RectTransform cRt = claimObj.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(-115f, -285f);
        cRt.sizeDelta = new Vector2(640f, 62f);

        Image cImg = claimObj.AddComponent<Image>();
        cImg.sprite = MainMenuManager.GetRoundedBoxSprite("streak_claim_active", new Color(0.24f, 0.88f, 0.96f, 1f), new Color(0.55f, 0.96f, 1f, 1f), 10, 1);
        cImg.type = Image.Type.Sliced;

        _claimButton = claimObj.AddComponent<Button>();
        _claimButton.targetGraphic = cImg;
        _claimButton.onClick.AddListener(OnClaimTodayClicked);

        _claimBtnTMP = CreateText(claimObj.transform, "Text", "<b>ÖDÜLÜ TOPLA</b>", Vector2.zero, new Vector2(620f, 56f), 21f, new Color(0.03f, 0.08f, 0.12f, 1f), TextAlignmentOptions.Center, 1.4f);

        // Kapat Butonu
        GameObject closeObj = new GameObject("CloseCalendarBtn", typeof(RectTransform));
        closeObj.transform.SetParent(boxObj.transform, false);
        RectTransform clRt = closeObj.GetComponent<RectTransform>();
        clRt.anchorMin = new Vector2(0.5f, 0.5f);
        clRt.anchorMax = new Vector2(0.5f, 0.5f);
        clRt.pivot = new Vector2(0.5f, 0.5f);
        clRt.anchoredPosition = new Vector2(355f, -285f);
        clRt.sizeDelta = new Vector2(240f, 62f);

        Image clImg = closeObj.AddComponent<Image>();
        clImg.sprite = MainMenuManager.GetRoundedBoxSprite("streak_close_btn", new Color(0.08f, 0.13f, 0.20f, 0.96f), new Color(0.20f, 0.32f, 0.44f, 0.95f), 10, 1);
        clImg.type = Image.Type.Sliced;

        Button closeBtn = closeObj.AddComponent<Button>();
        closeBtn.targetGraphic = clImg;
        closeBtn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayButtonClick();
            CloseModal();
        });

        CreateText(closeObj.transform, "Text", "<b>« KAPAT</b>", Vector2.zero, new Vector2(220f, 56f), 20f, new Color(0.85f, 0.90f, 0.96f), TextAlignmentOptions.Center, 1.2f);
    }

    private void BuildDayCard(Transform parent, DayRewardDef def, Vector2 pos, Vector2 size, bool isGrandFinale)
    {
        GameObject cardObj = new GameObject($"DayCard_{def.day}", typeof(RectTransform));
        cardObj.transform.SetParent(parent, false);
        RectTransform rt = cardObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = cardObj.AddComponent<Image>();
        bg.sprite = MainMenuManager.GetRoundedBoxSprite("streak_card_locked", new Color(0.05f, 0.09f, 0.14f, 0.90f), new Color(0.15f, 0.24f, 0.34f, 0.85f), 12, 1);
        bg.type = Image.Type.Sliced;
        _cardBgs.Add(bg);

        // Üst Gün Başlığı
        string dayHeader = isGrandFinale ? "7. GÜN • BÜYÜK FİNAL ÖDÜLÜ" : $"{def.day}. GÜN İKMALİ";
        Color hdrCol = isGrandFinale ? new Color(1f, 0.85f, 0.25f) : new Color(0.58f, 0.72f, 0.84f);
        CreateText(cardObj.transform, "DayHeader", $"<b>{dayHeader}</b>", new Vector2(0f, size.y * 0.5f - 26f), new Vector2(size.x - 16f, 26f), isGrandFinale ? 17.5f : 15f, hdrCol, TextAlignmentOptions.Center, 1.0f);

        // Orta Ödül Görseli / Rozeti (3D Sandık veya Altın Görseli + Etiket)
        string rewardSpriteName = def.hasKey
            ? (def.keyType == HangarManager.KeyType.Gold ? "crate_3d_2" : def.keyType == HangarManager.KeyType.Silver ? "crate_3d_1" : "crate_3d_0")
            : "loot_gold";
        Sprite iconSp = Resources.Load<Sprite>(rewardSpriteName);

        if (iconSp != null)
        {
            GameObject iconObj = new GameObject("RewardSpriteIcon", typeof(RectTransform));
            iconObj.transform.SetParent(cardObj.transform, false);
            RectTransform iRt = iconObj.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0.5f, 0.5f);
            iRt.anchorMax = new Vector2(0.5f, 0.5f);
            iRt.pivot = new Vector2(0.5f, 0.5f);
            iRt.anchoredPosition = new Vector2(0f, isGrandFinale ? 36f : 16f);
            iRt.sizeDelta = isGrandFinale ? new Vector2(150f, 150f) : new Vector2(68f, 68f);

            Image iImg = iconObj.AddComponent<Image>();
            iImg.sprite = iconSp;
            iImg.preserveAspect = true;
            iImg.raycastTarget = false;
        }
        else
        {
            GameObject badgeObj = new GameObject("RewardBadge", typeof(RectTransform));
            badgeObj.transform.SetParent(cardObj.transform, false);
            RectTransform bRt = badgeObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.5f, 0.5f);
            bRt.anchorMax = new Vector2(0.5f, 0.5f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.anchoredPosition = new Vector2(0f, isGrandFinale ? 28f : 14f);
            bRt.sizeDelta = isGrandFinale ? new Vector2(170f, 96f) : new Vector2(124f, 48f);

            Image bImg = badgeObj.AddComponent<Image>();
            bImg.sprite = MainMenuManager.GetRoundedBoxSprite(
                $"streak_badge_{def.day}",
                new Color(def.accentColor.r * 0.18f, def.accentColor.g * 0.18f, def.accentColor.b * 0.18f, 0.95f),
                def.accentColor,
                10, 1);
            bImg.type = Image.Type.Sliced;

            string badgeSymbol = def.hasKey
                ? (def.keyType == HangarManager.KeyType.Gold ? "ALTIN\nSANDIK" : def.keyType == HangarManager.KeyType.Silver ? "GÜMÜŞ\nSANDIK" : "BRONZ\nSANDIK")
                : $"+{def.gold:N0} G";
            CreateText(badgeObj.transform, "BadgeSymbol", $"<b>{badgeSymbol}</b>", Vector2.zero, bRt.sizeDelta, isGrandFinale ? 22f : 16f, def.accentColor, TextAlignmentOptions.Center, 1.1f);
        }

        // Ödül Başlığı ve Alt Başlığı
        CreateText(cardObj.transform, "RewardTitle", $"<b>{def.title}</b>", new Vector2(0f, isGrandFinale ? -68f : -32f), new Vector2(size.x - 16f, 28f), isGrandFinale ? 24f : 18.5f, Color.white, TextAlignmentOptions.Center, 1.0f);
        CreateText(cardObj.transform, "RewardSub", def.subtitle, new Vector2(0f, isGrandFinale ? -102f : -56f), new Vector2(size.x - 16f, 24f), isGrandFinale ? 17.5f : 14.5f, new Color(0.68f, 0.80f, 0.90f), TextAlignmentOptions.Center, 0.8f);

        // Alt Durum Etiketi (ALINDI / BUGÜN HAZIR / KİLİTLİ)
        TextMeshProUGUI statusTMP = CreateText(cardObj.transform, "StatusText", "", new Vector2(0f, -size.y * 0.5f + 22f), new Vector2(size.x - 16f, 24f), isGrandFinale ? 17f : 14.5f, Color.white, TextAlignmentOptions.Center, 1.1f);
        _cardStatusTMPs.Add(statusTMP);
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align, float spacing)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = MainMenuManager.GetTacticalFont();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.characterSpacing = spacing;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }
}
