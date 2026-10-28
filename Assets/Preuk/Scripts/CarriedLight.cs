using UnityEngine;
using UnityEngine.Rendering.Universal;

// Put this on a light that a character carries (child of the character).
// The light casts shadows from everything EXCEPT the character carrying it.
// Otherwise their own sprite would block the light and throw a giant shadow.
[RequireComponent(typeof(Light))]
public class CarriedLight : MonoBehaviour
{
    [Tooltip("The carrier's sprite. Leave empty to find it on the parent automatically.")]
    public SpriteRenderer holderSprite;

    [Tooltip("A rendering layer only this carrier uses. Give each carrier a DIFFERENT number.")]
    [Range(1, 7)] public int holderLayer = 1;

    [Tooltip("Hard shadows are cheaper and suit pixel art.")]
    public LightShadows shadowType = LightShadows.Hard;

    void Awake()
    {
        // Find the carrier's sprite if it wasn't set
        if (holderSprite == null && transform.parent != null)
            holderSprite = transform.parent.GetComponentInChildren<SpriteRenderer>();

        uint holderBit = 1u << holderLayer;   // e.g. layer 1 -> binary 10

        // 1. Put the carrier's sprite ONLY on its own layer (take it off "Default")
        if (holderSprite != null)
            holderSprite.renderingLayerMask = holderBit;

        // 2. This light casts shadows from every layer EXCEPT the carrier's
        Light light = GetComponent<Light>();
        light.shadows = shadowType;

        var data = light.GetUniversalAdditionalLightData();
        data.customShadowLayers = true;
        data.shadowRenderingLayers = ~holderBit;   // ~ flips the bits: "everything but this one"
    }
}