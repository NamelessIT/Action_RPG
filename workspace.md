# Action_RPG Workspace

File nay la **noi theo doi trang thai chinh** giua user va Claude. Trello board `Unity` la board cua
team; file nay la ban chi tiet o muc code, cap nhat sau moi phien lam viec.

Cap nhat lan cuoi: **2026-08-27**

---

## DA XONG — 2026-08-27

### Ha tang
- [x] **Fix Safe Mode do xung dot package AI.** `com.unity.ai.assistant` 2.x da bao gom san
  `ai.toolkit` + `ai.generators`, nhung manifest van keo ban standalone ve qua
  `com.unity.2d.enhancers` -> trung GUID + trung ten assembly. Da go
  `com.unity.2d.enhancers` va `com.unity.ai.generators`.
- [x] **Chon MCP for Unity (CoplayDev)** thay cho Unity MCP chinh chu.
  Ly do: license Unity Personal cho phep 0 direct MCP connection
  (`AllowedMcpConnections = 0`), Unity xac nhan chinh thuc "a subscription is required".
  Config o `.mcp.json` -> `http://127.0.0.1:8080/mcp`.
  Luu y: nut Configure cho Claude Code trong panel LUON hong vi no goi CLI `claude`
  ma may nay khong cai ban terminal. Ghi `.mcp.json` tay la xong.
- [x] **Go han `com.unity.ai.assistant`.** Khong con dung, va no la nguon cua cac dong
  `NoSubscription` do trong console. Con lai `ai.inference` (Sentis) va `ai.navigation` (NavMesh).

### Refactor buoc 1 — gom enum  (commit `23a6a92`)
- [x] Gop 4 enum rarity trung nhau thanh mot `Rarity` dung chung:
  `WeaponData.Rarity`, `AccessoryData.Rarity`, `CoreShieldData.Rarity`, `CompanionRarity`.
- [x] Xac minh 197 asset ScriptableObject giu nguyen gia tri (serialize la int).

### Bug fix — Player roi xuyen dat vo han khi vao Play Mode
- [x] **Nguyen nhan:** save file ghi `positionY = -2858.67`, `GameManager.AssignStatsToPlayer()`
  nap lai nguyen xi -> player o duoi dat -> Rigidbody keo roi tiep -> thoat Play lai luu Y con
  thap hon -> lan sau te hon. Vong lap tu nuoi.
- [x] **Da loai tru:** scene khong co loi. Terrain co TerrainCollider enabled, phu kin vi tri
  Player, raycast xuong trung Terrain tai y=0, Player scene pos y=1.402 (cao hon mat dat 1.4m).
- [x] **Da loai tru:** khong phai do doi GameObject.Find -> FindGameObjectWithTag,
  scene chi co dung 1 object mang tag Player.
- [x] Sua save file hien tai (backup `.bak`), giu nguyen tien do level 3 / 296 exp / 15 diem.
- [x] Them `GameManager.IsSavedPositionUsable()` — tu choi toa do NaN/Infinity hoac nam duoi
  day the gioi (`Terrain.activeTerrain.y - 5`), roi dung vi tri spawn cua scene thay the.
- **LUU Y cho tuong lai:** day la ca THU HAI cua cung mot kieu bug save->load. Ca thu nhat la
  `[P2-DATA-FIX-02]` (baseHp cong don 20*level moi chu ky). Khi them field moi vao save,
  luon hoi: gia tri nay co the tro thanh dau vao cua chinh no o lan load sau khong?

### Refactor buoc 2 — gom link
- [x] Gom 14 duong dan `Resources.Load` viet cung vao `ResourcePaths`.
- [x] Gom 15 cho `FindFirstObjectByType<CompanionAI>()` vao `CompanionAI.Current` (co cache).
- [x] Gom 7 `PlayerStats` + 4 `EquipmentManager` + 3 `InventoryRuntime` vao `.Current` tuong ung.
- [x] Thong nhat cach tim Player: ca 7 cho dung `GameObject.FindGameObjectWithTag("Player")`
  (truoc do `GameManager` tim theo TEN, de gay khi doi ten object).
- [x] Va bug hieu nang: `CoreShieldEffectManager` quet toan scene MOI FRAME trong `Update()`.

