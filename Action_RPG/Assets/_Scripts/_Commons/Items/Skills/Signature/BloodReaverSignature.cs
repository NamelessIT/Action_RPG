using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BloodReaverSignature : SkillBehavior
{
    [Header("Explosion Settings")]
    public float explosionRadius = 4.0f;
    [Tooltip("Sát thương = Máu hiến tế * Hệ số này")]
    public float damagePerSacrificedHp = 2.0f;

    [Header("Frenzy State Settings")]
    public float frenzyDuration = 5.0f;
    public float healRestorePercent = 0.5f; // Đặt lại 50% máu khi kết thúc

    [Header("VFX")]
    public GameObject bloodNovaVfxPrefab;
    public GameObject invincibleAuraVfx;

    private EquipmentManager equipmentManager;
    private BloodReaverPassive bloodPassive;
    private Coroutine frenzyCoroutine;
    private GameObject currentAuraVfx;

    // [LIFECYCLE] để EndFrenzy idempotent + nhả khóa đúng nguồn
    private bool _frenzyActive = false;
    private bool _healingBlockPushed = false;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        base.Initialize(myStats, myData, myPlayer);
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        bloodPassive = myPlayer.GetComponent<BloodReaverPassive>();
    }

    protected override void OnEquip() { }

    protected override void OnUnequip()
    {
        // Gỡ an toàn nếu tháo skill giữa chừng
        if (frenzyCoroutine != null) { StopCoroutine(frenzyCoroutine); frenzyCoroutine = null; }
        EndFrenzy(false);
    }

    public override bool Use()
    {
        if (!base.Use()) return false;

        // Nếu đang frenzy → kết thúc lượt cũ AN TOÀN (revert state) trước khi bắt đầu lượt mới.
        // Trước đây chỉ StopCoroutine → leak: khóa hồi máu/bất tử/passive nhân đôi không được revert.
        if (frenzyCoroutine != null) { StopCoroutine(frenzyCoroutine); frenzyCoroutine = null; }
        if (_frenzyActive) EndFrenzy(false);

        frenzyCoroutine = StartCoroutine(SignatureRoutine());

        return true;
    }

    private IEnumerator SignatureRoutine()
    {
        // 1. KHÓA SKILL VÀ HỒI MÁU
        _frenzyActive = true;
        player.isSkillBlocked = true;
        stats.PushHealingBlock(); _healingBlockPushed = true;

        // 2. HIẾN TẾ MÁU
        float oldHp = stats.currentHp;
        float sacrificedHp = oldHp - 1f;

        if (sacrificedHp < 0) sacrificedHp = 0; // An toàn nếu HP đang <= 1

        stats.currentHp = 1f;
        Debug.Log($"<color=red>BLOOD NOVA!</color> Hiến tế {sacrificedHp} HP. Đã khóa Skill và Hồi máu.");

        // 3. GÂY SÁT THƯƠNG NỔ AOE
        //if (bloodNovaVfxPrefab != null) Instantiate(bloodNovaVfxPrefab, transform.position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, player.dangerLayer);
        float explosionDamage = sacrificedHp * damagePerSacrificedHp;

        foreach (var hit in hits)
        {
            Stats enemyStats = hit.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                DamageInfo info = new DamageInfo();
                info.sourcePosition = transform.position;
                info.physDamage = explosionDamage;
                info.isCrit = false;
                info.isKnockback = true;
                info.knockbackForce = 8f;
                info.isStun = true;
                info.stunDuration = 0.5f;
                info.attacker = stats;

                enemyStats.TakeDamage(info);
            }
        }

        // 4. KÍCH HOẠT BẤT TỬ & NHÂN ĐÔI PASSIVE
        stats.isInvincible = true;
        //if (invincibleAuraVfx != null) currentAuraVfx = Instantiate(invincibleAuraVfx, transform);

        if (bloodPassive != null)
        {
            bloodPassive.physDmgPer1PercentLost *= 2f;
            bloodPassive.bonusCritChance *= 2f;
            bloodPassive.bonusCritMultiplier *= 2f;
        }

        if (stats is AllyStats allyStats) allyStats.CalculateCombatStatsOnly();

        // 5. CHỜ 5 GIÂY
        yield return new WaitForSeconds(frenzyDuration);

        // 6. KẾT THÚC
        EndFrenzy(true);
    }

    private void EndFrenzy(bool normalEnd)
    {
        // Idempotent: nếu không còn active thì bỏ qua (tránh revert passive/khóa 2 lần)
        if (!_frenzyActive) return;
        _frenzyActive = false;

        frenzyCoroutine = null;

        // Mở khóa Skill và Hồi máu (Phải mở TRƯỚC khi xử lý logic hồi 50% bên dưới)
        player.isSkillBlocked = false;
        // Nhả khóa hồi máu CHỈ KHI skill này đã push (tránh pop dư của nguồn khác)
        if (_healingBlockPushed) { stats.PopHealingBlock(); _healingBlockPushed = false; }

        // Xóa Bất Tử và Aura
        stats.isInvincible = false;
        //if (currentAuraVfx != null) Destroy(currentAuraVfx);

        // Chia đôi trả lại hệ số Passive
        if (bloodPassive != null)
        {
            bloodPassive.physDmgPer1PercentLost /= 2f;
            bloodPassive.bonusCritChance /= 2f;
            bloodPassive.bonusCritMultiplier /= 2f;
        }

        if (stats is AllyStats allyStats) allyStats.CalculateCombatStatsOnly();

        // Xử lý Hồi Máu 50% (Đảm bảo chỉ diễn ra nếu skill chạy trọn vẹn 5s)
        if (normalEnd)
        {
            float targetHp = stats.maxHp * healRestorePercent;
            // Chỉ hồi nếu máu hiện tại đang thấp hơn 50%
            if (stats.currentHp < targetHp)
            {
                // Khóa hồi máu của signature này đã được Pop ở trên; finisher heal dùng Heal() trực tiếp.
                float amountToHeal = targetHp - stats.currentHp;
                stats.Heal(amountToHeal, true, false, HealSource.Skill);
                Debug.Log($"<color=green>Kết thúc Huyết Tế: Đặt lại máu về {targetHp} HP.</color>");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}