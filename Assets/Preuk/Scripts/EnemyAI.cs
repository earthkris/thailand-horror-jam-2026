using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Melee: only hits right next to it. Ranged: only shoots from a distance.
// Both: attacks in melee when it can reach you this turn, otherwise shoots (a spider spitting web).
public enum AttackStyle { Melee, Ranged, Both }

// A monster's brain in combat. On its turn it walks (up to its speed), then attacks if it can.
// Target: it weighs everyone up. Who can it reach this turn, how likely is it to hit (their AC),
//   could it drop them, who keeps healing the others? A downed hero in reach is a feast:
//   attacks on them have advantage and a bite costs them two death saves (D&D).
// It also knows about opportunity attacks: it walks around the party's reach when it can.
//   Melee:  goes for its target and hits them when standing right next to them.
//   Ranged: keeps its distance, finds a spot with a clear shot, and fires a projectile.
//   Both:   walks up and attacks in melee if it can get there this turn; if not, shoots instead.
// Melee plays the monster's attack frames if its SpriteAnimator has some, then lunges.
// Monsters always know where the party is, even in the dark.
// The hit roll and damage come from the CombatUnit's "Attack" stats (the same for melee and shots).
[RequireComponent(typeof(CombatUnit))]
public class EnemyAI : CombatBrain
{
    [Header("Fighting Style")]
    public AttackStyle style = AttackStyle.Melee;
    [Tooltip("How far it can shoot, in tiles. Melee is always 1.")]
    [Min(1)] public int attackRange = 1;
    [Tooltip("Ranged only: how far from the party it likes to stand.")]
    [Min(1)] public int preferredDistance = 4;
    [Tooltip("Both only: its first turn of a fight is a shot (good for a tutorial).")]
    public bool openWithRanged = false;
    [Tooltip("Both only: chance each turn to shoot even when it could walk up and hit in melee.")]
    [Range(0f, 1f)] public float preferRangedChance = 0f;

    [Header("Picking a target")]
    [Tooltip("How badly it wants to finish off a downed hero it can reach this turn. 0 = leaves them alone.")]
    [Min(0f)] public float downedPriority = 5f;
    [Tooltip("Extra wish to go for whoever can heal (Lucius).")]
    [Min(0f)] public float healerPriority = 1.5f;
    [Tooltip("A bit of luck in the choice, so it isn't fully predictable. 0 = always the 'best' target.")]
    [Min(0f)] public float randomness = 1.5f;

    [Header("Cunning")]
    [Tooltip("How many extra tiles it will walk to avoid one opportunity attack.")]
    [Min(0)] public int opportunityAttackDetour = 6;

    [Header("Projectile (Ranged / Both)")]
    [Tooltip("What flies at the target. Leave empty for a plain ball.")]
    public GameObject projectilePrefab;
    [Tooltip("Spawned where the shot lands, e.g. a web splat. Optional.")]
    public GameObject impactPrefab;
    [Tooltip("Tiles per second.")]
    public float projectileSpeed = 10f;

    [Header("On every hit (optional)")]
    [Tooltip("Burn = acid or venom eating in. Paralyse = webbed, loses its turns.")]
    public StatusType inflicts = StatusType.None;
    [Min(1)] public int statusTurns = 2;
    [Range(0, 100)] public int statusChance = 35;
    [Min(1)] public int statusDiceCount = 1;
    [Min(1)] public int statusDiceSides = 4;

    [Tooltip("Drinks blood: heals itself by this much of the damage it deals, in percent (a bat).")]
    [Range(0, 100)] public int drainPercent = 0;

    [Header("Special move (optional)")]
    [Tooltip("Empty = none. Its 2nd turn, then every 'Special Every' turns, the attack is this instead.")]
    public string specialName = "";
    [Min(2)] public int specialEvery = 3;
    [Min(1)] public int specialDiceCount = 2;
    [Min(1)] public int specialDiceSides = 6;
    public int specialBonus = 2;
    public StatusType specialInflicts = StatusType.None;
    [Min(1)] public int specialStatusTurns = 1;
    [Range(0, 100)] public int specialStatusChance = 100;
    [Min(1)] public int specialStatusDiceCount = 1;
    [Min(1)] public int specialStatusDiceSides = 6;
    [Tooltip("Party members this many tiles from the target are caught too (half damage). 0 = only the target.")]
    [Min(0)] public int specialSplash = 0;

