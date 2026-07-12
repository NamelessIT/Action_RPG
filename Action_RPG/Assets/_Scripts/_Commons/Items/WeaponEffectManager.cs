using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponEffectManager : MonoBehaviour
{
    private PlayerController player;
    private AllyStats stats;
    private EquipmentManager eqManager;
    private SkillManager skillManager;

    private Dictionary<string, Action<Stats, int, bool, bool>> onHitEnemyEffects = new Dictionary<string, Action<Stats, int, bool, bool>>();
    private Dictionary<string, Action<Stats, bool>> onKillEnemyEffects = new Dictionary<string, Action<Stats, bool>>();
    private Dictionary<string, Action> onSkillCastEffects = new Dictionary<string, Action>();
    private Dictionary<string, Action> onSignatureCastEffects = new Dictionary<string, Action>();
    private Dictionary<string, Action> onDashEffects = new Dictionary<string, Action>();
    private Dictionary<string, Action> onParryEffects = new Dictionary<string, Action>();
    private Dictionary<string, Action> onPerfectDodgeEffects = new Dictionary<string, Action>();
    private Dictionary<string, Action> onHeavyAttackEffects = new Dictionary<string, Action>();
    private Dictionary<string, Action<float>> onSinConsumedEffects = new Dictionary<string, Action<float>>();
    private Dictionary<string, Action<DamageInfo>> onBeforeTakeDamageEffects = new Dictionary<string, Action<DamageInfo>>();
    // Hệ số nhân sát thương đòn vũ khí (đánh thường/heavy) theo passive: nhận target, trả hệ số (1 = không đổi).
    private Dictionary<string, Func<Stats, float>> basicDamageMultipliers = new Dictionary<string, Func<Stats, float>>();
    // Sự kiện khi nhận Heal (amount, excess, source) — định tuyến theo vũ khí.
    private Dictionary<string, Action<float, float, HealSource>> onHealEffects = new Dictionary<string, Action<float, float, HealSource>>();
    // Sự kiện khi KỸ NĂNG trúng kẻ địch (target, isMagic, isCrit) — định tuyến theo vũ khí.
    private Dictionary<string, Action<Stats, bool, bool>> onSkillHitEffects = new Dictionary<string, Action<Stats, bool, bool>>();

    private int attackHitCounter = 0;
    private float lastHitTime = 0f;

    // Cờ "đòn đánh kế tiếp xuyên giáp + chắc crit" (vd SP_T4_03). PlayerController tiêu thụ qua ConsumeArmorPenCritHit().
    private bool nextHitArmorPenCrit = false;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        stats = GetComponent<AllyStats>();
        eqManager = GetComponent<EquipmentManager>();
        skillManager = GetComponentInChildren<SkillManager>();

        RegisterAllWeaponEffects();
    }

    private void RegisterAllWeaponEffects()
    {
        onHitEnemyEffects.Add("WPN_SW_T3_01", Effect_WPN_SW_T3_01);
        onHitEnemyEffects.Add("WPN_SW_T3_02", Effect_WPN_SW_T3_02);
        onSkillCastEffects.Add("WPN_SW_T3_03", Effect_WPN_SW_T3_03_Trigger);
        onSignatureCastEffects.Add("WPN_SW_T3_03", Effect_WPN_SW_T3_03_Trigger);
        onHitEnemyEffects.Add("WPN_SW_T3_03", Effect_WPN_SW_T3_03_Hit);

        onSkillCastEffects.Add("WPN_SW_T4_01", Effect_WPN_SW_T4_01);
        onSignatureCastEffects.Add("WPN_SW_T4_01", Effect_WPN_SW_T4_01);
        onHitEnemyEffects.Add("WPN_SW_T4_02", Effect_WPN_SW_T4_02);
        onHitEnemyEffects.Add("WPN_SW_T4_03", Effect_WPN_SW_T4_03);
        onSkillCastEffects.Add("WPN_SW_T4_04", Effect_WPN_SW_T4_04_Trigger);
        onSignatureCastEffects.Add("WPN_SW_T4_04", Effect_WPN_SW_T4_04_Trigger);
        onHitEnemyEffects.Add("WPN_SW_T4_04", Effect_WPN_SW_T4_04_Hit);
        onPerfectDodgeEffects.Add("WPN_SW_T4_05", Effect_WPN_SW_T4_05); // [SỬA] Perfect DODGE, không phải Parry

        onDashEffects.Add("WPN_SW_T5_01", Effect_WPN_SW_T5_01);
        // T5_02: Nằm trong ChainsawAttackHandler
        onHitEnemyEffects.Add("WPN_SW_T5_04", Effect_WPN_SW_T5_04_Mark);
        onDashEffects.Add("WPN_SW_T5_04", Effect_WPN_SW_T5_04_Dash);

        onHitEnemyEffects.Add("WPN_DG_T3_01", Effect_WPN_DG_T3_01);
        onHitEnemyEffects.Add("WPN_DG_T3_02", Effect_WPN_DG_T3_02);
        onHitEnemyEffects.Add("WPN_DG_T3_03", Effect_WPN_DG_T3_03);

        onHitEnemyEffects.Add("WPN_DG_T4_01", Effect_WPN_DG_T4_01);
        onKillEnemyEffects.Add("WPN_DG_T4_02", Effect_WPN_DG_T4_02);
        onHitEnemyEffects.Add("WPN_DG_T4_03", Effect_WPN_DG_T4_03);
        onSkillCastEffects.Add("WPN_DG_T4_04", Effect_WPN_DG_T4_04_Trigger);
        onSignatureCastEffects.Add("WPN_DG_T4_04", Effect_WPN_DG_T4_04_Trigger);
        onHitEnemyEffects.Add("WPN_DG_T4_04", Effect_WPN_DG_T4_04_Hit);

        onHitEnemyEffects.Add("WPN_DG_T5_01", Effect_WPN_DG_T5_01);
        onDashEffects.Add("WPN_DG_T5_02", Effect_WPN_DG_T5_02);
        onKillEnemyEffects.Add("WPN_DG_T5_03", Effect_WPN_DG_T5_03);

        // ===== SPEAR =====
        // SP_T3_01: passive HP>70% → xử lý trong Update.
        basicDamageMultipliers.Add("WPN_SP_T3_02", SP_T3_02_Mult);
        onDashEffects.Add("WPN_SP_T3_03", SP_T3_03_Dash);
        onHitEnemyEffects.Add("WPN_SP_T3_03", SP_T3_03_Hit);
        onPerfectDodgeEffects.Add("WPN_SP_T4_01", SP_T4_01_Dodge);
        onDashEffects.Add("WPN_SP_T4_02", SP_T4_02_Dash);
        onHitEnemyEffects.Add("WPN_SP_T4_02", SP_T4_02_Hit);
        onSkillCastEffects.Add("WPN_SP_T4_03", SP_T4_03_Skill);
        onSignatureCastEffects.Add("WPN_SP_T4_03", SP_T4_03_Skill);
        onHeavyAttackEffects.Add("WPN_SP_T4_04", SP_T4_04_Heavy);
        onHitEnemyEffects.Add("WPN_SP_T4_05", SP_T4_05_Hit);
        onSkillCastEffects.Add("WPN_SP_T5_02", SP_T5_02_Skill);
        onSignatureCastEffects.Add("WPN_SP_T5_02", SP_T5_02_Skill);
        onBeforeTakeDamageEffects.Add("WPN_SP_T5_03", SP_T5_03_Absorb);
        onHitEnemyEffects.Add("WPN_SP_T5_03", SP_T5_03_Release);
        onDashEffects.Add("WPN_SP_T5_04", SP_T5_04_Dash);
        // SP_T5_01 (giáo bẻ góc xuyên): override đòn đánh thường → xử lý qua SpearChainAttackHandler (WeaponAttackDispatcher).

        // ===== GREATSWORD =====
        basicDamageMultipliers.Add("WPN_GS_T3_01", GS_T3_01_Mult);
        onHitEnemyEffects.Add("WPN_GS_T3_01", GS_T3_01_Hit);
        onBeforeTakeDamageEffects.Add("WPN_GS_T3_02", GS_T3_02_Reduce);
        onHitEnemyEffects.Add("WPN_GS_T3_03", GS_T3_03_Hit);
        onHeavyAttackEffects.Add("WPN_GS_T4_01", GS_T4_01_Heavy);
        // GS_T4_02 (armor→STR), GS_T4_04 (missingHP→STR/LS), GS_T5_01 (CC immune/atkSpd/armor shred): trong Update.
        onKillEnemyEffects.Add("WPN_GS_T4_03", GS_T4_03_Kill);
        basicDamageMultipliers.Add("WPN_GS_T4_03", GS_T4_03_Mult);
        onHitEnemyEffects.Add("WPN_GS_T4_03", GS_T4_03_Hit);
        onHitEnemyEffects.Add("WPN_GS_T5_01", GS_T5_01_Hit);
        onBeforeTakeDamageEffects.Add("WPN_GS_T5_02", GS_T5_02_Lethal);
        onHeavyAttackEffects.Add("WPN_GS_T5_03", GS_T5_03_Heavy);

        // ===== BOW =====
        // BW_T3_01: passive (Update) — gồng bắn không giảm tốc.
        basicDamageMultipliers.Add("WPN_BW_T3_02", BW_T3_02_Mult);
        basicDamageMultipliers.Add("WPN_BW_T3_03", BW_T3_03_Mult);
        onHeavyAttackEffects.Add("WPN_BW_T4_01", BW_T4_01_Heavy);
        onHitEnemyEffects.Add("WPN_BW_T4_03", BW_T4_03_Hit);
        onHitEnemyEffects.Add("WPN_BW_T5_02", BW_T5_02_Hit);
        // BW_T5_01 (Tia Sáng Mặt Trời) & BW_T5_02 (Chim Ánh Trăng homing): xử lý qua pipeline bắn
        //   (cờ AllyStats.bowSunBeam/bowHoming + RangedAttackHandler/BowHeavyAttack/Projectile), không qua dict.
        // BW_T4_02: phys→phép qua weaponAtkType=Magic (asset) + homing qua cờ bowHoming (xem Update).

        // ===== STAFF =====
        // ST_T3_01: passive (Update) — giảm 15% phí Sin của Signature.
        onSinConsumedEffects.Add("WPN_ST_T3_02", ST_T3_02_OnSin);
        // ST_T3_03: passive (Update) — đứng yên 2s tăng MagicAtk + CritChance.
        onSkillCastEffects.Add("WPN_ST_T4_02", ST_T4_02_Skill);
        onSignatureCastEffects.Add("WPN_ST_T4_02", ST_T4_02_Skill);
        // ST_T4_04: passive (Update) — bao vây bởi >3 địch.
        basicDamageMultipliers.Add("WPN_ST_T5_01", ST_T5_01_Mult);
        onHealEffects.Add("WPN_ST_T5_02", ST_T5_02_OnHeal);
        onHitEnemyEffects.Add("WPN_ST_T5_03", ST_T5_03_Hit);
        // ST_T4_01: kỹ năng gây sát thương phép + chí mạng → Vụ Nổ Phép (dedupe theo lần dùng kỹ năng).
        onSkillHitEffects.Add("WPN_ST_T4_01", ST_T4_01_SkillHit);
        onSkillCastEffects.Add("WPN_ST_T4_01", ST_T4_01_ResetUse);
        onSignatureCastEffects.Add("WPN_ST_T4_01", ST_T4_01_ResetUse);
        // ST_T4_03: kỹ năng trúng đích → Dây Xích Hư Không (Stun 3s + rút máu 30% magicAtk/s + chậm 40%).
        onSkillHitEffects.Add("WPN_ST_T4_03", ST_T4_03_SkillHit);
        // ST_T4_05 (quest-gated): SKIP (note).

        // ===== GRIMOIRE =====
        onSkillCastEffects.Add("WPN_GR_T3_01", GR_T3_01_Cast);
        onSignatureCastEffects.Add("WPN_GR_T3_01", GR_T3_01_Cast);
        // GR_T3_02: passive (Update) — Sin → bonusMagicAtk.
        onPerfectDodgeEffects.Add("WPN_GR_T3_03", GR_T3_03_Dodge);
        onSkillCastEffects.Add("WPN_GR_T3_03", GR_T3_03_Cast);
        onSignatureCastEffects.Add("WPN_GR_T3_03", GR_T3_03_Cast);
        onSignatureCastEffects.Add("WPN_GR_T4_02", GR_T4_02_Signature);
        // GR_T4_02: phí Sin Signature +15% (Update).
        onDashEffects.Add("WPN_GR_T4_03", GR_T4_03_Dash);
        onHitEnemyEffects.Add("WPN_GR_T5_02", GR_T5_02_Hit);
        // GR_T5_02: decay stack (Update).
        onSignatureCastEffects.Add("WPN_GR_T5_03", GR_T5_03_Signature);
        // GR_T4_04 (Phi Dao): override đánh thường/heavy qua RangedAttackHandler/GrimoireHeavyAttack + ApplyDamageToTarget(grimoirePhiDao),
        //   và bị loại khỏi check Grimoire ở MagePassive/MageSkill/TricksterSkill. Không qua dict.
        onSignatureCastEffects.Add("WPN_GR_T4_01", GR_T4_01_Signature);
        onKillEnemyEffects.Add("WPN_GR_T5_01", GR_T5_01_Kill);

        // ===== HEAL ROUTER =====
        onHealEffects.Add("WPN_SW_T5_03", SW_T5_03_OnHeal);
    }

    void OnEnable()
    {
        if (player != null)
        {
            player.OnHitEnemy += HandleOnHitEnemy;
            player.OnKillEnemy += HandleOnKillEnemy;
            player.OnDashPerformed += HandleOnDash;
            player.OnAttackPerformed += HandleOnAttackPerformed;
            player.OnSkillHitEnemy += HandleOnSkillHit;
        }
        if (stats != null)
        {
            if (stats is PlayerStats playerStats) playerStats.OnPerfectParryTriggered += HandleOnPerfectParry;
            stats.OnHealDetailed += HandleOnHealDetailed;
            stats.OnPerfectDodgeTriggered += HandleOnPerfectDodge;
            stats.OnSinConsumed += HandleOnSinConsumed;
            stats.OnBeforeTakeDamage += HandleOnBeforeTakeDamage;
        }
    }

    void OnDisable()
    {
        if (player != null)
        {
            player.OnHitEnemy -= HandleOnHitEnemy;
            player.OnKillEnemy -= HandleOnKillEnemy;
            player.OnDashPerformed -= HandleOnDash;
            player.OnAttackPerformed -= HandleOnAttackPerformed;
            player.OnSkillHitEnemy -= HandleOnSkillHit;
        }
        if (stats != null)
        {
            if (stats is PlayerStats playerStats) playerStats.OnPerfectParryTriggered -= HandleOnPerfectParry;
            stats.OnHealDetailed -= HandleOnHealDetailed;
            stats.OnPerfectDodgeTriggered -= HandleOnPerfectDodge;
            stats.OnSinConsumed -= HandleOnSinConsumed;
            stats.OnBeforeTakeDamage -= HandleOnBeforeTakeDamage;
        }
    }

    // ==========================================
    // API cho PlayerController (hook đòn đánh thường)
    // ==========================================
    /// <summary>Hệ số nhân sát thương đòn đánh thường/heavy theo passive vũ khí (khoảng cách, shield, Sin, stack...).</summary>
    public float GetBasicAttackDamageMultiplier(Stats target)
    {
        if (eqManager == null || eqManager.currentWeapon == null) return 1f;
        string id = eqManager.currentWeapon.id.Trim();
        if (basicDamageMultipliers.TryGetValue(id, out var fn)) return Mathf.Max(0f, fn(target));
        return 1f;
    }
    /// <summary>Tiêu thụ cờ "đòn kế tiếp xuyên giáp + chắc crit" (trả true 1 lần).</summary>
    public bool ConsumeArmorPenCritHit()
    {
        if (!nextHitArmorPenCrit) return false;
        nextHitArmorPenCrit = false;
        return true;
    }

    private void HandleOnAttackPerformed(int step, bool isHeavy)
    {
        if (!isHeavy) return; // chỉ quan tâm Heavy Attack
        if (eqManager.currentWeapon != null && onHeavyAttackEffects.ContainsKey(eqManager.currentWeapon.id.Trim()))
            onHeavyAttackEffects[eqManager.currentWeapon.id.Trim()].Invoke();
    }
    private void HandleOnSinConsumed(float amount)
    {
        if (eqManager.currentWeapon != null && onSinConsumedEffects.ContainsKey(eqManager.currentWeapon.id.Trim()))
            onSinConsumedEffects[eqManager.currentWeapon.id.Trim()].Invoke(amount);
    }
    private void HandleOnBeforeTakeDamage(DamageInfo info)
    {
        if (eqManager.currentWeapon != null && onBeforeTakeDamageEffects.ContainsKey(eqManager.currentWeapon.id.Trim()))
            onBeforeTakeDamageEffects[eqManager.currentWeapon.id.Trim()].Invoke(info);
    }

    // ==========================================
    // ROUTERS
    // ==========================================
    private void HandleOnHitEnemy(Stats target, int step, bool isHeavy, bool isCrit)
    {
        if (eqManager.currentWeapon == null) return;
        string wpnId = eqManager.currentWeapon.id.Trim();

        if (Time.time - lastHitTime > 3f) attackHitCounter = 0;
        attackHitCounter++;
        lastHitTime = Time.time;

        if (onHitEnemyEffects.ContainsKey(wpnId)) onHitEnemyEffects[wpnId].Invoke(target, step, isHeavy, isCrit);
    }
    private void HandleOnKillEnemy(Stats target, bool isBackstab)
    {
        if (eqManager.currentWeapon != null && onKillEnemyEffects.ContainsKey(eqManager.currentWeapon.id.Trim()))
            onKillEnemyEffects[eqManager.currentWeapon.id.Trim()].Invoke(target, isBackstab);
    }
    private void HandleOnDash()
    {
        if (eqManager.currentWeapon != null && onDashEffects.ContainsKey(eqManager.currentWeapon.id.Trim()))
            onDashEffects[eqManager.currentWeapon.id.Trim()].Invoke();
    }
    private void HandleOnPerfectParry()
    {
        if (eqManager.currentWeapon != null && onParryEffects.ContainsKey(eqManager.currentWeapon.id.Trim()))
            onParryEffects[eqManager.currentWeapon.id.Trim()].Invoke();
    }
    private void HandleOnPerfectDodge()
    {
        if (eqManager.currentWeapon != null && onPerfectDodgeEffects.ContainsKey(eqManager.currentWeapon.id.Trim()))
            onPerfectDodgeEffects[eqManager.currentWeapon.id.Trim()].Invoke();
    }
    public void TriggerWeaponSkillEffects(SkillData.SkillType skillType)
    {
        if (eqManager == null || eqManager.currentWeapon == null) return;
        string wpnId = eqManager.currentWeapon.id.Trim();

        if (skillType == SkillData.SkillType.Skill && onSkillCastEffects.ContainsKey(wpnId)) onSkillCastEffects[wpnId].Invoke();
        else if (skillType == SkillData.SkillType.Signature && onSignatureCastEffects.ContainsKey(wpnId)) onSignatureCastEffects[wpnId].Invoke();
    }

    // =========================================================================================
    #region [ KIẾM - SWORD TIER 3 & 4 ]

    private void Effect_WPN_SW_T3_01(Stats target, int step, bool isH, bool isC)
    {
        if (attackHitCounter % 3 == 0)
        {
            Collider[] hits = Physics.OverlapSphere(target.transform.position, 2f, player.dangerLayer);
            foreach (var hit in hits)
            {
                Stats e = hit.GetComponent<Stats>();
                if (e != null) DamageHelper.ApplyQuickProcDamage(stats, e, 0f, 0.3f, transform);
            }
            VisualDebugHelper.DrawSphere(target.transform.position, 2f, new Color(0, 1, 1, 0.5f), 0.5f);
        }
    }

    private int sw_t3_02_lastStep = -1;
    private void Effect_WPN_SW_T3_02(Stats target, int step, bool isH, bool isC)
    {
        if (sw_t3_02_lastStep != step)
        {
            sw_t3_02_lastStep = step;
            stats.currentStamina = Mathf.Min(stats.maxStamina, stats.currentStamina + 2f);
        }
    }

    private float sw_t3_03_empowerUntil = -1f;
    private void Effect_WPN_SW_T3_03_Trigger() { sw_t3_03_empowerUntil = Time.time + 3f; } // buff tồn tại 3s
    private void Effect_WPN_SW_T3_03_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (Time.time > sw_t3_03_empowerUntil) return; // hết 3s → không kích
        sw_t3_03_empowerUntil = -1f;
        DamageHelper.ApplyQuickProcDamage(stats, target, 0.5f, 0f, transform);
        StartCoroutine(ReduceArmorRoutine(target, 0.15f, 3f));
        VisualDebugHelper.DrawSphere(target.transform.position, 1.5f, new Color(0, 0, 1, 0.5f), 0.5f); // Sóng xanh lam
    }

    private float sw_t4_01_lastProc = -10f;
    private void Effect_WPN_SW_T4_01()
    {
        if (Time.time - sw_t4_01_lastProc < 3f) return;
        sw_t4_01_lastProc = Time.time;

        Collider[] hits = Physics.OverlapSphere(transform.position, 5f, player.dangerLayer);
        if (hits.Length > 0)
        {
            Stats target1 = hits[UnityEngine.Random.Range(0, hits.Length)].GetComponent<Stats>();
            Stats target2 = hits[UnityEngine.Random.Range(0, hits.Length)].GetComponent<Stats>();
            if (target1)
            {
                DamageHelper.ApplyQuickProcDamage(stats, target1, 1.2f, 0f, transform);
                VisualDebugHelper.DrawBox(target1.transform.position + Vector3.up * 2, new Vector3(0.2f, 1.5f, 0.2f), Quaternion.identity, Color.white, 0.5f);
            }
            if (target2)
            {
                DamageHelper.ApplyQuickProcDamage(stats, target2, 0f, 1.2f, transform);
                VisualDebugHelper.DrawBox(target2.transform.position + Vector3.up * 2, new Vector3(0.2f, 1.5f, 0.2f), Quaternion.identity, Color.magenta, 0.5f);
            }
        }
    }

    private void Effect_WPN_SW_T4_02(Stats target, int step, bool isH, bool isC)
    {
        if (UnityEngine.Random.value <= 0.25f)
        {
            float dps = (stats.armor + stats.magicResist) * 0.3f;
            target.ApplyBurn(dps, 3f);
        }
    }

    // [ĐÃ SỬA] Reset toán học cực chuẩn
    private int sw_t4_03_activeStacks = 0;
    private float sw_t4_03_addedSpd = 0f;
    private float sw_t4_03_addedLs = 0f;
    private Coroutine sw_t4_03_coro;
    private void Effect_WPN_SW_T4_03(Stats target, int step, bool isH, bool isC)
    {
        if (sw_t4_03_activeStacks < 5)
        {
            sw_t4_03_activeStacks++;
            sw_t4_03_addedSpd += 0.05f;
            sw_t4_03_addedLs += 0.02f;

            stats.bonusAttackSpeed += 0.05f;
            stats.physicalLifeSteal += 0.02f;
            stats.RecalculateStats();
        }
        if (sw_t4_03_coro != null) StopCoroutine(sw_t4_03_coro);
        sw_t4_03_coro = StartCoroutine(Reset_SW_T4_03());
    }
    private IEnumerator Reset_SW_T4_03()
    {
        yield return new WaitForSeconds(4f);
        stats.bonusAttackSpeed -= sw_t4_03_addedSpd;
        stats.physicalLifeSteal -= sw_t4_03_addedLs;

        sw_t4_03_activeStacks = 0;
        sw_t4_03_addedSpd = 0f;
        sw_t4_03_addedLs = 0f;
        stats.RecalculateStats();
    }

    // [ĐÃ SỬA] WPN_SW_T4_04 Lưỡi liềm Aether
    private float sw_t4_04_until = -1f;
    private void Effect_WPN_SW_T4_04_Trigger() { sw_t4_04_until = Time.time + 3f; Debug.Log("T4_04 Sẵn sàng Liềm! (3s)"); }
    private void Effect_WPN_SW_T4_04_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (Time.time <= sw_t4_04_until)
        {
            sw_t4_04_until = -1f;

            Vector3 center = transform.position + stats.facingDirection * 2f;
            Vector3 halfExtents = new Vector3(2f, 1f, 2f);

            // Vẽ hộp chữ nhật màu tím giả làm Lưỡi Liềm quét qua
            VisualDebugHelper.DrawBox(center, halfExtents * 2f, Quaternion.LookRotation(stats.facingDirection), new Color(0.6f, 0, 1, 0.6f), 0.5f);

            Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.LookRotation(stats.facingDirection), player.dangerLayer);
            foreach (var hit in hits)
            {
                Stats e = hit.GetComponent<Stats>();
                if (e != null)
                {
                    DamageHelper.ApplyQuickProcDamage(stats, e, 0f, 1.0f, transform);
                    e.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, 3f) { magnitude = 0.3f }, stats);
                }
            }
        }
    }

    private void Effect_WPN_SW_T4_05()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, 2.0f, player.dangerLayer); // Đổi bán kính 2f cho dễ trúng
        foreach (var e in enemies)
        {
            Stats eStats = e.GetComponent<Stats>();
            if (eStats != null)
            {
                var info = new DamageInfo { physDamage = 0 };
                info.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, 2f));
                eStats.TakeDamage(info);
            }
        }
        stats.Heal(stats.maxHp * 0.03f);
        if (skillManager != null) skillManager.ReduceAllCooldowns(1f);
        VisualDebugHelper.DrawSphere(transform.position, 2f, new Color(1, 1, 0, 0.3f), 0.5f);
    }
    #endregion

    // =========================================================================================
    #region [ KIẾM - SWORD TIER 5 ]

    private void Effect_WPN_SW_T5_01()
    {
        Vector3 startPos = transform.position;
        Vector3 dashDir = (stats.facingDirection != Vector3.zero) ? stats.facingDirection : transform.forward;
        Vector3 endPos = startPos + dashDir * stats.baseDashDistance;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;
        player.transform.position = endPos;
        player.isDashing = false;

        GameObject rift = new GameObject("SpatialRift_SW_T5_01");
        rift.transform.position = (startPos + endPos) / 2f;
        rift.transform.rotation = Quaternion.LookRotation(endPos - startPos);

        LineRenderer lr = rift.AddComponent<LineRenderer>();
        lr.SetPositions(new Vector3[] { startPos, endPos });
        lr.startWidth = 0.2f; lr.endWidth = 0.2f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.magenta; lr.endColor = Color.cyan;

        BoxCollider box = rift.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.5f, 2f, Vector3.Distance(startPos, endPos));

        SpatialRiftDamage dmgScript = rift.AddComponent<SpatialRiftDamage>();
        dmgScript.Initialize(stats.flatSTR * 2f, player.dangerLayer);

        Destroy(rift, 2f);
    }

    // [ĐÃ FIX LỖI T5_03] - Hiệu ứng bọt biển bảo vệ khi Overheal
    private float sw_t5_03_currentShield = 0f;
    private Coroutine sw_t5_03_coro;
    private GameObject sw_t5_03_visualBubble;

    // Router: định tuyến sự kiện Heal theo vũ khí đang trang bị (đăng ký trong onHealEffects).
    private void HandleOnHealDetailed(float amount, float excessAmount, HealSource source)
    {
        if (eqManager.currentWeapon == null) return;
        if (onHealEffects.TryGetValue(eqManager.currentWeapon.id.Trim(), out var fn)) fn(amount, excessAmount, source);
    }

    private void SW_T5_03_OnHeal(float amount, float excessAmount, HealSource source)
    {
        // Bỏ qua hồi máu TỰ NHIÊN (Regen): khi đứng đầy máu, regen tick liên tục tạo "excess ảo"
        // → trước đây cứ bị đánh rồi hồi đầy là cộng thêm shield. Chỉ tính overheal từ heal thật.
        if (source == HealSource.Regen) return;
        {
            if (excessAmount > 0)
            {
                float currentMaxShieldFromThis = stats.maxHp;
                float granted = Mathf.Min(excessAmount, currentMaxShieldFromThis - sw_t5_03_currentShield);

                if (granted > 0)
                {
                    stats.currentShield += granted;
                    sw_t5_03_currentShield += granted;
                    stats.superArmorLevel = 99;

                    if (sw_t5_03_coro != null) StopCoroutine(sw_t5_03_coro);
                    sw_t5_03_coro = StartCoroutine(Reset_SW_T5_03_Shield());

                    // Visual Bubble bám theo nhân vật
                    if (sw_t5_03_visualBubble == null)
                    {
                        sw_t5_03_visualBubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        sw_t5_03_visualBubble.transform.SetParent(player.transform);
                        sw_t5_03_visualBubble.transform.localPosition = Vector3.up * 1f;
                        sw_t5_03_visualBubble.transform.localScale = Vector3.one * 2.5f;
                        sw_t5_03_visualBubble.GetComponent<Collider>().enabled = false;
                        sw_t5_03_visualBubble.GetComponent<Renderer>().material.color = new Color(0.8f, 0.8f, 1f, 0.3f);
                        // Cần Transparent shader thực tế, tạm dùng Color alpha
                    }
                    Debug.Log($"<color=cyan>[T5_03] Kích hoạt Giáp Overheal: {sw_t5_03_currentShield}!</color>");
                }
            }
        }
    }
    private IEnumerator Reset_SW_T5_03_Shield()
    {
        yield return new WaitForSeconds(10f);
        stats.currentShield = Mathf.Max(0, stats.currentShield - sw_t5_03_currentShield);
        sw_t5_03_currentShield = 0f;
        stats.superArmorLevel -= 99;
        if (sw_t5_03_visualBubble != null) Destroy(sw_t5_03_visualBubble);
        Debug.Log("<color=cyan>[T5_03] Mất Giáp Overheal!</color>");
    }

    private Dictionary<Stats, float> sw_t5_04_seeds = new Dictionary<Stats, float>();
    private void Effect_WPN_SW_T5_04_Mark(Stats target, int step, bool isH, bool isC)
    {
        if (isC)
        {
            sw_t5_04_seeds[target] = Time.time + 5f;
            VisualDebugHelper.DrawSphere(target.transform.position + Vector3.up * 2, 0.5f, Color.red, 5f); // Mark trên đầu
        }
    }
    private void Effect_WPN_SW_T5_04_Dash()
    {
        List<Stats> keys = new List<Stats>(sw_t5_04_seeds.Keys);
        foreach (var k in keys) if (Time.time > sw_t5_04_seeds[k] || k == null || k.currentHp <= 0) sw_t5_04_seeds.Remove(k);

        Stats closest = null;
        float minDist = float.MaxValue;
        foreach (var target in sw_t5_04_seeds.Keys)
        {
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist < minDist && dist < 15f) { minDist = dist; closest = target; }
        }

        if (closest != null)
        {
            Collider targetCol = closest.GetComponent<Collider>();
            float tRadius = targetCol != null ? Mathf.Max(targetCol.bounds.extents.x, targetCol.bounds.extents.z) : 1.0f;

            // Lướt XUYÊN QUA địch theo hướng player tiến tới, dừng ngay SÁT phía sau nó (không ra xa).
            Vector3 approach = closest.transform.position - player.transform.position; approach.y = 0;
            if (approach.sqrMagnitude < 0.001f) approach = stats.facingDirection;
            approach.Normalize();
            Vector3 targetPos = closest.transform.position + approach * (tRadius + 0.3f);

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            player.transform.position = targetPos;
            player.isDashing = false;
            player.ForceFaceDirection(approach);

            DamageHelper.ApplyStandardDamage(stats, closest, transform, 3.0f, null, null, 0, false, 0f, true);
            sw_t5_04_seeds.Remove(closest);

            VisualDebugHelper.DrawSphere(closest.transform.position, 1f, new Color(1, 0, 0, 0.5f), 0.5f); // Nổ máu
        }
    }
    #endregion

    // =========================================================================================
    #region [ DAO GĂM - DAGGER TIER 3 & 4 ]

    private void Effect_WPN_DG_T3_01(Stats target, int step, bool isH, bool isC)
    {
        float t = CombatMath.CalculateDirectionFactor(transform, target);
        if (t == 1.0f)
        {
            DamageHelper.ApplyQuickProcDamage(stats, target, 0.1f, 0f, transform);
        }
    }

    // [ĐÃ SỬA]: Refresh Timer thay vì bỏ qua nếu dính Crit lúc đang bị debuff
    private Dictionary<Stats, float> dg_t3_02_timers = new Dictionary<Stats, float>();
    private void Effect_WPN_DG_T3_02(Stats target, int step, bool isH, bool isC)
    {
        if (isC)
        {
            if (!dg_t3_02_timers.ContainsKey(target))
            {
                float amount = target.armor * 0.20f;
                target.armor -= amount;
                dg_t3_02_timers[target] = Time.time + 4f;
                StartCoroutine(DG_T3_02_RestoreRoutine(target, amount));
            }
            else
            {
                // Refresh lại thời gian
                dg_t3_02_timers[target] = Time.time + 4f;
            }
        }
    }
    private IEnumerator DG_T3_02_RestoreRoutine(Stats target, float amount)
    {
        // Chờ chừng nào thời gian hiện tại còn nhỏ hơn hạn chót của mục tiêu đó
        while (target != null && Time.time < dg_t3_02_timers[target])
        {
            yield return null;
        }
        if (target != null)
        {
            target.armor += amount;
            dg_t3_02_timers.Remove(target);
        }
    }

    private void Effect_WPN_DG_T3_03(Stats target, int step, bool isH, bool isC)
    {
        stats.lastDashTime = -100f;
        stats.currentStamina = Mathf.Min(stats.maxStamina, stats.currentStamina + 10f);
    }

    private Dictionary<Stats, int> dg_t4_01_marks = new Dictionary<Stats, int>();
    private void Effect_WPN_DG_T4_01(Stats target, int step, bool isH, bool isC)
    {
        if (!isC) return;
        if (!dg_t4_01_marks.ContainsKey(target)) dg_t4_01_marks[target] = 0;

        dg_t4_01_marks[target]++;
        VisualDebugHelper.DrawSphere(target.transform.position + Vector3.up * (1f + 0.5f * dg_t4_01_marks[target]), 0.3f, Color.red, 1f);

        if (dg_t4_01_marks[target] >= 3)
        {
            float missingHp = target.maxHp - target.currentHp;
            target.TakeDamage(new DamageInfo { trueDamage = missingHp * 0.05f, attacker = stats });
            dg_t4_01_marks[target] = 0;
            VisualDebugHelper.DrawSphere(target.transform.position, 2f, new Color(0.5f, 0, 0, 0.5f), 0.5f);
        }
    }

    private void Effect_WPN_DG_T4_02(Stats target, bool isBackstab)
    {
        stats.isInvisible = true;
        stats.lastDashTime = -100f;
        stats.currentStamina = stats.maxStamina;
        StartCoroutine(RemoveInvisRoutine(2f));
    }
    private IEnumerator RemoveInvisRoutine(float delay) { yield return new WaitForSeconds(delay); stats.isInvisible = false; }

    private Dictionary<Stats, int> dg_t4_03_stacks = new Dictionary<Stats, int>();
    private void Effect_WPN_DG_T4_03(Stats target, int step, bool isH, bool isC)
    {
        if (!dg_t4_03_stacks.ContainsKey(target)) dg_t4_03_stacks[target] = 0;
        if (dg_t4_03_stacks[target] < 5)
        {
            dg_t4_03_stacks[target]++;
            StartCoroutine(ReduceArmorRoutine(target, 0.05f, 3f, () => { dg_t4_03_stacks[target]--; }));
        }
    }

    private bool dg_t4_04_teleportReady = false;
    private void Effect_WPN_DG_T4_04_Trigger() { dg_t4_04_teleportReady = true; }
    private void Effect_WPN_DG_T4_04_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (dg_t4_04_teleportReady)
        {
            dg_t4_04_teleportReady = false;

            Collider targetCol = target.GetComponent<Collider>();
            float tRadius = targetCol != null ? Mathf.Max(targetCol.bounds.extents.x, targetCol.bounds.extents.z) : 1.0f;

            Vector3 behindPos = target.transform.position - target.facingDirection * (tRadius + 0.5f);

            // Xóa lực quán tính trước khi tele
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;

            player.transform.position = behindPos;
            player.ForceFaceDirection(target.facingDirection);

            stats.bonusCritChance += 100f;
            StartCoroutine(RemoveCritHack());
        }
    }
    private IEnumerator RemoveCritHack() { yield return new WaitForEndOfFrame(); yield return new WaitForEndOfFrame(); stats.bonusCritChance -= 100f; }
    #endregion

    // =========================================================================================
    #region [ DAO GĂM - DAGGER TIER 5 ]

    private void Effect_WPN_DG_T5_01(Stats target, int step, bool isH, bool isC)
    {
        float t = CombatMath.CalculateDirectionFactor(transform, target);
        if (t == 1.0f)
        {
            EnemyStats es = target as EnemyStats;
            bool isBoss = es != null && es.monsterRank >= 2; // rank 2 = Boss
            if (isBoss)
            {
                float missing = target.maxHp - target.currentHp;
                target.TakeDamage(new DamageInfo { trueDamage = missing * 0.05f, attacker = stats });
            }
            else
            {
                target.TakeDamage(new DamageInfo { trueDamage = 999999f, impactLevel = 2 });
            }
        }
    }

    private bool dg_t5_02_cloneActive = false;
    private Vector3 dg_t5_02_clonePos;
    private float dg_t5_02_cloneTimer = 0f;
    private GameObject currentCloneVFX;

    private void Effect_WPN_DG_T5_02()
    {
        if (!dg_t5_02_cloneActive)
        {
            dg_t5_02_clonePos = transform.position;
            dg_t5_02_cloneActive = true;
            dg_t5_02_cloneTimer = Time.time + 3f;

            currentCloneVFX = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            currentCloneVFX.transform.position = dg_t5_02_clonePos;
            currentCloneVFX.GetComponent<Collider>().enabled = false;
            currentCloneVFX.GetComponent<Renderer>().material.color = new Color(0, 0, 0, 0.5f);
        }
        else if (Time.time <= dg_t5_02_cloneTimer)
        {
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            player.isDashing = false;

            transform.position = dg_t5_02_clonePos;
            dg_t5_02_cloneActive = false;
            if (currentCloneVFX) Destroy(currentCloneVFX);

            stats.Heal(stats.maxHp * 0.2f);
            StartCoroutine(BuffMoveSpeedRoutine(0.10f, 3f));
        }
    }
    void Update()
    {
        if (dg_t5_02_cloneActive && Time.time > dg_t5_02_cloneTimer)
        {
            dg_t5_02_cloneActive = false;
            if (currentCloneVFX) Destroy(currentCloneVFX);
        }

        // SP_T3_01: Buff Kỷ Luật khi Máu > 70% (+15% bonusPhysAtk, +15% bonusAttackSpeed).
        bool wantSP301 = eqManager != null && eqManager.currentWeapon != null
                         && eqManager.currentWeapon.id.Trim() == "WPN_SP_T3_01"
                         && stats.maxHp > 0 && stats.currentHp / stats.maxHp > 0.7f;
        if (wantSP301 && !sp_t3_01_active)
        {
            stats.bonusPhysicalAtk += 0.15f; stats.bonusAttackSpeed += 0.15f;
            sp_t3_01_active = true; stats.RecalculateStats();
        }
        else if (!wantSP301 && sp_t3_01_active)
        {
            stats.bonusPhysicalAtk -= 0.15f; stats.bonusAttackSpeed -= 0.15f;
            sp_t3_01_active = false; stats.RecalculateStats();
        }

        string wid = (eqManager != null && eqManager.currentWeapon != null) ? eqManager.currentWeapon.id.Trim() : "";

        // GS_T4_02: chuyển 30% Armor hiện tại thành flatSTR (realtime, có ngưỡng tránh giật).
        float gs402Target = (wid == "WPN_GS_T4_02") ? stats.armor * 0.3f : 0f;
        if (Mathf.Abs(gs402Target - gs_t4_02_added) > 0.5f)
        {
            stats.flatSTR += (gs402Target - gs_t4_02_added);
            gs_t4_02_added = gs402Target;
            stats.RecalculateStats();
        }

        // GS_T4_04: mỗi 1% máu mất → +0.5% bonusSTR & +0.5% PhysicalLifeSteal (tối đa 25% tại 50% máu mất).
        float missPct = stats.maxHp > 0 ? 1f - stats.currentHp / stats.maxHp : 0f;
        float gs404Target = (wid == "WPN_GS_T4_04") ? Mathf.Min(0.25f, missPct * 0.5f) : 0f;
        if (Mathf.Abs(gs404Target - gs_t4_04_added) > 0.005f)
        {
            float d = gs404Target - gs_t4_04_added;
            stats.bonusSTR += d;
            stats.physicalLifeSteal += d;
            gs_t4_04_added = gs404Target;
            stats.RecalculateStats();
        }

        // GS_T5_01: miễn nhiễm khống chế + khóa tốc đánh ở 0.6 (trừ giáp xử lý ở GS_T5_01_Hit).
        bool gs501 = wid == "WPN_GS_T5_01";
        if (gs501 && !gs_t5_01_active) { stats.isSuperArmor = true; stats.superArmorLevel += 50; gs_t5_01_active = true; }
        else if (!gs501 && gs_t5_01_active) { stats.superArmorLevel -= 50; gs_t5_01_active = false; }
        if (gs501) stats.attackSpeed = 0.6f; // khóa tốc đánh (best-effort, ghi đè mỗi frame)

        // BW_T3_01: gồng bắn (HeavyAttack charge) không bị giảm tốc di chuyển.
        stats.bowNoChargeSlow = (wid == "WPN_BW_T3_01");
        // BW_T5_02 & BW_T4_02: đạn homing thật (bẻ cong đuổi địch). BW_T5_01: Tia Sáng Mặt Trời (HP<50% kiểm ở handler).
        stats.bowHoming = (wid == "WPN_BW_T5_02" || wid == "WPN_BW_T4_02");
        stats.bowSunBeam = (wid == "WPN_BW_T5_01");

        // ST_T4_05: chỉ cộng stat khi đã hoàn thành quest ẩn (cờ stT4_05QuestComplete).
        bool wantT405 = (wid == "WPN_ST_T4_05" && stats.stT4_05QuestComplete);
        if (wantT405 && !st_t4_05_applied)
        {
            stats.flatINT += 100f; stats.bonusCritChance += 0.2f; stats.bonusAttackSpeed += 0.1f;
            st_t4_05_applied = true; stats.RecalculateStats();
        }
        else if (!wantT405 && st_t4_05_applied)
        {
            stats.flatINT -= 100f; stats.bonusCritChance -= 0.2f; stats.bonusAttackSpeed -= 0.1f;
            st_t4_05_applied = false; stats.RecalculateStats();
        }

        // Phí Sin Signature: ST_T3_01 giảm 15% (0.85), GR_T4_02 tăng 15% (1.15), còn lại 1.
        stats.signatureSinCostMult = (wid == "WPN_ST_T3_01") ? 0.85f : (wid == "WPN_GR_T4_02") ? 1.15f : 1f;

        // GR_T3_02: mỗi điểm Sin → +0.5% bonusMagicAtk (tối đa +25% tại 50 Sin).
        float gr302Target = (wid == "WPN_GR_T3_02") ? Mathf.Min(0.25f, stats.currentSin * 0.005f) : 0f;
        if (Mathf.Abs(gr302Target - gr_t3_02_added) > 0.005f)
        {
            stats.bonusMagicAtk += (gr302Target - gr_t3_02_added);
            gr_t3_02_added = gr302Target;
            stats.RecalculateStats();
        }

        // GR_T5_02: hết 5s không đánh → mất toàn bộ stack Chí mạng cộng dồn.
        if (gr_t5_02_stacks > 0 && Time.time > gr_t5_02_until)
        {
            stats.bonusCritChance -= 0.04f * gr_t5_02_stacks;
            gr_t5_02_stacks = 0;
            stats.RecalculateStats();
        }

        // ST_T3_03: đứng yên 2s → +20% bonusMagicAtk & +10% bonusCritChance, mất khi di chuyển.
        if (wid == "WPN_ST_T3_03")
        {
            bool standing = !player.isWalking && !player.isDashing;
            if (standing)
            {
                st_t3_03_timer += Time.deltaTime;
                if (st_t3_03_timer >= 2f && !st_t3_03_active)
                {
                    stats.bonusMagicAtk += 0.2f; stats.bonusCritChance += 0.1f;
                    st_t3_03_active = true; stats.RecalculateStats();
                }
            }
            else { st_t3_03_timer = 0f; ST_T3_03_Clear(); }
        }
        else { st_t3_03_timer = 0f; ST_T3_03_Clear(); }

        // ST_T4_04: bị bao vây bởi >3 địch trong 2f → đẩy lùi + 50% magicAtk + khiên (CD 5s).
        if (wid == "WPN_ST_T4_04" && Time.time >= st_t4_04_lastProc + 5f)
        {
            Collider[] near = Physics.OverlapSphere(transform.position, 2f, player.dangerLayer);
            HashSet<Stats> seen = new HashSet<Stats>();
            foreach (var h in near)
            {
                Stats e = h.GetComponentInParent<Stats>();
                if (e != null && e.currentHp > 0) seen.Add(e);
            }
            if (seen.Count > 3)
            {
                st_t4_04_lastProc = Time.time;
                foreach (var e in seen)
                {
                    DamageHelper.ApplyQuickProcDamage(stats, e, 0f, 0.5f, transform);
                    var kbInfo = new DamageInfo { attacker = stats, sourcePosition = transform.position, sourceType = DamageSourceType.Other };
                    kbInfo.AddEffect(new CombatEffectInfo(CombatEffectType.Knockback, 0f) { force = 4f, sourcePosition = transform.position, respectEffectResistance = false });
                    e.TakeDamage(kbInfo);
                }
                stats.AddShield(stats.magicAtk * 0.5f * 0.3f * seen.Count, 3f); // ~30% sát thương gây ra
                VisualDebugHelper.DrawSphere(transform.position, 2f, new Color(0.4f, 0.7f, 1f, 0.4f), 0.5f);
            }
        }
    }

    private void ST_T3_03_Clear()
    {
        if (st_t3_03_active)
        {
            stats.bonusMagicAtk -= 0.2f; stats.bonusCritChance -= 0.1f;
            st_t3_03_active = false; stats.RecalculateStats();
        }
    }
    private float st_t3_03_timer = 0f;
    private bool st_t3_03_active = false;
    private float st_t4_04_lastProc = -100f;

    // Bookkeeping cho passive Greatsword trong Update.
    private float gs_t4_02_added = 0f;
    private float gs_t4_04_added = 0f;
    private bool gs_t5_01_active = false;

    private int dg_t5_03_stacks = 0;
    private void Effect_WPN_DG_T5_03(Stats target, bool isBackstab)
    {
        if (dg_t5_03_stacks >= 10) return; // tối đa 10 cộng dồn
        dg_t5_03_stacks++;

        // Đánh cắp 5% STR và INT của mục tiêu (Stats nào cũng có STR/INT).
        float stolenStr = (target != null ? target.STR : 0f) * 0.05f;
        float stolenInt = (target != null ? target.INT : 0f) * 0.05f;
        if (stolenStr <= 0f) stolenStr = 5f; // fallback nếu mục tiêu không có chỉ số
        if (stolenInt <= 0f) stolenInt = 5f;

        stats.flatSTR += stolenStr;
        stats.flatINT += stolenInt;
        stats.RecalculateStats();
        StartCoroutine(RevertDGStats(stolenStr, stolenInt, 10f));
    }
    private IEnumerator RevertDGStats(float sStr, float sInt, float delay)
    {
        yield return new WaitForSeconds(delay);
        stats.flatSTR -= sStr;
        stats.flatINT -= sInt;
        dg_t5_03_stacks = Mathf.Max(0, dg_t5_03_stacks - 1);
        stats.RecalculateStats();
    }

    #endregion

    // =========================================================================================
    #region [ GIÁO - SPEAR ]

    // SP_T3_01: passive trong Update (HP>70% → +15% bonusPhysAtk + +15% bonusAtkSpeed).
    private bool sp_t3_01_active = false;

    // SP_T3_02: +25% sát thương đòn đánh thường lên mục tiêu xa > 2.5f.
    private float SP_T3_02_Mult(Stats target)
    {
        if (target == null) return 1f;
        return Vector3.Distance(transform.position, target.transform.position) > 2.5f ? 1.25f : 1f;
    }

    // SP_T3_03: sau Dash, đòn kế tiếp trong 2s gây thêm sát thương phép = 30% physicalAtk.
    private float sp_t3_03_until = -1f;
    private void SP_T3_03_Dash() { sp_t3_03_until = Time.time + 2f; }
    private void SP_T3_03_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (Time.time > sp_t3_03_until) return;
        sp_t3_03_until = -1f;
        target.TakeDamage(new DamageInfo { magicDamage = stats.physicalAtk * 0.3f, attacker = stats, sourcePosition = transform.position, sourceType = DamageSourceType.Melee });
        VisualDebugHelper.DrawSphere(target.transform.position, 0.6f, new Color(1, 1, 0.2f, 0.5f), 0.3f);
    }

    // SP_T4_01: Perfect Dodge → sóng xung kích 150% phys AoE 2.5f + khiên 10% maxHp 3s.
    private void SP_T4_01_Dodge()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 2.5f, player.dangerLayer);
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits) { Stats e = h.GetComponentInParent<Stats>(); if (e != null && e.currentHp > 0 && seen.Add(e)) DamageHelper.ApplyQuickProcDamage(stats, e, 1.5f, 0f, transform); }
        stats.AddShield(stats.maxHp * 0.1f, 3f);
        VisualDebugHelper.DrawSphere(transform.position, 2.5f, new Color(1, 1, 0, 0.4f), 0.4f);
    }

    // SP_T4_02: Dash rồi đánh ngay (trong 0.8s) → sét lan 4 mục tiêu, 100% physAtk TrueDamage + choáng 1s.
    private float sp_t4_02_until = -1f;
    private void SP_T4_02_Dash() { sp_t4_02_until = Time.time + 0.8f; }
    private void SP_T4_02_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (Time.time > sp_t4_02_until) return;
        sp_t4_02_until = -1f;
        List<Stats> chain = new List<Stats> { target };
        Vector3 from = target.transform.position;
        for (int i = 0; i < 3; i++)
        {
            Stats next = NearestEnemyExcept(from, 4f, chain);
            if (next == null) break;
            chain.Add(next); from = next.transform.position;
        }
        foreach (var e in chain)
        {
            if (e == null || e.currentHp <= 0) continue;
            var chainInfo = new DamageInfo { trueDamage = stats.physicalAtk, attacker = stats, sourcePosition = transform.position, impactLevel = 1, sourceType = DamageSourceType.Melee };
            chainInfo.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, 1f) { impactLevel = 1, sourcePosition = transform.position });
            e.TakeDamage(chainInfo);
            VisualDebugHelper.DrawSphere(e.transform.position + Vector3.up, 0.4f, new Color(1, 1, 0.3f, 0.6f), 0.3f);
        }
    }

    // SP_T4_03: HP<50% + dùng kỹ năng → hy sinh 5% máu hiện tại, đòn kế tiếp 3s xuyên 100% giáp/MR + chắc crit.
    private Coroutine sp_t4_03_coro;
    private void SP_T4_03_Skill()
    {
        if (stats.maxHp <= 0 || stats.currentHp / stats.maxHp >= 0.5f) return;
        float cost = stats.currentHp * 0.05f;
        if (stats.currentHp - cost > 1f) stats.currentHp -= cost; // Quy tắc Sinh-Tử: không xuống dưới 1
        nextHitArmorPenCrit = true;
        if (sp_t4_03_coro != null) StopCoroutine(sp_t4_03_coro);
        sp_t4_03_coro = StartCoroutine(SP_T4_03_Clear());
    }
    private IEnumerator SP_T4_03_Clear() { yield return new WaitForSeconds(3f); nextHitArmorPenCrit = false; sp_t4_03_coro = null; }

    // SP_T4_04: Heavy Attack → luồng gió 3s, 30% physAtk/s AoE 2f + gom quái. CD 5s.
    private float sp_t4_04_lastProc = -100f;
    private void SP_T4_04_Heavy()
    {
        if (Time.time - sp_t4_04_lastProc < 5f) return;
        sp_t4_04_lastProc = Time.time;
        StartCoroutine(SP_T4_04_Wind(transform.position + stats.facingDirection * 2f));
    }
    private IEnumerator SP_T4_04_Wind(Vector3 center)
    {
        float t = 0f;
        while (t < 3f)
        {
            Collider[] hits = Physics.OverlapSphere(center, 2f, player.dangerLayer);
            HashSet<Stats> seen = new HashSet<Stats>();
            foreach (var h in hits)
            {
                Stats e = h.GetComponentInParent<Stats>();
                if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
                DamageHelper.ApplyQuickProcDamage(stats, e, 0.15f, 0f, transform); // 0.15 mỗi 0.5s = 30%/s
                PullEnemyTo(e, center, 1.5f);
            }
            VisualDebugHelper.DrawSphere(center, 2f, new Color(0.5f, 1f, 1f, 0.2f), 0.5f);
            yield return new WaitForSeconds(0.5f);
            t += 0.5f;
        }
    }

    // SP_T4_05: gây crit → để lại Ảo ảnh, sau 1s đâm lại 50% physAtk (đòn này không kích hoạt tiếp). Tối đa 3 ảo ảnh.
    private int sp_t4_05_count = 0;
    private void SP_T4_05_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (!isC || sp_t4_05_count >= 3) return;
        sp_t4_05_count++;
        StartCoroutine(SP_T4_05_Mirage(target));
    }
    private IEnumerator SP_T4_05_Mirage(Stats target)
    {
        VisualDebugHelper.DrawSphere(target != null ? target.transform.position + Vector3.up : transform.position, 0.4f, new Color(1, 1, 1, 0.4f), 1f);
        yield return new WaitForSeconds(1f);
        // Đòn ảo ảnh đi qua DamageHelper (không gọi OnHitEnemy) → không tự kích hoạt SP_T4_05 lần nữa.
        if (target != null && target.currentHp > 0) DamageHelper.ApplyQuickProcDamage(stats, target, 0.5f, 0f, transform);
        sp_t4_05_count = Mathf.Max(0, sp_t4_05_count - 1);
    }

    // SP_T5_02: dùng kỹ năng → Vòi Rồng Nước trước mặt 3f, hút + làm Chìm Đuối (-50% moveSpeed & attackSpeed) 3s.
    private void SP_T5_02_Skill() { StartCoroutine(SP_T5_02_Tornado(transform.position + stats.facingDirection * 3f)); }
    private IEnumerator SP_T5_02_Tornado(Vector3 center)
    {
        float t = 0f;
        while (t < 3f)
        {
            Collider[] hits = Physics.OverlapSphere(center, 3f, player.dangerLayer);
            foreach (var h in hits)
            {
                Stats e = h.GetComponentInParent<Stats>();
                if (e == null || e.currentHp <= 0) continue;
                PullEnemyTo(e, center, 2f);
                // [SLOW] Chìm Đuối -50% move+attack: refresh effect ngắn mỗi tick (strongest-wins, không đụng base stat).
                e.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, 0.7f) { magnitude = 0.5f }, stats);
            }
            VisualDebugHelper.DrawSphere(center, 3f, new Color(0f, 0.5f, 1f, 0.2f), 0.5f);
            yield return new WaitForSeconds(0.5f);
            t += 0.5f;
        }
    }

    // SP_T5_03: sau khi chịu 1 đòn → hấp thụ (vô hiệu) đòn đó vào lò 3s; đòn tấn công kế tiếp giải phóng
    // lượng đã hấp thụ + 20% maxHp thành sát thương vật lý hình nón trước mặt 2f.
    private float sp_t5_03_stored = 0f;
    private float sp_t5_03_until = -1f;
    private void SP_T5_03_Absorb(DamageInfo info)
    {
        if (Time.time <= sp_t5_03_until) return; // đang giữ lò → không hấp thụ chồng
        sp_t5_03_stored = info.TotalRawDamage;
        info.physDamage = 0; info.magicDamage = 0; info.trueDamage = 0; // hấp thụ trọn đòn
        info.ClearCombatEffects(); // đòn bị hấp thụ → không còn BẤT KỲ CC nào (effects list + legacy)
        sp_t5_03_until = Time.time + 3f;
        VisualDebugHelper.DrawSphere(transform.position + Vector3.up, 1f, new Color(1, 0.5f, 0, 0.3f), 0.5f);
    }
    private void SP_T5_03_Release(Stats target, int step, bool isH, bool isC)
    {
        if (Time.time > sp_t5_03_until || sp_t5_03_stored <= 0f) return;
        float dmg = sp_t5_03_stored + stats.maxHp * 0.2f;
        sp_t5_03_stored = 0f; sp_t5_03_until = -1f;

        Vector3 fwd = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;
        Vector3 center = transform.position + fwd * 1f;
        Collider[] hits = Physics.OverlapSphere(center, 2f, player.dangerLayer);
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            Vector3 to = e.transform.position - transform.position; to.y = 0;
            if (Vector3.Angle(fwd, to) > 60f) continue; // hình nón ~120°
            e.TakeDamage(new DamageInfo { physDamage = dmg, attacker = stats, sourcePosition = transform.position, sourceType = DamageSourceType.Melee });
        }
        VisualDebugHelper.DrawBox(center, new Vector3(4f, 1f, 4f), Quaternion.LookRotation(fwd), new Color(1, 0.5f, 0, 0.4f), 0.4f);
    }

    // SP_T5_04: trong lúc Dash, xoay thương quanh người gây 200% physicalAtk bán kính 2f. Mỗi địch chỉ trúng 1 lần/lượt Dash.
    private void SP_T5_04_Dash() => StartCoroutine(SP_T5_04_Spin());
    private IEnumerator SP_T5_04_Spin()
    {
        HashSet<Stats> hit = new HashSet<Stats>();
        while (player.isDashing)
        {
            foreach (var h in Physics.OverlapSphere(transform.position, 2f, player.dangerLayer))
            {
                Stats e = h.GetComponentInParent<Stats>();
                if (e == null || e.currentHp <= 0 || !hit.Add(e)) continue;
                DamageHelper.ApplyQuickProcDamage(stats, e, 2.0f, 0f, transform); // 200% physicalAtk
            }
            VisualDebugHelper.DrawSphere(transform.position, 2f, new Color(1f, 0.2f, 0.2f, 0.35f), 0.1f);
            yield return null;
        }
    }
    #endregion

    // =========================================================================================
    #region [ ĐẠI KIẾM - GREATSWORD ]

    // GS_T3_01: tấn công vào địch CÓ SHIELD → +50% dmg + 10% phá vỡ shield.
    private float GS_T3_01_Mult(Stats target) => (target != null && target.currentShield > 0) ? 1.5f : 1f;
    private void GS_T3_01_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (target != null && target.currentShield > 0 && UnityEngine.Random.value <= 0.1f)
        {
            target.currentShield = 0; // Shield Break
            VisualDebugHelper.DrawSphere(target.transform.position + Vector3.up, 0.6f, new Color(1, 1, 0.2f, 0.6f), 0.3f);
        }
    }

    // GS_T3_02: giảm 15% sát thương nhận vào trong lúc đang vung kiếm.
    private void GS_T3_02_Reduce(DamageInfo info)
    {
        if (player != null && player.isAttacking)
        {
            info.physDamage *= 0.85f; info.magicDamage *= 0.85f; info.trueDamage *= 0.85f;
        }
    }

    // GS_T3_03: mỗi đòn thứ 3 → sóng xung kích hình nón trước mặt 40% physAtk + đẩy lùi nhẹ 2f.
    private void GS_T3_03_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (attackHitCounter % 3 != 0) return;
        Vector3 fwd = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;
        Vector3 center = transform.position + fwd * 1.5f;
        Collider[] hits = Physics.OverlapSphere(center, 2.5f, player.dangerLayer);
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            Vector3 to = e.transform.position - transform.position; to.y = 0;
            if (Vector3.Angle(fwd, to) > 60f) continue; // hình nón
            var coneInfo = new DamageInfo { physDamage = stats.physicalAtk * 0.4f, attacker = stats, sourcePosition = transform.position, sourceType = DamageSourceType.Melee };
            coneInfo.AddEffect(new CombatEffectInfo(CombatEffectType.Knockback, 0f) { force = 2f, sourcePosition = transform.position, respectEffectResistance = false });
            e.TakeDamage(coneInfo);
        }
        VisualDebugHelper.DrawBox(center, new Vector3(4f, 1f, 4f), Quaternion.LookRotation(fwd), new Color(1, 0.6f, 0.1f, 0.4f), 0.4f);
    }

    // GS_T4_01: Heavy → cột đá gai AoE 2f, 200% physAtk + choáng 1s.
    private void GS_T4_01_Heavy()
    {
        Vector3 center = transform.position + stats.facingDirection * 2f;
        Collider[] hits = Physics.OverlapSphere(center, 2f, player.dangerLayer);
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            DamageHelper.ApplyStandardDamage(stats, e, transform, 2.0f, null, null, 2, true, 1f);
        }
        VisualDebugHelper.DrawSphere(center, 2f, new Color(0.6f, 0.4f, 0.2f, 0.5f), 0.5f);
    }

    // GS_T4_03: hạ địch (đòn đánh thường) → hồi 10% maxHp + đòn đánh thường kế (3s) +50% sát thương (+50% tầm: TODO).
    private float gs_t4_03_buffUntil = -1f;
    private void GS_T4_03_Kill(Stats target, bool isBackstab)
    {
        // OnKillEnemy fire khi hạ địch bằng đòn đánh thường (đòn skill đi đường khác).
        stats.Heal(stats.maxHp * 0.1f, true, false, HealSource.Skill);
        gs_t4_03_buffUntil = Time.time + 3f;
    }
    private float GS_T4_03_Mult(Stats target) => Time.time <= gs_t4_03_buffUntil ? 1.5f : 1f;
    private void GS_T4_03_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (Time.time <= gs_t4_03_buffUntil) gs_t4_03_buffUntil = -1f; // tiêu hao sau 1 đòn
    }

    // GS_T5_01: mỗi đòn đánh → trừ 100% Armor của địch trong 5s. (CC-immune & khóa tốc đánh xử lý trong Update)
    private Dictionary<Stats, Coroutine> gs_t5_01_shred = new Dictionary<Stats, Coroutine>();
    private void GS_T5_01_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (target == null) return;
        if (gs_t5_01_shred.TryGetValue(target, out var c) && c != null) return; // đang bị trừ rồi → để routine cũ tự khôi phục/refresh
        gs_t5_01_shred[target] = StartCoroutine(GS_T5_01_ArmorBreak(target));
    }
    private IEnumerator GS_T5_01_ArmorBreak(Stats target)
    {
        float removed = target.armor;
        target.armor -= removed; // -100% armor
        yield return new WaitForSeconds(5f);
        if (target != null) target.armor += removed;
        gs_t5_01_shred.Remove(target);
    }

    // GS_T5_02: Khi máu xuống dưới 1 → khóa HP=1, bất tử 6s, +200% dmg & +200% atkSpd. CD 90s.
    private float gs_t5_02_lastUse = -1000f;
    private bool gs_t5_02_active = false;
    private void GS_T5_02_Lethal(DamageInfo info)
    {
        if (gs_t5_02_active) { info.physDamage = 0; info.magicDamage = 0; info.trueDamage = 0; return; } // đang bất tử
        if (Time.time < gs_t5_02_lastUse + 90f) return;
        if (stats.currentHp - info.TotalRawDamage > 0) return; // chưa chí mạng

        info.physDamage = 0; info.magicDamage = 0; info.trueDamage = 0;
        stats.currentHp = 1;
        gs_t5_02_lastUse = Time.time;
        StartCoroutine(GS_T5_02_Routine());
    }
    private IEnumerator GS_T5_02_Routine()
    {
        gs_t5_02_active = true;
        stats.isInvincible = true;
        stats.damageOutputMultiplier += 2f; // +200% dmg
        stats.bonusAttackSpeed += 2f;        // +200% atkSpd
        stats.RecalculateStats();
        Debug.Log("<color=red>[GS_T5_02]</color> GIAO THỨC BẤT TỬ! (6s)");
        yield return new WaitForSeconds(6f);
        stats.isInvincible = false;
        stats.damageOutputMultiplier -= 2f;
        stats.bonusAttackSpeed -= 2f;
        stats.RecalculateStats();
        gs_t5_02_active = false;
    }

    // GS_T5_03: Heavy → Hố Đen hút địch vào tâm + 100% magicAtk/giây trong 4s. Cùng lúc chỉ 1 hố đen (Heavy mới xóa cái cũ).
    private const float GS_T5_03_RADIUS = 1.5f;
    private Coroutine gs_t5_03_active;
    private void GS_T5_03_Heavy()
    {
        if (gs_t5_03_active != null) StopCoroutine(gs_t5_03_active);
        Vector3 fwd = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;
        Vector3 center = transform.position + fwd * 3f;
        gs_t5_03_active = StartCoroutine(GS_T5_03_BlackHole(center));
    }
    private IEnumerator GS_T5_03_BlackHole(Vector3 center)
    {
        float elapsed = 0f, dmgTimer = 0f;
        while (elapsed < 4f)
        {
            // Hút địch vào tâm mỗi frame.
            foreach (var h in Physics.OverlapSphere(center, GS_T5_03_RADIUS, player.dangerLayer))
            {
                Stats e = h.GetComponentInParent<Stats>();
                if (e == null || e.currentHp <= 0) continue;
                PullEnemyTo(e, center, 4f);
            }
            VisualDebugHelper.DrawSphere(center, GS_T5_03_RADIUS, new Color(0.5f, 0f, 0.8f, 0.4f), 0.1f);

            // Gây 100% magicAtk mỗi giây.
            dmgTimer += Time.deltaTime;
            if (dmgTimer >= 1f)
            {
                dmgTimer -= 1f;
                HashSet<Stats> seen = new HashSet<Stats>();
                foreach (var h in Physics.OverlapSphere(center, GS_T5_03_RADIUS, player.dangerLayer))
                {
                    Stats e = h.GetComponentInParent<Stats>();
                    if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
                    DamageHelper.ApplyQuickProcDamage(stats, e, 0f, 1.0f, transform); // 100% magicAtk
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
        gs_t5_03_active = null;
    }
    #endregion

    // =========================================================================================
    #region [ CUNG - BOW ]

    // BW_T3_02: sát thương tăng theo khoảng cách (+5% tại 2f → +30% tại 10f).
    private float BW_T3_02_Mult(Stats target)
    {
        if (target == null) return 1f;
        float d = Vector3.Distance(transform.position, target.transform.position);
        float t = Mathf.Clamp01((d - 2f) / (10f - 2f));
        return 1f + Mathf.Lerp(0.05f, 0.30f, t);
    }

    // BW_T3_03: mỗi mũi tên trúng CÙNG mục tiêu +5%, tối đa 5 lần (+25%). Đổi mục tiêu / >3s không bắn → reset.
    private Stats bw_t3_03_lastTarget;
    private int bw_t3_03_stacks = 0;
    private float bw_t3_03_lastTime = -10f;
    private float BW_T3_03_Mult(Stats target)
    {
        if (target == null) return 1f;
        if (Time.time - bw_t3_03_lastTime > 3f) { bw_t3_03_stacks = 0; bw_t3_03_lastTarget = null; }
        if (target == bw_t3_03_lastTarget) bw_t3_03_stacks = Mathf.Min(5, bw_t3_03_stacks + 1);
        else { bw_t3_03_lastTarget = target; bw_t3_03_stacks = 1; }
        bw_t3_03_lastTime = Time.time;
        return 1f + bw_t3_03_stacks * 0.05f;
    }

    // BW_T4_01: Heavy → mưa sao băng tại cụm địch trước mặt, 80% physAtk AoE + làm chậm 30% 2s.
    private void BW_T4_01_Heavy()
    {
        Vector3 fwd = stats.facingDirection != Vector3.zero ? stats.facingDirection : transform.forward;
        Stats firstHit = NearestEnemyExcept(transform.position + fwd * 3f, 8f, new List<Stats>());
        Vector3 center = firstHit != null ? firstHit.transform.position : transform.position + fwd * 5f;
        Collider[] hits = Physics.OverlapSphere(center, 2.5f, player.dangerLayer);
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            DamageHelper.ApplyQuickProcDamage(stats, e, 0.8f, 0f, transform);
            e.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, 2f) { magnitude = 0.3f }, stats);
        }
        VisualDebugHelper.DrawSphere(center, 2.5f, new Color(1f, 0.5f, 0.3f, 0.4f), 0.5f);
    }

    // BW_T4_03: đánh trúng địch ở khoảng cách < 3f → đẩy lùi 3f.
    private void BW_T4_03_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (target == null) return;
        if (Vector3.Distance(transform.position, target.transform.position) < 3f)
        {
            var kbInfo = new DamageInfo { attacker = stats, sourcePosition = transform.position, sourceType = DamageSourceType.Ranged };
            kbInfo.AddEffect(new CombatEffectInfo(CombatEffectType.Knockback, 0f) { force = 3f, sourcePosition = transform.position, respectEffectResistance = false });
            target.TakeDamage(kbInfo);
        }
    }

    // BW_T5_02: Chim Ánh Trăng trúng địch → làm chậm 20% trong 3s (homing xử lý ở Projectile).
    private void BW_T5_02_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (target == null || target.currentHp <= 0) return;
        target.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, 3f) { magnitude = 0.2f }, stats);
    }

    // BW_T5_01: Tia Sáng Mặt Trời — hitscan tức thì, xuyên mọi vật thể/địa hình/kẻ địch trên đường thẳng.
    // Trúng TẤT CẢ địch dọc tia: gây đúng sát thương đòn đánh hiện tại nhưng đổi sang True Damage,
    // + thiêu đốt 5% physicalAtk True Damage mỗi giây trong 4 giây.
    private const float SUNBEAM_LENGTH = 100f;
    private const float SUNBEAM_RADIUS = 0.3f;
    private const float SUNBEAM_BURN_DURATION = 4f;
    // Quản lý thiêu đốt theo từng mục tiêu: tấn công liên tục chỉ LÀM MỚI thời gian, không cộng dồn nhiều lượt nổ.
    private Dictionary<Stats, float> sunBeamBurnUntil = new Dictionary<Stats, float>();
    private HashSet<Stats> sunBeamBurning = new HashSet<Stats>();
    public void FireSunBeam(Vector3 origin, Vector3 dir, bool isHeavy, int stepIndex)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir = dir.normalized;
        Vector3 end = origin + dir * SUNBEAM_LENGTH;

        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in Physics.OverlapCapsule(origin, end, SUNBEAM_RADIUS, player.dangerLayer))
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            player.ApplyDamageToTarget(e, isHeavy, stepIndex, true); // forceTrueDamage

            // Làm mới mốc kết thúc thiêu đốt; chỉ chạy 1 coroutine/mục tiêu (không stack).
            sunBeamBurnUntil[e] = Time.time + SUNBEAM_BURN_DURATION;
            if (sunBeamBurning.Add(e)) StartCoroutine(SunBeamBurn(e));
        }

        stats.GainSinFromAttack(1); // giữ nhịp tích Sin như bắn thường
        VisualDebugHelper.DrawBox((origin + end) / 2f, new Vector3(SUNBEAM_RADIUS * 2f, 1f, SUNBEAM_LENGTH),
            Quaternion.LookRotation(dir), new Color(1f, 0.9f, 0.2f, 0.5f), 0.3f);
    }
    private IEnumerator SunBeamBurn(Stats target)
    {
        while (target != null && target.currentHp > 0
               && sunBeamBurnUntil.TryGetValue(target, out float until) && Time.time < until)
        {
            target.TakeDamage(new DamageInfo { trueDamage = stats.physicalAtk * 0.05f, attacker = stats, sourceType = DamageSourceType.DoT });
            yield return new WaitForSeconds(1f);
        }
        sunBeamBurning.Remove(target);
        sunBeamBurnUntil.Remove(target);
    }
    #endregion

    // =========================================================================================
    #region [ GẬY PHÉP - STAFF ]

    // ST_T3_02: tiêu hao Sin → hồi HP bằng 150% lượng Sin tiêu hao.
    private void ST_T3_02_OnSin(float amount)
    {
        if (amount > 0) stats.Heal(amount * 1.5f, true, false, HealSource.Skill);
    }

    // ST_T4_02: dùng skill/signature → Thánh Địa bán kính 5f trong 4s, hồi 5% maxHp/s cho bản thân & Companion trong vùng.
    private void ST_T4_02_Skill() => StartCoroutine(ST_T4_02_Routine());
    private IEnumerator ST_T4_02_Routine()
    {
        for (float t = 0; t < 4f; t += 1f)
        {
            stats.Heal(stats.maxHp * 0.05f, false, false, HealSource.Skill);
            Stats comp = GetCompanionStats();
            if (comp != null && !comp.isDead && Vector3.Distance(transform.position, comp.transform.position) <= 5f)
                comp.Heal(comp.maxHp * 0.05f, false, false, HealSource.Skill);
            VisualDebugHelper.DrawSphere(transform.position, 5f, new Color(1f, 0.95f, 0.5f, 0.25f), 1f);
            yield return new WaitForSeconds(1f);
        }
    }

    // ST_T5_01: sát thương đòn đánh thường tăng theo lượng Sin hiện có (tối đa +30% khi đầy Sin).
    private float ST_T5_01_Mult(Stats target)
    {
        if (stats.maxSin <= 0) return 1f;
        return 1f + Mathf.Clamp01(stats.currentSin / stats.maxSin) * 0.3f;
    }

    // ST_T5_02: mọi heal cho bản thân cũng hồi cho Companion; địch trong 0.5f nhận magic = lượng máu hồi.
    private void ST_T5_02_OnHeal(float amount, float excess, HealSource source)
    {
        if (amount <= 0) return;
        Stats comp = GetCompanionStats();
        if (comp != null && !comp.isDead) comp.Heal(amount, false, false, source);

        Collider[] near = Physics.OverlapSphere(transform.position, 0.5f, player.dangerLayer);
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in near)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            e.TakeDamage(new DamageInfo { magicDamage = amount, attacker = stats, sourcePosition = transform.position, sourceType = DamageSourceType.Other });
        }
    }

    // ST_T5_03: đánh trúng địch ở < 2f → Lưỡi Hái Hư Không: thêm sát thương vật lý = 30% magicAtk + đánh cắp 5% Armor & MR (tối đa 3 lần).
    private Dictionary<Stats, int> st_t5_03_stacks = new Dictionary<Stats, int>();
    private void ST_T5_03_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (target == null) return;
        if (Vector3.Distance(transform.position, target.transform.position) >= 2f) return;

        target.TakeDamage(new DamageInfo { physDamage = stats.magicAtk * 0.3f, attacker = stats, sourcePosition = transform.position, sourceType = DamageSourceType.Melee });

        if (!st_t5_03_stacks.TryGetValue(target, out int cur)) cur = 0;
        if (cur >= 3) return;
        st_t5_03_stacks[target] = cur + 1;

        float a = target.armor * 0.05f;
        float m = target.magicResist * 0.05f;
        target.armor -= a; target.magicResist -= m;
        stats.armor += a; stats.magicResist += m; // đánh cắp về cho mình
        stats.RecalculateStats();
        StartCoroutine(ST_T5_03_Restore(target, a, m, 5f));
    }
    private IEnumerator ST_T5_03_Restore(Stats target, float a, float m, float dur)
    {
        yield return new WaitForSeconds(dur);
        if (target != null) { target.armor += a; target.magicResist += m; }
        stats.armor -= a; stats.magicResist -= m;
        stats.RecalculateStats();
        if (target != null && st_t5_03_stacks.ContainsKey(target))
            st_t5_03_stacks[target] = Mathf.Max(0, st_t5_03_stacks[target] - 1);
    }

    // ----- Router sự kiện "kỹ năng trúng đích" -----
    private void HandleOnSkillHit(Stats target, bool isMagic, bool isCrit)
    {
        if (eqManager.currentWeapon == null) return;
        if (onSkillHitEffects.TryGetValue(eqManager.currentWeapon.id.Trim(), out var fn)) fn(target, isMagic, isCrit);
    }

    // ST_T4_01: kỹ năng gây sát thương PHÉP + CHÍ MẠNG → Vụ Nổ Phép tại mục tiêu (AoE 3f, 100% magicAtk).
    // Mỗi địch chỉ tạo 1 vụ nổ / lần dùng kỹ năng; địch đã dính vụ nổ trước đó chỉ nhận 20% (chống DoT nổ liên hồi).
    private HashSet<Stats> st_t4_01_exploded = new HashSet<Stats>();
    private HashSet<Stats> st_t4_01_damaged = new HashSet<Stats>();
    private void ST_T4_01_ResetUse() { st_t4_01_exploded.Clear(); st_t4_01_damaged.Clear(); }
    private void ST_T4_01_SkillHit(Stats target, bool isMagic, bool isCrit)
    {
        if (target == null || target.currentHp <= 0) return;
        if (!isMagic || !isCrit) return;
        if (!st_t4_01_exploded.Add(target)) return; // mỗi địch chỉ kích 1 vụ nổ / lần dùng

        Vector3 center = target.transform.position;
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in Physics.OverlapSphere(center, 3f, player.dangerLayer))
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            float mult = st_t4_01_damaged.Contains(e) ? 0.2f : 1.0f; // vụ nổ thứ 2 trở đi: 20%
            DamageHelper.ApplyQuickProcDamage(stats, e, 0f, mult, transform); // skill=null → không tái kích sự kiện
            st_t4_01_damaged.Add(e);
        }
        VisualDebugHelper.DrawSphere(center, 3f, Color.magenta, 0.4f);
    }

    // ST_T4_03: kỹ năng trúng đích → Dây Xích Hư Không: Stun 3s + rút máu 30% magicAtk/s + chậm 40% trong 3s.
    private Dictionary<Stats, float> st_t4_03_until = new Dictionary<Stats, float>();
    private void ST_T4_03_SkillHit(Stats target, bool isMagic, bool isCrit)
    {
        if (target == null || target.currentHp <= 0) return;
        if (st_t4_03_until.TryGetValue(target, out float u) && Time.time < u) return; // đang dính → không refresh liên tục
        st_t4_03_until[target] = Time.time + 3f;

        var stInfo = new DamageInfo { attacker = stats, sourcePosition = transform.position };
        stInfo.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, 3f) { sourcePosition = transform.position });
        target.TakeDamage(stInfo);
        target.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, 3f) { magnitude = 0.4f }, stats);
        StartCoroutine(ST_T4_03_Drain(target, 3f));
        VisualDebugHelper.DrawSphere(target.transform.position + Vector3.up, 0.6f, Color.yellow, 3f);
    }
    private IEnumerator ST_T4_03_Drain(Stats target, float dur)
    {
        float t = 0f;
        while (t < dur && target != null && target.currentHp > 0)
        {
            target.TakeDamage(new DamageInfo { magicDamage = stats.magicAtk * 0.3f, attacker = stats, sourceType = DamageSourceType.DoT });
            t += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    private Stats _cachedCompanion;
    private Stats GetCompanionStats()
    {
        if (_cachedCompanion != null) return _cachedCompanion;
        CompanionAI c = FindFirstObjectByType<CompanionAI>();
        if (c != null) _cachedCompanion = c.GetComponent<Stats>();
        return _cachedCompanion;
    }
    #endregion

    // =========================================================================================
    #region [ SÁCH PHÉP - GRIMOIRE ]

    // GR_T3_01: dùng skill/signature → 10% reset hồi chiêu của chính kỹ năng vừa dùng.
    private void GR_T3_01_Cast()
    {
        if (UnityEngine.Random.value <= 0.1f && skillManager != null)
            skillManager.ResetMostRecentSkillCooldown();
    }

    // GR_T3_03: Perfect Dodge → kỹ năng dùng trong 3s kế được giảm 50% hồi chiêu (sau khi thi triển).
    private float gr_t3_03_until = -1f;
    private void GR_T3_03_Dodge() => gr_t3_03_until = Time.time + 3f;
    private void GR_T3_03_Cast()
    {
        if (Time.time <= gr_t3_03_until && skillManager != null)
        {
            skillManager.ReduceMostRecentCooldownByFraction(0.5f);
            gr_t3_03_until = -1f; // dùng 1 lần
        }
    }

    // GR_T4_02: dùng Signature → +30% bonusMagicAtk trong 10s (refresh, không cộng dồn).
    private bool gr_t4_02_buffActive = false;
    private float gr_t4_02_until = 0f;
    private void GR_T4_02_Signature()
    {
        gr_t4_02_until = Time.time + 10f;
        if (!gr_t4_02_buffActive) StartCoroutine(GR_T4_02_Buff());
    }
    private IEnumerator GR_T4_02_Buff()
    {
        gr_t4_02_buffActive = true;
        stats.bonusMagicAtk += 0.3f; stats.RecalculateStats();
        while (Time.time < gr_t4_02_until) yield return null;
        stats.bonusMagicAtk -= 0.3f; stats.RecalculateStats();
        gr_t4_02_buffActive = false;
    }

    // GR_T4_03: mỗi lần Dash → để lại Ấn Phép tại vị trí cũ, nổ sau 1s hoặc khi địch chạm, 150% magicAtk AoE 0.5f.
    private void GR_T4_03_Dash() => StartCoroutine(GR_T4_03_Rune(transform.position));
    private IEnumerator GR_T4_03_Rune(Vector3 pos)
    {
        float t = 0f;
        bool detonate = false;
        while (t < 1f && !detonate)
        {
            foreach (var h in Physics.OverlapSphere(pos, 0.5f, player.dangerLayer))
            {
                Stats e = h.GetComponentInParent<Stats>();
                if (e != null && e.currentHp > 0) { detonate = true; break; }
            }
            VisualDebugHelper.DrawSphere(pos, 0.5f, new Color(1f, 0f, 1f, 0.25f), 0.12f);
            if (!detonate) { t += 0.1f; yield return new WaitForSeconds(0.1f); }
        }
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in Physics.OverlapSphere(pos, 0.5f, player.dangerLayer))
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            DamageHelper.ApplyQuickProcDamage(stats, e, 0f, 1.5f, transform);
        }
        VisualDebugHelper.DrawSphere(pos, 0.5f, Color.magenta, 0.4f);
    }

    // GR_T5_02: mỗi đòn đánh +1 stack Chí mạng (5s), mỗi stack +4% bonusCritChance, tối đa 5.
    private int gr_t5_02_stacks = 0;
    private float gr_t5_02_until = 0f;
    private void GR_T5_02_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (gr_t5_02_stacks < 5)
        {
            gr_t5_02_stacks++;
            stats.bonusCritChance += 0.04f;
            stats.RecalculateStats();
        }
        gr_t5_02_until = Time.time + 5f;
    }

    // GR_T5_03: dùng Signature → reset toàn bộ hồi chiêu (kể cả Companion), trừ Signature vừa dùng.
    private void GR_T5_03_Signature()
    {
        if (skillManager != null) skillManager.ResetAllCooldownsExcept(skillManager.currentSignature);
        CompanionAI c = FindFirstObjectByType<CompanionAI>();
        if (c != null)
        {
            SkillManager csm = c.GetComponentInChildren<SkillManager>();
            if (csm != null) csm.ResetAllCooldowns();
        }
    }

    // GR_T4_01: dùng Signature → ghi nhớ địch gần nhất; sau 3s giáng Vụ Nổ Phantom AoE 3f = 200% magicAtk tại vị trí nó.
    private void GR_T4_01_Signature()
    {
        Stats marked = NearestEnemyExcept(transform.position, 30f, _gr401Empty);
        if (marked == null) return;
        StartCoroutine(GR_T4_01_Phantom(marked, marked.transform.position));
    }
    private readonly List<Stats> _gr401Empty = new List<Stats>();
    private IEnumerator GR_T4_01_Phantom(Stats marked, Vector3 fallbackPos)
    {
        yield return new WaitForSeconds(3f);
        Vector3 center = (marked != null && marked.currentHp > 0) ? marked.transform.position : fallbackPos;
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in Physics.OverlapSphere(center, 3f, player.dangerLayer))
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            DamageHelper.ApplyQuickProcDamage(stats, e, 0f, 2.0f, transform); // 200% magicAtk
        }
        VisualDebugHelper.DrawSphere(center, 3f, new Color(0.6f, 0.2f, 1f, 0.45f), 0.5f);
    }

    // GR_T5_01: hạ địch → triệu hồi Ác Quỷ Bóng Tối (tối đa 5 cùng lúc).
    private readonly List<GameObject> gr_t5_01_demons = new List<GameObject>();
    private void GR_T5_01_Kill(Stats victim, bool isBackstab)
    {
        gr_t5_01_demons.RemoveAll(d => d == null);
        if (gr_t5_01_demons.Count >= 5) return;
        Vector3 pos = (victim != null) ? victim.transform.position : transform.position;
        DarkDemonAI demon = DarkDemonAI.Spawn(pos, stats);
        if (demon != null) gr_t5_01_demons.Add(demon.gameObject);
    }

    private float gr_t3_02_added = 0f;
    private bool st_t4_05_applied = false;
    #endregion

    // =========================================================================================
    #region [ TIỆN ÍCH DÙNG CHUNG CỦA MANAGER ]

    private IEnumerator ReduceArmorRoutine(Stats target, float percent, float duration, Action onComplete = null)
    {
        if (target != null)
        {
            float amount = target.armor * percent;
            target.armor -= amount;
            yield return new WaitForSeconds(duration);
            if (target != null) target.armor += amount;
            onComplete?.Invoke();
        }
    }

    private IEnumerator BuffMoveSpeedRoutine(float amount, float duration)
    {
        stats.bonusMoveSpeed += amount;
        stats.RecalculateStats();
        yield return new WaitForSeconds(duration);
        stats.bonusMoveSpeed -= amount;
        stats.RecalculateStats();
    }

    // Kẻ địch gần 'from' nhất trong bán kính, loại trừ các Stats trong 'exclude'.
    private Stats NearestEnemyExcept(Vector3 from, float radius, List<Stats> exclude)
    {
        Collider[] hits = Physics.OverlapSphere(from, radius, player.dangerLayer);
        Stats best = null; float min = float.MaxValue;
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || exclude.Contains(e)) continue;
            float d = Vector3.SqrMagnitude(e.transform.position - from);
            if (d < min) { min = d; best = e; }
        }
        return best;
    }

    // Kéo nhẹ kẻ địch về phía 'center' (gom quái). Dùng NavMeshAgent.Warp nếu có để khỏi giật.
    private void PullEnemyTo(Stats enemy, Vector3 center, float pullSpeed)
    {
        if (enemy == null) return;
        Vector3 next = Vector3.MoveTowards(enemy.transform.position, center, pullSpeed * Time.deltaTime);
        var agent = enemy.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(next);
        else enemy.transform.position = next;
    }
    #endregion
}

