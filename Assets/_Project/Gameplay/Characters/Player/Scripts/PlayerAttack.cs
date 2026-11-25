using System.Collections.Generic;
using UnityEngine;

/// <summary>Equipped players can swing once per T press; each monster is hit once per swing.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAttack : MonoBehaviour
{
    [SerializeField, Min(1)] private int unarmedDamage = 25;
    [SerializeField, Min(1)] private int weaponDamage = 50;
    [SerializeField, Min(.01f)] private float attackInterval = .5f;
    [SerializeField, Min(.01f)] private float reach = 1.8f;
    [SerializeField, Range(1f, 180f)] private float attackAngle = 100f;
    private float cooldown;
    public bool HasTempleBlessing { get; private set; }
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    public int Damage => HasTempleBlessing ? 70 : GetComponent<PlayerInventory>()?.HasWeapon == true ? weaponDamage : unarmedDamage;

    public bool GrantTempleBlessing()
    {
        var health = GetComponent<Health>();
        if (HasTempleBlessing || health == null || health.IsDead) return false;
        HasTempleBlessing = true;
        health.AddBonusArmor(200);
        health.RestoreHealthAndArmor();
        return true;
    }

    private void Update()
    {
        if (!GameplayState.CanAct) return;
        cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.T)) TryAttack();
    }

    public bool TryAttack()
    {
        if (GetComponent<PlayerInventory>()?.HasWeapon != true) return false;
        Health playerHealth = GetComponent<Health>();
        if (!isActiveAndEnabled || !GameplayState.CanAct || cooldown > 0 ||
            (playerHealth != null && playerHealth.IsDead)) return false;
        cooldown = attackInterval;
        CharacterAnimation visuals = GetComponent<CharacterAnimation>();
        Vector2 facing = visuals != null ? visuals.Facing : Vector2.down;
        Animator animator = visuals != null ? visuals.Animator : null;
        if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == AttackTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(AttackTrigger);
                    break;
                }
        }
        GridMovement motor = GetComponent<GridMovement>();
        LayerMask obstacles = motor != null ? motor.Obstacles : 0;
        var struck = new HashSet<EnemyBrain>();
        Vector3 origin = playerHealth != null ? EnemyAttack.GroundPosition(playerHealth) : transform.position;
        foreach (Collider2D contact in Physics2D.OverlapCircleAll(origin, reach))
        {
            EnemyBrain enemy = contact.GetComponentInParent<EnemyBrain>();
            if (enemy == null || !struck.Add(enemy)) continue;
            Health target = enemy.GetComponent<Health>();
            if (target == null || target.IsDead) continue;
            Vector3 targetPoint = EnemyAttack.GroundPosition(target);
            Vector2 delta = targetPoint - origin;
            if (delta.sqrMagnitude > reach * reach ||
                (delta.sqrMagnitude > .0001f && Vector2.Angle(facing, delta) > attackAngle * .5f)) continue;
            bool blocked = false;
            foreach (var hit in Physics2D.LinecastAll(origin, targetPoint, obstacles))
            {
                if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.transform.IsChildOf(enemy.transform)) continue;
                blocked = true; break;
            }
            if (!blocked) target.TakeDamage(Damage);
        }
        return true;
    }

}
