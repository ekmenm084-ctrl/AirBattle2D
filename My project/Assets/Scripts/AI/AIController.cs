using UnityEngine;

/// <summary>
/// Yatay düzlemde savaşan Yapay Zeka (AI).
/// - 20 Uçaklık İlerleme Seferinde dalga numarasına göre zorluğu artırır.
/// - Seçilen menü zorluğuna (Kolay, Normal, Zor) göre can, hız, ateş sıklığı ve kaçış parametrelerini ölçekler.
/// - Boss uçakları 2 kademe dengelenmiş olup oyuncuyu ezmeden taktiksel düello sunar.
/// </summary>
[RequireComponent(typeof(PlaneController))]
public class AIController : MonoBehaviour
{
    [Header("Hedef")]
    public Transform target;

    [Header("Ateş & Nişan")]
    public float fireInterval = 0.8f;
    public float aimToleranceX = 0.8f;

    [Header("Yapay Zeka Karakteri")]
    [Range(0f, 1f)]
    public float dodgeChance = 0.35f;
    public float reactionSpeed = 0.15f;

    private PlaneController _controller;
    private PlaneShooter _shooter;
    private PlaneHealth _health;

    private float _nextDecisionTime;
    private float _nextFireTime;
    private float _currentMoveX;
    private bool _isDodging = false;
    private float _dodgeEndTime;

    private void Awake()
    {
        _controller = GetComponent<PlaneController>();
        _shooter = GetComponent<PlaneShooter>();
        _health = GetComponent<PlaneHealth>();
    }

    private void Start()
    {
        GameSettings.LoadSettings();
        ApplyDifficultySettings();
        FindTargetIfNull();
    }

    /// <summary>
    /// Menüden seçilen Kolay / Normal / Zor ayarına göre AI parametrelerini ayarlar.
    /// </summary>
    public void ApplyDifficultySettings()
    {
        switch (GameSettings.SelectedDifficulty)
        {
            case GameSettings.Difficulty.Kolay:
                reactionSpeed = 0.40f;
                fireInterval = 1.35f;
                aimToleranceX = 0.5f;
                dodgeChance = 0.10f;
                if (_controller != null) _controller.moveSpeed = 3.8f;
                if (_shooter != null) _shooter.bulletDamage = 12f;
                break;

            case GameSettings.Difficulty.Normal:
                reactionSpeed = 0.22f;
                fireInterval = 0.85f;
                aimToleranceX = 0.8f;
                dodgeChance = 0.25f;
                if (_controller != null) _controller.moveSpeed = 5.5f;
                if (_shooter != null) _shooter.bulletDamage = 16f;
                break;

            case GameSettings.Difficulty.Zor:
                reactionSpeed = 0.10f;
                fireInterval = 0.50f;
                aimToleranceX = 1.1f;
                dodgeChance = 0.50f;
                if (_controller != null) _controller.moveSpeed = 7.5f;
                if (_shooter != null) _shooter.bulletDamage = 20f;
                break;
        }
    }

