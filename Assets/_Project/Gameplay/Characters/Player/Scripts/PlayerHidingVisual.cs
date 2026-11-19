using UnityEngine;

/// <summary>Only changes opacity; original sprites, Animator and movement remain untouched.</summary>
public sealed class PlayerHidingVisual : MonoBehaviour
{
    private PlayerHidingState state;
    private SpriteRenderer body,weapon;
    private float bodyAlpha=1;
    private void Start()
    {
        state=GetComponent<PlayerHidingState>();
        body=GetComponent<CharacterAnimation>().Animator.GetComponent<SpriteRenderer>();
        bodyAlpha=body.color.a;
    }
    private void LateUpdate()
    {
        if(state==null || body==null)return;
        if(weapon==null){var child=transform.Find("EquippedWeapon");if(child!=null)weapon=child.GetComponent<SpriteRenderer>();}
        SetAlpha(body,state.IsHidden?bodyAlpha*.5f:bodyAlpha);
        if(weapon!=null)SetAlpha(weapon,state.IsHidden?.5f:1);
    }
    private static void SetAlpha(SpriteRenderer renderer,float alpha){var color=renderer.color;color.a=alpha;renderer.color=color;}
    private void OnDisable(){if(body!=null)SetAlpha(body,bodyAlpha);if(weapon!=null)SetAlpha(weapon,1);}
}
