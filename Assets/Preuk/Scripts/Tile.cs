using UnityEngine;

// One square of the battle grid.
// Its visual is a flat glow on the floor that is hidden until something highlights it.
public class Tile
{
    public int x;
    public int y;
    public bool walkable = true;
    public GameObject visual;

    readonly Renderer rend;

    public Tile(int x, int y, GameObject visual)
    {
        this.x = x;
        this.y = y;
        this.visual = visual;
        rend = visual.GetComponent<Renderer>();
        Hide();
    }

    // Light up this tile. strength 0-1 = how bright the glow is.
    public void Show(Color color, float strength)
    {
        color.a = strength;
        rend.material.color = color;
        rend.enabled = true;
    }

    public void Hide()
    {
        rend.enabled = false;
    }
}