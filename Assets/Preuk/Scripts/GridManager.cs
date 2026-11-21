using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// The battle grid. When a fight starts it fills the whole room around the fight:
// every connected floor tile, stopping at walls (Obstacles layer) and at the edge of the floor.
// If the fight is inside a Room box, the grid also stays inside that box.
// Tiles are invisible glows on the floor: they only show when highlighted.
public class GridManager : MonoBehaviour
{
    [Header("Room Detection")]
    public float tileSize = 1f;
    [Tooltip("Things on these layers count as floor (the floor needs a collider).")]
    public LayerMask floorLayers = 1;   // 1 = the Default layer
    [Tooltip("Walls, rocks and anything else that blocks a tile.")]
    public LayerMask obstacleLayers;
    [Tooltip("Safety limit: the grid never gets wider or deeper than this many tiles.")]
    public int maxRoomSize = 50;

    [Header("Glow")]
    [Tooltip("How bright a lit tile is.")]
    [Range(0f, 1f)] public float glowStrength = 0.6f;
    [Tooltip("1 = glows touch each other, smaller = gaps between tiles.")]
    [Range(0.5f, 1f)] public float tileInset = 0.92f;
    [Tooltip("Height above the floor, so the glow doesn't flicker into it.")]
    public float floorOffset = 0.06f;

    [Header("Colors")]
    public Color hoverColor = Palette.Bone;
    public Color selectedColor = Palette.CandleGold;
    [Tooltip("Tiles the current unit can still walk to.")]
    public Color rangeColor = Palette.CandleGold;   // but dimmer (Range Strength)
    [Range(0f, 1f)] public float rangeStrength = 0.18f;

    Tile[,] tiles;          // null where there's no floor in the room
    Tile hovered;
    Tile selected;
    readonly HashSet<Tile> range = new HashSet<Tile>();
    readonly Dictionary<Tile, (Color color, float strength)> marked = new Dictionary<Tile, (Color, float)>();

    // Left-click on a floor tile (grid coordinates). CombatTargeting listens to this.
    public event System.Action<Vector2Int> OnTileClicked;
    // Left-click on a floor tile while nobody is aiming. CombatMovement walks there.
    public event System.Action<Vector2Int> OnMoveClicked;
    float floorY;

    public bool IsBuilt => tiles != null;
    public int Width { get; private set; }
    public int Height { get; private set; }

    // The world tile that grid tile (0,0) sits on (bottom-left corner of the room)
    public Vector2Int OriginTile { get; private set; }

    // Runs when the component is first added: pre-fill the layers
    void Reset()
    {
        floorLayers = LayerMask.GetMask("Default");
        obstacleLayers = LayerMask.GetMask("Obstacles");
    }

    void Awake()
    {
        if (Tile.GlowMaterial == null)
            Debug.LogError("TileGlow material not found in a Resources folder!");
    }

    void Update()
    {
        if (!IsBuilt || Mouse.current == null) return;

        // Mouse over a UI button? Then it's not over the grid.
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Tile underMouse = overUI ? null : GetTileUnderMouse();

        if (underMouse != hovered)
        {
            hovered = underMouse;
            RefreshColors();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && hovered != null && !GameState.DialogueOpen)
        {
            if (OnTileClicked != null)
            {
                OnTileClicked.Invoke(new Vector2Int(hovered.x, hovered.y));   // someone is choosing a target
            }
            else if (hovered.walkable && OnMoveClicked != null)
            {
                OnMoveClicked.Invoke(new Vector2Int(hovered.x, hovered.y));   // walk there
            }
            else if (hovered.walkable)
            {
                selected = hovered;
                RefreshColors();
            }
        }
    }

    // ---------- Building ----------

    // Fills the room around 'center' with tiles. 'room' is optional (null = no box limit).
    public void BuildAround(Vector3 center, Room room)
    {
        if (IsBuilt) ClearGrid();

        floorY = FindFloorHeight(center);
        Vector2Int start = TileMath.TileOf(center, tileSize);

        // 1. Flood fill: spread out from the fight, one tile at a time,
        //    through tiles that have floor. Walls get added (as blocked) but we don't spread past them.
        var found = new Dictionary<Vector2Int, bool>();   // tile -> walkable
        var queue = new Queue<Vector2Int>();
        found[start] = !HasObstacle(start);
        queue.Enqueue(start);

        int half = maxRoomSize / 2;

        while (queue.Count > 0)
        {
            Vector2Int t = queue.Dequeue();
            if (!found[t]) continue;   // blocked tile: don't spread from it

            foreach (Vector2Int d in TileMath.Directions)
            {
                Vector2Int n = t + d;
                if (found.ContainsKey(n)) continue;
                if (Mathf.Abs(n.x - start.x) > half || Mathf.Abs(n.y - start.y) > half) continue;
                if (room != null && !room.Contains(WorldOf(n))) continue;
                if (!HasFloor(n)) continue;

                found[n] = !HasObstacle(n);
                queue.Enqueue(n);
            }
        }

        // 2. Fit the grid around the tiles we found
        Vector2Int min = start, max = start;
        foreach (Vector2Int t in found.Keys)
        {
            min = Vector2Int.Min(min, t);
            max = Vector2Int.Max(max, t);
        }
        OriginTile = min;
        Width = max.x - min.x + 1;
        Height = max.y - min.y + 1;

        // 3. Make a tile (with its hidden glow) for every room square
        tiles = new Tile[Width, Height];
        foreach (var pair in found)
        {
            int x = pair.Key.x - min.x;
            int y = pair.Key.y - min.y;
            Tile tile = new Tile(x, y, CreateGlow(x, y));
            tile.walkable = pair.Value;
            tiles[x, y] = tile;
        }

        RefreshColors();
        Debug.Log($"Battle grid: {Width}x{Height}, {found.Count} tiles{(room != null ? $" (room '{room.name}')" : "")}");
    }

