using UnityEngine;

/// <summary>Spawns a health/weapon pickup once when the quiz unlocks this chest.</summary>
[RequireComponent(typeof(Chest))]
public sealed class ChestWeaponReward : MonoBehaviour
{
    [SerializeField] private GameObject dropPrefab;
    private Chest chest;
    private bool awarded;
    public GameObject DroppedItem { get; private set; }
    private void OnEnable()
    {
        chest = GetComponent<Chest>();
        chest.Opened += Award;
    }
    private void OnDisable() { if (chest != null) chest.Opened -= Award; }
    private void Award()
    {
        if (awarded || !chest.AllowsReward || dropPrefab == null) return;
        awarded = true;
        DroppedItem = Instantiate(dropPrefab, transform.position, Quaternion.identity, transform.parent);
        DroppedItem.AddComponent<LootDrop>();
    }
}
