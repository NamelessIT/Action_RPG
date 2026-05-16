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

    private int attackHitCounter = 0;
    private float lastHitTime = 0f;

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
        onParryEffects.Add("WPN_SW_T4_05", Effect_WPN_SW_T4_05);

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
    }

    void OnEnable()
    {
        if (player != null)
        {
            player.OnHitEnemy += HandleOnHitEnemy;
            player.OnKillEnemy += HandleOnKillEnemy;
            player.OnDashPerformed += HandleOnDash;
        }
        if (stats != null)
        {
            if (stats is PlayerStats playerStats) playerStats.OnPerfectParryTriggered += HandleOnPerfectParry;
            stats.OnHealReceived += HandleOnHealReceived;
        }
    }

    void OnDisable()
    {
        if (player != null)
        {
            player.OnHitEnemy -= HandleOnHitEnemy;
            player.OnKillEnemy -= HandleOnKillEnemy;
            player.OnDashPerformed -= HandleOnDash;
        }
        if (stats != null)
        {
            if (stats is PlayerStats playerStats) playerStats.OnPerfectParryTriggered -= HandleOnPerfectParry;
            stats.OnHealReceived -= HandleOnHealReceived;
        }
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
            float dmg = stats.magicAtk * 0.3f;
            Collider[] hits = Physics.OverlapSphere(target.transform.position, 2f, player.dangerLayer);
            foreach (var hit in hits)
            {
                Stats e = hit.GetComponent<Stats>();
                if (e != null) e.TakeDamage(new DamageInfo { magicDamage = dmg, attacker = stats });
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

    private bool sw_t3_03_empowered = false;
    private void Effect_WPN_SW_T3_03_Trigger() { sw_t3_03_empowered = true; }
    private void Effect_WPN_SW_T3_03_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (sw_t3_03_empowered)
        {
            sw_t3_03_empowered = false;
            float dmg = stats.physicalAtk * 0.5f;
            target.TakeDamage(new DamageInfo { physDamage = dmg, attacker = stats });
            StartCoroutine(ReduceArmorRoutine(target, 0.15f, 3f));
            VisualDebugHelper.DrawSphere(target.transform.position, 1.5f, new Color(0, 0, 1, 0.5f), 0.5f); // Sóng xanh lam
        }
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
                target1.TakeDamage(new DamageInfo { physDamage = stats.physicalAtk * 1.2f, attacker = stats });
                VisualDebugHelper.DrawBox(target1.transform.position + Vector3.up * 2, new Vector3(0.2f, 1.5f, 0.2f), Quaternion.identity, Color.white, 0.5f);
            }
            if (target2)
            {
                target2.TakeDamage(new DamageInfo { magicDamage = stats.magicAtk * 1.2f, attacker = stats });
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
    private bool sw_t4_04_empowered = false;
    private void Effect_WPN_SW_T4_04_Trigger() { sw_t4_04_empowered = true; Debug.Log("T4_04 Sẵn sàng Liềm!"); }
    private void Effect_WPN_SW_T4_04_Hit(Stats target, int step, bool isH, bool isC)
    {
        if (sw_t4_04_empowered)
        {
            sw_t4_04_empowered = false;

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
                    DamageInfo info = CreateProcDamage(e, isC, 0f, 1.0f);
                    e.TakeDamage(info);
                    StartCoroutine(SlowEnemyRoutine(e, 0.3f, 3f));
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
            if (eStats != null) eStats.TakeDamage(new DamageInfo { physDamage = 0, isStun = true, stunDuration = 2f });
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

    private void HandleOnHealReceived(float amount, float excessAmount)
    {
        if (eqManager.currentWeapon != null && eqManager.currentWeapon.id.Trim() == "WPN_SW_T5_03")
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

            Vector3 enemyForward = closest.facingDirection == Vector3.zero ? closest.transform.forward : closest.facingDirection;
            Vector3 targetPos = closest.transform.position - enemyForward * (tRadius + 0.5f);

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            player.transform.position = targetPos;
            player.isDashing = false;

            DamageInfo info = CreateProcDamage(closest, true, 3.0f, 0f);
            closest.TakeDamage(info);
            sw_t5_04_seeds.Remove(closest);

            VisualDebugHelper.DrawSphere(closest.transform.position, 3f, new Color(1, 0, 0, 0.5f), 0.5f); // Nổ máu
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
            target.TakeDamage(new DamageInfo { physDamage = stats.physicalAtk * 0.1f, attacker = stats });
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
            bool isBoss = false; // Thay bằng target.isBoss nếu có
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
    }

    private void Effect_WPN_DG_T5_03(Stats target, bool isBackstab)
    {
        float stolenStr = 5f;
        float stolenInt = 5f;
        if (target is AllyStats enemyAlly) { stolenStr = enemyAlly.flatSTR * 0.05f; stolenInt = enemyAlly.flatINT * 0.05f; }

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
        stats.RecalculateStats();
    }

    #endregion

    // =========================================================================================
    #region [ TIỆN ÍCH DÙNG CHUNG CỦA MANAGER ]

    private DamageInfo CreateProcDamage(Stats target, bool isCrit, float physMult, float magicMult)
    {
        float t = CombatMath.CalculateDirectionFactor(transform, target);
        var dmgTuple = CombatMath.CalculateFullDamage(stats, target, t, isCrit, null, eqManager.currentWeapon, 1f);

        DamageInfo info = new DamageInfo { attacker = stats, isCrit = isCrit, sourcePosition = transform.position };
        info.physDamage = stats.physicalAtk * physMult;
        info.magicDamage = stats.magicAtk * magicMult;
        return info;
    }

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

    private IEnumerator SlowEnemyRoutine(Stats target, float percent, float duration)
    {
        if (target != null)
        {
            target.baseMoveSpeed *= (1 - percent);
            yield return new WaitForSeconds(duration);
            if (target != null) target.baseMoveSpeed /= (1 - percent);
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