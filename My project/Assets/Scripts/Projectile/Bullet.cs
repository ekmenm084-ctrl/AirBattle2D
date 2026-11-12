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

    [Header("Güdümlü Füze (Opsiyonel)")]
    public Transform homingTarget;
    public float homingTurnSpeed = 0f; // Derece/sn (0 = düz mermi)
    public float homingDuration = 1.6f;

    [HideInInspector]
    public GameObject owner;

    private Rigidbody2D _rb;
    private float _age = 0f;

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
        _age += Time.deltaTime;

        if (homingTarget != null && homingTurnSpeed > 0f && _age < homingDuration && _rb != null)
        {
            Vector2 toTarget = ((Vector2)(homingTarget.position - transform.position)).normalized;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                float currentAngle = Mathf.Atan2(transform.up.y, transform.up.x) * Mathf.Rad2Deg;
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, homingTurnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, 0f, nextAngle - 90f);
                _rb.linearVelocity = transform.up * speed;
            }
        }

        if (Mathf.Abs(transform.position.y) > 6.5f || Mathf.Abs(transform.position.x) > 10.5f)
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
            DamageNumberManager.EnsureInstance()?.SpawnDamageNumber(transform.position, damage, isCritical, false);
            drone.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Sürü Düşman İHA'sına çarptı mı?
        SwarmEnemy swarm = target.GetComponent<SwarmEnemy>();
        if (swarm != null && swarm.gameObject != owner)
        {
            if (owner != null && owner.GetComponent<PlaneController>()?.isPlayer1 == true)
            {
                PlayerCombatManeuvers.Instance?.AddUltiEnergy(2.0f);
            }

            VFXManager.SpawnHitSparks(transform.position, isCritical ? new Color(1f, 0.95f, 0.1f) : new Color(1f, 0.8f, 0.2f));
            swarm.TakeDamage(damage, isCritical, transform.position);
            Destroy(gameObject);
            return;
        }

        PlaneHealth health = target.GetComponent<PlaneHealth>();
        if (health != null)
        {
            if (owner != null && owner.GetComponent<PlaneController>()?.isPlayer1 == true)
            {
                PlayerCombatManeuvers.Instance?.AddUltiEnergy(1.8f);
            }

            VFXManager.SpawnHitSparks(transform.position, isCritical ? new Color(1f, 0.95f, 0.1f) : new Color(1f, 0.9f, 0.2f));
            health.TakeDamage(damage, isCritical, transform.position);
            Destroy(gameObject);
        }
    }
}
