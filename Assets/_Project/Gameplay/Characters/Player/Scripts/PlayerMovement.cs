using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Compatibility adapter preserving old serialized fields and prefab overrides.</summary>
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float gridSize = 1f;
    public Tilemap groundTilemap;
    public LayerMask obstacleMask;
    public PlayerItemOption currentItem = PlayerItemOption.None;
    public int maxHealth = 200;
    public int currentHealth = 200;
    public int monsterDamage = 20;
    [SerializeField, HideInInspector] private int migrationVersion;
    private Health health;
    private PlayerInventory inventory;

    private void Awake()
    {
        EnsureComponents();
        health = GetComponent<Health>();
        inventory = GetComponent<PlayerInventory>();
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }
    private void OnEnable()
    {
        health = GetComponent<Health>();
        if (health != null) { health.Died += Die; health.Changed += SyncHealth; }
    }
    private void OnDisable()
    {
        if (health != null) { health.Died -= Die; health.Changed -= SyncHealth; }
    }
    private void SyncHealth(int value) => currentHealth = value;
    public bool CanDefeatMonster() => inventory != null && inventory.HasWeapon;
    public void TakeDamage() => health?.TakeDamage(monsterDamage);
    public void Die() { GameplayState.GameOver(); gameObject.SetActive(false); }

    // Used by runtime fallback and Editor migration. Existing components remain authoritative.
    public void EnsureComponents()
    {
        if (!TryGetComponent<CharacterAnimation>(out _)) gameObject.AddComponent<CharacterAnimation>();
        if (!TryGetComponent<GridMovement>(out _))
            gameObject.AddComponent<GridMovement>().Configure(groundTilemap, obstacleMask, gridSize);
        if (!TryGetComponent<Health>(out _)) gameObject.AddComponent<Health>().Configure(maxHealth, currentHealth);
        if (!TryGetComponent<PlayerInventory>(out _)) gameObject.AddComponent<PlayerInventory>().SetItem(currentItem);
        if (!TryGetComponent<PlayerArmorBar>(out _)) gameObject.AddComponent<PlayerArmorBar>();
        if (!TryGetComponent<PlayerAppearance>(out _)) gameObject.AddComponent<PlayerAppearance>();
        if (!TryGetComponent<PlayerInteractor>(out _)) gameObject.AddComponent<PlayerInteractor>();
        if (!TryGetComponent<PlayerInputReader>(out _)) gameObject.AddComponent<PlayerInputReader>().Configure(speed);
        if (!TryGetComponent<PlayerAttack>(out _)) gameObject.AddComponent<PlayerAttack>();
        if (!TryGetComponent<PlayerGroundShadow>(out _)) gameObject.AddComponent<PlayerGroundShadow>();
        GridMovement motor = GetComponent<GridMovement>();
        if (motor.Ground == null && groundTilemap != null) motor.Configure(groundTilemap, motor.Obstacles, motor.GridSize);
    }

    public bool Migrate(int version)
    {
        if (migrationVersion >= version) return false;
        EnsureComponents();
        GetComponent<GridMovement>().Configure(groundTilemap, obstacleMask, gridSize);
        GetComponent<Health>().Configure(maxHealth, currentHealth);
        GetComponent<PlayerInventory>().SetItem(currentItem);
        GetComponent<PlayerInputReader>().Configure(speed);
        migrationVersion = version;
        return true;
    }
}
