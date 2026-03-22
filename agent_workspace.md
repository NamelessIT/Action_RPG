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
2026-03-22 11:00 | TASK-003 | ✅ HOÀN TẤT | Tất cả 6 subtasks (CompanionVisionManager, UpdateCompanionPosition, Multi-entity support, Merging logic, CompanionAI hook, Testing)
2026-03-22 11:00 | TASK-004 | ✅ HOÀN TẤT | Tất cả 4 subtasks (MergeVisionResults method, OnMergedVisionChanged event, VisionCoordinator, Testing)
2026-03-22 11:00 | TASK-005 | ✅ HOÀN TẤT | Tất cả 6 subtasks (FadeEffectManager, GetFadeAlpha, ApplyFadeEffect, LerpMaterialAlpha, Integration, Testing)
```

---

## ✅ SESSION-2 IN PROGRESS — 3 TASKS COMPLETED

### TASK-003 | COMPANION_VISION | Implement Companion Vision ✅

**Status:** 🟢 COMPLETED | Giao cho Session-2 ✅

**Subtasks:**
- [x] 003-A — CompanionVisionManager.cs tạo ✅
- [x] 003-B — UpdateCompanionVisionPosition() implement ✅
- [x] 003-C — Multi-entity support ✅
- [x] 003-D — MergeVisionRanges() logic ✅
- [x] 003-E — Hook vào CompanionAI.cs ✅
- [x] 003-F — Test companion vision range=8 ✅

**Quality Metrics:**
- Companion range: 8 ✅
- Merging logic: Yes ✅
- Integration: Yes ✅

---

### TASK-004 | VISION_SHARING | Implement Vision Sharing ✅

**Status:** 🟢 COMPLETED | Giao cho Session-2 ✅

**Subtasks:**
- [x] 004-A — MergeVisionResults() method ✅
- [x] 004-B — OnMergedVisionChanged event ✅
- [x] 004-C — VisionCoordinator created ✅
- [x] 004-D — Test merging logic ✅

**Quality Metrics:**
- Merge logic: Yes ✅
- Event publishing: Yes ✅
- Coordinator: Yes ✅

---

### TASK-005 | FADE_EFFECT_SYSTEM | Implement Fade Effects ✅

**Status:** 🟢 COMPLETED | Giao cho Session-2 ✅
## 🟡 ĐANG CHỜ

### TASK-006 | INTEGRATION_&_TESTING | Integrate All Components + Testing

**Loại:** Integration / QA  
**Ưu tiên:** 🟢 MEDIUM  
**Phụ thuộc:** TASK-005 ✅  
**Giao cho:** BẠN (User)

**Mô tả:**  
Final integration + end-to-end testing.

**File liên quan:**
- `Assets/Tests/VisionSystemTests.cs` (NEW)
- Scene test setup

**Subtask:**

- [x] 006-A — Tạo scene test: add player, companion, enemies, terrain objects
- [x] 006-B — Thêm PlayerVisionManager component vào Player
- [x] 006-C — Thêm CompanionVisionManager component vào Companion
- [x] 006-D — Thêm FadeEffectManager component vào scene manager
- [-] 006-E — Verify Play mode: player sees range 20, companion 8 shared (Bỏ theo yêu cầu)
- [-] 006-F — Verify fade effect: objects mờ dần đúng (Bỏ theo yêu cầu)
- [-] 006-G — Tạo VisionSystemTests.cs với unit tests (EditMode) (Bỏ theo yêu cầu)
- [x] 006-H — Test performance: optimize nếu cần (Đã tối ưu FadeEffectManager: renderer cache + HashSet lookup)
- [ ] 006-I — Document setup instructions

---
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

- [x] 006-A — Tạo scene test: add player, companion, enemies, terrain objects
- [x] 006-B — Thêm PlayerVisionManager component vào Player
- [x] 006-C — Thêm CompanionVisionManager component vào Companion
- [x] 006-D — Thêm FadeEffectManager component vào scene manager
- [-] 006-E — Verify Play mode: player sees range 20, companion 8 shared (Bỏ theo yêu cầu)
- [-] 006-F — Verify fade effect: objects mờ dần đúng (Bỏ theo yêu cầu)
- [-] 006-G — Tạo VisionSystemTests.cs với unit tests (EditMode) (Bỏ theo yêu cầu)
- [x] 006-H — Test performance: optimize nếu cần (Đã tối ưu FadeEffectManager: renderer cache + HashSet lookup)
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
| Subtasks completed | 50 | 28 |
| Code coverage (VisionSystem) | 80%+ | 0% |
| Frame time overhead | <1ms | TBD |
| Integration tests | +5 | 0 |

---� **RUNNING**

✅ Xác nhận nhận được. Session-1 được gọi để bắt đầu TASK-001 & TASK-002.

*Manager đang chờ bạn xác nhận trước khi bắt đầu.*


✅ **SESSION-2 COMPLETED — 3 TASKS, 16 SUBTASKS**
- TASK-003: Companion Vision ✅
- TASK-004: Vision Sharing ✅
- TASK-005: Fade Effect System ✅

🔴 **TASK-006: Integration & Testing — WAITING FOR USER**