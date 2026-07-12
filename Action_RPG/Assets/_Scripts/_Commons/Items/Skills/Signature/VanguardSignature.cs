using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class VanguardSignature : SkillBehavior
{
    [Header("Wall Settings")]
    public float duration = 5.0f;               // Tồn tại 5 giây
    public float damageReflectPercent = 0.3f;   // Phản 30% sát thương
    public float reviveHpPercent = 0.3f;        // Hồi sinh với 30% HP

    [Header("VFX")]
    public GameObject wallVfxPrefab;            // Bức tường ánh sáng khổng lồ bán nguyệt
    public GameObject teleportVfxPrefab;        // Hiệu ứng dịch chuyển của Companion

    private Coroutine wallCoroutine;
    private GameObject currentWallVfx;

    // Lưu lại Stats của Companion để gỡ bỏ hiệu ứng khi kết thúc
    private Stats currentCompanionStats;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        if (wallCoroutine != null)
        {
            StopCoroutine(wallCoroutine);
            CleanUpWall();
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        if (wallCoroutine != null) StopCoroutine(wallCoroutine);
        wallCoroutine = StartCoroutine(WallRoutine());

        return true;
    }

    private IEnumerator WallRoutine()
    {
        // 1. Khóa Player tại chỗ (Cắm khiên)
        player.isAttacking = true;
        player.isSkillBlocked = true;

        Vector3 forward = stats.facingDirection;
        if (forward == Vector3.zero) forward = transform.forward;
        forward.y = 0;

        // 2. Kéo Companion về và Hồi Sinh (nếu cần)
        CompanionAI companion = FindFirstObjectByType<CompanionAI>();
        if (companion != null)
        {
            currentCompanionStats = companion.GetComponent<Stats>();

            Vector3 safePosBehindPlayer = transform.position - forward * 1.5f;
            UnityEngine.AI.NavMeshHit hit;

            // --- ĐÃ SỬA: ĐƯA CÁI XÁC VỀ VỊ TRÍ AN TOÀN TRƯỚC ---
            if (UnityEngine.AI.NavMesh.SamplePosition(safePosBehindPlayer, out hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // Ép vị trí của cái xác về chỗ mới
                companion.transform.position = hit.position;

                // SAU ĐÓ MỚI HỒI SINH (Lúc này bật Collider lên sẽ không bị kẹt vào ai cả)
                if (currentCompanionStats.isDead)
                {
                    currentCompanionStats.Revive(reviveHpPercent);
                    companion.enabled = true;
                }

                // Đồng bộ lại với NavMeshAgent
                UnityEngine.AI.NavMeshAgent compAgent = companion.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (compAgent != null && compAgent.enabled) compAgent.Warp(hit.position);

                //if (teleportVfxPrefab) Instantiate(teleportVfxPrefab, hit.position, Quaternion.identity);

                // Ép Companion đứng yên nấp sau khiên
                companion.ForceWait(duration);
            }

            // Cấp khiên cho Companion
            currentCompanionStats.damageInterceptor = WallInterceptor;
        }

        // 3. Cấp khiên cho Player và Dựng tường VFX
        stats.damageInterceptor = WallInterceptor;

        //if (wallVfxPrefab)
        //{
        //    // Tường dựng ngang trước mặt
        //    currentWallVfx = Instantiate(wallVfxPrefab, transform.position + forward * 1f, Quaternion.LookRotation(forward), transform);
        //}

        Debug.Log("<color=cyan>VANGUARD: AEGIS OF THE PROTECTOR KÍCH HOẠT!</color>");

        // 4. Giữ thế trong 5 giây
        yield return new WaitForSeconds(duration);

        // 5. Kết thúc
        CleanUpWall();
    }

    // --- HÀM KIỂM TRA & CHẶN SÁT THƯƠNG ---
    private bool WallInterceptor(DamageInfo info)
    {
        // Không phản đòn lại sát thương tự thân hoặc sát thương nổ không nguồn gốc
        if (info.attacker == null || info.attacker == stats) return false;

        // Tính hướng từ điểm phát nổ/kẻ địch tới Vị trí của Bức tường (Player)
        Vector3 dirToSource = (info.sourcePosition - transform.position).normalized;
        dirToSource.y = 0;

        Vector3 forward = stats.facingDirection;
        forward.y = 0;

        // Kiểm tra xem nguồn sát thương có nằm trong góc 180 độ phía trước mặt không (<= 90 độ mỗi bên)
        if (Vector3.Angle(forward, dirToSource) <= 90f)
        {
            // Phản Sát Thương lại kẻ đánh
            float reflectDmg = info.trueDamage * damageReflectPercent;

            DamageInfo reflectInfo = new DamageInfo();
            reflectInfo.sourcePosition = transform.position;
            reflectInfo.trueDamage = reflectDmg;
            reflectInfo.attacker = stats;
            // [CC] Phản đòn là PURE true damage — không kèm CC (DamageInfo mới, effects rỗng sẵn).

            // Xuyên luôn giáp để phản đòn đau nhất có thể
            info.attacker.TakeDamage(reflectInfo);

            Debug.Log($"<color=cyan>Bức Tường Năng Lượng:</color> Chặn 100% sát thương và phản lại {reflectDmg} DMG!");

            // Trả về true để Hủy bỏ sát thương gốc
            return true;
        }

        // Nếu kẻ địch móc lốp đánh từ sau lưng (>90 độ) -> Sát thương lọt qua khiên -> Nhận sát thương bình thường
        return false;
    }

    private void CleanUpWall()
    {
        player.isAttacking = false;
        player.isSkillBlocked = false;

        // Gỡ cổng chặn sát thương
        if (stats != null) stats.damageInterceptor = null;
        if (currentCompanionStats != null) currentCompanionStats.damageInterceptor = null;

        //if (currentWallVfx != null) Destroy(currentWallVfx);

        wallCoroutine = null;
        Debug.Log("<color=gray>Vanguard: Đã thu khiên.</color>");
    }
}