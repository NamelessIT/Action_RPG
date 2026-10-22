using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CatalystLiteSignature : SkillBehavior
{
    [Header("Fear Settings (Tiếng Hét)")]
    public float fearRadius = 1.5f;          // Bán kính gây sợ hãi
    public float fearDuration = 2.0f;        // Chạy trốn trong 2s
    [Tooltip("[LEGACY] Không còn dùng từ Phase E4B. Fear movement do ForcedControlRunner/consumer xử lý.")]
    public float fleeDistance = 8.0f;

    [Header("AOE Aura Settings (Vùng năng lượng)")]
    public float auraRadius = 4.0f;          // Bán kính vùng buff/debuff
    public float auraDuration = 7.0f;        // Tồn tại 7 giây
    public float buffPercent = 0.2f;         // +20% cho đồng minh
    public float debuffPercent = 0.2f;       // -20% cho kẻ địch

    [Header("VFX & SFX")]
    public GameObject howlVfxPrefab;         // Hiệu ứng sóng âm khi hét
    public GameObject auraVfxPrefab;         // Hiệu ứng vòng tròn đi theo người
    public AudioClip howlSfx;                // Âm thanh tiếng hét

    private Coroutine auraCoroutine;
    private GameObject currentAuraVfx;
    private string auraAttackSpeedDownSourceKey;

    // Quản lý để không cộng dồn/trừ lố chỉ số (Chống Stacking Bug)
    private HashSet<AllyStats> affectedAllies = new HashSet<AllyStats>();


    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }
    protected override void OnUnequip()
    {
        if (auraCoroutine != null) StopCoroutine(auraCoroutine);
        CleanUpAura();
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        // 1. THỰC HIỆN TIẾNG HÉT (FEAR)
        ExecuteFearScream();

        // 2. KÍCH HOẠT VÙNG AOE ĐI THEO NGƯỜI
        if (auraCoroutine != null) StopCoroutine(auraCoroutine);
        auraCoroutine = StartCoroutine(AuraRoutine());

        return true;
    }

    private void ExecuteFearScream()
    {
        Debug.Log("<color=orange>CATALYST: PRIMAL HOWL!</color>");

        // Hiệu ứng hình ảnh và âm thanh
        //if (howlVfxPrefab) Instantiate(howlVfxPrefab, transform.position, Quaternion.identity);
        // if (howlSfx) AudioSource.PlayClipAtPoint(howlSfx, transform.position);

        // Phase E4B: Fear đi qua StatusEffectController thay vì tự disable EnemyAI / mutate NavMeshAgent.
        // Duration được resistanceEffect xử lý ở core; consumer chạy trốn ở 50% final normal move speed.
        Collider[] hits = Physics.OverlapSphere(transform.position, fearRadius, player.dangerLayer);
        HashSet<Stats> seen = new HashSet<Stats>();

        foreach (var hit in hits)
        {
            Stats enemy = hit.GetComponentInParent<Stats>();
            if (enemy == null || enemy.isDead || !seen.Add(enemy)) continue;

            enemy.ApplyEffect(
                new CombatEffectInfo(CombatEffectType.Fear, fearDuration)
                {
                    effectId = "CatalystLiteSignature.PrimalHowl.Fear",
                    impactLevel = 0,
                    stackPolicy = EffectStackPolicy.RefreshSameSource,
                    interruptCurrentAction = true,
                    note = "Catalyst Lite Signature - Primal Howl"
                },
                stats);
        }
    }

    private IEnumerator AuraRoutine()
    {
        //if (auraVfxPrefab) currentAuraVfx = Instantiate(auraVfxPrefab, transform);

        float timer = 0f;
        while (timer < auraDuration)
        {
            UpdateAuraEffects();
            yield return null; // Chạy mỗi frame để cập nhật theo vị trí Player
            timer += Time.deltaTime;
        }

        CleanUpAura();
    }

    private void UpdateAuraEffects()
    {
        // 1. QUÉT ĐỐI TƯỢNG TRONG VÙNG 5M
        Collider[] allyHits = Physics.OverlapSphere(transform.position, auraRadius, LayerMask.GetMask("Ally"));
        Collider[] enemyHits = Physics.OverlapSphere(transform.position, auraRadius, player.dangerLayer);

        HashSet<AllyStats> currentAllies = new HashSet<AllyStats>();

        // --- XỬ LÝ ĐỒNG MINH (BUFF) ---
        foreach (var hit in allyHits)
        {
            AllyStats ally = hit.GetComponent<AllyStats>();
            if (ally != null && !ally.isDead)
            {
                currentAllies.Add(ally);
                if (!affectedAllies.Contains(ally))
                {
                    // Áp dụng buff lần đầu khi bước vào vùng
                    ally.bonusMoveSpeed += buffPercent;
                    ally.bonusAttackSpeed += buffPercent;
                    ally.CalculateMoveSpeedOnly();
                    ally.CalculateCombatStatsOnly();
                    affectedAllies.Add(ally);
                }
            }
        }

        // --- XỬ LÝ KẺ ĐỊCH (DEBUFF) ---
        // Phase F2: aura có HAI effect độc lập theo authored behavior:
        // Slow giảm movement + AttackSpeedDown giảm attack cadence. Cả hai refresh ngắn khi còn trong vùng.
        if (string.IsNullOrEmpty(auraAttackSpeedDownSourceKey))
            auraAttackSpeedDownSourceKey = $"CatalystLiteSignature.Aura.AttackSpeedDown:{GetInstanceID()}";

        foreach (var hit in enemyHits)
        {
            Stats enemy = hit.GetComponentInParent<Stats>();
            if (enemy != null && !enemy.isDead)
            {
                // Stable identity: refresh đúng MỘT Slow instance/source thay vì tạo instance mới mỗi frame.
                // Strongest-wins vẫn do StatusEffectController resolve giữa các nguồn Slow khác nhau.
                enemy.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, 0.25f)
                {
                    effectId = "CatalystLiteSignature.Aura.Slow",
                    magnitude = debuffPercent,
                    stackPolicy = EffectStackPolicy.RefreshSameSource,
                    maxStacks = 1,
                    note = "Catalyst Lite Signature - Aura Slow"
                }, stats);

                enemy.ApplyAttackSpeedDown(
                    debuffPercent, 0.25f, stats, auraAttackSpeedDownSourceKey,
                    "Catalyst Lite Signature - Aura AttackSpeedDown");
            }
        }

        // --- DỌN DẸP KHI RỜI VÙNG (chỉ ĐỒNG MINH; enemy Slow tự hết hạn) ---
        affectedAllies.RemoveWhere(ally => {
            if (!currentAllies.Contains(ally))
            {
                if (ally != null)
                {
                    ally.bonusMoveSpeed -= buffPercent;
                    ally.bonusAttackSpeed -= buffPercent;
                    ally.CalculateMoveSpeedOnly();
                    ally.CalculateCombatStatsOnly();
                }
                return true;
            }
            return false;
        });
    }

    private void CleanUpAura()
    {
        if (currentAuraVfx) Destroy(currentAuraVfx);

        // Trả lại chỉ số cho mọi đối tượng còn kẹt trong List
        foreach (var ally in affectedAllies)
        {
            if (ally != null)
            {
                ally.bonusMoveSpeed -= buffPercent;
                ally.bonusAttackSpeed -= buffPercent;
                ally.CalculateMoveSpeedOnly();
                ally.CalculateCombatStatsOnly();
            }
        }
        affectedAllies.Clear();
        // enemy Slow + AttackSpeedDown tự hết hạn qua effect system → không cần restore stat ở cleanup.

        auraCoroutine = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fearRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
}