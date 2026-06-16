using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerController))]
public class MagePassive : SkillBehavior
{
    // ========================================================
    // TODO: CƠ CHẾ BYPASS VŨ KHÍ CÙNG CẤP
    // Sau này sẽ làm: Cho phép sử dụng vũ khí cùng cấp mà không cần 
    // kiểm tra các chỉ số cơ bản theo các mốc 16-26-41-51.
    // ========================================================

    [Header("--- Core ---")]
    public WeaponData.WeaponType requiredWeaponType = WeaponData.WeaponType.Grimoire;
    public bool bypassWeaponCheck = false;
    public float baseMoveSpeedBonus = 0.05f;
    public float xpBonusPercent = 0.1f;

    [Header("--- N (Fire) ---")]
    public float fireballCooldown = 20f;
    public float fireballRadius = 2f;
    public float burnDuration = 2f;

    [Header("--- E (Wind) ---")]
    public float windStackInterval = 2f;
    public int windMaxStacks = 3;
    public float windSpeedPerStack = 0.036f;

    [Header("--- S (Ice) ---")]
    public float chillDuration = 5f;
    public int maxChillStacks = 7;
    public float freezeStunDuration = 3f;
    public float chillImmuneDuration = 5f;

    [Header("--- W (Earth) ---")]
    public float fractureDuration = 5f;
    public int maxFractureStacks = 3;
    public float defReducePercent = 0.15f;

    [Header("--- NW (Meteor) & SW (Ice Wall) ---")]
    public float meteorCooldown = 20f;
    public float iceWallCooldown = 20f;

    // Runtime state
    private bool isGrimoireEquipped = false;
    private Direction8 currentDirection = Direction8.None;
    private EquipmentManager eqManager;

    // Tham chiếu tới MageSkill (cùng GameObject) để nhường ưu tiên khi đang có buff cường hóa.
    // Lazy-fetch: skill có thể được trang bị/tháo động; '== null' của Unity bắt cả object đã hủy.
    private MageSkill mageSkill;
    private MageSkill MageSkillRef
    {
        get { if (mageSkill == null) mageSkill = GetComponent<MageSkill>(); return mageSkill; }
    }

    // Cooldown timers
    private float timerN = 0f;
    private float timerSW = 0f;
    private float timerNW = 0f;

    // Cờ: swing hiện tại có phóng kĩ năng đặc biệt không (để đòn cast không áp luôn hiệu ứng fallback)
    private bool castNThisSwing = false;
    private bool castSWThisSwing = false;
    private bool castNWThisSwing = false;

    // E - Wind variables
    private float windTimer = 0f;
    private int windStacks = 0;
    private float activeWindSpeedBonus = 0f;

    // Dictionaries for debuffs
    private Dictionary<Stats, ChillData> chillDict = new Dictionary<Stats, ChillData>();
    private Dictionary<Stats, FractureData> fracDict = new Dictionary<Stats, FractureData>();
    private Dictionary<Stats, float> immuneDict = new Dictionary<Stats, float>();
    private Dictionary<Stats, float> atkSlowDict = new Dictionary<Stats, float>(); // Chống cộng dồn Tốc đánh

    private enum Direction8 { None = 0, N = 1, NE = 2, E = 3, SE = 4, S = 5, SW = 6, W = 7, NW = 8 }

    private class ChillData { public int stacks; public float expireTime; }
    private class FractureData { public int stacks; public float expireTime; public bool isReduced; public float armorReduc; public float mrReduc; }

    // ========================================================
    // INITIALIZATION & CLEANUP
    // ========================================================
    protected override void OnEquip()
    {
        player = GetComponent<PlayerController>();
        eqManager = GetComponent<EquipmentManager>();

        player.OnMovementInputChanged += OnMovementInput;
        player.OnAttackPerformed += OnAttackPerformed;
        player.OnHitEnemy += OnHitEnemy;

        if (eqManager != null) eqManager.OnEquipmentChanged += OnEquipmentChanged;

        CheckGrimoireEquipped(eqManager?.currentWeapon);
    }

