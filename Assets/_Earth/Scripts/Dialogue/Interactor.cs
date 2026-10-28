using UnityEngine;
using UnityEngine.InputSystem;

public class Interactor : MonoBehaviour
{
    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {    
            Interact();
        }
    }

    private void Interact()
    {
        if (DialogueBox.Instance.IsContentOpen)
        {
            DialogueBox.Instance.NextDialogue();
        }
    }
}
