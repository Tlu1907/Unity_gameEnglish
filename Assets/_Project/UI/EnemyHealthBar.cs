using UnityEngine;
using UnityEngine.UI;

/// <summary>Small non-interactive health bar following the animated creature's head.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Health))]
public sealed class EnemyHealthBar : MonoBehaviour
{
    private Health health;
    private SpriteRenderer body;
    private GameObject display;
    private RectTransform fill;
    private Image fillImage;
    private Camera view;
    public float Fraction => health == null ? 0 : Mathf.Clamp01((float)health.Current / health.Maximum);

    private void Awake()
    {
        health = GetComponent<Health>();
        var animator = GetComponent<CharacterAnimation>()?.Animator;
        body = animator != null ? animator.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        display = new GameObject("Enemy HP", typeof(RectTransform), typeof(Canvas));
        display.transform.SetParent(transform, false);
        var canvas = display.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true; canvas.sortingLayerName = "WorldDepth"; canvas.sortingOrder = 31000;
        var rect = (RectTransform)display.transform; rect.sizeDelta = new Vector2(100, 12);
        var background = display.AddComponent<Image>(); background.color = new Color(.08f, .06f, .08f, .95f); background.raycastTarget = false;
        var inner = new GameObject("Fill", typeof(RectTransform), typeof(Image)); inner.transform.SetParent(display.transform, false);
        fill = inner.GetComponent<RectTransform>(); fill.pivot = new Vector2(0, .5f);
        fill.anchorMin = fill.anchorMax = new Vector2(0, .5f); fill.anchoredPosition = new Vector2(2, 0);
        fillImage = inner.GetComponent<Image>(); fillImage.raycastTarget = false;
        Refresh(health.Current);
    }
    private void OnEnable() { health.Changed += Refresh; Refresh(health.Current); }
    private void OnDisable() { health.Changed -= Refresh; if (display != null) display.SetActive(false); }
    private void Refresh(int value)
    {
        if (display == null) return;
        display.SetActive(isActiveAndEnabled && !health.IsDead);
        fill.sizeDelta = new Vector2(96 * Fraction, 8);
        fillImage.color = Fraction > .5f ? new Color(.3f, .85f, .25f) : Fraction > .25f ? new Color(1, .7f, .15f) : new Color(.95f, .2f, .15f);
    }
    private void LateUpdate()
    {
        if (body == null || display == null) return;
        if (view == null) view = Camera.main;
        display.transform.position = new Vector3(body.bounds.center.x, body.bounds.max.y + .16f, 0);
        display.transform.rotation = view != null ? view.transform.rotation : Quaternion.identity;
        Vector3 scale = transform.lossyScale;
        display.transform.localScale = new Vector3(.009f / Mathf.Max(.001f, Mathf.Abs(scale.x)), .009f / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
    }
}
