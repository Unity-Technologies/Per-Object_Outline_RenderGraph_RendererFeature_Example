using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class BlurredBufferMultiObjectOutlinePass : ScriptableRenderPass
{
    private const string DilationTex0Name = "_DilationTexture0";
    private const string DilationTex1Name = "_DilationTexture1";
    private const string DrawOutlineObjectsPassName = "DrawOutlineObjectsPass";
    private const string HorizontalPassName = "HorizontalDilationPass";
    private const string VerticalPassName = "VerticalDilationPass";

    public RenderPassEvent RenderEvent { private get; set; }
    public Material DilationMaterial { private get; set; }
    public Material OutlineMaterial { private get; set; }
    public Renderer[] Renderers { get; set; }

    private RenderTextureDescriptor _dilationDescriptor;

    public BlurredBufferMultiObjectOutlinePass()
    {
        _dilationDescriptor = new RenderTextureDescriptor(
            Screen.width,
            Screen.height,
            RenderTextureFormat.Default,
            depthBufferBits: 0);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph,
        ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();

        // The following line ensures that the render pass doesn't blit
        // from the back buffer.
        if (resourceData.isActiveTargetBackBuffer)
            return;

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        // Update Settings
        renderPassEvent = RenderEvent;

        // Set the dilation texture size to be the same as the camera target size.
        // depthBufferBits must be zero for color textures, and non-zero for depth textures
        // (it determines the texture format)
        _dilationDescriptor.width = cameraData.cameraTargetDescriptor.width;
        _dilationDescriptor.height = cameraData.cameraTargetDescriptor.height;
        _dilationDescriptor.msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;

        var dilation0Handle = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph,
            _dilationDescriptor,
            DilationTex0Name,
            clear: false);
        var dilation1Handle = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph,
            _dilationDescriptor,
            DilationTex1Name,
            clear: false);

        var screenColorHandle = resourceData.activeColorTexture;
        var screenDepthStencilHandle = resourceData.activeDepthTexture;

        // This check is to avoid an error from the material preview in the scene
        if (!screenColorHandle.IsValid() ||
            !screenDepthStencilHandle.IsValid() ||
            !dilation0Handle.IsValid() ||
            !dilation1Handle.IsValid())
            return;

        // Draw objects-to-outline pass
        using (var builder = renderGraph.AddRasterRenderPass<RenderObjectsPassData>(DrawOutlineObjectsPassName,
                   out var passData))
        {
            // Configure pass data
            passData.Renderers = Renderers;
            passData.Material = OutlineMaterial;

            // Draw to dilation0Handle
            builder.SetRenderAttachment(dilation0Handle, 0);

            // Blit from the source color to destination color,
            // using the first shader pass.
            builder.SetRenderFunc((RenderObjectsPassData data, RasterGraphContext context) =>
                ExecuteDrawOutlineObjects(data, context));
        }

        // Horizontal dilation pass
        using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(HorizontalPassName,
                   out var passData))
        {
            // Configure pass data
            passData.Source = dilation0Handle;
            passData.Material = DilationMaterial;

            // From dilation0Handle to dilation1Handle
            builder.UseTexture(passData.Source);
            builder.SetRenderAttachment(dilation1Handle, 0);

            // Blit from the source color to destination color,
            // using the first shader pass.
            builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                ExecuteBlit(data, context, 0));
        }

        // Vertical dilation pass
        using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(VerticalPassName, out var passData))
        {
            // Configure pass data
            passData.Source = dilation1Handle;
            passData.Material = DilationMaterial;

            // From dilation1Handle to screenColorHandle
            builder.UseTexture(passData.Source);
            builder.SetRenderAttachment(screenColorHandle, 0);

            // Make sure we also read from the active stencil buffer,
            // which was written to in the Draw objects-to-outline pass
            // and is used here to cut out the inside of the outline.
            builder.SetRenderAttachmentDepth(screenDepthStencilHandle, AccessFlags.Read);

            // Blit from the source color to destination (camera) color,
            // using the second shader pass.
            builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                ExecuteBlit(data, context, 1));
        }
    }

    private static void ExecuteDrawOutlineObjects(
        RenderObjectsPassData data,
        RasterGraphContext context)
    {
        // Render all the outlined objects to the temp texture
        foreach (Renderer objectRenderer in data.Renderers)
        {
            // Skip null renderers
            if (objectRenderer)
            {
                int materialCount = objectRenderer.sharedMaterials.Length;
                for (int i = 0; i < materialCount; i++)
                {
                    context.cmd.DrawRenderer(objectRenderer, data.Material, i, 0);
                }
            }
        }
    }

    private static void ExecuteBlit(BlitPassData data, RasterGraphContext context, int pass)
    {
        Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), data.Material, pass);
    }

    private class RenderObjectsPassData
    {
        internal Renderer[] Renderers;
        internal Material Material;
    }

    private class BlitPassData
    {
        internal TextureHandle Source;
        internal Material Material;
    }
}