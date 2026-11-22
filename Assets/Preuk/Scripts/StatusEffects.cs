using System.Collections.Generic;
using UnityEngine;

// New ones go on the end: assets store these as numbers.
public enum StatusType { None, Burn, Paralyse, Regen, Poisoned, Frightened, Restrained, Prone, Blinded, Bane }

// Words for the battle log, the dice banner and the turn panel
public static class StatusInfo
{
    public static string Word(StatusType s)
    {
        switch (s)
        {
            case StatusType.Burn: return "burning";
            case StatusType.Paralyse: return "paralysed";
            case StatusType.Regen: return "regenerating";
            case StatusType.Poisoned: return "poisoned";
            case StatusType.Frightened: return "frightened";
            case StatusType.Restrained: return "restrained";
            case StatusType.Prone: return "knocked prone";
            case StatusType.Blinded: return "blinded";
            case StatusType.Bane: return "cursed";
            default: return "";
        }
    }

    // Short name for the turn panel ("Poisoned 2")
    public static string Label(StatusType s)
    {
        switch (s)
        {
            case StatusType.Burn: return "Burning";
            case StatusType.Regen: return "Regen";
            case StatusType.Prone: return "Prone";
            case StatusType.Bane: return "Bane";
            default: string w = Word(s); return w.Length == 0 ? "" : char.ToUpper(w[0]) + w.Substring(1);
        }
    }

    // What it does, for move descriptions and the sheet
    public static string Rules(StatusType s)
    {
        switch (s)
        {
            case StatusType.Burn: return "takes fire damage at the start of each turn";
            case StatusType.Paralyse: return "loses its turns; attacks against it have advantage";
            case StatusType.Regen: return "heals at the start of each turn";
            case StatusType.Poisoned: return "attacks with disadvantage";
            case StatusType.Frightened: return "attacks with disadvantage and moves at half speed";
            case StatusType.Restrained: return "can't move, attacks with disadvantage, and attacks against it have advantage";
            case StatusType.Prone: return "spends half its move standing up; melee attacks against it have advantage, shots have disadvantage";
            case StatusType.Blinded: return "attacks with disadvantage, and attacks against it have advantage";
            case StatusType.Bane: return "subtracts 1d4 from its attack rolls";
            default: return "";
        }
    }
}

// The D&D conditions in numbers. Every attack (party, monsters, boss, opportunity attacks) asks here.
public static class StatusRules
{
    // Everything but Regen is bad for you (an Antidote Salve ends all of them)
    public static bool Harmful(StatusType s) => s != StatusType.None && s != StatusType.Regen;

    // Burn, Paralyse, Regen and Prone tick down at the start of the unit's turn (that's when they happen).
    // The rest last through the turn and tick down as it ends.
    public static bool CountsAtTurnEnd(StatusType s) =>
        s == StatusType.Poisoned || s == StatusType.Frightened || s == StatusType.Restrained ||
        s == StatusType.Blinded || s == StatusType.Bane;

    // Tiles it can walk this turn
    public static int Speed(CombatUnit u, int tiles)
    {
        if (u.Has(StatusType.Restrained) || u.Has(StatusType.Paralyse)) return 0;
        if (u.Has(StatusType.Frightened) || u.Has(StatusType.Prone)) return tiles / 2;
        return tiles;
    }

    // Does 'attacker' get advantage / disadvantage on 'target' from conditions? Adds the reason to 'why'.
    public static void Edge(CombatUnit attacker, CombatUnit target, bool melee, ref bool advantage, ref bool disadvantage, List<string> why)
    {
        if (attacker != null)
            foreach (StatusType s in Hampering)
                if (attacker.Has(s)) { disadvantage = true; why?.Add($"{attacker.unitName} is {StatusInfo.Word(s)}"); break; }
        if (target == null) return;
        foreach (StatusType s in Exposing)
            if (target.Has(s)) { advantage = true; why?.Add($"{target.unitName} is {StatusInfo.Word(s)}"); break; }
        if (target.Has(StatusType.Prone))
        {
            if (melee) advantage = true; else disadvantage = true;
            why?.Add($"{target.unitName} is prone");
        }
    }

