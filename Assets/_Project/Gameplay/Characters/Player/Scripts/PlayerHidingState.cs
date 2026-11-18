using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared hiding state. Each overlapping hiding spot owns one source.</summary>
public sealed class PlayerHidingState : MonoBehaviour
{
    private readonly HashSet<UnityEngine.Object> sources = new HashSet<UnityEngine.Object>();
    public bool IsHidden => sources.Count > 0;
    public event Action<bool> Changed;
    public void SetHidden(UnityEngine.Object source, bool hidden)
    {
        if (source == null) return;
        bool before = IsHidden;
        if (hidden) sources.Add(source); else sources.Remove(source);
        if (before != IsHidden) Changed?.Invoke(IsHidden);
    }
    private void OnDisable()
    {
        bool before = IsHidden;
        sources.Clear();
        if (before) Changed?.Invoke(false);
    }
}
