using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInputReader : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float speed = 5f;
    private GridMovement movement;
    public void Configure(float value) => speed = value;
    private void Start()
    {
        movement = GetComponent<GridMovement>();
        movement?.EnablePlayerPhysics();
    }
    private void Update()
    {
        if (movement == null) return;
        if (!GameplayState.CanAct) { movement.SetMovementInput(Vector3.zero, speed); return; }
        Vector3 direction = Vector3.zero;
        // Fixed priority is stable even when two perpendicular keys are held.
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction = Vector3.up;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction = Vector3.down;
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction = Vector3.left;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction = Vector3.right;
        movement.SetMovementInput(direction, speed);
    }
    private void OnDisable() => movement?.SetMovementInput(Vector3.zero, speed);
}
