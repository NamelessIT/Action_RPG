using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hướng 8 phương cho đòn đánh cường hóa (dùng chung MageSkill + TricksterSkill).
/// </summary>
public enum EmpowerDirection { None = 0, N = 1, NE = 2, E = 3, SE = 4, S = 5, SW = 6, W = 7, NW = 8 }

/// <summary>
/// MODULE DÙNG CHUNG — toàn bộ logic "đòn đánh cường hóa 8 hướng" của Mage.
/// MageSkill (buff 1 đòn) và TricksterSkill (buff 3 đòn) đều gọi <see cref="Execute"/> để
/// không phải nhân bản code. Sát thương luôn là PHÉP (truyền vũ khí ảo Magic + skill=null).
///
/// Các hiệu ứng tê tái / Fractured / miễn nhiễm tái sử dụng hệ cộng dồn của MagePassive
/// (nếu có) qua API public; nếu không có MagePassive thì bỏ qua phần cộng dồn đó.
/// </summary>
[System.Serializable]
public class ArcaneEmpowerment
{
    [Header("Default (Không Grimoire / Không hướng)")]
    public float defaultMult = 2.5f;
    public float defaultStun = 1.5f;
    public float defaultRadius = 2.0f;
    public float defaultRangeFwd = 1.5f;

    [Header("N — Hellfire (đạn bay nổ khi trúng địch)")]
    public float nMult = 3.0f;
    public float nRadius = 4.0f;
    public float nBurnPct = 0.10f;   // 10% magicAtk / giây
    public float nBurnTime = 4.0f;
    public float nProjSpeed = 14f;
    public float nProjDistance = 12f;
    public float nProjHitRadius = 0.6f;

    [Header("NE — Fire Tornado (đạn bay nổ khi trúng địch)")]
    public float neMult = 2.0f;
    public float neRadius = 4.5f;
    public float neProjSpeed = 14f;
    public float neProjDistance = 12f;
    public float neProjHitRadius = 0.6f;

    [Header("E — Tailwind (xuyên thấu + buff)")]
    public float ePierceMult = 1.0f;     // = đòn đánh thường
    public float ePierceDistance = 10f;
    public float ePierceSpeed = 18f;
    public float ePierceRadius = 0.6f;
    public float eAtkSpeedBuff = 0.30f;
    public float eMoveSpeedBuff = 0.30f;
    public float eBuffTime = 5.0f;

    [Header("SE — Snow Storm (5 mảnh băng tự tìm)")]
    public float seShardMult = 0.40f;
    public int seChillPerShard = 2;
    public int seShardCount = 5;
    public float seSeekRange = 15f;
    public float seShardSpeed = 4.8f;

    [Header("S — Absolute Zero (tảng băng nổ)")]
    public float sMult = 2.0f;
    public float sRadius = 3.0f;
    public float sFreezeStun = 3.0f;
    public float sProjSpeed = 14f;
    public float sProjDistance = 12f;
    public float sProjHitRadius = 0.6f;

    [Header("SW — Flash Frost (đá băng nổ)")]
    public float swMult = 2.0f;
    public float swSlowPct = 0.80f;
    public float swSlowTime = 3.0f;
    public int swFractureStacks = 2;
    public int swChillStacks = 2;
    public float swRadius = 4.0f;
    public float swProjSpeed = 12f;
    public float swProjDistance = 10f;
    public float swProjHitRadius = 0.6f;

    [Header("W — Earthquake (thiên thạch nổ)")]
    public float wMult = 2.5f;
    public float wRadius = 3.0f;
    public float wShredTime = 5.0f;   // giảm 30% giáp/kháng phép trong 5s, sau đó về 15% của nội tại
    public float wProjSpeed = 12f;
    public float wProjDistance = 12f;
    public float wProjHitRadius = 0.8f; // dày gấp ~3 đạn thường

