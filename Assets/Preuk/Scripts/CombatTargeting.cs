using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// After picking something in the ACTION or USE ITEM menu: shows what it can reach,
// waits for you to click a target, then rolls and applies it.
//   Attacks: d20 + bonus vs the target's AC, rolled on the real dice (DiceTray).
//            Natural 20 = critical hit (damage dice twice). Natural 1 = always a miss.
//            Disadvantage (roll twice, keep the lower): the target is hidden in the dark,
//            or it's a ranged attack with an enemy right next to you.
//   Heals and items: no attack roll, just roll how much.
// Right-click or Esc cancels while aiming. Lives on the Combat object.
[RequireComponent(typeof(TurnManager))]
public class CombatTargeting : MonoBehaviour
{
    [Header("Highlight")]
    public Color enemyColor = new Color32(194, 59, 59, 255);   // blood
    public Color allyColor = new Color32(143, 176, 120, 255);  // pale green
    [Tooltip("Tiles inside the action's reach.")]
    [Range(0f, 1f)] public float rangeStrength = 0.12f;
    [Tooltip("Tiles of the targets you can hit.")]
    [Range(0f, 1f)] public float targetStrength = 0.45f;

    [Header("Effects")]
    [Tooltip("What flies at the target for ranged actions (Holy Verse). Empty = plain ball.")]
    public GameObject rangedProjectile;
    [Tooltip("Tiles per second.")]
    public float projectileSpeed = 12f;

    TurnManager turns;
    CombatManager combat;
    CombatMovement movement;
    PartyInventory inventory;
    GridManager Grid => combat.grid;

    CombatUnit user;
    CombatAction action;   // one of these two is set
    ItemData item;
    bool aiming;
    bool busy;

    public bool IsActive => aiming || busy;   // CombatMovement stands still while true
    public bool IsBusy => busy;               // the dice are rolling

    void Awake()
    {
        turns = GetComponent<TurnManager>();
        combat = GetComponent<CombatManager>();
        movement = GetComponent<CombatMovement>();
        inventory = FindAnyObjectByType<PartyInventory>();
    }

    // ================= Start / stop aiming =================

    public void BeginAction(CombatUnit who, CombatAction what)
    {
        if (busy) return;   // still resolving the last one
        Cancel();
        user = who; action = what; item = null;
        StartAiming();
    }

    public void BeginItem(CombatUnit who, ItemData what)
    {
        if (busy) return;
        Cancel();
        user = who; action = null; item = what;
        StartAiming();
    }

    public void Cancel()
    {
        if (!aiming) return;
        aiming = false;
        if (Grid != null)
        {
            Grid.OnTileClicked -= OnTileClicked;
            Grid.ClearMarks();
        }
        if (movement != null) movement.Refresh();
    }

    void StartAiming()
    {
        aiming = true;
        Grid.OnTileClicked += OnTileClicked;
        if (movement != null) movement.Refresh();   // hide the walking range while aiming

        // Dim: everywhere it can reach. Bright: the targets you can actually hit (that you can see).
        Color color = Target == ActionTarget.Enemy ? enemyColor : allyColor;
        var reach = new List<Vector2Int>();
        for (int x = 0; x < Grid.Width; x++)
            for (int y = 0; y < Grid.Height; y++)
                if (Grid.GetTile(x, y) != null && GapTo(Grid.OriginTile + new Vector2Int(x, y)) <= Range)
                    reach.Add(new Vector2Int(x, y));
        Grid.MarkTiles(reach, color, rangeStrength);

        var hits = new List<Vector2Int>();
        foreach (CombatUnit t in Candidates())
            if (CanReach(t) && FogOfWar.CanSee(t))
                foreach (Vector2Int w in t.GetOccupiedTiles())
                    hits.Add(w - Grid.OriginTile);
        Grid.MarkTiles(hits, color, targetStrength);

        Debug.Log($"{user.unitName}: choose a target for {Name}  (right-click / Esc to cancel)");
    }

