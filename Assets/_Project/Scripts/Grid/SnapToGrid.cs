using UnityEngine;
using UnityEngine.Tilemaps;

public class SnapToGrid : MonoBehaviour
{
    public Tilemap referenceTilemap;

    [ContextMenu("Snap To Grid")]
    void Snap()
    {
        if (referenceTilemap == null)
        {
            Debug.LogWarning("Chưa gán referenceTilemap!");
            return;
        }

        Vector3Int cell = referenceTilemap.WorldToCell(transform.position);
        transform.position = referenceTilemap.GetCellCenterWorld(cell);
    }
}