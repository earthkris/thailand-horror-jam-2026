using UnityEngine;

// Makes a prop float beside a character, like the Friend's holy book or the Player's sword.
// Put it on the prop: a child of the character's ROOT (next to "Capsule", not inside it).
//   Normal:           floats where you placed it, bobbing gently (the book).
//   Stay Behind Back: circles around the character to stay behind its back, whichever way
//                     it faces (the sword). Facing the camera it hides behind the body,
//                     facing away it's in front, facing sideways it's on the far side.
// The bob runs on seconds, not frames, so it looks the same at any frame rate (web too).
public class FloatingProp : MonoBehaviour
{
    [Tooltip("The character's sprite animator. Found automatically.")]
    public SpriteAnimator body;

    [Header("Bob")]
    [Tooltip("How far it drifts up and down, in tiles.")]
    public float bobHeight = 0.08f;
    [Tooltip("Seconds for one full up-and-down.")]
    public float bobPeriod = 3f;
    [Tooltip("Gentle rocking, in degrees.")]
    public float rockAngle = 4f;
    [Tooltip("0-1. Offsets the bob so two props don't move in step.")]
    [Range(0f, 1f)] public float phase = 0f;

    [Header("Stay behind the back")]
    public bool stayBehindBack = false;
    [Tooltip("Facing the camera. x = right, y = up, z = away from the camera (behind the body).")]
    public Vector3 downOffset = new Vector3(0.12f, 0.05f, 0.12f);
    public float downAngle = 180f;
    [Tooltip("Facing away from the camera: the back faces us, so the prop is in front.")]
    public Vector3 upOffset = new Vector3(-0.12f, 0.05f, -0.12f);
    public float upAngle = 270f;
    [Tooltip("Facing right: the back is on the left. Facing left is mirrored for you.")]
    public Vector3 sideOffset = new Vector3(-0.3f, 0.05f, 0.08f);
    public float sideAngle = 270f;
    [Tooltip("Seconds to swing round when the character turns.")]
    public float turnTime = 0.12f;

    [Header("Extras")]
    [Tooltip("Hide while the attack animation plays (the swing draws its own sword).")]
    public bool hideWhileAttacking = false;
    [Tooltip("When the character is down, the prop drops to the floor and stops floating.")]
    public bool dropWhenDown = true;

    SpriteRenderer sr;
    CombatUnit unit;
    Vector3 home;          // where it floats in Normal mode (its starting spot)
    float homeAngle;
    Vector3 baseScale;
    Vector3 pos, posVel;   // smoothed position before the bob
    float angle, angleVel;
    bool started;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        unit = GetComponentInParent<CombatUnit>();
        if (body == null && transform.parent != null) body = transform.parent.GetComponentInChildren<SpriteAnimator>();

        home = transform.localPosition;
        homeAngle = transform.localEulerAngles.z;
        baseScale = transform.localScale;
        Target(out pos, out angle, out _);
        started = true;
    }

    void LateUpdate()
    {
        if (!started) return;

        // Where it wants to be
        Target(out Vector3 goalPos, out float goalAngle, out bool mirror);
        bool down = dropWhenDown && unit != null && !unit.IsAlive;
        if (down) goalPos.y = -0.85f;   // the root is at chest height, the floor ~1 tile below

        // Glide there (a quick swing round when turning)
        pos = Vector3.SmoothDamp(pos, goalPos, ref posVel, down ? 0.25f : turnTime);
        angle = Mathf.SmoothDampAngle(angle, goalAngle, ref angleVel, turnTime);

        // Bob and rock
        float t = (Time.time / Mathf.Max(bobPeriod, 0.01f) + phase) * Mathf.PI * 2f;
        float bob = down ? 0f : Mathf.Sin(t) * bobHeight;
        float rock = down ? 0f : Mathf.Sin(t * 0.5f) * rockAngle;

        transform.localPosition = pos + Vector3.up * bob;
        transform.localRotation = Quaternion.Euler(0f, 0f, angle + rock);
        Vector3 scale = baseScale;
        scale.x = Mathf.Abs(scale.x) * (mirror ? -1f : 1f);
        transform.localScale = scale;

        if (sr != null && hideWhileAttacking && body != null) sr.enabled = !body.IsAttacking;
    }

    void Target(out Vector3 p, out float a, out bool mirror)
    {
        mirror = false;
        if (!stayBehindBack || body == null) { p = home; a = homeAngle; return; }

        Vector2Int f = body.FacingDirection;
        if (f == Vector2Int.up) { p = upOffset; a = upAngle; }
        else if (f == Vector2Int.right) { p = sideOffset; a = sideAngle; }
        else if (f == Vector2Int.left)
        {
            p = new Vector3(-sideOffset.x, sideOffset.y, sideOffset.z);   // mirror image of facing right
            a = -sideAngle;
            mirror = true;
        }
        else { p = downOffset; a = downAngle; }
    }
}
