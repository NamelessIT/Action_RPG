# 🎮 BẢNG CÔNG VIỆC DỰ ÁN — Action_RPG Vision System

**Cập nhật lần cuối:** 2026-03-26 10:00 UTC  
**Trạng thái tổng thể:** 🔴 BUGFIX SESSION-3 READY  
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
2026-03-26 10:00 | TASK-007 | 🔴 BUG PHÁT HIỆN | Companion vision không hoạt động — fade dùng player-only data thay vì merged vision
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

## 🔴 BUGFIX — TASK-007 | COMPANION_VISION_FIX | Sửa tầm nhìn companion không hoạt động

**Loại:** Bugfix / Critical  
**Ưu tiên:** 🔴 HIGH  
**Phụ thuộc:** TASK-004, TASK-005  
**Giao cho:** Session-3 (Bugfix Specialist)

### 🐛 BUG REPORT

**Triệu chứng:**
1. Khi player chạy xa companion → companion bị mờ dần (fade) → SAI
2. Các vật thể xung quanh companion (trong range=8 của companion) không nhìn thấy → SAI
3. Tầm nhìn companion hoàn toàn không chia sẻ với player → SAI

### 🔍 NGUYÊN NHÂN GỐC (Root Cause Analysis)

**3 lỗi kiến trúc trong data flow:**

```
HIỆN TẠI (SAI):
PlayerVisionManager.OnVisibleObjectsChanged()
    → nhận visibleObjects = CHỈ từ player's VisionSystem (range=20)
    → gửi thẳng tới FadeEffectManager.UpdateFadeEffects(playerOnlyObjects, playerPos, ...)
    → FadeEffectManager dùng playerPos để tính khoảng cách
    → Companion nằm ngoài range 20 → bị fade
    → Objects gần companion nhưng xa player → bị fade

CẦN SỬA THÀNH:
VisionCoordinator.OnMergedVisionChanged()
    → nhận mergedObjects = player (range=20) + companion (range=8)
    → gửi tới FadeEffectManager.UpdateFadeEffects(mergedObjects, [playerPos, companionPos], ...)
    → FadeEffectManager dùng khoảng cách TỚI NGUỒN GẦN NHẤT (player HOẶC companion)
    → Companion nằm trong merged list → không bị fade
    → Objects gần companion → visible trong merged list → không bị fade
```

**Lỗi 1 — FadeEffectManager nhận player-only data thay vì merged data:**
- File: `PlayerVisionManager.cs` line `OnVisibleObjectsChanged()`
- Hiện tại: `_fadeEffectManager.UpdateFadeEffects(visibleObjects, ...)` với `visibleObjects` = chỉ player OverlapSphere
- Fix: Đổi sang dùng `_visionCoordinator.OnMergedVisionChanged` để nhận merged list

**Lỗi 2 — FadeEffectManager chỉ dùng playerPosition cho distance:**
- File: `FadeEffectManager.cs` method `UpdateFadeEffects()` và `CalculateTargetAlpha()`
- Hiện tại: `CalculateTargetAlpha(bounds, playerPosition, ...)` — chỉ 1 vị trí
- Fix: Truyền danh sách `Vector3[] visionSourcePositions` (player + companion), tính distance tới nguồn gần nhất

**Lỗi 3 — Companion bản thân không được loại trừ khỏi fade:**
- File: `FadeEffectManager.cs`
- Hiện tại: Companion là 1 GameObject có Renderer → bị fade như mọi object khác
- Fix: Thêm exclusion list cho vision owners (player + companion Transforms)

### 📁 FILE CẦN SỬA

| File | Thay đổi | Mức độ |
|------|----------|--------|
| `FadeEffectManager.cs` | Thêm multi-source distance, exclusion list | **MAJOR** |
| `PlayerVisionManager.cs` | Đổi data source sang merged vision | **MAJOR** |
| `VisionCoordinator.cs` | Expose companion position | **MINOR** |
| `CompanionVisionManager.cs` | Không đổi | — |

### ✅ SUBTASKS

- [ ] 007-A — Sửa `FadeEffectManager.UpdateFadeEffects()`: thêm tham số `Vector3[] visionSources` thay vì chỉ `Vector3 playerPosition`
- [ ] 007-B — Sửa `FadeEffectManager.CalculateTargetAlpha()`: tính distance tới nguồn gần nhất trong `visionSources`
- [ ] 007-C — Thêm exclusion system: `FadeEffectManager.SetExcludedTransforms(Transform[])` — các transform không bao giờ bị fade (player, companion)
- [ ] 007-D — Sửa `PlayerVisionManager`: subscribe `_visionCoordinator.OnMergedVisionChanged` thay vì dùng player-only `OnVisibleObjectsChanged` cho fade
- [ ] 007-E — Sửa `PlayerVisionManager`: truyền cả companion position vào FadeEffectManager khi gọi `UpdateFadeEffects()`
- [ ] 007-F — Sửa `PlayerVisionManager`: gọi `_fadeEffectManager.SetExcludedTransforms()` với player + companion transforms
- [ ] 007-G — Verify: companion KHÔNG bị fade khi player chạy xa
- [ ] 007-H — Verify: objects gần companion (trong range 8) vẫn hiển thị rõ
- [ ] 007-I — Verify: fade gradient hoạt động đúng với cả 2 nguồn vision

---

## 📌 GHI CHÚ QUAN TRỌNG

### **Phân chia Session:**

**Session-1** (Logic Specialist) ✅:
- TASK-001: Infrastructure ✅
- TASK-002: Player Vision ✅

**Session-2** (Feature Specialist) ✅:
- TASK-003: Companion Vision ✅
- TASK-004: Vision Sharing ✅
- TASK-005: Fade Effect System ✅
- TASK-006: Integration & Testing (partial) ✅

**Session-3** (Bugfix Specialist) ⏳:
- TASK-007: Companion Vision Bugfix — Sửa 3 lỗi kiến trúc trong data flow

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

## 📈 TRẠNG THÁI

✅ **SESSION-1 COMPLETED** — TASK-001, TASK-002 (12 subtasks)
✅ **SESSION-2 COMPLETED** — TASK-003, TASK-004, TASK-005 (16 subtasks)
✅ **POST-SESSION FIXES** — TASK-006 partial (006-A/B/C/D/H done, 006-E/F/G skipped)
🔴 **SESSION-3 READY** — TASK-007: Companion Vision Bugfix (9 subtasks)