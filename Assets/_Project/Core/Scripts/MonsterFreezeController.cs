/// <summary>Legacy API. Quiz now suspends gameplay for its entire session.</summary>
public static class MonsterFreezeController
{
    public static bool IsFrozen => GameplayState.Current == GameplayMode.Quiz;
    [System.Obsolete("Freeze duration is retained only for source compatibility. Use GameplayState.")]
    public static void TriggerFreeze(float duration) { }
}
