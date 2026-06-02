using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MageSkill : SkillBehavior
{
    [Header("Empower Settings")]
    public float buffDuration = 5.0f;

    [Header("Default (No Grimoire / No Direction)")]
    public float defaultBonusDamage = 2.5f;
    public float defaultStunDuration = 1.5f;

    [Header("VFX Prefabs (Gắn vào đây)")]
    public GameObject empowerAuraVfx;
    public GameObject blastVfx;  // Dùng chung cho nổ phép

    // Trạng thái hệ thống
    private bool isEmpowered = false;
    private Coroutine buffCoroutine;
    private GameObject currentAuraVfx;

    // Tham chiếu
    private PlayerController playerController;
    private EquipmentManager equipmentManager;
    private bool isGrimoireEquipped = false;
    private Direction8 currentDirection = Direction8.None;
    private string requiredWeaponName = "Grimoire";

    private enum Direction8
    {
        None = 0, N = 1, NE = 2, E = 3, SE = 4,
        S = 5, SW = 6, W = 7, NW = 8
    }

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        // [FIX] Gán ref TRƯỚC base.Initialize() vì base gọi OnEquip() ngay lập tức.
        // Nếu gán sau, OnEquip() thấy playerController = null → không subscribe được events.
        playerController = myPlayer;
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip()
    {
        if (playerController != null)
        {
            playerController.OnMovementInputChanged += OnMovementInput;
            playerController.OnAttackPerformed += OnAttackPerformed;
            playerController.OnWeaponEquipped += OnWeaponEquipped;
        }
        CheckGrimoireEquipped(equipmentManager?.currentWeapon);
    }

    protected override void OnUnequip()
    {
        if (playerController != null)
        {
            playerController.OnMovementInputChanged -= OnMovementInput;
            playerController.OnAttackPerformed -= OnAttackPerformed;
            playerController.OnWeaponEquipped -= OnWeaponEquipped;
        }
        RemoveBuff();
    }

    // ==========================================================
    // LẮNG NGHE INPUT & VŨ KHÍ
    // ==========================================================
    private void OnMovementInput(Vector2 input)
    {
        if (input == Vector2.zero)
        {
            currentDirection = Direction8.None;
            return;
        }

        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        if (angle >= 337.5f || angle < 22.5f) currentDirection = Direction8.E;
        else if (angle >= 22.5f && angle < 67.5f) currentDirection = Direction8.NE;
        else if (angle >= 67.5f && angle < 112.5f) currentDirection = Direction8.N;
        else if (angle >= 112.5f && angle < 157.5f) currentDirection = Direction8.NW;
        else if (angle >= 157.5f && angle < 202.5f) currentDirection = Direction8.W;
        else if (angle >= 202.5f && angle < 247.5f) currentDirection = Direction8.SW;
        else if (angle >= 247.5f && angle < 292.5f) currentDirection = Direction8.S;
        else if (angle >= 292.5f && angle < 337.5f) currentDirection = Direction8.SE;
    }

    private void OnWeaponEquipped(WeaponData weapon) { CheckGrimoireEquipped(weapon); }

    private void CheckGrimoireEquipped(WeaponData weapon)
    {
        isGrimoireEquipped = (weapon != null && weapon.weaponName == requiredWeaponName);
    }

    // ==========================================================
    // KÍCH HOẠT SKILL (BẬT BUFF)
    // ==========================================================
    public override bool Use()
    {
        if (!base.Use()) return false;

        isEmpowered = true;

        //if (currentAuraVfx != null) Destroy(currentAuraVfx);
        //if (empowerAuraVfx != null) currentAuraVfx = Instantiate(empowerAuraVfx, transform);

        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(BuffTimeoutRoutine());

        Debug.Log("<color=cyan>MAGE SKILL: BẬT CƯỜNG HÓA!</color>");
        return true;
    }

    private IEnumerator BuffTimeoutRoutine()
    {
        yield return new WaitForSeconds(buffDuration);
        RemoveBuff();
        Debug.Log("<color=gray>MAGE SKILL: Hết cường hóa.</color>");
    }

    private void RemoveBuff()
    {
        isEmpowered = false;
        //if (currentAuraVfx != null) Destroy(currentAuraVfx);
        if (buffCoroutine != null) { StopCoroutine(buffCoroutine); buffCoroutine = null; }
    }

    // ==========================================================
    // BẮT SỰ KIỆN ĐÁNH ĐỂ XẢ BUFF
    // ==========================================================
    private void OnAttackPerformed(int stepIndex, bool isHeavy)
    {
        if (!isEmpowered) return;

        RemoveBuff(); // Tiêu hao buff ngay lập tức

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();

        if (!isGrimoireEquipped || currentDirection == Direction8.None)
        {
            // --- ĐÒN CƠ BẢN ---
            Debug.Log("<color=orange>MAGE: Vụ Nổ Phép Thuật (Default)!</color>");
            DealEmpoweredAoe(
                center: transform.position + forward * 1.5f,
                radius: 2.0f,
                dmgMult: defaultBonusDamage,
                stunTime: defaultStunDuration
            );
        }
        else
        {
            // --- 8 ĐÒN CƯỜNG HÓA VỚI GRIMOIRE ---
            switch (currentDirection)
            {
                case Direction8.N: // HELLFIRE: Nổ to x3, Thiêu đốt 5s
                    Debug.Log("<color=red>MAGE (N): HELLFIRE!</color>");
                    DealEmpoweredAoe(transform.position + forward * 3f, 4f, 3.0f, 0f, stats.magicAtk * 0.1f, 4f);
                    break;

                case Direction8.NE: // GRAND HURRICANE: Nổ diện rộng
                    Debug.Log("<color=red>MAGE (NE): GRAND HURRICANE!</color>");
                    DealEmpoweredAoe(transform.position, 5f, 2.0f, 0.5f);
                    break;

                case Direction8.E: // TAILWIND: Buff siêu tốc cho bản thân (Không nổ)
                    Debug.Log("<color=green>MAGE (E): TAILWIND (+Tốc chạy/Tốc đánh)!</color>");
                    StartCoroutine(TailwindBuffRoutine());
                    break;

                case Direction8.SE: // BLIZZARD: Sát thương lớn diện rộng + Làm chậm
                    Debug.Log("<color=cyan>MAGE (SE): BLIZZARD!</color>");
                    DealEmpoweredAoe(transform.position + forward * 3f, 5f, 2.5f, 0f, 0f, 0f, 0.5f, 3f);
                    break;

                case Direction8.S: // ABSOLUTE ZERO: Sát thương nhỏ, Choáng diện siêu rộng 3s
                    Debug.Log("<color=cyan>MAGE (S): ABSOLUTE ZERO!</color>");
                    DealEmpoweredAoe(transform.position, 6f, 0.5f, 3.0f);
                    break;

                case Direction8.SW: // FLASH FROST: Nổ chậm 80%
                    Debug.Log("<color=blue>MAGE (SW): FLASH FROST!</color>");
                    DealEmpoweredAoe(transform.position + forward * 2f, 4f, 1.5f, 0f, 0f, 0f, 0.8f, 4f);
                    break;

                case Direction8.W: // EARTHQUAKE: Đập bể 30% giáp phép
                    Debug.Log("<color=yellow>MAGE (W): EARTHQUAKE!</color>");
                    DealEmpoweredAoe(transform.position, 5f, 2.0f, 0.5f, 0f, 0f, 0f, 0f, 0.3f, 5f);
                    break;

                case Direction8.NW: // MAGMA ERUPTION: Choáng 1s + Thiêu Đốt
                    Debug.Log("<color=orange>MAGE (NW): MAGMA ERUPTION!</color>");
                    DealEmpoweredAoe(transform.position + forward * 2.5f, 3.5f, 2.0f, 1.0f, stats.magicAtk * 0.08f, 3f);
                    break;
            }
        }
    }

    // ==========================================================
    // HÀM TÍNH TOÁN DÙNG CHUNG (DỰA TRÊN CODE CỦA BẠN)
    // ==========================================================
    private void DealEmpoweredAoe(
        Vector3 center, float radius, float dmgMult,
        float stunTime = 0f, float burnDps = 0f, float burnTime = 0f,
        float slowPercent = 0f, float slowTime = 0f,
        float mrShredPercent = 0f, float shredTime = 0f)
    {
        // 1. Kích hoạt hiệu ứng hình ảnh dùng chung (nếu có)
        //if (blastVfx != null) Instantiate(blastVfx, center, Quaternion.identity);

        // 2. Quét mục tiêu
        Collider[] hits = Physics.OverlapSphere(center, radius, playerController.dangerLayer);

        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager.currentWeapon;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                // [TÍNH TOÁN CỦA BẠN]
                float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);
                bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
                var dmgTuple = CombatMath.CalculateFullDamage(
                    stats, enemyStats, t, isCrit, data, currentWpn, dmgMult
                );

                DamageInfo info = new DamageInfo
                {
                    sourcePosition = transform.position,
                    attacker = stats,
                    physDamage = dmgTuple.phys,
                    magicDamage = dmgTuple.magic,
                    trueDamage = dmgTuple.trueDmg,
                    isCrit = isCrit,
                    isStun = (stunTime > 0),
                    stunDuration = stunTime,
                    impactLevel = 1
                };

                enemyStats.TakeDamage(info);

                // --- Áp dụng các hiệu ứng kèm theo ---
                if (burnDps > 0)
                    enemyStats.ApplyBleed(burnDps, burnTime);

                if (slowPercent > 0)
                    StartCoroutine(TempSlowRoutine(enemyStats, slowPercent, slowTime));

                if (mrShredPercent > 0)
                    StartCoroutine(TempShredRoutine(enemyStats, mrShredPercent, shredTime));
            }
        }
    }

    // ==========================================================
    // CÁC COROUTINE HIỆU ỨNG TẠM THỜI (DEBUFF / BUFF)
    // ==========================================================

    private IEnumerator TempSlowRoutine(Stats enemy, float slowPercent, float duration)
    {
        if (enemy != null && enemy.currentHp > 0)
        {
            enemy.baseMoveSpeed -= slowPercent;
            yield return new WaitForSeconds(duration);
            if (enemy != null) enemy.baseMoveSpeed += slowPercent;
        }
    }

    private IEnumerator TempShredRoutine(Stats enemy, float shredPercent, float duration)
    {
        if (enemy != null && enemy.currentHp > 0)
        {
            float amountToShred = enemy.magicResist * shredPercent;
            enemy.magicResist -= amountToShred;
            yield return new WaitForSeconds(duration);
            if (enemy != null && enemy.currentHp > 0) enemy.magicResist += amountToShred;
        }
    }

    private IEnumerator TailwindBuffRoutine()
    {
        float speedBuff = 0.5f; // +50% Tốc chạy và Tốc đánh
        stats.bonusMoveSpeed += speedBuff;
        stats.bonusAttackSpeed += speedBuff;

        if (stats is AllyStats ally)
        {
            ally.CalculateMoveSpeedOnly();
            ally.CalculateCombatStatsOnly();
        }

        yield return new WaitForSeconds(5.0f);

        stats.bonusMoveSpeed -= speedBuff;
        stats.bonusAttackSpeed -= speedBuff;

        if (stats is AllyStats allyRevert)
        {
            allyRevert.CalculateMoveSpeedOnly();
            allyRevert.CalculateCombatStatsOnly();
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 forward = Application.isPlaying && stats != null ? stats.facingDirection : transform.forward;
        if (forward == Vector3.zero) forward = transform.forward;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position + forward * 1.5f, 2.5f); // Mô phỏng vùng nổ Default
    }
}