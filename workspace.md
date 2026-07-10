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
  - Sau task P2-DATA, uu tien gan module vao `EnemyData`; `EnemyCombat.basicAttackModule/skillAttackModule` chi dung nhu local override/tam thoi.
  - Kiem tra prefab overrides khong mat reference sau khi move/save.

- [ ] **M-07 - Tao va gan Character/Enemy/Companion data assets sau refactor**
  - Tao folder neu chua co: `Assets/Resources/Datas/Enemies/`.
  - Tao asset data cho tung enemy archetype, vi du: `ED_Odo`, `ED_Orc_Warrior`, `ED_Boss_Golem`, `ED_Archer`, `ED_Mage`.
  - Set toi thieu: `enemyID`, `enemyName`, `monsterRank`, `baseHp`, `baseMoveSpeed`, `baseAttackSpeed`, `expReward`.
  - Gan `basicAttackModule` va `skillAttackModule` vao EnemyData thay vi gan truc tiep len tung prefab, tru truong hop can override rieng.
  - Tren prefab/scene enemy, gan `EnemyStats.data = EnemyData tuong ung`.
  - Neu code phase tao them Player/Companion data asset, tao asset theo dung menu/folder Claude ghi trong report va gan vao prefab/root tuong ung.
  - Evidence: ghi danh sach enemy prefab da gan data va module nao.

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
8. Batch mode: Claude duoc phep lam nhieu task lien tiep trong `ACTIVE TASKS` trong cung mot vong neu cac task do LOW/MEDIUM risk, build/check pass sau tung cum hop ly, va khong gap blocker. Khi gap HIGH/CRITICAL, migration serialization, test fail khong tu fix chac chan, hoac can Editor/manual decision thi dung va append report.

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
  - Base config van dang nam truc tiep tren `Stats`/`EnemyStats`/prefab; can tach source-of-truth data khoi runtime logic theo Flyweight/Data-Driven Design.
  - Refactor can bao gom ca Player/Companion/GameManager/SkillTree, khong chi Enemy, vi cac he nay dang doc/ghi truc tiep `Stats.base*`, `initialBaseHp`, `maxSin`, `baseAttackSpeed`, `baseMoveSpeed`.
  - P2-DATA-00 da scan xong: doi storage `Stats` root field -> nested `CharacterRuntimeStats` la CRITICAL vi Unity khong migrate root -> nested bang `[FormerlySerializedAs]`; phai tach phase an toan truoc khi chuyen storage that.
  - P2-DATA-01A da duoc Codex verify: BattleMageSignature va ArcaneEmpowerment Slow da migrate khoi direct `baseMoveSpeed` sang CombatEffect Slow; build pass 0 error sau khi user cai lai .NET.
  - P2-DATA-01C design da duoc Codex chap nhan o muc huong dan: khong code storage migration luc nay; uu tien P2-DATA-01B/Option C truoc, chi lam nested `CharacterRuntimeStats` khi co approve rieng va migration strategy bao toan serialization.
  - P2-DATA-01B/02/03 da duoc Codex static-verify: build pass 0 error, grep direct writes dat acceptance, EnemyCombat runtime path di qua resolved module helper. Play Mode/M-07 van can user test duong `EnemyData != null`.
  - BattleMageSignature `SlowRefreshDuration = tickRate * 1.5f` duoc Codex chap nhan tam thoi de tranh slow flicker giua cac tick; chi chinh sau Play Mode neu cam thay slow tat qua cham.
  - P2-DATA-FIX-01 da duoc Codex verify: CatalystSignature apply/revert attack speed dung chieu, build pass 0 error.
  - P2-DATA-FIX-02 da duoc Codex review va APPROVE Option 1: `PlayerRuntimeState.baseHp` chi la in-memory, khong persist vao `PlayerStateSaveData`, nen khong can migration save cu.

## ACTIVE TASKS

### P2-DATA-01A - Migrate legacy Slow khoi direct baseMoveSpeed writes

**CODEX VERIFIED DONE - Claude bo qua task nay va tiep tuc tu P2-DATA-01B.** Build `dotnet build Action_RPG\Action_RPG.slnx` pass 0 error; grep acceptance khong con direct slow write trong scope.

Muc tieu: giam risk truoc khi cham storage `Stats`. Cac skill/effect Slow khong duoc sua `enemy.baseMoveSpeed` truc tiep nua; phai route qua `CombatEffectInfo`/`ApplyEffect(Slow)` de dung pipeline duration/resistance/strongest-wins.

- [ ] Chay impact cho `BattleMageSignature`, `ArcaneEmpowerment`, `Stats.ApplyCombatEffects`/Slow handler truoc khi sua.
- [ ] Migrate `BattleMageSignature` cac doan slow/restore dang dung `enemy.baseMoveSpeed = ...` sang CombatEffect Slow.
- [ ] Migrate `ArcaneEmpowerment` slow/restore dang tru/trả `enemy.baseMoveSpeed` sang CombatEffect Slow.
- [ ] Khong con coroutine restore speed rieng cho cac slow nay neu Slow handler chung da quan ly duration.
- [ ] Giu behavior gameplay: dung target, dung duration, dung percent slow, stack voi slow khac theo rule strongest-wins hien co.
- [ ] Khong sua scene/prefab/manual assets.

Acceptance:

- [ ] `rg -n "enemy\\.baseMoveSpeed|baseMoveSpeed\\s*[*/+\\-]?=" Action_RPG/Assets/_Scripts/_Commons/Items/Skills Action_RPG/Assets/_Scripts/_Commons/Systems` khong con slow gameplay direct write, tru definition/API duoc giai thich.
- [ ] Build 0 error, `git diff --check` pass.
- [ ] Test logic trong report: Slow tu BattleMage/Arcane van apply va tu het han qua handler chung.

### P2-DATA-01B - Them Stats API va migrate direct writes an toan, giu storage cu

**CODEX STATIC VERIFIED DONE - Claude bo qua task nay tru khi sua follow-up ben duoi lam hong acceptance.** Build `dotnet build Action_RPG\Action_RPG.slnx` pass 0 error; grep direct write chi con data class hop le (`PlayerRuntimeState`/`currentPlayerState`).

Muc tieu: them API/source boundary truoc, chua doi shape serialization. Public fields trong `Stats` co the van ton tai tam thoi de bao toan prefab/scene data, nhung gameplay code moi phai di qua API.

- [ ] Chay impact cho `Stats`, `AllyStats`, `PlayerStats`, `GameManager`, `SkillTreeRuntime`, `StatAllocationUI`, `EquipmentManager`, `CompanionEquipmentManager`, `CatalystSignature`, `DarkDemonAI`, `DevToolPanel`.
- [ ] Them API ro nghia tren `Stats`/`AllyStats`/`PlayerStats` neu phu hop:
  - `ApplyBaseRuntimeStats(...)` hoac API tuong duong de load base stats tu save/data.
  - `CaptureRuntimeStats(...)` hoac API tuong duong de save.
  - `AddBaseAttribute(StatType type, float amount)`.
  - `AddBaseHp(float amount)` / `SetInitialBaseHp(float value)` neu can.
  - `SetBaseAttackSpeed(float value)` va `MultiplyBaseAttackSpeedTemporarily(...)` hoac helper cleanup-safe neu can.
  - `SetMaxStamina(float value)` / `SetMaxSin(float value)` neu can.
- [ ] API phase nay duoc phep doc/ghi field cu ben trong `Stats`; khong doi serialized field shape.
- [ ] Migrate direct writes trong `GameManager` load/save sang API/capture, giu save behavior cu.
- [ ] Migrate `SkillTreeRuntime` stat unlock sang API, khong `_allyStats.base* += val` truc tiep.
- [ ] Migrate `StatAllocationUI` sang API, read UI co the dung getter/property neu can.
- [ ] Migrate `EquipmentManager` weapon attack speed set sang API.
- [ ] Migrate Companion direct writes trong `CompanionEquipmentManager`/`CatalystSignature` sang API/helper cleanup-safe.
- [ ] Migrate `DevToolPanel` reset/load character stats sang API.
- [ ] Migrate `DarkDemonAI` reset stats sang API hoac helper ro nghia.
- [ ] Khong doi `Stats` public field thanh nested container trong phase nay.

Acceptance:

