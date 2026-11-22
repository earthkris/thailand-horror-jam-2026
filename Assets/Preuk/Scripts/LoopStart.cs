using System.Collections;
using UnityEngine;

// After a loop the game starts again in the dungeon, where the party woke up (storyboard 9),
// not at the forest intro. On a loop this:
//   - switches off the intro video and the tutorial fight (Skip After Loop)
//   - puts the party on the trap chest's landing tile
//   - fades in from black and plays this loop's wake-up talk.
// A fresh game (or START OVER) plays the normal intro.
public class LoopStart : MonoBehaviour
{
    [Tooltip("Its Destination is where the party wakes up.")]
    public TrapChest trapChest;
    [Tooltip("Switched off after a loop: the intro video, the tutorial monster, the tutorial.")]
    public GameObject[] skipAfterLoop;
    [Tooltip("Element 0 = after the 1st loop, 1 = after the 2nd... The last one is reused when there are fewer.")]
    public DialogueScriptableObject[] wakeUp;
    public float fadeInTime = 1.4f;

    public static bool StartedFromLoop => LoopManager.LoopsUsed > 0;

    // CONTINUE from a save made while exploring: AutoSave puts the party back where it stood
    static bool ContinuingMidLoop => SaveGame.Continuing && SaveGame.Pending.hasPosition;

    // Awake runs before any Start, so the intro video never begins
    void Awake()
    {
        if (!StartedFromLoop && !SaveGame.Continuing) return;
        foreach (GameObject g in skipAfterLoop)
            if (g != null) g.SetActive(false);
        GameState.Current = GameMode.Cutscene;
        GameState.InDungeon = true;
    }

    IEnumerator Start()
    {
        if (ContinuingMidLoop)
        {
            yield return null;
            yield return null;   // AutoSave moves the party first
            ScreenFade.FadeIn(fadeInTime, 0.1f);
            GameState.Current = GameMode.Exploring;
            yield break;
        }
        if (!StartedFromLoop) yield break;
        yield return null;   // let everyone else's Start run first (Lucius places himself, the camera finds Kairos)

        var player = FindAnyObjectByType<PlayerMovement>();
        if (player != null && trapChest != null && trapChest.destination != null)
            player.TeleportParty(trapChest.LandingSpot());
        else Debug.LogWarning("LoopStart: drag in the trap chest (with a destination).", this);

        // Wake up (the screen is still black from the loop)
        ScreenFade.FadeIn(fadeInTime, 0.3f);
        yield return new WaitForSecondsRealtime(0.9f);
        GameState.Current = GameMode.Exploring;

        DialogueScriptableObject lines = StoryDialogue.Pick(wakeUp, LoopManager.LoopsUsed - 1);
        if (lines != null && DialogueBox.Instance != null) DialogueBox.Instance.StartDialogue(lines);
    }
}
