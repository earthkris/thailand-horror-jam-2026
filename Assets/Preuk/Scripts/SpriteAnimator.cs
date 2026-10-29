using UnityEngine;

// Plays walk/idle frames based on how this object moves, plus an optional attack swing.
// Put it on the child object that has the SpriteRenderer.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimator : MonoBehaviour
{
    [Header("Frames (the first frame is also the idle pose)")]
    public Sprite[] downFrames;   // walking toward the camera
    public Sprite[] upFrames;     // walking away from the camera
    public Sprite[] sideFrames;   // walking sideways

    [Header("Settings")]
    [Tooltip("Tick this if the side frames are drawn facing right. Untick if they face left.")]
    public bool sideFramesFaceRight = false;
    public float framesPerSecond = 10f;
    [Tooltip("How long to wait after stopping before showing the idle pose. Prevents flicker between tiles.")]
    public float stopDelay = 0.1f;

    [Header("Attack (optional, played once by PlayAttack)")]
    public Sprite[] attackDownFrames;
    public Sprite[] attackUpFrames;
    [Tooltip("Drawn facing the same way as the walking side frames.")]
    public Sprite[] attackSideFrames;
    public float attackFramesPerSecond = 10f;
    [Tooltip("Extra time the last frame (the slash) stays on screen.")]
    public float attackHoldLastFrame = 0.15f;

    enum Facing { Down, Up, Left, Right }

    SpriteRenderer sr;
    Vector3 lastPos;
    Facing facing = Facing.Down;
    float animTime;    // how long we've been walking (picks the frame)
    float stillTime;   // how long we've been standing still

    bool attacking;
    float attackTime;
    Sprite[] swingDown, swingUp, swingSide;   // this swing's own frames (a special move can bring them)
    float holdFacingUntil;   // real time; until then moving doesn't turn us (a lunge sliding back, a hit shake)

    // ---------- Attack (called by CombatTargeting) ----------

    public bool HasAttackFrames => attackDownFrames != null && attackDownFrames.Length > 0;
    public bool IsAttacking => attacking;

    // Which way we face: (0,-1) toward the camera, (0,1) away, (-1,0) left, (1,0) right
    public Vector2Int FacingDirection =>
        facing == Facing.Down ? Vector2Int.down :
        facing == Facing.Up ? Vector2Int.up :
        facing == Facing.Left ? Vector2Int.left : Vector2Int.right;

    // True once the swing reaches its last frame (the slash) - a good moment to hit
    public bool OnLastAttackFrame
    {
        get
        {
            if (!attacking) return false;
            Sprite[] frames = GetAttackFrames();
            return frames != null && AttackFrameIndex(frames) >= frames.Length - 1;
        }
    }

    // Turn toward 'target' and swing once. Walking/idle resumes by itself afterwards,
    // and the character keeps facing the target.
    public void PlayAttack(Vector3 target) => PlayAttack(target, null, null, null);

    // Same, but with other frames for this one swing (e.g. Divine Smite's glowing sword).
    // Empty lists fall back to the normal attack frames.
    public void PlayAttack(Vector3 target, Sprite[] down, Sprite[] up, Sprite[] side)
    {
        swingDown = down;
        swingUp = up;
        swingSide = side;
        if (GetAttackFrames() == null || GetAttackFrames().Length == 0) return;
        FaceToward(target - transform.position);
        attacking = true;
        attackTime = 0f;
    }

    public void FaceToward(Vector3 direction)
    {
        if (new Vector2(direction.x, direction.z).sqrMagnitude < 0.0001f) return;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            facing = direction.x > 0 ? Facing.Right : Facing.Left;
        else
            facing = direction.z > 0 ? Facing.Up : Facing.Down;
    }

    // Moving in the next 'seconds' won't turn the character or play the walk.
    // Lunges and hit shakes call this, so sliding back home doesn't count as walking away.
    public void HoldFacing(float seconds) =>
        holdFacingUntil = Mathf.Max(holdFacingUntil, Time.unscaledTime + seconds);

    bool FacingHeld => Time.unscaledTime < holdFacingUntil;

    // ---------- Every frame ----------

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.flipX = false;   // we mirror with scale instead (works with any material)
        lastPos = transform.position;
    }

    // LateUpdate runs after all Update() calls, so movement for this frame is already done
    void LateUpdate()
    {
        // 1. How far did we move since last frame? (ignore height)
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        bool movedThisFrame = new Vector2(delta.x, delta.z).sqrMagnitude > 0.000001f;

        // 2. Attacking: play the swing once. The lunge moves us, but must not change facing.
        if (attacking)
        {
            Sprite[] swing = GetAttackFrames();
            attackTime += Time.deltaTime;
            int i = AttackFrameIndex(swing);
            float length = swing.Length / attackFramesPerSecond + attackHoldLastFrame;
            // Keep holding the slash while the lunge is still sliding us back home
            if (attackTime < length || movedThisFrame || FacingHeld)
            {
                sr.sprite = swing[Mathf.Min(i, swing.Length - 1)];
                ApplyMirror();
                return;
            }
            attacking = false;   // done: fall through to the idle pose
            stillTime = stopDelay;
        }

        // 3. Update facing direction from the movement
        if (movedThisFrame && !FacingHeld)   // a lunge or a hit shake isn't walking
        {
            stillTime = 0f;
            FaceToward(delta);
        }
        else
        {
            stillTime += Time.deltaTime;
        }

        bool walking = stillTime < stopDelay;

        // 4. Pick the frame list for this direction
        Sprite[] frames = GetFrames();
        if (frames == null || frames.Length == 0) return;

        // 5. Walking = cycle through frames. Idle = first frame.
        if (walking)
        {
            animTime += Time.deltaTime;
            int index = (int)(animTime * framesPerSecond) % frames.Length;
            sr.sprite = frames[index];
        }
        else
        {
            animTime = 0f;
            sr.sprite = frames[0];
        }

        ApplyMirror();
    }

    // Mirror the side frames when facing the other way.
    // Scale X = -1 mirrors the sprite AND its shadow, with any material.
    void ApplyMirror()
    {
        bool mirror = false;
        if (facing == Facing.Left) mirror = sideFramesFaceRight;    // drawn facing right -> mirror to face left
        if (facing == Facing.Right) mirror = !sideFramesFaceRight;   // drawn facing left -> mirror to face right

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (mirror ? -1f : 1f);
        transform.localScale = scale;
    }

    int AttackFrameIndex(Sprite[] frames) => Mathf.Min((int)(attackTime * attackFramesPerSecond), frames.Length - 1);

    Sprite[] GetFrames()
    {
        switch (facing)
        {
            case Facing.Up: return upFrames;
            case Facing.Down: return downFrames;
            default: return sideFrames;
        }
    }

    // Falls back to the down swing if a direction has no frames
    Sprite[] GetAttackFrames()
    {
        Sprite[] f;
        switch (facing)
        {
            case Facing.Up: f = Pick(swingUp, attackUpFrames); break;
            case Facing.Down: f = Pick(swingDown, attackDownFrames); break;
            default: f = Pick(swingSide, attackSideFrames); break;
        }
        return f != null && f.Length > 0 ? f : Pick(swingDown, attackDownFrames);
    }

    // The special move's frames if it has some, otherwise the normal ones
    static Sprite[] Pick(Sprite[] special, Sprite[] normal) =>
        special != null && special.Length > 0 ? special : normal;
}