    [Header("NW — Magma Eruption (thảm dung nham chữ nhật, kiểu Hwei QE)")]
    public float lavaTickDmgPct = 0.30f;   // 30% magicAtk mỗi tick
    public float lavaBurnPct = 0.20f;      // 20% magicAtk thiêu đốt
    public float lavaBurnTime = 2.0f;
    public float lavaDuration = 5.0f;
    public float lavaWidth = 2.0f;
    public float lavaLength = 5.0f;
    public float lavaGrowTime = 1.0f;
    public float lavaTickInterval = 0.5f;

    // --- Runtime context (set qua Setup) ---
    private MonoBehaviour host;   // để chạy coroutine
    private Transform self;       // transform người chơi (nguồn sát thương)
    private AllyStats stats;
    private LayerMask dangerLayer;
    private MagePassive magePassive;
    private float dmgScale = 1f;  // hệ số sát thương phụ từ node nâng cấp (nếu có)

    // Vũ khí "ảo" loại Magic: ép sát thương luôn là PHÉP bất kể vũ khí đang cầm.
    private static WeaponData _magicProxy;
    private static WeaponData MagicProxy
    {
        get
        {
            if (_magicProxy == null)
            {
                _magicProxy = ScriptableObject.CreateInstance<WeaponData>();
                _magicProxy.weaponAtkType = WeaponData.WeaponAtkType.Magic;
            }
            return _magicProxy;
        }
    }

    /// <summary>Gắn ngữ cảnh trước khi dùng. host = SkillBehavior gọi (để chạy coroutine).</summary>
    public void Setup(MonoBehaviour host, AllyStats stats, LayerMask dangerLayer, MagePassive magePassive, float dmgScale = 1f)
    {
        this.host = host;
        this.self = host.transform;
        this.stats = stats;
        this.dangerLayer = dangerLayer;
        this.magePassive = magePassive;
        this.dmgScale = dmgScale;
    }

    /// <summary>Cập nhật hệ số sát thương phụ (node nâng cấp) giữa trận nếu cần.</summary>
    public void SetDamageScale(float scale) => dmgScale = scale;

    /// <summary>
    /// Tính hướng 8 phương từ input di chuyển, GIỮ hướng cũ khi đứng yên (input = 0).
    /// </summary>
    public static EmpowerDirection DirectionFromInput(Vector2 input, EmpowerDirection current)
    {
        if (input == Vector2.zero) return current;

        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        if (angle >= 337.5f || angle < 22.5f) return EmpowerDirection.E;
        if (angle < 67.5f) return EmpowerDirection.NE;
        if (angle < 112.5f) return EmpowerDirection.N;
        if (angle < 157.5f) return EmpowerDirection.NW;
        if (angle < 202.5f) return EmpowerDirection.W;
        if (angle < 247.5f) return EmpowerDirection.SW;
        if (angle < 292.5f) return EmpowerDirection.S;
        return EmpowerDirection.SE;
    }

