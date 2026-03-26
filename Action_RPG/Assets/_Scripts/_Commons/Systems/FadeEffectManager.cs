using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace Game.Features.Vision.Systems
{
    /// <summary>
    /// Manages fade effects for objects outside vision range.
    /// Objects gradually become transparent as they move away from player/companion.
    /// Uses per-frame lerp (no coroutines) for smooth, flicker-free transitions.
    /// </summary>
    public class FadeEffectManager : MonoBehaviour
    {
        private struct MaterialState
        {
            public int SrcBlend;
            public int DstBlend;
            public int ZWrite;
            public int RenderQueue;
            public float Surface;
            public float Mode;
            public bool HasSurface;
            public bool HasMode;
        }

        [Header("Fade Parameters")]
        [Tooltip("Higher = faster fade transition. 5-8 is smooth, 10+ is snappy.")]
        [SerializeField] private float _transitionSpeed = 5f;
        [SerializeField] private AnimationCurve _fadeFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [SerializeField] private float _rendererCacheRefreshInterval = 1f;

        // Per-renderer alpha tracking — replaces coroutine system
        private Dictionary<Renderer, float> _targetAlphas = new Dictionary<Renderer, float>();
        private Dictionary<Renderer, float> _currentAlphas = new Dictionary<Renderer, float>();

        // Material state management
        private HashSet<Material> _preparedTransparentMaterials = new HashSet<Material>();
        private Dictionary<Material, MaterialState> _materialStates = new Dictionary<Material, MaterialState>();

        // [007-C] Transforms that should never be faded (player, companion, etc.)
        private HashSet<Transform> _excludedTransforms = new HashSet<Transform>();

        // Renderer cache
        private Renderer[] _cachedRenderers = new Renderer[0];
        private float _nextRendererCacheRefreshTime = 0f;

        // Vision evaluation throttle — targets evaluated every interval, lerp runs every frame
        private float _lastEvaluationTime = 0f;
        private const float EVALUATION_INTERVAL = 0.15f;

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
            Debug.Log($"[009-A] FadeEffectManager set vision sources: {sources.Length} sources");
        }

        /// <summary>
        /// [009-A] Set fade distances from VisionConfig. Called once during init.
        /// </summary>
        public void SetFadeDistances(float fadeStart, float fadeComplete)
        {
            _fadeStartDist = fadeStart;
            _fadeCompleteDist = fadeComplete;
            Debug.Log($"[009-A] FadeEffectManager fade distances set: start={fadeStart}, complete={fadeComplete}");
        }

        /// <summary>
        /// Per-frame smooth alpha lerp. Runs every frame for flicker-free transitions.
        /// [009-B] Refactored: now splits evaluation and lerp phases
        /// </summary>
        private void Update()
        {
            // [009-B] Check if vision sources are configured
            if (_visionSourceTransforms == null || _visionSourceTransforms.Length == 0)
                return;

            // [009-B] Phase 1: Evaluate targets periodically (not every frame)
            if (Time.time - _lastEvaluationTime >= EVALUATION_INTERVAL)
            {
                _lastEvaluationTime = Time.time;
                RefreshRendererCacheIfNeeded();
                EvaluateAllRenderers();
            }

            // [009-B] Phase 2: Smooth lerp every frame (flicker-free)
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

            Renderer[] allRenderers = _cachedRenderers;

            for (int i = 0; i < allRenderers.Length; i++)
            {
                var renderer = allRenderers[i];
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                    continue;

                // [007-C] Skip excluded transforms — always fully visible
                if (IsExcludedTransform(renderer.transform))
                {
                    SetTargetAlpha(renderer, 1f);
                    continue;
                }

                // [009-D] Calculate target alpha PURELY based on distance (no isVisible parameter)
                float targetAlpha = CalculateTargetAlpha(
                    renderer.bounds,
                    sourcePositions,
                    _fadeStartDist,
                    _fadeCompleteDist
                );
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

        /// <summary>
        /// Set target alpha for a renderer. The Update() loop handles smooth lerping.
        /// </summary>
        private void SetTargetAlpha(Renderer renderer, float target)
        {
            if (!_targetAlphas.ContainsKey(renderer))
            {
                // First time seeing this renderer — initialize current alpha
                _currentAlphas[renderer] = GetMaterialAlpha(renderer);
            }
            _targetAlphas[renderer] = target;
        }

        /// <summary>
        /// Apply alpha to renderer material. Handles mode switching with hysteresis.
        /// </summary>
        private void ApplyAlphaToRenderer(Renderer renderer, float alpha)
        {
            if (renderer == null) return;

            Material mat = renderer.material;
            if (mat == null) return;

            float clamped = Mathf.Clamp01(alpha);

            // Hysteresis: only switch to transparent when clearly fading (< 0.95)
            // Only restore opaque when fully opaque (> 0.995)
            if (clamped < 0.95f)
            {
                PrepareMaterialForTransparency(mat);
            }
            else if (clamped > 0.995f)
            {
                RestoreOpaqueMaterial(mat);
                clamped = 1f;
            }

            // Apply alpha to shader properties
            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = clamped;
                mat.SetColor("_BaseColor", c);
            }
            if (mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                c.a = clamped;
                mat.color = c;
            }

            // Only disable renderer when truly invisible (sustained alpha ≈ 0)
            renderer.enabled = clamped > 0.005f;
        }

        private void RefreshRendererCacheIfNeeded()
        {
            if (Time.time < _nextRendererCacheRefreshTime && _cachedRenderers.Length > 0)
                return;

            _cachedRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            _nextRendererCacheRefreshTime = Time.time + Mathf.Max(0.1f, _rendererCacheRefreshInterval);
        }

        /// <summary>
        /// Check if a transform or any of its parents is in the excluded set.
        /// [007-C] Prevents fading player/companion objects.
        /// </summary>
        private bool IsExcludedTransform(Transform t)
        {
            if (t == null || _excludedTransforms.Count == 0)
                return false;

            if (_excludedTransforms.Contains(t))
                return true;

            Transform current = t.parent;
            while (current != null)
            {
                if (_excludedTransforms.Contains(current))
                    return true;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Calculate target alpha based PURELY on distance to nearest vision source.
        /// [009-D] No OverlapSphere dependency → no binary flip → no flickering.
        /// No isVisible parameter — solely distance-based.
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

        private float GetMaterialAlpha(Renderer renderer)
        {
            if (renderer == null || renderer.material == null)
                return 1f;

            Material mat = renderer.material;

            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor").a;

            if (mat.HasProperty("_Color"))
                return mat.color.a;

            return 1f;
        }

        private void PrepareMaterialForTransparency(Material mat)
        {
            if (mat == null) return;

            if (!_materialStates.ContainsKey(mat))
            {
                _materialStates[mat] = new MaterialState
                {
                    SrcBlend = mat.HasProperty("_SrcBlend") ? mat.GetInt("_SrcBlend") : (int)BlendMode.One,
                    DstBlend = mat.HasProperty("_DstBlend") ? mat.GetInt("_DstBlend") : (int)BlendMode.Zero,
                    ZWrite = mat.HasProperty("_ZWrite") ? mat.GetInt("_ZWrite") : 1,
                    RenderQueue = mat.renderQueue,
                    HasSurface = mat.HasProperty("_Surface"),
                    HasMode = mat.HasProperty("_Mode"),
                    Surface = mat.HasProperty("_Surface") ? mat.GetFloat("_Surface") : 0f,
                    Mode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0f
                };
            }

            if (_preparedTransparentMaterials.Contains(mat))
                return;

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 2f);

            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;

            _preparedTransparentMaterials.Add(mat);
        }

        private void RestoreOpaqueMaterial(Material mat)
        {
            if (mat == null || !_preparedTransparentMaterials.Contains(mat))
                return;

            if (_materialStates.TryGetValue(mat, out MaterialState state))
            {
                if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", state.SrcBlend);
                if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", state.DstBlend);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", state.ZWrite);
                if (state.HasSurface && mat.HasProperty("_Surface")) mat.SetFloat("_Surface", state.Surface);
                if (state.HasMode && mat.HasProperty("_Mode")) mat.SetFloat("_Mode", state.Mode);
                mat.renderQueue = state.RenderQueue;
            }

            _preparedTransparentMaterials.Remove(mat);
        }

        /// <summary>
        /// Set transforms that should never be faded (e.g. player, companion).
        /// [007-C] Excluded transforms always keep alpha=1.
        /// </summary>
        public void SetExcludedTransforms(params Transform[] transforms)
        {
            _excludedTransforms.Clear();
            if (transforms == null) return;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                    _excludedTransforms.Add(transforms[i]);
            }
        }

        /// <summary>
        /// Clear all stored alpha states.
        /// </summary>
        public void ClearFadeState()
        {
            _targetAlphas.Clear();
            _currentAlphas.Clear();
            _preparedTransparentMaterials.Clear();
            _materialStates.Clear();
        }

        private void OnDestroy()
        {
            ClearFadeState();
        }
    }
}
