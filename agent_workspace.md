# 🎮 BẢNG CÔNG VIỆC DỰ ÁN — Action_RPG Vision System

**Cập nhật lần cuối:** 2026-03-26 18:00 UTC  
**Trạng thái tổng thể:** ✅ ALL TASKS COMPLETED  
**Manager:** Done

---

## 📊 SƠ ĐỒ PHỤ THUỘC

```
TASK-001~007 (COMPLETED)
     ↓
TASK-009 (FIX_FLICKERING)  ← Ưu tiên 1 — sửa bug critical
     ↓
TASK-008 (FOG_OF_WAR)      ← Ưu tiên 2 — tính năng mới
```

**Session-4 sẽ làm tuần tự:**
1. TASK-009 trước (sửa flickering — ảnh hưởng FadeEffectManager + PlayerVisionManager)
2. TASK-008 sau (thêm FoW overlay — tạo file mới + sửa VisionConfig + PlayerVisionManager)

**Lý do tuần tự:** TASK-009 refactor FadeEffectManager signature → TASK-008 hook vào PlayerVisionManager sau khi 009 stable.

---

## 📝 NHẬT KÝ THAY ĐỔI

```
2026-03-22 10:30 | TASK-001 | ✅ HOÀN TẤT | Infrastructure (VisionConfig, VisionModel, IVisionService, VisionSystem)
2026-03-22 10:30 | TASK-002 | ✅ HOÀN TẤT | Player Vision (PlayerVisionManager, Physics logic)
2026-03-22 11:00 | TASK-003 | ✅ HOÀN TẤT | Companion Vision (CompanionVisionManager, CompanionAI hook)
2026-03-22 11:00 | TASK-004 | ✅ HOÀN TẤT | Vision Sharing (MergeVisionResults, VisionCoordinator)
2026-03-22 11:00 | TASK-005 | ✅ HOÀN TẤT | Fade Effect System (FadeEffectManager)
2026-03-26 10:00 | TASK-007 | ✅ HOÀN TẤT | Companion Vision Bugfix (merged vision, exclusion, multi-source)
2026-03-26 14:00 | TASK-009 | ✅ HOÀN TẤT | Fix flickering objects — loại bỏ OverlapSphere dependency khỏi fade
2026-03-26 14:00 | TASK-008 | ✅ HOÀN TẤT | Fog of War — URP fullscreen shader + RenderGraph API (URP 17)
2026-03-26 16:00 | TASK-010 | ✅ HOÀN TẤT | Fix fade bouncing (AnimationCurve inversion) + FoW rewrite (shader/pass/feature/controller)
2026-03-26 18:00 | ALL      | ✅ HOÀN TẤT | Tất cả task hoàn tất — user confirmed working
```

---

## 🟢 COMPLETED (Sessions 1-3)

> TASK-001 ✅ | TASK-002 ✅ | TASK-003 ✅ | TASK-004 ✅ | TASK-005 ✅ | TASK-006 (partial) ✅ | TASK-007 ✅

---

## 🔴 SESSION-4 — 2 TASKS

### TASK-009 | FIX_FLICKERING | Loại bỏ nhấp nháy khi di chuyển gần/xa object

**Loại:** Bugfix / Refactor  
**Ưu tiên:** 🔴 HIGH (Critical UX bug)  
**Phụ thuộc:** TASK-007 ✅  
**Giao cho:** Session-4 (thực hiện TRƯỚC TASK-008)

### 🐛 BUG REPORT

**Triệu chứng:**
- Object nhấp nháy (lúc ẩn lúc hiện) khi player di chuyển gần/xa biên giới tầm nhìn
- Object không mờ dần mà giật cục — chỉ biến mất hoàn toàn khi chạy rất xa
- Hiệu ứng fade không smooth, bị bouncing

### 🔍 NGUYÊN NHÂN GỐC (Root Cause Analysis)

