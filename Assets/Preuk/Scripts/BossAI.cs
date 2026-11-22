using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The first boss's brain. It never walks: it moves by stomping (jumping onto a new spot).
//   Stomp:  squats, jumps up to 'Jump Range' tiles (stretching up), lands squashed with a shockwave
//           and hits everyone close around where it lands.
//   Tongue: stays put and lashes a fan of tongues in a triangle in front (attack frames 15-57).
// Both hit an area. Everyone inside rolls a DEX save (+ Bless): pass = half damage.
// Each turn it does whichever hits more people; if nobody is in reach it jumps toward the party.
// Forced death (first run only): it can't drop below 1 HP, it goes for Lucius first,
// and Kairos can't fall while Lucius stands. Then it finishes Kairos and time loops.
// The tiles about to be hit glow red for a moment first.
// TurnManager calls TakeTurn (like EnemyAI). Put it on the boss root, next to CombatUnit.
[RequireComponent(typeof(CombatUnit))]
public class BossAI : CombatBrain
{
    [Header("Body")]
    [Tooltip("The sprite child. It squashes, stretches and jumps; its pivot should be at its feet.")]
    public Transform body;

    [Header("Animation (Sprite-0011)")]
    [Tooltip("Idle loop: frames 0-14 then 58-63.")]
    public Sprite[] idleFrames;
    [Tooltip("The tongue attack: frames 15-57.")]
    public Sprite[] attackFrames;
    public float idleFramesPerSecond = 10f;
    public float attackFramesPerSecond = 12f;
    [Tooltip("Attack frame (0 = Frame_15) where the tongues are all the way out. The tongue fan and the damage land here.")]
    public int tongueOutFrame = 18;
    [Tooltip("On = the sprite is drawn facing left (it gets mirrored to face right).")]
    public bool spriteFacesLeft = true;

    [Header("Tongue (triangle in front)")]
    [Tooltip("The tongue pieces from tongue.PNG (tip pointing left, pivot on the right end).")]
    public Sprite[] tongueSprites;
    public string tongueName = "Tongue Lash";
    [Tooltip("How far the triangle reaches, in tiles. It is 1 tile wide at the mouth and 2 wider every tile.")]
    [Min(1)] public int tongueRange = 4;
    [Tooltip("How many tongues fan out.")]
    [Range(3, 11)] public int tongueCount = 7;
    [Tooltip("Half the fan's angle, in degrees.")]
    public float tongueSpread = 40f;
    public int tongueDiceCount = 2, tongueDiceSides = 6, tongueBonus = 2;

    [Header("Stomp (its only way to move)")]
    [Tooltip("Shockwave frames from stomp.PNG, played on landing.")]
    public Sprite[] stompFrames;
    public string stompName = "Stomp";
    [Tooltip("Hits everyone this many tiles (or fewer) from its body.")]
    [Min(1)] public int stompRadius = 2;
    [Tooltip("How far it can jump in one turn, in tiles.")]
    [Min(0)] public int jumpRange = 5;
    public int stompDiceCount = 2, stompDiceSides = 8, stompBonus = 2;
    [Tooltip("How high it jumps, in tiles.")]
    public float jumpHeight = 3f;

    [Header("Unholy Hymn (buff, once per fight)")]
    public string hymnName = "Unholy Hymn";
    [Tooltip("Sings it the first time its HP drops to this share of max, or on a turn when nobody is in reach.")]
    [Range(0f, 1f)] public float hymnAtHealth = 0.6f;
    [Tooltip("Extra damage die on every hit for the rest of the fight. 4 = +1d4.")]
    [Min(0)] public int hymnDamageDie = 4;
    [Tooltip("Added to its save DC: harder to dodge.")]
    public int hymnSaveBonus = 2;
    public int hymnAC = 2;
    [Tooltip("Regenerates (count)d(sides) at the start of its turns, for this many turns. 0 = none.")]
    [Min(0)] public int hymnRegenTurns = 3;
    [Min(1)] public int hymnRegenDiceCount = 2, hymnRegenDiceSides = 4;

