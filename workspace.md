# Action_RPG Shared Workspace - Claude Code <-> Codex

Day la workspace quan ly cong viec chung, khong phai prompt dung mot lan.

- Claude Code: coding agent, chi trien khai `ACTIVE TASKS` va viet bao cao vao `CLAUDE_REPORT`.
- Codex: manager/reviewer/git owner, doi chieu source/diff, chay lai test, quan ly task va quyet dinh commit/push.
- User: thuc hien va xac nhan cac viec thu cong trong Unity Editor.

## MANUAL UNITY EDITOR TASKS

Tat ca checkbox trong muc nay phai duoc user xac nhan truoc commit/push cuoi.

Trang thai sau checkpoint code EAM: cac viec duoi day chua duoc Codex tick vi can user thao tac trong Unity Editor/Play Mode. Theo yeu cau user ngay vong nay, commit code EAM duoc phep di truoc; cac muc nay tiep tuc la checklist editor sau push.

- [ ] **[GAP] M-01 - Xac minh scene dang dirty**
  - File user-owned: `Action_RPG/Assets/Scenes/OdoScene.unity`.
  - Diff hien tai chi tang `EnemyCombat.skillEffects.Array.size` len `1` tren mot prefab instance.
  - Trong Inspector, mo `Skill Effects[0]`, xac nhan `type`, `duration`, `impactLevel`, resistance va interrupt flags dung y do.
  - Save scene sau khi xac nhan. Claude/Codex khong duoc revert thay doi nay.
  - Evidence: ghi enemy/object da gan va gia tri effect vao day khi xong.

- [ ] **M-02 - Gan nut DevTool game speed**
  - Tren `Canvas.prefab > DevToolPanel > Combat`, tao/gan bon Button vao:
    - `CMD_SetGameSpeedSlow()` - 0.25x.
    - `CMD_SetGameSpeedHalf()` - 0.5x.
    - `CMD_SetGameSpeedNormal()` - 1x.
    - `CMD_SetGameSpeedFast()` - 2x.
  - Test mo Inventory/SkillTree/DevTool van pause 0x; dong panel tro ve gameplay speed da chon.

- [ ] **M-03 - Tao va gan sample Enemy Attack Module assets**
  - Thuc hien sau khi `EnemyAttackModuleData` compile thanh cong.
  - Tao trong `Assets/Resources/Datas/EnemyAttackModules/`:
    - `EAM_Melee_Sword`, `EAM_Melee_Dagger`, `EAM_Melee_Heavy_AOE`.
    - `EAM_Ranged_Bow`, `EAM_Mage_TargetBolt`, `EAM_Mage_GroundAOE`.
    - `EAM_DashStrike`.
  - Gan effects/timing/range theo tung style va luu day du `.asset.meta`.

- [ ] **M-04 - Tao/gan projectile va telegraph prefabs**
  - Tao hoac chon projectile prefab cho Bow va Mage.
  - Kiem tra Collider, Rigidbody neu dung, layer collision, `EnemyProjectile`, VFX va lifetime.
  - Tao/gan telegraph prefab cho GroundTargetAOE neu runtime field yeu cau.

- [ ] **M-05 - Gan module cho enemy prefabs**
  - Enemy melee fallback de module null phai van danh nhu cu.
  - Gan basic/skill module cho it nhat: Sword/Dagger, Bow, Mage Ground AoE va Dash Strike.
  - Kiem tra prefab overrides khong mat reference sau khi move/save.

- [ ] **[GAP TRUOC COMMIT] M-06 - Play Mode acceptance matrix**
  - Fallback melee, sword/dagger, ranged projectile, Ground AoE va Dash Strike hoat dong.
  - Stun, Knockback, Airborne, Root, Silence va Slow dung rule.
  - Stun co the cleanse bang skill duoc phep; Airborne khong the cleanse/action.
  - Player hit moi camera shake; Companion/Enemy hit khong camera shake.
  - Dev speed va UI pause khong dap `Time.timeScale` cua nhau.
  - Boss dash giu dung dash multiplier khi bi Slow; EnemyAI khong ghi de destination; disable giua dash khong ket invincibility/override.
  - Console khong co error do. Ghi ket qua/evidence vao checkbox nay.

