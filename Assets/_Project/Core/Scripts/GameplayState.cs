using System;
using UnityEngine;

public enum GameplayMode { Playing, Quiz, Paused, GameOver, Dialogue }

public static class GameplayState
{
    public static GameplayMode Current { get; private set; } = GameplayMode.Playing;
    public static bool CanAct => Current == GameplayMode.Playing;
    public static event Action<GameplayMode> Changed;
    private static GameplayMode beforeQuiz;
    private static object quizOwner;
    private static object dialogueOwner;
    public static bool BeginDialogue(object owner)
    {
        if (owner == null || dialogueOwner != null || !CanAct) return false;
        dialogueOwner = owner;
        Set(GameplayMode.Dialogue);
        return true;
    }
    public static void EndDialogue(object owner)
    {
        if (!ReferenceEquals(owner, dialogueOwner)) return;
        dialogueOwner = null;
        if (Current == GameplayMode.Dialogue) Set(GameplayMode.Playing);
    }
    public static bool BeginQuiz(object owner)
    {
        if (owner == null || quizOwner != null || Current == GameplayMode.GameOver || Current == GameplayMode.Dialogue) return false;
        beforeQuiz = Current;
        quizOwner = owner;
        Set(GameplayMode.Quiz);
        return true;
    }
    public static void EndQuiz(object owner)
    {
        if (!ReferenceEquals(owner, quizOwner)) return;
        quizOwner = null;
        if (Current == GameplayMode.Quiz) Set(beforeQuiz);
    }
    public static void Pause() { if (CanAct) Set(GameplayMode.Paused); }
    public static void Resume() { if (Current == GameplayMode.Paused) Set(GameplayMode.Playing); }
    public static void GameOver() => Set(GameplayMode.GameOver);
    private static void Set(GameplayMode mode) { Current = mode; Changed?.Invoke(mode); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Current = GameplayMode.Playing;
        beforeQuiz = GameplayMode.Playing;
        quizOwner = null;
        Changed = null;
        dialogueOwner = null;
    }
}
