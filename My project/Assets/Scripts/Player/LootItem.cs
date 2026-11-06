using System.Collections;
using UnityEngine;

/// <summary>
/// Düşman uçaklarından düşen ve sahada süzülen ganimet/güçlendirme küresi:
/// - Health: +35 Can Tamiri
/// - Shield: +60 Plazma Enerji Kalkanı
/// - Overdrive: 7 Saniye Süper Seri 3'lü Lazer Atışı
/// - Gold: +100 Altın Madalyon
/// </summary>
public class LootItem : MonoBehaviour
{
    public enum LootType
    {
        Health,
        Shield,
        Overdrive,
        Gold
    }

    public LootType type = LootType.Health;
    public float floatSpeed = 0.95f;
    public float magnetRadius = 2.6f;
    public float lifetime = 14f;

    private Transform _playerTransform;
    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private Color _themeColor = Color.green;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 0.42f;
        col.isTrigger = true;
    }

    private void Start()
    {
        FindPlayer();
        SetupVisuals();
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (_playerTransform == null || !_playerTransform.gameObject.activeInHierarchy)
            FindPlayer();

        // Hafifçe aşağı doğru süzülme
        Vector2 velocity = new Vector2(0f, -floatSpeed);

        // Manyetik Çekim: Oyuncuya yaklaştığında oyuncuya doğru çekilir
        if (_playerTransform != null)
        {
            float dist = Vector2.Distance(transform.position, _playerTransform.position);
            if (dist < magnetRadius)
            {
                Vector2 dir = ((Vector2)_playerTransform.position - _rb.position).normalized;
                velocity = Vector2.Lerp(velocity, dir * 4.5f, (magnetRadius - dist) / magnetRadius);
            }
        }

        _rb.linearVelocity = velocity;

        // Hafif nabız gibi atma animasyonu
        float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.12f;
        transform.localScale = new Vector3(pulse, pulse, 1f);
    }

    private void FindPlayer()
    {
        var planes = FindObjectsOfType<PlaneController>();
        foreach (var p in planes)
        {
            if (p.isPlayer1)
            {
                _playerTransform = p.transform;
                break;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlaneController pc = other.GetComponent<PlaneController>();
        if (pc == null || !pc.isPlayer1) return;

        PlaneHealth health = other.GetComponent<PlaneHealth>();
        PlaneShooter shooter = other.GetComponent<PlaneShooter>();
        var gameUI = FindObjectOfType<GameUI>();

        switch (type)
        {
            case LootType.Health:
                health?.Heal(35f);
                gameUI?.ShowWaveAlert("[ +35 GÖVDE TAMİRİ ]", new Color(0.2f, 1f, 0.4f), 1.3f);
                break;

            case LootType.Shield:
                PlayerUpgradeManager.Instance?.GrantShield(60f);
                gameUI?.ShowWaveAlert("[ +60 ENERJİ KALKANI AKTİF ]", new Color(0.2f, 0.9f, 1f), 1.3f);
                break;

            case LootType.Overdrive:
                shooter?.ActivateOverdrive(7.0f);
                gameUI?.ShowWaveAlert("[ AŞIRI YÜKLEME: 3'LÜ SERİ ATEŞ ]", new Color(1f, 0.85f, 0.1f), 1.5f);
                break;

            case LootType.Gold:
                int dropGold = 50;
                PlayerUpgradeManager.Instance?.AddGold(dropGold);
                gameUI?.ShowWaveAlert($"[ +{dropGold} HANGAR ALTINI ]", new Color(1f, 0.9f, 0.2f), 1.3f);
                break;
        }

        // Parlak kıvılcım ve ses efekti
        VFXManager.SpawnHitSparks(transform.position, _themeColor);
        AudioManager.Instance?.PlayGunfire(false);

        Destroy(gameObject);
    }

    private void SetupVisuals()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

        int size = 28;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        switch (type)
        {
            case LootType.Health:
                _themeColor = new Color(0.2f, 1f, 0.4f);
                DrawHealthKit(tex, size);
                break;
            case LootType.Shield:
                _themeColor = new Color(0.1f, 0.85f, 1f);
                DrawShieldSphere(tex, size);
                break;
            case LootType.Overdrive:
                _themeColor = new Color(1f, 0.85f, 0.15f);
                DrawLightning(tex, size);
                break;
            case LootType.Gold:
                _themeColor = new Color(1f, 0.75f, 0.1f);
                DrawCoin(tex, size);
                break;
        }

        tex.Apply();
        _sr.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 28f);
        _sr.sortingOrder = 16;
    }

    private void DrawHealthKit(Texture2D tex, int size)
    {
        Color green = new Color(0.2f, 0.95f, 0.35f);
        Color white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2, size / 2));
                if (dist > (size / 2 - 1))
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    // Yeşil arka plan küresi
                    tex.SetPixel(x, y, new Color(0.05f, 0.35f, 0.15f, 0.95f));

                    // Beyaz Sağlık Artısı
                    bool isCrossH = (y >= 11 && y <= 16 && x >= 7 && x <= 20);
                    bool isCrossV = (x >= 11 && x <= 16 && y >= 7 && y <= 20);
                    if (isCrossH || isCrossV)
                        tex.SetPixel(x, y, white);
                }
            }
        }
    }

    private void DrawShieldSphere(Texture2D tex, int size)
    {
        Color cyan = new Color(0f, 0.9f, 1f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2, size / 2));
                if (dist > (size / 2 - 1))
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    float ring = Mathf.Abs(dist - (size / 2 - 3f));
                    if (ring < 2.5f)
                        tex.SetPixel(x, y, cyan);
                    else
                        tex.SetPixel(x, y, new Color(0f, 0.3f, 0.6f, 0.85f));
                }
            }
        }
    }

    private void DrawLightning(Texture2D tex, int size)
    {
        Color yellow = new Color(1f, 0.9f, 0.1f);
        Color dark = new Color(0.4f, 0.3f, 0.05f, 0.9f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2, size / 2));
                if (dist > (size / 2 - 1))
                    tex.SetPixel(x, y, Color.clear);
                else
                    tex.SetPixel(x, y, dark);
            }
        }

        // Yıldırım simgesi pikselleri
        for (int y = 5; y < 23; y++)
        {
            int cx = 14 + (y > 14 ? (y - 14) : -(14 - y));
            for (int dx = -2; dx <= 2; dx++)
            {
                int px = Mathf.Clamp(cx + dx, 0, size - 1);
                tex.SetPixel(px, y, yellow);
            }
        }
    }

    private void DrawCoin(Texture2D tex, int size)
    {
        Color gold = new Color(1f, 0.85f, 0.2f);
        Color border = new Color(0.75f, 0.5f, 0.05f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2, size / 2));
                if (dist > (size / 2 - 1))
                    tex.SetPixel(x, y, Color.clear);
                else if (dist > (size / 2 - 3))
                    tex.SetPixel(x, y, border);
                else
                    tex.SetPixel(x, y, gold);
            }
        }
    }
}

