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

        PlaneHealth health = target.GetComponent<PlaneHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
