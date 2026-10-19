// Живой свет отмели (поток Б): светящийся мох и другие светящиеся пятна. Цвет из вершин × _Color
// прибавляется к тому, что под ним (Blend One One), глубину не пишет, видно с обеих сторон.
// Туман гасит свет к чёрному, а не к цвету воды — иначе вдали пятна светились бы туманом.
// Лежит в Resources, чтобы попасть в сборку и находиться через Resources.Load.
Shader "Pogrujenie/EcoGlow"
{
    Properties
    {
        _Color ("Цвет", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = i.color;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }
    }
}
