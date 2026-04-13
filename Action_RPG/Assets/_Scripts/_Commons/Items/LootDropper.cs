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
        if (_lootTable == null)
        {
            Debug.LogWarning($"[LootDropper] _lootTable is null on {gameObject.name}. Skipping loot drop.");
            return;
        }

        var rolledItems = _lootTable.RollLoot();
        foreach (var entry in rolledItems)
        {
            if (_droppedItemPrefab == null)
            {
                Debug.LogWarning($"[LootDropper] _droppedItemPrefab is null on {gameObject.name}. Skipping entry '{entry.displayName}'.");
                continue;
            }

            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * _spawnRadius;
            Vector3 spawnPos = new Vector3(
                transform.position.x + randomOffset.x,
                transform.position.y,
                transform.position.z + randomOffset.z
            );

            GameObject spawned = Instantiate(_droppedItemPrefab, spawnPos, Quaternion.identity);
            var droppedBehaviour = spawned.GetComponent<DroppedItemBehaviour>();
            if (droppedBehaviour != null)
                droppedBehaviour.Initialize(entry);
        }
    }
}
