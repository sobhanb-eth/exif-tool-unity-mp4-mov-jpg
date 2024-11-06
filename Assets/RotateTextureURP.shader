Shader "Custom/RotateTextureURP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Rotation ("Rotation Angle", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalRenderPipeline" "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma vertex vert
            #pragma fragment frag

            // Include URP core shader library
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Define shader properties
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float _Rotation;

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Transform object space position to homogeneous clip space
                output.positionHCS = TransformObjectToHClip(input.positionOS);

                // Apply tiling and offset to UVs
                float2 uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;

                // Center the UV coordinates
                uv -= 0.5;

                // Apply rotation
                float rad = radians(_Rotation);
                float s = sin(rad);
                float c = cos(rad);
                float2x2 rotationMatrix = float2x2(c, -s, s, c);
                uv = mul(rotationMatrix, uv);

                // Restore UV coordinates
                uv += 0.5;

                output.uv = uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample the texture with the rotated UVs
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return color;
            }

            ENDHLSL
        }
    }
}
