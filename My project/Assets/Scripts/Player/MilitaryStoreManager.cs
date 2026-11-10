using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Askeri Havacılık Parça Mağazası ve Uçak Donatım (Loadout) Yöneticisi:
/// - Gerçek dünya savaş uçağı parçaları (Motorlar, AESA Radarları, Zırh, Elektronik Harp).
/// - Seviye/Kademe Uyumluluk Kontrolü (100. seviye parça 20. seviye F-16'ya takılamaz!).
/// - Altın ile satın alma ve envanter kaydı.
/// - Uçak başına 4 slotlu donatım sistemi ve savaşa stat aktarımı.
/// </summary>
public class MilitaryStoreManager : MonoBehaviour
{
    public static MilitaryStoreManager Instance { get; private set; }

    private const string PREF_PART_UNLOCKED_PREFIX = "AirBattle_Part_Unl_";
    private const string PREF_EQUIPPED_PREFIX = "AirBattle_Equipped_";

    public event Action OnStoreDataChanged;

    private readonly List<AircraftPart> _catalog = new List<AircraftPart>();
    private readonly Dictionary<string, AircraftPart> _catalogDict = new Dictionary<string, AircraftPart>();

    public struct LoadoutStats
    {
        public float bonusSpeed;
        public float bonusHp;
        public float bonusDamage;
        public float bonusCrit;
        public float bonusShield;
        public float dodgeCdReduction;
        public float damageReductionPercent;
        public float ultiBonusPercent;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeCatalog();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static MilitaryStoreManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("MilitaryStoreManager");
        if (go == null) go = new GameObject("MilitaryStoreManager");
        Instance = go.GetComponent<MilitaryStoreManager>();
        if (Instance == null) Instance = go.AddComponent<MilitaryStoreManager>();
        Instance.InitializeCatalog();
        return Instance;
    }

    /// <summary>
    /// Uçak modellerinin donanım ve aviyonik kapasite seviyeleri (Tier / Level)
    /// </summary>
    public static int GetAircraftLevel(HangarManager.AircraftType type)
    {
        switch (type)
        {
            case HangarManager.AircraftType.F16Viper: return 20;
            case HangarManager.AircraftType.A10TankBuster: return 40;
            case HangarManager.AircraftType.F22Raptor: return 60;
            case HangarManager.AircraftType.Su57Felon: return 75;
            case HangarManager.AircraftType.F35Lightning: return 85;
            case HangarManager.AircraftType.KaanApex: return 100;
            default: return 20;
        }
    }

    public static string GetAircraftDisplayName(HangarManager.AircraftType type)
    {
        switch (type)
        {
            case HangarManager.AircraftType.F16Viper: return "F-16C Viper";
            case HangarManager.AircraftType.A10TankBuster: return "A-10C Tank Buster";
            case HangarManager.AircraftType.F22Raptor: return "F-22A Raptor";
            case HangarManager.AircraftType.Su57Felon: return "Su-57 Felon";
            case HangarManager.AircraftType.F35Lightning: return "F-35A Lightning II";
            case HangarManager.AircraftType.KaanApex: return "KAAN Apex";
            default: return "Savaş Jeti";
        }
    }

    public static string GetShortAircraftName(HangarManager.AircraftType type)
    {
        switch (type)
        {
            case HangarManager.AircraftType.F16Viper: return "F-16";
            case HangarManager.AircraftType.A10TankBuster: return "A-10";
            case HangarManager.AircraftType.F22Raptor: return "F-22";
            case HangarManager.AircraftType.Su57Felon: return "Su-57";
            case HangarManager.AircraftType.F35Lightning: return "F-35";
            case HangarManager.AircraftType.KaanApex: return "KAAN";
            default: return "Jet";
        }
    }

    /// <summary>
    /// Bir parçanın takılabileceği uçakların isimlerini döner (Örn: "F-22, Su-57, F-35, KAAN" veya "Yalnızca KAAN Apex")
    /// </summary>
    public static string GetCompatibleAircraftNames(int minLevel)
    {
        if (minLevel <= 20) return "Tüm Jetler (F-16, A-10, F-22, Su-57, F-35, KAAN)";
        if (minLevel <= 40) return "A-10, F-22, Su-57, F-35, KAAN";
        if (minLevel <= 60) return "F-22, Su-57, F-35, KAAN";
        if (minLevel <= 75) return "Su-57, F-35, KAAN";
        if (minLevel <= 85) return "F-35, KAAN";
        return "Yalnızca KAAN Apex";
    }

