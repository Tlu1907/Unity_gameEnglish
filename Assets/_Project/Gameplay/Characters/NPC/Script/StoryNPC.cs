using System.Collections.Generic;
using UnityEngine;

/// <summary>Progress-aware contact adapter for the existing NPCIntroDialogue and DialogueData.</summary>
public sealed class StoryNPC : MonoBehaviour
{
    [SerializeField] private LevelObjectives progress;
    [SerializeField] private NPCIntroDialogue dialogueUI;
    [SerializeField] private DialogueData[] stages;
    [SerializeField] private bool appearsAfterTwoChests;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Collider2D solid;
    [SerializeField] private Transform marker;
    private readonly HashSet<Collider2D> contacts=new HashSet<Collider2D>();
    private int lastReadStage=-1;
    private bool ownsConversation;
    private float clock;
    private Vector3 markerRest;
    private Animator animator;
    public int CurrentStage => progress!=null && progress.OpenedCount>=progress.Target?2:progress!=null && progress.OpenedCount>=2?1:0;
    public bool IsAvailable => !appearsAfterTwoChests || CurrentStage>0;
    public bool HasUnreadDialogue => IsAvailable && lastReadStage!=CurrentStage;
    public DialogueData CurrentDialogue => stages!=null && stages.Length>CurrentStage?stages[CurrentStage]:null;
    private void OnEnable(){GameplayState.Changed+=OnGameplayChanged;if(progress!=null)progress.Changed+=Refresh;Refresh();}
    private void OnGameplayChanged(GameplayMode mode){if(mode!=GameplayMode.Dialogue)ownsConversation=false;}
    private void Start(){if(marker!=null)markerRest=marker.localPosition;animator=body!=null?body.GetComponent<Animator>():null;Refresh();}
    private void Refresh()
    {
        if(body!=null){var c=body.color;c.a=IsAvailable?1:.22f;body.color=c;}
        if(solid!=null)solid.enabled=IsAvailable;
        if(marker!=null)marker.gameObject.SetActive(HasUnreadDialogue);
    }
    public bool TryTalk()
    {
        if(!isActiveAndEnabled || !IsAvailable || !GameplayState.CanAct || dialogueUI==null || dialogueUI.IsOpen || CurrentDialogue==null)return false;
        dialogueUI.ShowDialogue(CurrentDialogue);
        if(!dialogueUI.IsOpen)return false;
        ownsConversation=true;lastReadStage=CurrentStage;Refresh();return true;
    }
    private void OnTriggerEnter2D(Collider2D other){if(other.GetComponentInParent<PlayerMovement>()!=null)contacts.Add(other);}
    private void OnTriggerExit2D(Collider2D other){contacts.Remove(other);}
    private void Update()
    {
        contacts.RemoveWhere(c=>c==null || !c.enabled || !c.gameObject.activeInHierarchy);
        
        if(ownsConversation && !dialogueUI.IsOpen)ownsConversation=false;
        // Opening dialogue is owned by the player's explicit interaction button.
        if(animator!=null)animator.speed=GameplayState.CanAct?1:0;
        if(GameplayState.CanAct)clock+=Time.deltaTime;
        if(marker!=null)marker.localPosition=markerRest+Vector3.up*(Mathf.Sin(clock*3)*.06f);
    }
    private void OnDisable()
    {
        GameplayState.Changed-=OnGameplayChanged;
        if(progress!=null)progress.Changed-=Refresh;
        if(ownsConversation && dialogueUI!=null && dialogueUI.IsOpen)dialogueUI.Close();
        ownsConversation=false;contacts.Clear();
    }
}


