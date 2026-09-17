Shader "Hidden/WalkToBiome/Ground"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _WalkLightTint ("World Light", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry+1"
            "IgnoreProjector" = "True"
            "RenderType" = "Opaque"
        }
        ZWrite On
        ZTest LEqual
        Cull Off
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _WalkLightTint;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                #if defined(UNITY_REVERSED_Z)
                o.pos.z = min(o.pos.z, o.pos.w * 1.0e-4);
                #else
                o.pos.z = max(o.pos.z, o.pos.w * 1.0e-4);
                #endif
                return o;
            }

            float3 GroundWorld(float4 svpos)
            {
                float2 screen = svpos.xy / _ScreenParams.xy;
                #if UNITY_UV_STARTS_AT_TOP
                float2 ndc = float2(screen.x * 2.0 - 1.0, 1.0 - screen.y * 2.0);
                #else
                float2 ndc = float2(screen.x * 2.0 - 1.0, screen.y * 2.0 - 1.0);
                #endif
                float tanX = 1.0 / max(abs(UNITY_MATRIX_P[0][0]), 1e-6);
                float tanY = 1.0 / max(abs(UNITY_MATRIX_P[1][1]), 1e-6);
                float3 camRay = float3(ndc.x * tanX, ndc.y * tanY, 1.0);
                float3 worldDir = mul((float3x3)unity_CameraToWorld, camRay);
                float planeY = unity_ObjectToWorld._m13;
                float t = abs(worldDir.y) > 1e-8 ? (planeY - _WorldSpaceCameraPos.y) / worldDir.y : 0.0;
                return _WorldSpaceCameraPos + worldDir * t;
            }

            void frag(v2f i, out fixed4 col : SV_Target, out float outDepth : SV_Depth)
            {
                float3 worldPos = GroundWorld(i.pos);
                float3 local = mul(unity_WorldToObject, float4(worldPos, 1)).xyz;
                float2 uv = saturate(float2(local.x, local.z) + 0.5);
                uv = uv * _MainTex_ST.xy + _MainTex_ST.zw;

                col = tex2D(_MainTex, uv) * _Color;
                col.rgb *= _WalkLightTint.rgb;
                col.a = 1;

                float4 clipPos = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                outDepth = clipPos.z / max(abs(clipPos.w), 1e-6);
            }
            ENDCG
        }
    }
    FallBack Off
}
