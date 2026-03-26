Shader "Game/Vision/FogOfWar"
{
    Properties
    {
        _MainTex ("Screen Texture", 2D) = "white" {}
        _DepthTexture ("Depth Texture", 2D) = "white" {}
        _CameraDepthTexture ("Camera Depth Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FogOfWarPass"
            
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _VisionSources[2];      // Player + companion positions (xyz)
                float2 _VisionRanges;          // (player range, companion range)
                float4 _FogColor;              // Fog color + alpha
                float _FogEdgeSoftness;        // Edge gradient softness
                float _ScreenHeight;
                float _ScreenWidth;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            // Reconstruct world position from depth
            float3 ReconstructWorldPos(float2 uv, float depth)
            {
                float4 positionCS = float4(uv * 2.0 - 1.0, depth, 1.0);
                positionCS.y *= -1.0; // Flip Y for DirectX

                float4 positionVS = mul(UNITY_MATRIX_P, positionCS);
                positionVS /= positionVS.w;
                positionVS.z *= -1.0;

                float4 positionWS = mul(UNITY_MATRIX_I_V, positionVS);
                return positionWS.xyz;
            }

            // Check if point is inside vision circle
            float IsInVision(float3 worldPos)
            {
                // Distance from player
                float distToPlayer = distance(worldPos, _VisionSources[0].xyz);
                if (distToPlayer < _VisionRanges.x)
                    return 1.0;

                // Distance from companion
                float distToCompanion = distance(worldPos, _VisionSources[1].xyz);
                if (distToCompanion < _VisionRanges.y)
                    return 1.0;

                return 0.0;
            }

            // Calculate fog alpha based on distance to vision boundary
            float CalculateFogAlpha(float3 worldPos)
            {
                // Distance from player
                float distToPlayer = distance(worldPos, _VisionSources[0].xyz);
                float fadePlayer = smoothstep(_VisionRanges.x + _FogEdgeSoftness, _VisionRanges.x, distToPlayer);

                // Distance from companion
                float distToCompanion = distance(worldPos, _VisionSources[1].xyz);
                float fadeCompanion = smoothstep(_VisionRanges.y + _FogEdgeSoftness, _VisionRanges.y, distToCompanion);

                // Take max fade (closest vision source wins)
                return max(fadePlayer, fadeCompanion);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, input.texcoord).r;
                float3 worldPos = ReconstructWorldPos(input.texcoord, depth);

                // If inside vision, return screen color (no fog)
                if (IsInVision(worldPos) > 0.5)
                    return float4(0.0, 0.0, 0.0, 0.0); // Transparent

                // Outside vision: apply fog with smooth edge
                float fogAlpha = CalculateFogAlpha(worldPos);
                return _FogColor * fogAlpha;
            }
            ENDHLSL
        }
    }
}
