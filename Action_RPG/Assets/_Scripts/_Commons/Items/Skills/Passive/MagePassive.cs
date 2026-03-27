using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerController))]
public class MagePassive : SkillBehavior
{
    [Header("Core Requirements")]
    public string requiredWeaponName = "Grimoire";
    public bool bypassWeaponCheck = false; // ✅ DEBUG MODE: Bật để test không cần Grimoire
    public float baseMoveSpeedBonus = 0.05f;
    public float xpBonusPercent = 0.2f;

    [Header("Direction N - Fireball")]
    public float fireballDamageMultiplier = 1.8f;
    public float fireballBurnDuration = 3f;
    public float fireballBurnTickDamage = 0.08f;
    public float fireballCooldown = 20f;

    [Header("Direction NE - Fire Tornado")]
    public float tornadoDuration = 2.4f;
    public float tornadoSpeed = 8f;
    public float tornadoDamagePerSecond = 0.25f;
    public float tornadoICD = 5f;

    [Header("Direction E - Wind")]
    public float eastMoveSpeedBonus = 0.036f;

    [Header("Direction SE - Ice Crystals")]
    public int crystalCount = 3;
    public float crystalLifetime = 6f;
    public float crystalAttackRange = 5f;
    public float crystalDamagePercent = 0.12f;

    [Header("Direction S - Ice")]
    public int hitsToFreeze = 7;
    public float freezeDuration = 2f;
    public float chillDurationPerHit = 1.5f;

    [Header("Direction SW - Ice Path")]
    public float icePathDuration = 4f;
    public float icePathAllyDefBonus = 0.012f;
    public float icePathEnemySlow = 0.3f;

    [Header("Direction W - Earth")]
    public int hitsToReduceDef = 3;
    public float defReductionPercent = 0.15f;
    public float defReductionDuration = 4f;

    [Header("Direction NW - Fire Path")]
    public float firePathDuration = 4f;
    public float firePathBleedDamagePercent = 0.1f;
    public float firePathStunDuration = 1f;

    // Runtime state
    private PlayerController playerController;
    private bool isGrimoireEquipped = false;
    private Direction8 currentDirection = Direction8.None;
    private float fireballCD = 0f;
    private float currentTornadoICD = 0f;
    private float addedMoveSpeed = 0f;
    private Dictionary<Stats, int> iceHitCounter = new Dictionary<Stats, int>();
    private Dictionary<Stats, int> earthHitCounter = new Dictionary<Stats, int>();
    private Dictionary<Stats, Coroutine> activeDefReductionCoroutines = new Dictionary<Stats, Coroutine>();

    private enum Direction8
    {
        None = 0, N = 1, NE = 2, E = 3, SE = 4,
        S = 5, SW = 6, W = 7, NW = 8
    }

    protected override void OnEquip()
    {
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("[MagePassive] ❌ Missing PlayerController!");
            enabled = false;
            return;
        }

        // Subscribe events
        playerController.OnMovementInputChanged += OnMovementInput;
        playerController.OnAttackPerformed += OnAttackPerformed;
        playerController.OnHitEnemy += OnHitEnemy;
        playerController.OnWeaponEquipped += OnWeaponEquipped;

        // Initial weapon check
        var equipManager = GetComponent<EquipmentManager>();
        CheckGrimoireEquipped(equipManager?.currentWeapon);

