# Agent Workspace — Action RPG Dev Tool Refactor
**Cập nhật:** 2026-04-15  
**GitNexus index:** 3033 symbols · 6390 edges · 145 flows (indexed 15/04/2026)  
**Manager Agent:** Claude Sonnet 4.6

---

## 📌 YÊU CẦU GỐC

| # | Mô tả gốc |
|---|-----------|
| REQ-1 | Skills Panel: 5 dropdown (DefaultPassive, Passive1, Passive2, Skill, Signature) + EquipSkill/UnequipSkill cho mỗi loại. Bổ sung VerticalLayoutGroup + 5 sub-panel có LayoutElement + HorizontalLayoutGroup |
| REQ-2 | Equipment Panel > Accessories: tách 5 dropdown theo từng AccessoryType |
| REQ-3 | Giải thích SpawnLoot và lý do ResetGame không reset player |
| REQ-4 | Hướng dẫn chi tiết cách test nhặt đồ, xem thông báo, xem túi đồ |

---

## 🗂️ NHÓM CÔNG VIỆC

| Group | Tên | Loại | File chính |
|-------|-----|------|-----------|
| A | Skills Panel Refactor | Code + UI Guide | `DevToolPanel.cs`, `SkillManager.cs` |
| B | Accessories Panel Refactor | Code | `DevToolPanel.cs` |
| C | ResetGame Bug Fix | Code | `DevToolPanel.cs` |
| D | Loot Testing Guide | Documentation only | — |

---

## 🔗 SƠ ĐỒ PHỤ THUỘC

```
[A2: UnequipSkill SkillManager] ──► [A1: DevToolPanel Skills Tab]
                                                │
                                                ▼
                                    [A3: Inspector UI Guide]

[B1: DevToolPanel Accessories Tab]  (độc lập)

[C1: ResetGame Fix]  (độc lập)

[D1: Loot Test Guide]  (độc lập, chỉ đọc code)
```

**Thứ tự thực hiện:**
`A2` → `A1` → `A3` | `B1` | `C1` | `D1` (B, C, D song song sau A)

---

## ✅ TASK BOARD

---

### GROUP A — Skills Panel Refactor

#### TASK A2: Thêm UnequipSkill vào SkillManager.cs
> **Phụ thuộc:** Không có
> **File:** `Assets/_Scripts/_Commons/Items/SkillManager.cs`

| ID | Subtask | Trạng thái |
|----|---------|-----------|
| A2.1 | Uncomment `UnequipPlayerSkill()`, fix tham chiếu `currentPassive` → `currentPassive1`/`currentPassive2` | ⬜ pending |
| A2.2 | Thêm logic tháo passive: gọi `RemovePassiveEffect()` + set slot = null | ⬜ pending |
| A2.3 | Thêm logic tháo skill: gọi `RemoveSkillEffect()` + set `currentSkill = null` | ⬜ pending |
| A2.4 | Thêm logic tháo signature: gọi `RemoveSignatureEffect()` + set `currentSignature = null` | ⬜ pending |
| A2.5 | Uncomment `UnequipSkill()` public method và kết nối gọi `UnequipPlayerSkill()` | ⬜ pending |

---

#### TASK A1: Refactor DevToolPanel.cs — Skills Tab
> **Phụ thuộc:** A2 phải xong trước
> **File:** `Assets/_Scripts/_Commons/Systems/DevToolPanel.cs`