    [Header("Look (MoveFx, optional)")]
    [Tooltip("Drawn where a melee attack lands: Bite, Claw, Venom Bite, Drain, Slam, Bone...")]
    public MoveFxStyle meleeFx = MoveFxStyle.None;
    [Tooltip("Drawn where a shot lands. Wail = rings of sound instead of a projectile.")]
    public MoveFxStyle rangedFx = MoveFxStyle.None;
    [Tooltip("The special move's look. None = the melee or ranged one.")]
    public MoveFxStyle specialFx = MoveFxStyle.None;

    [Header("Words (battle log and dice banner)")]
    public string meleeVerb = "attacks";
    public string rangedVerb = "shoots at";

    [Header("Animation")]
    [Tooltip("Tiles per second while walking.")]
    public float stepSpeed = 4f;

    CombatUnit chosen;     // who we're after this turn
    int turnsTaken;        // for openWithRanged
    Dictionary<Vector2Int, int> pathCost;   // from the last Flood: steps + opportunity attack detours

    // Keeps the Inspector honest: melee can only hit right next to it
    void OnValidate()
    {
        if (style == AttackStyle.Melee) attackRange = 1;
    }

    // ================= The turn =================

    protected override IEnumerator Act()
    {
        CombatUnit target = ChooseTarget();
        if (target == null) yield break;   // nobody left to fight

        // Both: shoot this turn instead of going in for the bite?
        bool shoot = style == AttackStyle.Both &&
                     ((openWithRanged && turnsTaken == 0) || Random.value < preferRangedChance);
        turnsTaken++;
        bool special = !string.IsNullOrEmpty(specialName) && turnsTaken >= 2 && (turnsTaken - 2) % specialEvery == 0;

        // 1. Walk
        List<Vector2Int> path =
            style == AttackStyle.Melee ? PlanMelee(target) :
            style == AttackStyle.Ranged || shoot ? PlanRanged(target) :
            PlanBoth(target);
        foreach (Vector2Int tile in path)
        {
            // Walking out of a party member's reach: they get a free swing first
            foreach (CombatUnit p in OpportunityAttack.Provokers(unit, grid.OriginTile + tile, combat, turns))
                yield return OpportunityAttack.Resolve(p, unit, turns);
            if (!unit.IsAlive)
            {
                turns.CheckForVictory();
                yield break;
            }
            yield return Step(tile);
        }

        // 2. Attack whoever we can reach now (melee first, then a shot)
        CombatUnit victim = TargetInReach(out bool melee, shoot);
        if (victim != null)
            yield return Attack(victim, melee, special);
        else
            Debug.Log($"{Name()} can't reach anyone this turn.");
    }

    // ================= Planning the walk =================

    // Melee: the shortest way to a tile right next to the target
    List<Vector2Int> PlanMelee(CombatUnit target)
    {
        Flood(MyTile(), out var dist, out var cameFrom);

        Vector2Int best = MyTile();
        int bestScore = int.MaxValue;
        foreach (var pair in dist)
        {
            int gap = Gap(pair.Key, target);
            // Standing next to them beats everything; otherwise get as close as possible.
            // Getting there this turn beats a longer (safer) way round.
            int score = (gap <= 1 ? 0 : gap * 1000) + (pair.Value > unit.MoveTiles ? 500 : 0) + pathCost[pair.Key];
            if (score < bestScore) { bestScore = score; best = pair.Key; }
        }
        return Trim(PathTo(best, cameFrom), unit.MoveTiles);
    }

