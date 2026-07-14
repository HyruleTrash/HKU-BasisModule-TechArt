using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
public class CustomVolumeManager : MonoBehaviour
{
    private static readonly int VolumesBufferID = Shader.PropertyToID("_Volumes");
    private static readonly int VolumeCountID = Shader.PropertyToID("_VolumeCount");

    private static CustomVolumeManager instance;
    public static CustomVolumeManager Instance
    {
        get
        {
            if (!instance) instance = FindFirstObjectByType<CustomVolumeManager>();
            return instance;
        }
        private set => instance = value;
    }

    private readonly SortedList<float, VolumeInstance> volumes = new();
    
    private float frameCount;
    private float maxFrameCount = 16;
    
    private ComputeBuffer volumeBuffer;
    private GPUVolumeData[] cpuBufferData;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct GPUVolumeData
    {
        public Matrix4x4 worldToLocal;
        public Vector4 minBounds;
        public Vector4 maxBounds;
        public float id;
        private float pad0, pad1, pad2; // padding for allignment
    }
    
    public class VolumeInstance
    {
        public int Id { get; private set; }
        public Transform Transform { get; private set; }
        public Bounds Bounds { get; private set; }
        private readonly CustomVolumeManager parentRef;
        private readonly CustomVolume volumeRef;

        public VolumeInstance(int id, Transform volumeTransform, Bounds bounds, CustomVolumeManager parentRef,
            CustomVolume volume)
        {
            this.Id = id;
            this.Transform = volumeTransform;
            this.Bounds = bounds;
            this.parentRef = parentRef;
            this.volumeRef = volume;
        }

        public void Update(int desiredId, Transform transform, Bounds bounds)
        {
            if (this.parentRef.CheckIdValid(desiredId))
            {
                this.parentRef.RemoveVolume(this);
                this.volumeRef.SetInstanceRef(null);
                return;
            }
            
            this.Id = desiredId;
            this.Transform = transform;
            this.Bounds = bounds;
        }
    }
    
    private void OnEnable()
    {       
        if (Instance && Instance != this) Destroy(Instance);
        Instance = this;
    }

    private void Awake() => this.volumes.Clear();
    
    private void OnDisable() => ReleaseBuffer();
    private void OnDestroy() => ReleaseBuffer();
    
    private void ReleaseBuffer()
    {
        if (this.volumeBuffer == null) return;
        this.volumeBuffer.Release();
        this.volumeBuffer = null;
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateVolumeList();
            UpdateShaderVariables();
            return;
        }
        
        if (this.frameCount >= this.maxFrameCount)
        {
            UpdateVolumeList();
            UpdateShaderVariables();
            this.frameCount = 0;
            return;
        }

        this.frameCount += Time.deltaTime;
    }

    private void UpdateVolumeList()
    {
        List<KeyValuePair<float, VolumeInstance>> newList = this.volumes.ToList();
        this.volumes.Clear();
        foreach (KeyValuePair<float, VolumeInstance> volume in newList) AddVolume(volume.Value);
    }

    private void UpdateShaderVariables()
    {
        int count = this.volumes.Count;
        if (count == 0)
        {
            Shader.SetGlobalInt(VolumeCountID, 0);
            return;
        }
        
        if (this.volumeBuffer == null || this.volumeBuffer.count != count)
        {
            ReleaseBuffer();
            this.volumeBuffer = new ComputeBuffer(count, System.Runtime.InteropServices.Marshal.SizeOf<GPUVolumeData>());
            this.cpuBufferData = new GPUVolumeData[count];
        }

        int index = 0;
        foreach (KeyValuePair<float, VolumeInstance> pair in this.volumes)
        {
            if (pair.Value == null) continue;

            this.cpuBufferData[index] = new GPUVolumeData
            {
                worldToLocal = pair.Value.Transform.worldToLocalMatrix,
                minBounds = pair.Value.Bounds.min,
                maxBounds = pair.Value.Bounds.max,
                id = pair.Value.Id
            };
            index++;
        }
        this.volumeBuffer.SetData(this.cpuBufferData);
        
        // Set global shader data
        Shader.SetGlobalBuffer(VolumesBufferID, this.volumeBuffer);
        Shader.SetGlobalInt(VolumeCountID, count);
    }

    public void AddVolume(CustomVolume volume, int desiredId)
    {
        if (!CheckIdValid(desiredId))
            return;

        VolumeInstance instance = new(desiredId, volume.transform, volume.GetBounds(), this, volume);
        AddVolume(instance);
        volume.SetInstanceRef(instance);
    }
    private void AddVolume(VolumeInstance volume) => this.volumes.Add(Vector3.Distance(volume.Transform.position, this.transform.position), volume);
    
    public void RemoveVolume(VolumeInstance volume)
    {
        int i = this.volumes.IndexOfValue(volume);
        if (i < 0) return;
        this.volumes.RemoveAt(i);
    }

    private bool ContainsId(int id) => this.volumes.FirstOrDefault(a => a.Value.Id == id).Value != null;

    private bool CheckIdValid(int id)
    {
        if (ContainsId(id))
        {
            // Debug.Log("Duplicate ID: " + id);
            return false;
        }

        if (id == -1)
        {
            Debug.Log("-1 is used for outside of all volumes, and so cannot be declared as a desired Id");
            return false;
        }
        
        return true;
    }

    public VolumeInstance GetInstance(int desiredId)
    {
        VolumeInstance foundInstance = this.volumes.FirstOrDefault(a => a.Value.Id == desiredId).Value;
        return foundInstance;
    }
}