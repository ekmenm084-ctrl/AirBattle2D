using UnityEngine;

/// <summary>
/// Yüksek hızlı taktik izli mermi (Autocannon Tracer Round).
/// Uçağın namlusundan fırlatılır, hedefe çarptığında hasar verir.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Mermi Ayarları")]
    public float speed = 20f;          // Yüksek hızlı gerçekçi mermi hızı
    public float lifetime = 2f;
    public float damage = 20f;
    public bool isCritical = false;    // 2 Kat Hasar veren altın kritik vuruş

    [HideInInspector]
    public GameObject owner;

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        // Çarpışma kutusunu aerodinamik izli mermi boyutuna çek
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = new Vector2(0.25f, 0.8f);
            box.isTrigger = true;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            circle.radius = 0.3f;
            circle.isTrigger = true;
        }
    }

    private void Start()
    {
        if (isCritical)
        {
            transform.localScale = Vector3.one * 1.35f;
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(1f, 0.9f, 0.15f);
        }

        _rb.linearVelocity = transform.up * speed;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (Mathf.Abs(transform.position.y) > 6.5f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HitTarget(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HitTarget(collision.gameObject);
    }

    private void HitTarget(GameObject target)
    {
        if (target == owner) return;

        // Drona çarptı mı? (Düşman mermisi dronu vurup hasar verebilir)
        DroneCompanion drone = target.GetComponent<DroneCompanion>();
        if (drone != null && drone.GetPlayer() != null && drone.GetPlayer().gameObject != owner)
        {
            VFXManager.SpawnHitSparks(transform.position, new Color(1f, 0.7f, 0.2f));
            drone.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        PlaneHealth health = target.GetComponent<PlaneHealth>();
        if (health != null)
        {
            VFXManager.SpawnHitSparks(transform.position, new Color(1f, 0.9f, 0.2f));
            health.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
