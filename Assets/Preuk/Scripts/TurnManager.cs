using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Rolls initiative when a battle starts, then hands out turns in that order.
// Lives on the same GameObject as CombatManager.
[RequireComponent(typeof(CombatManager))]
public class TurnManager : MonoBehaviour
{
    [Tooltip("Seconds a monster pauses before acting, and after it's done.")]
    public float enemyTurnDelay = 0.5f;

    // One entry in the initiative order
    public class Combatant
    {
        public CombatUnit unit;
        public int roll;        // the d20 alone
        public int initiative;  // d20 + Dex
    }

    public List<Combatant> Order { get; } = new List<Combatant>();
    public CombatUnit Current { get; private set; }
    public int Round { get; private set; }
    public bool InBattle { get; private set; }

    // What the current unit has left this turn (D&D: movement can be split, plus one action)
    public int MovesLeft { get; private set; }
    public bool HasMoved => MovesLeft <= 0;   // no tiles of movement left
    public bool HasActed { get; private set; }

    // Is the "where can I walk" highlight on? (MOVE button toggles it)
    public bool ShowMoveRange { get; private set; } = true;

    public bool IsPartyTurn => Current != null && Current.team == Team.Party;

    // The UI listens to this and redraws whenever anything changes
    public event System.Action OnChanged;

    // Fired when the whole party is down. DeathScreen listens.
    public static event System.Action OnPartyFallen;

    CombatManager combat;
    CombatTargeting targeting;
    int index;   // position in Order
    readonly Dictionary<(CombatUnit, CombatAction), int> usesLeft = new Dictionary<(CombatUnit, CombatAction), int>();

    void Awake()
    {
        combat = GetComponent<CombatManager>();
        targeting = GetComponent<CombatTargeting>();
    }
    void OnEnable() => combat.OnBattleReady += BeginBattle;
    void OnDisable() => combat.OnBattleReady -= BeginBattle;

    // ---------- Start of battle ----------

    void BeginBattle()
    {
        Order.Clear();
        usesLeft.Clear();   // every action gets its full uses again each fight

        var everyone = new List<CombatUnit>(combat.Party);
        everyone.AddRange(combat.Enemies);

        // Everyone rolls d20 + Dex
        foreach (CombatUnit u in everyone)
        {
            int roll = DiceSimulator.Roll(20);
            int total = roll + u.dexModifier;
            Order.Add(new Combatant { unit = u, roll = roll, initiative = total });
            Debug.Log($"{u.unitName} rolls initiative: d20 ({roll}) + {u.dexModifier} = {total}");
        }

        // Highest goes first. Ties: higher Dex wins, then the party goes before enemies.
        Order.Sort((a, b) =>
        {
            if (a.initiative != b.initiative) return b.initiative.CompareTo(a.initiative);
            if (a.unit.dexModifier != b.unit.dexModifier) return b.unit.dexModifier.CompareTo(a.unit.dexModifier);
            return a.unit.team.CompareTo(b.unit.team);   // Party (0) before Enemy (1)
        });

        InBattle = true;
        Round = 1;
        index = 0;
        StartTurn();
    }

    // ---------- Turns ----------

    void StartTurn()
    {
        // Whole party down? The death screen takes over
        if (!AnyAlive(Team.Party))
        {
            FinishBattle(false);
            return;
        }

        // Every monster down? Victory!
        if (!AnyAlive(Team.Enemy))
        {
            FinishBattle(true);
            return;
        }

        // Skip anyone who is down
        int skipped = 0;
        while (!Order[index].unit.IsAlive)
        {
            Advance();
            if (++skipped > Order.Count)
            {
                Debug.LogWarning("Nobody left standing.");
                return;
            }
        }

        Current = Order[index].unit;
        MovesLeft = Current.MoveTiles;
        HasActed = false;

        // Don't point the camera at an enemy hiding in the dark (that would give it away)
        if (combat.cameraController != null && FogOfWar.CanSee(Current))
            combat.cameraController.FocusOn(Current.transform);

        Debug.Log($"Round {Round}: {Current.unitName}'s turn{(FogOfWar.CanSee(Current) ? "" : " (unseen, in the dark)")}");
        OnChanged?.Invoke();

        if (!IsPartyTurn) StartCoroutine(EnemyTurn());
    }

