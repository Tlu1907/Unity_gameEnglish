using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class GridMovement : MonoBehaviour
{
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float gridSize = 1f;
    [Tooltip("Optional bounds for sprite-based maps with an empty Ground Tilemap. Painted Tilemaps remain authoritative.")]
    [SerializeField] private SpriteRenderer walkableBackground;
    private CharacterAnimation visuals;
    private EnemyBrain enemy;
    private Rigidbody2D body;
    private Collider2D feetCollider;
    private CityTerrain terrain;
    private Vector2 inputDirection;
    private float inputSpeed;
    private readonly List<Collider2D> obstacleHits = new List<Collider2D>(8);
    public Vector2 MovementInput => inputDirection;
    public bool UsesPhysicsMovement { get; private set; }
    public bool IsMoving { get; private set; }
    public Tilemap Ground => groundTilemap;
    public SpriteRenderer WalkableBackground => walkableBackground;
    public void SetWalkableBackground(SpriteRenderer background) => walkableBackground = background;
    public float GridSize => gridSize;
    public LayerMask Obstacles => obstacleMask;
    public event Action StepCompleted;
    private void Awake()
    {
        visuals = GetComponent<CharacterAnimation>();
        enemy = GetComponent<EnemyBrain>();
        feetCollider = GetComponent<Collider2D>();
        ResolveTerrain();
        if (GetComponent<PlayerMovement>() != null || GetComponent<PlayerInputReader>() != null)
            EnablePlayerPhysics();
    }
    public void EnablePlayerPhysics()
    {
        if (UsesPhysicsMovement) return;
        StopAllCoroutines();
        IsMoving = false;
        UsesPhysicsMovement = true;
        body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        body.linearDamping = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }
    public void SetMovementInput(Vector3 direction, float speed)
    {
        EnablePlayerPhysics();
        inputDirection = direction == Vector3.zero ? Vector2.zero
            : Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0f) : new Vector2(0f, Mathf.Sign(direction.y));
        inputSpeed = Mathf.Max(0f, speed);
        visuals?.Face(inputDirection);
        if (!GameplayState.CanAct || inputDirection == Vector2.zero) StopPhysicsMotion();
    }
    private void OnEnable() => GameplayState.Changed += OnGameplayChanged;
    private void OnGameplayChanged(GameplayMode mode)
    {
        if (!GameplayState.CanAct) StopPhysicsMotion();
    }
    private void StopPhysicsMotion()
    {
        if (!UsesPhysicsMovement || body == null) return;
        body.linearVelocity = Vector2.zero;
        IsMoving = false;
        visuals?.SetMoving(false);
    }
    private void FixedUpdate()
    {
        if (!UsesPhysicsMovement || body == null) return;
        Vector2 velocity = GameplayState.CanAct ? inputDirection * inputSpeed : Vector2.zero;
        Vector2 feet = feetCollider != null ? (Vector2)feetCollider.bounds.center : body.position;
        if (terrain != null) velocity *= terrain.SpeedMultiplier(feet);
        // Map admission remains separate from solid collision, which Physics2D now resolves.
        Vector2 nextFeet = feet + velocity * Time.fixedDeltaTime;
        bool admitted = terrain != null ? terrain.ContainsSurface(nextFeet) : HasGround(nextFeet);
        if (velocity != Vector2.zero && !admitted)
            velocity = Vector2.zero;
        body.linearVelocity = velocity;
    }
    private void LateUpdate()
    {
        if (!UsesPhysicsMovement || body == null) return;
        // Use velocity after the solver, not a per-render-frame delta: render FPS may exceed physics FPS.
        IsMoving = GameplayState.CanAct && inputDirection != Vector2.zero
            && body.linearVelocity.sqrMagnitude > .0001f;
        visuals?.SetMoving(IsMoving);
    }
    public void Configure(Tilemap ground, LayerMask obstacles, float size)
    { groundTilemap = ground; obstacleMask = obstacles; gridSize = size; }
    private void Start()
    {
        ResolveTerrain();
        visuals = GetComponent<CharacterAnimation>();
        if (!UsesPhysicsMovement && terrain == null && groundTilemap != null)
            transform.position = groundTilemap.GetCellCenterWorld(groundTilemap.WorldToCell(transform.position));
        if (terrain == null && (groundTilemap == null || groundTilemap.GetUsedTilesCount() == 0) && walkableBackground == null)
            Debug.LogWarning("GridMovement: Ground has no tiles and Walkable Background is unassigned. No walkable cells. Run Tools/Player/Setup Player in Current Scene or assign a painted Ground Tilemap.", this);
    }
    public bool HasGround(Vector3 position)
    {
        if (terrain != null) return terrain.ContainsSurface(position);
        if (groundTilemap != null && groundTilemap.GetUsedTilesCount() > 0)
            return groundTilemap.HasTile(groundTilemap.WorldToCell(position));
        if (walkableBackground == null || walkableBackground.sprite == null) return false;
        Bounds bounds = walkableBackground.bounds;
        return position.x >= bounds.min.x && position.x <= bounds.max.x
            && position.y >= bounds.min.y && position.y <= bounds.max.y;
    }
    private void ResolveTerrain()
    {
        terrain = null;
        if (!gameObject.scene.IsValid()) return;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var candidate in root.GetComponentsInChildren<CityTerrain>(true))
            if (candidate.isActiveAndEnabled) { terrain = candidate; return; }
    }
    public Collider2D ObstacleAt(Vector3 position)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(obstacleMask);
        obstacleHits.Clear();
        Physics2D.OverlapBox(position, new Vector2(.8f, .8f), 0f, filter, obstacleHits);
        foreach (var hit in obstacleHits)
            if (hit != null && !hit.transform.IsChildOf(transform)) return hit;
        return null;
    }
    public bool IsWalkable(Vector3 position) => HasGround(position) && ObstacleAt(position) == null;
    // Caller owns interaction-specific admission; the motor always requires ground.
    public bool TryMove(Vector3 direction, float speed, bool admittedByInteraction = false)
    {
        if (UsesPhysicsMovement) return false;
        if (!isActiveAndEnabled || !GameplayState.CanAct || (enemy != null && enemy.IsImmobilized) || IsMoving || speed <= 0f || gridSize <= 0f || direction == Vector3.zero) return false;
        // Keep every caller on cardinal cells; never normalize a diagonal into an off-grid target.
        direction = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? new Vector3(Mathf.Sign(direction.x), 0f, 0f)
            : new Vector3(0f, Mathf.Sign(direction.y), 0f);
        if (direction == Vector3.zero) return false;
        visuals?.Face(direction);
        Vector3 target = transform.position + direction.normalized * gridSize;
        if (!HasGround(target) || (!admittedByInteraction && ObstacleAt(target) != null)) return false;
        StartCoroutine(Move(target, speed));
        return true;
    }
    private IEnumerator Move(Vector3 target, float speed)
    {
        IsMoving = true;
        visuals?.Face(target - transform.position);
        visuals?.SetMoving(true);
        while (transform.position != target)
        {
            Vector3 previous = transform.position;
            if (GameplayState.CanAct && (enemy == null || !enemy.IsImmobilized))
            {
                Vector3 next = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                if (enemy != null && !enemy.CanOccupy(next)) break;
                transform.position = next;
            }
            visuals?.SetMoving(transform.position != previous);
            yield return null;
        }
        while (!GameplayState.CanAct) yield return null;
        IsMoving = false;
        visuals?.SetMoving(false);
        StepCompleted?.Invoke();
    }
    private void OnDisable()
    {
        GameplayState.Changed -= OnGameplayChanged;
        if (UsesPhysicsMovement)
        {
            inputDirection = Vector2.zero;
            StopPhysicsMotion();
            return;
        }
        StopAllCoroutines();
        IsMoving = false;
        visuals?.SetMoving(false);
        if (groundTilemap != null)
            transform.position = groundTilemap.GetCellCenterWorld(groundTilemap.WorldToCell(transform.position));
    }
}
