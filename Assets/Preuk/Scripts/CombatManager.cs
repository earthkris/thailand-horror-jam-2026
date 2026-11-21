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

    // As a prefab in a new scene: find the grid and the camera by itself
    void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<GridManager>();
        if (cameraController == null) cameraController = CameraController.Main;
    }

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
        foreach (CombatUnit u in CombatUnit.OnTeam(Team.Party))
            if (u.IsAlive && u != touched)
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

    // A monster that arrives mid-fight (the boss calls its brood): onto the nearest free tile to 'near',
    // into the enemy list, frozen out of exploring. False if there was no room on the grid.
    public bool AddEnemy(CombatUnit u, Vector2Int near)
    {
        if (!InCombat || u == null) return false;
        var taken = new HashSet<Vector2Int>();
        foreach (CombatUnit other in Party)
            if (other != null && !other.IsDead) foreach (Vector2Int t in other.GetOccupiedTiles()) taken.Add(t - grid.OriginTile);
        foreach (CombatUnit other in Enemies)
            if (other != null && !other.IsDead) foreach (Vector2Int t in other.GetOccupiedTiles()) taken.Add(t - grid.OriginTile);
        if (!FindFreeSpot(near, u.size, taken, out Vector2Int spot)) return false;

        u.transform.position = u.WorldPositionFor(grid.OriginTile + spot);
        Enemies.Add(u);
        u.OnDied += HandleDeath;
        if (u.TryGetComponent(out EnemyEncounter e)) e.MarkInCombat();
        SetExplorationMovement(u, false);
        return true;
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
        {
            // Baldur's Gate: whoever is only downed gets back up when the fight is won. The dead stay down.
            if (u.IsDowned)
            {
                u.Revive(1);
                Debug.Log($"{u.unitName} gets back up after the fight (1 HP)");
            }
            if (u.IsAlive) SetExplorationMovement(u, true);
        }

        GameState.Current = GameMode.Exploring;
        if (cameraController != null && Party.Count > 0)
        {
            CombatUnit leader = Party.Find(p => p.IsPlayer) ?? Party[0];
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

    // ---------- Questions about the grid (combat movement and the monsters' brains ask these) ----------

    // The grid tile (0,0 = bottom-left of the room) that a unit's bottom-left tile is on
    public Vector2Int GridTileOf(CombatUnit u) => u.AnchorTile - grid.OriginTile;

    // Could this unit's whole footprint stand with its bottom-left on grid tile 'anchor'?
    // 'blocker' = the unit in the way, if that's what stopped it.
    // ignoreUnseen: monsters hidden in the dark don't count (so the UI doesn't give them away).
    public bool CanStand(CombatUnit u, Vector2Int anchor, out CombatUnit blocker, bool ignoreUnseen = false)
    {
        blocker = null;
        for (int x = 0; x < u.size; x++)
            for (int y = 0; y < u.size; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                if (!grid.IsWalkable(cell.x, cell.y)) return false;   // wall, rock, outside the room

                Vector2Int world = grid.OriginTile + cell;
                blocker = UnitOn(Party, world, u, ignoreUnseen);
                if (blocker == null) blocker = UnitOn(Enemies, world, u, ignoreUnseen);
                if (blocker != null) return false;
            }
        return true;
    }

    // Whoever of 'units' covers this world tile (the dead don't count, nor does 'except')
    static CombatUnit UnitOn(List<CombatUnit> units, Vector2Int worldTile, CombatUnit except, bool ignoreUnseen)
    {
        foreach (CombatUnit other in units)
        {
            if (other == null || other == except || other.IsDead || !other.Occupies(worldTile)) continue;
            if (ignoreUnseen && !FogOfWar.CanSee(other)) continue;
            return other;
        }
        return null;
    }

    // ---------- Placing everyone when the battle starts ----------

    // Is this unit standing inside the battle grid?
    bool OnGrid(CombatUnit u)
    {
        Vector2Int t = GridTileOf(u);
        return grid.GetTile(t.x, t.y) != null;
    }

    void Place(CombatUnit u, HashSet<Vector2Int> taken)
    {
        Vector2Int desired = GridTileOf(u);
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

    // Turns a unit's exploration scripts off (combat) or back on (after combat, or once it's back on its feet)
    public static void SetExplorationMovement(CombatUnit u, bool on)
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