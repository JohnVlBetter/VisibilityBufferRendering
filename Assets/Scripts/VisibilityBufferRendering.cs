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
        public Shader shader;
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingTransparents;
        public RenderQueueRangeEnum renderQueue = RenderQueueRangeEnum.Opaque;
        public LayerMask layerMask = -1;
    }

    public VisibilityBufferRenderingSettings settings = new VisibilityBufferRenderingSettings();
    private VisibilityBufferRenderingPass visibilityBufferRenderingPass;
    private VisibilityBufferPrePass drawObjectsPass;

    public override void Create()
    {
        var type = SystemInfo.graphicsDeviceType;
        RenderQueueRange renderQueueRange = RenderQueueRange.all;
        if (settings.renderQueue == RenderQueueRangeEnum.Opaque)
        {
            renderQueueRange = RenderQueueRange.opaque;
        }
        else if (settings.renderQueue == RenderQueueRangeEnum.Transparents)
        {
            renderQueueRange = RenderQueueRange.transparent;
        }
        drawObjectsPass = new VisibilityBufferPrePass(renderQueueRange, settings.layerMask)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses
        };


        //visibilityBufferRenderingPass ??= new();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView) return;
        drawObjectsPass.Setup(renderingData.cameraData.cameraTargetDescriptor);
        renderer.EnqueuePass(drawObjectsPass);
        //visibilityBufferRenderingPass.Setup(settings.Event, settings, (UniversalRenderer)renderer);
        //renderer.EnqueuePass(visibilityBufferRenderingPass);
    }
    protected override void Dispose(bool disposing)
    {
        drawObjectsPass.Dispose();
        //visibilityBufferRenderingPass.Dispose();
    }

    class VisibilityBufferPrePass : ScriptableRenderPass
    {
        private RTHandle[] attachmentHandle;

        internal RenderTextureDescriptor descriptor;

        private FilteringSettings m_FilteringSettings;

        string m_ProfilerTag = "VisibilityBufferRendering";
        ShaderTagId m_ShaderTagId;

        public VisibilityBufferPrePass(RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_ShaderTagId = new ShaderTagId("VisibilityBufferRendering");
            attachmentHandle = new RTHandle[1];
        }
        public void Setup(RenderTextureDescriptor baseDescriptor)
        {
            descriptor = baseDescriptor;
            descriptor.colorFormat = RenderTextureFormat.ARGB32;
            descriptor.sRGB = false;
            descriptor.enableRandomWrite = false;
            descriptor.bindMS = false;
            descriptor.msaaSamples = baseDescriptor.msaaSamples;
            descriptor.autoGenerateMips = false;
            descriptor.depthBufferBits = (int)DepthBits.None;
            RenderingUtils.ReAllocateIfNeeded(ref attachmentHandle[0], descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_VisibilityBuffer");
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(attachmentHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            ConfigureClear(ClearFlag.All, Color.clear);
        }


        public void Dispose()
        {
            foreach (var handle in attachmentHandle)
            {
                handle?.Release();
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, new ProfilingSampler(m_ProfilerTag)))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);
                cmd.SetGlobalTexture("_VisibilityBuffer", attachmentHandle[0]);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }

    }

    public class VisibilityBufferRenderingPass : ScriptableRenderPass
    {
        private const string CommandBufferTag = "VisibilityBufferRenderingPass";

        private static readonly ProfilingSampler m_CBSampler = new ProfilingSampler(CommandBufferTag);

        private Material material;

        private RTHandle renderTarget;
        private RTHandle tmpHandle;

        private UniversalRenderer renderer;

        private const string shaderName = "VisibilityBufferRenderingShader";

        public void Setup(RenderPassEvent renderPassEvent,
            VisibilityBufferRendering.VisibilityBufferRenderingSettings settings,
            UniversalRenderer renderer)
        {
            this.renderPassEvent = renderPassEvent;
            this.renderer = renderer;

            Shader vb = Shader.Find(shaderName);
            if (vb == null)
            {
                Debug.LogError("Shader找不到!");
                return;
            }
            material = CoreUtils.CreateEngineMaterial(vb);
            ConfigureInput(ScriptableRenderPassInput.Color);
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            renderTarget = renderer.cameraColorTargetHandle;
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref tmpHandle, descriptor, FilterMode.Point, name: "tmpHandle");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get(CommandBufferTag);
            using (new ProfilingScope(cmd, m_CBSampler))
            {

                CameraData cameraData = renderingData.cameraData;
                if (cameraData.isPreviewCamera)
                {
                    return;
                }

                RTHandle cameraDepthTargetHandle = renderer.cameraDepthTargetHandle;
                Blitter.BlitCameraTexture(cmd, tmpHandle, renderTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        public void Dispose()
        {
            tmpHandle?.Release();
        }
    }
}