    protected override void OnUnequip()
    {
        if (player != null)
        {
            player.OnMovementInputChanged -= OnMovementInput;
            player.OnAttackPerformed -= OnAttackPerformed;
            player.OnHitEnemy -= OnHitEnemy;
        }

        if (eqManager != null) eqManager.OnEquipmentChanged -= OnEquipmentChanged;

        if (isGrimoireEquipped)
        {
            ApplyBaseGrimoireBuffs(false);
            isGrimoireEquipped = false;
        }

        RestoreAllFracturedStats();
        RestoreAllAtkSpeed();
        chillDict.Clear();
        fracDict.Clear();
        immuneDict.Clear();
        atkSlowDict.Clear();
    }

    private void Update()
    {
        if (!isGrimoireEquipped) return;

        // 1. Tick Cooldowns
        if (timerN > 0) timerN -= Time.deltaTime;
        if (timerSW > 0) timerSW -= Time.deltaTime;
        if (timerNW > 0) timerNW -= Time.deltaTime;

        // 2. Track E (Wind) Stacks
        if (currentDirection == Direction8.E || currentDirection == Direction8.NE || currentDirection == Direction8.SE)
        {
            windTimer += Time.deltaTime;
            if (windTimer >= windStackInterval)
            {
                windTimer -= windStackInterval;
                if (windStacks < windMaxStacks)
                {
                    windStacks++;
                    UpdateWindSpeed();
                    Debug.Log($"<color=green>[Mage-E]</color> Lướt gió: {windStacks}/{windMaxStacks} stacks!");
                }
            }
        }

        // 3. Clear Expired Buffs/Debuffs
        List<Stats> chillKeys = new List<Stats>(chillDict.Keys);
        foreach (var k in chillKeys)
            if (k == null || k.currentHp <= 0 || Time.time > chillDict[k].expireTime) chillDict.Remove(k);

        List<Stats> fracKeys = new List<Stats>(fracDict.Keys);
        foreach (var k in fracKeys)
            if (k == null || k.currentHp <= 0 || Time.time > fracDict[k].expireTime)
            {
                RestoreSingleFracture(k);
                fracDict.Remove(k);
            }

        List<Stats> immuneKeys = new List<Stats>(immuneDict.Keys);
        foreach (var k in immuneKeys)
            if (k == null || k.currentHp <= 0 || Time.time > immuneDict[k]) immuneDict.Remove(k);

        List<Stats> slowKeys = new List<Stats>(atkSlowDict.Keys);
        foreach (var k in slowKeys)
            if (k == null || k.currentHp <= 0 || Time.time > atkSlowDict[k])
            {
                RestoreSingleAtkSpeed(k);
                atkSlowDict.Remove(k);
            }
    }

    // ========================================================
    // SYSTEM HOOKS
    // ========================================================
    private void OnEquipmentChanged()
    {
        if (eqManager != null) CheckGrimoireEquipped(eqManager.currentWeapon);
    }

    private void CheckGrimoireEquipped(WeaponData weapon)
    {
        bool wasEquipped = isGrimoireEquipped;

        if (bypassWeaponCheck) isGrimoireEquipped = true;
        // WPN_GR_T4_04: là Grimoire nhưng KHÔNG kích hoạt hiệu ứng yêu cầu Grimoire (tính như vũ khí khác).
        else isGrimoireEquipped = (weapon != null && weapon.weaponType == requiredWeaponType
                                   && (weapon.id == null || weapon.id.Trim() != "WPN_GR_T4_04"));

        if (isGrimoireEquipped && !wasEquipped)
        {
            ApplyBaseGrimoireBuffs(true);
            Debug.Log("<color=cyan>[MagePassive]</color> Đã trang bị Grimoire. Kích hoạt 8 Hướng!");
        }
        else if (!isGrimoireEquipped && wasEquipped)
        {
            ApplyBaseGrimoireBuffs(false);
            Debug.Log("<color=cyan>[MagePassive]</color> Đã tháo Grimoire. Hủy 8 Hướng!");
        }
    }

