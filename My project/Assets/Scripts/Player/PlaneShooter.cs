using UnityEngine;

/// <summary>
/// Uçağın silah sistemi. Mermi üretir ve ateş hızını yönetir.
/// P1: Space (Boşluk) veya Ateş Butonu
/// P2 (Online/2P): Enter, Keypad 0 veya Aşağı Ok (▼)
/// </summary>
public class PlaneShooter : MonoBehaviour
{
    [Header("Silah Ayarları")]
    public GameObject bulletPrefab;         // Bullet prefab'ı
    public Transform firePoint;             // Merminin çıktığı nokta
    public float fireRate = 0.28f;          // Seri ateş hızı
    public float bulletDamage = 20f;        // Mermi hasarı
    public float criticalChance = 0f;       // % Kritik Vuruş Şansı (2x Hasar)

    [Header("Çoklu / Boss Atış Sistemi")]
    public int bulletsPerShot = 1;          // 1: Tekli, 2: Çiftli, 3: Üçlü
    public float bulletSpread = 0.35f;      // Namlular arası yatay mesafe

    [Header("Mermi Sesi ve Namlu Parlama Animasyonu")]
    public Sprite muzzleFlashSprite;
    public bool enableRecoil = true;

    private float _nextFireTime = 0f;
    private bool _fireRequested = false;
    private Coroutine _recoilCoroutine;
    private Coroutine _overdriveCoroutine;
    public bool isOverdriveActive { get; private set; } = false;

    public void RequestFire() => _fireRequested = true;
    public void FireManual() => TryFire();

    private void Start()
    {
        PlaneController pc = GetComponent<PlaneController>();
        if (pc != null && pc.isPlayer1)
        {
            // Player 1 için taban %15 kritik darbe şansı
            if (criticalChance <= 0f)
            {
                criticalChance = 0.15f;
            }
        }
    }

    /// <summary>
    /// Ganimet kutusundan alınan aşırı yükleme ile uçağı geçici olarak süper seri 3'lü atış moduna geçirir.
    /// </summary>
    public void ActivateOverdrive(float duration)
    {
        if (_overdriveCoroutine != null)
            StopCoroutine(_overdriveCoroutine);
        _overdriveCoroutine = StartCoroutine(OverdriveRoutine(duration));
    }

    private System.Collections.IEnumerator OverdriveRoutine(float duration)
    {
        isOverdriveActive = true;
        float originalRate = fireRate;
        int originalBullets = bulletsPerShot;
        float originalCrit = criticalChance;

        fireRate = 0.12f; // Çok seri ateş hızı
        bulletsPerShot = Mathf.Max(3, bulletsPerShot + 1); // En az 3 namlu
        criticalChance = Mathf.Min(0.85f, criticalChance + 0.25f); // Aşırı yüklemede +%25 kritik şansı!

        yield return new WaitForSeconds(duration);

        fireRate = originalRate;
        bulletsPerShot = originalBullets;
        criticalChance = originalCrit;
        isOverdriveActive = false;
        _overdriveCoroutine = null;
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameEnded) return;

        PlaneController pc = GetComponent<PlaneController>();
        if (pc == null) return;

        bool isTwoPlayer = (GameSettings.SelectedMode == GameSettings.GameMode.Online) ||
                           (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);