**KHONG dong vao (co ly do):**
- `SkillManager` — scene co 3 instance (Player + Enemy). Comment o `DevToolPanel:129` da canh bao.
  Khong duoc gom thanh `.Current`.
- Cac `Find` con lai trong UI component — deu la lazy-init `if (_x == null) _x = Find...`,
  chay mot lan moi vong doi component. Gom them khong duoc gi.

---

### Refactor buoc 3a — gom style: hop nhat bang mau rarity

- [x] **Quyet dinh:** giu `RarityColors` lam NGUON DUY NHAT. Ly do: no da la bang dung chung,
  danh theo tier nen dung duoc cho ca module companion, va co san ca `Get()` lan `Hex()`.
- [x] Doi bac 1 Stained tu `#BFE6BF` (xanh nhat, chim tren nen dat) sang `#4CCC4C` cua bang
  item roi. 4 bac con lai giu theo bang UI (lech khong dang ke).
- [x] Xoa bang mau rieng trong `DroppedItemBehaviour.GetRarityColor()`, tro ve goi `RarityColors`.
- [x] Them overload `Get(Rarity)` / `Hex(Rarity)` de khoi phai ep `(int)` o cho goi.
- [x] **Tien the sua mot bug:** `GetRarityColor()` chi doc `entry.weaponData`, nen moi item roi
  la accessory deu ra mau TRANG. Nay doc ca `accessoryData`.
- Bang mau chot (da chay kiem tra tren Unity, khong can vao Play Mode):

  | Tier | Enum | RGB | Hex |
  |---|---|---|---|
  | 0 | Residual_1 | (0.62, 0.62, 0.62) | `#9E9E9E` xam |
  | 1 | Stained_2 | (0.30, 0.80, 0.30) | `#4CCC4C` luc |
  | 2 | Corrupted_3 | (0.30, 0.60, 1.00) | `#4C99FF` lam |
  | 3 | Condemned_4 | (0.70, 0.35, 1.00) | `#B259FF` tim |
  | 4 | Anomalous_5 | (1.00, 0.55, 0.10) | `#FF8C1A` cam |

---

## DANG CHO QUYET DINH

_Khong con muc nao._

---

## HANG DOI — REFACTOR (theo thu tu da thong nhat)

1. [x] Gom enum
2. [x] Gom link
3. [~] **Gom style** — bang mau rarity DA hop nhat (xem buoc 3a ben tren).
   Con lai: ~254 gia tri mau viet cung trong script (146 `new Color(...)` + 108 `Color.<ten>`)
   -> `UIPalette`.
4. [ ] **Gom time** — 12 cho ghi `Time.timeScale` (DevTool speed vs UI pause dang da nhau)
   va 78 so ma thuat trong `WaitForSeconds`. Day la buoc DUY NHAT doi hanh vi runtime,
   phai test theo ma tran M-06 ben duoi.
5. [ ] **Tach file qua lon** — `WeaponEffectManager` 1772 dong, `AccessoryEffectManager` 1648,
   `PlayerController` 1347, `Stats` 1295, `EnemyAI` 1188, `CoreShieldEffectManager` 1043,
   `EnemyCombat` 1009. Huong: switch khong lo -> polymorphism.
6. [ ] **Chia assembly** bang `.asmdef` (hien tai 191 script nam chung `Assembly-CSharp`).

---

## HANG DOI — CC & DEBUFF  (tu Trello, chua bat dau)

### T-CC-01 — Hoan thien he thong CC va Debuff

**Cac hieu ung CC:**
| Hieu ung | Hanh vi |
|---|---|
| Slow | Enemy: nhan % giam vao `baseMoveSpeed`. Player/Companion: cong vao `bonusMoveSpeed` |
| Stun | — |
| Knockback | — |
| Airborne | — |
| Root | Bi troi chan. VAN attack va dung Skill duoc, KHONG di chuyen va KHONG Dash |
| Silence | Khong the su dung Skill |
| Taunt | Bat buoc danh thuong vao muc tieu dung Taunt. Khong Skill, khong Dash |
| Pull | Lien tuc bi keo ve diem chi dinh |
| Fear | Chay ra xa khoi ke dung Fear voi 50% toc do. Khong Skill, khong Dash |

