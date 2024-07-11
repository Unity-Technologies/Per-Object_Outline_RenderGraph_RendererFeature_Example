using UnityEngine;

[ExecuteInEditMode]
public class SelectionManager : MonoBehaviour
{
    [SerializeField] private BlurredBufferMultiObjectOutlineRendererFeature outlineRendererFeature;
    [SerializeField] private Renderer[] selectedRenderers;

    [ContextMenu("Reassign Renderers Now")]
    private void OnValidate()
    {
        if (outlineRendererFeature)
            outlineRendererFeature.SetRenderers(selectedRenderers);
    }
}