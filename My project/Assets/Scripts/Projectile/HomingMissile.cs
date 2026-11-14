using System.Collections;
using UnityEngine;

/// <summary>
/// Gerçekçi Askeri Hava-Hava Güdümlü Füze (AIM-120 AMRAAM / Meteor Sınıfı):
/// - Katı yakıt roket motoru ateşleme gecikmesi (Pylon ayrılışı -> Boost fazı)
/// - Süpersonik ivmelenme (9.5f -> 24.5f)
/// - Oransal seyrüsefer ve hedef önleme (Lead Pursuit / Proportional Navigation)
/// - Aerodinamik G-kuvveti dönüş yumuşatması ve arayıcı başlık mikro titreşimi
/// - Çift parçacık egzoz sistemi: Süpersonik jet alevi + havada asılı kalarak genişleyen beyaz roket dumanı (contrail)
/// - Proximity Fuze (Yakınlık tapası ile parça tesirli patlama)
/// </summary>
public class HomingMissile : MonoBehaviour
{
    [Header("Gerçekçi Füze Parametreleri")]
    public float launchSpeed = 9.5f;
    public float maxSpeed = 24.5f;
    public float acceleration = 34f;
    public float minTurnRate = 170f;
    public float maxTurnRate = 320f;
    public float leadPredictionFactor = 0.45f;
    public float proximityRadius = 0.55f;
    public float damage = 65f;
    public float lifetime = 5.2f;

    [HideInInspector]
    public GameObject owner;

    private Transform _target;
    private Rigidbody2D _targetRb;
    private Rigidbody2D _rb;
    private ParticleSystem _flamePS;
    private ParticleSystem _smokePS;

    private float _currentSpeed;
    private float _flightTime = 0f;
    private float _ignitionDelay = 0.10f; // Pylondan ayrılma süresi
    private bool _isMotorIgnited = false;
    private float _wobbleSeed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 0.32f;
        col.isTrigger = true;

        _wobbleSeed = Random.Range(0f, 100f);
        _currentSpeed = launchSpeed;