**Khang va mien nhiem:**
- `resistanceEffect` giam THOI GIAN chiu CC. **Khong** ap dung cho Knockback, Airborne, Pull.
- `knockbackResistance` chi ap dung cho Knockback, giam LUC knockback.
- Them bien **mien nhiem CC** (bool): true -> khong bi anh huong boi moi CC noi tren.
- Them bien **mien nhiem Slow** (bool): true -> khong the bi Slow.
- Them trang thai moi: kich hoat -> **ngay lap tuc thoat khoi moi CC**.

**Debuff:**
- Bleed — chiu sat thuong VAT LY moi giay.
- Burn — chiu sat thuong PHEP moi giay.
- (sau nay them tiep)
- Them trang thai moi: kich hoat -> **ngay lap tuc thoat khoi moi Debuff dang chiu**.

### T-CC-02 — Chinh sua Skill va effect sau khi xong he thong CC

Bug chung can sua truoc: **Slow apply len Enemy** — trong Inspector chua thay giam
`baseMoveSpeed` cua Enemy, nhung thuc te Enemy co bi Slow. Sua lai cho giam dung.

| Nhom | Muc | Viec |
|---|---|---|
| Skill | MageSkill | Mat Stun cua huong S; chua Slow huong SW |
| Skill | TricksterSkill | Sua code, dung ham Taunt moi |
| Skill | SpellbinderSkill | Sua code, dung ham Taunt moi |
| Signature | WarriorLiteSignature | Sua code, dung bien mien nhiem CC moi |
| Signature | BattleMageSignature | Chua slow |
| Signature | MageLiteSignature | Chua slow |
| Signature | MageSignature | Chua slow |
| Signature | CatalystLiteSignature | Sua code, dung ham Fear moi |
| Weapon | WPN_SW_T4_04 | Chua slow |
| Weapon | WPN_BW_T4_01 | Chua slow |
| Weapon | WPN_SW_T5_03 | Sua code, dung bien mien nhiem CC moi |
| Weapon | WPN_SP_T5_02 | Chua slow |
| Core Shield | SHD_CS_T4_01 | Sua code, dung bien mien nhiem CC moi |
| Accessory | ACC_RM_T5_05 | Sua code, dung bien mien nhiem CC moi |
| Accessory | ACC_MS_T4_06 | Chua tu stun ban than (BUG) |
| Accessory | ACC_PA_T5_03 | Sua code, dung bien mien nhiem CC moi |
| Protocol | PRT_SUP_T3_02 | Chua test Silence |
| Protocol | PRT_CAR_T4_01 | Chua slow |
| Protocol | PRT_SUP_T4_02 | Chua slow |
| Matrix | MTX_DEF_T3_01 | Sua code, dung bien mien nhiem CC moi |
| Matrix | MTX_REG_T4_02 | Sua code, dung bien giai CC + giai Debuff moi |
| Matrix | MTX_PHA_T4_02 | Sua code, dung bien Taunt moi |

### T-CC-03 — Audit tinh CC  (DA XONG 2026-08-27 — doc code, CHUA vao Play Mode)

Ket qua: **17/26 dat**, **9/26 co van de**. Moi muc giu 1 dong rieng, khong gom.
Cot "Code thuc te" ghi dung file:dong de mo thang.

#### Passive

| | Muc | Spec doi | Code thuc te | Ket luan |
|---|---|---|---|---|
| [x] | VanguardPassive | tu slow ban than | `VanguardPassive.cs:81/107` — `bonusMoveSpeed -= 0.5`, co `_slowWasApplied` guard, gate `vanguardCM3_NoBlockSlow` | **DAT** (dung `bonusMoveSpeed` dung nhu spec, nhung ngoai pipeline CC) |
| [x] | DuelistPassive | stun quai khi perfect parry | `DuelistPassive.cs:261` — `AddEffect(Stun 0.5s)` | **DAT** |
| [x] | MagePassive | slow, stun | `MagePassive.cs:432` Slow(0.1, 3s) · `:515` `SkillForceFreeze` | **DAT** |

#### Skill

