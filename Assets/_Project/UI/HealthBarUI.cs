using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HealthBarUI : MonoBehaviour
{
    public Health source;
    public Image fill;
    public TMP_Text label;
    [Min(1)] public int hitPointsPerHeart = 40;
    private void OnEnable() { if (source != null) source.Changed += Refresh; Refresh(0); }
    private void OnDisable() { if (source != null) source.Changed -= Refresh; }
    private void Refresh(int ignored)
    {
        if (source == null) return;
        fill.fillAmount = (float)source.Current / Mathf.Max(1, source.Maximum);
        label.text = $"{source.Current}/{source.Maximum} HP  ·  {(float)source.Current / hitPointsPerHeart:0.#}/{(float)source.Maximum / hitPointsPerHeart:0.#} tim";
    }
}
