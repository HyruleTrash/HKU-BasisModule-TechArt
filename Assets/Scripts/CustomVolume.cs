using System;
using UnityEngine;

public class CustomVolume : MonoBehaviour
{
    [SerializeField] private int desiredId = 0;
    [SerializeField] private Bounds volume;
    [SerializeField] private Color debugColor;

    private CustomVolumeManager.VolumeInstance instanceRef;
    
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (!CustomVolumeManager.Instance)
        {
            Debug.LogWarning($"CustomVolumeManager is required for {GetType().Name} - {this.gameObject.name} to function.");
            return;
        }
        
        this.instanceRef ??= CustomVolumeManager.Instance.GetInstance(this.desiredId);
        if (this.instanceRef == null) CustomVolumeManager.Instance.AddVolume(this, this.desiredId);

        CustomVolumeManager.VolumeInstance volumeInstance = this.instanceRef;
        if (volumeInstance != null) volumeInstance.Update(this.desiredId, this.transform, this.volume);
        else Debug.Log("aa");
    }
    private void Start()
    {
        this.instanceRef = null;
        CustomVolumeManager.Instance.AddVolume(this, this.desiredId);
    }

    private void OnDrawGizmos()
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;

        // Set Gizmos matrix to volume's position, rotation, and scale
        Gizmos.matrix = this.transform.localToWorldMatrix;
        Gizmos.matrix *= Matrix4x4.TRS(this.volume.center, Quaternion.identity, Vector3.one);

        Gizmos.color = this.debugColor;
        Gizmos.DrawCube(Vector3.zero, this.volume.size);
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(Vector3.zero, this.volume.size);

        Gizmos.matrix = oldMatrix;
    }

    private void OnDisable()
    {
        if (this.instanceRef != null) CustomVolumeManager.Instance.RemoveVolume(this.instanceRef);
    }

    public Bounds GetBounds() => this.volume;
    public void SetInstanceRef(CustomVolumeManager.VolumeInstance instance) => this.instanceRef = instance;
}
