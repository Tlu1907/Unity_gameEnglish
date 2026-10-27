using UnityEngine;

/// <summary>Contact damage keeps the victim's legacy Inspector value.</summary>
[DisallowMultipleComponent]
public sealed class EnemyAttack : MonoBehaviour
{
    public void Attack(IDamageable target, int damage)
    {
        if (GameplayState.CanAct) target?.TakeDamage(damage);
    }
}