- [ ] Build 0 error, `git diff --check` pass.
- [ ] Grep direct write con lai duoc report va phan loai:
  - Allowed: ben trong `Stats`/API, data class rieng nhu `WeaponData`, `PlayerRuntimeState`, `PlayerStateSaveData`, read-only UI.
  - Not allowed: gameplay code ngoai API ghi `stats.base*`, `initialBaseHp`, `maxStamina`, `baseAttackSpeed`, `baseMoveSpeed`.
- [ ] Stat allocation, SkillTree stat node, equipment attack speed, dev tool reset, companion buff van compile va behavior duoc giai thich.

### P2-DATA-01C - Design migration cho CharacterRuntimeStats, CHUA code neu chua duoc approve

**CODEX VERIFIED DONE/DEFERRED - Claude khong code storage migration.** Ket luan hien tai: lam P2-DATA-01B truoc, giu storage root de bao toan Unity serialization; nested `CharacterRuntimeStats` chi lam khi co approve rieng.

Muc tieu: thiet ke cach doi storage that cua `Stats` ma khong mat Unity serialized data. Day la CRITICAL phase, khong code neu chua co approve moi cua Codex/User.

- [ ] De xuat migration strategy cu the:
  - Option A: giu legacy `[SerializeField, HideInInspector]` root backing fields mot thoi gian, `ISerializationCallbackReceiver.OnAfterDeserialize()` copy sang `CharacterRuntimeStats`.
  - Option B: tao Editor migration tool de rewrite prefab/scene YAML/data asset sang nested shape, chay tren branch rieng.
  - Option C: khong doi nested storage, chi doi access boundary/API va de field root hidden/serialized nhu migration-safe compromise.
- [ ] Ghi ro option nao giu data, option nao can user backup/Play Mode verify.
- [ ] Khong code storage migration trong task nay neu chua duoc Codex/User explicit approve.

Acceptance:

- [ ] Report co migration plan khong mau thuan voi Unity serialization.
- [ ] Codex/User quyet co lam 01C hay defer.

### P2-DATA-02 - EnemyData source-of-truth cho enemy archetype

**CODEX STATIC VERIFIED DONE - Claude bo qua task nay.** `EnemyData` runtime config hop le, `EnemyStats.ApplyEnemyData()` chay truoc `base.Start()`, fallback `data == null` giu behavior cu. Can M-07 de test Play Mode duong `data != null`.

Muc tieu: tat ca thong so goc bat bien cua enemy nam trong `EnemyData` ScriptableObject, khong gan cung tren tung prefab/scene instance.

- [ ] Task nay duoc phep lam cung batch voi P2-DATA-01A/01B neu build/check pass va khong cham scene/prefab.
- [ ] Tao `EnemyData : ScriptableObject`, menu: `[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Action_RPG/Enemy Data")]`.
- [ ] Field toi thieu: `enemyID`, `enemyName`, `monsterRank`, `baseHp`, `baseMoveSpeed`, `baseAttackSpeed`, `expReward`, `EnemyAttackModuleData basicAttackModule`, `EnemyAttackModuleData skillAttackModule`.
- [ ] Khong dua runtime state vao `EnemyData`: `currentHp`, timers, target, coroutine, aggro runtime, cooldown runtime.
- [ ] Them `public EnemyData data;` vao `EnemyStats`.
- [ ] `EnemyStats.Start()` phai apply `EnemyData` truoc `base.Start()`.
- [ ] `SetupResistances()` phai chay sau khi `monsterRank` da duoc copy tu data.
- [ ] Xoa hoac guard dong hard-code `base.baseAttackSpeed = 0.667f`; khong duoc de no ghi de data. Neu can default fallback: chi set khi `data == null` va attack speed chua hop le.
- [ ] `data == null` van fallback inspector/runtimeStats cu de enemy prefab cu khong vo.
- [ ] Them comment ngan trong `EnemyStats`: `EnemyData` la source-of-truth cho archetype, inspector runtime stats chi la fallback/override tam thoi.

Acceptance:

- [ ] Enemy co data copy dung ID/rank/hp/move speed/attack speed/exp truoc runtime combat.
- [ ] Enemy khong data van chay nhu truoc.
- [ ] `EnemyStats.Start()` khong con ghi de attack speed data thanh 0.667.
- [ ] Build/diff/GitNexus pass.

### P2-DATA-03 - EnemyCombat lay EAM tu EnemyData, local field chi la override

**CODEX STATIC VERIFIED DONE - Claude bo qua task nay.** Runtime path dung `GetResolvedBasicModule()`/`GetResolvedSkillModule()`; local override -> EnemyData -> fallback legacy.

Muc tieu: EAM module gan tap trung trong EnemyData; field tren `EnemyCombat` chi dung de override rieng/tam test.

- [ ] Task nay duoc phep lam cung batch voi P2-DATA-02 neu build/check pass.
- [ ] Them helper resolve module trong `EnemyCombat`, vi du `GetResolvedBasicModule()` va `GetResolvedSkillModule()`.
- [ ] Thu tu uu tien: local override tren `EnemyCombat` neu khac null -> `EnemyStats.data.basicAttackModule/skillAttackModule` -> null fallback legacy.
- [ ] `HasSkill`, `CanUseSkill()`, `GetSkillRange()`, `GetBasicRange()`, `PerformBasicAttack()`, `PerformSkillAttack()` phai dung resolved module, khong doc thang local field tru khi trong helper.
- [ ] Module null van dung melee fallback cu.
- [ ] Skill legacy (`enemySkill`, `skillCooldown`, `skillRange`, `skillEffects`) van hoat dong khi khong co skill module.
- [ ] Debug/log display name null-safe neu data/module thieu ten.
- [ ] `EnemyAI` tiep tuc chi goi API cua `EnemyCombat`, khong chua logic projectile/AoE.

Acceptance:

- [ ] `rg -n "basicAttackModule|skillAttackModule" Action_RPG/Assets/_Scripts/_Commons/Enemies/EnemyCombat.cs` cho thay runtime path di qua helper hoac co ly do ro.
- [ ] Module gan trong EnemyData duoc EnemyCombat su dung khi local field null.
- [ ] Local module tren EnemyCombat override duoc EnemyData module.
- [ ] Build/diff/GitNexus pass.

### P2-DATA-04 - Verify Player/GameManager/SkillTree sau API migration

Muc tieu: sau P2-DATA-01B, verify rieng cac flow Player/save/SkillTree khong bi regression.

- [ ] Behavior cu phai giu: stat allocation, skill tree stat node, save/load, equipment bonus, UI stat detail.
- [ ] Khong tao PlayerData asset bat buoc neu save system hien tai da la source-of-truth phu hop; neu tao, phai ghi ro manual asset task va migration.
- [ ] Report ro cac direct writes da migrate o P2-DATA-01B va cac read-only UI usage con lai.

Acceptance:

- [ ] `rg -n "playerStats\\.(base|initialBase|maxSin|maxStamina)|_allyStats\\.(base|initialBase|maxSin|maxStamina)" Action_RPG/Assets/_Scripts` khong con write direct ngoai compatibility/API duoc Codex chap nhan.
- [ ] Save/load player giu dung stat sau restart Play Mode.
- [ ] SkillTree unlock va stat allocation cap nhat UI/runtime/save nhu truoc.
- [ ] Build/diff/GitNexus pass.

### P2-DATA-FIX-01 - Sua dau buff attack speed trong CatalystSignature

**CODEX VERIFIED DONE - Claude bo qua task nay.** CatalystSignature da doi apply = nhan speed, revert = chia speed; build pass 0 error.

Muc tieu: fix bug san co duoc phat hien khi migrate API. `baseAttackSpeed` la toc do don/giay, khong phai cooldown; vi vay buff attack speed phai nhan speed khi apply va chia khi revert.

- [ ] Chay impact cho `CatalystSignature` va companion signature flow truoc khi sua.
- [ ] Trong nhanh companion khong phai `AllyStats`, doi apply tu `MultiplyBaseAttackSpeed(1f / (1f + compAttackSpeedBuff))` sang `MultiplyBaseAttackSpeed(1f + compAttackSpeedBuff)`.
- [ ] Doi revert tu `MultiplyBaseAttackSpeed(1f + compAttackSpeedBuff)` sang `MultiplyBaseAttackSpeed(1f / (1f + compAttackSpeedBuff))`.
- [ ] Giu nhanh `AllyStats` hien co neu no dang dung cooldown/attack interval rieng va khong lien quan `baseAttackSpeed`.
- [ ] Report ro day la bug fix co doi behavior: companion se danh nhanh hon dung theo mo ta buff.

