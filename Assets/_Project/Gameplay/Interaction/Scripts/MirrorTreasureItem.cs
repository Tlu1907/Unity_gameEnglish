using UnityEngine;

/// <summary>Mirror contacts open the generated English question instead of a local multiple-choice quiz.</summary>
public class MirrorTreasureItem : QuizTrigger
{
    [SerializeField] private FlaskEnglishQuizPanel flaskPanel;
    private bool awaitingExit;

    public override void Interact()
    {
        if (IsCompleted || awaitingExit || !GameplayState.CanAct) return;
        if (flaskPanel == null) flaskPanel = FlaskEnglishQuizPanel.FindOrCreate();
        if (flaskPanel == null)
        {
            Debug.LogWarning("Mirror: add the Flask English Quiz UI to this scene using GameObject/UI/Flask English Quiz (Test).", this);
            return;
        }
        awaitingExit = flaskPanel.TryOpen(HandleResult);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMovement>() != null) awaitingExit = false;
    }
}
