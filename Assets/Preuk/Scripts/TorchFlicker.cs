using UnityEngine;

// Makes a light flicker like a flame. Put it on any Point Light (torch, candle, brazier).
// Uses Perlin noise: smooth random wobble instead of harsh on/off blinking.
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [Tooltip("How much the brightness wobbles. 0 = steady, 0.3 = lively fire.")]
    [Range(0f, 1f)] public float intensityFlicker = 0.25f;
    [Tooltip("How much the light's reach wobbles.")]
    [Range(0f, 0.5f)] public float rangeFlicker = 0.08f;
    [Tooltip("How fast it flickers.")]
    public float speed = 6f;

    Light flame;
    float baseIntensity;
    float baseRange;
    float seed;   // so two torches never flicker in sync

    void Awake()
    {
        flame = GetComponent<Light>();
        baseIntensity = flame.intensity;
        baseRange = flame.range;
        seed = Random.Range(0f, 100f);
    }

    void Update()
    {
        float t = Time.time * speed + seed;
        float n1 = Mathf.PerlinNoise(t, 0f) * 2f - 1f;          // -1..1
        float n2 = Mathf.PerlinNoise(0f, t * 0.7f) * 2f - 1f;

        flame.intensity = baseIntensity * (1f + n1 * intensityFlicker);
        flame.range = baseRange * (1f + n2 * rangeFlicker);
    }
}
