using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý EFFECT của Accessory (GDD: "Tổng hợp Effect Accessory").
/// Gắn trực tiếp trên Player (cùng GameObject với EquipmentManager).
///
/// Kiến trúc:
///  - Lắng nghe event: đánh trúng (OnHitEnemy/OnDamageDealt), bị đánh chạm máu thật (OnDamageTakenHp),
///    perfect dodge, dash, kill, shield vỡ, skill/signature cast (SkillManager gọi TriggerSkillCastEffects).
///  - Mỗi khi EquipmentManager.OnEquipmentChanged → Rescan(): revert sạch mọi mod đã áp rồi áp lại
///    theo bộ accessory đang đeo (equip/unequip an toàn, không stack lậu).
///
/// GLOBAL RULES (GDD mục 2):
///  1. Hiệu ứng tiêu hao máu KHÔNG THỂ giết player — máu dừng ở 1 HP (SpendHealthSafe).
///  2. AoE on-hit hồi tài nguyên chỉ tính 1 lần mỗi cú vung (gate bằng swingId từ OnAttackPerformed).
///  3. "Mất máu" chỉ tính khi damage chạm currentHp thật — đã bảo đảm vì OnDamageTakenHp
///     chỉ bắn khi máu thật bị trừ (Stats.TakeDamage).
///
/// ⚠️ TRẠNG THÁI: ĐÃ IMPLEMENT 19/62 EFFECT (Batch 1). 43 effect còn lại CHƯA xong
/// (xem khối "GHI CHÚ BATCH SAU" cuối file — KHÔNG coi là hoàn tất).
///
/// Batch 1 (19 effect map sạch vào hạ tầng sẵn có):
///   RM (10): T3_01, T3_02, T3_03, T3_04, T4_01, T4_03, T4_04, T4_06, T5_01, T5_06
///   CH (6):  T3_01, T3_04, T3_06, T4_01, T4_02, T5_07
///   MS (2):  T4_01, T5_04
///   PA (1):  T5_01
/// Các effect còn lại cần hook chưa có (damage-modifier pipeline, kill-source attribution,
/// companion events, vision/maxSin override...) — bổ sung theo batch sau, xem ghi chú cuối file.
/// </summary>
[DisallowMultipleComponent]
public class AccessoryEffectManager : MonoBehaviour
{
    // Đặt true sau khi guard ở Awake pass → Start mới Subscribe/Rescan.
    private bool _valid = false;

    // ── ID constants (khớp DOCX) ───────────────────────────────────────────
    const string RM_T3_01 = "ACC_RM_T3_01"; // Chịu đòn trước mặt → +10 Stamina (CD 2s)
    const string RM_T3_02 = "ACC_RM_T3_02"; // Perfect Dodge → +10% damage 3s
    const string RM_T3_03 = "ACC_RM_T3_03"; // Đánh thường trúng → -0.1s CD kỹ năng E
    const string RM_T3_04 = "ACC_RM_T3_04"; // Dưới 50% máu → +15% effectRes
    const string RM_T4_01 = "ACC_RM_T4_01"; // Nhận >20% maxHP trong 2s → shield 15% maxHP 5s (CD 30s)
    const string RM_T4_03 = "ACC_RM_T4_03"; // Sau Skill E → +20% magicAtk +20% atkSpeed 4s
    const string RM_T4_04 = "ACC_RM_T4_04"; // +4% physAtk mỗi 10% máu đã mất
    const string RM_T4_06 = "ACC_RM_T4_06"; // Mỗi hit +2% atkSpeed, max 10 stack, mất sau 3s không đánh
    const string RM_T5_01 = "ACC_RM_T5_01"; // +15% damage nếu 10s không nhận sát thương
    const string RM_T5_06 = "ACC_RM_T5_06"; // Crit → +true damage 5% máu hiện tại địch (CD 1s)
    const string CH_T3_01 = "ACC_CH_T3_01"; // Dùng Skill E → +10% bonusSinGain 5s
    const string CH_T3_04 = "ACC_CH_T3_04"; // Heavy attack trúng → +10 Stamina (1 lần/cú vung)
    const string CH_T3_06 = "ACC_CH_T3_06"; // Perfect Dodge → -1s CD tất cả kỹ năng
    const string CH_T4_01 = "ACC_CH_T4_01"; // Gây phys → +1 Sin; gây magic → +2 Stamina (CD 0.5s mỗi loại)
    const string CH_T4_02 = "ACC_CH_T4_02"; // Nhận phys → +2 Stamina; nhận magic → +1 Sin (CD 0.5s mỗi loại)
    const string CH_T5_07 = "ACC_CH_T5_07"; // Shield vỡ → hồi 100% Stamina
    const string MS_T4_01 = "ACC_MS_T4_01"; // HP<30% → Cuồng Nộ 10s (CD 15s), khóa hồi máu
    const string MS_T5_04 = "ACC_MS_T5_04"; // Mất 1% maxHP → +2% Sin & Stamina; -20% moveSpeed
    const string PA_T5_01 = "ACC_PA_T5_01"; // +20% physAtk; mỗi hit tự mất 1% maxHP