    private void ApplyBaseGrimoireBuffs(bool isEquipping)
    {
        float sign = isEquipping ? 1f : -1f;
        stats.bonusMoveSpeed += baseMoveSpeedBonus * sign;

        if (!isEquipping && activeWindSpeedBonus > 0)
        {
            stats.bonusMoveSpeed -= activeWindSpeedBonus;
            activeWindSpeedBonus = 0f;
            windStacks = 0;
            windTimer = 0f;
        }

        stats.RecalculateStats();
    }

    private void OnMovementInput(Vector2 input)
    {
        if (!isGrimoireEquipped) return;

        Direction8 newDir = currentDirection;
        if (input != Vector2.zero)
        {
            float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            if (angle >= 337.5f || angle < 22.5f) newDir = Direction8.E;
            else if (angle >= 22.5f && angle < 67.5f) newDir = Direction8.NE;
            else if (angle >= 67.5f && angle < 112.5f) newDir = Direction8.N;
            else if (angle >= 112.5f && angle < 157.5f) newDir = Direction8.NW;
            else if (angle >= 157.5f && angle < 202.5f) newDir = Direction8.W;
            else if (angle >= 202.5f && angle < 247.5f) newDir = Direction8.SW;
            else if (angle >= 247.5f && angle < 292.5f) newDir = Direction8.S;
            else if (angle >= 292.5f && angle < 337.5f) newDir = Direction8.SE;
        }

        if (newDir != currentDirection)
        {
            currentDirection = newDir;
            if (currentDirection != Direction8.E && currentDirection != Direction8.NE && currentDirection != Direction8.SE)
            {
                windStacks = 0;
                windTimer = 0f;
                UpdateWindSpeed();
            }
        }
    }

    private void UpdateWindSpeed()
    {
        stats.bonusMoveSpeed -= activeWindSpeedBonus;
        activeWindSpeedBonus = windStacks * windSpeedPerStack;
        stats.bonusMoveSpeed += activeWindSpeedBonus;
        stats.RecalculateStats();
    }

    // ========================================================
    // LOGIC SPAWN ABILITIES
    // ========================================================
    private void OnAttackPerformed(int stepIndex, bool isHeavy)
    {
        // CHỐT CHẶN: Chỉ áp dụng khi đánh thường
        if (!isGrimoireEquipped || isHeavy) return;

        // ƯU TIÊN SKILL: nếu MageSkill đang cường hóa (hoặc vừa tiêu hao trong frame này),
        // đòn này thuộc về skill → KHÔNG phóng đặc kĩ nội tại và KHÔNG đụng tới cooldown (timerN/SW/NW).
        MageSkill ms = MageSkillRef;
        if (ms != null && ms.ShouldSuppressPassive) return;

        // Reset cờ cast mỗi swing
        castNThisSwing = false;
        castSWThisSwing = false;
        castNWThisSwing = false;

        Vector3 spawnPos = player.transform.position + Vector3.up * 0.5f;
        Vector3 fireDir = player.attackDispatcher.BuildContext(false, 0, 0, null, 0, null, null).FacingDir;

        // Hướng N: Fireball lớn
        if (currentDirection == Direction8.N && timerN <= 0f)
        {
            timerN = fireballCooldown;
            castNThisSwing = true;
            GameObject fb = new GameObject("Mage_Fireball_N");
            fb.transform.position = spawnPos;
            var script = fb.AddComponent<MageFireball>();
            script.Init(stats, fireDir, player.dangerLayer, eqManager?.currentWeapon);
            Debug.Log($"<color=red>[Mage-N]</color> Đã phóng Cầu Lửa Lớn!");
        }

        // Hướng SW: Tường Băng
        if (currentDirection == Direction8.SW && timerSW <= 0f)
        {
            timerSW = iceWallCooldown;
            castSWThisSwing = true;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Mage_IceWall_SW";
            wall.transform.rotation = Quaternion.LookRotation(fireDir);
            wall.transform.localScale = new Vector3(0.5f, 2f, 5f); // dài 5f chạy dọc theo hướng bắn
            wall.transform.position = spawnPos + fireDir * 3.5f;    // đẩy ra trước mặt nửa chiều dài
            wall.AddComponent<MageIceWall>().Init();
            Destroy(wall, 5f);
            Debug.Log($"<color=cyan>[Mage-SW]</color> Dựng Tường Băng!");
        }

        // Hướng NW: Thiên Thạch
        if (currentDirection == Direction8.NW && timerNW <= 0f)
        {
            timerNW = meteorCooldown;
            castNWThisSwing = true;
            GameObject met = new GameObject("Mage_Meteor_NW");
            met.transform.position = spawnPos;
            var script = met.AddComponent<MageMeteor>();
            script.Init(stats, fireDir, player.dangerLayer, this, eqManager?.currentWeapon);
            Debug.Log($"<color=orange>[Mage-NW]</color> Phóng Thiên Thạch Lửa-Đất!");
        }
    }

