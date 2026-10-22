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

    private readonly Dictionary<Stats, int> _stacks = new Dictionary<Stats, int>();      // CAR_T4_01 / SUP_T4_02
    private readonly Dictionary<Stats, float> _maxStackSince = new Dictionary<Stats, float>(); // SUP_T4_02
    private readonly Dictionary<Stats, EffectHandle> _supT4SlowHandles = new Dictionary<Stats, EffectHandle>();
    // F3C: Infection lifecycle is owned by the Debuff Registry. We keep only the accepted handle per target
    // so changing Protocol can remove this producer's live Infection immediately, matching the legacy reset.
    private readonly Dictionary<Stats, EffectHandle> _infectionHandles = new Dictionary<Stats, EffectHandle>();   // CAR_T5_01
    private int _carT4ArmorStackSerial;
    private int _carT4SlowStackSerial;
    private int _protocolDebuffSerial;

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
        ClearOwnedInfection();
        _stacks.Clear(); _maxStackSince.Clear(); _supT4SlowHandles.Clear();
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

        // ── PRT_SUP_T4_02: phải giữ max Slow stack LIÊN TỤC 5s → Stun 2s → reset sau khi Stun hết. ──
        if (_module.id == "PRT_SUP_T4_02" && _maxStackSince.Count > 0)
        {
            Stats toStun = null;
            var stale = new List<Stats>();
            foreach (var kv in _maxStackSince)
            {
                Stats target = kv.Key;
                if (target == null || !_supT4SlowHandles.TryGetValue(target, out EffectHandle slowHandle)
                    || !slowHandle.IsValid || !target.StatusEffects.IsEffectActive(slowHandle))
                {
                    stale.Add(target);
                    continue;
                }

                if (Time.time - kv.Value >= 5f)
                {
                    toStun = target;
                    break;
                }
            }

            foreach (Stats target in stale) ResetSuppressionT4State(target, removeSlow: false);

            if (toStun != null)
            {
                _maxStackSince.Remove(toStun);
                EffectApplyResult stunResult = toStun.ApplyEffectWithResult(
                    new CombatEffectInfo(CombatEffectType.Stun, 2f)
                    {
                        effectId = "PRT_SUP_T4_02.MaxStackStun",
                        sourcePosition = transform.position,
                        note = "Frostbite Matrix: max Slow stack held continuously for 5s"
                    },
                    stats, $"PRT_SUP_T4_02.MaxStackStun:{GetInstanceID()}");

                float resetDelay = stunResult.Accepted ? Mathf.Max(0f, stunResult.resolvedDuration) : 0f;
                StartCoroutine(ResetSuppressionT4AfterStun(toStun, resetDelay));
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
                if (Random.value < 0.20f) target.ApplyBleed(phys * 0.10f, 3f, stats, $"PRT_ART_T3_01.Bleed:{GetInstanceID()}");
                break;

            case "PRT_ART_T3_02": // -10% Giáp 3s, không cộng dồn / không refresh khi đang active
                ApplyProtocolArmorMrNoRefresh(target, "PRT_ART_T3_02.ArmorDown", target.armor * 0.10f, 0f, 3f);
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
            case "PRT_CAR_T3_01": // 10% Nhiễu loạn (-10% dmg địch) 3s; legacy proc có thể overlap độc lập
                if (Random.value < 0.10f) ApplyProtocolWeakenIndependent(target, "PRT_CAR_T3_01.Weaken", 0.10f, 3f);
                break;

            case "PRT_CAR_T3_02": // +2% atk speed Companion, tối đa 10 stack
                if (_carAtkSpeedStacks < 10)
                {
                    _carAtkSpeedStacks++;
                    stats.bonusAttackSpeed += 0.02f;
                    stats.CalculateCombatStatsOnly();
                }
                break;

            case "PRT_CAR_T4_01": // slow + -10% giáp, stack tối đa 3, mỗi stack 3s độc lập
                {
                    int s = GetStack(target);
                    if (s < 3)
                    {
                        ApplyCarnageT4SlowStack(target);
                        ApplyCarnageT4ArmorStack(target);
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
            case "PRT_SUP_T3_01": // -10% Kháng phép 3s, không cộng dồn / không refresh khi đang active
                ApplyProtocolArmorMrNoRefresh(target, "PRT_SUP_T3_01.MagicResistDown", 0f, target.magicResist * 0.10f, 3f);
                break;

            case "PRT_SUP_T3_02": // mỗi 10s → đòn kế tiếp tạo vùng Trầm Mặc r1.5f 3s
                if (_charged) { _charged = false; SpawnSilenceZone(target.transform.position, 1.5f, 3f); }
                break;

            case "PRT_SUP_T4_01": // đòn thứ 3 → hố đen r1f 1s hút địch
                if (_hitCount % 3 == 0) SpawnBlackHole(target.transform.position, 1f, 1f);
                break;

            case "PRT_SUP_T4_02": // Slow 10% additive max3; giữ max liên tục 5s → Stun 2s → reset sau Stun
                ApplySuppressionT4Slow(target);
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

            case "PRT_AEG_T4_01": // mỗi 5s → đòn kế tiếp chuyển Aggro của địch đang target Player sang Companion
                if (_charged)
                {
                    _charged = false;
                    // Source asset ghi rõ: chỉ những enemy ĐANG target Player mới bị chuyển Aggro.
                    // Giữ bán kính 8f theo implementation hiện hữu; đây là aggro mechanic, KHÔNG phải Taunt CC.
                    foreach (var e in EnemiesInRadius(transform.position, 8f))
                    {
                        EnemyAI enemyAI = e.GetComponent<EnemyAI>();
                        if (enemyAI != null && playerTf != null && enemyAI.nearestTarget == playerTf)
                            RedirectAggroToCompanion(e);
                    }
                    VisualDebugHelper.DrawSphere(transform.position, 8f, Color.blue, 0.3f);
                }
                break;

            case "PRT_AEG_T4_02": // -15% dmg địch 3s, không cộng dồn / không refresh khi đang active
                ApplyProtocolWeakenNoRefresh(target, "PRT_AEG_T4_02.Weaken", 0.15f, 3f);
                break;

            case "PRT_AEG_T5_01": // mỗi 10s → đòn kế tiếp sóng xung kích r5: dmg + chuyển Aggro + -30% dmg địch 4s
                if (_charged)
                {
                    _charged = false;
                    foreach (var e in EnemiesInRadius(transform.position, 5f))
                    {
                        DealByProtocol(e, 1.0f);
                        RedirectAggroToCompanion(e);
                        ApplyProtocolWeakenIndependent(e, "PRT_AEG_T5_01.Weaken", 0.30f, 4f);
                    }
                    VisualDebugHelper.DrawSphere(transform.position, 5f, Color.yellow, 0.5f);
                }
                break;
        }
    }

    // ── Helpers ──
    private int GetStack(Stats s) { _stacks.TryGetValue(s, out int v); return v; }
    private void SetStack(Stats s, int v) { _stacks[s] = v; }

    /// <summary>
    /// Closure fix: PRT_SUP_T4_02 Frostbite Matrix. Mỗi hit +10% Slow, shared refresh 5s, max 3 stack.
    /// Khi giữ 3 stack liên tục đủ 5s thì Stun; stack chỉ reset SAU khi Stun kết thúc theo asset description.
    /// </summary>
    private void ApplySuppressionT4Slow(Stats target)
    {
        if (target == null || target.currentHp <= 0) return;

        int current = GetStack(target);
        if (_supT4SlowHandles.TryGetValue(target, out EffectHandle tracked)
            && (!tracked.IsValid || !target.StatusEffects.IsEffectActive(tracked)))
        {
            ResetSuppressionT4State(target, removeSlow: false);
            current = 0;
        }

        const string effectId = "PRT_SUP_T4_02.Slow";
        var slow = new CombatEffectInfo(CombatEffectType.Slow, 5f)
        {
            effectId = effectId,
            magnitude = 0.10f,
            stackPolicy = EffectStackPolicy.AdditiveStacks,
            maxStacks = 3,
            note = "Frostbite Matrix +10% Slow stack, shared 5s refresh"
        };

        EffectApplyResult result = target.ApplyEffectWithResult(
            slow, stats, $"{effectId}:{GetInstanceID()}");
        if (!result.Accepted) return;

        _supT4SlowHandles[target] = result.handle;
        if (current < 3)
        {
            current++;
            SetStack(target, current);
            if (current == 3) _maxStackSince[target] = Time.time;
        }
    }

    private IEnumerator ResetSuppressionT4AfterStun(Stats target, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        ResetSuppressionT4State(target, removeSlow: true);
    }

    private void ResetSuppressionT4State(Stats target, bool removeSlow)
    {
        if (removeSlow && target != null
            && _supT4SlowHandles.TryGetValue(target, out EffectHandle slowHandle) && slowHandle.IsValid)
            target.StatusEffects.RemoveEffect(slowHandle, EffectRemoveReason.Manual);

        _supT4SlowHandles.Remove(target);
        _maxStackSince.Remove(target);
        _stacks.Remove(target);
    }

    /// <summary>
    /// F2E FIX2: PRT_CAR_T4_01 Slow là additive stack nhưng mỗi stack giữ timer 3s độc lập.
    /// Mỗi instance dùng sourceKey riêng; StatusEffectController cộng các Additive Slow cùng effect + source
    /// trước khi cạnh tranh theo strongest-wins với Slow khác.
    /// </summary>
    private void ApplyCarnageT4SlowStack(Stats target)
    {
        if (target == null || target.currentHp <= 0) return;

        const string effectId = "PRT_CAR_T4_01.Slow";
        string sourceKey = $"{effectId}:{GetInstanceID()}:{++_carT4SlowStackSerial}";
        var slow = new CombatEffectInfo(CombatEffectType.Slow, 3f)
        {
            effectId = effectId,
            magnitude = 0.10f,
            stackPolicy = EffectStackPolicy.AdditiveStacks,
            maxStacks = 3,
            note = "PRT_CAR_T4_01 independent additive Slow stack"
        };
        target.ApplyEffectWithResult(slow, stats, sourceKey);
    }

    /// <summary>
    /// F2E: PRT_CAR_T4_01 ArmorDown dùng 3 Debuff instance độc lập để giữ đúng legacy per-stack expiry.
    /// Mỗi hit snapshot 10% EffectiveArmor hiện tại; cleanse/expiry giải phóng stack ngay qua onRemove.
    /// </summary>
    private void ApplyCarnageT4ArmorStack(Stats target)
    {
        if (target == null || target.currentHp <= 0) return;

        int current = GetStack(target);
        if (current >= 3) return;

        float armorAmount = Mathf.Max(0f, target.EffectiveArmor * 0.10f);

        const string effectId = "PRT_CAR_T4_01.ArmorDown";
        string sourceKey = $"{effectId}:{GetInstanceID()}:{++_carT4ArmorStackSerial}";
        var request = new DebuffRequest
        {
            effectId = effectId,
            duration = 3f,
            cleanseable = true,
            stackPolicy = EffectStackPolicy.IndependentPerSource,
            maxStacks = 1,
            potency = armorAmount,
            note = "PRT_CAR_T4_01 independent 10% current-Armor stack"
        };
        request.statModifiers.Add(new RuntimeStatModifierSpec(
            RuntimeStatKey.Armor, RuntimeModifierOperation.Flat, -armorAmount));
        request.onRemove = (snapshot, reason) =>
        {
            if (target == null || !_stacks.TryGetValue(target, out int stacks)) return;
            stacks = Mathf.Max(0, stacks - 1);
            if (stacks == 0) _stacks.Remove(target);
            else _stacks[target] = stacks;
        };

        EffectApplyResult result = target.ApplyDebuff(request, stats, sourceKey);
        if (result.Accepted)
            SetStack(target, current + 1);
    }

    /// <summary>
    /// F2F: các Protocol ghi "không cộng dồn" đang có implementation no-refresh trong thời gian active.
    /// Registry là source of truth: nếu effectId còn tồn tại thì bỏ qua hit mới; Cleanse/expiry xóa effect
    /// nên lần hit sau có thể apply lại ngay mà không cần guard timer/coroutine riêng.
    /// </summary>
    private void ApplyProtocolArmorMrNoRefresh(Stats target, string effectId, float armorAmount, float mrAmount, float duration)
    {
        if (target == null || target.currentHp <= 0 || target.StatusEffects == null) return;
        if (target.StatusEffects.HasDebuff(effectId)) return;

        target.ApplyArmorMagicResistDown(
            effectId, armorAmount, mrAmount, duration, stats,
            $"{effectId}:{GetInstanceID()}", EffectStackPolicy.RefreshSameSource, 1,
            Mathf.Max(armorAmount, mrAmount), $"{effectId} Protocol no-stack/no-refresh");
    }

    private void ApplyProtocolWeakenNoRefresh(Stats target, string effectId, float magnitude, float duration)
    {
        if (target == null || target.currentHp <= 0 || target.StatusEffects == null) return;
        if (target.StatusEffects.HasDebuff(effectId)) return;

        target.ApplyDamageOutputDown(
            effectId, magnitude, duration, stats, $"{effectId}:{GetInstanceID()}",
            EffectStackPolicy.RefreshSameSource, 1, magnitude, $"{effectId} Protocol no-stack/no-refresh");
    }

    private void ApplyProtocolWeakenIndependent(Stats target, string effectId, float magnitude, float duration)
    {
        if (target == null || target.currentHp <= 0) return;
        target.ApplyDamageOutputDown(
            effectId, magnitude, duration, stats,
            $"{effectId}:{GetInstanceID()}:{++_protocolDebuffSerial}",
            EffectStackPolicy.IndependentPerSource, 1, magnitude, $"{effectId} Protocol independent proc");
    }

    private void HandleInfection(Stats target, float phys)
    {
        if (target == null || target.currentHp <= 0) return;

        const string effectId = "PRT_CAR_T5_01.Infection";
        string sourceKey = $"{effectId}:{GetInstanceID()}";

        var request = new DebuffRequest
        {
            effectId = effectId,
            duration = 5f,
            cleanseable = true,
            stackPolicy = EffectStackPolicy.AdditiveStacks,
            maxStacks = 10,
            potency = 1f,
            note = "Infection: 1 stack/hit, shared 5s refresh; stack 10 explodes for 500% Companion physicalAtk True Damage"
        };

        request.onApply = snapshot =>
        {
            if (snapshot.target != null) _infectionHandles[snapshot.target] = snapshot.handle;
        };
        request.onRefreshOrStack = snapshot =>
        {
            if (snapshot.target != null) _infectionHandles[snapshot.target] = snapshot.handle;
            if (snapshot.stackCount < 10 || snapshot.target == null || snapshot.target.currentHp <= 0) return;

            // Preserve legacy semantics: explosion uses the physicalAtk snapshot supplied by the hit
            // that reaches stack 10, then Infection is consumed immediately.
            DealTrue(snapshot.target, phys * 5.0f);
            VisualDebugHelper.DrawSphere(snapshot.target.transform.position, 1f, Color.magenta, 0.4f);
            snapshot.target.StatusEffects.RemoveEffect(snapshot.handle, EffectRemoveReason.Manual);
        };
        request.onRemove = (snapshot, reason) =>
        {
            if (snapshot.target != null
                && _infectionHandles.TryGetValue(snapshot.target, out EffectHandle tracked)
                && tracked.Equals(snapshot.handle))
                _infectionHandles.Remove(snapshot.target);
        };

        EffectApplyResult result = target.ApplyDebuff(request, stats, sourceKey);
        if (result.Accepted && result.handle.IsValid && target.StatusEffects.IsEffectActive(result.handle))
            _infectionHandles[target] = result.handle;
    }

    private void ClearOwnedInfection()
    {
        if (_infectionHandles.Count == 0) return;
        var entries = new List<KeyValuePair<Stats, EffectHandle>>(_infectionHandles);
        _infectionHandles.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            Stats target = entries[i].Key;
            EffectHandle handle = entries[i].Value;
            if (target != null && handle.IsValid)
                target.StatusEffects.RemoveEffect(handle, EffectRemoveReason.Manual);
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

    // G7B: Gravity Well is a non-CC Suction Field, same mechanic as the G3B weapon fields.
    // Each third-hit well owns an independent source key so simultaneous wells add suction vectors;
    // the MovementInfluenceController automatically uses the strongest field Move Speed penalty.
    private int _supT4BlackHoleSerial;
    private void SpawnBlackHole(Vector3 center, float radius, float dur)
    {
        string fieldKey = $"PRT_SUP_T4_01.BlackHole:{GetInstanceID()}:{++_supT4BlackHoleSerial}";
        StartCoroutine(SupT4BlackHoleField(center, radius, dur, fieldKey));
    }

    private IEnumerator SupT4BlackHoleField(Vector3 center, float radius, float dur, string fieldKey)
    {
        const float suctionSpeed = 2f;
        const float moveSpeedPenalty = 0.20f; // G7B initial tuning, not a universal rule.
        const float visualInterval = 0.05f;
        Color visualColor = new Color(0.4f, 0f, 0.6f, 0.5f);
        float elapsed = 0f;
        float nextVisualTick = 0f;

        while (elapsed < dur)
        {
            // Keep normal AI, attacks and skills. No CombatEffectType.Pull/Slow, Warp,
            // ControlState mutation or CC immunity/resistance/Super Armor checks.
            foreach (Stats enemy in EnemiesInRadius(center, radius))
            {
                MovementInfluenceController.GetOrAdd(enemy)?.ApplyOrRefresh(
                    fieldKey, center, suctionSpeed, moveSpeedPenalty);
            }

            if (elapsed + 0.0001f >= nextVisualTick)
            {
                VisualDebugHelper.DrawSphere(center, radius, visualColor, visualInterval);
                nextVisualTick += visualInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
        // Field contributions expire automatically when no longer refreshed this frame.
    }

}
