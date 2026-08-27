// Thanh khiên cho UI (uGUI Image).
//
// LÝ DO TỒN TẠI: đo trên Unity, BarShield trên nền BarHp chỉ đạt tương phản 2.84 —
// dưới xa ngưỡng đọc được. Nghĩa là ĐỔI MÀU KHÔNG CỨU ĐƯỢC thanh khiên. Shader này
// phân biệt khiên bằng thứ mắt bắt tốt hơn sắc độ:
//   1. hoa tiết gạch chéo ĐANG CHẠY  — chuyển động tách nó khỏi mọi thanh tĩnh
//   2. viền sáng ở mép dẫn            — cạnh cứng đọc ra ranh giới ngay
//   3. nhấp nháy khi khiên đổi lượng  — báo sự kiện
//
// Dùng với Image type = Filled. Truyền _Fill đúng bằng fillAmount, và _EdgeSide
// = 1 nếu fill từ trái, = -1 nếu fill từ phải.
Shader "UI/ShieldBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Stripes)]
        _StripeColor    ("Stripe Color", Color) = (1,1,1,0.35)
        _StripeDensity  ("Stripe Density", Float) = 14
        _StripeSpeed    ("Stripe Speed", Float) = 0.35
        _StripeSkew     ("Stripe Skew", Float) = 0.7
        _StripeWidth    ("Stripe Width", Range(0.05, 0.9)) = 0.42
        _StripeSoftness ("Stripe Softness", Range(0.01, 0.5)) = 0.14

        [Header(Leading edge)]
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        _EdgeWidth ("Edge Width", Range(0, 0.5)) = 0.05
        _EdgeSide  ("Edge Side (1 = from left, -1 = from right)", Float) = 1
        _Fill      ("Fill Amount", Range(0,1)) = 1

        [Header(Feedback)]
        _Pulse ("Pulse", Range(0,1)) = 0

        // Boilerplate bắt buộc của uGUI (mask, stencil, color mask)
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
            "Queue"            = "Transparent"
            "IgnoreProjector"  = "True"
            "RenderType"       = "Transparent"
            "PreviewType"      = "Plane"
            "CanUseSpriteAtlas"= "True"
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
            Name "SHIELDBAR"
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

            fixed4 _StripeColor;
            float  _StripeDensity;
            float  _StripeSpeed;
            float  _StripeSkew;
            float  _StripeWidth;
            float  _StripeSoftness;

            fixed4 _EdgeColor;
            float  _EdgeWidth;
            float  _EdgeSide;
            float  _Fill;
            float  _Pulse;

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

            fixed4 frag (v2f IN) : SV_Target
            {
                half4 base = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // ── 1. Gạch chéo đang chạy ────────────────────────────────
                // Toạ độ chéo: trượt theo x, nghiêng theo y, đẩy dần theo thời gian.
                float p   = frac(IN.texcoord.x * _StripeDensity
                               + IN.texcoord.y * _StripeSkew * _StripeDensity
                               - _Time.y * _StripeSpeed * _StripeDensity);
                // Sóng tam giác 0..1 rồi cắt mềm -> dải sáng có bề rộng điều chỉnh được.
                float tri  = abs(p - 0.5) * 2.0;
                float band = 1.0 - smoothstep(_StripeWidth, _StripeWidth + _StripeSoftness, tri);

                // ── 2. Viền sáng ở mép dẫn ────────────────────────────────
                // Image type=Filled cắt hình học tại _Fill, nên mép dẫn nằm đúng ở đó.
                float d    = (_EdgeSide >= 0.0)
                           ? (_Fill - IN.texcoord.x)
                           : (IN.texcoord.x - (1.0 - _Fill));
                float edge = 1.0 - smoothstep(0.0, max(_EdgeWidth, 1e-4), max(d, 0.0));

                half3 rgb = base.rgb;
                rgb = lerp(rgb, _StripeColor.rgb, band * _StripeColor.a);
                rgb += _EdgeColor.rgb * edge * _EdgeColor.a;

                // ── 3. Nhấp nháy khi khiên vừa đổi lượng ──────────────────
                rgb += _EdgeColor.rgb * _Pulse * 0.65;

                half4 col = half4(rgb, base.a);

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
