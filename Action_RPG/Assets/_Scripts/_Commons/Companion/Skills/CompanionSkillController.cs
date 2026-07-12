using UnityEngine;

/// <summary>
/// Bộ điều phối Kỹ năng của Companion: chọn nguyên mẫu (archetype), quản lý Sin/cooldown,
/// và nhận lệnh CAST từ Player (phím R = Skill, T = Signature).
/// Gắn cùng GameObject với CompanionAI + AllyStats + CompanionEquipmentManager.
///
/// • Archetype lưu PlayerPrefs ("CompanionArchetype"); đổi lúc runtime qua SetArchetype (DevTool).
/// • Sin: maxSin = phí Signature của nguyên mẫu; +SIN_PER_HIT mỗi khi Companion đánh trúng.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AllyStats))]
[RequireComponent(typeof(CompanionAI))]
public class CompanionSkillController : MonoBehaviour
{
    public const string PREF_KEY = "CompanionArchetype";

    private AllyStats stats;
    private CompanionEquipmentManager equip;
    private CompanionSkillBehavior currentBehavior;
    public CompanionSkillBehavior CurrentBehavior => currentBehavior;

    private float skillReadyTime = -999f;
    private float sigReadyTime   = -999f;

    public CompanionArchetype Archetype { get; private set; } = CompanionArchetype.Debuffer;

    /// <summary>ACC_CH_T5_06: ghi đè giới hạn Sin (0 = dùng phí Signature mặc định).</summary>
    public float maxSinOverride = 0f;
    /// <summary>Bắn khi Companion DÙNG Skill thành công (cho ACC_RM_T5_07: companion skill → buff player).</summary>
    public static event System.Action OnCompanionSkillUsed;
    /// <summary>Bắn khi Companion DÙNG Signature thành công (cho PRT_SUP_T5_01).</summary>
    public static event System.Action OnCompanionSignatureUsed;

    // ── Đọc cho HUD ──
    public float SkillCdRemaining     => currentBehavior == null ? 0f : Mathf.Max(0f, skillReadyTime - Time.time);
    public float SignatureCdRemaining => currentBehavior == null ? 0f : Mathf.Max(0f, sigReadyTime - Time.time);
    public float SkillCdNormalized     => currentBehavior == null ? 0f : Mathf.Clamp01(SkillCdRemaining / Mathf.Max(0.01f, currentBehavior.SkillCooldown));
    public float SignatureCdNormalized => currentBehavior == null ? 0f : Mathf.Clamp01(SignatureCdRemaining / Mathf.Max(0.01f, currentBehavior.SignatureCooldown));
    public bool  SignatureSinReady     => currentBehavior != null && stats != null && stats.currentSin >= currentBehavior.SignatureSinCost;

    private void Awake()
    {
        stats = GetComponent<AllyStats>();
        equip = GetComponent<CompanionEquipmentManager>();
    }

    private void OnEnable()  { if (stats != null) stats.OnHitEnemy += HandleCompanionHit; }
    private void OnDisable() { if (stats != null) stats.OnHitEnemy -= HandleCompanionHit; }

    private void Start()
    {
        int saved = PlayerPrefs.GetInt(PREF_KEY, (int)CompanionArchetype.Debuffer);
        SetArchetype((CompanionArchetype)saved, save: false);
    }

    private void Update()
    {
        // maxSin = phí Signature của nguyên mẫu (RecalculateStats hay đặt maxSin=40 → ép lại).
        if (currentBehavior != null)
        {
            float want = maxSinOverride > 0f ? maxSinOverride : currentBehavior.SignatureSinCost;
            if (!Mathf.Approximately(stats.maxSin, want))
            {
                stats.SetMaxSin(want);
                if (stats.currentSin > stats.maxSin) stats.currentSin = stats.maxSin;
            }
        }
    }

    // ── Sin gain (giống Player: dùng công thức SinGain của AllyStats) ──
    private void HandleCompanionHit(Stats victim, float t, bool isCrit)
    {
        if (currentBehavior == null) return;
        stats.GainSinFromAttack(1);
    }

    // ── Player ra lệnh ──
    private CompanionProtocolType? CurrentProtocol()
        => (equip != null && equip.Protocol != null) ? equip.Protocol.protocolType : (CompanionProtocolType?)null;

    /// <summary>Player bấm phím Skill (R).</summary>
    public void CommandSkill()
    {
        if (currentBehavior == null || stats.isDead) return;
        if (Time.time < skillReadyTime) { Debug.Log("[CompanionSkill] Skill đang hồi."); return; }
        skillReadyTime = Time.time + currentBehavior.SkillCooldown;
        currentBehavior.ExecuteSkill(CurrentProtocol());
        OnCompanionSkillUsed?.Invoke(); // ACC_RM_T5_07
        Debug.Log($"<color=cyan>[Companion]</color> Skill ({currentBehavior.Archetype} / {(CurrentProtocol()?.ToString() ?? "Basic")})");
    }

    /// <summary>Player bấm phím Signature (T).</summary>
    public void CommandSignature()
    {
        if (currentBehavior == null || stats.isDead) return;
        if (Time.time < sigReadyTime) { Debug.Log("[CompanionSkill] Signature đang hồi."); return; }
        if (stats.currentSin < currentBehavior.SignatureSinCost) { Debug.Log("[CompanionSkill] Không đủ Sin."); return; }
        stats.currentSin -= currentBehavior.SignatureSinCost;
        sigReadyTime = Time.time + currentBehavior.SignatureCooldown;
        currentBehavior.ExecuteSignature(CurrentProtocol());
        OnCompanionSignatureUsed?.Invoke(); // PRT_SUP_T5_01
        Debug.Log($"<color=magenta>[Companion]</color> Signature ({currentBehavior.Archetype} / {(CurrentProtocol()?.ToString() ?? "Basic")})");
    }

    // ── Đổi nguyên mẫu ──
    public void SetArchetype(CompanionArchetype a, bool save = true)
    {
        if (currentBehavior != null)
        {
            Destroy(currentBehavior);
            currentBehavior = null;
        }

        System.Type t = System.Type.GetType(ClassNameFor(a));
        if (t == null)
        {
            Debug.LogWarning($"[CompanionSkill] Chưa có lớp behavior cho {a} ({ClassNameFor(a)}).");
            return;
        }
        currentBehavior = (CompanionSkillBehavior)gameObject.AddComponent(t);
        Archetype = a;
        skillReadyTime = -999f;
        sigReadyTime   = -999f;
        if (currentBehavior != null) { stats.SetMaxSin(currentBehavior.SignatureSinCost); stats.currentSin = 0f; }

        if (save) { PlayerPrefs.SetInt(PREF_KEY, (int)a); PlayerPrefs.Save(); }
        Debug.Log($"<color=cyan>[Companion]</color> Đổi nguyên mẫu → {a}");
    }

    private static string ClassNameFor(CompanionArchetype a)
    {
        switch (a)
        {
            case CompanionArchetype.Debuffer:     return "CompanionDebufferBehavior";
            case CompanionArchetype.Sustain:      return "CompanionSustainBehavior";
            case CompanionArchetype.Controller:   return "CompanionControllerBehavior";
            case CompanionArchetype.Buffer:       return "CompanionBufferBehavior";
            case CompanionArchetype.DamageDealer: return "CompanionDamageDealerBehavior";
            default: return "CompanionDebufferBehavior";
        }
    }
}
