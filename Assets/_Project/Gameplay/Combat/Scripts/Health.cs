using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private int maximum = 200;
    [SerializeField] private int current = 200;
    public int Maximum => maximum;
    public int Current => current;
    public bool IsDead => current <= 0;
    public event Action<int> Changed;
    public event Action Died;
    public void Configure(int maxHealth, int currentHealth)
    {
        maximum = Mathf.Max(1, maxHealth);
        current = Mathf.Clamp(currentHealth, 0, maximum);
    }
    public void TakeDamage(int amount)
    {
        if (!GameplayState.CanAct || IsDead || amount <= 0) return;
        current = Mathf.Max(0, current - amount);
        Changed?.Invoke(current);
        if (IsDead) Died?.Invoke();
    }
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        current = Mathf.Min(maximum, current + amount);
        Changed?.Invoke(current);
    }
}
