using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A simple render pass that adds a post process shader for a toon look
/// </summary>
public class ToonRenderPass : ScriptableRenderPass
{
    private const string ToonPassName = "ToonRenderPass";
    private Material toonMaterial;
    
    // used to transfer data from render feature to render pass
    public void Setup(Material mat)
    {
        this.toonMaterial = mat;
        this.requiresIntermediateTexture = true;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        
        if (resourceData.isActiveTargetBackBuffer)
        {
            Debug.LogError($"Skipping render pass. ToonRenderPass requires an intermediate ColorTexture, we can't use the BackBuffer as a texture input.");
            return;
        }

        #region Applying shader

        TextureHandle source = resourceData.activeColorTexture;
        
        TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
        destinationDesc.name = $"CameraColor-{ToonPassName}";
        destinationDesc.clearBuffer = false;
        TextureHandle destination = renderGraph.CreateTexture(destinationDesc);
        
        RenderGraphUtils.BlitMaterialParameters para = new(source, destination, this.toonMaterial, 0);
        renderGraph.AddBlitPass(para, passName: ToonPassName);

        resourceData.cameraColor = destination;
        
        #endregion
    }
}