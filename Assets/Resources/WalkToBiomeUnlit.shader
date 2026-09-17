Shader "Hidden/WalkToBiome/Unlit"
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _LightTint;
            float4 _WalkLightTint;
            float _WalkUvFog;
            float _WalkFogAmount;

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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                clip(col.a - 0.01);
                col.rgb *= _LightTint.rgb * _WalkLightTint.rgb;

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
