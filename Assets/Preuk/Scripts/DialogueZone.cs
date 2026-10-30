using UnityEngine;

// Plays a dialogue once, the first time Kairos walks into this box (yellow in the Scene view).
// Storyboard 10.2: the room full of wandering monsters. Pick a dialogue per loop, so Lucius can go mute.
public class DialogueZone : MonoBehaviour
{
    [Tooltip("Walking onto a tile inside this box starts it. X = width, Y = depth (world Z).")]
    public Vector2 size = new Vector2(8f, 8f);
    [Tooltip("Element 0 = first run, 1 = after the 1st loop, 2 = after the 2nd... The last one is reused.")]
    public DialogueScriptableObject[] dialogueByLoop;

    PlayerMovement player;
    bool played;

    void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
    }

    void Update()
    {
        if (played || player == null || !GameState.CanMove || DialogueBox.Instance == null) return;
        if (!Inside(player.CurrentTile)) return;
        played = true;
        if (dialogueByLoop == null || dialogueByLoop.Length == 0) return;
        DialogueScriptableObject lines = dialogueByLoop[Mathf.Clamp(LoopManager.LoopsUsed, 0, dialogueByLoop.Length - 1)];
        if (lines != null)
        {
            player.StopWalking();
            DialogueBox.Instance.StartDialogue(lines);
        }
    }

    bool Inside(Vector3 p)
    {
        Vector3 c = transform.position;
        return Mathf.Abs(p.x - c.x) <= size.x * 0.5f + 0.01f && Mathf.Abs(p.z - c.z) <= size.y * 0.5f + 0.01f;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(size.x, 0.2f, size.y));
    }
}
