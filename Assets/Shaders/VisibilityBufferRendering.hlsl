#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/NormalReconstruction.hlsl"
float4 _CameraDepthTexture_TexelSize;

TEXTURE2D(_VisibilityBuffer); SAMPLER(sampler_VisibilityBuffer);
TEXTURE2D(_DepthBuffer); SAMPLER(sampler_DepthBuffer);

Buffer<float> _VertexBuffer;
Buffer<int> _IndexBuffer;

float4 GenerateGBUffer(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = input.texcoord;
    
    //float vertex = _VertexBuffer[0];
    //int index = _IndexBuffer[0];
    half4 visibilityBufferValHalf = _VisibilityBuffer.Sample(sampler_VisibilityBuffer, uv);
    //大坑，没法采样整数纹理，只能先采half4，然后转换成uint4，保证精度
    uint4 visibilityBufferVal = uint4(asuint(visibilityBufferValHalf.r), asuint(visibilityBufferValHalf.g), 
                                        asuint(visibilityBufferValHalf.b), asuint(visibilityBufferValHalf.a));
    //float depth = SAMPLE_TEXTURE2D_X(_DepthBuffer, sampler_DepthBuffer, uv).r;
    //float sceneLinearDepth = LinearEyeDepth(depth, _ZBufferParams);
    uint subMeshStartIndex = visibilityBufferVal.w;
    uint triangleID = visibilityBufferVal.y;
    uint vertexIdx = _IndexBuffer[triangleID * 3 + subMeshStartIndex] * 12;
    float3 normal = float3(_VertexBuffer[vertexIdx + 3], _VertexBuffer[vertexIdx + 4], _VertexBuffer[vertexIdx + 5]);
    return float4(normal, 1.0);
}

float4 GenerateGBUfferDebug(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = input.texcoord;
    
    float4 visibilityBufferVal = _VisibilityBuffer.Sample(sampler_VisibilityBuffer, uv);
    //float depth = SAMPLE_TEXTURE2D_X(_DepthBuffer, sampler_DepthBuffer, uv).r;
    //float sceneLinearDepth = LinearEyeDepth(depth, _ZBufferParams);
    return float4(visibilityBufferVal.x / 10.0, visibilityBufferVal.y / 1000.0, visibilityBufferVal.z / 5.0, visibilityBufferVal.w / 100.0);
}