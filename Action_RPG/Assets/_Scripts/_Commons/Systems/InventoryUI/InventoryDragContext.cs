public static class InventoryDragContext
{
    public static InventoryItemRecord ActiveItem              { get; private set; }

    // ── Nguồn từ inventory slot mới (InventorySlotView) ──────────────
    public static InventorySlotView   ActiveInventorySlotView { get; private set; }
    public static int                 ActiveInventorySlotSource { get; private set; } = -1;

    // ── Nguồn từ inventory cũ (DraggableItem) — giữ để tương thích ──
    public static DraggableItem       ActiveInventorySource   { get; private set; }

    // ── Nguồn từ equipment slot ───────────────────────────────────────
    public static EquipmentSlotUI     ActiveEquipmentSource   { get; private set; }

    public static bool HasActiveDrag => ActiveItem != null;

    /// <summary>Bắt đầu kéo từ InventorySlotView (hệ thống 25 ô mới).</summary>
    public static void BeginFromInventorySlot(InventoryItemRecord item, int slotIndex, InventorySlotView source)
    {
        ActiveItem                = item;
        ActiveInventorySlotSource = slotIndex;
        ActiveInventorySlotView   = source;
        ActiveInventorySource     = null;
        ActiveEquipmentSource     = null;
    }

    /// <summary>Bắt đầu kéo từ DraggableItem (hệ thống cũ — backward compat).</summary>
    public static void BeginFromInventory(InventoryItemRecord item, DraggableItem source)
    {
        ActiveItem                = item;
        ActiveInventorySource     = source;
        ActiveInventorySlotView   = null;
        ActiveInventorySlotSource = -1;
        ActiveEquipmentSource     = null;
    }

    /// <summary>Bắt đầu kéo từ EquipmentSlotUI.</summary>
    public static void BeginFromEquipment(InventoryItemRecord item, EquipmentSlotUI source)
    {
        ActiveItem                = item;
        ActiveEquipmentSource     = source;
        ActiveInventorySource     = null;
        ActiveInventorySlotView   = null;
        ActiveInventorySlotSource = -1;
    }

    public static void Clear()
    {
        ActiveItem                = null;
        ActiveInventorySource     = null;
        ActiveInventorySlotView   = null;
        ActiveInventorySlotSource = -1;
        ActiveEquipmentSource     = null;
    }
}