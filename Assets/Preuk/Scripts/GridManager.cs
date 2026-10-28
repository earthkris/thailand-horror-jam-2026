using JetBrains.Annotations;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridManager : MonoBehaviour
{
    [Header("Grid Size")]
    public int width = 10;
    public int height = 8;
    public float tileSize = 1f;

    [Header("Visuals")]
    public GameObject tilePrefab;
    public Color normalColor = new Color32(69, 71, 90, 255);
    public Color hoverColor = new Color32(137, 180, 250, 255);
    public Color selectedColor = new Color32(203, 166, 247, 255);

    Tile[,] tiles;
    Tile hovered;
    Tile selected;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        BuildGrid();
    }

    // Update is called once per frame
    void Update()
    {
        Tile underMouse = GetTileUnderMouse();
        if (underMouse != hovered)
        {
            hovered = underMouse;
            RefreshColors();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && hovered != null)
        {
            selected = hovered;
            Debug.Log($"Selected tile ({selected.x}, {selected.y})");
            RefreshColors();
        }
    }
    void BuildGrid()
    {
        tiles = new Tile[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = TileToWorld(x, y);
                GameObject go = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
                go.name = $"Tile {x},{y}";
                tiles[x, y] = new Tile(x, y, go);
                RefreshColors();
            }
        }
    }
    public Vector3 TileToWorld(int x, int y)
    {
        return new Vector3(x * tileSize, 0f, y * tileSize);
    }
    public bool WorldToTile(Vector3 world, out int x, out int y)
    {
        x = Mathf.RoundToInt(world.x / tileSize);
        y = Mathf.RoundToInt(world.z / tileSize);
        return InBounds(x, y);
    }
    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }
    public Tile GetTile(int x, int y)
    {
        return InBounds(x, y) ? tiles[x, y] : null;
    }
    Tile GetTileUnderMouse()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float distance))
        {
            Vector3 hitpoint = ray.GetPoint(distance);
            if (WorldToTile(hitpoint, out int x, out int y))
                return tiles[x, y];
        }
        return null;
    }
    void RefreshColors()
    {
        foreach (Tile t in tiles)
        {
            if (t == selected) t.SetColor(selectedColor);
            else if (t == hovered) t.SetColor(hoverColor);
            else t.SetColor(normalColor);
        }
    }
}
