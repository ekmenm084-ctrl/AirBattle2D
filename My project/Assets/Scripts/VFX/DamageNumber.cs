using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Uçuşan Taktik Hasar Sayısı (Floating Damage Number):
/// Mermi veya patlama isabetinde hedeften yukarıya doğru süzülür, kritik vuruşlarda
/// özel altın renk, büyük font ve punch-scale efekti sergiler.
/// </summary>
public class DamageNumber : MonoBehaviour
{
    private TextMeshPro _tmp;
    private DamageNumberManager _manager;
    private Coroutine _animCoroutine;

    private void Awake()
    {
        EnsureTMP();
    }

    private void EnsureTMP()
    {
        if (_tmp != null) return;

        _tmp = GetComponent<TextMeshPro>();
        if (_tmp == null)
        {
            _tmp = gameObject.AddComponent<TextMeshPro>();
        }

        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.sortingOrder = 35; // Uçak ve mermilerin üstünde net görünür
        _tmp.extraPadding = true;
    }

    public void Initialize(DamageNumberManager manager)
    {
        _manager = manager;
        EnsureTMP();
    }

    public void Play(Vector3 startPos, float damage, bool isCrit, bool isPlayerDamage = false)
    {
        EnsureTMP();

        if (_animCoroutine != null)
        {
            StopCoroutine(_animCoroutine);
        }

        transform.position = startPos;
        gameObject.SetActive(true);

        _animCoroutine = StartCoroutine(AnimateRoutine(damage, isCrit, isPlayerDamage));
    }

    private IEnumerator AnimateRoutine(float damage, bool isCrit, bool isPlayerDamage)
    {
        int displayVal = Mathf.Max(1, Mathf.RoundToInt(damage));
        float duration = isCrit ? 0.95f : 0.65f;
        float baseScale = isCrit ? 1.45f : 1.0f;
        float floatSpeedY = isCrit ? 2.2f : 1.6f;
        float driftX = Random.Range(-0.45f, 0.45f);

        Color startColor;
        if (isPlayerDamage)
        {
            _tmp.text = "-" + displayVal;
            _tmp.fontSize = 3.2f;
            _tmp.fontStyle = FontStyles.Bold;
            startColor = new Color(1f, 0.28f, 0.28f, 1f); // Kırmızı (Oyuncunun aldığı hasar)
        }
        else if (isCrit)
        {
            _tmp.text = "-" + displayVal + "! KRİTİK";
            _tmp.fontSize = 4.2f;
            _tmp.fontStyle = FontStyles.Bold;
            startColor = new Color(1f, 0.78f, 0.12f, 1f); // Parlak Altın Sarısı
        }
        else
        {
            _tmp.text = displayVal.ToString();
            _tmp.fontSize = 2.9f;
            _tmp.fontStyle = FontStyles.Normal;
            startColor = new Color(1f, 0.96f, 0.75f, 1f); // Açık krem / beyaz
        }

        Vector3 startScale = Vector3.one * (baseScale * (isCrit ? 1.7f : 1.25f));
        Vector3 targetScale = Vector3.one * baseScale;
        transform.localScale = startScale;

        float elapsed = 0f;
        Vector3 currentPos = transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Başlangıçta ani büyüme (Punch Pop), ardından hedefe oturma
            if (t < 0.15f)
            {
                float popT = t / 0.15f;
                transform.localScale = Vector3.Lerp(startScale, targetScale, popT);
            }
            else
            {
                transform.localScale = targetScale;
            }

            // Yukarı doğru süzülme ve hafif yatay sapma
            currentPos.y += floatSpeedY * Time.deltaTime * (1f - t * 0.45f);
            currentPos.x += driftX * Time.deltaTime;
            transform.position = currentPos;

            // Son %40'lık kısımda yumuşak alfa sönümlenmesi (Fade Out)
            float alpha = 1f;
            if (t > 0.6f)
            {
                alpha = Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            }
            _tmp.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            yield return null;
        }

        gameObject.SetActive(false);
        _animCoroutine = null;

        if (_manager != null)
        {
            _manager.ReturnToPool(this);
        }
    }
}

