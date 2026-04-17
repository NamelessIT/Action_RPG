using UnityEngine;
using System;
using System.Collections.Generic; // Bắt buộc để dùng Dictionary

public class EquipmentEffectManager : MonoBehaviour
{
    private PlayerController player;
    private AllyStats stats;
    private EquipmentManager eqManager;

    // ==========================================================
    // 1. TẠO CÁC CUỐN "TỪ ĐIỂN" HIỆU ỨNG
    // Mỗi loại Event (OnKill, OnHit...) sẽ có một từ điển riêng
    // ==========================================================
    private Dictionary<string, Action<Stats, bool>> onKillEffects = new Dictionary<string, Action<Stats, bool>>();
    private Dictionary<string, Action<Stats, int, bool, bool>> onHitEffects = new Dictionary<string, Action<Stats, int, bool, bool>>();

    void Awake()
    {
        player = GetComponent<PlayerController>();
        stats = GetComponent<AllyStats>();
        eqManager = GetComponent<EquipmentManager>();

        // Gọi hàm đăng ký hiệu ứng
        RegisterAllEffects();
    }

    // ==========================================================
    // 2. KHAI BÁO & ĐĂNG KÝ HIỆU ỨNG CHO TỪNG ID
    // ==========================================================
    private void RegisterAllEffects()
    {
        // Đăng ký nhóm OnKill (Khi giết địch)
        onKillEffects.Add("WPN_GR_T5_01", Effect_Necronomicon_OnKill); // Gọi đệ

        // Đăng ký nhóm OnHit (Khi đánh trúng)
        onHitEffects.Add("WPN_GR_T4_01", Effect_VoidScythe_OnHit); // Lưỡi hái hư không

        // Sau này có đồ mới, bạn chỉ cần Add("ID_Mới", Hàm_Mới) vào đây!
    }

    // ==========================================================
    // 3. LẮNG NGHE SỰ KIỆN TỪ PLAYER / STATS
    // ==========================================================
    void OnEnable()
    {
        if (player != null) player.OnHitEnemy += HandleOnHitEnemy;
        if (stats != null) stats.OnKillEnemy += HandleOnKillEnemy;
    }

    void OnDisable()
    {
        if (player != null) player.OnHitEnemy -= HandleOnHitEnemy;
        if (stats != null) stats.OnKillEnemy -= HandleOnKillEnemy;
    }

    // ==========================================================
    // 4. HỆ THỐNG "TÌM VÀ CHẠY" TỰ ĐỘNG (ROUTER)
    // ==========================================================
    private void HandleOnKillEnemy(Stats victim, bool isBackstab)
    {
        if (eqManager.currentWeapon == null) return;
        string wpnId = eqManager.currentWeapon.id;

        // Tốc độ tìm kiếm O(1). Nếu ID vũ khí có trong từ điển -> Chạy hàm của nó!
        if (onKillEffects.ContainsKey(wpnId))
        {
            onKillEffects[wpnId].Invoke(victim, isBackstab);
        }
    }

    private void HandleOnHitEnemy(Stats target, int step, bool isHeavy, bool isCrit)
    {
        if (eqManager.currentWeapon == null) return;
        string wpnId = eqManager.currentWeapon.id;

        if (onHitEffects.ContainsKey(wpnId))
        {
            onHitEffects[wpnId].Invoke(target, step, isHeavy, isCrit);
        }
    }

    // ==========================================================
    // 5. NƠI VIẾT CODE CHO TỪNG MÓN ĐỒ (CÁCH NHAU RÕ RÀNG)
    // ==========================================================

    private void Effect_Necronomicon_OnKill(Stats victim, bool isBackstab)
    {
        Debug.Log($"<color=red>Necronomicon:</color> Triệu hồi Ác Quỷ từ xác {victim.name}!");
        // Logic Instantiate quỷ...
    }

    private void Effect_VoidScythe_OnHit(Stats target, int step, bool isHeavy, bool isCrit)
    {
        // Ví dụ: Đòn đánh thường đánh cắp Sin
        if (!isHeavy)
        {
            Debug.Log($"<color=purple>Lưỡi hái Hư Không:</color> Đánh cắp Sin!");
            stats.currentSin += 5f;
            if (stats.currentSin > stats.maxSin) stats.currentSin = stats.maxSin;
        }
    }
}