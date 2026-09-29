Shader "WalkToBiome/BeltScroll"
{
    // Полотно ленты: освещение как у Standard + бегущие поперечные рёбра по ходу груза.
    // UV модели: u = 1 — рёбра включены (верх полотна), v — путь вдоль ленты в метрах.
    // Смещение _WiBeltOffset задаёт BeltSpeedSystem (глобально, со скоростью лент).
    Properties
    {
        _Color ("Color", Color) = (0.13, 0.13, 0.14, 1)
        _RibColor ("Rib Color", Color) = (0.24, 0.24, 0.26, 1)
        _RibCount ("Ribs per meter", Float) = 6
        _RibWidth ("Rib width 0..1", Range(0.05, 0.9)) = 0.3
        _RibDir ("Direction (1 / -1)", Float) = 1
        _Glossiness ("Smoothness", Range(0, 1)) = 0.1
        [HideInInspector] _MainTex ("UV source", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        fixed4 _RibColor;
        float _RibCount;
        float _RibWidth;
        float _RibDir;
        half _Glossiness;
        float _WiBeltOffset;
        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;
            float rib = step(frac((uv.y * _RibDir - _WiBeltOffset) * _RibCount), _RibWidth) * step(0.5, uv.x);
            o.Albedo = lerp(_Color.rgb, _RibColor.rgb, rib) * tex2D(_MainTex, float2(0.5, 0.5)).rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
