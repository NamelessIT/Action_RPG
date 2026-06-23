using GLTFast.Schema;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    protected EnemyStats stats; // Dùng 'protected' để lớp con (Boss) có thể dùng
    protected Transform target; // Player
    protected Animator animator;

    [Header("Combat Settings")]
    public bool isAttacking = false;
    protected float lastAttackTime = -10f;
    // Expose để EnemyAI đọc cooldown state
    public float LastAttackTime => lastAttackTime;

    public float basicAttackRange = 2.0f;
    [Range(0, 360)] public float attackAngle = 90f;

    [Header("Sweep Angles (tính từ hướng nhìn, âm=trái, dương=phải)")]
    [Tooltip("Góc bắt đầu quét. Ví dụ: 0 = từ thẳng trước, -45 = từ trái")]
    public float sweepStartAngle = -45f;
    [Tooltip("Góc kết thúc quét. Ví dụ: 45 = ra phải. Đặt 0→45 cho chém một chiều")]
    public float sweepEndAngle = 45f;

    [Header("Telegraph — Báo hiệu trước khi đánh")]
    [Tooltip("Bật/tắt animation báo hiệu. Tắt = tấn công ngay không cảnh báo.")]
    public bool useTelegraph = true;

    [Tooltip("Thời gian thực hiện animation báo hiệu (giây). Đây là window để player né đòn.")]
    public float telegraphDuration = 1.0f;

    [Tooltip("Tên Animator Trigger để kích hoạt animation báo hiệu.\n" +
             "Ví dụ: 'WindUp' (kiếm sĩ giơ tay), 'ChargeUp' (pháp sư tụ lực), 'Roar' (boss gầm).\n" +
             "Để TRỐNG nếu chỉ muốn đứng im không animation.")]
    public string telegraphAnimTrigger = "WindUp";

    [Tooltip("Tên Animator Bool để set TRUE suốt thời gian telegraph (optional).\n" +
             "Dùng khi animation báo hiệu dài/blend (VD: 'IsCharging').\n" +
             "Để TRỐNG nếu dùng Trigger.")]
    public string telegraphAnimBool = "";

    [Header("Attack Timing — chỉnh cảm giác đòn đánh")]
    [Tooltip("Độ dài animation đánh gốc (giây) trước khi chia cho attackSpeed.")]
    public float baseAttackAnimDuration = 0.5f;
    [Tooltip("Mốc BẮT ĐẦU cửa sổ gây damage (0-1 theo độ dài anim). VD 0.5 = nửa sau.")]
    [Range(0f, 1f)] public float damageWindowStartNormalized = 0.50f;
    [Tooltip("Mốc KẾT THÚC cửa sổ gây damage (0-1). Phải > start.")]
    [Range(0f, 1f)] public float damageWindowEndNormalized = 0.75f;

    // Trạng thái telegraph — EnemyAI đọc để block movement
    [HideInInspector] public bool isTelegraphing = false;

    private int currentComboStep = 0;
    private int maxCombo = 2;


    // [MỚI] Biến này để lưu Coroutine đánh, giúp Boss có thể Cancel
    protected Coroutine currentAttackCoroutine;

    // ── ENEMY SKILL (đơn giản: 1 skill ưu tiên, đánh mạnh hơn theo skillPhysicalMultiplier) ──
    [Header("--- Enemy Skill (optional) ---")]
    [Tooltip("Gán SkillData để enemy ƯU TIÊN dùng skill khi sẵn sàng. Để trống = chỉ đánh thường.")]
    public SkillData enemySkill;
    [Tooltip("Hồi chiêu skill (giây). Nếu 0 thì lấy theo enemySkill.cooldown.")]
    public float skillCooldown = 0f;
    [Tooltip("Tầm dùng skill. Nếu 0 thì dùng basicAttackRange.")]
    public float skillRange = 0f;
    [Tooltip("Hiệu ứng CC (Stun/Silence/Airborne/Root...) mà ĐÒN SKILL gây lên Player/Ally. " +
             "Đòn đánh thường KHÔNG áp các effect này.")]
    public System.Collections.Generic.List<CombatEffectInfo> skillEffects = new System.Collections.Generic.List<CombatEffectInfo>();
    private float lastSkillTime = -999f;
    // [INT-01C] lastSkillTime TRƯỚC khi skill hiện tại bắt đầu — để un-commit cooldown nếu skill bị
    // interrupt với putInterruptedSkillOnCooldown=false (không "phế" skill).
    private float _prevSkillTime = -999f;
    // Hệ số nhân damage cho đòn hiện tại (1 = đánh thường; skill set = skillPhysicalMultiplier)
    private float _currentAttackMultiplier = 1f;
    // Đòn hiện tại có phải skill không (để apply skillEffects, basic thì không).
    private bool _currentIsSkill = false;
    // [EAM] Module của đòn hiện tại (null = đòn legacy melee). Quyết định nguồn CC trong DealDamageToTarget.
    private EnemyAttackModuleData _currentModule = null;
    // [EAM-02B] DashStrike: mượn external move override của EnemyAI; cần dọn khi complete/interrupt.
    private bool _moduleDashOverride = false;
    private EnemyAI _dashAI = null;

    public bool HasSkill => enemySkill != null || skillAttackModule != null;
    public float SkillRange => skillRange > 0f ? skillRange : basicAttackRange;

    [Header("--- [EAM] Attack Modules (optional — null = melee fallback cũ) ---")]
    [Tooltip("Module đòn THƯỜNG. Null → dùng EnemyAttackRoutine melee hiện tại.")]
    public EnemyAttackModuleData basicAttackModule;
    [Tooltip("Module đòn SKILL. Null → dùng enemySkill melee + skillEffects hiện tại.")]
    public EnemyAttackModuleData skillAttackModule;

    /// <summary>[EAM] Tầm dùng skill — module.range (nếu >0) ưu tiên; module null hoặc range≤0 → SkillRange cũ.</summary>
    public float GetSkillRange()
        => (skillAttackModule != null && skillAttackModule.range > 0f) ? skillAttackModule.range : SkillRange;

    /// <summary>[EAM-04] Tầm đòn THƯỜNG cho EnemyAI (stopping/chase/spacing). basicAttackModule.range (nếu >0)
    /// ưu tiên; module null hoặc range≤0 → basicAttackRange cũ.</summary>
    public float GetBasicRange()
        => (basicAttackModule != null && basicAttackModule.range > 0f) ? basicAttackModule.range : basicAttackRange;

    /// <summary>[EAM] Skill sẵn sàng? Module-aware: dùng cooldown của skillAttackModule nếu có; null → IsSkillReady() cũ.</summary>
    public bool CanUseSkill()
    {
        if (skillAttackModule == null) return IsSkillReady(); // fallback legacy (enemySkill)
        if (isAttacking) return false;
        if (stats != null && stats.IsSkillLocked) return false; // Silence/Stun/Airborne → không cast
        float cd = skillAttackModule.cooldown > 0f ? skillAttackModule.cooldown : 0.1f;
        return Time.time >= lastSkillTime + cd;
    }

    /// <summary>Skill sẵn sàng dùng? (có skill + hết cooldown + không đang đánh + không bị Trầm Mặc).</summary>
    public bool IsSkillReady()
    {
        if (enemySkill == null || isAttacking) return false;
        if (stats != null && stats.IsSkillLocked) return false; // Silence/Stun/Airborne → không dùng được skill
        float cd = skillCooldown > 0f ? skillCooldown : Mathf.Max(0.1f, enemySkill.cooldown);
        return Time.time >= lastSkillTime + cd;
    }

    private List<Transform> hitTargets = new List<Transform>();

    public virtual void Setup(EnemyStats _stats, Transform _target, Animator _animator)
    {
        // [INT-01C] Nghe interrupt CÓ ngữ cảnh (KHÔNG nghe legacy OnInterrupted để tránh cancel 2 lần
        // — RaiseInterrupted(ctx) fire cả 2 event). Gỡ đăng ký cũ (đề phòng Setup gọi lại) rồi đăng ký mới.
        if (stats != null) stats.OnInterruptedContext -= OnInterruptContext;
        stats = _stats;
        target = _target;
        animator = _animator;
        if (stats != null) stats.OnInterruptedContext += OnInterruptContext; // CC ngắt windup/đòn đánh (context-aware)
    }

    protected virtual void OnDisable()
    {
        if (stats != null) stats.OnInterruptedContext -= OnInterruptContext;
    }

    // [INT-01C] CC ngắt: chỉ hủy khi effect THỰC SỰ ngắt hành động (interruptCurrentAction=true).
    // Cooldown skill commit/un-commit theo putInterruptedSkillOnCooldown. Legacy/Unknown (interruptCurrentAction
    // mặc định true) vẫn hủy như cũ.
    private void OnInterruptContext(InterruptContext ctx)
    {
        if (!ctx.interruptCurrentAction) return; // Root/Slow/effect không-ngắt → KHÔNG hủy đòn
        bool keepSkillCooldown = !_currentIsSkill || ctx.putInterruptedSkillOnCooldown;
        CancelAttack(keepSkillCooldown);
    }


    // Hàm Update để quản lý Cooldown skill (nếu có)
    public virtual void HandleCombatUpdate()
    {
        // Boss sẽ override hàm này để tính toán combo skill
    }


    public virtual void PerformBasicAttack()
    {
        if (stats == null || target == null) return;
        if (isAttacking) return;

        // Tính Cooldown. [SLOW] giảm cadence đánh (cooldown dài hơn) — chỉ tác động đòn thường, không skillCooldown.
        float speed = stats.baseAttackSpeed;
        if (speed <= 0) speed = 0.25f;
        speed *= stats.EffectiveSlowMultiplier;
        float cooldownTime = 1.0f / Mathf.Max(0.01f, speed);

        if (Time.time < lastAttackTime + cooldownTime) return;

        _currentAttackMultiplier = 1f; // đòn thường
        _currentIsSkill = false;
        // [EAM] Có module → dispatch theo style; null → melee fallback cũ.
        if (basicAttackModule != null) PerformModuleAttack(basicAttackModule, false);
        else currentAttackCoroutine = StartCoroutine(EnemyAttackRoutine());
    }

    /// <summary>
    /// Dùng skill: module (EAM) dispatch theo style, hoặc fallback enemySkill melee (nhân skillPhysicalMultiplier).
    /// </summary>
    public virtual void PerformSkillAttack()
    {
        if (stats == null || target == null) return;
        if (isAttacking) return;
        if (stats.IsSkillLocked) return; // bị Silence/Stun/Airborne → không cast được

        // [EAM] Module skill: tự quản cooldown/range theo data.
        if (skillAttackModule != null)
        {
            if (!CanUseSkill()) return;
            _prevSkillTime = lastSkillTime; // [INT-01C] un-commit nếu bị interrupt (flag false)
            lastSkillTime = Time.time;
            _currentAttackMultiplier = skillAttackModule.damageMultiplier > 0f ? skillAttackModule.damageMultiplier : 1f;
            _currentIsSkill = true;
            Debug.Log($"<color=magenta>{gameObject.name} dùng SKILL MODULE '{skillAttackModule.displayName}' (style {skillAttackModule.style})</color>");
            PerformModuleAttack(skillAttackModule, true);
            return;
        }

        // Fallback legacy (enemySkill + EnemyAttackRoutine).
        if (enemySkill == null) return;
        if (!IsSkillReady()) return;
        _prevSkillTime = lastSkillTime; // [INT-01C] lưu để un-commit nếu skill bị interrupt (flag false)
        lastSkillTime = Time.time;
        _currentAttackMultiplier = enemySkill.skillPhysicalMultiplier > 0f
                                   ? enemySkill.skillPhysicalMultiplier : 1.5f;
        _currentIsSkill = true;
        Debug.Log($"<color=magenta>{gameObject.name} dùng SKILL '{enemySkill.skillName}' (x{_currentAttackMultiplier})!</color>");
        currentAttackCoroutine = StartCoroutine(EnemyAttackRoutine());
    }

    // [EAM] Warn-once mỗi style chưa có runtime (tránh spam log mỗi đòn).
    private readonly System.Collections.Generic.HashSet<EnemyAttackStyle> _warnedStyles = new System.Collections.Generic.HashSet<EnemyAttackStyle>();

    /// <summary>[EAM-02A] Dispatch đòn theo style của module. Melee/local → ModuleMeleeRoutine; các style
    /// projectile/dash/ground/cone/buff/summon CHƯA implement ở 02A → warn-once + cleanup (không crash).</summary>
    protected virtual void PerformModuleAttack(EnemyAttackModuleData module, bool isSkill)
    {
        if (module == null) { _currentModule = null; currentAttackCoroutine = StartCoroutine(EnemyAttackRoutine()); return; }

        switch (module.style)
        {
            case EnemyAttackStyle.MeleeSingle:
            case EnemyAttackStyle.MeleeSweep:
            case EnemyAttackStyle.MeleeThrust:
            case EnemyAttackStyle.MeleeCircleAOE:
                _currentModule = module;
                currentAttackCoroutine = StartCoroutine(ModuleMeleeRoutine(module));
                break;
            case EnemyAttackStyle.DashStrike:
                _currentModule = module;
                currentAttackCoroutine = StartCoroutine(ModuleDashRoutine(module));
                break;
            case EnemyAttackStyle.ProjectileDirectional:
                _currentModule = module;
                currentAttackCoroutine = StartCoroutine(ModuleProjectileRoutine(module, targeted: false));
                break;
            case EnemyAttackStyle.ProjectileTargeted:
                _currentModule = module;
                currentAttackCoroutine = StartCoroutine(ModuleProjectileRoutine(module, targeted: true));
                break;
            case EnemyAttackStyle.GroundTargetAOE:
                _currentModule = module;
                currentAttackCoroutine = StartCoroutine(ModuleGroundAoeRoutine(module));
                break;
            default:
                // [EAM-02B] ConeBreath/SelfBuff/Summon → chưa implement: warn-once + cleanup (không crash).
                if (_warnedStyles.Add(module.style))
                    Debug.LogWarning($"[EAM] '{gameObject.name}' style {module.style} chưa implement → bỏ qua đòn này.");
                _currentModule = null;
                _currentIsSkill = false;
                _currentAttackMultiplier = 1f;
                break;
        }
    }

    // [EAM] Clone module.effects lên info (per-target sourcePosition, +impactBonus). KHÔNG mutate ScriptableObject.
    private void AddModuleEffects(DamageInfo info, EnemyAttackModuleData module)
    {
        if (module == null || module.effects == null) return;
        foreach (var src in module.effects)
        {
            if (src == null) continue;
            info.AddEffect(new CombatEffectInfo(src.type, src.duration)
            {
                force = src.force,
                height = src.height,
                magnitude = src.magnitude,
                impactLevel = src.impactLevel + module.impactBonus, // +impactBonus của module
                sourcePosition = transform.position,
                respectEffectResistance = src.respectEffectResistance,
                interruptCurrentAction = src.interruptCurrentAction,
                putInterruptedSkillOnCooldown = src.putInterruptedSkillOnCooldown,
                note = src.note,
            });
        }
    }

    // ── [EAM-02B] Helpers dùng chung cho các module routine ──

    /// <summary>Khóa hướng mặt về target (nếu faceTargetOnStart) và trả về hướng commit.</summary>
    private Vector3 ModuleFaceTarget(EnemyAttackModuleData module)
    {
        Vector3 facingDir = stats != null ? stats.facingDirection : transform.forward;
        if (module.faceTargetOnStart && target != null)
        {
            Vector3 d = (target.position - transform.position); d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) { facingDir = d.normalized; if (stats != null) stats.facingDirection = facingDir; }
        }
        return facingDir;
    }

    /// <summary>Telegraph + windup theo animator (projectile/dash). GroundAOE tự xử lý telegraphPrefab.</summary>
    private IEnumerator ModuleAnimWindup(EnemyAttackModuleData module)
    {
        if (module.useTelegraph && module.windupDuration > 0f)
        {
            isTelegraphing = true;
            if (animator != null)
            {
                if (!string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                    animator.SetBool(telegraphAnimBool, true);
                else if (!string.IsNullOrEmpty(telegraphAnimTrigger) && HasParameter(animator, telegraphAnimTrigger))
                    animator.SetTrigger(telegraphAnimTrigger);
            }
            yield return new WaitForSeconds(module.windupDuration);
            isTelegraphing = false;
            if (animator != null && !string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                animator.SetBool(telegraphAnimBool, false);
        }
        else if (module.windupDuration > 0f)
        {
            yield return new WaitForSeconds(module.windupDuration);
        }
    }

    /// <summary>Reset state cuối routine module (giống EnemyAttackRoutine end).</summary>
    private void ModuleAttackCleanup()
    {
        isAttacking = false;
        _currentIsSkill = false;
        _currentModule = null;
        _currentAttackMultiplier = 1f;
        currentAttackCoroutine = null;
    }

    /// <summary>Cone hit trong range + attackAngle (dùng cho DashStrike).</summary>
    private void ModuleConeHit(EnemyAttackModuleData module, Vector3 facingDir)
    {
        float range = Mathf.Max(0.1f, module.range);
        float halfAngle = Mathf.Max(1f, module.attackAngle) * 0.5f;
        foreach (var hit in Physics.OverlapSphere(transform.position, range))
        {
            Vector3 to = (hit.transform.position - transform.position); to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) { TryModuleHit(hit.transform, module); continue; }
            if (Vector3.Angle(facingDir, to.normalized) <= halfAngle) TryModuleHit(hit.transform, module);
        }
    }

    // [EAM-02B] ProjectileDirectional/Targeted: windup → spawn EnemyProjectile từ prefab → recovery.
    private IEnumerator ModuleProjectileRoutine(EnemyAttackModuleData module, bool targeted)
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        _currentAttackMultiplier = module.damageMultiplier > 0f ? module.damageMultiplier : 1f;
        if (stats != null) stats.EnterCombat();

        Vector3 facingDir = ModuleFaceTarget(module);
        yield return ModuleAnimWindup(module);
        if (animator != null) animator.SetTrigger("Attack");

        if (module.projectilePrefab == null)
        {
            if (_warnedStyles.Add(module.style))
                Debug.LogWarning($"[EAM] '{gameObject.name}' style {module.style} thiếu projectilePrefab → bỏ qua đạn.");
        }
        else
        {
            Vector3 spawn = transform.position + facingDir * 0.6f + Vector3.up * 0.5f;
            Quaternion rot = facingDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facingDir) : transform.rotation;
            GameObject go = Instantiate(module.projectilePrefab, spawn, rot);
            EnemyProjectile proj = go.GetComponent<EnemyProjectile>();
            if (proj == null) proj = go.AddComponent<EnemyProjectile>(); // an toàn nếu prefab thiếu script
            proj.Init(stats, facingDir, targeted ? target : null, module.projectileSpeed, module.projectileLifetime,
                      module.damageMultiplier, module.impactBonus, module.effects);
        }

        if (module.recoveryDuration > 0f) yield return new WaitForSeconds(module.recoveryDuration);
        ModuleAttackCleanup();
    }

    // [EAM-02B] GroundTargetAOE: telegraphPrefab tại tâm + delay windup → hit Player/Ally trong aoeRadius.
    private IEnumerator ModuleGroundAoeRoutine(EnemyAttackModuleData module)
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        hitTargets.Clear();
        _currentAttackMultiplier = module.damageMultiplier > 0f ? module.damageMultiplier : 1f;
        if (stats != null) stats.EnterCombat();

        Vector3 facingDir = ModuleFaceTarget(module);
        // Tâm AoE = vị trí target (nếu có) hoặc trước mặt theo range.
        Vector3 center = target != null ? target.position : transform.position + facingDir * Mathf.Max(0.1f, module.range);
        center.y = transform.position.y;

        GameObject tele = (module.telegraphPrefab != null) ? Instantiate(module.telegraphPrefab, center, Quaternion.identity) : null;
        isTelegraphing = true;
        if (module.windupDuration > 0f) yield return new WaitForSeconds(module.windupDuration);
        isTelegraphing = false;
        if (tele != null) Destroy(tele);

        // Active: quét Player/Ally trong aoeRadius quanh tâm (capsule dọc bắt mọi độ cao).
        float r = Mathf.Max(0.1f, module.aoeRadius);
        foreach (var col in Physics.OverlapCapsule(center + Vector3.up * 1.5f, center - Vector3.up * 1.5f, r))
            TryModuleHit(col.transform, module);

        if (module.recoveryDuration > 0f) yield return new WaitForSeconds(module.recoveryDuration);
        ModuleAttackCleanup();
    }

    // [EAM-02B] DashStrike: lướt tới target (mượn external move override của EnemyAI) + cone hit dọc đường.
    private IEnumerator ModuleDashRoutine(EnemyAttackModuleData module)
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        hitTargets.Clear();
        _currentAttackMultiplier = module.damageMultiplier > 0f ? module.damageMultiplier : 1f;
        if (stats != null) stats.EnterCombat();

        Vector3 facingDir = ModuleFaceTarget(module);
        yield return ModuleAnimWindup(module);
        if (animator != null) animator.SetTrigger("Attack");

        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _dashAI = GetComponent<EnemyAI>();
        float dashTime = Mathf.Max(0.05f, module.activeDuration);
        float range = Mathf.Max(0.1f, module.range);

        if (agent != null && agent.isOnNavMesh)
        {
            float baseMv = (stats != null && stats.baseMoveSpeed > 0f) ? stats.baseMoveSpeed : 5f;
            float dashSpeed = range / dashTime;
            // Mượn override để EnemyAI.Update KHÔNG ghi đè destination/speed trong lúc dash; RefreshAgentSpeed vẫn áp Slow.
            if (_dashAI != null) { _dashAI.BeginExternalMoveOverride(dashSpeed / baseMv); _moduleDashOverride = true; }
            agent.isStopped = false;
            agent.SetDestination(transform.position + facingDir * range);
        }

        float t = 0f;
        while (t < dashTime)
        {
            t += Time.deltaTime;
            ModuleConeHit(module, stats != null ? stats.facingDirection : facingDir);
            yield return null;
        }

        if (agent != null && agent.isOnNavMesh) { agent.velocity = Vector3.zero; agent.isStopped = true; }
        EndModuleDashOverride();

        if (module.recoveryDuration > 0f) yield return new WaitForSeconds(module.recoveryDuration);
        ModuleAttackCleanup();
    }

    /// <summary>Trả lại external move override của EnemyAI (idempotent) — gọi ở dash end + khi bị interrupt.</summary>
    private void EndModuleDashOverride()
    {
        if (_moduleDashOverride && _dashAI != null) _dashAI.EndExternalMoveOverride();
        _moduleDashOverride = false;
        _dashAI = null;
    }

    // [EAM-02A] Runtime cho melee/local styles. Timing/telegraph/hình học lấy từ module data.
    private IEnumerator ModuleMeleeRoutine(EnemyAttackModuleData module)
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        hitTargets.Clear();
        // [EAM] Damage multiplier theo MODULE (cả basic lẫn skill) — PerformBasicAttack set 1f không đủ.
        _currentAttackMultiplier = module.damageMultiplier > 0f ? module.damageMultiplier : 1f;
        if (stats != null) stats.EnterCombat();

        // Khóa hướng mặt về target (commit từ telegraph).
        Vector3 facingDir = stats != null ? stats.facingDirection : transform.forward;
        if (module.faceTargetOnStart && target != null)
        {
            Vector3 d = (target.position - transform.position); d.y = 0;
            if (d.sqrMagnitude > 0.0001f) { facingDir = d.normalized; if (stats != null) stats.facingDirection = facingDir; }
        }

        // ── TELEGRAPH (windup) ──
        if (module.useTelegraph && module.windupDuration > 0f)
        {
            isTelegraphing = true;
            if (animator != null)
            {
                if (!string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                    animator.SetBool(telegraphAnimBool, true);
                else if (!string.IsNullOrEmpty(telegraphAnimTrigger) && HasParameter(animator, telegraphAnimTrigger))
                    animator.SetTrigger(telegraphAnimTrigger);
            }
            yield return new WaitForSeconds(module.windupDuration);
            isTelegraphing = false;
            if (animator != null && !string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                animator.SetBool(telegraphAnimBool, false);
        }
        else if (module.windupDuration > 0f)
        {
            yield return new WaitForSeconds(module.windupDuration);
        }

        // Attack animation.
        if (animator != null) animator.SetTrigger("Attack");

        // ── ACTIVE (gây damage) ──
        float active = Mathf.Max(0.01f, module.activeDuration);
        if (module.style == EnemyAttackStyle.MeleeSweep)
        {
            float t = 0f; float prevAngle = module.sweepStartAngle;
            while (t < active)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / active);
                float curAngle = Mathf.Lerp(module.sweepStartAngle, module.sweepEndAngle, k);
                ModuleSweepCheck(module, curAngle, prevAngle, facingDir);
                prevAngle = curAngle;
                yield return null;
            }
        }
        else
        {
            // Single / Thrust / CircleAOE: quét MỘT lần ở đầu active window.
            ModuleApplyInstantHit(module, facingDir);
            yield return new WaitForSeconds(active);
        }

        // ── RECOVERY ──
        if (module.recoveryDuration > 0f) yield return new WaitForSeconds(module.recoveryDuration);

        // Cleanup (giống EnemyAttackRoutine end).
        isAttacking = false;
        _currentIsSkill = false;
        _currentModule = null;
        _currentAttackMultiplier = 1f;
        currentAttackCoroutine = null;
    }

    // Single (cone trong range+angle) / Thrust (box thẳng) / CircleAOE (vòng quanh enemy).
    private void ModuleApplyInstantHit(EnemyAttackModuleData module, Vector3 facingDir)
    {
        if (module.style == EnemyAttackStyle.MeleeCircleAOE)
        {
            foreach (var hit in Physics.OverlapSphere(transform.position, Mathf.Max(0.1f, module.aoeRadius)))
                TryModuleHit(hit.transform, module);
            return;
        }

        if (module.style == EnemyAttackStyle.MeleeThrust)
        {
            // Hộp thẳng phía trước: dài = range, rộng cố định (module không có field rộng riêng).
            float halfLen = Mathf.Max(0.1f, module.range) * 0.5f;
            Vector3 center = transform.position + facingDir * halfLen + Vector3.up * 0.5f;
            Vector3 halfExtents = new Vector3(0.6f, 1f, halfLen);
            Quaternion rot = facingDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facingDir) : transform.rotation;
            foreach (var hit in Physics.OverlapBox(center, halfExtents, rot))
                TryModuleHit(hit.transform, module);
            return;
        }

        // MeleeSingle: cone trong range + attackAngle.
        float halfAngle = Mathf.Max(1f, module.attackAngle) * 0.5f;
        foreach (var hit in Physics.OverlapSphere(transform.position, Mathf.Max(0.1f, module.range)))
        {
            Vector3 to = (hit.transform.position - transform.position); to.y = 0;
            if (to.sqrMagnitude < 0.0001f) { TryModuleHit(hit.transform, module); continue; }
            if (Vector3.Angle(facingDir, to.normalized) <= halfAngle) TryModuleHit(hit.transform, module);
        }
    }

    // Sweep-past detection cho module (dùng module.range).
    private void ModuleSweepCheck(EnemyAttackModuleData module, float currentAngle, float prevAngle, Vector3 facingDir)
    {
        float range = Mathf.Max(0.1f, module.range);
        foreach (var hit in Physics.OverlapSphere(transform.position, range))
        {
            if (hitTargets.Contains(hit.transform)) continue;
            if (!hit.CompareTag("Player") && !hit.CompareTag("Ally")) continue;
            Vector3 to = (hit.transform.position - transform.position); to.y = 0;
            if (to.sqrMagnitude < 0.0001f) continue;
            if (Vector3.Distance(transform.position, hit.transform.position) > range) continue;
            float targetAngle = Vector3.SignedAngle(facingDir, to.normalized, Vector3.up);
            bool sweptPast = currentAngle >= prevAngle
                ? (targetAngle > prevAngle && targetAngle <= currentAngle)
                : (targetAngle < prevAngle && targetAngle >= currentAngle);
            if (sweptPast) TryModuleHit(hit.transform, module);
        }
    }

    // Lọc Player/Ally + dedupe + gây damage qua DealDamageToTarget (đã branch _currentModule ở BƯỚC 2).
    private void TryModuleHit(Transform victim, EnemyAttackModuleData module)
    {
        if (victim == null) return;
        if (!victim.CompareTag("Player") && !victim.CompareTag("Ally")) return;
        if (hitTargets.Contains(victim)) return;
        hitTargets.Add(victim);
        DealDamageToTarget(victim, 0);
    }

