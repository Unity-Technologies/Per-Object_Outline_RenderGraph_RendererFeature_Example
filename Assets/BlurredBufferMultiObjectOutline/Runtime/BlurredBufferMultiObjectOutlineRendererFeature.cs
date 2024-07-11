using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BlurredBufferMultiObjectOutlineRendererFeature : ScriptableRendererFeature
{
    private static readonly int SpreadId = Shader.PropertyToID("_Spread");

    [SerializeField] private RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;
    [Space, SerializeField] private Material dilationMaterial;
    [SerializeField] private Material outlineMaterial;
    [SerializeField, Range(1, 60)] private int spread = 15;

    private BlurredBufferMultiObjectOutlinePass _outlinePass;

    private Renderer[] _targetRenderers;

    public void SetRenderers(Renderer[] targetRenderers)
    {
        _targetRenderers = targetRenderers;

        if (_outlinePass != null)
            _outlinePass.Renderers = _targetRenderers;
    }

    public override void Create()
    {
        name = "Multi-Object Outliner";

        // Pass in constructor variables which don't/shouldn't need to be updated every frame.
        _outlinePass = new BlurredBufferMultiObjectOutlinePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_outlinePass == null)
            return;

        if (!dilationMaterial ||
            !outlineMaterial ||
            _targetRenderers == null ||
            _targetRenderers.Length == 0)
        {
            // Don't render the effect if there's nothing to render
            return;
        }

        // Any variables you may want to update every frame should be set here.
        _outlinePass.RenderEvent = renderEvent;
        _outlinePass.DilationMaterial = dilationMaterial;
        dilationMaterial.SetInteger("_Spread", spread);
        _outlinePass.OutlineMaterial = outlineMaterial;
        _outlinePass.Renderers = _targetRenderers;

        renderer.EnqueuePass(_outlinePass);
    }
}