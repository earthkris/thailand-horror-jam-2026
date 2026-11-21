using System.Collections;
using UnityEngine;

// The trap chest (storyboard scene 6). Open it (E or click) and a magic circle flares up,
// the party is teleported into the dungeon, and the game fades back in (scene 7).
// Without circle frames set, a placeholder circle is drawn in code instead.
public class TrapChest : Interactable
{
    [Header("Teleport")]
    [Tooltip("Where the party lands, e.g. a dungeon floor tile. The middle of its picture is used.")]
    public Transform destination;

    [Header("Dialogue (optional, Earth's dialogue box)")]
    [Tooltip("Before the teleport: 'Huh, why is there a magic circle?!'")]
    public DialogueScriptableObject trapDialogue;
    [Tooltip("After landing: waking up in the dungeon.")]
    public DialogueScriptableObject arrivalDialogue;

    [Header("Magic circle")]
    [Tooltip("Played first as the circle opens (Magic_circle frames 0-2). Empty = a placeholder circle drawn in code.")]
    public Sprite[] circleOpenFrames;
    [Tooltip("Played after the open frames (frames 3-10); it stops on the last one.")]
    public Sprite[] circleLoopFrames;
    public float circleFramesPerSecond = 12f;
    [Tooltip("Colour of the glow (and of the placeholder circle).")]
    public Color circleColor = Palette.Blood;
    [Tooltip("Tiles across.")]
    public float circleSize = 3f;
    [Tooltip("Where the circle's centre is, from the chest, in tiles. (-1, 0, 0) = one tile to the left, where the player walks up.")]
    public Vector3 circleOffset = new Vector3(-1f, 0f, 0f);
    [Tooltip("Unlit material (SpellUnlit) so it glows in the dark.")]
    public Material circleMaterial;

    bool sprung;

    protected override string Prompt => "Open";

    protected override void Interact()
    {
        if (sprung) return;
        if (destination == null)
        {
            Debug.LogWarning($"TrapChest '{name}': no destination set.", this);
            return;
        }
        sprung = true;
        enabled = false;   // no more prompt (the sequence keeps running)
        StartCoroutine(Spring());
    }

    IEnumerator Spring()
    {
        GameState.Current = GameMode.Cutscene;   // nobody moves, monsters wait
        player.StopWalking();
        while (player.IsStepping) yield return null;

        Sfx.Play("chest_open");
        Sfx.Play("trap", 0.9f);

        // 1. The circle opens up under the chest and keeps turning
        GameObject circle = MakeCircle();
        Light glow = circle.GetComponentInChildren<Light>();
        SpriteRenderer ring = circle.GetComponentInChildren<SpriteRenderer>();
        bool spinning = true;
        Transform ringT = ring.transform;
        Vector3 fullSize = ringT.localScale;   // set by MakeCircle
        ringT.localScale = Vector3.zero;
        if (HasArt) StartCoroutine(PlayCircleFrames(ring, () => spinning));
        else StartCoroutine(Spin(ringT, () => spinning));   // the placeholder just turns
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            float k = 1f - (1f - t) * (1f - t);
            if (!HasArt) ringT.localScale = fullSize * k;   // the art grows by itself (frames 0-2)
            else ringT.localScale = fullSize;
            glow.intensity = 3f * k;
            yield return null;
        }

        // 2. 'Huh, why is there a magic circle?!' / 'It's a trap!'
        yield return StoryDialogue.Play(trapDialogue);

        // 3. It flares: shake, brighter, then black
        CameraController cam = CameraController.Main;
        if (cam != null) cam.Shake(0.15f, 0.8f);
        ScreenFade.FadeOut(0.6f);
        Color startColor = ring.color;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
        {
            glow.intensity = Mathf.Lerp(3f, 12f, t);
            ring.color = Color.Lerp(startColor, Color.white, t * 0.6f);
            ringT.localScale = fullSize * (1f + 0.15f * Mathf.Sin(t * 25f));
            yield return null;
        }

        // 4. Teleport while the screen is black
        player.TeleportParty(LandingSpot());
        GameState.InDungeon = true;   // the menu's LOOP unlocks
        spinning = false;
        Destroy(circle);
        yield return new WaitForSecondsRealtime(0.6f);

