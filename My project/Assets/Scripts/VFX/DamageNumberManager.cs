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

        DamageNumber dn = AcquirePooledInstance();
        dn.Play(worldPos, damage, isCrit, isPlayerDamage);

        // Kritik darbe görsel ve dokunsal geri bildirimi (Tok sarsıntı)
        if (isCrit && !isPlayerDamage)
        {
            CameraShake.Shake(0.14f, 0.10f);
        }
    }

    /// <summary>
    /// Dünyada özel metin (Mermi sıyırma, combo seviyesi vb.) uçuşan bildirimi oluşturur.
    /// </summary>
    public void SpawnCustomText(Vector3 worldPos, string text, Color color, float baseScale = 1.05f)
    {
        if (string.IsNullOrEmpty(text)) return;

        InitializePool();

        DamageNumber dn = AcquirePooledInstance();
        dn.PlayCustomText(worldPos, text, color, baseScale);
    }

    private DamageNumber AcquirePooledInstance()
    {
        DamageNumber dn;
        if (_pool.Count > 0)
        {
            dn = _pool.Dequeue();
        }
        else if (_activeList.Count > 0)
        {
            dn = _activeList[0];
            _activeList.RemoveAt(0);
        }
        else
        {
            dn = CreatePooledInstance();
            _pool.Dequeue();
        }

        _activeList.Add(dn);
        return dn;
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
}