        // ✅ DEBUG LOG: Confirm passive is ACTIVE
        Debug.Log($"[MagePassive] ✅ EQUIPPED! WeaponCheck bypass: {bypassWeaponCheck} | Grimoire active: {isGrimoireEquipped}");
    }

    protected override void OnUnequip()
    {
        if (playerController != null)
        {
            playerController.OnMovementInputChanged -= OnMovementInput;
            playerController.OnAttackPerformed -= OnAttackPerformed;
            playerController.OnHitEnemy -= OnHitEnemy;
            playerController.OnWeaponEquipped -= OnWeaponEquipped;
        }

        // Cleanup effects
        foreach (var coro in activeDefReductionCoroutines.Values)
            if (coro != null) StopCoroutine(coro);
        activeDefReductionCoroutines.Clear();

        CleanUpStats();
        iceHitCounter.Clear();
        earthHitCounter.Clear();
        isGrimoireEquipped = false;

        Debug.Log("[MagePassive] ⚪ UNEQUIPPED");
    }

    void Update()
    {
        if (fireballCD > 0f) fireballCD -= Time.deltaTime;
        if (currentTornadoICD > 0f) currentTornadoICD -= Time.deltaTime;
        ApplyDirectionalMoveSpeed();
    }

    // ========================================================
    // EVENT HANDLERS (WITH DEBUG LOGS)
    // ========================================================

    private void OnMovementInput(Vector2 input)
    {
      

        if (!isGrimoireEquipped || input == Vector2.zero) return;

        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        // Map to 8 directions
        if (angle >= 337.5f || angle < 22.5f) currentDirection = Direction8.E;
        else if (angle >= 22.5f && angle < 67.5f) currentDirection = Direction8.NE;
        else if (angle >= 67.5f && angle < 112.5f) currentDirection = Direction8.N;
        else if (angle >= 112.5f && angle < 157.5f) currentDirection = Direction8.NW;
        else if (angle >= 157.5f && angle < 202.5f) currentDirection = Direction8.W;
        else if (angle >= 202.5f && angle < 247.5f) currentDirection = Direction8.SW;
        else if (angle >= 247.5f && angle < 292.5f) currentDirection = Direction8.S;
        else if (angle >= 292.5f && angle < 337.5f) currentDirection = Direction8.SE;
        else currentDirection = Direction8.None;

        // ✅ DEBUG: Log detected direction
        //Debug.Log($"[Mage] 🧭 Direction DETECTED: {currentDirection} (Angle: {angle:F1}°)");
    }

    private void OnWeaponEquipped(WeaponData weapon)
    {
        string weaponName = weapon != null ? weapon.weaponName : "NONE";
        Debug.Log($"[Mage] ⚔️ Weapon Equipped: {weaponName}");
        CheckGrimoireEquipped(weapon);
    }

    private void OnAttackPerformed(int stepIndex, bool isHeavy)
    {
        if (!isGrimoireEquipped)
        {
            Debug.Log("[Mage] ⚠️ Attack ignored (Grimoire not equipped)");
            return;
        }

        Debug.Log($"[Mage] 💥 ATTACK PERFORMED | Direction: {currentDirection} | Step: {stepIndex} | Heavy: {isHeavy}");

        switch (currentDirection)
        {
            case Direction8.N: HandleNorthFireball(); break;
            case Direction8.NE: HandleNortheastTornado(); break;
            case Direction8.SE: HandleSoutheastCrystals(); break;
            case Direction8.SW: HandleSouthwestIcePath(); break;
            case Direction8.NW: HandleNorthwestFirePath(); break;
            case Direction8.E: Debug.Log("[Mage] 💨 Wind direction (E) - +MoveSpeed bonus active"); break;
                // S/W handled in OnHitEnemy
        }
    }

    private void OnHitEnemy(Stats target, int stepIndex, bool isHeavy, bool isCrit)
    {
        if (!isGrimoireEquipped || target == null) return;

        Debug.Log($"[Mage] 🎯 HIT ENEMY | Direction: {currentDirection} | Target: {target.name}");

        switch (currentDirection)
        {
            case Direction8.S: HandleSouthIce(target); break;
            case Direction8.W: HandleWestEarth(target); break;
        }
    }

    // ========================================================
    // DIRECTIONAL ABILITIES (MINIMAL - COMPILE SAFE)
    // ========================================================

    private void HandleNorthFireball()
    {
        if (fireballCD > 0f)
        {
            Debug.Log($"[Mage] 🔥 Fireball on cooldown ({fireballCD:F1}s remaining) - applying light burn");
            ApplyBurnToNearbyEnemies(2f, stats.magicAtk * 0.05f);
            return;
        }

        Debug.Log($"[Mage] 🔥 FIREBALL LAUNCHED! CD: {fireballCooldown}s");
        fireballCD = fireballCooldown;
    }

    private void HandleNortheastTornado()
    {
        if (currentTornadoICD > 0f)
        {
            Debug.Log($"[Mage] 🌪️ Tornado on ICD ({currentTornadoICD:F1}s remaining)");
            return;
        }

        Debug.Log($"[Mage] 🌪️ FIRE TORNADO LAUNCHED! ICD: {tornadoICD}s");
        currentTornadoICD = tornadoICD;
    }

    private void HandleSoutheastCrystals()
    {
        Debug.Log($"[Mage] ❄️ {crystalCount} ICE CRYSTALS SUMMONED!");
    }

    private void HandleSouthIce(Stats target)
    {
        Debug.Log($"[Mage] ❄️ Applying CHILL to {target.name}");

        if (!iceHitCounter.ContainsKey(target)) iceHitCounter[target] = 0;
        iceHitCounter[target]++;

        Debug.Log($"[Mage] ❄️ Hit count for {target.name}: {iceHitCounter[target]}/{hitsToFreeze}");

        if (iceHitCounter[target] >= hitsToFreeze)
        {
            ApplyStunEffect(target, freezeDuration);
            iceHitCounter[target] = 0;
            Debug.Log($"[Mage] ❄️❄️ TARGET FROZEN: {target.name} for {freezeDuration}s!");
        }
    }

    private void HandleSouthwestIcePath()
    {
        Debug.Log($"[Mage] ❄️ ICE PATH CREATED! Duration: {icePathDuration}s");
    }

    private void HandleWestEarth(Stats target)
    {
        Debug.Log($"[Mage] 🪨 Applying BLUNT to {target.name}");

        if (!earthHitCounter.ContainsKey(target)) earthHitCounter[target] = 0;
        earthHitCounter[target]++;

        Debug.Log($"[Mage] 🪨 Hit count for {target.name}: {earthHitCounter[target]}/{hitsToReduceDef}");

        if (earthHitCounter[target] >= hitsToReduceDef)
        {
            ApplyDefenseReduction(target, defReductionPercent, defReductionDuration);
            earthHitCounter[target] = 0;
            Debug.Log($"[Mage] 🪨 DEF REDUCED: {target.name} by {defReductionPercent * 100}% for {defReductionDuration}s!");
        }
    }

    private void HandleNorthwestFirePath()
    {
        Debug.Log($"[Mage] 🔥 FIRE PATH CREATED! Duration: {firePathDuration}s");
    }

    // ========================================================
    // MINIMAL STATUS EFFECTS (SAFE FOR CURRENT SYSTEM)
    // ========================================================

    private void ApplyStunEffect(Stats target, float duration)
    {
        DamageInfo stunInfo = new DamageInfo
        {
            damageAmount = 0,
            isStun = true,
            stunDuration = duration,
            sourcePosition = transform.position,
            attacker = stats
        };
        target.TakeDamage(stunInfo);
        Debug.Log($"[Mage] ⚡ STUN APPLIED to {target.name} for {duration}s");
    }

    private void ApplyDefenseReduction(Stats target, float reductionPercent, float duration)
    {
        float originalArmor = target.magicResist;
        target.magicResist *= (1f - reductionPercent);

        if (!activeDefReductionCoroutines.ContainsKey(target))
        {
            Coroutine coro = StartCoroutine(RevertDefenseAfterDelay(target, originalArmor, duration));
            activeDefReductionCoroutines[target] = coro;
        }
    }

    private IEnumerator RevertDefenseAfterDelay(Stats target, float originalValue, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null && target.currentHp > 0 && activeDefReductionCoroutines.ContainsKey(target))
        {
            target.magicResist = originalValue;
            activeDefReductionCoroutines.Remove(target);
            Debug.Log($"[Mage] 🛡️ Defense reverted for {target.name}");
        }
    }

    private void ApplyBurnToNearbyEnemies(float duration, float dps)
    {
        float range = playerController != null ? playerController.attackRange : 2f;
        Collider[] hits = Physics.OverlapSphere(transform.position, range, LayerMask.GetMask("Enemy"));
        foreach (var col in hits)
        {
            var enemyStats = col.GetComponent<Stats>();
            if (enemyStats != null && enemyStats.currentHp > 0)
            {
                enemyStats.ApplyBleed(dps, duration);
                Debug.Log($"[Mage] 🔥 Burn applied to {enemyStats.name} ({dps:F1} DPS for {duration}s)");
            }
        }
    }

    private void CheckGrimoireEquipped(WeaponData weapon)
    {
        bool wasEquipped = isGrimoireEquipped;

        // ✅ DEBUG MODE: Bypass weapon check khi cần test
        if (bypassWeaponCheck)
        {
            isGrimoireEquipped = true;
            Debug.Log("[MagePassive] ⚠️ DEBUG MODE: Grimoire requirement BYPASSED (bypassWeaponCheck = true)");
        }
        else
        {
            isGrimoireEquipped = (weapon != null && weapon.weaponName == requiredWeaponName);
        }

        if (isGrimoireEquipped && !wasEquipped)
            Debug.Log($"[MagePassive] 📚 Grimoire EQUIPPED! Directional combat ACTIVE");
        else if (!isGrimoireEquipped && wasEquipped)
        {
            Debug.Log($"[MagePassive] 📚 Grimoire UNEQUIPPED. Directional combat DISABLED");
            CleanUpStats();
        }
    }

    private void ApplyDirectionalMoveSpeed()
    {
        CleanUpStats();
        if (!isGrimoireEquipped) return;

        addedMoveSpeed = baseMoveSpeedBonus;
        stats.bonusMoveSpeed += addedMoveSpeed;

        if (currentDirection == Direction8.E)
        {
            addedMoveSpeed += eastMoveSpeedBonus;
            stats.bonusMoveSpeed += eastMoveSpeedBonus;
        
        }

        if (stats is AllyStats ally) ally.CalculateMoveSpeedOnly();
    }

    private void CleanUpStats()
    {
        if (addedMoveSpeed != 0f)
        {
            stats.bonusMoveSpeed -= addedMoveSpeed;
            addedMoveSpeed = 0f;
        }
    }

    public float GetXpBonusMultiplier() => isGrimoireEquipped ? (1f + xpBonusPercent) : 1f;
}
