Shader "VisibilityBufferRenderingShader"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    TEXTURE2D_X_FLOAT(_CameraColorTexture);
    SAMPLER(sampler_CameraColorTexture);

    TEXTURE2D_X_FLOAT(_MainTex);
    SAMPLER(sampler_MainTex);

    #include "VisibilityBufferRendering.hlsl"
    
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "GenerateGBUfferPass"
            ZTest Off
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma require integers
            #pragma vertex Vert
            #pragma fragment GenerateGBUffer
            #pragma enable_d3d11_debug_symbols 
            ENDHLSL
        }

        Pass
        {
            Name "GenerateGBUfferDebugPass"
            ZTest Off
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma enable_d3d11_debug_symbols 
            #pragma vertex Vert
            #pragma fragment GenerateGBUfferDebug
            ENDHLSL
        }

        /*Pass
        {
            Name "CombineColorPass"
            ZTest Off
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CombineColor

            float4 CombineColor(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float4 camera_color = SAMPLE_TEXTURE2D_X(_CameraColorTexture, sampler_CameraColorTexture, uv);
                float3 maintex_color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);

                return float4(maintex_color + camera_color.rgb, camera_color.a);
            }
            ENDHLSL
        }*/
    }
}
