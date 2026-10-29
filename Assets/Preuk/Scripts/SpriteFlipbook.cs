using UnityEngine;

// Plays a list of sprites on a SpriteRenderer, always facing the camera.
// Used for spell effects: the Holy Fireball (loops while flying) and its explosion (plays once).
//   Travel Direction: set by whoever moves it. The sprite's right side (+X) then points
//                     the way it flies on screen (the fireball sheet is drawn flying right).
//   Glow:             optional Light that fades out together with the sprite.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlipbook : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 12f;
    [Tooltip("On = repeat forever (projectile). Off = play once.")]
    public bool loop = true;

    [Header("Play once only")]
    [Tooltip("Fade the last frame out over this many seconds.")]
    public float fadeOutTime = 0.2f;
    public bool destroyWhenDone = true;

    [Header("Optional")]
    public Light glow;

    [HideInInspector] public Vector3 travelDirection;

    SpriteRenderer sr;
    float time;
    float glowIntensity;
    Color baseColor;

    // How long a play-once effect lasts (frames + fade)
    public float Duration => frames.Length / Mathf.Max(framesPerSecond, 0.01f) + fadeOutTime;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        if (glow != null) glowIntensity = glow.intensity;
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        time += Time.deltaTime;
        int index = (int)(time * framesPerSecond);

        if (loop)
        {
            sr.sprite = frames[index % frames.Length];
            return;
        }

        sr.sprite = frames[Mathf.Min(index, frames.Length - 1)];

        // Fade after the last frame has shown
        float shown = frames.Length / Mathf.Max(framesPerSecond, 0.01f);
        float fade = fadeOutTime > 0f ? Mathf.Clamp01((time - shown) / fadeOutTime) : (time >= shown ? 1f : 0f);
        Color c = baseColor;
        c.a *= 1f - fade;
        sr.color = c;
        if (glow != null) glow.intensity = glowIntensity * (1f - fade);

        if (fade >= 1f && destroyWhenDone) Destroy(gameObject);
    }

    // Face the camera (after the camera has moved this frame)
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Quaternion rot = cam.transform.rotation;
        if (travelDirection.sqrMagnitude > 0.0001f)
        {
            // Angle of the flight path as seen on screen
            float x = Vector3.Dot(travelDirection, cam.transform.right);
            float y = Vector3.Dot(travelDirection, cam.transform.up);
            rot *= Quaternion.Euler(0f, 0f, Mathf.Atan2(y, x) * Mathf.Rad2Deg);
        }
        transform.rotation = rot;
    }
}