    void FinishBattle(bool won)
    {
        StopAllCoroutines();
        if (targeting != null) targeting.Cancel();
        InBattle = false;
        Current = null;
        Debug.Log(won ? "Victory! The monsters are dead." : "The party has fallen...");
        OnChanged?.Invoke();
        if (won) combat.EndCombat();
        else OnPartyFallen?.Invoke();   // DeathScreen takes it from here
    }

    bool AnyAlive(Team team)
    {
        foreach (Combatant c in Order)
            if (c.unit != null && c.unit.team == team && c.unit.IsAlive) return true;
        return false;
    }

    // Next unit in the list; wrapping around starts a new round
    void Advance()
    {
        index++;
        if (index >= Order.Count)
        {
            index = 0;
            Round++;
        }
    }

    public void EndTurn()
    {
        if (!InBattle) return;
        if (targeting != null) targeting.Cancel();
        Advance();
        StartTurn();
    }

    // Monsters with an EnemyAI walk and attack; anything without one just waits
    IEnumerator EnemyTurn()
    {
        CombatUnit me = Current;
        yield return new WaitForSeconds(enemyTurnDelay);

        if (me.TryGetComponent(out EnemyAI ai))
            yield return ai.TakeTurn(combat);
        else
            Debug.Log($"{me.unitName} waits. (Give it an EnemyAI to make it fight)");

        OnChanged?.Invoke();   // HP may have changed
        yield return new WaitForSeconds(enemyTurnDelay);
        if (Current == me) EndTurn();
    }

    // CombatMovement calls this after every step (1 tile = 5 ft)
    public void SpendMove(int tiles = 1)
    {
        MovesLeft = Mathf.Max(MovesLeft - tiles, 0);
        OnChanged?.Invoke();
    }

    public void MarkActed() { HasActed = true; OnChanged?.Invoke(); }

    // ---------- Buttons (TurnPanel calls these) ----------

    // Walking itself is done with WASD / arrow keys (CombatMovement).
    // The MOVE button just shows or hides where you can still go.
    public void ChooseMove()
    {
        if (!IsPartyTurn || HasMoved) return;
        ShowMoveRange = !ShowMoveRange;
        OnChanged?.Invoke();
    }

    // ACTION menu -> an option was picked: now choose who it's aimed at (CombatTargeting)
    public void ChooseAction(CombatAction action)
    {
        if (!IsPartyTurn || HasActed || action == null) return;
        if (UsesLeft(Current, action) == 0) return;
        if (targeting != null) targeting.BeginAction(Current, action);
        else Debug.LogWarning("Add a CombatTargeting component to the Combat object.");
    }

    // USE ITEM menu -> an item was picked. Using an item takes your action (D&D rule).
    public void ChooseItem(ItemData item)
    {
        if (!IsPartyTurn || HasActed || item == null) return;
        if (targeting != null) targeting.BeginItem(Current, item);
        else Debug.LogWarning("Add a CombatTargeting component to the Combat object.");
    }

    // How many more times this unit can use this action this battle. -1 = unlimited.
    public int UsesLeft(CombatUnit unit, CombatAction action)
    {
        if (action.usesPerBattle <= 0) return -1;
        return usesLeft.TryGetValue((unit, action), out int n) ? n : action.usesPerBattle;
    }

    // CombatTargeting calls this after an action really happens
    public void SpendUse(CombatUnit unit, CombatAction action)
    {
        if (action.usesPerBattle <= 0) return;
        usesLeft[(unit, action)] = Mathf.Max(UsesLeft(unit, action) - 1, 0);
        OnChanged?.Invoke();
    }

    // After an attack kills the last monster, the fight should end right away
    public void CheckForVictory()
    {
        if (InBattle && !AnyAlive(Team.Enemy)) FinishBattle(true);
    }

    public void EndTurnButton()
    {
        if (!IsPartyTurn) return;
        if (targeting != null && targeting.IsBusy) return;   // wait for the dice
        EndTurn();
    }
}
