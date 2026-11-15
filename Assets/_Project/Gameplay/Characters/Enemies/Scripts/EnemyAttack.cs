using UnityEngine;

/// <summary>Each monster owns its attack cooldown across target exits.</summary>
[DisallowMultipleComponent]
public sealed class EnemyAttack : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 30;
    [SerializeField, Min(.01f)] private float attackInterval = 1.5f;
    [SerializeField, Min(.01f)] private float attackRange = 1.1f;
    private float cooldown;
    private void Update()
    {
        if (GameplayState.CanAct) cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
    }
    public bool IsInRange(Health target)
    {
        if (target == null || !target.gameObject.activeInHierarchy || target.IsDead) return false;
        if (Vector2.Distance(transform.position, target.transform.position) > attackRange) return false;
        GridMovement motor = GetComponent<GridMovement>();
        return motor == null || Physics2D.Linecast(transform.position, target.transform.position, motor.Obstacles).collider == null;
    }
    public bool TryAttack(Health target)
    {
        if (!GameplayState.CanAct || cooldown > 0 || !IsInRange(target)) return false;
        cooldown = attackInterval;
        target.TakeDamage(damage);
        return true;
    }
}