    [Header("Call the Brood (summon)")]
    [Tooltip("Spawned around the boss. Empty = it never summons.")]
    public GameObject minionPrefab;
    public string summonName = "Call the Brood";
    [Tooltip("Its 2nd turn, then every this many turns.")]
    [Min(2)] public int summonEvery = 3;
    [Min(1)] public int minionsPerCall = 2;
    [Tooltip("Never more than this many of its minions alive at once.")]
    [Min(1)] public int maxMinions = 4;
    [Tooltip("The minions' stats (they are weaker than a normal spider).")]
    public MonsterSpawner.Stats minion = new MonsterSpawner.Stats
    {
        name = "Brood Spawn", hp = 7, armorClass = 11, attackBonus = 3, diceSides = 4, damageBonus = 1,
        tint = new Color(0.75f, 0.55f, 0.85f), spriteScale = 0.7f
    };

    [Header("Rules")]
    [Tooltip("DEX save the party must reach to take half damage.")]
    public int saveDC = 13;
    [Tooltip("First run (no loops used yet): the party must lose. It goes for Lucius, Kairos can't fall before him, and it can't die.")]
    public bool forcedDeathOnFirstRun = true;

    [Header("Look")]
    [Tooltip("Unlit (SpellUnlit) so tongues and shockwave show in the dark. Empty = default sprite material.")]
    public Material effectMaterial;
    public Color warnColor = Palette.Blood;
    [Range(0f, 1f)] public float warnStrength = 0.55f;
    public float warnTime = 0.8f;

    bool lastWasStomp;
    int bossTurns;
    readonly List<CombatUnit> minions = new List<CombatUnit>();

    public bool ForcedFight => forcedDeathOnFirstRun && LoopManager.LoopsUsed == 0;

    SpriteRenderer bodySprite;
    float idleTime, attackTime;
    bool attacking;

    protected override void Awake()
    {
        base.Awake();
        bodySprite = body != null ? body.GetComponentInChildren<SpriteRenderer>(true) : GetComponentInChildren<SpriteRenderer>(true);
        // The frames are played from here, so the old Animator would only fight it
        Animator old = GetComponentInChildren<Animator>(true);
        if (old != null && idleFrames != null && idleFrames.Length > 0) old.enabled = false;
    }

    // ================= Animation =================

    void Update()
    {
        if (bodySprite == null) return;
        if (attacking && attackFrames != null && attackFrames.Length > 0)
        {
            attackTime += Time.deltaTime;
            int i = (int)(attackTime * attackFramesPerSecond);
            if (i < attackFrames.Length) { bodySprite.sprite = attackFrames[i]; return; }
            attacking = false;
            idleTime = 0f;
        }
        attacking = false;
        if (idleFrames == null || idleFrames.Length == 0) return;
        idleTime += Time.deltaTime;
        bodySprite.sprite = idleFrames[(int)(idleTime * idleFramesPerSecond) % idleFrames.Length];
    }

    // Seconds into the attack animation where the tongues are fully out
    float TongueOutTime => attackFrames != null && attackFrames.Length > 0 ? tongueOutFrame / Mathf.Max(attackFramesPerSecond, 1f) : 0f;

    // Turn left or right toward something (mirrors the body around its middle)
    void Face(Vector3 direction)
    {
        if (body == null || Mathf.Abs(direction.x) < 0.01f) return;
        bool right = direction.x > 0f;
        float sign = (right == spriteFacesLeft) ? -1f : 1f;
        Vector3 s = body.localScale;
        body.localScale = new Vector3(Mathf.Abs(s.x) * sign, s.y, s.z);
    }

    // ================= Boss intro =================

    // Hidden until the intro drops it in
    public void HideBody()
    {
        if (body != null) body.gameObject.SetActive(false);
    }

