using System.Collections;
using UnityEngine;

// After the first boss falls (storyboard 13-15):
//   1. 'Finally! Let's get out of here.'
//   2. a golden magic circle opens where the boss died: walk into it
//   3. it flares, the screen goes black, and the party is outside the dungeon
//   4. the world brightens into daylight
//   5. what the escape cost them (blind / mute / gone, by loops used)
//   6. the goddess: 'I told you, everything has a price to pay.' (GodScene.ShowEnding)
public class BossVictory : MonoBehaviour
{
    [Tooltip("Empty = the boss with BossAI.")]
    public CombatUnit boss;
    [Tooltip("Its magic circle art and material are reused for the exit.")]
    public TrapChest trapChest;
    [Tooltip("Where Kairos comes out (outside the dungeon).")]
    public Vector3 outsideSpot = new Vector3(-81f, 1f, -13f);

    [Header("Dialogue")]
    [Tooltip("13  'Finally! Let's get out of here.'")]
    public DialogueScriptableObject win;
    [Tooltip("14  Element 0 = escaped after 1 loop (blind), 1 = after 2 (mute), 2 = after 3 (gone).")]
    public DialogueScriptableObject[] exitByLoop;

    [Header("Exit circle")]
    public Color circleColor = Palette.CandleGold;
    [Tooltip("Tiles across.")]
    public float circleSize = 3f;

    [Header("Daylight outside")]
    public Color ambient = new Color32(190, 182, 165, 255);
    public Color sunColor = new Color32(255, 240, 215, 255);
    public float sunIntensity = 1.3f;
    public Color skyColor = new Color32(150, 185, 215, 255);
    public float brightenTime = 2.5f;

    PlayerMovement player;
    bool started;

