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

    [Tooltip("Testing: 1, 2 or 3 shows that stage now. -1 = use the real loop count.")]
    public int forceStage = -1;

    SpriteRenderer scribble;
    float timer;
    int frame;

    void Start()
    {
        int stage = forceStage >= 0 ? forceStage : LoopManager.LoopsUsed;
        if (stage <= 0 || frames == null || frames.Length == 0) { enabled = false; return; }
        if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        if (target == null) { enabled = false; return; }

        Cover c = covers[Mathf.Clamp(stage - 1, 0, covers.Length - 1)];
        var go = new GameObject("Loop Scribble", typeof(SpriteRenderer));
        go.transform.SetParent(target.transform, false);   // mirrors with him when he turns
        go.transform.localPosition = new Vector3(c.offset.x, c.offset.y, -0.03f);
        float frameSize = frames[0].rect.width / frames[0].pixelsPerUnit;
        go.transform.localScale = new Vector3(c.size.x / frameSize, c.size.y / frameSize, 1f);

        scribble = go.GetComponent<SpriteRenderer>();
        scribble.sharedMaterial = target.sharedMaterial;
        scribble.sortingLayerID = target.sortingLayerID;
        scribble.sortingOrder = target.sortingOrder + 2;
        scribble.sprite = frames[0];
    }

    void Update()
    {
        if (scribble == null) return;
        scribble.enabled = target.enabled;
        timer += Time.deltaTime;
        if (timer < 1f / Mathf.Max(framesPerSecond, 1f)) return;
        timer = 0f;
        if (frames.Length > 1) frame = (frame + Random.Range(1, frames.Length)) % frames.Length;   // never the same frame twice
        scribble.sprite = frames[frame];
        scribble.flipX = Random.value < 0.5f;
    }
}
