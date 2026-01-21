using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Core Shield", menuName = "Inventory/Core Shield Data")]
public class CoreShieldData : ScriptableObject
{
    public enum Rarity
    {
        Residual_1,
        Stained_2,
        Corrupted_3,
        Condemned_4,
        Anomalous_5
    }
    public string id;
    public string coreShieldName;
    public Sprite icon;
    [TextArea] public string lore;
    [TextArea] public string description;
    public Rarity rarity;
    public int setId;

    [Header("Base Stats")]
    public float armor;
    public float magicResist;

    [Header("Substats")]
    // Đổi từ string sang List để Inspector hiển thị đẹp và code đọc được luôn
    public List<StatModifier> substats = new List<StatModifier>();
    //public string effectConfig; để sau

    public bool playerOnly;
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