Acceptance:

- [ ] Build 0 error, `git diff --check` pass.
- [ ] Apply/revert doi xung, khong leak speed sau unequip/end signature.

### P2-DATA-FIX-02 - Xu ly GameManager save/load baseHp bat doi xung

**CODEX VERIFIED DONE - Claude bo qua task nay.** Option 1 da code: `GameManager.SavePlayerState` ghi `snap.initialBaseHp` vao `currentPlayerState.baseHp`; build full solution pass 0 error; khong can save migration.

Muc tieu: ngan HP goc phinh dan qua moi chu ky save/load. Hien tai save ghi `snap.baseHp` (dan xuat) vao `currentPlayerState.baseHp`, load lai nap vao `initialBaseHp`.

- [ ] Chay impact cho `GameManager`, `PlayerRuntimeState`, `PlayerStateSaveData`, va player save/load flow.
- [x] Codex da xac nhan source hien tai: `PlayerStateSaveData` khong co `baseHp`; `PlayerRuntimeState.GetSaveData()` khong ghi `baseHp`; `LoadFromSave()` khong doc `baseHp`. Khong co migration save cu.
- [x] APPROVED Option 1: sua dung 1 dong trong `GameManager.SavePlayerState`: `currentPlayerState.baseHp = snap.initialBaseHp;` thay vi `snap.baseHp`.
- [x] Cap nhat comment quanh dong save de khong con noi "giu nguyen hanh vi cu save ghi derived baseHp".
- [x] Khong rename `PlayerRuntimeState.baseHp` trong task nay; rename thanh `initialBaseHp` neu muon se la task MEDIUM rieng sau.
- [x] Khong lam mat save cu; khong reset stat player ngoai y muon.

Acceptance:

- [x] Report xac nhan khong can migration va da code Option 1.
- [x] Save -> load -> save khong lam `initialBaseHp` tang them `20*level` moi lan theo static review: save dung `snap.initialBaseHp`, khong con caller doc `snap.baseHp`.
- [x] Build 0 error, `git diff --check` pass.

### P2-DATA-05 - Verify Companion sau API migration

**CODEX STATIC VERIFIED DONE - Claude bo qua task nay.** Grep Companion khong con direct write vao legacy base fields; build full solution pass 0 error. Play Mode equip/unequip companion van nam trong M-06 neu user muon test gameplay.

Muc tieu: sau P2-DATA-01B, verify Companion co source-of-truth rieng theo module hien co, khong leak stat khi equip/unequip/effect.

- [x] Khong cho Companion dung weapon/core shield/accessory cua Player; giu thiet ke 3 slot rieng neu da co.
- [x] Temporary buffs/effects cua Companion phai cleanup idempotent khi unequip/disable/death/rescan theo static review cac path hien co; Play Mode evidence neu can nam o M-06.
- [x] Behavior cu phai giu: equip/unequip companion module, passive/effect manager, AI combat cadence, save/load neu co.
- [x] Report ro Companion direct writes da migrate o P2-DATA-01B va usage con lai neu co.

Acceptance:

- [x] Grep direct writes cua Companion vao legacy base fields chi con trong API/compatibility duoc Codex chap nhan.
- [x] Companion equip/unequip module khong double-apply/leak stat theo static review; Play Mode evidence neu can nam o M-06.
- [x] Build/diff pass; GitNexus detect-changes da chay va bao CRITICAL do scope refactor rong, can user approve checkpoint push.

### P2-DATA-06 - Legacy cleanup gate va diagnostics

**CODEX STATIC VERIFIED DONE - Claude bo qua task nay.** Legacy grep chi con data/runtime state hoac read-only UI duoc chap nhan; build full solution pass 0 error; manual Editor tasks van giu rieng.

Muc tieu: sau khi Player/Enemy/Companion migrate, legacy compatibility chi con o noi chu dich; workspace/report ro viec Editor can lam.

- [x] Chay grep tong ket cac legacy field usages va phan loai:
  - Compatibility property/API trong `Stats` duoc phep.
  - Read-only UI display duoc phep neu qua API/property.
  - Direct write gameplay moi khong duoc phep.
- [x] Neu compatibility properties con can giu, them comment va follow-up task ro rang; neu khong can, xoa chung trong cung patch va build lai.
- [x] Cap nhat `MANUAL UNITY EDITOR TASKS` neu code tao data asset moi hoac doi cach gan component/data.
- [x] `CLAUDE_REPORT` phai ghi: migration order, fallback behavior, files changed, tests run, grep result, editor tasks can user lam.
- [x] Khong commit/push/stage trong phase Claude; Codex se quyet checkpoint push.

Acceptance:

- [x] `dotnet build Action_RPG\Action_RPG.slnx` pass 0 error.
- [x] `git diff --check` pass.
- [x] `npx.cmd gitnexus detect-changes` da chay; scope dung ky vong refactor rong nhung risk = CRITICAL, can user approve checkpoint push.
- [x] Codex review khong thay scene/prefab/manual asset bi sua ngoai pham vi.
- [x] Manual workflow ro: tao/gang EnemyData, test save/load/stat/equip/combat.

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

`feat: tach data enemy va chan ghi truc tiep Stats`

Body - Codex chi append task da verify vao marker nay. Da reset sau checkpoint commit EAM.

<!-- CODEX_COMMIT_NOTES_START -->
- P2-DATA-01A: Migrate Slow cua BattleMageSignature va ArcaneEmpowerment sang CombatEffect pipeline, bo direct write `baseMoveSpeed`.
- P2-DATA-01C: Ghi nhan design migration Stats an toan; defer nested `CharacterRuntimeStats` cho toi khi co migration strategy duoc approve.
- P2-DATA-01B: Them Stats base-write API va migrate GameManager, SkillTree, StatAllocation, Equipment, Companion, DevTool sang API ma khong doi Unity serialization shape.
- P2-DATA-02: Them EnemyData lam source-of-truth cho enemy archetype va apply truoc Stats.Start.
- P2-DATA-03: Cho EnemyCombat resolve module theo thu tu local override -> EnemyData -> legacy fallback.
- P2-DATA-FIX-01: Sua CatalystSignature de buff attack speed nhan speed khi apply va chia lai khi revert.
- P2-DATA-FIX-02: Sua GameManager save/load baseHp de ghi initialBaseHp goc, ngan HP phinh qua cac chu ky save/load trong cung session.
- P2-DATA-05: Verify Companion sau API migration; khong con direct write vao legacy base fields trong scope Companion.
- P2-DATA-06: Chay legacy cleanup/static gate; direct writes con lai chi nam trong data/runtime state duoc phep va read-only UI.
<!-- CODEX_COMMIT_NOTES_END -->

## CLAUDE_REPORT - temporary handoff

<!-- CLAUDE_REPORT_START -->
Claude report placeholder.

Codex da dong toan bo code task EAM va commit checkpoint. Task tiep theo chi bat dau khi user/Codex tao ACTIVE TASK moi.

Rule nhac lai: Claude phai APPEND report vong moi trong marker nay, khong xoa/sua report cu. Chi Codex duoc reset/compact report sau review.

---

## Round P2-DATA-00 - Impact scan va chia phase an toan truoc khi refactor Stats

**Task ID:** P2-DATA-00 (scan-only). **Khong sua file code/scene/prefab nao trong vong nay.**

### Impact analysis - phuong phap
GitNexus MCP tools KHONG available trong session nay (chi co skill docs `.claude/skills/gitnexus/*`, khong co tool runtime).
Fallback theo thoa thuan cac vong truoc: **manual caller-grep** tren `Action_RPG/Assets/_Scripts` + quet serialized YAML (`*.prefab`, `*.unity`, `*.asset`).
Codex nen chay lai `npx gitnexus analyze` + `gitnexus_impact` cho 11 symbol truoc khi approve P2-DATA-01.

### Ket qua: RISK = **CRITICAL** -> DUNG, khong code tiep P2-DATA-01

