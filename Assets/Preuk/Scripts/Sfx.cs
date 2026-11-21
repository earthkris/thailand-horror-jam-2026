using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Every sound in the game goes through here. Nothing to set up in the scene: it makes itself on Play
// and lives through loops (scene reloads).
//
// Sounds live in Assets/Preuk/Resources/Sfx/<cue>/ (one folder per sound, any number of variants;
// a random one plays each time). Swap a sound by dropping new files in its folder.
// The clips are Kenney's CC0 packs (RPG Audio, Impact Sounds, Interface Sounds, Music Jingles),
// some pitched down and given an echo with ffmpeg to fit the dungeon.
//
// Most sounds play by themselves from events the game already has (hits, heals, dice, steps,
// dialogue, buttons). Scripts can also call Sfx.Play("cue").
//
// The background (dungeon drone, forest wind, heartbeat in a fight) is built in code from sine
// waves and noise, so it needs no files.
public class Sfx : MonoBehaviour
{
    static Sfx instance;

    // 0..1, saved between sessions
    public static float Volume
    {
        get => PlayerPrefs.GetFloat("sfxVolume", 0.75f);
        set => PlayerPrefs.SetFloat("sfxVolume", Mathf.Clamp01(value));
    }

    public static float AmbienceVolume
    {
        get => PlayerPrefs.GetFloat("ambienceVolume", 0.75f);
        set => PlayerPrefs.SetFloat("ambienceVolume", Mathf.Clamp01(value));
    }

    readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
    readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();
    readonly Dictionary<string, float> lastTime = new Dictionary<string, float>();
    readonly List<AudioSource> pool = new List<AudioSource>();
    int next;

    AudioSource drone, wind, heart;
    float droneLevel, windLevel, heartLevel;
    float nextDistant;
    bool bossFight;

    PlayerMovement player;
    bool dialogueWasOpen, wasTyping;
    float nextTypeTick;
    GameObject hovered;

    // ================= Playing =================

    // Play a sound once. pitchJitter: a little random pitch so repeats don't sound copied.
    public static void Play(string cue, float volume = 1f, float pitch = 1f, float pitchJitter = 0.06f)
    {
        if (instance == null) return;
        instance.PlayOne(cue, volume, pitch, pitchJitter);
    }

    // The same, but not again within 'gap' seconds (hits on many targets at once, footsteps of a crowd)
    public static void PlayLimited(string cue, float gap, float volume = 1f, float pitch = 1f)
    {
        if (instance == null) return;
        if (instance.lastTime.TryGetValue(cue, out float t) && Time.unscaledTime - t < gap) return;
        instance.PlayOne(cue, volume, pitch, 0.06f);
    }

    // A footstep for whoever walks, quieter the further they are from the camera's target
    public static void Step(Transform who, float volume = 0.35f)
    {
        if (instance == null || who == null) return;
        float v = volume;
        if (instance.player != null)
            v *= Mathf.Clamp01(1.2f - Vector3.Distance(who.position, instance.player.transform.position) / 12f);
        if (v <= 0.02f) return;
        PlayLimited(GameState.InDungeon ? "step_stone" : "step_grass", 0.06f, v);
    }

    // The sound a combat action makes as it starts. 'sound' on the action wins; otherwise by what it does.
    public static void ForAction(CombatAction a)
    {
        if (a == null) return;
        if (!string.IsNullOrEmpty(a.sound)) { Play(a.sound); return; }
        switch (a.effect)
        {
            case ActionEffect.Heal: Play("cast", 0.7f, 1.1f); break;
            case ActionEffect.Buff: Play("buff", 0.8f); break;
            case ActionEffect.Help: Play("bag", 0.8f); break;
            default:
                if (a.range <= 1) Play("swing", 0.9f);
                else Play("cast", 0.8f);
                break;
        }
    }

    // A bigger moment in a fight (boss stomps, door slams)
    public static void Boom(float volume = 1f) => Play("stomp", volume, 1f, 0.1f);

    void PlayOne(string cue, float volume, float pitch, float pitchJitter)
    {
        AudioClip clip = Pick(cue);
        if (clip == null) return;
        AudioSource s = pool[next];
        next = (next + 1) % pool.Count;
        s.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        s.PlayOneShot(clip, Mathf.Clamp01(volume) * Volume);
        lastTime[cue] = Time.unscaledTime;
    }

    AudioClip Pick(string cue)
    {
        if (!clips.TryGetValue(cue, out AudioClip[] set))
        {
            set = Resources.LoadAll<AudioClip>("Sfx/" + cue);
            if (set.Length == 0) Debug.LogWarning($"Sfx: no sounds in Resources/Sfx/{cue}");
            clips[cue] = set;
        }
        if (set.Length == 0) return null;
        int last = lastPick.TryGetValue(cue, out int l) ? l : -1;
        int i = Random.Range(0, set.Length);
        if (set.Length > 1 && i == last) i = (i + 1) % set.Length;   // never the same one twice in a row
        lastPick[cue] = i;
        return set[i];
    }

