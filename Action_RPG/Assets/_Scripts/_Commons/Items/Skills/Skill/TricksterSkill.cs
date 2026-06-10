using UnityEngine;
using System.Collections;

/// <summary>
/// TricksterSkill — "Blink Trap".
/// Dịch chuyển + tàng hình 4s, để lại tàn ảnh tại chỗ cũ khiêu khích kẻ địch.
/// Sau 2s tàn ảnh phát nổ (sát thương phép + choáng). Đồng thời cường hóa 3 đòn đánh
/// thường tiếp theo trong 7s — dùng chung module <see cref="ArcaneEmpowerment"/> với MageSkill.
/// </summary>
public class TricksterSkill : SkillBehavior, IEmpoweredAttackProvider
{
    [Header("Phase 1: Blink & Tàng hình")]
    public float blinkDistance = 3.0f;
    public float invisibilityDuration = 4.0f;

    [Header("Phase 2: Tàn ảnh & Khiêu khích")]
    public float tauntDuration = 2.0f;
    public float tauntRadius = 4.0f;
    public float explosionRadius = 3.0f;
    public float explosionDamageMult = 2.0f; // 200% magicAtk
    public float decoyStunDuration = 2.0f;

    [Header("Phase 3: Cường hóa 3 đòn")]
    public int maxEmpoweredStacks = 3;
    public float empowerBuffDuration = 7.0f;

    [Header("Đòn cường hóa 8 hướng (dùng chung)")]
    public ArcaneEmpowerment empower = new ArcaneEmpowerment();

    [Header("VFX (tuỳ chọn)")]
    public GameObject blinkVfxPrefab;
    public GameObject invisibilityAuraVfx;
    public GameObject decoyPrefab;
    public GameObject decoyExplosionVfx;

    // --- Refs ---
    private PlayerController playerController;
    private EquipmentManager equipmentManager;
    private MagePassive magePassive;
    private SpriteRenderer playerSprite;

    // --- Trạng thái ---
    private int currentEmpowerStacks = 0;
    private bool isGrimoireEquipped = false;
    private EmpowerDirection currentDirection = EmpowerDirection.None;

    private Coroutine invisibilityCoroutine;
    private Coroutine empowerBuffCoroutine;
    private GameObject currentInvVfx;

    public bool WillEmpowerAttack(bool isHeavy) => currentEmpowerStacks > 0 && !isHeavy;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        // Gán refs TRƯỚC base.Initialize: base gọi OnEquip() (đăng ký sự kiện) ngay trong nó.
        playerController = myPlayer;
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        magePassive = myPlayer.GetComponent<MagePassive>();
        playerSprite = myPlayer.GetComponentInChildren<SpriteRenderer>();
        base.Initialize(myStats, myData, myPlayer);

