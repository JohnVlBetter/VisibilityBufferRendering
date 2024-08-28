#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/NormalReconstruction.hlsl"
float4 _CameraDepthTexture_TexelSize;

TEXTURE2D(_VisibilityBuffer); SAMPLER(sampler_VisibilityBuffer);
TEXTURE2D(_DepthBuffer); SAMPLER(sampler_DepthBuffer);

float4 GenerateGBUffer(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = input.texcoord;
    
    int4 visibilityBufferVal = SAMPLE_TEXTURE2D_X(_VisibilityBuffer, sampler_VisibilityBuffer, uv);
    //float depth = SAMPLE_TEXTURE2D_X(_DepthBuffer, sampler_DepthBuffer, uv).r;
    //float sceneLinearDepth = LinearEyeDepth(depth, _ZBufferParams);
    return float4((visibilityBufferVal.x + 1) / 10.0, visibilityBufferVal.y / 100.0, (visibilityBufferVal.z + 1) / 5.0, 1);
}

