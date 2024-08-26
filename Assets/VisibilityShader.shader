Shader "Universal Render Pipeline/VisibilityShader"
{
    Properties { }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" "IgnoreProjector" = "True" "Queue" = "Geometry"}
        LOD 0

        Pass
        {
            Name "VisibilityBufferRendering"
            Tags { "LightMode" = "VisibilityBufferRendering" }

            HLSLPROGRAM
            #pragma target 5.0

            //--------------------------------------
            // GPU Instancing
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
                uint instanceID : SV_InstanceID;
                float4 positionCS : SV_POSITION;
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            float4 LitPassFragment(Varyings input, uint primitiveID : SV_PrimitiveID) : SV_Target
            {
                return float4(input.instanceID / 3, primitiveID / 10.0, 0, 1);
            }
            ENDHLSL
        }
    }
}
