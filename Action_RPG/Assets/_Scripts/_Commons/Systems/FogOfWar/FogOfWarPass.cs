using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Game.Features.Vision.Rendering
{
    /// <summary>
    /// [008-C] ScriptableRenderPass for fog of war fullscreen effect.
    /// Uses RenderGraph API (URP 17+) to blit scene through FogOfWar shader.
    /// </summary>
    public class FogOfWarPass : ScriptableRenderPass
    {
        private Material _fogMaterial;
        private const string PassName = "FogOfWarPass";

        public FogOfWarPass(Material material)
        {
            _fogMaterial = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        /// <summary>
        /// [008-C] RenderGraph implementation for URP 17+.
        /// Blits camera color through fog material using fullscreen pass.
        /// </summary>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_fogMaterial == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();

            // [008-C] Skip if no valid camera color
            if (resourceData.isActiveTargetBackBuffer)
                return;

            var source = resourceData.activeColorTexture;

            // [008-C] Create temp texture matching source
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_FoWTemp";
            desc.clearBuffer = false;
            var tempTexture = renderGraph.CreateTexture(desc);

            // [008-C] Pass 1: Blit source → temp with fog shader
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName + "_Apply", out var passData))
            {
                passData.source = source;
                passData.material = _fogMaterial;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // [008-C] Pass 2: Copy temp → source (result back to screen)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName + "_Copy", out var passData))
            {
                passData.source = tempTexture;
                passData.material = null;

                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        private class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        public void Dispose() { }
    }
}