    // ── References ─────────────────────────────────────────────────────────
    private AllyStats        stats;
    private PlayerController playerController;
    private EquipmentManager equipmentManager;
    private SkillManager     skillManager;

    // ── Equipped set ───────────────────────────────────────────────────────
    private readonly HashSet<string> equipped = new HashSet<string>();
    private bool Has(string id) => equipped.Contains(id);

    // ── Timed buff hệ thống chung: KEYED — mỗi key chỉ 1 instance đang chạy ──
    // Effect thiết kế KHÔNG stack: kích hoạt lại trong lúc còn hiệu lực = REFRESH thời lượng,
    // KHÔNG cộng dồn mod. (Vd RM_T3_02 +10% dmg: né hoàn hảo 2 lần liên tiếp vẫn chỉ +10%, gia hạn 3s.)
    private class TimedMod { public float endTime; public System.Action revert; }
    private readonly Dictionary<string, TimedMod> timedMods = new Dictionary<string, TimedMod>();
    private readonly List<string> _expiredKeys = new List<string>(); // buffer xóa key hết hạn (tránh sửa dict khi đang duyệt)

    // ── Healing-block ───────────────────────────────────────────────────────
    // Khóa hồi máu qua COUNTER DÙNG CHUNG trong Stats (Push/Pop). Mỗi nguồn (Ravager,
    // BloodReaver, accessory này...) tự push/pop riêng → KHÔNG bao giờ đạp khóa của nguồn khác:
    // Stats.isHealingBlocked chỉ false khi MỌI nguồn đã nhả. Ta giữ _ownHealBlocks để biết
    // số khóa của RIÊNG mình mà nhả đúng số đó khi Rescan/Destroy (không pop dư của nguồn khác).
    private int _ownHealBlocks = 0;
    private void BlockHealing(bool block)
    {
        if (block)
        {
            stats.PushHealingBlock();
            _ownHealBlocks++;
        }
        else if (_ownHealBlocks > 0)
        {
            stats.PopHealingBlock();
            _ownHealBlocks--;
        }
    }

    /// <summary>
    /// Áp 1 timed buff theo key. Nếu key đã chạy → chỉ gia hạn endTime (refresh), KHÔNG apply lần 2.
    /// Nếu chưa có → apply rồi đăng ký revert khi hết hạn.
    /// </summary>
    private void AddTimedMod(string key, float duration, System.Action apply, System.Action revert)
    {
        if (timedMods.TryGetValue(key, out var existing))
        {
            existing.endTime = Time.time + duration; // refresh, không stack
            return;
        }
        apply?.Invoke();
        timedMods[key] = new TimedMod { endTime = Time.time + duration, revert = revert };
    }

