using UnityEngine;

/// <summary>
/// Savaş Combo Çarpanı, Mermi Sıyırma (Bullet Graze) ve Hasarsız Dalga Bonusu Yöneticisi.
/// - Düşman öldürme ve mermi sıyırmalarıyla combo serisi birikir (1.0x -> 1.5x -> 2.0x -> 2.5x -> 3.0x).
/// - Kazanılan altın ödülleri aktif combo çarpanı ile katlanır.
/// - Mermi sıyırma (düşman mermisinin hasar vermeden çok yakından geçmesi) Ulti enerjisi, Combo ve Altın kazandırır.
/// - Dalga boyunca gövde hasarı alınmazsa dalga sonunda ek Hasarsız Dalga Altın + Ulti bonusu verilir.
/// </summary>
public class CombatComboManager : MonoBehaviour
{
    public static CombatComboManager Instance { get; private set; }

    public struct FlawlessWaveResult
    {
        public bool isFlawless;
        public int goldBonus;
        public float ultiBonus;
        public int streak;
    }

    [Header("Combo Ayarları")]
    public float comboDuration = 5.0f;

    public int currentCombo { get; private set; } = 0;
    public int maxCombo { get; private set; } = 0;
    public float maxMultiplier { get; private set; } = 1.0f;
    public float comboTimer { get; private set; } = 0f;

    [Header("Mermi Sıyırma (Graze) İstatistikleri")]
    public int totalGrazes { get; private set; } = 0;
    public int waveGrazes { get; private set; } = 0;
    public int totalGrazeGold { get; private set; } = 0;

    [Header("Hasarsız Dalga İstatistikleri")]
    public bool waveTookDamage { get; private set; } = false;
    public int noDamageWavesStreak { get; private set; } = 0;
    public int totalNoDamageWaves { get; private set; } = 0;
    public int totalComboBonusGold { get; private set; } = 0;
    public int totalFlawlessGold { get; private set; } = 0;

    private float _lastGrazeSfxTime = -999f;
    private float _lastGrazeTextTime = -999f;
    private float _lastComboLostTextTime = -999f;
    private int _pendingGrazeTextCount = 0;
    private int _pendingGrazeGold = 0;

    public float ComboTimerNormalized =>
        (currentCombo > 0 && comboDuration > 0f) ? Mathf.Clamp01(comboTimer / comboDuration) : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public static CombatComboManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("CombatComboManager");
        if (go == null)
            go = new GameObject("CombatComboManager");

        Instance = go.GetComponent<CombatComboManager>();
        if (Instance == null)
            Instance = go.AddComponent<CombatComboManager>();

        return Instance;
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;
        if (GameManager.Instance != null &&
            (GameManager.Instance.IsGameEnded || GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer))
        {
            return;
        }

