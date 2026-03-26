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
        private VisionCoordinator _visionCoordinator;

        /// <summary>
        /// Optional: Set vision coordinator reference (called externally).
        /// If not set, pass will try to find at runtime.
        /// </summary>
        public void SetVisionCoordinator(VisionCoordinator coordinator)
        {
            _visionCoordinator = coordinator;
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

            // [008-D] Create pass (coordinator can be found/injected later)
            _pass = new FogOfWarPass(_visionConfig, _visionCoordinator);
        }

        /// <summary>
        /// Add pass to renderer pipeline.
        /// Called by ScriptableRenderer.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || !_visionConfig.EnableFogOfWar)
                return;

            // [008-D] Lazy-load vision coordinator if not set
            if (_visionCoordinator == null)
            {
                _visionCoordinator = FindObjectOfType<VisionCoordinator>();
                if (_visionCoordinator == null)
                {
                    Debug.LogWarning("[008-D] FogOfWarFeature: VisionCoordinator not found in scene!");
                    return;
                }
                _pass = new FogOfWarPass(_visionConfig, _visionCoordinator);
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