    // ── State per-effect ────────────────────────────────────────────────────
    // Rule 2: swing gate
    private int swingId = 0;
    private int ch_t3_04_lastSwingGranted = -1;

    // Cooldown timestamps
    private float rm_t3_01_nextReady = -999f;
    private float rm_t4_01_nextReady = -999f;
    private float rm_t5_06_nextReady = -999f;
    private float ch_t4_01_physNext = -999f, ch_t4_01_magicNext = -999f;
    private float ch_t4_02_physNext = -999f, ch_t4_02_magicNext = -999f;
    private float ms_t4_01_nextReady = -999f;

    // Conditional toggles (áp/gỡ trong Update)
    private bool rm_t3_04_applied = false;       // +0.15 effectRes khi HP<50%
    private bool rm_t5_01_applied = false;       // +0.15 dmg khi 10s không nhận sát thương
    private float lastHpDamageTime = -999f;      // mốc cho RM_T5_01

    // Frame-based (revert-then-apply mỗi frame, pattern BattleMagePassive)
    private float rm_t4_04_added = 0f;           // bonusPhysicalAtk đã cộng theo máu đã mất

    // Attack-speed stacks (RM_T4_06)
    private int   rm_t4_06_stacks = 0;
    private float rm_t4_06_lastHitTime = -999f;
    private float rm_t4_06_added = 0f;

    // Damage pool 2s (RM_T4_01)
    private readonly List<(float time, float dmg)> rm_t4_01_pool = new List<(float, float)>();

    // Cuồng Nộ (MS_T4_01)
    private bool ms_t4_01_active = false;

    // Persistent mods đang áp (gỡ khi Rescan/Disable)
    private bool ms_t5_04_applied = false;       // -20% bonusMoveSpeed
    private bool pa_t5_01_applied = false;       // +20% bonusPhysicalAtk

    // ── Lifecycle ───────────────────────────────────────────────────────────
    private void Awake()
    {
        // Component NÀY là player-only. Chỉ resolve trong phạm vi cùng GameObject / parent / child
        // (KHÔNG FindFirstObjectByType) → nếu vô tình bị gắn trên Enemy thì KHÔNG "tóm" Player global,
        // mà tự vô hiệu hóa, tránh enemy subscribe nhầm event của Player gây bug ngầm.
        stats            = Resolve<AllyStats>();
        playerController = Resolve<PlayerController>();
        equipmentManager = Resolve<EquipmentManager>();
        skillManager     = Resolve<SkillManager>();

        // Fail-safe: thiếu AllyStats hoặc EquipmentManager (vd gắn nhầm trên Enemy đã remove các script đó)
        // → log 1 lần, tắt component, KHÔNG Subscribe/Rescan.
        if (stats == null || equipmentManager == null)
        {
            Debug.LogError($"[AccessoryEffect] '{gameObject.name}' thiếu " +
                           $"{(stats == null ? "AllyStats " : "")}{(equipmentManager == null ? "EquipmentManager " : "")}" +
                           "→ vô hiệu hóa. Component này CHỈ dành cho Player root.");
            enabled = false;
            return;
        }

        if (skillManager == null)
            Debug.LogWarning($"[AccessoryEffect] Không thấy SkillManager gần '{gameObject.name}' " +
                             "(effect liên quan skill sẽ bị bỏ qua).");

        _valid = true;
    }

    /// <summary>Tìm component theo thứ tự: cùng GameObject → parent → child (KHÔNG tìm global).</summary>
    private T Resolve<T>() where T : Component
    {
        T c = GetComponent<T>();
        if (c == null) c = GetComponentInParent<T>();
        if (c == null) c = GetComponentInChildren<T>();
        return c;
    }

    private void Start()
    {
        if (!_valid) return; // guard ở Awake fail → không chạy gì
        Subscribe();
        Rescan();
    }

