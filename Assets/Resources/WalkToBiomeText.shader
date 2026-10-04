Shader "Hidden/WalkToBiome/Text"
{
    // Текст TextMesh в мире (вывески). Встроенный GUI/Text Shader рисуется без отсечения граней
    // и сквозь стены — надпись видна зеркально с обратной стороны. Здесь: Cull Back + тест глубины.
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }
        ZWrite Off
        ZTest LEqual
        Cull Back
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma skip_variants FOG_EXP FOG_EXP2
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _WalkFogAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = i.color;
                col.a *= tex2D(_MainTex, i.uv).a;
                clip(col.a - 0.01);

                float dist = distance(i.worldPos, _WorldSpaceCameraPos);
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
