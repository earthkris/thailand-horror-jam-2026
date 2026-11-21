using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A pop-up list of choices (used for ACTION and BONUS ACTION).
// The look is all in the Hierarchy: restyle the panel, the Option template and the Back button freely.
// The Option template needs a child text called "Label"; a child text called "Detail" is optional,
// and so is a child Image called "Icon" (hidden when an option has no icon).
// Esc or right-click closes it.
public class ChoiceMenu : MonoBehaviour
{
    public TextMeshProUGUI title;
    [Tooltip("One option. It's copied for every choice; the original stays hidden.")]
    public Button optionTemplate;
    [Tooltip("Optional. Shows the description of the option under the mouse.")]
    public TextMeshProUGUI description;
    public Button backButton;
    [Tooltip("Shown when there's nothing to choose.")]
    public string emptyText = "Nothing here.";
    [Range(0f, 1f)] public float disabledLabelAlpha = 0.3f;

    public struct Option
    {
        public Sprite icon;         // small picture in front of the label, e.g. the attack type
        public string label;        // big text, e.g. "Sword Strike"
        public string detail;       // small text on the right, e.g. "1d8+3"
        public string description;  // shown when hovered
        public bool enabled;
        public System.Action onPick;
    }

    readonly List<Button> buttons = new List<Button>();
    bool ready;

    public bool IsOpen => gameObject.activeSelf;

    void Setup()
    {
        if (ready) return;
        ready = true;
        optionTemplate.gameObject.SetActive(false);
        if (backButton != null) backButton.onClick.AddListener(Close);
    }

    public void Open(string heading, List<Option> options)
    {
        Setup();
        gameObject.SetActive(true);
        if (title != null) title.text = heading;

        // One button per option (old ones are reused). Hovering one shows its description.
        MenuRows.Fill(buttons, optionTemplate, options, disabledLabelAlpha, disabledLabelAlpha,
                      desc => { if (description != null) description.text = desc; });

        if (description != null)
            description.text = options.Count == 0 ? emptyText : options[0].description;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (PauseMenu.CancelPressed) Close();
    }
}