    private void OnDestroy()
    {
        if (!_valid) return; // chưa từng Subscribe/áp mod thì không cần gỡ
        Unsubscribe();
        RevertEverything();
    }

    private void Subscribe()
    {
        if (equipmentManager != null) equipmentManager.OnEquipmentChanged += Rescan;
        if (playerController != null)
        {
            playerController.OnHitEnemy       += HandleHitEnemy;
            playerController.OnDamageDealt    += HandleDamageDealt;
            playerController.OnAttackPerformed += HandleAttackPerformed;
        }
        if (stats != null)
        {
            stats.OnPerfectDodgeTriggered += HandlePerfectDodge;
            stats.OnDamageTakenHp         += HandleDamageTakenHp;
            stats.OnShieldBroken          += HandleShieldBroken;
        }
    }

    private void Unsubscribe()
    {
        if (equipmentManager != null) equipmentManager.OnEquipmentChanged -= Rescan;
        if (playerController != null)
        {
            playerController.OnHitEnemy       -= HandleHitEnemy;
            playerController.OnDamageDealt    -= HandleDamageDealt;
            playerController.OnAttackPerformed -= HandleAttackPerformed;
        }
        if (stats != null)
        {
            stats.OnPerfectDodgeTriggered -= HandlePerfectDodge;
            stats.OnDamageTakenHp         -= HandleDamageTakenHp;
            stats.OnShieldBroken          -= HandleShieldBroken;
        }
    }

    // ── Rescan khi đổi trang bị ─────────────────────────────────────────────
    private void Rescan()
    {
        RevertEverything();

        equipped.Clear();
        if (equipmentManager == null) return;
        AddIfNotNull(equipmentManager.currentCoreShard);
        AddIfNotNull(equipmentManager.currentMarkOfSin);
        AddIfNotNull(equipmentManager.currentRelicOfMemory);
        AddIfNotNull(equipmentManager.currentParasite);
        AddIfNotNull(equipmentManager.currentChain);

        ApplyPersistent();
        Debug.Log($"[AccessoryEffect] Rescan: {equipped.Count} accessory có ID — [{string.Join(", ", equipped)}]");
    }

    private void AddIfNotNull(AccessoryData acc)
    {
        if (acc != null && !string.IsNullOrEmpty(acc.id)) equipped.Add(acc.id.Trim());
    }

    /// <summary>Áp các mod thường trực (passive vô điều kiện) theo bộ accessory hiện tại.</summary>
    private void ApplyPersistent()
    {
        if (stats == null) return;

        if (Has(MS_T5_04) && !ms_t5_04_applied)
        {
            stats.bonusMoveSpeed -= 0.20f;
            stats.CalculateMoveSpeedOnly();
            ms_t5_04_applied = true;
        }
        if (Has(PA_T5_01) && !pa_t5_01_applied)
        {
            stats.bonusPhysicalAtk += 0.20f;
            stats.CalculateCombatStatsOnly();
            pa_t5_01_applied = true;
        }
    }

