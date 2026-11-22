using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-owned chest progress, shared by HUD and story NPCs.</summary>
public sealed class LevelObjectives : MonoBehaviour
{
    [SerializeField, Min(1)] private int target = 4;
    private readonly HashSet<Chest> opened = new HashSet<Chest>();
    private readonly Dictionary<Chest, Action> bindings = new Dictionary<Chest, Action>();
    public int OpenedCount => opened.Count;
    public int Target => target;
    public event Action Changed;
    private void Start()
    {
        foreach (var chest in FindObjectsByType<Chest>(FindObjectsInactive.Include))
            if (chest.gameObject.scene == gameObject.scene) Register(chest);
    }
    public void Register(Chest chest)
    {
        if (chest == null || bindings.ContainsKey(chest)) return;
        Action handler = () => Record(chest);
        bindings.Add(chest, handler);
        chest.Opened += handler;
        if (chest.IsOpened) Record(chest);
    }
    private void Record(Chest chest) { if (opened.Add(chest)) Changed?.Invoke(); }
    private void OnDestroy()
    {
        foreach (var entry in bindings) if (entry.Key != null) entry.Key.Opened -= entry.Value;
        bindings.Clear();
    }
}
