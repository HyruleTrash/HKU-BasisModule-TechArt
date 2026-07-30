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
    
    private ComputeShader textureColorCountComputeShader;
    private int kernelIndexCount;
    private int kernelIndexClearResult;
    
    private RTHandle colorCountTextureHandle;
    
    private static readonly int ColorCountTextureID = Shader.PropertyToID("color_count_texture");

    public void Setup(Material newToonMaterial, RTHandle newColorCountTextureHandle)
    {
        this.toonMaterial = newToonMaterial;
        this.colorCountTextureHandle = newColorCountTextureHandle;
        this.requiresIntermediateTexture = true;
        
        this.textureColorCountComputeShader = Resources.Load<ComputeShader>("TextureColorCount");
        if (!this.textureColorCountComputeShader) return;
        this.kernelIndexCount = this.textureColorCountComputeShader.FindKernel("count");
        this.kernelIndexClearResult = this.textureColorCountComputeShader.FindKernel("clear_result");
        
        Shader.SetGlobalTexture(
            ColorCountTextureID,
            this.colorCountTextureHandle
        );
    }
    
    private class TexCountComputePassData
    {
        public ComputeShader computeShader;
        public int kernelIndex;
        public TextureHandle result;
        public TextureHandle source;
        public int threadGroupsX;
        public int threadGroupsY;
        public int threadGroupsZ;
    }
    
    private class ToonBlitPassData
    {
        public Material material;
        public TextureHandle source;
    }
    
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        if (!AbleToRun(resourceData)) return;

        TextureHandle source = resourceData.activeColorTexture;
        TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);

        TextureHandle countResult = renderGraph.ImportTexture(this.colorCountTextureHandle);
        ClearColorCount(renderGraph, countResult);
        CountColors(renderGraph, source, countResult, sourceDesc);

        TextureDesc finalResultDesc = sourceDesc;
        finalResultDesc.name = $"CameraColor-{ToonPassName}";
        finalResultDesc.clearBuffer = false;
        TextureHandle finalResult = renderGraph.CreateTexture(finalResultDesc);

        ApplyToonShader(renderGraph, source, finalResult);
        resourceData.cameraColor = finalResult;
    }
    
    /// <returns>false when toon shader shouldnt run</returns>
    private bool AbleToRun(UniversalResourceData resourceData)
    {
        if (!this.textureColorCountComputeShader)
        {
            Debug.LogError("ComputeShader 'textureColorCountComputeShader' was not found inside a Resources folder!");
            return false;
        }

        if (resourceData.isActiveTargetBackBuffer)
        {
            Debug.LogError($"Skipping render pass. ToonRenderPass requires an intermediate ColorTexture, we can't use the BackBuffer as a texture input.");
            return false;
        }

        return true;
    }

    private void ClearColorCount(RenderGraph renderGraph, TextureHandle countResult)
    {
        using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass("Clear Toon Colors", out TexCountComputePassData passData);
        passData.computeShader = this.textureColorCountComputeShader;
        passData.kernelIndex = this.kernelIndexClearResult;
        passData.result = countResult;
        passData.threadGroupsX = 32;
        passData.threadGroupsY = 32;
        passData.threadGroupsZ = 32;
            
        builder.UseTexture(countResult, AccessFlags.Write);
            
        builder.SetRenderFunc((TexCountComputePassData data, ComputeGraphContext context) =>
        {
            context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, "result", data.result);
            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.threadGroupsX, data.threadGroupsY, data.threadGroupsZ);
        });
    }

    private void CountColors(RenderGraph renderGraph, TextureHandle source, TextureHandle resultTex, TextureDesc sourceDesc)
    {
        using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass("Count Toon Colors", out TexCountComputePassData passData);
        passData.computeShader = this.textureColorCountComputeShader;
        passData.kernelIndex = this.kernelIndexCount;
        passData.source = source;
        passData.result = resultTex;
        passData.threadGroupsX = Mathf.CeilToInt(sourceDesc.width / 8.0f);
        passData.threadGroupsY = Mathf.CeilToInt(sourceDesc.height / 8.0f);
            
        builder.UseTexture(source, AccessFlags.Read);
        builder.UseTexture(resultTex, AccessFlags.ReadWrite);
            
        builder.SetRenderFunc((TexCountComputePassData data, ComputeGraphContext context) =>
        {
            context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, "source", data.source);
            context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex,"result", data.result);
            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, data.threadGroupsX, data.threadGroupsY, 1);
        });
    }
    
    private void ApplyToonShader(RenderGraph renderGraph, TextureHandle source, TextureHandle destination)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(ToonPassName, out ToonBlitPassData passData);
        passData.material = this.toonMaterial;
        passData.source = source;

        builder.UseTexture(source, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

        builder.SetRenderFunc((ToonBlitPassData data, RasterGraphContext context) =>
        {
            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
        });
    }
}