    // Ranged: a tile we can reach THIS turn, with a clear shot, near our favourite distance,
    // and not right next to a party member (that would make shooting harder)
    List<Vector2Int> PlanRanged(CombatUnit target)
    {
        Flood(MyTile(), out var dist, out var cameFrom);

        Vector2Int best = MyTile();
        int bestScore = int.MaxValue;
        bool found = false;
        foreach (var pair in dist)
        {
            if (pair.Value > unit.MoveTiles) continue;            // too far to walk this turn
            int gap = Gap(pair.Key, target);
            if (gap > attackRange || !ClearShot(pair.Key, target)) continue;

            int score = Mathf.Abs(gap - preferredDistance) * 10 + pathCost[pair.Key];
            if (NearestPartyGap(pair.Key) <= 1) score += 1000;   // don't shoot point-blank
            if (score < bestScore) { bestScore = score; best = pair.Key; found = true; }
        }
        if (found) return PathTo(best, cameFrom);

        // No good spot this turn: walk toward the target like a melee monster
        return PlanMelee(target);
    }

    // Both: if we can get next to the target this turn, go and hit it. Otherwise find a spot to shoot from.
    List<Vector2Int> PlanBoth(CombatUnit target)
    {
        List<Vector2Int> melee = PlanMelee(target);
        Vector2Int end = melee.Count > 0 ? melee[melee.Count - 1] : MyTile();
        if (Gap(end, target) <= 1) return melee;
        return PlanRanged(target);
    }

    // Spread out from 'start' over every tile we could stand on (walls, rocks and units block).
    // The cheapest way to each tile, where a step that provokes an opportunity attack counts as
    // opportunityAttackDetour extra steps. 'dist' = steps along that way, pathCost = its cost.
    void Flood(Vector2Int start, out Dictionary<Vector2Int, int> dist, out Dictionary<Vector2Int, Vector2Int> cameFrom)
    {
        dist = new Dictionary<Vector2Int, int> { [start] = 0 };
        cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        pathCost = new Dictionary<Vector2Int, int> { [start] = 0 };
        var open = new List<Vector2Int> { start };
        var done = new HashSet<Vector2Int>();

        while (open.Count > 0)
        {
            int pick = 0;   // the cheapest open tile (grids are small, a list is fine)
            for (int i = 1; i < open.Count; i++)
                if (pathCost[open[i]] < pathCost[open[pick]]) pick = i;
            Vector2Int t = open[pick];
            open.RemoveAt(pick);
            if (!done.Add(t)) continue;

            foreach (Vector2Int d in TileMath.Directions)
            {
                Vector2Int n = t + d;
                if (done.Contains(n) || !CanStand(n)) continue;
                int cost = pathCost[t] + 1 + (Provokes(t, n) ? opportunityAttackDetour : 0);
                if (pathCost.TryGetValue(n, out int old) && old <= cost) continue;
                pathCost[n] = cost;
                dist[n] = dist[t] + 1;
                cameFrom[n] = t;
                open.Add(n);
            }
        }
    }

    // Would stepping from 'from' to 'to' leave a party member's reach while they still have their reaction?
    bool Provokes(Vector2Int from, Vector2Int to) =>
        OpportunityAttack.Provokes(unit, grid.OriginTile + from, grid.OriginTile + to, combat, turns);

    static List<Vector2Int> PathTo(Vector2Int goal, Dictionary<Vector2Int, Vector2Int> cameFrom)
    {
        var path = new List<Vector2Int>();
        Vector2Int t = goal;
        while (cameFrom.ContainsKey(t))
        {
            path.Add(t);
            t = cameFrom[t];
        }
        path.Reverse();   // start -> goal, without the tile we're on
        return path;
    }

    static List<Vector2Int> Trim(List<Vector2Int> path, int maxSteps)
    {
        if (path.Count > maxSteps) path.RemoveRange(maxSteps, path.Count - maxSteps);
        return path;
    }

    // ================= Attacking =================

    // Who can we hit from here? 'melee' says how. 'preferShot' = try a shot before melee.
    CombatUnit TargetInReach(out bool melee, bool preferShot = false)
    {
        melee = false;
        Vector2Int me = MyTile();

        if (preferShot)
        {
            CombatUnit shot = PickWhere(p => Gap(me, p) <= attackRange && ClearShot(me, p));
            if (shot != null) return shot;   // no clear shot? fall back to a bite below
        }

        // Melee first: anyone right next to us
        if (style != AttackStyle.Ranged)
        {
            CombatUnit near = PickWhere(p => Gap(me, p) <= 1);
            if (near != null) { melee = true; return near; }
            if (style == AttackStyle.Melee) return null;
        }

        // Then a shot: in range, nothing in the way
        return PickWhere(p => Gap(me, p) <= attackRange && ClearShot(me, p));
    }

