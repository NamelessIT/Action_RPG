using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Prefab Model (Visual) của Enemy. Nếu để trống, sẽ tự tạo Sprite mặc định.")]
    public GameObject enemyVisualPrefab;

    [Tooltip("Animator Controller tạm thời (Kéo Player Animator vào đây)")]
    public RuntimeAnimatorController placeholderAnimator;

    [Tooltip("Số lượng Enemy tối đa")]
    public int maxCount = 3;
    public float respawnTime = 10f;
    public float spawnRadius = 2.0f;

    [Header("Runtime Info")]
    public List<GameObject> spawnedEnemies = new List<GameObject>();

    private float currentRespawnTimer = 0f;
    private bool isRespawning = false;

    void Start()
    {
        SpawnMissingEnemies();
    }

    void Update()
    {
        CleanUpList();

        if (spawnedEnemies.Count < maxCount)
        {
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
            currentRespawnTimer = 0f;
        }
    }

    void SpawnMissingEnemies()
    {
        int countNeeded = maxCount - spawnedEnemies.Count;
        if (countNeeded <= 0) return;

        isRespawning = true;
        for (int i = 0; i < countNeeded; i++)
        {
            SpawnOneEnemy();
        }
        isRespawning = false;
    }

    void SpawnOneEnemy()
    {
        // 1. TÍNH VỊ TRÍ SPAWN
        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
        randomOffset.y = 0;
        Vector3 spawnPos = transform.position + randomOffset;

        // 2. TẠO OBJECT CHA (PARENT) - CHỨA LOGIC & PHYSICS
        GameObject parentObj = new GameObject($"Enemy_{System.Guid.NewGuid().ToString().Substring(0, 4)}");
        parentObj.transform.position = spawnPos;

        // 3. TẠO OBJECT CON (CHILD) - CHỨA VISUALS
        GameObject childObj;
        if (enemyVisualPrefab != null)
        {
            // Nếu có prefab model thì dùng nó
            childObj = Instantiate(enemyVisualPrefab, parentObj.transform);
        }
        else
        {
            // Nếu không thì tạo object rỗng
            childObj = new GameObject("Visuals");
            childObj.transform.SetParent(parentObj.transform);
        }

        // Đặt lại vị trí con về 0 so với cha
        childObj.transform.localPosition = Vector3.zero;
        childObj.transform.localRotation = Quaternion.identity;

        // 4. SETUP COMPONENT CHO CHA
        SetupParentComponents(parentObj);

        // 5. SETUP COMPONENT CHO CON
        SetupChildComponents(childObj);

        // 6. THÊM VÀO LIST QUẢN LÝ
        spawnedEnemies.Add(parentObj);
    }

    // --- SETUP CHA (Logic, Physics, AI) ---
    void SetupParentComponents(GameObject parent)
    {
        // A. Layer & Tag
        parent.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1) parent.layer = enemyLayer;

        // B. Rigidbody (Quan trọng: Freeze Rotation để không bị ngã)
        Rigidbody rb = parent.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = true; // AI điều khiển nên để Kinematic hoặc Dynamic tùy logic skill
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // C. Capsule Collider (Hitbox)
        CapsuleCollider col = parent.AddComponent<CapsuleCollider>();
        col.height = 2.0f;
        col.center = new Vector3(0, 1.0f, 0); // Đẩy lên để chân chạm đất
        col.radius = 0.5f;

        // D. NavMesh Agent
        NavMeshAgent agent = parent.AddComponent<NavMeshAgent>();
        agent.speed = 3.5f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 1.5f;

        // E. Scripts Logic
        // 1. Stats
        EnemyStats stats = parent.AddComponent<EnemyStats>();
        stats.spawnPosition = transform.position; // Gán điểm spawn

        // 2. Skill Manager
        SkillManager sm = parent.AddComponent<SkillManager>();
        sm.isPlayer = false; // Set cho Enemy

        // 3. Combat
        parent.AddComponent<EnemyCombat>();

        // 4. AI (Cần thêm cuối cùng)
        parent.AddComponent<EnemyAI>();
    }

    // --- SETUP CON (Visuals, Animation) ---
    void SetupChildComponents(GameObject child)
    {
        // A. Sprite Renderer
        SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
        if (sr == null) sr = child.AddComponent<SpriteRenderer>();

        // Nếu không có prefab, gán tạm sprite mặc định để nhìn thấy (tròn tròn trắng trắng)
        if (enemyVisualPrefab == null && sr.sprite == null)
        {
            // Tạo texture trắng tạm 
            // (Thực tế bạn nên gán Sprite trong Inspector của Prefab thì tốt hơn)
        }

        // B. Animator
        Animator anim = child.GetComponent<Animator>();
        if (anim == null) anim = child.AddComponent<Animator>();

        // Gán Controller của Player vào (Tạm thời)
        if (placeholderAnimator != null)
        {
            anim.runtimeAnimatorController = placeholderAnimator;
        }
    }

    void CleanUpList()
    {
        spawnedEnemies.RemoveAll(item => item == null);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}