    // Falls out of the sky onto its spot, stretched, and lands with the stomp shockwave
    public IEnumerator DropIn(float height)
    {
        if (body == null) yield break;
        body.gameObject.SetActive(true);
        Vector3 home = body.localPosition;
        Vector3 size = body.localScale;
        yield return Tween.Over(0.5f, t =>
        {
            body.localPosition = home + Vector3.up * height * (1f - t * t);
            Scale(size, 0.78f, 1.35f);
        });
        yield return Land(home, size, null);
    }

    // Forced death: who can't fall yet. Called when the fight starts and after every hit.
    public void UpdateForcedRules()
    {
        if (unit == null) unit = GetComponent<CombatUnit>();
        bool forced = ForcedFight;
        unit.cannotDie = forced;
        CombatUnit kairos = FindParty(true), lucius = FindParty(false);
        if (kairos != null) kairos.cannotDie = forced && lucius != null && lucius.IsAlive;
    }

    // true = the one you play (Kairos), false = the friend (Lucius)
    static CombatUnit FindParty(bool player)
    {
        foreach (CombatUnit u in CombatUnit.OnTeam(Team.Party))
            if (u.IsPlayer == player) return u;
        return null;
    }

    // ================= The turn =================

    protected override IEnumerator Act()
    {
        UpdateForcedRules();
        bossTurns++;

        // Every few turns it calls its brood out of the dark instead of attacking
        if (ReadyToSummon())
        {
            yield return Summon();
            yield break;
        }

        CombatUnit focus = null;   // forced death: Lucius first
        if (ForcedFight)
        {
            CombatUnit lucius = FindParty(false);
            if (lucius != null && lucius.IsAlive) focus = lucius;
        }
        CombatUnit target = focus != null ? focus : ClosestPartyMember(MyTile());
        if (target == null) yield break;
        Face(target.transform.position - transform.position);

        // It never walks: either a tongue lash from here, or a stomp-jump to a new spot
        Vector2Int me = MyTile();
        Vector2Int dir = BestTongueDirection(me, focus, out List<CombatUnit> tongueHits);
        // The landing spot that hits the most people (or, if none, lands closest to the target)
        Vector2Int landing = BestLanding(me, out List<CombatUnit> stompHits, (spot, hits, steps) =>
        {
            int hitScore = Score(hits, focus);
            return hitScore > 0 ? hitScore * 100 - steps : -Gap(spot, target) * 10 - steps;
        });
        // Forced fight and Lucius is out of reach this turn: hitting Kairos alone gets nowhere
        // (he can't fall while Lucius stands), so leap toward Lucius instead:
        // the spot that ends closest to him (more people hit breaks a tie).
        if (focus != null && !tongueHits.Contains(focus) && !stompHits.Contains(focus))
        {
            landing = BestLanding(me, out stompHits, (spot, hits, steps) => -Gap(spot, focus) * 100 + hits.Count * 10 - steps);
            tongueHits.Clear();
        }
        int tongueScore = Score(tongueHits, focus), stompScore = Score(stompHits, focus);

        // Once a fight (the blessing lasts until it ends): when hurt, or when there's nobody to hit anyway
        bool hurt = unit.CurrentHP <= unit.maxHP * hymnAtHealth;
        if (!unit.IsBlessed && (hurt || (tongueScore == 0 && stompScore == 0)))
        {
            yield return Hymn();
            yield break;
        }

        bool tongue = tongueScore > 0 && (tongueScore > stompScore || (tongueScore == stompScore && lastWasStomp));
        lastWasStomp = !tongue;
        if (tongue) yield return Tongue(me, dir, tongueHits);
        else yield return StompTo(landing, stompHits);
    }

    // ================= Call the Brood =================

    bool ReadyToSummon()
    {
        if (minionPrefab == null || bossTurns < 2 || (bossTurns - 2) % summonEvery != 0) return false;
        minions.RemoveAll(m => m == null || !m.IsAlive);
        return minions.Count < maxMinions;
    }

