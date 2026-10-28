using UnityEngine;

public class Tile
{
    public int x;
    public int y;
    public bool walkable = true;
    public GameObject visual;

    public Tile(int x, int y, GameObject visual)
    {
        this.x = x;
        this.y = y;
        this.visual = visual;
    }
    public void SetColor(Color c)
    {
        visual.GetComponent<Renderer>().material.color = c;
    }
}