    /// <summary>Gỡ SẠCH mọi mod do manager này áp (timed/conditional/frame/persistent).</summary>
    private void RevertEverything()
    {
        if (stats == null) return;

        // 1. Timed buff đang chạy
        foreach (var kv in timedMods) kv.Value.revert?.Invoke();
        timedMods.Clear();

        // 2. Conditional toggles
        if (rm_t3_04_applied) { stats.resistanceEffect -= 0.15f; rm_t3_04_applied = false; }
        if (rm_t5_01_applied) { stats.damageOutputMultiplier -= 0.15f; rm_t5_01_applied = false; }

        // 3. Frame-based
        if (rm_t4_04_added != 0f) { stats.bonusPhysicalAtk -= rm_t4_04_added; rm_t4_04_added = 0f; }
        if (rm_t4_06_added != 0f) { stats.bonusAttackSpeed -= rm_t4_06_added; rm_t4_06_added = 0f; }
        rm_t4_06_stacks = 0;
        rm_t4_01_pool.Clear();

        // 4. Cuồng Nộ đang chạy (revert nằm trong timedMods đã chạy ở bước 1 — chỉ reset cờ)
        ms_t4_01_active = false;
        // An toàn: nhả ĐÚNG số khóa hồi máu của RIÊNG ta (nếu revert chưa cân), KHÔNG đụng khóa nguồn khác.
        while (_ownHealBlocks > 0) { stats.PopHealingBlock(); _ownHealBlocks--; }

        // 5. Persistent
        if (ms_t5_04_applied) { stats.bonusMoveSpeed += 0.20f; ms_t5_04_applied = false; stats.CalculateMoveSpeedOnly(); }
        if (pa_t5_01_applied) { stats.bonusPhysicalAtk -= 0.20f; pa_t5_01_applied = false; }

        stats.CalculateCombatStatsOnly();
    }

    // ── GLOBAL RULE 1: tiêu máu không thể giết player ───────────────────────
    /// <summary>Trừ máu an toàn: máu tối thiểu dừng ở 1. Trả false nếu đang 1 HP (không kích hoạt được).</summary>
    private bool SpendHealthSafe(float amount)
    {
        if (stats == null || amount <= 0f) return false;
        if (stats.currentHp <= 1f) return false; // Rule 1: 1 HP thì kỹ năng yêu cầu máu không kích hoạt
        stats.currentHp = Mathf.Max(1f, stats.currentHp - amount);
        return true;
    }

    private void RestoreStamina(float amount)
    {
        if (stats == null || amount <= 0f) return;
        stats.currentStamina = Mathf.Min(stats.maxStamina, stats.currentStamina + amount);
    }

    private void RestoreSin(float amount)
    {
        if (stats == null || amount <= 0f) return;
        stats.currentSin = Mathf.Min(stats.maxSin, stats.currentSin + amount);
    }

    // ── EVENT HANDLERS ──────────────────────────────────────────────────────

    /// <summary>Mỗi cú vung vũ khí (per-swing, từ WeaponAttackDispatcher) — mốc cho Rule 2.</summary>
    private void HandleAttackPerformed(int stepIndex, bool isHeavy) => swingId++;

    /// <summary>Đánh trúng 1 mục tiêu (per-target). victim, stepIndex, isHeavy, isCrit.</summary>
    private void HandleHitEnemy(Stats victim, int stepIndex, bool isHeavy, bool isCrit)
    {
        // RM_T3_03: đòn THƯỜNG trúng → giảm 0.1s CD kỹ năng E
        if (Has(RM_T3_03) && !isHeavy && skillManager != null && skillManager.currentSkill != null)
            skillManager.ReduceSkillCooldown(skillManager.currentSkill, 0.1f);

        // RM_T4_06: stack tốc đánh
        if (Has(RM_T4_06))
        {
            rm_t4_06_lastHitTime = Time.time;
            if (rm_t4_06_stacks < 10)
            {
                rm_t4_06_stacks++;
                stats.bonusAttackSpeed += 0.02f;
                rm_t4_06_added += 0.02f;
                stats.CalculateCombatStatsOnly();
            }
        }

        // RM_T5_06: crit → bồi 1 đòn Sát thương Chuẩn = 5% máu HIỆN TẠI của địch (CD 1s)
        if (Has(RM_T5_06) && isCrit && victim != null && Time.time >= rm_t5_06_nextReady)
        {
            rm_t5_06_nextReady = Time.time + 1f;
            var info = new DamageInfo
            {
                attacker       = stats,
                sourcePosition = transform.position,
                trueDamage     = victim.currentHp * 0.05f
            };
            victim.TakeDamage(info);
        }

        // CH_T3_04: heavy trúng → +10 Stamina — Rule 2: 1 lần mỗi cú vung
        if (Has(CH_T3_04) && isHeavy && ch_t3_04_lastSwingGranted != swingId)
        {
            ch_t3_04_lastSwingGranted = swingId;
            RestoreStamina(10f);
        }

        // PA_T5_01: mỗi hit tự mất 1% máu tối đa (Rule 1: không thể chết)
        if (Has(PA_T5_01))
            SpendHealthSafe(stats.maxHp * 0.01f);
    }

