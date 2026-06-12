using UnityEngine;

// Gọi hàm này từ EnemyController.OnDeath() hoặc Stats override
// Stats.cs hiện không có OnDeath event nên không subscribe tự động.
public class LootDropper : MonoBehaviour
{
    [SerializeField] private LootTable _lootTable;
    [SerializeField] private GameObject _droppedItemPrefab;
    [SerializeField] private float _spawnRadius = 0.5f;

    public void OnEnemyDeath()
    {
        // ── CHECKPOINT 1: LootDropper được gọi ───────────────────────────
        Debug.Log($"[LootDropper] ▶ OnEnemyDeath() called on '{gameObject.name}'");

        if (_lootTable == null)
        {
            Debug.LogWarning($"[LootDropper] ❌ _lootTable là NULL trên '{gameObject.name}'. " +
                             "Kéo LootTable ScriptableObject vào field trong Inspector.");
            return;
        }

        if (_droppedItemPrefab == null)
        {
            Debug.LogWarning($"[LootDropper] ❌ _droppedItemPrefab là NULL trên '{gameObject.name}'. " +
                             "Kéo DroppedItem Prefab vào field trong Inspector.");
            return;
        }

        // ── CHECKPOINT 2: Roll loot ───────────────────────────────────────
        var rolledItems = _lootTable.RollLoot();
        Debug.Log($"[LootDropper] 🎲 RollLoot → {rolledItems.Count} item(s) dropped " +
                  $"từ '{_lootTable.name}'");

        if (rolledItems.Count == 0)
        {
            Debug.Log("[LootDropper] ℹ Không có item nào roll thành công lần này (xác suất).");
            return;
        }

        // ── CHECKPOINT 3: Spawn từng item ────────────────────────────────
        foreach (var entry in rolledItems)
        {
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * _spawnRadius;
            Vector3 spawnPos = new Vector3(
                transform.position.x + randomOffset.x,
                transform.position.y,
                transform.position.z + randomOffset.z
            );

            GameObject spawned = Instantiate(_droppedItemPrefab, spawnPos, Quaternion.identity);
            Debug.Log($"[LootDropper] ✅ Spawned '{entry.displayName}' tại {spawnPos}");

            var droppedBehaviour = spawned.GetComponent<DroppedItemBehaviour>();
            if (droppedBehaviour != null)
            {
                droppedBehaviour.Initialize(entry);
            }
            else
            {
                Debug.LogWarning($"[LootDropper] ⚠ Prefab '{_droppedItemPrefab.name}' thiếu component " +
                                 "'DroppedItemBehaviour' → item spawn nhưng không nhặt được! " +
                                 "Add component DroppedItemBehaviour vào prefab.");
            }

            // ── CHECKPOINT 4: Kiểm tra Collider và Layer ─────────────────
            Collider col = spawned.GetComponent<Collider>();
            if (col == null)
                Debug.LogWarning($"[LootDropper] ⚠ Prefab '{_droppedItemPrefab.name}' không có Collider → " +
                                 "ItemPickupManager không thể detect! Thêm SphereCollider vào prefab.");
            else
                Debug.Log($"[LootDropper]   Collider: {col.GetType().Name} | Layer: " +
                          $"'{LayerMask.LayerToName(spawned.layer)}' (index {spawned.layer})");
        }
    }
}
