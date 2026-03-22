using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace Game.Features.Vision.Systems
{
    /// <summary>
    /// Manages fade effects for objects outside vision range.
    /// Objects gradually become transparent as they move away from player/companion.
    /// [005-A] Full implementation: alpha lerp, distance falloff, coroutine management
    /// </summary>
    public class FadeEffectManager : MonoBehaviour
    {
        [Header("Fade Parameters")]
        [SerializeField] private float _transitionSpeed = 2f; // [005-D] Alpha lerp speed
        [SerializeField] private AnimationCurve _fadeFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0); // [005-B] Fade curve

        private Dictionary<Renderer, float> _targetAlphas = new Dictionary<Renderer, float>();
        private Dictionary<Renderer, Coroutine> _activeCoroutines = new Dictionary<Renderer, Coroutine>();
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

            // [005-A] Get all objects in scene with renderers
            var allRenderers = FindObjectsOfType<Renderer>();

            for (int i = 0; i < allRenderers.Length; i++)
            {
                var renderer = allRenderers[i];

                // [005-A] Skip null or inactive renderers
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                    continue;

                // [005-B] Check if visible
                bool isVisible = IsObjectVisible(renderer, visibleObjects);

                // [005-B] Calculate target alpha based on distance and visibility
                float targetAlpha = CalculateTargetAlpha(
                    renderer.transform.position,
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
        private bool IsObjectVisible(Renderer renderer, List<Collider> visibleObjects)
        {
            if (renderer == null || visibleObjects == null || visibleObjects.Count == 0)
                return false;

            // [005-A] Try to get collider from renderer's gameobject
            Collider collider = renderer.GetComponent<Collider>();
            if (collider != null && visibleObjects.Contains(collider))
                return true;

            // [005-A] Also check parent colliders
            collider = renderer.GetComponentInParent<Collider>();
            if (collider != null && visibleObjects.Contains(collider))
                return true;

            return false;
        }

        /// <summary>
        /// Calculate target alpha based on visibility and distance.
        /// [005-B] Smooth falloff between fadeStartDistance and fadeCompleteDistance
        /// </summary>
        private float CalculateTargetAlpha(
            Vector3 objectPos,
            Vector3 playerPos,
            bool isVisible,
            float fadeStartDist,
            float fadeCompleteDist)
        {
            // [005-B] If visible, full alpha
            if (isVisible)
                return 1f;

            // [005-B] Calculate distance
            float distance = Vector3.Distance(objectPos, playerPos);

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

            // [005-C] Check for _Color property
            if (renderer.material.HasProperty("_Color"))
            {
                return renderer.material.color.a;
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

            // [005-C] Modify _Color.a property
            if (renderer.material.HasProperty("_Color"))
            {
                Color color = renderer.material.color;
                color.a = Mathf.Clamp01(alpha);
                renderer.material.color = color;
            }
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
