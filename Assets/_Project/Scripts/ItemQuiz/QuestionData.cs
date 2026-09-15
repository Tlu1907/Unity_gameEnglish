using UnityEngine;

[CreateAssetMenu(fileName = "QuestionData", menuName = "EarthDefender/Question Data")]
public class QuestionData : ScriptableObject
{
    public string id;
    public string skillTag;

    [TextArea(2, 4)]
    public string content;

    public string[] answers = new string[4];
    public int correctIndex;

    [TextArea(2, 4)]
    public string explanation;

    public float freezeDuration = 3f;
}