| | Muc | Spec doi | Code thuc te | Ket luan |
|---|---|---|---|---|
| [ ] | ChrisSkill | knockback | `ChrisSkill.cs:20` `knockbackForce=15f` **khong dung o dau**; `:178` truyen `applyStun:true` | **SAI** — ra Stun thay vi Knockback |
| [ ] | VanguardSkill | knockback | `VanguardSkill.cs:175` `applyStun:true, stunTime:stunDuration` | **SAI** — ra Stun thay vi Knockback |
| [x] | WarriorSkill | stun | `WarriorSkill.cs:115` | **DAT** |
| [x] | BattleMageSkill | stun | `BattleMageSkill.cs:70` | **DAT** |
| [ ] | DuelistSkill | stun | `DuelistSkill.cs:101` — `impactLvl:0`, `applyStun` mac dinh **false** | **THIEU** — khong co CC nao |
| [ ] | MageSkill (huong S) | stun | `ArcaneEmpowerment.cs:305` — `if (magePassive != null) SkillForceFreeze(...)` | **THIEU co dieu kien** — stun nam TRONG MagePassive; khong co component do thi S chi gay damage. Dung nguyen nhan Trello ghi |
| [x] | MageSkill (huong SW) | slow | `ArcaneEmpowerment.cs:315` — `ApplyEffect(Slow)` vo dieu kien | **DAT** — Trello ghi "chua slow" la da cu |
| [ ] | WardenSkill | knockback + stun | `WardenSkill.cs:205` chi `AddEffect(Stun)` impact 2 | **THIEU Knockback** |
| [x] | JuggernautSkill | stun | `JuggernautSkill.cs:202` Stun + `:204` Knockback | **DAT** (du them knockback) |
| [ ] | DarkInquisitorSkill | pull | `DarkInquisitorSkill.cs:201` `PullEnemiesToCage` — `agent.Warp` moi frame, goi trong while loop `:142/:153` | **NGOAI PIPELINE** — chay dung nhung khong kiem `ccImmune`/khang |
| [x] | SwordMasterSkill | stun | `SwordMasterSkill.cs:160` impact 2, kem `:52` `BreakCrowdControl` + `:53` `ClearDebuffs` | **DAT** |
| [ ] | TricksterSkill | taunt + stun | Stun `:232` OK. Taunt `:222` = `agent.SetDestination(decoyPos)` **mot lan duy nhat** | **TAUNT HONG** — `EnemyAI:755/879` ghi de destination tick sau |
| [x] | InfiltratorSkill | stun | `InfiltratorSkill.cs:207` `_effStunDur` | **DAT** |
| [x] | SpellbladeSkill | stun | `SpellbladeSkill.cs:205` (`:168` don chinh khong stun) | **DAT** — chi nhanh phan don |
| [ ] | TacticianSkill | taunt + stun | Taunt `:121` — aggro + `nearestTarget`, co refresh theo interval, co revert (ban tot nhat trong 3) | **STUN THIEU** — khong tim thay stun nao trong file |
| [x] | SpellbinderSkill | stun | `SpellbinderSkill.cs:209` | **DAT** (taunt `:156` khong refresh/revert nhung spec khong doi taunt o day) |

#### Signature

| | Muc | Spec doi | Code thuc te | Ket luan |
|---|---|---|---|---|
| [x] | ChrisSignature | knockback | `ChrisSignature.cs:76` `AddEffect(Knockback)` | **DAT** |
| [x] | LeoSignature | stun | `LeoSignature.cs:67` | **DAT** |
| [x] | VanguardLiteSignature | stun | `VanguardLiteSignature.cs:120` — stun + knockback o hit thu 3 | **DAT** |
| [x] | WarriorLiteSignature | mien nhiem CC | `:53` `BreakCrowdControl()` + `:72` `isSuperArmor=true, superArmorLevel=999`, restore tu bien luu | **DAT** — chay dung, nhung **khong counter-safe**, xem T-CC-04 |
| [x] | BattleMageSignature | slow | `BattleMageSignature.cs:120` `ApplyEffect(Slow)` | **DAT** — Trello ghi "chua slow" la da cu |
| [x] | MageLiteSignature | slow | `MageLiteSignature.cs:95` | **DAT** — nt |
| [x] | MageSignature | slow | `MageSignature.cs:161` | **DAT** — nt |
| [ ] | CatalystLiteSignature | fear | `CatalystLiteSignature.cs:78` `FearFleeRoutine` tu che | **HONG** — 4 loi, xem T-CC-04 |

#### Cac muc T-CC-02 THUC RA DA XONG (dong lai, khong can lam)

