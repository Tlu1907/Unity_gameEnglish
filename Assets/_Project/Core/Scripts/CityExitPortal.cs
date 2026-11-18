using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>The station exit unlocks from actual enemy deaths, never from hiding/disabling enemies.</summary>
public sealed class CityExitPortal : MonoBehaviour
{
    public GameObject portalVisual;
    public Collider2D entrance;
    public string destination = "Assets/_Project/Scenes/Scene_Test/SampleScene.unity";
    private readonly Dictionary<Health, Action> subscriptions = new Dictionary<Health, Action>();
    public int RemainingEnemies { get; private set; }
    public bool IsUnlocked { get; private set; }
    private bool loading;
    private float revealedAt;

    private void Awake() { portalVisual.SetActive(false); entrance.enabled = false; }
    private void Start()
    {
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (enemy.gameObject.scene != gameObject.scene) continue;
            var health = enemy.GetComponent<Health>();
            if (health == null || health.IsDead) continue;
            Action died = () => EnemyDied(health);
            subscriptions.Add(health, died); health.Died += died; RemainingEnemies++;
        }
        if (RemainingEnemies == 0) Unlock();
    }
    private void EnemyDied(Health health)
    {
        if (!subscriptions.TryGetValue(health, out var handler)) return;
        health.Died -= handler; subscriptions.Remove(health);
        RemainingEnemies--;
        if (RemainingEnemies == 0) Unlock();
    }
    private void Unlock()
    {
        if (IsUnlocked) return;
        IsUnlocked = true; revealedAt = Time.unscaledTime;
        portalVisual.SetActive(true); entrance.enabled = true;
    }
    private void OnDestroy()
    {
        foreach (var pair in subscriptions) if (pair.Key != null) pair.Key.Died -= pair.Value;
        subscriptions.Clear();
    }
    private void Update()
    {
        if (IsUnlocked) portalVisual.transform.localScale = Vector3.one * (1f + .04f * Mathf.Sin(Time.time * 3f));
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsUnlocked || loading || !GameplayState.CanAct) return;
        var player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || player.GetComponent<Health>().IsDead) return;
        if (!Application.CanStreamedLevelBeLoaded(destination))
        { Debug.LogError("Portal destination is not in Build Settings: " + destination, this); return; }
        loading = true;
        GameplayState.Reset();
        SceneManager.LoadScene(destination);
    }
    private void OnGUI()
    {
        if (!IsUnlocked || loading) return;
        var player = FindAnyObjectByType<PlayerMovement>();
        if (Time.unscaledTime - revealedAt > 9 && (player == null || Vector2.Distance(player.transform.position, transform.position) > 5)) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        GUI.Box(new Rect(Screen.width / 2f - 270, 90, 540, 60), "Đã tiêu diệt toàn bộ quái!\nCổng tại cửa ga đã mở — bước vào để sang màn tiếp theo.", style);
    }
}