protected IEnumerator EnemyAttackRoutine()
    {
        // 1. Setup ban đầu
        isAttacking = true;
        lastAttackTime = Time.time;
        hitTargets.Clear();
        if (stats != null) stats.EnterCombat();

        // Khóa hướng ngay khi bắt đầu chuỗi tấn công — commit từ pha telegraph
        Vector3 lockedFacingDir = stats != null ? stats.facingDirection : transform.forward;
        if (target != null)
        {
            Vector3 dirToTarget = (target.position - transform.position).normalized;
            dirToTarget.y = 0;
            if (dirToTarget != Vector3.zero)
            {
                lockedFacingDir = dirToTarget;
                if (stats != null) stats.facingDirection = lockedFacingDir;
            }
        }

        // ── TELEGRAPH PHASE ───────────────────────────────────────────────────
        // Enemy gồng / tụ lực / làm động tác báo hiệu — player có window để né
        // isAttacking đã = true → EnemyAI sẽ stop movement trong giai đoạn này
        if (useTelegraph && telegraphDuration > 0f)
        {
            isTelegraphing = true;

            if (animator != null)
            {
                // Set THẬT nếu Animator có parameter tương ứng (safe-set, không crash nếu thiếu).
                // Ưu tiên Bool (animation blend dài), nếu không thì dùng Trigger.
                if (!string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                    animator.SetBool(telegraphAnimBool, true);
                else if (!string.IsNullOrEmpty(telegraphAnimTrigger) && HasParameter(animator, telegraphAnimTrigger))
                    animator.SetTrigger(telegraphAnimTrigger);
            }

            yield return new WaitForSeconds(telegraphDuration);

            isTelegraphing = false;

            // Tắt Bool nếu đã bật (safe-set)
            if (animator != null && !string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                animator.SetBool(telegraphAnimBool, false);
        }
        // ─────────────────────────────────────────────────────────────────────

        // [SLOW] Tốc độ đánh hiệu lực = baseAttackSpeed × EffectiveSlowMultiplier → animation + hit window (startN/endN)
        // + recovery chậm ĐỒNG BỘ (Slow tác động cadence thực thi đòn đánh; KHÔNG đụng skillCooldown ở IsSkillReady).
        float effAttackSpeed = Mathf.Max(0.1f, stats.baseAttackSpeed * stats.EffectiveSlowMultiplier);

        // 2. Trigger Attack Animation (sau khi telegraph xong)
        if (animator != null)
        {
            animator.SetFloat("AttackSpeedMultiplier", effAttackSpeed);
            animator.SetTrigger("Attack");
        }

        float realAnimDuration = baseAttackAnimDuration / effAttackSpeed;

        // Cửa sổ gây damage (chỉnh qua Inspector). Clamp hợp lệ: 0 <= start < end <= 1.
        float startN = Mathf.Clamp01(damageWindowStartNormalized);
        float endN   = Mathf.Clamp01(damageWindowEndNormalized);
        if (endN <= startN) endN = Mathf.Min(1f, startN + 0.05f); // đảm bảo end > start

        // Wind-up [0..start]: player nhìn thấy & né. Active [start..end]: hitbox quét. Recovery [end..1]: hở sườn.
        float startDamageTime = realAnimDuration * startN;
        float endDamageTime   = realAnimDuration * endN;
        float swingDuration   = endDamageTime - startDamageTime;

        // 3. Wind-up — player nhìn thấy animation và bấm dash trong giai đoạn này
        yield return new WaitForSeconds(startDamageTime);

        // 4. Sweep — quét từ sweepStartAngle đến sweepEndAngle theo từng frame
        // Hit chỉ register khi góc quét "vượt qua" đúng góc mà player đứng
        float currentSweepTime = 0f;
        float prevAngle = sweepStartAngle;
        while (currentSweepTime < swingDuration)
        {
            currentSweepTime += Time.deltaTime;
            float t = Mathf.Clamp01(currentSweepTime / swingDuration);
            float currentAngle = Mathf.Lerp(sweepStartAngle, sweepEndAngle, t);
            PerformSweepCheck(currentAngle, prevAngle, lockedFacingDir);
            prevAngle = currentAngle;
            yield return null;
        }

        // 5. Recovery — enemy hở sườn trong giai đoạn này
        yield return new WaitForSeconds(realAnimDuration - endDamageTime);

        isAttacking = false;
        _currentIsSkill = false; // reset: đòn sau mặc định là đánh thường
        _currentModule = null;   // [EAM] đòn legacy không gắn module
        currentComboStep++;
        if (currentComboStep >= maxCombo) currentComboStep = 0;
        currentAttackCoroutine = null; // routine kết thúc bình thường
        _currentAttackMultiplier = 1f; // reset về đòn thường cho lần sau
    }

    // [MỚI] Hàm hỗ trợ Cancel Attack (Boss dash gọi trực tiếp). Mặc định GIỮ cooldown skill (như cũ).
    public void CancelAttack() => CancelAttack(keepSkillCooldown: true);

    /// <summary>[INT-01C] Hủy đòn đánh/skill hiện tại + reset đầy đủ state. keepSkillCooldown=false →
    /// un-commit cooldown skill (restore lastSkillTime cũ) khi đòn hiện tại là skill.</summary>
    public void CancelAttack(bool keepSkillCooldown)
    {
        // Không có gì để hủy
        if (currentAttackCoroutine == null && !isAttacking && !isTelegraphing) return;

        // [INT-01C] Un-commit cooldown skill nếu interrupt không "phế" skill.
        if (_currentIsSkill && !keepSkillCooldown) lastSkillTime = _prevSkillTime;

        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }

        isAttacking = false;

        // Đang trong pha telegraph thì StopCoroutine không tự dọn — reset thủ công (safe-set)
        if (isTelegraphing)
        {
            isTelegraphing = false;
            if (animator != null && !string.IsNullOrEmpty(telegraphAnimBool) && HasParameter(animator, telegraphAnimBool))
                animator.SetBool(telegraphAnimBool, false);
        }

        // Reset Animation trigger nếu cần
        if (animator != null) animator.ResetTrigger("Attack");

        // [INT-01C] Reset đầy đủ state để đòn sau sạch sẽ (không kẹt skill multiplier / hit dedupe).
        _currentIsSkill = false;
        _currentAttackMultiplier = 1f;
        _currentModule = null; // [EAM] dọn module của đòn bị hủy
        EndModuleDashOverride(); // [EAM-02B] trả override nếu đang DashStrike (interrupt giữa dash)
        hitTargets.Clear();

        Debug.Log($"{gameObject.name} đã HỦY đòn đánh!");
    }

    /// <summary>True nếu Animator có parameter tên paramName (an toàn — set vào param không tồn tại sẽ spam warning).</summary>
    protected static bool HasParameter(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (var p in anim.parameters)
            if (p.name == paramName) return true;
        return false;
    }

    // Hàm kiểm tra va chạm và gây damage
    //void CheckHitAndDealDamage()
    //{
    //    // Tìm tất cả đối tượng trong tầm đánh (Sphere)
    //    Collider[] hits = Physics.OverlapSphere(transform.position, basicAttackRange);

    //    foreach (var hit in hits)
    //    {
    //        // Chỉ quan tâm đến Player hoặc Ally
    //        if (hit.CompareTag("Player") || hit.CompareTag("Ally"))
    //        {
    //            // [MỚI] CHECK GÓC (CONE CHECK)
    //            Vector3 dirToHit = (hit.transform.position - transform.position).normalized;
    //            Vector3 facingDir = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;

    //            float angle = Vector3.Angle(facingDir, dirToHit);

    //            // Nếu nằm ngoài góc đánh -> Bỏ qua
    //            if (angle > attackAngle / 2f) continue;

    //            // Nếu thỏa mãn -> Gây damage
    //            DealDamageToTarget(hit.transform, currentComboStep);
    //        }
    //    }
    //}

    // Sweep-past detection: hit chỉ register khi góc quét đi qua đúng góc mà target đứng.
    // Ví dụ: player ở 30°, sweep từ 20°→32° trong frame này → HIT.
    // Nếu player dash ra khỏi range trước frame đó → MISS (vì dist check fail).
    void PerformSweepCheck(float currentAngle, float prevAngle, Vector3 lockedFacingDir)
    {
        // Quét toàn bộ range một lần, check từng target
        Collider[] hits = Physics.OverlapSphere(transform.position, basicAttackRange);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player") && !hit.CompareTag("Ally")) continue;
            if (hitTargets.Contains(hit.transform)) continue;

            // 1. Phải trong tầm đánh thực tế
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist > basicAttackRange) continue;

            // 2. Tính góc SIGNED của target so với lockedFacingDir (trong mặt phẳng XZ)
            Vector3 toTarget = (hit.transform.position - transform.position);
            toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.001f) continue;
            float targetAngle = Vector3.SignedAngle(lockedFacingDir, toTarget.normalized, Vector3.up);

            // 3. Hit khi sweep vượt qua góc của target trong frame này
            // Cả 2 chiều (prevAngle→currentAngle âm hoặc dương)
            bool sweptPast;
            if (currentAngle >= prevAngle)
                sweptPast = targetAngle > prevAngle && targetAngle <= currentAngle;
            else
                sweptPast = targetAngle < prevAngle && targetAngle >= currentAngle;

            if (!sweptPast) continue;

            hitTargets.Add(hit.transform);
            DealDamageToTarget(hit.transform, currentComboStep);
        }
    }

    // Hàm tính toán và gửi damage
    void DealDamageToTarget(Transform victim, int step)
    {
        Debug.Log($"{gameObject.name} CHÉM TRÚNG {victim.name}");

        Stats victimStats = victim.GetComponent<Stats>();
        if (victimStats != null)
        {
            float t = CombatMath.CalculateDirectionFactor(transform, victimStats);

            // --- BƯỚC 1: INFO ---
            DamageInfo info = new DamageInfo();
            info.sourcePosition = transform.position;
            info.attacker = stats;
            info.impactLevel = stats.monsterRank;

            // --- BƯỚC 2: HIỆU ỨNG (CC) ---
            // [EAM] Đòn module → dùng effects của module (clone per-target, +impactBonus). Bỏ qua skillEffects/hard-code.
            if (_currentModule != null)
            {
                AddModuleEffects(info, _currentModule);
            }
            else
            {
                // [SKILL EFFECTS] AUTHORITATIVE — đòn SKILL áp các CombatEffectInfo (Silence/Stun/Airborne/Root...)
                // TRƯỚC. CLONE từng effect cho mỗi target (sourcePosition riêng) → tránh mutate list dùng chung.
                if (_currentIsSkill && skillEffects != null)
                {
                    foreach (var src in skillEffects)
                    {
                        if (src == null) continue;
                        info.AddEffect(new CombatEffectInfo(src.type, src.duration)
                        {
                            force = src.force,
                            height = src.height,
                            magnitude = src.magnitude,
                            impactLevel = src.impactLevel,
                            sourcePosition = transform.position,
                            respectEffectResistance = src.respectEffectResistance,
                            interruptCurrentAction = src.interruptCurrentAction,
                            putInterruptedSkillOnCooldown = src.putInterruptedSkillOnCooldown,
                            note = src.note,
                        });
                    }
                }

                // [HARD-CODE FALLBACK theo enemyID] (chỉ áp khi KHÔNG dùng module). CHỈ add khi skillEffects
                // CHƯA có effect cùng loại (skill effect thắng) → mỗi loại ≤1 effect/đòn.
                // Boss Golem
                if (stats.enemyID == "Boss_Golem" && !info.HasEffect(CombatEffectType.Knockback))
                {
                    info.AddEffect(new CombatEffectInfo(CombatEffectType.Knockback, 0f)
                    { force = 12f, impactLevel = info.impactLevel, sourcePosition = info.sourcePosition, respectEffectResistance = false });
                }
                // Orc Warrior (Đòn cuối combo)
                if (stats.enemyID == "Orc_Warrior" && step == 2)
                {
                    if (!info.HasEffect(CombatEffectType.Stun))
                        info.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, 1.0f)
                        { impactLevel = info.impactLevel, sourcePosition = info.sourcePosition });
                    if (!info.HasEffect(CombatEffectType.Knockback))
                        info.AddEffect(new CombatEffectInfo(CombatEffectType.Knockback, 0f)
                        { force = 8f, impactLevel = info.impactLevel, sourcePosition = info.sourcePosition, respectEffectResistance = false });
                }
                // Odo (Đòn 2)
                if (stats.enemyID == "Odo" && step == 1 && !info.HasEffect(CombatEffectType.Knockback))
                {
                    info.AddEffect(new CombatEffectInfo(CombatEffectType.Knockback, 0f)
                    { force = 8f, impactLevel = info.impactLevel, sourcePosition = info.sourcePosition, respectEffectResistance = false });
                }
            }

            // --- BƯỚC 3: TÍNH DAMAGE ---
            bool isCrit = CombatMath.CheckIsCrit(stats.baseCritChance);
            info.isCrit = isCrit;
            if (isCrit) Debug.Log($"<color=orange>{gameObject.name} CRITS!</color>");

            // Gọi CombatMath (Nhớ cập nhật tham số ignoreReduction nếu cần, ở đây Enemy thường ko có True Damage nên để false)
            // externalMult = _currentAttackMultiplier: đòn thường = 1, skill = skillPhysicalMultiplier.
            var dmgTuple = CombatMath.CalculateFullDamage(
                stats,
                victimStats,
                t,
                isCrit,
                null,
                null,
                _currentAttackMultiplier,
                false // Enemy đánh thường không xuyên giáp
            );

            info.physDamage = dmgTuple.phys;
            info.magicDamage = dmgTuple.magic;
            info.trueDamage = dmgTuple.trueDmg;

            // --- BƯỚC 4: GỬI --- (skillEffects + hard-code CC đã gắn ở BƯỚC 2)
            victimStats.TakeDamage(info);

            // [COMBAT FEEL OWNER] Camera shake / hit-stop CHỈ thuộc về Player đánh trúng (game feel cho người chơi).
            // Enemy đánh player KHÔNG gây camera shake (theo yêu cầu). Hit flash + damage number vẫn chạy
            // qua HitFlash (tự lắng nghe OnDamageReceived) và DamageNumberManager — không phụ thuộc chỗ này.
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // Vẽ Gizmos để debug tầm đánh của Enemy
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, basicAttackRange);

        Vector3 forward = (stats != null && stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;
        Vector3 leftRay = Quaternion.AngleAxis(-attackAngle / 2, Vector3.up) * forward;
        Vector3 rightRay = Quaternion.AngleAxis(attackAngle / 2, Vector3.up) * forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, leftRay * basicAttackRange);
        Gizmos.DrawRay(transform.position, rightRay * basicAttackRange);
    }

    // Sau này sẽ có thêm: PerformSkill(string skillID) ...
}