| ID | Subtask | Trạng thái |
|----|---------|-----------|
| A1.1 | Xóa 2 field cũ: `_dropdownSkill1`, `_dropdownSkill2` | ⬜ pending |
| A1.2 | Thêm 5 field mới: `_ddDefaultPassive`, `_ddPassive1`, `_ddPassive2`, `_ddSkill`, `_ddSignature` (TMP_Dropdown) | ⬜ pending |
| A1.3 | Thêm 5 List filtered: `_defaultPassiveSkills`, `_passive1Skills`, `_passive2Skills`, `_activeSkills`, `_signatureSkills` | ⬜ pending |
| A1.4 | Viết `PopulateSkillDropdownsFiltered()` — filter `_skills` theo `SkillType` cho từng dropdown | ⬜ pending |
| A1.5 | Cập nhật `PopulateDropdowns()`: thay 2 lần gọi `PopulateSkillDropdown()` bằng `PopulateSkillDropdownsFiltered()` | ⬜ pending |
| A1.6 | Thêm `CMD_EquipDefaultPassive()` + `CMD_UnequipDefaultPassive()` | ⬜ pending |
| A1.7 | Thêm `CMD_EquipPassive1()` + `CMD_UnequipPassive1()` | ⬜ pending |
| A1.8 | Thêm `CMD_EquipPassive2()` + `CMD_UnequipPassive2()` | ⬜ pending |
| A1.9 | Thêm `CMD_EquipSkill()` + `CMD_UnequipSkill()` | ⬜ pending |
| A1.10 | Thêm `CMD_EquipSignature()` + `CMD_UnequipSignature()` | ⬜ pending |
| A1.11 | Xóa `CMD_ApplySkill1()`, `CMD_ApplySkill2()` (giữ `CMD_CastSkill1()`, `CMD_CastSkill2()` nếu vẫn cần test cast) | ⬜ pending |

---

#### TASK A3: Inspector UI Setup Guide — Skills Panel
> **Phụ thuộc:** A1 xong
> **Loại:** Hướng dẫn — không thay đổi code

```
Hierarchy DevTool Canvas
└── DevToolPanel (DevToolPanel.cs)
    └── PanelSkills  ← Add: VerticalLayoutGroup
        ├── RowDefaultPassive  ← Add: LayoutElement + HorizontalLayoutGroup
        │   ├── Label "Default Passive" (TMP_Text)
        │   ├── Dropdown (TMP_Dropdown) ← Inspector: gán vào _ddDefaultPassive
        │   ├── Button "Equip"          ← onClick → CMD_EquipDefaultPassive()
        │   └── Button "Unequip"        ← onClick → CMD_UnequipDefaultPassive()
        ├── RowPassive1  ← Add: LayoutElement + HorizontalLayoutGroup
        │   ├── Label "Passive 1"
        │   ├── Dropdown ← _ddPassive1
        │   ├── Button "Equip"   → CMD_EquipPassive1()
        │   └── Button "Unequip" → CMD_UnequipPassive1()
        ├── RowPassive2  ← Add: LayoutElement + HorizontalLayoutGroup
        │   ├── Label "Passive 2"
        │   ├── Dropdown ← _ddPassive2
        │   ├── Button "Equip"   → CMD_EquipPassive2()
        │   └── Button "Unequip" → CMD_UnequipPassive2()
        ├── RowSkill  ← Add: LayoutElement + HorizontalLayoutGroup
        │   ├── Label "Skill"
        │   ├── Dropdown ← _ddSkill
        │   ├── Button "Equip"   → CMD_EquipSkill()
        │   └── Button "Unequip" → CMD_UnequipSkill()
        └── RowSignature  ← Add: LayoutElement + HorizontalLayoutGroup
            ├── Label "Signature"
            ├── Dropdown ← _ddSignature
            ├── Button "Equip"   → CMD_EquipSignature()
            └── Button "Unequip" → CMD_UnequipSignature()
```

---

### GROUP B — Accessories Panel Refactor

#### TASK B1: Refactor DevToolPanel.cs — Accessories (5 loại)
> **Phụ thuộc:** Không có
> **File:** `Assets/_Scripts/_Commons/Systems/DevToolPanel.cs`

AccessoryType enum (từ `AccessoryData.cs`): `CoreShard`, `MarkOfSin`, `RelicOfMemory`, `Parasite`, `Chain`