    IEnumerator Summon()
    {
        Sfx.Play("roar", 1f, 0.8f);
        CameraController cam = CameraController.Main;
        if (cam != null) cam.Shake(0.15f, 0.6f);
        if (body != null)   // rears up
        {
            Vector3 size = body.localScale;
            yield return Tween.Over(0.3f, t => Scale(size, 1f - 0.1f * t, 1f + 0.2f * t));
            yield return Tween.Over(0.25f, t => Scale(size, 0.9f + 0.1f * t, 1.2f - 0.2f * t));
            body.localScale = size;
        }

        int made = 0;
        int count = Mathf.Min(minionsPerCall, maxMinions - minions.Count);
        Vector2Int me = MyTile();
        for (int i = 0; i < count; i++)
        {
            CombatUnit m = MonsterSpawner.Spawn(minionPrefab, minion, transform.position);
            if (m == null) break;
            // Around the boss, on the side nearest the party
            Vector2Int near = me + new Vector2Int(i % 2 == 0 ? -1 : unit.size, i < 2 ? 0 : unit.size);
            if (!combat.AddEnemy(m, near)) { Destroy(m.gameObject); break; }
            turns.Join(m);
            minions.Add(m);
            made++;
            StartCoroutine(MonsterSpawner.RiseFromFloor(m.transform));
            Sfx.Play("spit", 0.7f, 0.8f + 0.1f * i);
            yield return new WaitForSeconds(0.25f);
        }

        string text = made > 0 ? $"{made} {minion.name}{(made > 1 ? "s" : "")} crawl out of the dark" : "nothing answers";
        Debug.Log($"{Name()} uses {summonName}: {text}");
        yield return DiceTray.ShowMessage($"{Name()} uses {summonName}", text, false);
        turns.NotifyChanged();
    }

    // More people hit = better. In the forced fight, hitting Lucius beats everything.
    static int Score(List<CombatUnit> hits, CombatUnit focus) =>
        hits.Count * 10 + (focus != null && hits.Contains(focus) ? 100 : 0);

    // ================= Unholy Hymn =================

    // Swells up, the purple aura (BlessAura) blooms behind it, and it's stronger until the fight ends
    IEnumerator Hymn()
    {
        if (body != null)
        {
            Vector3 size = body.localScale;
            Sfx.Play("roar", 0.9f, 1.1f);
            yield return Tween.Over(0.35f, t => Scale(size, 1f + 0.15f * t, 1f + 0.1f * t));
            unit.AddBuff(hymnDamageDie, hymnAC, hymnSaveBonus);
            CameraController cam = CameraController.Main;
            if (cam != null) cam.Shake(0.25f, 0.25f);
            yield return Tween.Over(0.3f, t => Scale(size, 1.15f - 0.15f * t, 1.1f - 0.1f * t));
            body.localScale = size;
        }
        else unit.AddBuff(hymnDamageDie, hymnAC, hymnSaveBonus);

        if (hymnRegenTurns > 0) unit.AddStatus(StatusType.Regen, hymnRegenTurns, hymnRegenDiceCount, hymnRegenDiceSides);
        turns.NotifyChanged();

        string what = $"+1d{hymnDamageDie} damage   DC {SaveDC}   AC {unit.ArmorClass}"
                    + (hymnRegenTurns > 0 ? $"   regenerates {hymnRegenDiceCount}d{hymnRegenDiceSides} for {hymnRegenTurns} turns" : "");
        Debug.Log($"{unit.unitName} sings {hymnName}: {what}");
        yield return DiceTray.ShowMessage($"{Name()}: {hymnName.ToUpper()}", what, false);
    }

    // The save the party must reach (the hymn makes it harder)
    int SaveDC => saveDC + unit.BlessBonus;

    // ================= Tongue =================

