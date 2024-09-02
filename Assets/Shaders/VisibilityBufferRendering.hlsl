#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/NormalReconstruction.hlsl"

TEXTURE2D(_VisibilityBuffer); SAMPLER(sampler_VisibilityBuffer);
TEXTURE2D(_DepthBuffer); SAMPLER(sampler_DepthBuffer);
TEXTURE2D(_BaseColorTexture); SAMPLER(sampler_BaseColorTexture);

Buffer<float> _VertexBuffer;
Buffer<int> _IndexBuffer;
Buffer<float> _ObjectToWorldMatrixBuffer;

float4x4 _ProjectionViewCombineMatrix;
float4 _CameraColorTextureSize;

struct BarycentricDeriv
{
    float3 lambda;
    float3 ddx;
    float3 ddy;
};

struct InterpolatedVector3
{
    float3 value;
    float3 dx;
    float3 dy;
};

struct InterpolatedVector2
{
    float2 value;
    float2 dx;
    float2 dy;
};

float3 CalculateWorldPos(uint vertexIdx, float4x4 objectToWorldMatrix)
{
    float3 position = float3(_VertexBuffer[vertexIdx], _VertexBuffer[vertexIdx + 1], _VertexBuffer[vertexIdx + 2]);
    return mul(objectToWorldMatrix, float4(position, 1.0)).xyz;
}

BarycentricDeriv CalcFullBary(float4 pt0, float4 pt1, float4 pt2, float2 pixelNdc, float2 winSize)
{
    BarycentricDeriv ret;

    float3 invW = rcp(float3(pt0.w, pt1.w, pt2.w));

    float2 ndc0 = pt0.xy * invW.x;
    float2 ndc1 = pt1.xy * invW.y;
    float2 ndc2 = pt2.xy * invW.z;

    float invDet = rcp(determinant(float2x2(ndc2 - ndc1, ndc0 - ndc1)));
    ret.ddx = float3(ndc1.y - ndc2.y, ndc2.y - ndc0.y, ndc0.y - ndc1.y) * invDet * invW;
    ret.ddy = float3(ndc2.x - ndc1.x, ndc0.x - ndc2.x, ndc1.x - ndc0.x) * invDet * invW;
    float ddxSum = dot(ret.ddx, float3(1, 1, 1));
    float ddySum = dot(ret.ddy, float3(1, 1, 1));

    float2 deltaVec = pixelNdc - ndc0;
    float interpInvW = invW.x + deltaVec.x * ddxSum + deltaVec.y * ddySum;
    float interpW = rcp(interpInvW);

    ret.lambda.x = interpW * (invW[0] + deltaVec.x * ret.ddx.x + deltaVec.y * ret.ddy.x);
    ret.lambda.y = interpW * (0.0f + deltaVec.x * ret.ddx.y + deltaVec.y * ret.ddy.y);
    ret.lambda.z = interpW * (0.0f + deltaVec.x * ret.ddx.z + deltaVec.y * ret.ddy.z);

    ret.ddx *= (2.0f / winSize.x);
    ret.ddy *= (2.0f / winSize.y);
    ddxSum *= (2.0f / winSize.x);
    ddySum *= (2.0f / winSize.y);

    ret.ddy *= -1.0f;
    ddySum *= -1.0f;

    float interpW_ddx = 1.0f / (interpInvW + ddxSum);
    float interpW_ddy = 1.0f / (interpInvW + ddySum);

    ret.ddx = interpW_ddx * (ret.lambda * interpInvW + ret.ddx) - ret.lambda;
    ret.ddy = interpW_ddy * (ret.lambda * interpInvW + ret.ddy) - ret.lambda;

    return ret;
}

float3 InterpolateWithDeriv(BarycentricDeriv deriv, float v0, float v1, float v2)
{
    float3 mergedV = float3(v0, v1, v2);
    float3 ret;
    ret.x = dot(mergedV, deriv.lambda);
    ret.y = dot(mergedV, deriv.ddx);
    ret.z = dot(mergedV, deriv.ddy);
    return ret;
}

InterpolatedVector2 Interpolate(BarycentricDeriv deriv, float2 v0, float2 v1, float2 v2)
{
    InterpolatedVector2 interp;
    float3 x = InterpolateWithDeriv(deriv, v0.x, v1.x, v2.x);
    interp.value.x = x.x;
    interp.dx.x = x.y;
    interp.dy.x = x.z;
    float3 y = InterpolateWithDeriv(deriv, v0.y, v1.y, v2.y);
    interp.value.y = y.x;
    interp.dx.y = y.y;
    interp.dy.y = y.z;
    return interp;
}

