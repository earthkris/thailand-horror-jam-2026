using UnityEngine;

// A spell book lying on the floor. Stand next to it and press E (or click it) and one of the
// party learns a new move, e.g. the yellow book teaches Lucius 'Bless'.
// The move shows up in that character's ACTION menu from the next fight on.
// Put the book on a tile the player can reach.
public class SpellBook : Interactable
{
    [Tooltip("The move it teaches (a Combat Action asset).")]
    public CombatAction teaches;
    [Tooltip("Who learns it: the character's object name (Kairos, Lucius) or their Unit Name.")]
    public string learner = "Lucius";

    [Header("Look")]
    [Tooltip("The book sprite. It floats gently up and down (never below where you put it).")]
    public Transform visual;
    public float bobHeight = 0.08f;
    public float bobSpeed = 2f;

    Vector3 home;

    // '[E]  Pick up Bless'
    protected override string Prompt => teaches != null ? promptText + " " + teaches.displayName : promptText;

    protected override void Start()
    {
        base.Start();
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual != null) home = visual.localPosition;
    }

    protected override void Update()
    {
        if (visual != null)
        {
            float up = (Mathf.Sin(Time.time * bobSpeed) * 0.5f + 0.5f) * bobHeight;   // 0..bobHeight
            visual.localPosition = home + Vector3.up * up;
        }
        base.Update();   // E key, clicks, the prompt
    }

    protected override void Interact()
    {
        CombatUnit who = FindLearner();
        if (who == null || teaches == null)
        {
            Debug.LogWarning($"SpellBook '{name}': nobody called '{learner}' in the party, or no move set.", this);
            enabled = false;
            return;
        }

        if (!who.actions.Contains(teaches)) who.actions.Add(teaches);
        Debug.Log($"{who.unitName} learned {teaches.displayName}!");
        GameToast.Show($"{who.unitName} learned {teaches.displayName}", teaches.description);
        Destroy(gameObject);
    }

    CombatUnit FindLearner()
    {
        foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (u.team == Team.Party && (u.gameObject.name == learner || u.unitName == learner))
                return u;
        return null;
    }
}
