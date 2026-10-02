// Земля (тайлы карты биомов): как Hidden/WalkToBiome/Unlit, плюс свет ближайших точечных и прожекторных
// источников (фонари декораций, огонь печей, фонарик игрока). Список готовит GroundLights — один глобальный
// массив вместо прямого рендера, где на огромный тайл давалось лишь несколько попиксельных огней.
Shader "Hidden/WalkToBiome/GroundLit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _LightTint ("Light Tint", Color) = (1,1,1,1)
        _WalkLightTint ("World Light", Color) = (1,1,1,1)
        _WalkUvFog ("UV Fog", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry+1"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }
        ZWrite On
        Cull Off
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma skip_variants FOG_EXP FOG_EXP2
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define WALK_MAX_LIGHTS 32

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _LightTint;
            float4 _WalkLightTint;
            float _WalkUvFog;
            float _WalkFogAmount;

            // xyz — позиция, w — 1 / range²
            float4 _WalkPointPos[WALK_MAX_LIGHTS];
            // rgb — цвет × яркость (линейный), a — 1 у прожектора
            float4 _WalkPointColor[WALK_MAX_LIGHTS];
            // xyz — направление прожектора, w — cos внешнего угла
            float4 _WalkPointDir[WALK_MAX_LIGHTS];
            float _WalkPointCount;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 meshUv : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.meshUv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float3 PointLights(float3 worldPos)
            {
                float3 sum = 0;
                int count = (int)_WalkPointCount;
                [loop]
                for (int n = 0; n < WALK_MAX_LIGHTS; n++)
                {
                    if (n >= count)
                        break;
                    float3 toLight = _WalkPointPos[n].xyz - worldPos;
                    float d2 = max(dot(toLight, toLight), 1e-4);
                    // Мягкое затухание до нуля на range, как у встроенного точечного света.
                    float fall = saturate(1.0 - d2 * _WalkPointPos[n].w);
                    fall *= fall;
                    float3 dir = toLight * rsqrt(d2);
                    // Земля смотрит вверх; огонь у самой земли тоже должен её подсвечивать — полуобёрнутый Ламберт.
                    float ndotl = saturate(dir.y * 0.65 + 0.35);
                    float spot = 1;
                    if (_WalkPointColor[n].a > 0.5)
                    {
                        float cosA = dot(-dir, _WalkPointDir[n].xyz);
                        float outer = _WalkPointDir[n].w;
                        spot = saturate((cosA - outer) / max(1e-3, (1 - outer) * 0.5));
                        ndotl = saturate(dir.y);
                    }
                    sum += _WalkPointColor[n].rgb * (fall * ndotl * spot);
                }
                return sum;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                clip(albedo.a - 0.01);
                fixed4 col = albedo;
                col.rgb *= _LightTint.rgb * _WalkLightTint.rgb;
                col.rgb += albedo.rgb * PointLights(i.worldPos);

                float3 worldPos = i.worldPos;
                if (_WalkUvFog > 0.5)
                {
                    float2 meshUv = i.meshUv;
                    worldPos = mul(unity_ObjectToWorld, float4(meshUv.x - 0.5, meshUv.y - 0.5, 0, 1)).xyz;
                }
                float dist = distance(worldPos, _WorldSpaceCameraPos);
                UNITY_CALC_FOG_FACTOR_RAW(dist);
                float fogMix = _WalkFogAmount > 0.5 ? saturate(unityFogFactor) : 1;
                col.rgb = lerp(unity_FogColor.rgb, col.rgb, fogMix);

                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