    // The tile's hidden glow, just above the floor
    GameObject CreateGlow(int x, int y)
    {
        GameObject go = Tile.CreateGlow($"Tile {x},{y}", tileSize * tileInset);
        go.transform.position = TileToWorld(x, y) + Vector3.up * floorOffset;
        go.transform.SetParent(transform, true);
        return go;
    }

    public void ClearGrid()
    {
        if (!IsBuilt) return;
        foreach (Tile t in tiles)
            if (t != null) t.Destroy();
        tiles = null;
        hovered = null;
        selected = null;
        range.Clear();
        marked.Clear();
    }

    // ---------- Target highlight (attack / heal range) ----------

    // Light these grid tiles in a color (drawn above the movement range)
    public void MarkTiles(IEnumerable<Vector2Int> gridTiles, Color color, float strength)
    {
        if (!IsBuilt) return;
        foreach (Vector2Int t in gridTiles)
        {
            Tile tile = GetTile(t.x, t.y);
            if (tile != null) marked[tile] = (color, strength);
        }
        RefreshColors();
    }

    public void ClearMarks()
    {
        if (!IsBuilt || marked.Count == 0) return;
        marked.Clear();
        RefreshColors();
    }

    // ---------- Movement range highlight ----------

    // Dimly light these grid tiles (where the current unit can walk)
    public void ShowRange(IEnumerable<Vector2Int> gridTiles)
    {
        if (!IsBuilt) return;
        range.Clear();
        foreach (Vector2Int t in gridTiles)
        {
            Tile tile = GetTile(t.x, t.y);
            if (tile != null) range.Add(tile);
        }
        RefreshColors();
    }

    public void ClearRange()
    {
        if (!IsBuilt || range.Count == 0) return;
        range.Clear();
        RefreshColors();
    }

    // ---------- What's on a tile (world tiles) ----------

    // Height of the floor under a position (characters stand above it)
    float FindFloorHeight(Vector3 pos)
    {
        if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 20f,
                            floorLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y;
    }

    bool HasFloor(Vector2Int worldTile)
    {
        Vector3 above = WorldOf(worldTile) + Vector3.up * 0.5f;
        return Physics.Raycast(above, Vector3.down, 1f, floorLayers, QueryTriggerInteraction.Ignore);
    }

    bool HasObstacle(Vector2Int worldTile)
    {
        // Same tall box the player uses, at character height
        Vector3 center = WorldOf(worldTile) + Vector3.up * tileSize;
        return TileMath.IsBlocked(center, tileSize, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    Vector3 WorldOf(Vector2Int worldTile)
    {
        return new Vector3(worldTile.x * tileSize, floorY, worldTile.y * tileSize);
    }

    // ---------- Grid coordinates (0,0 = bottom-left of the room) ----------

    public Vector3 TileToWorld(int x, int y)
    {
        return WorldOf(OriginTile + new Vector2Int(x, y));
    }

    public bool WorldToTile(Vector3 world, out int x, out int y)
    {
        Vector2Int t = TileMath.TileOf(world, tileSize) - OriginTile;
        x = t.x;
        y = t.y;
        return InBounds(x, y);
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    // null if (x,y) is outside the room
    public Tile GetTile(int x, int y)
    {
        return IsBuilt && InBounds(x, y) ? tiles[x, y] : null;
    }

    // The grid tile under the mouse, if there is one (CombatMovement previews the path to it)
    public bool TryGetHovered(out Vector2Int tile)
    {
        tile = hovered != null ? new Vector2Int(hovered.x, hovered.y) : default;
        return hovered != null;
    }

    public bool IsWalkable(int x, int y)
    {
        Tile t = GetTile(x, y);
        return t != null && t.walkable;
    }

    Tile GetTileUnderMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        if (ground.Raycast(ray, out float distance) &&
            WorldToTile(ray.GetPoint(distance), out int x, out int y))
            return tiles[x, y];
        return null;
    }

    // Only highlighted tiles glow; everything else stays plain floor
    void RefreshColors()
    {
        foreach (Tile t in tiles)
        {
            if (t == null) continue;
            bool isMarked = marked.TryGetValue(t, out var mark);
            if (t == hovered && isMarked) t.Show(mark.color, Mathf.Min(mark.strength * 2f, 1f));   // aiming at it
            else if (t == selected) t.Show(selectedColor, glowStrength);
            else if (t == hovered && t.walkable) t.Show(hoverColor, glowStrength);
            else if (isMarked) t.Show(mark.color, mark.strength);
            else if (range.Contains(t)) t.Show(rangeColor, rangeStrength);
            else t.Hide();
        }
    }

    // While a battle is on: faint outline of every room tile in the Scene view (red = blocked)
    void OnDrawGizmos()
    {
        if (!IsBuilt) return;
        foreach (Tile t in tiles)
        {
            if (t == null) continue;
            Gizmos.color = t.walkable ? new Color(0.85f, 0.8f, 0.69f, 0.25f) : new Color(0.76f, 0.23f, 0.23f, 0.6f);
            Gizmos.DrawWireCube(TileToWorld(t.x, t.y), new Vector3(tileSize * 0.9f, 0.01f, tileSize * 0.9f));
        }
    }
}
