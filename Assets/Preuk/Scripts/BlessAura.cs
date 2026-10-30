using System.Collections.Generic;
using UnityEngine;

// A golden aura behind everyone who is blessed (the Bless spell, CombatUnit.BlessDie).
// It grows in (all frames of bless.PNG once), then breathes (the last frames back and forth)
// until the blessing ends with the fight. Hidden for units in the dark.
// Lives on the Combat object, next to StatusEffects.
public class BlessAura : MonoBehaviour
{
    [Tooltip("bless.PNG frames, in order.")]
    public Sprite[] frames;
    [Tooltip("Unlit (SpellUnlit) so it glows in the dark. Empty = default sprite material.")]
    public Material material;
    public float framesPerSecond = 10f;
    [Tooltip("Breathing speed once it has grown in.")]
    public float breatheFramesPerSecond = 5f;
    [Tooltip("From this frame on it breathes back and forth.")]
    public int breatheFrom = 2;
    [Tooltip("Aura height compared to the character's sprite.")]
    public float heightScale = 1.25f;
    [Range(0f, 1f)] public float alpha = 0.8f;

    class Aura
    {
        public SpriteRenderer sr;
        public float age;
    }

    readonly Dictionary<CombatUnit, Aura> auras = new Dictionary<CombatUnit, Aura>();
    readonly List<CombatUnit> units = new List<CombatUnit>();
    readonly List<CombatUnit> gone = new List<CombatUnit>();
    float refreshIn;

    void OnDisable()
    {
        foreach (Aura a in auras.Values)
            if (a.sr != null) Destroy(a.sr.gameObject);
        auras.Clear();
    }

    void LateUpdate()
    {
        if (frames == null || frames.Length == 0) return;

        // Who could be blessed (looked up now and then, units don't change often)
        refreshIn -= Time.deltaTime;
        if (refreshIn <= 0f)
        {
            refreshIn = 1f;
            units.Clear();
            units.AddRange(FindObjectsByType<CombatUnit>(FindObjectsSortMode.None));
        }

        foreach (CombatUnit u in units)
        {
            if (u == null) continue;
            SpriteRenderer body = u.GetComponentInChildren<SpriteRenderer>();
            bool show = u.IsAlive && u.IsBlessed && body != null && body.enabled &&
                        body.gameObject.activeInHierarchy && FogOfWar.CanSee(u);
            auras.TryGetValue(u, out Aura a);

            if (!show)
            {
                if (a != null) { if (a.sr != null) Destroy(a.sr.gameObject); auras.Remove(u); }
                continue;
            }
            if (a == null) auras[u] = a = Make(body);
            a.age += Time.deltaTime;
            Place(a, body);
        }

        // Units that were destroyed
        gone.Clear();
        foreach (var pair in auras) if (pair.Key == null) gone.Add(pair.Key);
        foreach (CombatUnit u in gone) { if (auras[u].sr != null) Destroy(auras[u].sr.gameObject); auras.Remove(u); }
    }

    Aura Make(SpriteRenderer body)
    {
        var go = new GameObject("Bless Aura", typeof(SpriteRenderer));
        var sr = go.GetComponent<SpriteRenderer>();
        if (material != null) sr.sharedMaterial = material;
        return new Aura { sr = sr };
    }

    void Place(Aura a, SpriteRenderer body)
    {
        // Grow in once, then breathe: breatheFrom .. last .. breatheFrom ..
        int index = (int)(a.age * framesPerSecond);
        if (index >= frames.Length)
        {
            int from = Mathf.Clamp(breatheFrom, 0, frames.Length - 1);
            int span = frames.Length - 1 - from;
            if (span <= 0) index = frames.Length - 1;
            else
            {
                int step = (int)((a.age - frames.Length / framesPerSecond) * breatheFramesPerSecond) % (span * 2);
                index = from + (step <= span ? step : span * 2 - step);
            }
        }
        Sprite s = frames[index];
        a.sr.sprite = s;

        // Standing behind the character, feet together, a bit taller than them
        Bounds b = body.bounds;
        float spriteHeight = s.rect.height / s.pixelsPerUnit;
        float height = b.size.y * heightScale;
        float scale = height / Mathf.Max(spriteHeight, 0.01f);
        a.sr.transform.rotation = body.transform.rotation;
        a.sr.transform.localScale = Vector3.one * scale;
        a.sr.transform.position = new Vector3(b.center.x, b.min.y + height * 0.42f, b.center.z) + Vector3.forward * 0.08f;
        a.sr.sortingOrder = body.sortingOrder - 1;
        Color c = Color.white;
        c.a = alpha;
        a.sr.color = c;
    }
}
