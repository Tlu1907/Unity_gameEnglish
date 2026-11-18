using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Semantic terrain painted in the scene, independent of the source map image.</summary>
public sealed class CityTerrain : MonoBehaviour
{
    public Tilemap stairs;
    public Tilemap bridges;
    public Tilemap[] surfaceLayers;
    // Player water admission is resolved by the fine physical collider, not a coarse navigation hole.
    public bool ContainsSurface(Vector3 feet)
    {
        if(surfaceLayers==null)return false;
        foreach(var layer in surfaceLayers)if(layer!=null && layer.HasTile(layer.WorldToCell(feet)))return true;
        return false;
    }
    [Range(.25f,1f)] public float stairSpeed=.72f;
    public bool IsStair(Vector3 feet)=>stairs!=null && stairs.HasTile(stairs.WorldToCell(feet));
    public bool IsBridge(Vector3 feet)=>bridges!=null && bridges.HasTile(bridges.WorldToCell(feet));
    public float SpeedMultiplier(Vector3 feet)=>IsStair(feet)?stairSpeed:1f;
}
