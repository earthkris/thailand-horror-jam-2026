using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Melee: only hits right next to it. Ranged: only shoots from a distance.
// Both: attacks in melee when it can reach you this turn, otherwise shoots (a spider spitting web).
public enum AttackStyle { Melee, Ranged, Both }

// A monster's brain in combat. On its turn it walks (up to its speed), then attacks if it can.
//   Melee:  goes for the nearest party member and hits them when standing right next to them.
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

        CombatUnit target = ClosestPartyMember(MyTile());
        if (target == null) yield break;   // nobody left to fight

        // 1. Walk
        List<Vector2Int> path =
            style == AttackStyle.Melee ? PlanMelee(target) :
            style == AttackStyle.Ranged ? PlanRanged(target) :
            PlanBoth(target);
        foreach (Vector2Int tile in path)
            yield return Step(tile);

        // 2. Attack whoever we can reach now (melee first, then a shot)
        CombatUnit victim = TargetInReach(out bool melee);
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
            // Standing next to them beats everything; otherwise get as close as possible
            int score = (gap <= 1 ? 0 : gap * 1000) + pair.Value;
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

            int score = Mathf.Abs(gap - preferredDistance) * 10 + pair.Value;
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

    // Spread out from 'start' over every tile we could stand on (walls, rocks and units block)
    void Flood(Vector2Int start, out Dictionary<Vector2Int, int> dist, out Dictionary<Vector2Int, Vector2Int> cameFrom)
    {
        dist = new Dictionary<Vector2Int, int> { [start] = 0 };
        cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int t = queue.Dequeue();
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int n = t + d;
                if (dist.ContainsKey(n) || !CanStand(n)) continue;
                dist[n] = dist[t] + 1;
                cameFrom[n] = t;
                queue.Enqueue(n);
            }
        }
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

    // Who can we hit from here? 'melee' says how.
    CombatUnit TargetInReach(out bool melee)
    {
        melee = false;
        Vector2Int me = MyTile();

        // Melee first: anyone right next to us
        if (style != AttackStyle.Ranged)
        {
            CombatUnit near = NearestWhere(p => Gap(me, p) <= 1);
            if (near != null) { melee = true; return near; }
            if (style == AttackStyle.Melee) return null;
        }

        // Then a shot: in range, nothing in the way
        return NearestWhere(p => Gap(me, p) <= attackRange && ClearShot(me, p));
    }

    CombatUnit NearestWhere(System.Func<CombatUnit, bool> ok)
    {
        CombatUnit best = null;
        int bestGap = int.MaxValue;
        Vector2Int me = MyTile();
        foreach (CombatUnit p in combat.Party)
        {
            if (p == null || !p.IsAlive || !ok(p)) continue;
            int gap = Gap(me, p);
            if (gap < bestGap) { bestGap = gap; best = p; }
        }
        return best;
    }

    IEnumerator Attack(CombatUnit target, bool melee)
    {
        // D&D: shooting with an enemy right next to you = disadvantage (roll twice, keep the lower)
        bool disadvantage = !melee && NearestPartyGap(MyTile()) <= 1;
        string verb = melee ? meleeVerb : rangedVerb;

        // Rolled on the real dice (DiceTray), or DiceSimulator if there's no dice in the scene
        int r1 = 0, r2 = 0;
        yield return DiceTray.RollD20($"{Name()} {verb} {target.unitName}" + (disadvantage ? "   (disadvantage)" : ""), v => r1 = v);
        r2 = r1;
        if (disadvantage)
            yield return DiceTray.RollD20("Disadvantage  —  second roll", v => r2 = v);
        int roll = Mathf.Min(r1, r2);

        // Hit or miss? (worked out first, so a missed shot can fly wide)
        int total = roll + unit.attackBonus;
        bool crit = roll == 20;
        bool hit = crit || (roll != 1 && total >= target.ArmorClass);

        // Show it
        SpriteAnimator facer = GetComponentInChildren<SpriteAnimator>();   // look at who we attack
        if (facer != null) facer.FaceToward(target.transform.position - transform.position);
        if (melee) yield return Swing(target);
        else yield return Shoot(target, hit);

        string rollText = disadvantage ? $"d20 ({r1}, {r2} -> {roll}, disadvantage)" : $"d20 ({roll})";
        string attack = $"{Name()} {verb} {target.unitName}: {rollText} + {unit.attackBonus} = {total} vs AC {target.ArmorClass}";

        if (!hit)
        {
            Debug.Log($"{attack} — miss{(roll == 1 ? " (natural 1)" : "")}");
            yield return DiceTray.ShowOutcome(roll == 1 ? "NATURAL 1  —  MISS" : "MISS", true);   // good for you
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
                    if (other != unit && other.IsAlive && other.Occupies(world)) return false;
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
        return dx + dy;
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
