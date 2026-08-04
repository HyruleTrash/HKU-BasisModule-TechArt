using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This component allows a designer to trigger the melt effect on any mesh rendered object
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeltFXToggle : MonoBehaviour
{
    // shader data
    private const string MeltShaderName = "Custom/MeltMatFX";
    private static Shader meltShader;
    private static readonly int ObjectSnapshotPropId = Shader.PropertyToID("object_snapshot");
    private static readonly int WorldCenterPropId = Shader.PropertyToID("world_center");
    private static readonly int WorldSizePropId = Shader.PropertyToID("world_size");
    private static readonly int UvMinPropId = Shader.PropertyToID("uv_min");
    private static readonly int UvMaxPropId = Shader.PropertyToID("uv_max");
    
    // required components
    [SerializeField, HideInInspector]
    private MeshRenderer rendererComp;
    [SerializeField, HideInInspector]
    private MeshFilter meshFilterComp;
    
    // melt material
    [SerializeField] private Material meltMaterial;
    [SerializeField] private int isolationLayer = 31;
    
    // original renderer data before FX
    [SerializeField, HideInInspector]
    private List<Material> originalMaterials = new();
    [SerializeField, HideInInspector]
    private Mesh originalMesh;

    // melt FX runtime data
    private Material meltMaterialRuntime;
    private RenderTexture objectSnapshot;

    // reusable data
    private static Mesh quadMesh;
    private static GameObject captureCamObj;
    private static Camera captureCam;

    private void OnValidate()
    {
        if (Application.isPlaying) return;

        meltShader ??= Shader.Find(MeltShaderName);
        if (!meltShader)
        {
            Debug.LogError($"Shader Not Found for {GetType()} component", this);
            return;
        }
        
        if (!this.rendererComp) this.rendererComp = GetComponent<MeshRenderer>();
        if (!this.meshFilterComp) this.meshFilterComp = GetComponent<MeshFilter>();

        if (this.rendererComp)
        {
            this.originalMaterials ??= new List<Material>();
            this.rendererComp.GetSharedMaterials(this.originalMaterials);
        }
        
        if (this.meshFilterComp) this.originalMesh ??= this.meshFilterComp.sharedMesh;
        
        if (this.meltMaterial && this.meltMaterial.shader != meltShader) this.meltMaterial = null;
    }

    private void Awake()
    {
        if (this.rendererComp && (this.originalMaterials == null || this.originalMaterials.Count == 0))
        {
            this.originalMaterials = new List<Material>();
            this.rendererComp.GetSharedMaterials(this.originalMaterials);
        }

        if (this.meshFilterComp && !this.originalMesh) this.originalMesh = this.meshFilterComp.sharedMesh;
        
        if (!quadMesh)
        {
            GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempQuad);
        }

        InitCaptureCam();
    }
    
    private void Start()
    {
        Invoke(nameof(TestToggle), 1f);
        Invoke(nameof(TestToggleTwo), 5f);
    }

    private void TestToggle() => SetEffect(true);
    private void TestToggleTwo() => SetEffect(false);

    /// <summary>
    /// Sets the melt effect state
    /// </summary>
    /// <param name="state">true == melt effect, false == original visual</param>
    private void SetEffect(bool state)
    {
        if (!this.meltMaterial || !this.rendererComp || this.originalMaterials.Count == 0) return;

        if (state) TriggerMelt();
        else ResetToOriginalVisuals();
    }

    private void ResetToOriginalVisuals()
    {
        this.rendererComp.SetMaterials(this.originalMaterials);
        this.meshFilterComp.sharedMesh = this.originalMesh;
    }

    /// <summary>
    /// Sets the melt materials and calls calculation of needed variables
    /// </summary>
    private void TriggerMelt()
    {
        CreateSnapshotOfRender();
            
        if (!this.meltMaterialRuntime) this.meltMaterialRuntime = new Material(this.meltMaterial);

        CalculateUvData(out Vector2 uvMin, out Vector2 uvMax, out Vector3 correctedWorldCenter, out Vector2 worldSize);

        // set shader/material data
        this.meltMaterialRuntime.SetTexture(ObjectSnapshotPropId, this.objectSnapshot);
        this.meltMaterialRuntime.SetVector(WorldCenterPropId, correctedWorldCenter);
        this.meltMaterialRuntime.SetVector(WorldSizePropId, worldSize);
        this.meltMaterialRuntime.SetVector(UvMinPropId, uvMin);
        this.meltMaterialRuntime.SetVector(UvMaxPropId, uvMax);

        List<Material> a = new() { this.meltMaterialRuntime };
        this.rendererComp.SetMaterials(a);
        this.meshFilterComp.sharedMesh = quadMesh;
    }

    /// <summary>
    /// Calculates needed variables for mapping uv onto billboard quad.
    /// </summary>
    /// <param name="uvMin"></param>
    /// <param name="uvMax"></param>
    /// <param name="correctedWorldCenter"></param>
    /// <param name="worldSize"></param>
    private void CalculateUvData(out Vector2 uvMin, out Vector2 uvMax, out Vector3 correctedWorldCenter, out Vector2 worldSize)
    {
        Bounds localBounds = this.meshFilterComp!.sharedMesh.bounds;
        Vector3 extents = localBounds.extents;
        Vector3 center = localBounds.center;
        Vector3[] corners = {
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x,  extents.y, -extents.z),
            center + new Vector3(extents.x,  extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y,  extents.z),
            center + new Vector3(extents.x, -extents.y,  extents.z),
            center + new Vector3(-extents.x,  extents.y,  extents.z),
            center + new Vector3(extents.x,  extents.y,  extents.z)
        };

        Matrix4x4 localToWorld = this.transform.localToWorldMatrix;
        Vector3 worldCenter = localToWorld.MultiplyPoint(center);

        Matrix4x4 captureVp = GL.GetGPUProjectionMatrix(captureCam.projectionMatrix, false) * captureCam.worldToCameraMatrix;
        Matrix4x4 captureMvp = captureVp * localToWorld;

        Vector2 minNDC = new(float.MaxValue, float.MaxValue);
        Vector2 maxNDC = new(float.MinValue, float.MinValue);

        for (int i = 0; i < 8; i++)
        {
            Vector4 clipPos = captureMvp * new Vector4(corners[i].x, corners[i].y, corners[i].z, 1.0f);
            if (!(clipPos.w > 0.0001f)) continue;
            Vector2 ndc = new(clipPos.x / clipPos.w, clipPos.y / clipPos.w);
            minNDC = Vector2.Min(minNDC, ndc);
            maxNDC = Vector2.Max(maxNDC, ndc);
        }

        uvMin = (minNDC * 0.5f) + new Vector2(0.5f, 0.5f);
        uvMax = (maxNDC * 0.5f) + new Vector2(0.5f, 0.5f);

        Vector2 centerNDC = (minNDC + maxNDC) * 0.5f;
        Vector4 centerClip = captureMvp * new Vector4(center.x, center.y, center.z, 1.0f);
        Vector2 boundsCenterNDC = (centerClip.w > 0.0001f) ? new Vector2(centerClip.x / centerClip.w, centerClip.y / centerClip.w) : centerNDC;
        Vector2 ndcOffset = centerNDC - boundsCenterNDC;

        float z = -captureCam.worldToCameraMatrix.MultiplyPoint(worldCenter).z;
        float factorY = captureCam.orthographic ? captureCam.orthographicSize : (z * Mathf.Tan(captureCam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        float factorX = factorY * captureCam.aspect;

        correctedWorldCenter = worldCenter + (ndcOffset.x * factorX) * captureCam.transform.right + (ndcOffset.y * factorY) * captureCam.transform.up;

        float ndcWidth = maxNDC.x - minNDC.x;
        float ndcHeight = maxNDC.y - minNDC.y;

        float worldWidth, worldHeight;
        if (captureCam.orthographic)
        {
            float orthoSize = captureCam.orthographicSize;
            worldHeight = ndcHeight * orthoSize;
            worldWidth = ndcWidth * orthoSize * captureCam.aspect;
        }
        else
        {
            float tanFov = Mathf.Tan(captureCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            worldHeight = ndcHeight * z * tanFov;
            worldWidth = ndcWidth * z * tanFov * captureCam.aspect;
        }
        worldSize = new Vector2(worldWidth, worldHeight);
    }

    /// <summary>
    /// using a camera copy, that only renders the current render, sets the render texture to the camera output
    /// </summary>
    private void CreateSnapshotOfRender()
    {
        Camera mainCam = Camera.main;
        if (!mainCam) return;
        InitCaptureCam();
        captureCamObj.SetActive(true);

        // Init RenderTexture
        if (!this.objectSnapshot || this.objectSnapshot.width != Screen.width || this.objectSnapshot.height != Screen.height)
        {
            if (this.objectSnapshot) this.objectSnapshot.Release();
            this.objectSnapshot = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            this.objectSnapshot.Create();
        }

        captureCam.CopyFrom(mainCam);
        captureCam.targetTexture = this.objectSnapshot;
        captureCam.clearFlags = CameraClearFlags.SolidColor;
        captureCam.backgroundColor = new Color(0, 0, 0, 0);

        int originalLayer = this.rendererComp.gameObject.layer;
        this.rendererComp.gameObject.layer = this.isolationLayer;
        captureCam.cullingMask = 1 << this.isolationLayer; // only render the isolation layer

        captureCam.Render();
        captureCamObj.SetActive(false);
        
        this.rendererComp.gameObject.layer = originalLayer;
    }
    
    private static void InitCaptureCam()
    {
        if (captureCamObj) return;
        captureCamObj = new GameObject("TempCaptureCam");
        if (!captureCamObj) return;
        captureCam = captureCamObj.AddComponent<Camera>();
        captureCamObj.SetActive(false);
    }

    private void OnDestroy()
    {
        if (this.meltMaterialRuntime) Destroy(this.meltMaterialRuntime);
        if (!captureCamObj) return; 
        Destroy(captureCamObj);
        captureCamObj = null;
        captureCam = null;
    }
}