Ly do CRITICAL (khong phai HIGH): P2-DATA-01 yeu cau bo public primitive **storage** tren `Stats` va chuyen vao
`[SerializeField] private CharacterRuntimeStats runtimeStats`. Day la **thay doi hinh dang serialization** (float o root -> field long trong nested class).

- `[FormerlySerializedAs]` **khong the** map `initialBaseHp: 100` (root) sang `runtimeStats.initialBaseHp` (nested). Unity se **im lang** deserialize ve default.
- Compatibility property cung ten (`public float initialBaseHp => runtimeStats.initialBaseHp`) chi cuu **compile**, khong cuu **data**: property khong serialize.
- Blast radius data thuc te (grep YAML, dang mang `Stats` storage o root):
  - `Assets/Scenes/OdoScene.unity` (**dang dirty, user-owned**), `Assets/Scenes/TmpScene.unity`
  - `_Prefabs/Object/Player/Player.prefab`, `_Prefabs/Object/Enemy/Enemy2.prefab`, `_Prefabs/Object/Enemy/Slime.prefab`
  - 70+ file `Assets/_Recovery/*.unity`
- => Neu merge P2-DATA-01 nhu spec, Player/Enemy prefab + OdoScene mat toan bo base stat, build van pass 0 error, **chi phat hien khi Play Mode**. Day dung la kich ban rule 3 yeu cau dung lai.

Luu y: 165 file `.asset` co `baseAttackSpeed:` la `WeaponData`/`CompanionProtocolData` (**field rieng cua chung**, khong phai `Stats`) -> **khong bi anh huong**, khong migrate.

### Usage map (acceptance: bang usage map ngan gon)

| Nhom | File / symbol | Doc | Ghi (nguy hiem nhat) |
|---|---|---|---|
| Storage goc | `_Stats/Stats.cs` | decl: `baseHp`, `initialBaseHp`, `baseHpGain`, `maxStamina`, `maxSin`, `baseSTR/DEX/INT/VIT/AGI`, `baseAttackSpeed`, `baseMoveSpeed` | - |
| Recalc | `_Stats/AllyStats.cs` | `RecalculateStats()` L272-314 + L383-387 doc gan het | `baseHp = initialBaseHp + 20*level` (279), `maxSin` (290/294) |
| Enemy | `_Stats/EnemyStats.cs` | `monsterRank`, `expReward` decl (44/48) | **`base.baseAttackSpeed = 0.667f` (L64) ghi de sau `base.Start()`** -> chinh la bug P2-DATA-02 phai guard |
| Enemy runtime | `Enemies/EnemyAI.cs` (528/547/755), `EnemyCombat.cs` (167/424/641/819), `BossCombat.cs` (72/89), `EnemyProjectile.cs` (105) | read-only | khong ghi |
| Player state / save | `_PlayerState/PlayerStateManager.cs` (33-39, 60-64), `PlayerRuntimeState.cs`, `PlayerStateSaveData.cs` | - | ghi vao `runtime.*` (**field rieng cua PlayerRuntimeState, khong phai Stats**) |
| GameManager / save-load | `Systems/GameManager.cs` | L246/255-259 doc `playerStats.base*` de save | L82-88 ghi `playerStats.initialBaseHp/baseSTR..AGI/maxStamina` khi load |
| SkillTree / stat alloc | `SkillTreeUI/SkillTreeRuntime.cs` L591-596, `InventoryUI/StatAllocationUI.cs` L141-153 | - | **`+=` truc tiep** vao `initialBaseHp`/`baseVIT/STR/INT/DEX/AGI` |
| Equipment / effects | `Items/EquipmentManager.cs` L192 (`baseAttackSpeed = weapon.baseAttackSpeed`), `CompanionEquipmentManager.cs` L114 (`+=`), `CatalystSignature.cs` L126 (`*=`), `BattleMageSignature.cs` L121/138/173 + `ArcaneEmpowerment.cs` L374/376 (**ghi truc tiep `enemy.baseMoveSpeed` de slow/restore**), `DarkDemonAI.cs` L51-56 (chained assign 5 stat) | | |
| DevTool | `Systems/DevToolPanel.cs` L851-857 | - | ghi 7 field tu WeaponDB entry |
| UI read-only | `UIStats.cs`, `StatDetailUI.cs`, `CompanionHUD.cs`, `SkillTreeAutoPopulate.cs` (Editor - chi la ten `StatFieldType.BaseSTR`, khong cham field) | read | - |

### Cac diem thiet ke phai chot TRUOC khi code (Codex/User quyet)

1. **Serialization migration**: neu van muon container, phai giu `runtimeStats` nhung them `ISerializationCallbackReceiver` migration doc field cu (giu lai duoi dang `[SerializeField, HideInInspector]` legacy backing) roi copy sang container 1 lan. Khong the vua "khong con field public storage" vua "khong duplicate serialized backing field cung ten" ma van giu data -> **hai bullet nay cua P2-DATA-01 dang mau thuan nhau**.
2. **`+=`/`*=` qua property**: `StatAllocationUI`, `SkillTreeRuntime`, `CompanionEquipmentManager`, `CatalystSignature` dung compound-assign. Property `get/set` van compile, nhung neu chuyen sang API `AddBaseAttribute(StatType, float)` thi 4 file nay phai sua cung luc.
3. **`enemy.baseMoveSpeed` bi dung lam co che Slow** (`BattleMageSignature`, `ArcaneEmpowerment`) - day la legacy slow chua migrate sang `ApplyEffect(Slow)`. Refactor storage se lam lo ra bug round-trip san co. **De xuat tach thanh task rieng truoc P2-DATA-01.**
4. **`_Recovery/*.unity`**: 70+ scene backup. Can xac nhan chung co con duoc load khong; neu khong, loai khoi pham vi migration.

### De xuat chia phase an toan (thay cho P2-DATA-01 hien tai)

- **P2-DATA-02 truoc, P2-DATA-01 sau.** `EnemyData` (P2-DATA-02) va `EnemyCombat` resolve module (P2-DATA-03) la **additive**, khong doi shape serialization cua `Stats`, risk = LOW/MEDIUM, co the lam ngay.
- **P2-DATA-01A (LOW)**: chi migrate legacy slow (`BattleMageSignature`, `ArcaneEmpowerment`) khoi `baseMoveSpeed`.
- **P2-DATA-01B (MEDIUM)**: them API `ApplyBaseRuntimeStats/CaptureRuntimeStats/AddBaseAttribute/SetBaseAttackSpeed` va chuyen 4 caller compound-assign sang API, **field van giu nguyen** -> khong rui ro data.
- **P2-DATA-01C (CRITICAL, can user backup/branch rieng + verify Play Mode)**: moi thuc su doi storage sang `CharacterRuntimeStats` kem migration callback.

### Test da chay
- Chi la scan; **khong sua code** nen khong chay build.
- `git status --short` sau vong nay: chi `M workspace.md` (report nay). `OdoScene.unity` **khong con dirty** — da vao commit checkpoint cua Codex; M-01 van con la viec verify trong Editor.

### Partial / chua lam
- **Chua code P2-DATA-01/02/03** — dung theo bullet "Neu bat ky impact HIGH/CRITICAL thi dung, append report va de Codex/User quyet tiep".
- Chua chay GitNexus that (MCP unavailable) — Codex can chay lai de xac nhan.
- **Regression moi:** khong co.

---

## Round P2-DATA-01A (+ 01C design) - Migrate legacy Slow, batch DUNG SOM vi thieu build toolchain

**Task ID:** P2-DATA-01A (code, xong) va P2-DATA-01C (design-only, xong).
**Batch DUNG tai day.** 01B/02/03 **chua lam** — ly do blocker o muc "Test / blocker" ben duoi. User da xac nhan chon phuong an "dung o 01A, cho cai dotnet".

### Impact analysis (manual caller-grep, GitNexus MCP van unavailable)

| Symbol | Callers | Risk |
|---|---|---|
| `BattleMageSignature.ProcessZoneTick/CleanUpZone` | private, chi goi trong file (ZoneRoutine, OnUnequip) | LOW |
| `BattleMageSignature.slowedEnemies` | private field, 6 usage, tat ca trong file | LOW |
| `ArcaneEmpowerment.TempSlowRoutine` | **1 caller duy nhat** (L315 `ExplodeFlashFrost`) | LOW |
| `Stats.ApplyEffect(CombatEffectInfo, Stats)` | additive, khong doi signature; da co 8+ caller tu CE02C4 | LOW |

