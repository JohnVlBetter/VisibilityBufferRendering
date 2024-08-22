using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VisibilityBufferRendering : ScriptableRendererFeature
{
    public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingSkybox;

    class VisibilityBufferRenderPass : ScriptableRenderPass
    {
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            VisibilityObject.objects.ForEach(obj =>
            {

            });
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraPos = renderingData.cameraData.camera.transform.position;
            var cullingMask = renderingData.cameraData.camera.cullingMask;
            CommandBuffer cmd = CommandBufferPool.Get("");
            VisibilityObject.objects.ForEach(obj =>
            {
                cmd.DrawRenderer(obj.meshRenderer, obj.meshRenderer.material);
            });
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnFinishCameraStackRendering(CommandBuffer cmd)
        {
            VisibilityObject.objects.ForEach((obj) => { });
        }
    }

    VisibilityBufferRenderPass pass;


    public override void Create()
    {
        pass = new VisibilityBufferRenderPass();
        pass.renderPassEvent = passEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game) return;
        renderer.EnqueuePass(pass);
    }
}
