using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Projects inventory state onto the Visual; never owns equipment or movement state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventory), typeof(CharacterAnimation))]
public sealed class PlayerAppearance : MonoBehaviour
{
    [SerializeField] private RuntimeAnimatorController baseController;
    [SerializeField] private AnimatorOverrideController equippedController;
    private PlayerInventory inventory;
    private CharacterAnimation visuals;
    private bool warned;
    private bool started;

    private void OnEnable()
    {
        inventory = GetComponent<PlayerInventory>();
        visuals = GetComponent<CharacterAnimation>();
        inventory.Changed += Apply;
        if (started) Apply();
    }
    private void Start()
    {
        visuals.RefreshController();
        Animator animator = visuals.Animator;
        if (baseController == null && animator != null)
        {
            baseController = animator.runtimeAnimatorController;
            if (baseController is AnimatorOverrideController overrides)
                baseController = overrides.runtimeAnimatorController;
        }
        started = true;
        Apply();
    }
    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= Apply;
    }
    private void Apply()
    {
        if (visuals == null) return;
        Animator animator = visuals.Animator;
        if (animator == null || baseController == null)
        {
            Warn("Assign CharacterAnimation.Animator to Visual and assign Base Controller.");
            return;
        }
        RuntimeAnimatorController desired = baseController;
        // Equipment is a separate sprite; keep the original body and all eight base animations.
        if (animator.runtimeAnimatorController == desired) return;
        // Both controllers share the same state machine, so Unity retains the current state.
        animator.runtimeAnimatorController = desired;
        visuals.RefreshController();
    }
    private bool HasCompleteOverrides()
    {
        if (equippedController == null || equippedController.runtimeAnimatorController != baseController) return false;
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        equippedController.GetOverrides(overrides);
        string[] names = { "Idle_Down", "Idle_Left", "Idle_Right", "Idle_Up",
            "Walk_Down", "Walk_Left", "Walk_Right", "Walk_Up" };
        foreach (string name in names)
        {
            bool found = false;
            foreach (var pair in overrides)
            {
                if (pair.Key == null || !string.Equals(pair.Key.name, "U_" + name, StringComparison.OrdinalIgnoreCase)) continue;
                if (pair.Value == null || !string.Equals(pair.Value.name, "E_" + name, StringComparison.OrdinalIgnoreCase)) return false;
                found = true;
            }
            if (!found) return false;
        }
        return true;
    }
    private void Warn(string message)
    {
        if (warned) return;
        warned = true;
        Debug.LogWarning(message, this);
    }
}