Risk tong: **LOW**. Khong cham `Stats` storage, khong cham scene/prefab/asset.

### Files / symbols thay doi

1. `Assets/_Scripts/_Commons/Items/Skills/Skill/ArcaneEmpowerment.cs`
   - **Xoa** `private IEnumerator TempSlowRoutine(Stats, float, float)` (dang `enemy.baseMoveSpeed -= amount` roi `+= amount` sau `WaitForSeconds`).
   - `ExplodeFlashFrost` L315: `host.StartCoroutine(TempSlowRoutine(e, swSlowPct, swSlowTime))`
     -> `e.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, swSlowTime) { magnitude = swSlowPct }, stats);`
   - `host.`/`IEnumerator`/`WaitForSeconds` van con dung o cho khac -> khong co using/field thua.

2. `Assets/_Scripts/_Commons/Items/Skills/Signature/BattleMageSignature.cs`
   - **Xoa** field `List<Stats> slowedEnemies` va local `List<Stats> currentTickEnemies`.
   - **Them** `private float SlowRefreshDuration => Mathf.Max(0.2f, tickRate * 1.5f);`
   - `ProcessZoneTick` block B: `enemyStats.baseMoveSpeed = enemyStats.baseMoveSpeed * (1 - slowPercent)` + add-to-list
     -> `enemyStats.ApplyEffect(new CombatEffectInfo(CombatEffectType.Slow, SlowRefreshDuration) { magnitude = slowPercent }, stats);` (refresh moi tick).
   - **Xoa** block C (loop go slow cho enemy roi vung, `/(1 - slowPercent)`).
   - `CleanUpZone`: **xoa** loop restore `baseMoveSpeed`; comment lai ly do (nguon Slow tu het han).
   - Block "D. CONG DON GIAP AO" doi thanh "C." cho khop.

**API doi:** khong co API public nao doi. Chi xoa 1 private coroutine + 1 private field.

### Behavior change (can Codex/User biet, KHONG phai bug moi)

- **Fix bug san co:** code cu nhan/chia `baseMoveSpeed` theo `(1 - slowPercent)`. Neu enemy chet/disable/destroy giua chung, hoac 2 nguon slow chong nhau, `baseMoveSpeed` bi lech **vinh vien** (mat round-trip). Bo hoan toan class bug nay vi khong con ghi `baseMoveSpeed`.
- **Doi nho:** Slow cua Dark Zone gio tat **tre ~0.75s** (`tickRate * 1.5` voi tickRate mac dinh 0.5) sau khi enemy roi vung, thay vi tat ngay o tick ke tiep. Do la he qua cua model endTime-based. Neu muon tat gan-nhu-ngay: ha `SlowRefreshDuration` xuong `tickRate * 1.05f`. **Can Codex quyet balance**, toi giu 1.5x cho an toan (tranh nhap nhay slow on/off khi tick lech frame).
- Slow tu BattleMage/Arcane gio di qua `resistanceEffect` (bi enemy resist duration) va **strongest-wins**, thay vi additive multiplicative. Dung y do chung cua CE-01.

### Test da chay / blocker

- `git diff --check`: **pass** (chi warning LF->CRLF cua cac file markdown user-owned dang dirty: `.claude/skills/gitnexus/*`, `AGENTS.md`, `CLAUDE.md`, `workspace.md`. **Khong dung/revert cac file nay.**)
- Acceptance grep: `rg "baseMoveSpeed" Assets/_Scripts/_Commons/Items/Skills Assets/_Scripts/_Commons/Systems`
  -> chi con `MagePassive.cs:17` `public float baseMoveSpeedBonus` (field bonus rieng, khong phai `Stats.baseMoveSpeed`),
     `MagePassive.cs:199` `stats.bonusMoveSpeed += baseMoveSpeedBonus * sign` (ghi `bonusMoveSpeed`, hop le),
     `MageSignature.cs:205` comment. **Khong con direct write slow.** -> acceptance dat.
- **BUILD: KHONG CHAY DUOC.** `dotnet` khong con tren may:
  - `dotnet --version` -> CommandNotFound (ca sandboxed lan `dangerouslyDisableSandbox`).
  - `C:\Program Files\dotnet` -> **khong ton tai**.
  - Khong tim thay `MSBuild.exe` trong `C:\Program Files (x86)\Microsoft Visual Studio`, khong tim thay `csc.exe` cua Unity.
  - Cac vong truoc `dotnet build Action_RPG\Assembly-CSharp.csproj -t:Rebuild` chay duoc -> day la thay doi moi truong may, khong phai do patch nay.
- Smoke evidence thay the (static): `ApplyEffect(CombatEffectInfo, Stats)` ton tai o `Stats.cs:771`; `CombatEffectInfo(type, duration)` + `magnitude` ton tai; pattern y het precedent da build pass o CE02C4 (`MageLiteSignature.cs:95`). Rui ro compile: rat thap nhung **chua duoc xac nhan**.

### Partial / chua lam

- **P2-DATA-01B, P2-DATA-02, P2-DATA-03: CHUA LAM.** Batch mode yeu cau build/check pass sau tung cum; khong co toolchain nen khong viet mu patch cham `Stats`/`GameManager`/`SkillTreeRuntime`/`EnemyStats`/`EnemyCombat`.
- P2-DATA-01A **chua build-verified**. Codex/User can chay `dotnet build` mot lan truoc khi coi la dat acceptance.
- **Regression moi:** khong co. **Test chua chay:** build, Play Mode.

---

## P2-DATA-01C - Design migration cho CharacterRuntimeStats (design-only, KHONG code)

### Rang buoc Unity serialization (co so cua moi option)
- Unity serialize field theo **duong dan trong object graph**. `initialBaseHp` o root khac `runtimeStats.initialBaseHp`.
- `[FormerlySerializedAs]` chi doi **ten** field trong **cung mot level**. Khong bac cau root -> nested. Khong co API nao lam viec do khi deserialize.
- **Property khong serialize.** Compatibility property cuu compile, khong cuu data.
- Anh huong: `OdoScene.unity`, `TmpScene.unity`, `Player.prefab`, `Enemy2.prefab`, `Slime.prefab`, 70+ `_Recovery/*.unity`.

### Option A - Legacy backing + `ISerializationCallbackReceiver` (**KHUYEN NGHI**)
```
[SerializeField, HideInInspector] private float initialBaseHp_legacy;  // [FormerlySerializedAs("initialBaseHp")]
[SerializeField] private CharacterRuntimeStats runtimeStats;
[SerializeField, HideInInspector] private bool _migratedToRuntimeStats;

void OnAfterDeserialize() {
    if (_migratedToRuntimeStats) return;
    runtimeStats.initialBaseHp = initialBaseHp_legacy;   // ... copy tung field
    _migratedToRuntimeStats = true;
}
```
- **Giu data:** CO. `FormerlySerializedAs("initialBaseHp")` hop le vi `initialBaseHp_legacy` van o **root**.
- Chi phi: moi field co 1 legacy backing (van la serialized field, chi `HideInInspector`) -> **vi pham bullet "khong duplicate serialized backing field"** cua P2-DATA-01 ban goc. Day la cai gia bat buoc de khong mat data. Xoa legacy backing sau khi da re-save toan bo prefab/scene 1 lan (task Editor rieng).
- Rui ro: `OnAfterDeserialize` chay ca tren **thread khac** va tren asset import; chi duoc lam copy field thuan, **khong duoc goi Unity API** (`transform`, `GetComponent`, `Debug.Log`). Cac field runtime (`currentHp`, coroutine, timer) tuyet doi khong dua vao.
- **Can user backup + verify Play Mode.**

### Option B - Editor tool rewrite YAML/prefab
- Migration script chay 1 lan, doi shape truc tiep trong asset.
- **Giu data:** CO, nhung phai chay tren **branch rieng**, co backup, va **72+ file scene/prefab se dirty cung luc** — dinh truc tiep `OdoScene.unity` dang user-owned. Diff review rat nang, de conflict voi manual task M-01.
- Chi nen chon neu muon dut diem legacy backing ngay.

