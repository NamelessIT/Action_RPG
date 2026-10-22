using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hệ "Điểm Yếu" cho Passive Debuffer [Điểm Yếu Chí Tử].
/// F3C: Debuff Registry owns mark duration / cleanse / remove lifecycle; this class only owns
/// Weakness-specific directional payload (sector 0..7 or -1 = all directions) and the 5s re-scan cooldown.
/// PlayerController.ApplyDamageToTarget gọi TryConsume khi đánh trúng: nếu đánh từ đúng hướng
/// → bỏ qua 50% Armor/MR + gây thêm True = 3% maxHp địch. Điểm yếu 1 hướng sẽ tiêu sau khi kích.
/// </summary>
public static class CompanionWeaknessSystem
{
    private const string EFFECT_ID = "CompanionDebuffer.Weakness";
    private const float RESCAN_COOLDOWN = 5f;

    // World-space Weakness marker: keep the dot close to the target while remaining readable.
    // Sector directions use the exact same 8-way world-space convention as SectorOf().
    private const float MARKER_DISTANCE = 0.50f;
    private const float MARKER_HEIGHT = 0.05f;
    private const float MARKER_RADIUS = 0.10f;
    private static readonly Color MARKER_COLOR = new Color(1f, 0.85f, 0.05f, 1f);
    private static Material _markerMaterial;

    private sealed class Weak
    {
        public int sector;
        public EffectHandle handle;
        public Dictionary<int, GameObject> markers;
        public int activeSectorMask;
    }

    private static readonly Dictionary<Stats, Weak> _active = new Dictionary<Stats, Weak>();
    private static readonly Dictionary<Stats, float> _cooldownUntil = new Dictionary<Stats, float>();

    private static int SectorOf(Vector3 from, Vector3 enemyPos)
    {
        Vector3 d = from - enemyPos; d.y = 0f;
        float ang = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
        if (ang < 0) ang += 360f;
        return Mathf.FloorToInt((ang + 22.5f) % 360f / 45f);
    }

    /// <summary>Debuffer Passive: lộ 1 điểm yếu ngẫu nhiên (sector 0..7) trong dur giây.</summary>
    public static void RevealRandom(Stats enemy, float dur, Stats source = null)
    {
        Reveal(enemy, Random.Range(0, 8), dur, source, "random directional Weakness");
    }

    /// <summary>Aegis skill: lộ điểm yếu CẢ 8 hướng trong dur giây.</summary>
    public static void RevealAll(Stats enemy, float dur, Stats source = null)
    {
        Reveal(enemy, -1, dur, source, "all-direction Weakness");
    }

    private static void Reveal(Stats enemy, int sector, float dur, Stats source, string note)
    {
        if (enemy == null || enemy.currentHp <= 0 || dur <= 0f) return;

        var request = new DebuffRequest
        {
            effectId = EFFECT_ID,
            duration = dur,
            cleanseable = true,
            // Explicit sourceKey below makes Weakness a single target-side mark. Aegis can replace
            // the random passive mark exactly like the legacy dictionary assignment did.
            stackPolicy = EffectStackPolicy.ReplaceSameSource,
            maxStacks = 1,
            potency = sector == -1 ? 2f : 1f,
            note = note
        };

        request.onApply = snapshot =>
        {
            if (snapshot.target == null) return;

            var weak = new Weak
            {
                sector = sector,
                handle = snapshot.handle,
                activeSectorMask = sector == -1 ? 0xFF : (1 << Mathf.Clamp(sector, 0, 7)),
                markers = CreateWeaknessMarkers(snapshot.target, sector)
            };
            _active[snapshot.target] = weak;
        };
        request.onRemove = (snapshot, reason) => OnWeaknessRemoved(snapshot, reason);

        enemy.ApplyDebuff(request, source, EFFECT_ID);
    }

    /// <summary>Enemy đang có điểm yếu còn hiệu lực? (để Passive khỏi quét đè).</summary>
    public static bool HasActive(Stats enemy)
    {
        if (enemy == null) return false;
        if (!_active.TryGetValue(enemy, out Weak weak)) return false;

        if (weak.handle.IsValid && enemy.StatusEffects.IsEffectActive(weak.handle)) return true;
        DestroyWeaknessMarkers(weak);
        _active.Remove(enemy);
        return false;
    }

    /// <summary>Có được phép quét lại điểm yếu trên enemy này chưa (cooldown 5s sau khi hết/kích/cleanse)?</summary>
    public static bool CanScan(Stats enemy)
    {
        if (enemy == null) return false;
        if (HasActive(enemy)) return false;
        return !_cooldownUntil.TryGetValue(enemy, out float until) || Time.time >= until;
    }

