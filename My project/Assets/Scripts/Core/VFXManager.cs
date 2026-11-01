using UnityEngine;

/// <summary>
/// Dinamik 2D Görsel Efekt Yöneticisi (Procedural VFX):
/// Harici asset bağımlılığı olmadan kod üzerinden muhteşem patlamalar,
/// mermi isabet kıvılcımları ve şok dalgaları üretir.
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    private static Material _particleMat;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitMaterial();
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    public static VFXManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("VFXManager");
        Instance = go.AddComponent<VFXManager>();
        InitMaterial();
        return Instance;
    }

    private static void InitMaterial()
    {
        if (_particleMat == null)
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.Pow(alpha, 1.8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            Shader shader = Shader.Find("Mobile/Particles/Additive");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _particleMat = new Material(shader);
            _particleMat.mainTexture = tex;
        }
    }

    /// <summary>
    /// Uçak vurulduğunda merminin çarptığı yerde parlak kıvılcım patlaması oluşturur.
    /// </summary>
    public static void SpawnHitSparks(Vector3 position, Color sparkColor)
    {
        EnsureInstance();

        GameObject sparkObj = new GameObject("HitSparks");
        sparkObj.transform.position = position;

        ParticleSystem ps = sparkObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = sparkObj.GetComponent<ParticleSystemRenderer>();
        psr.material = _particleMat;
        psr.sortingOrder = 15;

        var main = ps.main;
        main.startLifetime = 0.22f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = sparkColor;
        main.loop = false;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(sparkColor, 0f), new GradientColorKey(Color.white, 0.4f), new GradientColorKey(Color.yellow, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        ps.Play();
        Destroy(sparkObj, 0.4f);
    }

    /// <summary>
    /// Uçak düştüğünde alevli, dumanlı ve parçacıklı sinematik patlama oluşturur.
    /// </summary>
    public static void SpawnExplosion(Vector3 position, bool isBoss = false)
    {
        EnsureInstance();

        GameObject expObj = new GameObject("ExplosionEffect");
        expObj.transform.position = position;

        float scale = isBoss ? 2.2f : 1.2f;
        int count = isBoss ? 45 : 25;

        // 1. Alev ve Kıvılcım Çekirdeği
        ParticleSystem ps = expObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = expObj.GetComponent<ParticleSystemRenderer>();
        psr.material = _particleMat;
        psr.sortingOrder = 20;

        var main = ps.main;
        main.startLifetime = 0.45f * (isBoss ? 1.5f : 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f * scale, 10f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f * scale, 0.55f * scale);
        main.startColor = new Color(1f, 0.6f, 0.1f);
        main.loop = false;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f * scale;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 0.5f), new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        ps.Play();

        // 2. Patlama Sesi
        AudioManager.Instance?.PlayExplosion(isBoss);

        // 3. Ekran Sarsıntısı
        if (isBoss)
            CameraShake.ShakeBossDeath();
        else
            CameraShake.ShakeExplosion();

        Destroy(expObj, 1.2f);
    }
}