### Option C - Chi doi access boundary, giu field root (**an toan nhat, LOW risk**)
- `Stats` giu nguyen field root, nhung doi `public` -> `[SerializeField] private` + property `public float initialBaseHp { get; private set; }`-style, va bat gameplay code di qua API cua 01B.
- **Giu data:** CO, tuyet doi (shape khong doi).
- **Khong dat** muc tieu "storage nam trong runtime container", nhung dat 90% gia tri thuc te cua refactor (source-of-truth boundary, khong ai ghi bua).

### De xuat
Lam **01B truoc** (them API, giu field). Sau do chon **Option C** lam moc mac dinh; chi len **Option A** neu Codex/User thuc su can nested container, va phai lam tren branch rieng kem Play Mode verify.
**Khong code 01C cho toi khi co approve moi.**

---

## Round P2-DATA-01B + 02 + 03 (batch mode) - Stats API, EnemyData, EnemyCombat module resolve

**Task ID:** P2-DATA-01B, P2-DATA-02, P2-DATA-03. Ca ba **xong**, build 0 error sau moi cum.
`dotnet` da duoc user cai lai -> **P2-DATA-01A vong truoc da build-verified: 0 error, 16 warning co san.**

### Impact analysis (manual caller-grep; GitNexus MCP van unavailable)

| Symbol | Callers ngoai file | Risk |
|---|---|---|
| `Stats` (them API, KHONG doi field) | additive thuan -> khong the vo caller cu | LOW |
| `GameManager.SavePlayerState/LoadPlayerState` | private flow, doc/ghi `currentPlayerState` | MEDIUM |
| `SkillTreeRuntime.ApplySingleStat` | 1 caller trong file | LOW |
| `StatAllocationUI` allocate switch | UI, 1 caller | LOW |
| `EquipmentManager` weapon equip | 1 site | LOW |
| `CompanionEquipmentManager.AddProtocolStats` | 2 caller (Apply/Remove, sign=±1) | LOW |
| `CatalystSignature` buff/revert | 2 site doi xung | LOW |
| `CompanionSkillController` maxSin | 2 site | LOW |
| `DarkDemonAI` spawn reset | 1 site | LOW |
| `DevToolPanel` reset char | 1 site | LOW |
| `EnemyStats.Start/SetupResistances` | override cua `Stats.Start`; `BossStats` neu co | MEDIUM |
| `EnemyCombat.HasSkill/CanUseSkill/GetSkillRange/GetBasicRange` | `EnemyAI` (11 site), `BossCombat` | MEDIUM |

Risk tong: **MEDIUM**. Khong doi serialized field shape, khong sua scene/prefab/asset.

---

## P2-DATA-01B - Stats API + migrate direct writes (giu storage cu)

**Files:** `_Stats/Stats.cs`, `Systems/GameManager.cs`, `Systems/SkillTreeUI/SkillTreeRuntime.cs`,
`Systems/InventoryUI/StatAllocationUI.cs`, `Systems/DevToolPanel.cs`, `Items/EquipmentManager.cs`,
`Items/AdvancedEffects/DarkDemonAI.cs`, `Items/Skills/Signature/CatalystSignature.cs`,
`Companion/Equipment/CompanionEquipmentManager.cs`, `Companion/Skills/CompanionSkillController.cs`.

### API moi tren `Stats` (additive, khong doi field)
```csharp
public enum BaseAttribute { STR, DEX, INT, VIT, AGI }
[System.Serializable] public struct BaseStatSnapshot {
    public float initialBaseHp, baseHp, maxStamina, baseSTR, baseDEX, baseINT, baseVIT, baseAGI; }

float GetBaseAttribute(BaseAttribute)          void SetBaseAttribute(BaseAttribute, float)
void  AddBaseAttribute(BaseAttribute, float)
float InitialBaseHp { get; }                    void SetInitialBaseHp(float) / AddInitialBaseHp(float)
void  SetMaxStamina(float)                      void SetMaxSin(float)
void  SetBaseAttackSpeed(float) / AddBaseAttackSpeed(float) / MultiplyBaseAttackSpeed(float)
void  SetBaseMoveSpeed(float)
void  ApplyBaseRuntimeStats(BaseStatSnapshot, bool resetCurrentVitals)
BaseStatSnapshot CaptureRuntimeStats()
```
- Dung enum **rieng** `Stats.BaseAttribute` chu khong phai `StatModifier.StatType` (enum do gom ca Bonus*/Flat*/Crit... -> khong dung ngu nghia "base attribute").
- `BaseStatSnapshot` la **DTO thuan**, KHONG serialize tren `Stats` -> khong dung toi Unity serialization.
- **API khong tu goi `RecalculateStats()`** — caller giu nguyen thu tu recalc cu, tranh doi behavior.
- `MultiplyBaseAttackSpeed` co guard `factor > 0f`.

### Callers da migrate
| File | Truoc | Sau |
|---|---|---|
| `SkillTreeRuntime:591-596` | `_allyStats.initialBaseHp += val` / `.baseVIT += val` ... | `AddInitialBaseHp(val)` / `AddBaseAttribute(Stats.BaseAttribute.VIT, val)` ... |
| `StatAllocationUI:141-153` | `_allyStats.baseSTR += 1f` ... | `AddBaseAttribute(Stats.BaseAttribute.STR, 1f)` ... |
| `GameManager:82-88` (load) | 7 dong ghi field | 1 `ApplyBaseRuntimeStats(snapshot, resetCurrentVitals: false)`, fallback (`baseHp≤0→100`, `maxStamina≤0→giu cu`) giu y het |
| `GameManager:250-267` (save) | doc 7 field | `var snap = CaptureRuntimeStats();` roi doc `snap.*` |
| `DevToolPanel:851-857` | 7 dong ghi field | 1 `ApplyBaseRuntimeStats(..., resetCurrentVitals: false)` |
| `EquipmentManager:192` | `allyStats.baseAttackSpeed = w.baseAttackSpeed` | `SetBaseAttackSpeed(w.baseAttackSpeed)` |
| `CompanionEquipmentManager:114` | `_stats.baseAttackSpeed += p.baseAttackSpeed * sign` | `AddBaseAttackSpeed(p.baseAttackSpeed * sign)` |
| `CatalystSignature:87 / :126` | `/= (1+buff)` / `*= (1+buff)` | `MultiplyBaseAttackSpeed(1f/(1+buff))` / `MultiplyBaseAttackSpeed(1f+buff)` |
| `CompanionSkillController:65 / :125` | `stats.maxSin = want` | `stats.SetMaxSin(want)` |
| `DarkDemonAI:51-56` | 3 dong ghi + chained assign 5 attribute | `ApplyBaseRuntimeStats(new BaseStatSnapshot{ initialBaseHp=0, maxStamina=st.maxStamina })` (5 base attribute -> 0 mac dinh cua struct) + `SetBaseAttackSpeed(1f)` + `SetBaseMoveSpeed(5f)` |

### HAI BUG SAN CO phat hien khi migrate — KHONG sua, chi bao cao (doi Codex quyet balance)

1. **`CatalystSignature` sai dau.** Comment o L86 ghi "giam thoi gian vung don (Cooldown)" nhung `baseAttackSpeed` la **TOC DO (don/giay)**, khong phai cooldown (`AllyStats.RecalculateStats: attackSpeed = baseAttackSpeed * (1+bonus)`; `EnemyCombat: cooldown = 1/speed`). Nen nhanh `else` (companion khong phai `AllyStats`) dang lam companion **DANH CHAM DI** thay vi +100% toc danh. Round-trip van dung (revert nhan lai) nen khong leak. Toi giu nguyen phep tinh, chi doi sang API. **Sua dau = doi balance -> can Codex approve.**

2. **`GameManager` save/load bat doi xung ve HP.** Save ghi `currentPlayerState.baseHp = playerStats.baseHp` (**DAN XUAT** = `initialBaseHp + 20*level`), nhung load nap gia tri do vao **`initialBaseHp`**. Moi chu ky save->load, `initialBaseHp` bi cong them `20*level` -> **HP goc phinh dan**. Toi giu nguyen hanh vi (chi doi sang API + them comment). **Fix dung phai la save `snap.initialBaseHp`, nhung se doi save-file cu -> can Codex quyet + migration save.**

