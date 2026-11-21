using UnityEngine;

// The lights in the scene, looked up twice a second and shared by everyone who needs them
// (FogOfWar, every character's FakeShadow), instead of each searching the scene by itself.
// Entries can be destroyed lights for a moment after a scene change: check for null.
public static class SceneLights
{
    static Light[] lights;
    static float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        lights = null;
        nextRefresh = 0f;
    }

    public static Light[] All
    {
        get
        {
            if (lights == null || Time.time >= nextRefresh)   // torches can be lit, lights can spawn
            {
                lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                nextRefresh = Time.time + 0.5f;
            }
            return lights;
        }
    }

    // A switched-on point or spot light (torches, candles, carried lights). Not the sun.
    public static bool IsLamp(Light light) =>
        light != null && light.isActiveAndEnabled && (light.type == LightType.Point || light.type == LightType.Spot);

    // A new point light under 'parent', dark until its intensity is raised (spell glows, magic circles)
    public static Light NewGlow(string name, Transform parent, Color color, float range)
    {
        var go = new GameObject(name, typeof(Light));
        go.transform.SetParent(parent, false);
        var light = go.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = 0f;
        return light;
    }
}
