#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// TEMP DEV-ONLY CC/Debuff test panel for Phase F.
/// Auto-created at runtime; press F10 to show/hide. No scene/prefab setup required.
/// Delete this file once the CC/Debuff migration is complete.
/// </summary>
public sealed class CCDebugTestPanel : MonoBehaviour
{
    private enum TargetKind { Player, NearestEnemy, Companion, DarkDemon }

    private static CCDebugTestPanel _instance;
    public static bool IsOpen => _instance != null && _instance._visible;

    private const float PanelWidth = 720f;
    private const float PanelHeight = 980f; // Clamped to screen; sections are collapsed by default.
    private TargetKind _targetKind = TargetKind.Player;
    private Stats _target;
    private Vector2 _scroll;
    private Rect _window = new Rect(20f, 20f, PanelWidth, PanelHeight);
    private bool _visible;
    private bool _cursorStateCaptured;
    private bool _cursorWasVisible;
    private CursorLockMode _cursorWasLocked;

    private float _duration = 3f;
    private float _slowMagnitude = 0.30f;
    private float _knockbackForce = 12f;
    private float _airborneHeight = 1.2f;
    private float _pullSpeed = 4f;
    private int _impactLevel = 0;
    private Transform _fixedPullMarker;
    private Transform _movingPullAnchor;

    private bool _panelCcImmune;
    private bool _panelSlowImmune;
    private string _lastResult = "Ready";

    // Runtime IMGUI foldouts. Keep the high-signal overview visible and hide deep diagnostics
    // until a tester explicitly needs them. This state persists while the panel GameObject lives.
    private bool _showTargetTools;
    private bool _showRuntimeDetails;
    private bool _showRequestSettings;
    private bool _showCrowdControl;
    private bool _showSourceStackTests;
    private bool _showForcedControl;
    private bool _showImmunityResistance;
    private bool _showDamagePipeline;
    private bool _showDebuffRegistry;
    private bool _showEffectiveStatTests;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[DEV] CC Debug Test Panel");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<CCDebugTestPanel>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
            SetVisible(!_visible);

