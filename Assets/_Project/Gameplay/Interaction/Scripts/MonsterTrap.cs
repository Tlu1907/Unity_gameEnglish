using UnityEngine;

public enum MonsterTrapPhase { Ready, Holding, Cooling }
[RequireComponent(typeof(Collider2D))]
public sealed class MonsterTrap : MonoBehaviour
{
    [SerializeField,Min(.1f)] private float holdSeconds=4;
    [SerializeField,Min(.1f)] private float cooldownSeconds=8;
    [SerializeField] private SpriteRenderer visual;
    private float remaining;
    private Vector3 restScale;
    public MonsterTrapPhase Phase { get; private set; }
    public float Remaining => remaining;
    private void Awake(){if(visual!=null)restScale=visual.transform.localScale;Refresh();}
    public bool TryTrigger(EnemyBrain enemy)
    {
        if(!isActiveAndEnabled || !GameplayState.CanAct || Phase!=MonsterTrapPhase.Ready || enemy==null || !enemy.isActiveAndEnabled)return false;
        var health=enemy.GetComponent<Health>();if(health!=null && health.IsDead)return false;
        enemy.Immobilize(holdSeconds);Phase=MonsterTrapPhase.Holding;remaining=holdSeconds;Refresh();return true;
    }
    private void OnTriggerEnter2D(Collider2D other)=>TryTrigger(other.GetComponentInParent<EnemyBrain>());
    private void OnTriggerStay2D(Collider2D other)=>TryTrigger(other.GetComponentInParent<EnemyBrain>());
    private void Update()
    {
        if(!GameplayState.CanAct)return;
        if(Phase!=MonsterTrapPhase.Ready)
        {
            remaining=Mathf.Max(0,remaining-Time.deltaTime);
            if(remaining<=0)
            {
                if(Phase==MonsterTrapPhase.Holding){Phase=MonsterTrapPhase.Cooling;remaining=cooldownSeconds;}
                else Phase=MonsterTrapPhase.Ready;
            }
        }
        Refresh();
    }
    private void Refresh()
    {
        if(visual==null)return;
        visual.color=Phase==MonsterTrapPhase.Ready?new Color(1,.48f,.2f):
            Phase==MonsterTrapPhase.Holding?new Color(1,.85f,.35f):new Color(.45f,.36f,.3f,.8f);
        visual.transform.localScale=restScale*(Phase==MonsterTrapPhase.Holding?1+.08f*Mathf.Sin(remaining*15):1);
    }
}