    /// <summary>
    /// 20 Uçaklık İlerleme Modunda dalga numarasına VE seçilen zorluğa (Kolay/Normal/Zor)
    /// göre zorluk parametrelerini dinamik olarak dengeler.
    /// </summary>
    public void ConfigureWave(int wave, bool isBoss)
    {
        if (_controller == null) _controller = GetComponent<PlaneController>();
        if (_shooter == null) _shooter = GetComponent<PlaneShooter>();
        if (_health == null) _health = GetComponent<PlaneHealth>();

        float progress = Mathf.Clamp01((wave - 1f) / 19f); // 0 (1. uçak) -> 1 (20. uçak)

        // 1. ZORLUK MODU ÇARPANLARI (Kolay, Normal, Zor)
        float speedMod = 1f;
        float fireRateMod = 1f;
        float reactionMod = 1f;
        float dodgeMod = 1f;
        float damageMod = 1f;

        switch (GameSettings.SelectedDifficulty)
        {
            case GameSettings.Difficulty.Kolay:
                speedMod = 0.80f;     // %20 daha yavaş hareket
                fireRateMod = 1.35f;  // %35 daha seyrek ateş (interval uzar)
                reactionMod = 1.40f;  // %40 daha geç tepki
                dodgeMod = 0.40f;     // Çok az kaçış manevrası
                damageMod = 0.75f;    // %25 daha az mermi hasarı
                break;

            case GameSettings.Difficulty.Normal:
                speedMod = 1.00f;
                fireRateMod = 1.00f;
                reactionMod = 1.00f;
                dodgeMod = 1.00f;
                damageMod = 1.00f;
                break;

            case GameSettings.Difficulty.Zor:
                speedMod = 1.20f;     // %20 daha hızlı hareket
                fireRateMod = 0.80f;  // %20 daha seri ateş
                reactionMod = 0.75f;  // %25 daha ani tepki
                dodgeMod = 1.35f;     // Sık kaçış
                damageMod = 1.20f;    // %20 daha fazla mermi hasarı
                break;
        }

        // 2. BOSS VEYA STANDART UÇAK PARAMETRELERİ (2 Kademe Kolaylaştırılmış ve Dengeli)
        if (isBoss)
        {
            // Bosslar dengelendi: Oyuncunun nefes alabileceği ve vurabileceği akıcı düello temposu
            reactionSpeed = Mathf.Lerp(0.24f, 0.10f, progress) * reactionMod;
            fireInterval = Mathf.Lerp(0.90f, 0.45f, progress) * fireRateMod;
            aimToleranceX = Mathf.Lerp(0.85f, 1.25f, progress);
            dodgeChance = Mathf.Lerp(0.25f, 0.45f, progress) * dodgeMod;

            if (_controller != null)
                _controller.moveSpeed = Mathf.Lerp(5.2f, 7.5f, progress) * speedMod;

            if (_shooter != null)
            {
                _shooter.fireRate = fireInterval;
                _shooter.bulletsPerShot = (wave >= 15) ? 3 : 2; // 15 ve 20. dalga 3'lü salvo
                _shooter.bulletSpread = 0.40f;
                _shooter.bulletDamage = (wave >= 15 ? 12f : 14f) * damageMod;
            }
        }
        else
        {
            // Standart Uçaklar: 1'den 20'ye doğru tatlı ve hissedilir bir yükseliş
            reactionSpeed = Mathf.Lerp(0.38f, 0.12f, progress) * reactionMod;
            fireInterval = Mathf.Lerp(1.30f, 0.55f, progress) * fireRateMod;
            aimToleranceX = Mathf.Lerp(0.50f, 1.05f, progress);
            dodgeChance = Mathf.Lerp(0.10f, 0.35f, progress) * dodgeMod;

            if (_controller != null)
                _controller.moveSpeed = Mathf.Lerp(4.0f, 6.8f, progress) * speedMod;

            if (_shooter != null)
            {
                _shooter.fireRate = fireInterval;
                _shooter.bulletsPerShot = 1;
                _shooter.bulletDamage = 14f * damageMod;
            }
        }
    }

    private void FindTargetIfNull()
    {
        if (target != null) return;
        var planes = FindObjectsOfType<PlaneController>();
        foreach (var p in planes)
        {
            if (p != null && p.isPlayer1 && p.gameObject != gameObject)
            {
                target = p.transform;
                break;
            }
        }
    }

    private void Update()
    {
        if (_health != null && _health.IsDead) return;

        if (target == null)
        {
            FindTargetIfNull();
            if (target == null) return;
        }

        if (Time.time >= _nextDecisionTime)
        {
            _nextDecisionTime = Time.time + reactionSpeed;
            DecideMovement();
        }

        _controller.SetAIInput(new Vector2(_currentMoveX, 0f));

        float diffX = Mathf.Abs(target.position.x - transform.position.x);
        if (diffX <= aimToleranceX && Time.time >= _nextFireTime)
        {
            _nextFireTime = Time.time + fireInterval;
            _shooter?.TryFire();
        }
    }

    private void DecideMovement()
    {
        if (target == null) return;

        if (_isDodging)
        {
            if (Time.time >= _dodgeEndTime)
                _isDodging = false;
            else
                return;
        }

        if (Random.value < dodgeChance * 0.1f)
        {
            _isDodging = true;
            _dodgeEndTime = Time.time + Random.Range(0.4f, 0.8f);
            _currentMoveX = Random.value > 0.5f ? 1f : -1f;
            return;
        }

        float diffX = target.position.x - transform.position.x;
        if (Mathf.Abs(diffX) > 0.25f)
        {
            _currentMoveX = Mathf.Sign(diffX);
        }
        else
        {
            _currentMoveX = 0f;
        }
    }
}
