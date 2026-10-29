using UnityEngine;

public enum GameMode { Exploring, Dialogue, Menu, Combat, Cutscene }

public static class GameState
{
    public static GameMode Current = GameMode.Exploring;

    // Walking, picking things up, bumping into monsters: only while exploring with no dialogue box open.
    // The frame a dialogue box closes still counts as open, so the E that closes it doesn't also pick up a book.
    public static bool CanMove
    {
        get
        {
            if (DialogueOpen) lastDialogueFrame = Time.frameCount;
            return Current == GameMode.Exploring && Time.frameCount - lastDialogueFrame > 1;
        }
    }

    // Earth's dialogue box (DialogueBox) is showing
    public static bool DialogueOpen => DialogueBox.Instance != null && DialogueBox.Instance.IsContentOpen;

    static int lastDialogueFrame = -10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Current = GameMode.Exploring;
        lastDialogueFrame = -10;
    }
}