    // Of the party members we could hit: the one we came for, else the one we'd most like to hit.
    // A downed hero only if we came for them, or nobody standing is in reach.
    CombatUnit PickWhere(System.Func<CombatUnit, bool> ok)
    {
        if (chosen != null && (chosen.IsAlive || chosen.IsDowned) && ok(chosen)) return chosen;

        CombatUnit best = null;
        float bestScore = float.MinValue;
        foreach (CombatUnit p in combat.Party)
        {
            if (p == null || !ok(p)) continue;
            if (!p.IsAlive && !(p.IsDowned && downedPriority > 0f)) continue;
            float score = Worth(p, true, 0) - (p.IsAlive ? 0f : 100f);   // standing ones first here
            if (score > bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    // ================= Picking a target =================

    // Everyone gets a score (Worth); the best one is who we go after this turn
    CombatUnit ChooseTarget()
    {
        chosen = null;
        Flood(MyTile(), out var dist, out _);

        float bestScore = float.MinValue;
        foreach (CombatUnit p in combat.Party)
        {
            if (p == null || p.IsDead || (!p.IsAlive && !p.IsDowned)) continue;
            bool reach = CanReachThisTurn(p, dist, out int cost);
            if (p.IsDowned && (!reach || downedPriority <= 0f)) continue;   // not worth a long walk
            float score = Worth(p, reach, cost) + Random.Range(0f, randomness);
            if (score > bestScore) { bestScore = score; chosen = p; }
        }

        // Nobody worth it? Just go for the closest one standing
        if (chosen == null) chosen = ClosestPartyMember(MyTile());
        else if (chosen.IsDowned) Debug.Log($"{Name()} goes to finish off {chosen.unitName}.");
        return chosen;
    }

    // How much we want to attack 'p'. Roughly: the damage we expect to do, plus reasons to hurry.
    float Worth(CombatUnit p, bool reach, int cost)
    {
        if (p.IsDowned) return downedPriority + 4f * p.DeathSaveFailures;

        float average = unit.damageDiceCount * (unit.damageDiceSides + 1) * 0.5f + unit.damageBonus;
        float score = HitChance(p) * average;
        if (p.CurrentHP <= average) score += 4f;            // could drop them this turn
        score += (1f - HealthFraction(p)) * 3f;             // already hurt
        if (CanHeal(p)) score += healerPriority;            // take out the healer
        if (!reach) score -= 3f;                            // can't hit them this turn anyway
        score -= cost * 0.25f;                              // far away, or past someone's reach
        return score;
    }

    float HitChance(CombatUnit p)
    {
        int need = p.ArmorClass - unit.attackBonus;          // d20 roll needed
        return Mathf.Clamp((21 - need) / 20f, 0.05f, 0.95f);
    }

    static bool CanHeal(CombatUnit p) => p.actions.Exists(a => a != null && a.effect == ActionEffect.Heal);

    // Is there a tile we can walk to this turn and attack 'p' from? 'cost' = the cheapest way there.
    bool CanReachThisTurn(CombatUnit p, Dictionary<Vector2Int, int> dist, out int cost)
    {
        cost = int.MaxValue;
        int closest = int.MaxValue;
        foreach (var pair in dist)
        {
            int gap = Gap(pair.Key, p);
            closest = Mathf.Min(closest, pathCost[pair.Key] + gap);
            if (pair.Value > unit.MoveTiles) continue;
            bool ok = (style != AttackStyle.Ranged && gap <= 1) ||
                      (style != AttackStyle.Melee && gap <= attackRange && ClearShot(pair.Key, p));
            if (ok) cost = Mathf.Min(cost, pathCost[pair.Key]);
        }
        if (cost != int.MaxValue) return true;
        cost = closest == int.MaxValue ? 30 : closest;   // can't get there: how far it is instead
        return false;
    }

    static float HealthFraction(CombatUnit u) => u.maxHP > 0 ? (float)u.CurrentHP / u.maxHP : 1f;

    IEnumerator Attack(CombatUnit target, bool melee, bool special = false)
    {
        // D&D: shooting with an enemy right next to you = disadvantage (roll twice, keep the lower).
        // Attacking someone who is down = advantage (roll twice, keep the higher). Both cancel out.
        bool downed = target.IsDowned;
        bool crowded = !melee && NearestPartyGap(MyTile()) <= 1;
        bool favoured = downed, hampered = crowded;
        var reasons = new List<string>();
        if (downed) reasons.Add($"{target.unitName} is down");
        StatusRules.Edge(unit, target, melee, ref favoured, ref hampered, reasons);   // poisoned, blinded, restrained...
        RollMode mode = StatusRules.Mode(favoured, hampered);
        string verb = special ? "uses " + specialName + " on" : melee ? meleeVerb : rangedVerb;
        MoveFxStyle fx = special && specialFx != MoveFxStyle.None ? specialFx : melee ? meleeFx : rangedFx;
        if (special) Sfx.Play("roar", 0.55f, 1.6f);
        if (fx != MoveFxStyle.None)
        {
            float cast = MoveFx.Cast(fx, unit, target);
            if (cast > 0f) yield return new WaitForSeconds(cast);
        }

        // Rolled on the real dice (DiceTray), or DiceSimulator if there's no dice in the scene
        var roll = new AttackRoll();
        yield return roll.Throw(
            $"{Name()} {verb} {target.unitName}" + (mode == RollMode.Disadvantage ? "   (disadvantage)" : mode == RollMode.Advantage ? "   (advantage)" : ""),
            mode,
            mode == RollMode.Disadvantage ? "Disadvantage  —  second roll" : $"Advantage: {string.Join(", ", reasons)}  —  second roll");

        // Hit or miss? (worked out first, so a missed shot can fly wide)
        int total = roll.Value + unit.attackBonus - StatusRules.BaneRoll(unit);
        bool hit = roll.Hits(total, target.ArmorClass);

        // Show it
        SpriteAnimator facer = GetComponentInChildren<SpriteAnimator>();   // look at who we attack
        if (facer != null) facer.FaceToward(target.transform.position - transform.position);
        if (melee)
        {
            Sfx.Play("bite", 0.7f, unit.size > 1 ? 0.7f : 1f);
            yield return CombatAnimation.Swing(unit, target);
            if (fx != MoveFxStyle.None) MoveFx.Impact(fx, unit, target, MoveFx.Body(target), hit);
        }
        else { Sfx.Play(fx == MoveFxStyle.Wail ? "roar" : "spit", 0.8f, fx == MoveFxStyle.Wail ? 1.8f : 1f); yield return Shoot(target, hit, fx); }

        string attack = $"{Name()} {verb} {target.unitName}: {roll.Text} + {unit.attackBonus} = {total} vs AC {target.ArmorClass}";

        if (!hit)
        {
            Debug.Log($"{attack} — miss{(roll.Value == 1 ? " (natural 1)" : "")}");
            yield return DiceTray.ShowOutcome(roll.MissText, true);   // good for you
            yield break;
        }

        // Someone who is down: a hit costs a death save, a bite from right next to them costs two
        if (downed)
        {
            target.TakeDamage(1);
            if ((melee || roll.IsCrit) && !target.IsDead) target.AddDeathSaveFailures(1);
            Debug.Log($"{attack} — hit! {target.unitName}: {target.DeathSaveText}");
            string result = target.IsDead ? $"{target.unitName.ToUpper()} IS DEAD" : $"{target.unitName}: {target.DeathSaveFailures}/3 DEATH SAVES FAILED";
            yield return DiceTray.ShowOutcome("HIT  —  " + result, false);
            turns.NotifyChanged();
            yield break;
        }

        // A critical hit rolls the damage dice twice
        int count = special ? specialDiceCount : unit.damageDiceCount;
        int sides = special ? specialDiceSides : unit.damageDiceSides;
        int bonus = special ? specialBonus : unit.damageBonus;
        int dice = DiceSimulator.Roll(count * roll.DiceTimes, sides);
        int damage = Mathf.Max(dice + bonus, 1);
        Debug.Log($"{attack} — {(roll.IsCrit ? "CRITICAL HIT" : "hit")}! {damage} damage");
        target.TakeDamage(damage);
        string extra = Afflict(target, special);
        if (drainPercent > 0 && unit.CurrentHP < unit.maxHP)
        {
            int before = unit.CurrentHP;
            unit.Heal(Mathf.Max(damage * drainPercent / 100, 1));
            extra += $"   (drinks {unit.CurrentHP - before} HP)";
        }

        // A special with splash also catches whoever stands next to the target
        if (special && specialSplash > 0)
            foreach (CombatUnit other in combat.Party)
            {
                if (other == null || other == target || !other.IsAlive || other.ReachTo(target) > specialSplash) continue;
                if (!roll.Hits(total, other.ArmorClass)) continue;
                int d = Mathf.Max(damage / 2, 1);
                other.TakeDamage(d);
                extra += $"   +{other.unitName} {d}" + Afflict(other, true);
            }

        string down = target.IsAlive ? "" : $"  —  {target.unitName.ToUpper()} IS DOWN";
        yield return DiceTray.ShowOutcome(roll.HitText(damage) + down + extra, false);
        turns.NotifyChanged();
    }

    // Venom, acid, webs: a status on whoever got hit. Returns a few words for the banner.
    string Afflict(CombatUnit who, bool special)
    {
        StatusType s = special ? specialInflicts : inflicts;
        bool took = who != null && who.TryAddStatus(s, special ? specialStatusTurns : statusTurns, special ? specialStatusChance : statusChance,
                                                     special ? specialStatusDiceCount : statusDiceCount, special ? specialStatusDiceSides : statusDiceSides);
        if (!took) return "";
        Sfx.Play(s == StatusType.Paralyse || s == StatusType.Restrained ? "paralyse" : "burn", 0.7f);
        return $"  ({StatusInfo.Word(s)})";
    }

    // Fly a projectile from us to the target (CombatAnimation). A miss flies wide and lands beside them.
    IEnumerator Shoot(CombatUnit target, bool hit, MoveFxStyle fx = MoveFxStyle.None)
    {
        Vector3 from = transform.position;
        Vector3 to = CombatAnimation.ShotEnd(from, target.transform.position, hit, unit.tileSize);
        if (MoveFx.OwnsTravel(fx)) yield return MoveFx.Travel(fx, unit, target, to, hit, projectilePrefab, projectileSpeed * unit.tileSize);
        else yield return CombatAnimation.Fly(projectilePrefab, from, to, projectileSpeed * unit.tileSize);
        if (fx != MoveFxStyle.None) MoveFx.Impact(fx, unit, target, hit ? MoveFx.Body(target) : to, hit);

        // On the target, or on the floor beside them after a miss
        if (impactPrefab != null)
            Instantiate(impactPrefab, hit ? to : to + Vector3.down * 0.6f * unit.tileSize, Quaternion.identity);
    }

    // ================= Moving =================

    IEnumerator Step(Vector2Int gridTile)
    {
        Vector3 target = unit.WorldPositionFor(grid.OriginTile + gridTile);
        Sfx.Step(transform, 0.25f);
        while (transform.position != target)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, stepSpeed * unit.tileSize * Time.deltaTime);
            yield return null;
        }
    }

    // ================= Helpers (MyTile, CanStand, Gap, ClosestPartyMember and Name are in CombatBrain) =================

    int NearestPartyGap(Vector2Int anchor)
    {
        int best = int.MaxValue;
        foreach (CombatUnit p in combat.Party)
            if (p != null && p.IsAlive) best = Mathf.Min(best, Gap(anchor, p));
        return best;
    }

    // Nothing on the Obstacles layer between us (standing at 'anchor') and the target
    bool ClearShot(Vector2Int anchor, CombatUnit target)
    {
        Vector3 from = unit.WorldPositionFor(grid.OriginTile + anchor);
        return !Physics.Linecast(from, target.transform.position, grid.obstacleLayers, QueryTriggerInteraction.Ignore);
    }
}
