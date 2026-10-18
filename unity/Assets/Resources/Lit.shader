// Простые освещённые детали: корпус, отсеки, штурвал, светляки. Цвет _Color, освещение по Ламберту,
// туман сцены, точечные лампы отсеков и прожектор.
// Лежит в Resources, чтобы попасть в сборку: встроенный Standard в APK вырезается, и всё становится розовым.
Shader "Pogrujenie/Lit"
{
    Properties
    {
        _Color ("Цвет", Color) = (1, 1, 1, 1)
        _Glow ("Свечение", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0
        fixed4 _Color;
        fixed4 _Glow;
        struct Input { float3 worldPos; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Emission = _Glow.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
