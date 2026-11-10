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
                reactionSpeed = 0.35f;
                fireInterval = 1.15f;
                aimToleranceX = 0.9f;
                dodgeChance = 0.15f;
                if (_controller != null) _controller.moveSpeed = 4.2f;
                if (_shooter != null) _shooter.bulletDamage = 14f;
                break;

            case GameSettings.Difficulty.Normal:
                reactionSpeed = 0.15f;
                fireInterval = 0.65f;
                aimToleranceX = 1.4f;
                dodgeChance = 0.35f;
                if (_controller != null) _controller.moveSpeed = 6.2f;
                if (_shooter != null) _shooter.bulletDamage = 20f;
                break;

            case GameSettings.Difficulty.Zor:
                reactionSpeed = 0.05f; // Anlık refleksler
                fireInterval = 0.30f;  // Seri ve baskı kuran acımasız ateş
                aimToleranceX = 2.0f;  // Geniş nişan toleransı (ekrandan kaçışı zorlaştırır)
                dodgeChance = 0.70f;   // Çok çevik ve agresif kaçış manevrası
                if (_controller != null) _controller.moveSpeed = 9.2f; // Yüksek takip hızı
                if (_shooter != null) _shooter.bulletDamage = 28f;     // Ezici mermi hasarı
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
                speedMod = 0.85f;
                fireRateMod = 1.25f;
                reactionMod = 1.30f;
                dodgeMod = 0.40f;
                damageMod = 0.85f;
                break;

            case GameSettings.Difficulty.Normal:
                speedMod = 1.05f;
                fireRateMod = 0.90f;
                reactionMod = 0.90f;
                dodgeMod = 1.10f;
                damageMod = 1.15f;
                break;

            case GameSettings.Difficulty.Zor:
                speedMod = 1.35f;     // %35 daha hızlı ve çevik hareket
                fireRateMod = 0.50f;  // %50 daha seri ve acımasız ateş (aralık yarıya iner!)
                reactionMod = 0.45f;  // %55 daha keskin tepki
                dodgeMod = 1.85f;     // Çok yüksek kaçış ve manevra
                damageMod = 1.55f;    // %55 daha fazla mermi hasarı
                break;
        }

        // 2. HARDCORE MODU ÖZEL BOSS ÖLÇEKLENDİRMESİ
        if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.Hardcore)
        {
            // Her gelen düşman uçağı bir öncekinden daha çevik, seri ve güçlü bir BOSS!
            float hcProgress = Mathf.Clamp01((wave - 1f) / 10f); // 1. Boss'tan 10. Boss'a doğru maksimuma tırmanış
            reactionSpeed = Mathf.Lerp(0.14f, 0.04f, hcProgress) * reactionMod;
            fireInterval = Mathf.Lerp(0.65f, 0.28f, hcProgress) * fireRateMod;
            aimToleranceX = Mathf.Lerp(1.20f, 2.20f, hcProgress);
            dodgeChance = Mathf.Lerp(0.35f, 0.75f, hcProgress) * dodgeMod;

            if (_controller != null)
                _controller.moveSpeed = Mathf.Lerp(6.5f, 9.5f, hcProgress) * speedMod;

            if (_shooter != null)
            {
                _shooter.fireRate = fireInterval;
                // Boss 1: 2 Namlu, Boss 2-4: 3 Namlu, Boss 5+: 4 Namlu!
                _shooter.bulletsPerShot = (wave >= 5) ? 4 : (wave >= 2 ? 3 : 2);
                _shooter.bulletSpread = 0.42f;
                _shooter.bulletDamage = (18f + Mathf.Min(wave * 3.0f, 30f)) * damageMod;
            }
            return;
        }

        // 3. BOSS VEYA STANDART UÇAK PARAMETRELERİ
        bool isZor = (GameSettings.SelectedDifficulty == GameSettings.Difficulty.Zor);

        if (isBoss)
        {
            // Bosslar: Zor modda ve ilerleyen dalgalarda gerçek bir mermi cehennemi
            reactionSpeed = Mathf.Lerp(0.18f, 0.06f, progress) * reactionMod;
            fireInterval = Mathf.Lerp(0.75f, 0.35f, progress) * fireRateMod;
            aimToleranceX = Mathf.Lerp(1.20f, 2.20f, progress);
            dodgeChance = Mathf.Lerp(0.30f, 0.60f, progress) * dodgeMod;

            if (_controller != null)
                _controller.moveSpeed = Mathf.Lerp(5.5f, 8.5f, progress) * speedMod;

            if (_shooter != null)
            {
                _shooter.fireRate = fireInterval;
                // Zor modda Boss 1 dahil en az 3'lü atar! İleri dalgalarda veya Zor modda 4'lü salvo!
                if (wave >= 15 || (isZor && wave >= 10))
                    _shooter.bulletsPerShot = 4;
                else if (isZor || wave >= 10)
                    _shooter.bulletsPerShot = 3;
                else
                    _shooter.bulletsPerShot = 2;

                _shooter.bulletSpread = 0.42f;
                float bossBaseDmg = 18f + Mathf.Min(wave * 1.0f, 22f);
                _shooter.bulletDamage = bossBaseDmg * damageMod;
            }
        }
        else
        {
            // Standart Uçaklar
            reactionSpeed = Mathf.Lerp(0.28f, 0.07f, progress) * reactionMod;
            fireInterval = Mathf.Lerp(1.05f, 0.40f, progress) * fireRateMod;
            aimToleranceX = Mathf.Lerp(0.90f, 1.80f, progress);
            dodgeChance = Mathf.Lerp(0.15f, 0.45f, progress) * dodgeMod;

            if (_controller != null)
                _controller.moveSpeed = Mathf.Lerp(4.5f, 7.8f, progress) * speedMod;

            if (_shooter != null)
            {
                _shooter.fireRate = fireInterval;
                // Zor modda 2. dalgadan itibaren standart uçaklar da çiftli mermi atar!
                // Zor modda 11. dalgadan sonra 3'lü salvo mermi fırlatır!
                // Normal modda 7. dalgadan sonra çiftli mermi atar.
                if (isZor && wave >= 11)
                    _shooter.bulletsPerShot = 3;
                else if ((isZor && wave >= 2) || wave >= 7)
                    _shooter.bulletsPerShot = 2;
                else
                    _shooter.bulletsPerShot = 1;

                _shooter.bulletSpread = 0.32f;
                float planeBaseDmg = 16f + Mathf.Min(wave * 0.65f, 14f);
                _shooter.bulletDamage = planeBaseDmg * damageMod;
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

        if (Random.value < dodgeChance * 0.18f)
        {
            _isDodging = true;
            _dodgeEndTime = Time.time + Random.Range(0.35f, 0.75f);
            _currentMoveX = Random.value > 0.5f ? 1f : -1f;
            return;
        }

        float diffX = target.position.x - transform.position.x;
        if (Mathf.Abs(diffX) > 0.18f)
        {
            _currentMoveX = Mathf.Sign(diffX);
        }
        else
        {
            _currentMoveX = 0f;
        }
    }
}
