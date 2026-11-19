using UnityEngine;

/// <summary>Room entry and the successful document puzzle are two separate story signals.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ResearchRoomStoryTrigger : MonoBehaviour
{
    private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;
    private void OnTriggerEnter2D(Collider2D other) => Enter(other);
    private void OnTriggerStay2D(Collider2D other) => Enter(other);
    private void Enter(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerInventory>() != null && GameplayState.CanAct)
            StoryProgress.TryEnterResearchRoom();
    }
    // Wire the document puzzle's SUCCESS event to this method; entry alone never spawns enemies.
    public void OnDocumentsDecoded() => StoryProgress.TryDecodeResearchDocuments();
}