### Acceptance 01B
- Build: `dotnet build Action_RPG\Assembly-CSharp.csproj -t:Rebuild` -> **Build succeeded, 0 Error, 16 Warning** (co san).
- `git diff --check -- Action_RPG` -> **pass**.
- Grep direct write con lai ngoai `_Stats/`:
  ```
  rg '\.(initialBaseHp|maxStamina|maxSin|baseAttackSpeed|baseMoveSpeed|baseSTR|baseDEX|baseINT|baseVIT|baseAGI)\s*(=[^=]|\+=|-=|\*=|/=)' Assets/_Scripts | rg -v '_Stats/'
  ```
  -> **chi con `PlayerStateManager.cs:33-39,60-64` va `GameManager.cs:253,263-267`**, deu ghi vao `PlayerRuntimeState`/`currentPlayerState` = **data class rieng, ALLOWED** theo acceptance. **Khong con gameplay code ghi thang `Stats`.**
- Trong `_Stats/`: chi con `EnemyStats.cs:64` `base.baseAttackSpeed = 0.667f` -> da xu ly o P2-DATA-02 duoi.

---

## P2-DATA-02 - EnemyData source-of-truth

**Files:** `Enemies/EnemyData.cs` (**MOI**, guid `b507a5e49fc542b5b181c21d4355683e`), `EnemyData.cs.meta` (**MOI**, CRLF, no BOM),
`Assembly-CSharp.csproj` (them `<Compile Include>`, file nay **gitignored**), `_Stats/EnemyStats.cs`.

- `EnemyData : ScriptableObject`, `[CreateAssetMenu(fileName="NewEnemyData", menuName="Action_RPG/Enemy Data")]`.
- Fields: `enemyID`, `enemyName`, `monsterRank`, `baseHp`, `baseMoveSpeed`, `baseAttackSpeed`, `expReward`, `basicAttackModule`, `skillAttackModule`. **Khong co runtime state** (khong `currentHp`/timer/target/coroutine/aggro/cooldown).
- `EnemyStats`: them `public EnemyData data;` + comment "EnemyData la source-of-truth cho archetype; inspector runtime stats chi la fallback/override tam thoi".
- `EnemyStats.Start()`: goi `ApplyEnemyData()` **TRUOC `base.Start()`** -> `maxHp`, `SetupResistances()` (chay sau, doc `monsterRank` da copy), va AI/Combat deu thay dung gia tri archetype.
- `ApplyEnemyData()` no-op khi `data == null`; copy co guard `>0f` cho hp/move/attack speed, guard `IsNullOrEmpty` cho id/name -> data thieu field khong ghi de 0 len inspector.

### Xoa hard-code 0.667
Truoc: `base.baseAttackSpeed = 0.667f;` **vo dieu kien**, chay sau `base.Start()` -> **ghi de moi cau hinh**.
Sau: `if (data == null && baseAttackSpeed <= 0f) SetBaseAttackSpeed(0.667f);`

**Smoke evidence (khong doi behavior):** grep YAML cho thay **moi enemy prefab/scene deu dang `baseAttackSpeed: 0`**:
```
_Prefabs/Object/Enemy/Enemy2.prefab:212:  baseAttackSpeed: 0
_Prefabs/Object/Enemy/Slime.prefab:267:   baseAttackSpeed: 0
Scenes/OdoScene.unity:1845:              baseAttackSpeed: 0
```
-> voi `data == null` (tat ca enemy hien tai), guard van cho ra 0.667 y het truoc. Enemy co `data` se dung `data.baseAttackSpeed`, khong con bi ghi de.

---

## P2-DATA-03 - EnemyCombat resolve EAM tu EnemyData

**File:** `Enemies/EnemyCombat.cs`.

- Them `GetResolvedBasicModule()` / `GetResolvedSkillModule()`. Thu tu: **local override tren `EnemyCombat`** -> `stats.data.{basic,skill}AttackModule` -> `null` (fallback legacy). Ca hai null-safe voi `stats == null` va `stats.data == null`.
- Them `ModuleName(m)` null-safe cho log: `m == null ? "<null>" : (IsNullOrEmpty(displayName) ? m.name : m.displayName)`.
- `HasSkill`, `CanUseSkill()`, `GetSkillRange()`, `GetBasicRange()`, `PerformBasicAttack()`, `PerformSkillAttack()` **deu dung resolved module**.
- Tooltip 2 field local doi thanh "LOCAL OVERRIDE — null = lay tu EnemyStats.data".
- `EnemyAI` **khong doi mot dong nao** — van chi goi API cua `EnemyCombat` (`CanUseSkill`/`GetSkillRange`/`GetBasicRange`), khong chua logic projectile/AoE.
- Module null -> melee fallback cu; skill legacy (`enemySkill`/`skillCooldown`/`skillRange`/`skillEffects`) van chay khi khong co skill module.

### Acceptance 02 + 03
- Build sau ca hai: **0 Error, 16 Warning**.
- `git diff --check -- Action_RPG` -> **pass**.
- `rg "basicAttackModule|skillAttackModule" Enemies/EnemyCombat.cs` -> **8 hit**: 2 declaration, 2 tooltip, 4 nam **ben trong 2 helper**. Runtime path (`HasSkill`/`CanUseSkill`/`GetSkillRange`/`GetBasicRange`/`PerformBasicAttack`/`PerformSkillAttack`) **khong doc thang field**. Dat acceptance.
- `rg "basicAttackModule|skillAttackModule"` ngoai `EnemyCombat.cs`/`EnemyData.cs` -> **0 hit**.

---

### Files untracked can `git add` khi commit
- `Action_RPG/Assets/_Scripts/_Commons/Enemies/EnemyData.cs` + `.cs.meta`
- (con lai tu vong truoc: `EnemyAttackModuleData.cs(.meta)`, `EnemyProjectile.cs(.meta)` neu Codex chua add)

### Test chua chay / partial
- **Play Mode chua chay** (M-06). Rieng P2-DATA-02/03 can `EnemyData` asset that -> **M-07 la prerequisite** de test nhanh duong `data != null`. Hien tai moi enemy deu `data == null` nen chi duong fallback duoc phu.
- `npx gitnexus detect-changes` chua chay (MCP/CLI khong co trong session).
- **Regression moi: khong co.** Hai bug san co (Catalyst dau, GameManager baseHp) **da ton tai truoc patch**, chi duoc phat hien va bao cao, khong sua.

### Can Codex/User quyet
1. `CatalystSignature` co sua dau `MultiplyBaseAttackSpeed` khong (doi balance companion).
2. `GameManager` co sua save thanh `snap.initialBaseHp` khong (can migration save-file cu).
3. `BattleMageSignature.SlowRefreshDuration` giu `tickRate * 1.5f` hay ha xuong `* 1.05f` (vong truoc).

---

## Round P2-DATA-FIX-01 (xong) + FIX-02 (dieu tra xong, CHUA code — cho approve)

## P2-DATA-FIX-01 - Sua dau buff attack speed trong CatalystSignature — **DONE**

**File:** `Items/Skills/Signature/CatalystSignature.cs`. **Symbols:** nhanh `else` cua OnSignatureStart (L87) va OnSignatureEnd (L126).

### Impact (manual caller-grep; GitNexus MCP unavailable)
`rg "CatalystSignature" Assets/_Scripts` ngoai chinh file -> chi 3 hit, deu la **registry/enum**, khong ai goi truc tiep method:
`SkillData.cs:72` (`SignatureEffectCode.CatalystSignature`), `SkillFactory.cs:130-131` (`return typeof(CatalystSignature)`), `Editor/SkillTreeAutoPopulate.cs:501` (string node id).
`compAttackSpeedBuff` chi doc o 4 site trong file. **Risk: LOW.**

### Thay doi
```
L87  apply : MultiplyBaseAttackSpeed(1f / (1f + compAttackSpeedBuff))  ->  MultiplyBaseAttackSpeed(1f + compAttackSpeedBuff)
L126 revert: MultiplyBaseAttackSpeed(1f + compAttackSpeedBuff)         ->  MultiplyBaseAttackSpeed(1f / (1f + compAttackSpeedBuff))
```
- Nhanh `AllyStats` (L81/L121) **giu nguyen**: no dung `bonusAttackSpeed` + `CalculateCombatStatsOnly()`, khong lien quan `baseAttackSpeed`. Dung yeu cau task.
- Xoa comment "[P2-DATA-01B] giu nguyen hanh vi cu"; thay bang note ngan giai thich `baseAttackSpeed` la don/giay.

