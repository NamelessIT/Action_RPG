using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Accessory", menuName = "Inventory/Accessory Data")]
public class AccessoryData : ScriptableObject
{
    public enum AccessoryType
    {
        CoreShard,
        MarkOfSin,
        RelicOfMemory,
        Parasite,
        Chain
    }
    public enum Rarity
    {
        Residual_1,
        Stained_2,
        Corrupted_3,
        Condemned_4,
        Anomalous_5
    }
    public string id;
    public AccessoryType accessoryType;
    public string AccessoryName;
    [TextArea] public string lore;
    [TextArea] public string description;
    public Sprite icon;
    public Rarity rarity;
    public int setId;

    [Header("Substats")]
    // Đổi từ string sang List để Inspector hiển thị đẹp và code đọc được luôn
    public List<StatModifier> substats = new List<StatModifier>();

    //public string effectConfig; //để sau
    public bool isUnique;
    public bool playerOnly;

    [Header("--- Unique Effect VFX ---")]
    [Tooltip("VFX văng ra khi hiệu ứng đặc biệt kích hoạt")]
    public GameObject triggerVfxPrefab;

    [Tooltip("VFX kéo dài đi theo người (Aura) nếu có")]
    public GameObject auraVfxPrefab;

    private void OnValidate()
    {
        if (substats != null)
        {
            foreach (var sub in substats)
            {
                // Nếu thấy powerMod bằng 0 (do Unity tạo mặc định), tự sửa thành 1
                // Lưu ý: Dùng 0f để so sánh float chuẩn xác
                if (sub.powerMod == 0f)
                {
                    sub.powerMod = 1.0f;
                }
            }
        }
    }
}
