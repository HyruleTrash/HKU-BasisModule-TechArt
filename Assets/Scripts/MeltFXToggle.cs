using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeltFXToggle : MonoBehaviour
{
    private const string MeltShaderName = "Custom/MeltMatFX";
    private static Shader meltShader;
    private static readonly int ObjectSnapshotPropId = Shader.PropertyToID("object_snapshot");
    private static readonly int BoundsCenter = Shader.PropertyToID("bounds_center");
    private static readonly int BoundsExtents = Shader.PropertyToID("bounds_extents");
    
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
        if (!this.rendererComp || (this.originalMaterials != null && this.originalMaterials.Count != 0)) return;
        this.originalMaterials = new List<Material>();
        this.rendererComp.GetSharedMaterials(this.originalMaterials);
        
        if (!quadMesh)
        {
            GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempQuad);
        }

        InitCaptureCam();
    }

    private void InitCaptureCam()
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

            this.meltMaterialRuntime.SetTexture(ObjectSnapshotPropId, this.objectSnapshot);
            this.meltMaterialRuntime.SetVector(BoundsCenter, this.meshFilterComp.sharedMesh.bounds.center);
            this.meltMaterialRuntime.SetVector(BoundsExtents, this.meshFilterComp.sharedMesh.bounds.extents);
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