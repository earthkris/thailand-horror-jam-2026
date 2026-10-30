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

    // Fired right after the scene reloads for a loop (before any Start() runs).
    // The number is how many loops are used now (1, 2 or 3).
    public static event System.Action<int> OnLooped;

    static bool looping;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        LoopsUsed = 0;
        looping = false;
        OnLooped = null;
        SceneManager.sceneLoaded -= AfterReload;
        SceneManager.sceneLoaded += AfterReload;
    }

    // Spend one loop and go back to the start. Does nothing if none are left.
    public static void Loop()
    {
        if (!CanLoop) return;
        LoopsUsed++;
        looping = true;
        Debug.Log($"Looping back... ({LoopsUsed}/{MaxLoops} used, {LoopsLeft} left)");
        ReloadScene();
    }

    // After GAME OVER: a brand-new run with every loop back.
    public static void StartOver()
    {
        LoopsUsed = 0;
        looping = false;
        Debug.Log("Starting over.");
        ReloadScene();
    }

    static void AfterReload(Scene scene, LoadSceneMode mode)
    {
        if (!looping) return;
        looping = false;
        OnLooped?.Invoke(LoopsUsed);
    }

    static void ReloadScene()
    {
        // Undo anything the menus froze
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameState.Current = GameMode.Exploring;
        GameState.InDungeon = false;   // LoopStart puts the party back in after a loop

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
