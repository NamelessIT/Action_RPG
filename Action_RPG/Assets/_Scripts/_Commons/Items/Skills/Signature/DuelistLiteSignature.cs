using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DuelistLiteSignature : SkillBehavior
{
    [Header("Buff Settings")]
    public float buffDuration = 5.0f;
    public float critChanceBuff = 1.0f;     // +100% (1.0)
    public float moveSpeedBuff = 0.2f;      // +20%
    public float attackSpeedBuff = 0.2f;    // +20%

    [Header("On-Hit Shred Settings")]
    public float shredDuration = 3.0f;      // Duy trì 3 giây
    public float shredPercentPerStack = 0.05f; // Trừ 5% mỗi stack
    public int maxStacks = 10;              // Tối đa 10 stack (50%)

    [Header("VFX")]
    public GameObject buffAuraVfxPrefab;    // Hào quang quanh người lúc gồng
    public GameObject shredVfxPrefab;       // Hiệu ứng giáp vỡ trên người địch

    private Coroutine buffCoroutine;
    private GameObject currentAuraVfx;
    private bool isBuffActive = false;

    // --- LỚP LƯU TRỮ DỮ LIỆU TRỪ GIÁP ---
    private class ShredData
    {
        public int stacks;
        public float totalArmorShredded;
        public Coroutine timerCoroutine;
        public GameObject vfxInstance;
    }

    // Từ điển theo dõi lượng giáp đã trừ của từng kẻ địch
    private Dictionary<Stats, ShredData> activeSunders = new Dictionary<Stats, ShredData>();

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        // Nếu tháo skill ra khỏi người thì dọn dẹp sạch sẽ
        RemoveBuff();
        CleanUpAllShreds();
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(BuffRoutine());

        return true;
    }

    private IEnumerator BuffRoutine()
    {
        // 1. ÁP DỤNG BUFF CHO BẢN THÂN
        if (!isBuffActive)
        {
            isBuffActive = true;
            stats.bonusCritChance += critChanceBuff;
            stats.bonusMoveSpeed += moveSpeedBuff;
            stats.bonusAttackSpeed += attackSpeedBuff;

            // Cập nhật hệ thống
            if (stats is AllyStats ally)
            {
                ally.CalculateCombatStatsOnly();
                ally.CalculateMoveSpeedOnly();
            }

            // Đăng ký lắng nghe sự kiện "Đánh trúng địch" từ PlayerController
            player.OnHitEnemy += HandleOnHitEnemy;
        }

        // VFX
        //if (currentAuraVfx != null) Destroy(currentAuraVfx);
        //if (buffAuraVfxPrefab != null) currentAuraVfx = Instantiate(buffAuraVfxPrefab, transform);

        Debug.Log("<color=cyan>DUELIST LITE: VŨ ĐIỆU PHÁ GIÁP KÍCH HOẠT!</color>");

        // 2. CHỜ HẾT THỜI GIAN BUFF
        yield return new WaitForSeconds(buffDuration);

        // 3. HẾT HẠN BUFF TỰ THÂN
        RemoveBuff();
    }

    private void RemoveBuff()
    {
        if (isBuffActive)
        {
            isBuffActive = false;
            stats.bonusCritChance -= critChanceBuff;
            stats.bonusMoveSpeed -= moveSpeedBuff;
            stats.bonusAttackSpeed -= attackSpeedBuff;

            if (stats is AllyStats allyRevert)
            {
                allyRevert.CalculateCombatStatsOnly();
                allyRevert.CalculateMoveSpeedOnly();
            }

            // Ngừng lắng nghe sự kiện đánh trúng địch
            player.OnHitEnemy -= HandleOnHitEnemy;

            //if (currentAuraVfx != null) Destroy(currentAuraVfx);
            Debug.Log("<color=gray>Duelist Lite: Đã hết trạng thái cường hóa.</color>");
        }
    }

    // ==========================================================
    // LOGIC TRỪ GIÁP (ĐƯỢC GỌI MỖI KHI ĐÁNH TRÚNG ĐỊCH)
    // ==========================================================
    private void HandleOnHitEnemy(Stats enemy, int stepIndex, bool isHeavy, bool isCrit)
    {
        if (enemy == null || enemy.currentHp <= 0) return;

        // Nếu kẻ địch chưa bị trừ giáp bao giờ, tạo hồ sơ mới cho nó
        if (!activeSunders.ContainsKey(enemy))
        {
            activeSunders[enemy] = new ShredData { stacks = 0, totalArmorShredded = 0f };

            if (shredVfxPrefab != null)
                activeSunders[enemy].vfxInstance = Instantiate(shredVfxPrefab, enemy.transform);
        }

        ShredData data = activeSunders[enemy];

        // 1. Dừng đồng hồ đếm ngược cũ
        if (data.timerCoroutine != null) StopCoroutine(data.timerCoroutine);

        // 2. Tính toán cộng dồn trừ giáp (Tối đa 10 stacks)
        if (data.stacks < maxStacks)
        {
            // [QUAN TRỌNG] Lấy lượng Giáp gốc của quái (trước khi bị chiêu này trừ) để tính 5%
            float baseArmor = enemy.armor + data.totalArmorShredded;
            float amountToShred = baseArmor * shredPercentPerStack;

            enemy.armor -= amountToShred;
            data.totalArmorShredded += amountToShred;
            data.stacks++;

            Debug.Log($"<color=orange>Phá Giáp!</color> {enemy.name} bị trừ {amountToShred} Giáp (Stack {data.stacks}/{maxStacks}).");
        }
        else
        {
            Debug.Log($"<color=orange>Phá Giáp!</color> {enemy.name} đã max 50% trừ giáp. Làm mới thời gian!");
        }

        // 3. Khởi động lại đồng hồ 3 giây
        data.timerCoroutine = StartCoroutine(RestoreArmorRoutine(enemy, shredDuration));
    }

    // ==========================================================
    // COROUTINE TRẢ LẠI GIÁP CHO QUÁI
    // ==========================================================
    private IEnumerator RestoreArmorRoutine(Stats enemy, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (activeSunders.ContainsKey(enemy))
        {
            ShredData data = activeSunders[enemy];

            // Trả lại đúng lượng giáp đã lấy đi
            if (enemy != null && enemy.currentHp > 0)
            {
                enemy.armor += data.totalArmorShredded;
                Debug.Log($"<color=gray>{enemy.name} đã khôi phục lại {data.totalArmorShredded} Giáp.</color>");
            }

            if (data.vfxInstance != null) Destroy(data.vfxInstance);
            activeSunders.Remove(enemy);
        }
    }

    // Đề phòng trường hợp chuyển map hoặc tháo skill đột ngột
    private void CleanUpAllShreds()
    {
        foreach (var kvp in activeSunders)
        {
            if (kvp.Key != null) kvp.Key.armor += kvp.Value.totalArmorShredded;
            if (kvp.Value.vfxInstance != null) Destroy(kvp.Value.vfxInstance);
        }
        activeSunders.Clear();
    }
}