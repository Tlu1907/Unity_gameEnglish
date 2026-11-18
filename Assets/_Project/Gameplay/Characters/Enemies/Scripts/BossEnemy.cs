using UnityEngine;

/// <summary>
/// Quái boss Cổng Bắc: HP cao hơn, rớt key khi chết.
/// Thừa kế EnemyBrain để hưởng đầy đủ hành vi tuần tra + truy đuổi.
/// </summary>
public sealed class BossEnemy : EnemyBrain
{
    [SerializeField] private GameObject keyDropPrefab;
    [SerializeField, Min(1)] private int bossMaxHp = 300;

    protected override void Awake()
    {
        base.Awake();
        // Nâng HP sau khi EnsureComponents() trong base.Awake tạo xong Health.
        var h = GetComponent<Health>();
        if (h != null) h.Configure(bossMaxHp, bossMaxHp);
        // Thêm handler rớt key vào sự kiện Died — EnemyBrain.OnEnable đã đăng ký Die().
        if (h != null) h.Died += DropKeyBeforeDie;
    }

    private void OnDestroy()
    {
        var h = GetComponent<Health>();
        if (h != null) h.Died -= DropKeyBeforeDie;
    }

    private void DropKeyBeforeDie()
    {
        if (keyDropPrefab == null) return;
        // Instantiate trước khi EnemyBrain.Die() ẩn/destroy gameObject.
        Instantiate(keyDropPrefab, transform.position, Quaternion.identity);
    }
}