        empower.Setup(this, stats, playerController.dangerLayer, magePassive);
    }

    protected override void OnEquip()
    {
        if (playerController != null)
        {
            playerController.OnAttackPerformed += OnAttackPerformed;
            playerController.OnWeaponEquipped += OnWeaponEquipped;
            playerController.OnMovementInputChanged += OnMovementInput;
        }
        CheckGrimoireEquipped(equipmentManager?.currentWeapon);
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

    // ==========================================================
    // INPUT & VŨ KHÍ
    // ==========================================================
    private void OnMovementInput(Vector2 input)
    {
        currentDirection = ArcaneEmpowerment.DirectionFromInput(input, currentDirection);
    }

    private void OnWeaponEquipped(WeaponData weapon) => CheckGrimoireEquipped(weapon);

    private void CheckGrimoireEquipped(WeaponData weapon)
    {
        if (magePassive != null) isGrimoireEquipped = magePassive.IsGrimoireActive;
        else isGrimoireEquipped = (weapon != null && weapon.weaponType == WeaponData.WeaponType.Grimoire);
    }

    // ==========================================================
    // KÍCH HOẠT
    // ==========================================================
    public override bool Use()
    {
        if (!base.Use()) return false;

        // Node nâng cấp (Rogue U3 + Mage U3): +20% sát thương mỗi cái cho toàn bộ đòn cường hóa.
        float rU3 = stats != null ? stats.rogueSkillU3 : 0f;
        float mU3 = stats != null ? stats.mageSkillU3 : 0f;
        empower.SetDamageScale(1f + rU3 + mU3);

        CheckGrimoireEquipped(equipmentManager?.currentWeapon);
        StartCoroutine(TricksterRoutine());
        return true;
    }

    private IEnumerator TricksterRoutine()
    {
        float rU1 = stats != null ? stats.rogueSkillU1 : 0f; // +20% thời gian tàng hình
        float mU1 = stats != null ? stats.mageSkillU1 : 0f;  // +20% thời gian choáng tàn ảnh
        float mU3 = stats != null ? stats.mageSkillU3 : 0f;  // +20% sát thương nổ tàn ảnh

        float invisDur = invisibilityDuration * (1f + rU1);
        float stunDur = decoyStunDuration * (1f + mU1);
        float explosionDmg = explosionDamageMult * (1f + mU3);

        // ---------- PHASE 1: BLINK + TÀNG HÌNH ----------
        player.isAttacking = true;
        Vector3 oldPosition = transform.position;

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0; forward.Normalize();

        if (blinkVfxPrefab) Instantiate(blinkVfxPrefab, transform.position, Quaternion.LookRotation(forward));

        Vector3 targetPos = transform.position + forward * blinkDistance;
        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out var navHit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
            player.transform.position = navHit.position;
        else
            player.transform.position = targetPos;

        if (playerSprite) playerSprite.enabled = false;
        if (invisibilityAuraVfx) currentInvVfx = Instantiate(invisibilityAuraVfx, transform);
        stats.isInvisible = true;
        Debug.Log("<color=cyan>TRICKSTER: BLINK & TÀNG HÌNH!</color>");

        // ---------- PHASE 2: TÀN ẢNH ----------
        GameObject decoy = SpawnDecoy(oldPosition);
        StartCoroutine(DecoyLifecycleRoutine(decoy, stunDur, explosionDmg));

        // ---------- PHASE 3: 3 STACK CƯỜNG HÓA (7s) ----------
        currentEmpowerStacks = maxEmpoweredStacks;
        if (empowerBuffCoroutine != null) StopCoroutine(empowerBuffCoroutine);
        empowerBuffCoroutine = StartCoroutine(EmpowerBuffTimeoutRoutine());
        Debug.Log($"<color=orange>TRICKSTER: Cường hóa {currentEmpowerStacks} đòn trong {empowerBuffDuration}s!</color>");

        yield return new WaitForSeconds(0.1f);
        player.isAttacking = false;

        if (invisibilityCoroutine != null) StopCoroutine(invisibilityCoroutine);
        invisibilityCoroutine = StartCoroutine(InvisibilityTimeoutRoutine(invisDur));
    }

    private GameObject SpawnDecoy(Vector3 pos)
    {
        if (decoyPrefab) return Instantiate(decoyPrefab, pos, transform.rotation);

        // Không có prefab → tạo neo runtime để vẫn khiêu khích & phát nổ đúng vị trí.
        GameObject decoy = new GameObject("Trickster_Decoy");
        decoy.transform.position = pos;
        MageVfxHelper.AttachSphere(decoy.transform, 1f, new Color(0.6f, 0.3f, 1f, 0.6f));
        return decoy;
    }

    private IEnumerator EmpowerBuffTimeoutRoutine()
    {
        yield return new WaitForSeconds(empowerBuffDuration);
        currentEmpowerStacks = 0;
        empowerBuffCoroutine = null;
        Debug.Log("<color=gray>TRICKSTER: Hết cường hóa.</color>");
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

        if (!isForceCleanup) Debug.Log("<color=gray>TRICKSTER: Hiện hình!</color>");
    }

    // ==========================================================
    // TÀN ẢNH: KHIÊU KHÍCH & NỔ
    // ==========================================================
    private IEnumerator DecoyLifecycleRoutine(GameObject decoy, float stunDur, float explosionDmg)
    {
        if (decoy == null) yield break;
        Vector3 decoyPos = decoy.transform.position;

        // Khiêu khích: kéo kẻ địch gần đó di chuyển về phía tàn ảnh.
        Collider[] taunted = Physics.OverlapSphere(decoyPos, tauntRadius, playerController.dangerLayer);
        foreach (var col in taunted)
        {
            var agent = col.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.SetDestination(decoyPos);
        }

        yield return new WaitForSeconds(tauntDuration);

        if (decoyExplosionVfx) Instantiate(decoyExplosionVfx, decoyPos, Quaternion.identity);
        Debug.Log("<color=red>TRICKSTER: TÀN ẢNH PHÁT NỔ!</color>");

        // Nổ sát thương phép + choáng (dùng module chung để luôn ra sát thương phép).
        stats.EnterCombat();
        empower.DealMagicAoe(decoyPos, explosionRadius, explosionDmg, stunDur, Color.magenta);

        if (decoy != null) Destroy(decoy);
    }

    // ==========================================================
    // CƯỜNG HÓA ĐÒN ĐÁNH (tiêu hao stack)
    // ==========================================================
    private void OnAttackPerformed(int stepIndex, bool isHeavy)
    {
        if (stats.isInvisible) BreakInvisibility(false);

        if (isHeavy || currentEmpowerStacks <= 0) return;

        currentEmpowerStacks--;
        empower.Execute(currentDirection, isGrimoireEquipped);
        Debug.Log($"<color=orange>TRICKSTER: Tiêu hao 1 stack (còn {currentEmpowerStacks}/{maxEmpoweredStacks})</color>");

        if (currentEmpowerStacks <= 0 && empowerBuffCoroutine != null)
        {
            StopCoroutine(empowerBuffCoroutine);
            empowerBuffCoroutine = null;
        }
    }
}
