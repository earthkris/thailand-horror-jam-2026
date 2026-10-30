using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Turn-based walking during your party's turns: click a lit tile, or use WASD / arrow keys.
// A click walks the shortest way there, around walls, rocks and other units.
// Each step = 1 tile = 5 ft of the unit's speed. You can split it up: walk, attack, walk again.
// The tiles you can still reach glow dimly (MOVE button hides/shows them),
// and hovering one lights up the path you would take.
// Lives on the Combat object, next to TurnManager.
[RequireComponent(typeof(TurnManager))]
public class CombatMovement : MonoBehaviour
{
    [Tooltip("Tiles per second while stepping.")]
    public float stepSpeed = 5f;

    [Header("Path preview (under the mouse)")]
    public Color pathColor = new Color32(232, 190, 99, 255);   // candle gold
    [Range(0f, 1f)] public float pathStrength = 0.4f;

    static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    TurnManager turns;
    CombatManager combat;
    CombatTargeting targeting;
    GridManager Grid => combat.grid;

    bool stepping;
    CombatUnit mover;
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
        if (!canWalk) return;

        // 3. Keys take over from a clicked path (holding the key keeps walking, like exploring)
        Vector2Int dir = ReadDirection();
        if (dir != Vector2Int.zero)
        {
            path.Clear();
            TryStep(turns.Current, dir);
            return;
        }

        // 4. Next tile of a clicked path
        if (path.Count > 0)
        {
            Vector2Int step = path.Dequeue() - GridTileOf(turns.Current);
            bool ok = Mathf.Abs(step.x) + Mathf.Abs(step.y) == 1 && TryStep(turns.Current, step);
            if (!ok) path.Clear();   // something got in the way (e.g. a monster in the dark)
        }
    }

    // ---------- Click to move ----------

    void OnMoveClicked(Vector2Int goal)
    {
        if (!turns.InBattle || !turns.IsPartyTurn || turns.HasMoved) return;
        if (targeting != null && targeting.IsActive) return;
        if (PauseMenu.BlocksInput) return;

        CombatUnit u = turns.Current;
        // Clicked mid-step? Plan from the tile we're stepping onto.
        Vector2Int start = stepping ? StepTargetTile() : GridTileOf(u);
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

        var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        var dist = new Dictionary<Vector2Int, int> { [start] = 0 };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        // Every step costs the same, so a plain breadth-first search finds the shortest way
        while (queue.Count > 0)
        {
            Vector2Int t = queue.Dequeue();
            if (t == goal) break;
            if (dist[t] >= steps) continue;

            foreach (Vector2Int d in Directions)
            {
                Vector2Int n = t + d;
                if (dist.ContainsKey(n)) continue;
                if (!CanStand(u, n, out _, ignoreUnseen: true)) continue;
                dist[n] = dist[t] + 1;
                cameFrom[n] = t;
                queue.Enqueue(n);
            }
        }

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

        List<Vector2Int> route = FindPath(turns.Current, GridTileOf(turns.Current), hover, turns.MovesLeft);
        if (route == null) return;
        Grid.MarkTiles(route, pathColor, pathStrength);
        previewShown = true;
    }

    // ---------- Stepping ----------

    bool TryStep(CombatUnit u, Vector2Int dir)
    {
        Vector2Int to = GridTileOf(u) + dir;

        if (!CanStand(u, to, out CombatUnit blocker))
        {
            // Walking into an enemy you can't see: you feel it, you don't see it
            if (blocker != null && blocker != lastBump && !FogOfWar.CanSee(blocker))
                Debug.Log("Something in the dark blocks the way...");
            lastBump = blocker;
            return false;
        }

        lastBump = null;
        mover = u;
        stepTarget = u.WorldPositionFor(Grid.OriginTile + to);
        stepping = true;
        return true;
    }

    // Could this unit's whole footprint stand with its bottom-left on grid tile 'anchor'?
    // 'blocker' = the unit in the way, if that's what stopped us.
    bool CanStand(CombatUnit u, Vector2Int anchor, out CombatUnit blocker, bool ignoreUnseen = false)
    {
        blocker = null;
        for (int x = 0; x < u.size; x++)
            for (int y = 0; y < u.size; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                if (!Grid.IsWalkable(cell.x, cell.y)) return false;   // wall, rock, outside the room

                Vector2Int world = Grid.OriginTile + cell;
                foreach (CombatUnit other in AllUnits())
                {
                    if (other == u || !other.IsAlive || !other.Occupies(world)) continue;
                    if (ignoreUnseen && !FogOfWar.CanSee(other)) continue;
                    blocker = other;
                    return false;
                }
            }
        return true;
    }

    // ---------- Range highlight ----------

    void RefreshRange()
    {
        if (Grid == null || !Grid.IsBuilt) return;

        bool show = turns.InBattle && turns.IsPartyTurn && !turns.HasMoved && turns.ShowMoveRange
                    && (targeting == null || !targeting.IsActive);
        if (!show)
        {
            Grid.ClearRange();
            return;
        }

        CombatUnit u = turns.Current;
        var lit = new HashSet<Vector2Int>();
        foreach (Vector2Int anchor in Reachable(u, turns.MovesLeft))
            for (int x = 0; x < u.size; x++)
                for (int y = 0; y < u.size; y++)
                    lit.Add(anchor + new Vector2Int(x, y));

        Grid.ShowRange(lit);
    }

    // Every grid tile the unit can reach within 'steps' steps (going around things).
    // Enemies hidden in the dark are ignored here, so the glow doesn't give away where they are.
    List<Vector2Int> Reachable(CombatUnit u, int steps)
    {
        Vector2Int start = GridTileOf(u);
        var dist = new Dictionary<Vector2Int, int> { [start] = 0 };
        var queue = new Queue<Vector2Int>();
        var result = new List<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int t = queue.Dequeue();
            if (t != start) result.Add(t);
            if (dist[t] >= steps) continue;

            foreach (Vector2Int d in Directions)
            {
                Vector2Int n = t + d;
                if (dist.ContainsKey(n)) continue;
                if (!CanStand(u, n, out _, ignoreUnseen: true)) continue;
                dist[n] = dist[t] + 1;
                queue.Enqueue(n);
            }
        }
        return result;
    }

    // ---------- Helpers ----------

    Vector2Int GridTileOf(CombatUnit u) => u.AnchorTile - Grid.OriginTile;

    Vector2Int StepTargetTile()
    {
        Grid.WorldToTile(stepTarget, out int x, out int y);
        return new Vector2Int(x, y);
    }

    IEnumerable<CombatUnit> AllUnits()
    {
        foreach (CombatUnit u in combat.Party) yield return u;
        foreach (CombatUnit u in combat.Enemies) yield return u;
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
