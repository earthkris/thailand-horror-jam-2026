using UnityEngine;

// Grid maths shared by exploring and combat.
// The world is a grid of square tiles on the floor (the XZ plane). A "world tile" is a Vector2Int:
// x = world X, y = world Z, both counted in tiles.
// A unit covers a square footprint of size x size tiles, starting at its anchor (the bottom-left tile).
public static class TileMath
{
    // The four ways to step. No diagonals, anywhere in the game.
    public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // The world tile a position is on
    public static Vector2Int TileOf(Vector3 pos, float tileSize) =>
        new Vector2Int(Mathf.RoundToInt(pos.x / tileSize), Mathf.RoundToInt(pos.z / tileSize));

    // The middle of the tile a position is on (keeps its height)
    public static Vector3 Snap(Vector3 pos, float tileSize) =>
        new Vector3(Mathf.Round(pos.x / tileSize) * tileSize, pos.y, Mathf.Round(pos.z / tileSize) * tileSize);

    // Steps from one tile to another
    public static int Steps(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    // ---------- Footprints ----------

    // Tiles between two footprints along X and along Y (0 = level with each other on that axis)
    public static Vector2Int FootprintGap(Vector2Int anchorA, int sizeA, Vector2Int anchorB, int sizeB)
    {
        int dx = Mathf.Max(0, anchorB.x - (anchorA.x + sizeA - 1), anchorA.x - (anchorB.x + sizeB - 1));
        int dy = Mathf.Max(0, anchorB.y - (anchorA.y + sizeA - 1), anchorA.y - (anchorB.y + sizeB - 1));
        return new Vector2Int(dx, dy);
    }

    // Walking steps between two footprints. 0 = overlapping, 1 = next to each other.
    public static int StepsBetween(Vector2Int anchorA, int sizeA, Vector2Int anchorB, int sizeB)
    {
        Vector2Int gap = FootprintGap(anchorA, sizeA, anchorB, sizeB);
        return gap.x + gap.y;
    }

    // Like StepsBetween, but for fighting: a diagonal neighbour counts as next to you (melee hits diagonally).
    // Anything further adds up as usual.
    public static int ReachBetween(Vector2Int anchorA, int sizeA, Vector2Int anchorB, int sizeB)
    {
        Vector2Int gap = FootprintGap(anchorA, sizeA, anchorB, sizeB);
        return gap.x == 1 && gap.y == 1 ? 1 : gap.x + gap.y;
    }

    // ---------- What's on a tile ----------

    // A wall or rock on this tile? The same tall box for everyone, at character height.
    public static bool IsBlocked(Vector3 tileCenter, float tileSize, LayerMask obstacles,
                                 QueryTriggerInteraction triggers = QueryTriggerInteraction.UseGlobal) =>
        Physics.CheckBox(tileCenter, new Vector3(0.4f, 1f, 0.4f) * tileSize, Quaternion.identity, obstacles, triggers);

    // No wall here, and ground under it (so nobody spawns or walks off the edge of the map)
    public static bool IsOpenGround(Vector3 tileCenter, float tileSize, LayerMask obstacles) =>
        !IsBlocked(tileCenter, tileSize, obstacles) &&
        Physics.Raycast(tileCenter + Vector3.up, Vector3.down, 5f, ~obstacles);

    // Height of the floor under a position (things on 'layers' count as floor)
    public static float FloorY(Vector3 pos, int layers = ~0) =>
        Physics.Raycast(pos + Vector3.up, Vector3.down, out RaycastHit hit, 10f, layers, QueryTriggerInteraction.Ignore)
            ? hit.point.y : pos.y - 1f;
}
