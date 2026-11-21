using UnityEngine;

// Makes a torch or candle light waver a little. Each light gets its own rhythm.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Tooltip("How far the brightness swings, as a share of its normal brightness.")]
    [Range(0f, 0.6f)] public float amount = 0.18f;
    public float speed = 6f;

    Light lamp;
    float baseIntensity, seed;

    void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;
        lamp.intensity = baseIntensity * (1f + n * amount);
    }
}
