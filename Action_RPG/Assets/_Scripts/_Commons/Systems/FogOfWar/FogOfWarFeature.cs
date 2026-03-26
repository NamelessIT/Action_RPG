using UnityEngine;
using UnityEngine.Rendering.Universal;
using Game.Features.Vision.Data;
using Game.Features.Vision.Core;

namespace Game.Features.Vision.Rendering
{
    /// <summary>
    /// [008-D] ScriptableRendererFeature that adds fog of war post-processing to URP pipeline.
    /// Add this feature to your renderer asset (Inspector: Add Renderer Feature → FogOfWar)
    /// </summary>
    public class FogOfWarFeature : ScriptableRendererFeature
    {
        [SerializeField] private VisionConfig _visionConfig;
        
        private FogOfWarPass _pass;
        private Transform _playerTransform;
        private Transform _companionTransform;

        /// <summary>
        /// Set vision source transforms (player + companion).
        /// Called by PlayerVisionManager to provide position data for fog shader.
        /// </summary>
        public void SetVisionSources(Transform playerTransform, Transform companionTransform = null)
        {
            _playerTransform = playerTransform;
            _companionTransform = companionTransform;
        }

        /// <summary>
        /// Initialize feature and create pass.
        /// </summary>
        public override void Create()
        {
            if (_visionConfig == null)
            {
                Debug.LogWarning("[008-D] FogOfWarFeature: VisionConfig not assigned in Inspector!");
                return;
            }

            // [008-D] Create pass (vision sources can be set later)
            _pass = new FogOfWarPass(_visionConfig);
        }

        /// <summary>
        /// Add pass to renderer pipeline.
        /// Called by ScriptableRenderer.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || !_visionConfig.EnableFogOfWar)
                return;

            // [008-D] Update vision sources in pass every frame
            if (_playerTransform != null && _pass != null)
            {
                Vector3[] sources = _companionTransform != null
                    ? new Vector3[] { _playerTransform.position, _companionTransform.position }
                    : new Vector3[] { _playerTransform.position, _playerTransform.position };
                
                Vector2 ranges = new Vector2(_visionConfig.PlayerVisionRange, _visionConfig.CompanionVisionRange);
                _pass.SetVisionSources(sources, ranges);
            }

            // [008-D] Enqueue pass
            renderer.EnqueuePass(_pass);
        }

        /// <summary>
        /// Cleanup feature resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pass?.Cleanup();
            }
        }
    }
}
