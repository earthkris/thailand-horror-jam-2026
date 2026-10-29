using UnityEngine;

// Places a prop around a character depending on which way it faces:
//   Kairos's sword sits behind his back, Lucius's book stays at his hand in front of him.
// Set up:  Character (HoverProb: Prob = the prop)
//            Slot object    <- this script. Moves round the character as it turns.
//              Prop sprite  <- HoverProb floats it up and down inside the slot.
// The slot only moves; turning and mirroring happen on the prop itself,
// so HoverProb's float always stays straight up and down.
public class PropSlot : MonoBehaviour
{
    [Tooltip("The character's sprite animator. Found automatically.")]
    public SpriteAnimator body;
    [Tooltip("The sprite inside this slot. Empty = the first child.")]
    public Transform prop;

    [Header("Where it sits (x = right, y = up, z = away from the camera)")]
    [Tooltip("Facing the camera.")]
    public Vector3 downOffset = new Vector3(0.26f, 0.22f, 0.12f);
    public float downAngle = 180f;
    [Tooltip("Facing away from the camera.")]
    public Vector3 upOffset = new Vector3(-0.12f, 0.05f, -0.12f);
    public float upAngle = 270f;
    [Tooltip("Facing right. Facing left is mirrored for you.")]
    public Vector3 sideOffset = new Vector3(-0.3f, 0.05f, 0.08f);
    public float sideAngle = 270f;
    [Tooltip("Seconds to swing round when the character turns.")]
    public float turnTime = 0.12f;

    [Header("Attack")]
    [Tooltip("Hide while the attack swing plays (the swing draws its own sword).")]
    public bool hideWhileAttacking = false;

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

        if (hideWhileAttacking)
        {
            bool show = !body.IsAttacking;
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = show;
        }
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
