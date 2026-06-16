using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiệu Ứng Slot 1 (Protocol) — Rarity 3+. Theo "Tổng hợp Effect Protocol.docx".
/// Lắng nghe Companion ĐÁNH TRÚNG (OnHitEnemy) + bộ đếm/timer cho các điều kiện đặc biệt.
///
/// GHI CHÚ XẤP XỈ (game chưa có hệ thống tương ứng):
///  • PRT_ART_T4_02 (+50% khi địch &lt;30% máu) & PRT_ART_T5_01 (+20% true khi chí mạng):
///    OnHitEnemy bắn SAU khi damage đã áp → gây thêm 1 phát phụ bằng % physicalAtk (xấp xỉ, không lấy được
///    đúng damage gốc của đòn vừa rồi).
///  • PRT_CAR_T4_02 (hồi 10% sát thương gây ra): không có event "lượng damage Companion gây ra"
///    → hồi 10% physicalAtk mỗi đòn (xấp xỉ).
///  • PRT_SUP_T3_02 (Silence): enemy CHƯA có hệ thống cast skill để cấm → đặt cờ enemy.isSilenced +
///    visual vùng; hiện mang tính chuẩn bị (cosmetic) cho tới khi enemy có skill.
/// </summary>
[DisallowMultipleComponent]
public class ProtocolEffectManager : CompanionEffectManagerBase
{
    private CompanionProtocolData _module;

    // bộ đếm / trạng thái dùng chung (reset khi đổi module)
    private int _hitCount;
    private int _carAtkSpeedStacks;     // PRT_CAR_T3_02
    private float _aegShieldGuardTime;  // PRT_AEG_T3_01 (no-stack)
    private bool _charged;              // PRT_SUP_T3_02 / AEG_T4_01 / AEG_T5_01 — "đòn kế tiếp"
    private float _chargeTimer;

    private bool _supT5Buff;            // PRT_SUP_T5_01 — 5s sau Signature, mọi đòn stun 1s

    private readonly HashSet<Stats> _noStack = new HashSet<Stats>();              // ART_T3_02 / SUP_T3_01 / AEG_T4_02
    private readonly Dictionary<Stats, int> _stacks = new Dictionary<Stats, int>();      // CAR_T4_01 / SUP_T4_02
    private readonly Dictionary<Stats, float> _maxStackSince = new Dictionary<Stats, float>(); // SUP_T4_02
    private readonly Dictionary<Stats, int> _infection = new Dictionary<Stats, int>();   // CAR_T5_01
    private readonly Dictionary<Stats, float> _infectionTime = new Dictionary<Stats, float>();

    protected override void Awake()
    {
        base.Awake();
    }

    private void OnEnable()
    {
        if (stats != null) stats.OnHitEnemy += HandleHitEnemy;
        CompanionSkillController.OnCompanionSignatureUsed += OnSignature;
    }
    private void OnDisable()
    {
        if (stats != null) stats.OnHitEnemy -= HandleHitEnemy;
        CompanionSkillController.OnCompanionSignatureUsed -= OnSignature;
    }

    public void SetModule(CompanionProtocolData m)
    {
        // gỡ buff cộng dồn còn treo (atk speed) khi đổi module
        if (_carAtkSpeedStacks > 0) { stats.bonusAttackSpeed -= 0.02f * _carAtkSpeedStacks; stats.CalculateCombatStatsOnly(); }

        _module = m;
        _hitCount = 0;
        _carAtkSpeedStacks = 0;
        _charged = false;
        _chargeTimer = 0f;
        _noStack.Clear(); _stacks.Clear(); _maxStackSince.Clear(); _infection.Clear(); _infectionTime.Clear();
    }

    private bool Active => _module != null && _module.HasEffect;

    private void Update()
    {
        if (!Active) return;

        // ── Charge timer cho các effect "Mỗi X giây, đòn kế tiếp..." ──
        float interval = ChargeInterval();
        if (interval > 0f && !_charged)
        {
            _chargeTimer += Time.deltaTime;
            if (_chargeTimer >= interval) { _charged = true; _chargeTimer = 0f; }
        }

        // ── PRT_SUP_T4_02: max stack 5s → stun 2s, reset ──
        if (_module.id == "PRT_SUP_T4_02" && _maxStackSince.Count > 0)
        {
            Stats toStun = null;
            foreach (var kv in _maxStackSince)
                if (kv.Key != null && Time.time - kv.Value >= 5f) { toStun = kv.Key; break; }
            if (toStun != null)
            {
                StunEnemy(toStun, 2f);
                _stacks[toStun] = 0;
                _maxStackSince.Remove(toStun);
            }
        }
    }

    private float ChargeInterval()
    {
        switch (_module.id)
        {
            case "PRT_SUP_T3_02": return 10f;
            case "PRT_AEG_T4_01": return 5f;
            case "PRT_AEG_T5_01": return 10f;
            default: return 0f;
        }
    }

