using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Prefab của Enemy cần spawn (Phải có EnemyStats)")]
    public GameObject enemyPrefab;

    [Tooltip("Số lượng Enemy tối đa tại điểm này")]
    public int maxCount = 3;

    [Tooltip("Thời gian chờ để spawn lại sau khi bị giết (Giây)")]
    public float respawnTime = 10f;

    [Tooltip("Phạm vi ngẫu nhiên xung quanh điểm spawn (Để enemy không đứng chồng lên nhau)")]
    public float spawnRadius = 2.0f;

    [Header("Runtime Info")]
    // Danh sách theo dõi các enemy do spawner này tạo ra
    public List<GameObject> spawnedEnemies = new List<GameObject>();

    private float currentRespawnTimer = 0f;
    private bool isRespawning = false;

    void Start()
    {
        // Spawn lứa đầu tiên ngay lập tức
        SpawnMissingEnemies();
    }

    void Update()
    {
        // 1. Dọn dẹp danh sách (Xóa các enemy đã chết/null khỏi list)
        CleanUpList();

        // 2. Kiểm tra số lượng
        if (spawnedEnemies.Count < maxCount)
        {
            // Nếu thiếu quân -> Bắt đầu đếm ngược
            if (!isRespawning)
            {
                currentRespawnTimer += Time.deltaTime;
                if (currentRespawnTimer >= respawnTime)
                {
                    SpawnMissingEnemies();
                    currentRespawnTimer = 0f;
                }
            }
        }
        else
        {
            // Nếu đủ quân -> Reset timer
            currentRespawnTimer = 0f;
        }
    }

    void SpawnMissingEnemies()
    {
        if (enemyPrefab == null) return;

        int countNeeded = maxCount - spawnedEnemies.Count;
        if (countNeeded <= 0) return;

        isRespawning = true;

        for (int i = 0; i < countNeeded; i++)
        {
            SpawnOneEnemy();
        }

        Debug.Log($"[Spawner] Đã spawn lại {countNeeded} enemy tại {gameObject.name}");
        isRespawning = false;
    }

    void SpawnOneEnemy()
    {
        // Tính vị trí ngẫu nhiên xung quanh Spawner
        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
        randomOffset.y = 0; // Giữ nguyên độ cao (hoặc chỉnh theo terrain nếu cần)
        Vector3 spawnPos = transform.position + randomOffset;

        // Tạo Enemy
        GameObject newEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        // Setup thông tin Spawn cho EnemyStats
        EnemyStats stats = newEnemy.GetComponent<EnemyStats>();
        if (stats != null)
        {
            // [QUAN TRỌNG] Gán điểm spawn là vị trí của Spawner (để nó biết đường quay về)
            stats.spawnPosition = transform.position;
        }

        // Thêm vào danh sách quản lý
        spawnedEnemies.Add(newEnemy);
    }

    void CleanUpList()
    {
        // Xóa tất cả các phần tử null (đã bị Destroy)
        spawnedEnemies.RemoveAll(item => item == null);
    }

    // Vẽ Gizmos để dễ nhìn trong Editor
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.5f); // Màu xanh lá cây mờ
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.DrawIcon(transform.position, "d_S_Caver_Icon", true); // Vẽ icon (tùy chọn)
    }
}