using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VisibilityBufferRendering : ScriptableRendererFeature
{
    public enum RenderQueueRangeEnum
    {
        Opaque = 0,
        Transparents,
        All
    }

    [System.Serializable]
    public class VisibilityBufferRenderingSettings
    {
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingTransparents;
        public RenderQueueRangeEnum renderQueue = RenderQueueRangeEnum.Opaque;
        public Texture2D baseColorTexture;
        public LayerMask layerMask = -1;
        public bool debug = false;
    }

    public VisibilityBufferRenderingSettings settings = new VisibilityBufferRenderingSettings();
    private VisibilityBufferRenderingPass visibilityBufferRenderingPass;
    private VisibilityBufferPrePass visibilityBufferPrePass;
    private RTHandle visibilityBufferHandle;
    private RenderTextureDescriptor descriptor;

    public ComputeBuffer vertexBuffer;
    public ComputeBuffer indexBuffer;
    public ComputeBuffer objectToWorldMatrixBuffer;

    public override void Create()
    {
        RenderQueueRange renderQueueRange = RenderQueueRange.all;
        if (settings.renderQueue == RenderQueueRangeEnum.Opaque)
        {
            renderQueueRange = RenderQueueRange.opaque;
        }
        else if (settings.renderQueue == RenderQueueRangeEnum.Transparents)
        {
            renderQueueRange = RenderQueueRange.transparent;
        }

        visibilityBufferPrePass = new VisibilityBufferPrePass(renderQueueRange, settings.layerMask)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses
        };
        visibilityBufferRenderingPass ??= new();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView)
        {
            return;
        }
        descriptor = renderingData.cameraData.cameraTargetDescriptor;
        if (settings.debug)
        {
            descriptor.colorFormat = RenderTextureFormat.ARGBFloat;
        }
        else
        {
            descriptor.colorFormat = RenderTextureFormat.RGBAUShort;
        }
        descriptor.sRGB = false;
        descriptor.enableRandomWrite = false;
        descriptor.bindMS = false;
        descriptor.msaaSamples = renderingData.cameraData.cameraTargetDescriptor.msaaSamples;
        descriptor.autoGenerateMips = false;
        descriptor.depthBufferBits = (int)DepthBits.None;
        RenderingUtils.ReAllocateIfNeeded(ref visibilityBufferHandle, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_VisibilityBuffer");
        visibilityBufferPrePass.Setup(visibilityBufferHandle, settings.debug);
        renderer.EnqueuePass(visibilityBufferPrePass);

        VisibilityBufferRenderingMgr.Instance.CreateBufferIfNeed(ref vertexBuffer, ref indexBuffer, ref objectToWorldMatrixBuffer);
        visibilityBufferRenderingPass.Setup(settings.Event, settings, (UniversalRenderer)renderer,
            visibilityBufferHandle, vertexBuffer, indexBuffer, objectToWorldMatrixBuffer, settings.debug);
        renderer.EnqueuePass(visibilityBufferRenderingPass);
    }
    protected override void Dispose(bool disposing)
    {
        visibilityBufferHandle?.Release();
        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();
        objectToWorldMatrixBuffer?.Dispose();
        visibilityBufferPrePass.Dispose();
        visibilityBufferRenderingPass.Dispose();
    }

    class VisibilityBufferPrePass : ScriptableRenderPass
    {
        private RTHandle visibilityBufferHandle;

        private FilteringSettings m_FilteringSettings;

        private Shader shader;
        private Material material;

        string m_ProfilerTag = "VisibilityBufferRendering";
        ShaderTagId m_ShaderTagId;
        int passIdx = 0;

        public VisibilityBufferPrePass(RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_ShaderTagId = new ShaderTagId("UniversalForward");
            shader = Shader.Find("Universal Render Pipeline/VisibilityShader");
        }

        public void Setup(RTHandle _visibilityBufferHandle, bool debug = false)
        {
            visibilityBufferHandle = _visibilityBufferHandle;
            passIdx = debug ? 1 : 0;
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(visibilityBufferHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            ConfigureClear(ClearFlag.All, Color.clear);
        }


        public void Dispose()
        {
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, sortingCriteria);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/VisibilityShader");
                if (shader == null)
                {
                    Debug.LogError("Shader not found!");
                    return;
                }
            }
            if (material == null)
            {
                material = CoreUtils.CreateEngineMaterial(shader);
                if (material == null)
                {
                    Debug.LogError("Failed to create material!");
                    return;
                }
            }
            drawingSettings.overrideMaterial = material;
            drawingSettings.overrideMaterialPassIndex = passIdx;

            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, new ProfilingSampler(m_ProfilerTag)))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                foreach (var obj in VisibilityBufferRenderingMgr.Instance.objects)
                {
                    cmd.DrawRenderer(obj.meshRenderer, obj.meshRenderer.sharedMaterial);
                }
                //context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);
                //cmd.SetGlobalTexture("_VisibilityBuffer", visibilityBufferHandle);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }

    }

    public class VisibilityBufferRenderingPass : ScriptableRenderPass
    {
        private const string CommandBufferTag = "VisibilityBufferRenderingPass";

        private Material material;

        private RTHandle renderTarget;
        private RTHandle visibilityBufferHandle;
        private RTHandle gBufferHandle;
        private int passIdx = 0;

        private UniversalRenderer renderer;

        private const string shaderName = "VisibilityBufferRenderingShader";

        public VisibilityBufferRenderingPass()
        {
            Shader vb = Shader.Find(shaderName);
            if (vb == null)
            {
                Debug.LogError("Shader找不到!");
                return;
            }
            material = CoreUtils.CreateEngineMaterial(vb);
        }

        public void Setup(RenderPassEvent renderPassEvent,
            VisibilityBufferRendering.VisibilityBufferRenderingSettings settings,
            UniversalRenderer renderer,
            RTHandle _visibilityBufferHandle,
            ComputeBuffer vertexBuffer,
            ComputeBuffer indexBuffer,
            ComputeBuffer objectToWorldMatrixBuffer,
            bool debug = false)
        {
            this.renderPassEvent = renderPassEvent;
            this.renderer = renderer;
            visibilityBufferHandle = _visibilityBufferHandle;

            ConfigureInput(ScriptableRenderPassInput.Color);
            ConfigureInput(ScriptableRenderPassInput.Depth);

            passIdx = debug ? 1 : 0;

            material.SetBuffer("_VertexBuffer", vertexBuffer);
            material.SetBuffer("_IndexBuffer", indexBuffer);
            material.SetBuffer("_ObjectToWorldMatrixBuffer", objectToWorldMatrixBuffer);
            material.SetTexture("_BaseColorTexture", settings.baseColorTexture);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            renderTarget = renderer.cameraColorTargetHandle;
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            //descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            descriptor.colorFormat = RenderTextureFormat.ARGB32;
            RenderingUtils.ReAllocateIfNeeded(ref gBufferHandle, descriptor, FilterMode.Bilinear, name: "gBuffer");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get(CommandBufferTag);
            using (new ProfilingScope(cmd, new ProfilingSampler(CommandBufferTag)))
            {
                CameraData cameraData = renderingData.cameraData;
                if (cameraData.isPreviewCamera)
                {
                    return;
                }

                //RTHandle cameraDepthTargetHandle = renderer.cameraDepthTargetHandle;
                material.SetTexture("_VisibilityBuffer", visibilityBufferHandle);
                Matrix4x4 projectionViewCombineMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true) * cameraData.GetViewMatrix();
                material.SetMatrix("_ProjectionViewCombineMatrix", projectionViewCombineMatrix);
                material.SetVector("_CameraColorTextureSize", new Vector4(renderTarget.rt.width, renderTarget.rt.height, 1.0f / renderTarget.rt.width, 1.0f / renderTarget.rt.height));
                Blitter.BlitCameraTexture(cmd, visibilityBufferHandle, renderTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, passIdx);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        public void Dispose()
        {
            visibilityBufferHandle?.Release();
        }
    }
}