    private void OnSignature()
    {
        if (Active && _module.id == "PRT_SUP_T5_01")
            StartCoroutine(SupT5Routine());
    }
    private IEnumerator SupT5Routine()
    {
        _supT5Buff = true;
        VisualDebugHelper.DrawSphere(transform.position, 1f, Color.yellow, 0.3f);
        yield return new WaitForSeconds(5f);
        _supT5Buff = false;
    }

    private void HandleHitEnemy(Stats target, float dir, bool isCrit)
    {
        if (!Active || target == null || target.currentHp <= 0) return;
        _hitCount++;

        // PRT_SUP_T5_01 buff: mọi đòn trong 5s gây choáng 1s (ngoài effect riêng của module nếu có)
        if (_supT5Buff) StunEnemy(target, 1f);

        float phys = stats.physicalAtk;

        switch (_module.id)
        {
            // ───── ARTILLERY ─────
            case "PRT_ART_T3_01": // 20% Bleed 10% physAtk/s 3s
                if (Random.value < 0.20f) target.ApplyBleed(phys * 0.10f, 3f);
                break;

            case "PRT_ART_T3_02": // -10% Giáp 3s, không cộng dồn
                NoStackArmorMr(target, target.armor * 0.10f, 0f, 3f);
                break;

            case "PRT_ART_T4_01": // đòn thứ 3 → choáng 1.5s
                if (_hitCount % 3 == 0) StunEnemy(target, 1.5f);
                break;

            case "PRT_ART_T4_02": // địch <30% máu → +50% sát thương (xấp xỉ: phát phụ 50%)
                if (target.currentHp < target.maxHp * 0.30f) DealByProtocol(target, 0.50f);
                break;

            case "PRT_ART_T5_01": // chí mạng → +20% true của đòn (xấp xỉ theo physAtk*critMult)
                if (isCrit) DealTrue(target, phys * stats.critMultiplier * 0.20f);
                break;

            // ───── CARNAGE ─────
            case "PRT_CAR_T3_01": // 10% Nhiễu loạn (-10% dmg địch) 3s
                if (Random.value < 0.10f) WeakenEnemyDamage(target, 0.10f, 3f);
                break;

            case "PRT_CAR_T3_02": // +2% atk speed Companion, tối đa 10 stack
                if (_carAtkSpeedStacks < 10)
                {
                    _carAtkSpeedStacks++;
                    stats.bonusAttackSpeed += 0.02f;
                    stats.CalculateCombatStatsOnly();
                }
                break;

            case "PRT_CAR_T4_01": // slow + -10% giáp, stack tối đa 3, 3s
                {
                    int s = GetStack(target);
                    if (s < 3)
                    {
                        SetStack(target, s + 1);
                        SlowEnemy(target, 0.10f, 3f);
                        ReduceArmorMR(target, target.armor * 0.10f, 0f, 3f);
                        StartCoroutine(DecayStackAfter(target, 3f));
                    }
                }
                break;

            case "PRT_CAR_T4_02": // Passive: hồi 10% sát thương gây ra (xấp xỉ 10% physAtk)
                stats.Heal(phys * 0.10f, false, false, HealSource.Drain);
                break;

            case "PRT_CAR_T5_01": // Nhiễm Trùng: stack max 10 → nổ 500% physAtk true; mất hết nếu 5s không đánh tiếp
                HandleInfection(target, phys);
                break;

            // ───── SUPPRESSION ─────
            case "PRT_SUP_T3_01": // -10% Kháng phép 3s, không cộng dồn
                NoStackArmorMr(target, 0f, target.magicResist * 0.10f, 3f);
                break;

            case "PRT_SUP_T3_02": // mỗi 10s → đòn kế tiếp tạo vùng Trầm Mặc r1.5f 3s
                if (_charged) { _charged = false; SpawnSilenceZone(target.transform.position, 1.5f, 3f); }
                break;

            case "PRT_SUP_T4_01": // đòn thứ 3 → hố đen r1f 1s hút địch
                if (_hitCount % 3 == 0) SpawnBlackHole(target.transform.position, 1f, 1f);
                break;

            case "PRT_SUP_T4_02": // slow 10% stack max3; ở max 5s → stun 2s reset
                {
                    int s = GetStack(target);
                    SlowEnemy(target, 0.10f, 5f);
                    if (s < 3)
                    {
                        s++;
                        SetStack(target, s);
                        if (s == 3 && !_maxStackSince.ContainsKey(target)) _maxStackSince[target] = Time.time;
                    }
                }
                break;

            case "PRT_SUP_T5_01": // buff xử lý ở _supT5Buff (đã stun ở trên)
                break;

            // ───── AEGIS ─────
            case "PRT_AEG_T3_01": // đánh trúng → khiên Player 2% maxHp 1s, không cộng dồn
                if (Time.time >= _aegShieldGuardTime && playerStats != null)
                {
                    playerStats.AddShield(playerStats.maxHp * 0.02f, 1f);
                    _aegShieldGuardTime = Time.time + 1f;
                }
                break;

            case "PRT_AEG_T3_02": // đòn thứ 3 → hất tung địch trong tầm 0.5s
                if (_hitCount % 3 == 0)
                {
                    foreach (var e in EnemiesInRadius(transform.position, 2f))
                        e.ApplyEffect(new CombatEffectInfo(CombatEffectType.Airborne, 0.5f) { respectEffectResistance = false }, stats);
                    VisualDebugHelper.DrawSphere(transform.position, 2f, Color.yellow, 0.3f);
                }
                break;

            case "PRT_AEG_T4_01": // mỗi 5s → đòn kế tiếp ép địch đang target Player sang Companion
                if (_charged)
                {
                    _charged = false;
                    // Không đọc được "địch đang target ai" từ EnemyCombat (target private) → taunt toàn bộ quanh (xấp xỉ).
                    foreach (var e in EnemiesInRadius(transform.position, 8f)) TauntToCompanion(e);
                    VisualDebugHelper.DrawSphere(transform.position, 8f, Color.blue, 0.3f);
                }
                break;

            case "PRT_AEG_T4_02": // -15% dmg địch 3s, không cộng dồn
                NoStackWeaken(target, 0.15f, 3f);
                break;

            case "PRT_AEG_T5_01": // mỗi 10s → đòn kế tiếp sóng xung kích r5: dmg + taunt + -30% dmg địch 4s
                if (_charged)
                {
                    _charged = false;
                    foreach (var e in EnemiesInRadius(transform.position, 5f))
                    {
                        DealByProtocol(e, 1.0f);
                        TauntToCompanion(e);
                        WeakenEnemyDamage(e, 0.30f, 4f);
                    }
                    VisualDebugHelper.DrawSphere(transform.position, 5f, Color.yellow, 0.5f);
                }
                break;
        }
    }

