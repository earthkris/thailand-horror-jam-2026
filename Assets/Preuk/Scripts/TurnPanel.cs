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
    [Tooltip("'Round 3', shown above the initiative list.")]
    public TextMeshProUGUI roundText;

    [Header("Icons (TMP sprite asset)")]
    [Tooltip("Holds the HP heart and the armor icon. Empty = plain 'HP' / 'AC' text.")]
    public TMP_SpriteAsset icons;
    public string hpIcon = "Heart";
    public string acIcon = "Armor";

    [Header("Buttons")]
    public Button moveButton;
    [FormerlySerializedAs("attackButton")]
    public Button actionButton;
    [Tooltip("The BONUS ACTION button: bonus moves (Heal, Shield of Faith) and items. Optional.")]
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
    [Tooltip("The pop-up list ACTION and BONUS ACTION open.")]
    public ChoiceMenu menu;
    [Tooltip("The party's bag. Leave empty to find it automatically.")]
    public PartyInventory inventory;

    [Header("Colors")]
    public Color partyColor = Palette.Bone;
    public Color enemyColor = Palette.Blood;
    public Color currentColor = Palette.CandleGold;
    public Color downColor = new Color32(74, 68, 60, 255);        // ash
    public Color unseenColor = new Color32(90, 82, 72, 255);      // something in the dark

    [Header("Unseen Enemies (in the dark)")]
    public string unseenName = "???";
    public string unseenTurnTitle = "Something stirs in the dark";
    [Range(0f, 1f)] public float disabledLabelAlpha = 0.35f;

    readonly List<TextMeshProUGUI> rows = new List<TextMeshProUGUI>();
    CombatTargeting targeting;
    bool bonusMenu;   // the open menu is the BONUS ACTION one
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
        if (icons != null)
        {
            rowTemplate.spriteAsset = icons;
            if (turnInfo != null) turnInfo.spriteAsset = icons;
        }
        rowTemplate.gameObject.SetActive(false);

        if (inventory == null) inventory = FindAnyObjectByType<PartyInventory>();
        targeting = turns.GetComponent<CombatTargeting>();
        if (menu != null) menu.Close();
        if (cancelAimButton != null) cancelAimButton.onClick.AddListener(CancelAim);

        moveButton.onClick.AddListener(turns.ChooseMove);
        actionButton.onClick.AddListener(OpenActions);
        if (itemButton != null) itemButton.onClick.AddListener(OpenBonus);
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

        // Close the menu when it's no longer our turn, or what it spends is used up
        bool spent = bonusMenu ? turns.HasUsedBonus : turns.HasActed;
        if (menu != null && menu.IsOpen && (!show || !turns.IsPartyTurn || spent)) menu.Close();
        if (!show) return;

        if (roundText != null) roundText.text = $"Round {turns.Round}";

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
            row.text = seen ? $"{marker}{c.initiative}   {c.unit.unitName}   {Hp(c.unit)}"
                            : $"{marker}?    {unseenName}";   // you know something is there, nothing more
            row.fontStyle = rowStyle;
            if (isCurrent) row.fontStyle |= FontStyles.Bold;

            if (!c.unit.IsAlive)
            {
                row.color = downColor;
                if (c.unit.IsDead) row.fontStyle |= FontStyles.Strikethrough;   // downed can still get up
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
                turnInfo.text = RoundPrefix + $"{Hp(u)}     {Ac(u)}"
                              + (u.IsBlessed ? "     Blessed" + (u.BlessDie > 0 ? $" +1d{u.BlessDie}" : "") + (u.BlessBonus > 0 ? $" +{u.BlessBonus}" : "") : "") + u.StatusText;
        }
        else
        {
            turnTitle.text = unseenTurnTitle;
            turnTitle.color = enemyColor;
            if (turnInfo != null) turnInfo.text = RoundPrefix.Trim();
        }

        // Buttons only work on your own turn, and only once each
        bool mine = turns.IsPartyTurn;
        SetButton(moveButton, mine && !turns.HasMoved);
        SetButton(actionButton, mine && !turns.HasActed);
        if (itemButton != null) SetButton(itemButton, mine && !turns.HasUsedBonus);
        SetButton(endTurnButton, mine);
    }

    bool AnyAllyDowned() => turns.Order.Exists(c => c.unit != null && c.unit.team == Team.Party && c.unit.IsDowned);

    // HP and AC, with the icons when there are some. Downed party members show their death saves instead.
    string Hp(CombatUnit u) => u.IsDowned || (u.IsDead && u.team == Team.Party) ? u.DeathSaveText
                             : (icons != null ? $"<sprite name=\"{hpIcon}\">" : "HP ") + $"{u.CurrentHP}/{u.maxHP}";
    string Ac(CombatUnit u) => (icons != null ? $"<sprite name=\"{acIcon}\">" : "AC ") + u.ArmorClass;

    // The round is shown above the initiative list; only falls back into the info text without a Round Text
    string RoundPrefix => roundText != null ? "" : $"Round {turns.Round}     ";

    // ---------- Menus ----------

    void OpenActions()
    {
        if (menu == null || !turns.IsPartyTurn || turns.HasActed) return;
        CombatUnit u = turns.Current;
        var options = ActionOptions(u, bonus: false);
        bonusMenu = false;
        menu.Open($"{u.unitName}  —  ACTIONS", options);
    }

    // Bonus moves first, then the bag
    void OpenBonus()
    {
        if (menu == null || !turns.IsPartyTurn || turns.HasUsedBonus) return;
        CombatUnit u = turns.Current;
        var options = ActionOptions(u, bonus: true);
        if (inventory != null)
            foreach (PartyInventory.ItemStack s in inventory.items)
            {
                if (s.item == null || s.count <= 0 || s.item.effect == ItemEffect.Book) continue;   // no reading in a fight
                ItemData picked = s.item;
                options.Add(new ChoiceMenu.Option
                {
                    label = s.item.displayName,
                    detail = s.DetailText,
                    description = s.item.description,
                    enabled = true,
                    onPick = () => { menu.Close(); turns.ChooseItem(picked); }
                });
            }
        bonusMenu = true;
        menu.Open($"{u.unitName}  —  BONUS ACTIONS", options);
    }

    List<ChoiceMenu.Option> ActionOptions(CombatUnit u, bool bonus)
    {
        var options = new List<ChoiceMenu.Option>();
        // Help always at the top, the rest in the order they're listed on the unit
        var list = new List<CombatAction>(u.actions.FindAll(a => a != null && a.effect == ActionEffect.Help));
        list.AddRange(u.actions.FindAll(a => a != null && a.effect != ActionEffect.Help));
        foreach (CombatAction a in list)
        {
            if (a == null || a.bonusAction != bonus) continue;
            int left = turns.UsesLeft(u, a);
            string uses = left < 0 ? "" : $"   {left}/{a.usesPerBattle}";
            string range = a.range > 1 ? $"{a.range} tiles" : a.effect == ActionEffect.Damage ? "melee" : "touch";
            CombatAction picked = a;
            options.Add(new ChoiceMenu.Option
            {
                icon = a.typeIcon,
                label = a.displayName,
                detail = $"{a.AmountText}   {range}{uses}",
                description = a.FullDescription,
                enabled = left != 0 && (a.effect != ActionEffect.Help || AnyAllyDowned()),
                onPick = () => { menu.Close(); turns.ChooseAction(picked); }
            });
        }
        return options;
    }

    void CancelAim()
    {
        if (targeting != null && targeting.IsAiming) targeting.Cancel();
    }

    void SetButton(Button b, bool on) => MenuRows.SetButton(b, on, disabledLabelAlpha);

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
