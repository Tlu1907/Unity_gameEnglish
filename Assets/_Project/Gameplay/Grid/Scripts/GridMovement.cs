using System;
using System.Collections;
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
    private Rigidbody2D body;
    private Vector2 inputDirection;
    private float inputSpeed;
    public Vector2 MovementInput => inputDirection;
    public bool UsesPhysicsMovement { get; private set; }
    public bool IsMoving { get; private set; }
    public Tilemap Ground => groundTilemap;
    public float GridSize => gridSize;
    public LayerMask Obstacles => obstacleMask;
    public event Action StepCompleted;
    private void Awake()
    {
        visuals = GetComponent<CharacterAnimation>();
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
        // Map admission remains separate from solid collision, which Physics2D now resolves.
        if (velocity != Vector2.zero && !HasGround(body.position + velocity * Time.fixedDeltaTime))
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
        visuals = GetComponent<CharacterAnimation>();
        if (!UsesPhysicsMovement && groundTilemap != null)
            transform.position = groundTilemap.GetCellCenterWorld(groundTilemap.WorldToCell(transform.position));
        if ((groundTilemap == null || groundTilemap.GetUsedTilesCount() == 0) && walkableBackground == null)
            Debug.LogWarning("GridMovement: Ground has no tiles and Walkable Background is unassigned. No walkable cells. Run Tools/Player/Setup Player in Current Scene or assign a painted Ground Tilemap.", this);
    }
    public bool HasGround(Vector3 position)
    {
        if (groundTilemap != null && groundTilemap.GetUsedTilesCount() > 0)
            return groundTilemap.HasTile(groundTilemap.WorldToCell(position));
        if (walkableBackground == null || walkableBackground.sprite == null) return false;
        Bounds bounds = walkableBackground.bounds;
        return position.x >= bounds.min.x && position.x <= bounds.max.x
            && position.y >= bounds.min.y && position.y <= bounds.max.y;
    }
    public Collider2D ObstacleAt(Vector3 position) => Physics2D.OverlapBox(position, new Vector2(.8f, .8f), 0f, obstacleMask);
    public bool IsWalkable(Vector3 position) => HasGround(position) && ObstacleAt(position) == null;
    // Caller owns interaction-specific admission; the motor always requires ground.
    public bool TryMove(Vector3 direction, float speed, bool admittedByInteraction = false)
    {
        if (UsesPhysicsMovement) return false;
        if (!GameplayState.CanAct || IsMoving || speed <= 0f || gridSize <= 0f || direction == Vector3.zero) return false;
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
            if (GameplayState.CanAct)
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
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
