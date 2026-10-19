// Видимый луч прожектора в воде (S.beam в js/ship.js): цвета складываются с тем, что за ним,
// без записи глубины, видны обе стороны конуса, тумана нет. Яркость вдоль луча — в цвете вершин.
// Лежит в Resources, чтобы попасть в сборку.
Shader "Pogrujenie/Beam"
{
    Properties
    {
        _Color ("Цвет и яркость", Color) = (0.81, 0.96, 1, 0.06)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = fixed4(v.color.rgb * _Color.rgb, _Color.a);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
