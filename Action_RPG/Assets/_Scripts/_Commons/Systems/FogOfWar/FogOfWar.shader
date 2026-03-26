Shader "Game/Vision/FogOfWar"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FogOfWarPass"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // [008-B] Globals set by FogOfWarController MonoBehaviour via Shader.SetGlobalXXX
            float4 _FoW_PlayerPos;
            float4 _FoW_CompanionPos;
            float _FoW_PlayerRange;
            float _FoW_CompanionRange;
            float4 _FoW_FogColor;
            float _FoW_EdgeSoftness;
            float _FoW_HasCompanion;

            float4 frag(Varyings input) : SV_Target
            {
                // [008-B] Sample source scene color from blit texture
                float4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // [008-B] Sample depth and reconstruct world position
                float depth = SampleSceneDepth(input.texcoord);

                // Skip skybox / far plane (reversed-Z: depth near 0 = far)
                #if UNITY_REVERSED_Z
                if (depth < 0.0001)
                    return sceneColor;
                #else
                if (depth > 0.9999)
                    return sceneColor;
                #endif

                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);

                // [008-B] Calculate XZ distance to player (ignore Y for top-down/3rd person)
                float distPlayer = length(worldPos.xz - _FoW_PlayerPos.xz);
                float visPlayer = 1.0 - smoothstep(
                    _FoW_PlayerRange - _FoW_EdgeSoftness,
                    _FoW_PlayerRange,
                    distPlayer);

                // [008-B] Calculate visibility from companion (if present)
                float visCompanion = 0.0;
                if (_FoW_HasCompanion > 0.5)
                {
                    float distCompanion = length(worldPos.xz - _FoW_CompanionPos.xz);
                    visCompanion = 1.0 - smoothstep(
                        _FoW_CompanionRange - _FoW_EdgeSoftness,
                        _FoW_CompanionRange,
                        distCompanion);
                }

                // [008-B] Take maximum visibility (closest vision source wins)
                float visibility = saturate(max(visPlayer, visCompanion));

                // [008-B] Lerp between fog color and scene color
                float4 result;
                result.rgb = lerp(_FoW_FogColor.rgb, sceneColor.rgb, visibility);
                result.a = 1.0;
                return result;
            }
            ENDHLSL
        }
    }
}
