using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RavagerSkill : SkillBehavior
{
    [Header("Berserk Settings")]
    public float duration = 5.0f;
    public float scanRange = 10.0f; // Tầm tìm địch để lao vào
    public float stopDistance = 0.8f; // Khoảng cách dừng lại để đánh

    [Header("Buffs")]
    public float shieldPercent = 0.5f;     // 50% Max HP Shield
    public float atkSpeedBonus = 2.0f;     // +200% Attack Speed (Tức là +2.0 vào attackSpeed)
    public float physAtkPercent = 2.0f;    // +200% Phys Damage
    public float critMultBonus = 0.2f;     // +20% Crit Damage
    public float moveSpeedBonus = 0.2f;    // +20% Move Speed

    [Header("Finisher")]
    public float healOnEndPercent = 0.3f; // Hồi 30% máu khi hết
    public float stunRadius = 2.0f;
    public float stunDuration = 1.5f;

    [Header("VFX")]
    public GameObject berserkerVfxPrefab;
    public GameObject finisherVfxPrefab;

    private EquipmentManager equipmentManager;
    private BloodReaverPassive bloodPassive; // Tham chiếu đến nội tại BloodReaver
    private float originalHpCost = 0f; // Lưu lại giá trị cũ để trả lại


    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();

        // Tìm component BloodReaverPassive trên người chơi (nếu có)
        bloodPassive = myPlayer.GetComponent<BloodReaverPassive>();
    }

    protected override void OnEquip() { }
    protected override void OnUnequip()
    {
        // Nếu đang bật mà tháo skill -> Tắt ngay
        if (player.isBerserk)
        {
            EndBerserk(false);
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(BerserkRoutine());
        return true;
    }

    private IEnumerator BerserkRoutine()
    {
        // 1. KÍCH HOẠT TRẠNG THÁI
        player.isBerserk = true; // Chiếm quyền điều khiển
        stats.isHealingBlocked = true; // Chặn hồi máu

        // 2. TẠO SHIELD
        float shieldAmount = stats.maxHp * shieldPercent;
        stats.currentShield += shieldAmount;

        // 3. CỘNG CHỈ SỐ (BUFF)
        ApplyBuffs();

        // 4. SỬA BLOOD REAVER PASSIVE (Cost = 0)
        if (bloodPassive != null)
        {
            originalHpCost = bloodPassive.hpCostPercent;
            bloodPassive.hpCostPercent = 0f; // Miễn phí máu
        }

        // VFX Bắt đầu
        //GameObject currentVfx = null;
        //if (berserkerVfxPrefab) currentVfx = Instantiate(berserkerVfxPrefab, transform);

        Debug.Log("<color=red>WARLORD: SAY MÁU!!!</color>");

        // 5. VÒNG LẶP AI (TỰ TÌM ĐỊCH)
        float timer = 0f;
        while (timer < duration)
        {
            // Tìm địch gần nhất
            Stats target = FindNearestEnemy();

            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);

                // Nếu trong tầm đánh (dựa trên vũ khí hoặc fix cứng)
                if (dist <= player.attackRange + 0.2f) // +0.2f sai số
                {
                    // ĐÁNH
                    // Quay mặt về địch
                    Vector3 dir = (target.transform.position - transform.position).normalized;
                    stats.facingDirection = dir;

                    player.AI_Attack();
                }
                else
                {
                    // DI CHUYỂN LẠI GẦN
                    player.AI_MoveTo(target.transform.position);
                }
            }
            else
            {
                // Không thấy địch -> Đứng yên hoặc đi lung tung (ở đây cho đứng yên thở dốc)
                // Hoặc có thể tự hủy Shield nếu muốn
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 6. KẾT THÚC
        EndBerserk(true);
        //if (currentVfx) Destroy(currentVfx);
    }

    private void EndBerserk(bool triggerFinisher)
    {
        player.isBerserk = false; // Trả quyền điều khiển
        stats.isHealingBlocked = false; // Cho hồi máu lại

        // Xóa Shield còn dư
        stats.currentShield = 0f;

        // Xóa Buff
        RemoveBuffs();

        // Trả lại nội tại Blood Reaver
        if (bloodPassive != null)
        {
            bloodPassive.hpCostPercent = originalHpCost;
        }

        if (triggerFinisher)
        {
            // 7. HỒI MÁU KẾT THÚC
            float healAmt = stats.maxHp * healOnEndPercent;
            stats.Heal(healAmt); // Dùng hàm Heal mới sửa trong Stats
            Debug.Log($"<color=green>Ravager End: Hồi {healAmt} HP</color>");

            // 8. GÂY CHOÁNG XUNG QUANH
            //if (finisherVfxPrefab) Instantiate(finisherVfxPrefab, transform.position, Quaternion.identity);

            Collider[] hits = Physics.OverlapSphere(transform.position, stunRadius, player.dangerLayer);
            foreach (var hit in hits)
            {
                Stats enemy = hit.GetComponent<Stats>();
                if (enemy != null && enemy.currentHp > 0)
                {
                    // Gây 1 ít sát thương nhẹ hoặc chỉ stun
                    DamageInfo info = new DamageInfo();
                    info.isStun = true;
                    info.stunDuration = stunDuration;
                    info.physDamage = 0; // Hoặc gây dmg nổ nếu muốn
                    info.attacker = stats;

                    enemy.TakeDamage(info);
                }
            }
        }
    }

    private void ApplyBuffs()
    {
        // Lưu lại lượng đã cộng để trừ ra chính xác
        //addedPhysAtk = stats.flatPhysicalAtk * physAtkPercent; // +200% base
        // Hoặc nếu muốn +200% tổng: stats.bonusPhysicalAtk += 2.0f;
        // Ở đây mình làm theo kiểu +Flat dựa trên base hiện có

        stats.bonusPhysicalAtk += physAtkPercent; // +200% 
        stats.bonusAttackSpeed += atkSpeedBonus;
        stats.bonusCritMultiplier += critMultBonus;
        stats.bonusMoveSpeed += moveSpeedBonus;

        stats.CalculateCombatStatsOnly();
        stats.CalculateMoveSpeedOnly();
    }

    private void RemoveBuffs()
    {
        stats.bonusPhysicalAtk -= physAtkPercent;
        stats.bonusAttackSpeed -= atkSpeedBonus;
        stats.bonusCritMultiplier -= critMultBonus;
        stats.bonusMoveSpeed -= moveSpeedBonus;

        stats.CalculateCombatStatsOnly();
        stats.CalculateMoveSpeedOnly();
    }

    private Stats FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, scanRange, player.dangerLayer);
        Stats nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            Stats s = hit.GetComponent<Stats>();
            if (s != null && s.currentHp > 0)
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = s;
                }
            }
        }
        return nearest;
    }
}