        if (_visible)
        {
            // Gameplay normally locks/hides the cursor. Keep the debug panel interactive
            // without pausing Time.timeScale, so CC durations/DoTs can still be tested live.
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            ResolveTarget();
        }
    }

    private void OnGUI()
    {
        if (!_visible) return;

        // 1.5x panel footprint, clamped so it remains usable on 720p/1080p screens.
        _window.width = Mathf.Min(PanelWidth, Mathf.Max(360f, Screen.width - 20f));
        _window.height = Mathf.Min(PanelHeight, Mathf.Max(500f, Screen.height - 30f));
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - _window.height));

        // Enlarge controls/text as well as the window itself. Restore GUI.skin immediately
        // afterwards so this dev panel does not resize other OnGUI debug UIs.
        int oldButtonFont = GUI.skin.button.fontSize;
        float oldButtonHeight = GUI.skin.button.fixedHeight;
        int oldLabelFont = GUI.skin.label.fontSize;
        int oldTextFieldFont = GUI.skin.textField.fontSize;
        int oldTextAreaFont = GUI.skin.textArea.fontSize;
        int oldWindowFont = GUI.skin.window.fontSize;

        GUI.skin.button.fontSize = 15;
        GUI.skin.button.fixedHeight = 34f;
        GUI.skin.label.fontSize = 15;
        GUI.skin.textField.fontSize = 15;
        GUI.skin.textArea.fontSize = 15;
        GUI.skin.window.fontSize = 16;

        _window = GUI.Window(GetInstanceID(), _window, DrawWindow, "CC / Debuff Debug — F10 close");

        GUI.skin.button.fontSize = oldButtonFont;
        GUI.skin.button.fixedHeight = oldButtonHeight;
        GUI.skin.label.fontSize = oldLabelFont;
        GUI.skin.textField.fontSize = oldTextFieldFont;
        GUI.skin.textArea.fontSize = oldTextAreaFont;
        GUI.skin.window.fontSize = oldWindowFont;
    }

    private void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;

        if (_visible)
        {
            if (!_cursorStateCaptured)
            {
                _cursorWasVisible = Cursor.visible;
                _cursorWasLocked = Cursor.lockState;
                _cursorStateCaptured = true;
            }
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            RestoreCursorState();
        }
    }

    private void RestoreCursorState()
    {
        if (!_cursorStateCaptured) return;

        // If another blocking UI is open, it owns the cursor and must keep it visible/unlocked.
        if (UIPauseManager.IsPaused)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = _cursorWasVisible;
            Cursor.lockState = _cursorWasLocked;
        }

        _cursorStateCaptured = false;
    }

    private void DrawWindow(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        DrawTargetSelector();
        ResolveTarget();

        if (_target == null)
        {
            GUILayout.Space(8f);
            GUILayout.BeginVertical("box");
            GUILayout.Label("NO TARGET FOUND", LargeHeaderStyle());
            StatusLabel("No target found for the current selection. Choose another target or spawn a Dark Demon from Target Tools.");
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUI.DragWindow();
            return;
        }

        DrawQuickOverview();
        DrawQuickActions();
        DrawRecentResult();
        DrawExpandCollapseControls();

        _showTargetTools = DrawFoldoutHeader("TARGET TOOLS / SPAWN", _showTargetTools);
        if (_showTargetTools) DrawTargetToolsSection();

        _showRuntimeDetails = DrawFoldoutHeader("RUNTIME DETAILS", _showRuntimeDetails);
        if (_showRuntimeDetails) DrawRuntimeDetailsSection();

        _showRequestSettings = DrawFoldoutHeader("REQUEST SETTINGS", _showRequestSettings);
        if (_showRequestSettings) DrawRequestSettingsSection();

        _showCrowdControl = DrawFoldoutHeader("CROWD CONTROL ACTIONS", _showCrowdControl);
        if (_showCrowdControl) DrawCrowdControlSection();

        _showSourceStackTests = DrawFoldoutHeader("SOURCE / STACK TESTS", _showSourceStackTests);
        if (_showSourceStackTests) DrawSourceStackSection();

        _showForcedControl = DrawFoldoutHeader("FORCED CONTROL (TAUNT / FEAR)", _showForcedControl);
        if (_showForcedControl) DrawForcedControlSection();

        _showImmunityResistance = DrawFoldoutHeader("IMMUNITY / SUPER ARMOR / RESISTANCE", _showImmunityResistance);
        if (_showImmunityResistance) DrawImmunityResistanceSection();

        _showDamagePipeline = DrawFoldoutHeader("INVINCIBLE / DAMAGE PIPELINE", _showDamagePipeline);
        if (_showDamagePipeline) DrawDamagePipelineSection();

        _showDebuffRegistry = DrawFoldoutHeader("DEBUFF REGISTRY TESTS", _showDebuffRegistry);
        if (_showDebuffRegistry) DrawDebuffRegistrySection();

        _showEffectiveStatTests = DrawFoldoutHeader("EFFECTIVE STAT / COMBAT PROBES", _showEffectiveStatTests);
        if (_showEffectiveStatTests) DrawEffectiveStatTestsSection();

        GUILayout.Space(10f);
        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    private void DrawTargetSelector()
    {
        GUILayout.Label("SELECT TARGET", EditorLikeHeader());
        GUILayout.BeginHorizontal();
        TargetButton("Player", TargetKind.Player);
        TargetButton("Enemy", TargetKind.NearestEnemy);
        TargetButton("Companion", TargetKind.Companion);
        TargetButton("Dark Demon", TargetKind.DarkDemon);
        GUILayout.EndHorizontal();
    }

    private void DrawQuickOverview()
    {
        StatusControlSnapshot c = _target.StatusEffects != null ? _target.StatusEffects.ControlState : StatusControlSnapshot.Normal;
        string control = _target.StatusEffects != null ? c.mode.ToString() : "-";
        int debuffCount = _target.StatusEffects != null ? _target.StatusEffects.ManagedDebuffCount : 0;

        GUILayout.Space(8f);
        GUILayout.BeginVertical("box");
        GUILayout.Label(_target.gameObject.name, LargeHeaderStyle());
        GUILayout.Label($"Control: {control}   |   HP: {_target.currentHp:F0}/{_target.maxHp:F0}   |   Shield: {_target.currentShield:F0}", QuickStatusStyle());
        GUILayout.Label($"Slow: {_target.CurrentSlowAmount:P0}   |   Move: {_target.EffectiveMoveSpeed:F2}   |   AtkSpeed: {_target.EffectiveAttackSpeed:F2}   |   Debuffs: {debuffCount}", QuickStatusStyle());
        GUILayout.EndVertical();
    }

    private void DrawQuickActions()
    {
        GUILayout.Space(6f);
        GUILayout.Label("QUICK ACTIONS", EditorLikeSubHeader());
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Cleanse CC"))
        {
            _target.BreakCrowdControl();
            _lastResult = "Cleanse CC called";
        }
        if (GUILayout.Button("Cleanse Debuffs"))
        {
            _target.ClearDebuffs();
            _lastResult = "Cleanse Debuffs called";
        }
        if (GUILayout.Button("Reset Target")) ResetSelectedTargetTestFlags();
        GUILayout.EndHorizontal();
    }

    private void DrawRecentResult()
    {
        GUILayout.Space(6f);
        GUILayout.BeginVertical("box");
        GUILayout.Label("LAST RESULT", EditorLikeSubHeader());
        GUILayout.Label(_lastResult ?? string.Empty, ResultStyle(), GUILayout.ExpandWidth(true));
        GUILayout.EndVertical();
    }

    private void DrawExpandCollapseControls()
    {
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Expand all", GUILayout.Width(120f))) SetAllSections(true);
        if (GUILayout.Button("Collapse all", GUILayout.Width(120f))) SetAllSections(false);
        GUILayout.EndHorizontal();
    }

    private void DrawTargetToolsSection()
    {
        BeginSectionBody();
        AllyStats debugOwner = PlayerStats.Current;
        StatusLabel($"Dark Demons (Player owner): {DarkDemonAI.GetActiveCount(debugOwner)}/{DarkDemonAI.MaxActivePerOwner}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn 1 Demon")) SpawnDebugDemons(1);
        if (GUILayout.Button("Spawn 3 Demons")) SpawnDebugDemons(3);
        if (GUILayout.Button("Kill Player Demons")) KillDebugDemons();
        GUILayout.EndHorizontal();
        EndSectionBody();
    }

    private void DrawRuntimeDetailsSection()
    {
        BeginSectionBody();
        StatusLabel($"Type: {_target.GetType().Name}");
        StatusLabel($"HP {_target.currentHp:F1}/{_target.maxHp:F1} | Shield {_target.currentShield:F1}");
        StatusLabel($"Stun={_target.IsStunned} | Root={_target.IsRooted} | Silence={_target.IsSilenced} | Displaced={_target.IsDisplaced}");
        bool taunted = _target.StatusEffects != null && _target.StatusEffects.IsTaunted;
        bool feared = _target.StatusEffects != null && _target.StatusEffects.IsFeared;
        StatusLabel($"Taunt={taunted} | Fear={feared} | Slow={_target.CurrentSlowAmount:P0}");
        StatusLabel($"CCImmune={_target.IsCrowdControlImmune} | SlowImmune={_target.IsSlowImmune} | SuperArmor={_target.IsSuperArmor} | armorLevel={_target.EffectiveSuperArmorLevel}");
        StatusLabel($"Resistance={_target.resistanceEffect:P0} | KnockbackRes={_target.knockbackResistance:P0}");

        GUILayout.Space(4f);
        GUILayout.Label("EFFECTIVE STATS", EditorLikeSubHeader());
        StatusLabel($"Move: {_target.baseMoveSpeed:F2} -> {_target.EffectiveMoveSpeed:F2} | AttackSpeed: {_target.baseAttackSpeed:F2} -> {_target.EffectiveAttackSpeed:F2}");
        StatusLabel($"Armor: {_target.armor:F1} -> {_target.EffectiveArmor:F1} | MR: {_target.magicResist:F1} -> {_target.EffectiveMagicResist:F1}");
        StatusLabel($"Defense: {_target.defenseValue:F1} -> {_target.EffectiveDefenseValue:F1}");
        StatusLabel($"Damage Out: {_target.damageOutputMultiplier:F2} -> {_target.EffectiveDamageOutputMultiplier:F2} | Damage Taken: {_target.damageTakenMultiplier:F2} -> {_target.EffectiveDamageTakenMultiplier:F2}");

        if (_target.StatusEffects != null)
        {
            GUILayout.Space(4f);
            GUILayout.Label("CONTROL STATE", EditorLikeSubHeader());
            var c = _target.StatusEffects.ControlState;
            string forcedTarget = c.forcedTargetTransform != null
                ? c.forcedTargetTransform.name
                : (c.forcedTarget != null ? c.forcedTarget.name : "-");
            string controlSource = c.controlSource != null ? c.controlSource.name : "-";
            StatusLabel($"Mode={c.mode} | Move={c.canMove} | Dash={c.canDash} | Attack={c.canBasicAttack} | Skill={c.canCastNormalSkill} | Cleanse={c.canCastCleanseSkill}");
            StatusLabel($"ForcedMove={c.canForcedMove} | ForcedAttack={c.canForcedBasicAttack} | Source={controlSource} | Target={forcedTarget} | MoveMult={c.forcedMoveSpeedMultiplier:F2}");

            GUILayout.Space(4f);
            GUILayout.Label("MANAGED EFFECTS", EditorLikeSubHeader());
            StatusLabel("CC: " + _target.StatusEffects.GetManagedDebugSummary());
            StatusLabel("Displacement: " + _target.StatusEffects.GetDisplacementDebugSummary());
            StatusLabel("Debuffs: " + _target.StatusEffects.GetManagedDebuffDebugSummary());
            StatusLabel($"Bleed={_target.isBleeding} | Burn={_target.isBurning} | DebuffCount={_target.StatusEffects.ManagedDebuffCount}");
        }
        EndSectionBody();
    }

    private void DrawRequestSettingsSection()
    {
        BeginSectionBody();
        _duration = FloatField("Duration", _duration);
        _slowMagnitude = FloatField("Slow magnitude (0..1)", _slowMagnitude);
        _knockbackForce = FloatField("Knockback force", _knockbackForce);
        _airborneHeight = FloatField("Airborne height", _airborneHeight);
        _pullSpeed = FloatField("Pull speed", _pullSpeed);
        _impactLevel = IntField("Impact level", _impactLevel);
        EndSectionBody();
    }

    private void DrawCrowdControlSection()
    {
        BeginSectionBody();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Stun")) ApplyCc(CombatEffectType.Stun);
        if (GUILayout.Button("Root")) ApplyCc(CombatEffectType.Root);
        if (GUILayout.Button("Silence")) ApplyCc(CombatEffectType.Silence);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Taunt")) ApplyCc(CombatEffectType.Taunt);
        if (GUILayout.Button("Fear")) ApplyCc(CombatEffectType.Fear);
        if (GUILayout.Button("Slow")) ApplyCc(CombatEffectType.Slow);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Airborne")) ApplyCc(CombatEffectType.Airborne);
        if (GUILayout.Button("Knockback")) ApplyCc(CombatEffectType.Knockback);
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label("PULL", EditorLikeSubHeader());
        StatusLabel("Static Marker stays in world space. Moving Marker can be repositioned while Pull is active.");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Pull -> Static")) ApplyFixedPull();
        if (GUILayout.Button("Pull -> Moving")) ApplyMovingPull();
        if (GUILayout.Button("Move Marker +2m")) MoveDebugPullAnchor();
        GUILayout.EndHorizontal();
        EndSectionBody();
    }

    private void DrawSourceStackSection()
    {
        BeginSectionBody();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Slow A 20% / 5s")) ApplyNamedSlow("DEV_SLOW_A", "DEV_SOURCE_A", 0.20f, 5f, EffectStackPolicy.RefreshSameSource, 1);
        if (GUILayout.Button("Slow B 30% / 3s")) ApplyNamedSlow("DEV_SLOW_B", "DEV_SOURCE_B", 0.30f, 3f, EffectStackPolicy.RefreshSameSource, 1);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Stack Slow +10% (max 3)"))
            ApplyNamedSlow("DEV_STACK_SLOW", "DEV_STACK_SOURCE", 0.10f, 5f, EffectStackPolicy.AdditiveStacks, 3);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Stun A / 2s")) ApplyNamedCc(CombatEffectType.Stun, "DEV_STUN_A", "DEV_SOURCE_A", 2f);
        if (GUILayout.Button("Stun B / 5s")) ApplyNamedCc(CombatEffectType.Stun, "DEV_STUN_B", "DEV_SOURCE_B", 5f);
        GUILayout.EndHorizontal();
        EndSectionBody();
    }

    private void DrawForcedControlSection()
    {
        BeginSectionBody();
        StatusLabel("Layering check: Taunt 8s -> Fear 4s. Fear should win first; Taunt resumes if duration remains. Stun/Displacement override both.");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Taunt 8s")) ApplyNamedCc(CombatEffectType.Taunt, "DEV_TAUNT_LONG", "DEV_TAUNT_SOURCE", 8f);
        if (GUILayout.Button("Fear 4s")) ApplyNamedCc(CombatEffectType.Fear, "DEV_FEAR_SHORT", "DEV_FEAR_SOURCE", 4f);
        GUILayout.EndHorizontal();
        EndSectionBody();
    }

    private void DrawImmunityResistanceSection()
    {
        BeginSectionBody();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(_panelCcImmune ? "CC Immunity: ON" : "CC Immunity: OFF")) ToggleCcImmunity();
        if (GUILayout.Button(_panelSlowImmune ? "Slow Immunity: ON" : "Slow Immunity: OFF")) ToggleSlowImmunity();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Super Armor ON")) { _target.isSuperArmor = true; _lastResult = "Super Armor ON"; }
        if (GUILayout.Button("Super Armor OFF")) { _target.isSuperArmor = false; _lastResult = "Super Armor OFF"; }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("armor=0")) SetArmorLevel(0);
        if (GUILayout.Button("armor=1")) SetArmorLevel(1);
        if (GUILayout.Button("armor=2")) SetArmorLevel(2);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Resist 0%")) SetResistance(0f);
        if (GUILayout.Button("Resist 50%")) SetResistance(0.5f);
        if (GUILayout.Button("Resist 100%")) SetResistance(1f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("KB Resist 0%")) SetKnockbackResistance(0f);
        if (GUILayout.Button("KB Resist 50%")) SetKnockbackResistance(0.5f);
        if (GUILayout.Button("KB Resist 100%")) SetKnockbackResistance(1f);
        GUILayout.EndHorizontal();
        EndSectionBody();
    }

    private void DrawDamagePipelineSection()
    {
        BeginSectionBody();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Invincible ON")) { _target.isInvincible = true; _lastResult = "Invincible ON"; }
        if (GUILayout.Button("Invincible OFF")) { _target.isInvincible = false; _lastResult = "Invincible OFF"; }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+100 Shield")) { _target.AddShield(100f); _lastResult = "+100 Shield"; }
        if (GUILayout.Button("Hit 100 + Stun")) TestDamageHitWithStun();
        GUILayout.EndHorizontal();
        EndSectionBody();
    }

    private void DrawDebuffRegistrySection()
    {
        BeginSectionBody();
        StatusLabel("Bleed A/B use different source keys. AttackSpeedDown A/B use Strongest: 30% wins, then 20% resumes if still active.");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Bleed A 10/s 5s")) ApplyDebugDot("Bleed", "DEV_BLEED_A", 10f, 5f);
        if (GUILayout.Button("Bleed B 20/s 3s")) ApplyDebugDot("Bleed", "DEV_BLEED_B", 20f, 3f);
        if (GUILayout.Button("Burn 10/s 5s")) ApplyDebugDot("Burn", "DEV_BURN_A", 10f, 5f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("AtkSpeedDown 20% / 5s")) ApplyDebugAttackSpeedDown(0.20f, 5f, "DEV_AS_DOWN_A");
        if (GUILayout.Button("AtkSpeedDown 30% / 3s")) ApplyDebugAttackSpeedDown(0.30f, 3f, "DEV_AS_DOWN_B");
        GUILayout.EndHorizontal();
        EndSectionBody();
    }

    private void DrawEffectiveStatTestsSection()
    {
        BeginSectionBody();
        StatusLabel("Dev-only modifiers verify ledger -> effective stat -> combat consumer. They are test tools, not migrated gameplay producers.");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("ArmorDown -50 / 5s")) ApplyDebugFlatStatDebuff("DEV_ARMOR_DOWN", RuntimeStatKey.Armor, -50f, 5f);
        if (GUILayout.Button("MRDown -50 / 5s")) ApplyDebugFlatStatDebuff("DEV_MR_DOWN", RuntimeStatKey.MagicResist, -50f, 5f);
        if (GUILayout.Button("DefenseDown -20 / 5s")) ApplyDebugFlatStatDebuff("DEV_DEF_DOWN", RuntimeStatKey.DefenseValue, -20f, 5f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Weaken -20% / 5s")) ApplyDebugFlatStatDebuff("DEV_WEAKEN", RuntimeStatKey.DamageOutputMultiplier, -0.20f, 5f);
        if (GUILayout.Button("Vulnerability +20% / 5s")) ApplyDebugFlatStatDebuff("DEV_VULNERABILITY", RuntimeStatKey.DamageTakenMultiplier, 0.20f, 5f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Probe Physical")) ProbeCombatMath(false);
        if (GUILayout.Button("Probe Magic")) ProbeCombatMath(true);
        GUILayout.EndHorizontal();
        EndSectionBody();
    }

    private bool DrawFoldoutHeader(string title, bool expanded)
    {
        GUILayout.Space(6f);
        string prefix = expanded ? "▼" : "▶";
        if (GUILayout.Button($"{prefix}  {title}", FoldoutHeaderStyle(), GUILayout.ExpandWidth(true)))
            expanded = !expanded;
        return expanded;
    }

    private void BeginSectionBody()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Space(2f);
    }

    private void EndSectionBody()
    {
        GUILayout.Space(2f);
        GUILayout.EndVertical();
    }

    private void SetAllSections(bool expanded)
    {
        _showTargetTools = expanded;
        _showRuntimeDetails = expanded;
        _showRequestSettings = expanded;
        _showCrowdControl = expanded;
        _showSourceStackTests = expanded;
        _showForcedControl = expanded;
        _showImmunityResistance = expanded;
        _showDamagePipeline = expanded;
        _showDebuffRegistry = expanded;
        _showEffectiveStatTests = expanded;
    }

    private void TargetButton(string label, TargetKind kind)
    {
        bool selected = _targetKind == kind;
        if (GUILayout.Toggle(selected, label, "Button") && !selected)
        {
            // Immunity keys belong to previous target; revoke before switching.
            RevokePanelImmunities();
            _targetKind = kind;
            ResolveTarget();
        }
    }

    private void ResolveTarget()
    {
        switch (_targetKind)
        {
            case TargetKind.Player:
                _target = PlayerStats.Current;
                break;
            case TargetKind.NearestEnemy:
                _target = FindNearestEnemy();
                break;
            case TargetKind.Companion:
                CompanionAI c = CompanionAI.Current;
                _target = c != null ? c.GetComponent<Stats>() : null;
                break;
            case TargetKind.DarkDemon:
                DarkDemonAI demon = FindNearestDarkDemon();
                _target = demon != null ? demon.DemonStats : null;
                break;
        }
    }

    private DarkDemonAI FindNearestDarkDemon()
    {
        Vector3 origin = PlayerStats.Current != null ? PlayerStats.Current.transform.position : transform.position;
        return DarkDemonAI.FindNearest(origin, PlayerStats.Current);
    }

    private void SpawnDebugDemons(int count)
    {
        AllyStats owner = PlayerStats.Current;
        if (owner == null)
        {
            _lastResult = "DarkDemon DEV spawn failed: PlayerStats.Current not found";
            return;
        }

        int spawned = DarkDemonAI.SpawnDebugPack(owner, count);
        _targetKind = TargetKind.DarkDemon;
        ResolveTarget();
        _lastResult = $"DarkDemon DEV spawn: requested={count}, spawned={spawned}, active={DarkDemonAI.GetActiveCount(owner)}/{DarkDemonAI.MaxActivePerOwner}";
    }

    private void KillDebugDemons()
    {
        AllyStats owner = PlayerStats.Current;
        int killed = DarkDemonAI.DestroyAllForOwner(owner);
        if (_targetKind == TargetKind.DarkDemon) _target = null;
        _lastResult = $"DarkDemon DEV cleanup: removed={killed}";
    }

    private Stats FindNearestEnemy()
    {
        EnemyStats[] enemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return null;
        Vector3 origin = PlayerStats.Current != null ? PlayerStats.Current.transform.position : transform.position;
        EnemyStats best = null;
        float bestSq = float.PositiveInfinity;
        foreach (EnemyStats e in enemies)
        {
            if (e == null || e.isDead) continue;
            float sq = (e.transform.position - origin).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        return best;
    }

    private void ApplyCc(CombatEffectType type)
    {
        ResolveTarget();
        if (_target == null || _target.StatusEffects == null) return;

        var effect = new CombatEffectInfo(type, _duration)
        {
            effectId = $"DEV_TEST_{type}",
            impactLevel = Mathf.Max(0, _impactLevel),
            magnitude = Mathf.Clamp01(_slowMagnitude),
            force = Mathf.Max(0f, _knockbackForce),
            height = Mathf.Max(0f, _airborneHeight),
            sourcePosition = _target.transform.position + Vector3.left * 2f,
            note = "CCDebugTestPanel Phase E"
        };

        Stats source = ResolveSourceForTarget(_target);
        EffectApplyResult result = _target.StatusEffects.ApplyCrowdControl(effect, new StatusEffectContext(source, _target, "DEV_TEST_PANEL"));
        _lastResult = $"{type}: {result.status} | resolvedDuration={result.resolvedDuration:F2}s | handle={result.handle}";
        Debug.Log($"[CC TEST] target={_target.name} type={type} impact={_impactLevel} => {_lastResult}");
    }

    private void ApplyFixedPull()
    {
        ResolveTarget();
        if (_target == null || _target.StatusEffects == null) return;

        EnsureFixedPullMarker();
        _fixedPullMarker.position = _target.transform.position + Vector3.right * 3f;
        Vector3 anchor = _fixedPullMarker.position;
        var effect = new CombatEffectInfo(CombatEffectType.Pull, Mathf.Max(0.01f, _duration))
        {
            effectId = "DEV_PULL_FIXED",
            impactLevel = Mathf.Max(0, _impactLevel),
            pullAnchorMode = PullAnchorMode.FixedPoint,
            pullPoint = anchor,
            pullSpeed = Mathf.Max(0.01f, _pullSpeed),
            note = "CCDebugTestPanel fixed Pull"
        };
        Stats source = ResolveSourceForTarget(_target);
        EffectApplyResult result = _target.StatusEffects.ApplyCrowdControl(effect, new StatusEffectContext(source, _target, "DEV_PULL_FIXED_SOURCE"));
        _lastResult = $"Pull -> Static Marker {anchor}: {result.status} | {_target.StatusEffects.GetDisplacementDebugSummary()}";
    }

    private void ApplyMovingPull()
    {
        ResolveTarget();
        if (_target == null || _target.StatusEffects == null) return;
        EnsureMovingPullAnchor();
        _movingPullAnchor.position = _target.transform.position + Vector3.forward * 3f;

        var effect = new CombatEffectInfo(CombatEffectType.Pull, Mathf.Max(0.01f, _duration))
        {
            effectId = "DEV_PULL_MOVING",
            impactLevel = Mathf.Max(0, _impactLevel),
            pullAnchorMode = PullAnchorMode.TargetTransform,
            pullTransform = _movingPullAnchor,
            pullSpeed = Mathf.Max(0.01f, _pullSpeed),
            note = "CCDebugTestPanel moving Pull"
        };
        Stats source = ResolveSourceForTarget(_target);
        EffectApplyResult result = _target.StatusEffects.ApplyCrowdControl(effect, new StatusEffectContext(source, _target, "DEV_PULL_MOVING_SOURCE"));
        _lastResult = $"Pull -> Moving Marker {_movingPullAnchor.position}: {result.status} | {_target.StatusEffects.GetDisplacementDebugSummary()}";
    }

    private void EnsureFixedPullMarker()
    {
        if (_fixedPullMarker != null) return;
        _fixedPullMarker = CreateVisiblePullMarker("[DEV] Static Pull Marker", new Color(1f, 0.75f, 0.1f, 1f));
    }

    private void EnsureMovingPullAnchor()
    {
        if (_movingPullAnchor != null) return;
        _movingPullAnchor = CreateVisiblePullMarker("[DEV] Moving Pull Marker", new Color(0.1f, 0.9f, 1f, 1f));
    }

    private Transform CreateVisiblePullMarker(string markerName, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = markerName;
        go.transform.localScale = Vector3.one * 0.45f;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = color;
        DontDestroyOnLoad(go);
        return go.transform;
    }

    private void MoveDebugPullAnchor()
    {
        EnsureMovingPullAnchor();
        _movingPullAnchor.position += Vector3.right * 2f;
        _lastResult = $"Moving Pull anchor => {_movingPullAnchor.position}";
    }

    private void ApplyNamedCc(CombatEffectType type, string effectId, string sourceKey, float duration)
    {
        ResolveTarget();
        if (_target == null || _target.StatusEffects == null) return;

        var effect = new CombatEffectInfo(type, duration)
        {
            effectId = effectId,
            impactLevel = Mathf.Max(0, _impactLevel),
            stackPolicy = EffectStackPolicy.RefreshSameSource,
            note = "CCDebugTestPanel Phase C"
        };
        Stats source = ResolveSourceForTarget(_target);
        EffectApplyResult result = _target.StatusEffects.ApplyCrowdControl(
            effect, new StatusEffectContext(source, _target, sourceKey));
        _lastResult = $"{effectId}: {result.status} | { _target.StatusEffects.GetManagedDebugSummary()}";
    }

    private void ApplyNamedSlow(string effectId, string sourceKey, float magnitude, float duration, EffectStackPolicy policy, int maxStacks)
    {
        ResolveTarget();
        if (_target == null || _target.StatusEffects == null) return;

        var effect = new CombatEffectInfo(CombatEffectType.Slow, duration)
        {
            effectId = effectId,
            magnitude = Mathf.Clamp01(magnitude),
            impactLevel = Mathf.Max(0, _impactLevel),
            stackPolicy = policy,
            maxStacks = Mathf.Max(1, maxStacks),
            interruptCurrentAction = false,
            note = "CCDebugTestPanel Phase C"
        };

        Stats source = ResolveSourceForTarget(_target);
        EffectApplyResult result = _target.StatusEffects.ApplyCrowdControl(
            effect, new StatusEffectContext(source, _target, sourceKey));
        _lastResult = $"{effectId}: {result.status} | Slow={_target.CurrentSlowAmount:P0} | { _target.StatusEffects.GetManagedDebugSummary()}";
    }

    private void ApplyDebugDot(string effectId, string sourceKey, float damagePerTick, float duration)
    {
        ResolveTarget();
        if (_target == null || _target.StatusEffects == null) return;
        Stats source = ResolveSourceForTarget(_target);

        EffectApplyResult result;
        if (effectId == "Bleed")
            result = _target.ApplyBleed(damagePerTick, duration, source, sourceKey);
        else
            result = _target.ApplyBurn(damagePerTick, duration, source, sourceKey);

        _lastResult = $"{effectId}/{sourceKey}: {result.status} | {_target.StatusEffects.GetManagedDebuffDebugSummary()}";
    }

    private void ApplyDebugAttackSpeedDown(float magnitude, float duration, string sourceKey)
    {
        ResolveTarget();
        if (_target == null || _target.StatusEffects == null) return;
        Stats source = ResolveSourceForTarget(_target);
        EffectApplyResult result = _target.ApplyAttackSpeedDown(
            magnitude, duration, source, sourceKey, "CCDebugTestPanel Phase F2 normalized AttackSpeedDown");
        _lastResult = $"AttackSpeedDown {magnitude:P0}/{sourceKey}: {result.status} | EffectiveAS={_target.EffectiveAttackSpeed:F2} | {_target.StatusEffects.GetManagedDebuffDebugSummary()}";
    }

    private void ApplyDebugFlatStatDebuff(string effectId, RuntimeStatKey stat, float value, float duration)
    {
        ResolveTarget();
        if (_target == null || _target.StatusEffects == null) return;

        var request = new DebuffRequest
        {
            effectId = effectId,
            duration = Mathf.Max(0.01f, duration),
            cleanseable = true,
            stackPolicy = EffectStackPolicy.RefreshSameSource,
            maxStacks = 1,
            potency = Mathf.Abs(value),
            note = "CCDebugTestPanel Phase F2B effective-stat consumer validation only"
        };
        request.statModifiers.Add(new RuntimeStatModifierSpec(stat, RuntimeModifierOperation.Flat, value));

        Stats source = ResolveSourceForTarget(_target);
        EffectApplyResult result = _target.ApplyDebuff(request, source, effectId + "_SOURCE");
        _lastResult = $"{effectId}: {result.status} | Armor={_target.EffectiveArmor:F1} | MR={_target.EffectiveMagicResist:F1} | Def={_target.EffectiveDefenseValue:F1} | DmgOut={_target.EffectiveDamageOutputMultiplier:F2} | DmgTaken={_target.EffectiveDamageTakenMultiplier:F2}";
    }

    private void ProbeCombatMath(bool magic)
    {
        ResolveTarget();
        if (_target == null) return;
        Stats source = ResolveSourceForTarget(_target);
        if (source == null)
        {
            _lastResult = "CombatMath probe: no valid source found";
            return;
        }

        SkillData probeSkill = null;
        if (magic)
        {
            probeSkill = ScriptableObject.CreateInstance<SkillData>();
            probeSkill.skillType = SkillData.SkillType.Skill;
            probeSkill.skillPhysicalMultiplier = 0f;
            probeSkill.skillMagicMultiplier = 1f;
        }

        var result = CombatMath.CalculateFullDamage(
            source, _target, 0f, false, probeSkill, null, 1f, false, 0f);

        if (probeSkill != null) Destroy(probeSkill);
        float total = result.phys + result.magic + result.trueDmg;
        _lastResult = $"{(magic ? "MAGIC" : "PHYSICAL")} front probe | Source={source.name} | Total={total:F2} (P={result.phys:F2}, M={result.magic:F2}) | Armor={_target.EffectiveArmor:F1} | MR={_target.EffectiveMagicResist:F1} | Def={_target.EffectiveDefenseValue:F1}";
    }

    private Stats ResolveSourceForTarget(Stats target)
    {
        if (target == null) return null;

        // E3B/E3C testability: Companion và Dark Demon là Ally, nên Taunt/Fear test phải lấy Enemy làm source
        // để forced basic-attack path phản ánh đúng faction gameplay thật.
        if (target.GetComponent<CompanionAI>() != null || target.GetComponent<DarkDemonAI>() != null)
        {
            Stats enemySource = FindNearestEnemy();
            if (enemySource != null && enemySource != target) return enemySource;
        }

        if (!(target is PlayerStats) && PlayerStats.Current != null) return PlayerStats.Current;
        Stats e = FindNearestEnemy();
        if (e != null && e != target) return e;
        return null;
    }

    private void ToggleCcImmunity()
    {
        if (_target == null || _target.StatusEffects == null) return;
        _panelCcImmune = !_panelCcImmune;
        if (_panelCcImmune) _target.StatusEffects.GrantCrowdControlImmunity(this);
        else _target.StatusEffects.RevokeCrowdControlImmunity(this);
        _lastResult = $"CC Immunity {(_panelCcImmune ? "ON" : "OFF")}";
    }

    private void ToggleSlowImmunity()
    {
        if (_target == null || _target.StatusEffects == null) return;
        _panelSlowImmune = !_panelSlowImmune;
        if (_panelSlowImmune) _target.StatusEffects.GrantSlowImmunity(this);
        else _target.StatusEffects.RevokeSlowImmunity(this);
        _lastResult = $"Slow Immunity {(_panelSlowImmune ? "ON" : "OFF")}";
    }

    private void RevokePanelImmunities()
    {
        if (_target != null && _target.StatusEffects != null)
        {
            if (_panelCcImmune) _target.StatusEffects.RevokeCrowdControlImmunity(this);
            if (_panelSlowImmune) _target.StatusEffects.RevokeSlowImmunity(this);
        }
        _panelCcImmune = false;
        _panelSlowImmune = false;
    }

    private void SetArmorLevel(int level)
    {
        if (_target == null) return;
        _target.isSuperArmor = true;
        _target.superArmorLevel = Mathf.Max(0, level);
        _lastResult = $"Super Armor ON, armorLevel={_target.superArmorLevel}";
    }

    private void SetResistance(float value)
    {
        if (_target == null) return;
        _target.resistanceEffect = Mathf.Clamp01(value);
        _lastResult = $"resistanceEffect={_target.resistanceEffect:P0}";
    }

    private void SetKnockbackResistance(float value)
    {
        if (_target == null) return;
        _target.knockbackResistance = Mathf.Clamp01(value);
        _lastResult = $"knockbackResistance={_target.knockbackResistance:P0}";
    }

    private void TestDamageHitWithStun()
    {
        if (_target == null) return;
        float hpBefore = _target.currentHp;
        float shieldBefore = _target.currentShield;
        Stats source = ResolveSourceForTarget(_target);
        Vector3 sourcePos = source != null ? source.transform.position : _target.transform.position + Vector3.left * 2f;

        var info = new DamageInfo
        {
            attacker = source,
            sourcePosition = sourcePos,
            physDamage = 100f,
            impactLevel = Mathf.Max(0, _impactLevel),
            sourceType = DamageSourceType.Melee
        };
        info.AddEffect(new CombatEffectInfo(CombatEffectType.Stun, 2f)
        {
            effectId = "DEV_TEST_HIT_STUN",
            impactLevel = Mathf.Max(0, _impactLevel),
            sourcePosition = sourcePos
        });
        _target.TakeDamage(info);

        _lastResult = $"Hit100+Stun | Inv={_target.isInvincible} | HP {hpBefore:F1}->{_target.currentHp:F1} | Shield {shieldBefore:F1}->{_target.currentShield:F1} | Stunned={_target.IsStunned}";
        Debug.Log($"[CC TEST] {_lastResult}");
    }

    private void ResetSelectedTargetTestFlags()
    {
        if (_target == null) return;
        RevokePanelImmunities();
        _target.isInvincible = false;
        _target.isSuperArmor = false;
        _target.superArmorLevel = 0;
        _target.resistanceEffect = 0f;
        _target.knockbackResistance = 0f;
        if (_target.Displacement != null && _target.Displacement.IsActive)
            _target.Displacement.ForceRelease(EffectRemoveReason.Manual, true);
        _target.BreakCrowdControl();
        _target.ClearDebuffs();
        _lastResult = "Selected target test flags reset";
    }

    private float FloatField(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(255f));
        string s = GUILayout.TextField(value.ToString("0.##"), GUILayout.Width(120f));
        if (float.TryParse(s, out float parsed)) value = parsed;
        GUILayout.EndHorizontal();
        return value;
    }

    private int IntField(string label, int value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(255f));
        string s = GUILayout.TextField(value.ToString(), GUILayout.Width(120f));
        if (int.TryParse(s, out int parsed)) value = parsed;
        GUILayout.EndHorizontal();
        return value;
    }

    private void StatusLabel(string text)
    {
        GUILayout.Label(text ?? string.Empty, WrappedStatusStyle(), GUILayout.ExpandWidth(true));
        GUILayout.Space(2f);
    }

    private GUIStyle WrappedStatusStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            fixedHeight = 0f,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(2, 4, 2, 2)
        };
    }

    private GUIStyle EditorLikeSubHeader()
    {
        return new GUIStyle(WrappedStatusStyle())
        {
            fontStyle = FontStyle.Bold
        };
    }

    private GUIStyle EditorLikeHeader()
    {
        GUIStyle s = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 17,
            wordWrap = true,
            fixedHeight = 0f,
            margin = new RectOffset(2, 2, 4, 4)
        };
        return s;
    }

    private GUIStyle LargeHeaderStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 20,
            wordWrap = true,
            fixedHeight = 0f,
            margin = new RectOffset(4, 4, 4, 4)
        };
    }

    private GUIStyle QuickStatusStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            wordWrap = true,
            fixedHeight = 0f,
            padding = new RectOffset(4, 4, 2, 2)
        };
    }

    private GUIStyle ResultStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            wordWrap = true,
            fixedHeight = 0f,
            padding = new RectOffset(4, 4, 3, 3)
        };
    }

    private GUIStyle FoldoutHeaderStyle()
    {
        return new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 38f,
            padding = new RectOffset(12, 8, 4, 4)
        };
    }

    private void OnDestroy()
    {
        RevokePanelImmunities();
        RestoreCursorState();
        if (_fixedPullMarker != null) Destroy(_fixedPullMarker.gameObject);
        if (_movingPullAnchor != null) Destroy(_movingPullAnchor.gameObject);
        if (_instance == this) _instance = null;
    }
}
#endif
