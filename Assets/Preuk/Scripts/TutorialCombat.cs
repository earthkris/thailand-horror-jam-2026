using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The first fight (the forest spider) as a tutorial.
//   1. Initiative is fixed: Kairos first, Lucius second, the monster last.
//   2. The camera shows the monster hiding in the dark, then a hint box and a bobbing arrow teach two rounds:
//      the monster -> initiative -> HP and AC -> what a turn holds -> walk -> ACTION -> aim -> the bonus action
//      -> END TURN -> Lucius (attack, then heal as a bonus action) -> the monster's turn -> moving the camera
//      -> finish it off.
//   3. Nobody can die until the lesson is over: the heroes and the monster stop at 1 HP. Only at
//      "finish it off" can the monster fall, so the fight never ends in the middle of the lesson.
//   4. After the win: a short talk, Lucius heals the party, then "let's go open the chest".
// Put it on any GameObject in the scene and drag in the tutorial monster and the two heroes.
// The hint UI builds itself when the game starts.
public class TutorialCombat : MonoBehaviour
{
    [Header("Who")]
    public CombatUnit tutorialEnemy;
    public CombatUnit kairos;
    public CombatUnit lucius;

    [Header("Fixed initiative (the d20 rolls)")]
    [Range(1, 20)] public int kairosRoll = 17;
    [Range(1, 20)] public int luciusRoll = 12;
    [Range(1, 20)] public int enemyRoll = 4;

    [Header("Hints ({enemy} = the monster's name)")]
    [TextArea(2, 4)] public string spotHint = "A {enemy}, under the old lamp post.\nMonsters in the light are easy to see. Out in the dark they show only as shadows, and they're harder to hit.";
    [TextArea(2, 4)] public string initiativeHint = "Every battle, each character rolls a d20 to decide who goes first.\nThis list shows the order.";
    [TextArea(2, 4)] public string statsHint = "Heart = health (HP). Shield = armour (AC).\nAn attack has to roll at least the target's AC to hit.";
    [TextArea(2, 4)] public string turnHint = "On your turn you can MOVE, take one ACTION and one BONUS ACTION, in any order.\nNobody can die in this first fight, so try things out.";
    [TextArea(2, 4)] public string walkHint = "Walk up to the {enemy}.\nClick a lit tile, or use WASD. Each lit tile is one step.";
    [TextArea(2, 4)] public string actionHint = "Press ACTION and pick Sword Strike.";
    [TextArea(2, 4)] public string aimHint = "Click the {enemy}.\nYou roll a d20 and add your bonus. Reach its AC to hit; a natural 20 is a critical hit.";
    [TextArea(2, 4)] public string bonusHint = "The BONUS ACTION is for quick things, like drinking a potion.\nNobody's hurt yet, so press END TURN.";
    [TextArea(2, 4)] public string endTurnHint = "Done for this turn? Press END TURN.";
    [TextArea(2, 4)] public string luciusHint = "Lucius's turn. He fights from a distance:\nHoly Fireball can set the {enemy} burning. Press ACTION.";
    [TextArea(2, 4)] public string luciusBonusHint = "Heal is Lucius's BONUS ACTION, so he can attack and heal in one turn.\nTry it on Kairos, or press END TURN.";
    [TextArea(2, 4)] public string enemyHint = "The {enemy}'s turn. It bites up close and spits web from afar.";
    [TextArea(2, 4)] public string cameraHint = "Tip: press MOVE to switch walking off, then WASD moves the camera to look around.\nC snaps it back.";
    [TextArea(2, 4)] public string finishHint = "The {enemy} is barely standing. Finish it off!";
    public string continueHint = "Click or press E to continue";

    [Header("After the fight")]
    [Tooltip("Phew! That was a close call. / Here, let's heal up.")]
    public DialogueScriptableObject beforeHeal;
    [Tooltip("Lucius healed both of us. / Thanks, Lucius. / Anytime! Come on, let's go open the chest.")]
    public DialogueScriptableObject afterHeal;
    public float pauseBeforeDialogue = 0.8f;
    public float pauseAfterHeal = 0.7f;

