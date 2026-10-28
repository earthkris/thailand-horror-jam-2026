using UnityEngine;

public class TriggerDialogue : MonoBehaviour
{
    public DialogueScriptableObject dialogueSO;

    public void Start()
    {
        StartDialogue();
    }
    public void StartDialogue()
    {
        if (!dialogueSO) return;

        DialogueBox.Instance.StartDialogue(dialogueSO);
    }
}
