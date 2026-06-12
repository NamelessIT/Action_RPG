using UnityEngine;
using System.Collections;

/// <summary>
/// Catalyst Core Mechanic handler.
/// CM1: Player và Companion cùng tham gia hạ gục trong 3s → cả 2 +20% moveSpeed trong 3s.
///      Companion AI cần báo về thông qua sự kiện OnCompanionKillAssist.
/// CM2: Tăng sát thương Companion lên kẻ địch bị Mark từ Passive lên 50%.
///      CatalystPassive / CompanionAI cần đọc stats.catalystCM2_MarkDmgBonus.
/// CM3: Heal áp lên Player cũng áp lên Companion.
///      Heal logic cần kiểm tra stats.catalystCM3_HealShareCompanion.
/// U1: +20% thời gian debuff của Skill.
/// U2: −1s Cooldown.
/// U3: +20% scale damage của Skill.
/// </summary>
public class CatalystCoreMechanic : ClassCoreMechanicBase
{
    public override string ClassName => "Catalyst";

    private bool _cm1Active;

    // CM1: track damage timestamps
    private float _playerLastDmgTime = -99f;
    private float _companionLastDmgTime = -99f;
    private const float JointWindow = 3f;
    private const float BuffDuration = 3f;
    private bool _cm1BuffActive;

    // Reference to companion (optional, may be null)
    private AllyStats _companionStats;

    public override void Apply(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1:
                stats.catalystCM1_JointKillBuff = true;
                _cm1Active = true;
                // Lắng nghe kill event
                stats.OnKillEnemy += OnKillEnemy_CM1;
                Debug.Log("[CatalystCM1] Joint kill → +20% MoveSpeed 3s.");
                break;

            case 2:
                stats.catalystCM2_MarkDmgBonus = 0.5f; // +50%
                Debug.Log("[CatalystCM2] Companion +50% dmg trên mục tiêu bị Mark. (CompanionAI cần đọc flag)");
                break;

            case 3:
                stats.catalystCM3_HealShareCompanion = true;
                Debug.Log("[CatalystCM3] Heal Player → heal Companion 5%. (Heal logic cần implement)");
                break;
        }
    }

    public override void Revert(int index)
    {
        if (stats == null) return;
        switch (index)
        {
            case 1:
                stats.catalystCM1_JointKillBuff = false;
                _cm1Active = false;
                stats.OnKillEnemy -= OnKillEnemy_CM1;
                if (_cm1BuffActive) { stats.bonusMoveSpeed -= 0.20f; _cm1BuffActive = false; }
                break;
            case 2:
                stats.catalystCM2_MarkDmgBonus = 0f;
                break;
            case 3:
                stats.catalystCM3_HealShareCompanion = false;
                break;
        }
    }

    // Companion báo về rằng nó đã gây damage gần đây
    public void NotifyCompanionDealtDamage() => _companionLastDmgTime = Time.time;
    public void SetCompanionStats(AllyStats companion) => _companionStats = companion;

    private void OnKillEnemy_CM1(Stats victim, bool isBackstab)
    {
        if (!_cm1Active) return;
        float now = Time.time;
        _playerLastDmgTime = now; // player vừa kill = player vừa hit

        bool companionParticipated = (now - _companionLastDmgTime) <= JointWindow;
        if (companionParticipated)
        {
            StartCoroutine(ApplyJointKillBuff());
        }
    }

    private IEnumerator ApplyJointKillBuff()
    {
        if (_cm1BuffActive) yield break;
        _cm1BuffActive = true;

        stats.bonusMoveSpeed += 0.20f;
        if (_companionStats != null) _companionStats.bonusMoveSpeed += 0.20f;
        Debug.Log("[CatalystCM1] Joint kill! +20% MoveSpeed 3s.");

        yield return new WaitForSeconds(BuffDuration);

        stats.bonusMoveSpeed -= 0.20f;
        if (_companionStats != null) _companionStats.bonusMoveSpeed -= 0.20f;
        _cm1BuffActive = false;
    }

    public override void ApplyUpgrade(int index)
    {
        base.ApplyUpgrade(index); // U2: -1s CD
        if (index == 1 && stats != null) { stats.catalystSkillU1 += 0.20f; Debug.Log($"[CatalystU1] +20% stun duration (total {stats.catalystSkillU1:F1})"); }
        if (index == 3 && stats != null) { stats.catalystSkillU3 += 0.20f; Debug.Log($"[CatalystU3] +20% scale damage (total {stats.catalystSkillU3:F1})"); }
    }

    public override void RevertUpgrade(int index)
    {
        base.RevertUpgrade(index);
        if (index == 1 && stats != null) stats.catalystSkillU1 = Mathf.Max(0f, stats.catalystSkillU1 - 0.20f);
        if (index == 3 && stats != null) stats.catalystSkillU3 = Mathf.Max(0f, stats.catalystSkillU3 - 0.20f);
    }
}
