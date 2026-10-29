using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Connects the battle UI you designed in the Canvas to TurnManager.
// It only fills in text, recolors the initiative rows and turns buttons on/off.
// Everything else (layout, sprites, fonts, sizes) is up to you in the editor.
// Put this on the Canvas itself (not on a panel it hides).
public class TurnPanel : MonoBehaviour
{
    [Header("Logic")]
    public TurnManager turns;

    [Header("Panels (hidden outside battle)")]
    public GameObject initiativePanel;
    public GameObject actionPanel;

    [Header("Initiative List")]
    [Tooltip("One row of the list. It is copied once per unit; the original stays hidden.")]
    public TextMeshProUGUI rowTemplate;
    public string currentMarker = ">  ";

    [Header("Current Turn")]
    public TextMeshProUGUI turnTitle;
    public TextMeshProUGUI turnInfo;

    [Header("Buttons")]
    public Button moveButton;
    [FormerlySerializedAs("attackButton")]
    public Button actionButton;
    [Tooltip("Optional. Leave empty if you don't have a Use Item button yet.")]
    public Button itemButton;
    public Button endTurnButton;

    [Header("Choosing a target")]
    [Tooltip("Shown instead of the battle UI while you pick a target. Holds the X button.")]
    public GameObject aimPanel;
    [Tooltip("The X. Cancels the action and brings the battle UI back.")]
    public Button cancelAimButton;
    [Tooltip("Optional. Says what you're aiming, e.g. 'Holy Fireball - choose a target'.")]
    public TextMeshProUGUI aimText;
    public string aimFormat = "{0}  -  choose a target";

    [Header("Menus")]
    [Tooltip("The pop-up list ACTION and USE ITEM open.")]
    public ChoiceMenu menu;
    [Tooltip("The party's bag. Leave empty to find it automatically.")]
    public PartyInventory inventory;

    [Header("Colors")]
    public Color partyColor = new Color32(217, 203, 176, 255);    // bone
    public Color enemyColor = new Color32(194, 59, 59, 255);      // blood
    public Color currentColor = new Color32(232, 190, 99, 255);   // candle gold
    public Color downColor = new Color32(74, 68, 60, 255);        // ash
    public Color unseenColor = new Color32(90, 82, 72, 255);      // something in the dark

    [Header("Unseen Enemies (in the dark)")]
    public string unseenName = "???";
    public string unseenTurnTitle = "Something stirs in the dark";
    [Range(0f, 1f)] public float disabledLabelAlpha = 0.35f;

    readonly List<TextMeshProUGUI> rows = new List<TextMeshProUGUI>();
    CombatTargeting targeting;
    FontStyles rowStyle;
    bool ready;

    // Runs when you add the component: find the TurnManager for you
    void Reset()
    {
        turns = FindAnyObjectByType<TurnManager>();
    }

    void Awake()
    {
        if (turns == null) turns = FindAnyObjectByType<TurnManager>();   // works as a prefab in any scene
        ready = CheckReferences();
        if (!ready) return;

        rowStyle = rowTemplate.fontStyle;
        rowTemplate.gameObject.SetActive(false);

        if (inventory == null) inventory = FindAnyObjectByType<PartyInventory>();
        targeting = turns.GetComponent<CombatTargeting>();
        if (menu != null) menu.Close();
        if (cancelAimButton != null) cancelAimButton.onClick.AddListener(CancelAim);

        moveButton.onClick.AddListener(turns.ChooseMove);
        actionButton.onClick.AddListener(OpenActions);
        if (itemButton != null) itemButton.onClick.AddListener(OpenItems);
        endTurnButton.onClick.AddListener(turns.EndTurnButton);
    }

    void OnEnable()
    {
        if (!ready) return;
        turns.OnChanged += Refresh;
        FogOfWar.OnChanged += Refresh;   // an enemy stepped into or out of the light
        Refresh();
    }

    void OnDisable()
    {
        if (!ready) return;
        turns.OnChanged -= Refresh;
        FogOfWar.OnChanged -= Refresh;
    }

    // ---------- Redraw ----------

