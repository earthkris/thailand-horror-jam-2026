using System.Collections.Generic;
using UnityEngine;

// The party can only properly see enemies that stand in light:
// close enough to a switched-on point/spot light, with no wall or rock between them and it.
// Enemies in the dark still show, but only as a dim shape (Dark Material): you can tell something
// is lurking there, but not what it is. Its name, HP and turn stay hidden ('???'),
// the camera doesn't follow it, and attacks on it have disadvantage (CombatTargeting).
// Enemies ALWAYS see the party: their AI never checks this.
// Works on every enemy automatically (any CombatUnit on the Enemy team). One per scene.
public class FogOfWar : MonoBehaviour
{
    [Tooltip("How many tiles from a light an enemy can be seen. Lights with a LightReveal component use their own number.")]
    public float revealRadius = 4f;
    [Tooltip("Things that block light (walls, rocks).")]
    public LayerMask lightBlockers;
    [Tooltip("Seconds between visibility checks.")]
    public float checkInterval = 0.1f;
    [Tooltip("Enemies in the dark are drawn with this: an unlit, dark-tinted sprite material, so they show as a lurking shape. Empty = hidden completely.")]
    public Material darkMaterial;

    static readonly HashSet<CombatUnit> hidden = new HashSet<CombatUnit>();

    // Fired when any enemy steps into or out of the light (the UI redraws)
    public static event System.Action OnChanged;

    // Can the party see who this is? (Party members are always visible.)
    public static bool CanSee(CombatUnit u) => u != null && !hidden.Contains(u);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        hidden.Clear();
        OnChanged = null;
    }

    readonly List<CombatUnit> enemies = new List<CombatUnit>();
    readonly Dictionary<CombatUnit, List<SpriteRenderer>> bodies = new Dictionary<CombatUnit, List<SpriteRenderer>>();
    readonly Dictionary<SpriteRenderer, Material> normalMaterial = new Dictionary<SpriteRenderer, Material>();
    Light[] lights;
    float nextRefresh;
    float nextCheck;

    // Runs when the component is first added: pre-fill the layer
    void Reset()
    {
        lightBlockers = LayerMask.GetMask("Obstacles");
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;

        // Look for lights and enemies twice a second (torches can be lit, enemies can spawn)
        if (lights == null || Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + 0.5f;
            lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            enemies.Clear();
            foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                if (u.team == Team.Enemy) enemies.Add(u);
        }

        bool changed = false;
        foreach (CombatUnit u in enemies)
        {
            if (u == null) continue;
            if (!u.IsAlive) { Show(u, false, false); continue; }   // dead monsters stay gone
            bool seen = IsLit(u);
            bool wasSeen = !hidden.Contains(u);
            if (seen != wasSeen)
            {
                if (seen) hidden.Remove(u);
                else hidden.Add(u);
                changed = true;
            }
            Show(u, true, seen);
        }

        if (changed) OnChanged?.Invoke();
    }

    bool IsLit(CombatUnit u)
    {
        Vector3 body = u.transform.position;   // the root sits at chest height

        foreach (Light light in lights)
        {
            if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f) continue;
            if (light.type != LightType.Point && light.type != LightType.Spot) continue;

            // Carrying a torch? Then you're lit, wherever you are.
            if (light.transform.IsChildOf(u.transform)) return true;

            // How far this light reveals (never past the light's own reach)
            float radius = light.TryGetComponent(out LightReveal custom) ? custom.radius : revealRadius;
            radius = Mathf.Min(radius * u.tileSize, light.range);

            Vector3 toUnit = body - light.transform.position;
            toUnit.y = 0f;                                    // measure along the floor, in tiles
            if (toUnit.magnitude > radius) continue;          // too far from this light
            toUnit = body - light.transform.position;         // full 3D direction for the checks below

            // Spotlights only light what's inside their cone
            if (light.type == LightType.Spot &&
                Vector3.Angle(light.transform.forward, toUnit) > light.spotAngle * 0.5f) continue;

            // A wall or rock between the light and us = we're in its shadow
            if (Physics.Linecast(light.transform.position, body, lightBlockers, QueryTriggerInteraction.Ignore))
                continue;

            return true;
        }
        return false;
    }

    // alive + lit:  everything drawn normally
    // alive + dark: only the body, as a dim shape (no shadows, no status effects)
    // dead:         nothing
    void Show(CombatUnit u, bool alive, bool lit)
    {
        List<SpriteRenderer> body = BodyOf(u);
        bool lurk = alive && !lit && darkMaterial != null;
        foreach (Renderer r in u.GetComponentsInChildren<Renderer>(true))
        {
            bool isBody = r is SpriteRenderer sr && body.Contains(sr);
            r.enabled = alive && (lit || (lurk && isBody));
        }
        foreach (SpriteRenderer sr in body)
        {
            if (sr == null) continue;
            if (lurk)
            {
                // Remember its normal look (again if something else, like a hit flash, swapped it back)
                if (sr.sharedMaterial != darkMaterial) { normalMaterial[sr] = sr.sharedMaterial; sr.sharedMaterial = darkMaterial; }
            }
            else if (sr.sharedMaterial == darkMaterial && normalMaterial.TryGetValue(sr, out Material m)) sr.sharedMaterial = m;
        }
    }

    // The sprites that make up the monster itself (the animated ones; not shadows or effects)
    List<SpriteRenderer> BodyOf(CombatUnit u)
    {
        if (bodies.TryGetValue(u, out List<SpriteRenderer> list)) return list;
        list = new List<SpriteRenderer>();
        if (u.TryGetComponent(out BossAI boss) && boss.body != null)
            list.AddRange(boss.body.GetComponentsInChildren<SpriteRenderer>(true));
        foreach (SpriteAnimator a in u.GetComponentsInChildren<SpriteAnimator>(true))
            if (a.TryGetComponent(out SpriteRenderer sr) && !list.Contains(sr)) list.Add(sr);
        if (list.Count == 0)
            foreach (SpriteRenderer sr in u.GetComponentsInChildren<SpriteRenderer>(true))
                if (!sr.name.Contains("Shadow")) { list.Add(sr); break; }
        bodies[u] = list;
        return list;
    }

    // Turning the fog off shows every enemy again (handy for testing)
    void OnDisable()
    {
        foreach (CombatUnit u in enemies)
            if (u != null && u.IsAlive) { hidden.Remove(u); Show(u, true, true); }
        bool hadHidden = hidden.Count > 0;
        hidden.Clear();
        if (hadHidden) OnChanged?.Invoke();
    }
}
