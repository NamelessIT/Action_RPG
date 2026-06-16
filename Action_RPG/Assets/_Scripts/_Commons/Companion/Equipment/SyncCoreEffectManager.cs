using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiệu Ứng Slot 3 (Sync Core) — Rarity 3+. Theo "Tổng hợp Effect Sync Core.docx".
/// Hiệu ứng "Kết hợp" Player ⇄ Companion: lắng nghe cả sự kiện Player lẫn Companion.
///
/// GHI CHÚ XẤP XỈ:
///  • SYNC_T3_01 (+5% đòn kế của Player): cộng thêm 5% của lượng damage Player vừa gây (dạng true) rồi xoá dấu.
///  • SYNC_T4_02 (tia sét): định tuyến qua DealMagic để vẫn bị MR trừ (mult = (2*phys+2*magic)/magicAtk).
///  • SYNC_T5_02 (đồng-mục-tiêu +20%): "cùng target" xác định qua ai.currentTarget (phía Player) và
///    mốc thời gian Player vừa đánh địch đó (phía Companion) — xấp xỉ vì Player không có khái niệm "target".
///  • SYNC_T4_04 (luồng năng lượng): lấy mẫu vài điểm trên đoạn Player↔Companion để quét địch trúng luồng.
/// </summary>
[DisallowMultipleComponent]
public class SyncCoreEffectManager : CompanionEffectManagerBase
{
    private CompanionSyncCoreData _module;

    private float _cd1;
    private float _tetherTick;
    private readonly Dictionary<Stats, float> _markUntil = new Dictionary<Stats, float>();        // SYNC_T3_01
    private readonly Dictionary<Stats, float> _playerHitTime = new Dictionary<Stats, float>();     // SYNC_T5_02