    private void InitializeCatalog()
    {
        if (_catalog.Count > 0) return;

        // ==========================================
        // 1. MOTOR & İTKİ SİSTEMLERİ (Engine & Thrust)
        // ==========================================
        AddPart(new AircraftPart(
            "eng_f100_afterburner",
            "P&W F100-PW-229 Art Yakıcı",
            AircraftPartCategory.Engine,
            minAircraftLevel: 20,
            price: 600,
            realWorldLore: "F-16 Blok 50'lerde kullanılan 29.000 lbf itkili turbofan motor. Anlık süpersonik ivmelenme sağlar.",
            bonusSpeed: 1.5f,
            dodgeCooldownReduction: 0.3f
        ));

        AddPart(new AircraftPart(
            "eng_tf34_turbofan",
            "GE TF34-GE-100 Çift Ağır Turbofan",
            AircraftPartCategory.Engine,
            minAircraftLevel: 40,
            price: 1800,
            realWorldLore: "A-10 Thunderbolt'un gövde üstü korumalı ikiz motoru. Ağır zırh yükünü taşır ve vurulmaya dirençlidir.",
            bonusSpeed: 2.0f,
            bonusHp: 30f,
            dodgeCooldownReduction: 0.4f
        ));

        AddPart(new AircraftPart(
            "eng_f119_2dtvc",
            "P&W F119 2D İtki Yönlendirme (TVC)",
            AircraftPartCategory.Engine,
            minAircraftLevel: 60,
            price: 4800,
            realWorldLore: "F-22 Raptor için üretilen 2 boyutlu yönlendirilebilir egzoz nozülü. Süperseyir (Supercruise) imkanı sunar.",
            bonusSpeed: 3.2f,
            dodgeCooldownReduction: 0.8f
        ));

        AddPart(new AircraftPart(
            "eng_al41_3dtvc",
            "Saturn AL-41F1 3D Süper-Vektör İtki",
            AircraftPartCategory.Engine,
            minAircraftLevel: 75,
            price: 10500,
            realWorldLore: "Su-57'nin her yöne bağımsız dönebilen 3D nozülleri. İmkansız Kobra ve Kulbit akrobatik manevralarını açar.",
            bonusSpeed: 4.4f,
            dodgeCooldownReduction: 1.2f
        ));

        AddPart(new AircraftPart(
            "eng_kaan_adaptive",
            "KAAN TEI Adaptif Çevrim Motoru",
            AircraftPartCategory.Engine,
            minAircraftLevel: 95,
            price: 26000,
            realWorldLore: "KAAN için geliştirilen 35.000+ lbf itkili milli adaptif çevrim motoru. Devasa hız ve anlık kaçış çevikliği sunar.",
            bonusSpeed: 5.8f,
            dodgeCooldownReduction: 1.6f
        ));

        // ==========================================
        // 2. RADAR & AVİYONİK SİSTEMLER (Radar & Avionics)
        // ==========================================
        AddPart(new AircraftPart(
            "rad_apg68_pulse",
            "AN/APG-68(V)9 Pals Doppler Radar",
            AircraftPartCategory.Radar,
            minAircraftLevel: 20,
            price: 700,
            realWorldLore: "F-16'nın yüksek çözünürlüklü hedef arama ve atış kontrol radarı. Hedeflere hızlı ve kesin kilit atar.",
            bonusDamage: 5f,
            bonusCritChance: 8f
        ));

        AddPart(new AircraftPart(
            "rad_sniper_atp",
            "Sniper Gelişmiş Hedefleme Podu (ATP)",
            AircraftPartCategory.Radar,
            minAircraftLevel: 40,
            price: 2100,
            realWorldLore: "FLIR termal elektro-optik arayıcı pod. Gece ve fırtınada düşman zayıf noktalarını saptayarak kritik hasar verir.",
            bonusDamage: 9f,
            bonusCritChance: 14f
        ));

        AddPart(new AircraftPart(
            "rad_apg77_aesa",
            "AN/APG-77 GaN Aktif Dizinli AESA Radar",
            AircraftPartCategory.Radar,
            minAircraftLevel: 60,
            price: 5500,
            realWorldLore: "F-22'nin 1500+ galyum nitrür modüllü LPI (Düşük Yakalanma Olasılıklı) hayalet radarı. Düşman fark etmeden vurur.",
            bonusDamage: 15f,
            bonusCritChance: 20f
        ));

        AddPart(new AircraftPart(
            "rad_aaq37_das",
            "AN/AAQ-37 360° Dağıtık Açıklık (DAS)",
            AircraftPartCategory.Radar,
            minAircraftLevel: 75,
            price: 12000,
            realWorldLore: "F-35'in gövdeye dağıtık elektro-optik kask nişangahı. Pilot uçağın altını saydam görerek her açıdan tam isabet sağlar.",
            bonusDamage: 22f,
            bonusCritChance: 28f
        ));

        AddPart(new AircraftPart(
            "rad_murad_quantum",
            "ASELSAN MURAD-X Kuantum AESA Radar",
            AircraftPartCategory.Radar,
            minAircraftLevel: 95,
            price: 32000,
            realWorldLore: "KAAN için geliştirilen kuantum sinyal işlemcili yerli AESA radar. Eşzamanlı yüzlerce hedefi izler ve kritik tahribat yaratır.",
            bonusDamage: 32f,
            bonusCritChance: 38f
        ));

        // ==========================================
        // 3. GÖVDE & ZIRH SİSTEMLERİ (Airframe & Armor)
        // ==========================================
        AddPart(new AircraftPart(
            "air_f16_titanium",
            "F-16 Titanyum Kanat & Kokpit Takviyesi",
            AircraftPartCategory.Airframe,
            minAircraftLevel: 20,
            price: 650,
            realWorldLore: "Yüksek 9G manevralarına ve hafif şarapnellere dayanıklı titanyum-alüminyum alaşımlı gövde takviyesi.",
            bonusHp: 40f,
            bonusShield: 20f,
            damageReductionPercent: 0.05f
        ));

        AddPart(new AircraftPart(
            "air_a10_bathtub",
            "A-10 Titanyum Zırh Küveti (Bathtub)",
            AircraftPartCategory.Airframe,
            minAircraftLevel: 40,
            price: 2200,
            realWorldLore: "Pilot ve aviyonikleri 23mm uçaksavar mermilerine karşı koruyan efsanevi 540 kg titanyum zırh küveti.",
            bonusHp: 95f,
            bonusShield: 40f,
            damageReductionPercent: 0.10f
        ));

        AddPart(new AircraftPart(
            "air_ram_coating",
            "5. Nesil Radar Emici Materyal (RAM)",
            AircraftPartCategory.Airframe,
            minAircraftLevel: 60,
            price: 5200,
            realWorldLore: "F-22 ve B-2 uçaklarına uygulanan ferromanyetik kompozit radar soğurucu boya. Radar kesitini böcek boyutuna düşürür.",
            bonusHp: 75f,
            bonusShield: 65f,
            damageReductionPercent: 0.08f
        ));

        AddPart(new AircraftPart(
            "air_carbon_matrix",
            "Karbon Nanotüp Matris & Balistik Gövde",
            AircraftPartCategory.Airframe,
            minAircraftLevel: 75,
            price: 11500,
            realWorldLore: "Su-57 ve F-35 için üretilen darbe sönümleyici karbon fiber polimer zırh. Süpersonik mermi şokunu absorbe eder.",
            bonusHp: 130f,
            bonusShield: 95f,
            damageReductionPercent: 0.14f
        ));

        AddPart(new AircraftPart(
            "air_kaan_graphene",
            "KAAN Akıllı Grafen Kompozit Süper Zırh",
            AircraftPartCategory.Airframe,
            minAircraftLevel: 95,
            price: 30000,
            realWorldLore: "KAAN Apex için geliştirilen darbeyi kendi bünyesinde dağıtan nano-grafen akıllı kaplama. Maksimum beka kabiliyeti.",
            bonusHp: 190f,
            bonusShield: 150f,
            damageReductionPercent: 0.20f
        ));

        // ==========================================
        // 4. ELEKTRONİK HARP & SAVUNMA (EW & ECM)
        // ==========================================
        AddPart(new AircraftPart(
            "ew_ale47_dispenser",
            "AN/ALE-47 Otomatik Chaff/Flare Salıcı",
            AircraftPartCategory.ElectronicWarfare,
            minAircraftLevel: 20,
            price: 500,
            realWorldLore: "Radar ve ısı güdümlü tehditleri saptıran otomatik magnezyum flare ve alüminyum chaff kartuş fırlatıcısı.",
            bonusShield: 20f,
            ultiChargeBonusPercent: 0.10f
        ));

        AddPart(new AircraftPart(
            "ew_alq131_pod",
            "AN/ALQ-131 Elektronik Taarruz Karıştırıcı",
            AircraftPartCategory.ElectronicWarfare,
            minAircraftLevel: 40,
            price: 1900,
            realWorldLore: "Düşman radarlarını sahte eko dalgalarıyla kör eden gelişmiş harici elektronik harp karıştırma podu.",
            bonusShield: 35f,
            damageReductionPercent: 0.06f,
            ultiChargeBonusPercent: 0.18f
        ));

        AddPart(new AircraftPart(
            "ew_drfm_jammer",
            "Dijital Radyo Frekans Hafızası (DRFM Karıştırıcı)",
            AircraftPartCategory.ElectronicWarfare,
            minAircraftLevel: 60,
            price: 5000,
            realWorldLore: "Düşman radar sinyalini kaydedip milisaniyeler içinde değiştirerek düşmana yansıtan dijital ECM sistemi.",
            bonusShield: 55f,
            damageReductionPercent: 0.08f,
            ultiChargeBonusPercent: 0.26f
        ));

        AddPart(new AircraftPart(
            "ew_aaq24_dircm",
            "AN/AAQ-24 Nemesis Lazerli DIRCM Sistemi",
            AircraftPartCategory.ElectronicWarfare,
            minAircraftLevel: 75,
            price: 11000,
            realWorldLore: "Gelen füzelerin arayıcı başlıklarını çoklu kızılötesi lazer ışınlarıyla anında yakıp kör eden yönlendirilmiş savunma.",
            bonusShield: 80f,
            damageReductionPercent: 0.12f,
            ultiChargeBonusPercent: 0.35f
        ));

        AddPart(new AircraftPart(
            "ew_kaan_cyber_suite",
            "KAAN Entegre Siber & Elektronik Harp Süiti",
            AircraftPartCategory.ElectronicWarfare,
            minAircraftLevel: 95,
            price: 28000,
            realWorldLore: "ASELSAN İHS mimarili kuantum şifreli aviyonik koruma ve düşman uçaklarına uzaktan siber EMP yayma süiti.",
            bonusShield: 120f,
            damageReductionPercent: 0.16f,
            ultiChargeBonusPercent: 0.50f
        ));
    }