    // ==========================================================
    // THỰC THI ĐÒN CƯỜNG HÓA
    // ==========================================================
    public void Execute(EmpowerDirection direction, bool isGrimoireEquipped)
    {
        if (host == null || stats == null) return;

        Vector3 fwd = stats.facingDirection;
        if (fwd == Vector3.zero) fwd = self.forward;
        fwd.y = 0; fwd.Normalize();

        Vector3 origin = self.position + Vector3.up * 0.5f;
        stats.EnterCombat();

        if (!isGrimoireEquipped || direction == EmpowerDirection.None)
        {
            Debug.Log("<color=orange>EMPOWER: Arcane Blast (Default)!</color>");
            DealAoe(self.position + fwd * defaultRangeFwd, defaultRadius, defaultMult, defaultStun, 0f, 0f, Color.yellow);
            return;
        }

        switch (direction)
        {
            case EmpowerDirection.N:
                Debug.Log("<color=red>EMPOWER (N): HELLFIRE!</color>");
                SpawnProjectile(origin, fwd, nProjSpeed, nProjDistance, nProjHitRadius, 1f, Color.magenta, ExplodeHellfire);
                break;

            case EmpowerDirection.NE:
                Debug.Log("<color=red>EMPOWER (NE): FIRE TORNADO!</color>");
                SpawnProjectile(origin, fwd, neProjSpeed, neProjDistance, neProjHitRadius, 1.5f, Color.magenta, ExplodeFireTornado);
                break;

            case EmpowerDirection.E:
                Debug.Log("<color=green>EMPOWER (E): TAILWIND!</color>");
                SpawnPierce(origin, fwd);
                host.StartCoroutine(TailwindBuffRoutine());
                break;

            case EmpowerDirection.SE:
                Debug.Log("<color=cyan>EMPOWER (SE): SNOW STORM!</color>");
                SpawnSnowStorm(origin);
                break;

            case EmpowerDirection.S:
                Debug.Log("<color=cyan>EMPOWER (S): ABSOLUTE ZERO!</color>");
                SpawnProjectile(origin, fwd, sProjSpeed, sProjDistance, sProjHitRadius, 3.5f,
                    new Color(0.4f, 0.9f, 1f, 1f), ExplodeAbsoluteZero);
                break;

            case EmpowerDirection.SW:
                Debug.Log("<color=blue>EMPOWER (SW): FLASH FROST!</color>");
                SpawnProjectile(origin, fwd, swProjSpeed, swProjDistance, swProjHitRadius, 1.5f,
                    new Color(0.4f, 0.9f, 1f, 1f), ExplodeFlashFrost);
                break;

            case EmpowerDirection.W:
                Debug.Log("<color=yellow>EMPOWER (W): EARTHQUAKE!</color>");
                SpawnProjectile(origin, fwd, wProjSpeed, wProjDistance, wProjHitRadius, 1.6f,
                    new Color(0.6f, 0.4f, 0.2f, 1f), ExplodeEarthquake);
                break;

            case EmpowerDirection.NW:
                Debug.Log("<color=orange>EMPOWER (NW): MAGMA ERUPTION!</color>");
                SpawnLavaField(self.position, fwd);
                break;
        }
    }

    /// <summary>AoE phép đơn giản (dùng cho vụ nổ tàn ảnh của Trickster…). Cần Setup trước.</summary>
    public void DealMagicAoe(Vector3 center, float radius, float mult, float stun, Color vfx)
    {
        DealAoe(center, radius, mult, stun, 0f, 0f, vfx);
    }

    // ==========================================================
    // AOE TỨC THỜI
    // ==========================================================
    private void DealAoe(Vector3 center, float radius, float mult, float stun, float burnPct, float burnTime, Color vfx)
    {
        VisualDebugHelper.DrawSphere(center, radius, vfx, 0.5f);

        Collider[] hits = Physics.OverlapSphere(center, radius, dangerLayer);
        HashSet<Stats> done = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e == null || e.currentHp <= 0 || !done.Add(e)) continue;