    // Bane: 1d4 off its attack rolls (0 when not cursed)
    public static int BaneRoll(CombatUnit u) => u != null && u.Has(StatusType.Bane) ? DiceSimulator.Roll(1, 4) : 0;

    public static RollMode Mode(bool advantage, bool disadvantage) =>
        advantage == disadvantage ? RollMode.Normal : advantage ? RollMode.Advantage : RollMode.Disadvantage;

    static readonly StatusType[] Hampering = { StatusType.Poisoned, StatusType.Frightened, StatusType.Restrained, StatusType.Prone, StatusType.Blinded };
    static readonly StatusType[] Exposing = { StatusType.Restrained, StatusType.Blinded, StatusType.Paralyse };
}

// Shows burn and paralyse on whoever has them: little flames rising off a burning unit,
// blue sparks crackling over a paralysed one (sprites from Status_particle).
// The D&D conditions use FxPiece bits (Resources/Fx): green bubbles (poisoned), purple wisps (frightened),
// a web wrapped round the legs (restrained), dark smoke round the eyes (blinded), purple motes falling (bane).
// Prone is the body lying down (DownedPose).
// The rules (damage, lost turns) live in CombatUnit and TurnManager; this is only the look.
// Units hidden in the dark show nothing, so the effect doesn't give them away.
// Lives on the Combat object, next to HitEffects.
public class StatusEffects : MonoBehaviour
{
    [Header("Sprites (Status_particle)")]
    public Sprite burnSprite;
    public Sprite paralyseSprite;
    [Tooltip("Unlit (SpellUnlit) so flames and sparks glow in the dark.")]
    public Material material;

    [Header("Burn: flames rising")]
    [Tooltip("Seconds between flames.")]
    public float burnEvery = 0.1f;
    public float burnLife = 0.7f;
    [Tooltip("How fast flames rise, in tiles per second.")]
    public float burnRise = 0.9f;
    public Vector2 burnSize = new Vector2(0.4f, 0.7f);

    [Header("Paralyse: sparks crackling")]
    public float sparkEvery = 0.14f;
    public float sparkLife = 0.25f;
    public Vector2 sparkSize = new Vector2(0.45f, 0.75f);

    [Header("Regen: little + signs floating up (heal_type)")]
    public Sprite regenSprite;
    public Color regenColor = new Color(0.6f, 1f, 0.55f);
    public float regenEvery = 0.2f;
    public float regenLife = 1f;
    public float regenRise = 0.7f;
    public Vector2 regenSize = new Vector2(0.22f, 0.38f);

    class Particle
    {
        public Transform t;
        public SpriteRenderer sr;
        public float age, life, size, angle;
        public Vector3 velocity;
        public bool spark;
    }

    // The statuses that show something
    static readonly StatusType[] Shown = { StatusType.Burn, StatusType.Paralyse, StatusType.Regen };

    readonly List<Particle> particles = new List<Particle>();
    readonly List<CombatUnit> afflicted = new List<CombatUnit>();
    // Seconds until the next particle, for each unit and status
    readonly Dictionary<(CombatUnit, StatusType), float> clocks = new Dictionary<(CombatUnit, StatusType), float>();

    void OnEnable() => CombatUnit.OnAnyStatusChanged += OnStatusChanged;

    void OnDisable()
    {
        CombatUnit.OnAnyStatusChanged -= OnStatusChanged;
        foreach (Particle p in particles)
            if (p.t != null) Destroy(p.t.gameObject);
        particles.Clear();
        afflicted.Clear();
        clocks.Clear();
    }

    void OnStatusChanged(CombatUnit u)
    {
        if (u.HasAnyStatus) { if (!afflicted.Contains(u)) afflicted.Add(u); }
        else afflicted.Remove(u);
    }

    void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float dt = Time.deltaTime;

