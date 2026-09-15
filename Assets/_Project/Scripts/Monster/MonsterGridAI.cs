using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MonsterGridAI : MonoBehaviour
{
    [Header("References")]
    public GameObject player;
    public Tilemap groundTilemap;
    public LayerMask obstacleMask;

    [Header("Movement")]
    public float gridSize = 1f;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 3.5f;
    public int patrolRadius = 3;
    public int chaseRangeTiles = 4;

    [Header("Optional")]
    public bool useAnimator = true;
    public string runParameterName = "IsRunning";

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isMoving = false;
    private Vector3Int homeCell;
    private Vector3Int patrolCenterCell;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (groundTilemap == null)
        {
            groundTilemap = FindAnyObjectByType<Tilemap>();
        }

        if (groundTilemap != null)
        {
            homeCell = groundTilemap.WorldToCell(transform.position);
            patrolCenterCell = homeCell;
            transform.position = groundTilemap.GetCellCenterWorld(homeCell);
        }
    }

    private void Update()
    {
        if (QuestionManager.IsQuestionOpen)
            return;

        if (MonsterFreezeController.IsFrozen)
            return;

        if (isMoving || player == null || groundTilemap == null)
            return;

        if (IsPlayerInChaseRange())
        {
            ChasePlayer();
        }
        else
        {
            PatrolRandom();
        }
    }

    private bool IsPlayerInChaseRange()
    {
        Vector3 delta = player.transform.position - transform.position;
        return Mathf.Abs(delta.x) <= chaseRangeTiles * gridSize && Mathf.Abs(delta.y) <= chaseRangeTiles * gridSize;
    }

    private void PatrolRandom()
    {
        Vector3Int currentCell = groundTilemap.WorldToCell(transform.position);

        if (!IsInsidePatrolArea(currentCell))
        {
            Vector3Int towardCenter = GetStepTowardTarget(currentCell, patrolCenterCell);
            if (towardCenter != currentCell && IsTileWalkable(towardCenter))
            {
                MoveTowardCell(towardCenter, patrolSpeed);
                return;
            }
        }

        Vector3Int[] directions =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0)
        };

        for (int i = 0; i < 12; i++)
        {
            Vector3Int dir = directions[Random.Range(0, directions.Length)];
            Vector3Int candidate = currentCell + dir;

            if (IsInsidePatrolArea(candidate) && IsTileWalkable(candidate))
            {
                MoveTowardCell(candidate, patrolSpeed);
                return;
            }
        }

        foreach (var dir in directions)
        {
            Vector3Int candidate = currentCell + dir;
            if (IsTileWalkable(candidate))
            {
                MoveTowardCell(candidate, patrolSpeed);
                return;
            }
        }

        if (currentCell != patrolCenterCell && IsTileWalkable(patrolCenterCell))
        {
            MoveTowardCell(patrolCenterCell, patrolSpeed);
        }
    }

    private bool IsInsidePatrolArea(Vector3Int cell)
    {
        return Mathf.Abs(cell.x - patrolCenterCell.x) <= patrolRadius &&
               Mathf.Abs(cell.y - patrolCenterCell.y) <= patrolRadius;
    }

    private Vector3Int GetStepTowardTarget(Vector3Int from, Vector3Int target)
    {
        Vector3Int step = from;

        if (from.x < target.x) step.x += 1;
        else if (from.x > target.x) step.x -= 1;
        else if (from.y < target.y) step.y += 1;
        else if (from.y > target.y) step.y -= 1;

        return step;
    }

    private void ChasePlayer()
    {
        Vector3Int monsterCell = groundTilemap.WorldToCell(transform.position);
        Vector3Int playerCell = groundTilemap.WorldToCell(player.transform.position);

        Vector3Int bestCell = monsterCell;
        float bestDistance = float.MaxValue;

        Vector3Int[] directions =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0)
        };

        foreach (Vector3Int dir in directions)
        {
            Vector3Int candidate = monsterCell + dir;
            if (!IsTileWalkable(candidate))
                continue;

            float dist = Mathf.Abs(candidate.x - playerCell.x) + Mathf.Abs(candidate.y - playerCell.y);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestCell = candidate;
            }
        }

        if (bestCell != monsterCell)
        {
            MoveTowardCell(bestCell, chaseSpeed);
        }
    }

    private void MoveTowardCell(Vector3Int targetCell, float moveSpeed)
    {
        Vector3 targetWorld = groundTilemap.GetCellCenterWorld(targetCell);
        Vector3 direction = targetWorld - transform.position;

        if (direction == Vector3.zero)
            return;

        direction = direction.normalized;

        if (direction.x > 0f)
            spriteRenderer.flipX = false;
        else if (direction.x < 0f)
            spriteRenderer.flipX = true;

        StartCoroutine(MoveOneTile(direction, moveSpeed));
    }

    private IEnumerator MoveOneTile(Vector3 direction, float moveSpeed)
    {
        isMoving = true;

        if (useAnimator && animator != null)
            animator.SetBool(runParameterName, true);

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + direction * gridSize;
        float distance = 0f;

        while (distance < gridSize)
        {
            float step = moveSpeed * Time.deltaTime;
            if (step > gridSize - distance)
                step = gridSize - distance;

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);
            distance += step;
            yield return null;
        }

        transform.position = targetPosition;

        if (useAnimator && animator != null)
            animator.SetBool(runParameterName, false);

        isMoving = false;
    }

    private Vector3Int GetRandomCardinalStep()
    {
        int randomIndex = Random.Range(0, 4);

        switch (randomIndex)
        {
            case 0: return Vector3Int.up;
            case 1: return Vector3Int.down;
            case 2: return Vector3Int.left;
            default: return Vector3Int.right;
        }
    }

    private bool IsTileWalkable(Vector3Int cellPosition)
    {
        if (!groundTilemap.HasTile(cellPosition))
            return false;

        return !IsObstacleAtCell(cellPosition);
    }

    private bool IsObstacleAtCell(Vector3Int cellPosition)
    {
        Vector3 worldPosition = groundTilemap.GetCellCenterWorld(cellPosition);
        Collider2D obstacle = Physics2D.OverlapBox(worldPosition, new Vector2(0.8f, 0.8f), 0f, obstacleMask);
        return obstacle != null;
    }
}