- [x] **"Bug chung: Slow khong giam `baseMoveSpeed` cua Enemy"** — **KHONG phai bug.** Slow la he so
  nhan doc luc chay (`EffectiveSlowMultiplier`), co chu dich khong sua `baseMoveSpeed`, nen Inspector
  khong bao gio doi. Da noi day du: Player (`PlayerController:245/840/1279`), Enemy (`EnemyAI:755`,
  `EnemyCombat:199/510/720`), Companion (`CompanionAI:143/426/470`) — ca move lan attack cadence.
- [x] BattleMageSignature / MageLiteSignature / MageSignature "chua slow" — deu da co `ApplyEffect(Slow)`.
- [x] MageSkill "chua Slow huong SW" — da co (`ArcaneEmpowerment.cs:315`).
- [x] PRT_SUP_T4_02 "chua slow" — da co `SlowEnemy(target, 0.10f, 5f)` (`ProtocolEffectManager.cs:205`).
- [~] MTX_REG_T4_02 "dung bien giai CC + giai Debuff" — phan giai CC **da dung** (`BreakCrowdControl()`),
  phan giai Debuff **hong** — xem T-CC-04.

#### Chua audit (khong nam trong 26 muc user liet ke)

Cac muc T-CC-02 con lai thuoc Weapon/Core Shield/Accessory/Protocol/Matrix chua soi tung cai:
WPN_SW_T4_04, WPN_BW_T4_01, WPN_SW_T5_03, WPN_SP_T5_02, SHD_CS_T4_01, ACC_RM_T5_05,
ACC_MS_T4_06 (tu stun ban than), ACC_PA_T5_03, PRT_SUP_T3_02 (test Silence), PRT_CAR_T4_01,
MTX_DEF_T3_01, MTX_PHA_T4_02. Phan lon se tu het sau khi sua muc `isSuperArmor` o T-CC-04.


### T-CC-04 — Loi he thong tim duoc trong luc audit (MOI, uu tien cao)

Day la cac loi **khong nam trong Trello**, phat hien khi doc code. Sua nhung cai nay truoc
thi phan lon T-CC-02 tu het.

- [ ] **`isSuperArmor` bi ghi truc tiep tu 6 noi, khong counter-safe.** `superArmorLevel` duoc dung
  nhu bo dem cong don (`+=99/-=99`, `+=50/-=50`, `+=2/-=2`) nhung `isSuperArmor` lai la bool doc lap.
  Hai kieu hong nguoc nhau, ca hai deu that:
  - `AccessoryEffectManager` 630/1521 va `WeaponEffectManager` 795 khi TAT chi tru level,
    **khong bao gio set `isSuperArmor=false`** -> super armor ket vinh vien.
  - `CoreShieldEffectManager.ForceTurnOff_T4_01` (438) set `isSuperArmor=false`
    **vo dieu kien** -> **cuop mat** super armor cua nguon khac dang giu.
  - **Cach sua:** bien `isSuperArmor` thanh property dan xuat (`superArmorLevel > 0` hoac counter
    rieng), giong het cach `_ccImmuneCount` dang lam. Chi `MatrixEffectManager` (93/105) dung dung
    `PushCrowdControlImmunity/Pop`.
  - Muc T-CC-02 lien quan: WarriorLiteSignature, WPN_SW_T5_03, SHD_CS_T4_01, ACC_RM_T5_05, ACC_PA_T5_03, MTX_DEF_T3_01.

- [ ] **`MatrixEffectManager.Cleanse()` giai Debuff la NO-OP.** No set `stats.isBleeding = false`
  nhung `BleedRoutine` lap theo `bleedTimer` chu khong theo co `isBleeding` -> coroutine van tick
  du sat thuong roi tu set lai co. Va no **khong dung Burn**.
  `Stats.ClearDebuffs()` da lam dung ca hai (stop coroutine + reset timer). **Sua: goi `ClearDebuffs()`.**

- [ ] **`CatalystLiteSignature.FearFleeRoutine` — 4 loi:**
  1. `enemy.GetComponent<MonoBehaviour>()` lay **MonoBehaviour dau tien bat ky** tren object roi
     `enabled = false`. Comment trong code tu thu nhan "Gia dinh la EnemyAI script". Co the tat
     nham `Stats`/`EnemyCombat`. Thu tu component trong Inspector quyet dinh.
  2. Neu (1) khong trung EnemyAI thi EnemyAI van chay -> ghi de `agent.speed` (EnemyAI 755 set
     moi frame) va ghi de `SetDestination` -> **Fear khong lam gi ca**, chi pha mot component ngau nhien.
  3. `agent.speed *= 0.75f` roi `/= 0.75f`: hai Fear chong nhau -> hong vinh vien. Spec ghi **50%**, code **75%**.
  4. Quai chet giua Fear -> nhanh khoi phuc bi bo qua -> AI khong bat lai, speed khong tra ve.

