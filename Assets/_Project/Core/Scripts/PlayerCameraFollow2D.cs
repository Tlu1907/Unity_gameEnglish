using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Centers the camera on the Player in XY while retaining its configured depth/zoom.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public sealed class PlayerCameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.1f;
    private Vector3 velocity;
    [SerializeField] private bool constrainToMap;
    [SerializeField] private Bounds mapBounds;
    private Camera view;
    private float preferredSize;
    public Transform Target => target;
    public Bounds MapBounds => mapBounds;
    private void Awake() { view = GetComponent<Camera>(); preferredSize = view.orthographicSize; }
    public void SetTarget(Transform player) { target = player; velocity = Vector3.zero; }
    public void SetMapBounds(Bounds bounds)
    {
        if (bounds.size.x <= 0f || bounds.size.y <= 0f) return;
        mapBounds = bounds; constrainToMap = true;
    }
    public bool DetectMapBounds()
    {
        // Include the station, stairs and bridges as well as the city ground.
        bool found = false;
        Bounds paintedBounds = default;
        foreach (var tilemap in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            if (tilemap.gameObject.scene != gameObject.scene || !tilemap.name.StartsWith("Terrain_") || tilemap.GetUsedTilesCount() == 0) continue;
            var renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null && renderer.bounds.size.x > 0f && renderer.bounds.size.y > 0f)
            {
                if (found) paintedBounds.Encapsulate(renderer.bounds);
                else { paintedBounds = renderer.bounds; found = true; }
            }
        }
        if (found) { SetMapBounds(paintedBounds); return true; }
        foreach (var sprite in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude))
            if (sprite.gameObject.scene == gameObject.scene && sprite.name == "MapBackground" && sprite.sprite != null)
            { SetMapBounds(sprite.bounds); return true; }
        return false;
    }
    private void ResolveTarget()
    {
        if (target != null && target.gameObject.scene == gameObject.scene && target.GetComponent<PlayerMovement>() != null) return;
        foreach (var player in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude))
            if (player.gameObject.scene == gameObject.scene) { SetTarget(player.transform); return; }
    }
    public Vector3 ClampToMap(Vector3 position)
    {
        if (!constrainToMap) return position;
        if (view == null) { view = GetComponent<Camera>(); preferredSize = view.orthographicSize; }
        float aspect = Mathf.Max(.01f, view.aspect);
        // Also handle windows wider/taller than the map, without exposing clear colour.
        view.orthographicSize = Mathf.Min(preferredSize, mapBounds.extents.y, mapBounds.extents.x / aspect);
        float halfHeight = view.orthographicSize, halfWidth = halfHeight * aspect;
        position.x = Mathf.Clamp(position.x, mapBounds.min.x + halfWidth, mapBounds.max.x - halfWidth);
        position.y = Mathf.Clamp(position.y, mapBounds.min.y + halfHeight, mapBounds.max.y - halfHeight);
        return position;
    }

    private void Start()
    {
        ResolveTarget();
        DetectMapBounds();
        if (target == null)
        {
            Debug.LogWarning("PlayerCameraFollow2D: assign the Player target.", this);
            return;
        }
        Focus();
    }
    private void LateUpdate()
    {
        ResolveTarget();
        if (target == null) return;
        Vector3 destination = ClampToMap(new Vector3(target.position.x, target.position.y, transform.position.z));
        transform.position = ClampToMap(smoothTime <= 0f ? destination
            : Vector3.SmoothDamp(transform.position, destination, ref velocity, smoothTime));
    }
    [ContextMenu("Focus on Player")]
    public void Focus()
    {
        if (target == null) return;
        transform.position = ClampToMap(new Vector3(target.position.x, target.position.y, transform.position.z));
        velocity = Vector3.zero;
    }
}
