using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum EnemyBehaviour { Chaser, Patrol, Guard }
public enum EnemyEncounterMode
{
    Normal = 0,
    MirrorAmbush = 1,
    ArchaeologistGuard = 2,
    LaboratoryAmbush = 3
}

public class EnemyBrain : MonoBehaviour
{
    public event System.Action<EnemyBrain> Defeated;
    private bool defeatReported;
    [SerializeField] private GameObject player;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField, Min(.01f)] private float gridSize = 1f;
    [Header("Movement speeds")]
    [Tooltip("Tốc độ đi tuần khi chưa phát hiện Player (đơn vị/giây).")]
    [SerializeField, Min(.01f)] private float patrolSpeed = 2f;
    [Tooltip("Tốc độ truy đuổi khi Player vào phạm vi phát hiện (đơn vị/giây). Behaviour phải là Chaser.")]
    [SerializeField, Min(.01f)] private float chaseSpeed = 2f;
    [Header("Player detection")]
    [Tooltip("Phạm vi bắt đầu truy đuổi, tính bằng tổng số ô ngang và dọc tới Player.")]
    [SerializeField, Min(1)] private int chaseRangeTiles = 3;
    [SerializeField] private bool useAnimator = true;
    [SerializeField] private string runParameterName = "IsRunning";
    [SerializeField, HideInInspector] private int migrationVersion;
    [Tooltip("Chọn Chaser để quái tự phát hiện và truy đuổi Player khi đến gần.")]
    [SerializeField] private EnemyBehaviour behaviour;
    [Header("Mission encounter")]
    [Tooltip("Normal: tuần tra; MirrorAmbush: nhận trang bị ở tháp; ArchaeologistGuard: hạ quái để cứu NPC; LaboratoryAmbush: hoàn tất tài liệu.")]
    [SerializeField] private EnemyEncounterMode encounterMode;
    [SerializeField] private GameObject archaeologistToReveal;
    [SerializeField] private Transform archaeologistSpawnPoint;
    public EnemyEncounterMode EncounterMode => encounterMode;
    private bool encounterInitialized, encounterActive;
    private Renderer[] encounterRenderers;
    private Collider2D[] encounterColliders;
    private bool[] originalRendererStates, originalColliderStates;
    private PlayerInventory encounterInventory;
    public bool IsEncounterActive => encounterInitialized ? encounterActive :
        encounterMode == EnemyEncounterMode.Normal || encounterMode == EnemyEncounterMode.ArchaeologistGuard;
    public bool PursuesPlayerAfterTrigger => IsEncounterActive &&
        (encounterMode == EnemyEncounterMode.MirrorAmbush || encounterMode == EnemyEncounterMode.LaboratoryAmbush);
    [SerializeField] private Vector2[] patrolOffsets = new Vector2[0];
    [SerializeField, Min(1)] private int patrolRadius = 2;
    [Tooltip("Số giây tiếp tục tìm Player ở vị trí cuối cùng sau khi mất dấu.")]
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
        if (health != null) { health.Died -= Die; health.Died += Die; }
        StoryProgress.BeatCompleted -= OnStoryBeat;
        StoryProgress.BeatCompleted += OnStoryBeat;
        StoryProgress.ResearchDocumentsDecoded -= RefreshEncounter;
        StoryProgress.ResearchDocumentsDecoded += RefreshEncounter;
        BindEncounterInventory();
        if (encounterInitialized) RefreshEncounter();
    }
    private void OnDisable()
    {
        if (health != null) health.Died -= Die;
        StoryProgress.BeatCompleted -= OnStoryBeat;
        StoryProgress.ResearchDocumentsDecoded -= RefreshEncounter;
        if (encounterInventory != null) encounterInventory.Changed -= RefreshEncounter;
        encounterInventory = null;
    }
    private void Die()
    {
        if (defeatReported) return;
        defeatReported = true;
        if (encounterMode == EnemyEncounterMode.ArchaeologistGuard && archaeologistToReveal != null)
        {
            if (archaeologistSpawnPoint != null) archaeologistToReveal.transform.position = archaeologistSpawnPoint.position;
            archaeologistToReveal.SetActive(true);
        }
        Defeated?.Invoke(this);
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
    }
    private static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    private void Start() => InitializeEncounter();
    private void OnStoryBeat(string id) => RefreshEncounter();
    private void BindEncounterInventory()
    {
        if (player == null)
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var candidate = root.GetComponentInChildren<PlayerMovement>(true);
                if (candidate != null && candidate.gameObject.activeInHierarchy) { player = candidate.gameObject; break; }
            }
        var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (encounterInventory == inventory) return;
        if (encounterInventory != null) encounterInventory.Changed -= RefreshEncounter;
        encounterInventory = inventory;
        if (encounterInventory != null) encounterInventory.Changed += RefreshEncounter;
    }
    public void InitializeEncounter()
    {
        if (encounterInitialized) { RefreshEncounter(); return; }
        encounterInitialized = true;
        encounterRenderers = GetComponentsInChildren<Renderer>(true);
        encounterColliders = GetComponentsInChildren<Collider2D>(true);
        originalRendererStates = new bool[encounterRenderers.Length];
        originalColliderStates = new bool[encounterColliders.Length];
        for (int i = 0; i < encounterRenderers.Length; i++) originalRendererStates[i] = encounterRenderers[i].enabled;
        for (int i = 0; i < encounterColliders.Length; i++) originalColliderStates[i] = encounterColliders[i].enabled;
        if (encounterMode == EnemyEncounterMode.ArchaeologistGuard && archaeologistToReveal != null)
            archaeologistToReveal.SetActive(false);
        RefreshEncounter();
    }
    public void RefreshEncounter()
    {
        if (!encounterInitialized) return;
        BindEncounterInventory();
        if (defeatReported) return;
        bool activate = encounterMode == EnemyEncounterMode.Normal || encounterMode == EnemyEncounterMode.ArchaeologistGuard ||
            encounterMode == EnemyEncounterMode.MirrorAmbush && StoryProgress.IsCompleted("q1_spirit") && encounterInventory != null && encounterInventory.HasWeapon ||
            encounterMode == EnemyEncounterMode.LaboratoryAmbush && StoryProgress.HasEnteredResearchRoom && StoryProgress.HasDecodedResearchDocuments;
        // A triggered encounter remains active even if the Player later unequips.
        encounterActive |= activate;
        for (int i = 0; i < encounterRenderers.Length; i++)
            if (encounterRenderers[i] != null) encounterRenderers[i].enabled = encounterActive && originalRendererStates[i];
        for (int i = 0; i < encounterColliders.Length; i++)
            if (encounterColliders[i] != null) encounterColliders[i].enabled = encounterActive && originalColliderStates[i];
    }

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
        if (!encounterInitialized) InitializeEncounter();
        else if (!encounterActive) RefreshEncounter();
        if (!IsEncounterActive) return;
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
        bool forcedPursuit = PursuesPlayerAfterTrigger && !concealed;
        bool visible = !concealed && target != null && !target.IsDead && player.activeInHierarchy
            && (forcedPursuit || alerted || (behaviour == EnemyBehaviour.Chaser && Distance(Cell(transform.position), Cell(PlayerFeet)) <= chaseRangeTiles));
        if (visible) { lastSeen = Cell(PlayerFeet); memoryRemaining = loseSightSeconds; }
        else memoryRemaining = Mathf.Max(0, memoryRemaining - Time.deltaTime);
        IsChasing = forcedPursuit || alerted || (behaviour == EnemyBehaviour.Chaser && (visible || memoryRemaining > 0));
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
