using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float gridSize = 1f;
    public Tilemap groundTilemap;
    public LayerMask obstacleMask; // set = layer "Obstacle" chứa Chest trong Inspector

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isMoving = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        // Snap Player vào đúng tâm ô Tilemap khi bắt đầu
        Vector3Int cell = groundTilemap.WorldToCell(transform.position);
        transform.position = groundTilemap.GetCellCenterWorld(cell);
    }

    void Update()
    {
        if (isMoving) return;

        Vector3 direction = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            direction = Vector3.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            direction = Vector3.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            direction = Vector3.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            direction = Vector3.right;

        if (direction != Vector3.zero)
        {
            if (direction.x > 0) spriteRenderer.flipX = false;
            else if (direction.x < 0) spriteRenderer.flipX = true;

            Vector3 targetPosition = transform.position + direction * gridSize;

            // Check xem ô đích có vật cản (Chest) không
            Collider2D obstacle = Physics2D.OverlapBox(targetPosition, new Vector2(0.8f, 0.8f), 0f, obstacleMask);

            if (obstacle != null)
            {
                // Bị chặn -> không di chuyển, nhưng nếu là Chest thì mở luôn
                if (obstacle.CompareTag("Chest"))
                {
                    Chest chest = obstacle.GetComponent<Chest>();
                    if (chest != null) chest.Open();
                }
                // Nếu là vật cản khác (không phải chest) thì chỉ đơn giản không làm gì, bị chặn lại
            }
            else if (IsTileWalkable(targetPosition))
            {
                StartCoroutine(MoveOneTile(direction));
            }
        }
    }

    bool IsTileWalkable(Vector3 worldPosition)
    {
        Vector3Int cellPosition = groundTilemap.WorldToCell(worldPosition);
        return groundTilemap.HasTile(cellPosition);
    }

    IEnumerator MoveOneTile(Vector3 direction)
    {
        isMoving = true;
        animator.SetBool("IsRunning", true);

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + direction * gridSize;
        float distance = 0f;

        while (distance < gridSize)
        {
            float step = speed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);
            distance += step;
            yield return null;
        }

        transform.position = targetPosition;
        animator.SetBool("IsRunning", false);
        isMoving = false;
    }
}