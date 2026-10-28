using System.Collections.Generic;
using UnityEngine;

// Takes over when an encounter starts: fills the room with the battle grid,
// freezes exploration movement, and snaps everyone onto the nearest free tile
// right where they are standing.
// When everyone is in place it fires OnBattleReady, and TurnManager takes it from there.
public class CombatManager : MonoBehaviour
{
    [Header("References")]
    public GridManager grid;
    public CameraController cameraController;

    [Header("Setup")]
    [Tooltip("Enemies this many steps from the one that touched you join the fight. 0 = every enemy in the room joins.")]
    public int joinRange = 0;

    public List<CombatUnit> Party { get; } = new List<CombatUnit>();
    public List<CombatUnit> Enemies { get; } = new List<CombatUnit>();
    public bool InCombat { get; private set; }

    // Fired once both sides are placed on the grid
    public event System.Action OnBattleReady;

    void OnEnable() => EnemyEncounter.OnCombatStarted += BeginCombat;
    void OnDisable() => EnemyEncounter.OnCombatStarted -= BeginCombat;

    void BeginCombat(EnemyEncounter starter, CombatUnit touched)
    {
        if (InCombat) return;
        if (grid == null)
        {
            Debug.LogError("CombatManager has no GridManager assigned!");
            return;
        }

        InCombat = true;
        Party.Clear();
        Enemies.Clear();

        // 1. Fill the room around the fight with the grid
        grid.BuildAround(touched.transform.position, Room.At(touched.transform.position));

        // 2. Party: whoever got touched goes first in the list
        Party.Add(touched);
        foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (u.team == Team.Party && u.IsAlive && u != touched)
                Party.Add(u);

        // 3. Enemies: the one that touched us, plus the others in the same room
        Enemies.Add(starter.Unit);
        foreach (EnemyEncounter e in FindObjectsByType<EnemyEncounter>(FindObjectsSortMode.None))
        {
            if (e == starter || e.InCombat || !e.Unit.IsAlive) continue;
            if (!OnGrid(e.Unit)) continue;   // behind a wall / in another room
            if (joinRange > 0 && e.Unit.StepsTo(starter.Unit.AnchorTile) > joinRange) continue;

            e.MarkInCombat();
            Enemies.Add(e.Unit);
        }

        // 4. Freeze exploration movement (it would fight with combat movement)
        foreach (CombatUnit u in Party) SetExplorationMovement(u, false);
        foreach (CombatUnit u in Enemies) SetExplorationMovement(u, false);

        // Know when someone drops
        foreach (CombatUnit u in Party) u.OnDied += HandleDeath;
        foreach (CombatUnit u in Enemies) u.OnDied += HandleDeath;

        // 5. Everyone snaps to the nearest free tile from where they stand
        var taken = new HashSet<Vector2Int>();
        foreach (CombatUnit u in Party) Place(u, taken);
        foreach (CombatUnit u in Enemies) Place(u, taken);

        Debug.Log($"Battle! Party: {Names(Party)}  vs  Enemies: {Names(Enemies)}");

        // 6. Hand over to TurnManager (initiative, turns, camera)
        OnBattleReady?.Invoke();
    }

    // ---------- End of battle ----------

    // Called by TurnManager when every monster is dead: back to exploring
    public void EndCombat()
    {
        if (!InCombat) return;
        InCombat = false;

        foreach (CombatUnit u in Party) u.OnDied -= HandleDeath;
        foreach (CombatUnit u in Enemies) u.OnDied -= HandleDeath;

        grid.ClearGrid();
        foreach (CombatUnit u in Party)
            if (u.IsAlive) SetExplorationMovement(u, true);

        GameState.Current = GameMode.Exploring;
        if (cameraController != null && Party.Count > 0)
        {
            CombatUnit leader = Party.Find(p => p.GetComponent<PlayerMovement>() != null) ?? Party[0];
            cameraController.FocusOn(leader.transform);
        }
        Debug.Log("Back to exploring.");
    }

    // Monsters vanish when they die; party members go grey (they're down, not gone)
    void HandleDeath(CombatUnit u)
    {
        if (u.team == Team.Enemy)
        {
            foreach (Renderer rend in u.GetComponentsInChildren<Renderer>(true)) rend.enabled = false;
            if (u.TryGetComponent(out EnemyChase chase)) chase.enabled = false;
            if (u.TryGetComponent(out EnemyEncounter encounter)) encounter.enabled = false;
        }
        else
        {
            foreach (SpriteRenderer sr in u.GetComponentsInChildren<SpriteRenderer>(true))
                sr.color = new Color(0.35f, 0.32f, 0.3f, 1f);
        }
    }

    // Is this unit standing inside the battle grid?
    bool OnGrid(CombatUnit u)
    {
        Vector2Int t = u.AnchorTile - grid.OriginTile;
        return grid.GetTile(t.x, t.y) != null;
    }

    void Place(CombatUnit u, HashSet<Vector2Int> taken)
    {
        Vector2Int desired = u.AnchorTile - grid.OriginTile;   // world tile -> grid tile
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

    // Closest spot (in steps) where the whole footprint fits
    bool FindFreeSpot(Vector2Int desired, int size, HashSet<Vector2Int> taken, out Vector2Int best)
    {
        best = desired;
        int bestCost = int.MaxValue;

        for (int x = 0; x <= grid.Width - size; x++)
            for (int y = 0; y <= grid.Height - size; y++)
            {
                Vector2Int a = new Vector2Int(x, y);
                int cost = Mathf.Abs(x - desired.x) + Mathf.Abs(y - desired.y);
                if (cost >= bestCost || !Fits(a, size, taken)) continue;

                bestCost = cost;
                best = a;
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