```
HIỆN TẠI (SAI):
Physics.OverlapSphereNonAlloc(pos, range=20) → binary result (in/out)
    ↓
Object ở biên (~20 units) → mỗi tick (0.15s) flip giữa visible/not-visible
    ↓
CalculateTargetAlpha(isVisible=true) → target = 1.0
CalculateTargetAlpha(isVisible=false) → target = 0.3 (distance-based)
    ↓
MoveTowards chạy theo target flip-flop → alpha dao động → NHẤP NHÁY

CẦN SỬA THÀNH:
KHÔNG dùng Physics.OverlapSphere cho fade system
    ↓
CalculateTargetAlpha() tính 100% bằng KHOẢNG CÁCH tới vision source gần nhất
    ↓
distance <= fadeStart(18) → alpha = 1.0
fadeStart < distance < fadeComplete(25) → alpha lerp smooth
distance >= fadeComplete(25) → alpha = 0.0
    ↓
Không có binary flip → không nhấp nháy → mượt hoàn toàn
```

**Thêm vấn đề hiệu năng:**  
Hiện tại `UpdateFadeEffects()` được gọi từ event `OnMergedVisionChanged` → phải truyền `visibleObjects` list → gây GC allocations + phức tạp không cần thiết.

**Giải pháp:** FadeEffectManager tự quản lý — chỉ cần biết Transform[] của vision sources. Tự tính distance trong Update(). Không cần OverlapSphere data.

### 📁 FILE CẦN SỬA

| File | Thay đổi | Mức độ |
|------|----------|--------|
| `FadeEffectManager.cs` | Refactor: bỏ visibleObjects, tự quản cycle, 100% distance-based | **MAJOR** |
| `PlayerVisionManager.cs` | Đơn giản hóa: chỉ set vision sources 1 lần, bỏ merged vision callback cho fade | **MAJOR** |

### ✅ SUBTASKS

- [x] 009-A — `FadeEffectManager.cs`: Thêm `SetVisionSources(Transform[])` + `SetFadeDistances(float start, float complete)` ✅
- [x] 009-B — `FadeEffectManager.cs`: Refactor `Update()` — tách thành 2 phase: evaluation + lerp ✅
- [x] 009-C — `FadeEffectManager.cs`: Xóa `UpdateFadeEffects`, `IsObjectVisible`, `BuildVisibleRoots` ✅
- [x] 009-D — `FadeEffectManager.cs`: Sửa `CalculateTargetAlpha()` — 100% distance-based ✅
- [x] 009-E — `PlayerVisionManager.cs`: Đơn giản hóa — chỉ gọi SetVisionSources + SetFadeDistances 1 lần ✅
- [x] 009-F — `PlayerVisionManager.cs`: Xóa OnMergedVisionChanged, BuildVisionSources ✅
- [x] 009-G — Verify: User confirmed — fade smooth, không nhấp nháy ✅

---

### TASK-008 | FOG_OF_WAR | Vùng xám ngoài tầm nhìn

**Loại:** Feature / Visual  
**Ưu tiên:** 🟡 MEDIUM  
**Phụ thuộc:** TASK-009 ✅  
**Giao cho:** Session-4 (thực hiện SAU TASK-009)

### 📋 MÔ TẢ

Ngoài tầm nhìn player (range=20) và companion (range=8), khu vực sẽ phủ màu xám/tối — tạo hiệu ứng "Fog of War". Player chỉ nhìn rõ khu vực trong phạm vi tầm nhìn, phần còn lại bị che phủ.

**Giải pháp:** URP Fullscreen Renderer Feature + Custom Shader
- Shader nhận vị trí player + companion + vision ranges
- Tái tạo world position từ depth buffer cho mỗi pixel
- Tính khoảng cách XZ tới vision source gần nhất
- Nếu ngoài range → áp overlay xám mờ
- Edge mềm (gradient) ở biên giới tầm nhìn — không hard-cut

