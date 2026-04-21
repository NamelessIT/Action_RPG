using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VanguardLiteSignature : SkillBehavior
{
    [Header("Spin Settings")]
    public float duration = 4.0f;         // Xoay trong 4 giây
    public float tickRate = 0.5f;         // Mỗi 0.5s gây sát thương 1 lần
    public float spinRadius = 3.0f;       // Tầm vung khiên
    public float moveSpeedPenalty = 0.5f; // Trừ 50% bonusMoveSpeed

    [Header("Damage Scaling")]
    public float vitScaling = 2.0f;       // Sát thương cộng thêm = VIT x 2
    public float armorScaling = 1.0f;     // + Armor x 1
    public float mrScaling = 1.0f;        // + MagicResist x 1
    public float damageIncreasePerHit = 0.2f; // Trúng càng nhiều càng đau (+20% mỗi hit dính liên tục)

    [Header("CC Settings (3rd Hit)")]
    public int hitsToStun = 3;            // Trúng 3 lần thì bị CC
    public float stunDuration = 1.5f;
    public float knockbackForce = 5f;

    [Header("VFX")]
    public GameObject spinVfxPrefab;      // Lốc xoáy/gió lốc bao quanh người

    private EquipmentManager equipmentManager;
    private Coroutine spinCoroutine;
    private GameObject currentVfx;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        // Gỡ an toàn nếu tháo skill giữa chừng
        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
            CleanUpSpinState();
        }
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        if (spinCoroutine != null) StopCoroutine(spinCoroutine);
        spinCoroutine = StartCoroutine(SpinRoutine());

        return true;
    }

    private IEnumerator SpinRoutine()
    {
        // 1. Setup trạng thái: Khóa đánh, khóa skill nhưng VẪN ĐƯỢC ĐI LẠI
        player.isStance = true;
        player.isSkillBlocked = true;

        // 2. Trừ 50% tốc chạy
        stats.bonusMoveSpeed -= moveSpeedPenalty;
        if (stats is AllyStats ally) ally.CalculateMoveSpeedOnly();

        //if (spinVfxPrefab) currentVfx = Instantiate(spinVfxPrefab, transform);

        Debug.Log("<color=cyan>VANGUARD: SHIELD CYCLONE!</color>");

        // 3. Khởi tạo bộ nhớ đếm số Hit của từng kẻ địch
        Dictionary<Stats, int> hitTracker = new Dictionary<Stats, int>();

        // 4. Vòng lặp xoay khiên
        float timer = 0f;
        while (timer < duration)
        {
            ProcessSpinTick(hitTracker);
            yield return new WaitForSeconds(tickRate);
            timer += tickRate;
        }

        // 5. Kết thúc
        CleanUpSpinState();
    }

    private void ProcessSpinTick(Dictionary<Stats, int> hitTracker)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, spinRadius, player.dangerLayer);

        foreach (var hit in hits)
        {
            Stats enemy = hit.GetComponent<Stats>();
            if (enemy != null && enemy.currentHp > 0)
            {
                // Cập nhật số lần trúng đòn
                if (!hitTracker.ContainsKey(enemy)) hitTracker[enemy] = 0;
                hitTracker[enemy]++;
                int currentHits = hitTracker[enemy];

                // --- TÍNH TOÁN SÁT THƯƠNG SCALE THEO CHỈ SỐ THỦ ---
                float scalingDmg = (stats.VIT * vitScaling) + (stats.armor * armorScaling) + (stats.magicResist * mrScaling);

                // Quy đổi lượng sát thương cộng thêm thành Multiplier để đưa vào CombatMath
                float standardDmg = stats.physicalAtk * data.skillPhysicalMultiplier;
                float totalRaw = standardDmg + scalingDmg;
                float baseMultiplier = totalRaw / stats.physicalAtk;

                // Khuếch đại sát thương dựa trên số hit (VD: Hit 1 = x1, Hit 2 = x1.2, Hit 3 = x1.4)
                float damageMultiplier = baseMultiplier * (1.0f + (currentHits - 1) * damageIncreasePerHit);

                stats.EnterCombat();
                WeaponData currentWpn = equipmentManager.currentWeapon;
                float totalCritChance = stats.critChance + (currentWpn != null ? currentWpn.bonusCritChance : 0);
                bool isCrit = CombatMath.CheckIsCrit(totalCritChance);

                // Tính sát thương xuyên qua giáp địch
                var dmgTuple = CombatMath.CalculateFullDamage(
                    stats, enemy, 1.0f, isCrit, data, currentWpn, damageMultiplier
                );

                // --- ÁP DỤNG DAMAGE & HIỆU ỨNG ---
                DamageInfo info = new DamageInfo();
                info.sourcePosition = transform.position;
                info.isCrit = isCrit;
                info.physDamage = dmgTuple.phys;
                info.magicDamage = dmgTuple.magic;
                info.trueDamage = dmgTuple.trueDmg;

                // Đủ 3 hit -> Nổ CC
                if (currentHits == hitsToStun)
                {
                    info.isKnockback = true;
                    info.knockbackForce = knockbackForce;
                    info.isStun = true;
                    info.stunDuration = stunDuration;
                    Debug.Log($"<color=orange>Vanguard Spin:</color> Đập văng {enemy.name} (Trúng 3 hit)!");
                }

                enemy.TakeDamage(info);
            }
        }
    }

    private void CleanUpSpinState()
    {
        // Hoàn trả trạng thái và tốc độ di chuyển
        //if (currentVfx) Destroy(currentVfx);

        stats.bonusMoveSpeed += moveSpeedPenalty;
        if (stats is AllyStats ally) ally.CalculateMoveSpeedOnly();

        player.isStance = false;
        player.isSkillBlocked = false;

        spinCoroutine = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, spinRadius);
    }
}