    IEnumerator Tongue(Vector2Int me, Vector2Int dir, List<CombatUnit> victims)
    {
        List<Vector2Int> tiles = TongueTiles(me, dir);
        Sfx.Play("roar", 0.6f, 1.4f);
        yield return Warn(tiles);
        List<bool> saved = new List<bool>();
        yield return RollSaves(victims, tongueName, saved);

        Face(new Vector3(dir.x, 0f, dir.y));
        attacking = true;
        attackTime = 0f;
        while (attacking && attackTime < TongueOutTime - 0.3f) yield return null;   // the fan takes 0.3 s to come out

        bool hitNow = false;
        StartCoroutine(LashTongues(dir, () => hitNow = true));   // runs on us, so it always cleans up
        while (!hitNow) yield return null;
        grid.ClearMarks();
        yield return Damage(victims, saved, tongueName, tongueDiceCount, tongueDiceSides, tongueBonus);
        while (attacking) yield return null;   // the mouth closes
        turns.CheckForVictory();
    }

    // A fan of tongues lying on the floor, shooting out from the mouth, wiggling, then pulling back
    IEnumerator LashTongues(Vector2Int dir, System.Action onFullyOut)
    {
        float floor = FloorY();
        Vector3 forward = new Vector3(dir.x, 0f, dir.y);
        Vector3 mouth = transform.position + forward * (unit.size * 0.5f * unit.tileSize);
        mouth.y = floor + 0.09f;
        float baseAngle = Mathf.Atan2(forward.z, forward.x) * Mathf.Rad2Deg;
        float reach = (tongueRange + 0.5f) * unit.tileSize;

        int n = Mathf.Max(1, tongueCount);
        var tongues = new List<(Transform t, float angle, float length, float delay, float yScale)>();
        for (int i = 0; i < n; i++)
        {
            float k = n == 1 ? 0.5f : (float)i / (n - 1);
            float off = Mathf.Lerp(-tongueSpread, tongueSpread, k);
            Sprite s = tongueSprites != null && tongueSprites.Length > 0 ? tongueSprites[i % tongueSprites.Length] : null;
            var go = new GameObject("Tongue", typeof(SpriteRenderer));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = s;
            if (effectMaterial != null) sr.sharedMaterial = effectMaterial;
            go.transform.position = mouth + Vector3.up * (0.002f * i);
            float length = reach / Mathf.Max(Mathf.Cos(off * Mathf.Deg2Rad), 0.5f);
            float delay = Mathf.Abs(k - 0.5f) * 0.12f + Random.Range(0f, 0.04f);   // the middle ones first
            tongues.Add((go.transform, baseAngle + off, length, delay, Random.Range(1.7f, 2.3f)));
            go.transform.localScale = Vector3.zero;
        }

        const float extend = 0.18f, hold = 0.35f, retract = 0.22f;
        float total = 0.12f + extend + hold + retract;
        bool told = false;
        for (float t = 0f; t < total + 0.05f; t += Time.deltaTime)
        {
            foreach (var tg in tongues)
            {
                if (tg.t == null) continue;
                float lt = t - tg.delay;
                float out01 =
                    lt <= 0f ? 0f :
                    lt < extend ? 1f - Mathf.Pow(1f - lt / extend, 3f) :            // shoot out fast
                    lt < extend + hold ? 1f :
                    Mathf.Clamp01(1f - (lt - extend - hold) / retract);             // pull back
                float wiggle = lt > 0f ? Mathf.Sin(lt * 28f + tg.angle) * 5f * out01 : 0f;
                SpriteRenderer sr = tg.t.GetComponent<SpriteRenderer>();
                float spriteLength = sr.sprite != null ? sr.sprite.rect.width / sr.sprite.pixelsPerUnit : 1f;
                // Lying flat, its tip (the sprite's left) pointing along 'angle'
                tg.t.rotation = Quaternion.Euler(0f, 180f - (tg.angle + wiggle), 0f) * Quaternion.Euler(90f, 0f, 0f);
                tg.t.localScale = new Vector3(tg.length / spriteLength * out01, tg.yScale, 1f);
            }
            if (!told && t >= 0.12f + extend) { told = true; onFullyOut?.Invoke(); }
            yield return null;
        }
        if (!told) onFullyOut?.Invoke();
        foreach (var tg in tongues) if (tg.t != null) Destroy(tg.t.gameObject);
    }

