# 🎮 BẢNG CÔNG VIỆC DỰ ÁN — Action_RPG Vision System

**Cập nhật lần cuối:** 2026-03-22 10:30 UTC  
**Trạng thái tổng thể:** 🟡 SESSION-2 READY  
**Manager:** Monitoring

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
2026-03-22 10:30 | TASK-001 | ✅ HOÀN TẤT | Tất cả 6 subtasks (VisionConfig, VisionModel, IVisionService, VisionSystem, XML docs, VisionConfig.asset)
2026-03-22 10:30 | TASK-002 | ✅ HOÀN TẤT | Tất cả 6 subtasks (PlayerVisionManager, UpdatePosition, Physics logic, Caching, PlayerController hook, Gizmos debug)
```

---
✅ SESSION-1 COMPLETED

### TASK-001 | INFRA | Tạo Vision System Infrastructure ✅

**Status:** 🟢 COMPLETED | Giao cho Session-1 ✅

**Subtasks:**
- [x] 001-A — VisionConfig.cs tạo ✅ (file: `Assets/_Scripts/_Commons/Systems/VisionConfig.cs`)
- [x] 001-B — VisionModel.cs tạo ✅ (file: `Assets/_Scripts/_Commons/Systems/VisionModel.cs`)
- [x] 001-C — IVisionService.cs tạo ✅ (file: `Assets/_Scripts/_Commons/Interfaces/IVisionService.cs`)
- [x] 001-D — VisionSystem.cs tạo ✅ (file: `Assets/_Scripts/_Commons/Systems/VisionSystem.cs`)
- [x] 001-E — XML docs hoàn thành ✅ (tất cả files có full XML documentation)
- [x] 001-F — VisionConfig.asset tạo ✅ (file: `Assets/_Configs/VisionConfig.asset`)

**Quality Metrics:**
- Code style: Clean Architecture ✅
- XML docs: 100% ✅
- Pure C# (VisionSystem): Yes ✅
- Error handling: Yes ✅
- Physics optimization: OverlapSphereNonAlloc ✅

---

### TASK-002 | PLAYER_VISION | Implement Player Vision Range ✅

**Status:** 🟢 COMPLETED | Giao cho Session-1 ✅

**Subtasks:**
- [x] 002-A — PlayerVisionManager.cs tạo ✅ (file: `Assets/_Scripts/_Commons/PlayerController/PlayerVisionManager.cs`)
- [x] 002-B — UpdatePlayerVisionPosition() implement ✅ (time throttling + position sync)
- [x] 002-C — GetVisibleObjectsInRange() logic ✅ (Physics.OverlapSphereNonAlloc in VisionSystem)
- [x] 002-D — Caching optimization ✅ (non-alloc buffer, list cache, time throttling)
- [x] 002-E — Hook vào PlayerController.cs ✅ (AddComponent logic in Awake)
- [x] 002-F — OnDrawGizmos debug ✅ (DrawWireSphere tested)

**Quality Metrics:**
- Gizmo debug draw: Yes ✅
- Update throttling: Yes (0.1s interval) ✅
- Error checking: Yes ✅
- Subtask markers: Yes [002-A], [002-B] ✅ition trong Update)
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
 🔴 ĐANG CHỜ / CHƯA BẮT ĐẦU

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
- [ ] 003-C — Extend VisionSystem để hỗ trợ multi-entity vision (player + companion)
- [ ] 003-D — Implement MergeVisionRanges() method: combine player_visible + companion_visible objects
- [ ] 003-E — Hook CompanionVisionManager vào CompanionAI.cs hoặc spawn script
- [ ] 003-F — Test: verify companion vision range = 8, objects vượt player range được thêm vào visible list

---

### TASK-004 | VISION_SHARING | Implement Vision Share Logic (Companion → Player)

**Loại:** Feature  
**Ưu tiên:** 🟡 CAO  
**Phụ thuộc:** TASK-002 ✅ + TASK-003 ✅  
**Giao cho:** Session-2 (Feature Specialist)

**Mô tả:**  
Companion vision được chia sẻ cho player.  
Player nhìn thấy mọi object mà companion thấy.

**File liên quan:**
- `Assets/_Scripts/_Commons/Systems/VisionSystem.cs` (extend)

**Subtask:**

- [ ] 004-A — Thêm method MergeCompanionVisionToPlayer() trong VisionSystem
- [ ] 004-B — Thêm event OnVisionRangeChanged để notify khi visible objects thay đổi
- [ ] 004-C — Implement subscription pattern cho UI/Camera listeners
- [ ] 004-D — Test: verify companion vision được merge vào player vision

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

- [ ] 005-A — Tạo file FadeEffectManager.cs (MonoBehaviour)
- [ ] 005-B — Implement GetFadeAlpha() method: calculate alpha based on distance
- [ ] 005-C — Implement ApplyFadeEffect() method: modify material opacity
- [ ] 005-D — Implement LerpMaterialAlpha() helper: smooth transition
- [ ] 005-E — Hook FadeEffectManager.UpdateFade() vào OnVisionRangeChanged event
- [ ] 005-F — Test: verify fade effect gradient works correctly

---

### TASK-006 | INTEGRATION_&_TESTING | Integrate All Components + Testing

**Loại:** Integration / QA  
**Ưu tiên:** 🟢 MEDIUM  
**Phụ thuộc:** TASK-005 ✅  
**Giao cho:** Session-2 (Feature Specialist)

**Mô tả:**  
Gắn toàn bộ hệ thống vision vào game, test end-to-end, viết unit tests.

**File liên quan:**
- `Assets/_Scripts/_Commons/Systems/VisionSystem.cs`
- `Assets/_Scripts/_Commons/PlayerController/PlayerVisionManager.cs`
- `Assets/_Scripts/_Commons/Companion/CompanionVisionManager.cs`
- `Assets/_Scripts/_Commons/Systems/FadeEffectManager.cs`
- `Assets/Tests/VisionSystemTests.cs` (NEW)

**Subtask:**

- [ ] 006-A — Tạo scene test: add player, companion, enemies, terrain objects
- [ ] 006-B — Thêm PlayerVisionManager component vào Player
- [ ] 006-C — Thêm CompanionVisionManager component vào Companion
- [ ] 006-D — Thêm FadeEffectManager component vào scene manager
- [ ] 006-E — Verify Play mode: player sees range 20, companion 8 shared
- [ ] 006-F — Verify fade effect: objects mờ dần đúng
- [ ] 006-G — Tạo VisionSystemTests.cs với unit tests (EditMode)
- [ ] 006-H — Test performance: optimize nếu cần
- [ ] 006-I — Document setup instructions
✅ **SESSION-1 COMPLETED — 2 TASKS, 12 SUBTASKS**
- TASK-001: Infrastructure ✅
- TASK-002: Player Vision ✅
## 📌 GHI CHÚ QUAN TRỌNG

### **Phân chia Session:**

**Session-1** (Logic Specialist):
- TASK-001: Infrastructure (VisionConfig, VisionSystem, VisionModel, Interfaces)
- TASK-002: Player Vision Implementation
2 (Feature Specialist) — READY TO START
- ⏳ TASK-003: Companion Vision (Chờ xác nhận)
- ⏳ TASK-004: Vision Sharing
- ⏳ TASK-005: Fade Effect System
- ⏳ TASK-006: Integration & Testing
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

