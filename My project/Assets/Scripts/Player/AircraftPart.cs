using System;
using UnityEngine;

/// <summary>
/// Gerçek Savaş Uçağı Askeri Parça Veri Modeli:
/// Gerçek dünya aviyonik, motor, zırh ve elektronik harp sistemlerini temsil eder.
/// Uçak seviye/kademe uyumluluk gereksinimine ve detaylı savaş bonuslarına sahiptir.
/// </summary>
public enum AircraftPartCategory
{
    Engine,             // Motor & İtki Sistemleri (Hız, Kaçınma Süresi, İtki Yönlendirme)
    Radar,              // Radar & Aviyonik & Atış Kontrol (Hasar, Kritik Şans, Hedefleme)
    Airframe,           // Gövde, Zırh & Stealth RAM (Can, Kalkan, Hasar Azaltma)
    ElectronicWarfare   // Elektronik Harp & Karşı Tedbirler (Ulti Dolumu, Karıştırıcı, Kalkan)
}

[System.Serializable]
public class AircraftPart
{
    public string id;
    public string name;
    public AircraftPartCategory category;
    public int minAircraftLevel;    // Uçak minimum kademe/seviye gereksinimi (20, 40, 60, 75, 85, 95)
    public int minLevel => minAircraftLevel;
    public int price;               // Altın maliyeti
    public string realWorldLore;    // Gerçek askeri havacılık tarihi ve mühendislik detayı

    [Header("Muharebe İstatistikleri")]
    public float bonusSpeed = 0f;
    public float bonusHp = 0f;
    public float bonusDamage = 0f;
    public float bonusCritChance = 0f;
    public float bonusShield = 0f;
    public float dodgeCooldownReduction = 0f;
    public float damageReductionPercent = 0f;   // 0.05 = %5 hasar emilimi
    public float ultiChargeBonusPercent = 0f;   // 0.20 = +%20 Ulti dolum hızı

    public AircraftPart(
        string id,
        string name,
        AircraftPartCategory category,
        int minAircraftLevel,
        int price,
        string realWorldLore,
        float bonusSpeed = 0f,
        float bonusHp = 0f,
        float bonusDamage = 0f,
        float bonusCritChance = 0f,
        float bonusShield = 0f,
        float dodgeCooldownReduction = 0f,
        float damageReductionPercent = 0f,
        float ultiChargeBonusPercent = 0f)
    {
        this.id = id;
        this.name = name;
        this.category = category;
        this.minAircraftLevel = minAircraftLevel;
        this.price = price;
        this.realWorldLore = realWorldLore;
        this.bonusSpeed = bonusSpeed;
        this.bonusHp = bonusHp;
        this.bonusDamage = bonusDamage;
        this.bonusCritChance = bonusCritChance;
        this.bonusShield = bonusShield;
        this.dodgeCooldownReduction = dodgeCooldownReduction;
        this.damageReductionPercent = damageReductionPercent;
        this.ultiChargeBonusPercent = ultiChargeBonusPercent;
    }

    public string GetCategoryTitle()
    {
        switch (category)
        {
            case AircraftPartCategory.Engine: return "MOTOR & İTKİ";
            case AircraftPartCategory.Radar: return "RADAR & AVİYONİK";
            case AircraftPartCategory.Airframe: return "GÖVDE & ZIRH";
            case AircraftPartCategory.ElectronicWarfare: return "ELEKTRONİK HARP";
            default: return "GENEL PARÇA";
        }
    }

    public string GetCategoryIcon()
    {
        switch (category)
        {
            case AircraftPartCategory.Engine: return "⚡";
            case AircraftPartCategory.Radar: return "📡";
            case AircraftPartCategory.Airframe: return "🛡️";
            case AircraftPartCategory.ElectronicWarfare: return "🛰️";
            default: return "⚙️";
        }
    }

    public string GetStatsSummary()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (bonusSpeed > 0f)
            sb.Append($"<color=#38BDF8>+{bonusSpeed:0.1f} Hız</color>  ");
        if (bonusHp > 0f)
            sb.Append($"<color=#4ADE80>+{bonusHp:0} Gövde HP</color>  ");
        if (bonusDamage > 0f)
            sb.Append($"<color=#F87171>+{bonusDamage:0} Mermi Dmg</color>  ");
        if (bonusCritChance > 0f)
            sb.Append($"<color=#FBBF24>+%{bonusCritChance:0} Kritik</color>  ");
        if (bonusShield > 0f)
            sb.Append($"<color=#67E8F9>+{bonusShield:0} Enerji Kalkanı</color>  ");
        if (dodgeCooldownReduction > 0f)
            sb.Append($"<color=#A78BFA>-{dodgeCooldownReduction:0.1f}s Kaçınma CD</color>  ");
        if (damageReductionPercent > 0f)
            sb.Append($"<color=#34D399>-%{damageReductionPercent * 100f:0} Hasar Emilimi</color>  ");
        if (ultiChargeBonusPercent > 0f)
            sb.Append($"<color=#F472B6>+%{ultiChargeBonusPercent * 100f:0} Ulti Hızı</color>  ");

        return sb.ToString().Trim();
    }
}
