using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Player settings that aren't saves: the browser / PC remembers them between visits (PlayerPrefs).
//   UI Size        - makes every on-screen menu, panel and banner bigger or smaller.
//   Dice Animation - off = combat rolls show their result straight away.
// The pause menu's OPTIONS page changes them (PauseMenu). Nothing to set up in the scene.
public static class GameSettings
{
    const string UiScaleKey = "ui_scale";
    const string DiceAnimationKey = "dice_animation";

    public const float MinUiScale = 0.75f;
    public const float MaxUiScale = 1.5f;

    // 1 = the size the UI was designed at. 1.25 = a quarter bigger.
    public static float UiScale
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(UiScaleKey, 1f), MinUiScale, MaxUiScale);
        set
        {
            PlayerPrefs.SetFloat(UiScaleKey, Mathf.Clamp(value, MinUiScale, MaxUiScale));
            ApplyUiScale();
        }
    }

    public static bool DiceAnimation
    {
        get => PlayerPrefs.GetInt(DiceAnimationKey, 1) == 1;
        set => PlayerPrefs.SetInt(DiceAnimationKey, value ? 1 : 0);
    }

    // ---------- UI size ----------
    // Every canvas drawn on top of the screen scales with the screen from a reference size (1920x1080).
    // A smaller reference size makes everything on it bigger, so UI Size divides it.

    static readonly Dictionary<CanvasScaler, Vector2> designSize = new Dictionary<CanvasScaler, Vector2>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        designSize.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Forget canvases from the scene that was unloaded
        var gone = new List<CanvasScaler>();
        foreach (CanvasScaler s in designSize.Keys) if (s == null) gone.Add(s);
        foreach (CanvasScaler s in gone) designSize.Remove(s);
        ApplyUiScale();
    }

    // Canvases made in code (UIBuilder) call this when they're built
    public static void Register(CanvasScaler scaler)
    {
        if (scaler == null) return;
        Apply(scaler, UiScale);
    }

    public static void ApplyUiScale()
    {
        float scale = UiScale;
        foreach (CanvasScaler s in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Apply(s, scale);
    }

    static void Apply(CanvasScaler s, float scale)
    {
        if (s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;
        // Only what's drawn over the screen: the dice tray is a 3D scene seen through its own camera
        Canvas c = s.GetComponent<Canvas>();
        if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) return;
        if (!designSize.TryGetValue(s, out Vector2 size))
        {
            size = s.referenceResolution;
            designSize[s] = size;
        }
        s.referenceResolution = size / scale;
    }
}
