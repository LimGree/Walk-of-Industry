// Груз на лентах: одна модель = один меш, цвет частей запечён в цвет вершин,
// металл/гладкость — в uv4. Один материал на все предметы → GPU instancing.
Shader "Hidden/WalkToBiome/BeltItem"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma multi_compile_instancing
        #pragma target 3.0

        struct Input
        {
            float4 tint;
            float2 metalGloss;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.tint = v.color;
            o.metalGloss = v.texcoord3.xy;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = IN.tint.rgb;
            o.Metallic = IN.metalGloss.x;
            o.Smoothness = IN.metalGloss.y;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
