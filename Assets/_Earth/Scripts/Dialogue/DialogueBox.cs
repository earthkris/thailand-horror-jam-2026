using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueBox : MonoBehaviour
{
    [SerializeField]
    private GameObject content;

    [SerializeField]
    private TextMeshProUGUI dialogueText;

    [SerializeField]
    private TextMeshProUGUI speakerNameText;
    
    [SerializeField]
    private Image speakerImage;

    [SerializeField]
    private float textSpeed = 20f;

    private DialogueScriptableObject activeDialogue;
    private string fullText;
    private int dialogueIndex;

    public bool IsTyping { get; private set; }

    public bool IsContentOpen
    {
        get => content.activeInHierarchy;
    }

    public static DialogueBox Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        CloseContent();
    }

    public void OpenContent()
    {
        content.SetActive(true);
    }

    public void CloseContent()
    {
        content.SetActive(false);
    }

    public void NextDialogue()
    {
        if (!activeDialogue)
        {
            return;
        }

        if (IsTyping)
        {
            FastForward();
            return;
        }

        // if all the lines have been display. the dialogue box will close.
        if(dialogueIndex >= activeDialogue.lines.Count)
        {
            CloseContent();
            return;
        }
        
        var line = activeDialogue.lines[dialogueIndex];
        speakerNameText.text = line.name;
        fullText = line.text;
        speakerImage.sprite = line.spriteImage;
        IsTyping = true;
        StartCoroutine(TyperWritterEffectCoroutine());

        dialogueIndex++;
    }
    public void FastForward()
    {
        StopAllCoroutines();
        dialogueText.text = fullText;
        IsTyping = false;
    }

    public void StartDialogue(DialogueScriptableObject dialogueSO)
    {
        activeDialogue = dialogueSO;
        dialogueIndex = 0;
        OpenContent();
        NextDialogue();
    }

    private IEnumerator TyperWritterEffectCoroutine()
    {
        dialogueText.text = string.Empty;
        float delay = 1f/ textSpeed;
        foreach (char ch in fullText)
        {
            dialogueText.text += ch;
            yield return new WaitForSeconds(delay);
        }

        IsTyping = false; 
    }
}
