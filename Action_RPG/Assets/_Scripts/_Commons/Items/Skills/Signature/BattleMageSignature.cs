using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleMageSignature : SkillBehavior
{
    [Header("Dark Zone Settings")]
    public float duration = 10.0f;          // Tồn tại 10 giây
    public float tickRate = 0.5f;           // Rút máu mỗi 0.5 giây
    public float radius = 6.0f;             // Bán kính vùng tối
    //public float damageMultiplierPerTick = 0.4f; // Sát thương mỗi nhịp (40% sức mạnh)
    public float slowPercent = 0.4f;        // Làm chậm 40%

    [Header("Shield Settings")]
    public float shieldConversionRate = 0.5f; // 50% sát thương thành giáp ảo
    public float shieldDuration = 10.0f;      // Giáp tồn tại 10s sau lần hút cuối cùng

    [Header("VFX")]
    public GameObject darkZoneVfxPrefab;    // Hiệu ứng vùng tối lốm đốm dưới chân
    public GameObject shieldVfxPrefab;      // Hiệu ứng bong bóng giáp quanh người

    private EquipmentManager equipmentManager;
    private Coroutine zoneCoroutine;

    private GameObject currentZoneVfx;
    private GameObject currentShieldVfx;

    // --- Quản lý Trạng thái ---
    private float shieldTimer = 0f;
    private float totalGrantedShield = 0f; // Theo dõi tổng lượng giáp đã cấp để thu hồi cho đúng

    /// <summary>Slow được refresh mỗi tick nên chỉ cần sống hơi lâu hơn 1 nhịp tick.</summary>
    private float SlowRefreshDuration => Mathf.Max(0.2f, tickRate * 1.5f);

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        // Tháo skill thì dọn dẹp sạch sẽ
        if (zoneCoroutine != null) StopCoroutine(zoneCoroutine);
        CleanUpZone();
        RemoveShield();
    }

    void Update()
    {
        // Quản lý thời gian tồn tại của Giáp ảo độc lập với vùng tối
        if (shieldTimer > 0)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0)
            {
                RemoveShield();
            }
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        if (zoneCoroutine != null) StopCoroutine(zoneCoroutine);
        zoneCoroutine = StartCoroutine(ZoneRoutine());

        return true;
    }

    private IEnumerator ZoneRoutine()
    {
        // 1. Kích hoạt VFX Vùng Tối (Đặt làm Child để đi theo người chơi)
        //if (darkZoneVfxPrefab != null)
        //{
        //    if (currentZoneVfx != null) Destroy(currentZoneVfx);
        //    currentZoneVfx = Instantiate(darkZoneVfxPrefab, transform);
        //}

        Debug.Log("<color=purple>BATTLE MAGE: MỞ RỘNG TRƯỜNG NHẬT THỰC!</color>");

        // 2. Vòng lặp Rút máu và Làm chậm
        float timer = 0f;
        while (timer < duration)
        {
            ProcessZoneTick();
            yield return new WaitForSeconds(tickRate);
            timer += tickRate;
        }

        // 3. Hết 10 giây -> Xóa vùng tối
        CleanUpZone();
    }

    private void ProcessZoneTick()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, player.dangerLayer);

        float totalDamageDealtThisTick = 0f;

        stats.EnterCombat();
        WeaponData currentWpn = equipmentManager != null ? equipmentManager.currentWeapon : null;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                // --- A. GÂY SÁT THƯƠNG RÚT MÁU ---
                float hpBefore = enemyStats.currentHp;
                DamageHelper.ApplyStandardDamage(stats, enemyStats, transform, data.skillMagicMultiplier, data, currentWpn, 0, sourceType: DamageSourceType.DoT);
                totalDamageDealtThisTick += Mathf.Max(0f, hpBefore - enemyStats.currentHp);

                // --- B. LÀM CHẬM ---
                // Refresh mỗi tick, duration > tickRate. Địch rời vùng thì nguồn Slow này tự hết hạn,
                // không cần list tracking / restore thủ công (strongest-wins, expiry-safe).
                enemyStats.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, SlowRefreshDuration) { magnitude = slowPercent }, stats);
            }
        }

        // --- C. CỘNG DỒN GIÁP ẢO VÀ RESET TIMER ---
        if (totalDamageDealtThisTick > 0)
        {
            float newShield = totalDamageDealtThisTick * shieldConversionRate;
            stats.currentShield += newShield;
            totalGrantedShield += newShield; // Lưu lại để lúc hết giờ biết đường thu hồi

            // [QUAN TRỌNG] Reset lại thời gian 10 giây
            shieldTimer = shieldDuration;

            // Bật hiệu ứng giáp nếu chưa có
            //if (currentShieldVfx == null && shieldVfxPrefab != null)
            //{
            //    currentShieldVfx = Instantiate(shieldVfxPrefab, transform);
            //}

            Debug.Log($"<color=cyan>Hấp thụ {newShield} Giáp ảo. Tổng giáp: {stats.currentShield} (Timer Reset: 10s)</color>");
        }
    }

    private void CleanUpZone()
    {
        if (currentZoneVfx != null) Destroy(currentZoneVfx);

        // Không cần gỡ Slow thủ công: nguồn Slow do zone cấp tự hết hạn sau SlowRefreshDuration.
        zoneCoroutine = null;
        Debug.Log("<color=gray>BattleMage: Lãnh địa đã khép lại.</color>");
    }

    private void RemoveShield()
    {
        if (totalGrantedShield > 0)
        {
            // Trừ đi đúng lượng giáp mà skill này đã buff (Không làm âm phần giáp của skill khác nếu có)
            stats.currentShield = Mathf.Max(0, stats.currentShield - totalGrantedShield);
            totalGrantedShield = 0;
            shieldTimer = 0;

            if (currentShieldVfx != null) Destroy(currentShieldVfx);
            Debug.Log("<color=gray>BattleMage: Đã hết thời gian duy trì Giáp Ảo, giáp vỡ!</color>");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}