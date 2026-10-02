Shader "Hidden/WalkToBiome/Ghost"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,0.45)
        // 0 — альфа текстуры не прозрачность (палитра wi_palette хранит в ней глянец)
        _TexAlpha ("Use texture alpha", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _TexAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 col = tex * _Color;
                col.a = _Color.a * lerp(1.0, max(tex.a, 0.15), _TexAlpha);
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
