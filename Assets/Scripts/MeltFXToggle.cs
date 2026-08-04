using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeltFXToggle : MonoBehaviour
{
    private const string MeltShaderName = "Custom/MeltMatFX";
    private static Shader meltShader;
    private static readonly int ObjectSnapshotPropId = Shader.PropertyToID("object_snapshot");
    private static readonly int WorldCenterPropId = Shader.PropertyToID("world_center");
    private static readonly int WorldSizePropId = Shader.PropertyToID("world_size");
    private static readonly int UvMinPropId = Shader.PropertyToID("uv_min");
    private static readonly int UvMaxPropId = Shader.PropertyToID("uv_max");
    
    [SerializeField, HideInInspector]
    private MeshRenderer rendererComp;
    [SerializeField, HideInInspector]
    private MeshFilter meshFilterComp;
    
    [SerializeField] private Material meltMaterial;
    [SerializeField] private int isolationLayer = 31;
    
    [SerializeField, HideInInspector]
    private List<Material> originalMaterials = new();
    [SerializeField, HideInInspector]
    private Mesh originalMesh;

    private Material meltMaterialRuntime;
    private RenderTexture objectSnapshot;

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

    private static void InitCaptureCam()
    {
        if (captureCamObj) return;
        captureCamObj = new GameObject("TempCaptureCam");
        if (!captureCamObj) return;
        captureCam = captureCamObj.AddComponent<Camera>();
        captureCamObj.SetActive(false);
    }

    private void Start() => Invoke(nameof(TestToggle), 1f);
    private void TestToggle() => SetEffectActive(true);

    private void SetEffectActive(bool state)
    {
        if (!this.meltMaterial || !this.rendererComp || this.originalMaterials.Count == 0) return;

        if (state)
        {
            CaptureIsolatedObject();
            
            if (!this.meltMaterialRuntime) this.meltMaterialRuntime = new Material(this.meltMaterial);

            Bounds localBounds = this.meshFilterComp.sharedMesh.bounds;
            Vector3 extents = localBounds.extents;
            Vector3 center = localBounds.center;
            Vector3[] corners = new Vector3[8]
            {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x,  extents.y, -extents.z),
                center + new Vector3(extents.x,  extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y,  extents.z),
                center + new Vector3(extents.x, -extents.y,  extents.z),
                center + new Vector3(-extents.x,  extents.y,  extents.z),
                center + new Vector3(extents.x,  extents.y,  extents.z)
            };

            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            Vector3 worldCenter = localToWorld.MultiplyPoint(center);

            // 1. Calculate screen-space NDC bounds and sub-region UVs
            Matrix4x4 captureVP = GL.GetGPUProjectionMatrix(captureCam.projectionMatrix, false) * captureCam.worldToCameraMatrix;
            Matrix4x4 captureMVP = captureVP * localToWorld;

            Vector2 minNDC = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maxNDC = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < 8; i++)
            {
                Vector4 clipPos = captureMVP * new Vector4(corners[i].x, corners[i].y, corners[i].z, 1.0f);
                if (clipPos.w > 0.0001f)
                {
                    Vector2 ndc = new Vector2(clipPos.x / clipPos.w, clipPos.y / clipPos.w);
                    minNDC = Vector2.Min(minNDC, ndc);
                    maxNDC = Vector2.Max(maxNDC, ndc);
                }
            }

            Vector2 uvMin = (minNDC * 0.5f) + new Vector2(0.5f, 0.5f);
            Vector2 uvMax = (maxNDC * 0.5f) + new Vector2(0.5f, 0.5f);

            // 2. Fix positional offset by matching the quad center to the exact NDC bounding box center
            Vector2 centerNDC = (minNDC + maxNDC) * 0.5f;
            Vector4 centerClip = captureMVP * new Vector4(center.x, center.y, center.z, 1.0f);
            Vector2 boundsCenterNDC = (centerClip.w > 0.0001f) ? new Vector2(centerClip.x / centerClip.w, centerClip.y / centerClip.w) : centerNDC;
            Vector2 ndcOffset = centerNDC - boundsCenterNDC;

            float z = -captureCam.worldToCameraMatrix.MultiplyPoint(worldCenter).z;
            float factorY = captureCam.orthographic ? captureCam.orthographicSize : (z * Mathf.Tan(captureCam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float factorX = factorY * captureCam.aspect;

            Vector3 correctedWorldCenter = worldCenter + (ndcOffset.x * factorX) * captureCam.transform.right + (ndcOffset.y * factorY) * captureCam.transform.up;

            // 3. Derive precise world size from camera distance and NDC footprint span
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
            Vector2 worldSize = new Vector2(worldWidth, worldHeight);

            // 4. Apply properties to runtime material
            this.meltMaterialRuntime.SetTexture(ObjectSnapshotPropId, this.objectSnapshot);
            this.meltMaterialRuntime.SetVector(WorldCenterPropId, correctedWorldCenter);
            this.meltMaterialRuntime.SetVector(WorldSizePropId, worldSize);
            this.meltMaterialRuntime.SetVector(UvMinPropId, uvMin);
            this.meltMaterialRuntime.SetVector(UvMaxPropId, uvMax);
            
            this.rendererComp.material = this.meltMaterialRuntime;
            this.meshFilterComp.sharedMesh = quadMesh;
        }
        else
        {
            this.rendererComp.SetMaterials(this.originalMaterials);
            this.meshFilterComp.sharedMesh = this.originalMesh;
        }
    }
    
    private void CaptureIsolatedObject()
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

    private void OnDestroy()
    {
        if (this.meltMaterialRuntime) Destroy(this.meltMaterialRuntime);
        if (!captureCamObj) return; 
        Destroy(captureCamObj);
        captureCamObj = null;
        captureCam = null;
    }
}