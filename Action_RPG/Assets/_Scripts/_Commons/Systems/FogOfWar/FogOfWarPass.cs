using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Game.Features.Vision.Data;
using Game.Features.Vision.Interfaces;

namespace Game.Features.Vision.Rendering
{
    /// <summary>
    /// [008-C] ScriptableRenderPass for fog of war post-processing.
    /// Executes after opaque/transparent rendering to overlay fog effect.
    /// Reads depth texture and reconstructs world positions for vision checks.
    /// </summary>
    public class FogOfWarPass : ScriptableRenderPass
    {
        private static readonly int _VisionSources = Shader.PropertyToID("_VisionSources");
        private static readonly int _VisionRanges = Shader.PropertyToID("_VisionRanges");
        private static readonly int _FogColor = Shader.PropertyToID("_FogColor");
        private static readonly int _FogEdgeSoftness = Shader.PropertyToID("_FogEdgeSoftness");
        private static readonly int _DepthTexture = Shader.PropertyToID("_CameraDepthTexture");

        private Material _fogMaterial;
        private VisionConfig _visionConfig;
        private IVisionCoordinator _visionCoordinator;
        private RenderTextureDescriptor _descriptor;

        public FogOfWarPass(VisionConfig config, IVisionCoordinator coordinator)
        {
            _visionConfig = config ?? throw new System.ArgumentNullException(nameof(config));
            _visionCoordinator = coordinator ?? throw new System.ArgumentNullException(nameof(coordinator));

            // Create material from shader
            Shader shader = Shader.Find("Game/Vision/FogOfWar");
            if (shader == null)
            {
                Debug.LogError("[008-C] FogOfWar shader not found!");
                return;
            }

            _fogMaterial = new Material(shader);
            
            // Set render pass event (after transparent rendering, before UI)
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            // Initialize descriptor
            _descriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default)
            {
                msaaSamples = 1
            };
        }

        /// <summary>
        /// Configure pass for frame execution.
        /// Called every frame by the renderer feature.
        /// </summary>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _descriptor.width = renderingData.cameraData.cameraTargetDescriptor.width;
            _descriptor.height = renderingData.cameraData.cameraTargetDescriptor.height;
        }

        /// <summary>
        /// Execute fog rendering pass.
        /// </summary>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_fogMaterial == null || !_visionConfig.EnableFogOfWar)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(name: "FogOfWarPass");
            try
            {
                // [008-C] Set vision sources
                SetVisionSourcesInMaterial();

                // [008-C] Set fog color + softness
                _fogMaterial.SetColor(_FogColor, _visionConfig.FogColor);
                _fogMaterial.SetFloat(_FogEdgeSoftness, _visionConfig.FogEdgeSoftness);

                // [008-C] Execute blit
                RenderingUtils.FinalBlit(cmd, _fogMaterial, 0);

                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }

        /// <summary>
        /// Set vision sources (player + companion positions) in shader.
        /// </summary>
        private void SetVisionSourcesInMaterial()
        {
            if (_visionCoordinator == null)
                return;

            Vector4[] sources = new Vector4[2];
            Vector2 ranges = new Vector2(_visionConfig.PlayerVisionRange, _visionConfig.CompanionVisionRange);

            // [008-C] Get vision source positions from coordinator
            var visibleSources = _visionCoordinator.GetVisibleSources();
            for (int i = 0; i < visibleSources.Count && i < 2; i++)
            {
                sources[i] = new Vector4(visibleSources[i].x, visibleSources[i].y, visibleSources[i].z, 1.0f);
            }

            // [008-C] Pad with zero if only one source
            if (visibleSources.Count < 2)
            {
                sources[1] = Vector4.zero;
            }

            _fogMaterial.SetVectorArray(_VisionSources, sources);
            _fogMaterial.SetVector(_VisionRanges, ranges);
        }

        /// <summary>
        /// Cleanup pass resources.
        /// </summary>
        public void Cleanup()
        {
            if (_fogMaterial != null)
            {
                Object.Destroy(_fogMaterial);
            }
        }
    }
}
