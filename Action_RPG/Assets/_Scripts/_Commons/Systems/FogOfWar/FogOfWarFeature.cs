using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Features.Vision.Rendering
{
    /// <summary>
    /// [008-D] ScriptableRendererFeature that adds fog of war to URP pipeline.
    /// Add to your renderer asset: Inspector → Add Renderer Feature → FogOfWar.
    /// Shader globals (_FoW_*) are set by FogOfWarController MonoBehaviour at runtime.
    /// </summary>
    public class FogOfWarFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _fogShader;

        private Material _material;
        private FogOfWarPass _pass;

        /// <summary>
        /// [008-D] Create pass and material from shader.
        /// </summary>
        public override void Create()
        {
            if (_fogShader == null)
                _fogShader = Shader.Find("Game/Vision/FogOfWar");

            if (_fogShader == null)
            {
                Debug.LogWarning("[008-D] FogOfWarFeature: shader 'Game/Vision/FogOfWar' not found.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_fogShader);
            _pass = new FogOfWarPass(_material);
        }

        /// <summary>
        /// [008-D] Enqueue fog pass into renderer pipeline.
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null)
                return;

            // [008-D] Request depth texture for world position reconstruction
            renderer.EnqueuePass(_pass);
        }

        /// <summary>
        /// [008-D] Cleanup material and pass.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_material);
        }
    }
}
