using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen-space health/armor display; resolves the Player in its own scene.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class PlayerVitalsHUD : MonoBehaviour
{
    [SerializeField] private Health source;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image armorFill;
    [SerializeField] private TMP_Text healthValue;
    [SerializeField] private TMP_Text armorValue;
    private Health bound;
    private bool subscribed;
    private float nextResolve;
    public Health Source => bound;

    public void Configure(Image hp, Image armor, TMP_Text hpText, TMP_Text armorText)
    {
        healthFill = hp; armorFill = armor; healthValue = hpText; armorValue = armorText;
        Resolve(); Refresh(0);
    }
    private void OnEnable() { nextResolve = 0; Resolve(); }
    private void OnDisable() => Unbind();
    private void Update()
    {
        if (Time.realtimeSinceStartup < nextResolve) return;
        nextResolve = Time.realtimeSinceStartup + .5f;
        Resolve(); Refresh(0);
    }
    private void Resolve()
    {
        Health candidate = source;
        if (candidate == null)
        {
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var player in root.GetComponentsInChildren<PlayerMovement>(true))
            {
                var health = player.GetComponent<Health>();
                if (health == null) continue;
                if (candidate == null || player.gameObject.activeInHierarchy && !health.IsDead) candidate = health;
            }
        }
        if (candidate == bound && subscribed) return;
        Unbind(); bound = candidate;
        if (bound != null) { bound.Changed += Refresh; bound.ArmorChanged += Refresh; subscribed = true; }
        Refresh(0);
    }
    private void Unbind()
    {
        if (bound != null) { bound.Changed -= Refresh; bound.ArmorChanged -= Refresh; }
        subscribed = false;
    }
    private void Refresh(int ignored)
    {
        int hp = bound != null ? bound.Current : 0, maxHp = bound != null ? bound.Maximum : 0;
        int armor = bound != null ? bound.Armor : 0, maxArmor = bound != null ? bound.MaximumArmor : 0;
        Fill(healthFill, hp, maxHp); Fill(armorFill, armor, maxArmor);
        if (healthValue != null) healthValue.text = hp + " / " + maxHp;
        if (armorValue != null) armorValue.text = armor + " / " + maxArmor;
        if (healthFill != null) healthFill.color = hp > maxHp * .25f ? new Color(.94f, .25f, .34f) : new Color(1f, .47f, .22f);
    }
    private static void Fill(Image image, int value, int maximum)
    {
        if (image == null) return;
        float fraction = Mathf.Clamp01((float)value / Mathf.Max(1, maximum));
        image.rectTransform.anchorMax = new Vector2(fraction, 1f);
        if (image.gameObject.activeSelf != (fraction > 0)) image.gameObject.SetActive(fraction > 0);
    }
}
