using UnityEngine;

// Keeps a prop (the sword) behind the character's back, whichever way it faces,
// and hides it while the attack swing plays (the swing draws its own sword).
// Set up:  Kairos (HoverProb: Prob = Sword)
//            SwordSlot   <- this script. Moves round the character as it turns.
//              Sword     <- the sprite. HoverProb makes it float up and down.
// The slot only moves; the turning and mirroring happen on the Sword itself,
// so HoverProb's floating always stays straight up and down.
public class BackSlot : MonoBehaviour
{
    [Tooltip("The character's sprite animator. Found automatically.")]
    public SpriteAnimator body;
    [Tooltip("The sprite inside this slot. Empty = the first child.")]
    public Transform prop;

    [Header("Where it sits (x = right, y = up, z = away from the camera)")]
    [Tooltip("Facing the camera: behind the body, hilt over the right shoulder.")]
    public Vector3 downOffset = new Vector3(0.26f, 0.22f, 0.12f);
    public float downAngle = 180f;
    [Tooltip("Facing away: the back faces us, so the sword is in front, across the back.")]
    public Vector3 upOffset = new Vector3(-0.12f, 0.05f, -0.12f);
    public float upAngle = 270f;
    [Tooltip("Facing right: the back is on the left. Facing left is mirrored for you.")]
    public Vector3 sideOffset = new Vector3(-0.3f, 0.05f, 0.08f);
    public float sideAngle = 270f;
    [Tooltip("Seconds to swing round when the character turns.")]
    public float turnTime = 0.12f;

    [Header("Attack")]
    public bool hideWhileAttacking = true;

    Renderer[] renderers;
    Vector3 velocity;
    float angle, angleVelocity;

    void Start()
    {
        if (prop == null && transform.childCount > 0) prop = transform.GetChild(0);
        if (body == null && transform.parent != null) body = transform.parent.GetComponentInChildren<SpriteAnimator>();
        renderers = GetComponentsInChildren<Renderer>(true);

        Target(out Vector3 p, out angle, out _);
        transform.localPosition = p;
    }

    void LateUpdate()
    {
        if (body == null || prop == null) return;

        Target(out Vector3 goal, out float goalAngle, out bool mirror);
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, goal, ref velocity, turnTime);
        angle = Mathf.SmoothDampAngle(angle, goalAngle, ref angleVelocity, turnTime);

        prop.localRotation = Quaternion.Euler(0f, 0f, angle);
        Vector3 s = prop.localScale;
        s.x = Mathf.Abs(s.x) * (mirror ? -1f : 1f);
        prop.localScale = s;

        bool show = !(hideWhileAttacking && body.IsAttacking);
        foreach (Renderer r in renderers)
            if (r != null) r.enabled = show;
    }

    void Target(out Vector3 p, out float a, out bool mirror)
    {
        mirror = false;
        Vector2Int f = body != null ? body.FacingDirection : Vector2Int.down;
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
