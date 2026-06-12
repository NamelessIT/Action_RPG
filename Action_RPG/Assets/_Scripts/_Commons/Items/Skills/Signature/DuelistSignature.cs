using System.Collections;
using UnityEngine;
using UnityEngine.AdaptivePerformance;
using static UnityEngine.AdaptivePerformance.Provider.AdaptivePerformanceSubsystemDescriptor;

public class DuelistSignature : SkillBehavior
{
    [Header("Counter Settings")]
    [Tooltip("Khung thời gian vàng để bắt sát thương (0.3 giây)")]
    public float counterWindow = 0.3f;
    //public float damageMultiplier = 5.0f; // Chém x5 sát thương

    [Header("Debuff Settings")]
    public float debuffDuration = 10.0f;
    public float damageReductionPercent = 0.5f; // Trừ đi 0.5 (Giảm 50% sát thương)

    [Header("VFX")]
    public GameObject stanceVfxPrefab; // Hạt sáng lấp lánh lúc thu kiếm chờ địch
    public GameObject slashVfxPrefab;  // Vết chém xé rách không gian
    public GameObject debuffVfxPrefab; // Icon kiếm gãy trên đầu địch

    private Coroutine windowCoroutine;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
    }

    protected override void OnEquip() { }
    protected override void OnUnequip()
    {
        // Tháo skill thì phải gỡ cái Cổng chặn ra cho an toàn
        if (stats != null) stats.damageInterceptor = null;
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        if (windowCoroutine != null) StopCoroutine(windowCoroutine);
        windowCoroutine = StartCoroutine(CounterWindowRoutine());
        return true;
    }

    private IEnumerator CounterWindowRoutine()
    {
        // 1. VÀO THẾ BẠT KIẾM
        player.isAttacking = true;
        stats.damageInterceptor = OnDamageIntercepted; // Đăng ký bắt sát thương

        //GameObject stanceVfx = null;
        //if (stanceVfxPrefab != null) stanceVfx = Instantiate(stanceVfxPrefab, transform);

        Debug.Log("<color=cyan>Duelist: BẠT KIẾM THUẬT - SẴN SÀNG!</color>");

        // 2. CHỜ ĐỢI TRONG 0.3 GIÂY
        yield return new WaitForSeconds(counterWindow);

        // 3. NẾU KHÔNG CÓ AI ĐÁNH -> THẤT BẠI
        stats.damageInterceptor = null; // Hủy bắt sát thương
        player.isAttacking = false;
        //if (stanceVfx != null) Destroy(stanceVfx);

        Debug.Log("<color=gray>Duelist: Bạt Kiếm Thất Bại (Mất chiêu).</color>");
    }

    // --- HÀM TỰ ĐỘNG CHẠY NẾU BỊ ĐÁNH TRONG LÚC ĐANG SỬ DỤNG CHIÊU ---
    private bool OnDamageIntercepted(DamageInfo info)
    {
        if (info.attacker != null)
        {
            // 1. Kẻ địch đã sập bẫy -> Hủy thế chờ
            if (windowCoroutine != null) StopCoroutine(windowCoroutine);
            stats.damageInterceptor = null;

            // 2. Kích hoạt chuỗi chém phản công
            StartCoroutine(ExecuteCounterAttack(info.attacker));

            // 3. Trả về true để Stats.cs HỦY bỏ việc trừ máu Player
            return true;
        }
        return false; // Trả về false nếu là sát thương môi trường (cháy, độc)
    }

    private IEnumerator ExecuteCounterAttack(Stats target)
    {
        Debug.Log($"<color=red>Duelist: PHẢN CÔNG {target.name}!</color>");

        // --- PHASE 1: NGƯNG ĐỌNG THỜI GIAN ---
        Time.timeScale = 0.02f; // Chậm gần như dừng hẳn (Slow motion x50)
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        stats.isInvincible = true; // Bất tử tuyệt đối lúc đang múa

        // THUẬT TOÁN DỊCH CHUYỂN ĐỘNG VÀ QUAY MẶT
        Vector3 dirToTarget = (target.transform.position - transform.position).normalized;
        dirToTarget.y = 0;

        // 1. Lấy Hitbox của Địch và Mình
        Collider targetCol = target.GetComponent<Collider>();
        Collider playerCol = player.GetComponent<Collider>();

        // 2. Tính bán kính (Lấy chiều x hoặc z lớn nhất của bounds)
        float targetRadius = targetCol != null ? Mathf.Max(targetCol.bounds.extents.x, targetCol.bounds.extents.z) : 1.0f;
        float playerRadius = playerCol != null ? Mathf.Max(playerCol.bounds.extents.x, playerCol.bounds.extents.z) : 0.5f;

        // 3. Khoảng cách an toàn = Bán kính địch + Bán kính mình + 0.5m (cách ra 1 chút cho đẹp)
        float safeDistance = targetRadius + playerRadius + 0.5f;

        // Tính đích đến: Xuyên qua kẻ địch một đoạn bằng safeDistance
        Vector3 dashDestination = target.transform.position + dirToTarget * safeDistance;

        // Dịch chuyển
        player.transform.position = dashDestination;

        // Hướng nhìn ngược lại hướng lướt (Nhìn lại kẻ địch)
        Vector3 lookBackDir = -dirToTarget;
        player.ForceFaceDirection(lookBackDir); // Gọi hàm mới tạo trong PlayerController

        // Chờ 0.5s THỜI GIAN THỰC để người chơi ngắm tư thế ngầu
        yield return new WaitForSecondsRealtime(0.5f);

        // --- PHASE 2: TRẢM ---
        //if (slashVfxPrefab) Instantiate(slashVfxPrefab, target.transform.position, Quaternion.LookRotation(-dirToTarget));

        WeaponData currentWpn = player.GetComponent<EquipmentManager>().currentWeapon;

        // Đòn Bạt kiếm chắc chắn Crit và xuyên giáp
        bool isCrit = true;
        float t = 1.0f; // Coi như chém lén sau lưng (t=1)

        var dmgTuple = CombatMath.CalculateFullDamage(
            stats, target, t, isCrit, data, currentWpn, data.skillPhysicalMultiplier, true // true = Xuyên giáp
        );

        DamageInfo counterInfo = new DamageInfo();
        counterInfo.sourcePosition = transform.position;
        counterInfo.isCrit = isCrit;
        counterInfo.physDamage = dmgTuple.phys;
        counterInfo.magicDamage = dmgTuple.magic;
        counterInfo.trueDamage = dmgTuple.trueDmg;

        target.TakeDamage(counterInfo);

        // Gắn Debuff giảm sát thương
        StartCoroutine(ApplyDamageDebuff(target));

        // Chờ thêm 0.3s xem ngầu trước khi nhả Time Stop
        yield return new WaitForSecondsRealtime(0.3f);

        // --- PHASE 3: KẾT THÚC ---
        // Tôn trọng UIPauseManager: nếu UI đang mở thì giữ pause (0), không ép 1 (tránh unpause oan).
        Time.timeScale = UIPauseManager.ResumeTimeScale;
        Time.fixedDeltaTime = 0.02f; // Trả lại vật lý bình thường

        stats.isInvincible = false;
        player.isAttacking = false;
    }

    private IEnumerator ApplyDamageDebuff(Stats target)
    {
        if (target != null && target.currentHp > 0)
        {
            Debug.Log($"<color=purple>Duelist: {target.name} bị suy yếu, giảm 50% Sát thương!</color>");

            target.damageOutputMultiplier -= damageReductionPercent; // Trừ đi 0.5

            //GameObject vfx = null;
            //if (debuffVfxPrefab) vfx = Instantiate(debuffVfxPrefab, target.transform);

            // Chờ thời gian THỰC (Phòng trường hợp TimeScale bị đổi bởi skill khác)
            yield return new WaitForSecondsRealtime(debuffDuration);

            if (target != null)
            {
                target.damageOutputMultiplier += damageReductionPercent; // Trả lại 0.5
                //if (vfx) Destroy(vfx);
                Debug.Log($"<color=gray>Duelist: {target.name} đã phục hồi sức mạnh.</color>");
            }
        }
    }
}