    // ========================================================
    // LOGIC APPLY STATUS EFFECTS
    // ========================================================
    private void OnHitEnemy(Stats target, int stepIndex, bool isHeavy, bool isCrit)
    {
        // CHỐT CHẶN: Chỉ áp dụng thuộc tính lên đòn đánh thường
        if (!isGrimoireEquipped || target == null || isHeavy) return;

        float burnDmg = stats.magicAtk * 0.05f;

        switch (currentDirection)
        {
            case Direction8.N:
                // Áp dụng Burn khi đang Cooldown (không phải swing vừa phóng cầu lửa)
                if (timerN > 0f && !castNThisSwing) target.ApplyBurn(burnDmg, burnDuration);
                break;

            case Direction8.NE:
                target.ApplyBurn(burnDmg, burnDuration);
                Collider[] hits = Physics.OverlapSphere(target.transform.position, 1.5f, player.dangerLayer);
                foreach (var hit in hits)
                {
                    Stats e = hit.GetComponent<Stats>();
                    if (e != null && e != target) e.ApplyBurn(burnDmg, burnDuration);
                }
                VisualDebugHelper.DrawSphere(target.transform.position, 1.5f, new Color(1, 0.5f, 0, 0.3f), 0.5f);
                break;

            case Direction8.SE:
                ApplyChill(target);
                ApplyAtkSlow(target); // Dùng hàm riêng chống cộng dồn
                break;

            case Direction8.S:
                ApplyChill(target);
                break;

            case Direction8.SW:
                // Nếu Tường Băng đang Cooldown -> Đánh thường gây Tê tái + Rạn nứt
                if (timerSW > 0f && !castSWThisSwing) { ApplyChill(target); ApplyFracture(target); }
                break;

            case Direction8.W:
                ApplyFracture(target);
                break;

            case Direction8.NW:
                // Nếu Thiên thạch đang Cooldown -> Đánh thường gây Cháy + Rạn nứt
                if (timerNW > 0f && !castNWThisSwing) { target.ApplyBurn(burnDmg, burnDuration); ApplyFracture(target); }
                break;
        }
    }

    // ========================================================
    // DEBUFF LOGIC (CHILL, FRACTURE, SLOW)
    // ========================================================
    private void ApplyChill(Stats target)
    {
        if (immuneDict.ContainsKey(target) && Time.time < immuneDict[target]) return;

        if (!chillDict.ContainsKey(target)) chillDict[target] = new ChillData { stacks = 0 };

        // ĐÒN THỨ 8 ĐÓNG BĂNG: Chỉ nổ khi TRƯỚC ĐÓ đã có đủ 7 cộng dồn
        if (chillDict[target].stacks >= maxChillStacks)
        {
            DamageHelper.ApplyStandardDamage(stats, target, player.transform, 0f, null, null, 0, true, freezeStunDuration, sourceType: DamageSourceType.Ranged);
            chillDict.Remove(target);
            immuneDict[target] = Time.time + chillImmuneDuration;
            VisualDebugHelper.DrawBox(target.transform.position + Vector3.up, new Vector3(1, 2, 1), Quaternion.identity, new Color(0, 1, 1, 0.6f), 3f);
            Debug.Log($"<color=cyan>[Mage-S]</color> Đòn thứ 8! Đóng băng {target.name} trong 3s!");
        }
        else
        {
            // Nếu chưa đủ 7 thì tăng số đếm lên
            chillDict[target].stacks++;
            chillDict[target].expireTime = Time.time + chillDuration;
            VisualDebugHelper.DrawSphere(target.transform.position + Vector3.up * 2f, 0.3f, Color.cyan, 1f);
        }
    }