        SetupMissileVisuals();
        SetupMissileVFX();
    }

    private void Start()
    {
        FindNearestTarget();
        AudioManager.Instance?.PlayMissileLaunch();

        // Başlangıç ayrılma hızı (uçaktan fırlama ivmesi)
        _rb.linearVelocity = transform.up * launchSpeed;

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        _flightTime += Time.deltaTime;

        // 1. Motor Ateşleme & İvmelenme Dinamiği
        if (!_isMotorIgnited && _flightTime >= _ignitionDelay)
        {
            _isMotorIgnited = true;
            if (_flamePS != null) _flamePS.Play();
            if (_smokePS != null) _smokePS.Play();
        }

        if (_isMotorIgnited)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed, acceleration * Time.deltaTime);
        }

        // 2. Hedef Takip ve Önleme Mantığı
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            FindNearestTarget();
        }

        Vector2 desiredDirection = transform.up;

        if (_target != null)
        {
            Vector2 targetPos = _target.position;

            // Proximity Fuze (Yakınlık Tapası): Hedefe kritik yakınlığa ulaştığında anında infilak eder
            float distToTarget = Vector2.Distance(_rb.position, targetPos);
            if (distToTarget <= proximityRadius && _flightTime > 0.25f)
            {
                TriggerDirectHit(_target.gameObject);
                return;
            }

            // Oransal Seyrüsefer / Önleme Hesaplaması (Lead Pursuit)
            if (_targetRb != null)
            {
                float timeToReach = distToTarget / Mathf.Max(_currentSpeed, 1f);
                Vector2 targetVelocity = _targetRb.linearVelocity;
                targetPos += targetVelocity * (timeToReach * leadPredictionFactor);
            }

            Vector2 toTarget = targetPos - _rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                desiredDirection = toTarget.normalized;
            }
        }

        // 3. Arayıcı Başlık Mikro Salınımı (Gerçekçi Seeker Micro-Wobble)
        float wobble = Mathf.Sin(Time.time * 26f + _wobbleSeed) * 1.6f;

        // 4. Aerodinamik Açısal Dönüş Sınırı (G-Force Limit)
        float targetAngle = Mathf.Atan2(desiredDirection.y, desiredDirection.x) * Mathf.Rad2Deg - 90f + wobble;
        float currentAngle = transform.eulerAngles.z;

        // Hız arttıkça dönüş yarıçapı aerodinamik olarak hafif genişler (gerçekçi füze fiziği)
        float speedRatio = Mathf.InverseLerp(launchSpeed, maxSpeed, _currentSpeed);
        float currentTurnRate = Mathf.Lerp(maxTurnRate, minTurnRate, speedRatio);

        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, currentTurnRate * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);

        // 5. İleri Hız & Eylemsizlik (Inertial Aerodynamic Velocity)
        Vector2 forwardVelocity = (Vector2)transform.up * _currentSpeed;
        _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, forwardVelocity, Time.deltaTime * 14f);
    }

    private void FindNearestTarget()
    {
        var planes = FindObjectsOfType<PlaneController>();
        float closestDist = Mathf.Infinity;
        Transform closest = null;
        Rigidbody2D closestRb = null;
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
                        closestRb = p.GetComponent<Rigidbody2D>();
                    }
                }
            }
        }

        // Swarm Düşmanları da hedef alabilir
        if (closest == null)
        {
            var swarms = FindObjectsOfType<SwarmEnemy>();
            foreach (var s in swarms)
            {
                if (s.gameObject == owner || !s.gameObject.activeInHierarchy) continue;
                float dist = Vector2.Distance(transform.position, s.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = s.transform;
                    closestRb = s.GetComponent<Rigidbody2D>();
                }
            }
        }

        _target = closest;
        _targetRb = closestRb;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == owner) return;
        TriggerDirectHit(other.gameObject);
    }

    private void TriggerDirectHit(GameObject hitObj)
    {
        SwarmEnemy swarm = hitObj.GetComponent<SwarmEnemy>();
        if (swarm != null && swarm.gameObject != owner)
        {
            swarm.TakeDamage(damage, true, transform.position);
            Detonate();
            return;
        }

        PlaneHealth health = hitObj.GetComponent<PlaneHealth>();
        if (health != null && hitObj != owner)
        {
            health.TakeDamage(damage, true, transform.position);
            Detonate();
            return;
        }

        BossPatternController boss = hitObj.GetComponent<BossPatternController>();
        if (boss != null && hitObj != owner)
        {
            PlaneHealth bHealth = boss.GetComponent<PlaneHealth>();
            if (bHealth != null)
            {
                bHealth.TakeDamage(damage, true, transform.position);
            }
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

        // 1. Taktik Yüksek Kaliteli Füze Sprite'ını Yükle
        Sprite loadedSpr = Resources.Load<Sprite>("missile_tactical");
        if (loadedSpr != null)
        {
            sr.sprite = loadedSpr;
            // 64x192 px askeri füze için mükemmel sahne ölçeği
            transform.localScale = new Vector3(0.36f, 0.36f, 1f);
        }
        else
        {
            // Yedek: Yüksek çözünürlüklü prosedürel askeri AMRAAM füzesi
            GenerateProceduralMilitaryMissile(sr);
        }

        sr.sortingOrder = 18;
    }

    private void GenerateProceduralMilitaryMissile(SpriteRenderer sr)
    {
        int w = 24;
        int h = 72;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color bodyBase = new Color(0.85f, 0.88f, 0.92f);
        Color radomeCol = new Color(0.96f, 0.96f, 0.98f);
        Color warheadBand = new Color(0.95f, 0.80f, 0.15f);
        Color finCol = new Color(0.20f, 0.22f, 0.26f);
        Color nozzleCol = new Color(0.12f, 0.12f, 0.14f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x - w * 0.5f) / (w * 0.5f);
                tex.SetPixel(x, y, Color.clear);

                // Burun konisi (Radome)
                if (y >= 56)
                {
                    float factor = 1f - ((float)(y - 56) / 16f);
                    if (Mathf.Abs(nx) <= factor * 0.55f)
                        tex.SetPixel(x, y, radomeCol);
                }
                // Savaş Başlığı Sarı Taktik Halkası
                else if (y >= 48 && y < 52)
                {
                    if (Mathf.Abs(nx) <= 0.55f)
                        tex.SetPixel(x, y, warheadBand);
                }
                // Ön Kontrol Kanatçıkları (Canards)
                else if (y >= 40 && y < 48)
                {
                    if (Mathf.Abs(nx) <= 0.95f)
                        tex.SetPixel(x, y, (Mathf.Abs(nx) > 0.55f) ? finCol : bodyBase);
                }
                // Kuyruk Dümen Kanatları
                else if (y >= 4 && y < 18)
                {
                    if (Mathf.Abs(nx) <= 0.98f)
                        tex.SetPixel(x, y, (Mathf.Abs(nx) > 0.55f) ? finCol : bodyBase);
                }
                // Egzoz Nozülü
                else if (y < 4)
                {
                    if (Mathf.Abs(nx) <= 0.45f)
                        tex.SetPixel(x, y, nozzleCol);
                }
                // Ana Gövde
                else
                {
                    if (Mathf.Abs(nx) <= 0.55f)
                        tex.SetPixel(x, y, bodyBase);
                }
            }
        }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 56f);
        transform.localScale = Vector3.one;
    }

    private void SetupMissileVFX()
    {
        // 1. JET ALEVİ (Supersonic Core Flame)
        GameObject flameObj = new GameObject("MissileJetFlame");
        flameObj.transform.SetParent(transform, false);
        flameObj.transform.localPosition = new Vector3(0f, -0.65f, 0f);

        _flamePS = flameObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer flamePsr = flameObj.GetComponent<ParticleSystemRenderer>();
        Shader addShader = Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        flamePsr.material = new Material(addShader);
        flamePsr.sortingOrder = 17;

        var flameMain = _flamePS.main;
        flameMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        flameMain.startLifetime = 0.12f;
        flameMain.startSpeed = 2.4f;
        flameMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
        flameMain.startColor = new Color(1f, 0.85f, 0.4f, 0.95f);
        flameMain.playOnAwake = false;
        flameMain.loop = true;

        var flameShape = _flamePS.shape;
        flameShape.shapeType = ParticleSystemShapeType.Cone;
        flameShape.angle = 6f;
        flameShape.radius = 0.05f;
        flameShape.rotation = new Vector3(180f, 0f, 0f);

        var flameEmission = _flamePS.emission;
        flameEmission.rateOverTime = 45f;

        // 2. GERÇEKÇİ DUMAN İZİ (Contrail - World Space Asılı Kalan Genişleyen Duman)
        GameObject smokeObj = new GameObject("MissileSmokeTrail");
        smokeObj.transform.SetParent(transform, false);
        smokeObj.transform.localPosition = new Vector3(0f, -0.70f, 0f);

        _smokePS = smokeObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer smokePsr = smokeObj.GetComponent<ParticleSystemRenderer>();
        Shader alphaShader = Shader.Find("Mobile/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        smokePsr.material = new Material(alphaShader);
        smokePsr.sortingOrder = 16;

        var smokeMain = _smokePS.main;
        smokeMain.simulationSpace = ParticleSystemSimulationSpace.World;
        smokeMain.startLifetime = 0.85f;
        smokeMain.startSpeed = 0.4f;
        smokeMain.startSize = 0.15f;
        smokeMain.startColor = new Color(0.92f, 0.94f, 0.96f, 0.75f);
        smokeMain.playOnAwake = false;
        smokeMain.loop = true;

        var smokeSize = _smokePS.sizeOverLifetime;
        smokeSize.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.20f);
        sizeCurve.AddKey(0.4f, 0.65f);
        sizeCurve.AddKey(1f, 1.25f); // Zamanla genişleyen duman halkası
        smokeSize.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var smokeColor = _smokePS.colorOverLifetime;
        smokeColor.enabled = true;
        Gradient smokeGrad = new Gradient();
        smokeGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0f),
                new GradientColorKey(new Color(0.88f, 0.90f, 0.92f), 0.35f),
                new GradientColorKey(new Color(0.70f, 0.72f, 0.75f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.80f, 0f),
                new GradientAlphaKey(0.55f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        smokeColor.color = smokeGrad;

        var smokeEmission = _smokePS.emission;
        smokeEmission.rateOverTime = 55f;
    }
}
