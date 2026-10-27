using UnityEngine;
using UnityEngine.Tilemaps;

public class EnemyBrain : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject player;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Movement")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private int patrolRadius = 3;
    [SerializeField] private int chaseRangeTiles = 4;

    [Header("Optional")]
    [SerializeField] private bool useAnimator = true;
    [SerializeField] private string runParameterName = "IsRunning";
    [SerializeField, HideInInspector] private int migrationVersion;

    private GridMovement movement;

    private Vector3Int homeCell;
    private Vector3Int patrolCenterCell;

    protected virtual void Awake()
    {

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (groundTilemap == null)
        {
            groundTilemap = FindAnyObjectByType<Tilemap>();
        }

        EnsureComponents();
        movement = GetComponent<GridMovement>();
        if (groundTilemap == null) groundTilemap = movement.Ground;
        if (groundTilemap != null)
        {
            homeCell = groundTilemap.WorldToCell(transform.position);
            patrolCenterCell = homeCell;

        }
    }

    protected virtual void Update()
    {
        if (!GameplayState.CanAct)
            return;

        if (movement == null || movement.IsMoving || player == null || groundTilemap == null)
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
        Vector3 direction = groundTilemap.GetCellCenterWorld(targetCell) - transform.position;
        movement.TryMove(direction, moveSpeed);
    }

    // Existing components keep their own migrated configuration.
    public void EnsureComponents()
    {
        if (!TryGetComponent<CharacterAnimation>(out _))
            gameObject.AddComponent<CharacterAnimation>().Configure(useAnimator, runParameterName);
        if (!TryGetComponent<GridMovement>(out _))
            gameObject.AddComponent<GridMovement>().Configure(groundTilemap, obstacleMask, gridSize);
        if (!TryGetComponent<EnemyAttack>(out _)) gameObject.AddComponent<EnemyAttack>();
        GridMovement motor = GetComponent<GridMovement>();
        if (motor.Ground == null && groundTilemap != null) motor.Configure(groundTilemap, motor.Obstacles, motor.GridSize);
    }

    public bool Migrate(int version)
    {
        if (migrationVersion >= version) return false;
        EnsureComponents();
        GetComponent<GridMovement>().Configure(groundTilemap, obstacleMask, gridSize);
        GetComponent<CharacterAnimation>().Configure(useAnimator, runParameterName);
        migrationVersion = version;
        return true;
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
