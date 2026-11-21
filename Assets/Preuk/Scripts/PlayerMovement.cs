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

    [Header("Click target glow")]
    [Tooltip("Light up the tile you clicked until you get there.")]
    public bool showTargetGlow = true;
    public Color targetColor = Palette.CandleGold;   // same as combat
    [Range(0f, 1f)] public float targetStrength = 0.6f;
    [Tooltip("How much the glow breathes (0 = steady).")]
    [Range(0f, 0.5f)] public float targetPulse = 0.2f;
    [Tooltip("1 = fills the tile, smaller = a gap around it.")]
    [Range(0.5f, 1f)] public float targetInset = 0.92f;
    [Tooltip("Height above the floor, so the glow doesn't flicker into it.")]
    public float targetFloorOffset = 0.06f;

    [Header("References")]
    public SpriteRenderer sprite;
    public InputActionReference moveAction; // optional
    public event System.Action<Vector3> OnStepStarted;   // "I'm leaving this tile"
    bool isMoving;
    bool scripted;          // walking a path a cutscene set: keys and clicks are ignored
    Vector3 targetPos;

    // Tiles still to walk after a click (world positions, in order)
    readonly List<Vector3> path = new List<Vector3>();

    // The glow on the clicked tile
    GameObject targetGlow;
    Renderer targetRend;
    Material targetTint;   // the glow's own copy of the material (so the pulsing never edits the shared one)
    bool hasTarget;

    void Start()
    {
        transform.position = TileMath.Snap(transform.position, tileSize);
        targetPos = transform.position;
    }

    void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();

        // Coming back after combat moved us: start from where we stand now
        targetPos = TileMath.Snap(transform.position, tileSize);
        isMoving = false;
        scripted = false;
        path.Clear();
        ClearTarget();
    }

    void OnDisable() => ClearTarget();

    void OnDestroy()
    {
        if (targetTint != null) Destroy(targetTint);
        if (targetGlow != null) Destroy(targetGlow);
    }

    void Update()
    {
        bool canMove = (GameState.CanMove || scripted) && !PauseMenu.BlocksInput;

        // 0. A click picks a new destination. Mid-step, the new path starts where this step ends.
        //    A click on a book (any Interactable) goes to it instead.
        if (canMove && !scripted && ClickedTile(out Vector3 clicked, out Ray clickRay) && !Interactable.TryHandleClick(clickRay, clicked))
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
            ClearTarget();
            return;
        }

        // 3. Keys win. Otherwise take the next tile of a clicked path.
        Vector2Int dir = scripted ? Vector2Int.zero : GetDirection();
        Vector3 next;
        if (dir != Vector2Int.zero)
        {
            path.Clear();   // the keys take over
            ClearTarget();

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
                scripted = false;
                ClearTarget();
                return;
            }
            if (sprite != null && Mathf.Abs(next.x - targetPos.x) > 0.01f) sprite.flipX = next.x < targetPos.x;
        }
        else
        {
            ClearTarget();   // arrived (or nowhere to go)
            scripted = false;
            return;
        }

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
    public void StopWalking() { path.Clear(); ClearTarget(); scripted = false; }   // finish this step, then stand still

    // Walk there even during a cutscene (keys and clicks are ignored on the way). PathBlocker uses it to turn you back.
    public bool WalkToScripted(Vector3 goal)
    {
        scripted = WalkTo(goal);
        return scripted;
    }

    // Walk the shortest way to this tile. False if it can't be reached (or we're already there).
    public bool WalkTo(Vector3 goal)
    {
        path.Clear();
        List<Vector3> route = FindPath(targetPos, TileMath.Snap(new Vector3(goal.x, targetPos.y, goal.z), tileSize));
        if (route == null) { ClearTarget(); return false; }
        path.AddRange(route);
        SetTarget(route[route.Count - 1]);
        return true;
    }

    // Jump straight to a spot (a teleport trap). Snaps to the grid and forgets any walk in progress.
    public void Teleport(Vector3 spot)
    {
        path.Clear();
        ClearTarget();
        isMoving = false;
        scripted = false;
        transform.position = TileMath.Snap(new Vector3(spot.x, transform.position.y, spot.z), tileSize);
        targetPos = transform.position;
    }

    // Teleport the whole party: the followers land next to us and the camera cuts over.
    // Cutscenes do this while the screen is black.
    public void TeleportParty(Vector3 spot)
    {
        Teleport(spot);
        foreach (Follower f in FindObjectsByType<Follower>(FindObjectsSortMode.None)) f.SnapNextToLeader();
        CameraController cam = CameraController.Main;
        if (cam != null) cam.SnapToTarget();
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
        tile = TileMath.Snap(new Vector3(p.x, targetPos.y, p.z), tileSize);
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
        if (route == null) { ClearTarget(); return; }   // can't get there
        path.AddRange(route);
        SetTarget(route[route.Count - 1]);
    }

    // ---------- Click target glow ----------

    // Light up the tile we're walking to (same glow as the battle grid)
    void SetTarget(Vector3 tile)
    {
        if (!showTargetGlow) return;
        if (targetGlow == null && !CreateGlow()) return;

        // Sit just above the floor under that tile
        float y = FloorHeight();
        Vector3 above = new Vector3(tile.x, transform.position.y, tile.z);
        if (Physics.Raycast(above, Vector3.down, out RaycastHit hit, tileSize * 3f, floorLayers, QueryTriggerInteraction.Ignore))
            y = hit.point.y;

        targetGlow.transform.position = new Vector3(tile.x, y + targetFloorOffset, tile.z);
        targetRend.enabled = true;
        hasTarget = true;
        UpdateGlow();
    }

    void ClearTarget()
    {
        hasTarget = false;
        if (targetRend != null) targetRend.enabled = false;
    }

    void LateUpdate()
    {
        if (hasTarget) UpdateGlow();
    }

    // Gentle breathing so it reads as "you're going here"
    void UpdateGlow()
    {
        Color c = targetColor;
        c.a = targetStrength * (1f - targetPulse + targetPulse * Mathf.Sin(Time.time * 4f));
        targetTint.color = c;
    }

    bool CreateGlow()
    {
        if (Tile.GlowMaterial == null)
        {
            Debug.LogWarning("TileGlow material not found in a Resources folder: no click glow.");
            showTargetGlow = false;
            return false;
        }

        targetGlow = Tile.CreateGlow("Click Target Glow", tileSize * targetInset);   // the battle grid's glow
        targetGlow.layer = 2;   // Ignore Raycast
        targetRend = targetGlow.GetComponent<MeshRenderer>();
        targetTint = targetRend.material;
        targetRend.enabled = false;
        return true;
    }

    // A* on the tile grid (no diagonals): the tiles to walk, not counting the start.
    // Null if the goal is a wall, off the floor, too far, or can't be reached.
    public List<Vector3> FindPath(Vector3 start, Vector3 goal)
    {
        Vector2Int s = TileMath.TileOf(start, tileSize), g = TileMath.TileOf(goal, tileSize);
        if (s == g || TileMath.Steps(s, g) > maxPathLength || !IsWalkable(g)) return null;

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

            foreach (Vector2Int d in TileMath.Directions)
            {
                Vector2Int n = t + d;
                if (done.Contains(n) || TileMath.Steps(s, n) > maxPathLength) continue;

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

        int Score(Vector2Int t) => cost[t] + TileMath.Steps(t, g);
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

    bool IsBlocked(Vector3 tileCenter) => TileMath.IsBlocked(tileCenter, tileSize, obstacleLayers);

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
