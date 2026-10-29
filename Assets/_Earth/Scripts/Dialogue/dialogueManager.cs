using System.Collections.Generic;
using UnityEngine;

public class dialogueManager : MonoBehaviour
{
    public static dialogueManager Instance {get; private set;}
    [SerializeField] List<TriggerDialogue> triggerDialogue; 

    void Awake()
    {
        Instance = this;
    }

    public void TriggerDialogue(int dialogueIndex)
    {
        triggerDialogue[dialogueIndex].StartDialogue();
    }
}
