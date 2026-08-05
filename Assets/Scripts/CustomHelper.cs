using UnityEngine;

public static class CustomHelper
{
    /// <summary>
    /// Creates an array of local vector positions, that are the corners of the bounds
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
}