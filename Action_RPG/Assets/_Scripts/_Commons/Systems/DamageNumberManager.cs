using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton quản lý hiển thị số sát thương nổi lên màn hình.
/// Dùng Object Pool để tránh Instantiate liên tục.
///
/// Cách dùng (từ Stats.cs):
///   DamageNumberManager.Show(info, transform.position);
///
/// Setup: Kéo prefab DamageNumberPopup vào _popupPrefab trong Inspector.
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  SINGLETON
    // ─────────────────────────────────────────────────────────────
    public static DamageNumberManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    //  CONFIG
    // ─────────────────────────────────────────────────────────────
    [Header("Pool")]
    [SerializeField] private DamageNumberPopup _popupPrefab;
    [SerializeField] private int               _poolSize = 20;

    [Header("Offset Y — vị trí spawn so với chân nhân vật")]
    [SerializeField] private float _targetHeightOffset = 1.0f;

    // ─────────────────────────────────────────────────────────────
    //  POOL
    // ─────────────────────────────────────────────────────────────
    private readonly Queue<DamageNumberPopup> _pool = new Queue<DamageNumberPopup>();

    // ─────────────────────────────────────────────────────────────
    //  UNITY
    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        PrewarmPool();
    }

    // ─────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Hiển thị số sát thương tại vị trí worldPos.
    /// Gọi từ Stats.TakeDamage.
    /// </summary>
    public static void Show(DamageInfo info, Vector3 worldPos)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[DMG] Instance NULL — chưa có DamageNumberManager trong scene!");
            return;
        }
        Debug.Log($"[DMG] Show called | phys={info.physDamage:F1} mag={info.magicDamage:F1} true={info.trueDamage:F1} pos={worldPos}");
        Instance.SpawnPopup(info, worldPos);
    }

    /// <summary>Trả popup về pool sau khi hết vòng đời.</summary>
    public void ReturnToPool(DamageNumberPopup popup)
    {
        popup.ResetPopup();
        _pool.Enqueue(popup);
    }

    // ─────────────────────────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────────────────────────
    private void PrewarmPool()
    {
        if (_popupPrefab == null)
        {
            Debug.LogError("[DamageNumberManager] _popupPrefab chưa được gán! Kéo prefab vào Inspector.");
            return;
        }

        for (int i = 0; i < _poolSize; i++)
        {
            DamageNumberPopup popup = Instantiate(_popupPrefab, transform);
            popup.ResetPopup();
            _pool.Enqueue(popup);
        }
    }

    private void SpawnPopup(DamageInfo info, Vector3 worldPos)
    {
        if (_popupPrefab == null)
        {
            Debug.LogError("[DMG] _popupPrefab NULL — kéo prefab DamageNumberPopup vào Inspector của DamageNumberManager!");
            return;
        }

        if (_pool.Count == 0)
        {
            DamageNumberPopup extra = Instantiate(_popupPrefab, transform);
            extra.ResetPopup();
            _pool.Enqueue(extra);
        }

        DamageNumberPopup next = _pool.Dequeue();
        Vector3 spawnPos = worldPos + Vector3.right * Random.Range(-0.2f, 0.2f);
        Debug.Log($"[DMG] Spawning popup at {spawnPos} | active={next.gameObject.activeSelf}");
        next.Show(info, spawnPos);
        Debug.Log($"[DMG] After Show active={next.gameObject.activeSelf} | lines={next.transform.childCount}");
    }
}
