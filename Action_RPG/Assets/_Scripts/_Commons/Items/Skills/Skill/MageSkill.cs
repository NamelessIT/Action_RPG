using UnityEngine;
using System.Collections;

/// <summary>
/// Skill có đòn đánh cường hóa THAY THẾ đòn đánh thường.
/// RangedAttackHandler hỏi cờ này để KHÔNG bắn đạn thường khi đòn này sẽ bị cường hóa tiêu hao.
/// </summary>
public interface IEmpoweredAttackProvider
{
    bool WillEmpowerAttack(bool isHeavy);
}

/// <summary>
/// MageSkill — "Arcane Empowerment".
/// Kích hoạt → nhận buff 5s. Đòn đánh thường KẾ TIẾP trong 5s sẽ được cường hóa theo hướng
/// di chuyển (khi cầm Grimoire), rồi xóa buff. Toàn bộ logic 8 hướng nằm trong
/// <see cref="ArcaneEmpowerment"/> (dùng chung với TricksterSkill).
/// </summary>
public class MageSkill : SkillBehavior, IEmpoweredAttackProvider
{
    [Header("Buff")]
    public float buffDuration = 5.0f;

    [Header("Đòn cường hóa 8 hướng (dùng chung)")]
    public ArcaneEmpowerment empower = new ArcaneEmpowerment();

    // --- State & refs ---
    private bool isEmpowered = false;
    private int consumedFrame = -1; // frame buff bị tiêu hao (để MagePassive cùng dispatch bỏ qua)
    private Coroutine buffCoroutine;

    private PlayerController playerController;
    private EquipmentManager equipmentManager;
    private MagePassive magePassive;
    private bool isGrimoireEquipped = false;
    private EmpowerDirection currentDirection = EmpowerDirection.None;

    /// <summary>
    /// MagePassive hỏi cờ này để ƯU TIÊN skill: true khi buff đang bật hoặc vừa tiêu hao
    /// NGAY trong frame này (an toàn với mọi thứ tự gọi sự kiện).
    /// </summary>
    public bool ShouldSuppressPassive => isEmpowered || consumedFrame == Time.frameCount;

    public bool WillEmpowerAttack(bool isHeavy) => isEmpowered && !isHeavy;

    public override void Initialize(AllyStats myStats, SkillData myData, PlayerController myPlayer)
    {
        // Gán refs TRƯỚC base.Initialize: base gọi OnEquip() (đăng ký sự kiện) ngay trong nó.
        playerController = myPlayer;
        equipmentManager = myPlayer.GetComponent<EquipmentManager>();
        magePassive = myPlayer.GetComponent<MagePassive>();
        base.Initialize(myStats, myData, myPlayer);

        empower.Setup(this, stats, playerController.dangerLayer, magePassive);
    }

    protected override void OnEquip()
    {
        if (playerController != null)
        {
            playerController.OnMovementInputChanged += OnMovementInput;
            playerController.OnAttackPerformed += OnAttackPerformed;
            playerController.OnWeaponEquipped += OnWeaponEquipped;
        }
        CheckGrimoireEquipped(equipmentManager?.currentWeapon);
    }

    protected override void OnUnequip()
    {
        if (playerController != null)
        {
            playerController.OnMovementInputChanged -= OnMovementInput;
            playerController.OnAttackPerformed -= OnAttackPerformed;
            playerController.OnWeaponEquipped -= OnWeaponEquipped;
        }
        RemoveBuff();
    }

    // ==========================================================
    // INPUT & VŨ KHÍ
    // ==========================================================
    private void OnMovementInput(Vector2 input)
    {
        currentDirection = ArcaneEmpowerment.DirectionFromInput(input, currentDirection);
    }

    private void OnWeaponEquipped(WeaponData weapon) => CheckGrimoireEquipped(weapon);

    private void CheckGrimoireEquipped(WeaponData weapon)
    {
        // Ưu tiên trạng thái của nội tại (đã tính bypass); fallback theo weaponType.
        if (magePassive != null) isGrimoireEquipped = magePassive.IsGrimoireActive;
        // WPN_GR_T4_04: tính như vũ khí khác Grimoire → không kích hoạt MageSkill.
        else isGrimoireEquipped = (weapon != null && weapon.weaponType == WeaponData.WeaponType.Grimoire
                                   && (weapon.id == null || weapon.id.Trim() != "WPN_GR_T4_04"));
    }

    // ==========================================================
    // KÍCH HOẠT (BẬT BUFF)
    // ==========================================================
    public override bool Use()
    {
        if (!base.Use()) return false;

        CheckGrimoireEquipped(equipmentManager?.currentWeapon);

        isEmpowered = true;
        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(BuffTimeoutRoutine());

        Debug.Log("<color=cyan>MAGE SKILL: BẬT CƯỜNG HÓA!</color>");
        return true;
    }

    private IEnumerator BuffTimeoutRoutine()
    {
        yield return new WaitForSeconds(buffDuration);
        RemoveBuff();
        Debug.Log("<color=gray>MAGE SKILL: Hết cường hóa.</color>");
    }

    private void RemoveBuff()
    {
        isEmpowered = false;
        if (buffCoroutine != null) { StopCoroutine(buffCoroutine); buffCoroutine = null; }
    }

    // ==========================================================
    // XẢ BUFF KHI ĐÁNH
    // ==========================================================
    private void OnAttackPerformed(int stepIndex, bool isHeavy)
    {
        if (!isEmpowered || isHeavy) return; // chỉ cường hóa đòn đánh THƯỜNG
        consumedFrame = Time.frameCount;     // đánh dấu frame tiêu hao để passive bỏ qua
        RemoveBuff();

        empower.Execute(currentDirection, isGrimoireEquipped);
    }
}