        if (pc.isPlayer1)
        {
            // P1 Ateş Tuşları: Space (Boşluk) veya F
            try
            {
                if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.F))
                {
                    _fireRequested = true;
                }
            }
            catch { }
        }
        else if (isTwoPlayer)
        {
            // P2 Ateş Tuşları: Space (diğeri gibi!), Enter, KeypadEnter, Keypad 0, Yukarı Ok, Aşağı Ok, Sağ Ctrl, Sağ Shift, K
            try
            {
                if (Input.GetKey(KeyCode.Space) ||
                    Input.GetKey(KeyCode.Return) ||
                    Input.GetKey(KeyCode.KeypadEnter) ||
                    Input.GetKey(KeyCode.Keypad0) ||
                    Input.GetKey(KeyCode.DownArrow) ||
                    Input.GetKey(KeyCode.UpArrow) ||
                    Input.GetKey(KeyCode.RightControl) ||
                    Input.GetKey(KeyCode.RightShift) ||
                    Input.GetKey(KeyCode.K))
                {
                    _fireRequested = true;
                }
            }
            catch { }
        }

        if (_fireRequested)
        {
            TryFire();
            _fireRequested = false;
        }
    }

    /// <summary>Mermi fırlatır (Tekli, Çiftli veya Üçlü Boss salvosu) ve mermi sesi/namlu parlaması oluşturur.</summary>
    public void TryFire()
    {
        if (Time.time < _nextFireTime) return;
        if (bulletPrefab == null) return;

        _nextFireTime = Time.time + fireRate;

        Vector3 basePos = firePoint != null ? firePoint.position : transform.position + transform.up * 0.65f;
        Quaternion spawnRot = firePoint != null ? firePoint.rotation : transform.rotation;

        if (bulletsPerShot <= 1)
        {
            SpawnSingleBullet(basePos, spawnRot);
            TriggerMuzzleFlash(basePos, spawnRot);
        }
        else if (bulletsPerShot == 2)
        {
            // Çift namlulu paralel atış (Boss 1 / Boss 2)
            Vector3 leftPos = basePos - transform.right * (bulletSpread * 0.5f);
            Vector3 rightPos = basePos + transform.right * (bulletSpread * 0.5f);
            SpawnSingleBullet(leftPos, spawnRot);
            SpawnSingleBullet(rightPos, spawnRot);
            TriggerMuzzleFlash(leftPos, spawnRot);
            TriggerMuzzleFlash(rightPos, spawnRot);
        }
        else
        {
            // Üçlü salvo atışı (Boss 3 / Final Boss)
            Vector3 leftPos = basePos - transform.right * bulletSpread;
            Vector3 rightPos = basePos + transform.right * bulletSpread;
            SpawnSingleBullet(leftPos, spawnRot);
            SpawnSingleBullet(basePos, spawnRot);
            SpawnSingleBullet(rightPos, spawnRot);
            TriggerMuzzleFlash(leftPos, spawnRot);
            TriggerMuzzleFlash(basePos, spawnRot);
            TriggerMuzzleFlash(rightPos, spawnRot);
        }

        // Dinamik Makineli Top Sesi
        AudioManager.Instance?.PlayGunfire(bulletsPerShot > 1);

        // Namlu ateşleme görsel geri bildirimi (Sprite ölçek tepmesi)
        if (enableRecoil)
        {
            TriggerRecoil();
        }
    }

    private GameObject _muzzleFlashObj;
    private SpriteRenderer _muzzleFlashRenderer;
    private Coroutine _flashCoroutine;

    private void EnsureMuzzleFlashObject()
    {
        if (_muzzleFlashObj != null) return;

        if (muzzleFlashSprite == null)
        {
            muzzleFlashSprite = Resources.Load<Sprite>("muzzle_flash");
        }

        _muzzleFlashObj = new GameObject("MuzzleFlashFX");
        _muzzleFlashObj.transform.SetParent(transform, false);

        Vector3 localOffset = firePoint != null ? transform.InverseTransformPoint(firePoint.position) : new Vector3(0f, 0.65f, 0f);
        _muzzleFlashObj.transform.localPosition = localOffset;
        _muzzleFlashObj.transform.localRotation = Quaternion.identity;

        _muzzleFlashRenderer = _muzzleFlashObj.AddComponent<SpriteRenderer>();
        if (muzzleFlashSprite != null) _muzzleFlashRenderer.sprite = muzzleFlashSprite;
        _muzzleFlashRenderer.sortingOrder = 14;
        _muzzleFlashObj.SetActive(false);
    }

    private void TriggerMuzzleFlash(Vector3 position, Quaternion rotation)
    {
        EnsureMuzzleFlashObject();

        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);

        _flashCoroutine = StartCoroutine(AnimateMuzzleFlashRoutine());
    }

    private System.Collections.IEnumerator AnimateMuzzleFlashRoutine()
    {
        if (_muzzleFlashObj != null)
        {
            _muzzleFlashObj.SetActive(true);
            if (_muzzleFlashRenderer != null)
            {
                _muzzleFlashRenderer.color = Color.white;
            }
            yield return new WaitForSeconds(0.045f);
            if (_muzzleFlashObj != null)
            {
                _muzzleFlashObj.SetActive(false);
            }
        }
        _flashCoroutine = null;
    }

    private void TriggerRecoil()
    {
        // Uçağın Transform veya Rigidbody konumunu ASLA sıfırlamıyoruz.
        // Konumu sıfırlamak hareket halindeyken uçağın takılmasına ve titremesine sebep oluyordu.
        // Yalnızca uçağın görsel Sprite'ına anlık hafif bir tepme hissi veriyoruz:
        if (_recoilCoroutine != null)
            StopCoroutine(_recoilCoroutine);

        _recoilCoroutine = StartCoroutine(RecoilRoutine());
    }

    private System.Collections.IEnumerator RecoilRoutine()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Vector3 originalScale = sr.transform.localScale;
            sr.transform.localScale = new Vector3(originalScale.x * 1.05f, originalScale.y * 0.93f, originalScale.z);
            yield return new WaitForSeconds(0.045f);
            if (sr != null)
            {
                sr.transform.localScale = originalScale;
            }
        }
        _recoilCoroutine = null;
    }

    private void SpawnSingleBullet(Vector3 position, Quaternion rotation)
    {
        GameObject bullet = Instantiate(bulletPrefab, position, rotation);
        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            bool isCrit = (Random.value < criticalChance);
            b.isCritical = isCrit;
            b.damage = bulletDamage * (isCrit ? 2.0f : 1.0f);
            b.owner = gameObject;
        }

        // Merminin ateş eden uçağın collider'ına çarpıp uçağı takmasını/durdurmasını kesinlikle engelle
        Collider2D planeCol = GetComponent<Collider2D>();
        Collider2D bulletCol = bullet.GetComponent<Collider2D>();
        if (planeCol != null && bulletCol != null)
        {
            Physics2D.IgnoreCollision(planeCol, bulletCol);
        }
    }

    /// <summary>
    /// Mermi Cehennemi (Bullet Hell) veya özel mermi desenleri için istenen yöne, hıza ve renge sahip mermi fırlatır.
    /// </summary>
    public GameObject SpawnCustomBullet(Vector3 position, Vector2 direction, float speed = 10f, float damage = 15f, Color? bulletColor = null)
    {
        if (bulletPrefab == null) return null;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        GameObject bullet = Instantiate(bulletPrefab, position, rot);
        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.speed = speed;
            b.damage = damage;
            b.owner = gameObject;
        }

        if (bulletColor.HasValue)
        {
            SpriteRenderer sr = bullet.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = bulletColor.Value;
        }

        Collider2D planeCol = GetComponent<Collider2D>();
        Collider2D bulletCol = bullet.GetComponent<Collider2D>();
        if (planeCol != null && bulletCol != null)
        {
            Physics2D.IgnoreCollision(planeCol, bulletCol);
        }

        return bullet;
    }
}
