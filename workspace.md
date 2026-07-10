# Action_RPG Workspace

Trang thai hien tai: code task da xong va da checkpoint len `test`.

- Codex da verify static/build cho P2-DATA-04 va cac task P2-DATA truoc do.
- `ACTIVE TASKS`: khong con task code.
- Claude khong can lam gi them cho den khi user/Codex tao task moi.
- File nay tam thoi chi giu cac viec can lam thu cong trong Unity Editor.

## MANUAL UNITY EDITOR TASKS

Tat ca checkbox trong muc nay can user thuc hien/confirm trong Unity Editor hoac Play Mode.

- [ ] **[GAP] M-01 - Xac minh scene dang dirty**
  - File user-owned: `Action_RPG/Assets/Scenes/OdoScene.unity`.
  - Diff truoc do chi tang `EnemyCombat.skillEffects.Array.size` len `1` tren mot prefab instance.
  - Trong Inspector, mo `Skill Effects[0]`, xac nhan `type`, `duration`, `impactLevel`, resistance va interrupt flags dung y do.
  - Save scene sau khi xac nhan.
  - Evidence: ghi enemy/object da gan va gia tri effect vao day khi xong.

- [ ] **M-02 - Gan nut DevTool game speed**
  - Tren `Canvas.prefab > DevToolPanel > Combat`, tao/gan bon Button vao:
    - `CMD_SetGameSpeedSlow()` - 0.25x.
    - `CMD_SetGameSpeedHalf()` - 0.5x.
    - `CMD_SetGameSpeedNormal()` - 1x.
    - `CMD_SetGameSpeedFast()` - 2x.
  - Test mo Inventory/SkillTree/DevTool van pause 0x.
  - Dong panel phai tro ve gameplay speed da chon.

- [ ] **M-03 - Tao va gan sample Enemy Attack Module assets**
  - Tao folder neu chua co: `Assets/Resources/Datas/EnemyAttackModules/`.
  - Tao cac asset:
    - `EAM_Melee_Sword`
    - `EAM_Melee_Dagger`
    - `EAM_Melee_Heavy_AOE`
    - `EAM_Ranged_Bow`
    - `EAM_Mage_TargetBolt`
    - `EAM_Mage_GroundAOE`
    - `EAM_DashStrike`
  - Gan style/timing/range/damage/effects phu hop cho tung module.
  - Luu day du `.asset` va `.meta`.

- [ ] **M-04 - Tao/gan projectile va telegraph prefabs**
  - Tao hoac chon projectile prefab cho Bow va Mage.
  - Kiem tra Collider, Rigidbody neu dung, layer collision, `EnemyProjectile`, VFX va lifetime.
  - Tao/gan telegraph prefab cho GroundTargetAOE neu module can.

- [ ] **M-05 - Gan module cho enemy prefabs**
  - Enemy melee fallback de module null phai van danh nhu cu.
  - Gan basic/skill module cho it nhat:
    - Sword/Dagger enemy.
    - Bow enemy.
    - Mage Ground AoE enemy.
    - Dash Strike enemy.
  - Sau P2-DATA, uu tien gan module vao `EnemyData`.
  - `EnemyCombat.basicAttackModule/skillAttackModule` chi dung nhu local override/tam thoi.
  - Kiem tra prefab overrides khong mat reference sau khi move/save.

- [ ] **M-07 - Tao va gan EnemyData assets**
  - Tao folder neu chua co: `Assets/Resources/Datas/Enemies/`.
  - Tao asset data cho tung enemy archetype, vi du:
    - `ED_Odo`
    - `ED_Orc_Warrior`
    - `ED_Boss_Golem`
    - `ED_Archer`
    - `ED_Mage`
  - Set toi thieu:
    - `enemyID`
    - `enemyName`
    - `monsterRank`
    - `baseHp`
    - `baseMoveSpeed`
    - `baseAttackSpeed`
    - `expReward`
  - Gan `basicAttackModule` va `skillAttackModule` vao EnemyData thay vi gan truc tiep len tung prefab, tru truong hop can override rieng.
  - Tren prefab/scene enemy, gan `EnemyStats.data = EnemyData tuong ung`.
  - Evidence: ghi danh sach enemy prefab da gan data va module nao.

- [ ] **[GAP TRUOC COMMIT CUOI] M-06 - Play Mode acceptance matrix**
  - Fallback melee hoat dong khi enemy khong co module.
  - Sword/Dagger, ranged projectile, Ground AoE va Dash Strike hoat dong.
  - Stun, Knockback, Airborne, Root, Silence va Slow dung rule.
  - Stun co the cleanse bang skill duoc phep; Airborne khong the cleanse/action.
  - Player hit moi camera shake; Companion/Enemy hit khong camera shake.
  - Dev speed va UI pause khong dap `Time.timeScale` cua nhau.
  - Boss dash giu dung dash multiplier khi bi Slow.
  - EnemyAI khong ghi de destination khi module dang override movement.
  - Disable enemy giua dash khong ket invincibility/override.
  - Save/load player khong lam `initialBaseHp` tang them `20*level`.
  - SkillTree unlock va stat allocation cap nhat UI/runtime/save dung.
  - Console khong co error do.
  - Evidence: ghi ket qua test vao day khi xong.

## NEXT CODE TASKS

Khong co. Khi user muon lam tiep, Codex se tao task moi trong workspace nay.
