using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Popup số sát thương dùng TextMeshPro 3D (KHÔNG dùng Canvas).
/// Render trực tiếp trong world space, tương thích mọi kiểu camera.
///
/// Prefab cần có:
///   - Root: GameObject rỗng + component này
///   - Child "Template": TextMeshPro (3D, KHÔNG phải UGUI) đặt SetActive(false)
/// </summary>
public class DamageNumberPopup : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────────────────────
    [Header("Template (TextMeshPro 3D — không phải UGUI)")]
    [SerializeField] private TextMeshPro _tmpTemplate;

    [Header("Animation")]
    [SerializeField] private float _floatSpeed    = 2.0f;
    [SerializeField] private float _lifetime      = 1.2f;
    [SerializeField] private float _fadeStartTime = 0.5f;

    [Header("Layout")]
    [SerializeField] private float _lineSpacing  = 0.4f;   // khoảng cách dọc giữa các dòng (world units)
    [SerializeField] private float _spawnOffsetY = 1.5f;   // offset Y so với chân target

    [Header("Font Size")]
    [SerializeField] private float _normalSize = 4f;
    [SerializeField] private float _critSize   = 6f;

    // ─────────────────────────────────────────────────────────────
    //  COLORS
    // ─────────────────────────────────────────────────────────────
    private static readonly Color ColPhys  = new Color(1.0f, 0.55f, 0.1f);  // Cam vàng
    private static readonly Color ColMagic = new Color(0.75f, 0.2f, 1.0f); // Tím
    private static readonly Color ColTrue  = Color.white;                    // Trắng
    private static readonly Color ColHeal  = new Color(0.2f, 0.9f, 0.2f);  // Xanh lá

    // ─────────────────────────────────────────────────────────────
    //  RUNTIME
    // ─────────────────────────────────────────────────────────────
    private readonly List<TextMeshPro> _lines = new List<TextMeshPro>();
    private Camera _cam;
    private float  _timer;
    private bool   _active;

    // ─────────────────────────────────────────────────────────────
    //  UNITY
    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        _cam = Camera.main;
        if (_tmpTemplate != null) _tmpTemplate.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!_active) return;

        _timer += Time.deltaTime;

        // Nổi lên
        transform.position += Vector3.up * _floatSpeed * Time.deltaTime;

        // Billboard: luôn quay mặt về phía camera
        if (_cam != null)
            transform.rotation = _cam.transform.rotation;

        // Fade out
        if (_timer >= _fadeStartTime)
        {
            float alpha = 1f - Mathf.Clamp01(
                (_timer - _fadeStartTime) / (_lifetime - _fadeStartTime));
            foreach (var line in _lines) line.alpha = alpha;
        }

        // Trả về pool khi hết vòng đời
        if (_timer >= _lifetime)
            DamageNumberManager.Instance?.ReturnToPool(this);
    }

    // ─────────────────────────────────────────────────────────────
    //  PUBLIC
    // ─────────────────────────────────────────────────────────────
    public void Show(DamageInfo info, Vector3 worldPos)
    {
        // Refresh camera (đề phòng Camera.main thay đổi)
        if (_cam == null) _cam = Camera.main;

        transform.position = worldPos + Vector3.up * _spawnOffsetY;
        _timer  = 0f;
        _active = true;
        gameObject.SetActive(true);

        ClearLines();

        int idx = 0;
        if (info.physDamage  > 0f) SpawnLine(info.physDamage,  info.isCrit, ColPhys,  idx++);
        if (info.magicDamage > 0f) SpawnLine(info.magicDamage, info.isCrit, ColMagic, idx++);
        if (info.trueDamage  > 0f) SpawnLine(info.trueDamage,  info.isCrit, ColTrue,  idx++);

        //if (idx == 0) SpawnLine(0f, false, Color.gray, 0, "BLOCKED");
    }

    public void ShowHeal(float amount, Vector3 worldPos)
    {
        if (_cam == null) _cam = Camera.main;

        transform.position = worldPos + Vector3.up * _spawnOffsetY;
        _timer  = 0f;
        _active = true;
        gameObject.SetActive(true);

        ClearLines();
        SpawnLine(amount, false, ColHeal, 0, $"+{Mathf.RoundToInt(amount)}");
    }

    public void ResetPopup()
    {
        _active = false;
        gameObject.SetActive(false);
        ClearLines();
    }

    // ─────────────────────────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────────────────────────
    private void SpawnLine(float damage, bool isCrit, Color color, int index,
                           string overrideText = null)
    {
        if (_tmpTemplate == null)
        {
            Debug.LogError("[DamageNumberPopup] _tmpTemplate chưa gán! Kéo child TextMeshPro vào Inspector.");
            return;
        }

        TextMeshPro line = Instantiate(_tmpTemplate, transform);
        line.gameObject.SetActive(true);

        line.alpha    = 1f;
        // Crit: tăng sáng màu gốc để nổi bật (lerp về trắng 35%) + scale to hơn
        line.color    = isCrit ? Color.Lerp(color, Color.white, 0.35f) : color;
        line.fontSize = isCrit ? _critSize : _normalSize;

        // Text content
        if (overrideText != null)
        {
            line.text = overrideText;
        }
        else
        {
            string num = Mathf.RoundToInt(damage).ToString();
            // Dùng "!" thay ký tự ⚡ để tránh warning missing-glyph của LiberationSans.
            line.text = isCrit ? $"<b>{num}!</b>" : num;
        }

        // Xếp dọc: dòng đầu cao nhất, dòng sau thấp hơn
        line.transform.localPosition = Vector3.up * (-index * _lineSpacing);
        line.transform.localRotation = Quaternion.identity;

        _lines.Add(line);
    }

    private void ClearLines()
    {
        foreach (var l in _lines)
            if (l != null) Destroy(l.gameObject);
        _lines.Clear();
    }
}