    private void ApplyFracture(Stats target)
    {
        if (!fracDict.ContainsKey(target)) fracDict[target] = new FractureData { stacks = 0, isReduced = false };

        fracDict[target].stacks = Mathf.Min(maxFractureStacks, fracDict[target].stacks + 1);
        fracDict[target].expireTime = Time.time + fractureDuration;

        if (fracDict[target].stacks == maxFractureStacks && !fracDict[target].isReduced)
        {
            fracDict[target].isReduced = true;
            fracDict[target].armorReduc = target.armor * defReducePercent;
            fracDict[target].mrReduc = target.magicResist * defReducePercent;

            target.armor -= fracDict[target].armorReduc;
            target.magicResist -= fracDict[target].mrReduc;
            VisualDebugHelper.DrawSphere(target.transform.position + Vector3.up * 0.5f, 0.8f, new Color(0.5f, 0.5f, 0.5f, 0.5f), 1f);
        }
    }

    private void RestoreSingleFracture(Stats target)
    {
        if (fracDict.ContainsKey(target) && fracDict[target].isReduced)
        {
            target.armor += fracDict[target].armorReduc;
            target.magicResist += fracDict[target].mrReduc;
        }
    }

    private void RestoreAllFracturedStats()
    {
        foreach (var k in fracDict.Keys)
            if (k != null) RestoreSingleFracture(k);
    }

    // [FIX SE]: Quản lý Giảm Tốc Đánh bằng Dictionary để không bị cộng dồn
    private void ApplyAtkSlow(Stats target)
    {
        if (!atkSlowDict.ContainsKey(target))
        {
            // Lần đầu dính -> Trừ thẳng 10%
            target.baseAttackSpeed *= 0.9f;
            atkSlowDict[target] = Time.time + 3f;
        }
        else
        {
            // Đã dính rồi -> Chỉ làm mới thời gian
            atkSlowDict[target] = Time.time + 3f;
        }
    }

    private void RestoreSingleAtkSpeed(Stats target)
    {
        if (target != null)
        {
            target.baseAttackSpeed /= 0.9f; // Phục hồi lại chỉ số gốc
        }
    }

    private void RestoreAllAtkSpeed()
    {
        foreach (var k in atkSlowDict.Keys)
            RestoreSingleAtkSpeed(k);
    }

    // Gọi bởi Thiên Thạch (NW): thiêu đốt 5s + giảm giáp/kháng phép CÓ THỜI HẠN (5s)
    // Dùng lại fracDict để cleanup tự khôi phục khi hết hạn (tránh giảm giáp vĩnh viễn)
    public void ApplyMeteorHit(Stats target)
    {
        if (target == null) return;

        target.ApplyBurn(stats.magicAtk * 0.05f, 5f);

        if (!fracDict.ContainsKey(target)) fracDict[target] = new FractureData { stacks = 0, isReduced = false };
        FractureData f = fracDict[target];
        if (!f.isReduced)
        {
            f.isReduced = true;
            f.armorReduc = target.armor * defReducePercent;
            f.mrReduc = target.magicResist * defReducePercent;
            target.armor -= f.armorReduc;
            target.magicResist -= f.mrReduc;
        }
        f.stacks = maxFractureStacks;
        f.expireTime = Time.time + fractureDuration;
    }

    public float GetXpBonusMultiplier() => isGrimoireEquipped ? (1f + xpBonusPercent) : 1f;

    // ========================================================
    // PUBLIC API — cho MageSkill (Arcane Empowerment) tái sử dụng
    // hệ cộng dồn tê tái / Fractured / miễn nhiễm của nội tại.
    // ========================================================
    public bool IsGrimoireActive => isGrimoireEquipped;
    public int MaxFractureStacks => maxFractureStacks;

    /// <summary>Đặt nhiều cộng dồn tê tái (mỗi lần gọi đi qua logic đóng băng đòn thứ 8 của nội tại).</summary>
    public void SkillAddChill(Stats target, int stacks)
    {
        if (target == null) return;
        for (int i = 0; i < stacks; i++) ApplyChill(target);
    }

