using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Connects combat to the 3D dice roller (RollerPrefabChanged).
// Put it on the RollerPrefabChanged object. During a fight, every d20 (attacks) is rolled
// on the real dice: the tray appears, the dice tumbles, the result is read off the top face,
// and a banner says what the roll was for and whether it hit.
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

    [Header("Banner (above the tray)")]
    public Color captionColor = new Color32(217, 203, 176, 255);   // bone
    public Color hitColor = new Color32(232, 190, 99, 255);        // candle gold
    public Color missColor = new Color32(194, 59, 59, 255);        // blood
    [Tooltip("Banner height on screen (0 = bottom, 1 = top).")]
    [Range(0f, 1f)] public float bannerHeight = 0.6f;
    [Tooltip("Gap between the banner and the right edge of the screen.")]
    public float bannerRightMargin = 20f;

    TextMeshProUGUI caption;
    TextMeshProUGUI outcome;
    GameObject banner;
    int lastResult;
    bool waiting;

    public bool IsBusy => waiting;

    void Awake()
    {
        Instance = this;
        if (dice == null) dice = GetComponentInChildren<DiceRoller>(true);
        if (tray == null && dice != null) tray = dice.transform.parent.gameObject;
        BuildBanner();
        SetVisible(!hideWhenIdle);
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
        yield return Instance.RollRoutine(heading, done);
    }

    // Show what happened under the heading ("HIT — 7 damage"), then put the tray away
    public static IEnumerator ShowOutcome(string text, bool good)
    {
        if (Instance == null) yield break;
        yield return Instance.OutcomeRoutine(text, good);
    }

    IEnumerator RollRoutine(string heading, System.Action<int> done)
    {
        while (waiting) yield return null;   // one roll at a time
        waiting = true;

        SetVisible(true);
        dice.ClickToRoll = false;
        caption.text = heading;
        outcome.text = "";

        bool finished = false;
        void OnRolled(int value) { lastResult = value; finished = true; }
        dice.OnRolled += OnRolled;
        yield return null;          // let the dice wake up if the tray was hidden
        dice.Roll();

        float timeout = 6f;         // safety: never hang the turn
        while (!finished && (timeout -= Time.deltaTime) > 0f) yield return null;
        dice.OnRolled -= OnRolled;
        if (!finished) lastResult = DiceSimulator.Roll(20);

        outcome.text = lastResult.ToString();
        outcome.color = lastResult == 20 ? hitColor : lastResult == 1 ? missColor : captionColor;
        yield return new WaitForSeconds(0.4f);

        waiting = false;
        done(lastResult);
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
        outcome.text = text;
        outcome.color = good ? hitColor : missColor;
        yield return new WaitForSeconds(showResultFor);
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
        banner = new GameObject("Dice Banner", typeof(Canvas), typeof(CanvasScaler));
        banner.transform.SetParent(transform, false);
        var canvas = banner.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = banner.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        caption = MakeText("Caption", 24, captionColor, new Vector2(0, 26));
        caption.characterSpacing = 4;
        outcome = MakeText("Outcome", 44, captionColor, new Vector2(0, -16));
        outcome.fontStyle = FontStyles.Bold;
    }

    TextMeshProUGUI MakeText(string name, float size, Color color, Vector2 offset)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(banner.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, bannerHeight);   // pinned to the right edge
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(560, 60);
        rt.anchoredPosition = new Vector2(-bannerRightMargin, offset.y);

        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;                 // long lines shrink instead of running off screen
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.5f;
        t.raycastTarget = false;
        t.outlineWidth = 0.25f;                     // dark edge so it reads over anything
        t.outlineColor = new Color32(5, 4, 7, 255);
        return t;
    }
}