    /// <summary>Gây damage kèm DamageInfo đầy đủ (biết phys/magic/true).</summary>
    private void HandleDamageDealt(Stats victim, DamageInfo info)
    {
        // CH_T4_01: gây phys → +1 Sin; gây magic → +2 Stamina (CD 0.5s riêng từng loại)
        if (Has(CH_T4_01))
        {
            if (info.physDamage > 0f && Time.time >= ch_t4_01_physNext)
            {
                ch_t4_01_physNext = Time.time + 0.5f;
                RestoreSin(1f);
            }
            if (info.magicDamage > 0f && Time.time >= ch_t4_01_magicNext)
            {
                ch_t4_01_magicNext = Time.time + 0.5f;
                RestoreStamina(2f);
            }
        }
    }

    /// <summary>Nhận sát thương CHẠM MÁU THẬT (Rule 3 đã bảo đảm ở Stats).</summary>
    private void HandleDamageTakenHp(DamageInfo info, float hpLost)
    {
        lastHpDamageTime = Time.time;

        // RM_T3_01: chịu đòn từ TRƯỚC MẶT (t<=0.25) → +10 Stamina (CD 2s, không kích hoạt khi đầy)
        if (Has(RM_T3_01) && Time.time >= rm_t3_01_nextReady
            && stats.currentStamina < stats.maxStamina
            && info.attacker != null)
        {
            float t = CombatMath.CalculateDirectionFactor(info.attacker.transform, stats);
            if (t <= 0.25f)
            {
                rm_t3_01_nextReady = Time.time + 2f;
                RestoreStamina(10f);
            }
        }

        // RM_T4_01: bể chứa sát thương 2s ≥ 20% maxHP → shield 15% maxHP trong 5s (CD 30s)
        if (Has(RM_T4_01))
        {
            rm_t4_01_pool.Add((Time.time, hpLost));
            rm_t4_01_pool.RemoveAll(e => Time.time - e.time > 2f);
            if (Time.time >= rm_t4_01_nextReady)
            {
                float sum = 0f;
                foreach (var e in rm_t4_01_pool) sum += e.dmg;
                if (sum >= stats.maxHp * 0.20f)
                {
                    rm_t4_01_nextReady = Time.time + 30f;
                    rm_t4_01_pool.Clear();
                    stats.AddShield(stats.maxHp * 0.15f, 5f);
                    Debug.Log("<color=cyan>[ACC_RM_T4_01]</color> Kích hoạt lá chắn khẩn cấp!");
                }
            }
        }

        // CH_T4_02: nhận phys → +2 Stamina; nhận magic → +1 Sin (CD 0.5s riêng từng loại)
        if (Has(CH_T4_02))
        {
            if (info.physDamage > 0f && Time.time >= ch_t4_02_physNext)
            {
                ch_t4_02_physNext = Time.time + 0.5f;
                RestoreStamina(2f);
            }
            if (info.magicDamage > 0f && Time.time >= ch_t4_02_magicNext)
            {
                ch_t4_02_magicNext = Time.time + 0.5f;
                RestoreSin(1f);
            }
        }

        // MS_T5_04: mỗi 1% maxHP mất → hồi 2% Sin & Stamina
        if (Has(MS_T5_04))
        {
            float pctLost = (hpLost / stats.maxHp) * 100f;
            RestoreSin(stats.maxSin * 0.02f * pctLost);
            RestoreStamina(stats.maxStamina * 0.02f * pctLost);
        }
    }

