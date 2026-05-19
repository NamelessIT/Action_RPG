using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TricksterSkill : SkillBehavior
{
    [Header("Phase 1: Blink & Invisibility Settings")]
    public float blinkDistance = 3.0f;       // Dịch chuyển 3m
    public float invisibilityDuration = 4.0f;// Tàng hình 4 giây

    [Header("Phase 2: Decoy & Taunt Settings")]
    public float tauntDuration = 2.0f;       // Khiêu khích 2 giây
    public float tauntRadius = 4.0f;         // Bán kính khiêu khích
    public float explosionRadius = 3.0f;
    public float explosionDamageMult = 2.0f; // Sát thương nổ ảo ảnh (x2)
    public float decoyStunDuration = 2.0f;   // Choáng khi nổ

    [Header("Phase 3: Empowerment Stacks")]
    public int maxEmpoweredStacks = 3;       // Cường hóa 3 đòn đánh
    public float defaultBonusDamageMult = 2.5f; // Sát thương khi cầm Dagger
    public string requiredGrimoireName = "Grimoire";
    public string requiredDaggerName = "Dagger";

    [Header("VFX & Visuals")]
    public GameObject blinkVfxPrefab;
    public GameObject invisibilityAuraVfx;
    public GameObject decoyPrefab;
    public GameObject decoyExplosionVfx;
    public GameObject blastVfx;             // Hiệu ứng nổ đòn đánh cường hóa

    // --- Tham chiếu hệ thống ---
    private PlayerController playerController;
    private EquipmentManager equipmentManager;
    private SpriteRenderer playerSprite;
    private AllyStats allyStats;

    // --- Trạng thái tàng hình ---
    private Coroutine invisibilityCoroutine;
    private GameObject currentInvVfx;

    // --- Trạng thái cường hóa ---
    private int currentEmpowerStacks = 0;
    private float _currentDefaultDmgMult; // effective mult for current empower session
    private bool isGrimoireEquipped = false;
    private bool isDaggerEquipped = false;
    private Direction8 currentDirection = Direction8.None;

    private enum Direction8 { None = 0, N = 1, NE = 2, E = 3, SE = 4, S = 5, SW = 6, W = 7, NW = 8 }

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        playerSprite = myPlayer.GetComponentInChildren<SpriteRenderer>();
        allyStats = myStats;
    }

    protected override void OnEquip()
    {
        if (playerController != null)
        {
            playerController.OnAttackPerformed += OnAttackPerformed;
            playerController.OnWeaponEquipped += OnWeaponEquipped;
            playerController.OnMovementInputChanged += OnMovementInput;
        }
        CheckWeaponType(equipmentManager?.currentWeapon);
    }

    protected override void OnUnequip()
    {
        if (playerController != null)
        {
            playerController.OnAttackPerformed -= OnAttackPerformed;
            playerController.OnWeaponEquipped -= OnWeaponEquipped;
            playerController.OnMovementInputChanged -= OnMovementInput;
        }
        currentEmpowerStacks = 0;
        BreakInvisibility(true);
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(TricksterRoutine());
        return true;
    }

    private IEnumerator TricksterRoutine()
    {
        // Rogue U1:  +20% invisibilityDuration  | Rogue U3:  +20% defaultBonusDamageMult
        // Mage U1:   +20% decoyStunDuration     | Mage U3:   +20% explosionDamageMult
        float rU1 = allyStats != null ? allyStats.rogueSkillU1 : 0f;
        float rU3 = allyStats != null ? allyStats.rogueSkillU3 : 0f;
        float mU1 = allyStats != null ? allyStats.mageSkillU1  : 0f;
        float mU3 = allyStats != null ? allyStats.mageSkillU3  : 0f;

        float effectiveInvisDur   = invisibilityDuration   * (1f + rU1);
        float effectiveDefaultDmg = defaultBonusDamageMult * (1f + rU3);
        float effectiveStunDur    = decoyStunDuration      * (1f + mU1);
        float effectiveExplosionDmg = explosionDamageMult  * (1f + mU3);

        // ==========================================
        // PHASE 1
        // ==========================================
        player.isAttacking = true;
        Vector3 oldPosition = transform.position;

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();

        if (blinkVfxPrefab) Instantiate(blinkVfxPrefab, transform.position, Quaternion.LookRotation(forward));

        // Blink
        Vector3 targetPos = transform.position + forward * blinkDistance;
        UnityEngine.AI.NavMeshHit navHit;
        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
            player.transform.position = navHit.position;
        else
            player.transform.position = targetPos;

        // Tàng hình
        if (playerSprite) playerSprite.enabled = false;
        if (invisibilityAuraVfx) currentInvVfx = Instantiate(invisibilityAuraVfx, transform);
        stats.isInvisible = true; // Sử dụng cờ tàng hình của hệ thống

        Debug.Log("<color=cyan>TRICKSTER: BLINK VÀ TÀNG HÌNH!</color>");

        // ==========================================
        // PHASE 2: TRIỆU HỒI ẢO ẢNH
        // ==========================================
        if (decoyPrefab)
        {
            GameObject decoy = Instantiate(decoyPrefab, oldPosition, transform.rotation);
            StartCoroutine(DecoyLifecycleRoutine(decoy, effectiveStunDur, effectiveExplosionDmg));
        }

        // ==========================================
        // PHASE 3: CẤP 3 STACK CƯỜNG HÓA
        // ==========================================
        currentEmpowerStacks = maxEmpoweredStacks;
        _currentDefaultDmgMult = effectiveDefaultDmg; // store for OnAttackPerformed
        Debug.Log($"<color=orange>Trickster:</color> Stacks x{currentEmpowerStacks}, DmgMult {_currentDefaultDmgMult:F2}");

        yield return new WaitForSeconds(0.1f);
        player.isAttacking = false;

        if (invisibilityCoroutine != null) StopCoroutine(invisibilityCoroutine);
        invisibilityCoroutine = StartCoroutine(InvisibilityTimeoutRoutine(effectiveInvisDur));
    }

    private IEnumerator InvisibilityTimeoutRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        BreakInvisibility(false);
    }

    private void BreakInvisibility(bool isForceCleanup)
    {
        if (!stats.isInvisible && !isForceCleanup) return;

        stats.isInvisible = false;
        if (playerSprite) playerSprite.enabled = true;
        if (currentInvVfx) Destroy(currentInvVfx);
        if (invisibilityCoroutine != null && !isForceCleanup) { StopCoroutine(invisibilityCoroutine); invisibilityCoroutine = null; }

        if (!isForceCleanup) Debug.Log("<color=gray>Trickster: Đã hiện hình!</color>");
    }

    // ==========================================================
    // LOGIC ẢO ẢNH (KHIÊU KHÍCH & NỔ)
    // ==========================================================
    private IEnumerator DecoyLifecycleRoutine(GameObject decoy, float effectiveStunDur, float effectiveExplosionDmg)
    {
        Collider[] hitsToTaunt = Physics.OverlapSphere(decoy.transform.position, tauntRadius, player.dangerLayer);

        foreach (var col in hitsToTaunt)
        {
            var agent = col.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
                agent.SetDestination(decoy.transform.position);
        }

        yield return new WaitForSeconds(tauntDuration);

        if (decoyExplosionVfx) Instantiate(decoyExplosionVfx, decoy.transform.position, Quaternion.identity);

        Debug.Log("<color=red>DECOY EXPLODES!</color>");

        // Mage U1 scales stun, Mage U3 scales explosion damage
        DealEmpoweredAoe(decoy.transform.position, explosionRadius, effectiveExplosionDmg, effectiveStunDur);

        Destroy(decoy);
    }

    // ==========================================================
    // LOGIC CƯỜNG HÓA ĐÒN ĐÁNH (Tiêu hao Stack)
    // ==========================================================
    private void OnAttackPerformed(int stepIndex, bool isHeavy)
    {
        if (stats.isInvisible) BreakInvisibility(false);

        if (currentEmpowerStacks > 0)
        {
            currentEmpowerStacks--;
            ExecuteEmpoweredAttack();
            Debug.Log($"<color=orange>Trickster:</color> Tiêu hao 1 stack (Còn {currentEmpowerStacks}/{maxEmpoweredStacks})");
        }
    }

    private void ExecuteEmpoweredAttack()
    {
        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();

        Vector3 center = transform.position + forward * 1.5f;

        if (isGrimoireEquipped && currentDirection != Direction8.None)
        {
            ExecuteGrimoireDirectionalEmpowerment(center, forward);
        }
        else if (isDaggerEquipped || !isGrimoireEquipped)
        {
            Debug.Log("<color=orange>TRICKSTER: Empowered Attack!</color>");
            // Rogue U3 scales defaultBonusDamageMult (cached in _currentDefaultDmgMult)
            DealEmpoweredAoe(center, 2.5f, _currentDefaultDmgMult, 1.5f);
        }
    }

    // ==========================================================
    // GRIMOIRE: LOGIC 8 HƯỚNG CƯỜNG HÓA TÙY CHỈNH
    // ==========================================================
    private void ExecuteGrimoireDirectionalEmpowerment(Vector3 center, Vector3 forward)
    {
        switch (currentDirection)
        {
            case Direction8.N:
                Debug.Log("<color=red>TRICKSTER (N): HELLFIRE!</color>");
                // Nổ to x3, Thiêu đốt 3s (10% magicAtk/tick)
                DealEmpoweredAoe(transform.position + forward * 3f, 4.0f, 3.0f, 0f, stats.magicAtk * 0.1f, 3f);
                break;

            case Direction8.NE:
                Debug.Log("<color=red>TRICKSTER (NE): GRAND HURRICANE!</color>");
                // Nổ diện rộng, Hút/Giật nhẹ 0.5s
                DealEmpoweredAoe(transform.position, 5.0f, 2.0f, 0.5f);
                break;

            case Direction8.E:
                Debug.Log("<color=green>TRICKSTER (E): TAILWIND!</color>");
                // Chém sát thương x1.5 + Tự buff tốc độ 3s
                DealEmpoweredAoe(center, 3.0f, 1.5f);
                StartCoroutine(TailwindBuffRoutine(3.0f));
                break;

            case Direction8.SE:
                Debug.Log("<color=cyan>TRICKSTER (SE): BLIZZARD!</color>");
                // Sát thương lớn x2.5 + Làm chậm 50% trong 3s
                DealEmpoweredAoe(transform.position + forward * 3f, 5.0f, 2.5f, 0f, 0f, 0f, 0.5f, 3f);
                break;

            case Direction8.S:
                Debug.Log("<color=cyan>TRICKSTER (S): ABSOLUTE ZERO!</color>");
                // Choáng diện rộng 3s, sát thương siêu nhỏ x0.5
                DealEmpoweredAoe(transform.position, 6.0f, 0.5f, 3.0f);
                break;

            case Direction8.SW:
                Debug.Log("<color=blue>TRICKSTER (SW): FLASH FROST!</color>");
                // Nổ làm chậm 80% trong 4s
                DealEmpoweredAoe(transform.position + forward * 2f, 4.0f, 1.5f, 0f, 0f, 0f, 0.8f, 4f);
                break;

            case Direction8.W:
                Debug.Log("<color=yellow>TRICKSTER (W): EARTHQUAKE!</color>");
                // Đập phá 30% giáp phép trong 5s
                DealEmpoweredAoe(transform.position, 5.0f, 2.0f, 0.5f, 0f, 0f, 0f, 0f, 0.3f, 5f);
                break;

            case Direction8.NW:
                Debug.Log("<color=orange>TRICKSTER (NW): MAGMA ERUPTION!</color>");
                // Sát thương x2, Choáng 1s + Thiêu Đốt
                DealEmpoweredAoe(transform.position + forward * 2.5f, 3.5f, 2.0f, 1.0f, stats.magicAtk * 0.08f, 3f);
                break;
        }
    }

    // ==========================================================
    // HÀM TÍNH TOÁN AOE DÙNG CHUNG
    // ==========================================================
    private void DealEmpoweredAoe(
        Vector3 center, float radius, float dmgMult,
        float stunTime = 0f, float burnDps = 0f, float burnTime = 0f,
        float slowPercent = 0f, float slowTime = 0f,
        float mrShredPercent = 0f, float shredTime = 0f)
    {
        if (blastVfx != null) Instantiate(blastVfx, center, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(center, radius, playerController.dangerLayer);

        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager.currentWeapon;
        float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                float t = CombatMath.CalculateDirectionFactor(transform, enemyStats);
                bool isCrit = CombatMath.CheckIsCrit(totalCritChance);
                var dmgTuple = CombatMath.CalculateFullDamage(stats, enemyStats, t, isCrit, data, currentWpn, dmgMult);

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

                // --- Áp dụng Debuff kèm theo ---
                if (burnDps > 0) enemyStats.ApplyBleed(burnDps, burnTime);
                if (slowPercent > 0) StartCoroutine(TempSlowRoutine(enemyStats, slowPercent, slowTime));
                if (mrShredPercent > 0) StartCoroutine(TempShredRoutine(enemyStats, mrShredPercent, shredTime));
            }
        }
    }

    // ==========================================================
    // COROUTINES XỬ LÝ DEBUFF / BUFF TẠM THỜI
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

    private IEnumerator TailwindBuffRoutine(float duration)
    {
        float speedBuff = 0.4f;
        stats.bonusMoveSpeed += speedBuff;
        stats.bonusAttackSpeed += speedBuff;
        if (stats is AllyStats ally) { ally.CalculateMoveSpeedOnly(); ally.CalculateCombatStatsOnly(); }

        yield return new WaitForSeconds(duration);

        stats.bonusMoveSpeed -= speedBuff;
        stats.bonusAttackSpeed -= speedBuff;
        if (stats is AllyStats allyRevert) { allyRevert.CalculateMoveSpeedOnly(); allyRevert.CalculateCombatStatsOnly(); }
    }

    // ==========================================
    // QUẢN LÝ VŨ KHÍ & HƯỚNG BẤM
    // ==========================================
    private void OnWeaponEquipped(WeaponData weapon) { CheckWeaponType(weapon); }

    private void CheckWeaponType(WeaponData weapon)
    {
        isGrimoireEquipped = (weapon != null && weapon.weaponName == requiredGrimoireName);
        isDaggerEquipped = (weapon != null && weapon.weaponName == requiredDaggerName);

        if (!isGrimoireEquipped && !isDaggerEquipped && weapon != null)
        {
            currentEmpowerStacks = 0;
            Debug.Log("<color=gray>Trickster: Vũ khí không hỗ trợ, mất cường hóa đòn đánh.</color>");
        }
    }

    private void OnMovementInput(Vector2 input)
    {
        if (input == Vector2.zero) { currentDirection = Direction8.None; return; }
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, tauntRadius);
    }
}