// =========================================================================================
// LỚP HỖ TRỢ VISUAL DEBUG (CỰC KỲ HỮU ÍCH ĐỂ THẤY MỌI HITBOX VÀ EFFECT)
// =========================================================================================
public static class VisualDebugHelper
{
    public static void DrawSphere(Vector3 position, float radius, Color color, float duration)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * radius * 2f; // scale là đường kính
        SetupMaterialAndDestroy(go, color, duration);
    }

    public static void DrawBox(Vector3 center, Vector3 size, Quaternion rotation, Color color, float duration)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = center;
        go.transform.rotation = rotation;
        go.transform.localScale = size;
        SetupMaterialAndDestroy(go, color, duration);
    }

    private static void SetupMaterialAndDestroy(GameObject go, Color color, float duration)
    {
        // Tắt va chạm để không cản đường đánh
        Collider col = go.GetComponent<Collider>();
        if (col) col.enabled = false;

        // Tạo chất liệu màu
        Renderer r = go.GetComponent<Renderer>();
        if (r)
        {
            Material m = new Material(Shader.Find("Sprites/Default"));
            m.color = color;
            r.material = m;
        }

        GameObject.Destroy(go, duration);
    }
}
// Lớp phụ trợ sinh ra để check va chạm cho Vết nứt Không Gian (WPN_SW_T5_01)
public class SpatialRiftDamage : MonoBehaviour
{
    private float trueDamageAmount;
    private LayerMask enemyLayer;
    private List<Collider> hitTargets = new List<Collider>(); // Đảm bảo quái chỉ mất máu 1 lần
public void Initialize(float damage, LayerMask layer) { trueDamageAmount = damage; enemyLayer = layer; }
void OnTriggerEnter(Collider other)
{
    if ((enemyLayer.value & (1 << other.gameObject.layer)) > 0)
    {
        if (!hitTargets.Contains(other))
        {
            hitTargets.Add(other);
            Stats enemy = other.GetComponent<Stats>();
            if (enemy) enemy.TakeDamage(new DamageInfo { trueDamage = trueDamageAmount });
        }
    }
}
}