            DamageHelper.ApplyStandardDamage(stats, e, self, mult * dmgScale, null, MagicProxy, 1, stun > 0f, stun, sourceType: DamageSourceType.Ranged);
            if (burnPct > 0f) e.ApplyBurn(stats.magicAtk * burnPct, burnTime);
        }
    }

    // ==========================================================
    // SPAWN ĐẠN / VÙNG
    // ==========================================================
    private void SpawnProjectile(Vector3 pos, Vector3 dir, float speed, float maxDist, float hitRadius,
                                 float visualDiameter, Color color, Action<Vector3> onExplode)
    {
        GameObject go = new GameObject("Empower_Projectile");
        go.transform.position = pos;
        go.AddComponent<MageSkillProjectile>()
          .Init(dir, speed, maxDist, hitRadius, dangerLayer, onExplode, visualDiameter, color);
    }

    private void SpawnPierce(Vector3 pos, Vector3 dir)
    {
        GameObject go = new GameObject("Empower_Pierce");
        go.transform.position = pos;
        go.AddComponent<MagePierceProjectile>()
          .Init(dir, ePierceSpeed, ePierceDistance, ePierceRadius, dangerLayer,
                OnPierceHit, new Color(0.6f, 1f, 0.6f, 1f));
    }

    private void SpawnLavaField(Vector3 pos, Vector3 dir)
    {
        GameObject go = new GameObject("Empower_LavaField");
        go.transform.position = pos;
        go.AddComponent<MageLavaField>()
          .Init(dir, lavaWidth, lavaLength, lavaGrowTime, lavaTickInterval, lavaDuration,
                dangerLayer, OnLavaTick, new Color(1f, 0.4f, 0f, 0.6f));
    }

    private void SpawnSnowStorm(Vector3 origin)
    {
        List<Stats> targets = FindNearestEnemies(seSeekRange, seShardCount);
        if (targets.Count == 0) return;

        for (int i = 0; i < seShardCount; i++)
        {
            Stats tgt = targets[i % targets.Count];
            GameObject go = new GameObject("Empower_IceShard");
            go.transform.position = origin;
            go.AddComponent<MageHomingShard>()
              .Init(tgt, seShardSpeed, OnShardArrive, new Color(0.4f, 0.9f, 1f, 1f));
        }
    }

    // ==========================================================
    // CALLBACKS NỔ / TRÚNG
    // ==========================================================
    private void ExplodeHellfire(Vector3 center) => DealAoe(center, nRadius, nMult, 0f, nBurnPct, nBurnTime, Color.magenta);
    private void ExplodeFireTornado(Vector3 center) => DealAoe(center, neRadius, neMult, 0f, nBurnPct, nBurnTime, Color.magenta);

    private void ExplodeAbsoluteZero(Vector3 center)
    {
        VisualDebugHelper.DrawSphere(center, sRadius, Color.yellow, 0.6f);
        foreach (Stats e in EnemiesInRadius(center, sRadius))
        {
            DamageHelper.ApplyStandardDamage(stats, e, self, sMult * dmgScale, null, MagicProxy, 1, sourceType: DamageSourceType.Ranged);
            if (magePassive != null) magePassive.SkillForceFreeze(e, sFreezeStun);
        }
    }

    private void ExplodeFlashFrost(Vector3 center)
    {
        VisualDebugHelper.DrawSphere(center, swRadius, new Color(0.4f, 0.9f, 1f, 0.5f), 0.6f);
        foreach (Stats e in EnemiesInRadius(center, swRadius))
        {
            DamageHelper.ApplyStandardDamage(stats, e, self, swMult * dmgScale, null, MagicProxy, 1, sourceType: DamageSourceType.Ranged);
            host.StartCoroutine(TempSlowRoutine(e, swSlowPct, swSlowTime));
            if (magePassive != null)
            {
                magePassive.SkillAddFracture(e, swFractureStacks);
                magePassive.SkillAddChill(e, swChillStacks);
            }
        }
    }

    private void ExplodeEarthquake(Vector3 center)
    {
        VisualDebugHelper.DrawSphere(center, wRadius, new Color(0.6f, 0.4f, 0.2f, 0.5f), 0.6f);
        foreach (Stats e in EnemiesInRadius(center, wRadius))
        {
            DamageHelper.ApplyStandardDamage(stats, e, self, wMult * dmgScale, null, MagicProxy, 1, sourceType: DamageSourceType.Ranged);
            // Giảm 30% giáp/kháng phép 5s rồi về 15% nội tại — tính tập trung trong MagePassive.
            if (magePassive != null) magePassive.SkillApplyEarthquakeShred(e, wShredTime);
        }
    }

    private void OnPierceHit(Stats e)
    {
        DamageHelper.ApplyStandardDamage(stats, e, self, ePierceMult * dmgScale, null, MagicProxy, 1, sourceType: DamageSourceType.Ranged);
    }

    private void OnShardArrive(Stats e)
    {
        if (e == null || e.currentHp <= 0) return;
        DamageHelper.ApplyStandardDamage(stats, e, self, seShardMult * dmgScale, null, MagicProxy, 0, sourceType: DamageSourceType.Ranged);
        if (magePassive != null) magePassive.SkillAddChill(e, seChillPerShard);
    }

    private void OnLavaTick(Stats e)
    {
        DamageHelper.ApplyStandardDamage(stats, e, self, lavaTickDmgPct * dmgScale, null, MagicProxy, 0, sourceType: DamageSourceType.DoT);
        e.ApplyBurn(stats.magicAtk * lavaBurnPct, lavaBurnTime);
        if (magePassive != null) magePassive.SkillAddFracture(e, 1);
    }

    // ==========================================================
    // BUFF / DEBUFF TẠM THỜI
    // ==========================================================
    private IEnumerator TailwindBuffRoutine()
    {
        stats.bonusMoveSpeed += eMoveSpeedBuff;
        stats.bonusAttackSpeed += eAtkSpeedBuff;
        if (stats is AllyStats ally) { ally.CalculateMoveSpeedOnly(); ally.CalculateCombatStatsOnly(); }

        yield return new WaitForSeconds(eBuffTime);

        stats.bonusMoveSpeed -= eMoveSpeedBuff;
        stats.bonusAttackSpeed -= eAtkSpeedBuff;
        if (stats is AllyStats ally2) { ally2.CalculateMoveSpeedOnly(); ally2.CalculateCombatStatsOnly(); }
    }

    private IEnumerator TempSlowRoutine(Stats enemy, float slowPct, float duration)
    {
        if (enemy == null || enemy.currentHp <= 0) yield break;
        float amount = enemy.baseMoveSpeed * slowPct;
        enemy.baseMoveSpeed -= amount;
        yield return new WaitForSeconds(duration);
        if (enemy != null) enemy.baseMoveSpeed += amount;
    }

    // ==========================================================
    // TIỆN ÍCH TÌM MỤC TIÊU
    // ==========================================================
    private List<Stats> EnemiesInRadius(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, dangerLayer);
        List<Stats> result = new List<Stats>();
        HashSet<Stats> seen = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e != null && e.currentHp > 0 && seen.Add(e)) result.Add(e);
        }
        return result;
    }

    private List<Stats> FindNearestEnemies(float range, int max)
    {
        List<Stats> all = EnemiesInRadius(self.position, range);
        all.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - self.position)
            .CompareTo(Vector3.SqrMagnitude(b.transform.position - self.position)));
        if (all.Count > max) all.RemoveRange(max, all.Count - max);
        return all;
    }
}

