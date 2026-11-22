using UnityEngine;

// One bit of a spell or hit effect: a glowing sprite that grows, moves, spins and fades by itself,
// then removes itself. MoveFx and StatusEffects build every effect out of these.
// Sprites come from Resources/Fx (Kenney Particle Pack, CC0), drawn with the Preuk/Fx Glow shader,
// so they light up even in the dark.
//
// Usage:  FxPiece.Make("slash_04", pos, Color.white, 1.2f, 0.25f).Grow(1.4f).Spin(90f);
public class FxPiece : MonoBehaviour
{
    SpriteRenderer sr;
    Color color;
    float age, life, delay;
    float size0, size1;
    Vector2 stretch = Vector2.one;
    Vector3 velocity;
    float gravity;
    float spin, angle;
    float fadeIn = 0.1f, fadeOut = 0.5f;   // fractions of the life
    bool flat;                             // lies on the floor instead of facing the camera
    bool flicker;
    Transform follow;
    Vector3 followOffset;
    Light glow;
    float glowPeak;

    static Material glowMat, softMat;

    // ================= Making one =================

    // 'size' in world units (a tile is about 1), 'life' in seconds. glow = adds light (fire, holy);
    // off = normal see-through (smoke, webs, dark curses).
    public static FxPiece Make(string sprite, Vector3 position, Color color, float size, float life, bool glow = true)
    {
        var go = new GameObject("Fx " + sprite, typeof(SpriteRenderer));
        go.transform.position = position;
        var p = go.AddComponent<FxPiece>();
        p.sr = go.GetComponent<SpriteRenderer>();
        p.sr.sprite = Sprite(sprite);
        p.sr.sharedMaterial = glow ? GlowMaterial : SoftMaterial;
        p.sr.sortingOrder = 50;
        p.color = color;
        p.size0 = p.size1 = size;
        p.life = Mathf.Max(life, 0.02f);
        p.Apply(0f);
        return p;
    }

    public static Sprite Sprite(string name)
    {
        Sprite s = Resources.Load<Sprite>("Fx/" + name);
        if (s == null) Debug.LogWarning($"FxPiece: no sprite Resources/Fx/{name}");
        return s;
    }

    static Material GlowMaterial => glowMat != null ? glowMat : (glowMat = NewMaterial(true));
    static Material SoftMaterial => softMat != null ? softMat : (softMat = NewMaterial(false));

    static Material NewMaterial(bool additive)
    {
        // A material in Resources keeps the shader in the web build
        Material source = Resources.Load<Material>(additive ? "Fx/FxGlow" : "Fx/FxSoft");
        if (source != null) return source;
        var m = new Material(Shader.Find("Preuk/Fx Glow"));
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        return m;
    }

    // ================= Settings (chain them) =================

    public FxPiece Grow(float toSize) { size1 = toSize; return this; }
    public FxPiece Stretch(float x, float y) { stretch = new Vector2(x, y); return this; }
    public FxPiece Move(Vector3 v, float g = 0f) { velocity = v; gravity = g; return this; }
    public FxPiece Spin(float degreesPerSecond) { spin = degreesPerSecond; return this; }
    public FxPiece Angle(float degrees) { angle = degrees; return this; }
    public FxPiece Fade(float inFraction, float outFraction) { fadeIn = inFraction; fadeOut = outFraction; return this; }
    public FxPiece Flat() { flat = true; return this; }
    public FxPiece Flicker() { flicker = true; return this; }
    public FxPiece After(float seconds) { delay = seconds; if (delay > 0f) sr.enabled = false; return this; }
    public FxPiece Order(int sortingOrder) { sr.sortingOrder = sortingOrder; return this; }

    // Sticks to someone (an aura on a moving unit)
    public FxPiece Follow(Transform t)
    {
        follow = t;
        if (t != null) followOffset = transform.position - t.position;
        return this;
    }

    // A point light that brightens and fades with the piece
    public FxPiece Light(Color c, float range, float intensity)
    {
        glow = SceneLights.NewGlow("Fx Light", transform, c, range);
        glowPeak = intensity;
        return this;
    }

    // ================= Running =================

    void Update()
    {
        if (delay > 0f)
        {
            delay -= Time.deltaTime;
            if (delay > 0f) return;
            sr.enabled = true;
        }
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }

        velocity += Vector3.down * gravity * Time.deltaTime;
        if (follow != null) followOffset += velocity * Time.deltaTime;
        else transform.position += velocity * Time.deltaTime;
        angle += spin * Time.deltaTime;
        Apply(age / life);
    }

    void Apply(float k)
    {
        if (follow != null) transform.position = follow.position + followOffset;

        Camera cam = Camera.main;
        if (flat) transform.rotation = Quaternion.Euler(90f, 0f, angle);
        else if (cam != null) transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, angle);

        // Ease out: quick at first, settling at the end
        float e = 1f - (1f - k) * (1f - k);
        float s = Mathf.Lerp(size0, size1, e);
        transform.localScale = new Vector3(s * stretch.x, s * stretch.y, s);

        float a = 1f;
        if (fadeIn > 0f && k < fadeIn) a = k / fadeIn;
        if (fadeOut > 0f && k > 1f - fadeOut) a = Mathf.Min(a, (1f - k) / fadeOut);
        if (flicker) a *= Random.value > 0.35f ? 1f : 0.3f;
        Color c = color;
        c.a *= a;
        if (sr != null) sr.color = c;
        if (glow != null) glow.intensity = glowPeak * a;
    }
}
