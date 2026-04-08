public static class InventoryDragContext
{
    public static InventoryItemRecord ActiveItem { get; private set; }
    public static DraggableItem ActiveInventorySource { get; private set; }
    public static EquipmentSlotUI ActiveEquipmentSource { get; private set; }

    public static bool HasActiveDrag => ActiveItem != null;

    public static void BeginFromInventory(InventoryItemRecord item, DraggableItem source)
    {
        ActiveItem = item;
        ActiveInventorySource = source;
        ActiveEquipmentSource = null;
    }

    public static void BeginFromEquipment(InventoryItemRecord item, EquipmentSlotUI source)
    {
        ActiveItem = item;
        ActiveInventorySource = null;
        ActiveEquipmentSource = source;
    }

    public static void Clear()
    {
        ActiveItem = null;
        ActiveInventorySource = null;
        ActiveEquipmentSource = null;
    }
}