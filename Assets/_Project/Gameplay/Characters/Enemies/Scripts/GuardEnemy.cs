using UnityEngine;

/// <summary>
/// Quái canh cổng: đứng yên tại vị trí spawn, chỉ tấn công khi player đến gần.
/// Ghi đè Update của EnemyBrain để vô hiệu hoàn toàn logic tuần tra/đuổi.
/// </summary>
public sealed class GuardEnemy : EnemyBrain
{
    private EnemyAttack attack;
    private Health playerHealth;

    protected override void Awake()
    {
        base.Awake();
        attack = GetComponent<EnemyAttack>();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerHealth = player.GetComponent<Health>();
    }

    protected override void Update()
    {
        // Không di chuyển — chỉ tấn công khi player trong tầm.
        if (!GameplayState.CanAct || attack == null || playerHealth == null) return;
        if (playerHealth.IsDead) return;

        var player = playerHealth.gameObject;
        GetComponent<CharacterAnimation>()?.Face(player.transform.position - transform.position);
        attack.TryAttack(playerHealth);
    }
}
