using UnityEngine;

/// <summary>Fallback weapon overlay when the body is not already using equipped animation sprites.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventory), typeof(CharacterAnimation))]
public sealed class PlayerWeaponVisual : MonoBehaviour
{
    [SerializeField] private Sprite weaponSprite;
    [SerializeField, Min(.1f)] private float worldLength = .75f;
    private PlayerInventory inventory;
    private CharacterAnimation animationState;
    private SpriteRenderer body;
    private SpriteRenderer weapon;
    public bool IsVisible => weapon != null && weapon.enabled;
    private void OnEnable()
    {
        inventory=GetComponent<PlayerInventory>();
        inventory.Changed+=Refresh;
        Refresh();
    }
    private void Start()
    {
        animationState=GetComponent<CharacterAnimation>();
        body=animationState.Animator!=null?animationState.Animator.GetComponent<SpriteRenderer>():GetComponentInChildren<SpriteRenderer>();
        if(body==null || weaponSprite==null)return;
        var go=new GameObject("EquippedWeapon");go.transform.SetParent(transform,false);
        weapon=go.AddComponent<SpriteRenderer>();weapon.sprite=weaponSprite;
        weapon.sharedMaterial=body.sharedMaterial;
        float scale=worldLength/Mathf.Max(.01f,weaponSprite.bounds.size.x);
        go.transform.localScale=new Vector3(scale/Mathf.Abs(transform.lossyScale.x),scale/Mathf.Abs(transform.lossyScale.y),1);
        Refresh();LateUpdate();
    }
    private void Refresh()
    {
        if (weapon == null) return;
        var appearance = GetComponent<PlayerAppearance>();
        weapon.enabled = isActiveAndEnabled && inventory != null && inventory.HasWeapon &&
            (appearance == null || !appearance.UsesEquippedAnimations);
    }
    private void LateUpdate()
    {
        if(weapon==null || body==null)return;
        Refresh();
        Vector2 facing=animationState.Facing;
        Vector3 hand=body.bounds.center+Vector3.down*(body.bounds.size.y*.16f);
        hand+=new Vector3(facing.x*.3f+(facing.x==0?.22f:0),facing.y*.12f,0);
        weapon.transform.position=hand;
        float angle=facing.x<0?180:facing.y>0?70:facing.y<0?-65:0;
        weapon.transform.rotation=Quaternion.Euler(0,0,angle);
        weapon.sortingLayerID=body.sortingLayerID;
        weapon.sortingOrder=body.sortingOrder+(facing.y>0?-1:1);
    }
    private void OnDisable() {if(inventory!=null)inventory.Changed-=Refresh;if(weapon!=null)weapon.enabled=false;}
    private void OnDestroy() {if(weapon!=null)Destroy(weapon.gameObject);}
}
