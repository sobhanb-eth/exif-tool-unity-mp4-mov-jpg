Shader "Custom/RotateTextureShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Rotation ("Rotation Angle", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Rotation;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // Center the UV coordinates
                uv -= 0.5;

                // Convert rotation angle from degrees to radians
                float rad = radians(_Rotation);

                // Apply rotation
                float s = sin(rad);
                float c = cos(rad);
                float2x2 rotationMatrix = float2x2(c, -s, s, c);
                uv = mul(rotationMatrix, uv);

                // Restore UV coordinates
                uv += 0.5;

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
