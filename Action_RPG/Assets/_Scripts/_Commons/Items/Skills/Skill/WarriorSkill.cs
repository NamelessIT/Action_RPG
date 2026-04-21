using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WarriorSkill : SkillBehavior
{
    [Header("Skill Settings")]
    public float chargeTime = 1.0f; // Thời gian gồng (1s)

    [Header("AOE Settings (Rectangular)")]
    public float boxWidth = 1.5f;   // Chiều ngang vùng đập
    public float boxLength = 3.0f;  // Chiều dài vùng đập

    [Header("Effects")]
    public float stunDuration = 2.0f;     // Choáng 2s
    public float armorBreakPercent = 0.5f;// Giảm 50% giáp
    public float armorBreakDuration = 5f; // Trong 5s

    private Rigidbody rb;
    private EquipmentManager equipmentManager;

    // Dictionary để quản lý việc giảm giáp (Theo code của bạn)
    private Dictionary<Stats, Coroutine> activeDefReductionCoroutines = new Dictionary<Stats, Coroutine>();

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip()
    {
        // Khởi tạo lại list nếu cần
        activeDefReductionCoroutines.Clear();
    }

    protected override void OnUnequip()
    {
        // [QUAN TRỌNG] Khi tháo skill, phải trả lại giáp ngay cho những đứa đang bị trừ
        // Nếu không chúng sẽ bị yếu vĩnh viễn
        List<Stats> keys = new List<Stats>(activeDefReductionCoroutines.Keys);
        foreach (Stats target in keys)
        {
            if (target != null && activeDefReductionCoroutines.ContainsKey(target))
            {
                // Dừng coroutine đang chạy
                StopCoroutine(activeDefReductionCoroutines[target]);

                // Vì không lưu originalArmor vào Dictionary nên ta không biết chính xác số cũ là bao nhiêu
                // Nhưng ta biết công thức giảm là: current = original * (1 - percent)
                // => original = current / (1 - percent)
                target.armor = target.armor / (1f - armorBreakPercent);
            }
        }
        activeDefReductionCoroutines.Clear();
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(SlamRoutine());
        return true;
    }

    private IEnumerator SlamRoutine()
    {
        // 1. KHÓA DI CHUYỂN
        player.isAttacking = true;
        rb.linearVelocity = Vector3.zero;

        // [Optional] Play Animation Gồng
        // animator.SetTrigger("WarriorCharge");
        Debug.Log("Warrior: Đang gồng...");

        // 2. GỒNG (CHARGE)
        yield return new WaitForSeconds(chargeTime);

        // 3. ĐẬP (SLAM)
        // Play Animation Đập
        // animator.SetTrigger("WarriorSlam");

        // 4. XỬ LÝ VA CHẠM
        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;

        Vector3 center = transform.position + (forward * boxLength * 0.5f);
        Vector3 size = new Vector3(boxWidth, 2f, boxLength);
        Quaternion orientation = Quaternion.LookRotation(forward);

        Collider[] hits = Physics.OverlapBox(center, size * 0.5f, orientation, player.dangerLayer);

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                ApplySlamEffect(enemyStats);
            }
        }

        // Chờ animation đập xong (0.5s)
        yield return new WaitForSeconds(0.5f);

        // 5. MỞ KHÓA
        player.isAttacking = false;
        Debug.Log("Warrior: Đập xong!");
    }

    private void ApplySlamEffect(Stats enemyStats)
    {
        stats.EnterCombat();

        // 1. Gây Sát Thương & Stun
        WeaponData currentWpn = equipmentManager.currentWeapon;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
        bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
        float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);

        var dmgTuple = CombatMath.CalculateFullDamage(
            stats, enemyStats, t, isCrit, data, currentWpn, 1f
        );

        DamageInfo info = new DamageInfo();
        info.sourcePosition = transform.position;
        info.isCrit = isCrit;
        info.physDamage = dmgTuple.phys;
        info.magicDamage = dmgTuple.magic;
        info.trueDamage = dmgTuple.trueDmg;
        info.isStun = true;
        info.stunDuration = stunDuration;
        info.attacker = stats;

        enemyStats.TakeDamage(info);

        // 2. Phá Giáp (Sử dụng hàm của bạn)
        ApplyDefenseReduction(enemyStats, armorBreakPercent, armorBreakDuration);
    }

    // --- LOGIC CỦA BẠN (Đã tích hợp) ---
    private void ApplyDefenseReduction(Stats target, float reductionPercent, float duration)
    {
        // Chỉ trừ nếu chưa bị trừ (để tránh cộng dồn sai)
        if (!activeDefReductionCoroutines.ContainsKey(target))
        {
            float originalArmor = target.armor;
            target.armor *= (1f - reductionPercent);

            Debug.Log($"<color=orange>{target.name} bị Phá Giáp! Armor: {originalArmor} -> {target.armor}</color>");

            Coroutine coro = StartCoroutine(RevertDefenseAfterDelay(target, originalArmor, duration));
            activeDefReductionCoroutines[target] = coro;
        }
    }

    private IEnumerator RevertDefenseAfterDelay(Stats target, float originalValue, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Kiểm tra điều kiện an toàn trước khi trả
        if (target != null && activeDefReductionCoroutines.ContainsKey(target))
        {
            target.armor = originalValue;
            activeDefReductionCoroutines.Remove(target);
            Debug.Log($"[Warrior] 🛡️ Defense reverted for {target.name}");
        }
    }
    // -----------------------------------
    // Vẽ Gizmos để căn chỉnh vùng va chạm (Slam Area)
    void OnDrawGizmosSelected()
    {
        // 1. Xác định hướng (Forward) giống như trong SlamRoutine
        // Nếu game đang chạy, ưu tiên facingDirection của stats
        Vector3 forward = transform.forward;
        if (Application.isPlaying && stats != null && stats.facingDirection != Vector3.zero)
        {
            forward = stats.facingDirection;
        }

        // 2. Tính toán vị trí tâm của Box (Center)
        // Tâm của OverlapBox nằm ở phía trước nhân vật một khoảng bằng nửa chiều dài box
        Vector3 center = transform.position + (forward * boxLength * 0.5f);

        // 3. Thiết lập ma trận vẽ để xoay Box theo hướng nhân vật
        Gizmos.color = Color.red;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(forward), Vector3.one);

        // 4. Vẽ Box (Size khớp với size trong OverlapBox)
        // Lưu ý: Chiều cao (y) để là 2f như trong code xử lý va chạm của bạn
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxWidth, 2f, boxLength));

        // Khôi phục lại ma trận mặc định
        Gizmos.matrix = oldMatrix;
    }
}