        // 1. New flames and sparks on everyone affected (and visible)
        for (int i = afflicted.Count - 1; i >= 0; i--)
        {
            CombatUnit u = afflicted[i];
            if (u == null || !u.IsAlive || !u.HasAnyStatus) { afflicted.RemoveAt(i); continue; }
            SpriteRenderer body = u.VisibleSprite();
            if (body == null || !FogOfWar.CanSee(u)) continue;

            foreach (StatusType status in Shown)
                if (u.Has(status) && Due(u, status, dt)) Spawn(status, body, cam);
            foreach (StatusType status in Conditions)
                if (u.Has(status) && Due(u, status, dt)) SpawnCondition(status, body, cam);
        }

        // 2. Move, fade, face the camera
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle p = particles[i];
            p.age += dt;
            if (p.t == null || p.age >= p.life)
            {
                if (p.t != null) Destroy(p.t.gameObject);
                particles.RemoveAt(i);
                continue;
            }
            float k = p.age / p.life;   // 0 -> 1 over its life
            p.t.position += p.velocity * dt;
            p.t.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, p.angle);

            float alpha, scale;
            if (p.spark)
            {
                alpha = (Random.value > 0.3f ? 1f : 0.25f) * (1f - k);   // crackle
                scale = p.size;
            }
            else
            {
                alpha = 1f - k * k;                 // flames fade near the top
                scale = p.size * (1f - 0.5f * k);   // and shrink
            }
            Color c = p.sr.color;
            c.a = alpha;
            p.sr.color = c;
            p.t.localScale = Vector3.one * scale;
        }
    }

    // Time for the next flame / spark / + on this unit?
    bool Due(CombatUnit u, StatusType status, float dt)
    {
        var key = (u, status);
        clocks.TryGetValue(key, out float wait);
        if (wait > 0f) { clocks[key] = wait - dt; return false; }

        float every = status == StatusType.Burn ? burnEvery : status == StatusType.Paralyse ? sparkEvery
                    : status == StatusType.Regen ? regenEvery : ConditionEvery(status);
        clocks[key] = Mathf.Max(every, 0.02f) * Random.Range(0.7f, 1.3f);
        return true;
    }

    // ================= D&D conditions =================

    static readonly StatusType[] Conditions = { StatusType.Poisoned, StatusType.Frightened, StatusType.Restrained, StatusType.Blinded, StatusType.Bane };

    static float ConditionEvery(StatusType s) =>
        s == StatusType.Poisoned ? 0.2f : s == StatusType.Frightened ? 0.3f : s == StatusType.Restrained ? 0.45f
        : s == StatusType.Blinded ? 0.22f : 0.25f;

    static readonly Color PoisonGreen = new Color(0.5f, 0.95f, 0.2f, 0.9f);
    static readonly Color FearPurple = new Color(0.62f, 0.4f, 1f, 0.8f);
    static readonly Color WebWhite = new Color(0.92f, 0.92f, 0.88f, 0.75f);
    static readonly Color BlindSmoke = new Color(0.3f, 0.27f, 0.34f, 0.85f);
    static readonly Color BaneViolet = new Color(0.75f, 0.3f, 0.95f, 0.9f);

    void SpawnCondition(StatusType s, SpriteRenderer body, Camera cam)
    {
        Bounds b = body.bounds;
        Vector3 front = -cam.transform.forward * 0.2f;
        Vector3 right = cam.transform.right;
        float w = Mathf.Max(b.extents.x, b.extents.z);
        Vector3 feet = new Vector3(b.center.x, b.min.y, b.center.z) + front;
        Vector3 head = new Vector3(b.center.x, b.min.y + b.size.y * 0.8f, b.center.z) + front;
        Vector3 jitter = right * Random.Range(-w, w) * 0.6f;
        switch (s)
        {
            case StatusType.Poisoned:   // bubbles rising and popping
                FxPiece.Make("circle_05", feet + jitter + Vector3.up * b.size.y * Random.Range(0.1f, 0.6f), PoisonGreen, Random.Range(0.08f, 0.16f), 0.8f)
                    .Grow(0.22f).Move(Vector3.up * 0.5f).Fade(0.2f, 0.3f);
                break;
            case StatusType.Frightened:   // pale wisps leaving the head
                FxPiece.Make("smoke_07", head + jitter, FearPurple, 0.2f, 0.9f).Grow(0.45f).Move(Vector3.up * 0.4f + right * Random.Range(-0.2f, 0.2f)).Spin(90f);
                break;
            case StatusType.Restrained:   // a web wrapped round the legs
                FxPiece.Make("twirl_03", feet + Vector3.up * b.size.y * 0.3f, WebWhite, b.size.y * 0.6f, 0.9f, false).Spin(Random.Range(-30f, 30f))
                    .Angle(Random.Range(0f, 360f)).Fade(0.3f, 0.3f);
                break;
            case StatusType.Blinded:   // dark smoke round the eyes
                FxPiece.Make("smoke_02", head + jitter * 0.5f, BlindSmoke, 0.25f, 0.6f, false).Grow(0.45f).Spin(Random.Range(-60f, 60f)).Fade(0.2f, 0.4f);
                break;
            case StatusType.Bane:   // motes of a curse falling onto it
                FxPiece.Make("star_01", head + jitter + Vector3.up * 0.35f, BaneViolet, 0.25f, 0.8f).Move(Vector3.down * 0.6f).Fade(0.2f, 0.4f);
                break;
        }
    }

    // One particle on 'body': a flame rising off it (burn), a spark crackling in place (paralyse),
    // or a green + floating up (regen)
    void Spawn(StatusType status, SpriteRenderer body, Camera cam)
    {
        bool spark = status == StatusType.Paralyse, regen = status == StatusType.Regen;
        Sprite sprite = spark ? paralyseSprite : regen ? regenSprite : burnSprite;
        if (sprite == null) return;

        // Somewhere over the body, just in front of it
        Bounds b = body.bounds;
        Vector3 right = cam.transform.right;
        float halfWidth = Mathf.Max(b.extents.x, b.extents.z) * (regen ? 0.7f : 0.6f);
        float height = b.size.y * (spark ? Random.Range(0.15f, 0.85f) : regen ? Random.Range(0.1f, 0.6f) : Random.Range(0f, 0.5f));
        Vector3 pos = new Vector3(b.center.x, b.min.y, b.center.z)
                    + right * Random.Range(-halfWidth, halfWidth)
                    + Vector3.up * height
                    - cam.transform.forward * 0.15f;

        Vector2 range = spark ? sparkSize : regen ? regenSize : burnSize;
        float size = Random.Range(range.x, range.y);
        float angle = spark ? Random.Range(0f, 360f) : regen ? 0f : Random.Range(-10f, 10f);

        var go = new GameObject(spark ? "Spark" : regen ? "Regen" : "Flame", typeof(SpriteRenderer));
        go.transform.SetPositionAndRotation(pos, cam.transform.rotation * Quaternion.Euler(0f, 0f, angle));
        go.transform.localScale = Vector3.one * size;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (material != null) sr.sharedMaterial = material;
        sr.sortingOrder = body.sortingOrder + 1;
        if (regen) sr.color = regenColor;
        else sr.flipX = Random.value < 0.5f;

        // Sparks stay put and are gone in a blink; flames and + signs rise (flames drift sideways a little)
        Vector3 velocity = spark ? Vector3.zero
                         : regen ? Vector3.up * regenRise * Random.Range(0.7f, 1.2f)
                         : Vector3.up * burnRise * Random.Range(0.7f, 1.2f) + right * Random.Range(-0.15f, 0.15f);

        particles.Add(new Particle
        {
            t = go.transform,
            sr = sr,
            life = spark ? sparkLife : (regen ? regenLife : burnLife) * Random.Range(0.8f, 1.2f),
            size = size,
            angle = angle,
            velocity = velocity,
            spark = spark,
        });
    }
}
