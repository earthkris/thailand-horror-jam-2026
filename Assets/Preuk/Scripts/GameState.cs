using UnityEngine;

public enum GameMode { Exploring, Dialogue, Menu, Combat }

public static class GameState
{
    public static GameMode Current = GameMode.Exploring;

    public static bool CanMove => Current == GameMode.Exploring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Current = GameMode.Exploring;
}