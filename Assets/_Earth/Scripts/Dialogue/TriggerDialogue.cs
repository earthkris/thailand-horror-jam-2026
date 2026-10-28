using UnityEngine;

public class TriggerDialogue : MonoBehaviour
{
    public DialogueScriptableObject dialogueSO;

    public void Start()
    {
        // StartDialogue() for testing only
        //StartDialogue();
    }
    public void StartDialogue()
    {
        if (!dialogueSO) return;

        DialogueBox.Instance.StartDialogue(dialogueSO);
    }
}
