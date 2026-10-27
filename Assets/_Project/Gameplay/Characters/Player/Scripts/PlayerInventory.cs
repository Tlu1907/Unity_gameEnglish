using System;
using UnityEngine;

public enum PlayerItemOption { None, Weapon }

[DisallowMultipleComponent]
public sealed class PlayerInventory : MonoBehaviour
{
    [SerializeField] private PlayerItemOption currentItem;
    public PlayerItemOption CurrentItem => currentItem;
    public bool HasWeapon => currentItem != PlayerItemOption.None;
    public event Action Changed;
    public void SetItem(PlayerItemOption item)
    {
        if (currentItem == item) return;
        currentItem = item;
        Changed?.Invoke();
    }
    public void SetEquipped(bool equipped) => SetItem(equipped ? PlayerItemOption.Weapon : PlayerItemOption.None);

    [ContextMenu("Equipment/Equip (Play Mode)")]
    private void Equip() { if (Application.isPlaying) SetEquipped(true); }
    [ContextMenu("Equipment/Unequip (Play Mode)")]
    private void Unequip() { if (Application.isPlaying) SetEquipped(false); }
}
