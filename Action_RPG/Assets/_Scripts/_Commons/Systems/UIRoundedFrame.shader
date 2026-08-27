// Khung bo tròn cho ô UI (skill slot, item slot, avatar...).
//
// LÝ DO TỒN TẠI: hiện các ô skill KHÔNG có phần tử viền nào — cái "border bo tròn"
// nhìn thấy trong game thực ra được vẽ sẵn trong file ảnh icon. Skill nào art không
// có khung thì nhìn trần, và không cách nào bắt nó đồng bộ. Shader này vẽ khung
// bằng SDF nên mọi ô đều có khung giống nhau bất kể icon là ảnh gì.
//
// Vẽ hoàn toàn bằng công thức, không cần sprite. Truyền _Aspect = width/height của
// RectTransform để góc bo không bị méo khi ô không vuông.
Shader "UI/RoundedFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Shape)]
        _Radius ("Corner Radius", Range(0, 0.5)) = 0.18
        _Border ("Border Thickness", Range(0, 0.25)) = 0.045
        _Aspect ("Aspect (width / height)", Float) = 1

        [Header(Colors)]
        _FillColor   ("Fill Color", Color)   = (0.055, 0.043, 0.098, 0.85)
        _BorderColor ("Border Color", Color) = (0.42, 0.31, 0.659, 1)
        _GlowColor   ("Glow Color", Color)   = (0.702, 0.533, 1, 1)
        _GlowWidth   ("Glow Width", Range(0, 0.3)) = 0.06
        _GlowPower   ("Glow Power", Range(0, 4)) = 1

        [Header(Fill source)]
        // 0 = to nen bang _FillColor (o trong).
        // 1 = lay chinh sprite lam nen -> ICON duoc BO TRON theo khung.
        //     Can cho o skill: icon lap kin o nen neu khung nam sau no thi bi che sach.
        [Toggle] _TextureFill ("Fill from sprite", Float) = 0

        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID", Float) = 0
        _StencilOp        ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask", Float) = 255
        _ColorMask        ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "ROUNDEDFRAME"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4    _Color;
            fixed4    _TextureSampleAdd;
            float4    _ClipRect;
            float4    _MainTex_ST;

            float  _Radius;
            float  _Border;
            float  _Aspect;
            fixed4 _FillColor;
            fixed4 _BorderColor;
            fixed4 _GlowColor;
            float  _GlowWidth;
            float  _GlowPower;
            float  _TextureFill;

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex   = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color    = v.color * _Color;
                return OUT;
            }

            // SDF hộp bo tròn. Âm = bên trong, dương = bên ngoài.
            float RoundedBoxSDF(float2 p, float2 halfSize, float r)
            {
                float2 d = abs(p) - (halfSize - r);
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float aspect = max(_Aspect, 1e-3);

                // Đưa về không gian có tỉ lệ đúng, gốc ở tâm ô.
                float2 p        = (IN.texcoord - 0.5) * float2(aspect, 1.0);
                float2 halfSize = float2(aspect, 1.0) * 0.5;

                // Bán kính tính theo cạnh NGẮN để góc luôn tròn đều.
                float r    = _Radius * min(aspect, 1.0);
                float dist = RoundedBoxSDF(p, halfSize, r);

                // Khử răng cưa theo đạo hàm màn hình -> nét sắc ở mọi độ phân giải.
                float aa = fwidth(dist) * 0.9 + 1e-5;

                // Nền bên trong
                float inside = 1.0 - smoothstep(-aa, aa, dist);

                // Vành viền: dải quanh mép trong
                float bw   = max(_Border * min(aspect, 1.0), 1e-4);
                float ring = (1.0 - smoothstep(-aa, aa, dist))
                           * smoothstep(-bw - aa, -bw + aa, dist);

                // Quầng sáng lan ra ngoài
                float glow = 0.0;
                if (_GlowWidth > 1e-4)
                {
                    glow = saturate(1.0 - dist / _GlowWidth);
                    glow = pow(max(glow, 0.0), max(_GlowPower, 0.001));
                    glow *= step(0.0, dist); // chỉ phía ngoài, trong đã có nền lo
                }

                // Nền: hoặc màu phẳng, hoặc chính sprite (icon bo tròn).
                half4 tex   = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half4 fillC = lerp(_FillColor, tex * _FillColor, _TextureFill);

                half4 col = half4(0, 0, 0, 0);

                col.rgb  = fillC.rgb;
                col.a    = fillC.a * inside;

                // Viền chồng lên nền
                col.rgb  = lerp(col.rgb, _BorderColor.rgb, ring * _BorderColor.a);
                col.a    = max(col.a, ring * _BorderColor.a);

                // Quầng sáng
                col.rgb  = lerp(col.rgb, _GlowColor.rgb, saturate(glow * _GlowColor.a));
                col.a    = max(col.a, glow * _GlowColor.a);

                col *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
        ENDCG
        }
    }

    Fallback "UI/Default"
}