// ==========================================================
// ĐẠN NỔ KHI TRÚNG KẺ ĐỊCH ĐẦU TIÊN (S / SW / W / N / NE)
// ==========================================================
public class MageSkillProjectile : MonoBehaviour
{
    private Vector3 dir;
    private float speed, maxDist, hitRadius, traveled;
    private LayerMask layer;
    private Action<Vector3> onExplode;
    private bool done;

    public void Init(Vector3 d, float spd, float maxD, float radius, LayerMask mask,
                     Action<Vector3> explodeCb, float visualDiameter, Color color)
    {
        dir = d.normalized; speed = spd; maxDist = maxD; hitRadius = radius; layer = mask; onExplode = explodeCb;
        MageVfxHelper.AttachSphere(transform, visualDiameter, color);
        Destroy(gameObject, 6f);
    }

    void Update()
    {
        if (done) return;
        float step = speed * Time.deltaTime;
        transform.position += dir * step;
        traveled += step;

        Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, layer);
        foreach (var h in hits)
            if (h.GetComponentInParent<Stats>() != null) { Explode(); return; }

        if (traveled >= maxDist) Explode();
    }

    private void Explode()
    {
        if (done) return;
        done = true;
        onExplode?.Invoke(transform.position);
        Destroy(gameObject);
    }
}

// ==========================================================
// ĐẠN XUYÊN THẤU (E — Tailwind)
// ==========================================================
public class MagePierceProjectile : MonoBehaviour
{
    private Vector3 dir;
    private float speed, maxDist, hitRadius, traveled;
    private LayerMask layer;
    private Action<Stats> onHit;
    private HashSet<Stats> hitSet = new HashSet<Stats>();

