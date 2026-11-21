using UnityEngine;
using UnityEngine.SceneManagement;

// There are no save files in this game. When the party dies (or you pick LOOP in the menu),
// time loops back to the start of the scene. You can loop 3 times,
// and every loop takes something from you (coming later: losing sight of your friend).
//
// This is a static class, so it is NOT reset when the scene reloads.
// It IS reset when the game starts fresh (pressing Play, refreshing the web page).
//
// For the "loop takes something" effects later: in that script's Start(), read
// LoopManager.LoopsUsed (0 = first run, 1 = after the first loop, 2, 3).
public static class LoopManager
{
    public const int MaxLoops = 3;

    public static int LoopsUsed { get; private set; }
    public static int LoopsLeft => MaxLoops - LoopsUsed;
    public static bool CanLoop => LoopsUsed < MaxLoops;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => LoopsUsed = 0;

    // Spend one loop and go back to the start. Does nothing if none are left.
    public static void Loop()
    {
        if (!CanLoop) return;
        LoopsUsed++;
        Sfx.Play("loop_warp", 0.9f, 1f, 0f);
        Debug.Log($"Looping back... ({LoopsUsed}/{MaxLoops} used, {LoopsLeft} left)");
        ReloadScene();
    }

    // Back to the title screen after the end: every loop back, nothing reloaded
    public static void ResetLoops() => LoopsUsed = 0;

    // After GAME OVER: a brand-new run with every loop back.
    public static void StartOver()
    {
        LoopsUsed = 0;
        Debug.Log("Starting over.");
        ReloadScene();
    }

    static void ReloadScene()
    {
        PauseMenu.SetFrozen(false);   // undo anything the menus froze
        GameState.Current = GameMode.Exploring;
        GameState.InDungeon = false;   // LoopStart puts the party back in after a loop
        RunStats.Rescued.Clear();      // the survivors are back in the silk

        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }
#if UNITY_EDITOR
        // The scene isn't ticked in File > Build Profiles > Scene List.
        // Fine while testing in the editor, but it must be ticked for the web build.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogError($"Can't reload '{scene.name}': it isn't in the build's scene list.");
#endif
    }
}