    void Update()
    {
        if (!aiming) return;
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        if (esc || rightClick) Cancel();
    }

    void OnTileClicked(Vector2Int gridTile)
    {
        if (!aiming || busy) return;
        Vector2Int world = Grid.OriginTile + gridTile;

        CombatUnit target = null;
        foreach (CombatUnit c in Candidates())
            if (c.Occupies(world)) { target = c; break; }

        if (target == null) { Debug.Log("No target there."); return; }
        if (GapTo(target) > Range) { Debug.Log($"{TargetName(target)} is out of reach."); return; }
        if (!HasLineOfSight(target)) { Debug.Log("Something is in the way."); return; }

        StartCoroutine(Resolve(target));
    }

    // ================= Doing it =================

    IEnumerator Resolve(CombatUnit target)
    {
        Cancel();
        busy = true;
        if (movement != null) movement.Refresh();

        if (item != null) yield return UseItem(target);
        else if (action.effect == ActionEffect.Damage) yield return Attack(target);
        else yield return Heal(action.displayName, action.diceCount, action.diceSides, action.bonus, target);

        if (action != null) turns.SpendUse(user, action);
        turns.MarkActed();
        busy = false;
        if (movement != null) movement.Refresh();
        turns.CheckForVictory();
    }

    IEnumerator Attack(CombatUnit target)
    {
        bool hidden = !FogOfWar.CanSee(target);
        bool ranged = action.range > 1;
        bool crowded = ranged && EnemyNextTo(user);
        bool disadvantage = hidden || crowded;
        string why = hidden ? "can't see the target" : "enemy right next to you";
        string tName = TargetName(target);
        int need = Mathf.Clamp(target.armorClass - action.attackBonus, 2, 20);

        // 1. Roll to hit (twice with disadvantage, keep the lower)
        int r1 = 0, r2 = 0;
        yield return DiceTray.RollD20($"{action.displayName}  —  need {need}+" + (disadvantage ? "   (disadvantage)" : ""), v => r1 = v);
        int roll = r1;
        string rollText = $"d20 ({r1})";
        if (disadvantage)
        {
            yield return DiceTray.RollD20($"Disadvantage: {why}  —  second roll", v => r2 = v);
            roll = Mathf.Min(r1, r2);
            rollText = $"d20 ({r1}, {r2} -> {roll}, disadvantage)";
        }

        // 2. Show the swing / shot
        if (ranged) yield return Shoot(user.transform, target.transform);
        else yield return Lunge(user.transform, target.transform);

        // 3. Hit or miss
        int total = roll + action.attackBonus;
        bool crit = roll == 20;
        bool hit = crit || (roll != 1 && total >= target.armorClass);
        string head = $"{user.unitName} uses {action.displayName} on {tName}: {rollText} + {action.attackBonus} = {total} vs AC {target.armorClass}";

        if (!hit)
        {
            Debug.Log($"{head} — miss{(roll == 1 ? " (natural 1)" : "")}");
            yield return DiceTray.ShowOutcome(roll == 1 ? "NATURAL 1  —  MISS" : "MISS", false);
            yield break;
        }

        // 4. Damage (a critical hit rolls every damage die twice)
        int times = crit ? 2 : 1;
        int damage = DiceSimulator.Roll(action.diceCount * times, action.diceSides) + action.bonus;
        if (action.extraDiceCount > 0) damage += DiceSimulator.Roll(action.extraDiceCount * times, action.extraDiceSides);
        damage = Mathf.Max(damage, 1);

        Debug.Log($"{head} — {(crit ? "CRITICAL HIT" : "hit")}! {damage} {action.damageType} damage");
        target.TakeDamage(damage);
        string killed = target.IsAlive ? "" : "  —  IT FALLS";
        yield return DiceTray.ShowOutcome((crit ? $"CRITICAL!  {damage} damage" : $"HIT  —  {damage} damage") + killed, true);
    }

