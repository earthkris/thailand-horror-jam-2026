using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The goddess (storyboard scenes 8, 10, 15, 16).
// When the whole party falls she appears in the middle of a bright screen, ringed by watching eyes,
// and speaks through Earth's dialogue box. Then time loops back (LoopManager).
//   First death:   the screen goes dark, Kairos' last words, she fades in blurry, then sharp,
//                  and gives him the loop power.
//   Later deaths:  she reminds him how many chances are left.
//   No loops left: her last line, then GAME OVER (DeathScreen).
// Everything on screen is built in code. Drag in the sprites and the dialogues.
// E or a click moves the dialogue on.
public class GodScene : MonoBehaviour
{
    public static GodScene Instance { get; private set; }

    // DeathScreen waits for us while we're in the scene
    public static bool TakesOverDeath => Instance != null && Instance.isActiveAndEnabled;

    [Header("Sprites")]
    [Tooltip("god-move-sheet frames, in order.")]
    public Sprite[] godFrames;
    [Tooltip("eye-move-sheet frames, in order.")]
    public Sprite[] eyeFrames;
    public float godFramesPerSecond = 8f;
    public float eyeFramesPerSecond = 7f;
    [Range(6, 8)] public int eyeCount = 7;

    [Header("Layout (1920 x 1080 screen)")]
    [Tooltip("Screen size of one pixel of the goddess.")]
    public float godPixelScale = 5f;
    [Tooltip("Screen size of one pixel of an eye.")]
    public float eyePixelScale = 5f;
    [Tooltip("Where she floats, from the middle of the screen. A bit up, because the dialogue box covers the bottom.")]
    public Vector2 godOffset = new Vector2(0f, 180f);
    [Tooltip("The eyes sit on this oval around her (half width, half height).")]
    public Vector2 eyeRing = new Vector2(420f, 280f);
    [Tooltip("The eyes spread over this arc, in degrees (0 = right of her, 90 = above). Leaves the bottom free for the dialogue box.")]
    public Vector2 eyeArc = new Vector2(-30f, 210f);

    [Header("Colours")]
    public Color brightColor = new Color32(250, 244, 228, 255);   // warm white light
    public Color glowColor = new Color32(232, 190, 99, 255);      // candle gold

    [Header("Dialogue (storyboard scene numbers)")]
    [Tooltip("8.1  In the dark: 'Am I dead? Is this it...?'")]
    public DialogueScriptableObject firstDeathDark;
    [Tooltip("8.2  She's still blurry: 'Am I dreaming?'")]
    public DialogueScriptableObject firstDeathBlurry;
    [Tooltip("8.2  Sharp: she gives the loop power.")]
    public DialogueScriptableObject firstDeathGift;
    [Tooltip("10.1 'Here we go again, only 1 chance left.'")]
    public DialogueScriptableObject secondDeath;
    [Tooltip("After the third death (no chances left after this loop).")]
    public DialogueScriptableObject thirdDeath;
    [Tooltip("16  No loops left: 'They didn't make it either...'")]
    public DialogueScriptableObject gameOver;
    [Tooltip("15  After the escape (call ShowEnding): 'I told you, everything has a price to pay.'")]
    public DialogueScriptableObject ending;

    [Header("Timing (seconds)")]
    [Tooltip("Wait after the last hit.")]
    public float delay = 1f;
    public float darkenTime = 1.5f;
    public float brightenTime = 2.2f;
    public float sharpenTime = 1.3f;

    [Tooltip("Draw order. Under the pause menu (100); the dialogue box is lifted just above this while she talks.")]
    public int sortingOrder = 90;

    Canvas canvas;
    RectTransform root, godRoot;
    Image bg, rays, glow, god, mist, flash;
    readonly Image[] blurCopies = new Image[4];
    Image[] eyes;
    float[] eyeAngle, eyePhase, eyeSpeed;
    float reveal;   // 0 = not there, 1 = fully there
    float blur;     // 1 = blurry, 0 = sharp
    bool running;
    Canvas dialogueCanvas;
    int dialogueOrder;
    DeathScreen deathScreen;

