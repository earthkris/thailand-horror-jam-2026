using System.Collections.Generic;
using UnityEngine;

public enum StatusType { None, Burn, Paralyse, Regen }

// Words for the battle log and the dice banner
public static class StatusInfo
{
    public static string Word(StatusType s) => s == StatusType.Burn ? "burning" : s == StatusType.Paralyse ? "paralysed" : s == StatusType.Regen ? "regenerating" : "";
}

// Shows burn and paralyse on whoever has them: little flames rising off a burning unit,
// blue sparks crackling over a paralysed one (sprites from Status_particle).
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

        float every = status == StatusType.Burn ? burnEvery : status == StatusType.Paralyse ? sparkEvery : regenEvery;
        clocks[key] = Mathf.Max(every, 0.02f) * Random.Range(0.7f, 1.3f);
        return true;
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
