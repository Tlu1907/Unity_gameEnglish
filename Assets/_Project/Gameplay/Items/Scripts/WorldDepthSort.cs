using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Depth belongs to each whole object; category layers sort its child renderers locally.</summary>
[ExecuteAlways]
public sealed class WorldDepthSort : MonoBehaviour
{
    public SpriteRenderer target;
    public Transform groundAnchor;
    [Tooltip("Foot offset in the ground anchor's local units; follows prefab scaling.")]
    public float footOffset;
    public SortingGroup group;
    public Collider2D feetCollider;
    public int orderOffset;
    public float GroundY
    {
        get
        {
            if(feetCollider!=null)
            {
                // Calculate box feet directly: prefab previews and newly moved objects may
                // not yet have a synchronized Physics2D bounds cache.
                if (feetCollider is BoxCollider2D box)
                {
                    Vector2 half = box.size * .5f;
                    float minimum = float.PositiveInfinity;
                    for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                    {
                        Vector2 corner = box.offset + new Vector2(x == 0 ? -half.x : half.x, y == 0 ? -half.y : half.y);
                        minimum = Mathf.Min(minimum, box.transform.TransformPoint(corner).y);
                    }
                    return minimum;
                }
                if(feetCollider.enabled && feetCollider.gameObject.activeInHierarchy)return feetCollider.bounds.min.y;
                Vector2 point=feetCollider.offset;
                return feetCollider.transform.TransformPoint(point).y;
            }
            Transform anchor = groundAnchor != null ? groundAnchor : transform;
            return anchor.TransformPoint(new Vector3(0f, footOffset, 0f)).y;
        }
    }
    public void Apply()
    {
        int order = Mathf.Clamp(Mathf.RoundToInt(-GroundY*100)*10+orderOffset,-30000,30000);
        if(group!=null)
        {
            group.sortingLayerName="WorldDepth";
            group.sortingOrder=order;
        }
        else if(target!=null)
        {
            target.sortingLayerName="WorldDepth";
            target.sortingOrder=order;
        }
    }
    private void LateUpdate()=>Apply();
}
