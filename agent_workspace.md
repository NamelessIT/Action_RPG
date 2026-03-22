# 🎮 BẢNG CÔNG VIỆC DỰ ÁN — Action_RPG Vision System

**Cập nhật lần cuối:** 2026-03-22 10:00 UTC  
**Trạng thái tổng thể:** 🟡 ĐANG CHẠY  
**Manager:** Hoạt động

---

## 📊 SƠ ĐỒ PHỤ THUỘC

```
TASK-001 (INFRA)
     ↓
TASK-002 (PLAYER_VISION)  ←→  TASK-003 (COMPANION_VISION)
     ↓                              ↓
TASK-004 (VISION_SHARING)
     ↓
TASK-005 (FADE_EFFECT_SYSTEM)
     ↓
TASK-006 (INTEGRATION_&_TESTING)
```

**Ghi chú:**
- TASK-002 & TASK-003 có thể chạy **song song** (cùng phụ thuộc TASK-001)
- Session-1 sẽ làm: TASK-001 + TASK-002
- Session-2 sẽ làm: TASK-003 + TASK-004 + TASK-005 + TASK-006

---

## 📝 NHẬT KÝ THAY ĐỔI

```
[Đang chờ xác nhận — chưa bắt đầu]
```

---

## 🔴 ĐANG CHỜ / CHƯA BẮT ĐẦU

### TASK-001 | INFRA | Tạo Vision System Infrastructure

**Loại:** Architecture / Core Systems  
**Ưu tiên:** 🔴 CAO — Phải xong trước  
**Phụ thuộc:** Không có  
**Giao cho:** Session-1 (Logic Specialist)

