using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DuelistLiteSignature : SkillBehavior
{
    [Header("Buff Settings")]
    public float buffDuration = 5.0f;
    public float critChanceBuff = 0.5f;     // +50%
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

    // --- LỚP LƯU TRỮ DỮ LIỆU SUNDER MANAGED ---
    private class ShredData
    {
        public Stats target;
        public int stacks;
        public float armorPerStack;
        public EffectHandle handle;
        public GameObject vfxInstance;
    }

    // F2E: gameplay stack count/VFX local; lifecycle/stat ownership nằm trong Debuff Registry.
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
    // LOGIC SUNDER MANAGED (ĐƯỢC GỌI MỖI KHI ĐÁNH TRÚNG ĐỊCH)
    // ==========================================================
    private void HandleOnHitEnemy(Stats enemy, int stepIndex, bool isHeavy, bool isCrit)
    {
        if (enemy == null || enemy.currentHp <= 0) return;

        if (!activeSunders.TryGetValue(enemy, out ShredData data))
        {
            // Legacy intent: mỗi stack = 5% "giáp gốc trước khi bị chính Sunder này trừ".
            // Snapshot tại stack đầu để 10 stack = đúng 50% snapshot base và không destructive-mutate raw Armor.
            data = new ShredData
            {
                target = enemy,
                stacks = 0,
                armorPerStack = Mathf.Max(0f, enemy.EffectiveArmor * shredPercentPerStack),
                handle = EffectHandle.Invalid
            };
            activeSunders[enemy] = data;
        }

        var request = new DebuffRequest
        {
            effectId = "DuelistLiteSignature.Sunder",
            duration = shredDuration,
            cleanseable = true,
            stackPolicy = EffectStackPolicy.AdditiveStacks,
            maxStacks = Mathf.Max(1, maxStacks),
            potency = data.armorPerStack,
            note = "DuelistLiteSignature Sunder: shared 3s refresh, 5% snapshot Armor per stack"
        };
        request.statModifiers.Add(new RuntimeStatModifierSpec(
            RuntimeStatKey.Armor, RuntimeModifierOperation.Flat, -data.armorPerStack));
        request.onRemove = (snapshot, reason) => HandleSunderRemoved(enemy);

        EffectApplyResult result = enemy.ApplyDebuff(
            request,
            stats,
            $"DuelistLiteSignature.Sunder:{GetInstanceID()}");

        if (!result.Accepted)
        {
            if (data.stacks == 0) activeSunders.Remove(enemy);
            return;
        }

        data.handle = result.handle;
        data.stacks = Mathf.Min(Mathf.Max(1, maxStacks), data.stacks + 1);

        if (data.vfxInstance == null && shredVfxPrefab != null)
            data.vfxInstance = Instantiate(shredVfxPrefab, enemy.transform);

        if (data.stacks < maxStacks)
            Debug.Log($"<color=orange>Phá Giáp!</color> {enemy.name} bị Sunder (Stack {data.stacks}/{maxStacks}).");
        else
            Debug.Log($"<color=orange>Phá Giáp!</color> {enemy.name} đạt {maxStacks} stack; hit tiếp theo chỉ refresh {shredDuration:0.##}s.");
    }

    private void HandleSunderRemoved(Stats enemy)
    {
        if (enemy == null || !activeSunders.TryGetValue(enemy, out ShredData data)) return;
        if (data.vfxInstance != null) Destroy(data.vfxInstance);
        activeSunders.Remove(enemy);
    }

    // Đề phòng trường hợp chuyển map hoặc tháo skill đột ngột: chỉ remove effect do skill này sở hữu.
    private void CleanUpAllShreds()
    {
        if (activeSunders.Count == 0) return;

        var snapshot = new List<ShredData>(activeSunders.Values);
        activeSunders.Clear();

        foreach (ShredData data in snapshot)
        {
            if (data == null) continue;
            if (data.vfxInstance != null) Destroy(data.vfxInstance);
            if (data.target == null || !data.handle.IsValid) continue;

            StatusEffectController controller = data.target.GetComponent<StatusEffectController>();
            if (controller != null)
                controller.RemoveEffect(data.handle, EffectRemoveReason.Manual);
        }
    }

}