    [Header("Look (sprites from Assets/Preuk/Tile/32x32-Tilesheet.png)")]
    [Tooltip("The hint box frame, 9-sliced. UIKit_01 = the gold and blue frame.")]
    public Sprite boxSprite;
    [Tooltip("Arrow sprites, one for each way they point (UIKit_58-61).")]
    public Sprite arrowLeft;
    public Sprite arrowUp;
    public Sprite arrowDown;
    public Sprite arrowRight;
    [Tooltip("Screen size of one pixel of the frame. The frame's pattern repeats (tiles) instead of stretching.")]
    [Min(1f)] public float framePixelScale = 2f;
    [Tooltip("Screen size of one pixel of the arrow.")]
    [Min(1f)] public float arrowPixelScale = 4f;
    [Tooltip("About how wide the hint box is. It snaps to whole tiles of the frame.")]
    public float boxWidth = 880f;
    public Color textColor = Palette.Bone;
    public Color continueColor = new Color32(232, 190, 99, 190);    // candle gold
    [Tooltip("How dark the screen gets while a hint waits for you to continue.")]
    [Range(0f, 1f)] public float dimScreen = 0.2f;
    [Tooltip("Empty = the battle UI's font.")]
    public TMP_FontAsset font;

    // True while a hint waits for "continue": combat clicks and keys are ignored
    public static bool BlocksInput { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => BlocksInput = false;

    TurnManager turns;
    CombatTargeting targeting;
    TurnPanel panel;

    Canvas canvas;
    RectTransform box, arrow;
    TextMeshProUGUI boxText, continueText;
    Image blocker, arrowImage;
    RectTransform pointUI;      // the arrow points at this UI element...
    Transform pointWorld;       // ...or at this thing in the world
    string shownText;

    IEnumerator Start()
    {
        turns = FindAnyObjectByType<TurnManager>();
        panel = FindAnyObjectByType<TurnPanel>(FindObjectsInactive.Include);
        if (turns == null || tutorialEnemy == null || kairos == null || lucius == null)
        {
            Debug.LogWarning("TutorialCombat needs a TurnManager in the scene, and the monster, Kairos and Lucius dragged in.", this);
            yield break;
        }
        targeting = turns.GetComponent<CombatTargeting>();
        if (font == null && panel != null && panel.turnTitle != null) font = panel.turnTitle.font;
        BuildUI();
        Hide();

        // 1. The tutorial fight always goes Kairos, Lucius, monster
        turns.FixInitiativeFor(tutorialEnemy, (kairos, kairosRoll), (lucius, luciusRoll), (tutorialEnemy, enemyRoll));

        while (!(turns.InBattle && InTutorialFight())) yield return null;

        // 2. The lesson (nobody can die during it), then wait for the fight to end
        SetSafe(true);
        yield return FightHints();
        tutorialEnemy.cannotDie = false;   // the lesson is over: the monster can fall
        Hide();
        while (turns.InBattle) yield return null;
        SetSafe(false);                    // the heroes stay safe until this first fight is over

        // 3. Won? (If the party fell, the death screen takes over.)
        if (!tutorialEnemy.IsAlive && (kairos.IsAlive || lucius.IsAlive))
            yield return AfterFight();
    }

    void OnDestroy() => BlocksInput = false;

    // While the lesson runs, the heroes and the monster stop at 1 HP (the heroes for the whole first fight)
    void SetSafe(bool on)
    {
        tutorialEnemy.cannotDie = on;
        kairos.cannotDie = on;
        lucius.cannotDie = on;
    }

    bool InTutorialFight()
    {
        foreach (TurnManager.Combatant c in turns.Order)
            if (c.unit == tutorialEnemy) return true;
        return false;
    }

    // ================= During the fight =================

    IEnumerator FightHints()
    {
        yield return null;   // let the turn panel draw the initiative list first

        // 1. Show the monster: it starts out of sight in the dark
        CameraController cam = CameraController.Main;
        if (cam != null) cam.FocusOn(tutorialEnemy.transform);
        yield return Explain(spotHint, null, tutorialEnemy.transform);
        if (cam != null && turns.Current != null) cam.FocusOn(turns.Current.transform);

        // 2. How a fight works
        yield return Explain(initiativeHint, Ui(panel != null ? panel.initiativePanel : null), null);
        yield return Explain(statsHint, Ui(panel != null ? panel.turnTitle : null), null);
        yield return Explain(turnHint, Ui(panel != null ? panel.actionPanel : null), null);

        // 3. Round one: each of the three turns, hinted as it happens
        bool kairosStarted = false, kairosDone = false;
        bool luciusStarted = false, luciusDone = false;
        bool enemyStarted = false, enemyDone = false;
        bool walked = false;

        while (turns.InBattle && !(kairosDone && luciusDone && enemyDone))
        {
            CombatUnit cur = turns.Current;
            bool mine = turns.IsPartyTurn;
            bool aiming = targeting != null && targeting.IsAiming;
            bool rolling = targeting != null && targeting.IsBusy && !aiming;   // the dice are rolling

            if (kairosStarted && cur != kairos) kairosDone = true;
            if (luciusStarted && cur != lucius) luciusDone = true;
            if (enemyStarted && cur != tutorialEnemy) enemyDone = true;

            if (cur == kairos && mine && !kairosDone)
            {
                kairosStarted = true;
                if (NextTo(kairos, tutorialEnemy) || turns.HasMoved || turns.HasActed || aiming) walked = true;

                if (rolling) Hide();
                else if (!walked) Show(walkHint, null, tutorialEnemy.transform);
                else if (turns.HasActed) Show(bonusHint, Ui(panel != null ? panel.endTurnButton : null), null);
                else if (aiming) Show(aimHint, null, tutorialEnemy.transform);
                else Show(actionHint, Ui(panel != null ? panel.actionButton : null), null);
            }
            else if (cur == lucius && mine && !luciusDone)
            {
                luciusStarted = true;
                if (rolling || aiming) Hide();
                else if (turns.HasActed && turns.HasUsedBonus) Show(endTurnHint, Ui(panel != null ? panel.endTurnButton : null), null);
                else if (turns.HasActed) Show(luciusBonusHint, Ui(panel != null ? panel.itemButton : null), null);
                else Show(luciusHint, Ui(panel != null ? panel.actionButton : null), null);
            }
            else if (cur == tutorialEnemy && !enemyDone)
            {
                enemyStarted = true;
                Show(enemyHint, null, tutorialEnemy.transform);
            }
            else Hide();

            yield return null;
        }

        // 4. Round two: a tip for looking around, then the monster can fall
        while (turns.InBattle && !turns.IsPartyTurn) yield return null;
        if (!turns.InBattle) yield break;
        yield return Explain(cameraHint, Ui(panel != null ? panel.moveButton : null), null);
        tutorialEnemy.cannotDie = false;
        CombatUnit finisher = turns.Current;
        while (turns.InBattle && turns.Current == finisher && !turns.HasActed)
        {
            bool busy = targeting != null && (targeting.IsBusy || targeting.IsAiming);
            if (busy) Hide();
            else Show(finishHint, null, tutorialEnemy.transform);
            yield return null;
        }
    }

    // A hint that waits for the player to click on (the game waits too)
    IEnumerator Explain(string text, RectTransform ui, Transform world)
    {
        Show(text, ui, world, true);
        yield return WaitForContinue();
        Hide();
    }

    IEnumerator WaitForContinue()
    {
        yield return null;   // not the click that got us here
        while (turns.InBattle && !(ContinuePressed() && !PauseMenu.BlocksInput))
            yield return null;
    }

    static bool ContinuePressed()
    {
        Keyboard kb = Keyboard.current;
        Mouse m = Mouse.current;
        return (m != null && m.leftButton.wasPressedThisFrame) ||
               (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
    }

    static bool NextTo(CombatUnit a, CombatUnit b) => b.ReachTo(a.AnchorTile) <= 1;

    static RectTransform Ui(Component c) => c != null ? c.transform as RectTransform : null;
    static RectTransform Ui(GameObject g) => g != null ? g.transform as RectTransform : null;

    // ================= After the fight =================

    IEnumerator AfterFight()
    {
        GameState.Current = GameMode.Dialogue;   // nobody wanders off until the talk is over
        yield return new WaitForSeconds(pauseBeforeDialogue);

        yield return StoryDialogue.Play(beforeHeal);

        // "Lucius healed both of us."
        HealUp(kairos);
        HealUp(lucius);
        Debug.Log("Lucius heals the party to full.");
        yield return new WaitForSeconds(pauseAfterHeal);

        yield return StoryDialogue.Play(afterHeal);
        GameState.Current = GameMode.Exploring;
    }

    static void HealUp(CombatUnit u)
    {
        if (u == null) return;
        if (u.IsAlive)
        {
            u.Heal(Mathf.Max(u.maxHP - u.CurrentHP, 0));
            return;
        }
        // Went down in the fight: back on their feet
        u.Revive(u.maxHP);
        CombatManager.SetExplorationMovement(u, true);
    }

    // ================= Hint box and arrow =================

    void Show(string text, RectTransform ui, Transform world, bool waitForContinue = false)
    {
        text = text.Replace("{enemy}", tutorialEnemy.unitName);
        bool refit = false;
        if (text != shownText)
        {
            boxText.text = text;
            shownText = text;
            refit = true;
        }
        pointUI = ui;
        pointWorld = world;
        if (!box.gameObject.activeSelf) { box.gameObject.SetActive(true); refit = true; }
        if (continueText.gameObject.activeSelf != waitForContinue) { continueText.gameObject.SetActive(waitForContinue); refit = true; }
        if (refit) FitBox();
        if (blocker.gameObject.activeSelf != waitForContinue) blocker.gameObject.SetActive(waitForContinue);
        BlocksInput = waitForContinue;
    }

    // Size the box in whole tiles of the frame, so its pattern is never cut off or stretched
    void FitBox()
    {
        float edgeW = 32f, edgeH = 32f, tileW = 32f, tileH = 32f;
        if (boxSprite != null)
        {
            Vector4 b = boxSprite.border;   // left, bottom, right, top (sprite pixels)
            Rect r = boxSprite.rect;
            edgeW = (b.x + b.z) * framePixelScale;
            edgeH = (b.y + b.w) * framePixelScale;
            tileW = Mathf.Max(r.width - b.x - b.z, 1f) * framePixelScale;
            tileH = Mathf.Max(r.height - b.y - b.w, 1f) * framePixelScale;
        }

        int cols = Mathf.Max(1, Mathf.RoundToInt((boxWidth - edgeW) / tileW));
        float w = edgeW + cols * tileW;
        box.sizeDelta = new Vector2(w, box.sizeDelta.y);

        // How tall the text needs to be at that width, rounded up to whole tiles
        LayoutRebuilder.ForceRebuildLayoutImmediate(box);
        float need = LayoutUtility.GetPreferredHeight(box);
        int rows = Mathf.Max(1, Mathf.CeilToInt((need - edgeH) / tileH - 0.01f));
        box.sizeDelta = new Vector2(w, edgeH + rows * tileH);
    }

    void Hide()
    {
        BlocksInput = false;
        pointUI = null;
        pointWorld = null;
        shownText = null;
        if (box == null) return;
        if (box.gameObject.activeSelf) box.gameObject.SetActive(false);
        if (arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
        if (blocker.gameObject.activeSelf) blocker.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        canvas.enabled = !PauseMenu.IsOpen && !DeathScreen.IsShowing;
        if (!box.gameObject.activeSelf) return;

        bool hasTarget = TryGetTarget(out Rect r, out bool isWorld);
        if (arrow.gameObject.activeSelf != hasTarget) arrow.gameObject.SetActive(hasTarget);
        if (!hasTarget) return;

        // The arrow points at the target from the side facing the middle of the screen, and bobs
        float s = canvas.scaleFactor;
        float gap = (10f + Mathf.Sin(Time.unscaledTime * 6f) * 8f) * s;
        Vector2 c = r.center;

        if (!isWorld && c.x < Screen.width * 0.3f)
        {
            SetArrow(arrowLeft);    // right of it, pointing left
            arrow.position = new Vector2(r.xMax + gap + arrow.sizeDelta.x * 0.5f * s, c.y);
        }
        else if (isWorld || c.y < Screen.height * 0.5f)
        {
            SetArrow(arrowDown);    // above it, pointing down
            arrow.position = new Vector2(c.x, r.yMax + gap + arrow.sizeDelta.y * 0.5f * s);
        }
        else
        {
            SetArrow(arrowUp);      // below it, pointing up
            arrow.position = new Vector2(c.x, r.yMin - gap - arrow.sizeDelta.y * 0.5f * s);
        }

        // A tall monster's head can reach the hint box: keep the arrow below the box, never over its text
        if (isWorld)
        {
            var corners = new Vector3[4];
            box.GetWorldCorners(corners);
            float limit = corners[0].y - (arrow.sizeDelta.y * 0.5f + 6f) * s;
            if (arrow.position.y > limit) arrow.position = new Vector2(arrow.position.x, limit);
        }
    }

    // Where the target is on screen (pixels). A world target is the top of its sprite.
    bool TryGetTarget(out Rect r, out bool isWorld)
    {
        r = default;
        isWorld = false;

        if (pointUI != null && pointUI.gameObject.activeInHierarchy)
        {
            Canvas c = pointUI.GetComponentInParent<Canvas>();
            Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            var corners = new Vector3[4];
            pointUI.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        if (pointWorld != null && Camera.main != null)
        {
            Bounds b = new Bounds(pointWorld.position, Vector3.zero);
            foreach (SpriteRenderer sr in pointWorld.GetComponentsInChildren<SpriteRenderer>())
                if (sr.enabled) b.Encapsulate(sr.bounds);
            Vector3 top = Camera.main.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            if (top.z < 0f) return false;   // behind the camera
            r = new Rect(top.x, top.y, 0f, 0f);
            isWorld = true;
            return true;
        }
        return false;
    }

    // ================= Building the UI =================

    void BuildUI()
    {
        canvas = UIBuilder.NewOverlayCanvas("Tutorial Hints", transform, UIBuilder.HintOrder, clickable: true);   // above the battle UI
        var scaler = canvas.GetComponent<CanvasScaler>();
        Transform root = canvas.transform;

        // Dims the screen and eats clicks while a hint waits for "continue"
        blocker = UIBuilder.NewImage("Blocker", root, raycastTarget: true);
        blocker.color = new Color(0f, 0f, 0f, dimScreen);
        UIBuilder.Stretch(blocker.rectTransform);

        // The hint box: the UIKit frame, 9-sliced, with the text inside
        Image frame = UIBuilder.NewImage("Hint Box", root);
        if (boxSprite != null)
        {
            frame.sprite = boxSprite;
            frame.type = Image.Type.Tiled;   // the edges repeat their pattern instead of stretching
            frame.pixelsPerUnitMultiplier = scaler.referencePixelsPerUnit / (boxSprite.pixelsPerUnit * framePixelScale);
        }
        else Debug.LogWarning("TutorialCombat: no Box Sprite set (use UIKit_01 from 32x32-Tilesheet).", this);
        Vector4 b = boxSprite != null ? boxSprite.border * framePixelScale : new Vector4(16f, 16f, 16f, 16f);   // left, bottom, right, top

        box = frame.rectTransform;
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
        box.pivot = new Vector2(0.5f, 1f);
        box.anchoredPosition = new Vector2(0f, -36f);
        box.sizeDelta = new Vector2(boxWidth, 200f);
        var layout = frame.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(Mathf.RoundToInt(b.x) + 16, Mathf.RoundToInt(b.z) + 16, Mathf.RoundToInt(b.w) + 4, Mathf.RoundToInt(b.y) + 4);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        boxText = NewText("Text", frame.transform, 32f, textColor);
        continueText = NewText("Continue", frame.transform, 20f, continueColor);
        continueText.fontStyle = FontStyles.Italic;
        continueText.text = continueHint;

        // The arrow: one sheet sprite per direction (picked in LateUpdate)
        arrowImage = UIBuilder.NewImage("Arrow", root);
        arrowImage.preserveAspect = true;
        arrow = arrowImage.rectTransform;
        SetArrow(arrowDown);
        if (arrowLeft == null || arrowUp == null || arrowDown == null || arrowRight == null)
            Debug.LogWarning("TutorialCombat: set all 4 arrow sprites (UIKit_58-61 from 32x32-Tilesheet).", this);
    }

    // Centred, wrapping text in the hint font
    TextMeshProUGUI NewText(string name, Transform parent, float size, Color color)
    {
        TextMeshProUGUI t = UIBuilder.NewText(name, parent, size, color, TextAlignmentOptions.Center, wrap: true);
        if (font != null) t.font = font;
        return t;
    }

    // Show the arrow sprite for one direction, at its pixel size
    void SetArrow(Sprite s)
    {
        if (arrowImage.sprite == s && s != null) return;
        arrowImage.sprite = s;
        arrow.sizeDelta = s != null ? s.rect.size * arrowPixelScale : new Vector2(48f, 48f);
    }
}
