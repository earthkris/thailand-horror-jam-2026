using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Turn-based walking during your party's turns: click a lit tile, or use WASD / arrow keys.
// A click walks the shortest way there, around walls, rocks and other units.
// Each step = 1 tile = 5 ft of the unit's speed. You can split it up: walk, attack, walk again.
// Stepping out of a monster's reach gives it an opportunity attack first (OpportunityAttack).
// The tiles you can still reach glow dimly (MOVE button hides/shows them),
// and hovering one lights up the path you would take.
// Lives on the Combat object, next to TurnManager.
[RequireComponent(typeof(TurnManager))]
public class CombatMovement : MonoBehaviour
{
    [Tooltip("Tiles per second while stepping.")]
    public float stepSpeed = 5f;

    [Header("Path preview (under the mouse)")]
    public Color pathColor = Palette.CandleGold;
    [Range(0f, 1f)] public float pathStrength = 0.4f;

    TurnManager turns;
    CombatManager combat;
    CombatTargeting targeting;
    GridManager Grid => combat.grid;

    bool stepping;
    bool reacting;   // waiting for opportunity attacks before a step
    CombatUnit mover;

    public bool IsReacting => reacting;
    Vector3 stepTarget;
    CombatUnit lastBump;   // so bumping into the same thing only logs once

    // A clicked route: grid tiles still to walk, and whose turn it was for
    readonly Queue<Vector2Int> path = new Queue<Vector2Int>();
    CombatUnit pathOwner;

    // What the preview currently shows
    bool previewShown, previewKnown;
    Vector2Int previewTile;
    int previewMoves;

    void Awake()
    {
        turns = GetComponent<TurnManager>();
        combat = GetComponent<CombatManager>();
        targeting = GetComponent<CombatTargeting>();
    }

    // Start runs after every Awake, so CombatManager has found its grid by now
    void Start()
    {
        if (Grid != null) Grid.OnMoveClicked += OnMoveClicked;
    }

    void OnDestroy()
    {
        if (combat != null && Grid != null) Grid.OnMoveClicked -= OnMoveClicked;
    }

    // CombatTargeting calls this when aiming starts/stops
    public void Refresh() => RefreshRange();

    void OnEnable()
    {
        turns.OnChanged += RefreshRange;
        FogOfWar.OnChanged += RefreshRange;   // an enemy stepped into the light
    }

    void OnDisable()
    {
        turns.OnChanged -= RefreshRange;
        FogOfWar.OnChanged -= RefreshRange;
    }

    void Update()
    {
        if (reacting) return;   // the dice are out for an opportunity attack

        // 1. Finish the step we're in
        if (stepping)
        {
            mover.transform.position = Vector3.MoveTowards(
                mover.transform.position, stepTarget, stepSpeed * mover.tileSize * Time.deltaTime);
            if (mover.transform.position != stepTarget) return;

            stepping = false;
            if (turns.Current == mover) turns.SpendMove();   // also redraws the range
        }

        // 2. Only on our own turn, with movement left, while not aiming or rolling
        bool canWalk = turns.InBattle && turns.IsPartyTurn && !turns.HasMoved
                       && Grid != null && Grid.IsBuilt
                       && (targeting == null || !targeting.IsActive)
                       && !PauseMenu.BlocksInput && !TutorialCombat.BlocksInput;
        if (!canWalk || turns.Current != pathOwner) path.Clear();
        UpdatePreview(canWalk);

        // Move mode off (MOVE button), or nobody can walk right now: the keys move the camera instead
        bool keysWalk = canWalk && turns.ShowMoveRange;
        if (turns.InBattle && !PauseMenu.BlocksInput && !TutorialCombat.BlocksInput && !GameState.DialogueOpen)
            PanCamera(keysWalk);
        if (!canWalk) return;

        // 3. Keys take over from a clicked path (holding the key keeps walking, like exploring)
        Vector2Int dir = keysWalk ? ReadDirection() : Vector2Int.zero;
        if (dir != Vector2Int.zero)
        {
            path.Clear();
            TryStep(turns.Current, dir);
            return;
        }

        // 4. Next tile of a clicked path
        if (path.Count > 0)
        {
            Vector2Int step = path.Dequeue() - combat.GridTileOf(turns.Current);
            bool ok = Mathf.Abs(step.x) + Mathf.Abs(step.y) == 1 && TryStep(turns.Current, step);
            if (!ok) path.Clear();   // something got in the way (e.g. a monster in the dark)
        }
    }

