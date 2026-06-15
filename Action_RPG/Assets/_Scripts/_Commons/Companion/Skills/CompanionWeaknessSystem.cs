using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hệ "Điểm Yếu" cho Passive Debuffer [Điểm Yếu Chí Tử].
/// Debuffer làm lộ 1 điểm yếu ở 1 trong 8 hướng (hoặc cả 8 hướng — Aegis skill) quanh kẻ địch.
/// PlayerController.ApplyDamageToTarget gọi TryConsume khi đánh trúng: nếu đánh từ đúng hướng
/// → bỏ qua 50% Armor/MR + gây thêm True = 3% maxHp địch. Điểm yếu 1 hướng sẽ tiêu sau khi kích.
/// </summary>
public static class CompanionWeaknessSystem
{
    private class Weak { public int sector; public float expiry; } // sector = -1 nghĩa là cả 8 hướng
    private static readonly Dictionary<Stats, Weak> _active = new Dictionary<Stats, Weak>();
    private static readonly Dictionary<Stats, float> _cooldownUntil = new Dictionary<Stats, float>(); // mốc được quét lại

    private static int SectorOf(Vector3 from, Vector3 enemyPos)
    {
        Vector3 d = from - enemyPos; d.y = 0f;
        float ang = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg; // -180..180
        if (ang < 0) ang += 360f;
        return Mathf.FloorToInt((ang + 22.5f) % 360f / 45f); // 0..7
    }

    /// <summary>Debuffer Passive: lộ 1 điểm yếu ngẫu nhiên (sector 0..7) trong dur giây.</summary>
    public static void RevealRandom(Stats enemy, float dur)
    {
        if (enemy == null) return;
        _active[enemy] = new Weak { sector = Random.Range(0, 8), expiry = Time.time + dur };
    }

    /// <summary>Aegis skill: lộ điểm yếu CẢ 8 hướng trong dur giây.</summary>
    public static void RevealAll(Stats enemy, float dur)
    {
        if (enemy == null) return;
        _active[enemy] = new Weak { sector = -1, expiry = Time.time + dur };
    }

    /// <summary>Enemy đang có điểm yếu còn hiệu lực? (để Passive khỏi quét đè).</summary>
    public static bool HasActive(Stats enemy)
    {
        if (enemy == null) return false;
        if (_active.TryGetValue(enemy, out var w))
        {
            if (Time.time <= w.expiry) return true;
            Clear(enemy);
        }
        return false;
    }

    /// <summary>Có được phép quét lại điểm yếu trên enemy này chưa (cooldown 5s sau khi hết/kích)?</summary>
    public static bool CanScan(Stats enemy)
    {
        if (enemy == null) return false;
        if (HasActive(enemy)) return false;
        return !_cooldownUntil.TryGetValue(enemy, out float until) || Time.time >= until;
    }

    /// <summary>
    /// Player đánh trúng 'enemy' từ vị trí 'fromPos'. Nếu trúng hướng điểm yếu → trả true.
    /// Điểm yếu 1 hướng sẽ bị tiêu (kích nổ) + bật cooldown 5s; điểm yếu cả-8-hướng giữ tới hết thời gian.
    /// </summary>
    public static bool TryConsume(Stats enemy, Vector3 fromPos)
    {
        if (enemy == null) return false;
        if (!_active.TryGetValue(enemy, out var w)) return false;
        if (Time.time > w.expiry) { Clear(enemy); return false; }

        if (w.sector == -1) return true; // cả 8 hướng → luôn trúng, không tiêu
        int hitSector = SectorOf(fromPos, enemy.transform.position);
        if (hitSector == w.sector)
        {
            Clear(enemy); // kích nổ → tiêu + cooldown
            return true;
        }
        return false;
    }

    private static void Clear(Stats enemy)
    {
        _active.Remove(enemy);
        _cooldownUntil[enemy] = Time.time + 5f; // 5s sau mới quét lại
    }
}
