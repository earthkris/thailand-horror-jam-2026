using UnityEngine;

// What each loop costs (storyboard 9-14): black scribbles over the friend (Lucius).
//   after the 1st loop: over his eyes
//   after the 2nd loop: his whole face
//   after the 3rd loop: his whole body
// The three frames of BLACKs-Sheet play in a jittery loop, so it looks scratched over him.
// Put it on Lucius. It reads LoopManager.LoopsUsed when the scene (re)starts.
public class LoopScribble : MonoBehaviour
{
    [Tooltip("BLACKs-Sheet_0, _1, _2.")]
    public Sprite[] frames;
    public float framesPerSecond = 9f;
    [Tooltip("The friend's sprite. Empty = the first SpriteRenderer in children.")]
    public SpriteRenderer target;

    [System.Serializable]
    public struct Cover
    {
        [Tooltip("From his feet, in world units (x = toward the way he faces).")]
        public Vector2 offset;
        [Tooltip("Size of one scribble frame, in world units.")]
        public Vector2 size;
    }

    [Tooltip("Element 0 = after the 1st loop (eyes), 1 = after the 2nd (face), 2 = after the 3rd (whole body).")]
    public Cover[] covers =
    {
        new Cover { offset = new Vector2(0.12f, 1.12f), size = new Vector2(0.6f, 0.6f) },
        new Cover { offset = new Vector2(0.06f, 1.08f), size = new Vector2(0.95f, 0.95f) },
        new Cover { offset = new Vector2(0.02f, 0.68f), size = new Vector2(1.4f, 3.1f) },
    };

    [Tooltip("Facing the camera (or away), his face is in the middle and wider than from the side: "
           + "the eye and face scribbles are centred and at least this wide (world units).")]
    public float frontMinWidth = 1.05f;

    [Tooltip("Testing: 1, 2 or 3 shows that stage now. -1 = use the real loop count.")]
    public int forceStage = -1;

    SpriteRenderer scribble;
    SpriteAnimator body;
    Cover cover;
    bool wholeBody;
    float frameSize;
    float timer;
    int frame;

    void Start()
    {
        int stage = forceStage >= 0 ? forceStage : LoopManager.LoopsUsed;
        if (stage <= 0 || frames == null || frames.Length == 0) { enabled = false; return; }
        if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        if (target == null) { enabled = false; return; }

        int index = Mathf.Clamp(stage - 1, 0, covers.Length - 1);
        cover = covers[index];
        wholeBody = index >= 2;
        body = target.GetComponent<SpriteAnimator>();
        var go = new GameObject("Loop Scribble", typeof(SpriteRenderer));
        go.transform.SetParent(target.transform, false);   // mirrors with him when he turns
        frameSize = frames[0].rect.width / frames[0].pixelsPerUnit;

        scribble = go.GetComponent<SpriteRenderer>();
        scribble.sharedMaterial = target.sharedMaterial;
        scribble.sortingLayerID = target.sortingLayerID;
        scribble.sortingOrder = target.sortingOrder + 2;
        scribble.sprite = frames[0];
        Place();
    }

    // From the side his face is toward the way he looks (the offset); facing the camera or away it's centred and wider
    void Place()
    {
        bool side = body == null || body.FacingDirection.x != 0;
        Vector2 offset = cover.offset, size = cover.size;
        if (!side)
        {
            offset.x = 0f;
            if (!wholeBody) size.x = Mathf.Max(size.x, frontMinWidth);
        }
        scribble.transform.localPosition = new Vector3(offset.x, offset.y, -0.03f);
        scribble.transform.localScale = new Vector3(size.x / frameSize, size.y / frameSize, 1f);
    }

    void Update()
    {
        if (scribble == null) return;
        scribble.enabled = target.enabled;
        Place();
        timer += Time.deltaTime;
        if (timer < 1f / Mathf.Max(framesPerSecond, 1f)) return;
        timer = 0f;
        if (frames.Length > 1) frame = (frame + Random.Range(1, frames.Length)) % frames.Length;   // never the same frame twice
        scribble.sprite = frames[frame];
        scribble.flipX = Random.value < 0.5f;
    }
}
