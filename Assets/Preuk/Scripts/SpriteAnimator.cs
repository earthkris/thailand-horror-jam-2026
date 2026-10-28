using UnityEngine;

// Plays walk/idle frames based on how this object moves.
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

    enum Facing { Down, Up, Left, Right }

    SpriteRenderer sr;
    Vector3 lastPos;
    Facing facing = Facing.Down;
    float animTime;    // how long we've been walking (picks the frame)
    float stillTime;   // how long we've been standing still

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        lastPos = transform.position;
    }

    // LateUpdate runs after all Update() calls, so movement for this frame is already done
    void LateUpdate()
    {
        // 1. How far did we move since last frame? (ignore height)
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        bool movedThisFrame = new Vector2(delta.x, delta.z).sqrMagnitude > 0.000001f;

        // 2. Update facing direction from the movement
        if (movedThisFrame)
        {
            stillTime = 0f;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.z))
                facing = delta.x > 0 ? Facing.Right : Facing.Left;
            else
                facing = delta.z > 0 ? Facing.Up : Facing.Down;   // +Z is "into the screen"
        }
        else
        {
            stillTime += Time.deltaTime;
        }

        bool walking = stillTime < stopDelay;

        // 3. Pick the frame list for this direction
        Sprite[] frames = GetFrames();
        if (frames == null || frames.Length == 0) return;

        // 4. Walking = cycle through frames. Idle = first frame.
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

        // 5. Mirror the side frames when walking the other way
        if (facing == Facing.Left)
            sr.flipX = sideFramesFaceRight;     // drawn facing right -> flip to face left
        else if (facing == Facing.Right)
            sr.flipX = !sideFramesFaceRight;    // drawn facing left -> flip to face right
        else
            sr.flipX = false;                   // up/down frames are never flipped
    }

    Sprite[] GetFrames()
    {
        switch (facing)
        {
            case Facing.Up: return upFrames;
            case Facing.Down: return downFrames;
            default: return sideFrames;
        }
    }
}