// 失明 / 视力模糊滤镜。挂在背景 Image 上，用 _BlindAmount 统一控制强度。
Shader "VN/BlindEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _BlindAmount ("失明强度", Range(0,1)) = 0
        _Brightness ("提亮", Range(0.5,2.5)) = 1.55
        _Contrast ("对比度", Range(0,2)) = 0.85
        _BlurPixels ("模糊（像素）", Range(0,12)) = 5
        _WarpStrength ("扭曲", Range(0,0.06)) = 0.018
        _VignetteStrength ("暗角", Range(0,1)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;

            float _BlindAmount;
            float _Brightness;
            float _Contrast;
            float _BlurPixels;
            float _WarpStrength;
            float _VignetteStrength;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 sampleBlur(float2 uv, float2 texel, float amount)
            {
                // 模糊强度必须跟着失明强度走，否则强度为 0 时画面也是糊的
                if (_BlurPixels <= 0.001 || amount <= 0.001)
                    return tex2D(_MainTex, uv);

                float2 o = texel * _BlurPixels * amount;

                fixed4 c = tex2D(_MainTex, uv);

                c += tex2D(_MainTex, uv + float2( o.x, 0));
                c += tex2D(_MainTex, uv + float2(-o.x, 0));
                c += tex2D(_MainTex, uv + float2(0,  o.y));
                c += tex2D(_MainTex, uv + float2(0, -o.y));

                c += tex2D(_MainTex, uv + float2( o.x,  o.y));
                c += tex2D(_MainTex, uv + float2(-o.x,  o.y));
                c += tex2D(_MainTex, uv + float2( o.x, -o.y));
                c += tex2D(_MainTex, uv + float2(-o.x, -o.y));

                return c / 9.0;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                // 扭曲：低频随机扰动，随时间缓慢漂移
                float t = _Time.y * 0.25;
                float2 cell = floor(uv * 26.0 + t);
                float nx = hash21(cell) - 0.5;
                float ny = hash21(cell + 19.7) - 0.5;
                uv += float2(nx, ny) * _WarpStrength * _BlindAmount;

                float2 texel = _MainTex_TexelSize.xy;
                fixed4 col = sampleBlur(uv, texel, _BlindAmount) * i.color;

                // 去色 + 提亮 + 压对比
                float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float gray = saturate((lum - 0.5) * _Contrast + 0.5) * _Brightness;

                // 略微偏暖，避免死灰
                float3 blindColor = lerp(float3(gray, gray, gray), float3(gray * 1.02, gray, gray * 0.95), 0.6);

                col.rgb = lerp(col.rgb, blindColor, _BlindAmount);

                // 边缘收缩的暗角
                float2 d = abs(i.texcoord - 0.5) * 2.0;
                float vig = 1.0 - saturate(dot(d, d) * 0.45 * _VignetteStrength * _BlindAmount);
                col.rgb *= vig;

                return col;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
