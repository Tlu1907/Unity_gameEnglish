using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reveals the exit after the research encounter is completed and its enemies actually die.</summary>
public sealed class CityExitPortal : MonoBehaviour
{
    public GameObject portalVisual;
    public Collider2D entrance;
    [Tooltip("Scene name or asset path registered in Build Profiles.")]
    public string destination = "";
    private readonly Dictionary<Health, Action> subscriptions = new Dictionary<Health, Action>();
    private readonly HashSet<Health> tracked = new HashSet<Health>();
    private Renderer[] visuals;
    private bool[] visibleStates;
    private Collider2D[] colliders;
    private bool[] colliderStates;
    public int RemainingEnemies { get; private set; }
    public bool IsUnlocked { get; private set; }
    private bool loading, destinationReported;
    private float revealedAt;

    private void Awake()
    {
        if (portalVisual == null) portalVisual = gameObject;
        if (entrance == null)
        {
            var trigger = gameObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            var sprite = portalVisual.GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null && sprite.sprite != null && sprite.transform == transform)
            {
                trigger.size = sprite.sprite.bounds.size * .6f;
                trigger.offset = sprite.sprite.bounds.center;
            }
            entrance = trigger;
        }
        entrance.isTrigger = true;
        visuals = portalVisual.GetComponentsInChildren<Renderer>(true);
        visibleStates = Array.ConvertAll(visuals, view => view.enabled);
        colliders = portalVisual.GetComponentsInChildren<Collider2D>(true);
        colliderStates = Array.ConvertAll(colliders, collider => collider.enabled);
        SetVisible(false);
    }
    private void OnEnable() => StoryProgress.ResearchDocumentsDecoded += OnResearchDecoded;
    private void OnDisable() => StoryProgress.ResearchDocumentsDecoded -= OnResearchDecoded;
    private void Start() { RegisterEnemies(); TryUnlock(); }
    private void OnResearchDecoded() { RegisterEnemies(); TryUnlock(); }
    private void RegisterEnemies()
    {
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include))
        {
            if (enemy.gameObject.scene != gameObject.scene || enemy.EncounterMode != EnemyEncounterMode.LaboratoryAmbush) continue;
            var health = enemy.GetComponent<Health>();
            if (health == null || !tracked.Add(health) || health.IsDead) continue;
            Action died = () => EnemyDied(health);
            subscriptions.Add(health, died);
            health.Died += died;
            RemainingEnemies++;
        }
    }
    private void EnemyDied(Health health)
    {
        if (!subscriptions.TryGetValue(health, out var handler)) return;
        health.Died -= handler;
        subscriptions.Remove(health);
        RemainingEnemies--;
        TryUnlock();
    }
    private void TryUnlock()
    {
        if (IsUnlocked || !StoryProgress.HasDecodedResearchDocuments || tracked.Count == 0 || RemainingEnemies != 0) return;
        IsUnlocked = true;
        revealedAt = Time.unscaledTime;
        SetVisible(true);
    }
    private void SetVisible(bool visible)
    {
        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i] != null) visuals[i].enabled = visible && visibleStates[i];
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = visible && colliderStates[i];
        entrance.enabled = visible;
    }
    private void OnDestroy()
    {
        foreach (var pair in subscriptions) if (pair.Key != null) pair.Key.Died -= pair.Value;
        subscriptions.Clear();
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsUnlocked || loading || !GameplayState.CanAct) return;
        var player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.TryGetComponent<Health>(out var health) || health.IsDead) return;
        if (string.IsNullOrWhiteSpace(destination) || !Application.CanStreamedLevelBeLoaded(destination))
        {
            if (!destinationReported) Debug.LogWarning("Exit unlocked. Set CityExitPortal.destination to the next scene and add it to Build Profiles: " + destination, this);
            destinationReported = true;
            return;
        }
        loading = true;
        Time.timeScale = 1f;
        GameplayState.Reset();
        SceneManager.LoadScene(destination);
    }
    private void OnGUI()
    {
        if (!IsUnlocked || loading || Time.unscaledTime - revealedAt > 9f) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        GUI.Box(new Rect(Screen.width / 2f - 270, 90, 540, 60), "Research enemies defeated!\nThe exit portal is open.", style);
    }
}
