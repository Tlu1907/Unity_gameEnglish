using UnityEngine;
/// <summary>Animates only the visual, keeping the trigger and shadow fixed.</summary>
public sealed class ItemBob : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.07576f;
    [SerializeField] private float frequency = 1.1f;
    private Vector3 origin;
    private float phase;
    private void Awake() => origin = transform.localPosition;
    private void Update()
    {
        if (!GameplayState.CanAct) return;
        phase += Time.deltaTime * frequency * Mathf.PI * 2;
        transform.localPosition = origin + Vector3.up * (Mathf.Sin(phase) * amplitude);
    }
}
