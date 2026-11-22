using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Opportunity attacks (Baldur's Gate / D&D): walking out of a foe's reach lets them take a free
// swing at you as you leave. It uses their reaction: one per round, back at the start of their turn.
//   Who can: anyone standing, not paralysed, who fights up close. Monsters with only a ranged
//            attack can't. The boss never walks (it jumps), so it never provokes one.
//   The hit: d20 + attack bonus vs AC, like a normal attack. Party members swing with their first
//            melee move (Sword Strike), or their own Attack stats if they have none.
// CombatMovement (the party) and EnemyAI (monsters) call this before every step.
public static class OpportunityAttack
{
    // Foes next to 'mover' now that would no longer be next to it on 'toAnchor' (a world tile)
    public static List<CombatUnit> Provokers(CombatUnit mover, Vector2Int toAnchor, CombatManager combat, TurnManager turns)
    {
        var list = new List<CombatUnit>();
        foreach (CombatUnit foe in Foes(mover, combat))
            if (WouldSwing(foe, mover, mover.AnchorTile, toAnchor, turns)) list.Add(foe);
        return list;
    }

    // Would stepping from one world tile to another give any foe a free swing? (Monsters plan their walk with this.)
    public static bool Provokes(CombatUnit mover, Vector2Int fromAnchor, Vector2Int toAnchor, CombatManager combat, TurnManager turns)
    {
        foreach (CombatUnit foe in Foes(mover, combat))
            if (WouldSwing(foe, mover, fromAnchor, toAnchor, turns)) return true;
        return false;
    }

    static List<CombatUnit> Foes(CombatUnit mover, CombatManager combat) =>
        mover.team == Team.Party ? combat.Enemies : combat.Party;

    // Can 'foe' react, and does the step take 'mover' out of its reach?
    static bool WouldSwing(CombatUnit foe, CombatUnit mover, Vector2Int from, Vector2Int to, TurnManager turns)
    {
        if (foe == null || !foe.IsAlive || foe.Has(StatusType.Paralyse) || foe.Has(StatusType.Restrained) || !turns.HasReaction(foe)) return false;
        if (!FightsUpClose(foe)) return false;
        return Reach(foe, from, mover.size) <= 1 && Reach(foe, to, mover.size) > 1;
    }

    // Steps between 'foe' and a footprint of 'size' with its bottom-left on 'anchor' (1 = next to each other)
    static int Reach(CombatUnit foe, Vector2Int anchor, int size) =>
        TileMath.ReachBetween(foe.AnchorTile, foe.size, anchor, size);

    // One free swing at 'mover' as it leaves. Uses up the attacker's reaction.
    public static IEnumerator Resolve(CombatUnit attacker, CombatUnit mover, TurnManager turns)
    {
        if (attacker == null || !attacker.IsAlive || !mover.IsAlive) yield break;
        turns.UseReaction(attacker);

        CombatAction melee = MeleeAction(attacker);
        int bonus = melee != null ? melee.attackBonus : attacker.attackBonus;
        int need = AttackRoll.Needed(mover.ArmorClass, bonus);
        bool goodForParty = mover.team == Team.Enemy;

        bool favoured = false, hampered = false;
        StatusRules.Edge(attacker, mover, true, ref favoured, ref hampered, null);
        RollMode mode = StatusRules.Mode(favoured, hampered);
        var roll = new AttackRoll();
        yield return roll.Throw($"{FogOfWar.NameOf(attacker)}: OPPORTUNITY ATTACK on {mover.unitName}  —  need {need}+", mode,
                                mode == RollMode.Advantage ? "Advantage  —  second roll" : "Disadvantage  —  second roll");
        int bless = attacker.RollBless();
        int total = roll.Value + bonus + bless - StatusRules.BaneRoll(attacker);

        yield return CombatAnimation.Lunge(attacker, mover);
        string head = $"{attacker.unitName} opportunity attack on {mover.unitName}: {roll.Text} + {bonus}" + (bless > 0 ? $" + bless ({bless})" : "") + $" = {total} vs AC {mover.ArmorClass}";
        if (!roll.Hits(total, mover.ArmorClass))
        {
            Debug.Log($"{head} — miss");
            yield return DiceTray.ShowOutcome(roll.MissText, !goodForParty);
            yield break;
        }

        int damage = melee != null
            ? DiceSimulator.Roll(melee.diceCount * roll.DiceTimes, melee.diceSides) + melee.bonus
            : DiceSimulator.Roll(attacker.damageDiceCount * roll.DiceTimes, attacker.damageDiceSides) + attacker.damageBonus;
        damage = Mathf.Max(damage, 1);
        Debug.Log($"{head} — {(roll.IsCrit ? "CRITICAL HIT" : "hit")}! {damage} damage");
        mover.TakeDamage(damage);
        string down = mover.IsAlive ? "" : $"  —  {mover.unitName.ToUpper()} IS DOWN";
        yield return DiceTray.ShowOutcome(roll.HitText(damage) + down, goodForParty);
        turns.NotifyChanged();
    }

    // Monsters: melee or both styles. The boss: yes (its body is right there). Party: everyone.
    static bool FightsUpClose(CombatUnit u)
    {
        if (u.TryGetComponent(out EnemyAI ai)) return ai.style != AttackStyle.Ranged;
        return true;
    }

    // The party's swing: their first unlimited melee damage move (Sword Strike), if any
    static CombatAction MeleeAction(CombatUnit u)
    {
        if (u.team != Team.Party) return null;
        foreach (CombatAction a in u.actions)
            if (a != null && a.effect == ActionEffect.Damage && a.range <= 1 && a.usesPerBattle == 0 && !a.bonusAction) return a;
        return null;
    }
}
