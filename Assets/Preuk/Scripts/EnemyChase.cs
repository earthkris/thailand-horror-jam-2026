using System.Collections.Generic;
using UnityEngine;

// Exploration AI: when a party member comes close, walk toward them tile by tile,
// going AROUND obstacles (breadth-first search). Touching them starts combat
// (EnemyEncounter handles that). Works for any size.
[RequireComponent(typeof(CombatUnit))]
public class EnemyChase : MonoBehaviour
{
    [Tooltip("Starts chasing when a party member is this many steps away or closer (straight-line steps).")]
    public int detectRange = 5;
    [Tooltip("Gives up if the way around obstacles is longer than this many steps.")]
    public int maxPathLength = 15;
    [Tooltip("Tiles per second. Keep it below the player's speed (5) so you can run away.")]
    public float moveSpeed = 3f;
    [Tooltip("Short wait between steps, in seconds.")]
    public float pauseBetweenSteps = 0.15f;
    [Tooltip("Things the enemy can't walk through (rocks, walls).")]
    public LayerMask obstacleLayers;

    static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    CombatUnit unit;
    readonly List<CombatUnit> party = new List<CombatUnit>();
    readonly List<CombatUnit> otherEnemies = new List<CombatUnit>();
    bool isMoving;
    Vector3 targetPos;
    float waitTimer;

    // Runs when the component is first added: pre-fill the layer
    void Reset()
    {
        obstacleLayers = LayerMask.GetMask("Obstacles");
    }

    void Awake()
    {
        unit = GetComponent<CombatUnit>();
    }

    void Start()
    {
        targetPos = transform.position;   // CombatUnit already snapped us to the grid in Awake
        foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (u.team == Team.Party) party.Add(u);
            else if (u != unit) otherEnemies.Add(u);
        }
    }

    void Update()
    {
        // 1. Finish the step we're in
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, targetPos, moveSpeed * unit.tileSize * Time.deltaTime);

            if (transform.position == targetPos)
            {
                isMoving = false;
                waitTimer = pauseBetweenSteps;
            }
            else return;
        }

        // 2. Only start new steps while exploring
        if (!GameState.CanMove) return;   // not during dialogue or cutscenes

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        // 3. Who is closest?
        CombatUnit target = ClosestPartyMember(out int steps);
        if (target == null || steps > detectRange || steps <= 1) return;   // too far, or already touching

        // 4. Find the route and take its first step
        Vector2Int? next = NextStepToward(TileOf(target.transform.position));
        if (next == null) return;   // no way through right now

        targetPos = unit.WorldPositionFor(next.Value);
        isMoving = true;
    }

    // Breadth-first search: spread out from where we stand one step at a time (like a flood),
    // going around anything blocked, until we reach a spot that touches the target.
    // Then trace back to find the FIRST step of that route.
    Vector2Int? NextStepToward(Vector2Int targetTile)
    {
        Vector2Int start = unit.AnchorTile;
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();   // tile -> the tile we reached it from
        var distance = new Dictionary<Vector2Int, int>();          // tile -> steps from start
        var queue = new Queue<Vector2Int>();

        queue.Enqueue(start);
        distance[start] = 0;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (StepsFrom(current, unit.size, targetTile) == 1)   // from here we'd be touching
                return FirstStep(current, start, cameFrom);

            if (distance[current] >= maxPathLength) continue;     // too far, don't spread further

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = current + dir;
                if (distance.ContainsKey(next)) continue;          // already visited
                if (!IsFree(next)) continue;                       // blocked

                distance[next] = distance[current] + 1;
                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }
        return null;
    }

    // Walk back from the goal until the tile right after the start
    static Vector2Int? FirstStep(Vector2Int goal, Vector2Int start, Dictionary<Vector2Int, Vector2Int> cameFrom)
    {
        if (goal == start) return null;
        Vector2Int step = goal;
        while (cameFrom[step] != start)
            step = cameFrom[step];
        return step;
    }

    CombatUnit ClosestPartyMember(out int bestSteps)
    {
        CombatUnit best = null;
        bestSteps = int.MaxValue;
        foreach (CombatUnit p in party)
        {
            if (p == null || !p.IsAlive) continue;
            int s = unit.StepsTo(TileOf(p.transform.position));
            if (s < bestSteps)
            {
                bestSteps = s;
                best = p;
            }
        }
        return best;
    }

    // Could our whole footprint stand here (bottom-left tile = 'anchor')?
    bool IsFree(Vector2Int anchor)
    {
        float t = unit.tileSize;
        Vector3 halfSize = new Vector3(0.4f, 1f, 0.4f) * t;   // same tall box the player uses

        for (int x = 0; x < unit.size; x++)
            for (int y = 0; y < unit.size; y++)
            {
                Vector2Int tile = anchor + new Vector2Int(x, y);
                Vector3 center = new Vector3(tile.x * t, transform.position.y, tile.y * t);

                // Rock or wall?
                if (Physics.CheckBox(center, halfSize, Quaternion.identity, obstacleLayers))
                    return false;

                // Ground under it? (don't walk off the map)
                if (!Physics.Raycast(center + Vector3.up, Vector3.down, 5f, ~obstacleLayers))
                    return false;

                // A party member standing there?
                foreach (CombatUnit p in party)
                    if (p != null && TileOf(p.transform.position) == tile)
                        return false;

                // Another enemy standing there?
                foreach (CombatUnit e in otherEnemies)
                    if (e != null && e.IsAlive && e.Occupies(tile))
                        return false;
            }
        return true;
    }

    // Steps from a tile to the nearest tile of a footprint (same maths as CombatUnit.StepsTo)
    static int StepsFrom(Vector2Int anchor, int size, Vector2Int tile)
    {
        int dx = Mathf.Max(anchor.x - tile.x, 0, tile.x - (anchor.x + size - 1));
        int dy = Mathf.Max(anchor.y - tile.y, 0, tile.y - (anchor.y + size - 1));
        return dx + dy;
    }

    Vector2Int TileOf(Vector3 pos)
    {
        return new Vector2Int(Mathf.RoundToInt(pos.x / unit.tileSize),
                              Mathf.RoundToInt(pos.z / unit.tileSize));
    }
}