    protected override void Awake() { base.Awake(); }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.OnHitEnemy  += OnCompanionHit;
            stats.OnKillEnemy += OnCompanionKill;
        }
        if (playerStats != null)
        {
            playerStats.OnPerfectDodgeTriggered += OnPlayerPerfectDodge;
            playerStats.OnBeforeTakeDamage      += OnPlayerBeforeDamage;
        }
        if (playerController != null)
        {
            playerController.OnDamageDealt += OnPlayerDamageDealt;
            playerController.OnDashPerformed += OnPlayerDash;
        }
        SkillManager.OnPlayerSignatureCast += OnPlayerSignature;
    }
    private void OnDisable()
    {
        if (stats != null)
        {
            stats.OnHitEnemy  -= OnCompanionHit;
            stats.OnKillEnemy -= OnCompanionKill;
        }
        if (playerStats != null)
        {
            playerStats.OnPerfectDodgeTriggered -= OnPlayerPerfectDodge;
            playerStats.OnBeforeTakeDamage      -= OnPlayerBeforeDamage;
        }
        if (playerController != null)
        {
            playerController.OnDamageDealt -= OnPlayerDamageDealt;
            playerController.OnDashPerformed -= OnPlayerDash;
        }
        SkillManager.OnPlayerSignatureCast -= OnPlayerSignature;
    }

    public void SetModule(CompanionSyncCoreData m)
    {
        _module = m;
        _cd1 = 0f;
        _markUntil.Clear();
        _playerHitTime.Clear();
    }

    private bool Active => _module != null && _module.HasEffect;

    private void Update()
    {
        if (!Active) return;

        // SYNC_T4_04: luồng năng lượng Player↔Companion (<10f) → địch trúng luồng bị Bleed 10% physAtk/s
        if (_module.id == "SYNC_T4_04" && playerTf != null)
        {
            float d = Vector3.Distance(playerTf.position, transform.position);
            if (d <= 10f)
            {
                _tetherTick += Time.deltaTime;
                VisualDebugHelper.DrawSphere((playerTf.position + transform.position) * 0.5f, 0.3f, new Color(0.2f, 0.8f, 1f, 0.3f), 0.1f);
                if (_tetherTick >= 1f)
                {
                    _tetherTick = 0f;
                    foreach (var e in EnemiesOnTether(playerTf.position, transform.position, 1f))
                        e.ApplyBleed(stats.physicalAtk * 0.10f, 1f);
                }
            }
        }
    }

    // ───────────────────────── COMPANION EVENTS ─────────────────────────
    private void OnCompanionHit(Stats target, float dir, bool isCrit)
    {
        if (!Active || target == null || target.currentHp <= 0) return;

        switch (_module.id)
        {
            case "SYNC_T3_01": // đánh dấu 3s cho đòn kế của Player
                _markUntil[target] = Time.time + 3f;
                break;

            case "SYNC_T5_02": // đồng-mục-tiêu: nếu Player vừa đánh địch này trong 2s → +20%
                if (_playerHitTime.TryGetValue(target, out float t) && Time.time - t <= 2f)
                    DealByProtocol(target, 0.20f);
                break;
        }
    }

    private void OnCompanionKill(Stats victim, bool backstab)
    {
        if (!Active) return;
        // SYNC_T4_01: Companion hạ gục → Player hồi 3% maxHp + 20% moveSpeed 5s
        if (_module.id == "SYNC_T4_01" && playerStats != null)
        {
            playerStats.Heal(playerStats.maxHp * 0.03f, true, false, HealSource.Drain);
            StartCoroutine(PlayerMoveSpeed(0.20f, 5f));
        }
    }

    // ───────────────────────── PLAYER EVENTS ─────────────────────────
    private void OnPlayerDamageDealt(Stats target, DamageInfo info)
    {
        if (!Active || target == null || info == null) return;

        switch (_module.id)
        {
            case "SYNC_T3_01": // đòn Player lên mục tiêu đã đánh dấu → +5% + xoá dấu
                if (_markUntil.TryGetValue(target, out float until) && Time.time <= until)
                {
                    _markUntil.Remove(target);
                    target.TakeDamage(new DamageInfo { trueDamage = info.TotalRawDamage * 0.05f, attacker = playerStats, sourcePosition = playerTf != null ? playerTf.position : transform.position, sourceType = DamageSourceType.Other });
                }
                break;

            case "SYNC_T3_03": // Passive: Companion hồi 5% sát thương Player gây ra
                stats.Heal(info.TotalRawDamage * 0.05f, false, false, HealSource.Drain);
                break;

            case "SYNC_T5_02": // Passive: cùng đánh 1 địch → +20% cho cả hai (phía Player)
                _playerHitTime[target] = Time.time;
                if (CurrentTargetStats() == target)
                    target.TakeDamage(new DamageInfo { trueDamage = info.TotalRawDamage * 0.20f, attacker = playerStats, sourcePosition = playerTf != null ? playerTf.position : transform.position, sourceType = DamageSourceType.Other });
                break;
        }
    }

    private void OnPlayerDash()
    {
        if (!Active) return;
        // SYNC_T3_02: Player Dash → Companion +20% moveSpeed +20% atkSpeed 2s
        if (_module.id == "SYNC_T3_02")
            StartCoroutine(CompanionDashBuff(0.20f, 2f));
    }

    private void OnPlayerSignature()
    {
        if (!Active) return;
        // SYNC_T4_02: Player Signature → tia sét vào địch nhiều máu nhất trong 5f của Player
        if (_module.id == "SYNC_T4_02")
        {
            Vector3 center = playerTf != null ? playerTf.position : transform.position;
            Stats tgt = HighestHpEnemy(center, 5f);
            if (tgt != null)
            {
                float amount = stats.physicalAtk * 2f + stats.magicAtk * 2f;
                if (stats.magicAtk > 0.01f) DealMagic(tgt, amount / stats.magicAtk);
                else DealTrue(tgt, amount);
                VisualDebugHelper.DrawSphere(tgt.transform.position, 0.5f, Color.magenta, 0.4f);
            }
        }
    }

    private void OnPlayerPerfectDodge()
    {
        if (!Active) return;
        // SYNC_T4_03: Player Perfect Dodge → Companion dịch chuyển tới địch gần nhất + Stun 1.5s + 150% magicAtk
        if (_module.id == "SYNC_T4_03")
        {
            Stats tgt = NearestEnemy(playerTf != null ? playerTf.position : transform.position, 15f);
            if (tgt != null)
            {
                Vector3 dir = (transform.position - tgt.transform.position);
                dir.y = 0f; dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
                Vector3 dest = tgt.transform.position + dir * 1.5f;
                var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(dest);
                else transform.position = dest;
                DealMagic(tgt, 1.5f, 0, true, 1.5f);
                VisualDebugHelper.DrawSphere(tgt.transform.position, 0.5f, Color.yellow, 0.4f);
            }
        }
    }

    private void OnPlayerBeforeDamage(DamageInfo info)
    {
        if (!Active || info == null || playerStats == null) return;
        // SYNC_T5_01: Player nhận đòn chí mạng → vô hiệu, Companion chết thay, Player hồi 50% + Cuồng Nộ 10s, CD90s
        if (_module.id != "SYNC_T5_01") return;
        if (stats == null || stats.isDead) return;
        if (Time.time < _cd1) return;

        float dmg = info.TotalRawDamage;
        if (playerStats.currentHp - dmg > 0f) return; // chưa chí mạng

        _cd1 = Time.time + 90f;
        // vô hiệu đòn
        info.physDamage = 0f; info.magicDamage = 0f; info.trueDamage = 0f;
        // Companion chết thay
        stats.TakeDamage(new DamageInfo { trueDamage = stats.maxHp * 10f, attacker = null, sourcePosition = transform.position, sourceType = DamageSourceType.Other });
        // Player hồi 50% + Cuồng Nộ
        playerStats.Heal(playerStats.maxHp * 0.50f, true, false, HealSource.Other);
        StartCoroutine(PlayerRage(0.50f, 10f));
        VisualDebugHelper.DrawSphere(playerStats.transform.position, 1.5f, Color.red, 0.6f);
    }

    // ───────────────────────── ROUTINES / HELPERS ─────────────────────────
    private IEnumerator CompanionDashBuff(float pct, float dur)
    {
        stats.bonusMoveSpeed += pct; stats.bonusAttackSpeed += pct; stats.CalculateCombatStatsOnly();
        yield return new WaitForSeconds(dur);
        if (stats != null) { stats.bonusMoveSpeed -= pct; stats.bonusAttackSpeed -= pct; stats.CalculateCombatStatsOnly(); }
    }

    private IEnumerator PlayerMoveSpeed(float pct, float dur)
    {
        if (playerStats == null) yield break;
        playerStats.bonusMoveSpeed += pct; playerStats.CalculateCombatStatsOnly();
        yield return new WaitForSeconds(dur);
        if (playerStats != null) { playerStats.bonusMoveSpeed -= pct; playerStats.CalculateCombatStatsOnly(); }
    }

    private IEnumerator PlayerRage(float pct, float dur)
    {
        if (playerStats == null) yield break;
        playerStats.damageOutputMultiplier += pct;
        yield return new WaitForSeconds(dur);
        if (playerStats != null) playerStats.damageOutputMultiplier -= pct;
    }

    private List<Stats> EnemiesOnTether(Vector3 a, Vector3 b, float radius)
    {
        var seen = new HashSet<Stats>();
        var list = new List<Stats>();
        int samples = 6;
        for (int i = 0; i <= samples; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / (float)samples);
            foreach (var h in Physics.OverlapSphere(p, radius, EnemyMask()))
            {
                Stats e = CompanionCombat.GetEnemy(h);
                if (e != null && e.currentHp > 0 && seen.Add(e)) list.Add(e);
            }
        }
        return list;
    }
}
