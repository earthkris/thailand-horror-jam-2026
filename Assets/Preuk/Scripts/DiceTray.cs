using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Connects combat to the 3D dice roller (RollerPrefabChanged).
// Put it on the RollerPrefabChanged object. During a fight, every d20 (attacks) is rolled
// on the real dice: the tray appears, the dice is thrown in, and a banner says what the roll
// was for and whether it hit.
// Every throw is the same: the number is picked fairly first, then the dice bounces in and tumbles
// to rest flat on that face in 'Throw Time' seconds (the physics roll could stall, crawl or land
// on an edge). Advantage / disadvantage throws two dice at once and dims the one that doesn't count.
// Click, Space or Enter skips to the result; the DICE ANIMATION option turns the throw off.
// The dice's own click-to-roll (DiceRoller, Earth's test scene) is untouched.
// No DiceTray in the scene? Rolls fall back to DiceSimulator (instant random numbers).
public class DiceTray : MonoBehaviour
{
    public static DiceTray Instance { get; private set; }

    [Tooltip("The d20 with the DiceRoller script.")]
    public DiceRoller dice;
    [Tooltip("What gets shown/hidden: the tray with the dice inside.")]
    public GameObject tray;
    [Tooltip("Hide the tray when no roll is happening.")]
    public bool hideWhenIdle = true;
    [Tooltip("Seconds the result stays on screen.")]
    public float showResultFor = 1.2f;
    [Tooltip("How bright the light on the dice is. 1 = the light's own strength. Changes live while playing.")]
    [Range(0f, 1f)] public float diceBrightness = 0.2f;

    [Header("Throw (combat rolls)")]
    [Tooltip("Seconds from the throw to the dice lying still. The same for every roll.")]
    public float throwTime = 0.9f;
    [Tooltip("Seconds the number stays before the game goes on (a click goes on sooner).")]
    public float holdResult = 0.45f;
    [Tooltip("How high the dice bounces, in dice sizes.")]
    public float bounceHeight = 1.2f;
    [Tooltip("Gap between the two dice of an advantage / disadvantage roll, in dice sizes.")]
    public float pairSpacing = 1.7f;
    [Tooltip("The dice that doesn't count is darkened to this.")]
    [Range(0f, 1f)] public float droppedDiceBrightness = 0.35f;

    [Header("Banner (above the tray)")]
    public Color captionColor = Palette.Bone;
    public Color hitColor = Palette.CandleGold;
    public Color missColor = Palette.Blood;
    [Tooltip("Banner height on screen (0 = bottom, 1 = top).")]
    [Range(0f, 1f)] public float bannerHeight = 0.6f;
    [Tooltip("Gap between the banner and the right edge of the screen.")]
    public float bannerRightMargin = 20f;

    TextMeshProUGUI caption;
    TextMeshProUGUI outcome;
    TextMeshProUGUI skipHint;
    GameObject banner;
    int lastResult;
    Camera diceCamera;   // draws the dice on top of the game
    Light[] diceLights;
    float[] lightStrength;   // each light's own intensity, before Dice Brightness
    bool fogWasOn;
    bool waiting;

    public bool IsBusy => waiting;

    void Awake()
    {
        Instance = this;
        if (dice == null) dice = GetComponentInChildren<DiceRoller>(true);
        if (tray == null && dice != null) tray = dice.transform.parent.gameObject;
        diceCamera = GetComponentInChildren<Camera>(true);
        diceLights = GetComponentsInChildren<Light>(true);
        lightStrength = new float[diceLights.Length];
        for (int i = 0; i < diceLights.Length; i++) lightStrength[i] = diceLights[i].intensity;
        BuildBanner();
        SetVisible(!hideWhenIdle);
    }

