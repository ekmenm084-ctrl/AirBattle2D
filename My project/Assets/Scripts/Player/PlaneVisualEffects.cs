using System.Collections;
using UnityEngine;

/// <summary>
/// Uçak Gerçekçi Görsel Efekt Sistemi:
/// 1. Sabit & Kararlı Uçuş: Sallanma/yatış kapalı; uçaklar gerçekçi ve stabil durur.
/// 2. Gerçekçi Hasar Dumanı & Alev (Soft World-Space Smoke):
///    - Yalnızca uçak hasar aldığında (%50 altı) kuyruktan arkaya doğru süzülen yumuşak duman bırakır.
///    - Can %25 altına indiğinde motor yangını (kıvılcım + siyah duman) üretir.
///    - Duman 'World Space' simülasyonu ile havada asılı kalarak gerçek bir hava koridoru hissi verir.
/// 3. Vuruş Geri Bildirimi (Hit Flash): Mermi isabet aldığında gövde anlık parlar.
/// </summary>
public class PlaneVisualEffects : MonoBehaviour
{
    private PlaneController _pc;
    private PlaneHealth _health;
    private SpriteRenderer _sr;

    private Color _baseColor = Color.white;
    private Coroutine _flashCoroutine;

    private ParticleSystem _damageSmokePS;
    private ParticleSystem _criticalFirePS;
    private static Material _softSmokeMat;

    private void Awake()
    {
        _pc = GetComponent<PlaneController>();
        _health = GetComponent<PlaneHealth>();
        _sr = GetComponent<SpriteRenderer>();

        if (_sr != null)
            _baseColor = _sr.color;
    }

    private void Start()
    {
        // Uçağın açısını kesin olarak sabitle (Oyuncu: 0°, Düşman: 180°)
        LockPlaneRotation();

        // Gerçekçi partikül materyalini hazırla
        InitSmokeMaterial();

        // Hasar duman sistemlerini oluştur
        SetupRealisticDamageSmoke();
    }

    private void Update()
    {
        // Uçağın açısının sağa-sola sallanmasını kesinlikle engelle
        LockPlaneRotation();

        // Sadece can azaldığında gerçekçi dumanı devreye sok
        UpdateDamageSmoke();
    }

    private void LockPlaneRotation()
    {
        if (_pc == null) return;
        float targetZ = _pc.isPlayer1 ? 0f : 180f;
        transform.rotation = Quaternion.Euler(0f, 0f, targetZ);
    }

    #region Yumuşak Duman Materyali (Soft Radial Alpha)