    static readonly Vector2[] BlurDirections =
        { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) };

    void Awake()
    {
        Instance = this;
        deathScreen = FindAnyObjectByType<DeathScreen>(FindObjectsInactive.Include);
    }

    void OnEnable() => TurnManager.OnPartyFallen += OnPartyFallen;
    void OnDisable() => TurnManager.OnPartyFallen -= OnPartyFallen;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        RestoreDialogueBox();
    }

    void OnPartyFallen()
    {
        if (!running) StartCoroutine(Death());
    }

    // Right-click the component header in Play mode to see it without dying
    [ContextMenu("Test Death (Play mode)")]
    void TestDeath()
    {
        if (Application.isPlaying) OnPartyFallen();
    }

    // The menu's LOOP: looping means dying. The party falls on the spot, then the usual goddess scene.
    public void DieAndLoop()
    {
        if (running || !LoopManager.CanLoop) return;
        StartCoroutine(FallThenDeath());
    }

    IEnumerator FallThenDeath()
    {
        running = true;   // a fight noticing the party fell won't start a second scene
        GameState.Current = GameMode.Cutscene;
        var party = new System.Collections.Generic.List<CombatUnit>();
        foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (u.team == Team.Party && u.IsAlive) party.Add(u);
        foreach (CombatUnit u in party) u.TakeDamage(u.CurrentHP);   // hit flash and shake
        yield return new WaitForSecondsRealtime(0.4f);
        foreach (CombatUnit u in party)
            foreach (SpriteRenderer sr in u.GetComponentsInChildren<SpriteRenderer>())
                sr.color = new Color(0.4f, 0.4f, 0.4f, sr.color.a);   // grey = down
        yield return Death();
    }

    // Storyboard 15: after walking out of the dungeon
    public void ShowEnding()
    {
        if (!running) StartCoroutine(Ending());
    }

    // ================= The scenes =================

    IEnumerator Death()
    {
        running = true;
        GameState.Current = GameMode.Cutscene;   // nobody moves
        int used = LoopManager.LoopsUsed;
        bool final = !LoopManager.CanLoop;
        bool first = used == 0 && !final;

        yield return new WaitForSecondsRealtime(delay);
        Open();

        // 1. The world goes dark
        yield return Tween(darkenTime, k => bg.color = new Color(0f, 0f, 0f, k));
        if (first)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Talk(firstDeathDark);
        }

        // 2. Light. The first time she comes in blurry.
        blur = first ? 1f : 0f;
        yield return Tween(first ? brightenTime : brightenTime * 0.6f, k =>
        {
            bg.color = Color.Lerp(Color.black, brightColor, k);
            reveal = k;
        });

        // 3. She speaks
        if (first)
        {
            yield return Talk(firstDeathBlurry);
            yield return Tween(sharpenTime, k => blur = 1f - k);
            yield return Talk(firstDeathGift);
        }
        else if (final) yield return Talk(gameOver);
        else if (used == 1) yield return Talk(secondDeath);
        else yield return Talk(thirdDeath);

        // 4a. No loops left: black, then GAME OVER
        if (final)
        {
            ScreenFade.FadeOut(1.5f);
            yield return new WaitForSecondsRealtime(1.7f);
            Close();
            running = false;
            if (deathScreen != null)
            {
                deathScreen.ShowGameOver();
                yield return new WaitForSecondsRealtime(deathScreen.fadeTime);
            }
            ScreenFade.FadeIn(1f, 0f);
            yield break;
        }

        // 4b. Back in time: white, then black, then the loop (LoopStart fades in in the dungeon)
        yield return Tween(0.7f, k => flash.color = new Color(1f, 1f, 1f, k));
        ScreenFade.FadeOut(0.6f);
        yield return new WaitForSecondsRealtime(0.8f);
        running = false;
        RestoreDialogueBox();
        LoopManager.Loop();
    }

    IEnumerator Ending()
    {
        running = true;
        GameState.Current = GameMode.Cutscene;
        ScreenFade.FadeOut(0.8f);
        yield return new WaitForSecondsRealtime(1f);

        Open();
        bg.color = brightColor;
        reveal = 1f;
        blur = 0f;
        ScreenFade.FadeIn(1.2f, 0f);
        yield return new WaitForSecondsRealtime(1.2f);
        yield return Talk(ending);

        ScreenFade.FadeOut(2f);   // the end: stays black
        yield return new WaitForSecondsRealtime(2.2f);
        Close();
        running = false;
    }

    IEnumerator Talk(DialogueScriptableObject lines)
    {
        if (lines == null || DialogueBox.Instance == null) yield break;
        DialogueBox.Instance.StartDialogue(lines);
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.IsContentOpen) yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
    }

    static IEnumerator Tween(float seconds, System.Action<float> step)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(seconds, 0.01f))
        {
            step(t * t * (3f - 2f * t));   // smooth start and end
            yield return null;
        }
        step(1f);
    }

    // ================= Showing and hiding =================

    void Open()
    {
        if (canvas == null) Build();
        canvas.gameObject.SetActive(true);
        reveal = 0f;
        blur = 0f;
        bg.color = new Color(0f, 0f, 0f, 0f);
        flash.color = new Color(1f, 1f, 1f, 0f);
        ApplyVisuals(Time.unscaledTime);

        // Lift Earth's dialogue box above us while she talks
        if (DialogueBox.Instance != null && dialogueCanvas == null)
        {
            dialogueCanvas = DialogueBox.Instance.GetComponentInParent<Canvas>();
            if (dialogueCanvas != null)
            {
                dialogueCanvas = dialogueCanvas.rootCanvas;
                dialogueOrder = dialogueCanvas.sortingOrder;
                dialogueCanvas.sortingOrder = sortingOrder + 5;
            }
        }
    }

    void Close()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
        RestoreDialogueBox();
    }

    void RestoreDialogueBox()
    {
        if (dialogueCanvas != null) dialogueCanvas.sortingOrder = dialogueOrder;
        dialogueCanvas = null;
    }

    void Update()
    {
        if (canvas == null || !canvas.gameObject.activeSelf) return;
        ApplyVisuals(Time.unscaledTime);

        // A click moves the dialogue on too (E already does, through Earth's Interactor)
        if (running && GameState.DialogueOpen && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            DialogueBox.Instance.NextDialogue();
    }

    // Every frame: her animation, the blur, the eyes looking around
    void ApplyVisuals(float t)
    {
        if (root == null) return;

        Sprite frame = godFrames != null && godFrames.Length > 0
            ? godFrames[(int)(t * godFramesPerSecond) % godFrames.Length] : null;
        Vector2 bob = new Vector2(0f, Mathf.Sin(t * 1.4f) * 8f);   // she floats

        godRoot.anchoredPosition = godOffset + bob;
        god.sprite = frame;
        god.color = new Color(1f, 1f, 1f, reveal * Mathf.Lerp(1f, 0.45f, blur));

        // Blurry = four faint copies spread apart; sharp = they meet in the middle and vanish
        float spread = blur * 24f;
        for (int i = 0; i < blurCopies.Length; i++)
        {
            blurCopies[i].sprite = frame;
            blurCopies[i].rectTransform.anchoredPosition = BlurDirections[i] * spread;
            blurCopies[i].color = new Color(1f, 1f, 1f, reveal * blur * 0.3f);
        }

        Color light = bg.color;
        mist.color = new Color(light.r, light.g, light.b, reveal);   // hides where her picture ends

        glow.rectTransform.anchoredPosition = godOffset + bob * 0.5f;
        glow.color = WithAlpha(glowColor, reveal * (0.55f + 0.08f * Mathf.Sin(t * 2f)));
        rays.rectTransform.anchoredPosition = godOffset;
        rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 4f);
        rays.color = WithAlpha(glowColor, reveal * 0.3f);

        if (eyes == null) return;
        int n = eyes.Length;
        for (int i = 0; i < n; i++)
        {
            Image eye = eyes[i];
            float open = Mathf.Clamp01(reveal * 2f - (float)i / n);   // they open one after another
            float a = (eyeAngle[i] + Mathf.Sin(t * 0.35f + i * 1.7f) * 6f) * Mathf.Deg2Rad;
            Vector2 drift = new Vector2(0f, Mathf.Sin(t * 1.8f + i * 2.1f) * 6f);
            eye.rectTransform.anchoredPosition = godOffset + new Vector2(Mathf.Cos(a) * eyeRing.x, Mathf.Sin(a) * eyeRing.y) + drift;
            eye.rectTransform.localScale = new Vector3(1f, open, 1f);   // opens like an eyelid
            if (eyeFrames != null && eyeFrames.Length > 0)
                eye.sprite = eyeFrames[(int)(t * eyeSpeed[i] + eyePhase[i]) % eyeFrames.Length];
            eye.color = new Color(1f, 1f, 1f, open * Mathf.Lerp(1f, 0.5f, blur));
        }
    }

    // ================= Building the screen =================

    void Build()
    {
        var go = new GameObject("God Scene", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        BuildInto((RectTransform)go.transform);
        go.SetActive(false);
    }

    // Used by Build, and for a preview in the editor
    public void BuildInto(RectTransform parent)
    {
        root = MakeRect("God Scene", parent);
        Stretch(root);

        bg = MakeImage("Light", root, null);
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;   // no clicking the world behind her

        rays = MakeImage("Rays", root, MakeRaysSprite());
        rays.rectTransform.sizeDelta = new Vector2(2400f, 2400f);
        glow = MakeImage("Glow", root, MakeGlowSprite());
        glow.rectTransform.sizeDelta = new Vector2(1300f, 1300f);

        // The eyes, spread over an arc around her
        RectTransform eyesRoot = MakeRect("Eyes", root);
        Stretch(eyesRoot);
        int n = Mathf.Clamp(eyeCount, 1, 12);
        eyes = new Image[n];
        eyeAngle = new float[n];
        eyePhase = new float[n];
        eyeSpeed = new float[n];
        Vector2 eyeSize = eyeFrames != null && eyeFrames.Length > 0 ? eyeFrames[0].rect.size * eyePixelScale : new Vector2(160f, 150f);
        for (int i = 0; i < n; i++)
        {
            eyes[i] = MakeImage("Eye " + (i + 1), eyesRoot, null);
            eyes[i].rectTransform.sizeDelta = eyeSize;
            eyeAngle[i] = n == 1 ? 90f : Mathf.Lerp(eyeArc.x, eyeArc.y, (float)i / (n - 1));
            eyePhase[i] = Mathf.Repeat(i * 5.37f, 14f);                            // each starts on a different frame
            eyeSpeed[i] = eyeFramesPerSecond * (0.75f + 0.5f * Mathf.Repeat(i * 0.618f, 1f));   // and looks around at its own pace
        }

        // The goddess, her blur copies, and a mist over the bottom edge of her picture
        godRoot = MakeRect("Goddess", root);
        Vector2 godSize = godFrames != null && godFrames.Length > 0 ? godFrames[0].rect.size * godPixelScale : new Vector2(1000f, 825f);
        for (int i = 0; i < blurCopies.Length; i++)
        {
            blurCopies[i] = MakeImage("Blur " + (i + 1), godRoot, null);
            blurCopies[i].rectTransform.sizeDelta = godSize;
        }
        god = MakeImage("Goddess", godRoot, null);
        god.rectTransform.sizeDelta = godSize;
        mist = MakeImage("Mist", godRoot, MakeMistSprite());
        mist.rectTransform.sizeDelta = new Vector2(128f, 34f) * godPixelScale;
        mist.rectTransform.anchoredPosition = new Vector2(0f, -43f * godPixelScale);

        flash = MakeImage("Flash", root, null);
        Stretch(flash.rectTransform);
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    // Editor preview: build into any canvas and show her fully there
    public void PreviewInto(RectTransform parent, float revealAmount, float blurAmount, float time)
    {
        BuildInto(parent);
        reveal = revealAmount;
        blur = blurAmount;
        bg.color = Color.Lerp(Color.black, brightColor, revealAmount);
        flash.color = new Color(1f, 1f, 1f, 0f);
        ApplyVisuals(time);
    }

    static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image MakeImage(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    // A soft round glow: bright in the middle, gone at the edge
    static Sprite MakeGlowSprite()
    {
        const int size = 128;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = (1f - r) * (1f - r);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        return MakeSprite(px, size, size, FilterMode.Bilinear);
    }

    // Soft light rays fanning out from the middle
    static Sprite MakeRaysSprite()
    {
        const int size = 256;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(angle * 14f), 6f);
                float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(ray * fade * 255f));
            }
        return MakeSprite(px, size, size, FilterMode.Bilinear);
    }

    // See-through at the top, solid at the bottom, soft at the sides
    static Sprite MakeMistSprite()
    {
        const int w = 64, h = 32;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float down = 1f - (y + 0.5f) / h;                                  // 0 at the top, 1 at the bottom
                float side = Mathf.Abs((x + 0.5f) / w * 2f - 1f);                  // 0 in the middle, 1 at the sides
                float a = Mathf.SmoothStep(0f, 1f, down * 2f) * Mathf.SmoothStep(1f, 0f, (side - 0.7f) / 0.3f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        return MakeSprite(px, w, h, FilterMode.Bilinear);
    }

    static Sprite MakeSprite(Color32[] px, int w, int h, FilterMode filter)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