    /// <summary>Đặt nhiều cộng dồn Fractured (tự kích hoạt giảm giáp khi đủ max như nội tại).</summary>
    public void SkillAddFracture(Stats target, int stacks)
    {
        if (target == null) return;
        for (int i = 0; i < stacks; i++) ApplyFracture(target);
    }

    /// <summary>
    /// W - Earthquake: giảm 30% giáp/kháng phép trong `duration` giây, sau đó về 15% của nội tại.
    /// Tính toán tập trung tại đây (nơi sở hữu fracDict) để KHÔNG bị cộng dồn sai 15%+30%=40.5%.
    /// Đúng cả khi địch ĐÃ bị giảm 15% trước lẫn khi CHƯA bị giảm.
    /// </summary>
    public void SkillApplyEarthquakeShred(Stats target, float duration)
    {
        if (target == null || target.currentHp <= 0) return;

        // 1) Đưa Fractured lên max → nội tại giảm đúng 15% (chỉ giảm 1 lần nhờ cờ isReduced).
        //    Nếu địch đã bị giảm trước thì chỉ làm mới thời gian, không giảm thêm.
        SkillAddFracture(target, maxFractureStacks);
        if (!fracDict.ContainsKey(target) || !fracDict[target].isReduced) return;

        // 2) Giảm TẠM thêm đúng bằng phần nội tại (15% của base) → tổng = 30% trong `duration`.
        StartCoroutine(EarthquakeExtraShred(target, fracDict[target].armorReduc, fracDict[target].mrReduc, duration));
    }

    private IEnumerator EarthquakeExtraShred(Stats target, float extraArmor, float extraMr, float duration)
    {
        if (target == null) yield break;
        target.armor -= extraArmor;
        target.magicResist -= extraMr;

        yield return new WaitForSeconds(duration);

        // Trả lại đúng phần TẠM đã trừ (đối xứng), giữ nguyên phần 15% của nội tại.
        if (target != null)
        {
            target.armor += extraArmor;
            target.magicResist += extraMr;
        }
    }

    /// <summary>S - Absolute Zero: đóng băng (stun) tức thì bất kể số cộng dồn, xóa cộng dồn + miễn nhiễm tê tái.</summary>
    public void SkillForceFreeze(Stats target, float stunDuration)
    {
        if (target == null || target.currentHp <= 0) return;
        DamageHelper.ApplyStandardDamage(stats, target, player.transform, 0f, null, null, 0, true, stunDuration, sourceType: DamageSourceType.Ranged);
        if (chillDict.ContainsKey(target)) chillDict.Remove(target);
        immuneDict[target] = Time.time + chillImmuneDuration;
    }
}

// ========================================================
// SUB-PROJECTILES BEHAVIORS
// ========================================================
public class MageFireball : MonoBehaviour
{
    private AllyStats attacker;
    private LayerMask enemyLayer;
    private Vector3 dir;
    private float speed = 8f;
    private WeaponData weapon;

    public void Init(AllyStats caster, Vector3 direction, LayerMask mask, WeaponData wpn)
    {
        attacker = caster; dir = direction.normalized; enemyLayer = mask; weapon = wpn;
        MageVfxHelper.AttachSphere(transform, 1f, Color.red); // VFX đi theo cầu lửa
        Destroy(gameObject, 3f);
    }

    void Update()
    {
        transform.position += dir * speed * Time.deltaTime;
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f, enemyLayer);
        if (hits.Length > 0)
        {
            // Collider địch thường nằm trên child → tìm Stats ở parent (giống GetEnemyStats)
            Stats e = hits[0].GetComponentInParent<Stats>();
            if (e != null)
            {
                // Truyền weapon (Grimoire = Magic) để gây sát thương PHÉP 200%
                DamageHelper.ApplyStandardDamage(attacker, e, transform, 2.0f, null, weapon);
            }

            GameObject area = new GameObject("Mage_BurningArea");
            area.transform.position = transform.position;
            var script = area.AddComponent<MageBurningArea>();
            script.Init(attacker, enemyLayer);

            Destroy(gameObject);
        }
    }
}

public class MageBurningArea : MonoBehaviour
{
    private AllyStats attacker;
    private LayerMask enemyLayer;