**Tại sao chọn URP Renderer Feature thay vì overlay mesh:**
- Zero per-object cost — GPU-only, không quét renderer
- Không Z-fighting, không phụ thuộc camera angle
- Hoạt động với mọi object tự động (kể cả terrain, particles)
- Professional, scalable, dễ mở rộng (thêm minimap fog, memory fog...)

### 📁 FILE CẦN TẠO/SỬA

| File | Thay đổi | Mức độ |
|------|----------|--------|
| `Assets/_Scripts/_Commons/Systems/FogOfWar/FogOfWarFeature.cs` | **NEW** — ScriptableRendererFeature | **CREATE** |
| `Assets/_Scripts/_Commons/Systems/FogOfWar/FogOfWarPass.cs` | **NEW** — ScriptableRenderPass (fullscreen blit) | **CREATE** |
| `Assets/_Scripts/_Commons/Systems/FogOfWar/FogOfWarController.cs` | **NEW** — MonoBehaviour cập nhật shader data | **CREATE** |
| `Assets/_Scripts/_Commons/Systems/FogOfWar/FogOfWar.shader` | **NEW** — URP fullscreen shader (depth reconstruct + fog) | **CREATE** |
| `Assets/_Scripts/_Commons/Systems/VisionConfig.cs` | Thêm FoW config fields | **MINOR** |
| `Assets/_Scripts/_Commons/PlayerController/PlayerVisionManager.cs` | Hook FogOfWarController | **MINOR** |
| `Assets/Settings/PC_Renderer.asset` | Add RendererFeature (thủ công trong Unity Editor) | **MANUAL** |

### ✅ SUBTASKS

- [x] 008-A — `VisionConfig.cs`: Thêm FoW config fields (fogColor, fogEdgeSoftness, enableFogOfWar) ✅
- [x] 008-B — Tạo `FogOfWar.shader`: URP fullscreen shader — depth reconstruct + fog overlay ✅
- [x] 008-C — Tạo `FogOfWarPass.cs`: ScriptableRenderPass — RenderGraph API (URP 17 compatible) ✅
- [x] 008-D — Tạo `FogOfWarFeature.cs`: ScriptableRendererFeature — Create + AddRenderPasses ✅
- [x] 008-E — Tạo `FogOfWarController.cs`: MonoBehaviour — Shader.SetGlobalXXX mỗi frame ✅
- [x] 008-F — `PlayerVisionManager.cs`: Tạo FogOfWarController + truyền player/companion/config ✅
- [x] 008-G — **MANUAL** (User): Đã setup Renderer Feature trong PC_Renderer.asset ✅
- [x] 008-H — Verify: User confirmed — FoW hoạt động, vùng ngoài range bị phủ xám ✅

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

**Session-3** (Bugfix Specialist) ✅:
- TASK-007: Companion Vision Bugfix ✅

**Session-4** (Anti-Flicker + FoW) ✅:
- TASK-009: Fix Flickering ✅
- TASK-008: Fog of War ✅
- TASK-010: Hotfix (fade bouncing + FoW rewrite + URP 17 RenderGraph) ✅

### **Quy tắc vàng:**

- ✅ Mỗi subtask chỉ thay đổi **một file hoặc một concept duy nhất**
- ✅ Không làm song song các task có phụ thuộc
- ✅ Báo cáo **ngay** sau khi xong subtask
- ✅ Comment code: `// [XXX-Y] Describe purpose`
- ✅ GitCommit sau mỗi session
- ✅ TASK-009 **PHẢI HOÀN TẤT** trước khi bắt đầu TASK-008

### **Architecture Notes:**

