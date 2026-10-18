// Дно и лёд отмели: цвет из вершин, освещение по Ламберту, туман сцены.
// _Lit — доля освещённого цвета (дно 1, лёд 0), _Glow — собственное свечение (лёд светится сам).
// Лежит в Resources, чтобы попасть в сборку и находиться через Shader.Find.
Shader "Pogrujenie/Seabed"
{
    Properties
    {
        _Lit ("Освещённость", Range(0, 1)) = 1
        _Glow ("Свечение", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0
        half _Lit;
        fixed4 _Glow;
        struct Input { float4 color : COLOR; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb * _Lit;
            o.Emission = IN.color.rgb * _Glow.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
