// 内置渲染管线：配合 Camera 上的 CameraSpeedSideBlur + Graphics.Blit
Shader "Hidden/Drone/SpeedSideBlur"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _BlurIntensity;
            float _ClearCenter;
            float _MaxBlurPx;

            fixed3 SampleRgb(float2 uv)
            {
                return tex2D(_MainTex, uv).rgb;
            }

            fixed3 BlurHorizontal(float2 uv, float pixelSpread)
            {
                float2 off = _MainTex_TexelSize.xy * pixelSpread;
                fixed3 c = SampleRgb(uv) * 0.227027;
                c += SampleRgb(uv + float2(off.x * 1.0, 0.0)) * 0.1945946;
                c += SampleRgb(uv - float2(off.x * 1.0, 0.0)) * 0.1945946;
                c += SampleRgb(uv + float2(off.x * 2.0, 0.0)) * 0.1216216;
                c += SampleRgb(uv - float2(off.x * 2.0, 0.0)) * 0.1216216;
                c += SampleRgb(uv + float2(off.x * 3.0, 0.0)) * 0.054054;
                c += SampleRgb(uv - float2(off.x * 3.0, 0.0)) * 0.054054;
                c += SampleRgb(uv + float2(off.x * 4.0, 0.0)) * 0.016216;
                c += SampleRgb(uv - float2(off.x * 4.0, 0.0)) * 0.016216;
                return c;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float dist = abs(uv.x - 0.5) * 2.0;
                float side = smoothstep(_ClearCenter, 1.0, dist) * _BlurIntensity;

                if (side < 0.001)
                    return fixed4(SampleRgb(uv), 1.0);

                float spread = side * _MaxBlurPx;
                fixed3 blurred = BlurHorizontal(uv, spread);
                fixed3 sharp = SampleRgb(uv);
                fixed3 rgb = lerp(sharp, blurred, side);
                return fixed4(rgb, 1.0);
            }
            ENDCG
        }
    }
}
