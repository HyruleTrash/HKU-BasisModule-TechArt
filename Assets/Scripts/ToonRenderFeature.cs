using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ToonRenderFeature : ScriptableRendererFeature
{
    private Material toonMat;
    private ToonRenderPass toonRenderPass;
    
    private RTHandle colorCountTextureHandle;
    private RTHandle colorPalletTextureHandle;

    /// <summary>
    /// Runs:
    /// When the Scriptable Renderer Feature loads the first time.
    /// When you enable or disable the Scriptable Renderer Feature.
    /// When you change a property in the Inspector window of the Renderer Feature.
    /// </summary>
    public override void Create()
    {
        Dispose();
        
        this.toonRenderPass = new ToonRenderPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing // Inject the render pass after rendering the skybox
        };
        
        this.toonMat = CoreUtils.CreateEngineMaterial(Shader.Find("ToonPostProcess"));
        ReallocateTextures();
    }
    
    private void ReallocateTextures()
    {
        this.colorCountTextureHandle ??= RTHandles.Alloc(
            width: 256,
            height: 256,
            slices: 256,
            colorFormat: GraphicsFormat.R32_UInt,
            dimension: TextureDimension.Tex3D,
            enableRandomWrite: true,
            useMipMap: false,
            autoGenerateMips: false,
            name: "ColorCountResult_Persistent"
        );
        
        this.colorPalletTextureHandle ??= RTHandles.Alloc(
            width: 256,
            height: 256,
            slices: 256,
            colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
            dimension: TextureDimension.Tex3D,
            enableRandomWrite: true,
            useMipMap: false,
            autoGenerateMips: false,
            name: "ColorPalletResult_Persistent"
        );
    }

    // every frame, once for each camera. Don’t create or instantiate any resources within this method.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!this.toonMat)
        {
            Debug.LogWarning(this.name + " material is null and will be skipped.");
            return;
        }
        this.toonRenderPass.Setup(this.toonMat, this.colorCountTextureHandle, this.colorPalletTextureHandle);
        
        if (renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(this.toonRenderPass);
    }

    // to dispose of any resources after render feature is gone
    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(this.toonMat);
        this.colorCountTextureHandle?.Release();
        this.colorCountTextureHandle = null;
        this.colorPalletTextureHandle?.Release();
        this.colorPalletTextureHandle = null;
        base.Dispose(disposing);
    }
}