    public void Init(AllyStats caster, LayerMask mask)
    {
        attacker = caster; enemyLayer = mask;
        VisualDebugHelper.DrawBox(transform.position, new Vector3(4f, 0.1f, 4f), Quaternion.identity, new Color(1, 0, 0, 0.4f), 5f);
        Destroy(gameObject, 5f);
        StartCoroutine(BurnTick());
    }

    IEnumerator BurnTick()
    {
        float dps = attacker.magicAtk * 0.05f;
        while (true)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 2f, enemyLayer);
            foreach (var hit in hits)
            {
                Stats e = hit.GetComponentInParent<Stats>();
                if (e != null) e.ApplyBurn(dps, 2f);
            }
            yield return new WaitForSeconds(0.5f); // Gây tick mỗi 0.5s nhưng damage vẫn tính theo giây chuẩn của ApplyBurn
        }
    }
}

public class MageMeteor : MonoBehaviour
{
    private AllyStats attacker;
    private LayerMask enemyLayer;
    private Vector3 dir;
    private float speed = 4f; // Chậm hơn bình thường
    private float distanceTraveled = 0f;
    private HashSet<Stats> hitTargets = new HashSet<Stats>();
    private MagePassive passive;
    private WeaponData weapon;

    public void Init(AllyStats caster, Vector3 direction, LayerMask mask, MagePassive owner, WeaponData wpn)
    {
        attacker = caster; dir = direction.normalized; enemyLayer = mask; passive = owner; weapon = wpn;
        MageVfxHelper.AttachSphere(transform, 3f, new Color(1, 0.5f, 0, 1f)); // VFX đi theo thiên thạch (bán kính 1.5f)
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position += dir * step;
        distanceTraveled += step;

        Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f, enemyLayer);
        foreach (var hit in hits)
        {
            // Collider địch thường nằm trên child → tìm Stats ở parent (giống GetEnemyStats)
            Stats e = hit.GetComponentInParent<Stats>();
            if (e != null && !hitTargets.Contains(e))
            {
                hitTargets.Add(e);
                // Gây 200% dmg PHÉP + Stun 2s (truyền weapon Grimoire = Magic)
                DamageHelper.ApplyStandardDamage(attacker, e, transform, 2.0f, null, weapon, 0, true, 2f, sourceType: DamageSourceType.Ranged);

                // Đính kèm các debuff của hướng NW (thiêu đốt 5s + giảm giáp/kháng phép 5s, tự khôi phục)
                if (passive != null) passive.ApplyMeteorHit(e);
                else e.ApplyBurn(attacker.magicAtk * 0.05f, 5f);
            }
        }

        if (distanceTraveled >= 10f) Destroy(gameObject);
    }
}

public class MageIceWall : MonoBehaviour
{
    public void Init()
    {
        // Collider đặc chặn người chơi & đồng minh đi xuyên qua
        var box = GetComponent<BoxCollider>();
        box.isTrigger = false;

        // Carving NavMesh để chặn cả enemy dùng NavMeshAgent
        var obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        obstacle.size = Vector3.one; // theo localScale của cube
        obstacle.carving = true;

        // Vật liệu màu lơ (cyan) cho dễ nhìn
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        }
    }

    // Collider đặc + đối tượng kia là trigger (đạn địch có Rigidbody) vẫn nhận callback này
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("EnemyProjectile"))
        {
            Destroy(other.gameObject);
        }
    }
}

// ========================================================
// VFX HELPER — tạo hình cầu con bám theo vật thể đang di chuyển
// (VisualDebugHelper.DrawSphere tạo object riêng đứng yên, không đi theo được)
// ========================================================
public static class MageVfxHelper
{
    public static GameObject AttachSphere(Transform parent, float diameter, Color color)
    {
        GameObject vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vfx.name = "Mage_VFX";

        var col = vfx.GetComponent<Collider>();
        if (col != null) Object.Destroy(col); // không cản va chạm

        vfx.transform.SetParent(parent, false);
        vfx.transform.localPosition = Vector3.zero;
        vfx.transform.localScale = Vector3.one * diameter;

        var rend = vfx.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = color;
        }
        return vfx;
    }
}