| ID | Subtask | Trạng thái |
|----|---------|-----------|
| B1.1 | Xóa field cũ `_dropdownAccessory` (1 dropdown chung) | ⬜ pending |
| B1.2 | Thêm 5 field: `_ddCoreShard`, `_ddMarkOfSin`, `_ddRelicOfMemory`, `_ddParasite`, `_ddChain` | ⬜ pending |
| B1.3 | Thêm 5 List: `_coreShardList`, `_markOfSinList`, `_relicList`, `_parasiteList`, `_chainList` | ⬜ pending |
| B1.4 | Viết `PopulateAccessoryDropdowns()` — filter `_accessories` theo từng `AccessoryType` | ⬜ pending |
| B1.5 | Cập nhật `PopulateDropdowns()` để gọi `PopulateAccessoryDropdowns()` thay chỗ cũ | ⬜ pending |
| B1.6 | Thêm `CMD_EquipCoreShard()` + `CMD_UnequipCoreShard()` | ⬜ pending |
| B1.7 | Thêm `CMD_EquipMarkOfSin()` + `CMD_UnequipMarkOfSin()` | ⬜ pending |
| B1.8 | Thêm `CMD_EquipRelicOfMemory()` + `CMD_UnequipRelicOfMemory()` | ⬜ pending |
| B1.9 | Thêm `CMD_EquipParasite()` + `CMD_UnequipParasite()` | ⬜ pending |
| B1.10 | Thêm `CMD_EquipChain()` + `CMD_UnequipChain()` | ⬜ pending |
| B1.11 | Xóa `CMD_EquipAccessory()`, `CMD_UnequipAccessory()` cũ | ⬜ pending |

**Inspector UI Guide cho Accessories:**
```
PanelEquipment
├── RowWeapon   (giữ nguyên)
├── RowShield   (giữ nguyên)
└── AccessoriesSection  ← Add: VerticalLayoutGroup
    ├── RowCoreShard     [HorizontalLayoutGroup] → Dropdown _ddCoreShard + Equip + Unequip
    ├── RowMarkOfSin     [HorizontalLayoutGroup] → Dropdown _ddMarkOfSin + ...
    ├── RowRelicOfMemory [HorizontalLayoutGroup] → Dropdown _ddRelicOfMemory + ...
    ├── RowParasite      [HorizontalLayoutGroup] → Dropdown _ddParasite + ...
    └── RowChain         [HorizontalLayoutGroup] → Dropdown _ddChain + ...
```

---

### GROUP C — ResetGame Fix

#### TASK C1: Giải thích + Fix CMD_ResetGame
> **Phụ thuộc:** Không có
> **File:** `Assets/_Scripts/_Commons/Systems/DevToolPanel.cs`

**Phân tích nguyên nhân:**

`CMD_SpawnLootTest()` — **hoạt động như sau:**
Tìm `LootDropper` đầu tiên trong scene → gọi `OnEnemyDeath()` → `LootTable.RollLoot()` → spawn các `DroppedItemBehaviour` prefab quanh vị trí LootDropper. Đây là cách test drop đồ mà không cần kill enemy thật.

`CMD_ResetGame()` — **tại sao player không reset:**
Hiện tại code: xóa save file → reload scene. Nếu `PlayerController` hoặc `GameManager` có `DontDestroyOnLoad`, Player object **không bị destroy** khi reload scene → state cũ (HP, level, skills) vẫn còn nguyên. Scene reload chỉ destroy các object KHÔNG có `DontDestroyOnLoad`.

| ID | Subtask | Trạng thái |
|----|---------|-----------|
| C1.1 | Đọc `PlayerController.cs` và `GameManager.cs` — xác nhận có `DontDestroyOnLoad` không | ⬜ pending |
| C1.2 | Nếu có: thêm `CMD_ResetPlayerStats()` call ngay trước `SceneManager.LoadScene()` trong `CMD_ResetGame()` | ⬜ pending |
| C1.3 | Kiểm tra xem InventoryRuntime có cần clear không, nếu cần thêm `_inventoryRuntime?.ClearAll()` | ⬜ pending |

