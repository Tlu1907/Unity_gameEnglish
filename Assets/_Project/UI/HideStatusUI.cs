using TMPro;
using UnityEngine;

public sealed class HideStatusUI : MonoBehaviour
{
    public PlayerHidingState source;
    public TMP_Text label;
    private void OnEnable() { if (source != null) source.Changed += Refresh; Refresh(source != null && source.IsHidden); }
    private void OnDisable() { if (source != null) source.Changed -= Refresh; }
    private void Refresh(bool hidden) { label.enabled = hidden; }
}
