using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nháy trắng (hit flash) cho SpriteRenderer khi nhận sát thương — feedback "đã tay".
/// Gắn trên Enemy (root). Tự tìm mọi SpriteRenderer con. An toàn nếu không có renderer.
///
/// Gọi: GetComponent<HitFlash>()?.Flash();  (PlayerController gọi khi đánh trúng)
/// Hoặc tự lắng nghe Stats.OnDamageReceived nếu muốn tự động (xem autoSubscribe).
/// </summary>
public class HitFlash : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;
    [Tooltip("Tự lắng nghe Stats.OnDamageReceived trên cùng GameObject (nếu có Stats).")]
    [SerializeField] private bool autoSubscribe = true;

    private SpriteRenderer[] _renderers;
    private Color[]          _originalColors;
    private Coroutine        _routine;
    private Stats            _stats;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalColors[i] = _renderers[i].color;

        if (_renderers.Length == 0)
            Debug.LogWarning($"[HitFlash] '{gameObject.name}' không có SpriteRenderer con — flash sẽ không hiện gì.");

        // Robust: Stats có thể ở root, parent, hoặc child tùy cách gắn HitFlash.
        _stats = GetComponent<Stats>();
        if (_stats == null) _stats = GetComponentInParent<Stats>();
        if (_stats == null) _stats = GetComponentInChildren<Stats>();

        if (autoSubscribe && _stats == null)
            Debug.LogWarning($"[HitFlash] '{gameObject.name}' bật autoSubscribe nhưng không tìm thấy Stats " +
                             "(root/parent/child) — sẽ không tự flash khi nhận damage. Gọi Flash() thủ công hoặc sửa hierarchy.");
    }

    private void OnEnable()
    {
        if (autoSubscribe && _stats != null)
            _stats.OnDamageReceived += HandleDamageReceived;
    }

    private void OnDisable()
    {
        if (autoSubscribe && _stats != null)
            _stats.OnDamageReceived -= HandleDamageReceived;
    }

    private void HandleDamageReceived(float dmg, Stats src) => Flash();

    /// <summary>Kích hoạt nháy trắng 1 lần.</summary>
    public void Flash()
    {
        if (_renderers == null || _renderers.Length == 0) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].color = flashColor;

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].color = _originalColors[i];

        _routine = null;
    }
}
