using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uçuşan Hasar Sayıları ve Kritik Darbe Efekt Yöneticisi (Damage Number Manager).
/// Nesne havuzlama (Object Pooling) ile bellek tahsis etmeden akıcı 60 FPS hasar göstergesi sunar.
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }

    [Header("Havuz Ayarları")]
    public int initialPoolSize = 25;

    private readonly Queue<DamageNumber> _pool = new Queue<DamageNumber>();
    private readonly List<DamageNumber> _activeList = new List<DamageNumber>();
    private Transform _poolContainer;
    private Coroutine _hitStopCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializePool();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static DamageNumberManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("DamageNumberManager");
        if (go == null)
        {
            go = new GameObject("DamageNumberManager");
        }

        Instance = go.GetComponent<DamageNumberManager>();
        if (Instance == null)
        {
            Instance = go.AddComponent<DamageNumberManager>();
        }
        Instance.InitializePool();
        return Instance;
    }

    private void InitializePool()
    {
        if (_poolContainer != null) return;

        _poolContainer = new GameObject("-- Damage Numbers Pool --").transform;
        _poolContainer.SetParent(transform);

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledInstance();
        }
    }

    private DamageNumber CreatePooledInstance()
    {
        GameObject itemObj = new GameObject("DamageNum_" + (_pool.Count + _activeList.Count));
        itemObj.transform.SetParent(_poolContainer);
        itemObj.SetActive(false);

        DamageNumber dn = itemObj.AddComponent<DamageNumber>();
        dn.Initialize(this);
        _pool.Enqueue(dn);
        return dn;
    }

    /// <summary>
    /// Dünyada belirtilen koordinatta uçuşan hasar sayısı oluşturur.
    /// </summary>
    public void SpawnDamageNumber(Vector3 worldPos, float damage, bool isCrit, bool isPlayerDamage = false)
    {
        if (damage <= 0f) return;

        InitializePool();

        DamageNumber dn = null;
        if (_pool.Count > 0)
        {
            dn = _pool.Dequeue();
        }
        else if (_activeList.Count > 0)
        {
            // Havuz dolduysa en eski aktifi al
            dn = _activeList[0];
            _activeList.RemoveAt(0);
        }
        else
        {
            dn = CreatePooledInstance();
            _pool.Dequeue(); // Az önce eklendi
        }

        _activeList.Add(dn);
        dn.Play(worldPos, damage, isCrit, isPlayerDamage);

        // Kritik darbe görsel ve dokunsal geri bildirimi
        if (isCrit && !isPlayerDamage)
        {
            CameraShake.Shake(0.18f, 0.14f);
            TriggerHitStop(0.04f); // Micro Hit-Stop (Visceral Impact)
        }
    }

    public void ReturnToPool(DamageNumber dn)
    {
        if (_activeList.Contains(dn))
        {
            _activeList.Remove(dn);
        }

        if (!_pool.Contains(dn))
        {
            _pool.Enqueue(dn);
        }
    }

    /// <summary>
    /// Kritik vuruşlarda arcade dövüş oyunları hissi veren mikro duraksama (Micro Hit-Stop).
    /// </summary>
    public void TriggerHitStop(float duration)
    {
        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
        }
        _hitStopCoroutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        float prevScale = Time.timeScale;
        // Eğer oyun zaten duraklatıldıysa TimeScale'i değiştirme
        if (prevScale > 0.05f)
        {
            Time.timeScale = 0.08f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = prevScale;
        }
        _hitStopCoroutine = null;
    }

    private void OnDestroy()
    {
        if (Time.timeScale < 0.1f)
        {
            Time.timeScale = 1.0f;
        }
    }
}

