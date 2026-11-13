Shader "Shadow Steps/Vision Cone Vertex Color"
{
    Properties
    {
        _Color ("Tint and opacity", Color) = (1, 1, 1, 1)
        _FlashColor ("Alarm flash", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                half4 color : COLOR;
                float4 mountPlane : TEXCOORD1;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float mountDistance : TEXCOORD0;
            };

            half4 _Color;
            half4 _FlashColor;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                float2 mountNormal = input.mountPlane.xy;
                output.mountDistance = any(mountNormal)
                    ? dot(input.position.xy - input.mountPlane.zw, mountNormal)
                    : 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.mountDistance);
                half4 color = input.color * _Color;
                color.rgb = lerp(color.rgb, _FlashColor.rgb, _FlashColor.a);
                return color;
            }
            ENDCG
        }
    }
}
