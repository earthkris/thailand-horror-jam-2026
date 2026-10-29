using System.Collections.Generic;
using UnityEngine;

public enum StatusType { None, Burn, Paralyse }

// Words for the battle log and the dice banner
public static class StatusInfo
{
    public static string Word(StatusType s) => s == StatusType.Burn ? "burning" : s == StatusType.Paralyse ? "paralysed" : "";
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

    class Particle
    {
        public Transform t;
        public SpriteRenderer sr;
        public float age, life, size, angle;
        public Vector3 velocity;
        public bool spark;
    }

    readonly List<Particle> particles = new List<Particle>();
    readonly List<CombatUnit> afflicted = new List<CombatUnit>();
    readonly Dictionary<CombatUnit, float> burnClock = new Dictionary<CombatUnit, float>();
    readonly Dictionary<CombatUnit, float> sparkClock = new Dictionary<CombatUnit, float>();

    void OnEnable() => CombatUnit.OnAnyStatusChanged += OnStatusChanged;

    void OnDisable()
    {
        CombatUnit.OnAnyStatusChanged -= OnStatusChanged;
        foreach (Particle p in particles)
            if (p.t != null) Destroy(p.t.gameObject);
        particles.Clear();
        afflicted.Clear();
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
            SpriteRenderer body = u.GetComponentInChildren<SpriteRenderer>();
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy || !FogOfWar.CanSee(u)) continue;

            if (u.Has(StatusType.Burn) && Due(burnClock, u, burnEvery, dt)) Spawn(body, cam, false);
            if (u.Has(StatusType.Paralyse) && Due(sparkClock, u, sparkEvery, dt)) Spawn(body, cam, true);
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

    // Time for the next flame / spark on this unit?
    static bool Due(Dictionary<CombatUnit, float> clock, CombatUnit u, float every, float dt)
    {
        clock.TryGetValue(u, out float wait);
        if (wait > 0f) { clock[u] = wait - dt; return false; }
        clock[u] = Mathf.Max(every, 0.02f) * Random.Range(0.7f, 1.3f);
        return true;
    }

    void Spawn(SpriteRenderer body, Camera cam, bool spark)
    {
        Sprite sprite = spark ? paralyseSprite : burnSprite;
        if (sprite == null) return;

        Bounds b = body.bounds;
        Vector3 right = cam.transform.right;
        float halfWidth = Mathf.Max(b.extents.x, b.extents.z) * 0.6f;
        float height = b.size.y * (spark ? Random.Range(0.15f, 0.85f) : Random.Range(0f, 0.5f));
        Vector3 pos = new Vector3(b.center.x, b.min.y, b.center.z)
                    + right * Random.Range(-halfWidth, halfWidth)
                    + Vector3.up * height
                    - cam.transform.forward * 0.15f;   // just in front of the body

        Vector2 range = spark ? sparkSize : burnSize;
        float size = Random.Range(range.x, range.y);
        float angle = spark ? Random.Range(0f, 360f) : Random.Range(-10f, 10f);

        var go = new GameObject(spark ? "Spark" : "Flame", typeof(SpriteRenderer));
        go.transform.SetPositionAndRotation(pos, cam.transform.rotation * Quaternion.Euler(0f, 0f, angle));
        go.transform.localScale = Vector3.one * size;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (material != null) sr.sharedMaterial = material;
        sr.sortingOrder = body.sortingOrder + 1;
        sr.flipX = Random.value < 0.5f;

        particles.Add(new Particle
        {
            t = go.transform,
            sr = sr,
            life = spark ? sparkLife : burnLife * Random.Range(0.8f, 1.2f),
            size = size,
            angle = angle,
            velocity = spark ? Vector3.zero : Vector3.up * burnRise * Random.Range(0.7f, 1.2f) + right * Random.Range(-0.15f, 0.15f),
            spark = spark,
        });
    }
}
