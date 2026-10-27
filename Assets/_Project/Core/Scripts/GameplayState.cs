using System;
using UnityEngine;

public enum GameplayMode { Playing, Quiz, Paused, GameOver }

public static class GameplayState
{
    public static GameplayMode Current { get; private set; } = GameplayMode.Playing;
    public static bool CanAct => Current == GameplayMode.Playing;
    public static event Action<GameplayMode> Changed;
    private static GameplayMode beforeQuiz;
    private static object quizOwner;
    public static bool BeginQuiz(object owner)
    {
        if (owner == null || quizOwner != null || Current == GameplayMode.GameOver) return false;
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
    }
}
