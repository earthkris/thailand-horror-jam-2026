using System.Collections.Generic;
using UnityEngine;

// Takes over when an encounter starts: builds the grid, freezes exploration movement,
// and lines both sides up on opposite edges of the battle area.
public class CombatManager : MonoBehaviour
{
    [Header("References")]
    public GridManager grid;
    public CameraController cameraController;

    [Header("Setup")]
    [Tooltip("Other enemies this many steps from the one that touched you join the fight.")]
    public int joinRadius = 4;
    [Tooltip("Empty columns between each side and the edge of the grid.")]
    public int edgeMargin = 1;

    public List<CombatUnit> Party { get; } = new List<CombatUnit>();
    public List<CombatUnit> Enemies { get; } = new List<CombatUnit>();
    public bool InCombat { get; private set; }

    void OnEnable() => EnemyEncounter.OnCombatStarted += BeginCombat;
    void OnDisable() => EnemyEncounter.OnCombatStarted -= BeginCombat;

    void BeginCombat(EnemyEncounter starter, CombatUnit touched)
    {
        if (InCombat) return;
        InCombat = true;
        Party.Clear();
        Enemies.Clear();

        // 1. Party: whoever got touched goes first in the list = front row
        Party.Add(touched);
        foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (u.team == Team.Party && u.IsAlive && u != touched)
                Party.Add(u);

        // 2. Enemies: the one that touched us, plus any others nearby
        Enemies.Add(starter.Unit);
        foreach (EnemyEncounter e in FindObjectsByType<EnemyEncounter>(FindObjectsSortMode.None))
        {
            if (e == starter || e.InCombat || !e.Unit.IsAlive) continue;
            if (e.Unit.StepsTo(starter.Unit.AnchorTile) <= joinRadius)
            {
                e.MarkInCombat();
                Enemies.Add(e.Unit);
            }
        }

        // 3. Freeze exploration movement (it would fight with the teleport)
        foreach (CombatUnit u in Party) SetExplorationMovement(u, false);
        foreach (CombatUnit u in Enemies) SetExplorationMovement(u, false);

        // 4. Build the grid and line everyone up
        grid.BuildGrid();
        var taken = new HashSet<Vector2Int>();
        int mid = grid.height / 2;

        // Party on the LEFT: first one in the front column, the rest in the column behind
        int partyFront = edgeMargin + 1;
        for (int i = 0; i < Party.Count; i++)
        {
            int column = (i == 0) ? partyFront : partyFront - 1;
            int row = mid + RowOffset(i == 0 ? 0 : i - 1);
            Place(Party[i], new Vector2Int(column, row), taken);
        }

        // Enemies on the RIGHT: the left edge of their footprint faces the party
        int enemyFront = grid.width - 1 - edgeMargin - 1;
        foreach (CombatUnit e in Enemies)
            Place(e, new Vector2Int(enemyFront, mid - (e.size - 1) / 2), taken);

        // 5. Camera on whoever is in front
        if (cameraController != null) cameraController.FocusOn(Party[0].transform);

        Debug.Log($"Battle! Party: {Names(Party)}  vs  Enemies: {Names(Enemies)}");
    }

    // 0, +1, -1, +2, -2 ... spreads extra units above and below the middle row
    static int RowOffset(int k) => (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);

    void Place(CombatUnit u, Vector2Int desired, HashSet<Vector2Int> taken)
    {
        if (!FindFreeSpot(desired, u.size, taken, out Vector2Int spot))
        {
            Debug.LogWarning($"No room on the grid for {u.unitName}!");
            return;
        }

        for (int x = 0; x < u.size; x++)
            for (int y = 0; y < u.size; y++)
                taken.Add(spot + new Vector2Int(x, y));

        // Grid tile -> world tile -> world position (keeps the unit's own height)
        u.transform.position = u.WorldPositionFor(grid.OriginTile + spot);
    }

    // Closest spot where the whole footprint fits. Prefers moving up/down over sideways,
    // so units stay on their own side.
    bool FindFreeSpot(Vector2Int desired, int size, HashSet<Vector2Int> taken, out Vector2Int best)
    {
        best = desired;
        int bestCost = int.MaxValue;

        for (int x = 0; x <= grid.width - size; x++)
            for (int y = 0; y <= grid.height - size; y++)
            {
                Vector2Int a = new Vector2Int(x, y);
                if (!Fits(a, size, taken)) continue;

                int cost = Mathf.Abs(x - desired.x) * 3 + Mathf.Abs(y - desired.y);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = a;
                }
            }
        return bestCost != int.MaxValue;
    }

    bool Fits(Vector2Int anchor, int size, HashSet<Vector2Int> taken)
    {
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                Vector2Int t = anchor + new Vector2Int(x, y);
                if (!grid.IsWalkable(t.x, t.y) || taken.Contains(t)) return false;
            }
        return true;
    }

    // Turns exploration scripts off (combat) or back on (after combat)
    static void SetExplorationMovement(CombatUnit u, bool on)
    {
        if (u.TryGetComponent(out PlayerMovement pm)) pm.enabled = on;
        if (u.TryGetComponent(out Follower f)) f.enabled = on;
        if (u.TryGetComponent(out EnemyChase c)) c.enabled = on;
    }

    static string Names(List<CombatUnit> units)
    {
        var names = new List<string>();
        foreach (CombatUnit u in units) names.Add(u.unitName);
        return string.Join(", ", names);
    }
}