using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// What the last run did, for the end screen. Static, so it survives scene loads.
public static class RunStats
{
    // Allies cut free on the current run (cleared on every loop: they are back in the silk)
    public static readonly List<string> Rescued = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Rescued.Clear();
}

// Shared look for the start and end screens: gothic text and candle-gold buttons, all made in code.
public static class TitleUI
{
    public static Canvas Canvas(string name, Transform parent)
    {
        Canvas c = UIBuilder.NewOverlayCanvas(name, parent, 50, clickable: true);
        Image bg = UIBuilder.NewImage("Background", c.transform, raycastTarget: true);
        UIBuilder.Stretch(bg.rectTransform);
        bg.color = new Color(0.02f, 0.015f, 0.025f, 1f);
        EnsureEventSystem();
        return c;
    }

    // A scene loaded on its own needs something to send clicks to the buttons
    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box, float spacing = 0f)
    {
        TextMeshProUGUI t = UIBuilder.NewText("Text", parent, size, color, TextAlignmentOptions.Center, wrap: true);
        UIBuilder.AddOutline(t);
        t.text = text;
        t.characterSpacing = spacing;
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        return t;
    }

    public static Button Button(Transform parent, string label, Vector2 pos, System.Action onClick)
    {
        Image bg = UIBuilder.NewImage(label, parent, raycastTarget: true);
        bg.color = new Color(0.08f, 0.05f, 0.06f, 0.85f);
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(380f, 64f);
        rt.anchoredPosition = pos;

        var button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() => { Sfx.Play("ui_confirm", 0.7f); onClick(); });

        TextMeshProUGUI t = UIBuilder.NewText("Label", bg.transform, 32, Palette.Bone, TextAlignmentOptions.Center);
        UIBuilder.Stretch(t.rectTransform);
        t.text = label;
        t.characterSpacing = 8;

        UIBuilder.AddHover(bg.gameObject,
            () => { t.color = Palette.CandleGold; bg.color = new Color(0.22f, 0.08f, 0.07f, 0.95f); },
            () => { t.color = Palette.Bone; bg.color = new Color(0.08f, 0.05f, 0.06f, 0.85f); });
        return button;
    }

    // Faint eyes drifting in the dark behind the text (the goddess is watching)
    public static IEnumerator Eyes(Transform parent, Sprite[] frames, int count)
    {
        if (frames == null || frames.Length == 0) yield break;
        var eyes = new List<(Image img, Vector2 home, float phase, float speed)>();
        var rng = new System.Random(3);
        for (int i = 0; i < count; i++)
        {
            Image e = UIBuilder.NewImage("Eye", parent);
            e.sprite = frames[0];
            e.rectTransform.sizeDelta = frames[0].rect.size * (3f + (float)rng.NextDouble() * 2f);
            Vector2 home = new Vector2(((float)rng.NextDouble() - 0.5f) * 1700f, ((float)rng.NextDouble() - 0.5f) * 900f);
            if (Mathf.Abs(home.x) < 700f && Mathf.Abs(home.y) < 320f) home.x = (home.x < 0 ? -1f : 1f) * (700f + (float)rng.NextDouble() * 150f);   // keep the text clear
            eyes.Add((e, home, (float)rng.NextDouble() * 10f, 0.5f + (float)rng.NextDouble()));
        }
        while (parent != null)
        {
            float t = Time.unscaledTime;
            foreach (var (img, home, phase, speed) in eyes)
            {
                if (img == null) yield break;
                img.rectTransform.anchoredPosition = home + new Vector2(Mathf.Sin(t * 0.2f * speed + phase) * 30f, Mathf.Cos(t * 0.15f * speed + phase) * 20f);
                img.sprite = frames[(int)(t * 6f * speed + phase * 3f) % frames.Length];
                float open = Mathf.Clamp01(Mathf.Sin(t * 0.3f * speed + phase) * 1.5f);
                img.color = new Color(1f, 1f, 1f, 0.18f * open);
            }
            yield return null;
        }
    }
}

// The start screen: the first frame of the intro video (the sky before the story starts), with
// NEW GAME, CONTINUE and QUIT along the black bar under it. NEW GAME cuts straight into that video.
// CONTINUE picks up the autosave (SaveGame); it's greyed out when there's none.
// Put it on an empty object in the StartScreen scene; it builds itself.
public class StartScreen : MonoBehaviour
{
    [Tooltip("The game scene NEW GAME loads.")]
    public string gameScene = "Main_Expanded";
    [Tooltip("The first frame of StreamingAssets/Cutscene.mp4 (Sprites/Title/TitleFrame).")]
    public Sprite background;
    [Tooltip("Height of the buttons above the bottom of the screen (the video's black bar is about 210).")]
    public float buttonHeight = 105f;
    public float buttonGap = 420f;

    bool starting;
    bool confirmNew;          // a save exists: NEW GAME asks once before throwing it away
    TextMeshProUGUI newLabel;
    bool hasSave;

