using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tam Sayfa Askeri Envanter ve Donanım Deposu Arayüzü:
/// Sandıklardan ve savaşlardan kazanılan tüm As çipleri, drone modelleri ve aktif filo muharebe gücünü gösterir.
/// Önceki menüyü tamamen kapatır, yeni bir sayfada açılır ve sol en altta GERİ DÖN butonu içerir.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    private GameObject _panelRoot;
    private TextMeshProUGUI _goldTMP;
    private TextMeshProUGUI _chipsContentTMP;
    private TextMeshProUGUI _dronesContentTMP;
    private TextMeshProUGUI _combatContentTMP;

    private Action _onBackCallback;

    private static RectTransform GetRect(GameObject go)
    {
        if (go == null) return null;
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        return rt;
    }

    public static void MakeFullScreenRect(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static InventoryUI EnsureUI()
    {
        if (Instance != null && Instance.gameObject != null) return Instance;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("InventoryUI");
        GameObject go = existing != null ? existing.gameObject : new GameObject("InventoryUI");
        go.transform.SetParent(canvas.transform, false);
        MakeFullScreenRect(GetRect(go));
        Instance = go.GetComponent<InventoryUI>();
        if (Instance == null) Instance = go.AddComponent<InventoryUI>();
        if (Instance._panelRoot == null) Instance.BuildUI();
        return Instance;
    }

    public void OpenInventory(Action onBack = null)
    {
        if (onBack != null) _onBackCallback = onBack;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        MakeFullScreenRect(GetRect(gameObject));

        if (_panelRoot == null)
            BuildUI();

        MakeFullScreenRect(GetRect(_panelRoot));
        _panelRoot.SetActive(true);
        RefreshVisuals();
    }

    public void CloseInventory()
    {
        CloseDirectly();

        if (_onBackCallback != null)
        {
            Action callback = _onBackCallback;
            _onBackCallback = null;
            callback.Invoke();
        }
        else
        {
            MainMenuManager.Instance?.OpenMainPanel();
            if (MainMenuManager.Instance == null)
            {
                MainMenuManager menu = FindObjectOfType<MainMenuManager>();
                menu?.OpenMainPanel();
            }
        }
    }

    public void CloseDirectly()
    {
        if (this == null) return;
        try
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
            if (gameObject != null)
                gameObject.SetActive(false);
        }
        catch { }
    }

    public void RefreshVisuals()
    {
        HangarManager hm = HangarManager.EnsureInstance();
        hm.LoadHangarData();

        if (_goldTMP != null)
            _goldTMP.text = $"ALTIN: {hm.gold:N0} G";

        // 1. As Ganimet Çipleri Kartı
        if (_chipsContentTMP != null)
        {
            _chipsContentTMP.text =
                $"<b>[KALICI AS GANİMET ÇİPLERİ]</b>\n\n" +
                $"• Taktik Mermi Çipleri: <color=#00E5FF>+{hm.chipBonusDamage:0.0} Hasar</color>\n" +
                $"  <size=78%><color=#8AB4F8>Tüm uçakların namlu gücüne kalıcı eklenir.</color></size>\n\n" +
                $"• Kompozit Zırh Çipleri: <color=#00E5FF>+{hm.chipBonusHealth:0} Can (HP)</color>\n" +
                $"  <size=78%><color=#8AB4F8>Tüm filonun gövde dayanıklılığına eklenir.</color></size>\n\n" +
                $"• Toplam Açılan İkmal Sandığı: <color=#FFE082>{hm.totalCratesOpened} Adet</color>\n\n" +
                $"• Donanım Durumu: <color=#2ECC71>AKTİF VE ENTEGRE</color>\n\n" +
                $"<size=80%><color=#F1C40F><i>İpucu: İkmal Sandıkları açarak sınırsız kalıcı hasar ve zırh çipi toplayabilirsiniz!</i></color></size>";
        }

        // 2. Destek Drone Hangarı Kartı
        if (_dronesContentTMP != null)
        {
            string GetDroneStat(DroneCompanion.DroneType dt)
            {
                if (!hm.IsDroneUnlocked(dt))
                {
                    int reqLvl = HangarManager.GetDroneRequiredLevel(dt);
                    return $"<color=#888888>KİLİTLİ (SEVİYE {reqLvl})</color>";
                }
                float hp = hm.GetDroneHealth(dt);
                float maxHp = HangarManager.GetDroneMaxHealth(dt);
                string slot = (hm.leftDroneType == dt) ? "Sol Kanatta" : (hm.rightDroneType == dt) ? "Sağ Kanatta" : "Hangarda";
                return $"<color=#00E5FF>{hp:0}/{maxHp:0} HP</color> ({slot})";
            }

            _dronesContentTMP.text =
                $"<b>[DESTEK DRONE ENVANTERİ]</b>\n\n" +
                $"• MK-1 İzci: {GetDroneStat(DroneCompanion.DroneType.MK1_Scout)}\n" +
                $"• MK-2 Avcı: {GetDroneStat(DroneCompanion.DroneType.MK2_Interceptor)}\n" +
                $"• MK-3 Taarruz: {GetDroneStat(DroneCompanion.DroneType.MK3_Striker)}\n" +
                $"• MK-4 Valkyrie: {GetDroneStat(DroneCompanion.DroneType.MK4_Missile)}\n" +
                $"• MK-5 Titan Aegis: {GetDroneStat(DroneCompanion.DroneType.MK5_TitanAegis)}\n\n" +
                $"• Aktif Kanatlar:\n" +
                $"  Sol: <color=#00E5FF>{HangarManager.GetDroneDisplayName(hm.leftDroneType)}</color> | Sağ: <color=#00E5FF>{HangarManager.GetDroneDisplayName(hm.rightDroneType)}</color>\n\n" +
                $"<size=80%><color=#8AB4F8><i>Dronlar çatışmada hasar alır. Canı bitince Hangar'dan tekrar satın alınabilir.</i></color></size>";
        }

        // 3. Filo Muharebe Gücü ve Skoru
        if (_combatContentTMP != null)
        {
            float baseHp = hm.activeAircraft switch { HangarManager.AircraftType.A10TankBuster => 160f, HangarManager.AircraftType.F22Raptor => 80f, _ => 100f };
            float baseDmg = hm.activeAircraft switch { HangarManager.AircraftType.A10TankBuster => 28f, HangarManager.AircraftType.F22Raptor => 22f, _ => 20f };
            float baseSpd = hm.activeAircraft switch { HangarManager.AircraftType.A10TankBuster => 6.2f, HangarManager.AircraftType.F22Raptor => 10.5f, _ => 8.0f };

            float totalHp = baseHp + hm.chipBonusHealth;
            float totalDmg = baseDmg + hm.chipBonusDamage;
            float totalSpd = baseSpd;
            float totalCrit = 0f;

            int ratingScore = (int)((totalHp * 1.5f) + (totalDmg * 12f) + (totalSpd * 18f) + (totalCrit * 10f));

            string planeName = hm.activeAircraft switch
            {
                HangarManager.AircraftType.A10TankBuster => "A-10 TANK BUSTER",
                HangarManager.AircraftType.F22Raptor     => "F-22 RAPTOR",
                _                                        => "F-16 VIPER"
            };

            _combatContentTMP.text =
                $"<b>[AKTİF MUHAREBE GÜCÜ]</b>\n\n" +
                $"• Görev Uçağı: <color=#FFE082>{planeName}</color>\n\n" +
                $"• Toplam Can: <color=#2ECC71>{totalHp:0} HP</color>\n\n" +
                $"• Mermi Hasarı: <color=#E74C3C>{totalDmg:0.0} DMG</color>\n\n" +
                $"• Uçuş Hızı: <color=#3498DB>{totalSpd:0.0} Mach</color>\n\n" +
                $"• Kritik Vuruş Şansı: <color=#F1C40F>%{totalCrit:0}</color>\n\n" +
                $"• Toplam Muharebe Skoru:\n  <color=#F1C40F><size=125%>★ {ratingScore:N0} GP</size></color>";
        }
    }

    private string GetDroneDisplayName(DroneCompanion.DroneType type)
    {
        return type switch
        {
            DroneCompanion.DroneType.Laser   => "Lazer Dronu",
            DroneCompanion.DroneType.Missile => "Füze Dronu",
            DroneCompanion.DroneType.Shield  => "Kalkan Dronu",
            _                                => "BOŞ"
        };
    }

    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        _panelRoot = new GameObject("InventoryPanelRoot");
        _panelRoot.transform.SetParent(transform, false);

        RectTransform rootRt = GetRect(_panelRoot);
        MakeFullScreenRect(rootRt);

        Image panelBg = _panelRoot.AddComponent<Image>();
        panelBg.color = new Color(0.02f, 0.05f, 0.09f, 1.0f);

        // 1. ÜST BAŞLIK & ALTIN ROZETİ
        BuildHeader();

        // 2. 3 FERAH ENVANTER KARTI
        BuildInventoryCards();

        // 3. EKRANIN SOL EN ALTINDA GERİ DÖN BUTONU
        BuildBottomLeftBackButton();

        _panelRoot.SetActive(false);
    }

    private void BuildHeader()
    {
        GameObject headerObj = new GameObject("HeaderTitle");
        headerObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform hRt = GetRect(headerObj);
        hRt.anchorMin = new Vector2(0.5f, 1f);
        hRt.anchorMax = new Vector2(0.5f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -22f);
        hRt.sizeDelta = new Vector2(1100f, 65f);

        TextMeshProUGUI titleTMP = headerObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "<b>ASKERİ ENVANTER VE DONANIM DEPOSU</b>\n<size=55%><color=#8AB4F8>Kazanılan tüm kalıcı As çipleri, drone hangarı ve filo güç değerlendirmesi.</color></size>";
        titleTMP.fontSize = 28f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;

        // Üst Sağda Altın Rozeti
        GameObject goldBadgeObj = new GameObject("GoldBadge");
        goldBadgeObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform gRt = GetRect(goldBadgeObj);
        gRt.anchorMin = new Vector2(1f, 1f);
        gRt.anchorMax = new Vector2(1f, 1f);
        gRt.pivot = new Vector2(1f, 1f);
        gRt.anchoredPosition = new Vector2(-40f, -22f);
        gRt.sizeDelta = new Vector2(320f, 52f);

        Image gBg = goldBadgeObj.AddComponent<Image>();
        gBg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

        Outline gOutline = goldBadgeObj.AddComponent<Outline>();
        gOutline.effectColor = new Color(1f, 0.85f, 0.2f);
        gOutline.effectDistance = new Vector2(2f, -2f);

        GameObject goldTxtObj = new GameObject("GoldText");
        goldTxtObj.transform.SetParent(goldBadgeObj.transform, false);
        RectTransform gtRt = GetRect(goldTxtObj);
        gtRt.anchorMin = Vector2.zero;
        gtRt.anchorMax = Vector2.one;
        gtRt.offsetMin = new Vector2(10f, 0f);
        gtRt.offsetMax = new Vector2(-10f, 0f);

        _goldTMP = goldTxtObj.AddComponent<TextMeshProUGUI>();
        _goldTMP.text = "ALTIN: 0 G";
        _goldTMP.fontSize = 24f;
        _goldTMP.fontStyle = FontStyles.Bold;
        _goldTMP.alignment = TextAlignmentOptions.Center;
        _goldTMP.color = new Color(1f, 0.88f, 0.25f);

        // Üst Sol Geri Dön Butonu (Hızlı erişim)
        GameObject topBackBtnObj = new GameObject("TopBackFromInventoryButton");
        topBackBtnObj.transform.SetParent(_panelRoot.transform, false);
        RectTransform tbRt = GetRect(topBackBtnObj);
        tbRt.anchorMin = new Vector2(0f, 1f);
        tbRt.anchorMax = new Vector2(0f, 1f);
        tbRt.pivot = new Vector2(0f, 1f);
        tbRt.anchoredPosition = new Vector2(35f, -22f);
        tbRt.sizeDelta = new Vector2(200f, 52f);

        Image tbImg = topBackBtnObj.AddComponent<Image>();
        tbImg.color = new Color(0.18f, 0.28f, 0.40f);

        Outline tbOutline = topBackBtnObj.AddComponent<Outline>();
        tbOutline.effectColor = new Color(0.35f, 0.75f, 1f);
        tbOutline.effectDistance = new Vector2(2f, -2f);

        Button tbBtn = topBackBtnObj.AddComponent<Button>();
        tbBtn.onClick.AddListener(CloseInventory);

        GameObject tbTxtObj = new GameObject("Text");
        tbTxtObj.transform.SetParent(topBackBtnObj.transform, false);
        RectTransform tbTxtRt = GetRect(tbTxtObj);
        tbTxtRt.anchorMin = Vector2.zero;
        tbTxtRt.anchorMax = Vector2.one;
        tbTxtRt.offsetMin = Vector2.zero;
        tbTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tbTMP = tbTxtObj.AddComponent<TextMeshProUGUI>();
        tbTMP.text = "< GERİ DÖN";
        tbTMP.fontSize = 21f;
        tbTMP.fontStyle = FontStyles.Bold;
        tbTMP.alignment = TextAlignmentOptions.Center;
        tbTMP.color = Color.white;
    }

    private void BuildInventoryCards()
    {
        float boxY = 15f;
        float spacingX = 490f;

        CreateCard("ChipsBox", -spacingX, boxY, "KALICI AS ÇİPLERİ", new Color(0.2f, 0.8f, 0.45f), out _chipsContentTMP);
        CreateCard("DronesBox", 0f, boxY, "DRONE HANGAR ENVANTERİ", new Color(0.2f, 0.7f, 1f), out _dronesContentTMP);
        CreateCard("CombatBox", spacingX, boxY, "FİLO MUHAREBE GÜCÜ", new Color(1f, 0.82f, 0.2f), out _combatContentTMP);
    }

    private void CreateCard(string objName, float posX, float posY, string headerTitle, Color accentColor, out TextMeshProUGUI contentTMP)
    {
        GameObject cardObj = new GameObject(objName);
        cardObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform cRt = GetRect(cardObj);
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(posX, posY);
        cRt.sizeDelta = new Vector2(450f, 540f);

        Image cBg = cardObj.AddComponent<Image>();
        cBg.color = new Color(0.06f, 0.10f, 0.17f, 0.95f);

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(3f, -3f);

        // Başlık
        GameObject tObj = new GameObject("Title");
        tObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = GetRect(tObj);
        tRt.anchorMin = new Vector2(0f, 0.86f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tTMP = tObj.AddComponent<TextMeshProUGUI>();
        tTMP.text = $"<b>{headerTitle}</b>";
        tTMP.fontSize = 22f;
        tTMP.alignment = TextAlignmentOptions.Center;
        tTMP.color = accentColor;

        // İçerik Metni
        GameObject dObj = new GameObject("ContentText");
        dObj.transform.SetParent(cardObj.transform, false);
        RectTransform dRt = GetRect(dObj);
        dRt.anchorMin = new Vector2(0.06f, 0.04f);
        dRt.anchorMax = new Vector2(0.94f, 0.85f);
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;

        contentTMP = dObj.AddComponent<TextMeshProUGUI>();
        contentTMP.text = "Yükleniyor...";
        contentTMP.fontSize = 19f;
        contentTMP.lineSpacing = 5f;
        contentTMP.alignment = TextAlignmentOptions.TopLeft;
        contentTMP.color = new Color(0.92f, 0.95f, 1f);
    }

    private void BuildBottomLeftBackButton()
    {
        // EKRANIN SOL EN ALTINDA GERİ DÖN BUTONU (Anchor 0,0 - Pivot 0,0)
        GameObject backBtnObj = new GameObject("BottomLeftBackButton");
        backBtnObj.transform.SetParent(_panelRoot.transform, false);

        RectTransform bRt = GetRect(backBtnObj);
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.zero;
        bRt.pivot = Vector2.zero;
        bRt.anchoredPosition = new Vector2(40f, 25f);
        bRt.sizeDelta = new Vector2(260f, 54f);
        backBtnObj.transform.SetAsLastSibling();

        Image bImg = backBtnObj.AddComponent<Image>();
        bImg.color = new Color(0.20f, 0.30f, 0.44f);

        Outline bOutline = backBtnObj.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.35f, 0.75f, 1f);
        bOutline.effectDistance = new Vector2(2f, -2f);

        Button bBtn = backBtnObj.AddComponent<Button>();
        bBtn.onClick.AddListener(CloseInventory);

        GameObject bTxtObj = new GameObject("Text");
        bTxtObj.transform.SetParent(backBtnObj.transform, false);
        RectTransform bTxtRt = GetRect(bTxtObj);
        bTxtRt.anchorMin = Vector2.zero;
        bTxtRt.anchorMax = Vector2.one;
        bTxtRt.offsetMin = Vector2.zero;
        bTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI bTMP = bTxtObj.AddComponent<TextMeshProUGUI>();
        bTMP.text = "< GERİ DÖN";
        bTMP.fontSize = 22f;
        bTMP.fontStyle = FontStyles.Bold;
        bTMP.alignment = TextAlignmentOptions.Center;
        bTMP.color = Color.white;
    }
}
