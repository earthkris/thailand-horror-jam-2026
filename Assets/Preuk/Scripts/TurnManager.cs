using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Rolls initiative when a battle starts, then hands out turns in that order.
// Lives on the same GameObject as CombatManager.
[RequireComponent(typeof(CombatManager))]
public class TurnManager : MonoBehaviour
{
    [Tooltip("Seconds an enemy waits before ending its turn (until enemy AI exists in step 5).")]
    public float enemyTurnDelay = 1f;

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

    CombatManager combat;
    int index;   // position in Order

    void Awake() => combat = GetComponent<CombatManager>();
    void OnEnable() => combat.OnBattleReady += BeginBattle;
    void OnDisable() => combat.OnBattleReady -= BeginBattle;

    // ---------- Start of battle ----------

    void BeginBattle()
    {
        Order.Clear();

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
        Advance();
        StartTurn();
    }

    // Placeholder until step 5: the enemy just waits a moment, then passes
    IEnumerator EnemyTurn()
    {
        CombatUnit me = Current;
        yield return new WaitForSeconds(enemyTurnDelay);
        Debug.Log($"{me.unitName} waits. (Enemy AI comes in step 5)");
        if (Current == me) EndTurn();
    }

    // Later steps call these after a real move / attack happens
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

    public void ChooseAttack()
    {
        if (!IsPartyTurn || HasActed) return;
        Debug.Log($"{Current.unitName}: Attack (+{Current.attackBonus}, {Current.DamageText}) — coming in step 3");
    }

    // Using an item takes your action, same as attacking (D&D rule)
    public void ChooseItem()
    {
        if (!IsPartyTurn || HasActed) return;
        Debug.Log($"{Current.unitName}: Use Item — coming after the inventory exists");
    }

    public void EndTurnButton()
    {
        if (IsPartyTurn) EndTurn();
    }
}