    // Grid tiles of the triangle: 1 wide at the mouth, then 3, 5, 7...
    List<Vector2Int> TongueTiles(Vector2Int anchor, Vector2Int dir)
    {
        var tiles = new List<Vector2Int>();
        int half = (unit.size - 1) / 2;
        Vector2Int centre = anchor + new Vector2Int(half, half);
        Vector2Int side = new Vector2Int(-dir.y, dir.x);
        Vector2Int front = centre + dir * (half + 1);
        for (int k = 1; k <= tongueRange; k++)
            for (int j = -(k - 1); j <= k - 1; j++)
            {
                Vector2Int t = front + dir * (k - 1) + side * j;
                if (grid.InBounds(t.x, t.y)) tiles.Add(t);
            }
        return tiles;
    }

    Vector2Int BestTongueDirection(Vector2Int anchor, CombatUnit focus, out List<CombatUnit> hits)
    {
        Vector2Int best = Vector2Int.right;
        hits = new List<CombatUnit>();
        foreach (Vector2Int d in TileMath.Directions)
        {
            List<CombatUnit> h = InArea(TongueTiles(anchor, d));
            if (Score(h, focus) > Score(hits, focus)) { hits = h; best = d; }
        }
        return best;
    }

    // ================= Stomp (its only way to move) =================

    // The landing spot within jumpRange with the highest score. 'score' is told the spot,
    // who a stomp there would hit, and how many steps away it is. 'hits' = who the winner hits.
    Vector2Int BestLanding(Vector2Int me, out List<CombatUnit> hits, System.Func<Vector2Int, List<CombatUnit>, int, int> score)
    {
        Vector2Int best = me;
        int bestScore = int.MinValue;
        hits = new List<CombatUnit>();
        for (int dx = -jumpRange; dx <= jumpRange; dx++)
            for (int dy = -jumpRange; dy <= jumpRange; dy++)
            {
                int steps = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (steps > jumpRange) continue;
                Vector2Int spot = me + new Vector2Int(dx, dy);
                if (spot != me && !CanStand(spot)) continue;
                List<CombatUnit> h = InArea(StompTiles(spot));
                int s = score(spot, h, steps);
                if (s > bestScore) { bestScore = s; best = spot; hits = h; }
            }
        return best;
    }

    IEnumerator StompTo(Vector2Int landing, List<CombatUnit> victims)
    {
        Vector3 to = unit.WorldPositionFor(grid.OriginTile + landing);
        Face(to - transform.position);
        yield return Warn(StompTiles(landing));
        List<bool> saved = new List<bool>();
        yield return RollSaves(victims, stompName, saved);

        bool landed = false;
        StartCoroutine(Jump(to, () => landed = true));   // runs on us, so it always finishes
        while (!landed) yield return null;
        grid.ClearMarks();
        if (victims.Count > 0) yield return Damage(victims, saved, stompName, stompDiceCount, stompDiceSides, stompBonus);
        yield return new WaitForSeconds(0.35f);   // let the body settle
        turns.CheckForVictory();
    }

