using System.Collections;
using UnityEngine;

/// <summary>
/// Oyun İçi Canlı Taktik Fırtına & Sonsuz Konveyör Akış Sistemi.
/// Sahnedeki orijinal SpriteRenderer'ı ve URP materyalini korur; mavi ekran oluşmasını kesinlikle engeller.
/// İki kardeş nesne olarak kusursuzca akar ve çatışma esnasında atmosferik şimşek çakmaları oluşturur.
/// </summary>
public class BackgroundScroller : MonoBehaviour
{
    [Header("Akış Ayarları")]
    public float scrollSpeed = 1.6f;             // Dünya birimi akış hızı
    public bool enableSway = true;               // Sinematik süzülme
    public float swayAmount = 0.12f;             // Süzülme genliği
    public float swayFrequency = 0.45f;          // Süzülme frekansı

    [Header("Şimşek Ayarları")]
    public bool enableLightning = true;
    public float minLightningInterval = 5f;
    public float maxLightningInterval = 10f;
    public Color normalColor = Color.white;
    public Color lightningTint = new Color(0.72f, 0.90f, 1.0f, 1f);

    private SpriteRenderer _sr1;
    private SpriteRenderer _sr2;
    private GameObject _layer2Obj;

    private float _spriteHeightWorld;
    private Coroutine _lightningCoroutine;

    private void Start()
    {
        SetupWorldScroller();
        if (enableLightning && gameObject.activeInHierarchy)
        {
            _lightningCoroutine = StartCoroutine(CombatLightningRoutine());
        }
    }

    private void OnDestroy()
    {
        if (_lightningCoroutine != null)
        {
            StopCoroutine(_lightningCoroutine);
            _lightningCoroutine = null;
        }
        if (_layer2Obj != null)
        {
            Destroy(_layer2Obj);
        }
    }

    private void SetupWorldScroller()
    {
        // 1. Kök nesnedeki orijinal SpriteRenderer'ı al (Daima AKTİF tut, URP materyali bozulmasın)
        _sr1 = GetComponent<SpriteRenderer>();
        if (_sr1 == null || _sr1.sprite == null) return;
        _sr1.enabled = true;
        _sr1.color = normalColor;

        Camera cam = Camera.main;
        if (cam == null) return;

        float camHeight = cam.orthographicSize * 2f;
        float camWidth = camHeight * ((float)Screen.width / Mathf.Max(1, Screen.height));

        float spriteW = _sr1.sprite.rect.width / _sr1.sprite.pixelsPerUnit;
        float spriteH = _sr1.sprite.rect.height / _sr1.sprite.pixelsPerUnit;

        // Ekranı kenarlarda hiç boşluk bırakmadan güvenle kaplayacak ölçek
        float scaleX = (camWidth / spriteW) * 1.10f;
        float scaleY = (camHeight / spriteH) * 1.05f;
        float maxScale = Mathf.Max(scaleX, scaleY);

        transform.localScale = new Vector3(maxScale, maxScale, 1f);
        transform.position = new Vector3(0f, 0f, 2f);

        _spriteHeightWorld = spriteH * maxScale;

        // 2. İkinci Katman (Kardeş nesne olarak sahnede oluşturulur, ebeveyn hareketiyle çakışmaz)
        _layer2Obj = new GameObject("GameBackground_CombatLayer2");
        _layer2Obj.transform.localScale = new Vector3(maxScale, maxScale, 1f);
        _layer2Obj.transform.position = new Vector3(0f, _spriteHeightWorld, 2f);

        _sr2 = _layer2Obj.AddComponent<SpriteRenderer>();
        _sr2.sprite = _sr1.sprite;
        _sr2.sharedMaterial = _sr1.sharedMaterial; // URP materyali kesinlikle kopyalanır!
        _sr2.sortingLayerID = _sr1.sortingLayerID;
        _sr2.sortingOrder = _sr1.sortingOrder;
        _sr2.color = normalColor;
    }

    private void Update()
    {
        if (_sr1 == null || _sr2 == null || _spriteHeightWorld <= 0.1f) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 pos1 = transform.position;
        Vector3 pos2 = _layer2Obj.transform.position;

        pos1.y -= scrollSpeed * dt;
        pos2.y -= scrollSpeed * dt;

        // Katman 1 alt sınıra indiğinde Katman 2'nin üstüne atlar
        if (pos1.y <= -_spriteHeightWorld)
        {
            pos1.y = pos2.y + _spriteHeightWorld;
        }

        // Katman 2 alt sınıra indiğinde Katman 1'in üstüne atlar
        if (pos2.y <= -_spriteHeightWorld)
        {
            pos2.y = pos1.y + _spriteHeightWorld;
        }

        // Sinematik yatay süzülme
        float sway = 0f;
        if (enableSway)
        {
            sway = Mathf.Sin(Time.time * swayFrequency) * swayAmount;
        }

        pos1.x = sway;
        pos2.x = sway;

        transform.position = pos1;
        _layer2Obj.transform.position = pos2;
    }

    private IEnumerator CombatLightningRoutine()
    {
        while (true)
        {
            float waitSec = Random.Range(minLightningInterval, maxLightningInterval);
            yield return new WaitForSeconds(waitSec);

            yield return StartCoroutine(FlashSequence());
        }
    }

    private IEnumerator FlashSequence()
    {
        if (_sr1 == null || _sr2 == null) yield break;

        // 1. Öncü Darbe (Pre-strike)
        SetSpriteColors(Color.Lerp(normalColor, lightningTint, 0.40f));
        yield return new WaitForSeconds(0.04f);
        SetSpriteColors(normalColor);
        yield return new WaitForSeconds(0.06f);

        // 2. Ana Parlama (Bright Flash)
        SetSpriteColors(Color.white * 1.30f);
        yield return new WaitForSeconds(0.08f);

        // 3. Art Parıltı Titremesi (Afterglow Flicker)
        SetSpriteColors(Color.Lerp(normalColor, lightningTint, 0.45f));
        yield return new WaitForSeconds(0.05f);
        SetSpriteColors(Color.Lerp(normalColor, lightningTint, 0.20f));
        yield return new WaitForSeconds(0.07f);

        // 4. Doğal Sönümlenme (Smooth Fade)
        float elapsed = 0f;
        float fadeDur = 0.25f;
        Color startCol = _sr1 != null ? _sr1.color : normalColor;
        while (elapsed < fadeDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDur;
            SetSpriteColors(Color.Lerp(startCol, normalColor, t));
            yield return null;
        }
        SetSpriteColors(normalColor);
    }

    private void SetSpriteColors(Color col)
    {
        if (_sr1 != null) _sr1.color = col;
        if (_sr2 != null) _sr2.color = col;
    }
}