    private void HandlePerfectDodge()
    {
        // RM_T3_02: +10% sát thương gây ra trong 3s (né liên tiếp = refresh, không stack)
        if (Has(RM_T3_02))
            AddTimedMod(RM_T3_02, 3f,
                apply:  () => stats.damageOutputMultiplier += 0.10f,
                revert: () => stats.damageOutputMultiplier -= 0.10f);

        // CH_T3_06: giảm 1s CD tất cả kỹ năng đang chờ
        if (Has(CH_T3_06) && skillManager != null)
            skillManager.ReduceAllCooldowns(1f);
    }

    private void HandleShieldBroken()
    {
        // CH_T5_07: shield vỡ → hồi 100% Stamina
        if (Has(CH_T5_07))
            RestoreStamina(stats.maxStamina);
    }

    /// <summary>SkillManager gọi khi cast skill THÀNH CÔNG (cùng pattern CoreShield/Weapon).</summary>
    public void TriggerSkillCastEffects(SkillData.SkillType skillType)
    {
        if (stats == null) return;

        if (skillType == SkillData.SkillType.Skill)
        {
            // RM_T4_03: sau Skill E → +20% bonusMagicAtk + 20% bonusAttackSpeed trong 4s (refresh)
            if (Has(RM_T4_03))
                AddTimedMod(RM_T4_03, 4f,
                    apply: () =>
                    {
                        stats.bonusMagicAtk    += 0.20f;
                        stats.bonusAttackSpeed += 0.20f;
                        stats.CalculateCombatStatsOnly();
                    },
                    revert: () =>
                    {
                        stats.bonusMagicAtk    -= 0.20f;
                        stats.bonusAttackSpeed -= 0.20f;
                        stats.CalculateCombatStatsOnly();
                    });

            // CH_T3_01: dùng Skill E → +10% bonusSinGain trong 5s (refresh)
            if (Has(CH_T3_01))
                AddTimedMod(CH_T3_01, 5f,
                    apply:  () => { stats.bonusSinGain += 0.10f; stats.RecalculateStats(); },
                    revert: () => { stats.bonusSinGain -= 0.10f; stats.RecalculateStats(); });
        }
    }

