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
/// ⚠️ TRẠNG THÁI: ĐÃ IMPLEMENT 62/62 EFFECT (Batch 1→18). Chỉ còn 1 phần phụ là UI:
///   - PA_T5_07: phần MẶT HẠI UI (vision tối 4 góc + ẩn HP bar địch) — cần HUD (phần crit 100% đã xong).
/// Batch 18 (sau khi Companion có hệ Sin + cast skill):
///   - CH_T5_06: companion maxSinOverride=500 + hồi 1 Sin/s khi giao tranh.
///   - RM_T5_07: hoàn tất phần "Companion dùng Skill → player +15% atkSpd 5s" (event CompanionSkillController.OnCompanionSkillUsed).
///
/// Batch 17 (3 effect cuối nhóm chính):
///   RM (1):  T5_04 (vùng tử khí đếm death → bắn projectile)
///   MS (1):  T5_06 (choáng liên tục 2s → Hóa Quỷ)
///   CH (1):  T4_03 (gồng 1s + -30% CD/phí; mất máu lúc gồng → mất trắng)
///
/// Batch 16 (2 effect — companion bổ sung):
///   RM (1):  T5_07 (3/4 phần; phần "Companion dùng Skill → player +atkSpd" inert vì chưa có event companion-skill)
///   MS (1):  T4_03 (hồi sinh companion qua Stats.Revive)
///
/// Batch 15 (1 effect):
///   PA (1):  T5_08 (Companion +200% dmg + bất tử; player đánh thường 0 dmg + -50% nguồn khác)
///
/// Batch 14 (2 effect):
///   MS (2):  T5_03 (Signature x1.5 dmg + -50% SinGain 10s), T5_02 (chỉ heal từ hút máu; hút máu x3 khi HP&lt;50%)
///
/// Batch 13 (2 effect — lẻ):
///   CH (2):  T5_03 (CDR realtime theo atkSpeed + 15 Sin/Signature), T4_04 (dash/sprint không gián đoạn hồi Stamina)
///
/// Batch 12 (2 effect — kill-source attribution qua DamageHelper → PlayerController.OnSkillKillEnemy):
///   RM (1):  T3_06
///   CH (1):  T5_01
///
/// Batch 11 (1 effect — MS_T4_06, nối tiếp resource-payment):
///   MS (1):  T4_06 (20HP/Sin ghi đè CH_T5_02; Sig dùng máu +30% dmg; -50% SinGain 5s)
///
/// Batch 10 (3 effect — phí Sin Signature qua AllyStats.accSignatureSinCostMult):
///   CH (2):  T3_03, T5_08
///   PA (1):  T5_05
///
/// Batch 9 (1 effect — resource-payment override qua SkillBehavior.Use + Stats.TryConsumeStamina):
///   CH (1):  T5_02
///
/// Batch 8 (3 effect — stamina-consume hook qua Stats.TryConsumeStamina + cờ AllyStats):
///   RM (1):  T5_02
///   CH (2):  T5_04, T5_05
///
/// Batch 7 (3 effect — conditional/passive sạch):
///   RM (1):  T5_05
///   MS (1):  T5_08
///   PA (1):  T5_07 (chỉ phần crit 100%; mặt hại UI vision/HP-bar CHƯA làm — cần HUD)
///
/// Batch 6 (3 effect — companion hooks, bind động qua EnsureCompanionBinding):
///   RM (1):  T4_05
///   CH (1):  T4_06
///   MS (1):  T5_07
///
/// Batch 5 (3 effect — movement gate / dash / standing):
///   MS (1):  T4_04 (Dash→tàng hình dùng stats.isInvisible)
///   PA (2):  T5_02, T5_04 (gate Dash/Sprint qua AllyStats.accBlockDashSprint)
///
/// Batch 3 (4 effect — damage-modifier pipeline cho đòn đánh, qua PlayerController.ApplyDamageToTarget):
///   RM (3):  T3_05, T4_02, T5_03
///   MS (1):  T5_05
/// Batch 4 (3 effect — incoming-damage modifier qua OnBeforeTakeDamage):
///   MS (1):  T4_02
///   PA (1):  T5_03  ("không bị làm chậm" = no-op, chưa có nguồn slow lên player)
///   CH (1):  T4_05
///
/// Batch 1 (19 effect map sạch vào hạ tầng sẵn có):
///   RM (10): T3_01, T3_02, T3_03, T3_04, T4_01, T4_03, T4_04, T4_06, T5_01, T5_06
///   CH (6):  T3_01, T3_04, T3_06, T4_01, T4_02, T5_07
///   MS (2):  T4_01, T5_04
///   PA (1):  T5_01
/// Batch 2 (6 effect — hạ tầng sẵn có gồm OnHealDetailed/OnKillEnemy/OnBeforeTakeDamage):
///   RM (1):  T5_08
///   CH (2):  T3_02, T3_05
///   MS (2):  T4_05, T5_01
///   PA (1):  T5_06
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

    // ── Batch 2 (hạ tầng sẵn có) ──
    const string RM_T5_08 = "ACC_RM_T5_08"; // Skill→ -3s CD Signature; Signature→ -3s CD Skill
    const string CH_T3_02 = "ACC_CH_T3_02"; // Mất 5% máu→1 điểm Gan dạ (30s); đủ 3→nổ 500 true dmg + full Stamina
    const string CH_T3_05 = "ACC_CH_T3_05"; // 1% máu hồi từ Hút máu → +1 Stamina
    const string MS_T4_05 = "ACC_MS_T4_05"; // Hạ địch→hồi 20% maxHP; 10s không hạ ai→tự mất 10% máu (chặn 1 HP)
    const string MS_T5_01 = "ACC_MS_T5_01"; // Nhận đòn chí mạng→xoá đòn + bất tử 3s, hết→set 20% maxHP (CD 30s)
    const string PA_T5_06 = "ACC_PA_T5_06"; // Đứng yên 3s→khiên bất tử 1 đòn; vỡ→ -50% Giáp/MR (hồi sau 3s)

    // ── Batch 3 (damage-modifier pipeline cho đòn đánh thường) ──
    const string RM_T3_05 = "ACC_RM_T3_05"; // Đánh thường +15% dmg với địch >50% máu
    const string RM_T4_02 = "ACC_RM_T4_02"; // Đánh từ sau (t=1) chắc chắn crit + +20% bonusCritMultiplier
    const string RM_T5_03 = "ACC_RM_T5_03"; // Đánh thường xuyên 25% giáp; crit xuyên 50% giáp
    const string MS_T5_05 = "ACC_MS_T5_05"; // Đánh thường: 5% x5 dmg (Jackpot); 5% tự nhận 10% maxHP

    // ── Batch 4 (incoming-damage modifier qua OnBeforeTakeDamage) ──
    const string MS_T4_02 = "ACC_MS_T4_02"; // Perfect Dodge → +100% bonusCritChance 3s; nhận thêm 20% dmg 3s
    const string PA_T5_03 = "ACC_PA_T5_03"; // Miễn CC (+không bị làm chậm); nhận thêm 20% True dmg từ mọi đòn
    const string CH_T4_05 = "ACC_CH_T4_05"; // Stamina đầy → -20% dmg nhận; Sin đầy → +20% damageOutputMultiplier

    // ── Batch 5 (movement gate / dash / standing) ──
    const string MS_T4_04 = "ACC_MS_T4_04"; // HP<30%: +50% moveSpeed; Dash→tàng hình 2s + 10s tự mất 1% maxHP/s
    const string PA_T5_02 = "ACC_PA_T5_02"; // -30% bonusMoveSpeed; đứng yên >1s → tua nhanh CD x2 tới khi di chuyển
    const string PA_T5_04 = "ACC_PA_T5_04"; // Hồi 2% maxHP/s; cấm Dash & chạy nhanh

    // ── Batch 6 (companion hooks) ──
    const string RM_T4_05 = "ACC_RM_T4_05"; // Companion +20% bonusHp + +20% damageOutputMultiplier
    const string CH_T4_06 = "ACC_CH_T4_06"; // Companion đánh trúng → 50% hồi 5 Stamina + 3 Sin cho player
    const string MS_T5_07 = "ACC_MS_T5_07"; // Gánh 100% dmg companion; player HP<20% maxHp → companion chết ngay

    // ── Batch 7 (conditional/passive sạch) ──
    const string RM_T5_05 = "ACC_RM_T5_05"; // HP>70% → miễn CC + hồi 1% maxHP/s
    const string MS_T5_08 = "ACC_MS_T5_08"; // Mỗi 30s giao tranh: +10% dmg, -10% maxHP, max 9; reset khi out-combat 15s
    const string PA_T5_07 = "ACC_PA_T5_07"; // Crit luôn 100% (mặt hại UI: vision tối + ẩn HP bar — CHƯA làm, cần HUD)

    // ── Batch 8 (stamina-consume hook) ──
    const string RM_T5_02 = "ACC_RM_T5_02"; // -50% Stamina tiêu hao; hết Stamina → hồi 50 (CD 15s)
    const string CH_T5_04 = "ACC_CH_T5_04"; // HP>80% → Stamina không bao giờ giảm
    const string CH_T5_05 = "ACC_CH_T5_05"; // Tiêu Stamina → nhận Sin = 30% lượng tiêu hao

    // ── Batch 9 (resource-payment override) ──
    const string CH_T5_02 = "ACC_CH_T5_02"; // Thiếu Sin (Signature)/Stamina → trả bằng Máu (x10)

    // ── Batch 10 (phí Sin Signature / "maxSin") ──
    const string CH_T3_03 = "ACC_CH_T3_03"; // Giảm 10% phí Sin Signature
    const string CH_T5_08 = "ACC_CH_T5_08"; // Xóa CD Signature; dùng liên tiếp <5s → nhân đôi phí Sin, reset sau 5s
    const string PA_T5_05 = "ACC_PA_T5_05"; // Signature miễn phí Sin nhưng tiêu 10% maxHp (ghi đè mọi acc Sin khác)
    const string MS_T4_06 = "ACC_MS_T4_06"; // Thiếu Sin → Signature dùng Máu 20HP/Sin (ghi đè CH_T5_02 phần Sin); Sig dùng máu +30% dmg; sau đó -50% SinGain 5s

    // ── Batch 12 (kill-source attribution) ──
    const string RM_T3_06 = "ACC_RM_T3_06"; // Hạ địch bằng Skill E → +20 Sin (1 lần/lần dùng E)
    const string CH_T5_01 = "ACC_CH_T5_01"; // Signature hạ địch → hoàn 30% CD + 20% Sin của Signature (1 lần/lần dùng)

    // ── Batch 13 (lẻ) ──
    const string CH_T5_03 = "ACC_CH_T5_03"; // +bonusCDR = 30% bonusAttackSpeed (realtime); dùng Signature → +15 Sin
    const string CH_T4_04 = "ACC_CH_T4_04"; // Dash & chạy nhanh không làm gián đoạn hồi Stamina tự nhiên

    // ── Batch 14 ──
    const string MS_T5_03 = "ACC_MS_T5_03"; // Signature x1.5 dmg, sau đó -50% SinGain 10s
    const string MS_T5_02 = "ACC_MS_T5_02"; // Chỉ nhận hồi máu từ Hút máu; Hút máu x3 khi HP<50%

    // ── Batch 15 (companion buff + player nerf) ──
    const string PA_T5_08 = "ACC_PA_T5_08"; // Companion +200% dmg + bất tử; player đánh thường 0 dmg + -50% mọi nguồn khác

    // ── Batch 16 (companion bổ sung) ──
    const string RM_T5_07 = "ACC_RM_T5_07"; // Player & Companion +15% maxHp; player skill→companion +15% dmgOut 5s; (companion skill→player atkSpd: chưa có event)
    const string MS_T4_03 = "ACC_MS_T4_03"; // Companion bị hạ → hồi sinh 50% maxHp; player -50% máu hiện tại
    const string CH_T5_06 = "ACC_CH_T5_06"; // Companion: maxSin = 500 + hồi 1 Sin/s khi giao tranh (giờ companion ĐÃ có hệ Sin)

    // ── Batch 17 (4 effect cuối) ──
    const string RM_T5_04 = "ACC_RM_T5_04"; // Signature → vùng 5f/5s đếm địch chết → bắn projectile 200% magicAtk
    const string MS_T5_06 = "ACC_MS_T5_06"; // Choáng 1 mục tiêu liên tục 2s → Hóa Quỷ +100% dmg 5s, sau đó tự choáng 2s (CD10s)
    const string CH_T4_03 = "ACC_CH_T4_03"; // -30% CD & phí skill; nhưng gồng 1s trước khi thi triển, mất máu lúc gồng → mất trắng
    // CH_T5_06: companion KHÔNG dùng Sin (CompanionEquipmentManager:179) → SKIP cho tới khi companion có kỹ năng dùng Sin.

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

    // ── Batch 2 state ──
    // CH_T3_02: điểm Gan dạ (mốc thời gian tạo, hết hạn 30s) + bể tích máu mất.
    private readonly List<float> ch_t3_02_points = new List<float>();
    private float ch_t3_02_accum = 0f;
    // MS_T4_05: hồi máu khi hạ địch + phạt nếu 10s không hạ ai.
    private bool ms_t4_05_armed = false;
    private float ms_t4_05_lastKillTime = -999f;
    // MS_T5_01: bất tử khi chí mạng.
    private bool ms_t5_01_active = false;
    private float ms_t5_01_nextReady = -999f;
    // PA_T5_06: khiên bất tử khi đứng yên 3s.
    private bool pa_t5_06_armed = false;
    private float pa_t5_06_standTimer = 0f;
    private float pa_t5_06_armorPenalty = 0f;    // lượng Giáp/MR đang bị trừ (để hồi khi Rescan)
    private float pa_t5_06_mrPenalty = 0f;
    // Batch 4: incoming-damage modifier.
    private float ms_t4_02_vulnUntil = -999f;    // MS_T4_02: cửa sổ nhận thêm 20% dmg
    private bool  ch_t4_05_dmgApplied = false;   // CH_T4_05: +20% damageOutputMultiplier khi Sin đầy
    private bool  pa_t5_03_applied = false;       // PA_T5_03: miễn CC (superArmorLevel +99)

    // Batch 5: movement gate / dash / standing.
    private bool  pa_t5_02_applied = false;       // -30% bonusMoveSpeed
    private bool  pa_t5_04_applied = false;       // gate dash/sprint
    private float pa_t5_02_standTimer = 0f;
    private bool  ms_t4_04_speedApplied = false;  // +50% moveSpeed khi HP<30%
    private float ms_t4_04_drainUntil = -999f;
    private bool  ms_t4_04_draining = false;
    private bool  ms_t4_04_invisSet = false;       // ta đang bật isInvisible (để gỡ khi Rescan)

    // Batch 6: companion binding.
    private AllyStats companionStats;
    private bool companionSubscribed = false;
    private bool companionBuffApplied = false;     // RM_T4_05 đang áp lên companion
    private bool companionPaT508Applied = false;   // PA_T5_08 đang áp lên companion (+200% dmg + bất tử)
    private bool pa_t5_08_applied = false;          // PA_T5_08 đã áp nerf -50% damageOutputMultiplier lên player
    private bool rm_t5_07_playerApplied = false;    // RM_T5_07 +15% maxHp player
    private bool companionRmT507Applied = false;    // RM_T5_07 +15% maxHp companion
    private CompanionSkillController _companionCtrl; // cache controller companion (CH_T5_06)
    private bool companionChT506Applied = false;    // CH_T5_06 đã set maxSinOverride

    // Batch 17: MS_T5_06 theo dõi choáng liên tục.
    private Stats ms_t5_06_target;
    private float ms_t5_06_stunStart = 0f;
    private bool  ms_t5_06_active = false;
    private float ms_t5_06_nextReady = -999f;

    // Batch 7: conditional/passive.
    private bool  rm_t5_05_ccApplied = false;       // miễn CC khi HP>70%
    private int   ms_t5_08_stacks = 0;
    private float ms_t5_08_combatTimer = 0f;
    private float ms_t5_08_outCombatTimer = 0f;
    private bool  pa_t5_07_applied = false;         // crit 100%

    // Batch 8: stamina-consume hook.
    private float rm_t5_02_nextRefill = -999f;

    // Batch 10: phí Sin Signature (CH_T5_08 leo thang).
    private float ch_t5_08_mult = 1f;
    private float ch_t5_08_lastSigTime = -999f;

    // Batch 12: kill-source token (1 lần/lần dùng).
    private bool rm_t3_06_canGrant = false;
    private bool ch_t5_01_canGrant = false;

    // Batch 13: CH_T5_03 CDR realtime theo atkSpeed.
    private float ch_t5_03_added = 0f;

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
            playerController.OnKillEnemy      += HandleKillEnemy;
            playerController.OnDashPerformed  += HandleDash;
            playerController.OnSkillKillEnemy += HandleSkillKill;
        }
        if (stats != null)
        {
            stats.OnPerfectDodgeTriggered += HandlePerfectDodge;
            stats.OnDamageTakenHp         += HandleDamageTakenHp;
            stats.OnShieldBroken          += HandleShieldBroken;
            stats.OnHealDetailed          += HandleHealDetailed;
            stats.OnBeforeTakeDamage      += HandleBeforeTakeDamage;
        }
        CompanionSkillController.OnCompanionSkillUsed += HandleCompanionSkillUsed; // RM_T5_07
    }

    private void Unsubscribe()
    {
        if (equipmentManager != null) equipmentManager.OnEquipmentChanged -= Rescan;
        if (playerController != null)
        {
            playerController.OnHitEnemy       -= HandleHitEnemy;
            playerController.OnDamageDealt    -= HandleDamageDealt;
            playerController.OnAttackPerformed -= HandleAttackPerformed;
            playerController.OnKillEnemy      -= HandleKillEnemy;
            playerController.OnDashPerformed  -= HandleDash;
            playerController.OnSkillKillEnemy -= HandleSkillKill;
        }
        if (stats != null)
        {
            stats.OnPerfectDodgeTriggered -= HandlePerfectDodge;
            stats.OnDamageTakenHp         -= HandleDamageTakenHp;
            stats.OnShieldBroken          -= HandleShieldBroken;
            stats.OnHealDetailed          -= HandleHealDetailed;
            stats.OnBeforeTakeDamage      -= HandleBeforeTakeDamage;
        }
        CompanionSkillController.OnCompanionSkillUsed -= HandleCompanionSkillUsed;
    }

    /// <summary>RM_T5_07: Companion dùng Skill → Player +15% bonusAttackSpeed 5s.</summary>
    private void HandleCompanionSkillUsed()
    {
        if (!Has(RM_T5_07) || stats == null) return;
        AddTimedMod("RM_T5_07_PLAYER_ATKSPD", 5f,
            apply:  () => { stats.bonusAttackSpeed += 0.15f; stats.CalculateCombatStatsOnly(); },
            revert: () => { stats.bonusAttackSpeed -= 0.15f; stats.CalculateCombatStatsOnly(); });
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
        // PA_T5_03: miễn nhiễm khống chế (nâng superArmorLevel). ("không bị làm chậm" hiện chưa có nguồn slow lên player → no-op.)
        if (Has(PA_T5_03) && !pa_t5_03_applied)
        {
            stats.isSuperArmor = true;
            stats.superArmorLevel += 99;
            pa_t5_03_applied = true;
        }
        // PA_T5_02: -30% bonusMoveSpeed.
        if (Has(PA_T5_02) && !pa_t5_02_applied)
        {
            stats.bonusMoveSpeed -= 0.30f;
            stats.CalculateMoveSpeedOnly();
            pa_t5_02_applied = true;
        }
        // PA_T5_04: cấm Dash & chạy nhanh (PlayerController đọc cờ này).
        if (Has(PA_T5_04) && !pa_t5_04_applied)
        {
            stats.accBlockDashSprint = true;
            pa_t5_04_applied = true;
        }
        // PA_T5_07: Crit luôn 100% (cộng đủ để critChance >= 1). Mặt hại UI (vision/HP bar) CHƯA làm.
        if (Has(PA_T5_07) && !pa_t5_07_applied)
        {
            stats.bonusCritChance += 1.0f;
            stats.CalculateCombatStatsOnly();
            pa_t5_07_applied = true;
        }

        // Batch 8: stamina-consume hook (cờ trên AllyStats, TryConsumeStamina đọc).
        stats.accStaminaConsumeMult = Has(RM_T5_02) ? 0.5f : 1f;
        stats.accStaminaFreeWhileHighHp = Has(CH_T5_04);
        stats.accStaminaToSinGain = Has(CH_T5_05) ? 0.3f : 0f;

        // Batch 9 + MS_T4_06: resource-payment override.
        // Sin: MS_T4_06 (20HP/Sin) ghi đè CH_T5_02 (10HP/Sin). Stamina: chỉ CH_T5_02 (10HP/Stamina).
        stats.accHpPerSinOverride = Has(MS_T4_06) ? 20f : (Has(CH_T5_02) ? 10f : 0f);
        stats.accHpPerStaminaOverride = Has(CH_T5_02) ? 10f : 0f;
        // MS_T4_06: Signature dùng máu được +30% sát thương.
        stats.accBloodSignatureDmgBonus = Has(MS_T4_06) ? 0.30f : 0f;

        // Batch 10: phí Sin Signature (baseline; CH_T5_08 leo thang xử lý trong Update).
        stats.accSignatureSinCostMult = Has(PA_T5_05) ? 0f : (Has(CH_T3_03) ? 0.9f : 1f);

        // Batch 13: CH_T4_04 — dash/chạy nhanh không gián đoạn hồi Stamina.
        stats.accDashSprintNoRegenInterrupt = Has(CH_T4_04);

        // Batch 14:
        stats.accSignatureDmgBonusAlways = Has(MS_T5_03) ? 0.50f : 0f; // MS_T5_03: Signature x1.5 dmg
        stats.accOnlyLifestealHeal = Has(MS_T5_02);                     // MS_T5_02: chỉ nhận hút máu
        stats.accLifestealTripleLowHp = Has(MS_T5_02);                  // MS_T5_02: hút máu x3 khi HP<50%

        // Batch 15: PA_T5_08 — player giảm 50% sát thương mọi nguồn (đánh thường = 0 xử lý ở ModifyBasicAttack).
        if (Has(PA_T5_08) && !pa_t5_08_applied)
        {
            stats.damageOutputMultiplier -= 0.50f;
            stats.CalculateCombatStatsOnly();
            pa_t5_08_applied = true;
        }

        // Batch 16: RM_T5_07 — player +15% maxHp (companion phần riêng ở BindCompanion).
        if (Has(RM_T5_07) && !rm_t5_07_playerApplied)
        {
            stats.bonusHp += 0.15f;
            rm_t5_07_playerApplied = true;
            stats.RecalculateStats();
        }

        // Batch 17: CH_T4_03 — gồng 1s + giảm 30% CD & phí skill.
        stats.accSkillChargeTime = Has(CH_T4_03) ? 1.0f : 0f;
        stats.accSkillCdrBonus   = Has(CH_T4_03) ? 0.30f : 0f;
        stats.accSkillSinCostMult = Has(CH_T4_03) ? 0.70f : 1f;
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

        // 6. Batch 2: dừng coroutine + khôi phục trạng thái tạm.
        StopAllCoroutines();
        if (ms_t5_01_active) { stats.isInvincible = false; ms_t5_01_active = false; }
        if (pa_t5_06_armorPenalty != 0f || pa_t5_06_mrPenalty != 0f)
        {
            stats.armor       += pa_t5_06_armorPenalty;
            stats.magicResist += pa_t5_06_mrPenalty;
            pa_t5_06_armorPenalty = 0f; pa_t5_06_mrPenalty = 0f;
        }
        ch_t3_02_points.Clear(); ch_t3_02_accum = 0f;
        ms_t4_05_armed = false;
        pa_t5_06_armed = false; pa_t5_06_standTimer = 0f;

        // Batch 4
        if (pa_t5_03_applied) { stats.superArmorLevel -= 99; pa_t5_03_applied = false; }
        if (ch_t4_05_dmgApplied) { stats.damageOutputMultiplier -= 0.20f; ch_t4_05_dmgApplied = false; }
        ms_t4_02_vulnUntil = -999f;

        // Batch 5
        if (pa_t5_02_applied) { stats.bonusMoveSpeed += 0.30f; pa_t5_02_applied = false; stats.CalculateMoveSpeedOnly(); }
        if (pa_t5_04_applied) { stats.accBlockDashSprint = false; pa_t5_04_applied = false; }
        if (ms_t4_04_speedApplied) { stats.bonusMoveSpeed -= 0.50f; ms_t4_04_speedApplied = false; stats.CalculateMoveSpeedOnly(); }
        if (ms_t4_04_invisSet) { stats.isInvisible = false; ms_t4_04_invisSet = false; }
        ms_t4_04_draining = false; ms_t4_04_drainUntil = -999f;
        pa_t5_02_standTimer = 0f;

        // Batch 6: gỡ buff + unsub companion (Update sẽ tự bind lại nếu còn effect).
        UnbindCompanion();

        // Batch 7
        if (rm_t5_05_ccApplied) { stats.superArmorLevel -= 99; rm_t5_05_ccApplied = false; }
        if (pa_t5_07_applied) { stats.bonusCritChance -= 1.0f; pa_t5_07_applied = false; }
        if (ms_t5_08_stacks > 0)
        {
            stats.damageOutputMultiplier -= 0.10f * ms_t5_08_stacks;
            stats.bonusHp += 0.10f * ms_t5_08_stacks;
            ms_t5_08_stacks = 0;
            stats.RecalculateStats();
        }
        ms_t5_08_combatTimer = 0f; ms_t5_08_outCombatTimer = 0f;

        // Batch 8: reset cờ stamina-consume về mặc định.
        stats.accStaminaConsumeMult = 1f;
        stats.accStaminaFreeWhileHighHp = false;
        stats.accStaminaToSinGain = 0f;

        // Batch 9 + MS_T4_06: reset resource-payment override.
        stats.accHpPerSinOverride = 0f;
        stats.accHpPerStaminaOverride = 0f;
        stats.accBloodSignatureDmgBonus = 0f;

        // Batch 10: reset phí Sin Signature.
        stats.accSignatureSinCostMult = 1f;
        ch_t5_08_mult = 1f; ch_t5_08_lastSigTime = -999f;

        // Batch 12: reset token kill-source.
        rm_t3_06_canGrant = false; ch_t5_01_canGrant = false;

        // Batch 13: reset CH_T4_04 + CH_T5_03 CDR.
        stats.accDashSprintNoRegenInterrupt = false;
        // Batch 14: reset MS_T5_03 / MS_T5_02.
        stats.accSignatureDmgBonusAlways = 0f;
        stats.accOnlyLifestealHeal = false;
        stats.accLifestealTripleLowHp = false;

        // Batch 15: reset PA_T5_08 nerf player (phần companion gỡ trong UnbindCompanion).
        if (pa_t5_08_applied) { stats.damageOutputMultiplier += 0.50f; pa_t5_08_applied = false; }
        // Batch 16: reset RM_T5_07 player maxHp.
        if (rm_t5_07_playerApplied) { stats.bonusHp -= 0.15f; rm_t5_07_playerApplied = false; stats.RecalculateStats(); }
        // Batch 17: dọn MS_T5_06 (StopAllCoroutines có thể cắt giữa Hóa Quỷ → gỡ buff).
        if (ms_t5_06_active) { stats.damageOutputMultiplier -= 1.0f; ms_t5_06_active = false; }
        ms_t5_06_target = null;
        // Batch 17: reset CH_T4_03.
        stats.accSkillChargeTime = 0f;
        stats.accSkillCdrBonus = 0f;
        stats.accSkillSinCostMult = 1f;
        if (ch_t5_03_added != 0f)
        {
            stats.bonusCdr -= ch_t5_03_added;
            ch_t5_03_added = 0f;
            stats.cooldownReduction = stats.baseCdr + stats.cdrPerAGI * stats.AGI + stats.bonusCdr;
        }

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

        // MS_T5_06: đòn gây choáng → bắt đầu theo dõi 1 mục tiêu cho mốc "choáng liên tục 2s".
        if (Has(MS_T5_06) && info.isStun && victim != null && victim.currentHp > 0f)
        {
            if (ms_t5_06_target != victim)
            {
                ms_t5_06_target = victim;
                ms_t5_06_stunStart = Time.time;
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

        // CH_T3_02: mỗi 5% maxHP mất → 1 điểm Gan dạ (30s); đủ 3 điểm → nổ 500 true dmg + full Stamina.
        if (Has(CH_T3_02) && stats.maxHp > 0f)
        {
            ch_t3_02_accum += hpLost;
            float per = stats.maxHp * 0.05f;
            while (ch_t3_02_accum >= per)
            {
                ch_t3_02_accum -= per;
                ch_t3_02_points.Add(Time.time);
            }
            while (ch_t3_02_points.Count >= 3)
            {
                ch_t3_02_points.RemoveRange(0, 3);
                CH_T3_02_Explode();
            }
        }
    }

    private void CH_T3_02_Explode()
    {
        RestoreStamina(stats.maxStamina); // hồi toàn bộ Stamina
        int mask = playerController != null ? playerController.dangerLayer.value : ~0;
        Collider[] hits = Physics.OverlapSphere(transform.position, 3f, mask);
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !seen.Add(e)) continue;
            e.TakeDamage(new DamageInfo { attacker = stats, sourcePosition = transform.position, trueDamage = 500f });
        }
        VisualDebugHelper.DrawSphere(transform.position, 3f, new Color(1f, 0.8f, 0.2f, 0.5f), 0.4f);
        Debug.Log("<color=orange>[ACC_CH_T3_02]</color> Gan dạ phát nổ! 500 true dmg + full Stamina.");
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

        // MS_T4_02: +100% bonusCritChance trong 3s, đổi lại nhận thêm 20% sát thương trong 3s.
        if (Has(MS_T4_02))
        {
            ms_t4_02_vulnUntil = Time.time + 3f;
            AddTimedMod(MS_T4_02, 3f,
                apply:  () => { stats.bonusCritChance += 1.0f; stats.CalculateCombatStatsOnly(); },
                revert: () => { stats.bonusCritChance -= 1.0f; stats.CalculateCombatStatsOnly(); });
        }
    }

    private void HandleShieldBroken()
    {
        // CH_T5_07: shield vỡ → hồi 100% Stamina
        if (Has(CH_T5_07))
            RestoreStamina(stats.maxStamina);
    }

    /// <summary>Hạ gục kẻ địch (PlayerController.OnKillEnemy).</summary>
    private void HandleKillEnemy(Stats victim, bool isBackstab)
    {
        // MS_T4_05: hồi 20% maxHP; nếu 10s không hạ ai nữa → tự mất 10% máu (Update xử lý phạt).
        if (Has(MS_T4_05))
        {
            stats.Heal(stats.maxHp * 0.20f, true, false, HealSource.Other);
            ms_t4_05_lastKillTime = Time.time;
            ms_t4_05_armed = true;
        }
    }

    /// <summary>Player vừa Dash (PlayerController.OnDashPerformed).</summary>
    private void HandleDash()
    {
        // MS_T4_04: khi HP<30% → Dash giúp tàng hình 2s + bắt đầu 10s tự mất 1% maxHP/s.
        // Chỉ tàng hình nếu còn có thể mất máu (currentHp > 1).
        if (Has(MS_T4_04) && stats.currentHp < stats.maxHp * 0.30f && stats.currentHp > 1f)
        {
            stats.isInvisible = true;
            ms_t4_04_invisSet = true;
            StartCoroutine(MS_T4_04_Stealth(2f));
            ms_t4_04_drainUntil = Time.time + 10f; // refresh cửa sổ drain
            if (!ms_t4_04_draining) StartCoroutine(MS_T4_04_Drain());
        }
    }

    private System.Collections.IEnumerator MS_T4_04_Stealth(float dur)
    {
        yield return new WaitForSeconds(dur);
        stats.isInvisible = false;
        ms_t4_04_invisSet = false;
    }

    private System.Collections.IEnumerator MS_T4_04_Drain()
    {
        ms_t4_04_draining = true;
        while (Time.time < ms_t4_04_drainUntil)
        {
            SpendHealthSafe(stats.maxHp * 0.01f);
            yield return new WaitForSeconds(1f);
        }
        ms_t4_04_draining = false;
    }

    // ── Companion binding (Batch 6) ──────────────────────────────────────────
    private bool AnyCompanionEffect() => Has(RM_T4_05) || Has(CH_T4_06) || Has(MS_T5_07) || Has(PA_T5_08) || Has(RM_T5_07) || Has(MS_T4_03) || Has(CH_T5_06);

    /// <summary>Mỗi frame: bind companion khi cần (companion có thể spawn/đổi/huỷ sau Start), unbind khi không còn effect.</summary>
    private void EnsureCompanionBinding()
    {
        if (AnyCompanionEffect())
        {
            // companionStats == null: chưa bind, hoặc companion vừa bị huỷ (Unity null) → reset cờ rồi bind lại.
            if (companionStats == null)
            {
                companionSubscribed = false;
                companionBuffApplied = false;
                BindCompanion();
            }
        }
        else if (companionStats != null)
        {
            UnbindCompanion();
        }
    }

    private void BindCompanion()
    {
        CompanionAI c = FindFirstObjectByType<CompanionAI>();
        if (c == null) return;
        AllyStats cs = c.GetComponent<AllyStats>();
        if (cs == null) return;

        companionStats = cs;
        _companionCtrl = c.GetComponent<CompanionSkillController>();
        companionStats.OnHitEnemy += HandleCompanionHit;
        companionStats.OnBeforeTakeDamage += HandleCompanionBeforeDamage;
        companionSubscribed = true;

        // CH_T5_06: nâng giới hạn Sin companion lên 500 (regen xử lý trong Update).
        if (Has(CH_T5_06) && _companionCtrl != null)
        {
            _companionCtrl.maxSinOverride = 500f;
            companionChT506Applied = true;
        }

        // RM_T4_05: buff thường trực cho companion.
        if (Has(RM_T4_05))
        {
            companionStats.bonusHp += 0.20f;
            companionStats.damageOutputMultiplier += 0.20f;
            companionStats.RecalculateStats();
            companionBuffApplied = true;
        }

        // PA_T5_08: Companion hóa khổng lồ — +200% sát thương + bất tử.
        if (Has(PA_T5_08))
        {
            companionStats.damageOutputMultiplier += 2.0f;
            companionStats.isInvincible = true;
            companionPaT508Applied = true;
        }

        // RM_T5_07: Companion +15% maxHp.
        if (Has(RM_T5_07))
        {
            companionStats.bonusHp += 0.15f;
            companionStats.RecalculateStats();
            companionRmT507Applied = true;
        }
    }

    private void UnbindCompanion()
    {
        if (companionStats != null)
        {
            if (companionSubscribed)
            {
                companionStats.OnHitEnemy -= HandleCompanionHit;
                companionStats.OnBeforeTakeDamage -= HandleCompanionBeforeDamage;
            }
            if (companionBuffApplied)
            {
                companionStats.bonusHp -= 0.20f;
                companionStats.damageOutputMultiplier -= 0.20f;
                companionStats.RecalculateStats();
            }
            if (companionPaT508Applied)
            {
                companionStats.damageOutputMultiplier -= 2.0f;
                companionStats.isInvincible = false;
            }
            if (companionRmT507Applied)
            {
                companionStats.bonusHp -= 0.15f;
                companionStats.RecalculateStats();
            }
        }
        if (companionChT506Applied && _companionCtrl != null) _companionCtrl.maxSinOverride = 0f;
        companionStats = null;
        _companionCtrl = null;
        companionSubscribed = false;
        companionBuffApplied = false;
        companionPaT508Applied = false;
        companionRmT507Applied = false;
        companionChT506Applied = false;
    }

    /// <summary>Companion đánh trúng địch (companion AllyStats.OnHitEnemy).</summary>
    private void HandleCompanionHit(Stats victim, float t, bool isCrit)
    {
        // CH_T4_06: 50% tỷ lệ hồi 5 Stamina + 3 Sin cho player.
        if (Has(CH_T4_06) && Random.value < 0.5f)
        {
            RestoreStamina(5f);
            RestoreSin(3f);
        }
    }

    /// <summary>Companion sắp nhận sát thương (companion AllyStats.OnBeforeTakeDamage).</summary>
    private void HandleCompanionBeforeDamage(DamageInfo info)
    {
        if (info == null || !Has(MS_T5_07) || companionStats == null) return;

        // Player còn dưới 20% maxHP → Companion chết ngay (đảm bảo đòn này lethal), KHÔNG gánh.
        if (stats.currentHp < stats.maxHp * 0.20f)
        {
            info.trueDamage += companionStats.maxHp;
            return;
        }

        // Gánh 100% sát thương của Companion → Player.
        DamageInfo redirect = new DamageInfo
        {
            physDamage = info.physDamage, magicDamage = info.magicDamage, trueDamage = info.trueDamage,
            attacker = info.attacker, impactLevel = 0, sourcePosition = info.sourcePosition
        };
        info.physDamage = 0f; info.magicDamage = 0f; info.trueDamage = 0f;
        stats.TakeDamage(redirect);
    }

    /// <summary>Nhận hồi máu (Stats.OnHealDetailed) — biết nguồn (Lifesteal/Skill/...).</summary>
    private void HandleHealDetailed(float amount, float excess, HealSource source)
    {
        // CH_T3_05: mỗi 1% maxHP hồi từ HÚT MÁU → +1 Stamina.
        if (Has(CH_T3_05) && source == HealSource.Lifesteal && amount > 0f && stats.maxHp > 0f)
            RestoreStamina((amount / stats.maxHp) * 100f);
    }

    /// <summary>Can thiệp TRƯỚC khi tính sát thương (Stats.OnBeforeTakeDamage). Không chạy khi đang bất tử.</summary>
    private void HandleBeforeTakeDamage(DamageInfo info)
    {
        if (info == null || stats == null || stats.isDead) return;

        // PA_T5_06: khiên bất tử cho 1 đòn (ưu tiên chặn trước MS_T5_01).
        if (Has(PA_T5_06) && pa_t5_06_armed)
        {
            info.physDamage = 0f; info.magicDamage = 0f; info.trueDamage = 0f;
            info.isStun = false; info.isKnockback = false;
            pa_t5_06_armed = false;
            StartCoroutine(PA_T5_06_ArmorPenalty());
            Debug.Log("<color=cyan>[ACC_PA_T5_06]</color> Khiên bất tử chặn 1 đòn!");
            return;
        }

        // ── Incoming-damage modifier (áp TRƯỚC khi check lethal của MS_T5_01) ──
        // CH_T4_05: Stamina đầy → giảm 20% sát thương nhận.
        if (Has(CH_T4_05) && stats.currentStamina >= stats.maxStamina)
        { info.physDamage *= 0.8f; info.magicDamage *= 0.8f; info.trueDamage *= 0.8f; }

        // MS_T4_02: trong 3s sau Perfect Dodge → nhận thêm 20% sát thương từ mọi nguồn.
        if (Has(MS_T4_02) && Time.time < ms_t4_02_vulnUntil)
        { info.physDamage *= 1.2f; info.magicDamage *= 1.2f; info.trueDamage *= 1.2f; }

        // PA_T5_03: nhận thêm 20% Sát thương Chuẩn (True) từ mọi đòn tấn công trúng đích.
        if (Has(PA_T5_03))
            info.trueDamage += info.TotalRawDamage * 0.20f;

        // MS_T5_01: đòn chí mạng → xoá đòn + bất tử 3s, hết thì đặt 20% maxHP (CD 30s).
        if (Has(MS_T5_01) && !ms_t5_01_active && Time.time >= ms_t5_01_nextReady)
        {
            float dmgToHp = info.TotalRawDamage - stats.currentShield;
            if (dmgToHp >= stats.currentHp)
            {
                info.physDamage = 0f; info.magicDamage = 0f; info.trueDamage = 0f;
                info.isStun = false; info.isKnockback = false;
                ms_t5_01_active = true;
                ms_t5_01_nextReady = Time.time + 30f;
                StartCoroutine(MS_T5_01_Immortal());
                Debug.Log("<color=red>[ACC_MS_T5_01]</color> Thoát chết! Bất tử 3s.");
            }
        }
    }

    // MS_T5_06: Hóa Quỷ — +100% damageOutputMultiplier 5s, sau đó tự choáng 2s.
    private System.Collections.IEnumerator MS_T5_06_Demon()
    {
        ms_t5_06_active = true;
        stats.damageOutputMultiplier += 1.0f;
        stats.CalculateCombatStatsOnly();
        Debug.Log("<color=red>[ACC_MS_T5_06]</color> HÓA QUỶ! +100% sát thương 5s.");
        yield return new WaitForSeconds(5f);
        stats.damageOutputMultiplier -= 1.0f;
        stats.CalculateCombatStatsOnly();
        // Tự choáng 2s (đi qua ApplyCrowdControl; có thể bị superArmor chặn).
        stats.TakeDamage(new DamageInfo { isStun = true, stunDuration = 2f, attacker = stats, sourcePosition = transform.position });
        ms_t5_06_active = false;
    }

    // RM_T5_04: vùng tử khí 5f/5s — đếm địch chết trong vùng, sau đó bắn projectile = số đếm (200% magicAtk mỗi cái).
    private System.Collections.IEnumerator RM_T5_04_Zone(Vector3 center)
    {
        int mask = playerController != null ? playerController.dangerLayer.value : ~0;
        HashSet<Stats> tracked = new HashSet<Stats>();
        List<Stats> toCheck = new List<Stats>();
        int deathCount = 0;
        float t = 0f;
        while (t < 5f)
        {
            // Thêm địch còn sống trong vùng vào danh sách theo dõi.
            foreach (var h in Physics.OverlapSphere(center, 5f, mask))
            {
                Stats e = h.GetComponentInParent<Stats>();
                if (e != null && e.currentHp > 0 && e.CompareTag("Enemy")) tracked.Add(e);
            }
            // Phát hiện địch đã theo dõi nay chết/biến mất → +1 Chết chóc.
            toCheck.Clear(); toCheck.AddRange(tracked);
            foreach (var e in toCheck)
            {
                if (e == null || e.currentHp <= 0) { deathCount++; tracked.Remove(e); }
            }
            VisualDebugHelper.DrawSphere(center, 5f, new Color(0.4f, 0f, 0.1f, 0.25f), 0.25f);
            t += 0.25f;
            yield return new WaitForSeconds(0.25f);
        }

        if (deathCount <= 0) yield break;

        // Bắn deathCount projectile, chia đều lên địch gần (ưu tiên gần hơn) — mỗi cái 200% magicAtk.
        List<Stats> targets = new List<Stats>();
        foreach (var h in Physics.OverlapSphere(center, 10f, mask))
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e != null && e.currentHp > 0 && e.CompareTag("Enemy") && !targets.Contains(e)) targets.Add(e);
        }
        if (targets.Count == 0) yield break;
        targets.Sort((a, b) => Vector3.SqrMagnitude(a.transform.position - center).CompareTo(Vector3.SqrMagnitude(b.transform.position - center)));
        for (int i = 0; i < deathCount; i++)
        {
            Stats tgt = targets[i % targets.Count]; // round-robin, gần nhất nhận trước
            if (tgt != null && tgt.currentHp > 0)
                DamageHelper.ApplyQuickProcDamage(stats, tgt, 0f, 2.0f, transform);
        }
        Debug.Log($"<color=magenta>[ACC_RM_T5_04]</color> Vùng tử khí: {deathCount} Chết chóc → bắn {deathCount} đòn 200% magicAtk.");
    }

    private System.Collections.IEnumerator MS_T5_01_Immortal()
    {
        stats.isInvincible = true;
        yield return new WaitForSeconds(3f);
        stats.isInvincible = false;
        stats.currentHp = stats.maxHp * 0.20f;
        ms_t5_01_active = false;
    }

    private System.Collections.IEnumerator PA_T5_06_ArmorPenalty()
    {
        pa_t5_06_armorPenalty = stats.armor * 0.5f;
        pa_t5_06_mrPenalty    = stats.magicResist * 0.5f;
        stats.armor       -= pa_t5_06_armorPenalty;
        stats.magicResist -= pa_t5_06_mrPenalty;
        yield return new WaitForSeconds(3f);
        stats.armor       += pa_t5_06_armorPenalty;
        stats.magicResist += pa_t5_06_mrPenalty;
        pa_t5_06_armorPenalty = 0f; pa_t5_06_mrPenalty = 0f;
    }

    /// <summary>
    /// PlayerController gọi trong ApplyDamageToTarget (đòn đánh vũ khí thường/heavy) để accessory chỉnh:
    /// hệ số sát thương, ép crit, +crit mult, xuyên giáp; đồng thời xử lý side-effect (MS_T5_05 backfire).
    /// </summary>
    public void ModifyBasicAttack(Stats target, bool isHeavy, float dirFactor,
        ref float attackMultiplier, ref bool isCrit, ref float armorPenetration, ref float critMultiplierBonus)
    {
        if (stats == null) return;

        // RM_T4_02: đánh từ phía sau (t=1) → chắc chắn crit + +20% bonusCritMultiplier cho đòn này.
        if (Has(RM_T4_02) && dirFactor >= 0.999f)
        {
            isCrit = true;
            critMultiplierBonus += 0.20f;
        }

        // Các effect chỉ cho ĐÒN ĐÁNH THƯỜNG (không áp Heavy).
        if (!isHeavy)
        {
            // PA_T5_08: đánh thường gây 0 sát thương (player thành support).
            if (Has(PA_T5_08)) { attackMultiplier = 0f; return; }

            // RM_T3_05: +15% dmg với địch còn trên 50% máu.
            if (Has(RM_T3_05) && target != null && target.maxHp > 0f && target.currentHp > target.maxHp * 0.5f)
                attackMultiplier *= 1.15f;

            // MS_T5_05: 5% Jackpot x5 dmg; 5% backfire tự nhận 10% maxHP (chặn ở 1 HP).
            if (Has(MS_T5_05))
            {
                if (Random.value < 0.05f) { attackMultiplier *= 5f; Debug.Log("<color=yellow>[ACC_MS_T5_05]</color> JACKPOT x5!"); }
                if (Random.value < 0.05f) { SpendHealthSafe(stats.maxHp * 0.10f); Debug.Log("<color=red>[ACC_MS_T5_05]</color> Backfire -10% maxHP!"); }
            }

            // RM_T5_03: đánh thường xuyên 25% giáp; nếu crit → 50%.
            if (Has(RM_T5_03))
                armorPenetration = Mathf.Max(armorPenetration, isCrit ? 0.50f : 0.25f);
        }
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

            // RM_T5_08: dùng Skill E → giảm 3s CD Signature.
            if (Has(RM_T5_08) && skillManager != null && skillManager.currentSignature != null)
                skillManager.ReduceSkillCooldown(skillManager.currentSignature, 3f);

            // RM_T3_06: mở token cho 1 lần hồi Sin nếu Skill E hạ địch.
            if (Has(RM_T3_06)) rm_t3_06_canGrant = true;

            // RM_T5_07: player dùng Skill → Companion +15% damageOutputMultiplier trong 5s.
            if (Has(RM_T5_07) && companionStats != null)
            {
                AllyStats comp = companionStats;
                AddTimedMod("RM_T5_07_COMP_DMG", 5f,
                    apply:  () => { comp.damageOutputMultiplier += 0.15f; },
                    revert: () => { if (comp != null) comp.damageOutputMultiplier -= 0.15f; });
            }
        }
        else if (skillType == SkillData.SkillType.Signature)
        {
            // RM_T5_08: dùng Signature → giảm 3s CD Skill E.
            if (Has(RM_T5_08) && skillManager != null && skillManager.currentSkill != null)
                skillManager.ReduceSkillCooldown(skillManager.currentSkill, 3f);

            // CH_T5_08: xóa CD Signature (cho dùng lại ngay) + nhân đôi phí Sin cho lần kế (trong 5s).
            if (Has(CH_T5_08) && skillManager != null && skillManager.currentSignature != null)
            {
                skillManager.ReduceSkillCooldown(skillManager.currentSignature, 99999f);
                ch_t5_08_lastSigTime = Time.time;
                ch_t5_08_mult *= 2f;
            }

            // PA_T5_05: dùng Signature → tiêu 10% maxHp (chặn ở 1 HP).
            if (Has(PA_T5_05)) SpendHealthSafe(stats.maxHp * 0.10f);

            // MS_T4_06: nếu Signature vừa rồi DÙNG MÁU → giảm 50% SinGain trong 5s.
            if (Has(MS_T4_06) && stats.lastSignatureBloodPaid)
                AddTimedMod("MS_T4_06_SINGAIN", 5f,
                    apply:  () => { stats.bonusSinGain -= 0.50f; stats.RecalculateStats(); },
                    revert: () => { stats.bonusSinGain += 0.50f; stats.RecalculateStats(); });

            // CH_T5_01: mở token cho 1 lần hoàn CD/Sin nếu Signature hạ địch.
            if (Has(CH_T5_01)) ch_t5_01_canGrant = true;

            // CH_T5_03: dùng Signature → +15 Sin.
            if (Has(CH_T5_03)) RestoreSin(15f);

            // MS_T5_03: sau khi dùng Signature → -50% SinGain trong 10s. (x1.5 dmg đã áp ở SkillBehavior.)
            if (Has(MS_T5_03))
                AddTimedMod("MS_T5_03_SINGAIN", 10f,
                    apply:  () => { stats.bonusSinGain -= 0.50f; stats.RecalculateStats(); },
                    revert: () => { stats.bonusSinGain += 0.50f; stats.RecalculateStats(); });

            // RM_T5_04: tạo vùng tử khí (đếm địch chết) tại vị trí player lúc dùng Signature.
            if (Has(RM_T5_04)) StartCoroutine(RM_T5_04_Zone(transform.position));
        }
    }

    /// <summary>Kỹ năng (có SkillData) hạ gục kẻ địch — attribute theo loại skill (PlayerController.OnSkillKillEnemy).</summary>
    private void HandleSkillKill(SkillData skill)
    {
        if (skill == null) return;

        // RM_T3_06: hạ địch bằng Skill E → +20 Sin (1 lần/lần dùng E).
        if (skill.skillType == SkillData.SkillType.Skill && Has(RM_T3_06) && rm_t3_06_canGrant)
        {
            rm_t3_06_canGrant = false;
            RestoreSin(20f);
            Debug.Log("<color=cyan>[ACC_RM_T3_06]</color> Hạ địch bằng Skill E → +20 Sin.");
        }

        // CH_T5_01: Signature hạ địch → hoàn 30% CD + 20% Sin của Signature đó (1 lần/lần dùng).
        if (skill.skillType == SkillData.SkillType.Signature && Has(CH_T5_01) && ch_t5_01_canGrant)
        {
            ch_t5_01_canGrant = false;
            if (skillManager != null) skillManager.ReduceSkillCooldown(skill, skill.cooldown * 0.30f);
            RestoreSin(skill.sinChargeReq * 0.20f);
            Debug.Log("<color=cyan>[ACC_CH_T5_01]</color> Signature hạ địch → hoàn 30% CD + 20% Sin.");
        }
    }

    // ── UPDATE: timed buff + conditional + frame-based ──────────────────────
    private void Update()
    {
        if (stats == null || stats.isDead) return;
        float now = Time.time;

        EnsureCompanionBinding();

        // CH_T5_06: companion hồi 1 Sin/s khi đang giao tranh.
        if (Has(CH_T5_06) && companionStats != null && !companionStats.outCombat)
            companionStats.currentSin = Mathf.Min(companionStats.maxSin, companionStats.currentSin + Time.deltaTime);

        // MS_T4_03: Companion bị hạ → hồi sinh 50% maxHp; player mất 50% máu hiện tại.
        if (Has(MS_T4_03) && companionStats != null && companionStats.isDead)
        {
            companionStats.Revive(0.5f);
            var cai = companionStats.GetComponent<CompanionAI>();
            if (cai != null) cai.enabled = true; // Die đã tắt CompanionAI → bật lại
            stats.currentHp = Mathf.Max(1f, stats.currentHp * 0.5f);
            Debug.Log("<color=cyan>[ACC_MS_T4_03]</color> Hồi sinh Companion (50% HP); player -50% máu hiện tại.");
        }

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

        // 7. CH_T3_02: hết hạn điểm Gan dạ (30s/điểm).
        if (Has(CH_T3_02) && ch_t3_02_points.Count > 0)
            ch_t3_02_points.RemoveAll(t => now - t > 30f);

        // 8. MS_T4_05: 10s không hạ ai sau khi từng hạ → tự mất 10% máu (chặn ở 1 HP).
        if (Has(MS_T4_05) && ms_t4_05_armed && now - ms_t4_05_lastKillTime >= 10f)
        {
            ms_t4_05_armed = false;
            SpendHealthSafe(stats.maxHp * 0.10f);
            Debug.Log("<color=red>[ACC_MS_T4_05]</color> 10s không hạ ai → mất 10% máu.");
        }

        // 8b. CH_T4_05: Sin đầy → +20% damageOutputMultiplier (phần DR khi Stamina đầy xử lý ở OnBeforeTakeDamage).
        if (Has(CH_T4_05))
        {
            bool sinFull = stats.maxSin > 0f && stats.currentSin >= stats.maxSin;
            if (sinFull && !ch_t4_05_dmgApplied)  { stats.damageOutputMultiplier += 0.20f; ch_t4_05_dmgApplied = true; }
            if (!sinFull && ch_t4_05_dmgApplied)  { stats.damageOutputMultiplier -= 0.20f; ch_t4_05_dmgApplied = false; }
        }

        // 8c. PA_T5_04: hồi 2% maxHP/giây.
        if (Has(PA_T5_04))
            stats.Heal(stats.maxHp * 0.02f * Time.deltaTime, false, false, HealSource.Regen);

        // 8d. PA_T5_02: đứng yên >1s → tua nhanh hồi chiêu x2 (giảm thêm 1x deltaTime mỗi frame) tới khi di chuyển.
        if (Has(PA_T5_02) && skillManager != null)
        {
            bool standing = playerController != null && !playerController.isWalking && !playerController.isDashing;
            if (standing)
            {
                pa_t5_02_standTimer += Time.deltaTime;
                if (pa_t5_02_standTimer > 1f) skillManager.ReduceAllCooldowns(Time.deltaTime);
            }
            else pa_t5_02_standTimer = 0f;
        }

        // 8e. MS_T4_04: HP<30% → +50% bonusMoveSpeed (bật/tắt theo máu).
        if (Has(MS_T4_04))
        {
            bool low = stats.currentHp < stats.maxHp * 0.30f;
            if (low && !ms_t4_04_speedApplied)  { stats.bonusMoveSpeed += 0.50f; ms_t4_04_speedApplied = true; stats.CalculateMoveSpeedOnly(); }
            if (!low && ms_t4_04_speedApplied)  { stats.bonusMoveSpeed -= 0.50f; ms_t4_04_speedApplied = false; stats.CalculateMoveSpeedOnly(); }
        }

        // 8f. RM_T5_05: HP>70% → miễn nhiễm CC + hồi 1% maxHP/s.
        if (Has(RM_T5_05))
        {
            bool high = stats.currentHp > stats.maxHp * 0.70f;
            if (high && !rm_t5_05_ccApplied)  { stats.isSuperArmor = true; stats.superArmorLevel += 99; rm_t5_05_ccApplied = true; }
            if (!high && rm_t5_05_ccApplied)  { stats.superArmorLevel -= 99; rm_t5_05_ccApplied = false; }
            if (high) stats.Heal(stats.maxHp * 0.01f * Time.deltaTime, false, false, HealSource.Regen);
        }

        // 8g. MS_T5_08: mỗi 30s GIAO TRANH → +10% dmg & -10% maxHP (max 9 stack); thoát giao tranh 15s → reset.
        if (Has(MS_T5_08))
        {
            if (!stats.outCombat)
            {
                ms_t5_08_outCombatTimer = 0f;
                ms_t5_08_combatTimer += Time.deltaTime;
                if (ms_t5_08_combatTimer >= 30f && ms_t5_08_stacks < 9)
                {
                    ms_t5_08_combatTimer -= 30f;
                    ms_t5_08_stacks++;
                    stats.damageOutputMultiplier += 0.10f;
                    stats.bonusHp -= 0.10f;
                    stats.RecalculateStats();
                    Debug.Log($"<color=red>[ACC_MS_T5_08]</color> Stack {ms_t5_08_stacks}/9 (+dmg/-maxHP).");
                }
            }
            else
            {
                ms_t5_08_combatTimer = 0f;
                if (ms_t5_08_stacks > 0)
                {
                    ms_t5_08_outCombatTimer += Time.deltaTime;
                    if (ms_t5_08_outCombatTimer >= 15f)
                    {
                        stats.damageOutputMultiplier -= 0.10f * ms_t5_08_stacks;
                        stats.bonusHp += 0.10f * ms_t5_08_stacks;
                        ms_t5_08_stacks = 0;
                        ms_t5_08_outCombatTimer = 0f;
                        stats.RecalculateStats();
                        Debug.Log("<color=gray>[ACC_MS_T5_08]</color> Thoát giao tranh → reset stack.");
                    }
                }
            }
        }

        // 8i. Batch 10: phí Sin Signature từ trang sức (PA_T5_05 ghi đè = 0; CH_T5_08 leo thang; CH_T3_03 -10%).
        if (Has(CH_T3_03) || Has(CH_T5_08) || Has(PA_T5_05))
        {
            if (Has(CH_T5_08) && ch_t5_08_mult != 1f && now - ch_t5_08_lastSigTime > 5f)
                ch_t5_08_mult = 1f; // reset leo thang sau 5s không dùng Signature

            float m;
            if (Has(PA_T5_05)) m = 0f;
            else { m = 1f; if (Has(CH_T3_03)) m *= 0.9f; if (Has(CH_T5_08)) m *= ch_t5_08_mult; }
            stats.accSignatureSinCostMult = m;
        }

        // 8j. CH_T5_03: bonusCDR = 30% bonusAttackSpeed (realtime; chỉ recalc khi atkSpeed đổi đáng kể).
        if (Has(CH_T5_03))
        {
            float desired = 0.30f * stats.bonusAttackSpeed;
            if (Mathf.Abs(desired - ch_t5_03_added) > 0.001f)
            {
                stats.bonusCdr -= ch_t5_03_added;
                ch_t5_03_added = desired;
                stats.bonusCdr += ch_t5_03_added;
                stats.cooldownReduction = stats.baseCdr + stats.cdrPerAGI * stats.AGI + stats.bonusCdr;
            }
        }

        // 8h. RM_T5_02: hết Stamina → hồi 50 ngay (CD 15s). (Phần -50% tiêu hao xử lý ở TryConsumeStamina.)
        if (Has(RM_T5_02) && stats.currentStamina <= 0.01f && now >= rm_t5_02_nextRefill)
        {
            rm_t5_02_nextRefill = now + 15f;
            RestoreStamina(50f);
            Debug.Log("<color=cyan>[ACC_RM_T5_02]</color> Hết Stamina → hồi 50.");
        }

        // 8k. MS_T5_06: choáng 1 mục tiêu LIÊN TỤC 2s → Hóa Quỷ (CD 10s).
        if (Has(MS_T5_06) && ms_t5_06_target != null && !ms_t5_06_active)
        {
            if (ms_t5_06_target.currentHp <= 0 || !ms_t5_06_target.isStunned)
                ms_t5_06_target = null; // hết choáng/chết → reset mốc
            else if (now - ms_t5_06_stunStart >= 2f && now >= ms_t5_06_nextReady)
            {
                ms_t5_06_target = null;
                ms_t5_06_nextReady = now + 10f;
                StartCoroutine(MS_T5_06_Demon());
            }
        }

        // 9. PA_T5_06: đứng yên 3s → vũ trang khiên bất tử (giữ tới khi chặn 1 đòn, kể cả khi đã di chuyển).
        if (Has(PA_T5_06) && !pa_t5_06_armed)
        {
            bool standing = playerController != null && !playerController.isWalking && !playerController.isDashing;
            if (standing)
            {
                pa_t5_06_standTimer += Time.deltaTime;
                if (pa_t5_06_standTimer >= 3f)
                {
                    pa_t5_06_armed = true;
                    pa_t5_06_standTimer = 0f;
                    VisualDebugHelper.DrawSphere(transform.position + Vector3.up, 1f, new Color(0.3f, 0.6f, 1f, 0.5f), 0.6f);
                    Debug.Log("<color=cyan>[ACC_PA_T5_06]</color> Khiên bất tử sẵn sàng.");
                }
            }
            else pa_t5_06_standTimer = 0f;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GHI CHÚ — PHẦN CHƯA HOÀN TẤT (62/62):
//  - PA_T5_07 phần MẶT HẠI UI (vision tối 4 góc + ẩn thanh máu địch): cần HUD — chưa làm (phần crit 100% đã xong).
//  [ĐÃ XONG Batch 2]: RM_T5_08, CH_T3_02, CH_T3_05 (lifesteal qua OnHealDetailed), MS_T4_05, MS_T5_01, PA_T5_06.
//  [ĐÃ XONG Batch 3]: RM_T3_05, RM_T4_02, RM_T5_03, MS_T5_05 (qua CombatMath.armorPenetration + PlayerController.ModifyBasicAttack).
//  [ĐÃ XONG Batch 4]: MS_T4_02, PA_T5_03 (slow-immunity no-op), CH_T4_05 (qua OnBeforeTakeDamage + Update).
//  [ĐÃ XONG Batch 5]: MS_T4_04, PA_T5_02, PA_T5_04 (gate Dash/Sprint qua AllyStats.accBlockDashSprint; tàng hình dùng stats.isInvisible).
//  [ĐÃ XONG Batch 6]: RM_T4_05, CH_T4_06, MS_T5_07 (bind companion động: OnHitEnemy/OnBeforeTakeDamage của companion + buff stats).
//  [ĐÃ XONG Batch 7]: RM_T5_05, MS_T5_08, PA_T5_07 (crit-only) — qua Update + ApplyPersistent.
//  [ĐÃ XONG Batch 8]: RM_T5_02, CH_T5_04, CH_T5_05 — qua Stats.TryConsumeStamina + cờ AllyStats (accStaminaConsumeMult/accStaminaFreeWhileHighHp/accStaminaToSinGain).
//  [ĐÃ XONG Batch 9]: CH_T5_02 — trả Máu cho Sin (SkillBehavior.Use) & Stamina (TryConsumeStamina) thiếu; giữ tối thiểu 1 HP.
//  [ĐÃ XONG Batch 10]: CH_T3_03, CH_T5_08, PA_T5_05 — qua AllyStats.accSignatureSinCostMult (nhân với signatureSinCostMult của weapon) + xóa CD/HP-cost ở TriggerSkillCastEffects.
//    Lưu ý: KHÔNG đổi field maxSin thật (giữ clamp/UI bar); chỉ chỉnh phí Sin của Signature.
//  [ĐÃ XONG Batch 11]: MS_T4_06 — accHpPerSinOverride=20 (ghi đè CH_T5_02), accBloodSignatureDmgBonus, lastSignatureBloodPaid; -50% SinGain 5s khi blood-paid.
//  [ĐÃ XONG Batch 12]: RM_T3_06, CH_T5_01 — DamageHelper bắn PlayerController.OnSkillKillEnemy(SkillData) khi địch chết bởi đòn có SkillData.
//    LƯU Ý: chỉ áp cho skill gây damage QUA DamageHelper.ApplyStandardDamage VỚI tham số skill!=null. Skill dùng MagicProxy/TakeDamage trực tiếp (skill=null) sẽ KHÔNG attribute.
//  [ĐÃ XONG Batch 13]: CH_T5_03 (bonusCdr realtime theo bonusAttackSpeed + 15 Sin/Signature), CH_T4_04 (AllyStats.accDashSprintNoRegenInterrupt + isMovement ở TryConsumeStamina).
//  [ĐÃ XONG Batch 14]: MS_T5_03 (accSignatureDmgBonusAlways ở SkillBehavior + -50% SinGain 10s), MS_T5_02 (Stats.Heal lọc theo HealSource: chỉ Lifesteal; x3 khi HP<50%).
//  [ĐÃ XONG Batch 15]: PA_T5_08 — companion +200% dmg + isInvincible (qua bind); player đánh thường=0 (ModifyBasicAttack) + -50% damageOutputMultiplier.
//  [ĐÃ XONG Batch 16]: RM_T5_07 (player+companion +15% maxHp; player skill→companion +15% dmgOut 5s), MS_T4_03 (Stats.Revive companion 50% + player -50% currentHp).
//  [ĐÃ XONG Batch 17]: RM_T5_04 (vùng tử khí poll death → bắn proc), MS_T5_06 (theo dõi stun liên tục 2s → Hóa Quỷ), CH_T4_03 (ChargeThenCast ở SkillManager + accSkillChargeTime/CdrBonus/SinCostMult).
// ─────────────────────────────────────────────────────────────────────────────