    IEnumerator Heal(string what, int count, int sides, int bonus, CombatUnit target)
    {
        int amount = Mathf.Max(DiceSimulator.Roll(count, sides) + bonus, 0);
        int before = target.CurrentHP;
        target.Heal(amount);
        int healed = target.CurrentHP - before;
        Debug.Log($"{user.unitName} uses {what} on {target.unitName}: {count}d{sides}+{bonus} = {amount} -> healed {healed} ({target.CurrentHP}/{target.maxHP} HP)");
        yield return DiceTray.ShowMessage(what, $"+{healed} HP  {target.unitName}", true);
    }

    IEnumerator UseItem(CombatUnit target)
    {
        if (inventory == null || !inventory.Remove(item))
        {
            Debug.Log($"No {item.displayName} left.");
            yield break;
        }
        if (item.effect == ItemEffect.Heal)
            yield return Heal(item.displayName, item.diceCount, item.diceSides, item.bonus, target);
        else
            Debug.Log($"{user.unitName} uses {item.displayName}.");
    }

    // ================= Rules =================

    ActionTarget Target => item != null ? item.target : action.target;
    int Range => item != null ? item.range : action.range;
    string Name => item != null ? item.displayName : action.displayName;
    bool NeedsSight => item == null && action.needsLineOfSight;

    // Who could this be aimed at at all?
    IEnumerable<CombatUnit> Candidates()
    {
        if (Target == ActionTarget.Self) { yield return user; yield break; }
        var list = Target == ActionTarget.Enemy ? combat.Enemies : combat.Party;
        foreach (CombatUnit u in list)
            if (u != null && u.IsAlive) yield return u;
    }

    bool CanReach(CombatUnit t) => GapTo(t) <= Range && HasLineOfSight(t);

    bool HasLineOfSight(CombatUnit t)
    {
        if (!NeedsSight || t == user) return true;
        return !Physics.Linecast(user.transform.position, t.transform.position, Grid.obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    // Steps between the user's footprint and a tile / another unit (0 = same spot, 1 = next to it)
    int GapTo(Vector2Int worldTile)
    {
        Vector2Int a = user.AnchorTile;
        int dx = Mathf.Max(0, worldTile.x - (a.x + user.size - 1), a.x - worldTile.x);
        int dy = Mathf.Max(0, worldTile.y - (a.y + user.size - 1), a.y - worldTile.y);
        return dx + dy;
    }

    int GapTo(CombatUnit t)
    {
        int best = int.MaxValue;
        foreach (Vector2Int w in t.GetOccupiedTiles()) best = Mathf.Min(best, GapTo(w));
        return best;
    }

    bool EnemyNextTo(CombatUnit u)
    {
        foreach (CombatUnit e in combat.Enemies)
            if (e != null && e.IsAlive && e.StepsTo(u.AnchorTile) <= 1) return true;
        return false;
    }

    static string TargetName(CombatUnit t) => FogOfWar.CanSee(t) ? t.unitName : "something in the dark";

    // ================= Animation =================

    static IEnumerator Lunge(Transform who, Transform at)
    {
        Vector3 home = who.position;
        Vector3 toward = at.position - home;
        toward.y = 0f;
        Vector3 hitPos = home + toward.normalized * 0.35f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.1f) { who.position = Vector3.Lerp(home, hitPos, t); yield return null; }
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f) { who.position = Vector3.Lerp(hitPos, home, t); yield return null; }
        who.position = home;
    }

    IEnumerator Shoot(Transform from, Transform to)
    {
        Vector3 a = from.position, b = to.position;
        GameObject p = rangedProjectile != null ? Instantiate(rangedProjectile) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (rangedProjectile == null)
        {
            Destroy(p.GetComponent<Collider>());
            p.transform.localScale = Vector3.one * 0.2f;
        }
        p.transform.SetPositionAndRotation(a, Quaternion.LookRotation(b - a));
        float time = Vector3.Distance(a, b) / Mathf.Max(projectileSpeed, 0.1f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / time) { p.transform.position = Vector3.Lerp(a, b, t); yield return null; }
        Destroy(p);
    }
}
