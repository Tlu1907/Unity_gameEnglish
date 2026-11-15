using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EnemyBrain : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField, Min(.01f)] private float gridSize = 1f;
    [SerializeField, Min(.01f)] private float patrolSpeed = 2f;
    [SerializeField, Min(.01f)] private float chaseSpeed = 3.5f;
    [SerializeField, Min(1)] private int chaseRangeTiles = 3;
    [SerializeField] private bool useAnimator = true;
    [SerializeField] private string runParameterName = "IsRunning";
    [SerializeField, HideInInspector] private int migrationVersion;
    private GridMovement movement;
    private EnemyAttack attack;
    private Health target;
    private Vector3 origin;
    private bool initialized;
    private float retryAt;
    private Health health;
    private void OnEnable()
    {
        health = GetComponent<Health>();
        if (health != null) health.Died += Die;
    }
    private void OnDisable()
    {
        if (health != null) health.Died -= Die;
    }
    private void Die()
    {
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
    }
    private static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    protected virtual void Awake()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        EnsureComponents();
        movement = GetComponent<GridMovement>();
        attack = GetComponent<EnemyAttack>();
        GridMovement playerMotor = player != null ? player.GetComponent<GridMovement>() : null;
        if (playerMotor != null && movement.WalkableBackground == null)
            movement.SetWalkableBackground(playerMotor.WalkableBackground);
    }
    protected virtual void Update()
    {
        if (!initialized)
        {
            // All Start methods, including the motor's initial grid snap, have completed.
            origin = transform.position;
            gridSize = Mathf.Max(.01f, movement.GridSize);
            if (player != null)
            {
                target = player.GetComponent<Health>();
                GridMovement playerMotor = player.GetComponent<GridMovement>();
                if (playerMotor != null)
                {
                    if (movement.WalkableBackground == null)
                        movement.SetWalkableBackground(playerMotor.WalkableBackground);
                    if (movement.Ground == null)
                        movement.Configure(playerMotor.Ground, movement.Obstacles, gridSize);
                }
            }
            initialized = true;
        }
        if (!GameplayState.CanAct || movement.IsMoving || Time.time < retryAt) return;
        Vector2Int current = Cell(transform.position);
        if (target != null && !target.IsDead && player.activeInHierarchy &&
            Vector2.Distance(transform.position, player.transform.position) <= chaseRangeTiles * gridSize)
        {
            if (attack.IsInRange(target))
            {
                GetComponent<CharacterAnimation>()?.Face(player.transform.position - transform.position);
                attack.TryAttack(target);
                return;
            }
            MoveToward(current, Cell(player.transform.position), chaseSpeed);
            return;
        }
        if (!IsInsidePatrolArea(current))
        {
            MoveToward(current, Vector2Int.zero, patrolSpeed);
            return;
        }
        int start = Random.Range(0, Directions.Length);
        for (int i = 0; i < Directions.Length; i++)
        {
            Vector2Int next = current + Directions[(start + i) % Directions.Length];
            if (IsInsidePatrolArea(next) && movement.IsWalkable(World(next)) &&
                movement.TryMove(World(next) - transform.position, patrolSpeed)) return;
        }
        retryAt = Time.time + .25f;
    }
    // Exactly five cells on each axis, centered on the spawn cell.
    private bool IsInsidePatrolArea(Vector2Int cell) => Mathf.Abs(cell.x) <= 2 && Mathf.Abs(cell.y) <= 2;
    private Vector2Int Cell(Vector3 position) => new Vector2Int(
        Mathf.RoundToInt((position.x - origin.x) / gridSize),
        Mathf.RoundToInt((position.y - origin.y) / gridSize));
    private Vector3 World(Vector2Int cell) => origin + new Vector3(cell.x * gridSize, cell.y * gridSize, 0);
    private static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    private void MoveToward(Vector2Int current, Vector2Int goal, float speed)
    {
        // Bounded BFS finds routes around obstacles, both chasing and returning home.
        var queue = new Queue<Vector2Int>();
        var firstStep = new Dictionary<Vector2Int, Vector2Int>();
        queue.Enqueue(current);
        firstStep[current] = current;
        Vector2Int best = current;
        int bestDistance = Distance(current, goal);
        while (queue.Count > 0 && firstStep.Count < 1024)
        {
            Vector2Int cell = queue.Dequeue();
            if (cell == goal) { best = cell; break; }
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                if (firstStep.ContainsKey(next) || Mathf.Abs(next.x - current.x) > 16 ||
                    Mathf.Abs(next.y - current.y) > 16 || !movement.IsWalkable(World(next))) continue;
                firstStep[next] = cell == current ? next : firstStep[cell];
                queue.Enqueue(next);
                int distance = Distance(next, goal);
                if (distance < bestDistance) { best = next; bestDistance = distance; }
            }
        }
        if (best != current && movement.TryMove(World(firstStep[best]) - transform.position, speed)) return;
        retryAt = Time.time + .25f;
    }
    public void EnsureComponents()
    {
        if (!TryGetComponent<CharacterAnimation>(out _))
            gameObject.AddComponent<CharacterAnimation>().Configure(useAnimator, runParameterName);
        if (!TryGetComponent<GridMovement>(out _))
            gameObject.AddComponent<GridMovement>().Configure(groundTilemap, obstacleMask, gridSize);
        if (!TryGetComponent<EnemyAttack>(out _)) gameObject.AddComponent<EnemyAttack>();
        if (!TryGetComponent<Health>(out _)) gameObject.AddComponent<Health>().Configure(100, 100);
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
}
