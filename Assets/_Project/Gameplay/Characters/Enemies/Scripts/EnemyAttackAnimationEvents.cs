using UnityEngine;

/// <summary>Animation Events on CreatureVisual forward to its owning enemy.</summary>
public sealed class EnemyAttackAnimationEvents : MonoBehaviour
{
    private EnemyAttack owner;
    private void Awake() => owner=GetComponentInParent<EnemyAttack>();
    public void Strike() { if(owner!=null)owner.Strike(); }
    public void FinishAttack() { if(owner!=null)owner.FinishAttack(); }
}