        if (currentCombo > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                currentCombo = 0;
                comboTimer = 0f;
            }
        }
    }

    public void ResetSession()
    {
        currentCombo = 0;
        maxCombo = 0;
        maxMultiplier = 1.0f;
        comboTimer = 0f;
        totalGrazes = 0;
        waveGrazes = 0;
        totalGrazeGold = 0;
        waveTookDamage = false;
        noDamageWavesStreak = 0;
        totalNoDamageWaves = 0;
        totalComboBonusGold = 0;
        totalFlawlessGold = 0;
        _pendingGrazeTextCount = 0;
        _pendingGrazeGold = 0;
        _lastGrazeSfxTime = -999f;
        _lastGrazeTextTime = -999f;
        _lastComboLostTextTime = -999f;
    }

    public void OnWaveStarted(int waveIndex)
    {
        waveTookDamage = false;
        waveGrazes = 0;

        // Dalga geçiş bekleme süresinin mevcut seriyi bozmaması için zamanlayıcıyı tazele
        if (currentCombo > 0)
        {
            comboTimer = Mathf.Max(comboTimer, comboDuration);
        }
    }

    public float GetMultiplier()
    {
        return GetMultiplierForCombo(currentCombo);
    }

    public static float GetMultiplierForCombo(int combo)
    {
        if (combo >= 15) return 3.0f;
        if (combo >= 10) return 2.5f;
        if (combo >= 6) return 2.0f;
        if (combo >= 3) return 1.5f;
        return 1.0f;
    }

    public Color GetTierColor()
    {
        float mult = GetMultiplier();
        if (mult >= 3.0f) return new Color(1f, 0.22f, 0.65f);   // Efsanevi As (Neon Pembe/Kızıl)
        if (mult >= 2.5f) return new Color(1f, 0.72f, 0.12f);   // Dominasyon (Altın/Amber)
        if (mult >= 2.0f) return new Color(0.22f, 1f, 0.48f);   // Seri (Zümrüt Yeşili)
        if (mult >= 1.5f) return new Color(0.18f, 0.90f, 1f);   // Yükselen (Cyan)
        return new Color(0.68f, 0.78f, 0.90f);                  // Standart
    }

    public string GetTierLabel()
    {
        float mult = GetMultiplier();
        if (mult >= 3.0f) return "EFSANEVİ AS";
        if (mult >= 2.5f) return "DOMİNASYON";
        if (mult >= 2.0f) return "SERİ";
        if (mult >= 1.5f) return "YÜKSELEN";
        return "STANDART";
    }

    public void RefreshComboTimer(float minRemaining = 2.6f)
    {
        if (currentCombo <= 0) return;
        comboTimer = Mathf.Max(comboTimer, Mathf.Min(comboDuration, minRemaining));
    }

    public void AddCombo(int amount = 1, Vector3 worldPos = default)
    {
        if (GameManager.Instance != null &&
            (GameManager.Instance.IsGameEnded || GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer))
        {
            return;
        }

        float prevMult = GetMultiplier();
        currentCombo += Mathf.Max(1, amount);
        comboTimer = comboDuration;

        if (currentCombo > maxCombo)
            maxCombo = currentCombo;

        float newMult = GetMultiplier();
        if (newMult > maxMultiplier)
            maxMultiplier = newMult;

        if (newMult > prevMult)
        {
            AudioManager.Instance?.PlayComboTierUp(newMult);

            Vector3 popupPos = (worldPos != default)
                ? worldPos + Vector3.up * 0.45f
                : GetPlayerPosition() + Vector3.up * 0.85f;
            DamageNumberManager.EnsureInstance()?.SpawnCustomText(
                popupPos,
                $"COMBO x{newMult:0.0}!",
                GetTierColor(),
                1.25f
            );
        }

        GameUI ui = FindObjectOfType<GameUI>();
        ui?.PulseComboHUD();
    }

    public void RegisterBulletGraze(Vector3 bulletPos, bool duringDodge)
    {
        if (GameManager.Instance != null &&
            (GameManager.Instance.IsGameEnded || GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer))
        {
            return;
        }

        totalGrazes++;
        waveGrazes++;

        float ultiGain = duringDodge ? 1.5f : 2.5f;
        PlayerCombatManeuvers.Instance?.AddUltiEnergy(ultiGain);

        int grazeGold = Mathf.Max(2, Mathf.RoundToInt(2f * GetMultiplier()));
        GrantGoldInternal(grazeGold);
        totalGrazeGold += grazeGold;

        Vector3 playerPos = GetPlayerPosition();
        Vector3 sparkPos = Vector3.Lerp(playerPos, bulletPos, 0.45f);
        VFXManager.SpawnHitSparks(sparkPos, new Color(0.25f, 0.96f, 1f));

        AddCombo(1, playerPos);

        if (Time.time >= _lastGrazeSfxTime + 0.09f)
        {
            _lastGrazeSfxTime = Time.time;
            AudioManager.Instance?.PlayGraze(currentCombo);
        }

        _pendingGrazeTextCount++;
        _pendingGrazeGold += grazeGold;

        if (!duringDodge && Time.time >= _lastGrazeTextTime + 0.22f)
        {
            _lastGrazeTextTime = Time.time;
            string label = (_pendingGrazeTextCount > 1)
                ? $"SIYIRMA x{_pendingGrazeTextCount}! +{_pendingGrazeGold}G"
                : $"SIYIRMA! +{_pendingGrazeGold}G";
            _pendingGrazeTextCount = 0;
            _pendingGrazeGold = 0;

            DamageNumberManager.EnsureInstance()?.SpawnCustomText(
                playerPos + new Vector3(0f, 0.72f, 0f),
                label,
                new Color(0.28f, 0.96f, 1f),
                0.98f
            );
        }
        else if (duringDodge)
        {
            _pendingGrazeTextCount = 0;
            _pendingGrazeGold = 0;
        }
    }

    public void NotifyPlayerHullDamaged()
    {
        if (GameManager.Instance != null &&
            (GameManager.Instance.IsGameEnded || GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer))
        {
            return;
        }

        waveTookDamage = true;
        noDamageWavesStreak = 0;

        if (currentCombo >= 3 && Time.time >= _lastComboLostTextTime + 0.6f)
        {
            _lastComboLostTextTime = Time.time;
            DamageNumberManager.EnsureInstance()?.SpawnCustomText(
                GetPlayerPosition() + new Vector3(0f, 0.85f, 0f),
                "COMBO BOZULDU!",
                new Color(1f, 0.36f, 0.24f),
                1.08f
            );
        }

        currentCombo = 0;
        comboTimer = 0f;
    }

    public int ApplyGoldMultiplier(int baseGold, out int bonusGoldOnly)
    {
        if (baseGold <= 0)
        {
            bonusGoldOnly = 0;
            return 0;
        }

        float mult = GetMultiplier();
        int finalGold = Mathf.Max(baseGold, Mathf.RoundToInt(baseGold * mult));
        bonusGoldOnly = finalGold - baseGold;
        totalComboBonusGold += bonusGoldOnly;
        return finalGold;
    }

    public FlawlessWaveResult EvaluateFlawlessWaveBonus(int waveIndex, bool isBoss)
    {
        if (waveTookDamage || (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer))
        {
            return new FlawlessWaveResult { isFlawless = false };
        }

        noDamageWavesStreak++;
        totalNoDamageWaves++;

        int baseBonus = isBoss ? (75 + waveIndex * 5) : (20 + waveIndex * 3);
        int streakBonus = Mathf.Min(50, (noDamageWavesStreak - 1) * 10);
        int goldBonus = baseBonus + streakBonus;
        float ultiBonus = isBoss ? 20f : 12f;

        GrantGoldInternal(goldBonus);
        totalFlawlessGold += goldBonus;
        PlayerCombatManeuvers.Instance?.AddUltiEnergy(ultiBonus);

        return new FlawlessWaveResult
        {
            isFlawless = true,
            goldBonus = goldBonus,
            ultiBonus = ultiBonus,
            streak = noDamageWavesStreak
        };
    }

    public string BuildSessionSummaryLine()
    {
        return $"<size=78%><color=#80DEEA>MAKS COMBO: x{maxMultiplier:0.0} ({maxCombo}) • SIYIRMA: {totalGrazes} • HASARSIZ DALGA: {totalNoDamageWaves}</color></size>";
    }

    private void GrantGoldInternal(int amount)
    {
        if (amount <= 0) return;
        HangarManager.EnsureInstance()?.AddGold(amount);
        if (GameManager.Instance != null)
            GameManager.Instance.sessionGoldEarned += amount;
    }

    private Vector3 GetPlayerPosition()
    {
        if (GameManager.Instance != null && GameManager.Instance.plane1Health != null)
            return GameManager.Instance.plane1Health.transform.position;
        return Vector3.zero;
    }
}