InterpolatedVector3 Interpolate(BarycentricDeriv deriv, float3 v0, float3 v1, float3 v2)
{
    InterpolatedVector3 interp;
    float3 x = InterpolateWithDeriv(deriv, v0.x, v1.x, v2.x);
    interp.value.x = x.x;
    interp.dx.x = x.y;
    interp.dy.x = x.z;
    float3 y = InterpolateWithDeriv(deriv, v0.y, v1.y, v2.y);
    interp.value.y = y.x;
    interp.dx.y = y.y;
    interp.dy.y = y.z;
    float3 z = InterpolateWithDeriv(deriv, v0.z, v1.z, v2.z);
    interp.value.z = z.x;
    interp.dx.z = z.y;
    interp.dy.z = z.z;
    return interp;
}

uint CustomAsUint(float x)
{
    return asuint(x);
}

float4 GenerateGBUffer(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    //float vertex = _VertexBuffer[0];
    //int index = _IndexBuffer[0];
    half4 visibilityBufferValHalf = _VisibilityBuffer.Sample(sampler_VisibilityBuffer, input.texcoord);
    //大坑，没法采样整数纹理，只能先采half4，然后转换成uint4，保证精度
    uint4 visibilityBufferVal = uint4(CustomAsUint(visibilityBufferValHalf.r), CustomAsUint(visibilityBufferValHalf.g),
                                      CustomAsUint(visibilityBufferValHalf.b), CustomAsUint(visibilityBufferValHalf.a));
    //float depth = SAMPLE_TEXTURE2D_X(_DepthBuffer, sampler_DepthBuffer, input.texcoord).r;
    //float sceneLinearDepth = LinearEyeDepth(depth, _ZBufferParams);
    uint subMeshStartIndex = visibilityBufferVal.w;
    uint triangleID = visibilityBufferVal.y;
    uint indexIdx = triangleID * 3 + subMeshStartIndex;
    uint3 vertexIdx = uint3(_IndexBuffer[indexIdx] * 12, _IndexBuffer[indexIdx + 1] * 12, _IndexBuffer[indexIdx + 2] * 12);
    float4x4 objectToWorldMatrix = float4x4(
        _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 1], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 2], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 3],
        _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 4], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 5], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 6], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 7],
        _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 8], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 9], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 10], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 11],
        _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 12], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 13], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 14], _ObjectToWorldMatrixBuffer[visibilityBufferVal.x * 16 + 15]
    );
    float3 posA = CalculateWorldPos(vertexIdx.x, objectToWorldMatrix);
    float3 posB = CalculateWorldPos(vertexIdx.y, objectToWorldMatrix);
    float3 posC = CalculateWorldPos(vertexIdx.z, objectToWorldMatrix);
    float2 ndc = input.texcoord * 2.0 - 1.0;
    BarycentricDeriv bary = CalcFullBary(
        mul(_ProjectionViewCombineMatrix, float4(posA, 1.0)),
        mul(_ProjectionViewCombineMatrix, float4(posB, 1.0)),
        mul(_ProjectionViewCombineMatrix, float4(posC, 1.0)),
        ndc,
        _CameraColorTextureSize.xy
    );
    InterpolatedVector3 worldPos = Interpolate(bary, posA, posB, posC);
    InterpolatedVector2 uv = Interpolate(
        bary,
        float2(_VertexBuffer[vertexIdx.x + 10], _VertexBuffer[vertexIdx.x + 11]),
        float2(_VertexBuffer[vertexIdx.y + 10], _VertexBuffer[vertexIdx.y + 11]),
        float2(_VertexBuffer[vertexIdx.z + 10], _VertexBuffer[vertexIdx.z + 11])
    );
    float3 normal = Interpolate(
        bary,
        normalize(float3(_VertexBuffer[vertexIdx.x + 3], _VertexBuffer[vertexIdx.x + 4], _VertexBuffer[vertexIdx.x + 5])),
        normalize(float3(_VertexBuffer[vertexIdx.x + 3], _VertexBuffer[vertexIdx.x + 4], _VertexBuffer[vertexIdx.x + 5])),
        normalize(float3(_VertexBuffer[vertexIdx.x + 3], _VertexBuffer[vertexIdx.x + 4], _VertexBuffer[vertexIdx.x + 5]))
    ).value;
    normal = normalize(mul(objectToWorldMatrix, float4(normal, 1.0)).xyz);
    float4 baseColor = SAMPLE_TEXTURE2D(_BaseColorTexture, sampler_BaseColorTexture, uv.value);
    return float4(baseColor.rgb, 1.0);
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