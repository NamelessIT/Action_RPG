using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Rendering;

namespace Game.Features.Vision.Systems
{
    /// <summary>
    /// Manages fade effects for objects outside vision range.
    /// Objects gradually become transparent as they move away from player/companion.
    /// [005-A] Full implementation: alpha lerp, distance falloff, coroutine management
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
        [SerializeField] private float _transitionSpeed = 2f; // [005-D] Alpha lerp speed
        [SerializeField] private AnimationCurve _fadeFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0); // [005-B] Fade curve
        [SerializeField] private float _rendererCacheRefreshInterval = 1f;

        private Dictionary<Renderer, float> _targetAlphas = new Dictionary<Renderer, float>();
        private Dictionary<Renderer, Coroutine> _activeCoroutines = new Dictionary<Renderer, Coroutine>();
        private HashSet<Material> _preparedTransparentMaterials = new HashSet<Material>();
        private Dictionary<Material, MaterialState> _materialStates = new Dictionary<Material, MaterialState>();
        private Renderer[] _cachedRenderers = new Renderer[0];
        private float _nextRendererCacheRefreshTime = 0f;
        private float _lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 0.1f; // Throttle updates

        /// <summary>
        /// Update fade effect based on visible objects list and player position.
        /// [005-A] Main entry point - called by PlayerVisionManager when vision updates
        /// </summary>
        public void UpdateFadeEffects(
            List<Collider> visibleObjects,
            Vector3 playerPosition,
            float fadeStartDist,
            float fadeCompleteDist)
        {
            // [005-A] Throttle updates to improve performance
            if (Time.time - _lastUpdateTime < UPDATE_INTERVAL)
                return;

            _lastUpdateTime = Time.time;

            // [005-A] Validate inputs
            if (visibleObjects == null)
                visibleObjects = new List<Collider>();

            RefreshRendererCacheIfNeeded();

            // Build visible roots so multi-renderer prefabs fade as one object.
            var visibleRoots = BuildVisibleRoots(visibleObjects);
            var visibleColliderSet = new HashSet<Collider>(visibleObjects);

            // [006-H] Use cached renderer array to reduce scene scan overhead.
            var allRenderers = _cachedRenderers;

            for (int i = 0; i < allRenderers.Length; i++)
            {
                var renderer = allRenderers[i];

                // [005-A] Skip null or inactive renderers
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                    continue;

                // [005-B] Check if visible
                bool isVisible = IsObjectVisible(renderer, visibleColliderSet, visibleRoots);

                // [005-B] Calculate target alpha based on distance and visibility
                float targetAlpha = CalculateTargetAlpha(
                    renderer.bounds,
                    playerPosition,
                    isVisible,
                    fadeStartDist,
                    fadeCompleteDist
                );

                // [005-D] Apply fade with lerping
                bool alphaChanged = false;
                if (_targetAlphas.ContainsKey(renderer))
                {
                    if (Mathf.Abs(_targetAlphas[renderer] - targetAlpha) > 0.01f)
                    {
                        _targetAlphas[renderer] = targetAlpha;
                        alphaChanged = true;
                    }
                }
                else
                {
                    _targetAlphas[renderer] = targetAlpha;
                    alphaChanged = true;
                }

                // [005-D] Start lerp if alpha changed
                if (alphaChanged)
                {
                    LerpMaterialAlpha(renderer, targetAlpha);
                }
            }
        }

        /// <summary>
        /// Check if a renderer's collider is in visible objects list.
        /// [005-A] Matches renderer to collider safely
        /// </summary>
        private bool IsObjectVisible(Renderer renderer, HashSet<Collider> visibleColliders, HashSet<Transform> visibleRoots)
        {
            if (renderer == null || visibleColliders == null || visibleColliders.Count == 0)
                return false;

            Transform root = GetRootTransform(renderer.transform);
            if (root != null && visibleRoots.Contains(root))
                return true;

            // [005-A] Try to get collider from renderer's gameobject
            Collider collider = renderer.GetComponent<Collider>();
            if (collider != null && visibleColliders.Contains(collider))
                return true;

            // [005-A] Also check parent colliders
            collider = renderer.GetComponentInParent<Collider>();
            if (collider != null && visibleColliders.Contains(collider))
                return true;

            return false;
        }

        private void RefreshRendererCacheIfNeeded()
        {
            if (Time.time < _nextRendererCacheRefreshTime && _cachedRenderers.Length > 0)
                return;

            _cachedRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            _nextRendererCacheRefreshTime = Time.time + Mathf.Max(0.1f, _rendererCacheRefreshInterval);
        }

        private HashSet<Transform> BuildVisibleRoots(List<Collider> visibleObjects)
        {
            var roots = new HashSet<Transform>();
            for (int i = 0; i < visibleObjects.Count; i++)
            {
                Collider col = visibleObjects[i];
                if (col == null)
                    continue;

                Transform root = GetRootTransform(col.transform);
                if (root != null)
                    roots.Add(root);
            }

            return roots;
        }

        private Transform GetRootTransform(Transform t)
        {
            if (t == null)
                return null;

            Rigidbody rb = t.GetComponentInParent<Rigidbody>();
            if (rb != null)
                return rb.transform;

            return t.root;
        }

        /// <summary>
        /// Calculate target alpha based on visibility and distance.
        /// [005-B] Smooth falloff between fadeStartDistance and fadeCompleteDistance
        /// </summary>
        private float CalculateTargetAlpha(
            Bounds objectBounds,
            Vector3 playerPos,
            bool isVisible,
            float fadeStartDist,
            float fadeCompleteDist)
        {
            // [005-B] If visible, full alpha
            if (isVisible)
                return 1f;

            // [005-B] Calculate distance from player to nearest bounds point.
            float distance = Vector3.Distance(objectBounds.ClosestPoint(playerPos), playerPos);

            // [005-B] If inside fade start distance, full alpha
            if (distance < fadeStartDist)
                return 1f;

            // [005-B] If beyond fade complete distance, zero alpha
            if (distance > fadeCompleteDist)
                return 0f;

            // [005-B] Linear falloff between with animation curve
            float normalizedDist = (distance - fadeStartDist) / (fadeCompleteDist - fadeStartDist);
            float curveValue = _fadeFalloff.Evaluate(normalizedDist);
            return Mathf.Clamp01(1f - curveValue);
        }

        /// <summary>
        /// Start lerping material alpha to target value.
        /// [005-D] Manages coroutine lifecycle
        /// </summary>
        private void LerpMaterialAlpha(Renderer renderer, float targetAlpha)
        {
            if (renderer == null)
                return;

            // [005-D] Stop existing coroutine if running
            if (_activeCoroutines.ContainsKey(renderer))
            {
                Coroutine existingCoroutine = _activeCoroutines[renderer];
                if (existingCoroutine != null)
                {
                    StopCoroutine(existingCoroutine);
                }
                _activeCoroutines.Remove(renderer);
            }

            // [005-D] Start new lerp coroutine
            var coroutine = StartCoroutine(LerpAlphaCoroutine(renderer, targetAlpha));
            _activeCoroutines[renderer] = coroutine;
        }

        /// <summary>
        /// Coroutine to smoothly lerp material alpha over time.
        /// [005-D] Full implementation with frame-based lerping
        /// </summary>
        private IEnumerator LerpAlphaCoroutine(Renderer renderer, float targetAlpha)
        {
            if (renderer == null)
                yield break;

            // [005-D] Lerp until close enough to target
            while (renderer != null)
            {
                float currentAlpha = GetMaterialAlpha(renderer);
                float alphaDiff = Mathf.Abs(currentAlpha - targetAlpha);

                // [005-D] Stop when close enough
                if (alphaDiff < 0.01f)
                {
                    SetMaterialAlpha(renderer, targetAlpha);
                    break;
                }

                // [005-D] Smooth lerp to target alpha using Time.deltaTime
                float newAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * _transitionSpeed);
                SetMaterialAlpha(renderer, newAlpha);
                yield return null;
            }

            // [005-D] Cleanup coroutine reference
            if (_activeCoroutines.ContainsKey(renderer))
            {
                _activeCoroutines.Remove(renderer);
            }
        }

        /// <summary>
        /// Get current alpha value from renderer material.
        /// [005-C] Safe property access
        /// </summary>
        private float GetMaterialAlpha(Renderer renderer)
        {
            if (renderer == null || renderer.material == null)
                return 1f;

            Material mat = renderer.material;

            // URP/HDRP Lit and many custom shaders
            if (mat.HasProperty("_BaseColor"))
            {
                return mat.GetColor("_BaseColor").a;
            }

            // [005-C] Check for _Color property
            if (mat.HasProperty("_Color"))
            {
                return mat.color.a;
            }

            return 1f;
        }

        /// <summary>
        /// Set alpha value on renderer material.
        /// [005-C] Implementation: modifies _Color.a property
        /// </summary>
        private void SetMaterialAlpha(Renderer renderer, float alpha)
        {
            if (renderer == null || renderer.material == null)
                return;

            float clampedAlpha = Mathf.Clamp01(alpha);
            Material mat = renderer.material;

            if (clampedAlpha < 0.99f)
            {
                PrepareMaterialForTransparency(mat);
            }
            else
            {
                RestoreOpaqueMaterial(mat);
            }

            // URP/HDRP Lit and many custom shaders
            if (mat.HasProperty("_BaseColor"))
            {
                Color baseColor = mat.GetColor("_BaseColor");
                baseColor.a = clampedAlpha;
                mat.SetColor("_BaseColor", baseColor);
            }

            // [005-C] Modify _Color.a property
            if (mat.HasProperty("_Color"))
            {
                Color color = mat.color;
                color.a = clampedAlpha;
                mat.color = color;
            }

            // Keep objects loaded but hidden visually when fully faded.
            renderer.enabled = clampedAlpha > 0.02f;
        }

        private void PrepareMaterialForTransparency(Material mat)
        {
            if (mat == null)
                return;

            if (!_materialStates.ContainsKey(mat))
            {
                var state = new MaterialState
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
                _materialStates[mat] = state;
            }

            if (_preparedTransparentMaterials.Contains(mat))
                return;

            // URP/HDRP style surface controls
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);

            // Built-in Standard shader style mode controls
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
        /// Clear all stored alpha states and stop coroutines.
        /// [005-A] Cleanup method for scene transitions
        /// </summary>
        public void ClearFadeState()
        {
            // [005-A] Stop all active coroutines
            var coroutinesArray = new List<Coroutine>(_activeCoroutines.Values);
            foreach (var coroutine in coroutinesArray)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }

            _targetAlphas.Clear();
            _activeCoroutines.Clear();
            _preparedTransparentMaterials.Clear();
            _materialStates.Clear();
            Debug.Log("[005-A] FadeEffectManager: Cleared all fade states.");
        }

        /// <summary>
        /// OnDestroy cleanup.
        /// [005-A] Ensure coroutines are stopped
        /// </summary>
        private void OnDestroy()
        {
            ClearFadeState();
        }
    }
}
