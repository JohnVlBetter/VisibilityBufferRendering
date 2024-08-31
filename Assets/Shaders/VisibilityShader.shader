Shader "Universal Render Pipeline/VisibilityShader"
{
    Properties { }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" "IgnoreProjector" = "True" "Queue" = "Geometry" }
        LOD 0

        Pass
        {
            Name "VisibilityBufferRendering"
            Tags { "LightMode" = "VisibilityBufferRendering" }

            HLSLPROGRAM
            #pragma target 5.0
            #pragma require integers
            #pragma enable_d3d11_debug_symbols 

            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerDraw)
                int _InstanceID;
                int _MaterialID;
                int _SubMeshStartIndex;
            CBUFFER_END

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            uint4 LitPassFragment(Varyings input, uint primitiveID : SV_PrimitiveID) : SV_Target
            {
                return uint4(_InstanceID, primitiveID, _MaterialID, _SubMeshStartIndex);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VisibilityBufferRenderingDebug"
            Tags { "LightMode" = "VisibilityBufferRenderingDebug" }

            HLSLPROGRAM
            #pragma target 5.0
            #pragma multi_compile_instancing
            #pragma enable_d3d11_debug_symbols 

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerDraw)
                int _InstanceID;
                int _MaterialID;
                int _SubMeshStartIndex;
            CBUFFER_END

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            float4 LitPassFragment(Varyings input, uint primitiveID : SV_PrimitiveID) : SV_Target
            {
                return float4(_InstanceID, primitiveID, _MaterialID, _SubMeshStartIndex);
            }
            ENDHLSL
        }
    }
}
