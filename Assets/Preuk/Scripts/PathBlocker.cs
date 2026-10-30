using System.Collections;
using UnityEngine;

// Storyboard 7: on the first run the party has to meet the first boss, so the other way is closed.
// Step into the box and Lucius stops you ("My gut is telling me this isn't the way..."),
// then Kairos walks back out. After the first loop the way is open (that's the new route).
// The box is drawn in yellow in the Scene view.
public class PathBlocker : MonoBehaviour
{
    [Tooltip("Tiles inside this box (centred on this object) are off limits. X = width, Y = depth (world Z).")]
    public Vector2 size = new Vector2(10f, 24f);
    [Tooltip("Blocked while fewer loops than this have been used. 1 = only the first run.")]
    public int blockUntilLoop = 1;
    [Tooltip("The first time: 'My gut is telling me this isn't the way...'")]
    public DialogueScriptableObject firstTime;
    [Tooltip("Every time after (empty = the first one again).")]
    public DialogueScriptableObject again;

    PlayerMovement player;
    Vector3 lastOutside;
    bool busy, warned;

    void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        if (player != null) lastOutside = player.CurrentTile;
    }

    void Update()
    {
        if (player == null || busy || LoopManager.LoopsUsed >= blockUntilLoop) return;
        if (GameState.Current != GameMode.Exploring) return;

        Vector3 tile = player.CurrentTile;   // the tile we stand on, or are stepping onto
        if (!Inside(tile))
        {
            lastOutside = tile;
            return;
        }
        busy = true;
        StartCoroutine(TurnBack());
    }

    IEnumerator TurnBack()
    {
        GameState.Current = GameMode.Cutscene;   // no more steps, monsters wait
        player.StopWalking();
        while (player.IsStepping) yield return null;

        DialogueScriptableObject lines = warned && again != null ? again : firstTime;
        warned = true;
        if (lines != null && DialogueBox.Instance != null)
        {
            DialogueBox.Instance.StartDialogue(lines);
            yield return null;
            while (DialogueBox.Instance != null && DialogueBox.Instance.IsContentOpen) yield return null;
        }
        yield return null;

        // Walk back to the last tile outside the box (keys are ignored on the way)
        player.WalkToScripted(lastOutside);
        while (player.IsWalking) yield return null;

        GameState.Current = GameMode.Exploring;
        busy = false;
    }

    bool Inside(Vector3 p)
    {
        Vector3 c = transform.position;
        return Mathf.Abs(p.x - c.x) <= size.x * 0.5f + 0.01f && Mathf.Abs(p.z - c.z) <= size.y * 0.5f + 0.01f;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
        Vector3 box = new Vector3(size.x, 0.2f, size.y);
        Gizmos.DrawCube(transform.position + Vector3.up * 0.1f, box);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, box);
    }
}
