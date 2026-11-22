using UnityEngine;
using UnityEngine.SceneManagement;

// When the party dies (or you pick LOOP in the menu), time loops back to the start of the scene.
// You can loop 3 times, and every loop takes something from you (Lucius's face, then his voice...).
// The game saves by itself (SaveGame); a loop is saved the moment it's spent.
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

    public const string TitleScene = "StartScreen";

    // CONTINUE: the saved run's loop count
    public static void SetLoopsUsed(int loops) => LoopsUsed = Mathf.Clamp(loops, 0, MaxLoops);

    // Spend one loop and go back to the start. Does nothing if none are left.
    public static void Loop()
    {
        if (!CanLoop) return;
        LoopsUsed++;
        SaveGame.SaveLoopCheckpoint(LoopsUsed);   // spent, even if the game is closed right now
        Sfx.Play("loop_warp", 0.9f, 1f, 0f);
        Debug.Log($"Looping back... ({LoopsUsed}/{MaxLoops} used, {LoopsLeft} left)");
        ReloadScene();
    }

    // Back to the title screen after the end: every loop back, nothing reloaded
    public static void ResetLoops() => LoopsUsed = 0;

    // After GAME OVER: the run is over, its save goes, and it's back to the start screen
    public static void StartOver()
    {
        SaveGame.Delete();
        Debug.Log("Game over: back to the start screen.");
        GoToTitle();
    }

    // The start screen (GAME OVER, EXIT in the pause menu, the end screen). Every loop back.
    public static void GoToTitle()
    {
        LoopsUsed = 0;
        PauseMenu.SetFrozen(false);
        GameState.Current = GameMode.Menu;
        GameState.InDungeon = false;
        RunStats.Rescued.Clear();
        if (Application.CanStreamedLevelBeLoaded(TitleScene)) SceneManager.LoadScene(TitleScene);
        else
        {
            Debug.LogWarning($"'{TitleScene}' isn't in the build's scene list: starting the game scene over instead.");
            ReloadScene();
        }
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
