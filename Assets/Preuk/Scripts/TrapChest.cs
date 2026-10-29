using System.Collections;
using UnityEngine;

// The trap chest (storyboard scene 6). Open it (E or click) and a magic circle flares up,
// the party is teleported into the dungeon, and the game fades back in (scene 7).
// The magic circle is a placeholder drawn in code until there's real art for it.
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
    public Color circleColor = new Color32(194, 59, 59, 255);   // blood
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

        // 1. The circle opens up under the chest and keeps turning
        GameObject circle = MakeCircle();
        Light glow = circle.GetComponentInChildren<Light>();
        SpriteRenderer ring = circle.GetComponentInChildren<SpriteRenderer>();
        bool spinning = true;
        Transform ringT = ring.transform;
        Vector3 fullSize = ringT.localScale;   // set by MakeCircle
        ringT.localScale = Vector3.zero;
        if (HasArt) StartCoroutine(Animate(ring, () => spinning));
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
        yield return PlayDialogue(trapDialogue);

        // 3. It flares: shake, brighter, then black
        var cam = FindAnyObjectByType<CameraController>();
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
        player.Teleport(LandingSpot());
        foreach (Follower f in FindObjectsByType<Follower>(FindObjectsSortMode.None)) f.SnapNextToLeader();
        if (cam != null) cam.SnapToTarget();
        spinning = false;
        Destroy(circle);
        yield return new WaitForSecondsRealtime(0.6f);

        // 5. Wake up in the dungeon
        ScreenFade.FadeIn(1.4f, 0.2f);
        yield return new WaitForSecondsRealtime(0.8f);
        GameState.Current = GameMode.Exploring;
        yield return PlayDialogue(arrivalDialogue);
    }

    // The middle of the destination's picture (a 4x4 floor tile's pivot is at its corner), on the grid
    Vector3 LandingSpot()
    {
        Renderer r = destination.GetComponentInChildren<Renderer>();
        Vector3 p = r != null ? r.bounds.center : destination.position;
        float ts = player.tileSize;
        return new Vector3(Mathf.Round(p.x / ts) * ts, p.y, Mathf.Round(p.z / ts) * ts);
    }

    static IEnumerator PlayDialogue(DialogueScriptableObject lines)
    {
        if (lines == null || DialogueBox.Instance == null) yield break;
        DialogueBox.Instance.StartDialogue(lines);
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.IsContentOpen) yield return null;
    }

    // Beside the chest (Circle Offset), just above the floor, on the grid
    Vector3 CircleCentre()
    {
        float ts = player != null ? player.tileSize : 1f;
        Vector3 p = transform.position + circleOffset * ts;
        return new Vector3(Mathf.Round(p.x / ts) * ts, transform.position.y + 0.03f, Mathf.Round(p.z / ts) * ts);
    }

    bool HasArt => circleLoopFrames != null && circleLoopFrames.Length > 0;

    // Every frame once (open, then the rest), then it stays on the last frame
    IEnumerator Animate(SpriteRenderer sr, System.Func<bool> keepGoing)
    {
        float wait = 1f / Mathf.Max(circleFramesPerSecond, 1f);
        if (circleOpenFrames != null)
            foreach (Sprite f in circleOpenFrames)
            {
                if (sr == null || !keepGoing()) yield break;
                sr.sprite = f;
                yield return new WaitForSeconds(wait);
            }
        foreach (Sprite f in circleLoopFrames)
        {
            if (sr == null || !keepGoing()) yield break;
            sr.sprite = f;
            yield return new WaitForSeconds(wait);
        }
        // stays on the last frame
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

    // ---------- The magic circle from Magic_circle.PNG ----------

    // The art is drawn as seen from the camera (squashed). Lying flat on the floor and stretched back
    // into a true circle, it looks exactly like the drawing on screen, and characters stand on it.
    GameObject MakeArtCircle()
    {
        var go = new GameObject("Magic Circle");
        go.transform.position = CircleCentre();
        var ringGo = new GameObject("Ring", typeof(SpriteRenderer));
        ringGo.transform.SetParent(go.transform, false);
        ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Sprite full = circleLoopFrames[0];
        Rect r = full.rect;
        float across = circleSize / (r.width / full.pixelsPerUnit);
        ringGo.transform.localScale = new Vector3(across, across * r.width / r.height, 1f);

        var sr = ringGo.GetComponent<SpriteRenderer>();
        sr.sprite = circleOpenFrames != null && circleOpenFrames.Length > 0 ? circleOpenFrames[0] : full;
        if (circleMaterial != null) sr.sharedMaterial = circleMaterial;

        var lightGo = new GameObject("Glow", typeof(Light));
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.6f;
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = circleColor;
        light.range = circleSize + 2f;
        light.intensity = 0f;
        return go;
    }

    // ---------- Placeholder magic circle: rings and rune ticks drawn into a texture ----------

    GameObject MakeCircle()
    {
        if (HasArt) return MakeArtCircle();
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                bool on =
                    (r > 0.90f && r < 0.97f) ||                                   // outer ring
                    (r > 0.62f && r < 0.66f) ||                                   // inner ring
                    (r > 0.68f && r < 0.86f && Mathf.Repeat(a, 30f) < 3.5f) ||    // rune ticks
                    (r < 0.62f && Mathf.Abs(Mathf.Repeat(a + 15f, 60f) - 30f) < 1.2f && r > 0.15f) ||   // star spokes
                    (r < 0.12f);                                                  // centre
                px[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();

        // Root at normal scale; the ring child grows and spins, the light stays put
        var go = new GameObject("Magic Circle (placeholder)");
        go.transform.position = CircleCentre();
        var ringGo = new GameObject("Ring", typeof(SpriteRenderer));
        ringGo.transform.SetParent(go.transform, false);
        ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ringGo.transform.localScale = Vector3.zero;
        var sr = ringGo.GetComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);   // 1 unit across
        sr.color = circleColor;
        if (circleMaterial != null) sr.sharedMaterial = circleMaterial;

        var lightGo = new GameObject("Glow", typeof(Light));
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.6f;
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = circleColor;
        light.range = circleSize + 2f;
        light.intensity = 0f;
        return go;
    }
}
