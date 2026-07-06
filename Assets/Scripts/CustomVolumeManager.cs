using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CustomVolumeManager : MonoBehaviour
{
    private static readonly int VolumeWorldToLocalMatrices = Shader.PropertyToID("_VolumeWorldToLocalMatrices");
    private static readonly int VolumeIDs = Shader.PropertyToID("_VolumeIDs");
    private static readonly int VolumeCount = Shader.PropertyToID("_VolumeCount");
    private static readonly int VolumeMins = Shader.PropertyToID("_VolumeMins");
    private static readonly int VolumeMaxs = Shader.PropertyToID("_VolumeMaxs");

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
    public int maxVolumeCount = 8;

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

    void FixedUpdate()
    {
        UpdateVolumeList();
        UpdateShaderVariables();
    }

    private void UpdateVolumeList()
    {
        List<KeyValuePair<float, VolumeInstance>> newList = this.volumes.ToList();
        this.volumes.Clear();
        foreach (KeyValuePair<float, VolumeInstance> volume in newList) AddVolume(volume.Value);
    }

    private void UpdateShaderVariables()
    {
        int count = Mathf.Min(this.volumes.Count, this.maxVolumeCount);
        
        Matrix4x4[] matrices = new Matrix4x4[count];
        float[] ids = new float[count];
        Vector4[] minBounds = new Vector4[count];
        Vector4[] maxBounds = new Vector4[count];

        int indexCount = 0;
        foreach (KeyValuePair<float, VolumeInstance> pair in this.volumes)
        {
            if (indexCount >= this.maxVolumeCount) break;
            if (pair.Value == null) continue;
            matrices[indexCount] = pair.Value.Transform.worldToLocalMatrix;
            ids[indexCount] = pair.Value.Id;
            minBounds[indexCount] = pair.Value.Bounds.min;
            maxBounds[indexCount] = pair.Value.Bounds.max;
            indexCount++;
        }
        
        // Set global array data
        Shader.SetGlobalMatrixArray(VolumeWorldToLocalMatrices, matrices);
        Shader.SetGlobalFloatArray(VolumeIDs, ids);
        Shader.SetGlobalVectorArray(VolumeMins, minBounds);
        Shader.SetGlobalVectorArray(VolumeMaxs, maxBounds);
        Shader.SetGlobalInt(VolumeCount, count);
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
    
    public void RemoveVolume(VolumeInstance volume) => this.volumes.RemoveAt(this.volumes.IndexOfValue(volume));
    
    private bool ContainsId(int id) => this.volumes.FirstOrDefault(a => a.Value.Id == id).Value != null;

    private bool CheckIdValid(int id)
    {
        if (ContainsId(id))
        {
            Debug.Log("Duplicate ID: " + id);
            return false;
        }

        if (id == -1)
        {
            Debug.Log("-1 is used for outside of all volumes, and so cannot be declared as a desired Id");
            return false;
        }
        
        return true;
    }
}