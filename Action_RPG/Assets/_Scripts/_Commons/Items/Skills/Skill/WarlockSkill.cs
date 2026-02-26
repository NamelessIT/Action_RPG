using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WarlockSkill : SkillBehavior
{
    [Header("Mark Settings")]
    public float castRadius = 5.0f;      // Phạm vi gắn dấu ấn
    public int maxStacks = 5;            // Số lượng stack tối đa
    public float markDuration = 10.0f;   // Tồn tại 10 giây

    [Header("Damage Settings")]
    [Tooltip("Sát thương phép thêm = % Máu đã mất (0.1 = 10%)")]
    public float missingHpDamagePercent = 0.10f;

    [Header("Buff Settings")]
    public float msBuffPercent = 0.20f;  // Tăng 20% Tốc chạy
    public float msBuffDuration = 3.0f;  // Duy trì 3 giây
    public float healPercent = 0.03f;    // Hồi 3% Max HP mỗi stack

    [Header("VFX")]
    public GameObject markCastVfxPrefab; // Hiệu ứng tỏa ra khi dùng chiêu
    public GameObject markHitVfxPrefab;  // Hiệu ứng lóe lên khi kích nổ stack

    private Rigidbody rb;

    // Lớp để quản lý dữ liệu dấu ấn trên từng kẻ địch
    private class MarkData
    {
        public int stacks;
        public Coroutine expireRoutine;
    }

    // Từ điển lưu trữ kẻ địch đang bị đánh dấu
    private Dictionary<Stats, MarkData> activeMarks = new Dictionary<Stats, MarkData>();

    // Quản lý Coroutine buff tốc độ
    private Coroutine msBuffCoroutine;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        rb = myPlayer.GetComponent<Rigidbody>();
    }

    protected override void OnEquip()
    {
        // Lắng nghe sự kiện đánh trúng địch (Từ AllyStats)
        stats.OnHitEnemy += HandleOnHitEnemy;
    }

    protected override void OnUnequip()
    {
        stats.OnHitEnemy -= HandleOnHitEnemy;

        // Dọn dẹp Coroutine xóa mark nếu tháo skill giữa chừng
        foreach (var kvp in activeMarks)
        {
            if (kvp.Value.expireRoutine != null) StopCoroutine(kvp.Value.expireRoutine);
        }
        activeMarks.Clear();

        // Dọn dẹp Buff Tốc chạy
        if (msBuffCoroutine != null)
        {
            StopCoroutine(msBuffCoroutine);
            stats.bonusMoveSpeed -= msBuffPercent;
            stats.CalculateMoveSpeedOnly();
            msBuffCoroutine = null;
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;
        StartCoroutine(CastRoutine());
        return true;
    }

    private IEnumerator CastRoutine()
    {
        // 1. Khóa di chuyển để vận chiêu
        player.isAttacking = true;
        rb.linearVelocity = Vector3.zero;

        // 2. Spawn VFX
        //if (markCastVfxPrefab != null)
        //{
        //    Instantiate(markCastVfxPrefab, transform.position, Quaternion.identity);
        //}

        // 3. Quét địch trong phạm vi
        Collider[] hits = Physics.OverlapSphere(transform.position, castRadius, player.dangerLayer);
        int markCount = 0;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                ApplyMark(enemyStats);
                markCount++;
            }
        }

        Debug.Log($"<color=magenta>Warlock:</color> Gắn {maxStacks} dấu ấn Máu Tươi lên {markCount} kẻ địch!");

        // Khựng lại một xíu (Chờ animation)
        yield return new WaitForSeconds(0.3f);
        player.isAttacking = false;
    }

    private void ApplyMark(Stats target)
    {
        if (activeMarks.ContainsKey(target))
        {
            // Đã có dấu ấn -> Reset số stack và thời gian
            MarkData data = activeMarks[target];
            data.stacks = maxStacks;
            if (data.expireRoutine != null) StopCoroutine(data.expireRoutine);
            data.expireRoutine = StartCoroutine(MarkTimer(target));
        }
        else
        {
            // Chưa có -> Tạo mới
            MarkData data = new MarkData();
            data.stacks = maxStacks;
            data.expireRoutine = StartCoroutine(MarkTimer(target));
            activeMarks.Add(target, data);
        }
    }

    private IEnumerator MarkTimer(Stats target)
    {
        yield return new WaitForSeconds(markDuration);

        // Hết thời gian thì xóa khỏi danh sách
        if (target != null && activeMarks.ContainsKey(target))
        {
            activeMarks.Remove(target);
        }
    }

    // --- HÀM NÀY CHẠY MỖI KHI BẠN ĐÁNH TRÚNG ĐỊCH ---
    private void HandleOnHitEnemy(Stats target, float t, bool isCrit)
    {
        if (target != null && activeMarks.ContainsKey(target))
        {
            MarkData mark = activeMarks[target];

            if (mark.stacks > 0)
            {
                // 1. Trừ 1 stack
                mark.stacks--;

                // 2. Gây thêm sát thương (Dựa trên máu đã mất)
                if (target.currentHp > 0)
                {
                    float missingHp = target.maxHp - target.currentHp;

                    // Công thức: Máu đã mất * 10% * Hệ số Magic của Skill
                    float extraDamage = missingHp * missingHpDamagePercent;
                    if (data != null) extraDamage *= data.skillMagicMultiplier;

                    DamageInfo extraInfo = new DamageInfo();
                    extraInfo.damageAmount = extraDamage;
                    extraInfo.sourcePosition = transform.position;
                    // Sát thương nổ này có thể Crit ké theo đòn đánh gốc
                    extraInfo.isCrit = isCrit;
                    extraInfo.isKnockback = false;
                    extraInfo.isStun = false;

                    target.TakeDamage(extraInfo);

                    //if (markHitVfxPrefab != null)
                    //{
                    //    Instantiate(markHitVfxPrefab, target.transform.position + Vector3.up, Quaternion.identity);
                    //}
                }

                // 3. Kích hoạt Hồi máu & Tốc chạy
                TriggerBuffAndHeal();

                // 4. Nếu dùng hết stack thì gỡ dấu ấn
                if (mark.stacks <= 0)
                {
                    if (mark.expireRoutine != null) StopCoroutine(mark.expireRoutine);
                    activeMarks.Remove(target);
                }
            }
        }
    }

    private void TriggerBuffAndHeal()
    {
        // A. Hồi máu (3%)
        float healAmount = stats.maxHp * healPercent;
        stats.Heal(healAmount);

        // B. Buff Tốc Độ Di Chuyển
        if (msBuffCoroutine != null)
        {
            // Nếu ĐANG BUFF -> Dừng đồng hồ cũ, chạy lại đồng hồ mới (Làm mới thời gian)
            StopCoroutine(msBuffCoroutine);
            msBuffCoroutine = StartCoroutine(MsBuffTimer());
        }
        else
        {
            // Nếu CHƯA BUFF -> Cộng chỉ số và chạy đồng hồ
            stats.bonusMoveSpeed += msBuffPercent;
            stats.CalculateMoveSpeedOnly(); // Cập nhật tốc độ ngay lập tức
            msBuffCoroutine = StartCoroutine(MsBuffTimer());
        }
    }

    private IEnumerator MsBuffTimer()
    {
        yield return new WaitForSeconds(msBuffDuration);

        // Hết giờ -> Trừ trả lại chỉ số tốc độ
        stats.bonusMoveSpeed -= msBuffPercent;
        stats.CalculateMoveSpeedOnly();
        msBuffCoroutine = null;
    }
}