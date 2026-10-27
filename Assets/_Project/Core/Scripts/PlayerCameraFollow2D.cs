using UnityEngine;

/// <summary>Centers the camera on the Player in XY while retaining its configured depth/zoom.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PlayerCameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.1f;
    private Vector3 velocity;

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
        Vector3 destination = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = smoothTime <= 0f ? destination
            : Vector3.SmoothDamp(transform.position, destination, ref velocity, smoothTime);
    }
    [ContextMenu("Focus on Player")]
    public void Focus()
    {
        if (target == null) return;
        transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
        velocity = Vector3.zero;
    }
}
