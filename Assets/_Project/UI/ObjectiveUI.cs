using TMPro;
using UnityEngine;

public sealed class ObjectiveUI : MonoBehaviour
{
    public LevelObjectives source;
    public TMP_Text label;
    private void OnEnable() { if (source != null) source.Changed += Refresh; Refresh(); }
    private void OnDisable() { if (source != null) source.Changed -= Refresh; }
    private void Refresh() { if (source != null) label.text = $"Tiếp tế {source.OpenedCount}/{source.Target}"; }
}
