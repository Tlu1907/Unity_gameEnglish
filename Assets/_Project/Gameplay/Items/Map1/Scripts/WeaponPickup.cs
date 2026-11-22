using UnityEngine;
public sealed class WeaponPickup : MonoBehaviour
{
    private bool collected;
    public bool TryCollect(PlayerInventory player)
    {
        if (collected || !GameplayState.CanAct || player == null || player.HasWeapon || player.GetComponent<PlayerMovement>() == null) return false;
        var drop = GetComponent<LootDrop>();
        if (drop != null && !drop.IsReady) return false;
        collected = true; player.SetEquipped(true); gameObject.SetActive(false); return true;
    }
    private void OnTriggerEnter2D(Collider2D other) => TryCollect(other.GetComponentInParent<PlayerInventory>());
    private void OnTriggerStay2D(Collider2D other) => TryCollect(other.GetComponentInParent<PlayerInventory>());
}
