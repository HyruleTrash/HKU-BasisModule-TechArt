using UnityEngine;

public static class CustomHelper
{
    /// <summary>
    /// Creates an array of local vector positions, these are the corners of the bounds
    /// Calculated by its size/extends
    /// </summary>
    public static Vector3[] GetCorners(this Bounds bounds)
    {
        Vector3 extents = bounds.extents;
        Vector3 center = bounds.center;
        return new []{
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x,  extents.y, -extents.z),
            center + new Vector3(extents.x,  extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y,  extents.z),
            center + new Vector3(extents.x, -extents.y,  extents.z),
            center + new Vector3(-extents.x,  extents.y,  extents.z),
            center + new Vector3(extents.x,  extents.y,  extents.z)
        };
    }
    
    private static Material blurMaterial;
    private static readonly int Offset = Shader.PropertyToID("_Offset");

    /// <summary>
    /// Blurs source texture and returns a blurred RenderTexture.
    /// </summary>
    public static RenderTexture Blur(this Texture sourceTex, int iterations = 3, float blurStep = 1.0f)
    {
        blurMaterial ??= new Material(Shader.Find("Hidden/SimpleBlur"));
        if (!sourceTex || !blurMaterial) return null;

        int width = sourceTex.width;
        int height = sourceTex.height;

        RenderTextureDescriptor desc = new(width, height, RenderTextureFormat.ARGB32, 0);
        RenderTexture rtA = RenderTexture.GetTemporary(desc);
        RenderTexture rtB = RenderTexture.GetTemporary(desc);
        rtA.filterMode = FilterMode.Bilinear;
        rtB.filterMode = FilterMode.Bilinear;

        // Create copy in buffer
        Graphics.Blit(sourceTex, rtA);

        // blur and swamp buffers, for x amount of iterations
        for (int i = 0; i < iterations; i++)
        {
            float offset = (i * blurStep) + 0.5f;
            blurMaterial.SetVector(Offset, new Vector2(offset, offset));
            Graphics.Blit(rtA, rtB, blurMaterial);
            (rtA, rtB) = (rtB, rtA);
        }

        // Create final renderTex
        RenderTexture finalResult = new(desc)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            depth = 24
        };
        finalResult.Create();
        Graphics.Blit(rtA, finalResult);

        // cleanup
        RenderTexture.ReleaseTemporary(rtA);
        RenderTexture.ReleaseTemporary(rtB);

        return finalResult;
    }
}