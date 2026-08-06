using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This component allows a designer to trigger the melt effect on any mesh rendered object
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeltFXToggle : MonoBehaviour
{
    // required components, (hidden)
    [SerializeField, HideInInspector]
    private MeshRenderer rendererComp;
    [SerializeField, HideInInspector]
    private MeshFilter meshFilterComp;
    
    #region Serialized Fields
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
    #endregion

    // shader data
    private const string MeltShaderName = "Custom/MeltMatFX";
    private static Shader meltShader;
    private static readonly int ObjectSnapshotPropId = Shader.PropertyToID("object_snapshot");
    private static readonly int BoundsCenterPropId = Shader.PropertyToID("bounds_center");
    private static readonly int BoundsSizePropId = Shader.PropertyToID("bounds_size");
    private static readonly int UvMinPropId = Shader.PropertyToID("uv_min");
    private static readonly int UvMaxPropId = Shader.PropertyToID("uv_max");
    private static readonly int RngSeedPropId = Shader.PropertyToID("rng_seed");

    // reused data
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
            RegisterOriginalMaterials(true);
        
        if (this.meshFilterComp) 
            this.originalMesh ??= this.meshFilterComp.sharedMesh;
        
        if (this.meltMaterial && this.meltMaterial.shader != meltShader) 
            this.meltMaterial = null;
    }

    private void Awake()
    {
        if (this.rendererComp && (this.originalMaterials == null || this.originalMaterials.Count == 0))
            RegisterOriginalMaterials();

        if (this.meshFilterComp && !this.originalMesh) 
            this.originalMesh = this.meshFilterComp.sharedMesh;
        
        if (!quadMesh)
        {
            GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempQuad);
        }

        InitializeCaptureCam();
    }
    
    private void Start()
    {
        Invoke(nameof(TestToggle), 1f);
        // Invoke(nameof(TestToggleTwo), 5f);
    }

    private void TestToggle() => SetEffect(true);
    private void TestToggleTwo() => SetEffect(false);

    private void RegisterOriginalMaterials(bool onlyIfUnRegistered = false)
    {
        if (onlyIfUnRegistered)
            this.originalMaterials ??= new List<Material>();
        else
            this.originalMaterials = new List<Material>();
        this.rendererComp.GetSharedMaterials(this.originalMaterials);
    }

    /// <summary>
    /// Sets the melt effect state
    /// </summary>
    /// <param name="state">true == melt effect, false == original visual</param>
    private void SetEffect(bool state)
    {
        if (!this.meltMaterial || !this.rendererComp || this.originalMaterials.Count == 0) 
            return;

        if (state) 
            TriggerMelt();
        else 
            ResetToOriginalVisuals();
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
        // Sets current render texture to screenshot of entire screen
        CreateSnapshotOfObjectRender();
            
        if (!this.meltMaterialRuntime) this.meltMaterialRuntime = new Material(this.meltMaterial);

        // calculates needed data to crop empty space out, and only use space where the object actually is (2d bounds on screen)
        GetCroppedUvForCapturedObject(out Vector2 uvMin, out Vector2 uvMax, out Vector3 boundsCenter, out Vector2 boundsSize);

        // set shader/material data
        this.meltMaterialRuntime.SetTexture(ObjectSnapshotPropId, this.objectSnapshot);
        this.meltMaterialRuntime.SetVector(BoundsCenterPropId, boundsCenter);
        this.meltMaterialRuntime.SetVector(BoundsSizePropId, boundsSize);
        this.meltMaterialRuntime.SetVector(UvMinPropId, uvMin);
        this.meltMaterialRuntime.SetVector(UvMaxPropId, uvMax);
        this.meltMaterialRuntime.SetFloat(RngSeedPropId, Mathf.PerlinNoise(this.gameObject.GetInstanceID() * 0.12345f, 0f));

        List<Material> a = new() { this.meltMaterialRuntime };
        this.rendererComp.SetMaterials(a);
        this.meshFilterComp.sharedMesh = quadMesh;
    }

    /// <summary>
    /// Calculates needed variables for mapping uv onto billboard quad.
    /// </summary>
    /// <param name="uvMin">Minimum values for uv bounds, used for uv.x and uv.y</param>
    /// <param name="uvMax">Maximum values for uv bounds, used for uv.x and uv.y</param>
    /// <param name="centerOfBoundsInWorldFinal"></param>
    /// <param name="boundsSize">bounds within screen space, of the captured object</param>
    private void GetCroppedUvForCapturedObject(out Vector2 uvMin, out Vector2 uvMax, out Vector3 centerOfBoundsInWorldFinal, out Vector2 boundsSize)
    {
        Matrix4x4 localToWorld = this.transform.localToWorldMatrix;
        // prepare camera projection
        Matrix4x4 captureVp = GL.GetGPUProjectionMatrix(captureCam.projectionMatrix, false) * captureCam.worldToCameraMatrix;
        Matrix4x4 captureMvp = captureVp * localToWorld;
        
        // bound variables
        Bounds localBounds = this.meshFilterComp!.sharedMesh.bounds;
        Vector3[] corners = localBounds.GetCorners();
        Vector3 center = localBounds.center;

        Vector3 centerOfBoundsInWorld = localToWorld.MultiplyPoint(center);

        Vector2 minNDC = new(float.MaxValue, float.MaxValue);
        Vector2 maxNDC = new(float.MinValue, float.MinValue);

        // calculate the 2d bounds that the target object exists within the screen. using the object's 3d bounding box
        for (int i = 0; i < 8; i++)
        {
            // project corners to clip space, then to Normalized device coordinates (so normalized contained to aspect ratio)
            Vector4 clipPos = captureMvp * new Vector4(corners[i].x, corners[i].y, corners[i].z, 1.0f);
            if (!(clipPos.w > 0.0001f)) continue;
            Vector2 ndc = new(clipPos.x / clipPos.w, clipPos.y / clipPos.w);
            minNDC = Vector2.Min(minNDC, ndc);
            maxNDC = Vector2.Max(maxNDC, ndc);
        }

        // offset values from 0 to 1, so it aligns in uv space
        uvMin = (minNDC * 0.5f) + new Vector2(0.5f, 0.5f);
        uvMax = (maxNDC * 0.5f) + new Vector2(0.5f, 0.5f);

        // calculate offset, to align uv positions, according to camera parameters
        // these camera parameters impact the original object's position on screen
        Vector2 centerNDC = (minNDC + maxNDC) * 0.5f;
        Vector4 centerClip = captureMvp * new Vector4(center.x, center.y, center.z, 1.0f);
        Vector2 boundsCenterNDC = (centerClip.w > 0.0001f) ? new Vector2(centerClip.x / centerClip.w, centerClip.y / centerClip.w) : centerNDC;
        Vector2 ndcOffset = centerNDC - boundsCenterNDC;

        float z = -captureCam.worldToCameraMatrix.MultiplyPoint(centerOfBoundsInWorld).z;
        float factorY = captureCam.orthographic ? captureCam.orthographicSize : (z * Mathf.Tan(captureCam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        float factorX = factorY * captureCam.aspect;

        centerOfBoundsInWorldFinal = centerOfBoundsInWorld + (ndcOffset.x * factorX) * captureCam.transform.right + (ndcOffset.y * factorY) * captureCam.transform.up;

        // calculate the width and height of the 2d bounds the object takes up on screen
        float ndcWidth = maxNDC.x - minNDC.x;
        float ndcHeight = maxNDC.y - minNDC.y;

        float boundsWidth, boundsHeight;
        if (captureCam.orthographic)
        {
            float orthoSize = captureCam.orthographicSize;
            boundsHeight = ndcHeight * orthoSize;
            boundsWidth = ndcWidth * orthoSize * captureCam.aspect;
        }
        else
        {
            float tanFov = Mathf.Tan(captureCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            boundsHeight = ndcHeight * z * tanFov;
            boundsWidth = ndcWidth * z * tanFov * captureCam.aspect;
        }
        boundsSize = new Vector2(boundsWidth, boundsHeight);
    }

    /// <summary>
    /// using a camera copy, that only renders the current render, sets the render texture to the camera output
    /// </summary>
    private void CreateSnapshotOfObjectRender()
    {
        Camera mainCam = Camera.main;
        if (!mainCam) return;
        InitializeCaptureCam();
        
        captureCamObj.SetActive(true);

        // Init RenderTexture
        if (!this.objectSnapshot || this.objectSnapshot.width != Screen.width || this.objectSnapshot.height != Screen.height)
        {
            if (this.objectSnapshot) 
                this.objectSnapshot.Release();
            this.objectSnapshot = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            this.objectSnapshot.Create();
        }

        captureCam.CopyFrom(mainCam);
        captureCam.targetTexture = this.objectSnapshot;
        captureCam.clearFlags = CameraClearFlags.SolidColor;
        captureCam.backgroundColor = new Color(0, 0, 0, 0);

        // register old layer, and move to isolated layer so that only targeted object shows in snapshot
        int originalLayer = this.rendererComp.gameObject.layer;
        this.rendererComp.gameObject.layer = this.isolationLayer;
        captureCam.cullingMask = 1 << this.isolationLayer; // only render the isolation layer

        captureCam.Render();
        captureCamObj.SetActive(false);
        
        this.rendererComp.gameObject.layer = originalLayer;
    }
    
    /// <summary>
    /// Creates a gameObject with camera, for taking screen snapshots
    /// </summary>
    private static void InitializeCaptureCam()
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