    // ---------- Click to move ----------

    void OnMoveClicked(Vector2Int goal)
    {
        if (!turns.InBattle || !turns.IsPartyTurn || turns.HasMoved || reacting) return;
        if (targeting != null && targeting.IsActive) return;
        if (PauseMenu.BlocksInput) return;

        CombatUnit u = turns.Current;
        // Clicked mid-step? Plan from the tile we're stepping onto.
        Vector2Int start = stepping ? StepTargetTile() : combat.GridTileOf(u);
        int moves = turns.MovesLeft - (stepping ? 1 : 0);

        List<Vector2Int> route = FindPath(u, start, goal, moves);
        if (route == null) return;   // too far, or no way through

        path.Clear();
        foreach (Vector2Int t in route) path.Enqueue(t);
        pathOwner = u;
    }

    // Shortest way (no diagonals) from 'start' to 'goal' within 'steps' steps, not counting 'start'.
    // Null if there is none. Monsters hidden in the dark are ignored, like the glow;
    // walking into one stops you.
    List<Vector2Int> FindPath(CombatUnit u, Vector2Int start, Vector2Int goal, int steps)
    {
        if (start == goal || steps <= 0) return null;

        Dictionary<Vector2Int, Vector2Int> cameFrom = Flood(u, start, steps, goal);
        if (!cameFrom.ContainsKey(goal)) return null;

        // Walk back from the goal to the start, then flip it
        var route = new List<Vector2Int>();
        for (Vector2Int t = goal; t != start; t = cameFrom[t]) route.Add(t);
        route.Reverse();
        return route;
    }

    // Light up the way to the tile under the mouse
    void UpdatePreview(bool canWalk)
    {
        Vector2Int hover = default;
        bool want = canWalk && !stepping && path.Count == 0 && Grid.TryGetHovered(out hover);

        if (!want)
        {
            // Aiming paints its own marks: leave those alone
            if (previewShown && (targeting == null || !targeting.IsActive)) Grid.ClearMarks();
            previewShown = false;
            previewKnown = false;
            return;
        }

        if (previewKnown && hover == previewTile && previewMoves == turns.MovesLeft) return;   // nothing changed
        previewKnown = true;
        previewTile = hover;
        previewMoves = turns.MovesLeft;

        if (previewShown) Grid.ClearMarks();
        previewShown = false;

        List<Vector2Int> route = FindPath(turns.Current, combat.GridTileOf(turns.Current), hover, turns.MovesLeft);
        if (route == null) return;
        Grid.MarkTiles(route, pathColor, pathStrength);
        previewShown = true;
    }

    // ---------- Stepping ----------

    bool TryStep(CombatUnit u, Vector2Int dir)
    {
        Vector2Int to = combat.GridTileOf(u) + dir;

        if (!combat.CanStand(u, to, out CombatUnit blocker))
        {
            // Walking into an enemy you can't see: you feel it, you don't see it
            if (blocker != null && blocker != lastBump && !FogOfWar.CanSee(blocker))
                Debug.Log("Something in the dark blocks the way...");
            lastBump = blocker;
            return false;
        }

        lastBump = null;

        // Leaving a monster's reach? It gets a free swing first
        List<CombatUnit> provokers = OpportunityAttack.Provokers(u, Grid.OriginTile + to, combat, turns);
        if (provokers.Count > 0)
        {
            StartCoroutine(ReactThenStep(u, to, provokers));
            return true;
        }

        BeginStep(u, to);
        return true;
    }

    void BeginStep(CombatUnit u, Vector2Int to)
    {
        mover = u;
        stepTarget = u.WorldPositionFor(Grid.OriginTile + to);
        Sfx.Step(u.transform, 0.3f);
        stepping = true;
    }

    IEnumerator ReactThenStep(CombatUnit u, Vector2Int to, List<CombatUnit> provokers)
    {
        reacting = true;
        RefreshRange();
        foreach (CombatUnit p in provokers)
            if (u.IsAlive) yield return OpportunityAttack.Resolve(p, u, turns);
        reacting = false;

        if (!u.IsAlive)
        {
            // Cut down on the way out: Kairos loops time, Lucius's turn is over
            path.Clear();
            turns.EndTurnOf(u);
            yield break;
        }
        BeginStep(u, to);
    }