    private void AddPart(AircraftPart p)
    {
        _catalog.Add(p);
        _catalogDict[p.id] = p;
    }

    public List<AircraftPart> GetAllParts() => new List<AircraftPart>(_catalog);

    public List<AircraftPart> GetPartsByCategory(AircraftPartCategory cat)
    {
        List<AircraftPart> res = new List<AircraftPart>();
        for (int i = 0; i < _catalog.Count; i++)
        {
            if (_catalog[i].category == cat)
                res.Add(_catalog[i]);
        }
        return res;
    }

    public AircraftPart GetPartById(string partId)
    {
        if (string.IsNullOrEmpty(partId)) return null;
        if (_catalogDict.TryGetValue(partId, out AircraftPart p)) return p;
        return null;
    }

    // ==========================================
    // ENVANTER & SATIN ALMA
    // ==========================================
    public bool IsPartUnlocked(string partId)
    {
        if (string.IsNullOrEmpty(partId)) return false;
        return PlayerPrefs.GetInt(PREF_PART_UNLOCKED_PREFIX + partId, 0) == 1;
    }

    /// <summary>
    /// Sandık anahtarı veya özel ikramiyeden çıkan parçayı ücretsiz olarak kalıcı açar.
    /// </summary>
    public bool UnlockPartDirectly(string partId)
    {
        if (string.IsNullOrEmpty(partId)) return false;
        PlayerPrefs.SetInt(PREF_PART_UNLOCKED_PREFIX + partId, 1);
        PlayerPrefs.Save();
        OnStoreDataChanged?.Invoke();
        return true;
    }

