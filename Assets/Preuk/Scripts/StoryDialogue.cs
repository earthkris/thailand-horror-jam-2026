using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Story scripts (cutscenes, zones, the tutorial) talk through Earth's dialogue box with these.
public static class StoryDialogue
{
    // Shows the lines and waits until the player has clicked through all of them.
    // Does nothing if there are no lines (or no dialogue box in the scene).
    public static IEnumerator Play(DialogueScriptableObject lines)
    {
        if (lines == null || DialogueBox.Instance == null) yield break;
        DialogueBox.Instance.StartDialogue(lines);
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.IsContentOpen) yield return null;
    }

    // Every dialogue box moves on the same way: E (Earth's Interactor), a left click, Space or Enter.
    // Before, only the goddess scene took clicks; now the dialogue in the world does too.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddInput()
    {
        if (Object.FindAnyObjectByType<DialogueInput>() != null) return;
        var go = new GameObject("Dialogue Input");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<DialogueInput>();
    }

    // From a list with one dialogue per loop: the one at 'index', or the last one when the list is shorter.
    // Null if the list is empty.
    public static DialogueScriptableObject Pick(DialogueScriptableObject[] byLoop, int index) =>
        byLoop != null && byLoop.Length > 0 ? byLoop[Mathf.Clamp(index, 0, byLoop.Length - 1)] : null;
}

// Click / Space / Enter moves any open dialogue on (E is already handled by Earth's Interactor).
public class DialogueInput : MonoBehaviour
{
    int openedFrame = -1;

    void Update()
    {
        DialogueBox box = DialogueBox.Instance;
        if (box == null || !box.IsContentOpen) { openedFrame = -1; return; }
        if (openedFrame < 0) { openedFrame = Time.frameCount; return; }   // not the click that opened it
        if (PauseMenu.BlocksInput) return;

        Mouse m = Mouse.current;
        Keyboard kb = Keyboard.current;
        bool next = (m != null && m.leftButton.wasPressedThisFrame) ||
                    (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame));
        if (next) box.NextDialogue();
    }
}
