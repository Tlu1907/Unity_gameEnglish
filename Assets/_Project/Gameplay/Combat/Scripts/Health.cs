using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private int maximum = 200;
    [SerializeField] private int current = 200;
    [SerializeField, Min(1)] private int maximumArmor = 100;
    [SerializeField] private int armor;
    [SerializeField, Min(0)] private float invulnerabilitySeconds;
    private float invulnerabilityRemaining;
    private void Update() { if (GameplayState.CanAct) invulnerabilityRemaining = Mathf.Max(0, invulnerabilityRemaining - Time.deltaTime); }
    private bool armorEquipped;
    private int bonusArmor;
    public int MaximumArmor => maximumArmor + bonusArmor;
    public int Armor => armor;
    public event Action<int> ArmorChanged;
    public int Maximum => maximum;
    public int Current => current;
    public bool IsDead => current <= 0;
    public event Action<int> Changed;
    public event Action Died;
    public void SetArmorEquipped(bool equipped)
    {
        if (armorEquipped == equipped) return;
        armorEquipped = equipped;
        armor = equipped ? MaximumArmor : Mathf.Min(armor, bonusArmor);
        ArmorChanged?.Invoke(armor);
    }
    public void AddBonusArmor(int amount)
    {
        if (amount <= 0 || IsDead) return;
        bonusArmor += amount;
        armor += amount;
        ArmorChanged?.Invoke(armor);
    }
    public void Configure(int maxHealth, int currentHealth)
    {
        maximum = Mathf.Max(1, maxHealth);
        current = Mathf.Clamp(currentHealth, 0, maximum);
    }
    public void TakeDamage(int amount)
    {
        if (!GameplayState.CanAct || IsDead || amount <= 0 || invulnerabilityRemaining > 0) return;
        var enemy = GetComponent<EnemyBrain>();
        if (enemy != null && !enemy.IsEncounterActive) return;
        invulnerabilityRemaining = invulnerabilitySeconds;
        int absorbed = Mathf.Min(armor, amount);
        if (absorbed > 0)
        {
            armor -= absorbed;
            amount -= absorbed;
            ArmorChanged?.Invoke(armor);
        }
        if (amount == 0) return;
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
    public void RestoreHealthAndArmor()
    {
        if (IsDead) return;
        current = maximum;
        armor = MaximumArmor;
        Changed?.Invoke(current);
        ArmorChanged?.Invoke(armor);
    }
}