    void Refresh()
    {
        bool show = turns.InBattle && turns.Current != null;
        bool aiming = show && targeting != null && targeting.IsAiming;
        initiativePanel.SetActive(show && !aiming);   // out of the way while you pick a target
        actionPanel.SetActive(show && !aiming);
        if (aimPanel != null) aimPanel.SetActive(aiming);
        if (aiming && aimText != null) aimText.text = string.Format(aimFormat, targeting.AimingName);

        // Close the menu when it's no longer our turn, or our action is used up
        if (menu != null && menu.IsOpen && (!show || !turns.IsPartyTurn || turns.HasActed)) menu.Close();
        if (!show) return;

        // One copy of the row template per unit
        while (rows.Count < turns.Order.Count)
        {
            TextMeshProUGUI row = Instantiate(rowTemplate, rowTemplate.transform.parent);
            row.gameObject.SetActive(true);
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            TextMeshProUGUI row = rows[i];
            bool used = i < turns.Order.Count;
            row.gameObject.SetActive(used);
            if (!used) continue;

            TurnManager.Combatant c = turns.Order[i];
            bool isCurrent = c.unit == turns.Current;
            bool seen = FogOfWar.CanSee(c.unit);

            string marker = isCurrent ? currentMarker : "";
            row.text = seen ? $"{marker}{c.initiative}   {c.unit.unitName}"
                            : $"{marker}?    {unseenName}";   // you know something is there, nothing more
            row.fontStyle = rowStyle;
            if (isCurrent) row.fontStyle |= FontStyles.Bold;

            if (!c.unit.IsAlive)
            {
                row.color = downColor;
                row.fontStyle |= FontStyles.Strikethrough;
            }
            else if (!seen) row.color = unseenColor;
            else if (isCurrent) row.color = currentColor;
            else row.color = c.unit.team == Team.Party ? partyColor : enemyColor;
        }

        // Whose turn
        CombatUnit u = turns.Current;
        if (FogOfWar.CanSee(u))
        {
            turnTitle.text = $"{u.unitName}'s turn";
            turnTitle.color = u.team == Team.Party ? partyColor : enemyColor;
            if (turnInfo != null)
                turnInfo.text = $"Round {turns.Round}     HP {u.CurrentHP}/{u.maxHP}     AC {u.ArmorClass}\nMove {turns.MovesLeft}/{u.MoveTiles}"
                              + (u.BlessDie > 0 ? $"     Blessed +1d{u.BlessDie}" : "") + u.StatusText;
        }
        else
        {
            turnTitle.text = unseenTurnTitle;
            turnTitle.color = enemyColor;
            if (turnInfo != null) turnInfo.text = $"Round {turns.Round}";
        }

        // Buttons only work on your own turn, and only once each
        bool mine = turns.IsPartyTurn;
        SetButton(moveButton, mine && !turns.HasMoved);
        SetButton(actionButton, mine && !turns.HasActed);
        if (itemButton != null) SetButton(itemButton, mine && !turns.HasActed);   // items use your action too
        SetButton(endTurnButton, mine);
    }

    // ---------- Menus ----------

    void OpenActions()
    {
        if (menu == null || !turns.IsPartyTurn || turns.HasActed) return;
        CombatUnit u = turns.Current;

        var options = new List<ChoiceMenu.Option>();
        foreach (CombatAction a in u.actions)
        {
            if (a == null) continue;
            int left = turns.UsesLeft(u, a);
            string uses = left < 0 ? "" : $"   {left}/{a.usesPerBattle}";
            string range = a.range > 1 ? $"{a.range} tiles" : a.effect == ActionEffect.Damage ? "melee" : "touch";
            CombatAction picked = a;
            options.Add(new ChoiceMenu.Option
            {
                icon = a.typeIcon,
                label = a.displayName,
                detail = $"{a.AmountText}   {range}{uses}",
                description = a.description,
                enabled = left != 0,
                onPick = () => { menu.Close(); turns.ChooseAction(picked); }
            });
        }
        menu.Open($"{u.unitName}  —  ACTIONS", options);
    }

    void OpenItems()
    {
        if (menu == null || !turns.IsPartyTurn || turns.HasActed) return;

        var options = new List<ChoiceMenu.Option>();
        if (inventory != null)
            foreach (PartyInventory.ItemStack s in inventory.items)
            {
                if (s.item == null || s.count <= 0) continue;
                ItemData picked = s.item;
                options.Add(new ChoiceMenu.Option
                {
                    label = s.item.displayName,
                    detail = $"{s.item.RangeText}   x{s.count}",
                    description = s.item.description,
                    enabled = true,
                    onPick = () => { menu.Close(); turns.ChooseItem(picked); }
                });
            }
        menu.Open("ITEMS", options);
    }

    void CancelAim()
    {
        if (targeting != null && targeting.IsAiming) targeting.Cancel();
    }

    void SetButton(Button b, bool on)
    {
        b.interactable = on;
        TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.alpha = on ? 1f : disabledLabelAlpha;
    }

    // Tells you exactly which slot is still empty instead of crashing
    bool CheckReferences()
    {
        var missing = new List<string>();
        if (turns == null) missing.Add("Turns");
        if (initiativePanel == null) missing.Add("Initiative Panel");
        if (actionPanel == null) missing.Add("Action Panel");
        if (rowTemplate == null) missing.Add("Row Template");
        if (turnTitle == null) missing.Add("Turn Title");
        if (moveButton == null) missing.Add("Move Button");
        if (actionButton == null) missing.Add("Action Button");
        if (endTurnButton == null) missing.Add("End Turn Button");

        if (missing.Count == 0) return true;
        Debug.LogError($"TurnPanel on '{name}' is missing: {string.Join(", ", missing)}", this);
        return false;
    }
}