    private static void InitSmokeMaterial()
    {
        if (_softSmokeMat != null) return;

        // 32x32 Kenarları yumuşatılmış radyal duman dokusu
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
                alpha = Mathf.Pow(alpha, 1.8f); // Yumuşak geçişli kenar
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        Shader shader = Shader.Find("Mobile/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        _softSmokeMat = new Material(shader);
        _softSmokeMat.mainTexture = tex;
    }

    #endregion

    #region Gerçekçi Hasar Dumanı & Yangın Sistemi

    private void SetupRealisticDamageSmoke()
    {
        bool isP1 = (_pc != null && _pc.isPlayer1);

        // 1. Duman Yayıcı Objesi (Egzoz Çıkış Konumu)
        GameObject smokeObj = new GameObject("RealisticDamageSmoke");
        smokeObj.transform.SetParent(transform, false);
        // Yerel koordinatta motor çıkışı (-Y arkasıdır)
        smokeObj.transform.localPosition = new Vector3(0f, -0.6f, 0f);

        _damageSmokePS = smokeObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = smokeObj.GetComponent<ParticleSystemRenderer>();
        psr.material = _softSmokeMat;
        psr.sortingOrder = (_sr != null ? _sr.sortingOrder - 1 : 5);

        var main = _damageSmokePS.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // Uçak hareket ettikçe duman havada kalır!
        main.startLifetime = 0.65f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startColor = new Color(0.18f, 0.18f, 0.18f, 0.55f); // Koyu kömür dumanı
        main.loop = true;
        main.playOnAwake = false;

        var emission = _damageSmokePS.emission;
        emission.rateOverTime = 18f;

        // Duman uçağın arkasına doğru dar bir koni ile üflenir
        var shape = _damageSmokePS.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.08f;
        shape.rotation = new Vector3(90f, 0f, 0f); // Gerçekçi arkaya püskürtme

        // Duman arkaya süzülürken genişler
        var sol = _damageSmokePS.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.6f);
        sizeCurve.AddKey(1f, 1.9f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Duman havada dağılıp şeffaflaşır
        var col = _damageSmokePS.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.25f, 0.25f, 0.25f), 0f), new GradientColorKey(new Color(0.10f, 0.10f, 0.10f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.4f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        _damageSmokePS.Stop();

        // 2. Kritik Yangın / Kıvılcım Yayıcısı (%25 HP altı)
        GameObject fireObj = new GameObject("CriticalFireSparks");
        fireObj.transform.SetParent(transform, false);
        fireObj.transform.localPosition = new Vector3(0f, -0.55f, 0f);

        _criticalFirePS = fireObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer firePsr = fireObj.GetComponent<ParticleSystemRenderer>();
        firePsr.material = _softSmokeMat;
        firePsr.sortingOrder = (_sr != null ? _sr.sortingOrder + 1 : 7);

        var fMain = _criticalFirePS.main;
        fMain.simulationSpace = ParticleSystemSimulationSpace.World;
        fMain.startLifetime = 0.25f;
        fMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        fMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        fMain.startColor = new Color(1f, 0.65f, 0.15f, 0.95f); // Alev Turuncusu
        fMain.loop = true;
        fMain.playOnAwake = false;

        var fEmission = _criticalFirePS.emission;
        fEmission.rateOverTime = 14f;

        var fShape = _criticalFirePS.shape;
        fShape.shapeType = ParticleSystemShapeType.Cone;
        fShape.angle = 12f;
        fShape.radius = 0.06f;
        fShape.rotation = new Vector3(90f, 0f, 0f);

        _criticalFirePS.Stop();
    }

    private void UpdateDamageSmoke()
    {
        if (_health == null) return;

        if (_health.IsDead)
        {
            if (_damageSmokePS != null && _damageSmokePS.isPlaying) _damageSmokePS.Stop();
            if (_criticalFirePS != null && _criticalFirePS.isPlaying) _criticalFirePS.Stop();
            return;
        }

        float hpPercent = _health.HealthPercent;

        // %50 Altı: Gerçekçi duman çıkar
        if (hpPercent < 0.50f)
        {
            if (_damageSmokePS != null && !_damageSmokePS.isPlaying)
                _damageSmokePS.Play();
        }
        else
        {
            // Sağlamken hiçbir duman veya rahatsız edici iz çıkmaz
            if (_damageSmokePS != null && _damageSmokePS.isPlaying)
                _damageSmokePS.Stop();
        }

        // %25 Altı: Kritik motor alevi ve kıvılcımlar
        if (hpPercent < 0.25f)
        {
            if (_criticalFirePS != null && !_criticalFirePS.isPlaying)
                _criticalFirePS.Play();
        }
        else
        {
            if (_criticalFirePS != null && _criticalFirePS.isPlaying)
                _criticalFirePS.Stop();
        }
    }

    #endregion

    #region Vuruş Geri Bildirimi (Hit Flash)

    public void TriggerHitFlash(bool isShieldHit)
    {
        if (_sr == null) return;

        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);

        _flashCoroutine = StartCoroutine(HitFlashRoutine(isShieldHit));
    }

    private IEnumerator HitFlashRoutine(bool isShieldHit)
    {
        // Kalkan varsa Cyan neon parlama, direkt gövde ise beyaz/kırmızı flaş
        Color flashColor = isShieldHit ? new Color(0.3f, 1f, 1f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
        _sr.color = flashColor;

        yield return new WaitForSeconds(0.06f);

        _sr.color = _baseColor;
        _flashCoroutine = null;
    }

    #endregion

    public void SetBaseColor(Color color)
    {
        _baseColor = color;
        if (_sr != null)
            _sr.color = color;
    }

    public void ResetVisuals()
    {
        if (_sr != null) _sr.color = _baseColor;
        if (_damageSmokePS != null && _damageSmokePS.isPlaying) _damageSmokePS.Stop();
        if (_criticalFirePS != null && _criticalFirePS.isPlaying) _criticalFirePS.Stop();
        LockPlaneRotation();
    }
}
