using UnityEngine;

/// <summary>Optional one-shot Editor fitting. Authored collider shapes stay unchanged during gameplay.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TowerCollisionBounds : MonoBehaviour
{
    [SerializeField, Range(.1f, 1f)] private float baseWidthFraction = .8f;
    [SerializeField, Range(.05f, .5f)] private float baseHeightFraction = .15f;
    [SerializeField, Min(.1f)] private float interactionPadding = .65f;
    [SerializeField] private BoxCollider2D solidCollider;

    [ContextMenu("Fit Tower Colliders To Visuals")]
    public void RefreshColliders()
    {
        var trigger = GetComponent<BoxCollider2D>();
        if (trigger == null) return;
        if (solidCollider == null)
        {
            Transform child = transform.Find("Collision");
            if (child == null)
            {
                var go = new GameObject("Collision");
                go.transform.SetParent(transform, false);
                child = go.transform;
            }
            solidCollider = child.GetComponent<BoxCollider2D>();
            if (solidCollider == null) solidCollider = child.gameObject.AddComponent<BoxCollider2D>();
        }
        bool hasVisual = false;
        Bounds bounds = default;
        Transform visualRoot = transform.Find("Visual");
        foreach (var renderer in (visualRoot != null ? visualRoot : transform).GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null) continue;
            Bounds local = renderer.sprite.bounds;
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                {
                    Vector3 corner = new Vector3(x == 0 ? local.min.x : local.max.x, y == 0 ? local.min.y : local.max.y, 0);
                    Vector3 point = transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!hasVisual) { bounds = new Bounds(point, Vector3.zero); hasVisual = true; }
                    else bounds.Encapsulate(point);
                }
        }
        if (!hasVisual) bounds = new Bounds(Vector3.zero, new Vector3(2f, 1f, 0));
        float scaleX = Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.x));
        float scaleY = Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.y));
        float width = Mathf.Max(.3f / scaleX, bounds.size.x * baseWidthFraction);
        float height = Mathf.Clamp(bounds.size.y * baseHeightFraction, .3f / scaleY, .8f / scaleY);
        Vector2 center = new Vector2(bounds.center.x, bounds.min.y + height * .5f);
        solidCollider.transform.localPosition = Vector3.zero;
        solidCollider.transform.localRotation = Quaternion.identity;
        solidCollider.transform.localScale = Vector3.one;
        solidCollider.offset = center;
        solidCollider.size = new Vector2(width, height);
        solidCollider.isTrigger = false;
        solidCollider.enabled = true;
        int obstacle = LayerMask.NameToLayer("Obstacle");
        if (obstacle >= 0) solidCollider.gameObject.layer = obstacle;
        trigger.offset = center;
        trigger.size = solidCollider.size + new Vector2(2f * interactionPadding / scaleX, 2f * interactionPadding / scaleY);
        trigger.isTrigger = true;
        trigger.enabled = true;
    }
}
