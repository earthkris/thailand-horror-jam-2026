using UnityEngine;

public class TriggerDialogue : MonoBehaviour
{
    public DialogueScriptableObject dialogueSO;
    public void StartDialogue()
    {
        Debug.Log("Start Dialogue");
        if (!dialogueSO) return;

        DialogueBox.Instance.StartDialogue(dialogueSO);
    }
}
