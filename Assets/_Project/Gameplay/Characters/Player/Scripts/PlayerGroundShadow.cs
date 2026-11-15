using UnityEngine;

/// <summary>A stable ground oval, independent of animation frame sizes and Visual flipping.</summary>
[DisallowMultipleComponent]
public sealed class PlayerGroundShadow : MonoBehaviour
{
    [Tooltip("Head width in Player local units. Zero estimates it from the initial sprite width.")]
    [SerializeField, Min(0f)] private float headWidth;
    [SerializeField, Range(.6f, .75f)] private float widthRelativeToHead = .675f;
    [SerializeField, Range(.25f, .3f)] private float heightRelativeToWidth = .275f;
    [SerializeField] private Color shadowColor = new Color(32f / 255f, 38f / 255f, 44f / 255f, .2f);
    [Tooltip("Adjust the shadow relative to its initial position at the soles, in Player local units.")]
    [SerializeField] private Vector2 footOffset;
    private GameObject shadow;
    private Mesh mesh;
    private Material material;
    private MeshRenderer shadowRenderer;
    private SpriteRenderer visual;
    private Vector3 anchor;
    private float referenceHeadWidth;

    private void Start()
    {
        // An authored shadow is edited directly in the Hierarchy; preserve all its settings.
        if (transform.Find("GroundShadow") != null) return;
        visual = GetComponent<CharacterAnimation>()?.Animator?.GetComponent<SpriteRenderer>();
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        if (visual == null) return;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) { Debug.LogWarning("Player shadow: Sprites/Default shader unavailable.", this); return; }

        // Capture once so differently cropped walk frames never resize or bounce the shadow.
        Collider2D feet = GetComponent<Collider2D>();
        Bounds bounds = feet != null ? feet.bounds : visual.bounds;
        anchor = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, transform.position.z));
        referenceHeadWidth = visual.bounds.size.x * .6f / Mathf.Max(.0001f, Mathf.Abs(transform.lossyScale.x));
        shadow = new GameObject("GroundShadow");
        shadow.transform.SetParent(transform, false);
        mesh = CreateOval();
        shadow.AddComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(shader) { name = "Player Ground Shadow" };
        shadowRenderer = shadow.AddComponent<MeshRenderer>();
        shadowRenderer.sharedMaterial = material;
        shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
        Apply();
    }

    private void LateUpdate() { if (shadow != null) Apply(); }
    private void Apply()
    {
        float width = (headWidth > 0 ? headWidth : referenceHeadWidth) * widthRelativeToHead;
        shadow.transform.localPosition = anchor + (Vector3)footOffset;
        shadow.transform.localScale = new Vector3(width, width * heightRelativeToWidth, 1);
        shadowRenderer.sortingLayerID = visual.sortingLayerID;
        shadowRenderer.sortingOrder = visual.sortingOrder - 1;
        shadowRenderer.enabled = visual.enabled;
        material.color = shadowColor;
    }

    private static Mesh CreateOval()
    {
        const int segments = 64;
        var vertices = new Vector3[segments + 1];
        var colors = new Color[vertices.Length];
        var triangles = new int[segments * 3];
        colors[0] = Color.white;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .5f;
            colors[i + 1] = Color.white;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }
        var result = new Mesh { name = "Player Shadow Oval" };
        result.vertices = vertices;
        result.colors = colors;
        result.triangles = triangles;
        result.RecalculateBounds();
        return result;
    }

    private void OnEnable() { if (shadow != null) shadow.SetActive(true); }
    private void OnDisable() { if (shadow != null) shadow.SetActive(false); }
    private void OnDestroy()
    {
        if (shadow != null) Destroy(shadow);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