- [ ] **Taunt co 3 ban tu che, khong ban nao theo spec.** Spec: "bat buoc danh thuong vao muc tieu
  Taunt, khong Skill, khong Dash". Ca 3 ban chi doi aggro, khong chan Skill/Dash:
  `TricksterSkill` (SetDestination mot lan, vo hieu) < `SpellbinderSkill` (aggro, khong refresh,
  khong revert) < `TacticianSkill` (aggro + refresh + revert).

---

### T-CC-05 — Viec con lai cua T-CC-01 sau audit

Nhieu thu spec doi **da co san** trong `Stats.cs`, khong can viet lai:

| Spec doi | Trang thai |
|---|---|
| Bien mien nhiem CC | DA CO — `IsCrowdControlImmune` + `Push/PopCrowdControlImmunity` (counter-safe) |
| Trang thai giai het CC | DA CO — `BreakCrowdControl()` (dung rule: khong cleanse duoc khi Airborne) |
| Trang thai giai het Debuff | DA CO — `ClearDebuffs()` (Bleed + Burn, stop dung coroutine) |
| Bleed / Burn | DA CO — `ApplyBleed` / `ApplyBurn` |
| `resistanceEffect` KHONG ap cho Knockback/Airborne | DA DUNG — Airborne dung raw `duration`, Knockback dung `knockbackResistance` rieng |
| `knockbackResistance` chi giam luc knockback | DA DUNG — `ApplyKnockbackEffect` |
| Slow: Enemy vao baseMoveSpeed, Player/Companion vao bonusMoveSpeed | Lam KHAC spec nhung **tot hon**: mot he so nhan chung `EffectiveSlowMultiplier` cho ca 3 phe. Khuyen nghi giu, sua spec thay vi sua code |

**Con THIEU that su — day moi la viec can lam cho T-CC-01:**
- [ ] Them `Taunt`, `Pull`, `Fear` vao enum `CombatEffectType` (hien chi co Stun/Knockback/Airborne/Root/Silence/Slow/Unknown).
- [ ] Viet handler cho 3 loai do trong `Stats.ApplyCombatEffects()`, va cac gate hanh vi:
      Taunt -> khoa Skill + Dash + ep muc tieu; Fear -> chay ra xa 50% toc do, khoa Skill + Dash;
      Pull -> keo lien tuc ve diem chi dinh.
- [ ] Them **mien nhiem Slow** (bool/counter rieng) — hien `ApplySlow` khong kiem gi ca.
- [ ] `ResistedDuration` phai bo qua Pull (spec) khi Pull ra doi.

---

## HANG DOI — COMPANION  (tu Trello, chua bat dau)

### T-COMP-01 — Sua UI Companion
Sua lai cho dep. Hien tai **chua hien**: Avatar, ProtocolType, MatrixType, PassiveIcon.
(Cac phan tu nay da tao san trong Canvas.)

### T-COMP-02 — Them Inventory cho Companion
Giong Inventory cua Player, co ca bang Stats de cong luon.

---

## MANUAL UNITY EDITOR TASKS

Tat ca checkbox trong muc nay can user thuc hien/confirm trong Unity Editor hoac Play Mode.

- [ ] **[GAP] M-01 - Xac minh scene dang dirty**
  - File user-owned: `Action_RPG/Assets/Scenes/OdoScene.unity`.
  - Diff truoc do chi tang `EnemyCombat.skillEffects.Array.size` len `1` tren mot prefab instance.
  - Trong Inspector, mo `Skill Effects[0]`, xac nhan `type`, `duration`, `impactLevel`,
    resistance va interrupt flags dung y do.
  - Save scene sau khi xac nhan.

