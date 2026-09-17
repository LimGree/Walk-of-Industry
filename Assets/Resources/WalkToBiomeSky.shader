Shader "Hidden/WalkToBiome/Sky"
{
    Properties
    {
        _SkyColor ("Sky", Color) = (0.42, 0.66, 0.95, 1)
        _HorizonColor ("Horizon", Color) = (0.74, 0.82, 0.90, 1)
        _GroundColor ("Ground", Color) = (0.28, 0.26, 0.24, 1)
        _SunColor ("Sun", Color) = (1, 0.92, 0.75, 1)
        _SunsetColor ("Sunset", Color) = (1, 0.45, 0.18, 1)
        _SunDir ("Sun Dir", Vector) = (0, 1, 0, 0)
        _StarStrength ("Stars", Float) = 0
        _Exposure ("Exposure", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }
        Cull Off
        ZWrite Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _SkyColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float4 _SunColor;
            float4 _SunsetColor;
            float4 _SunDir;
            float _StarStrength;
            float _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Stars(float3 dir)
            {
                float3 n = dir * 92.0;
                float3 cell = floor(n);
                float3 f = frac(n) - 0.5;
                float h = Hash31(cell);
                float d = dot(f, f);
                float spark = saturate(h - 0.973) / 0.027;
                return spark * spark * saturate(1.0 - d * 14.0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float h = dir.y;
                float3 sunDir = normalize(_SunDir.xyz);
                float3 moonDir = normalize(-sunDir);
                float sunH = sunDir.y;
                float sunDot = saturate(dot(dir, sunDir));
                float moonDot = saturate(dot(dir, moonDir));

                float3 col;
                if (h >= 0.0)
                {
                    float t = saturate(pow(h, 0.52));
                    col = lerp(_HorizonColor.rgb, _SkyColor.rgb, t);
                }
                else
                {
                    float t = saturate(-h * 1.55);
                    col = lerp(_HorizonColor.rgb, _GroundColor.rgb, t);
                }

                float horizon = exp(-abs(h) * 7.5);
                float sunNear = saturate(1.15 - abs(sunH) * 3.6);
                float mie = pow(sunDot, 8.0) * 0.55 + pow(sunDot, 32.0) * 0.8;
                col += _SunsetColor.rgb * horizon * sunNear * (0.22 + mie);

                float glow = pow(sunDot, 12.0);
                col += _SunColor.rgb * glow * saturate(sunH + 0.15) * 0.35;

                float disc = smoothstep(0.9994, 0.99985, sunDot);
                float limb = smoothstep(0.9982, 0.9994, sunDot) * 0.45;
                float sunVis = saturate(sunH + 0.08);
                col += (_SunColor.rgb * 1.35 + 0.35) * (disc + limb) * sunVis;

                float night = saturate(_StarStrength);
                float moonVis = saturate(-sunH + 0.05);
                float moonBody = smoothstep(0.99915, 0.99955, moonDot);
                float moonHalo = pow(moonDot, 90.0) * 0.28;
                float3 moonCol = float3(0.78, 0.84, 0.95);
                col += moonCol * (moonBody * 0.9 + moonHalo) * moonVis;

                float starMask = saturate(-h * 0.15 + 0.92) * night * night;
                col += Stars(dir) * starMask * 1.15;

                return float4(col * _Exposure, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
