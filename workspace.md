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

### T-CC-03 — Test khong che

Kiem tra CC cua tung muc duoi day.

**Passive**
- [ ] VanguardPassive — tu slow ban than
- [ ] DuelistPassive — stun quai khi perfect parry
- [ ] MagePassive — slow, stun

**Skill**
- [ ] ChrisSkill — knockback
- [ ] VanguardSkill — knockback
- [ ] WarriorSkill — stun
- [ ] BattleMageSkill — stun
- [ ] DuelistSkill — stun
- [ ] MageSkill — stun, slow
- [ ] WardenSkill — knockback, stun
- [ ] JuggernautSkill — stun
- [ ] DarkInquisitorSkill — pull
- [ ] SwordMasterSkill — stun
- [ ] TricksterSkill — taunt, stun
- [ ] InfiltratorSkill — stun
- [ ] SpellbladeSkill — stun
- [ ] TacticianSkill — taunt, stun
- [ ] SpellbinderSkill — stun

**Signature**
- [ ] ChrisSignature — knockback
- [ ] LeoSignature — stun
- [ ] VanguardLiteSignature — stun
- [ ] WarriorLiteSignature — mien nhiem CC
- [ ] BattleMageSignature — slow
- [ ] MageLiteSignature — slow
- [ ] MageSignature — slow
- [ ] CatalystLiteSignature — fear

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