**Mô tả:**  
Xây dựng cơ sở hạ tầng cho hệ thống vision:
- Tạo VisionConfig ScriptableObject (config static data)
- Tạo VisionSystem service (core logic, pure C#)
- Tạo VisionModel runtime state holder
- Tạo interfaces (IVisionService, IVisionModel)

**File liên quan:**
- `Assets/_Scripts/_Commons/Systems/VisionSystem.cs` (service + core logic)
- `Assets/_Scripts/_Commons/Systems/VisionConfig.cs` (SO config)
- `Assets/_Scripts/_Commons/Systems/VisionModel.cs` (runtime state)
- `Assets/_Scripts/_Commons/Interfaces/IVisionService.cs` (interface)

**Subtask:**

- [ ] 001-A — Tạo file VisionConfig.cs (ScriptableObject) với field: playerVisionRange (20), companionVisionRange (8), fadeStartDistance, fadeCompleteDistance
- [ ] 001-B — Tạo file VisionModel.cs (pure C#) để hold runtime state: position, visionRange, visibleObjects[], etc.
- [ ] 001-C — Tạo file IVisionService.cs interface với methods: Initialize(), GetVisibleObjects(), UpdateVision(), AddObstacle(), RemoveObstacle()
- [ ] 001-D — Tạo file VisionSystem.cs service (logic ngôn), không inherit MonoBehaviour, implement IVisionService
- [ ] 001-E — Thêm XML doc comments cho tất cả public members
- [ ] 001-F — Tạo VisionConfig.asset trong Project/Configs/ folder

---

### TASK-002 | PLAYER_VISION | Implement Player Vision Range

**Loại:** Feature  
**Ưu tiên:** 🔴 CAO  
**Phụ thuộc:** TASK-001 ✅  
**Giao cho:** Session-1 (Logic Specialist)

**Mô tả:**  
Implement tầm nhìn cho player (range = 20).  
Player là trung tâm, mọi object ngoài 20m sẽ được track để fade effect.

**File liên quan:**
- `Assets/_Scripts/_Commons/Systems/VisionSystem.cs` (extend)
- `Assets/_Scripts/_Commons/PlayerController/PlayerVisionManager.cs` (NEW)

**Subtask:**

- [ ] 002-A — Tạo file PlayerVisionManager.cs (MonoBehaviour) để manage player vision instance
- [ ] 002-B — Implement UpdatePlayerVisionPosition() method để sync player position → VisionSystem mỗi frame
- [ ] 002-C — Implement GetVisibleObjectsInRange() logic trong VisionSystem: sphere cast, filter by distance, collider raycast
- [ ] 002-D — Cache visible objects list, update incrementally (optimize: không recalc toàn bộ mỗi frame)
- [ ] 002-E — Hook PlayerVisionManager vào PlayerController.cs (call UpdatePlayerVisionPosition trong Update)
- [ ] 002-F — Test: verify player vision range = 20 units đúng thông qua Gizmos debug draw

---

### TASK-003 | COMPANION_VISION | Implement Companion Vision + Share to Player

**Loại:** Feature  
**Ưu tiên:** 🟡 CAO  
**Phụ thuộc:** TASK-001 ✅  
**Giao cho:** Session-2 (Feature Specialist)

**Mô tả:**  
Implement tầm nhìn cho companion (range = 8).  
Companion vision được **merge** với player vision: player có thể thấy cả object mà companion thấy.

**File liên quan:**
- `Assets/_Scripts/_Commons/Systems/VisionSystem.cs` (extend)
- `Assets/_Scripts/_Commons/Companion/CompanionVisionManager.cs` (NEW)

**Subtask:**

- [ ] 003-A — Tạo file CompanionVisionManager.cs (MonoBehaviour) để manage companion vision instance
- [ ] 003-B — Implement UpdateCompanionVisionPosition() method để sync companion position → VisionSystem mỗi frame
- [ ] 003-C — Extend VisionSystem.GetVisibleObjectsInRange() để hỗ trợ companion vision (range = 8)
- [ ] 003-D — Implement MergeVisionRanges() method: combine player_visible + companion_visible objects
- [ ] 003-E — Hook CompanionVisionManager vào CompanionAI.cs hoặc spawn script (call UpdateCompanionVisionPosition)
- [ ] 003-F — Test: verify companion vision range = 8, và objects vượt player range nhưng trong companion range được thêm vào visible list

---

### TASK-004 | VISION_SHARING | Implement Vision Share Logic (Companion → Player)

**Loại:** Feature  
**Ưu tiên:** 🟡 CAO  
**Phụ thuộc:** TASK-002 ✅ + TASK-003 ✅  
**Giao cho:** Session-2 (Feature Specialist)

**Mô tả:**  
Companion vision được chia sẻ cho player.  
Player nhìn thấy mọi object mà companion thấy (hiệu quả: fake player vision extend lên 20 + companion 8 shared).

**File liên quan:**
- `Assets/_Scripts/_Commons/Systems/VisionSystem.cs` (extend)

**Subtask:**

- [ ] 004-A — Thêm method MergeCompanionVisionToPlayer() trong VisionSystem: combine visible objects từ cả player + companion
- [ ] 004-B — Thêm event OnVisionRangeChanged để notify khi danh sách visible objects thay đổi
- [ ] 004-C — Implement subscription pattern: UI/Camera listen to vision changes để update fade effect
- [ ] 004-D — Test: verify when companion sees enemy mà player không thấy, enemy được thêm vào player's visible list

---

### TASK-005 | FADE_EFFECT_SYSTEM | Implement Fade Effect for Out-of-Range Objects

**Loại:** Feature  
**Ưu tiên:** 🟡 TRUNG BÌNH  
**Phụ thuộc:** TASK-004 ✅  
**Giao cho:** Session-2 (Feature Specialist)

**Mô tả:**  
Object ngoài tầm nhìn sẽ **mờ dần** (fade), không biến mất hoàn toàn.  
Vẫn load map — chỉ adjust alpha/material opacity gradient.

**File liên quan:**
- `Assets/_Scripts/_Commons/Systems/FadeEffectManager.cs` (NEW)
- `Assets/_Scripts/_Commons/Systems/VisionSystem.cs` (integrate)

**Subtask:**

- [ ] 005-A — Tạo file FadeEffectManager.cs (MonoBehaviour) để manage fade effects trên objects
- [ ] 005-B — Implement GetFadeAlpha() method: calculate alpha based on distance from vision center (falloff curve)
- [ ] 005-C — Implement ApplyFadeEffect() method: modify material opacity for Renderer components
- [ ] 005-D — Implement LerpMaterialAlpha() helper: smooth transition (target alpha, duration)
- [ ] 005-E — Hook FadeEffectManager.UpdateFade() vào VisionSystem.OnVisionRangeChanged event
- [ ] 005-F — Test: verify objects gradually fade as move away from player/companion vision range, alpha = 1 when visible, alpha ≈ 0 when far

---

### TASK-006 | INTEGRATION_&_TESTING | Integrate All Components + Testing

**Loại:** Integration / QA  
**Ưu tiên:** 🟢 MEDIUM  
**Phụ thuộc:** TASK-005 ✅  
**Giao cho:** Session-2 (Feature Specialist)

**Mô tả:**  
Gắn toàn bộ hệ thống vision vào game:
- Hook PlayerVisionManager vào scene
- Hook CompanionVisionManager vào companion
- Hook FadeEffectManager vào scene
- Test end-to-end
- Viết unit tests cho VisionSystem logic

**File liên quan:**
- `Assets/_Scripts/_Commons/Systems/VisionSystem.cs`
- `Assets/_Scripts/_Commons/PlayerController/PlayerVisionManager.cs`
- `Assets/_Scripts/_Commons/Companion/CompanionVisionManager.cs`
- `Assets/_Scripts/_Commons/Systems/FadeEffectManager.cs`
- `Assets/Tests/VisionSystemTests.cs` (NEW)

**Subtask:**

- [ ] 006-A — Tạo scene test hoặc update existing scene: add player, companion, enemies, terrain objects
- [ ] 006-B — Thêm PlayerVisionManager component vào Player GameObject
- [ ] 006-C — Thêm CompanionVisionManager component vào Companion GameObject
- [ ] 006-D — Thêm FadeEffectManager component vào scene manager
- [ ] 006-E — Verify trong Play mode: player thấy được tất cả object trong range 20, companion range 8 được chia sẻ
- [ ] 006-F — Verify fade effect: objects mờ dần đúng gradient khi vượt range
- [ ] 006-G — Tạo file VisionSystemTests.cs với unit tests cho VisionSystem logic (EditMode)
- [ ] 006-H — Test performance: optimize nếu vision update quá heavy (frame time < 1ms)
- [ ] 006-I — Document setup instructions trong README hoặc SETUP.md

---

## 🟡 ĐANG THỰC HIỆN

### SESSION-1 (Logic Specialist)
- 🟡 TASK-001: Infrastructure (Bắt đầu lúc 10:00 UTC)
- ⏳ TASK-002: Player Vision (Chờ TASK-001 xong)

---

## 🟢 HOÀN THÀNH

(Chuyển task vào đây sau khi Manager xác nhận xong)

---

## 📌 GHI CHÚ QUAN TRỌNG

### **Phân chia Session:**

**Session-1** (Logic Specialist):
- TASK-001: Infrastructure (VisionConfig, VisionSystem, VisionModel, Interfaces)
- TASK-002: Player Vision Implementation

**Session-2** (Feature Specialist):
- TASK-003: Companion Vision + Sharing
- TASK-004: Vision Sharing Logic
- TASK-005: Fade Effect System
- TASK-006: Integration & Testing

### **Quy tắc vàng:**

- ✅ Mỗi subtask chỉ thay đổi **một file hoặc một concept duy nhất**
- ✅ Không làm song song các task có phụ thuộc
- ✅ Báo cáo **ngay** sau khi xong subtask (K chờ hoàn tất cả task)
- ✅ Comment code: `// [XXX-Y] Describe purpose`
- ✅ GitCommit sau mỗi session

### **Architecture Notes:**

- **Core Logic:** VisionSystem (pure C#) — 100% testable
- **Presentation:** PlayerVisionManager, CompanionVisionManager, FadeEffectManager (MonoBehaviour)
- **Config:** VisionConfig (ScriptableObject) — no hardcoded values
- **State:** VisionModel (pure C#) — tracked by VisionSystem

---

## 📈 KPI THEO DÕI

| Metric | Target | Current |
|--------|--------|---------|
| Subtasks completed | 50 | 0 |
| Code coverage (VisionSystem) | 80%+ | 0% |
| Frame time overhead | <1ms | TBD |
| Integration tests | +5 | 0 |

---� **RUNNING**

✅ Xác nhận nhận được. Session-1 được gọi để bắt đầu TASK-001 & TASK-002.

*Manager đang chờ bạn xác nhận trước khi bắt đầu.*

