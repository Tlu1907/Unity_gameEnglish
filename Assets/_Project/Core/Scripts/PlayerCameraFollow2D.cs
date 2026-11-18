using UnityEngine;

/// <summary>Centers the camera on the Player in XY while retaining its configured depth/zoom.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PlayerCameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.1f;
    private Vector3 velocity;
    [SerializeField] private bool constrainToMap;
    [SerializeField] private Bounds mapBounds;
    private Camera view;
    private float preferredSize;
    public Bounds MapBounds => mapBounds;
    private void Awake() { view = GetComponent<Camera>(); preferredSize = view.orthographicSize; }
    public void SetMapBounds(Bounds bounds) { mapBounds = bounds; constrainToMap = true; }
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
        if (target == null)
        {
            Debug.LogWarning("PlayerCameraFollow2D: assign the Player target.", this);
            return;
        }
        Focus();
    }
    private void LateUpdate()
    {
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