---

### GROUP D — Loot Testing Guide ✅

#### TASK D1: Pipeline và hướng dẫn test nhặt đồ
> **Loại:** Documentation — ĐÃ GHI XONG

**Pipeline hoạt động:**
```
LootTable (ScriptableObject)
    └── LootDropper.OnEnemyDeath()          ← DevTool: CMD_SpawnLootTest() trigger điều này
            └── DroppedItemBehaviour prefab  ← spawn trong world
                    └── ItemPickupManager     ← gắn trên Player, OverlapSphere scan
                            └── TryPickupNearest() [key F/interact]
                                    ├── InventoryRuntime.AddItem()
                                    └── LootNotificationUI.ShowPickup()
```

**Setup từng bước:**

**[Bước 1] Tạo LootTable ScriptableObject**
- Project → chuột phải → Create → Game/Loot/Loot Table
- Thêm entries: gán `WeaponData` hoặc `AccessoryData`, đặt `dropChance` (0.0–1.0), `displayName`, `icon`

**[Bước 2] Tạo DroppedItem Prefab**
- Tạo Empty GameObject → đặt tên `DroppedItemPrefab`
- Add components: `SpriteRenderer`, `DroppedItemBehaviour`, `SphereCollider` (isTrigger = false)
- **QUAN TRỌNG — Layer:** Tạo layer mới tên `DroppedItem` → gán cho prefab này
- Trong Inspector của `DroppedItemBehaviour`: kéo `SpriteRenderer` vào field `_spriteRenderer`
- Save as prefab vào `Assets/_Prefabs/`

**[Bước 3] Setup LootDropper trong Scene**
- Tạo Empty GameObject trong scene → đặt tên `TestLootDropper`
- Add component: `LootDropper`
- Gán `LootTable` vào `_lootTable`
- Gán `DroppedItemPrefab` vào `_droppedItemPrefab`

**[Bước 4] Setup Player**
- Player GameObject cần có `ItemPickupManager` component
- `_droppedItemLayer` = LayerMask chọn layer `DroppedItem`
- `_inventoryRuntime` = kéo InventoryRuntime object vào
- `_notificationUI` = kéo LootNotificationUI object vào
- `_promptPanel` = Panel UI hiện "Nhấn F để nhặt"
- `_promptItemNameLabel` = TMP_Text hiện tên item

**[Bước 5] Setup LootNotificationUI**
- Trong Canvas → tạo Panel `LootNotifPanel`
- Add component `LootNotificationUI` → ban đầu đặt `SetActive(false)` (script tự bật)
- Tạo `TextMeshProUGUI` prefab (1 dòng) → gán vào `_rowPrefab`
- Tạo empty `RowContainer` bên trong Panel → gán vào `_rowContainer`

**[Bước 6] Input nhặt đồ trong PlayerController**
- Tìm key tương tác (phím F hoặc E) trong `PlayerController.cs`
- Đảm bảo có: `_itemPickupManager.TryPickupNearest()` được gọi khi nhấn phím đó

**[Bước 7] Test nhanh**
1. Play game → mở DevTool (phím V)
2. Tab Player → bấm "Spawn Loot Test"
3. Item xuất hiện gần TestLootDropper
4. Di chuyển Player đến gần → prompt "Nhấn F để nhặt" xuất hiện
5. Nhấn F → notification xuất hiện → mở túi đồ để xem

---

## 📊 PRIORITY TABLE

| Task | Priority | Effort | Dependencies | Status |
|------|----------|--------|-------------|--------|
| A2 | 🔴 HIGH | S | — | ⬜ pending |
| A1 | 🔴 HIGH | M | A2 | ⬜ pending |
| B1 | 🟡 MEDIUM | M | — | ⬜ pending |
| C1 | 🟡 MEDIUM | S | — | ⬜ pending |
| A3 | 🟢 LOW | S (docs) | A1 | ✅ documented |
| D1 | 🟢 LOW | S (docs) | — | ✅ documented |

