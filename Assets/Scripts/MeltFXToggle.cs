using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeltFXToggle : MonoBehaviour
{
    private const string MeltShaderName = "Custom/MeltMatFX";
    private static Shader meltShader;
    private static readonly int ObjectSnapshotPropId = Shader.PropertyToID("object_snapshot");
    private static readonly int WorldCenterPropId = Shader.PropertyToID("world_center");
    private static readonly int WorldRadiusPropId = Shader.PropertyToID("world_radius");
    private static readonly int CaptureVpPropId = Shader.PropertyToID("capture_vp");
    
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

            // Calculate captured View-Projection matrix
            Matrix4x4 captureVP = GL.GetGPUProjectionMatrix(captureCam.projectionMatrix, false) * captureCam.worldToCameraMatrix;

            // Calculate fully scaled world center and bounding radius to prevent cut-offs on larger/scaled meshes
            Bounds localBounds = this.meshFilterComp.sharedMesh.bounds;
            Vector3 worldCenter = transform.TransformPoint(localBounds.center);
            float worldRadius = Vector3.Scale(localBounds.extents, transform.lossyScale).magnitude;

            this.meltMaterialRuntime.SetTexture(ObjectSnapshotPropId, this.objectSnapshot);
            this.meltMaterialRuntime.SetVector(WorldCenterPropId, worldCenter);
            this.meltMaterialRuntime.SetFloat(WorldRadiusPropId, worldRadius);
            this.meltMaterialRuntime.SetMatrix(CaptureVpPropId, captureVP);
            
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