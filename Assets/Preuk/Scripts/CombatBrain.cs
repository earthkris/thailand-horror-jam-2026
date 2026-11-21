using System.Collections;
using UnityEngine;

// What every monster brain shares (EnemyAI, BossAI): the battle it is in and questions about the grid.
// TurnManager calls TakeTurn on whichever brain a monster has. To make a new kind of monster,
// inherit from this and write Act().
public abstract class CombatBrain : MonoBehaviour
{
    protected CombatUnit unit;
    protected CombatManager combat;
    protected GridManager grid;
    protected TurnManager turns;

    protected virtual void Awake() => unit = GetComponent<CombatUnit>();

    // The monster's whole turn: moving and attacking
    public IEnumerator TakeTurn(CombatManager battle, TurnManager turnManager)
    {
        combat = battle;
        grid = battle.grid;
        turns = turnManager;
        return Act();
    }

    protected abstract IEnumerator Act();

    // ---------- Helpers (grid tiles: 0,0 = bottom-left of the room) ----------

    protected Vector2Int MyTile() => combat.GridTileOf(unit);

    // Could our whole footprint stand with its bottom-left on this grid tile?
    protected bool CanStand(Vector2Int anchor) => combat.CanStand(unit, anchor, out _);

    // Steps between our footprint (if standing at 'anchor') and theirs. 1 = right next to each other.
    protected int Gap(Vector2Int anchor, CombatUnit other) => unit.ReachFrom(grid.OriginTile + anchor, other);

    // The standing party member nearest to 'anchor'
    protected CombatUnit ClosestPartyMember(Vector2Int anchor)
    {
        CombatUnit best = null;
        int bestGap = int.MaxValue;
        foreach (CombatUnit p in combat.Party)
        {
            if (p == null || !p.IsAlive) continue;
            int gap = Gap(anchor, p);
            if (gap < bestGap) { bestGap = gap; best = p; }
        }
        return best;
    }

    // What the log calls us (hidden monsters stay mysterious)
    protected string Name() => FogOfWar.NameOf(unit);
}
