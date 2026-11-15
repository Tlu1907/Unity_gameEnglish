using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays equipment armor independently of the player's world scale.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public sealed class PlayerArmorBar : MonoBehaviour
{
    private Health health;
    private GameObject canvasObject;
    private Image fill;
    private Text label;

    private void Start()
    {
        health = GetComponent<Health>();
        canvasObject = new GameObject("Player Armor HUD", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var background = new GameObject("Armor", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = background.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(24, -24);
        rect.sizeDelta = new Vector2(260, 28);
        background.GetComponent<Image>().color = new Color(.06f, .09f, .15f, .9f);
        background.GetComponent<Image>().raycastTarget = false;

        var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(background.transform, false);
        fill = fillObject.GetComponent<Image>();
        fill.color = new Color(.2f, .65f, 1f);
        fill.raycastTarget = false;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(background.transform, false);
        label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        RectTransform textRect = label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        health.ArmorChanged += Refresh;
        Refresh(health.Armor);
    }

    private void Refresh(int value)
    {
        fill.rectTransform.anchorMax = new Vector2((float)value / Mathf.Max(1, health.MaximumArmor), 1);
        label.text = $"GIÁP {value}/{health.MaximumArmor}";
    }

    private void OnDisable() { if (canvasObject != null) canvasObject.SetActive(false); }
    private void OnEnable() { if (canvasObject != null) canvasObject.SetActive(true); }
    private void OnDestroy()
    {
        if (health != null) health.ArmorChanged -= Refresh;
        if (canvasObject != null) Destroy(canvasObject);
    }
}