    // Squat, jump up stretched (flying over to 'to'), fall stretched, land squashed with a shockwave
    IEnumerator Jump(Vector3 to, System.Action onLand)
    {
        Vector3 from = transform.position;
        if (body == null) { transform.position = to; onLand?.Invoke(); yield break; }
        Vector3 home = body.localPosition;
        Vector3 size = body.localScale;
        float height = jumpHeight * unit.tileSize;

        yield return Tween.Over(0.2f, t => Scale(size, Mathf.Lerp(1f, 1.18f, t), Mathf.Lerp(1f, 0.78f, t)));   // squat
        yield return Tween.Over(0.34f, t =>                                                                    // up
        {
            float e = 1f - (1f - t) * (1f - t);
            transform.position = Vector3.Lerp(from, to, t * 0.55f);
            body.localPosition = home + Vector3.up * height * e;
            Scale(size, Mathf.Lerp(0.82f, 0.95f, t), Mathf.Lerp(1.3f, 1.08f, t));
        });
        yield return Tween.Over(0.12f, t =>                                                                    // hang
        {
            transform.position = Vector3.Lerp(from, to, 0.55f + t * 0.1f);
            Scale(size, 0.97f, 1.03f);
        });
        yield return Tween.Over(0.22f, t =>                                                                    // down
        {
            transform.position = Vector3.Lerp(from, to, 0.65f + t * 0.35f);
            body.localPosition = home + Vector3.up * height * (1f - t * t);
            Scale(size, Mathf.Lerp(0.95f, 0.8f, t), Mathf.Lerp(1.05f, 1.32f, t));
        });
        transform.position = to;
        yield return Land(home, size, onLand);
    }