    void Start()
    {
        GameState.Current = GameMode.Menu;
        GameState.InDungeon = false;
        Canvas c = TitleUI.Canvas("Title Screen", transform);
        c.transform.Find("Background").GetComponent<Image>().color = Color.black;

        if (background != null)
        {
            Image frame = UIBuilder.NewImage("Video Frame", c.transform);
            frame.sprite = background;
            frame.preserveAspect = true;   // the frame's own black bars stay; any spare screen is black too
            UIBuilder.Stretch(frame.rectTransform);
        }

        hasSave = SaveGame.Exists;
        bool web = Application.platform == RuntimePlatform.WebGLPlayer;   // a web page can't quit
        int count = web ? 2 : 3;
        float y = -540f + buttonHeight;
        float x0 = -(count - 1) * buttonGap * 0.5f;

        Button newGame = TitleUI.Button(c.transform, "NEW GAME", new Vector2(x0, y), NewGame);
        newLabel = newGame.GetComponentInChildren<TextMeshProUGUI>();
        Button cont = TitleUI.Button(c.transform, "CONTINUE", new Vector2(x0 + buttonGap, y), Continue);
        if (!hasSave)
        {
            cont.interactable = false;
            TextMeshProUGUI label = cont.GetComponentInChildren<TextMeshProUGUI>();
            label.alpha = 0.3f;
        }
        if (!web) TitleUI.Button(c.transform, "QUIT", new Vector2(x0 + buttonGap * 2f, y), Application.Quit);

        ScreenFade.FadeIn(1.2f, 0.1f);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || starting) return;
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            if (hasSave) Continue(); else NewGame();
        }
    }

    void NewGame()
    {
        if (starting) return;
        if (hasSave && !confirmNew)
        {
            confirmNew = true;   // second click starts over
            if (newLabel != null) { newLabel.text = "START OVER?"; newLabel.color = Palette.Blood; }
            GameToast.Show("Your saved run will be lost", "Click again to start a new game.");
            return;
        }
        starting = true;
        StartCoroutine(Go(() =>
        {
            SaveGame.Delete();
            LoopManager.SetLoopsUsed(0);
            GameState.InDungeon = false;
            GameState.Current = GameMode.Exploring;
            RunStats.Rescued.Clear();
            SceneManager.LoadScene(gameScene);
        }));
    }

    void Continue()
    {
        if (starting || !hasSave) return;
        starting = true;
        StartCoroutine(Go(() =>
        {
            if (!SaveGame.Continue()) { starting = false; ScreenFade.FadeIn(0.5f, 0f); }
        }));
    }

    IEnumerator Go(System.Action load)
    {
        Sfx.Play("god_bell", 0.6f, 1.2f, 0f);
        ScreenFade.FadeOut(1f);
        yield return new WaitForSecondsRealtime(1.1f);
        load();
        ScreenFade.FadeIn(0.8f, 0.1f);   // the fade lives through the load; the game takes over from black
    }
}

// The end screen, after the escape and the goddess's last words (GodScene.ShowEnding loads it).
public class EndScreen : MonoBehaviour
{
    public string titleScene = "StartScreen";
    public Sprite[] eyeFrames;
    [TextArea(4, 12)]
    public string credits =
        "Made for Thailand Horror Jam 2026\n" +
        "Preuk  ·  Earth  ·  Kunthi-S  ·  Hong\n\n" +
        "<size=70%>Art packs: Dungeon Tale (NullTale), Dungeon Modular Pack, Stylized Hand Painted Dungeon, Free Low Poly Dungeon Pack, Kenney Tiny Dungeon\n" +
        "Sound: Kenney RPG Audio, Impact Sounds, Interface Sounds, Music Jingles (CC0)</size>";

    void Start()
    {
        GameState.Current = GameMode.Menu;
        GameState.InDungeon = true;
        Canvas c = TitleUI.Canvas("End Screen", transform);
        StartCoroutine(TitleUI.Eyes(c.transform, eyeFrames, 7));

        int loops = LoopManager.LoopsUsed;
        TitleUI.Text(c.transform, "YOU ESCAPED", 84, Palette.Bone, new Vector2(0f, 360f), new Vector2(1600f, 120f), 12f);
        TitleUI.Text(c.transform, Epilogue(loops), 32, new Color(0.85f, 0.78f, 0.68f), new Vector2(0f, 170f), new Vector2(1300f, 220f));
        TitleUI.Text(c.transform, $"Loops spent: {loops} of {LoopManager.MaxLoops}      Survivors saved: {RunStats.Rescued.Count} of 2",
                     26, Palette.CandleGold, new Vector2(0f, 20f), new Vector2(1400f, 50f), 4f);
        TitleUI.Text(c.transform, credits, 26, new Color(0.7f, 0.64f, 0.58f), new Vector2(0f, -170f), new Vector2(1500f, 260f));
        TitleUI.Button(c.transform, "RETURN TO TITLE", new Vector2(0f, -400f), BackToTitle);
        ScreenFade.FadeIn(2f, 0.3f);
    }

    static string Epilogue(int loops)
    {
        string friend = loops <= 1
            ? "Lucius walked out into the sun beside you. He still cannot look you in the eyes; the scribbles never left them."
            : loops == 2
                ? "Lucius walks beside you still. He has not said a word since the dungeon, and you have stopped asking him to."
                : "You walked out alone. Some mornings you still set two cups on the table.";
        string others = "";
        if (RunStats.Rescued.Contains("Theron")) others += "\nTheron went back to the edge of the forest, and he keeps the path clear of webs.";
        if (RunStats.Rescued.Contains("Magnus")) others += "\nMagnus went home with his axe and twelve names. He says them every night.";
        if (others == "") others = "\nNobody else came out of the dark.";
        return friend + "\n" + others;
    }

    void BackToTitle()
    {
        SaveGame.Delete();
        LoopManager.GoToTitle();
    }
}