        // 5. Wake up in the dungeon
        ScreenFade.FadeIn(1.4f, 0.2f);
        yield return new WaitForSecondsRealtime(0.8f);
        GameState.Current = GameMode.Exploring;
        yield return StoryDialogue.Play(arrivalDialogue);
    }

    // The middle of the destination's picture (a 4x4 floor tile's pivot is at its corner), on the grid
    public Vector3 LandingSpot()
    {
        Renderer r = destination.GetComponentInChildren<Renderer>();
        Vector3 p = r != null ? r.bounds.center : destination.position;
        if (player == null) player = FindAnyObjectByType<PlayerMovement>();
        float ts = player != null ? player.tileSize : 1f;
        return TileMath.Snap(p, ts);
    }

    // Beside the chest (Circle Offset), just above the floor, on the grid
    Vector3 CircleCentre()
    {
        float ts = player != null ? player.tileSize : 1f;
        Vector3 centre = TileMath.Snap(transform.position + circleOffset * ts, ts);
        centre.y = transform.position.y + 0.03f;
        return centre;
    }

    bool HasArt => circleLoopFrames != null && circleLoopFrames.Length > 0;

    // The circle art, every frame once (open, then the rest); it stays on the last frame.
    // Stops early when 'keepGoing' says no. BossVictory plays its exit circle with this too.
    public IEnumerator PlayCircleFrames(SpriteRenderer sr, System.Func<bool> keepGoing = null)
    {
        float wait = 1f / Mathf.Max(circleFramesPerSecond, 1f);
        if (circleOpenFrames != null)
            foreach (Sprite f in circleOpenFrames)
            {
                if (sr == null || (keepGoing != null && !keepGoing())) yield break;
                sr.sprite = f;
                yield return new WaitForSeconds(wait);
            }
        foreach (Sprite f in circleLoopFrames)
        {
            if (sr == null || (keepGoing != null && !keepGoing())) yield break;
            sr.sprite = f;
            yield return new WaitForSeconds(wait);
        }
    }

    IEnumerator Spin(Transform t, System.Func<bool> keepGoing)
    {
        float angle = 0f;
        while (t != null && keepGoing())
        {
            angle += 40f * Time.deltaTime;
            t.rotation = Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(90f, 0f, 0f);   // flat on the floor
            yield return null;
        }
    }

    // ---------- The magic circle ----------

    // A root at normal scale with two children: the ring lying on the floor (it grows, animates or spins)
    // and a glow above it that stays put.
    GameObject MakeCircle()
    {
        var go = new GameObject(HasArt ? "Magic Circle" : "Magic Circle (placeholder)");
        go.transform.position = CircleCentre();

        if (HasArt)
        {
            // Magic_circle.PNG: sized by its full frame, starting on the first opening frame
            SpriteRenderer ring = AddRing(go.transform, circleLoopFrames[0], circleSize, circleMaterial);
            if (circleOpenFrames != null && circleOpenFrames.Length > 0) ring.sprite = circleOpenFrames[0];
        }
        else AddRing(go.transform, PlaceholderCircle(), circleSize, circleMaterial).color = circleColor;

        AddGlow(go.transform, circleColor, circleSize);
        return go;
    }

    // A sprite lying flat on the floor under 'parent', 'across' wide and as deep as it is wide.
    // Circle art is drawn as seen from the camera (squashed). Lying flat and stretched back into a true
    // circle, it looks exactly like the drawing on screen, and characters stand on it.
    public static SpriteRenderer AddRing(Transform parent, Sprite shape, float across, Material material)
    {
        var go = new GameObject("Ring", typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Rect r = shape.rect;
        float scale = across / (r.width / shape.pixelsPerUnit);
        go.transform.localScale = new Vector3(scale, scale * r.width / r.height, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = shape;
        if (material != null) sr.sharedMaterial = material;
        return sr;
    }

    // The light over a circle 'circleSize' tiles across. Dark until its intensity is raised.
    public static Light AddGlow(Transform parent, Color color, float circleSize)
    {
        Light glow = SceneLights.NewGlow("Glow", parent, color, circleSize + 2f);
        glow.transform.localPosition = Vector3.up * 0.6f;
        return glow;
    }

    // Placeholder magic circle: rings and rune ticks painted in code (1 unit across)
    static Sprite PlaceholderCircle()
    {
        const int size = 256;
        return ProceduralSprite.Create(size, size, (dx, dy) =>
        {
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
            bool on =
                (r > 0.90f && r < 0.97f) ||                                   // outer ring
                (r > 0.62f && r < 0.66f) ||                                   // inner ring
                (r > 0.68f && r < 0.86f && Mathf.Repeat(a, 30f) < 3.5f) ||    // rune ticks
                (r < 0.62f && Mathf.Abs(Mathf.Repeat(a + 15f, 60f) - 30f) < 1.2f && r > 0.15f) ||   // star spokes
                (r < 0.12f);                                                  // centre
            return on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        }, FilterMode.Point, size);
    }
}
