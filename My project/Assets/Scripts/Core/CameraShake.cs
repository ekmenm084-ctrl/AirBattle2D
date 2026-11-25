using System.Collections;
using UnityEngine;

/// <summary>
/// Dinamik 2D Ekran Sarsıntısı (Screen Shake) Sistemi:
/// Mermi darbelerinde, uçak patlamalarında ve Boss imhalarında kameraya tok ve sinematik sarsıntı verir.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 _originalPos;
    private Coroutine _shakeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            _originalPos = new Vector3(0f, 0f, transform.position.z);
            transform.position = _originalPos;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    /// <summary>
    /// Eğer sahnede CameraShake yoksa otomatik olarak Main Camera'ya ekler.
    /// </summary>
    public static CameraShake GetOrAdd()
    {
        if (Instance != null) return Instance;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Instance = mainCam.GetComponent<CameraShake>();
            if (Instance == null)
            {
                Instance = mainCam.gameObject.AddComponent<CameraShake>();
            }
            Instance._originalPos = new Vector3(0f, 0f, mainCam.transform.position.z);
        }
        return Instance;
    }

    public static void StopAllAndReset()
    {
        if (Instance != null)
        {
            if (Instance._shakeCoroutine != null)
            {
                Instance.StopCoroutine(Instance._shakeCoroutine);
                Instance._shakeCoroutine = null;
            }
            Instance._originalPos = new Vector3(0f, 0f, Instance.transform.position.z);
            Instance.transform.position = Instance._originalPos;
        }
        else if (Camera.main != null)
        {
            Vector3 p = Camera.main.transform.position;
            Camera.main.transform.position = new Vector3(0f, 0f, p.z);
        }
    }

    public static void Shake(float intensity, float duration)
    {
        GetOrAdd()?.TriggerShake(intensity, duration);
    }

    public static void ShakeHit()
    {
        GetOrAdd()?.TriggerShake(0.12f, 0.12f);
    }

    public static void ShakeExplosion()
    {
        GetOrAdd()?.TriggerShake(0.32f, 0.28f);
    }

    public static void ShakeBossDeath()
    {
        GetOrAdd()?.TriggerShake(0.65f, 0.70f);
    }

    public void TriggerShake(float intensity, float duration)
    {
        if (!gameObject.activeInHierarchy) return;

        if (_shakeCoroutine != null)
            StopCoroutine(_shakeCoroutine);

        _shakeCoroutine = StartCoroutine(ShakeRoutine(intensity, duration));
    }

    private IEnumerator ShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float damper = 1f - (elapsed / duration); // Zamanla azalan sönümleme
            float offsetX = (Random.value * 2f - 1f) * intensity * damper;
            float offsetY = (Random.value * 2f - 1f) * intensity * damper;

            transform.position = new Vector3(_originalPos.x + offsetX, _originalPos.y + offsetY, _originalPos.z);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.position = _originalPos;
        _shakeCoroutine = null;
    }

    private void OnDisable()
    {
        transform.position = _originalPos;
    }
}