    /// <summary>
    /// Player đánh trúng 'enemy' từ vị trí 'fromPos'. Nếu trúng một hướng Weakness còn active → trả true.
    /// Mỗi hướng chỉ kích được một lần. Random Weakness có 1 hướng; Reveal-All có 8 hướng độc lập.
    /// </summary>
    public static bool TryConsume(Stats enemy, Vector3 fromPos)
    {
        if (enemy == null || !_active.TryGetValue(enemy, out Weak weak)) return false;
        if (!weak.handle.IsValid || !enemy.StatusEffects.IsEffectActive(weak.handle))
        {
            DestroyWeaknessMarkers(weak);
            _active.Remove(enemy);
            return false;
        }

        int hitSector = SectorOf(fromPos, enemy.transform.position);
        int hitBit = 1 << hitSector;

        // Each authored Weakness direction is a one-shot charge. Reveal-All starts with all eight
        // bits active; hitting one sector consumes only that sector, so repeating the same angle
        // cannot repeatedly trigger the bonus. A normal random Weakness simply has one active bit.
        if ((weak.activeSectorMask & hitBit) == 0) return false;

        weak.activeSectorMask &= ~hitBit;
        DestroyWeaknessMarker(weak, hitSector);

        // End the managed Weakness only when the last remaining direction is consumed. Expiry /
        // Cleanse still remove all remaining directions at once through OnWeaknessRemoved.
        if (weak.activeSectorMask == 0)
            enemy.StatusEffects.RemoveEffect(weak.handle, EffectRemoveReason.Manual);

        return true;
    }


    private static Dictionary<int, GameObject> CreateWeaknessMarkers(Stats enemy, int sector)
    {
        var markers = new Dictionary<int, GameObject>(sector == -1 ? 8 : 1);
        if (enemy == null) return markers;

        if (sector == -1)
        {
            for (int i = 0; i < 8; i++)
                markers[i] = CreateWeaknessMarker(enemy.transform, i);
        }
        else
        {
            int authoredSector = Mathf.Clamp(sector, 0, 7);
            markers[authoredSector] = CreateWeaknessMarker(enemy.transform, authoredSector);
        }

        return markers;
    }

    private static GameObject CreateWeaknessMarker(Transform target, int sector)
    {
        float angle = sector * 45f * Mathf.Deg2Rad;
        Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * MARKER_DISTANCE;
        Vector3 worldOffset = radial + Vector3.up * MARKER_HEIGHT;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = $"WeaknessMarker_Sector{sector}";
        marker.transform.position = target.position + worldOffset;
        marker.transform.localScale = Vector3.one * (MARKER_RADIUS * 2f);

        Collider collider = marker.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = GetMarkerMaterial();

        CompanionWeaknessMarkerFollower follower = marker.AddComponent<CompanionWeaknessMarkerFollower>();
        follower.Initialize(target, worldOffset);
        return marker;
    }

    private static Material GetMarkerMaterial()
    {
        if (_markerMaterial != null) return _markerMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        _markerMaterial = new Material(shader)
        {
            name = "Runtime_WeaknessMarker_Yellow",
            color = MARKER_COLOR
        };
        return _markerMaterial;
    }

    private static void DestroyWeaknessMarker(Weak weak, int sector)
    {
        if (weak?.markers == null) return;
        if (!weak.markers.TryGetValue(sector, out GameObject marker)) return;
        if (marker != null) Object.Destroy(marker);
        weak.markers.Remove(sector);
    }

    private static void DestroyWeaknessMarkers(Weak weak)
    {
        if (weak?.markers == null) return;
        foreach (GameObject marker in weak.markers.Values)
            if (marker != null) Object.Destroy(marker);
        weak.markers.Clear();
    }

    private static void OnWeaknessRemoved(DebuffRuntimeSnapshot snapshot, EffectRemoveReason reason)
    {
        Stats enemy = snapshot.target;
        if (enemy == null) return;

        if (_active.TryGetValue(enemy, out Weak current) && current.handle.Equals(snapshot.handle))
        {
            DestroyWeaknessMarkers(current);
            _active.Remove(enemy);
        }

        // Aegis Reveal-All replacing a random mark is an in-place authoring override, not a real
        // end of Weakness. Preserve legacy behavior: do not insert a cooldown between the two marks.
        if (reason == EffectRemoveReason.Overridden || reason == EffectRemoveReason.TargetDied)
            return;

        _cooldownUntil[enemy] = Time.time + RESCAN_COOLDOWN;
    }
}


/// <summary>
/// Keeps a Weakness marker in world-space so enemy rotation/animation cannot rotate the authored sector.
/// </summary>
internal sealed class CompanionWeaknessMarkerFollower : MonoBehaviour
{
    private Transform _target;
    private Vector3 _worldOffset;

    public void Initialize(Transform target, Vector3 worldOffset)
    {
        _target = target;
        _worldOffset = worldOffset;
        if (_target != null) transform.position = _target.position + _worldOffset;
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = _target.position + _worldOffset;
    }
}
