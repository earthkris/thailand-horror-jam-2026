using UnityEngine;
using UnityEngine.InputSystem;

// The battle grid. Built when combat starts, cleared when it ends.
// Tile (0,0) sits at this object's position (rounded to whole tiles); the grid extends +X and +Z.
public class GridManager : MonoBehaviour
{
    [Header("Grid Size")]
    public int width = 10;
    public int height = 8;
    public float tileSize = 1f;

    [Header("Obstacles")]
    [Tooltip("Tiles with something on these layers can't be walked on (rocks, walls).")]
    public LayerMask obstacleLayers;

    [Header("Visuals")]
    public GameObject tilePrefab;
    public Color normalColor = new Color32(69, 71, 90, 255);      // Catppuccin Surface1
    public Color blockedColor = new Color32(24, 24, 37, 255);     // Catppuccin Mantle
    public Color hoverColor = new Color32(137, 180, 250, 255);    // Catppuccin Blue
    public Color selectedColor = new Color32(203, 166, 247, 255); // Catppuccin Mauve

    Tile[,] tiles;
    Tile hovered;
    Tile selected;

    public bool IsBuilt => tiles != null;

    // The world tile that grid tile (0,0) sits on
    public Vector2Int OriginTile => new Vector2Int(
        Mathf.RoundToInt(transform.position.x / tileSize),
        Mathf.RoundToInt(transform.position.z / tileSize));

    // Runs when the component is first added: pre-fill the layer
    void Reset()
    {
        obstacleLayers = LayerMask.GetMask("Obstacles");
    }

    void Update()
    {
        if (!IsBuilt || Mouse.current == null) return;

        Tile underMouse = GetTileUnderMouse();
        if (underMouse != hovered)
        {
            hovered = underMouse;
            RefreshColors();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && hovered != null && hovered.walkable)
        {
            selected = hovered;
            Debug.Log($"Selected tile ({selected.x}, {selected.y})");
            RefreshColors();
        }
    }

    public void BuildGrid()
    {
        if (IsBuilt) ClearGrid();

        tiles = new Tile[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = TileToWorld(x, y);
                GameObject go = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
                go.name = $"Tile {x},{y}";

                Tile tile = new Tile(x, y, go);
                tile.walkable = !HasObstacle(pos);
                tiles[x, y] = tile;
            }
        }
        RefreshColors();   // once, after every tile exists
    }

    public void ClearGrid()
    {
        if (!IsBuilt) return;
        foreach (Tile t in tiles)
            if (t != null && t.visual != null) Destroy(t.visual);
        tiles = null;
        hovered = null;
        selected = null;
    }

    bool HasObstacle(Vector3 tileCenter)
    {
        // Same tall box the player uses, at character height
        Vector3 center = tileCenter + Vector3.up * tileSize;
        Vector3 halfSize = new Vector3(0.4f, 1f, 0.4f) * tileSize;
        return Physics.CheckBox(center, halfSize, Quaternion.identity, obstacleLayers);
    }

    public Vector3 TileToWorld(int x, int y)
    {
        Vector2Int o = OriginTile;
        return new Vector3((o.x + x) * tileSize, transform.position.y, (o.y + y) * tileSize);
    }

    public bool WorldToTile(Vector3 world, out int x, out int y)
    {
        Vector2Int o = OriginTile;
        x = Mathf.RoundToInt(world.x / tileSize) - o.x;
        y = Mathf.RoundToInt(world.z / tileSize) - o.y;
        return InBounds(x, y);
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    public Tile GetTile(int x, int y)
    {
        return IsBuilt && InBounds(x, y) ? tiles[x, y] : null;
    }

    public bool IsWalkable(int x, int y)
    {
        Tile t = GetTile(x, y);
        return t != null && t.walkable;
    }

    Tile GetTileUnderMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (ground.Raycast(ray, out float distance) &&
            WorldToTile(ray.GetPoint(distance), out int x, out int y))
            return tiles[x, y];
        return null;
    }

    void RefreshColors()
    {
        foreach (Tile t in tiles)
        {
            if (!t.walkable) t.SetColor(blockedColor);
            else if (t == selected) t.SetColor(selectedColor);
            else if (t == hovered) t.SetColor(hoverColor);
            else t.SetColor(normalColor);
        }
    }

    // Blue outline of the battle area in the Scene view, even before it's built
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.54f, 0.71f, 0.98f, 0.8f);
        Vector3 size = new Vector3(width * tileSize, 0.01f, height * tileSize);
        Vector3 corner = TileToWorld(0, 0) - new Vector3(tileSize, 0f, tileSize) * 0.5f;
        Gizmos.DrawWireCube(corner + size * 0.5f, size);
    }
}
