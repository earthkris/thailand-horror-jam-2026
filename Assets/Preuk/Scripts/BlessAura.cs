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
    [Range(0f, 1f)] public float alpha = 0.45f;
    [Tooltip("Monsters' blessings (the boss's Unholy Hymn) glow in this colour instead of gold.")]
    public Color enemyColor = new Color32(168, 72, 255, 255);
    [Tooltip("Monsters only: the aura is at least this wide compared to their body, so a wide body doesn't hide it.")]
    [Range(0f, 1.5f)] public float enemyWidth = 0.7f;
    class Aura
    {
        public SpriteRenderer sr;
        public float age;
        public bool enemy;
    }

    Sprite[] enemyFrames;   // the frames recoloured purple, made the first time a monster is blessed

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
            if (a == null) auras[u] = a = Make(body, u.team == Team.Enemy);
            a.age += Time.deltaTime;
            Place(a, body);
        }

        // Units that were destroyed
        gone.Clear();
        foreach (var pair in auras) if (pair.Key == null) gone.Add(pair.Key);
        foreach (CombatUnit u in gone) { if (auras[u].sr != null) Destroy(auras[u].sr.gameObject); auras.Remove(u); }
    }

    Aura Make(SpriteRenderer body, bool enemy)
    {
        var go = new GameObject(enemy ? "Unholy Aura" : "Bless Aura", typeof(SpriteRenderer));
        var sr = go.GetComponent<SpriteRenderer>();
        if (material != null) sr.sharedMaterial = material;
        if (enemy && enemyFrames == null) enemyFrames = Recolor(frames, enemyColor);
        return new Aura { sr = sr, enemy = enemy };
    }

    // Copies the gold frames and paints them in 'color', keeping how bright each pixel is
    // (the brightest cores stay near white). Works on textures that aren't readable too.
    static Sprite[] Recolor(Sprite[] source, Color color)
    {
        var textures = new Dictionary<Texture2D, Texture2D>();
        var result = new Sprite[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            Sprite s = source[i];
            if (s == null) continue;
            if (!textures.TryGetValue(s.texture, out Texture2D tex))
                textures[s.texture] = tex = RecolorTexture(s.texture, color);
            result[i] = Sprite.Create(tex, s.rect, new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height), s.pixelsPerUnit);
        }
        return result;
    }

    static Texture2D RecolorTexture(Texture2D src, Color color)
    {
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];
            float light = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
            Color tinted = Color.Lerp(color * light, Color.white * light, Mathf.Clamp01((light - 0.92f) * 6f));
            pixels[i] = new Color(tinted.r, tinted.g, tinted.b, p.a);
        }
        tex.SetPixels(pixels);
        tex.filterMode = src.filterMode;
        tex.wrapMode = src.wrapMode;
        tex.Apply();
        return tex;
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
        Sprite s = a.enemy && enemyFrames != null && enemyFrames[index] != null ? enemyFrames[index] : frames[index];
        a.sr.sprite = s;

        // Standing behind the character, feet together, a bit taller than them
        // (monsters' also a bit wider: a wide body like the boss's would hide it otherwise)
        Bounds b = body.bounds;
        float spriteHeight = s.rect.height / s.pixelsPerUnit;
        float spriteWidth = s.rect.width / s.pixelsPerUnit;
        float scale = b.size.y * heightScale / Mathf.Max(spriteHeight, 0.01f);
        if (a.enemy) scale = Mathf.Max(scale, Mathf.Max(b.size.x, b.size.z) * enemyWidth / Mathf.Max(spriteWidth, 0.01f));
        float height = spriteHeight * scale;
        a.sr.transform.rotation = body.transform.rotation;
        a.sr.transform.localScale = Vector3.one * scale;
        a.sr.transform.position = new Vector3(b.center.x, b.min.y + height * 0.42f, b.center.z) + Vector3.forward * 0.08f;
        a.sr.sortingOrder = body.sortingOrder - 1;
        Color c = Color.white;
        c.a = alpha;
        a.sr.color = c;
    }
}
