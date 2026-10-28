using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Fake ground shadows for 2D characters in a 3D scene.
// 1. Contact shadow: a small dark oval under the feet (always).
// 2. Light shadows: a dark copy of the sprite lying on the ground, pointing away from
//    each nearby point/spot light. If we carry our own light, it washes these out.
// Put this on the character's ROOT object (Player / Friend), not on the sprite child.
public class FakeShadows : MonoBehaviour
{
    [Tooltip("The character's sprite. Leave empty to find it automatically.")]
    public SpriteRenderer sprite;
    [Tooltip("World height of the ground.")]
    public float groundY = 0f;

    [Header("Contact shadow (under the feet)")]
    public bool contactShadow = true;
    public Color contactColor = new Color(0f, 0f, 0f, 0.45f);
    [Tooltip("Width of the oval in tiles.")]
    public float contactWidth = 0.7f;
    [Tooltip("Depth of the oval in tiles.")]
    public float contactDepth = 0.35f;

    [Header("Shadows from other lights")]
    public Color shadowColor = new Color(0f, 0f, 0f, 0.5f);
    [Tooltip("Shadow length (x sprite height) when the light is right next to us.")]
    public float maxLength = 1.5f;
    [Tooltip("Shadow length at the edge of the light's range.")]
    public float minLength = 0.5f;
    [Tooltip("How many lights can give us a shadow at the same time.")]
    public int maxShadows = 3;
    [Tooltip("How much of those shadows remains when we carry our own light. 0 = none, 1 = full.")]
    [Range(0f, 1f)] public float ownLightFill = 0f;

    readonly List<SpriteRenderer> shadows = new List<SpriteRenderer>();
    SpriteRenderer contact;
    Light[] lights;
    float nextLightRefresh;

    void Awake()
    {
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();

        // Light shadows: create once, reuse every frame
        for (int i = 0; i < maxShadows; i++)
        {
            SpriteRenderer sr = CreateShadowRenderer("FakeShadow " + i);
            sr.gameObject.SetActive(false);
            shadows.Add(sr);
        }

        // Contact shadow: a generated oval lying flat under the feet
        contact = CreateShadowRenderer("ContactShadow");
        contact.sprite = MakeOvalSprite(16);
        contact.color = contactColor;
        contact.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, face the sky
    }

    SpriteRenderer CreateShadowRenderer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();   // unlit sprite material by default
        sr.shadowCastingMode = ShadowCastingMode.Off;
        sr.receiveShadows = false;
        return sr;
    }

    void LateUpdate()
    {
        if (sprite == null) return;

        Vector3 feet = new Vector3(transform.position.x, groundY, transform.position.z);

        // 1. Contact shadow
        contact.gameObject.SetActive(contactShadow);
        if (contactShadow)
        {
            contact.transform.position = feet + Vector3.up * 0.005f;
            contact.transform.localScale = new Vector3(contactWidth, contactDepth, 1f);
            contact.color = contactColor;
        }

        // 2. Shadows from other lights
        if (lights == null || Time.time >= nextLightRefresh)   // look for lights twice a second
        {
            lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            nextLightRefresh = Time.time + 0.5f;
        }

        float strength = CarriesOwnLight() ? ownLightFill : 1f;
        int used = 0;

        if (strength > 0f)
        {
            foreach (Light light in lights)
            {
                if (used >= shadows.Count) break;
                if (light == null || !light.isActiveAndEnabled) continue;
                if (light.type != LightType.Point && light.type != LightType.Spot) continue;
                if (light.transform.IsChildOf(transform)) continue;   // our own light

                // Direction from the light to our feet, flat on the ground
                Vector3 toUs = feet - light.transform.position;
                toUs.y = 0f;
                float dist = toUs.magnitude;
                if (dist < 0.01f || dist > light.range) continue;

                float t = dist / light.range;   // 0 = next to the light, 1 = edge of range
                SpriteRenderer sr = shadows[used++];
                sr.gameObject.SetActive(true);
                sr.sprite = sprite.sprite;      // same animation frame

                Color c = shadowColor;
                c.a *= (1f - t) * strength;     // fade with distance (and our own light)
                sr.color = c;

                // Lie flat: face the sky, "head" points away from the light
                Vector3 away = toUs / dist;
                sr.transform.SetPositionAndRotation(feet + Vector3.up * 0.01f,
                                                    Quaternion.LookRotation(Vector3.down, away));

                float length = Mathf.Lerp(maxLength, minLength, t);
                float mirror = Mathf.Sign(sprite.transform.lossyScale.x);
                sr.transform.localScale = new Vector3(mirror, length, 1f);
            }
        }

        for (int i = used; i < shadows.Count; i++)
            shadows[i].gameObject.SetActive(false);
    }

    // True if one of our own children is a switched-on point/spot light
    bool CarriesOwnLight()
    {
        foreach (Light l in GetComponentsInChildren<Light>())
            if (l.isActiveAndEnabled && (l.type == LightType.Point || l.type == LightType.Spot))
                return true;
        return false;
    }

    // Builds a small pixel-art oval: dark in the middle, fading to the edge
    static Sprite MakeOvalSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;          // crisp pixels, matches the art
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);            // 0 = centre, 1 = edge
                float a = r >= 1f ? 0f : (r < 0.6f ? 1f : 0.5f);    // two-step pixel fade
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}