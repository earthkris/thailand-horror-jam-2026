using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A pop-up list of choices (used for ACTION and USE ITEM).
// The look is all in the Hierarchy: restyle the panel, the Option template and the Back button freely.
// The Option template needs a child text called "Label"; a child text called "Detail" is optional.
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

        // One button per option (reuse old ones)
        while (buttons.Count < options.Count)
        {
            Button b = Instantiate(optionTemplate, optionTemplate.transform.parent);
            buttons.Add(b);
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            Button b = buttons[i];
            bool used = i < options.Count;
            b.gameObject.SetActive(used);
            if (!used) continue;

            Option o = options[i];
            SetText(b.transform, "Label", o.label, o.enabled);
            SetText(b.transform, "Detail", o.detail, o.enabled);
            b.interactable = o.enabled;

            b.onClick.RemoveAllListeners();
            System.Action pick = o.onPick;
            b.onClick.AddListener(() => pick?.Invoke());

            MenuHover hover = b.GetComponent<MenuHover>();
            if (hover == null) hover = b.gameObject.AddComponent<MenuHover>();
            string desc = o.description;
            hover.onEnter = () => { if (description != null) description.text = desc; };
        }

        if (description != null)
            description.text = options.Count == 0 ? emptyText : options[0].description;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (PauseMenu.BlocksInput) return;   // the pause menu has the keys
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        if (esc || rightClick) Close();
    }

    void SetText(Transform button, string child, string text, bool enabled)
    {
        Transform t = button.Find(child);
        if (t == null || !t.TryGetComponent(out TMP_Text tmp)) return;
        tmp.text = text;
        tmp.alpha = enabled ? 1f : disabledLabelAlpha;
    }
}
