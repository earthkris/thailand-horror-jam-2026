using System.Collections.Generic;
using UnityEngine;

// The party can only see enemies that stand in light:
// close enough to a switched-on point/spot light, with no wall or rock between them and it.
// Enemies in the dark are invisible (sprite + shadows hidden).
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

    static readonly HashSet<CombatUnit> hidden = new HashSet<CombatUnit>();

    // Fired when any enemy steps into or out of the light (the UI redraws)
    public static event System.Action OnChanged;

    // Can the party see this unit? (Party members are always visible.)
    public static bool CanSee(CombatUnit u) => u != null && !hidden.Contains(u);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        hidden.Clear();
        OnChanged = null;
    }

    readonly List<CombatUnit> enemies = new List<CombatUnit>();
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
            bool seen = IsLit(u);
            bool wasSeen = !hidden.Contains(u);
            if (seen != wasSeen)
            {
                if (seen) hidden.Remove(u);
                else hidden.Add(u);
                changed = true;
            }
            SetVisible(u, seen);
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

    // Shows or hides everything that draws the unit (sprite and fake shadows).
    // Lights the enemy carries stay on, so you'd see their glow.
    static void SetVisible(CombatUnit u, bool visible)
    {
        foreach (Renderer r in u.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    // Turning the fog off shows every enemy again (handy for testing)
    void OnDisable()
    {
        foreach (CombatUnit u in enemies)
            if (u != null) SetVisible(u, true);
        bool hadHidden = hidden.Count > 0;
        hidden.Clear();
        if (hadHidden) OnChanged?.Invoke();
    }
}