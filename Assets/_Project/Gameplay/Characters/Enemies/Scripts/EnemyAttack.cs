using UnityEngine;

/// <summary>Enemy melee: visible wind-up, one animation-event hit, then recovery.</summary>
[DisallowMultipleComponent]
public sealed class EnemyAttack : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 30;
    [SerializeField, Min(.01f)] private float attackInterval = 1.5f;
    [SerializeField, Min(.01f)] private float attackRange = 1.5f;
    [Header("Laboratory ambush")]
    [SerializeField, Min(.01f)] private float laboratoryAttackInterval = .8f;
    [SerializeField, Min(1f)] private float laboratoryAttackAnimationSpeed = 1.5f;
    private bool IsLaboratory => brain != null && brain.EncounterMode == EnemyEncounterMode.LaboratoryAmbush;
    public float AttackInterval => IsLaboratory ? Mathf.Min(attackInterval, laboratoryAttackInterval) : attackInterval;
    private float AnimationSpeed => IsAttacking && IsLaboratory ? laboratoryAttackAnimationSpeed : 1f;
    private float cooldown;
    private Health victim;
    private Animator animator;
    private CharacterAnimation visuals;
    private EnemyBrain brain;
    private bool CanAct => GameplayState.CanAct && (brain == null || brain.IsEncounterActive && !brain.IsImmobilized);
    private Vector2 attackFacing;
    private bool hitResolved;
    public bool IsAttacking { get; private set; }
    public float Range => attackRange;
    public static Vector3 GroundPosition(Health target)
    {
        var col=target!=null?target.GetComponent<Collider2D>():null;
        return col!=null?col.bounds.center:target!=null?target.transform.position:Vector3.zero;
    }
    private void Awake()
    {
        visuals=GetComponent<CharacterAnimation>();
        brain=GetComponent<EnemyBrain>();
    }
    private void OnEnable() => GameplayState.Changed+=OnStateChanged;
    private void OnDisable()
    {
        GameplayState.Changed-=OnStateChanged;IsAttacking=false;victim=null;
        if(animator!=null)animator.speed=1;
    }
    private void OnStateChanged(GameplayMode mode)
    {
        if(animator!=null)animator.speed=CanAct?AnimationSpeed:0;
    }
    private void Update()
    {
        if (animator == null && visuals != null) animator = visuals.Animator;
        if (animator != null) animator.speed = CanAct ? AnimationSpeed : 0;
        if (CanAct) cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
    }
    public bool IsInRange(Health target)
    {
        if (target == null || !target.gameObject.activeInHierarchy || target.IsDead) return false;
        Vector3 point=GroundPosition(target);
        if (Vector2.Distance(transform.position, point) > attackRange) return false;
        GridMovement motor = GetComponent<GridMovement>();
        return motor == null || Physics2D.Linecast(transform.position, point, motor.Obstacles).collider == null;
    }
    public bool TryAttack(Health target)
    {
        if (!isActiveAndEnabled || !CanAct || IsAttacking || cooldown > 0 || !IsInRange(target)) return false;
        var ownHealth=GetComponent<Health>();if(ownHealth!=null && ownHealth.IsDead)return false;
        animator=visuals!=null?visuals.Animator:null;
        if(animator==null || animator.runtimeAnimatorController==null || !animator.HasState(0,Animator.StringToHash("Attack")))return false;
        visuals.Face(GroundPosition(target)-transform.position);visuals.SetMoving(false);
        attackFacing=visuals.Facing;victim=target;hitResolved=false;IsAttacking=true;cooldown=AttackInterval;
        animator.speed=AnimationSpeed;animator.Play("Attack",0,0);
        return true;
    }
    // Called by the Attack clip on its strike frame. Blend-tree duplicate events cannot double-hit.
    public void Strike()
    {
        if(!IsAttacking || hitResolved || !CanAct)return;
        hitResolved=true;
        if(!IsInRange(victim))return;
        Vector2 direction=GroundPosition(victim)-transform.position;
        if(direction.sqrMagnitude>.001f && Vector2.Dot(direction.normalized,attackFacing)<.5f)return;
        victim.TakeDamage(damage);
    }
    public void FinishAttack()
    {
        if(!CanAct)return;
        IsAttacking=false;victim=null;
        if(animator!=null)animator.speed=1f;
    }
}
