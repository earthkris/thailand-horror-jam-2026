using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Exploring: walk tile by tile with WASD / arrow keys,
// or click a floor tile and walk the shortest way there around walls and rocks (A* pathfinding).
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float tileSize = 1f;
    public float moveSpeed = 5f;            // tiles per second
    public LayerMask obstacleLayers;        // set to "Obstacles" in the Inspector

    [Header("Click to move")]
    [Tooltip("What counts as floor you can walk on (it needs a collider).")]
    public LayerMask floorLayers = 1;       // 1 = the Default layer
    [Tooltip("Clicks further away than this many tiles are ignored (keeps the search quick).")]
    public int maxPathLength = 40;

    [Header("References")]
    public SpriteRenderer sprite;
    public InputActionReference moveAction; // optional
    public event System.Action<Vector3> OnStepStarted;   // "I'm leaving this tile"
    bool isMoving;
    Vector3 targetPos;

    // Tiles still to walk after a click (world positions, in order)
    readonly List<Vector3> path = new List<Vector3>();

    static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    void Start()
    {
        transform.position = SnapToGrid(transform.position);
        targetPos = transform.position;
    }

    void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();

        // Coming back after combat moved us: start from where we stand now
        targetPos = SnapToGrid(transform.position);
        isMoving = false;
        path.Clear();
    }

    void Update()
    {
        bool canMove = GameState.CanMove && !PauseMenu.BlocksInput;

        // 0. A click picks a new destination. Mid-step, the new path starts where this step ends.
        //    A click on a book (any Interactable) goes to it instead.
        if (canMove && ClickedTile(out Vector3 clicked, out Ray clickRay) && !Interactable.TryHandleClick(clickRay, clicked))
            SetPathTo(clicked);

        // 1. If we're mid-step, keep sliding toward the target tile
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, targetPos, moveSpeed * tileSize * Time.deltaTime);

            if (transform.position == targetPos)
                isMoving = false;   // arrived, fall through to check for the next step
            else
                return;             // still sliding, nothing else to do this frame
        }

        // 2. Don't start a new step during dialogue, menus, or combat
        if (!canMove)
        {
            path.Clear();
            return;
        }

        // 3. Keys win. Otherwise take the next tile of a clicked path.
        Vector2Int dir = GetDirection();
        Vector3 next;
        if (dir != Vector2Int.zero)
        {
            path.Clear();   // the keys take over

            // Face left/right even when walking into a wall
            if (sprite != null && dir.x != 0) sprite.flipX = dir.x < 0;

            next = targetPos + new Vector3(dir.x, 0f, dir.y) * tileSize;
            if (IsBlocked(next)) return;
        }
        else if (path.Count > 0)
        {
            next = path[0];
            path.RemoveAt(0);
            if (IsBlocked(next))
            {
                path.Clear();   // something moved into the way
                return;
            }
            if (sprite != null && Mathf.Abs(next.x - targetPos.x) > 0.01f) sprite.flipX = next.x < targetPos.x;
        }
        else return;

        // 4. Start the step
        Vector3 from = targetPos;      // the tile we're leaving
        targetPos = next;
        isMoving = true;
        OnStepStarted?.Invoke(from);   // tell anyone listening (Lucius follows)
    }

    // ---------- Used by Interactable (books, later chests) ----------

    public bool IsStepping => isMoving;                    // sliding between two tiles right now
    public bool IsWalking => isMoving || path.Count > 0;   // still has somewhere to go
    public Vector3 CurrentTile => targetPos;               // the tile we stand on, or are stepping onto
    public void StopWalking() => path.Clear();             // finish this step, then stand still

    // Walk the shortest way to this tile. False if it can't be reached (or we're already there).
    public bool WalkTo(Vector3 goal)
    {
        path.Clear();
        List<Vector3> route = FindPath(targetPos, SnapToGrid(new Vector3(goal.x, targetPos.y, goal.z)));
        if (route == null) return false;
        path.AddRange(route);
        return true;
    }

    // ---------- Click to move ----------

    // Which floor tile was clicked this frame? (Clicks on buttons don't count.)
    bool ClickedTile(out Vector3 tile, out Ray ray)
    {
        tile = default;
        ray = default;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return false;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;

        // Where the click meets the floor we stand on
        ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane floor = new Plane(Vector3.up, new Vector3(0f, FloorHeight(), 0f));
        if (!floor.Raycast(ray, out float distance)) return false;

        Vector3 p = ray.GetPoint(distance);
        tile = SnapToGrid(new Vector3(p.x, targetPos.y, p.z));
        return true;
    }

    float FloorHeight()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f, floorLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y - tileSize;   // characters stand about a tile above the floor
    }

    void SetPathTo(Vector3 goal)
    {
        path.Clear();
        List<Vector3> route = FindPath(targetPos, goal);
        if (route != null) path.AddRange(route);
    }

    // A* on the tile grid (no diagonals): the tiles to walk, not counting the start.
    // Null if the goal is a wall, off the floor, too far, or can't be reached.
    public List<Vector3> FindPath(Vector3 start, Vector3 goal)
    {
        Vector2Int s = ToTile(start), g = ToTile(goal);
        if (s == g || Distance(s, g) > maxPathLength || !IsWalkable(g)) return null;

        var open = new List<Vector2Int> { s };                          // tiles to look at next
        var cost = new Dictionary<Vector2Int, int> { [s] = 0 };         // steps from the start
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();        // how we got to each tile
        var done = new HashSet<Vector2Int>();
        int budget = 2000;                                              // give up after this many tiles

        while (open.Count > 0 && budget-- > 0)
        {
            // The open tile that looks best: steps so far + a straight-line guess of steps left
            int best = 0;
            for (int i = 1; i < open.Count; i++)
                if (Score(open[i]) < Score(open[best])) best = i;
            Vector2Int t = open[best];
            open.RemoveAt(best);

            if (t == g) return Rebuild(cameFrom, s, g, start.y);
            done.Add(t);

            foreach (Vector2Int d in Directions)
            {
                Vector2Int n = t + d;
                if (done.Contains(n) || Distance(s, n) > maxPathLength) continue;

                int c = cost[t] + 1;
                if (cost.TryGetValue(n, out int old))
                {
                    if (old <= c) continue;   // already found a way here that's as short
                }
                else if (!IsWalkable(n))
                {
                    done.Add(n);              // wall or no floor: never look at it again
                    continue;
                }

                cost[n] = c;
                cameFrom[n] = t;
                if (!open.Contains(n)) open.Add(n);
            }
        }
        return null;

        int Score(Vector2Int t) => cost[t] + Distance(t, g);
    }

    List<Vector3> Rebuild(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int s, Vector2Int g, float y)
    {
        var route = new List<Vector3>();
        for (Vector2Int t = g; t != s; t = cameFrom[t])
            route.Add(new Vector3(t.x * tileSize, y, t.y * tileSize));
        route.Reverse();
        return route;
    }

    bool IsWalkable(Vector2Int t)
    {
        Vector3 center = new Vector3(t.x * tileSize, transform.position.y, t.y * tileSize);
        return !IsBlocked(center) && HasFloor(center);
    }

    bool HasFloor(Vector3 tileCenter)
    {
        return Physics.Raycast(tileCenter, Vector3.down, tileSize * 3f, floorLayers, QueryTriggerInteraction.Ignore);
    }

    Vector2Int ToTile(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / tileSize), Mathf.RoundToInt(p.z / tileSize));

    static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    // ---------- Keys ----------

    // Turns input into exactly one of: up, down, left, right, or none
    Vector2Int GetDirection()
    {
        Vector2 input = ReadInput();

        if (Mathf.Abs(input.x) > 0.5f && Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            return new Vector2Int(input.x > 0 ? 1 : -1, 0);

        if (Mathf.Abs(input.y) > 0.5f)
            return new Vector2Int(0, input.y > 0 ? 1 : -1);

        return Vector2Int.zero;
    }

    bool IsBlocked(Vector3 tileCenter)
    {
        Vector3 halfSize = new Vector3(0.4f, 1f, 0.4f) * tileSize;
        bool blocked = Physics.CheckBox(tileCenter, halfSize, Quaternion.identity, obstacleLayers);
        return blocked;
    }

    Vector3 SnapToGrid(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(pos.x / tileSize) * tileSize,
            pos.y,
            Mathf.Round(pos.z / tileSize) * tileSize);
    }

    Vector2 ReadInput()
    {
        if (moveAction != null)
            return moveAction.action.ReadValue<Vector2>();

        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = 0f, y = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
        return new Vector2(x, y);
    }
}