    // ── UPDATE: timed buff + conditional + frame-based ──────────────────────
    private void Update()
    {
        if (stats == null || stats.isDead) return;
        float now = Time.time;

        // 1. Hết hạn timed buff → revert (keyed dictionary)
        if (timedMods.Count > 0)
        {
            _expiredKeys.Clear();
            foreach (var kv in timedMods)
                if (now >= kv.Value.endTime) _expiredKeys.Add(kv.Key);
            foreach (var key in _expiredKeys)
            {
                timedMods[key].revert?.Invoke();
                timedMods.Remove(key);
            }
        }

        // 2. RM_T3_04: dưới 50% máu → +15% effectRes
        if (Has(RM_T3_04))
        {
            bool low = stats.currentHp < stats.maxHp * 0.5f;
            if (low && !rm_t3_04_applied)  { stats.resistanceEffect += 0.15f; rm_t3_04_applied = true; }
            if (!low && rm_t3_04_applied)  { stats.resistanceEffect -= 0.15f; rm_t3_04_applied = false; }
        }

        // 3. RM_T5_01: 10s không nhận sát thương → +15% damage
        if (Has(RM_T5_01))
        {
            bool calm = now - lastHpDamageTime >= 10f;
            if (calm && !rm_t5_01_applied)  { stats.damageOutputMultiplier += 0.15f; rm_t5_01_applied = true; }
            if (!calm && rm_t5_01_applied)  { stats.damageOutputMultiplier -= 0.15f; rm_t5_01_applied = false; }
        }

        // 4. RM_T4_04: +4% physAtk mỗi 10% máu đã mất (revert-then-apply, pattern BattleMagePassive)
        if (Has(RM_T4_04))
        {
            stats.bonusPhysicalAtk -= rm_t4_04_added;
            float missingPct = 1f - (stats.currentHp / Mathf.Max(1f, stats.maxHp));
            rm_t4_04_added = Mathf.Floor(missingPct * 10f) * 0.04f;
            stats.bonusPhysicalAtk += rm_t4_04_added;
            stats.CalculateCombatStatsOnly();
        }

        // 5. RM_T4_06: mất sạch stack nếu 3s không đánh trúng
        if (Has(RM_T4_06) && rm_t4_06_stacks > 0 && now - rm_t4_06_lastHitTime > 3f)
        {
            stats.bonusAttackSpeed -= rm_t4_06_added;
            rm_t4_06_added  = 0f;
            rm_t4_06_stacks = 0;
            stats.CalculateCombatStatsOnly();
        }

        // 6. MS_T4_01: HP<30% → Cuồng Nộ (CD 15s): +30% physAtk +20% atkSpeed, KHÓA hồi máu 10s
        if (Has(MS_T4_01) && !ms_t4_01_active && now >= ms_t4_01_nextReady
            && stats.currentHp < stats.maxHp * 0.30f)
        {
            ms_t4_01_active   = true;
            ms_t4_01_nextReady = now + 15f;
            Debug.Log("<color=red>[ACC_MS_T4_01]</color> CUỒNG NỘ kích hoạt!");
            AddTimedMod(MS_T4_01, 10f,
                apply: () =>
                {
                    stats.bonusPhysicalAtk += 0.30f;
                    stats.bonusAttackSpeed += 0.20f;
                    BlockHealing(true);
                    stats.CalculateCombatStatsOnly();
                },
                revert: () =>
                {
                    stats.bonusPhysicalAtk -= 0.30f;
                    stats.bonusAttackSpeed -= 0.20f;
                    BlockHealing(false);
                    stats.CalculateCombatStatsOnly();
                    ms_t4_01_active = false;
                });
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GHI CHÚ BATCH SAU (effect chưa implement và lý do):
//  - RM_T3_05, MS_T5_05, RM_T5_03: cần damage-modifier pipeline (sửa damage TRƯỚC khi gửi).
//  - RM_T3_06, CH_T5_01: cần kill-source attribution (giết bằng skill nào).
//  - RM_T4_05, RM_T5_07, CH_T4_06, CH_T5_06, MS_T4_03, MS_T5_07, PA_T5_08: cần Companion events/stats hooks.
//  - CH_T3_03, CH_T5_08, PA_T5_05: đụng maxSin (bị RecalculateStats ghi đè theo sinChargeReq) — cần refactor maxSin.
//  - CH_T3_05: cần lifesteal-heal hook; CH_T4_04: cần stamina-regen-interrupt hook; CH_T5_02/MS_T4_06: cần resource-payment override trong SkillBehavior.Use().
//  - CH_T4_03, MS_T5_03: cần skill-cast pre-hook (gồng/sửa damage signature).
//  - MS_T4_02 (phần +20% dmg nhận), PA_T5_03 (+20% true dmg nhận): cần incoming-damage modifier.
//  - MS_T4_04, PA_T5_02, PA_T5_04 (phần cấm dash/chạy): cần movement gate trong PlayerController.
//  - MS_T5_01 (chặn lethal), PA_T5_06 (khiên bất tử 1 đòn): dùng damageInterceptor/OnBeforeTakeDamage (hạ tầng CÓ SẴN — ưu tiên batch 2).
//  - MS_T5_02, MS_T5_06, MS_T5_08, RM_T5_02, RM_T5_04, RM_T5_05, CH_T4_05, CH_T5_03..06, PA_T5_07: phức tạp riêng lẻ, xem DOCX.
// ─────────────────────────────────────────────────────────────────────────────
