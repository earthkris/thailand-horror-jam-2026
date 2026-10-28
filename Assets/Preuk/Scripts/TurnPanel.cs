using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
    public Button attackButton;
    [Tooltip("Optional. Leave empty if you don't have a Use Item button yet.")]
    public Button itemButton;
    public Button endTurnButton;

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
    FontStyles rowStyle;
    bool ready;

    // Runs when you add the component: find the TurnManager for you
    void Reset()
    {
        turns = FindAnyObjectByType<TurnManager>();
    }

    void Awake()
    {
        ready = CheckReferences();
        if (!ready) return;

        rowStyle = rowTemplate.fontStyle;
        rowTemplate.gameObject.SetActive(false);

        moveButton.onClick.AddListener(turns.ChooseMove);
        attackButton.onClick.AddListener(turns.ChooseAttack);
        if (itemButton != null) itemButton.onClick.AddListener(turns.ChooseItem);
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
        initiativePanel.SetActive(show);
        actionPanel.SetActive(show);
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
                turnInfo.text = $"Round {turns.Round}     HP {u.CurrentHP}/{u.maxHP}     AC {u.armorClass}     Move {turns.MovesLeft}/{u.MoveTiles}";
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
        SetButton(attackButton, mine && !turns.HasActed);
        if (itemButton != null) SetButton(itemButton, mine && !turns.HasActed);   // items use your action too
        SetButton(endTurnButton, mine);
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
        if (attackButton == null) missing.Add("Attack Button");
        if (endTurnButton == null) missing.Add("End Turn Button");

        if (missing.Count == 0) return true;
        Debug.LogError($"TurnPanel on '{name}' is missing: {string.Join(", ", missing)}", this);
        return false;
    }
}