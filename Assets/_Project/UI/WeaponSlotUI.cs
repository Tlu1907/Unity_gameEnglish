using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WeaponSlotUI : MonoBehaviour
{
    public PlayerInventory source;
    public TMP_Text label;
    public Image icon;
    private void OnEnable() { if (source != null) source.Changed += Refresh; Refresh(); }
    private void OnDisable() { if (source != null) source.Changed -= Refresh; }
    private void Refresh()
    {
        bool equipped = source != null && source.HasWeapon;
        icon.enabled = equipped;
        label.text = equipped ? "Đã trang bị · T" : "Chưa có vũ khí";
    }
}