    // ================= Setup =================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("Sfx");
        DontDestroyOnLoad(go);
        go.AddComponent<Sfx>();
    }

    void Awake()
    {
        instance = this;
        for (int i = 0; i < 16; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            pool.Add(s);
        }
        drone = Loop(AmbientSynth.Drone());
        wind = Loop(AmbientSynth.Wind());
        heart = Loop(AmbientSynth.Heartbeat());
        nextDistant = Time.unscaledTime + 6f;
    }

    AudioSource Loop(AudioClip clip)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.volume = 0f;
        s.spatialBlend = 0f;
        s.Play();
        return s;
    }

    void OnEnable()
    {
        CombatUnit.OnAnyDamaged += OnDamaged;
        CombatUnit.OnAnyHealed += OnHealed;
        EnemyEncounter.OnCombatStarted += OnCombatStarted;
        TurnManager.OnPartyFallen += OnPartyFallen;
        SceneManager.sceneLoaded += OnSceneLoaded;
        HookPlayer();
    }

    void OnDisable()
    {
        CombatUnit.OnAnyDamaged -= OnDamaged;
        CombatUnit.OnAnyHealed -= OnHealed;
        EnemyEncounter.OnCombatStarted -= OnCombatStarted;
        TurnManager.OnPartyFallen -= OnPartyFallen;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (player != null) player.OnStepStarted -= OnPlayerStep;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        bossFight = false;
        HookPlayer();
    }

    void HookPlayer()
    {
        if (player != null) player.OnStepStarted -= OnPlayerStep;
        player = FindAnyObjectByType<PlayerMovement>();
        if (player != null) player.OnStepStarted += OnPlayerStep;
    }

    // ================= Listening to the game =================

    void OnPlayerStep(Vector3 _)
    {
        if (player != null) Step(player.transform, 0.3f);
    }

    void OnDamaged(CombatUnit u, int amount)
    {
        if (u == null) return;
        bool big = amount >= 8;
        if (u.team == Team.Party) PlayLimited(big ? "hit_heavy" : "hit", 0.05f, 1f, 0.85f);
        else PlayLimited(big ? "hit_heavy" : "hit", 0.05f, 0.9f);

        if (!u.IsAlive)
        {
            if (u.team == Team.Enemy) Play("monster_die", 0.9f, u.size > 1 ? 0.7f : 1f);
            else if (u.IsDowned && amount > 1) Play("party_down", 0.9f);
        }
    }

    void OnHealed(CombatUnit u, int amount)
    {
        if (amount > 0) PlayLimited("heal", 0.15f, 0.7f);
    }

    void OnCombatStarted(EnemyEncounter starter, CombatUnit touched)
    {
        bossFight = starter != null && starter.Unit != null && starter.Unit.size > 1;
        Play("combat_start", 0.9f, 1f, 0f);
    }

    void OnPartyFallen() => Play("defeat", 0.9f, 1f, 0f);

    // ================= Every frame =================

    void Update()
    {
        Dialogue();
        Buttons();
        Ambience();
    }

    // A page turn when a dialogue opens or moves on, soft ticks while the text types out
    void Dialogue()
    {
        DialogueBox box = DialogueBox.Instance;
        bool open = box != null && box.IsContentOpen;
        bool typing = open && box.IsTyping;
        if (open && typing && !wasTyping) Play("dialogue_next", 0.35f);
        if (typing && Time.unscaledTime >= nextTypeTick)
        {
            Play("dialogue_type", 0.12f, 1.1f, 0.15f);
            nextTypeTick = Time.unscaledTime + 0.075f;
        }
        if (dialogueWasOpen && !open) Play("dialogue_next", 0.25f, 0.85f);
        dialogueWasOpen = open;
        wasTyping = typing;
    }

    // Any uGUI button: a tick on hover, a click on press (no wiring per button)
    void Buttons()
    {
        EventSystem es = EventSystem.current;
        if (es == null || UnityEngine.InputSystem.Mouse.current == null) return;
        var mouse = UnityEngine.InputSystem.Mouse.current;

        GameObject now = null;
        if (es.IsPointerOverGameObject())
        {
            var data = new PointerEventData(es) { position = mouse.position.ReadValue() };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (RaycastResult r in hits)
            {
                Selectable sel = r.gameObject.GetComponentInParent<Selectable>();
                if (sel != null && sel.interactable) { now = sel.gameObject; break; }
                if (sel != null) break;
            }
        }
        if (now != null && now != hovered) Play("ui_hover", 0.35f, 1f, 0.05f);
        hovered = now;
        if (now != null && mouse.leftButton.wasPressedThisFrame) Play("ui_select", 0.6f);
    }

    // Drone in the dungeon, wind outside, a heartbeat under every fight (faster for a boss)
    void Ambience()
    {
        bool dungeon = GameState.InDungeon;
        bool fight = GameState.Current == GameMode.Combat;
        bool cutscene = GameState.Current == GameMode.Cutscene;
        float dt = Time.unscaledDeltaTime;

        droneLevel = Mathf.MoveTowards(droneLevel, dungeon ? (fight ? 0.45f : 0.6f) : 0f, dt * 0.4f);
        windLevel = Mathf.MoveTowards(windLevel, dungeon ? 0f : 0.45f, dt * 0.4f);
        heartLevel = Mathf.MoveTowards(heartLevel, fight ? 0.8f : 0f, dt * (fight ? 0.8f : 0.4f));
        float amb = AmbienceVolume * (cutscene ? 0.6f : 1f);
        drone.volume = droneLevel * amb;
        wind.volume = windLevel * amb;
        heart.volume = heartLevel * amb;
        heart.pitch = Mathf.MoveTowards(heart.pitch, bossFight ? 1.35f : 1f, dt * 0.3f);

        // Now and then: a drip or a far-off creak in the dark
        if (dungeon && !fight && Time.unscaledTime >= nextDistant)
        {
            Play(Random.value < 0.65f ? "drip" : "creak_far", Random.Range(0.15f, 0.35f) * AmbienceVolume, 1f, 0.15f);
            nextDistant = Time.unscaledTime + Random.Range(5f, 14f);
        }
    }
}