### **BEHAVIOR CHANGE co chu dich (bug fix)**
Companion **khong phai `AllyStats`** truoc day bi **DANH CHAM DI** khi Catalyst Signature bat (voi `compAttackSpeedBuff = 1.0` -> speed × 0.5). Sau fix: **danh nhanh gap doi**, dung mo ta buff "+100% toc danh".
Apply/revert doi xung: `x * (1+b) * 1/(1+b) = x` -> **khong leak speed** sau unequip / het signature. `MultiplyBaseAttackSpeed` co guard `factor > 0f`.

### Acceptance FIX-01
- `dotnet build Action_RPG\Assembly-CSharp.csproj -t:Rebuild` -> **Build succeeded, 0 Error, 16 Warning** (co san).
- `git diff --check -- Action_RPG` -> **pass**.
- Grep xac nhan 2 site da doi dung chieu (apply nhan, revert chia).

---

## P2-DATA-FIX-02 - GameManager save/load baseHp — **DIEU TRA XONG, CHUA CODE**

Task yeu cau "sau khi Codex/User chap nhan huong migration, moi code fix" -> toi **dung o buoc de xuat**. Da hoi user, chua co tra loi.

### PHAT HIEN QUAN TRONG: **KHONG CO VAN DE MIGRATION SAVE CU**

Gia dinh trong task ("save cu dang luu derived `baseHp`", "can them field/version moi", "load heuristic co guard") **khong dung voi source hien tai**.

Bang chung — `rg '\.baseHp\b' Assets/_Scripts` cho **dung 5 hit**:
| File:Line | Vai tro |
|---|---|
| `PlayerStateManager.cs:38` | `runtime.baseHp = player.base_hp` — seed tu DB, moi lan boot |
| `GameManager.cs:86` (load) | `initialBaseHp = currentPlayerState.baseHp > 0f ? ... : 100f` |
| `GameManager.cs:254` (save) | `currentPlayerState.baseHp = snap.baseHp` **<- BUG** (`snap.baseHp` la DAN XUAT) |
| `PlayerRuntimeState.cs:114` | `MaxHp => baseHp + flatHp + TotalVIT*HP_PER_VIT` |
| `EnemyStats.cs:104` | khong lien quan (enemy) |

Va: `rg -c "baseHp" PlayerStateSaveData.cs` -> **0**. `PlayerRuntimeState.ToSaveData()` **khong ghi `baseHp`**; `LoadFromSave()` **khong doc `baseHp`**.

=> `baseHp` chi ton tai **IN-MEMORY** tren `PlayerRuntimeState`, **khong bao gio xuong JSON/DB**.

### Hai he qua that su
1. **Phinh HP giua cac lan load TRONG CUNG 1 SESSION** (checkpoint / doi scene), khong phai giua cac lan chay game:
   `seed 100` -> load `initialBaseHp=100` -> `AllyStats.baseHp = 100 + 20*level` -> save ghi nguoc `300` vao runtime -> load ke `initialBaseHp=300` -> `baseHp=500` -> ...
   Restart app thi `PlayerStateManager:38` seed lai tu DB nen **tu reset**. Bug nay khong lam hong save file, chi lam sai trong phien choi.
2. **`PlayerRuntimeState.MaxHp` cung sai.** L114 cong `baseHp + flatHp + VIT*HP_PER_VIT` -> no **mong doi hang so goc**. Ghi gia tri dan xuat vao do lam `MaxHp` cua runtime state phinh theo.

### De xuat fix (cho approve)
**Option 1 (KHUYEN NGHI, risk LOW, 1 dong):**
```csharp
// GameManager.cs:254
currentPlayerState.baseHp = snap.initialBaseHp;   // thay vi snap.baseHp
```
- Khong them field, khong them version, **khong dung toi `PlayerStateSaveData`** -> khong mat/khong can migrate save cu (vi save cu chua tung chua `baseHp`).
- Sua luon ca hai he qua o tren. Round-trip `load -> save -> load` tro thanh idempotent: `initialBaseHp` giu nguyen 100.
- `snap` da co san `initialBaseHp` tu `CaptureRuntimeStats()` (P2-DATA-01B) -> khong can them API.

**Option 2 (risk MEDIUM):** Option 1 + rename `PlayerRuntimeState.baseHp` -> `initialBaseHp` cho khop ngu nghia. Cham them `PlayerStateManager:38` va `MaxHp:114`. Chi nen lam neu Codex muon don ten mot the.

**Option 3:** chi report, de Codex tu code.

### Trang thai FIX-02
- **Chua sua mot dong nao.**
- **Test chua chay:** Play Mode save->load->save de xac nhan `initialBaseHp` khong tang `20*level`.
- **Regression moi:** khong co.

### Files untracked can `git add` khi commit (khong doi tu vong truoc)
`Enemies/EnemyData.cs(.meta)`, `Enemies/EnemyAttackModuleData.cs(.meta)`, `Enemies/EnemyProjectile.cs(.meta)` — neu Codex chua add.

---

## Round P2-DATA-FIX-02 (Option 1, user approved) — **DONE**

**Task ID:** P2-DATA-FIX-02. **File:** `Systems/GameManager.cs`. **Symbol:** `SavePlayerState` (khoi capture snapshot).

### Impact (manual caller-grep; GitNexus MCP unavailable)
`currentPlayerState.baseHp` co **1 writer duy nhat** (site dang sua) va **1 reader** (`GameManager:86`, load) + gian tiep `PlayerRuntimeState.MaxHp:114`.
`PlayerStateSaveData` **khong co** field `baseHp`; `ToSaveData()`/`LoadFromSave()` khong dung -> **khong cham save file**. **Risk: LOW.**

### Thay doi (dung 1 dong code + comment)
```csharp
// GameManager.cs — SavePlayerState
- currentPlayerState.baseHp = snap.baseHp;        // DAN XUAT = initialBaseHp + 20*level
+ currentPlayerState.baseHp = snap.initialBaseHp; // HANG SO goc
```
- **Xoa comment sai** cu (dang ghi "Giu nguyen hanh vi cu: save ghi `baseHp` (DAN XUAT)... xem note bat doi xung").
- Thay bang comment giai thich tai sao phai ghi hang so goc.
- **Khong** them field/version vao `PlayerStateSaveData`. **Khong** rename `PlayerRuntimeState.baseHp` (theo quyet dinh user vong nay).

### Ket qua
Vong `seed -> load -> save -> load` gio **idempotent**:
`PlayerStateManager:38` seed `runtime.baseHp = player.base_hp` -> `GameManager:86` nap vao `initialBaseHp` -> `GameManager:256` save lai **dung `initialBaseHp` do**.
Het cong don `20*level` moi chu ky. Dong thoi `PlayerRuntimeState.MaxHp:114` (`baseHp + flatHp + VIT*HP_PER_VIT`) lai nhan dung hang so goc nhu no mong doi.

### Acceptance FIX-02
- `dotnet build Action_RPG\Assembly-CSharp.csproj -t:Rebuild` -> **Build succeeded, 0 Error, 16 Warning** (co san).
- `git diff --check -- Action_RPG` -> **pass**.
- Grep sau fix: chi con 1 writer `currentPlayerState.baseHp = snap.initialBaseHp` (GameManager:256). **Khong con caller nao doc `snap.baseHp`.**
- `snap.baseHp` van giu trong `BaseStatSnapshot` (gia tri dan xuat, co the dung cho UI/debug sau); khong xoa de tranh doi API o vong fix nho.

### Test chua chay
- **Play Mode:** save -> doi scene/checkpoint -> load -> save -> load, xac nhan `initialBaseHp` giu nguyen (truoc day tang `20*level` moi vong). Thuoc M-06.
- `npx gitnexus detect-changes` chua chay (MCP/CLI khong co trong session).

### Regression moi
Khong co. Bug fix in-memory, khong cham save file, khong doi API.

### Trang thai ACTIVE TASKS sau vong nay
`P2-DATA-FIX-01` va `FIX-02` deu xong. Con lai: **P2-DATA-05** (verify Companion) va **P2-DATA-06** (legacy cleanup gate) — ca hai chu yeu la verify/grep, lam batch duoc o vong sau.
<!-- CLAUDE_REPORT_END -->