    // ── Helpers ──
    private int GetStack(Stats s) { _stacks.TryGetValue(s, out int v); return v; }
    private void SetStack(Stats s, int v) { _stacks[s] = v; }
    private IEnumerator DecayStackAfter(Stats s, float dur)
    {
        yield return new WaitForSeconds(dur);
        if (s != null && _stacks.ContainsKey(s)) _stacks[s] = Mathf.Max(0, _stacks[s] - 1);
    }

    private void NoStackArmorMr(Stats t, float armorAmt, float mrAmt, float dur)
    {
        if (_noStack.Contains(t)) return;
        _noStack.Add(t);
        ReduceArmorMR(t, armorAmt, mrAmt, dur);
        StartCoroutine(ClearNoStack(t, dur));
    }
    private void NoStackWeaken(Stats t, float pct, float dur)
    {
        if (_noStack.Contains(t)) return;
        _noStack.Add(t);
        WeakenEnemyDamage(t, pct, dur);
        StartCoroutine(ClearNoStack(t, dur));
    }
    private IEnumerator ClearNoStack(Stats t, float dur)
    {
        yield return new WaitForSeconds(dur);
        if (t != null) _noStack.Remove(t);
    }

    private void HandleInfection(Stats target, float phys)
    {
        _infection.TryGetValue(target, out int n);
        _infectionTime.TryGetValue(target, out float last);
        if (last > 0f && Time.time - last > 5f) n = 0; // quá 5s không đánh → mất hết
        n++;
        if (n >= 10)
        {
            DealTrue(target, phys * 5.0f);
            VisualDebugHelper.DrawSphere(target.transform.position, 1f, Color.magenta, 0.4f);
            _infection[target] = 0;
            _infectionTime[target] = 0f;
        }
        else
        {
            _infection[target] = n;
            _infectionTime[target] = Time.time;
        }
    }

    private void SpawnSilenceZone(Vector3 center, float radius, float dur)
    {
        // Câm lặng địch trong vùng: mỗi tick refresh Silence ngắn (endTime) → tự hết khi rời vùng,
        // KHÔNG set isSilenced=false trực tiếp (tránh xóa nhầm silence từ nguồn khác).
        SpawnGroundZone(center, radius, dur, 0.25f,
            e => e.ApplyEffect(new CombatEffectInfo(CombatEffectType.Silence, 0.4f), stats),
            new Color(0.5f, 0.5f, 1f, 0.4f));
    }

    private void SpawnBlackHole(Vector3 center, float radius, float dur)
    {
        SpawnGroundZone(center, radius, dur, 0.05f, e =>
        {
            Vector3 next = Vector3.MoveTowards(e.transform.position, center, 2f * Time.deltaTime);
            var agent = e.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(next);
            else e.transform.position = next;
        }, new Color(0.4f, 0f, 0.6f, 0.5f));
    }

}
