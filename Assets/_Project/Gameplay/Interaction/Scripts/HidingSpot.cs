using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class HidingSpot : MonoBehaviour
{
    [SerializeField] private Transform foliage;
    private readonly Dictionary<PlayerHidingState,HashSet<Collider2D>> occupants=new Dictionary<PlayerHidingState,HashSet<Collider2D>>();
    private Vector3 restPosition;
    private float clock;
    public bool IsOccupied => occupants.Count>0;
    private void Awake(){if(foliage!=null)restPosition=foliage.localPosition;}
    private void OnTriggerEnter2D(Collider2D other)=>Enter(other);
    private void OnTriggerStay2D(Collider2D other)=>Enter(other);
    private void Enter(Collider2D other)
    {
        var state=other.GetComponentInParent<PlayerHidingState>();if(state==null)return;
        if(!occupants.TryGetValue(state,out var contacts)){contacts=new HashSet<Collider2D>();occupants.Add(state,contacts);}
        contacts.Add(other);state.SetHidden(this,true);
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        var state=other.GetComponentInParent<PlayerHidingState>();
        if(state==null || !occupants.TryGetValue(state,out var contacts))return;
        contacts.Remove(other);if(contacts.Count>0)return;
        state.SetHidden(this,false);occupants.Remove(state);
    }
    private void Update()
    {
        if(foliage==null || !GameplayState.CanAct)return;
        clock+=Time.deltaTime;
        foliage.localPosition=restPosition+(IsOccupied?Vector3.right*Mathf.Sin(clock*14)*.035f:Vector3.zero);
    }
    private void OnDisable()
    {
        foreach(var state in occupants.Keys)if(state!=null)state.SetHidden(this,false);
        occupants.Clear();if(foliage!=null)foliage.localPosition=restPosition;
    }
}
