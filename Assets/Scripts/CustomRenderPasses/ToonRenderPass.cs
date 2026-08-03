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
    private int kICountMethod; // KI stands for kernelIndex
    private int kIClearResultMethodTextureCount;
    
    private ComputeShader colorPalletComputeShader;
    private int kIGrowMethod;
    private int kICheckArgsMethod;
    private int kIResetArgsMethod;
    private int kIClearResultMethodColorPallet;
    private int kIClearUniqueColorBufferMethod;
    private int kIMarkUniqueColorsMethod;
    private int kISumUniqueColorsMethod;
    
    private RTHandle colorCountTextureHandle;
    private RTHandle colorPalletTextureHandle;
    private GraphicsBuffer colorPalletDataBuffer = new(GraphicsBuffer.Target.Structured, 4, sizeof(uint));
    private GraphicsBuffer bitmaskBuffer = new(GraphicsBuffer.Target.Structured, 524288, sizeof(uint)); // 255 ^ 255 ^ 255 bit count
    private int colorLimit;
    private int maxFloodStep;

    private static readonly int ColorCountTextureID = Shader.PropertyToID("color_count_texture");
    private static readonly int ColorPalletTextureID = Shader.PropertyToID("screen_color_pallet_texture");

    public void Setup(Material newToonMaterial, RTHandle newColorCountTextureHandle, RTHandle newColorPalletTextureHandle, int newColorLimit, int newMaxFloodStep)
    {
        this.toonMaterial = newToonMaterial;
        this.colorCountTextureHandle = newColorCountTextureHandle;
        this.colorPalletTextureHandle = newColorPalletTextureHandle;
        this.requiresIntermediateTexture = true;
        this.colorLimit = newColorLimit;
        this.maxFloodStep = newMaxFloodStep;
        
        LoadRequiredComponents();
    }

    private void LoadRequiredComponents()
    {
        this.textureColorCountComputeShader = Resources.Load<ComputeShader>("TextureColorCount");
        if (!this.textureColorCountComputeShader) return;
        this.kICountMethod = this.textureColorCountComputeShader.FindKernel("count");
        this.kIClearResultMethodTextureCount = this.textureColorCountComputeShader.FindKernel("clear_result");
        
        this.colorPalletComputeShader = Resources.Load<ComputeShader>("ColorPalletComputeShader");
        if (!this.colorPalletComputeShader) return;
        this.kIGrowMethod = this.colorPalletComputeShader.FindKernel("grow");
        this.kICheckArgsMethod = this.colorPalletComputeShader.FindKernel("check_args");
        this.kIResetArgsMethod = this.colorPalletComputeShader.FindKernel("reset_args");
        this.kIClearResultMethodColorPallet = this.colorPalletComputeShader.FindKernel("clear_result");
        this.kIClearUniqueColorBufferMethod = this.colorPalletComputeShader.FindKernel("clear_unique_colors");
        this.kIMarkUniqueColorsMethod = this.colorPalletComputeShader.FindKernel("mark_unique_colors");
        this.kISumUniqueColorsMethod = this.colorPalletComputeShader.FindKernel("sum_unique_colors");
        
        Shader.SetGlobalTexture(
            ColorCountTextureID,
            this.colorCountTextureHandle
        );
        
        Shader.SetGlobalTexture(
            ColorPalletTextureID,
            this.colorPalletTextureHandle
        );
    }
    
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        if (!AbleToRun(resourceData)) return;

        TextureHandle source = resourceData.activeColorTexture;
        TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);

        TextureHandle countResult = renderGraph.ImportTexture(this.colorCountTextureHandle);
        TextureHandle palletResult = renderGraph.ImportTexture(this.colorPalletTextureHandle);
        
        ClearColorCount(renderGraph, countResult);
        CountColors(renderGraph, source, countResult, sourceDesc);
        
        ClearPallet(renderGraph, countResult, palletResult);
        GrowCountResultToPalletLookUp(renderGraph, countResult, palletResult);

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

    private void ClearColorCount(RenderGraph renderGraph, TextureHandle countResult)
    {
        using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass("Clear color count", out TexCountComputePassData passData);
        passData.computeShader = this.textureColorCountComputeShader;
        passData.kernelIndex = this.kIClearResultMethodTextureCount;
        passData.result = countResult;
            
        builder.UseTexture(countResult, AccessFlags.Write);
            
        builder.SetRenderFunc((TexCountComputePassData data, ComputeGraphContext context) =>
        {
            context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, "result", data.result);
            context.cmd.DispatchCompute(data.computeShader, data.kernelIndex, 32, 32, 32); // 32 = 255 / 8 roughly
        });
    }

    private void CountColors(RenderGraph renderGraph, TextureHandle source, TextureHandle resultTex, TextureDesc sourceDesc)
    {
        using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass("Count screenTex Colors", out TexCountComputePassData passData);
        passData.computeShader = this.textureColorCountComputeShader;
        passData.kernelIndex = this.kICountMethod;
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
    
    private class PalletComputePassData
    {
        public ComputeShader computeShader;
        public TextureHandle valueLookup;
        public TextureHandle colorResult;
        public BufferHandle dataBuffer;
        public int colorLimit = 16;
        public int maxFloodStep = 128;
    }
    
    private void ClearPallet(RenderGraph renderGraph, TextureHandle countResult, TextureHandle palletResult)
    {
        using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass("Clear pallet Colors", out PalletComputePassData passData);
        passData.computeShader = this.colorPalletComputeShader;
        passData.valueLookup = countResult;
        passData.colorResult = palletResult;
            
        builder.UseTexture(countResult, AccessFlags.ReadWrite);
        builder.UseTexture(palletResult, AccessFlags.Write);
            
        builder.SetRenderFunc((PalletComputePassData data, ComputeGraphContext context) =>
        {
            context.cmd.SetComputeTextureParam(data.computeShader, this.kIClearResultMethodColorPallet, "value_lookup", data.valueLookup);
            context.cmd.SetComputeTextureParam(data.computeShader, this.kIClearResultMethodColorPallet, "color_result", data.colorResult);
            context.cmd.DispatchCompute(data.computeShader,  this.kIClearResultMethodColorPallet, 32, 32, 32);
        });
    }
    
    private void GrowCountResultToPalletLookUp(RenderGraph renderGraph, TextureHandle countResult, TextureHandle palletResult)
    {
        using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass("Clear pallet Colors", out PalletComputePassData passData);
        passData.computeShader = this.colorPalletComputeShader;
        passData.valueLookup = countResult;
        passData.colorResult = palletResult;
        passData.dataBuffer = renderGraph.ImportBuffer(this.colorPalletDataBuffer);
        passData.colorLimit = this.colorLimit;
        passData.maxFloodStep = this.maxFloodStep;
            
        builder.UseTexture(countResult, AccessFlags.ReadWrite);
        builder.UseTexture(palletResult, AccessFlags.ReadWrite);
        builder.UseBuffer(passData.dataBuffer, AccessFlags.ReadWrite);
            
        builder.SetRenderFunc((PalletComputePassData data, ComputeGraphContext context) =>
        {
            #region Linking methods and data

            context.cmd.SetComputeIntParam(data.computeShader, "width", 256);
            context.cmd.SetComputeIntParam(data.computeShader, "height", 256);
            context.cmd.SetComputeIntParam(data.computeShader, "depth", 256);
            context.cmd.SetComputeIntParam(data.computeShader, "color_limit", data.colorLimit);

            ConnectToKernel(this.kIResetArgsMethod, context, data);
            ConnectToKernel(this.kIGrowMethod, context, data);
            ConnectToKernel(this.kICheckArgsMethod, context, data);
            
            context.cmd.SetComputeBufferParam(data.computeShader, this.kIClearUniqueColorBufferMethod, "unique_colors_bitmask", this.bitmaskBuffer);
            context.cmd.SetComputeBufferParam(data.computeShader, this.kIMarkUniqueColorsMethod, "unique_colors_bitmask", this.bitmaskBuffer);
            context.cmd.SetComputeBufferParam(data.computeShader, this.kISumUniqueColorsMethod, "unique_colors_bitmask", this.bitmaskBuffer);
            
            ConnectToKernel(this.kIClearUniqueColorBufferMethod, context, data);
            ConnectToKernel(this.kIMarkUniqueColorsMethod, context, data);
            ConnectToKernel(this.kISumUniqueColorsMethod, context, data);

            #endregion
            
            context.cmd.DispatchCompute(data.computeShader, this.kIResetArgsMethod, 1, 1, 1);

            for (int step = 1; step < passData.maxFloodStep; step *= 2)
            {
                context.cmd.SetComputeIntParam(data.computeShader, "step_size", step);
                context.cmd.DispatchCompute(data.computeShader, this.kIGrowMethod, 32, 32, 32);
                context.cmd.DispatchCompute(data.computeShader, this.kIClearUniqueColorBufferMethod, 8192, 1, 1);
                context.cmd.DispatchCompute(data.computeShader, this.kIMarkUniqueColorsMethod, 32, 32, 32);
                context.cmd.DispatchCompute(data.computeShader, this.kISumUniqueColorsMethod, 8192, 1, 1);
                context.cmd.DispatchCompute(data.computeShader, this.kICheckArgsMethod, 1, 1, 1);
            }
        });
        return;

        void ConnectToKernel(int kernelId, ComputeGraphContext context, PalletComputePassData data)
        {
            context.cmd.SetComputeTextureParam(data.computeShader, kernelId, "color_result", data.colorResult);
            context.cmd.SetComputeTextureParam(data.computeShader, kernelId, "value_lookup", data.valueLookup);
            context.cmd.SetComputeBufferParam(data.computeShader, kernelId, "live_data", data.dataBuffer);
        }
    }

    private class ToonBlitPassData
    {
        public Material material;
        public TextureHandle source;
    }
    
    private void ApplyToonShader(RenderGraph renderGraph, TextureHandle source, TextureHandle destination)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(ToonPassName, out ToonBlitPassData passData);
        passData.material = this.toonMaterial;
        passData.source = source;

        builder.UseTexture(source, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

        builder.SetRenderFunc((ToonBlitPassData data, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0));
    }
    
    public void Dispose()
    {
        this.colorPalletDataBuffer?.Release();
        this.bitmaskBuffer?.Release();
    }
}