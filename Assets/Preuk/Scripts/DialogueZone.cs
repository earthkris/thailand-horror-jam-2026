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
        if (!ZoneBox.Contains(transform.position, size, player.CurrentTile)) return;
        played = true;
        DialogueScriptableObject lines = StoryDialogue.Pick(dialogueByLoop, LoopManager.LoopsUsed);
        if (lines != null)
        {
            player.StopWalking();
            DialogueBox.Instance.StartDialogue(lines);
        }
    }

    void OnDrawGizmos() => ZoneBox.DrawGizmo(transform.position, size);
}
