using UnityEngine;

/// <summary>Retains the old component GUID; only this handler processes contact combat.</summary>
public class PlayerCollision : MonoBehaviour
{
    private PlayerInventory inventory;
    private Health health;
    private PlayerMovement legacy;
    private void Start()
    {
        inventory = GetComponent<PlayerInventory>();
        health = GetComponent<Health>();
        legacy = GetComponent<PlayerMovement>();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!GameplayState.CanAct || !other.CompareTag("Monster") || health == null || health.IsDead) return;
        if (inventory != null && inventory.HasWeapon) { Destroy(other.gameObject); return; }
        EnemyAttack attack = other.GetComponentInParent<EnemyAttack>();
        int damage = legacy != null ? legacy.monsterDamage : 20;
        if (attack != null) attack.Attack(health, damage);
        else ((IDamageable)health).TakeDamage(damage);
    }
}
