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

    [Header("Çoklu / Boss Atış Sistemi")]
    public int bulletsPerShot = 1;          // 1: Tekli, 2: Çiftli, 3: Üçlü
    public float bulletSpread = 0.35f;      // Namlular arası yatay mesafe

    [Header("Mermi Sesi ve Namlu Parlama Animasyonu")]
    public Sprite muzzleFlashSprite;
    public bool enableRecoil = true;

    private float _nextFireTime = 0f;
    private bool _fireRequested = false;
    private Coroutine _recoilCoroutine;
    private Vector3 _originalLocalPos;
    private bool _originalPosCached = false;

    public void RequestFire() => _fireRequested = true;
    public void FireManual() => TryFire();

    private void Update()
    {
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
            // P2 Ateş Tuşları: Enter, Keypad 0, Aşağı Ok veya Sağ Ctrl
            try
            {
                if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Keypad0) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.K))
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

        Vector3 basePos = firePoint != null ? firePoint.position : transform.position + transform.up * 0.9f;
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

        // Geri tepme (recoil) animasyonu
        if (enableRecoil)
        {
            TriggerRecoil();
        }
    }

    private void TriggerMuzzleFlash(Vector3 position, Quaternion rotation)
    {
        if (muzzleFlashSprite == null)
        {
            muzzleFlashSprite = Resources.Load<Sprite>("muzzle_flash");
        }

        GameObject flashObj = new GameObject("MuzzleFlashFX");
        flashObj.transform.position = position;
        flashObj.transform.rotation = rotation;
        flashObj.transform.Rotate(0f, 0f, Random.Range(-30f, 30f));

        SpriteRenderer sr = flashObj.AddComponent<SpriteRenderer>();
        if (muzzleFlashSprite != null) sr.sprite = muzzleFlashSprite;
        sr.sortingOrder = 12; // Mermilerin ve uçağın üstünde parlar

        StartCoroutine(AnimateMuzzleFlashRoutine(flashObj, sr));
    }

    private System.Collections.IEnumerator AnimateMuzzleFlashRoutine(GameObject flashObj, SpriteRenderer sr)
    {
        float duration = 0.065f;
        float elapsed = 0f;
        Vector3 startScale = new Vector3(0.5f, 0.5f, 1f);
        Vector3 peakScale = new Vector3(1.15f, 1.35f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (flashObj != null)
            {
                flashObj.transform.localScale = Vector3.Lerp(startScale, peakScale, t);
                if (sr != null)
                {
                    Color col = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.1f, 0f), t);
                    sr.color = col;
                }
            }
            yield return null;
        }

        if (flashObj != null)
            Destroy(flashObj);
    }

    private void TriggerRecoil()
    {
        if (!_originalPosCached)
        {
            _originalLocalPos = transform.localPosition;
            _originalPosCached = true;
        }

        if (_recoilCoroutine != null)
            StopCoroutine(_recoilCoroutine);

        _recoilCoroutine = StartCoroutine(RecoilRoutine());
    }

    private System.Collections.IEnumerator RecoilRoutine()
    {
        Vector3 kickOffset = -transform.up * 0.07f;
        transform.localPosition = _originalLocalPos + kickOffset;

        float duration = 0.055f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(_originalLocalPos + kickOffset, _originalLocalPos, elapsed / duration);
            yield return null;
        }

        transform.localPosition = _originalLocalPos;
        _recoilCoroutine = null;
    }

    private void SpawnSingleBullet(Vector3 position, Quaternion rotation)
    {
        GameObject bullet = Instantiate(bulletPrefab, position, rotation);
        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.damage = bulletDamage;
            b.owner = gameObject;
        }
    }
}