    // ---------- Range highlight ----------

    void RefreshRange()
    {
        if (Grid == null || !Grid.IsBuilt) return;

        bool show = turns.InBattle && turns.IsPartyTurn && !turns.HasMoved && turns.ShowMoveRange
                    && (targeting == null || !targeting.IsActive) && !reacting;
        if (!show)
        {
            Grid.ClearRange();
            return;
        }

        // Every tile the unit can still get to (not where it stands now), with its whole footprint lit
        CombatUnit u = turns.Current;
        Vector2Int start = combat.GridTileOf(u);
        var lit = new HashSet<Vector2Int>();
        foreach (Vector2Int anchor in Flood(u, start, turns.MovesLeft).Keys)
        {
            if (anchor == start) continue;
            for (int x = 0; x < u.size; x++)
                for (int y = 0; y < u.size; y++)
                    lit.Add(anchor + new Vector2Int(x, y));
        }

        Grid.ShowRange(lit);
    }

    // ---------- Pathfinding ----------

    // Spreads out from 'start' one step at a time (breadth-first, going around things):
    // every grid tile the unit can reach within 'steps' steps, each with the tile it was reached from
    // ('start' maps to itself). Every step costs the same, so following those back gives the shortest way.
    // Stops early once 'goal' is reached, if one is given.
    // Enemies hidden in the dark are ignored here, so the glow doesn't give away where they are.
    Dictionary<Vector2Int, Vector2Int> Flood(CombatUnit u, Vector2Int start, int steps, Vector2Int? goal = null)
    {
        var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        var dist = new Dictionary<Vector2Int, int> { [start] = 0 };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int t = queue.Dequeue();
            if (t == goal) break;
            if (dist[t] >= steps) continue;

            foreach (Vector2Int d in TileMath.Directions)
            {
                Vector2Int n = t + d;
                if (dist.ContainsKey(n)) continue;
                if (!combat.CanStand(u, n, out _, ignoreUnseen: true)) continue;
                dist[n] = dist[t] + 1;
                cameFrom[n] = t;
                queue.Enqueue(n);
            }
        }
        return cameFrom;
    }

    // ---------- Helpers ----------

    Vector2Int StepTargetTile()
    {
        Grid.WorldToTile(stepTarget, out int x, out int y);
        return new Vector2Int(x, y);
    }

    // WASD / arrows slide the view (when they don't walk); middle-mouse drag works any time; C or Home snaps back
    Vector3 lastDrag;
    bool dragging;

    void PanCamera(bool keysWalk)
    {
        CameraController cam = CameraController.Main;
        if (cam == null) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.cKey.wasPressedThisFrame || kb.homeKey.wasPressedThisFrame)) cam.ResetPan();

        if (!keysWalk && kb != null)
        {
            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (input != Vector2.zero) cam.Pan(input.normalized, Time.unscaledDeltaTime);
        }

        Mouse m = Mouse.current;
        if (m == null) return;
        Camera c = cam.GetComponent<Camera>();
        if (m.middleButton.wasPressedThisFrame) { dragging = FloorPoint(c, m, out lastDrag); return; }
        if (!m.middleButton.isPressed) { dragging = false; return; }
        if (dragging && FloorPoint(c, m, out Vector3 now))
        {
            cam.PanBy(lastDrag - now);   // the floor under the mouse stays under the mouse
            FloorPoint(c, m, out lastDrag);
        }
    }

    bool FloorPoint(Camera c, Mouse m, out Vector3 point)
    {
        point = default;
        if (c == null) return false;
        Ray ray = c.ScreenPointToRay(m.position.ReadValue());
        float y = turns.Current != null ? turns.Current.transform.position.y - 1f : 0f;
        if (!new Plane(Vector3.up, new Vector3(0f, y, 0f)).Raycast(ray, out float d)) return false;
        point = ray.GetPoint(d);
        return true;
    }

    static Vector2Int ReadDirection()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2Int.zero;

        // One direction at a time, no diagonals (same rule as exploring)
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) return Vector2Int.up;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) return Vector2Int.down;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) return Vector2Int.left;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) return Vector2Int.right;
        return Vector2Int.zero;
    }
}
