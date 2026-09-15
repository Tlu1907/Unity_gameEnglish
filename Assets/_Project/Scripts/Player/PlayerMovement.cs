using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public enum PlayerItemOption
{
    None,
    Weapon
}

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float gridSize = 1f;
    public Tilemap groundTilemap;
    public LayerMask obstacleMask; // set = layer "Obstacle" chứa Chest trong Inspector
    public PlayerItemOption currentItem = PlayerItemOption.None;

    [Header("Health")]
    public int maxHealth = 200;
    public int currentHealth = 200;
    public int monsterDamage = 20;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isMoving = false;

    private void Awake()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

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
        if (QuestionManager.IsQuestionOpen)
            return;

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

            MirrorTreasureItem mirror = FindMirrorAtPosition(targetPosition);
            if (mirror != null && IsTileWalkable(targetPosition))
            {
                StartCoroutine(MoveOneTile(direction, mirror));
                return;
            }

            Collider2D obstacle = Physics2D.OverlapBox(targetPosition, new Vector2(0.8f, 0.8f), 0f, obstacleMask);

            if (obstacle != null)
            {
                if (obstacle.CompareTag("Chest"))
                {
                    Chest chest = obstacle.GetComponent<Chest>();
                    if (chest != null) chest.Open();
                    return;
                }

                return;
            }

            if (IsTileWalkable(targetPosition))
            {
                StartCoroutine(MoveOneTile(direction));
            }
        }
    }

    private MirrorTreasureItem FindMirrorAtPosition(Vector3 worldPosition)
    {
        Collider2D[] colliders = Physics2D.OverlapBoxAll(worldPosition, new Vector2(0.8f, 0.8f), 0f);

        for (int i = 0; i < colliders.Length; i++)
        {
            MirrorTreasureItem mirror = colliders[i].GetComponentInParent<MirrorTreasureItem>();
            if (mirror != null)
                return mirror;
        }

        return null;
    }

    bool IsTileWalkable(Vector3 worldPosition)
    {
        Vector3Int cellPosition = groundTilemap.WorldToCell(worldPosition);
        return groundTilemap.HasTile(cellPosition);
    }

    public bool CanDefeatMonster()
    {
        return currentItem != PlayerItemOption.None;
    }

    public void TakeDamage()
    {
        currentHealth -= monsterDamage;
        Debug.Log("Player bị quái đánh, máu còn: " + currentHealth + "/" + maxHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    public void Die()
    {
        Debug.Log("Player chết!");
        gameObject.SetActive(false);
    }

    IEnumerator MoveOneTile(Vector3 direction, MirrorTreasureItem mirror = null)
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

        if (mirror != null)
            mirror.TryActivate();
    }
}