// The background sounds, made in code (no files). Each one loops without a click.
public static class AmbientSynth
{
    const int Rate = 22050;

    // Low, uneasy hum: a deep note and its fifth, slightly out of tune with each other, over rumbling noise
    public static AudioClip Drone()
    {
        const float seconds = 8f;
        int n = (int)(Rate * seconds);
        var data = new float[n];
        float[] noise = BrownNoise(n, 0.02f, 11);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            // whole cycles in 8 s, so the loop meets itself
            float s = 0.32f * Mathf.Sin(2f * Mathf.PI * 55f * t)
                    + 0.22f * Mathf.Sin(2f * Mathf.PI * 55.5f * t)
                    + 0.12f * Mathf.Sin(2f * Mathf.PI * 82.375f * t)
                    + 0.05f * Mathf.Sin(2f * Mathf.PI * 110.25f * t);
            float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / seconds);
            data[i] = (s * swell + noise[i] * 0.9f) * 0.45f;
        }
        return Make("Drone", data);
    }

    // Wind through trees: soft noise that rises and falls
    public static AudioClip Wind()
    {
        const float seconds = 10f;
        int n = (int)(Rate * seconds);
        float[] a = BrownNoise(n, 0.06f, 23);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float gust = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * t * 2f / seconds) + 0.15f * Mathf.Sin(2f * Mathf.PI * t * 3f / seconds + 1f);
            data[i] = a[i] * gust * 0.8f;
        }
        return Make("Wind", data);
    }

    // Lub-dub at 60 beats a minute (one second loop); played faster for a boss
    public static AudioClip Heartbeat()
    {
        int n = Rate;
        var data = new float[n];
        void Thump(float at, float strength)
        {
            int start = (int)(at * Rate);
            for (int i = 0; i < Rate / 4 && start + i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 22f) * Mathf.Min(1f, t * 400f);
                float f = 52f - 18f * t;   // the pitch sinks a little
                data[start + i] += strength * env * Mathf.Sin(2f * Mathf.PI * f * t);
            }
        }
        Thump(0f, 0.9f);
        Thump(0.27f, 0.6f);
        return Make("Heartbeat", data);
    }

    // Noise with most of its energy low down (a random walk, kept near zero). Crossfaded so it loops.
    static float[] BrownNoise(int n, float step, int seed)
    {
        var rng = new System.Random(seed);
        int fade = Rate / 2;
        var raw = new float[n + fade];
        float v = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            v += ((float)rng.NextDouble() * 2f - 1f) * step;
            v *= 0.995f;
            raw[i] = v;
        }
        var outp = new float[n];
        for (int i = 0; i < n; i++) outp[i] = raw[i];
        for (int i = 0; i < fade; i++)   // the extra tail blends into the start
        {
            float k = (float)i / fade;
            outp[i] = raw[n + i] * (1f - k) + raw[i] * k;
        }
        return outp;
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
