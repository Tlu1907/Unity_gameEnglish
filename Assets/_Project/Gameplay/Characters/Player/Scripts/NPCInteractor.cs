using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One explicit interaction target, measured from the player's feet.</summary>
[DisallowMultipleComponent]
public sealed class NPCInteractor : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float talkDistance = .85f;
    private StoryNPC[] stories;
    private NPCDialogueTrigger[] legacy;
    private MonoBehaviour target;
    private Collider2D feet;
    private GameObject prompt;
    private TMP_Text label;
    public MonoBehaviour Target => target;
    private void Start()
    {
        feet=GetComponent<Collider2D>();
        stories=FindObjectsByType<StoryNPC>(FindObjectsInactive.Include);
        legacy=FindObjectsByType<NPCDialogueTrigger>(FindObjectsInactive.Include);
        var hud=FindAnyObjectByType<GameStateUI>();
        var canvas=hud!=null?hud.GetComponentInParent<Canvas>():null;
        if (canvas == null)
            foreach (var vitals in FindObjectsByType<PlayerVitalsHUD>(FindObjectsInactive.Exclude))
                if (vitals.gameObject.scene == gameObject.scene) { canvas = vitals.GetComponentInParent<Canvas>(); if (canvas != null) break; }
        if(canvas==null){Debug.LogError("NPC interaction requires the existing HUD Canvas.",this);enabled=false;return;}
        var buttonObject=new GameObject("TalkButton",typeof(RectTransform),typeof(Image),typeof(Button));
        buttonObject.transform.SetParent(canvas.transform,false);prompt=buttonObject;
        var rect=buttonObject.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,68);rect.sizeDelta=new Vector2(480,72);
        buttonObject.GetComponent<Image>().color=new Color(.08f,.13f,.17f,.95f);
        buttonObject.GetComponent<Button>().onClick.AddListener(()=>TryInteract());
        var textObject=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));textObject.transform.SetParent(buttonObject.transform,false);
        var textRect=textObject.GetComponent<RectTransform>();textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=textRect.offsetMax=Vector2.zero;
        label=textObject.GetComponent<TextMeshProUGUI>();label.fontSize=34;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        prompt.SetActive(false);
    }
    public void RefreshTarget()
    {
        target=null;
        if(!GameplayState.CanAct || feet==null)return;
        float best=talkDistance*talkDistance;
        foreach(var npc in stories)
            if(npc!=null && npc.isActiveAndEnabled && npc.IsAvailable && npc.CurrentDialogue!=null)Consider(npc,ref best);
        foreach(var npc in legacy)
            if(npc!=null && npc.CanTalk)Consider(npc,ref best);
    }
    private void Consider(MonoBehaviour npc,ref float best)
    {
        var solid=npc.transform.parent.GetComponent<Collider2D>();
        Vector2 position=solid!=null?(Vector2)solid.bounds.center:(Vector2)npc.transform.parent.position;
        float distance=(position-(Vector2)feet.bounds.center).sqrMagnitude;
        if(distance>best)return;
        // Walls, trees and other solid props block interaction through them.
        foreach(var hit in Physics2D.LinecastAll(feet.bounds.center,position))
            if(!hit.collider.isTrigger && !hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(npc.transform.parent))return;
        best=distance;target=npc;
    }
    public bool TryInteract()
    {
        RefreshTarget();
        bool opened=target is StoryNPC story?story.TryTalk():target is NPCDialogueTrigger old && old.TryTalk();
        if(opened && prompt!=null)prompt.SetActive(false);
        return opened;
    }
    private void Update()
    {
        RefreshTarget();
        prompt.SetActive(target!=null);
        if(target==null)return;
        string speaker=target is StoryNPC story?story.CurrentDialogue.speakerName:((NPCDialogueTrigger)target).SpeakerName;
        label.text="[E] Nói chuyện — "+speaker;
        if(Input.GetKeyDown(KeyCode.E))TryInteract();
    }
    private void OnDisable(){target=null;if(prompt!=null)prompt.SetActive(false);}
    private void OnDestroy(){if(prompt!=null)Destroy(prompt);}
}