    void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        if (boss == null)
        {
            BossAI ai = FindAnyObjectByType<BossAI>();
            if (ai != null) boss = ai.GetComponent<CombatUnit>();
        }
        if (boss == null) { Debug.LogWarning("BossVictory: no boss found.", this); return; }
        boss.OnDied += OnBossDied;
    }

    void OnDestroy()
    {
        if (boss != null) boss.OnDied -= OnBossDied;
    }

    void OnBossDied(CombatUnit _)
    {
        if (started) return;
        started = true;
        StartCoroutine(Play());
    }

    [ContextMenu("Test: play the escape now (Play mode)")]
    void TestEscape()
    {
        if (Application.isPlaying && !started) { started = true; StartCoroutine(Play()); }
    }

    IEnumerator Play()
    {
        // Let the fight wrap up and the boss door sink
        while (GameState.Current == GameMode.Combat) yield return null;
        yield return new WaitForSeconds(1.2f);
        while (GameState.DialogueOpen) yield return null;

        GameState.Current = GameMode.Cutscene;
        player.StopWalking();
        while (player.IsStepping) yield return null;

        // 1. 'Finally!'
        yield return Talk(win);

        // 2. The way out opens where the boss fell
        float ts = player.tileSize;
        Vector3 centre = TileMath.Snap(boss.transform.position, ts);
        centre.y = TileMath.FloorY(boss.transform.position) + 0.03f;
        GameObject circle = MakeCircle(centre, out SpriteRenderer ring, out Light glow);
        CameraController cam = CameraController.Main;
        if (cam != null) cam.FocusOn(circle.transform);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            glow.intensity = 3f * t;
            yield return null;
        }
        yield return new WaitForSeconds(0.4f);
        if (cam != null) cam.FocusOn(player.transform);

        // Walk into it
        GameState.Current = GameMode.Exploring;
        float reach = circleSize * 0.5f * ts;
        while (true)
        {
            Vector3 d = player.CurrentTile - centre;
            d.y = 0f;
            if (d.magnitude <= reach && !player.IsStepping && !GameState.DialogueOpen) break;
            yield return null;
        }

        // 3. It flares, then black
        GameState.Current = GameMode.Cutscene;
        player.StopWalking();
        if (cam != null) cam.Shake(0.12f, 0.8f);
        ScreenFade.FadeOut(0.8f);
        Color startColor = ring != null ? ring.color : Color.white;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
        {
            glow.intensity = Mathf.Lerp(3f, 12f, t);
            if (ring != null) ring.color = Color.Lerp(startColor, Color.white, t * 0.7f);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.3f);

        // Outside
        if (cam != null) cam.FocusOn(player.transform);
        player.TeleportParty(outsideSpot);
        GameState.InDungeon = false;   // no more looping
        Destroy(circle);
        yield return new WaitForSecondsRealtime(0.8f);

        // 4. Daylight: fade in from black while the world brightens
        ScreenFade.FadeIn(1.6f, 0.2f);
        yield return Brighten();

        // 5. What it cost
        yield return Talk(StoryDialogue.Pick(exitByLoop, LoopManager.LoopsUsed - 1));
        yield return new WaitForSeconds(0.6f);

        // 6. The goddess has the last word
        if (GodScene.Instance != null) GodScene.Instance.ShowEnding();
    }

    // From the dungeon's darkness to a bright day
    IEnumerator Brighten()
    {
        Light sun = null;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null)
        {
            sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.intensity = 0f;
        }
        Camera camera = Camera.main;

        Color ambient0 = RenderSettings.ambientLight, fog0 = RenderSettings.fogColor;
        Color sun0 = sun.color, sky0 = camera != null ? camera.backgroundColor : skyColor;
        float sunI0 = sun.intensity;
        if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Flat)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        if (camera != null && camera.clearFlags == CameraClearFlags.Skybox) camera.clearFlags = CameraClearFlags.SolidColor;

        yield return Tween.Over(brightenTime, k =>
        {
            RenderSettings.ambientLight = Color.Lerp(ambient0, ambient, k);
            RenderSettings.fogColor = Color.Lerp(fog0, skyColor, k);
            sun.color = Color.Lerp(sun0, sunColor, k);
            sun.intensity = Mathf.Lerp(sunI0, sunIntensity, k);
            if (camera != null) camera.backgroundColor = Color.Lerp(sky0, skyColor, k);
        }, unscaled: true, smooth: true);
        RenderSettings.fog = false;
    }

    static IEnumerator Talk(DialogueScriptableObject lines)
    {
        if (lines == null || DialogueBox.Instance == null) yield break;
        yield return StoryDialogue.Play(lines);
        yield return new WaitForSeconds(0.2f);
    }

    // The trap chest's magic circle art, lying flat, golden this time
    GameObject MakeCircle(Vector3 centre, out SpriteRenderer ring, out Light glow)
    {
        var go = new GameObject("Exit Circle");
        go.transform.position = centre;
        ring = null;

        Sprite[] loop = trapChest != null ? trapChest.circleLoopFrames : null;
        if (loop != null && loop.Length > 0)
        {
            ring = TrapChest.AddRing(go.transform, loop[0], circleSize * player.tileSize, trapChest.circleMaterial);
            ring.color = circleColor;
            if (trapChest.circleOpenFrames != null && trapChest.circleOpenFrames.Length > 0) ring.sprite = trapChest.circleOpenFrames[0];
            StartCoroutine(Animate(ring));
        }

        glow = TrapChest.AddGlow(go.transform, circleColor, circleSize);
        return go;
    }

    // Opens once, then keeps cycling its last frames so it stays alive while you walk over
    IEnumerator Animate(SpriteRenderer sr)
    {
        yield return trapChest.PlayCircleFrames(sr);

        Sprite[] loop = trapChest.circleLoopFrames;
        float wait = 1.5f / Mathf.Max(trapChest.circleFramesPerSecond, 1f);
        int tail = Mathf.Min(3, loop.Length);
        for (int i = 0; sr != null; i++)
        {
            sr.sprite = loop[loop.Length - tail + i % tail];
            yield return new WaitForSeconds(wait);
        }
    }
}
