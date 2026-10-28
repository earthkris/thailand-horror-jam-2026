using UnityEngine;

// OPTIONAL. Put on a light to give it its own reveal distance for the fog of war,
// e.g. a small candle reveals 2 tiles, a big brazier 6.
// Lights without this use FogOfWar's Reveal Radius.
// This only changes what you can SEE; the light's glow still uses its normal Range.
[RequireComponent(typeof(Light))]
public class LightReveal : MonoBehaviour
{
    [Tooltip("Enemies within this many tiles of this light are visible.")]
    public float radius = 3f;
}