## RULES AND OWNERSHIP

### Claude Code

1. Truoc moi vong, tu tao Work Todo va chi lam task dang co trong `ACTIVE TASKS`.
2. Khong commit/push, khong stage, khong revert user changes va khong tu sua/xoa checkbox task.
3. Truoc khi sua symbol, bat buoc chay GitNexus impact theo `AGENTS.md`; HIGH/CRITICAL phai dung va bao user/Codex.
4. Khong sua `OdoScene.unity` hoac manual assets tru khi task ghi ro va user cho phep.
5. Sau khi code/test, chi duoc APPEND report moi ben trong marker `CLAUDE_REPORT_START/END`; khong xoa/sua report vong truoc. Chi Codex duoc reset/compact report sau review.
6. Bao cao phai co: task ID, symbols/files/API doi, impact risk, lenh test va output, smoke evidence, task partial, test chua chay va regression moi.
7. Khong tu them noi dung vao `FINAL COMMIT DRAFT`.

### Codex

1. Doc `CLAUDE_REPORT`, doi chieu source va diff thuc te; khong chap nhan task chi dua tren bao cao.
2. Chay lai build/static checks/repro quan trong va review Unity YAML/GUID khi co asset changes.
3. Task dat acceptance moi duoc xoa khoi `ACTIVE TASKS`; sau do append mot bullet ngan vao `FINAL COMMIT DRAFT`.
4. Task partial/fail phai duoc viet lai theo van de thuc te; regression moi phai them task ID moi.
5. Chi Codex duoc reset/compact `CLAUDE_REPORT` ve placeholder sau review. Neu Codex chua reset, Claude phai append report vong moi, khong overwrite report cu.
6. Chi Codex duoc commit/push, va chi khi:
   - `ACTIVE TASKS` rong.
   - Toan bo `MANUAL UNITY EDITOR TASKS` da check, tru khi user yeu cau ro checkpoint code truoc manual.
   - Build/static checks pass.
   - GitNexus `detect-changes` chi ra dung pham vi, khong con HIGH/CRITICAL chua xu ly.
   - Working tree khong chua thay doi ngoai pham vi da xac nhan.
7. Commit cuoi phai dung nguyen subject/body tich luy trong `FINAL COMMIT DRAFT`, sau do push branch `test`.

### Shared Safety Rules

- Khong dung `git reset --hard`, `git checkout --`, xoa/move recursive hoac restore file user-owned.
- Khong commit asset ngoai pham vi chi vi no dang staged/dirty.
- Legacy compatibility chi duoc giu co chu dich va phai co acceptance grep ngan usage moi.
- Moi coroutine/temp modifier phai cleanup idempotent khi complete, interrupt, disable va unequip.

## VERIFIED BASELINE

