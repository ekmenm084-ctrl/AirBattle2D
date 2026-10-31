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

    private float _nextFireTime = 0f;
    private bool _fireRequested = false;

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

    /// <summary>Mermi fırlatır (Tekli, Çiftli veya Üçlü Boss salvosu).</summary>
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
        }
        else if (bulletsPerShot == 2)
        {
            // Çift namlulu paralel atış (Boss 1 / Boss 2)
            Vector3 leftPos = basePos - transform.right * (bulletSpread * 0.5f);
            Vector3 rightPos = basePos + transform.right * (bulletSpread * 0.5f);
            SpawnSingleBullet(leftPos, spawnRot);
            SpawnSingleBullet(rightPos, spawnRot);
        }
        else
        {
            // Üçlü salvo atışı (Boss 3 / Final Boss)
            Vector3 leftPos = basePos - transform.right * bulletSpread;
            Vector3 rightPos = basePos + transform.right * bulletSpread;
            SpawnSingleBullet(leftPos, spawnRot);
            SpawnSingleBullet(basePos, spawnRot);
            SpawnSingleBullet(rightPos, spawnRot);
        }
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