    // The scene's fog would turn the dice black (its camera is far from the dice),
    // so fog is switched off just while the dice camera draws, and back on right after.
    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
        RenderPipelineManager.endCameraRendering += AfterCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        RenderPipelineManager.endCameraRendering -= AfterCamera;
    }

    void BeforeCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam != diceCamera) return;
        for (int i = 0; i < diceLights.Length; i++)
            if (diceLights[i] != null) diceLights[i].intensity = lightStrength[i] * diceBrightness;
        fogWasOn = RenderSettings.fog;
        RenderSettings.fog = false;
    }

    void AfterCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam != diceCamera) return;
        RenderSettings.fog = fogWasOn;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Rolling ----------

    // Roll the real d20 and hand the number to 'done'. 'heading' is the banner line, e.g. "Sword Strike — need 12".
    public static IEnumerator RollD20(string heading, System.Action<int> done)
    {
        if (Instance == null || Instance.dice == null)
        {
            done(DiceSimulator.Roll(20));   // no dice in the scene
            yield break;
        }
        yield return Instance.ThrowRoutine(heading, 1, true, values => done(values[0]));
    }

    // Advantage (keepHigher) or disadvantage: two d20 thrown together.
    // 'done' gets both numbers and the one that counts.
    public static IEnumerator RollD20Pair(string heading, bool keepHigher, System.Action<int, int, int> done)
    {
        if (Instance == null || Instance.dice == null)
        {
            int a = DiceSimulator.Roll(20), b = DiceSimulator.Roll(20);
            done(a, b, keepHigher ? Mathf.Max(a, b) : Mathf.Min(a, b));
            yield break;
        }
        yield return Instance.ThrowRoutine(heading, 2, keepHigher,
            v => done(v[0], v[1], keepHigher ? Mathf.Max(v[0], v[1]) : Mathf.Min(v[0], v[1])));
    }

    // Show what happened under the heading ("HIT — 7 damage"), then put the tray away
    public static IEnumerator ShowOutcome(string text, bool good)
    {
        if (Instance == null) yield break;
        yield return Instance.OutcomeRoutine(text, good);
    }

    // ---------- The throw ----------

    class Throw
    {
        public Transform die;
        public Vector3 from, rest;
        public Quaternion fromRot, restRot;
        public Vector3 spinAxis;
        public float spin;   // degrees it turns over the throw
    }

    Transform secondDie;        // a look-alike of the dice for advantage / disadvantage
    float dieRadius;            // half the dice's size, in world units
    TMP_Text trayNumber;        // the number printed inside the tray (Earth's "Result" text)
    int inputFrame;             // presses on this frame or earlier don't skip (the click that chose the target)

    IEnumerator ThrowRoutine(string heading, int count, bool keepHigher, System.Action<int[]> done)
    {
        while (waiting) yield return null;   // one roll at a time
        waiting = true;

        SetVisible(true);
        dice.ClickToRoll = false;
        caption.text = heading;
        outcome.text = "";
        yield return null;                   // the tray was hidden: let it lay out first
        inputFrame = Time.frameCount;

        // The numbers are decided first, fairly; the dice then lands on them
        int[] values = new int[count];
        for (int i = 0; i < count; i++) values[i] = DiceSimulator.Roll(20);

        PrepareDice(count);
        var throws = new Throw[count];
        Vector3 centre = TrayCentre();
        for (int i = 0; i < count; i++)
        {
            float side = count == 1 ? 0f : (i == 0 ? -1f : 1f);
            Vector3 rest = centre + Vector3.right * side * pairSpacing * dieRadius;
            throws[i] = PlanThrow(i == 0 ? dice.transform : secondDie, rest, values[i]);
        }

        if (trayNumber != null) trayNumber.text = "";
        Sfx.Play("dice_rattle", 0.7f);

        if (GameSettings.DiceAnimation)
        {
            if (skipHint != null) skipHint.gameObject.SetActive(true);
            for (float t = 0f; t < throwTime; t += Time.deltaTime)
            {
                if (SkipPressed()) break;
                foreach (Throw th in throws) Pose(th, t / throwTime);
                yield return null;
            }
            if (skipHint != null) skipHint.gameObject.SetActive(false);
        }
        foreach (Throw th in throws) Pose(th, 1f);

        // Show the result: one number, or both with the one that counts bright and the other dimmed
        int kept = count == 1 ? values[0] : keepHigher ? Mathf.Max(values[0], values[1]) : Mathf.Min(values[0], values[1]);
        lastResult = kept;
        Sfx.Play("dice_land", 0.8f, kept == 20 ? 1.2f : kept == 1 ? 0.8f : 1f);
        outcome.color = kept == 20 ? hitColor : kept == 1 ? missColor : captionColor;
        if (count == 1) outcome.text = kept.ToString();
        else
        {
            int dropped = values[0] == kept ? 1 : 0;
            Dim(throws[dropped].die, true);
            outcome.text = dropped == 1
                ? $"{values[0]}   <alpha=#55>{values[1]}<alpha=#FF>"
                : $"<alpha=#55>{values[0]}<alpha=#FF>   {values[1]}";
        }
        if (trayNumber != null) trayNumber.text = kept.ToString();

        yield return WaitOrSkip(GameSettings.DiceAnimation ? holdResult : holdResult * 0.6f);

        if (count == 2) { Dim(dice.transform, false); Dim(secondDie, false); }
        waiting = false;
        done(values);
    }

    // The real dice stops being physical for combat rolls; the look-alike is made the first time it's needed
    void PrepareDice(int count)
    {
        if (dice.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        if (dieRadius <= 0f)
        {
            Collider col = dice.GetComponent<Collider>();
            Vector3 e = col != null ? col.bounds.extents : Vector3.one * 0.5f;
            dieRadius = (e.x + e.y + e.z) / 3f;
            foreach (TMP_Text t in tray.GetComponentsInChildren<TMP_Text>(true))
                if (t.name == "Result") trayNumber = t;
        }
        if (count == 2 && secondDie == null)
        {
            var go = new GameObject(dice.name + " (second)");
            go.layer = dice.gameObject.layer;
            go.transform.SetParent(dice.transform.parent, false);
            go.transform.localScale = dice.transform.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = dice.GetComponent<MeshFilter>().sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            var source = dice.GetComponent<MeshRenderer>();
            mr.sharedMaterials = source.sharedMaterials;
            mr.shadowCastingMode = source.shadowCastingMode;
            mr.receiveShadows = source.receiveShadows;
            secondDie = go.transform;
        }
        if (secondDie != null) secondDie.gameObject.SetActive(count == 2);
    }

    // The middle of the tray floor, at the height a dice lies on it
    Vector3 TrayCentre()
    {
        Collider floor = null;
        foreach (Collider c in tray.GetComponentsInChildren<Collider>(true))
            if (c.name == "Back") floor = c;
        if (floor == null) return dice.transform.position;
        Bounds b = floor.bounds;
        return new Vector3(b.center.x, b.max.y + dieRadius * 0.92f, b.center.z);
    }

    // Where it comes from, how it spins, and how it must lie for 'value' to face up
    Throw PlanThrow(Transform die, Vector3 rest, int value)
    {
        Vector3 local = Vector3.up;
        foreach (DiceSide s in dice.GetComponent<DiceSides>().Sides)
            if (s.Value == value) { local = s.Normal; break; }
        Quaternion any = Random.rotation;
        Quaternion restRot = Quaternion.FromToRotation(any * local, Vector3.up) * any;
        restRot = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * restRot;   // any turn about the up face

        Vector2 dir = Random.insideUnitCircle.normalized;
        return new Throw
        {
            die = die,
            rest = rest,
            restRot = restRot,
            from = rest + new Vector3(dir.x, 0f, dir.y) * dieRadius * 1.8f + Vector3.up * dieRadius * bounceHeight * 2f,
            fromRot = Random.rotation,
            spinAxis = Random.onUnitSphere,
            spin = Random.Range(540f, 900f),
        };
    }

    // k: 0 = leaving the hand, 1 = lying still on its number
    void Pose(Throw th, float k)
    {
        k = Mathf.Clamp01(k);
        float travel = 1f - (1f - k) * (1f - k);                                          // slows as it lands
        float bounce = Mathf.Abs(Mathf.Sin(travel * Mathf.PI * 3f)) * (1f - k) * (1f - k); // three bounces, each lower
        Vector3 p = Vector3.Lerp(th.from, th.rest, travel);
        p.y = Mathf.Lerp(th.from.y, th.rest.y, Mathf.Min(k * 3f, 1f)) + bounce * dieRadius * bounceHeight;
        th.die.position = p;

        Quaternion tumbling = Quaternion.AngleAxis(th.spin * travel, th.spinAxis) * th.fromRot;
        float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, k));         // rolls onto its face at the end
        th.die.rotation = Quaternion.Slerp(tumbling, th.restRot, settle);
    }

    // Darken (or restore) the dice that doesn't count
    void Dim(Transform die, bool dim)
    {
        if (die == null || !die.TryGetComponent(out MeshRenderer mr)) return;
        if (!dim) { mr.SetPropertyBlock(null); return; }
        var block = new MaterialPropertyBlock();
        Material m = mr.sharedMaterial;
        Color baseColor = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
        Color c = baseColor * droppedDiceBrightness;
        c.a = baseColor.a;
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        mr.SetPropertyBlock(block);
    }

    // ---------- Skipping ----------

    // Left click, Space or Enter (not the press that started the roll, and not in the pause menu)
    bool SkipPressed()
    {
        if (Time.frameCount <= inputFrame || PauseMenu.BlocksInput) return false;
        var m = UnityEngine.InputSystem.Mouse.current;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return (m != null && m.leftButton.wasPressedThisFrame) ||
               (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame));
    }

    IEnumerator WaitOrSkip(float seconds)
    {
        int from = Time.frameCount;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (Time.frameCount > from && SkipPressed()) yield break;
            yield return null;
        }
    }

    // Just the banner, no dice (heals, potions)
    public static IEnumerator ShowMessage(string heading, string text, bool good)
    {
        if (Instance == null) yield break;
        Instance.caption.text = heading;
        yield return Instance.OutcomeRoutine(text, good);
    }

    IEnumerator OutcomeRoutine(string text, bool good)
    {
        if (banner != null) banner.SetActive(true);
        if (text.StartsWith("MISS") || text.StartsWith("NATURAL 1")) Sfx.Play("miss", 0.8f);
        else if (text.StartsWith("CRITICAL")) Sfx.Play("crit", 0.8f);
        outcome.text = text;
        outcome.color = good ? hitColor : missColor;
        inputFrame = Time.frameCount;
        yield return WaitOrSkip(showResultFor);   // a click goes on sooner
        if (!waiting) SetVisible(!hideWhenIdle);
    }

    void SetVisible(bool on)
    {
        if (tray != null) tray.SetActive(on);
        if (banner != null) banner.SetActive(on);
    }

    // ---------- Banner (made in code, sits on top of everything) ----------

    void BuildBanner()
    {
        banner = UIBuilder.NewOverlayCanvas("Dice Banner", transform, UIBuilder.DiceOrder).gameObject;

        caption = MakeText("Caption", 24, captionColor, new Vector2(0, 26));
        caption.characterSpacing = 4;
        outcome = MakeText("Outcome", 44, captionColor, new Vector2(0, -16));
        outcome.fontStyle = FontStyles.Bold;
        skipHint = MakeText("Skip Hint", 18, new Color(captionColor.r, captionColor.g, captionColor.b, 0.55f), new Vector2(0, -58));
        skipHint.text = "click or space to skip";
        skipHint.gameObject.SetActive(false);
    }

    TextMeshProUGUI MakeText(string name, float size, Color color, Vector2 offset)
    {
        TextMeshProUGUI t = UIBuilder.NewText(name, banner.transform, size, color, TextAlignmentOptions.Center);
        UIBuilder.AddOutline(t);
        t.enableAutoSizing = true;                 // long lines shrink instead of running off screen
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.5f;

        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, bannerHeight);   // pinned to the right edge
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(560, 60);
        rt.anchoredPosition = new Vector2(-bannerRightMargin, offset.y);
        return t;
    }
}
