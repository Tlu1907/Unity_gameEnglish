using System.Collections.Generic;
using UnityEngine;

/// <summary>One forward melee swing per T press; each monster is hit once per swing.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAttack : MonoBehaviour
{
    [SerializeField, Min(1)] private int unarmedDamage = 25;
    [SerializeField, Min(1)] private int weaponDamage = 50;
    [SerializeField, Min(.01f)] private float attackInterval = .5f;
    [SerializeField, Min(.01f)] private float reach = 1.8f;
    [SerializeField, Range(1f, 180f)] private float attackAngle = 100f;
    [SerializeField, Min(.01f)] private float effectDuration = .15f;
    private float cooldown;
    private float effectRemaining;
    private LineRenderer slash;
    private Material slashMaterial;
    public int Damage => GetComponent<PlayerInventory>()?.HasWeapon == true ? weaponDamage : unarmedDamage;

    private void Update()
    {
        if (!GameplayState.CanAct) return;
        cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
        if (effectRemaining > 0)
        {
            effectRemaining -= Time.deltaTime;
            if (effectRemaining <= 0 && slash != null) slash.enabled = false;
        }
        if (Input.GetKeyDown(KeyCode.T)) TryAttack();
    }

    public bool TryAttack()
    {
        Health playerHealth = GetComponent<Health>();
        if (!isActiveAndEnabled || !GameplayState.CanAct || cooldown > 0 ||
            (playerHealth != null && playerHealth.IsDead)) return false;
        cooldown = attackInterval;
        CharacterAnimation visuals = GetComponent<CharacterAnimation>();
        Vector2 facing = visuals != null ? visuals.Facing : Vector2.down;
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
        if (Application.isPlaying) PlayEffect(facing);
        return true;
    }

    private void PlayEffect(Vector2 facing)
    {
        // Procedural feedback also works before an attack animation has been authored.
        if (slash == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            var go = new GameObject("Melee Swing");
            go.transform.SetParent(transform, false);
            slash = go.AddComponent<LineRenderer>();
            slashMaterial = new Material(shader);
            slash.sharedMaterial = slashMaterial;
            slash.useWorldSpace = true;
            slash.startWidth = .12f;
            slash.endWidth = .03f;
            slash.positionCount = 13;
            SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite != null)
            {
                slash.sortingLayerID = sprite.sortingLayerID;
                slash.sortingOrder = sprite.sortingOrder + 1;
            }
        }
        Color color = GetComponent<PlayerInventory>()?.HasWeapon == true ? new Color(1f, .8f, .25f) : Color.white;
        slash.startColor = slash.endColor = color;
        float centerAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        for (int i = 0; i < slash.positionCount; i++)
        {
            float angle = (centerAngle + Mathf.Lerp(-attackAngle * .5f, attackAngle * .5f,
                (float)i / (slash.positionCount - 1))) * Mathf.Deg2Rad;
            slash.SetPosition(i, EnemyAttack.GroundPosition(GetComponent<Health>()) + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * reach * .8f);
        }
        slash.enabled = true;
        effectRemaining = effectDuration;
    }

    private void OnDisable()
    {
        if (slash != null) slash.enabled = false;
        effectRemaining = 0;
    }
    private void OnDestroy()
    {
        if (slashMaterial != null) Destroy(slashMaterial);
    }
}