    // Squashed flat on impact, shockwave and shake, then a wobble back to normal
    IEnumerator Land(Vector3 home, Vector3 size, System.Action onLand)
    {
        body.localPosition = home;
        Scale(size, 1.35f, 0.65f);
        StartCoroutine(Shockwave());
        Sfx.Boom();
        CameraController cam = CameraController.Main;
        if (cam != null) cam.Shake(0.3f, 0.45f);
        onLand?.Invoke();

        yield return Tween.Over(0.35f, t =>
        {
            float wob = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t) * 0.25f;
            Scale(size, 1f + 0.35f * (1f - t) * (1f - t) - wob * 0.5f, 1f - 0.35f * (1f - t) * (1f - t) + wob);
        });
        body.localScale = size;
        body.localPosition = home;
    }

    void Scale(Vector3 size, float x, float y) => body.localScale = new Vector3(size.x * x, size.y * y, size.z);

    // The shockwave from stomp.PNG, lying flat around its feet
    IEnumerator Shockwave()
    {
        if (stompFrames == null || stompFrames.Length == 0) yield break;
        var go = new GameObject("Stomp Shockwave", typeof(SpriteRenderer));
        var sr = go.GetComponent<SpriteRenderer>();
        if (effectMaterial != null) sr.sharedMaterial = effectMaterial;
        Vector3 p = transform.position;
        go.transform.position = new Vector3(p.x, FloorY() + 0.08f, p.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        float across = (unit.size + stompRadius * 2f) * unit.tileSize * 1.15f;
        float spriteSize = stompFrames[0].rect.width / stompFrames[0].pixelsPerUnit;
        go.transform.localScale = Vector3.one * across / spriteSize;

        const float fps = 14f;
        float shown = stompFrames.Length / fps;
        for (float t = 0f; t < shown + 0.25f; t += Time.deltaTime)
        {
            sr.sprite = stompFrames[Mathf.Min((int)(t * fps), stompFrames.Length - 1)];
            Color c = sr.color;
            c.a = t < shown ? 1f : 1f - (t - shown) / 0.25f;   // the last frame fades
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    // Grid tiles at most stompRadius steps from its body (not under it)
    List<Vector2Int> StompTiles(Vector2Int anchor)
    {
        var tiles = new List<Vector2Int>();
        int r = stompRadius;
        for (int x = anchor.x - r; x < anchor.x + unit.size + r; x++)
            for (int y = anchor.y - r; y < anchor.y + unit.size + r; y++)
            {
                int gap = TileMath.StepsBetween(anchor, unit.size, new Vector2Int(x, y), 1);
                if (gap >= 1 && gap <= r && grid.InBounds(x, y)) tiles.Add(new Vector2Int(x, y));
            }
        return tiles;
    }

    // ================= Rolls and damage =================

    IEnumerator Warn(List<Vector2Int> tiles)
    {
        grid.MarkTiles(tiles, warnColor, warnStrength);
        yield return new WaitForSeconds(warnTime);
    }

    // Each victim rolls a DEX save, on the real dice
    IEnumerator RollSaves(List<CombatUnit> victims, string move, List<bool> saved)
    {
        foreach (CombatUnit v in victims)
        {
            // D&D: paralysed fails DEX saves outright; restrained rolls them with disadvantage; bane takes 1d4 off
            if (v.Has(StatusType.Paralyse))
            {
                saved.Add(false);
                Debug.Log($"{v.unitName} is paralysed and fails the DEX save vs {move}");
                continue;
            }
            int roll = 0;
            yield return DiceTray.RollD20($"{v.unitName}: DEX save vs {move}  (DC {SaveDC})", x => roll = x);
            if (v.Has(StatusType.Restrained))
            {
                int second = 0;
                yield return DiceTray.RollD20($"Disadvantage: {v.unitName} is restrained  —  second roll", x => second = x);
                roll = Mathf.Min(roll, second);
            }
            int total = roll + v.dexModifier + v.BlessBonus - StatusRules.BaneRoll(v);
            bool ok = AttackRoll.Beats(roll, total, SaveDC);
            saved.Add(ok);
            Debug.Log($"{v.unitName} DEX save vs {move}: d20 ({roll}) + {v.dexModifier} = {total} vs DC {SaveDC} — {(ok ? "half damage" : "full damage")}");
        }
    }

    // One damage roll for the whole attack; a passed save halves it
    IEnumerator Damage(List<CombatUnit> victims, List<bool> saved, string move, int count, int sides, int bonus)
    {
        int damage = Mathf.Max(DiceSimulator.Roll(count, sides) + bonus, 1);
        int hymn = unit.RollBlessDie();   // Unholy Hymn: +1d4
        damage += hymn;
        string text = "";
        // Lucius first, so Kairos stays protected until that hit has landed
        var order = new List<int>();
        for (int i = 0; i < victims.Count; i++) if (victims[i] != null && !victims[i].IsPlayer) order.Add(i);
        for (int i = 0; i < victims.Count; i++) if (!order.Contains(i)) order.Add(i);
        foreach (int i in order)
        {
            CombatUnit v = victims[i];
            if (v == null || !v.IsAlive) continue;
            bool half = i < saved.Count && saved[i];
            int dealt = half ? Mathf.Max(damage / 2, 1) : damage;
            v.TakeDamage(dealt);
            UpdateForcedRules();
            Debug.Log($"{Name()} {move} hits {v.unitName} for {dealt}{(half ? " (half)" : "")}");
            if (text.Length > 0) text += "     ";
            text += $"{v.unitName} {dealt}{(half ? " (half)" : "")}" + (v.IsAlive ? "" : " DOWN");
        }
        string dice = DiceFormat.Faint($"{count}d{sides}+{bonus}" + (hymn > 0 ? $" +1d{unit.BlessDie}" : "") + $" = {damage}");
        yield return DiceTray.ShowMessage($"{Name()}: {move.ToUpper()}", text + "   " + dice, false);
    }

    // Party members standing on any of these grid tiles
    List<CombatUnit> InArea(List<Vector2Int> gridTiles)
    {
        var hit = new List<CombatUnit>();
        foreach (CombatUnit p in combat.Party)
        {
            if (p == null || !p.IsAlive) continue;
            foreach (Vector2Int t in gridTiles)
                if (p.Occupies(grid.OriginTile + t)) { hit.Add(p); break; }
        }
        return hit;
    }

    // ================= Helpers (MyTile, CanStand, Gap, ClosestPartyMember and Name are in CombatBrain) =================

    float FloorY() => TileMath.FloorY(transform.position, grid.floorLayers);
}