- **URP Pipeline:** Forward Renderer (PC_Renderer.asset + Mobile_Renderer.asset)
- **Post-Processing:** DefaultVolumeProfile.asset (LiftGammaGain, SplitToning)
- **Core Logic:** VisionSystem (pure C#) — 100% testable
- **Presentation:** PlayerVisionManager, CompanionVisionManager, FadeEffectManager (MonoBehaviour)
- **Config:** VisionConfig (ScriptableObject) — no hardcoded values
- **FoW Approach:** URP ScriptableRendererFeature + depth-based fullscreen shader

---

## 📈 TRẠNG THÁI

✅ **SESSION-1 COMPLETED** — TASK-001, TASK-002
✅ **SESSION-2 COMPLETED** — TASK-003, TASK-004, TASK-005
✅ **SESSION-3 COMPLETED** — TASK-007 (Companion Vision Bugfix)
✅ **SESSION-4 COMPLETED** — TASK-009 (Fix Flickering) + TASK-008 (Fog of War) + TASK-010 (Hotfix)

---

## 🚀 SESSION-4 EXECUTION PROMPT

> Copy toàn bộ nội dung bên dưới vào conversation mới để thực thi Session-4.

---

Bạn là **Session-4 Execution Agent** cho dự án Action_RPG Vision System.

### NHIỆM VỤ

Thực hiện tuần tự 2 task từ file `agent_workspace.md`:
1. **TASK-009 (FIX_FLICKERING)** — sửa trước
2. **TASK-008 (FOG_OF_WAR)** — thêm sau

### CONTEXT

- **Project:** Unity C# / URP (Universal Render Pipeline)
- **URP Renderer:** `Assets/Settings/PC_Renderer.asset` (ForwardRenderer, chưa có custom RendererFeature)
- **Camera:** Cinemachine
- **Architecture:** Clean Architecture — Pure C# services + MonoBehavior adapters + ScriptableObject config

### ROOT CAUSE — FLICKERING

`FadeEffectManager` hiện dùng `Physics.OverlapSphereNonAlloc` result (binary visible/not-visible) để quyết định alpha. Tại biên tầm nhìn (~20 units), object flip giữa visible/not-visible mỗi 0.15s → `CalculateTargetAlpha()` trả target alpha dao động → `MoveTowards` chạy theo → nhấp nháy.

**Giải pháp:** Bỏ hoàn toàn `isVisible` parameter. `FadeEffectManager` chỉ dùng **DISTANCE** tới vision source transforms gần nhất để tính alpha. `FadeEffectManager` tự quản lý — lưu `Transform[]` references tới vision sources, tự tính distance mỗi evaluation interval.

---

### TASK-009: FIX_FLICKERING

**Files cần sửa:**
- `Assets/_Scripts/_Commons/Systems/FadeEffectManager.cs`
- `Assets/_Scripts/_Commons/PlayerController/PlayerVisionManager.cs`

#### Subtasks (thực hiện tuần tự):

**009-A** — `FadeEffectManager.cs`: Thêm fields + 2 public method mới:

```csharp
// [009-A] Vision source transforms — FadeEffectManager reads position from these every evaluation
private Transform[] _visionSourceTransforms;
private float _fadeStartDist;
private float _fadeCompleteDist;

/// <summary>
/// [009-A] Set vision source transforms (player, companion). Called once during init.
/// FadeEffectManager will read .position from these transforms every evaluation interval.
/// </summary>
public void SetVisionSources(params Transform[] sources)
{
    _visionSourceTransforms = sources;
}

/// <summary>
/// [009-A] Set fade distances from VisionConfig. Called once during init.
/// </summary>
public void SetFadeDistances(float fadeStart, float fadeComplete)
{
    _fadeStartDist = fadeStart;
    _fadeCompleteDist = fadeComplete;
}
```

**009-B** — `FadeEffectManager.cs`: Refactor `Update()` — tách thành 2 phase rõ ràng:
1. Mỗi `EVALUATION_INTERVAL` (0.15s): gọi `EvaluateAllRenderers()` — iterate `_cachedRenderers`, tính target alpha bằng distance tới `_visionSourceTransforms`
2. Mỗi frame: `LerpAllRenderers()` — lerp `_currentAlpha` → `_targetAlpha` bằng `MoveTowards`

```csharp
private void Update()
{
    if (_visionSourceTransforms == null || _visionSourceTransforms.Length == 0) return;

    // Phase 1: Evaluate targets periodically (not every frame)
    if (Time.time - _lastEvaluationTime >= EVALUATION_INTERVAL)
    {
        _lastEvaluationTime = Time.time;
        RefreshRendererCacheIfNeeded();
        EvaluateAllRenderers();
    }

    // Phase 2: Smooth lerp every frame (flicker-free)
    LerpAllRenderers();
}

/// <summary>
/// [009-B] Evaluate target alpha for all cached renderers based on distance to vision sources.
/// Called every EVALUATION_INTERVAL, NOT every frame.
/// </summary>
private void EvaluateAllRenderers()
{
    Vector3[] sourcePositions = GetVisionSourcePositions();
    if (sourcePositions.Length == 0) return;

    for (int i = 0; i < _cachedRenderers.Length; i++)
    {
        var renderer = _cachedRenderers[i];
        if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;

        // [007-C] Skip excluded transforms — always fully visible
        if (IsExcludedTransform(renderer.transform))
        {
            SetTargetAlpha(renderer, 1f);
            continue;
        }

        float targetAlpha = CalculateTargetAlpha(
            renderer.bounds, sourcePositions, _fadeStartDist, _fadeCompleteDist);
        SetTargetAlpha(renderer, targetAlpha);
    }
}

/// <summary>
/// [009-B] Get current positions from vision source transforms. Filters null/destroyed.
/// </summary>
private Vector3[] GetVisionSourcePositions()
{
    int count = 0;
    for (int i = 0; i < _visionSourceTransforms.Length; i++)
        if (_visionSourceTransforms[i] != null) count++;

    var positions = new Vector3[count];
    int idx = 0;
    for (int i = 0; i < _visionSourceTransforms.Length; i++)
    {
        if (_visionSourceTransforms[i] != null)
            positions[idx++] = _visionSourceTransforms[i].position;
    }
    return positions;
}

/// <summary>
/// [009-B] Per-frame smooth alpha lerp. Separated from evaluation for clarity.
/// </summary>
private void LerpAllRenderers()
{
    if (_targetAlphas.Count == 0) return;

    float dt = Time.deltaTime * _transitionSpeed;
    var toRemove = new List<Renderer>();

    foreach (var kvp in _targetAlphas)
    {
        Renderer rend = kvp.Key;
        if (rend == null) { toRemove.Add(rend); continue; }

        float target = kvp.Value;
        float current;
        if (!_currentAlphas.TryGetValue(rend, out current))
            current = GetMaterialAlpha(rend);

        float newAlpha = Mathf.MoveTowards(current, target, dt);
        _currentAlphas[rend] = newAlpha;
        ApplyAlphaToRenderer(rend, newAlpha);
    }

    for (int i = 0; i < toRemove.Count; i++)
    {
        _targetAlphas.Remove(toRemove[i]);
        _currentAlphas.Remove(toRemove[i]);
    }
}
```

**009-C** — `FadeEffectManager.cs`: **Xóa** các method không cần nữa:
- `UpdateFadeEffects(List<Collider>, Vector3[], float, float)` — public method cũ, thay bằng `SetVisionSources()` + `SetFadeDistances()` + tự chạy trong `Update()`
- `IsObjectVisible(Renderer, HashSet<Collider>, HashSet<Transform>)` — không cần vì bỏ OverlapSphere dependency
- `BuildVisibleRoots(List<Collider>)` — không cần
- `GetRootTransform(Transform)` — không cần (chỉ dùng bởi `BuildVisibleRoots`)

**009-D** — `FadeEffectManager.cs`: Sửa `CalculateTargetAlpha()` — **bỏ param `isVisible`**:

```csharp
/// <summary>
/// [009-D] Calculate target alpha based PURELY on distance to nearest vision source.
/// No OverlapSphere dependency → no binary flip → no flickering.
/// </summary>
private float CalculateTargetAlpha(
    Bounds objectBounds,
    Vector3[] visionSources,
    float fadeStartDist,
    float fadeCompleteDist)
{
    // [009-D] Find minimum distance to ANY vision source
    float distance = float.MaxValue;
    for (int i = 0; i < visionSources.Length; i++)
    {
        float d = Vector3.Distance(
            objectBounds.ClosestPoint(visionSources[i]), visionSources[i]);
        if (d < distance) distance = d;
    }

    if (distance <= fadeStartDist) return 1f;
    if (distance >= fadeCompleteDist) return 0f;

    float normalizedDist = (distance - fadeStartDist) / (fadeCompleteDist - fadeStartDist);
    return Mathf.Clamp01(1f - _fadeFalloff.Evaluate(normalizedDist));
}
```

**009-E** — `PlayerVisionManager.cs`: Đơn giản hóa `TryInitializeFadeEffects()`:
- Gọi `_fadeEffectManager.SetVisionSources(...)` với player transform + companion transform (nếu có)
- Gọi `_fadeEffectManager.SetFadeDistances(_visionConfig.FadeStartDistance, _visionConfig.FadeCompleteDistance)`
- **Bỏ** subscribe `OnMergedVisionChanged` cho fade trong `TryInitializeCoordinator()` — FadeEffectManager giờ tự quản

**009-F** — `PlayerVisionManager.cs`: **Xóa** các method/logic không cần:
- Method `OnMergedVisionChanged(List<Collider>)` — FadeEffectManager tự quản, không cần callback
- Method `BuildVisionSources()` — FadeEffectManager tự đọc position từ Transform references
- Bỏ subscribe/unsubscribe `OnMergedVisionChanged` trong `TryInitializeCoordinator()` và `OnDestroy()`
- Giữ `OnVisibleObjectsChanged()` nhưng bỏ fade logic bên trong — chỉ giữ cho gameplay event nếu cần

**009-G** — Verify: Compile check, đảm bảo không lỗi cú pháp

---

### TASK-008: FOG_OF_WAR

**Thực hiện SAU TASK-009 hoàn tất.**

**Files cần tạo mới:**
- `Assets/_Scripts/_Commons/Systems/FogOfWar/FogOfWarFeature.cs`
- `Assets/_Scripts/_Commons/Systems/FogOfWar/FogOfWarPass.cs`
- `Assets/_Scripts/_Commons/Systems/FogOfWar/FogOfWarController.cs`
- `Assets/_Scripts/_Commons/Systems/FogOfWar/FogOfWar.shader`

**Files cần sửa:**
- `Assets/_Scripts/_Commons/Systems/VisionConfig.cs`
- `Assets/_Scripts/_Commons/PlayerController/PlayerVisionManager.cs`

#### Subtasks (thực hiện tuần tự):

**008-A** — `VisionConfig.cs`: Thêm Fog of War config fields:

```csharp
[Header("Fog of War")]
[SerializeField] private bool _enableFogOfWar = true;
[SerializeField] private Color _fogColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
[SerializeField] private float _fogEdgeSoftness = 3f;

public bool EnableFogOfWar => _enableFogOfWar;
public Color FogColor => _fogColor;
public float FogEdgeSoftness => _fogEdgeSoftness;
```

**008-B** — Tạo `FogOfWar.shader`: URP compatible fullscreen shader:
- **Inputs:** `_PlayerPos` (Vector4), `_CompanionPos` (Vector4), `_PlayerRange` (float), `_CompanionRange` (float), `_FogColor` (Color), `_EdgeSoftness` (float), `_HasCompanion` (float 0/1)
- **Fragment logic:**
  1. Sample `_CameraDepthTexture` → reconstruct world position từ depth + inverse VP matrix (`unity_MatrixInvVP`)
  2. Tính XZ distance tới player: `length(worldPos.xz - _PlayerPos.xz)`
  3. Tính XZ distance tới companion (nếu `_HasCompanion > 0.5`)
  4. `visibility = max(playerVisibility, companionVisibility)` với mỗi cái = `1 - smoothstep(range - softness, range, dist)`
  5. `finalColor = lerp(_FogColor, sceneColor, visibility)`
- **QUAN TRỌNG:** Render BEFORE post-processing, KHÔNG ảnh hưởng UI

**008-C** — Tạo `FogOfWarPass.cs`: `ScriptableRenderPass` class:
- `renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing`
- Setup RTHandle, configure Blit source → temp → dest
- `Execute()`: draw fullscreen quad với FogOfWar material
- `Dispose()`: release RTHandle
- Dùng `Blitter.BlitCameraTexture()` hoặc `cmd.Blit()` tùy URP version

**008-D** — Tạo `FogOfWarFeature.cs`: `ScriptableRendererFeature` class:
- Expose `Shader fogOfWarShader` field
- `Create()`: tạo `FogOfWarPass` instance, tạo `Material` từ shader
- `AddRenderPasses()`: thêm pass vào renderer
- `Dispose()`: cleanup material

**008-E** — Tạo `FogOfWarController.cs`: MonoBehaviour quản lý runtime shader data:
- References: Material (lấy từ `FogOfWarFeature`), Transform player, Transform companion, VisionConfig
- `Update()`: set shader globals — `Shader.SetGlobalVector("_PlayerPos", ...)`, `Shader.SetGlobalFloat("_PlayerRange", ...)`, v.v.
- `SetVisionSources(Transform player, Transform companion)`
- `SetConfig(VisionConfig config)`

**008-F** — `PlayerVisionManager.cs`: Tích hợp FogOfWarController:
- Trong `TryInitializeFadeEffects()` hoặc method init riêng
- Tìm/tạo `FogOfWarController` object
- Gọi `fogOfWarController.SetVisionSources(transform, _companionVisionManager?.transform)`
- Gọi `fogOfWarController.SetConfig(_visionConfig)`

**008-G** — **MANUAL (User phải làm trong Unity Editor):**
1. Mở `Assets/Settings/PC_Renderer.asset`
2. Click "Add Renderer Feature" → chọn "Fog Of War Feature"
3. Assign `FogOfWar.shader` vào settings của Renderer Feature
4. Play → verify vùng ngoài range 20 bị phủ xám, biên mềm, companion range 8 tạo vùng sáng riêng

**008-H** — Verify: compile check + ghi chú cho user test thủ công

---

### QUY TẮC THỰC THI

1. **Đọc `AGENTS.md`** trước khi code — tuân thủ gitnexus workflow (impact analysis trước khi sửa)
2. **Đọc file TRƯỚC khi sửa** — không sửa blind, luôn dùng `read_file` trước `replace_string_in_file`
3. **Comment code:** `// [XXX-Y] Describe purpose` (VD: `// [009-A] Vision source transforms`)
4. **Mỗi subtask xong → báo cáo ngay** — không batch
5. **TASK-009 PHẢI HOÀN TẤT** trước khi bắt đầu TASK-008
6. **Cập nhật `agent_workspace.md`**: đánh dấu `[x]` cho subtask hoàn tất, cập nhật trạng thái TASK
7. **Sau khi xong cả 2 task** → cập nhật `agent_workspace.md` status = `SESSION-4 COMPLETED`
8. **Namespace convention:** `Game.Features.Vision.Systems` cho Systems, `Game.Features.Vision.Data` cho Config
9. **Không tạo file thừa** — chỉ tạo file được liệt kê trong subtasks