**Effort:** S=Small(< 30min), M=Medium(30–60min)

---

## 🔄 EXECUTION ORDER

```
Phase 1:  A2  — SkillManager: implement UnequipSkill()
Phase 2:  A1  — DevToolPanel: Skills Tab 5 dropdowns        (cần A2 xong)
Phase 3:  B1  — DevToolPanel: Accessories Tab 5 types       (song song với A)
Phase 4:  C1  — DevToolPanel: ResetGame fix                 (song song với B)
Phase 5:  A3 + D1 — Documentation guides                   (đã viết sẵn)
```

---

## ⚠️ ĐIỂM CHÚ Ý TRƯỚC KHI CODE

1. **UnequipSkill chưa implement** — `SkillManager.UnequipSkill()` bị comment out, có bug tham chiếu `currentPassive` (không tồn tại) phải sửa thành `currentPassive1`/`currentPassive2`
2. **Filtered lists thay thế list chung** — mỗi dropdown dùng list riêng đã filter theo type
3. **DevToolPanel sẽ tăng nhiều field** — dùng `[Header]` phân chia rõ trong Inspector
4. **DontDestroyOnLoad** — phải kiểm tra trước khi viết fix ResetGame

---

---

## 📝 THAY ĐỔI ĐÃ THỰC HIỆN (2026-04-15)

### SkillManager.cs
- ✅ A2: Implement 5 hàm UnequipDefaultPassive/Passive1/Passive2/SkillSlot/SignatureSlot — thay thế block comment cũ bị lỗi tham chiếu

### GameManager.cs
- ✅ C1a: Thêm field `public bool suppressNextSave = false;`
- ✅ C1b: Thêm early-return trong `SaveGame()` khi flag = true (fix bug OnDestroy ghi đè save đã xóa)

### DevToolPanel.cs — REWRITE HOÀN TOÀN
- ✅ A1: Thay 2 dropdown skill cũ → 5 dropdown lọc theo SkillType (DefaultPassive/Passive1/Passive2/Skill/Signature)
- ✅ A1: Thêm 10 CMD_ mới (equip + unequip × 5 slot)
- ✅ B1: Thay 1 dropdown accessory chung → 5 dropdown lọc theo AccessoryType
- ✅ B1: Thêm 10 CMD_ mới cho accessories
- ✅ C1: Fix CMD_ResetGame: set suppressNextSave → xóa save → LoadScene (không còn bug ghi đè)
- ✅ REQ-1: CMD_SpawnLootTest mới: spawn enemy prefab (_spawnEnemyPrefab) trước mặt player → trigger LootDropper.OnEnemyDeath() → destroy enemy

### Cần làm trong Unity Editor (bạn phải tự làm):
1. **Skills Panel**: Thêm VerticalLayoutGroup vào PanelSkills. Tạo 5 sub-panel con, mỗi panel add LayoutElement + HorizontalLayoutGroup. Mỗi panel có: Label + TMP_Dropdown + Button "Equip" + Button "Unequip". Gán dropdown vào các field _ddDefaultPassive/_ddPassive1/_ddPassive2/_ddSkill/_ddSignature trên DevToolPanel Inspector.
2. **Accessories Panel**: Tương tự — 5 panel cho 5 loại phụ kiện, gán _ddCoreShard/_ddMarkOfSin/_ddRelicOfMemory/_ddParasite/_ddChain.
3. **SpawnLoot**: Kéo Enemy Prefab (có LootDropper) vào field `_spawnEnemyPrefab` trên DevToolPanel.
4. **Tag Player**: Đảm bảo GameObject Player có tag "Player" (dùng để tính vị trí spawn).
