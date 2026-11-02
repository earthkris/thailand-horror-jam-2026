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
public class EnemyAI : MonoBehaviour
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

    [Header("Words (battle log and dice banner)")]
    public string meleeVerb = "attacks";
    public string rangedVerb = "shoots at";

    [Header("Animation")]
    [Tooltip("Tiles per second while walking.")]
    public float stepSpeed = 4f;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    CombatUnit unit;
    CombatManager combat;
    GridManager grid;
    TurnManager turns;

    CombatUnit chosen;     // who we're after this turn
    int turnsTaken;        // for openWithRanged
    Dictionary<Vector2Int, int> pathCost;   // from the last Flood: steps + opportunity attack detours

    void Awake() => unit = GetComponent<CombatUnit>();

    // Keeps the Inspector honest: melee can only hit right next to it
    void OnValidate()
    {
        if (style == AttackStyle.Melee) attackRange = 1;
    }

    // ================= The turn =================

    public IEnumerator TakeTurn(CombatManager battle)
    {
        combat = battle;
        grid = battle.grid;
        turns = battle.GetComponent<TurnManager>();

        CombatUnit target = ChooseTarget();
        if (target == null) yield break;   // nobody left to fight

        // Both: shoot this turn instead of going in for the bite?
        bool shoot = style == AttackStyle.Both &&
                     ((openWithRanged && turnsTaken == 0) || Random.value < preferRangedChance);
        turnsTaken++;

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
            yield return Attack(victim, melee);
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

            foreach (Vector2Int d in Dirs)
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
    bool Provokes(Vector2Int from, Vector2Int to)
    {
        foreach (CombatUnit p in combat.Party)
        {
            if (p == null || !p.IsAlive || p.Has(StatusType.Paralyse)) continue;
            if (turns != null && !turns.HasReaction(p)) continue;
            if (Gap(from, p) <= 1 && Gap(to, p) > 1) return true;
        }
        return false;
    }

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

    IEnumerator Attack(CombatUnit target, bool melee)
    {
        // D&D: shooting with an enemy right next to you = disadvantage (roll twice, keep the lower).
        // Attacking someone who is down = advantage (roll twice, keep the higher). Both cancel out.
        bool downed = target.IsDowned;
        bool crowded = !melee && NearestPartyGap(MyTile()) <= 1;
        bool disadvantage = crowded && !downed;
        bool advantage = downed && !crowded;
        string verb = melee ? meleeVerb : rangedVerb;

        // Rolled on the real dice (DiceTray), or DiceSimulator if there's no dice in the scene
        int r1 = 0, r2 = 0;
        yield return DiceTray.RollD20($"{Name()} {verb} {target.unitName}" + (disadvantage ? "   (disadvantage)" : advantage ? "   (advantage)" : ""), v => r1 = v);
        r2 = r1;
        if (disadvantage)
            yield return DiceTray.RollD20("Disadvantage  —  second roll", v => r2 = v);
        if (advantage)
            yield return DiceTray.RollD20($"Advantage: {target.unitName} is down  —  second roll", v => r2 = v);
        int roll = advantage ? Mathf.Max(r1, r2) : Mathf.Min(r1, r2);

        // Hit or miss? (worked out first, so a missed shot can fly wide)
        int total = roll + unit.attackBonus;
        bool crit = roll == 20;
        bool hit = crit || (roll != 1 && total >= target.ArmorClass);

        // Show it
        SpriteAnimator facer = GetComponentInChildren<SpriteAnimator>();   // look at who we attack
        if (facer != null) facer.FaceToward(target.transform.position - transform.position);
        if (melee) yield return Swing(target);
        else yield return Shoot(target, hit);

        string rollText = disadvantage ? $"d20 ({r1}, {r2} -> {roll}, disadvantage)"
                        : advantage ? $"d20 ({r1}, {r2} -> {roll}, advantage)" : $"d20 ({roll})";
        string attack = $"{Name()} {verb} {target.unitName}: {rollText} + {unit.attackBonus} = {total} vs AC {target.ArmorClass}";

        if (!hit)
        {
            Debug.Log($"{attack} — miss{(roll == 1 ? " (natural 1)" : "")}");
            yield return DiceTray.ShowOutcome(roll == 1 ? "NATURAL 1  —  MISS" : "MISS", true);   // good for you
            yield break;
        }

        // Someone who is down: a hit costs a death save, a bite from right next to them costs two
        if (downed)
        {
            target.TakeDamage(1);
            if ((melee || crit) && !target.IsDead) target.AddDeathSaveFailures(1);
            Debug.Log($"{attack} — hit! {target.unitName}: {target.DeathSaveText}");
            string result = target.IsDead ? $"{target.unitName.ToUpper()} IS DEAD" : $"{target.unitName}: {target.DeathSaveFailures}/3 DEATH SAVES FAILED";
            yield return DiceTray.ShowOutcome("HIT  —  " + result, false);
            turns.NotifyChanged();
            yield break;
        }

        // A critical hit rolls the damage dice twice
        int dice = DiceSimulator.Roll(unit.damageDiceCount * (crit ? 2 : 1), unit.damageDiceSides);
        int damage = Mathf.Max(dice + unit.damageBonus, 1);
        Debug.Log($"{attack} — {(crit ? "CRITICAL HIT" : "hit")}! {damage} damage");
        target.TakeDamage(damage);
        string down = target.IsAlive ? "" : $"  —  {target.unitName.ToUpper()} IS DOWN";
        yield return DiceTray.ShowOutcome((crit ? $"CRITICAL!  {damage} damage" : $"HIT  —  {damage} damage") + down, false);
    }

    // Play the attack frames (if any), lunge on the last one (the hit), then finish the swing
    IEnumerator Swing(CombatUnit target)
    {
        SpriteAnimator body = GetComponentInChildren<SpriteAnimator>();
        if (body == null || !body.HasAttackFrames)
        {
            yield return Lunge(target);
            yield break;
        }

        body.PlayAttack(target.transform.position);
        while (body.IsAttacking && !body.OnLastAttackFrame) yield return null;   // wind-up
        yield return Lunge(target);                                              // the hit
        while (body.IsAttacking) yield return null;                              // follow-through
    }

    // Quick jab toward the target and back. Facing is held so sliding back doesn't turn us around.
    IEnumerator Lunge(CombatUnit target)
    {
        SpriteAnimator body = GetComponentInChildren<SpriteAnimator>();
        Vector3 home = transform.position;
        Vector3 toward = target.transform.position - home;
        toward.y = 0f;
        Vector3 hitPos = home + toward.normalized * 0.35f * unit.tileSize;

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.1f)
        {
            transform.position = Vector3.Lerp(home, hitPos, t);
            if (body != null) body.HoldFacing(0.1f);
            yield return null;
        }
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
        {
            transform.position = Vector3.Lerp(hitPos, home, t);
            if (body != null) body.HoldFacing(0.1f);
            yield return null;
        }
        transform.position = home;
        if (body != null) body.HoldFacing(0.1f);
    }

    // Fly a projectile from us to the target. A miss flies wide and lands beside them.
    IEnumerator Shoot(CombatUnit target, bool hit)
    {
        Vector3 from = transform.position;
        Vector3 to = target.transform.position;
        Vector3 flat = to - from;
        flat.y = 0f;
        if (!hit && flat.sqrMagnitude > 0.0001f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, flat).normalized * (Random.value < 0.5f ? -0.7f : 0.7f);
            to += (side + flat.normalized * 0.4f) * unit.tileSize;
        }

        GameObject p = projectilePrefab != null ? Instantiate(projectilePrefab) : MakePlaceholderBall();
        p.transform.position = from;
        if (to != from) p.transform.rotation = Quaternion.LookRotation(to - from);   // point it at the target (for arrows)
        if (p.TryGetComponent(out SpriteFlipbook flipbook)) flipbook.travelDirection = to - from;   // sprites: turn on screen instead

        float time = Vector3.Distance(from, to) / Mathf.Max(projectileSpeed * unit.tileSize, 0.1f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
        {
            p.transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        Destroy(p);

        // On the target, or on the floor beside them after a miss
        if (impactPrefab != null)
            Instantiate(impactPrefab, hit ? to : to + Vector3.down * 0.6f * unit.tileSize, Quaternion.identity);
    }

    static GameObject MakePlaceholderBall()
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Projectile (placeholder)";
        Destroy(ball.GetComponent<Collider>());
        ball.transform.localScale = Vector3.one * 0.2f;
        return ball;
    }

    // ================= Moving =================

    IEnumerator Step(Vector2Int gridTile)
    {
        Vector3 target = unit.WorldPositionFor(grid.OriginTile + gridTile);
        while (transform.position != target)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, stepSpeed * unit.tileSize * Time.deltaTime);
            yield return null;
        }
    }

    // ================= Helpers =================

    Vector2Int MyTile() => unit.AnchorTile - grid.OriginTile;

    // Could our whole footprint stand with its bottom-left on this grid tile?
    bool CanStand(Vector2Int anchor)
    {
        for (int x = 0; x < unit.size; x++)
            for (int y = 0; y < unit.size; y++)
            {
                Vector2Int cell = anchor + new Vector2Int(x, y);
                if (!grid.IsWalkable(cell.x, cell.y)) return false;

                Vector2Int world = grid.OriginTile + cell;
                foreach (CombatUnit other in AllUnits())
                    if (other != unit && !other.IsDead && other.Occupies(world)) return false;
            }
        return true;
    }

    // Steps between our footprint (if standing at 'anchor') and theirs. 1 = right next to each other.
    int Gap(Vector2Int anchor, CombatUnit other)
    {
        Vector2Int a = grid.OriginTile + anchor;
        Vector2Int b = other.AnchorTile;
        int dx = Mathf.Max(0, b.x - (a.x + unit.size - 1), a.x - (b.x + other.size - 1));
        int dy = Mathf.Max(0, b.y - (a.y + unit.size - 1), a.y - (b.y + other.size - 1));
        return CombatUnit.Reach(dx, dy);
    }

    int NearestPartyGap(Vector2Int anchor)
    {
        int best = int.MaxValue;
        foreach (CombatUnit p in combat.Party)
            if (p != null && p.IsAlive) best = Mathf.Min(best, Gap(anchor, p));
        return best;
    }

    CombatUnit ClosestPartyMember(Vector2Int anchor)
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

    // Nothing on the Obstacles layer between us (standing at 'anchor') and the target
    bool ClearShot(Vector2Int anchor, CombatUnit target)
    {
        Vector3 from = unit.WorldPositionFor(grid.OriginTile + anchor);
        return !Physics.Linecast(from, target.transform.position, grid.obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    IEnumerable<CombatUnit> AllUnits()
    {
        foreach (CombatUnit u in combat.Party) yield return u;
        foreach (CombatUnit u in combat.Enemies) yield return u;
    }

    // What the log calls us (hidden monsters stay mysterious)
    string Name() => FogOfWar.CanSee(unit) ? unit.unitName : "Something in the dark";
}
