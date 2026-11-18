using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum EnemyBehaviour { Chaser, Patrol, Guard }

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
    [SerializeField] private EnemyBehaviour behaviour;
    [SerializeField] private Vector2[] patrolOffsets = new Vector2[0];
    [SerializeField, Min(1)] private int patrolRadius = 2;
    [SerializeField, Min(0)] private float loseSightSeconds = 3f;
    private int waypoint;
    private float memoryRemaining;
    private float immobilizedRemaining;
    private PlayerHidingState hiding;
    public bool IsImmobilized => immobilizedRemaining > 0;
    public float ImmobilizedRemaining => immobilizedRemaining;
    public void Immobilize(float seconds)
    {
        if (health != null && health.IsDead) return;
        immobilizedRemaining = Mathf.Max(immobilizedRemaining, seconds);
        var animator = GetComponent<CharacterAnimation>()?.Animator;
        if (animator != null && IsImmobilized) animator.speed = 0;
    }
    [SerializeField, Min(.5f)] private float personalSpace = 1.1f;
    private Vector3 PlayerFeet => target != null ? EnemyAttack.GroundPosition(target) : player != null ? player.transform.position : Vector3.zero;
    public bool CanOccupy(Vector3 position)
    {
        if (player == null || target == null || target.IsDead) return true;
        float distance = Vector2.Distance(position, PlayerFeet);
        // Allow a trapped enemy to retreat, but never let it advance into the player's space.
        return distance >= personalSpace || distance > Vector2.Distance(transform.position, PlayerFeet) + .001f;
    }
    private float alertRemaining;
    public bool IsAlerted => alertRemaining > 0;
    public void Alert(float seconds)
    {
        if (health != null && health.IsDead) return;
        alertRemaining = Mathf.Max(alertRemaining, seconds);
    }
    private Vector2Int lastSeen;
    public EnemyBehaviour Behaviour => behaviour;
    public bool IsChasing { get; private set; }
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
                hiding = player.GetComponent<PlayerHidingState>();
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
        if (!GameplayState.CanAct) return;
        if (IsImmobilized)
        {
            immobilizedRemaining = Mathf.Max(0, immobilizedRemaining - Time.deltaTime);
            return;
        }
        bool concealed = hiding != null && hiding.IsHidden && Vector2.Distance(transform.position, PlayerFeet) > gridSize;
        if (concealed) { memoryRemaining = 0; alertRemaining = 0; }
        bool alerted = alertRemaining > 0;
        alertRemaining = Mathf.Max(0, alertRemaining - Time.deltaTime);
        bool visible = !concealed && target != null && !target.IsDead && player.activeInHierarchy
            && (alerted || (behaviour == EnemyBehaviour.Chaser && Distance(Cell(transform.position), Cell(PlayerFeet)) <= chaseRangeTiles));
        if (visible) { lastSeen = Cell(PlayerFeet); memoryRemaining = loseSightSeconds; }
        else memoryRemaining = Mathf.Max(0, memoryRemaining - Time.deltaTime);
        IsChasing = alerted || (behaviour == EnemyBehaviour.Chaser && (visible || memoryRemaining > 0));
        if (attack.IsAttacking || movement.IsMoving || Time.time < retryAt) return;
        Vector2Int current = Cell(transform.position);
        if (target != null && !target.IsDead && Vector2.Distance(transform.position, PlayerFeet) < personalSpace - .15f)
        {
            foreach (var direction in Directions)
            {
                Vector3 next = transform.position + (Vector3)(Vector2)direction * gridSize;
                if (Vector2.Distance(next, PlayerFeet) <= Vector2.Distance(transform.position, PlayerFeet)) continue;
                if (movement.IsWalkable(next) && CanOccupy(next) && movement.TryMove(next - transform.position, patrolSpeed)) return;
            }
            return;
        }
        if (!concealed && target != null && attack.IsInRange(target))
        {
            GetComponent<CharacterAnimation>()?.Face(PlayerFeet - transform.position);
            attack.TryAttack(target);
            return;
        }
        if (IsChasing)
        {
            if (!visible || !attack.IsInRange(target)) MoveToward(current, lastSeen, chaseSpeed);
            return;
        }
        if (patrolOffsets != null && patrolOffsets.Length > 0)
        {
            Vector2 p = patrolOffsets[waypoint % patrolOffsets.Length];
            Vector2Int goal = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            if (current == goal) { waypoint = (waypoint + 1) % patrolOffsets.Length; return; }
            MoveToward(current, goal, patrolSpeed);
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
            if (IsInsidePatrolArea(next) && movement.IsWalkable(World(next)) && CanOccupy(World(next)) &&
                movement.TryMove(World(next) - transform.position, patrolSpeed)) return;
        }
        retryAt = Time.time + .25f;
    }
    // Exactly five cells on each axis, centered on the spawn cell.
    private bool IsInsidePatrolArea(Vector2Int cell) => Mathf.Abs(cell.x) <= patrolRadius && Mathf.Abs(cell.y) <= patrolRadius;
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
                    Mathf.Abs(next.y - current.y) > 16 || !movement.IsWalkable(World(next)) || !CanOccupy(World(next))) continue;
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
        if (Application.isPlaying && !TryGetComponent<EnemyHealthBar>(out _)) gameObject.AddComponent<EnemyHealthBar>();
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
