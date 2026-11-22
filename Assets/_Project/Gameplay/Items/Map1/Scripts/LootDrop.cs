using UnityEngine;

/// <summary>Visible toss before a dropped item becomes collectable.</summary>
public sealed class LootDrop : MonoBehaviour
{
    private Vector3 landing;
    private float elapsed;
    public bool IsReady => elapsed >= .6f;
    private void Awake() => landing = transform.position;
    private void Update()
    {
        if (!GameplayState.CanAct || IsReady) return;
        elapsed = Mathf.Min(.6f, elapsed + Time.deltaTime);
        transform.position = landing + Vector3.up * (Mathf.Sin(elapsed / .6f * Mathf.PI) * .8f);
        if (IsReady) transform.position = landing;
    }
}