    public bool BuyPart(string partId, out string message)
    {
        AircraftPart part = GetPartById(partId);
        if (part == null)
        {
            message = "Parça katalogda bulunamadı!";
            return false;
        }

        if (IsPartUnlocked(partId))
        {
            message = "Bu parça zaten envanterinizde mevcut!";
            return false;
        }

        HangarManager hangar = HangarManager.EnsureInstance();
        if (hangar == null || hangar.gold < part.price)
        {
            int diff = (part.price - (hangar != null ? hangar.gold : 0));
            message = $"Yetersiz Fon! Bu askeri parçayı tedarik etmek için {diff:N0} Altın daha gerekiyor.";
            return false;
        }

        if (hangar.SpendGold(part.price))
        {
            PlayerPrefs.SetInt(PREF_PART_UNLOCKED_PREFIX + partId, 1);
            PlayerPrefs.Save();
            OnStoreDataChanged?.Invoke();
            message = $"{part.name} başarıyla satın alındı ve askeri envantere eklendi!";
            return true;
        }

        message = "Satın alma işlemi tamamlanamadı.";
        return false;
    }

    // ==========================================
    // UYUMLULUK KONTROLÜ (Level / Compatibility)
    // ==========================================
    /// <summary>
    /// Bir parçanın seçili uçağa takılıp takılamayacağını denetler.
    /// 100. seviye parça 20. seviye uçağa fazla gelir ve donatılamaz!
    /// </summary>
    public bool IsPartCompatible(HangarManager.AircraftType plane, AircraftPart part, out string failureReason)
    {
        if (part == null)
        {
            failureReason = "Geçersiz parça verisi!";
            return false;
        }

        int planeLevel = GetAircraftLevel(plane);
        string planeName = GetAircraftDisplayName(plane);

        if (planeLevel < part.minLevel)
        {
            string compPlanes = GetCompatibleAircraftNames(part.minLevel);
            failureReason = $"⚠️ UÇAK UYUMSUZLUĞU!\n{planeName} bu parçayı desteklememektedir!\n\nBu parça şu uçaklar için uygundur:\n{compPlanes}";
            return false;
        }

        failureReason = "Donanım uyumlu.";
        return true;
    }

