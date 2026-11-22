using UnityEngine;
public sealed class HealthPickup : MonoBehaviour
{
    [SerializeField, Min(1)] private int amount = 40;
    private bool collected;
    public bool TryCollect(Health player)
    {
        if (collected || !GameplayState.CanAct || player == null ||
            player.GetComponent<PlayerMovement>() == null || player.IsDead || player.Current >= player.Maximum) return false;
        var drop = GetComponent<LootDrop>();
        if (drop != null && !drop.IsReady) return false;
        collected = true;
        player.Heal(amount);
        gameObject.SetActive(false);
        return true;
    }
    private void OnTriggerEnter2D(Collider2D other) => TryCollect(other.GetComponentInParent<Health>());
    private void OnTriggerStay2D(Collider2D other) => TryCollect(other.GetComponentInParent<Health>());
}