    public void Init(Vector3 d, float spd, float maxD, float radius, LayerMask mask, Action<Stats> cb, Color color)
    {
        dir = d.normalized; speed = spd; maxDist = maxD; hitRadius = radius; layer = mask; onHit = cb;
        MageVfxHelper.AttachSphere(transform, 0.6f, color);
        Destroy(gameObject, 4f);
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position += dir * step;
        traveled += step;

        Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, layer);
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e != null && e.currentHp > 0 && hitSet.Add(e)) onHit?.Invoke(e);
        }

        if (traveled >= maxDist) Destroy(gameObject);
    }
}

// ==========================================================
// MẢNH BĂNG TỰ TÌM MỤC TIÊU (SE — Snow Storm)
// ==========================================================
public class MageHomingShard : MonoBehaviour
{
    private Stats targetStats;
    private float speed;
    private Action<Stats> onArrive;
    private bool done;

    public void Init(Stats tgt, float spd, Action<Stats> cb, Color color)
    {
        targetStats = tgt; speed = spd; onArrive = cb;
        MageVfxHelper.AttachSphere(transform, 0.5f, color);
        Destroy(gameObject, 4f);
    }

    void Update()
    {
        if (done) return;
        if (targetStats == null || targetStats.currentHp <= 0) { Destroy(gameObject); return; }

        Vector3 tp = targetStats.transform.position + Vector3.up * 0.5f;
        transform.position = Vector3.MoveTowards(transform.position, tp, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, tp) <= 0.4f)
        {
            done = true;
            onArrive?.Invoke(targetStats);
            Destroy(gameObject);
        }
    }
}

// ==========================================================
// THẢM DUNG NHAM (NW — Magma Eruption)
// ==========================================================
public class MageLavaField : MonoBehaviour
{
    private Vector3 dir;
    private float width, maxLength, growTime, tickInterval, duration;
    private LayerMask layer;
    private Action<Stats> onTick;
    private Transform visual;

    public void Init(Vector3 d, float w, float len, float grow, float tick, float dur,
                     LayerMask mask, Action<Stats> cb, Color color)
    {
        dir = d.normalized;
        if (dir == Vector3.zero) dir = Vector3.forward;
        dir.y = 0f; dir.Normalize();
        width = w; maxLength = len; growTime = grow; tickInterval = tick; duration = dur;
        layer = mask; onTick = cb;

        transform.rotation = Quaternion.LookRotation(dir);

        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Lava_VFX";
        var col = box.GetComponent<Collider>();
        if (col != null) Destroy(col);
        box.transform.SetParent(transform, false);
        var rend = box.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = color;
        }
        visual = box.transform;

        Destroy(gameObject, dur);
        StartCoroutine(Run());
    }

    private float CurrentLength(float elapsed)
    {
        if (growTime <= 0f) return maxLength;
        return Mathf.Min(maxLength, maxLength * (elapsed / growTime));
    }

    private void UpdateVisual(float len)
    {
        if (visual == null) return;
        visual.localScale = new Vector3(width, 0.1f, Mathf.Max(0.01f, len));
        visual.localPosition = new Vector3(0f, 0f, len * 0.5f);
    }

    private IEnumerator Run()
    {
        float elapsed = 0f;
        float tickAccum = tickInterval; // tick ngay khi xuất hiện
        while (elapsed < duration)
        {
            float len = CurrentLength(elapsed);
            UpdateVisual(len);

            tickAccum += Time.deltaTime;
            if (tickAccum >= tickInterval)
            {
                tickAccum -= tickInterval;
                DoTick(len);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void DoTick(float len)
    {
        if (len <= 0.01f) return;
        Vector3 center = transform.position + dir * (len * 0.5f) + Vector3.up * 0.1f;
        Vector3 halfExtents = new Vector3(width * 0.5f, 1f, len * 0.5f);
        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.LookRotation(dir), layer);
        HashSet<Stats> done = new HashSet<Stats>();
        foreach (var h in hits)
        {
            Stats e = h.GetComponentInParent<Stats>();
            if (e != null && e.currentHp > 0 && done.Add(e)) onTick?.Invoke(e);
        }
    }
}
