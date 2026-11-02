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
        var foes = mover.team == Team.Party ? combat.Enemies : combat.Party;
        foreach (CombatUnit f in foes)
        {
            if (f == null || !f.IsAlive || f.Has(StatusType.Paralyse) || !turns.HasReaction(f)) continue;
            if (!FightsUpClose(f)) continue;
            if (Gap(f, mover.AnchorTile, mover.size) <= 1 && Gap(f, toAnchor, mover.size) > 1) list.Add(f);
        }
        return list;
    }

    // One free swing at 'mover' as it leaves. Uses up the attacker's reaction.
    public static IEnumerator Resolve(CombatUnit attacker, CombatUnit mover, TurnManager turns)
    {
        if (attacker == null || !attacker.IsAlive || !mover.IsAlive) yield break;
        turns.UseReaction(attacker);

        CombatAction melee = MeleeAction(attacker);
        int bonus = melee != null ? melee.attackBonus : attacker.attackBonus;
        string who = FogOfWar.CanSee(attacker) ? attacker.unitName : "Something in the dark";
        int need = Mathf.Clamp(mover.ArmorClass - bonus, 2, 20);

        int roll = 0;
        yield return DiceTray.RollD20($"{who}: OPPORTUNITY ATTACK on {mover.unitName}  —  need {need}+", v => roll = v);
        int bless = (attacker.BlessDie > 0 ? DiceSimulator.Roll(attacker.BlessDie) : 0) + attacker.BlessBonus;
        int total = roll + bonus + bless;
        bool crit = roll == 20;
        bool hit = crit || (roll != 1 && total >= mover.ArmorClass);
        bool goodForParty = mover.team == Team.Enemy;

        yield return Lunge(attacker.transform, mover.transform);
        string head = $"{attacker.unitName} opportunity attack on {mover.unitName}: d20 ({roll}) + {bonus}" + (bless > 0 ? $" + bless ({bless})" : "") + $" = {total} vs AC {mover.ArmorClass}";
        if (!hit)
        {
            Debug.Log($"{head} — miss");
            yield return DiceTray.ShowOutcome(roll == 1 ? "NATURAL 1  —  MISS" : "MISS", !goodForParty);
            yield break;
        }

        int times = crit ? 2 : 1;
        int damage = melee != null
            ? DiceSimulator.Roll(melee.diceCount * times, melee.diceSides) + melee.bonus
            : DiceSimulator.Roll(attacker.damageDiceCount * times, attacker.damageDiceSides) + attacker.damageBonus;
        damage = Mathf.Max(damage, 1);
        Debug.Log($"{head} — {(crit ? "CRITICAL HIT" : "hit")}! {damage} damage");
        mover.TakeDamage(damage);
        string down = mover.IsAlive ? "" : $"  —  {mover.unitName.ToUpper()} IS DOWN";
        yield return DiceTray.ShowOutcome((crit ? $"CRITICAL!  {damage} damage" : $"HIT  —  {damage} damage") + down, goodForParty);
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

    // Steps between 'f' and a footprint of 'size' with its bottom-left on 'anchor' (1 = next to each other)
    static int Gap(CombatUnit f, Vector2Int anchor, int size)
    {
        Vector2Int a = f.AnchorTile;
        int dx = Mathf.Max(0, anchor.x - (a.x + f.size - 1), a.x - (anchor.x + size - 1));
        int dy = Mathf.Max(0, anchor.y - (a.y + f.size - 1), a.y - (anchor.y + size - 1));
        return CombatUnit.Reach(dx, dy);
    }

    // Quick jab toward the one leaving, and back
    static IEnumerator Lunge(Transform who, Transform at)
    {
        SpriteAnimator anim = who.GetComponentInChildren<SpriteAnimator>();
        Vector3 home = who.position;
        Vector3 toward = at.position - home;
        toward.y = 0f;
        if (anim != null) anim.FaceToward(toward);
        Vector3 hitPos = home + toward.normalized * 0.35f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.1f) { who.position = Vector3.Lerp(home, hitPos, t); if (anim != null) anim.HoldFacing(0.1f); yield return null; }
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f) { who.position = Vector3.Lerp(hitPos, home, t); if (anim != null) anim.HoldFacing(0.1f); yield return null; }
        who.position = home;
    }
}
