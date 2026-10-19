// Простые освещённые детали: корпус, отсеки, штурвал, светляки. Цвет _Color, освещение по Ламберту,
// туман сцены, точечные лампы отсеков и прожектор. Модели художника — с текстурой _MainTex (у остальных она белая);
// _TexGlow > 0 зажигает голубые места текстуры (огни голостола #67DDEB): синий заметно больше красного.
// Лежит в Resources, чтобы попасть в сборку: встроенный Standard в APK вырезается, и всё становится розовым.
Shader "Pogrujenie/Lit"
{
    Properties
    {
        _Color ("Цвет", Color) = (1, 1, 1, 1)
        _Glow ("Свечение", Color) = (0, 0, 0, 1)
        _MainTex ("Текстура", 2D) = "white" {}
        _TexGlow ("Свечение голубых мест текстуры", Float) = 0
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
        sampler2D _MainTex;
        float _TexGlow;
        struct Input { float2 uv_MainTex; float3 worldPos; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 t = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = _Color.rgb * t.rgb;
            o.Emission = _Glow.rgb + _TexGlow * t.rgb * saturate((t.b - t.r - 0.25) * 4);
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