    // ==========================================
    // DONATMA & ÇIKARMA (Loadout Equipment)
    // ==========================================
    private string GetEquipPrefKey(HangarManager.AircraftType plane, AircraftPartCategory cat)
    {
        return $"{PREF_EQUIPPED_PREFIX}{(int)plane}_{(int)cat}";
    }

    public string GetEquippedPartId(HangarManager.AircraftType plane, AircraftPartCategory cat)
    {
        return PlayerPrefs.GetString(GetEquipPrefKey(plane, cat), "");
    }

    public AircraftPart GetEquippedPart(HangarManager.AircraftType plane, AircraftPartCategory cat)
    {
        string id = GetEquippedPartId(plane, cat);
        return GetPartById(id);
    }

    public bool EquipPart(HangarManager.AircraftType plane, string partId, out string resultMessage)
    {
        AircraftPart part = GetPartById(partId);
        if (part == null)
        {
            resultMessage = "Parça bulunamadı!";
            return false;
        }

        if (!IsPartUnlocked(partId))
        {
            resultMessage = "Önce bu parçayı askeri mağazadan satın almalısınız!";
            return false;
        }

        if (!IsPartCompatible(plane, part, out string compReason))
        {
            resultMessage = compReason;
            return false;
        }

        PlayerPrefs.SetString(GetEquipPrefKey(plane, part.category), part.id);
        PlayerPrefs.Save();
        OnStoreDataChanged?.Invoke();

        resultMessage = $"{part.name}, {GetAircraftDisplayName(plane)} uçağına başarıyla monte edildi!";
        return true;
    }

    public void UnequipPart(HangarManager.AircraftType plane, AircraftPartCategory cat)
    {
        PlayerPrefs.DeleteKey(GetEquipPrefKey(plane, cat));
        PlayerPrefs.Save();
        OnStoreDataChanged?.Invoke();
    }

    // ==========================================
    // TOPLAM MUHAREBE STATLARI HESAPLAYICI
    // ==========================================
    public LoadoutStats GetLoadoutStats(HangarManager.AircraftType plane)
    {
        LoadoutStats stats = new LoadoutStats();

        AircraftPartCategory[] categories = {
            AircraftPartCategory.Engine,
            AircraftPartCategory.Radar,
            AircraftPartCategory.Airframe,
            AircraftPartCategory.ElectronicWarfare
        };

        foreach (var cat in categories)
        {
            AircraftPart p = GetEquippedPart(plane, cat);
            if (p != null)
            {
                // Ekstra güvenlik: Uçak seviyesi parçayı kaldırıyor mu kontrol et
                if (GetAircraftLevel(plane) >= p.minLevel)
                {
                    stats.bonusSpeed += p.bonusSpeed;
                    stats.bonusHp += p.bonusHp;
                    stats.bonusDamage += p.bonusDamage;
                    stats.bonusCrit += p.bonusCritChance;
                    stats.bonusShield += p.bonusShield;
                    stats.dodgeCdReduction += p.dodgeCooldownReduction;
                    stats.damageReductionPercent += p.damageReductionPercent;
                    stats.ultiBonusPercent += p.ultiChargeBonusPercent;
                }
            }
        }

        return stats;
    }
}

