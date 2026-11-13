using System.Collections;
using UnityEngine;

/// <summary>
/// Isı Güdümlü Taktik Mikro Füze (Homing Missile):
/// Kanatlardan fırlar, düşman jetine kilitlenerek spiral izle takip eder ve yüksek hasar verir.
/// </summary>
public class HomingMissile : MonoBehaviour
{
    [Header("Füze Ayarları")]
    public float speed = 14f;
    public float turnSpeed = 380f;
    public float damage = 60f;
    public float lifetime = 4.5f;

    [HideInInspector]
    public GameObject owner;

    private Transform _target;
    private Rigidbody2D _rb;
    private ParticleSystem _trailPS;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 0.28f;
        col.isTrigger = true;

        SetupMissileVisuals();
        SetupMissileTrail();
    }

    private void Start()
    {
        FindNearestTarget();
        AudioManager.Instance?.PlayMissileLaunch();
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            FindNearestTarget();
        }

        // Hedefe doğru yumuşak yönelme
        if (_target != null)
        {
            Vector2 direction = (Vector2)_target.position - _rb.position;
            direction.Normalize();

            float rotateAmount = Vector3.Cross(direction, transform.up).z;
            _rb.angularVelocity = -rotateAmount * turnSpeed;
        }
        else
        {
            _rb.angularVelocity = 0f;
        }

        // İleri itiş
        _rb.linearVelocity = transform.up * speed;
    }

    private void FindNearestTarget()
    {
        var planes = FindObjectsOfType<PlaneController>();
        float closestDist = Mathf.Infinity;
        Transform closest = null;
        bool ownerIsP1 = (owner != null && owner.GetComponent<PlaneController>()?.isPlayer1 == true);

        foreach (var p in planes)
        {
            if (p.gameObject == owner) continue;

            PlaneHealth health = p.GetComponent<PlaneHealth>();
            if (health != null && !health.IsDead)
            {
                bool isValidTarget = ownerIsP1 ? !p.isPlayer1 : p.isPlayer1;
                if (isValidTarget)
                {
                    float dist = Vector2.Distance(transform.position, p.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = p.transform;
                    }
                }
            }
        }

        _target = closest;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == owner) return;

        SwarmEnemy swarm = other.GetComponent<SwarmEnemy>();
        if (swarm != null && swarm.gameObject != owner)
        {
            swarm.TakeDamage(damage, true, transform.position);
            Detonate();
            return;
        }

        PlaneHealth health = other.GetComponent<PlaneHealth>();
        if (health != null)
        {
            health.TakeDamage(damage, true, transform.position);
            Detonate();
        }
    }

    private void Detonate()
    {
        VFXManager.SpawnExplosion(transform.position, isBoss: false);
        CameraShake.ShakeExplosion();
        Destroy(gameObject);
    }

    private void SetupMissileVisuals()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        // Prosedürel keskin füze gövdesi
        int w = 12;
        int h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color bodyCol = new Color(0.9f, 0.9f, 0.95f);
        Color tipCol = new Color(1f, 0.2f, 0.2f); // Kırmızı savaş başlığı
        Color finCol = new Color(0.3f, 0.35f, 0.45f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (y >= 24)
                {
                    // Sivri burun
                    int inset = (y - 24);
                    if (x >= inset && x < (w - inset))
                        tex.SetPixel(x, y, tipCol);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
                else if (y < 6)
                {
                    // Kanatçıklar
                    tex.SetPixel(x, y, finCol);
                }
                else
                {
                    // Gövde
                    if (x >= 2 && x <= 9)
                        tex.SetPixel(x, y, bodyCol);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        sr.sortingOrder = 14;
    }

    private void SetupMissileTrail()
    {
        GameObject trailObj = new GameObject("MissileExhaustTrail");
        trailObj.transform.SetParent(transform, false);
        trailObj.transform.localPosition = new Vector3(0f, -0.45f, 0f);

        _trailPS = trailObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = trailObj.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Mobile/Particles/Additive");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        psr.material = new Material(shader);
        psr.sortingOrder = 13;

        var main = _trailPS.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.35f;
        main.startSpeed = 1.0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startColor = new Color(1f, 0.7f, 0.2f, 0.9f);
        main.loop = true;

        var emission = _trailPS.emission;
        emission.rateOverTime = 30f;

        var col = _trailPS.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.4f), new GradientColorKey(Color.gray, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        _trailPS.Play();
    }
}

