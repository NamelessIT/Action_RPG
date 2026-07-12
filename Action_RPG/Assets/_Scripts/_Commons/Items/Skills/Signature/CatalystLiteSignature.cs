using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CatalystLiteSignature : SkillBehavior
{
    [Header("Fear Settings (Tiếng Hét)")]
    public float fearRadius = 1.5f;          // Bán kính gây sợ hãi
    public float fearDuration = 2.0f;        // Chạy trốn trong 2s
    public float fleeDistance = 8.0f;        // Khoảng cách quái sẽ bỏ chạy ra xa

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

        // Tìm quái trong phạm vi 1.5f
        Collider[] hits = Physics.OverlapSphere(transform.position, fearRadius, player.dangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = hit.GetComponent<Stats>();
            if (enemy != null && !enemy.isDead)
            {
                StartCoroutine(FearFleeRoutine(enemy));
            }
        }
    }

    private IEnumerator FearFleeRoutine(Stats enemy)
    {
        // Vô hiệu hóa AI cũ để quái không tự đánh trả
        var enemyAI = enemy.GetComponent<MonoBehaviour>(); // Giả định là EnemyAI script
        if (enemyAI != null) enemyAI.enabled = false;

        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;

            // Tính toán vị trí chạy trốn (Ngược hướng với Player)
            Vector3 fleeDir = (enemy.transform.position - transform.position).normalized;
            Vector3 targetPos = enemy.transform.position + fleeDir * fleeDistance;

            // Tìm điểm gần nhất trên NavMesh để quái không chạy vào tường
            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out navHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
                agent.speed *= 0.75f; // Quái chạy chậm hơn vì đang sợ hãi
            }
        }

        yield return new WaitForSeconds(fearDuration);

        // Khôi phục lại quái
        if (enemy != null && !enemy.isDead)
        {
            if (agent != null && agent.enabled)
            {
                agent.speed /= 0.75f;
                agent.ResetPath();
            }
            if (enemyAI != null) enemyAI.enabled = true;
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
        // [CE-02C4] Slow -debuffPercent (move+attack) qua effect system; refresh mỗi frame khi còn trong vùng,
        // tự hết hạn khi rời vùng (strongest-wins, KHÔNG mutate base stat → không cần lưu gốc/restore).
        foreach (var hit in enemyHits)
        {
            Stats enemy = hit.GetComponent<Stats>();
            if (enemy != null && !enemy.isDead)
                enemy.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, 0.25f) { magnitude = debuffPercent }, stats);
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
        // enemy Slow tự hết hạn qua effect system → không cần restore base stat ở cleanup.

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