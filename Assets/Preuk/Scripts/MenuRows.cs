using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Fills a list of menu rows from a template button. Shared by the battle's ACTION / BONUS ACTION
// pop-up (ChoiceMenu) and the pause menu's item lists (PauseMenu).
// A row needs a child text called "Label"; a child text "Detail" and a child Image "Icon" are optional.
public static class MenuRows
{
    // One row per option: old rows are reused, new ones are copied from 'template', spare ones are hidden.
    // 'onHover' gets the description of the row under the mouse.
    public static void Fill(List<Button> rows, Button template, List<ChoiceMenu.Option> options,
                            float disabledLabelAlpha, float disabledDetailAlpha, System.Action<string> onHover)
    {
        while (rows.Count < options.Count)
            rows.Add(Object.Instantiate(template, template.transform.parent));

        for (int i = 0; i < rows.Count; i++)
        {
            Button b = rows[i];
            bool used = i < options.Count;
            b.gameObject.SetActive(used);
            if (!used) continue;

            ChoiceMenu.Option o = options[i];
            SetText(b.transform, "Label", o.label, o.enabled ? 1f : disabledLabelAlpha);
            SetText(b.transform, "Detail", o.detail, o.enabled ? 1f : disabledDetailAlpha);
            SetIcon(b.transform, o.icon, o.enabled ? 1f : disabledLabelAlpha);
            b.interactable = o.enabled;

            b.onClick.RemoveAllListeners();
            System.Action pick = o.onPick;
            b.onClick.AddListener(() => pick?.Invoke());

            MenuHover hover = b.GetComponent<MenuHover>();
            if (hover == null) hover = b.gameObject.AddComponent<MenuHover>();
            string description = o.description;
            hover.onEnter = () => onHover(description);
        }
    }

    // Sets the text of a button's child (if it has one by that name)
    public static void SetText(Transform button, string child, string text, float alpha = 1f)
    {
        Transform t = button.Find(child);
        if (t == null || !t.TryGetComponent(out TMP_Text tmp)) return;
        tmp.text = text;
        tmp.alpha = alpha;
    }

    static void SetIcon(Transform button, Sprite icon, float alpha)
    {
        Transform t = button.Find("Icon");
        if (t == null || !t.TryGetComponent(out Image img)) return;
        img.sprite = icon;
        img.enabled = icon != null;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    // Turns a button on/off and fades its label to match
    public static void SetButton(Button b, bool on, float disabledLabelAlpha)
    {
        b.interactable = on;
        TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.alpha = on ? 1f : disabledLabelAlpha;
    }
}