- Branch: `test`; baseline commit checkpoint: `c6934eb` (`origin/test`) sau commit CE/INT.
- Baseline build ngay 2026-06-22: 0 error, 17 warning co san.
- `git diff --check`: pass.
- Dirty user change: chi `OdoScene.unity`, them `skillEffects.Array.size = 1`; phai giu nguyen toi khi user xac minh.
- Da co va khong lap lai thanh task:
  - `CombatEffectInfo` va `DamageInfo.effects/AddEffect/BridgeLegacyEffects`.
  - `Stats.ApplyCombatEffects` cho Stun, Knockback, Airborne, Root va Silence o muc co ban.
  - Action-lock queries va guard co ban cho Player/Enemy/Companion.
  - Companion direct Airborne call da route qua effect wrapper.
  - Camera shake ownership hien chi co Player hit call `CombatFeel.OnHit`.
  - DevTool speed API va `UIPauseManager.GameplayTimeScale` da co trong code.
  - Player attack interrupt da stop channeled attack va refund heavy armor idempotent.
  - P0-CE-01/01R da duoc Codex verify: per-effect Super Armor, effect resistance, source-safe immunity, Slow strongest-wins va movement/attack cadence cho Player/Enemy/Companion, Matrix passive lifecycle, Boss external movement override.
  - P0-BUILD-01 da xoa UnityEditor imports khoi runtime EnemyAI.
  - P0-BUILD-02 da xoa `Mono.Cecil.Cil` import thua va sua newline BossCombat.
  - P0-CE-02A da migrate DamageHelper, PlayerController va EnemyCombat sang effect list, giu impact/source/last-wins va skill-effect precedence khong double CC.
  - P0-CE-02B/C da migrate Weapon/Accessory/CoreShield/Companion/Skill/Signature/Passive CC va Slow gameplay emitters sang CombatEffect pipeline.
  - P0-INT-01A da them `InterruptContext`, `OnInterruptedContext` va bridge legacy `OnInterrupted` trong Stats.
  - P0-INT-01B da cho SkillManager consume interrupt context khi player charge ACC_CH_T4_03, voi cost/cooldown theo flag.
  - P0-INT-01C da cho EnemyCombat consume interrupt context, cleanup attack state va commit/uncommit skill cooldown theo flag.
  - P1-EAM-01 da tao `EnemyAttackModuleData` va module-aware API trong EnemyCombat/EnemyAI, module null van fallback melee cu.
  - P1-EAM-02A da implement melee/local runtime cho Enemy Attack Module: Single/Sweep/Thrust/CircleAOE, module effect clone, damageMultiplier cho basic/skill va cleanup state.
  - P1-EAM-03 da them `EnemyProjectile` runtime cho ProjectileDirectional/ProjectileTargeted: Player/Ally target, lifetime, dedupe, safe destroy va cloned module effects.
  - P1-EAM-02B da implement DashStrike, ProjectileDirectional, ProjectileTargeted va GroundTargetAOE runtime trong EnemyCombat, dung EnemyProjectile, telegraph optional va dash movement override cleanup.
  - P1-EAM-04 da cho EnemyAI dung `GetBasicRange()` cho attack decision/stopping/chase/spacing, module null van fallback `basicAttackRange` cu.
- Da xac minh con no:
  - Khong con code task active cho EAM trong workspace nay.
  - Con sample module assets/Play Mode matrix la viec Unity Editor/manual.
  - Follow-up thiet ke sau EAM neu can: GroundTargetAOE knockback sourcePosition theo tam AoE, projectile magic atkType, ConeBreath/SelfBuff/Summon runtime.

## ACTIVE TASKS

Khong con code task active sau checkpoint EAM. Vong tiep theo chi them task moi khi user/Codex tao.

## ACCEPTANCE AND TEST MATRIX

Static checks toi thieu moi vong:

```powershell
dotnet build Action_RPG\Action_RPG.slnx
git diff --check
rg -n "\.Airborne\(|isStun\s*=|isKnockback\s*=|stunDuration\s*=|knockbackForce\s*=" Action_RPG/Assets/_Scripts
npx.cmd gitnexus detect-changes
```

Final acceptance:

- Build 0 error; warning moi phai duoc giai thich.
- Legacy grep chi con compatibility/data definitions da duoc Codex chap nhan.
- Enemy module null va bay style sample deu pass Play Mode.
- CC overlap, interrupt/cooldown, camera ownership va time-scale matrix pass.
- Unity Console khong co error do va YAML/GUID diff khong mat reference.

## FINAL COMMIT DRAFT

Subject:

`TODO`

Body - Codex chi append task da verify vao marker nay. Da reset sau checkpoint commit EAM.

<!-- CODEX_COMMIT_NOTES_START -->
- Chua co task moi nao duoc verify sau checkpoint EAM.
<!-- CODEX_COMMIT_NOTES_END -->

## CLAUDE_REPORT - temporary handoff

<!-- CLAUDE_REPORT_START -->
Claude report placeholder.

Codex da dong toan bo code task EAM va commit checkpoint. Task tiep theo chi bat dau khi user/Codex tao ACTIVE TASK moi.

Rule nhac lai: Claude phai APPEND report vong moi trong marker nay, khong xoa/sua report cu. Chi Codex duoc reset/compact report sau review.
<!-- CLAUDE_REPORT_END -->
