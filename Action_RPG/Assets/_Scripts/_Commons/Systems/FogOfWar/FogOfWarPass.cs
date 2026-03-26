using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Game.Features.Vision.Data;
using Game.Features.Vision.Core;

namespace Game.Features.Vision.Rendering
{
    /// <summary>
    /// [008-C] ScriptableRenderPass for fog of war post-processing.
    /// Executes after opaque/transparent rendering to overlay fog effect.
    /// Reads depth texture and reconstructs world positions for vision checks.
    /// </summary>
    public class FogOfWarPass : ScriptableRenderPass
    {

        private Material _fogMaterial;
        private VisionConfig _visionConfig;
        private Vector4[] _visionSources = new Vector4[2];
        private Vector2 _visionRanges;
        private RenderTextureDescriptor _descriptor;

        public FogOfWarPass(VisionConfig config)
        {
            _visionConfig = config ?? throw new System.ArgumentNullException(nameof(config));

            // Create material from shader
            Shader shader = Shader.Find("Game/Vision/FogOfWar");
            if (shader == null)
            {
                Debug.LogError("[008-C] FogOfWar shader not found!");
                return;
            }

            _fogMaterial = new Material(shader);
            
            // [008-C] Initialize vision sources with default values
            _visionSources[0] = Vector3.zero;
            _visionSources[1] = Vector3.zero;
            _visionRanges = new Vector2(20f, 8f);
            
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
        [System.Obsolete]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _descriptor.width = renderingData.cameraData.cameraTargetDescriptor.width;
            _descriptor.height = renderingData.cameraData.cameraTargetDescriptor.height;
        }

        /// <summary>
        /// Set vision sources and ranges for fog shader.
        /// Called by FogOfWarFeature when transforms initialized.
        /// </summary>
        public void SetVisionSources(Vector3[] sourcePositions, Vector2 visionRanges)
        {
            for (int i = 0; i < sourcePositions.Length && i < 2; i++)
            {
                _visionSources[i] = sourcePositions[i];
            }
            _visionRanges = visionRanges;
        }

        /// <summary>
        /// Execute fog rendering pass.
        /// </summary>
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_fogMaterial == null || !_visionConfig.EnableFogOfWar)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(name: "FogOfWarPass");
            try
            {
                // [008-C] Set vision sources and ranges in shader
                _fogMaterial.SetVectorArray("_VisionSources", _visionSources);
                _fogMaterial.SetVector("_VisionRanges", _visionRanges);

                // [008-C] Set fog color + softness
                _fogMaterial.SetColor("_FogColor", _visionConfig.FogColor);
                _fogMaterial.SetFloat("_FogEdgeSoftness", _visionConfig.FogEdgeSoftness);

                // [008-C] Blit fog material to screen
                cmd.Blit(renderingData.cameraData.renderer.cameraColorTargetHandle, renderingData.cameraData.renderer.cameraColorTargetHandle, _fogMaterial);

                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
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