- [ ] **M-02 - Gan nut DevTool game speed**
  - Tren `Canvas.prefab > DevToolPanel > Combat`, tao/gan bon Button vao:
    `CMD_SetGameSpeedSlow()` 0.25x, `CMD_SetGameSpeedHalf()` 0.5x,
    `CMD_SetGameSpeedNormal()` 1x, `CMD_SetGameSpeedFast()` 2x.
  - Test mo Inventory/SkillTree/DevTool van pause 0x.
  - **Lam SAU khi xong buoc refactor 4 (gom time)**, khong thi gan vao API sap bi thay.

- [ ] **M-03 - Tao va gan sample Enemy Attack Module assets**
  - Folder: `Assets/Resources/Datas/EnemyAttackModules/`.
  - Assets: `EAM_Melee_Sword`, `EAM_Melee_Dagger`, `EAM_Melee_Heavy_AOE`, `EAM_Ranged_Bow`,
    `EAM_Mage_TargetBolt`, `EAM_Mage_GroundAOE`, `EAM_DashStrike`.

- [ ] **M-04 - Tao/gan projectile va telegraph prefabs**
  - Projectile prefab cho Bow va Mage. Kiem Collider, Rigidbody, layer collision,
    `EnemyProjectile`, VFX, lifetime. Telegraph prefab cho GroundTargetAOE.

- [ ] **M-05 - Gan module cho enemy prefabs**
  - Enemy melee fallback de module null phai van danh nhu cu.
  - Gan basic/skill module cho: Sword/Dagger, Bow, Mage Ground AoE, Dash Strike.
  - Uu tien gan vao `EnemyData`; `EnemyCombat.basicAttackModule/skillAttackModule`
    chi dung nhu local override.

- [ ] **M-07 - Tao va gan EnemyData assets**
  - Folder: `Assets/Resources/Datas/Enemies/`.
  - `ED_Odo`, `ED_Orc_Warrior`, `ED_Boss_Golem`, `ED_Archer`, `ED_Mage`.
  - Set toi thieu: `enemyID`, `enemyName`, `monsterRank`, `baseHp`, `baseMoveSpeed`,
    `baseAttackSpeed`, `expReward`.

- [ ] **[GAP TRUOC COMMIT CUOI] M-06 - Play Mode acceptance matrix**
  - Fallback melee hoat dong khi enemy khong co module.
  - Sword/Dagger, ranged projectile, Ground AoE va Dash Strike hoat dong.
  - Stun, Knockback, Airborne, Root, Silence, Slow dung rule.
  - Stun co the cleanse bang skill duoc phep; Airborne khong the cleanse/action.
  - Player hit moi camera shake; Companion/Enemy hit khong camera shake.
  - Dev speed va UI pause khong dap `Time.timeScale` cua nhau.
  - Boss dash giu dung dash multiplier khi bi Slow.
  - EnemyAI khong ghi de destination khi module dang override movement.
  - Disable enemy giua dash khong ket invincibility/override.
  - Save/load player khong lam `initialBaseHp` tang them `20*level`.
  - SkillTree unlock va stat allocation cap nhat UI/runtime/save dung.
  - Console khong co error do.

---

## VIEC LINH TINH CHUA XU LY

- [ ] `Action_RPG/Assets/_Sprites/` — meta cu `craftpix-net-176111-...` bi xoa, `Boss.meta` moi
  chua track. Day la dau vet doi ten thu muc. Xac nhan Unity khong bao missing sprite roi commit rieng.
- [ ] File rong `rm` o goc repo (0 byte, tao 2026-08-27 09:55) — nhieu kha nang go nham lenh, xoa duoc.
- [ ] `GeneratedAssets/` (27 muc) o goc project — output cu cua `com.unity.ai.generators` da go.
  Neu khong con dung thi xoa.

---

## INFRA NOTES

### 2026-07-12 - Fix mat sprite khi clone tu nhanh `test`
- Nguyen nhan: `.gitignore` co dong `/Action_RPG/Assets/_Sprites` -> toan bo thu muc sprite
  nhan vat/enemy bi bo, khong push len. Prefab + animation van co, nhung tham chieu sprite
  bi thieu -> hien o hong/missing khi clone.
- Da xu ly: xoa dong ignore do, `git add` lai 813 file trong `Action_RPG/Assets/_Sprites`,
  commit + push nhanh `test`.
- **Viec can lam cho ai da clone truoc do:** `git pull origin test`. Sau khi pull, mo Unity
  de re-import; neu prefab van missing thi Reimport All.
