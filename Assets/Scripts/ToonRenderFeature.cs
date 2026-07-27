using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ToonRenderFeature : ScriptableRendererFeature
{
    private Material toonMat;
    private ToonRenderPass toonRenderPass;

    /// <summary>
    /// Runs:
    /// When the Scriptable Renderer Feature loads the first time.
    /// When you enable or disable the Scriptable Renderer Feature.
    /// When you change a property in the Inspector window of the Renderer Feature.
    /// </summary>
    public override void Create()
    {
        this.toonRenderPass = new ToonRenderPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing // Inject the render pass after rendering the skybox
        };

        this.toonMat = new Material(Shader.Find("ToonPostProcess"));
    }

    // every frame, once for each camera. Don’t create or instantiate any resources within this method.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!this.toonMat)
        {
            Debug.LogWarning(this.name + " material is null and will be skipped.");
            return;
        }
        this.toonRenderPass.Setup(this.toonMat);
        
        if (renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(this.toonRenderPass);
    }

    // to dispose of any resources after render feature is gone
    protected override void Dispose(bool disposing)
    {
        DestroyImmediate(this.toonMat);
        base.Dispose(disposing);
    }
}
