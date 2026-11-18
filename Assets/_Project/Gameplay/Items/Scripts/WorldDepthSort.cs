using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Depth belongs to each whole object; category layers sort its child renderers locally.</summary>
[ExecuteAlways]
public sealed class WorldDepthSort : MonoBehaviour
{
    public SpriteRenderer target;
    public Transform groundAnchor;
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
                if(feetCollider.enabled && feetCollider.gameObject.activeInHierarchy)return feetCollider.bounds.min.y;
                Vector2 point=feetCollider.offset;
                if(feetCollider is BoxCollider2D box)point.y-=box.size.y*.5f;
                return feetCollider.transform.TransformPoint(point).y;
            }
            return (groundAnchor!=null?groundAnchor.position.y:transform.position.y)+footOffset;
        }
    }
    public void Apply()
    {
        if(group!=null)
        {
            group.sortingLayerName="WorldDepth";
            group.sortingOrder=Mathf.Clamp(Mathf.RoundToInt(-GroundY*100)*10+orderOffset,-30000,30000);
        }
        else if(target!=null)target.sortingOrder=Mathf.RoundToInt(-GroundY*20);
    }
    private void LateUpdate()=>Apply();
}
